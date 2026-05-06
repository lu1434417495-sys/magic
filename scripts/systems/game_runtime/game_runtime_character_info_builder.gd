class_name GameRuntimeCharacterInfoBuilder
extends RefCounted

const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

const FORTUNE_MARKED_STAT_ID: StringName = &"fortune_marked"
const DOOM_MARKED_STAT_ID: StringName = &"doom_marked"
const DOOM_AUTHORITY_STAT_ID: StringName = &"doom_authority"

var _runtime_ref: WeakRef = null
var _runtime = null:
	get:
		return _runtime_ref.get_ref() if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null


func setup(runtime) -> void:
	_runtime = runtime


func dispose() -> void:
	_runtime = null


func build_character_info_meta_label(type_label: String, faction_label: String, coord: Vector2i) -> String:
	return "%s  |  阵营 %s  |  坐标 %s" % [type_label, faction_label, _format_coord(coord)]


func build_world_character_info_sections(npc: Dictionary, coord: Vector2i, faction_label: String) -> Array[Dictionary]:
	var entries: Array[Dictionary] = [
		{
			"label": "类型",
			"value": "世界 NPC",
		},
		{
			"label": "阵营",
			"value": faction_label,
		},
		{
			"label": "坐标",
			"value": _format_coord(coord),
		},
	]
	var service_type := String(npc.get("service_type", "")).strip_edges()
	if not service_type.is_empty():
		entries.append({
			"label": "服务",
			"value": service_type,
		})
	var facility_name := String(npc.get("facility_name", "")).strip_edges()
	if not facility_name.is_empty():
		entries.append({
			"label": "所属设施",
			"value": facility_name,
		})
	return [{
		"title": "基础概览",
		"entries": entries,
	}]


func build_battle_character_info_sections(unit: BattleUnitState, type_label: String, faction_label: String) -> Array[Dictionary]:
	var sections: Array[Dictionary] = [{
		"title": "基础概览",
		"entries": build_battle_character_info_base_entries(unit, type_label, faction_label),
	}]
	var identity_entries := build_battle_character_identity_entries(unit)
	if not identity_entries.is_empty():
		sections.append({
			"title": "身份与特性",
			"entries": identity_entries,
		})
	var status_entries := build_battle_character_status_entries(unit)
	if not status_entries.is_empty():
		sections.append({
			"title": "状态效果",
			"entries": status_entries,
		})
	var skill_entries := build_battle_character_skill_entries(unit)
	if not skill_entries.is_empty():
		sections.append({
			"title": "技能摘要",
			"entries": skill_entries,
		})
	return sections


func build_battle_character_identity_entries(unit: BattleUnitState) -> Array[Dictionary]:
	var summary := _get_battle_unit_identity_summary(unit)
	if summary.is_empty():
		return []
	var entries: Array[Dictionary] = [
		{
			"label": "种族",
			"value": String(summary.get("race_label", "")),
		},
		{
			"label": "亚种",
			"value": String(summary.get("subrace_label", "")),
		},
		{
			"label": "年龄",
			"value": "%d 岁" % int(summary.get("age_years", 0)),
		},
		{
			"label": "自然阶段",
			"value": String(summary.get("natural_age_stage_label", "")),
		},
		{
			"label": "有效阶段",
			"value": String(summary.get("effective_age_stage_label", "")),
		},
		{
			"label": "体型",
			"value": "%s（%d）" % [
				String(summary.get("body_size_category", "")),
				int(summary.get("body_size", 0)),
			],
		},
	]
	var bloodline_label := String(summary.get("bloodline_label", "")).strip_edges()
	if not bloodline_label.is_empty():
		entries.append({
			"label": "血脉",
			"value": _join_identity_label_pair(
				bloodline_label,
				String(summary.get("bloodline_stage_label", "")).strip_edges()
			),
		})
	var ascension_label := String(summary.get("ascension_label", "")).strip_edges()
	if not ascension_label.is_empty():
		entries.append({
			"label": "升华",
			"value": _join_identity_label_pair(
				ascension_label,
				String(summary.get("ascension_stage_label", "")).strip_edges()
			),
		})
	var damage_resistance_text := _format_identity_map(summary.get("damage_resistances", {}))
	if not damage_resistance_text.is_empty():
		entries.append({
			"label": "伤害抗性",
			"value": damage_resistance_text,
		})
	var save_advantage_text := _format_identity_array(summary.get("save_advantage_tags", []))
	if not save_advantage_text.is_empty():
		entries.append({
			"label": "豁免优势",
			"value": save_advantage_text,
		})
	for line in _identity_text_array(summary.get("trait_summary", [])):
		entries.append({"text": "特性：%s" % line})
	for line in _identity_text_array(summary.get("racial_skill_lines", [])):
		entries.append({"text": "种族法术：%s" % line})
	return entries


