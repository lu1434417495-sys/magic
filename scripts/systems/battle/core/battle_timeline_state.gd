## 文件说明：该脚本属于战斗时间轴状态相关的状态数据脚本，集中维护当前TU、单位集合每秒、行动阈值等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleTimelineState
extends RefCounted

const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const TU_GRANULARITY := 5
const SCHEMA_FIELDS := {
	"current_tu": true,
	"tu_per_tick": true,
	"frozen": true,
	"ready_unit_ids": true,
}

## 字段说明：记录当前TU，会参与运行时状态流转、系统协作和存档恢复。
var current_tu := 0
## 字段说明：记录每个离散节拍应推进的 TU 数量。
var tu_per_tick := TU_GRANULARITY
## 字段说明：用于标记冻结当前是否成立或生效，供脚本后续分支判断使用，会参与运行时状态流转、系统协作和存档恢复。
var frozen := false
## 字段说明：保存就绪单位标识列表，便于批量遍历、交叉查找和界面展示。
var ready_unit_ids: Array[StringName] = []


func clear() -> void:
	current_tu = 0
	tu_per_tick = TU_GRANULARITY
	frozen = false
	ready_unit_ids.clear()


func to_dict() -> Dictionary:
	return {
		"current_tu": current_tu,
		"tu_per_tick": tu_per_tick,
		"frozen": frozen,
		"ready_unit_ids": _string_name_array_to_strings(ready_unit_ids),
	}


static func from_dict(data: Variant):
	if data is not Dictionary:
		return null
	if not _has_exact_schema_fields(data):
		return null
	if typeof(data["current_tu"]) != TYPE_INT or int(data["current_tu"]) < 0:
		return null
	if typeof(data["tu_per_tick"]) != TYPE_INT or int(data["tu_per_tick"]) <= 0:
		return null
	if typeof(data["frozen"]) != TYPE_BOOL:
		return null
	if data["ready_unit_ids"] is not Array:
		return null
	var parsed_ready_unit_ids = _strings_to_string_name_array(data["ready_unit_ids"])
	if parsed_ready_unit_ids == null:
		return null
	var ready_unit_ids: Array[StringName] = parsed_ready_unit_ids

	var state = BATTLE_TIMELINE_STATE_SCRIPT.new()
	state.current_tu = int(data["current_tu"])
	state.tu_per_tick = int(data["tu_per_tick"])
	state.frozen = bool(data["frozen"])
	state.ready_unit_ids = ready_unit_ids
	return state


static func _string_name_array_to_strings(values: Array[StringName]) -> Array[String]:
	var results: Array[String] = []
	for value in values:
		results.append(String(value))
	return results


static func _has_exact_schema_fields(data: Dictionary) -> bool:
	if data.size() != SCHEMA_FIELDS.size():
		return false
	for field_name in SCHEMA_FIELDS:
		if not data.has(field_name):
			return false
	return true


static func _strings_to_string_name_array(values: Variant):
	var results: Array[StringName] = []
	if values is not Array:
		return null
	for value in values:
		if typeof(value) != TYPE_STRING and typeof(value) != TYPE_STRING_NAME:
			return null
		var id_text := String(value)
		if id_text.is_empty():
			return null
		results.append(StringName(id_text))
	return results
