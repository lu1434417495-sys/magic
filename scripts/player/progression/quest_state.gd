class_name QuestState
extends RefCounted

const QUEST_STATE_SCRIPT = preload("res://scripts/player/progression/quest_state.gd")

const STATUS_INACTIVE: StringName = &"inactive"
const STATUS_ACTIVE: StringName = &"active"
const STATUS_COMPLETED: StringName = &"completed"
const STATUS_REWARDED: StringName = &"rewarded"
const STATUS_FAILED: StringName = &"failed"
const REQUIRED_SERIALIZED_FIELDS := [
	"quest_id",
	"status_id",
	"objective_progress",
	"accepted_at_world_step",
	"completed_at_world_step",
	"reward_claimed_at_world_step",
	"last_progress_context",
]

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
	target_value: int = 0,
	context: Dictionary = {}
) -> int:
	if objective_id == &"" or delta <= 0 or target_value <= 0 or not is_active():
		return get_objective_progress(objective_id)
	var next_value := mini(get_objective_progress(objective_id) + delta, target_value)
	objective_progress[objective_id] = next_value
	last_progress_context = context.duplicate(true)
	return next_value


func is_objective_complete(objective_id: StringName, target_value: int = 0) -> bool:
	return objective_id != &"" and target_value > 0 and get_objective_progress(objective_id) >= target_value


func has_completed_all_objectives(quest_def) -> bool:
	if quest_def == null or not quest_def.has_method("get_objective_ids"):
		return false
	for objective_variant in quest_def.objective_defs:
		if objective_variant is not Dictionary:
			return false
		var objective_data := objective_variant as Dictionary
		var objective_id := ProgressionDataUtils.to_string_name(objective_data.get("objective_id", ""))
		if not objective_data.has("target_value") or objective_data["target_value"] is not int:
			return false
		var target_value := int(objective_data["target_value"])
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


static func from_dict(data: Variant):
	if data is not Dictionary:
		return null
	var payload := data as Dictionary
	if not _has_exact_serialized_fields(payload):
		return null
	var objective_progress_variant: Variant = payload["objective_progress"]
	var context_variant: Variant = payload["last_progress_context"]
	if objective_progress_variant is not Dictionary:
		return null
	if context_variant is not Dictionary:
		return null
	var quest_id := _read_required_string_name(payload["quest_id"])
	var status_id := _read_required_string_name(payload["status_id"])
	if quest_id == &"" or not _is_valid_status_id(status_id):
		return null
	if (
		payload["accepted_at_world_step"] is not int
		or int(payload["accepted_at_world_step"]) < -1
		or payload["completed_at_world_step"] is not int
		or int(payload["completed_at_world_step"]) < -1
		or payload["reward_claimed_at_world_step"] is not int
		or int(payload["reward_claimed_at_world_step"]) < -1
	):
		return null
	var objective_progress_values: Dictionary = {}
	for objective_id_variant in (objective_progress_variant as Dictionary).keys():
		var objective_id := _read_required_string_name(objective_id_variant)
		if objective_id == &"":
			return null
		var progress_variant: Variant = (objective_progress_variant as Dictionary)[objective_id_variant]
		if progress_variant is not int or int(progress_variant) < 0:
			return null
		objective_progress_values[objective_id] = int(progress_variant)

	var state = QUEST_STATE_SCRIPT.new()
	state.quest_id = quest_id
	state.status_id = status_id
	state.objective_progress = objective_progress_values
	state.accepted_at_world_step = int(payload["accepted_at_world_step"])
	state.completed_at_world_step = int(payload["completed_at_world_step"])
	state.reward_claimed_at_world_step = int(payload["reward_claimed_at_world_step"])
	state.last_progress_context = context_variant.duplicate(true)
	return state


static func _has_exact_serialized_fields(payload: Dictionary) -> bool:
	if payload.size() != REQUIRED_SERIALIZED_FIELDS.size():
		return false
	for field_name in REQUIRED_SERIALIZED_FIELDS:
		if not payload.has(field_name):
			return false
	return true


static func _read_required_string_name(value: Variant) -> StringName:
	if value is not String and value is not StringName:
		return &""
	var text := String(value)
	if text.strip_edges().is_empty():
		return &""
	return StringName(text)


static func _is_valid_status_id(status_id: StringName) -> bool:
	match status_id:
		STATUS_INACTIVE, STATUS_ACTIVE, STATUS_COMPLETED, STATUS_REWARDED, STATUS_FAILED:
			return true
		_:
			return false
