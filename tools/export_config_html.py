#!/usr/bin/env python3

from __future__ import annotations

import argparse
import hashlib
import html
import json
import re
import shutil
import subprocess
import sys
import tempfile
import textwrap
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


HELPER_SCRIPT = textwrap.dedent(
	"""
	extends SceneTree

	const SKIP_PROPERTY_NAMES := {
		&"resource_local_to_scene": true,
		&"resource_name": true,
		&"resource_path": true,
		&"script": true,
	}

	var _entries: Array[Dictionary] = []
	var _entry_by_instance_id: Dictionary = {}
	var _scan_errors: Array[String] = []
	var _root_resource_paths: Array[String] = []


	func _initialize() -> void:
		var options = _parse_args(OS.get_cmdline_user_args())
		var config_dir = String(options.get("config-dir", "res://data/configs"))
		var output_json = String(options.get("output-json", ""))
		if output_json.is_empty():
			push_error("配置导出缺少 --output-json 参数。")
			quit(2)
			return

		var report = _build_report(config_dir)
		var output_file = FileAccess.open(output_json, FileAccess.WRITE)
		if output_file == null:
			push_error(
				"无法写入 %s。error=%d" % [
					output_json,
					FileAccess.get_open_error(),
				]
			)
			quit(3)
			return

		output_file.store_string(JSON.stringify(report, "\\t"))
		output_file.close()
		print("已导出配置快照到 %s" % output_json)
		quit(0)


	func _parse_args(raw_args: PackedStringArray) -> Dictionary:
		var options = {}
		var index = 0
		while index < raw_args.size():
			var token = String(raw_args[index])
			if not token.begins_with("--"):
				index += 1
				continue

			var key = token.trim_prefix("--")
			var value: Variant = true
			if index + 1 < raw_args.size():
				var next_token = String(raw_args[index + 1])
				if not next_token.begins_with("--"):
					value = next_token
					index += 1
			options[key] = value
			index += 1
		return options


	func _build_report(config_dir: String) -> Dictionary:
		_entries.clear()
		_entry_by_instance_id.clear()
		_scan_errors.clear()
		_root_resource_paths.clear()
		_scan_directory(config_dir)

		var root_paths = _root_resource_paths.duplicate()
		root_paths.sort()

		return {
			"generated_unix_time": int(Time.get_unix_time_from_system()),
			"config_dir": config_dir,
			"root_files": root_paths,
			"entry_count": _entries.size(),
			"entries": _entries,
			"scan_errors": _scan_errors,
		}


	func _scan_directory(directory_path: String) -> void:
		if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(directory_path)):
			_scan_errors.append("配置目录不存在：%s" % directory_path)
			return

		var directory = DirAccess.open(directory_path)
		if directory == null:
			_scan_errors.append("无法打开配置目录：%s" % directory_path)
			return

		var child_names: Array[String] = []
		directory.list_dir_begin()
		while true:
			var entry_name = directory.get_next()
			if entry_name.is_empty():
				break
			if entry_name == "." or entry_name == "..":
				continue
			child_names.append(entry_name)
		directory.list_dir_end()
		child_names.sort()

		for child_name in child_names:
			var child_path = "%s/%s" % [directory_path, child_name]
			if DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(child_path)):
				_scan_directory(child_path)
				continue
			if not child_name.ends_with(".tres") and not child_name.ends_with(".res"):
				continue
			_scan_resource_file(child_path)


	func _scan_resource_file(resource_path: String) -> void:
		_root_resource_paths.append(resource_path)
		var resource = load(resource_path)
		if resource == null:
			_scan_errors.append("加载配置资源失败：%s" % resource_path)
			return
		if resource is not Resource:
			_scan_errors.append("加载到的配置不是 Resource：%s" % resource_path)
			return
		_register_resource_entry(resource as Resource, resource_path, "root", true)


	func _register_resource_entry(
		resource: Resource,
		root_path: String,
		context_path: String,
		is_top_level: bool
	) -> String:
		var instance_id = resource.get_instance_id()
		if _entry_by_instance_id.has(instance_id):
			return String(_entry_by_instance_id[instance_id].get("entry_id", ""))

		var type_info = _get_type_info(resource)
		var entry_id = "entry_%04d" % (_entries.size() + 1)
		var entry = {
			"entry_id": entry_id,
			"root_path": root_path,
			"context_path": context_path,
			"entry_kind": "top_level" if is_top_level else "embedded",
			"resource_path": String(resource.resource_path),
			"type_key": String(type_info.get("type_key", "")),
			"script_path": String(type_info.get("script_path", "")),
			"built_in_type": String(type_info.get("built_in_type", "")),
			"properties": {},
		}
		var entry_index = _entries.size()
		_entry_by_instance_id[instance_id] = entry
		_entries.append(entry)

		var serialized_properties = {}
		for property_name in _get_storage_property_names(resource):
			var property_context = _append_context(context_path, property_name)
			serialized_properties[property_name] = _serialize_variant(
				resource.get(property_name),
				root_path,
				property_context
			)
		entry["properties"] = serialized_properties
		_entry_by_instance_id[instance_id] = entry
		_entries[entry_index] = entry
		return entry_id


	func _get_storage_property_names(resource: Resource) -> Array[String]:
		var result: Array[String] = []
		for property_variant in resource.get_property_list():
			if property_variant is not Dictionary:
				continue
			var property_data = property_variant as Dictionary
			var property_name = StringName(property_data.get("name", &""))
			if property_name == &"" or SKIP_PROPERTY_NAMES.has(property_name):
				continue
			var usage = int(property_data.get("usage", 0))
			if (usage & PROPERTY_USAGE_STORAGE) == 0:
				continue
			result.append(String(property_name))
		return result


	func _serialize_variant(value: Variant, root_path: String, context_path: String) -> Variant:
		if value == null or value is bool or value is int or value is float or value is String:
			return value

		if value is StringName:
			return {
				"_kind": "string_name",
				"value": String(value),
			}

		if value is NodePath:
			return {
				"_kind": "node_path",
				"value": String(value),
			}

		if value is Vector2i:
			var vector2i_value = value as Vector2i
			return {
				"_kind": "vector2i",
				"x": vector2i_value.x,
				"y": vector2i_value.y,
				"text": "Vector2i(%d, %d)" % [vector2i_value.x, vector2i_value.y],
			}

		if value is Vector2:
			var vector2_value = value as Vector2
			return {
				"_kind": "vector2",
				"x": vector2_value.x,
				"y": vector2_value.y,
				"text": "Vector2(%.3f, %.3f)" % [vector2_value.x, vector2_value.y],
			}

		if value is PackedInt32Array:
			var int_items: Array[int] = []
			for item in value:
				int_items.append(int(item))
			return int_items

		if value is PackedFloat32Array or value is PackedFloat64Array:
			var float_items: Array[float] = []
			for item in value:
				float_items.append(float(item))
			return float_items

		if value is PackedStringArray:
			var string_items: Array[String] = []
			for item in value:
				string_items.append(String(item))
			return string_items

		if value is PackedVector2Array:
			var vector_items: Array = []
			for item in value:
				vector_items.append(
					{
						"_kind": "vector2",
						"x": item.x,
						"y": item.y,
						"text": "Vector2(%.3f, %.3f)" % [item.x, item.y],
					}
				)
			return vector_items

		if value is Array:
			var array_items: Array = []
			for index in range(value.size()):
				array_items.append(
					_serialize_variant(
						value[index],
						root_path,
						"%s[%d]" % [context_path, index]
					)
				)
			return array_items

		if value is Dictionary:
			var serialized_entries: Array[Dictionary] = []
			for key_variant in value.keys():
				serialized_entries.append(
					{
						"key": _serialize_variant(
							key_variant,
							root_path,
							"%s{key}" % context_path
						),
						"value": _serialize_variant(
							value[key_variant],
							root_path,
							"%s[%s]" % [context_path, String(key_variant)],
						),
					}
				)
			return {
				"_kind": "dictionary",
				"entries": serialized_entries,
			}

		if value is Resource:
			var nested_resource = value as Resource
			if nested_resource == null:
				return {
					"_kind": "resource_ref",
					"entry_id": "",
					"target_path": "",
					"type_key": "builtin:Resource",
					"script_path": "",
					"built_in_type": "Resource",
					"label": "Resource",
				}

			if _is_embedded_resource(nested_resource, root_path):
				var entry_id = _register_resource_entry(
					nested_resource,
					root_path,
					context_path,
					false
				)
				return _build_resource_ref(nested_resource, entry_id, String(nested_resource.resource_path))

			return _build_resource_ref(nested_resource, "", String(nested_resource.resource_path))

		return {
			"_kind": "godot_value",
			"type": type_string(typeof(value)),
			"text": var_to_str(value),
		}


	func _is_embedded_resource(resource: Resource, root_path: String) -> bool:
		var resource_path = String(resource.resource_path)
		if resource_path.is_empty():
			return true
		if resource_path == root_path:
			return true
		return resource_path.begins_with("%s::" % root_path)


	func _build_resource_ref(resource: Resource, entry_id: String, target_path: String) -> Dictionary:
		var type_info = _get_type_info(resource)
		return {
			"_kind": "resource_ref",
			"entry_id": entry_id,
			"target_path": target_path,
			"type_key": String(type_info.get("type_key", "")),
			"script_path": String(type_info.get("script_path", "")),
			"built_in_type": String(type_info.get("built_in_type", "")),
			"label": _build_resource_label(resource),
		}


	func _build_resource_label(resource: Resource) -> String:
		for candidate_name in [
			"item_id",
			"skill_id",
			"profession_id",
			"recipe_id",
			"template_id",
			"brain_id",
			"profile_id",
			"settlement_id",
			"facility_id",
			"npc_id",
			"slot_id",
			"state_id",
			"action_id",
			"display_name",
		]:
			if not _resource_has_property(resource, candidate_name):
				continue
			var value = resource.get(candidate_name)
			var label = String(value)
			if not label.strip_edges().is_empty():
				return label

		var resource_path = String(resource.resource_path)
		if not resource_path.is_empty():
			return resource_path.get_file()
		return String(_get_type_info(resource).get("built_in_type", "Resource"))


	func _resource_has_property(resource: Resource, property_name: String) -> bool:
		for property_variant in resource.get_property_list():
			if property_variant is not Dictionary:
				continue
			var property_data = property_variant as Dictionary
			if String(property_data.get("name", "")) == property_name:
				return true
		return false


	func _get_type_info(resource: Resource) -> Dictionary:
		var built_in_type = String(resource.get_class())
		var script_path = ""
		var type_key = ""
		var script = resource.get_script()
		if script != null and script is Script:
			script_path = String(script.resource_path)
			type_key = script_path
		if type_key.is_empty():
			type_key = "builtin:%s" % built_in_type
		return {
			"type_key": type_key,
			"script_path": script_path,
			"built_in_type": built_in_type,
		}


	func _append_context(context_path: String, property_name: String) -> String:
		if context_path.is_empty():
			return property_name
		return "%s.%s" % [context_path, property_name]
	"""
).strip() + "\n"


