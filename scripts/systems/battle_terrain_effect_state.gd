## 文件说明：该脚本属于战斗地形效果状态相关的状态数据脚本，集中维护字段实例唯一标识、效果唯一标识、效果类型等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleTerrainEffectState
extends RefCounted

const BATTLE_TERRAIN_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle_terrain_effect_state.gd")

## 字段说明：记录字段实例唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var field_instance_id: StringName = &""
## 字段说明：记录效果唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var effect_id: StringName = &""
## 字段说明：记录效果类型，用于区分不同规则、资源类别或行为分支。
var effect_type: StringName = &"damage"
## 字段说明：记录来源单位唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var source_unit_id: StringName = &""
## 字段说明：记录来源技能唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var source_skill_id: StringName = &""
## 字段说明：记录目标队伍过滤，会参与运行时状态流转、系统协作和存档恢复。
var target_team_filter: StringName = &"any"
## 字段说明：记录强度，会参与运行时状态流转、系统协作和存档恢复。
var power := 0
## 字段说明：记录伤害标签，作为抗性、减伤和日志语义的结算主键。
var damage_tag: StringName = &""
## 字段说明：记录抗性属性唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var resistance_attribute_id: StringName = &""
## 字段说明：记录剩余TU，会参与运行时状态流转、系统协作和存档恢复。
var remaining_tu := 0
## 字段说明：记录跳点间隔TU，会参与运行时状态流转、系统协作和存档恢复。
var tick_interval_tu := 0
## 字段说明：记录下一个跳点TU，会参与运行时状态流转、系统协作和存档恢复。
var next_tick_at_tu := 0
## 字段说明：记录堆叠行为，会参与运行时状态流转、系统协作和存档恢复。
var stack_behavior: StringName = &"refresh"
## 字段说明：缓存参数字典，集中保存可按键查询的运行时数据。
var params: Dictionary = {}


func to_dict() -> Dictionary:
	return {
		"field_instance_id": String(field_instance_id),
		"effect_id": String(effect_id),
		"effect_type": String(effect_type),
		"source_unit_id": String(source_unit_id),
		"source_skill_id": String(source_skill_id),
		"target_team_filter": String(target_team_filter),
		"power": power,
		"damage_tag": String(damage_tag),
		"resistance_attribute_id": String(resistance_attribute_id),
		"remaining_tu": remaining_tu,
		"tick_interval_tu": tick_interval_tu,
		"next_tick_at_tu": next_tick_at_tu,
		"stack_behavior": String(stack_behavior),
		"params": params.duplicate(true),
	}


static func from_dict(data: Dictionary):
	var effect_state := BATTLE_TERRAIN_EFFECT_STATE_SCRIPT.new()
	effect_state.field_instance_id = StringName(String(data.get("field_instance_id", "")))
	effect_state.effect_id = StringName(String(data.get("effect_id", "")))
	effect_state.effect_type = StringName(String(data.get("effect_type", "damage")))
	effect_state.source_unit_id = StringName(String(data.get("source_unit_id", "")))
	effect_state.source_skill_id = StringName(String(data.get("source_skill_id", "")))
	effect_state.target_team_filter = StringName(String(data.get("target_team_filter", "any")))
	effect_state.power = int(data.get("power", 0))
	effect_state.damage_tag = StringName(String(data.get("damage_tag", "")))
	effect_state.resistance_attribute_id = StringName(String(data.get("resistance_attribute_id", "")))
	effect_state.remaining_tu = maxi(int(data.get("remaining_tu", 0)), 0)
	effect_state.tick_interval_tu = maxi(int(data.get("tick_interval_tu", 0)), 0)
	effect_state.next_tick_at_tu = maxi(int(data.get("next_tick_at_tu", 0)), 0)
	effect_state.stack_behavior = StringName(String(data.get("stack_behavior", "refresh")))
	effect_state.params = data.get("params", {}).duplicate(true)
	return effect_state


static func to_dict_array(effect_states: Array) -> Array[Dictionary]:
	var payloads: Array[Dictionary] = []
	for effect_state_variant in effect_states:
		var effect_state := effect_state_variant as BattleTerrainEffectState
		if effect_state == null:
			continue
		payloads.append(effect_state.to_dict())
	return payloads


static func from_dict_array(values: Variant) -> Array[BattleTerrainEffectState]:
	var effect_states: Array[BattleTerrainEffectState] = []
	if values is Array:
		for value in values:
			if value is Dictionary:
				effect_states.append(from_dict(value))
	return effect_states


static func duplicate_array(effect_states: Array) -> Array[BattleTerrainEffectState]:
	return from_dict_array(to_dict_array(effect_states))
