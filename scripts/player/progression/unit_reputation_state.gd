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
	for field_name in [
		"morality",
		"custom_states",
	]:
		if not data.has(field_name):
			return null
	var custom_states_variant: Variant = data["custom_states"]
	if custom_states_variant is not Dictionary:
		return null
	if data["morality"] is not int:
		return null
	var parsed_custom_states = _parse_int_map(custom_states_variant)
	if parsed_custom_states == null:
		return null

	var state := UNIT_REPUTATION_STATE_SCRIPT.new()
	state.morality = data["morality"]
	state.custom_states = parsed_custom_states
	return state


static func _parse_int_map(values: Dictionary):
	var parsed_values: Dictionary = {}
	var seen_keys: Dictionary = {}
	for raw_key in values.keys():
		var key_type := typeof(raw_key)
		if key_type != TYPE_STRING and key_type != TYPE_STRING_NAME:
			return null
		var parsed_key := ProgressionDataUtils.to_string_name(raw_key)
		if parsed_key == &"" or seen_keys.has(parsed_key):
			return null
		if values[raw_key] is not int:
			return null
		seen_keys[parsed_key] = true
		parsed_values[parsed_key] = int(values[raw_key])
	return parsed_values