ID_FIELD_PRIORITY = [
	"item_id",
	"skill_id",
	"profession_id",
	"recipe_id",
	"template_id",
	"brain_id",
	"profile_id",
	"quest_id",
	"settlement_id",
	"facility_id",
	"npc_id",
	"slot_id",
	"state_id",
	"action_id",
	"variant_id",
	"attribute_id",
	"tag",
	"region_tag",
	"source_id",
]

SECONDARY_FIELD_PRIORITY = [
	"display_name",
	"service_type",
	"category",
	"interaction_type",
	"effect_type",
]

DOMAIN_LABELS = {
	"enemies": "敌方配置",
	"items": "物品配置",
	"professions": "职业配置",
	"recipes": "配方配置",
	"skills": "技能配置",
	"world_map": "世界地图配置",
	".": "根目录",
}

TYPE_LABELS = {
	"AttributeModifier": "属性修正",
	"CombatCastVariantDef": "战斗施法变体定义",
	"CombatEffectDef": "战斗效果定义",
	"CombatSkillDef": "战斗技能定义",
	"EnemyAiBrainDef": "敌方 AI 脑定义",
	"EnemyAiStateDef": "敌方 AI 状态定义",
	"EnemyContentSeed": "敌方内容种子",
	"EnemyTemplateDef": "敌方模板定义",
	"FacilityConfig": "设施定义",
	"FacilityNpcConfig": "设施 NPC 定义",
	"FacilitySlotConfig": "设施槽位定义",
	"ItemDef": "物品定义",
	"MountedSubmapConfig": "挂载子地图配置",
	"MoveToRangeAction": "移动到距离行动",
	"ProfessionDef": "职业定义",
	"ProfessionPromotionRequirement": "职业晋升条件",
	"ProfessionRankRequirement": "职业阶位条件",
	"RecipeDef": "配方定义",
	"RetreatAction": "撤退行动",
	"SettlementConfig": "聚落定义",
	"SettlementDistributionRule": "聚落分布规则",
	"SkillDef": "技能定义",
	"TagRequirement": "标签条件",
	"UseChargeAction": "冲锋技能行动",
	"UseGroundSkillAction": "地面技能行动",
	"UseUnitSkillAction": "单位技能行动",
	"WaitAction": "等待行动",
	"WeightedFacilityEntry": "加权设施条目",
	"WildEncounterRosterDef": "野外遭遇编队定义",
	"WildSpawnRule": "野外刷新规则",
	"WorldEventConfig": "世界事件配置",
	"WorldMapGenerationConfig": "世界地图生成配置",
}

