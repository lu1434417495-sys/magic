class_name QuestState
extends RefCounted

const QUEST_STATE_SCRIPT = preload("res://scripts/player/progression/quest_state.gd")

const STATUS_INACTIVE: StringName = &"inactive"
const STATUS_ACTIVE: StringName = &"active"
const STATUS_COMPLETED: StringName = &"completed"
const STATUS_REWARDED: StringName = &"rewarded"
const STATUS_FAILED: StringName = &"failed"

var quest_id: StringName = &""
var status_id: StringName = STATUS_INACTIVE
var objective_progress: Dictionary = {}
var accepted_at_world_step := -1
var completed_at_world_step := -1
var reward_claimed_at_world_step := -1
var last_progress_context: Dictionary = {}


func is_active() -> bool:
	return status_id == STATUS_ACTIVE


func is_completed() -> bool:
	return status_id == STATUS_COMPLETED or status_id == STATUS_REWARDED


func is_terminal() -> bool:
	return status_id == STATUS_REWARDED or status_id == STATUS_FAILED


func get_objective_progress(objective_id: StringName) -> int:
	return maxi(int(objective_progress.get(objective_id, 0)), 0)


func record_objective_progress(
	objective_id: StringName,
	delta: int,
	target_value: int = 1,
	context: Dictionary = {}
) -> int:
	if objective_id == &"" or delta <= 0 or not is_active():
		return get_objective_progress(objective_id)
	var next_value := mini(get_objective_progress(objective_id) + delta, maxi(target_value, 1))
	objective_progress[objective_id] = next_value
	last_progress_context = context.duplicate(true)
	return next_value


func is_objective_complete(objective_id: StringName, target_value: int = 1) -> bool:
	return get_objective_progress(objective_id) >= maxi(target_value, 1)


func has_completed_all_objectives(quest_def) -> bool:
	if quest_def == null or not quest_def.has_method("get_objective_ids"):
		return false
	for objective_variant in quest_def.objective_defs:
		if objective_variant is not Dictionary:
			return false
		var objective_data := objective_variant as Dictionary
		var objective_id := ProgressionDataUtils.to_string_name(objective_data.get("objective_id", ""))
		var target_value := int(objective_data.get("target_value", 1))
		if objective_id == &"" or not is_objective_complete(objective_id, target_value):
			return false
	return true


func mark_accepted(world_step: int = -1) -> void:
	status_id = STATUS_ACTIVE
	accepted_at_world_step = world_step
	if completed_at_world_step < accepted_at_world_step:
		completed_at_world_step = -1
	if reward_claimed_at_world_step < accepted_at_world_step:
		reward_claimed_at_world_step = -1


func mark_completed(world_step: int = -1) -> void:
	status_id = STATUS_COMPLETED
	completed_at_world_step = world_step


func mark_reward_claimed(world_step: int = -1) -> void:
	status_id = STATUS_REWARDED
	reward_claimed_at_world_step = world_step


func mark_failed() -> void:
	status_id = STATUS_FAILED


func to_dict() -> Dictionary:
	return {
		"quest_id": String(quest_id),
		"status_id": String(status_id),
		"objective_progress": ProgressionDataUtils.string_name_int_map_to_string_dict(objective_progress),
		"accepted_at_world_step": accepted_at_world_step,
		"completed_at_world_step": completed_at_world_step,
		"reward_claimed_at_world_step": reward_claimed_at_world_step,
		"last_progress_context": last_progress_context.duplicate(true),
	}


static func from_dict(data: Dictionary):
	var state = QUEST_STATE_SCRIPT.new()
	state.quest_id = ProgressionDataUtils.to_string_name(data.get("quest_id", ""))
	state.status_id = _normalize_status_id(ProgressionDataUtils.to_string_name(data.get("status_id", STATUS_INACTIVE)))
	state.objective_progress = ProgressionDataUtils.to_string_name_int_map(data.get("objective_progress", {}))
	state.accepted_at_world_step = int(data.get("accepted_at_world_step", -1))
	state.completed_at_world_step = int(data.get("completed_at_world_step", -1))
	state.reward_claimed_at_world_step = int(data.get("reward_claimed_at_world_step", -1))
	var context_variant: Variant = data.get("last_progress_context", {})
	state.last_progress_context = context_variant.duplicate(true) if context_variant is Dictionary else {}
	return state


static func _normalize_status_id(status_id: StringName) -> StringName:
	match status_id:
		STATUS_ACTIVE, STATUS_COMPLETED, STATUS_REWARDED, STATUS_FAILED:
			return status_id
		_:
			return STATUS_INACTIVE