func build_battle_character_info_fate_payload(unit: BattleUnitState) -> Dictionary:
	if unit == null or unit.attribute_snapshot == null:
		return {}

	var hidden_luck_at_birth := get_battle_unit_attribute_value(unit, UNIT_BASE_ATTRIBUTES_SCRIPT.HIDDEN_LUCK_AT_BIRTH)
	var faith_luck_bonus := get_battle_unit_attribute_value(unit, UNIT_BASE_ATTRIBUTES_SCRIPT.FAITH_LUCK_BONUS)
	var effective_luck := clampi(
		hidden_luck_at_birth + faith_luck_bonus,
		UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MIN,
		UNIT_BASE_ATTRIBUTES_SCRIPT.EFFECTIVE_LUCK_MAX
	)
	var fortune_marked := get_battle_unit_attribute_value(unit, FORTUNE_MARKED_STAT_ID)
	var doom_marked := get_battle_unit_attribute_value(unit, DOOM_MARKED_STAT_ID)
	var doom_authority := get_battle_unit_attribute_value(unit, DOOM_AUTHORITY_STAT_ID)
	var has_source_member := false
	var party_state = _runtime.get_party_state() if _runtime != null else null
	if party_state != null and unit.source_member_id != &"" and party_state.has_method("get_member_state"):
		has_source_member = party_state.get_member_state(unit.source_member_id) != null
	if not has_source_member \
		and hidden_luck_at_birth == 0 \
		and faith_luck_bonus == 0 \
		and fortune_marked == 0 \
		and doom_marked == 0 \
		and doom_authority == 0:
		return {}

	return {
		"hidden_luck_at_birth": hidden_luck_at_birth,
		"faith_luck_bonus": faith_luck_bonus,
		"effective_luck": effective_luck,
		"fortune_marked": fortune_marked,
		"doom_marked": doom_marked,
		"doom_authority": doom_authority,
		"has_misfortune": doom_authority > 0,
	}


func build_battle_character_info_base_entries(unit: BattleUnitState, type_label: String, faction_label: String) -> Array[Dictionary]:
	var entries: Array[Dictionary] = [
		{
			"label": "类型",
			"value": type_label,
		},
		{
			"label": "阵营",
			"value": faction_label,
		},
		{
			"label": "坐标",
			"value": _format_coord(unit.coord),
		},
		{
			"label": "HP",
			"value": "%d / %d" % [int(unit.current_hp), maxi(get_battle_unit_attribute_value(unit, &"hp_max"), 1)],
		},
		{
			"label": "MP",
			"value": "%d / %d" % [int(unit.current_mp), maxi(get_battle_unit_attribute_value(unit, &"mp_max"), 0)],
		},
		{
			"label": "AP",
			"value": "%d" % int(unit.current_ap),
		},
		{
			"label": "行动",
			"value": "%d" % int(unit.current_move_points),
		},
	]
	var stamina_max := get_battle_unit_attribute_value(unit, &"stamina_max")
	if stamina_max > 0 or int(unit.current_stamina) > 0:
		entries.append({
			"label": "ST",
			"value": "%d / %d" % [int(unit.current_stamina), maxi(stamina_max, 0)],
		})
	var aura_max := get_battle_unit_attribute_value(unit, &"aura_max")
	if aura_max > 0 or int(unit.current_aura) > 0:
		entries.append({
			"label": "Aura",
			"value": "%d / %d" % [int(unit.current_aura), maxi(aura_max, 0)],
		})
	return entries


func build_battle_character_status_entries(unit: BattleUnitState) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	for status_key in ProgressionDataUtils.sorted_string_keys(unit.status_effects):
		var status_id := StringName(status_key)
		var effect_state = unit.get_status_effect(status_id)
		if effect_state == null:
			continue
		var line := String(status_id)
		if int(effect_state.stacks) > 1:
			line += " x%d" % int(effect_state.stacks)
		if effect_state.has_duration():
			line += " · %d TU" % int(effect_state.duration)
		entries.append({"text": line})
	return entries


func build_battle_character_skill_entries(unit: BattleUnitState) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	for skill_id in unit.known_active_skill_ids:
		var resolved_skill_id := ProgressionDataUtils.to_string_name(skill_id)
		if resolved_skill_id == &"":
			continue
		entries.append({
			"text": _get_skill_display_name(resolved_skill_id),
		})
		if entries.size() >= 6:
			break
	return entries


func get_battle_unit_attribute_value(unit: BattleUnitState, attribute_id: StringName) -> int:
	if unit == null or unit.attribute_snapshot == null:
		return 0
	return int(unit.attribute_snapshot.get_value(attribute_id))


func _format_coord(coord: Vector2i) -> String:
	return _runtime.format_coord(coord) if _runtime != null else "(%d,%d)" % [coord.x, coord.y]


func _get_skill_display_name(skill_id: StringName) -> String:
	return _runtime._get_skill_display_name(skill_id) if _runtime != null else String(skill_id)


func _get_battle_unit_identity_summary(unit: BattleUnitState) -> Dictionary:
	if unit == null or unit.source_member_id == &"" or _runtime == null:
		return {}
	if not _runtime.has_method("get_character_management"):
		return {}
	var character_management = _runtime.get_character_management()
	if character_management == null or not character_management.has_method("get_identity_summary_for_member"):
		return {}
	var summary = character_management.get_identity_summary_for_member(unit.source_member_id)
	return summary if summary is Dictionary else {}


func _join_identity_label_pair(primary_label: String, secondary_label: String) -> String:
	if secondary_label.is_empty():
		return primary_label
	return "%s · %s" % [primary_label, secondary_label]


func _format_identity_map(value: Variant) -> String:
	if value is not Dictionary:
		return ""
	var parts := PackedStringArray()
	var data := value as Dictionary
	for key in data.keys():
		parts.append("%s=%s" % [String(key), String(data[key])])
	parts.sort()
	return "，".join(parts)


func _format_identity_array(value: Variant) -> String:
	return "，".join(_identity_text_array(value))


func _identity_text_array(value: Variant) -> PackedStringArray:
	var result := PackedStringArray()
	if value is not Array:
		return result
	for entry in value:
		var text := String(entry).strip_edges()
		if text.is_empty():
			continue
		result.append(text)
	return result