FIELD_LABELS = {
	"achievement_requirements": "成就前置",
	"action_id": "行动ID",
	"actions": "行动列表",
	"active_conditions": "激活条件",
	"ai_tags": "AI标签",
	"allowed_base_terrains": "允许基础地形",
	"allowed_slot_tags": "允许槽位标签",
	"allow_repeat_hits_across_steps": "允许跨步重复命中",
	"ap_cost": "行动点消耗",
	"ap_gain": "行动点恢复",
	"area_direction_mode": "范围方向模式",
	"area_origin_mode": "范围起点模式",
	"area_pattern": "范围形状",
	"area_value": "范围数值",
	"assigned_core_must_be_subset_of_qualifiers": "已分配核心必须属于候选集合",
	"attribute_id": "属性ID",
	"attribute_modifiers": "属性修正",
	"attribute_overrides": "属性覆盖",
	"aura_cost": "光环消耗",
	"base_break_chance": "基础破坏概率",
	"base_chain_radius": "基础连锁半径",
	"base_distance": "基础距离",
	"base_hit_rate": "基础命中率",
	"base_price": "基础价格",
	"body": "身体",
	"body_size": "体型",
	"bonus_condition": "额外条件",
	"bonus_terrain_effect_id": "额外地形效果ID",
	"bound_service_npcs": "绑定服务NPC",
	"brain_id": "AI脑ID",
	"buy_price": "购买价格",
	"capital_spacing_cells": "都城间距格",
	"cast_variants": "施法变体",
	"category": "分类",
	"chain_shape": "连锁形状",
	"chunk_coords": "区块坐标",
	"chunk_size": "区块尺寸",
	"city_spacing_cells": "城市间距格",
	"collision_base_damage": "碰撞基础伤害",
	"collision_size_gap_damage": "碰撞体型差伤害",
	"combat_profile": "战斗配置",
	"consume_cost_on_attempt": "尝试时消耗资源",
	"cooldown_tu": "冷却TU",
	"core_skill_transition_mode": "核心技能切换模式",
	"cost_resource": "消耗资源类型",
	"count": "数量",
	"crafting_groups": "制作分组",
	"damage_multiplier_stage": "伤害倍率阶段",
	"damage_ratio_percent": "伤害倍率百分比",
	"default_state_id": "默认状态ID",
	"defense_attribute_id": "防御属性ID",
	"density_per_chunk": "每区块密度",
	"dependency_visibility_mode": "依赖可见性模式",
	"description": "描述",
	"desired_max_distance": "目标最大距离",
	"desired_min_distance": "目标最小距离",
	"discovery_condition_id": "发现条件ID",
	"display_name": "显示名称",
	"distance": "距离",
	"drop_entries": "掉落条目",
	"drop_id": "掉落ID",
	"drop_type": "掉落类型",
	"duration": "持续时间",
	"duration_tu": "持续TU",
	"effect_defs": "效果定义",
	"effect_target_team_filter": "效果目标阵营筛选",
	"effect_type": "效果类型",
	"encounter_profile_id": "遭遇档案ID",
	"enemy_ai_brains": "敌方AI脑列表",
	"enemy_count": "敌人数",
	"enemy_templates": "敌方模板列表",
	"equip_requirement": "装备条件",
	"equipment_slot_ids": "装备槽位ID",
	"equipment_type_id": "装备类型ID",
	"event_id": "事件ID",
	"event_type": "事件类型",
	"facility_id": "设施ID",
	"facility_library": "设施库",
	"facility_slots": "设施槽位",
	"faction_id": "阵营ID",
	"failure_reason": "失败原因",
	"follow_up_cost_multiplier": "追击消耗倍率",
	"follow_up_damage_multiplier": "追击伤害倍率",
	"follow_up_hit_rate_penalty": "追击命中惩罚",
	"footprint_pattern": "覆盖形状",
	"forced_move_distance": "强制位移距离",
	"forced_move_mode": "强制位移模式",
	"free_move_points_gain": "获得免费移动点",
	"generation_config_path": "生成配置路径",
	"grant_scope": "授予范围",
	"granted_skill_id": "授予技能ID",
	"granted_skills": "授予技能",
	"growth_step_interval": "成长步进间隔",
	"guarantee_starting_wild_encounter": "保底起始野外遭遇",
	"guaranteed_facility_ids": "保底设施ID",
	"head": "头部",
	"height_delta": "高差",
	"hit_rate": "命中率",
	"hp_max": "最大生命",
	"icon": "图标",
	"icon_id": "图标ID",
	"initial_stage": "初始阶段",
	"initial_state_id": "初始状态ID",
	"input_item_ids": "输入物品ID",
	"input_item_quantities": "输入物品数量",
	"interaction_script_id": "交互脚本ID",
	"interaction_type": "交互类型",
	"is_initial_profession": "是否初始职业",
	"is_stackable": "是否可堆叠",
	"item_category": "物品分类",
	"item_id": "物品ID",
	"knowledge_requirements": "知识前置",
	"learn_requirements": "学习条件",
	"learn_source": "学习来源",
	"local_coord": "本地坐标",
	"local_slot_id": "本地槽位ID",
	"magic_attack": "魔法攻击",
	"magic_defense": "魔法防御",
	"main_hand": "主手",
	"mastery_curve": "熟练度曲线",
	"mastery_sources": "熟练度来源",
	"max_broken_items": "最大破损物品数",
	"max_level": "最大等级",
	"max_optional_facilities": "最大可选设施数",
	"max_rank": "最大阶位",
	"max_stack": "最大堆叠",
	"max_target_count": "最大目标数",
	"metropolis_spacing_cells": "大都会间距格",
	"min_distance_to_settlement": "距聚落最小距离",
	"min_settlement_tier": "最低聚落等级",
	"min_skill_level": "最低技能等级",
	"min_target_count": "最少目标数",
	"minimum_hit_count": "最低命中数",
	"minimum_safe_distance": "最小安全距离",
	"mode": "模式",
	"monster_name": "怪物名称",
	"monster_template_id": "怪物模板ID",
	"mounted_submaps": "挂载子地图",
	"mp_cost": "法力消耗",
	"mp_max": "最大法力",
	"npc_id": "NPC ID",
	"occupied_slot_ids": "实际占用槽位ID",
	"off_hand": "副手",
	"optional_facility_pool": "可选设施池",
	"origin_filter": "来源筛选",
	"output_item_id": "输出物品ID",
	"output_quantity": "输出数量",
	"params": "参数",
	"physical_attack": "物理攻击",
	"physical_defense": "物理防御",
	"player_start_coord": "玩家起始坐标",
	"player_vision_range": "玩家视野范围",
	"power": "强度",
	"preferred_origin": "首选起点",
	"pressure_distance": "压迫距离",
	"prevent_repeat_target": "防止重复目标",
	"procedural_capital_count": "程序化都城数",
	"procedural_city_count": "程序化城市数",
	"procedural_generation_enabled": "是否启用程序化生成",
	"procedural_metropolis_count": "程序化大都会数",
	"procedural_town_count": "程序化城镇数",
	"procedural_village_count": "程序化村庄数",
	"procedural_world_stronghold_count": "程序化世界要塞数",
	"profession_id": "职业ID",
	"profile_id": "档案ID",
	"prompt_text": "提示文本",
	"prompt_title": "提示标题",
	"quantity": "数量",
	"quest_groups": "任务分组",
	"range_pattern": "距离形状",
	"range_value": "距离数值",
	"rank_requirements": "阶位条件",
	"reactivation_mode": "重新激活模式",
	"recipe_id": "配方ID",
	"region_tag": "区域标签",
	"required": "是否必需",
	"required_attribute_rules": "属性条件",
	"required_coord_count": "需要坐标数",
	"required_facility_tags": "所需设施标签",
	"required_profession_ranks": "职业阶位条件",
	"required_reputation_rules": "声望条件",
	"required_skill_ids": "技能条件ID",
	"required_tag_rules": "标签条件",
	"require_damage_applied": "需要造成伤害",
	"require_target_defeated_by_same_skill": "需要由同技能击败目标",
	"requires_los": "是否需要视线",
	"retain_source_skills_on_unlock": "解锁后保留来源技能",
	"retreat_hp_ratio": "撤退血量比",
	"return_hint_text": "返回提示文本",
	"same_target_only": "仅同一目标",
	"scaling_attribute_id": "缩放属性ID",
	"score_bucket_id": "评分桶ID",
	"seed": "随机种子",
	"selection_order_mode": "选择顺序模式",
	"selection_role": "选择角色",
	"sell_price": "出售价格",
	"sellable": "是否可出售",
	"service_type": "服务类型",
	"settlement_distribution": "聚落分布",
	"settlement_id": "聚落ID",
	"settlement_library": "聚落库",
	"skill_id": "技能ID",
	"skill_ids": "技能ID列表",
	"skill_level_map": "技能等级映射",
	"skill_level_requirements": "技能等级前置",
	"skill_state": "技能状态",
	"skill_type": "技能类型",
	"slot_break_chance_map": "槽位破坏概率映射",
	"slot_id": "槽位ID",
	"slot_tag": "槽位标签",
	"slot_weight_map": "槽位权重映射",
	"source_id": "来源ID",
	"source_type": "来源类型",
	"speed": "速度",
	"stack_behavior": "叠加行为",
	"stack_limit": "叠加上限",
	"stack_on_multiple_kills": "多重击杀时叠加",
	"stage": "阶段",
	"stages": "阶段列表",
	"stamina_cost": "体力消耗",
	"stamina_max": "最大体力",
	"starting_wild_spawn_max_distance": "起始野怪最大距离",
	"starting_wild_spawn_min_distance": "起始野怪最小距离",
	"state_id": "状态ID",
	"states": "状态列表",
	"status_id": "状态效果ID",
	"step_radius": "步进半径",
	"step_shape": "步进形状",
	"stop_on_insufficient_resource": "资源不足时停止",
	"stop_on_miss": "未命中时停止",
	"stop_on_target_down": "目标倒下时停止",
	"submap_id": "子地图ID",
	"support_hp_ratio": "支援血量比",
	"suppression_steps_on_victory": "胜利压制步数",
	"tag": "标签",
	"tags": "标签列表",
	"target_mode": "目标模式",
	"target_rank": "目标阶位",
	"target_selection_mode": "目标选择模式",
	"target_selector": "目标选择器",
	"target_submap_id": "目标子地图ID",
	"target_team_filter": "目标阵营筛选",
	"template_id": "模板ID",
	"terrain_effect_id": "地形效果ID",
	"terrain_replace_to": "替换地形为",
	"tick_effect_type": "周期效果类型",
	"tick_interval_tu": "周期间隔TU",
	"tier": "等级",
	"town_spacing_cells": "城镇间距格",
	"trigger_event": "触发事件",
	"unit_entries": "单位条目",
	"unlock_knowledge_id": "解锁知识ID",
	"unlock_mode": "解锁模式",
	"unlock_requirement": "解锁条件",
	"upgrade_source_skill_ids": "升级来源技能ID",
	"value": "数值",
	"value_per_rank": "每阶数值",
	"variant_id": "变体ID",
	"village_spacing_cells": "村庄间距格",
	"vision_range": "视野范围",
	"warrior_aura_slash": "斗气斩",
	"warrior_combo_strike": "连击",
	"weight": "权重",
	"wet_chain_radius": "潮湿连锁半径",
	"wild_encounter_rosters": "野外遭遇编队",
	"wild_monster_distribution": "野外怪物分布",
	"world_coord": "世界坐标",
	"world_events": "世界事件",
	"world_size_in_chunks": "世界区块尺寸",
	"world_stronghold_spacing_cells": "世界要塞间距格",
	"hands": "手部",
	"feet": "脚部",
	"cloak": "披风",
	"necklace": "项链",
	"ring_1": "戒指一",
	"ring_2": "戒指二",
	"special_trinket": "特殊饰品",
	"badge": "徽章",
}


