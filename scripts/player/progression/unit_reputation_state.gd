## 文件说明：该脚本属于单位声望状态相关的状态数据脚本，集中维护道德、自定义状态集合等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name UnitReputationState
extends RefCounted

const UNIT_REPUTATION_STATE_SCRIPT = preload("res://scripts/player/progression/unit_reputation_state.gd")

const MORALITY: StringName = &"morality"
const STANDARD_STATE_IDS := [
	MORALITY,
]

## 字段说明：记录道德，会参与成长规则判定、序列化和界面展示。
var morality := 0
## 字段说明：缓存自定义状态集合字典，集中保存可按键查询的运行时数据。
var custom_states: Dictionary = {}


func get_reputation_value(state_id: StringName) -> int:
	match state_id:
		MORALITY:
			return morality
		_:
			return int(custom_states.get(state_id, 0))


func set_reputation_value(state_id: StringName, value: int) -> void:
	match state_id:
		MORALITY:
			morality = value
		_:
			custom_states[state_id] = value


func get_all_state_ids() -> Array[StringName]:
	return ProgressionDataUtils.to_string_name_array(STANDARD_STATE_IDS)


func to_dict() -> Dictionary:
	return {
		"morality": morality,
		"custom_states": ProgressionDataUtils.string_name_int_map_to_string_dict(custom_states),
	}


static func from_dict(data: Dictionary):
	var state := UNIT_REPUTATION_STATE_SCRIPT.new()
	state.morality = int(data.get("morality", 0))
	state.custom_states = ProgressionDataUtils.to_string_name_int_map(data.get("custom_states", {}))
	return state
