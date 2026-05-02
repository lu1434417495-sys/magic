## 文件说明：该脚本属于战斗时间轴状态相关的状态数据脚本，集中维护当前TU、单位集合每秒、行动阈值等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleTimelineState
extends RefCounted

const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const TU_GRANULARITY := 5
const DEFAULT_TICK_INTERVAL_SECONDS := 1.0
const SCHEMA_FIELDS := {
	"current_tu": true,
	"units_per_second": true,
	"tick_interval_seconds": true,
	"tu_per_tick": true,
	"frozen": true,
	"ready_unit_ids": true,
	"delta_remainder": true,
}

## 字段说明：记录当前TU，会参与运行时状态流转、系统协作和存档恢复。
var current_tu := 0
## 字段说明：记录时间轴每秒推进的单位数，用于把真实时间换算为战斗 TU 进度。
var units_per_second := TU_GRANULARITY
## 字段说明：记录离散时间轴的秒级节拍；大于 0 时，TU 只会在满一个节拍后按整块推进。
var tick_interval_seconds := DEFAULT_TICK_INTERVAL_SECONDS
## 字段说明：记录每个离散节拍应推进的 TU 数量。
var tu_per_tick := TU_GRANULARITY
## 字段说明：用于标记冻结当前是否成立或生效，供脚本后续分支判断使用，会参与运行时状态流转、系统协作和存档恢复。
var frozen := false
## 字段说明：保存就绪单位标识列表，便于批量遍历、交叉查找和界面展示。
var ready_unit_ids: Array[StringName] = []
## 字段说明：记录增量余量，会参与运行时状态流转、系统协作和存档恢复。
var delta_remainder := 0.0


func clear() -> void:
	current_tu = 0
	units_per_second = TU_GRANULARITY
	tick_interval_seconds = DEFAULT_TICK_INTERVAL_SECONDS
	tu_per_tick = TU_GRANULARITY
	frozen = false
	ready_unit_ids.clear()
	delta_remainder = 0.0


func to_dict() -> Dictionary:
	return {
		"current_tu": current_tu,
		"units_per_second": units_per_second,
		"tick_interval_seconds": tick_interval_seconds,
		"tu_per_tick": tu_per_tick,
		"frozen": frozen,
		"ready_unit_ids": _string_name_array_to_strings(ready_unit_ids),
		"delta_remainder": delta_remainder,
	}


static func from_dict(data: Variant):
	if data is not Dictionary:
		return null
	if not _has_exact_schema_fields(data):
		return null
	if typeof(data["current_tu"]) != TYPE_INT or int(data["current_tu"]) < 0:
		return null
	if typeof(data["units_per_second"]) != TYPE_INT or int(data["units_per_second"]) <= 0:
		return null
	if typeof(data["tu_per_tick"]) != TYPE_INT or int(data["tu_per_tick"]) <= 0:
		return null
	if not _is_number(data["tick_interval_seconds"]) or float(data["tick_interval_seconds"]) <= 0.0:
		return null
	if not _is_number(data["delta_remainder"]) or float(data["delta_remainder"]) < 0.0:
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
	state.units_per_second = int(data["units_per_second"])
	state.tick_interval_seconds = float(data["tick_interval_seconds"])
	state.tu_per_tick = int(data["tu_per_tick"])
	state.frozen = bool(data["frozen"])
	state.ready_unit_ids = ready_unit_ids
	state.delta_remainder = float(data["delta_remainder"])
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


static func _is_number(value: Variant) -> bool:
	var value_type := typeof(value)
	return value_type == TYPE_FLOAT or value_type == TYPE_INT


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