def parse_args() -> argparse.Namespace:
	parser = argparse.ArgumentParser(
		description="把 data/configs 下的 Godot 配置资源按资源类型汇总导出为 HTML。"
	)
	parser.add_argument(
		"--repo-root",
		default="",
		help="仓库根目录。默认取当前脚本目录的上一级。",
	)
	parser.add_argument(
		"--config-dir",
		default="data/configs",
		help="相对仓库根目录的配置目录。",
	)
	parser.add_argument(
		"--output-dir",
		default=".tmp/config_html",
		help="相对仓库根目录的输出目录。",
	)
	parser.add_argument(
		"--godot-bin",
		default="godot",
		help="Godot 可执行文件名或路径。",
	)
	return parser.parse_args()


def resolve_command_path(command: str) -> str:
	command_path = Path(command).expanduser()
	if command_path.exists():
		return str(command_path.resolve())
	resolved = shutil.which(command)
	if resolved:
		return resolved
	raise RuntimeError(f"Required command is not available: {command}")


def resolve_repo_root(script_path: Path, explicit_repo_root: str) -> Path:
	if explicit_repo_root:
		candidate = Path(explicit_repo_root).expanduser().resolve()
	else:
		candidate = script_path.parent.parent.resolve()
	if not candidate.exists():
		raise RuntimeError(f"Repository root does not exist: {candidate}")
	return candidate


