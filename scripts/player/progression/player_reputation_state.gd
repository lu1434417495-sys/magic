class_name PlayerReputationState
extends RefCounted

const SELF_SCRIPT = preload("res://scripts/player/progression/player_reputation_state.gd")

const MORALITY: StringName = &"morality"
const STANDARD_STATE_IDS := [
	MORALITY,
]

var morality := 0
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


static func from_dict(data: Dictionary) -> PlayerReputationState:
	var state: PlayerReputationState = SELF_SCRIPT.new()
	state.morality = int(data.get("morality", 0))
	state.custom_states = ProgressionDataUtils.to_string_name_int_map(data.get("custom_states", {}))
	return state
