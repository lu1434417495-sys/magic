## 文件说明：该脚本属于战斗时间轴状态相关的状态数据脚本，集中维护当前TU、单位集合每秒、行动阈值等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleTimelineState
extends RefCounted

const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle_timeline_state.gd")

## 字段说明：记录当前TU，会参与运行时状态流转、系统协作和存档恢复。
var current_tu := 0
## 字段说明：记录时间轴每秒推进的单位数，用于把真实时间换算为战斗 TU 进度。
var units_per_second := 100
## 字段说明：记录行动阈值，达到该值时通常会触发后续逻辑或状态切换。
var action_threshold := 1000
## 字段说明：记录离散时间轴的秒级节拍；大于 0 时，TU 只会在满一个节拍后按整块推进。
var tick_interval_seconds := 0.0
## 字段说明：记录每个离散节拍应推进的 TU 数量。
var tu_per_tick := 0
## 字段说明：用于标记冻结当前是否成立或生效，供脚本后续分支判断使用，会参与运行时状态流转、系统协作和存档恢复。
var frozen := false
## 字段说明：保存就绪单位标识列表，便于批量遍历、交叉查找和界面展示。
var ready_unit_ids: Array[StringName] = []
## 字段说明：记录增量余量，会参与运行时状态流转、系统协作和存档恢复。
var delta_remainder := 0.0


func clear() -> void:
	current_tu = 0
	units_per_second = 100
	action_threshold = 1000
	tick_interval_seconds = 0.0
	tu_per_tick = 0
	frozen = false
	ready_unit_ids.clear()
	delta_remainder = 0.0


func to_dict() -> Dictionary:
	return {
		"current_tu": current_tu,
		"units_per_second": units_per_second,
		"action_threshold": action_threshold,
		"tick_interval_seconds": tick_interval_seconds,
		"tu_per_tick": tu_per_tick,
		"frozen": frozen,
		"ready_unit_ids": _string_name_array_to_strings(ready_unit_ids),
		"delta_remainder": delta_remainder,
	}


static func from_dict(data: Dictionary):
	var state = BATTLE_TIMELINE_STATE_SCRIPT.new()
	state.current_tu = int(data.get("current_tu", 0))
	state.units_per_second = int(data.get("units_per_second", 100))
	state.action_threshold = int(data.get("action_threshold", 1000))
	state.tick_interval_seconds = float(data.get("tick_interval_seconds", 0.0))
	state.tu_per_tick = int(data.get("tu_per_tick", 0))
	state.frozen = bool(data.get("frozen", false))
	state.ready_unit_ids = _strings_to_string_name_array(data.get("ready_unit_ids", []))
	state.delta_remainder = float(data.get("delta_remainder", 0.0))
	return state


static func _string_name_array_to_strings(values: Array[StringName]) -> Array[String]:
	var results: Array[String] = []
	for value in values:
		results.append(String(value))
	return results


static func _strings_to_string_name_array(values: Variant) -> Array[StringName]:
	var results: Array[StringName] = []
	if values is Array:
		for value in values:
			results.append(StringName(String(value)))
	return results