def to_res_path(path: Path, repo_root: Path) -> str:
	return "res://" + path.relative_to(repo_root).as_posix()


def run_godot_snapshot(
	repo_root: Path,
	config_dir: Path,
	output_json_path: Path,
	godot_bin: str,
) -> dict[str, Any]:
	with tempfile.TemporaryDirectory(prefix="config_html_") as temp_dir_name:
		helper_path = Path(temp_dir_name) / "config_snapshot_dump.gd"
		helper_path.write_text(HELPER_SCRIPT, encoding="utf-8")

		command = [
			godot_bin,
			"--headless",
			"--path",
			str(repo_root),
			"--script",
			str(helper_path),
			"--",
			"--config-dir",
			to_res_path(config_dir, repo_root),
			"--output-json",
			str(output_json_path),
		]
		result = subprocess.run(
			command,
			capture_output=True,
			text=True,
			encoding="utf-8",
		)
		if result.returncode != 0:
			stderr = result.stderr.strip()
			stdout = result.stdout.strip()
			details = stderr or stdout or "no error output"
			raise RuntimeError(f"Godot config export failed: {details}")

	if not output_json_path.exists():
		raise RuntimeError(f"Godot export did not produce snapshot JSON: {output_json_path}")
	return json.loads(output_json_path.read_text(encoding="utf-8"))


def load_class_name_map(repo_root: Path) -> dict[str, str]:
	class_name_map: dict[str, str] = {}
	class_name_pattern = re.compile(r"^\s*class_name\s+([A-Za-z_][A-Za-z0-9_]*)", re.MULTILINE)
	for script_path in repo_root.rglob("*.gd"):
		try:
			content = script_path.read_text(encoding="utf-8")
		except UnicodeDecodeError:
			content = script_path.read_text(encoding="utf-8", errors="ignore")
		match = class_name_pattern.search(content)
		if not match:
			continue
		class_name_map[to_res_path(script_path, repo_root)] = match.group(1)
	return class_name_map


def slugify(text: str) -> str:
	slug = re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")
	return slug or "type"


def clean_output_directory(output_dir: Path) -> None:
	output_dir.mkdir(parents=True, exist_ok=True)


def resolve_type_name(entry: dict[str, Any], class_name_map: dict[str, str]) -> str:
	type_key = str(entry.get("type_key", "") or "")
	script_path = str(entry.get("script_path", "") or "")
	if script_path and script_path in class_name_map:
		return class_name_map[script_path]
	if type_key in class_name_map:
		return class_name_map[type_key]
	if script_path:
		return snake_to_pascal(Path(script_path).stem)
	if type_key.startswith("builtin:"):
		return type_key.split(":", 1)[1]
	return str(entry.get("built_in_type", "Resource") or "Resource")


def snake_to_pascal(value: str) -> str:
	return "".join(part.capitalize() for part in re.split(r"[_\-\s]+", value) if part)


def get_domain_display_name(domain: str) -> str:
	return DOMAIN_LABELS.get(domain, domain)


def get_type_display_name(type_name: str) -> str:
	return TYPE_LABELS.get(type_name, type_name)


def get_field_display_name(field_name: str) -> str:
	return FIELD_LABELS.get(field_name, field_name)


def render_field_name(field_name: str) -> str:
	display_name = get_field_display_name(field_name)
	return html.escape(display_name)


def render_dictionary_key_name(value: Any) -> str:
	if isinstance(value, str):
		return render_field_name(value)
	if isinstance(value, dict):
		kind = value.get("_kind")
		if kind in {"string_name", "node_path"}:
			raw_value = str(value.get("value", ""))
			return render_field_name(raw_value)
	return html.escape(str(value))


def build_field_summary_text(fields: list[str]) -> str:
	return "、".join(get_field_display_name(field_name) for field_name in fields)


def extract_scalar_text(value: Any) -> str:
	if value is None:
		return ""
	if isinstance(value, bool):
		return "true" if value else "false"
	if isinstance(value, (int, float)):
		return str(value)
	if isinstance(value, str):
		return value.strip()
	if isinstance(value, list):
		return ""
	if isinstance(value, dict):
		kind = value.get("_kind")
		if kind in {"string_name", "node_path"}:
			return str(value.get("value", "")).strip()
		if kind in {"vector2i", "vector2", "godot_value"}:
			return str(value.get("text", "")).strip()
		if kind == "resource_ref":
			return str(value.get("label", "")).strip()
	return ""


def build_entry_label(entry: dict[str, Any]) -> str:
	properties = entry.get("properties", {})
	if not isinstance(properties, dict):
		return str(entry.get("context_path", "entry"))

	primary = ""
	for field_name in ID_FIELD_PRIORITY:
		primary = extract_scalar_text(properties.get(field_name))
		if primary:
			break

	display_name = extract_scalar_text(properties.get("display_name"))
	if primary and display_name and display_name != primary:
		return f"{primary} | {display_name}"
	if primary:
		return primary
	if display_name:
		return display_name

	for field_name in SECONDARY_FIELD_PRIORITY:
		secondary = extract_scalar_text(properties.get(field_name))
		if secondary:
			return secondary

	context_path = str(entry.get("context_path", "") or "")
	if context_path and context_path != "root":
		return context_path

	root_path = str(entry.get("root_path", "") or "")
	if root_path:
		return Path(root_path).name
	return "entry"


def summarize_fields(entries: list[dict[str, Any]]) -> list[str]:
	field_counter: Counter[str] = Counter()
	for entry in entries:
		properties = entry.get("properties", {})
		if not isinstance(properties, dict):
			continue
		for field_name in properties:
			field_counter[str(field_name)] += 1
	return [field_name for field_name, _ in field_counter.most_common()]


def build_type_filename_map(
	entries_by_type: dict[str, list[dict[str, Any]]],
	class_name_map: dict[str, str],
) -> dict[str, str]:
	type_filename_map: dict[str, str] = {}
	used_filenames: set[str] = set()
	for type_key, type_entries in sorted(entries_by_type.items()):
		type_name = resolve_type_name(type_entries[0], class_name_map)
		base_name = slugify(type_name)
		file_name = f"{base_name}.html"
		if file_name in used_filenames:
			type_hash = hashlib.sha1(type_key.encode("utf-8")).hexdigest()[:8]
			file_name = f"{base_name}-{type_hash}.html"
		used_filenames.add(file_name)
		type_filename_map[type_key] = file_name
	return type_filename_map


def build_root_entry_lookup(entries: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
	root_entries: dict[str, dict[str, Any]] = {}
	for entry in entries:
		if str(entry.get("context_path", "")) != "root":
			continue
		root_path = str(entry.get("root_path", "") or "")
		if root_path:
			root_entries[root_path] = entry
	return root_entries


def build_entry_lookup(entries: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
	result: dict[str, dict[str, Any]] = {}
	for entry in entries:
		entry_id = str(entry.get("entry_id", "") or "")
		if entry_id:
			result[entry_id] = entry
	return result


def render_value(
	value: Any,
	entry_lookup: dict[str, dict[str, Any]],
	root_entry_lookup: dict[str, dict[str, Any]],
	type_filename_map: dict[str, str],
	class_name_map: dict[str, str],
	current_page_type_key: str,
) -> str:
	if value is None:
		return '<span class="token token-null">空</span>'
	if isinstance(value, bool):
		return f'<span class="token token-bool">{"true" if value else "false"}</span>'
	if isinstance(value, (int, float)):
		return f'<span class="token token-number">{html.escape(str(value))}</span>'
	if isinstance(value, str):
		if "\n" in value:
			return f'<pre class="value-block">{html.escape(value)}</pre>'
		return f'<span class="token token-string">{html.escape(value)}</span>'
	if isinstance(value, list):
		if not value:
			return '<span class="token token-empty">[]</span>'
		items = "".join(
			"<li>%s</li>"
			% render_value(
				item,
				entry_lookup,
				root_entry_lookup,
				type_filename_map,
				class_name_map,
				current_page_type_key,
			)
			for item in value
		)
		return f'<ul class="value-list">{items}</ul>'
	if isinstance(value, dict):
		kind = str(value.get("_kind", "") or "")
		if kind in {"string_name", "node_path"}:
			return f'<code>{html.escape(str(value.get("value", "")))}</code>'
		if kind in {"vector2i", "vector2", "godot_value"}:
			return f'<code>{html.escape(str(value.get("text", "")))}</code>'
		if kind == "resource_ref":
			return render_resource_ref(
				value,
				entry_lookup,
				root_entry_lookup,
				type_filename_map,
				class_name_map,
				current_page_type_key,
			)
		if kind == "dictionary":
			entries = value.get("entries", [])
			if not entries:
				return '<span class="token token-empty">{}</span>'
			rows: list[str] = []
			for entry_pair in entries:
				if not isinstance(entry_pair, dict):
					continue
				key_html = render_dictionary_key_name(entry_pair.get("key"))
				value_html = render_value(
					entry_pair.get("value"),
					entry_lookup,
					root_entry_lookup,
					type_filename_map,
					class_name_map,
					current_page_type_key,
				)
				rows.append(
					"<tr><th>%s</th><td>%s</td></tr>" % (key_html, value_html)
				)
			return '<table class="dictionary-table">%s</table>' % "".join(rows)
		rows = []
		for key, nested_value in value.items():
			rows.append(
				"<tr><th>%s</th><td>%s</td></tr>"
				% (
					render_dictionary_key_name(key),
					render_value(
						nested_value,
						entry_lookup,
						root_entry_lookup,
						type_filename_map,
						class_name_map,
						current_page_type_key,
					),
				)
			)
		return '<table class="dictionary-table">%s</table>' % "".join(rows)
	return f'<code>{html.escape(str(value))}</code>'


def render_resource_ref(
	value: dict[str, Any],
	entry_lookup: dict[str, dict[str, Any]],
	root_entry_lookup: dict[str, dict[str, Any]],
	type_filename_map: dict[str, str],
	class_name_map: dict[str, str],
	current_page_type_key: str,
) -> str:
	label = str(value.get("label", "") or "资源")
	type_key = str(value.get("type_key", "") or "")
	script_path = str(value.get("script_path", "") or "")
	type_name = class_name_map.get(script_path) or class_name_map.get(type_key) or (
		type_key.split(":", 1)[1] if type_key.startswith("builtin:") else snake_to_pascal(Path(type_key).stem)
	)
	type_display_name = get_type_display_name(type_name)

	target_entry = None
	entry_id = str(value.get("entry_id", "") or "")
	if entry_id:
		target_entry = entry_lookup.get(entry_id)
	if target_entry is None:
		target_path = str(value.get("target_path", "") or "")
		if target_path:
			target_entry = root_entry_lookup.get(target_path)

	if target_entry is None:
		target_text = str(value.get("target_path", "") or "").strip()
		detail_parts = [type_display_name, label]
		if target_text:
			detail_parts.append(target_text)
		return '<span class="resource-ref">%s</span>' % html.escape(" | ".join(part for part in detail_parts if part))

	target_type_key = str(target_entry.get("type_key", "") or "")
	target_file = type_filename_map.get(target_type_key, "index.html")
	target_anchor = f'#{str(target_entry.get("entry_id", "") or "")}'
	if target_type_key == current_page_type_key:
		href = target_anchor
	else:
		href = f"{target_file}{target_anchor}"
	return '<a class="resource-link" href="%s">%s</a>' % (
		html.escape(href),
		html.escape(f"{type_display_name}: {build_entry_label(target_entry)}"),
	)


def build_html_shell(title: str, body: str) -> str:
	css = """
	body {
		margin: 0;
		font-family: "Segoe UI", "Noto Sans SC", sans-serif;
		background: #f4f0e6;
		color: #1f1d19;
	}
	a {
		color: #0d4f6f;
		text-decoration: none;
	}
	a:hover {
		text-decoration: underline;
	}
	code,
	pre,
	.token {
		font-family: "Consolas", "Cascadia Code", monospace;
	}
	header {
		padding: 24px 32px;
		background: linear-gradient(135deg, #f1d6a8, #d9e6d3);
		border-bottom: 1px solid #ccbfa8;
	}
	main {
		max-width: 1480px;
		margin: 0 auto;
		padding: 24px 32px 48px;
	}
	h1,
	h2,
	h3 {
		margin-top: 0;
	}
	.summary-grid {
		display: grid;
		grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
		gap: 12px;
		margin: 20px 0 28px;
	}
	.summary-card,
	.entry-card,
	.error-card,
	.type-card {
		background: #fffdf8;
		border: 1px solid #d9cfbb;
		border-radius: 14px;
		box-shadow: 0 10px 30px rgba(84, 71, 45, 0.08);
	}
	.summary-card,
	.error-card,
	.type-card {
		padding: 16px 18px;
	}
	.type-table,
	.property-table,
	.dictionary-table {
		width: 100%;
		border-collapse: collapse;
		background: #fffdf8;
	}
	.type-table th,
	.type-table td,
	.property-table th,
	.property-table td,
	.dictionary-table th,
	.dictionary-table td {
		border: 1px solid #e5dbc8;
		padding: 10px 12px;
		vertical-align: top;
		text-align: left;
	}
	.type-table th,
	.property-table th,
	.dictionary-table th {
		background: #f5efe3;
		font-weight: 600;
	}
	.type-table td,
	.property-table td,
	.dictionary-table td {
		background: #fffdf8;
	}
	.fields {
		color: #6b6458;
		font-size: 0.92rem;
	}
	.entry-list {
		columns: 2;
		column-gap: 24px;
		margin: 16px 0 24px;
		padding-left: 18px;
	}
	.entry-list li {
		break-inside: avoid;
		margin-bottom: 6px;
	}
	.entry-card {
		padding: 18px 20px;
		margin-bottom: 18px;
	}
	.entry-meta {
		display: flex;
		flex-wrap: wrap;
		gap: 8px 16px;
		margin-bottom: 14px;
		color: #5f584d;
		font-size: 0.95rem;
	}
	.value-list {
		margin: 0;
		padding-left: 20px;
	}
	.value-block {
		margin: 0;
		padding: 10px 12px;
		background: #f6f2e8;
		border-radius: 10px;
		white-space: pre-wrap;
	}
	.token-empty,
	.token-null {
		color: #8b8475;
	}
	.resource-link {
		font-weight: 600;
	}
	.back-link {
		display: inline-block;
		margin-bottom: 16px;
		font-weight: 600;
	}
	@media (max-width: 980px) {
		main {
			padding: 18px 16px 36px;
		}
		header {
			padding: 18px 16px;
		}
		.entry-list {
			columns: 1;
		}
	}
	"""
	return """<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
<style>{css}</style>
</head>
<body>
{body}
</body>
</html>
""".format(title=html.escape(title), css=css, body=body)


def build_index_html(
	snapshot: dict[str, Any],
	entries_by_type: dict[str, list[dict[str, Any]]],
	type_filename_map: dict[str, str],
	class_name_map: dict[str, str],
	config_dir: str,
) -> str:
	entries = list(snapshot.get("entries", []))
	root_files = list(snapshot.get("root_files", []))
	domain_counter: Counter[str] = Counter()
	for root_path in root_files:
		relative_path = root_path.removeprefix(config_dir).strip("/")
		domain = relative_path.split("/", 1)[0] if relative_path else "."
		domain_counter[domain] += 1

	type_rows: list[str] = []
	for type_key, type_entries in sorted(
		entries_by_type.items(),
		key=lambda item: (resolve_type_name(item[1][0], class_name_map).lower(), item[0]),
	):
		type_name = resolve_type_name(type_entries[0], class_name_map)
		type_display_name = get_type_display_name(type_name)
		fields = summarize_fields(type_entries)
		root_count = len({str(entry.get("root_path", "") or "") for entry in type_entries})
		type_rows.append(
			"<tr>"
			"<td><a href=\"%s\">%s</a></td>"
			"<td>%d</td>"
			"<td>%d</td>"
			"<td class=\"fields\">%s</td>"
			"</tr>"
			% (
				html.escape(type_filename_map[type_key]),
				html.escape(type_display_name),
				len(type_entries),
				root_count,
				html.escape(build_field_summary_text(fields)),
			)
		)

	domain_cards = "".join(
		"<div class=\"summary-card\"><strong>%s</strong><div>%d 个根配置文件</div></div>"
		% (html.escape(get_domain_display_name(domain)), count)
		for domain, count in sorted(domain_counter.items())
	)

	error_block = ""
	scan_errors = snapshot.get("scan_errors", [])
	if scan_errors:
		error_items = "".join("<li>%s</li>" % html.escape(str(item)) for item in scan_errors)
		error_block = """
<section class="error-card">
<h2>扫描错误</h2>
<ul>%s</ul>
</section>
""" % error_items

	body = """
<header>
<h1>配置总览</h1>
<div>生成时间：%s</div>
</header>
<main>
<section class="summary-grid">
<div class="summary-card"><strong>根配置文件</strong><div>%d</div></div>
<div class="summary-card"><strong>资源实例</strong><div>%d</div></div>
<div class="summary-card"><strong>类型页面</strong><div>%d</div></div>
<div class="summary-card"><strong>配置目录</strong><div>%s</div></div>
</section>
<section>
<h2>目录分组</h2>
<div class="summary-grid">%s</div>
</section>
<section>
<h2>类型分组</h2>
<table class="type-table">
<thead>
<tr><th>类型</th><th>条目数</th><th>根配置文件</th><th>字段</th></tr>
</thead>
<tbody>%s</tbody>
</table>
</section>
%s
</main>
""" % (
		html.escape(datetime.now(timezone.utc).astimezone().isoformat(timespec="seconds")),
		len(root_files),
		len(entries),
		len(entries_by_type),
		html.escape(config_dir),
		domain_cards,
		"".join(type_rows),
		error_block,
	)
	return build_html_shell("配置总览", body)


def build_type_page_html(
	type_key: str,
	type_entries: list[dict[str, Any]],
	entry_lookup: dict[str, dict[str, Any]],
	root_entry_lookup: dict[str, dict[str, Any]],
	type_filename_map: dict[str, str],
	class_name_map: dict[str, str],
) -> str:
	type_name = resolve_type_name(type_entries[0], class_name_map)
	type_display_name = get_type_display_name(type_name)
	script_path = str(type_entries[0].get("script_path", "") or "")
	fields = summarize_fields(type_entries)
	sorted_entries = sorted(
		type_entries,
		key=lambda entry: (
			build_entry_label(entry).lower(),
			str(entry.get("root_path", "") or ""),
			str(entry.get("context_path", "") or ""),
		),
	)
	entry_nav = "".join(
		"<li><a href=\"#%s\">%s</a></li>"
		% (
			html.escape(str(entry.get("entry_id", "") or "")),
			html.escape(build_entry_label(entry)),
		)
		for entry in sorted_entries
	)

	entry_cards: list[str] = []
	for entry in sorted_entries:
		properties = entry.get("properties", {})
		if not isinstance(properties, dict):
			properties = {}
		property_rows = "".join(
			"<tr><th>%s</th><td>%s</td></tr>"
			% (
				render_field_name(str(field_name)),
				render_value(
					properties[field_name],
					entry_lookup,
					root_entry_lookup,
					type_filename_map,
					class_name_map,
					type_key,
				),
			)
			for field_name in properties
		)
		root_path = str(entry.get("root_path", "") or "")
		context_path = str(entry.get("context_path", "") or "")
		resource_path = str(entry.get("resource_path", "") or "")
		entry_cards.append(
			"""
<article class="entry-card" id="{anchor}">
<h3>{title}</h3>
<div class="entry-meta">
<span><strong>根配置</strong>: {root_path}</span>
<span><strong>位置</strong>: {context_path}</span>
<span><strong>资源路径</strong>: {resource_path}</span>
</div>
<table class="property-table">
<tbody>{property_rows}</tbody>
</table>
</article>
""".format(
				anchor=html.escape(str(entry.get("entry_id", "") or "")),
				title=html.escape(build_entry_label(entry)),
				root_path=html.escape(root_path),
				context_path=html.escape(context_path),
				resource_path=html.escape(resource_path),
				property_rows=property_rows,
			)
		)

	body = """
<header>
<h1>{type_display_name}</h1>
<div>{entry_count} 条条目 | {root_count} 个根配置文件</div>
</header>
<main>
<a class="back-link" href="index.html">返回总览</a>
<section class="summary-grid">
<div class="summary-card"><strong>类型</strong><div>{type_display_name}</div></div>
<div class="summary-card"><strong>脚本</strong><div>{script_path}</div></div>
<div class="summary-card"><strong>条目数</strong><div>{entry_count}</div></div>
<div class="summary-card"><strong>字段数</strong><div>{field_count}</div></div>
</section>
<section>
<h2>字段概览</h2>
<div class="fields">{fields}</div>
</section>
<section>
<h2>条目列表</h2>
<ol class="entry-list">{entry_nav}</ol>
</section>
<section>
{entry_cards}
</section>
</main>
""".format(
		type_name=html.escape(type_name),
		type_display_name=html.escape(type_display_name),
		script_path=html.escape(script_path or type_key),
		entry_count=len(sorted_entries),
		root_count=len({str(entry.get("root_path", "") or "") for entry in sorted_entries}),
		field_count=len(fields),
		fields=html.escape(build_field_summary_text(fields)),
		entry_nav=entry_nav,
		entry_cards="".join(entry_cards),
	)
	return build_html_shell(f"{type_display_name} - 配置详情", body)


def main() -> int:
	args = parse_args()
	script_path = Path(__file__).resolve()
	repo_root = resolve_repo_root(script_path, args.repo_root)
	config_dir = (repo_root / args.config_dir).resolve()
	output_dir = (repo_root / args.output_dir).resolve()

	if not config_dir.exists():
		raise RuntimeError(f"Config directory does not exist: {config_dir}")
	if not config_dir.is_dir():
		raise RuntimeError(f"Config path is not a directory: {config_dir}")

	godot_bin = resolve_command_path(args.godot_bin)
	clean_output_directory(output_dir)

	snapshot_json_path = output_dir / "config_snapshot.json"
	snapshot = run_godot_snapshot(repo_root, config_dir, snapshot_json_path, godot_bin)
	snapshot_json_path.write_text(
		json.dumps(snapshot, ensure_ascii=False, indent=2) + "\n",
		encoding="utf-8",
	)

	entries = list(snapshot.get("entries", []))
	entries_by_type: dict[str, list[dict[str, Any]]] = defaultdict(list)
	for entry in entries:
		type_key = str(entry.get("type_key", "") or "")
		entries_by_type[type_key].append(entry)

	class_name_map = load_class_name_map(repo_root)
	type_filename_map = build_type_filename_map(entries_by_type, class_name_map)
	entry_lookup = build_entry_lookup(entries)
	root_entry_lookup = build_root_entry_lookup(entries)

	index_html = build_index_html(
		snapshot=snapshot,
		entries_by_type=entries_by_type,
		type_filename_map=type_filename_map,
		class_name_map=class_name_map,
		config_dir=to_res_path(config_dir, repo_root),
	)
	(output_dir / "index.html").write_text(index_html, encoding="utf-8")

	for type_key, type_entries in entries_by_type.items():
		page_html = build_type_page_html(
			type_key=type_key,
			type_entries=type_entries,
			entry_lookup=entry_lookup,
			root_entry_lookup=root_entry_lookup,
			type_filename_map=type_filename_map,
			class_name_map=class_name_map,
		)
		(output_dir / type_filename_map[type_key]).write_text(page_html, encoding="utf-8")

	print(
		"已导出 %d 个根配置文件、%d 个资源实例、%d 个类型页面到 %s"
		% (
			len(snapshot.get("root_files", [])),
			len(entries),
			len(entries_by_type),
			output_dir,
		)
	)
	if snapshot.get("scan_errors"):
		print("扫描完成，共 %d 条错误。" % len(snapshot["scan_errors"]))
	return 0


if __name__ == "__main__":
	try:
		raise SystemExit(main())
	except RuntimeError as exc:
		print(str(exc), file=sys.stderr)
		raise SystemExit(1)
