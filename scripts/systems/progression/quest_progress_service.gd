class_name QuestProgressService
extends RefCounted

const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const QUEST_STATE_SCRIPT = preload("res://scripts/player/progression/quest_state.gd")
const QuestState = QUEST_STATE_SCRIPT

const EVENT_ACCEPT: StringName = &"accept"
const EVENT_PROGRESS: StringName = &"progress"
const EVENT_COMPLETE: StringName = &"complete"

var _party_state: PartyState = PARTY_STATE_SCRIPT.new()
var _quest_defs: Dictionary = {}


func setup(party_state: PartyState, quest_defs: Dictionary = {}) -> void:
	_party_state = party_state if party_state != null else PARTY_STATE_SCRIPT.new()
	_quest_defs = quest_defs if quest_defs != null else {}


func set_party_state(party_state: PartyState, quest_defs: Dictionary = _quest_defs) -> void:
	setup(party_state, quest_defs)


func get_party_state() -> PartyState:
	return _party_state


func get_quest_defs() -> Dictionary:
	return _quest_defs


func get_active_quests() -> Array[QuestState]:
	return _party_state.get_active_quests() if _party_state != null else []


func get_claimable_quests() -> Array[QuestState]:
	return _party_state.get_claimable_quests() if _party_state != null else []


func get_claimable_quest_ids() -> Array[StringName]:
	return _party_state.get_claimable_quest_ids() if _party_state != null else []


func get_completed_quest_ids() -> Array[StringName]:
	return _party_state.get_completed_quest_ids() if _party_state != null else []


func accept_quest(quest_id: StringName, world_step: int = -1, allow_reaccept: bool = false) -> bool:
	if _party_state == null or quest_id == &"":
		return false
	if not _quest_defs.is_empty() and not _quest_defs.has(quest_id):
		return false
	if _party_state.has_active_quest(quest_id):
		return false
	if _party_state.has_claimable_quest(quest_id):
		return false
	if _party_state.has_completed_quest(quest_id) and not allow_reaccept:
		return false
	if allow_reaccept and _party_state.has_completed_quest(quest_id):
		_party_state.completed_quest_ids.erase(quest_id)
	var quest_state := QUEST_STATE_SCRIPT.new()
	quest_state.quest_id = quest_id
	quest_state.mark_accepted(world_step)
	_party_state.set_active_quest_state(quest_state)
	return true


func complete_quest(quest_id: StringName, world_step: int = -1) -> bool:
	if _party_state == null or quest_id == &"":
		return false
	if _party_state.has_claimable_quest(quest_id):
		return false
	if _party_state.has_completed_quest(quest_id):
		return false
	return _party_state.mark_quest_claimable(quest_id, world_step)


func apply_quest_progress_events(event_variants: Array, _unused_world_step: int = -1) -> Dictionary:
	var summary := {
		"accepted_quest_ids": [],
		"progressed_quest_ids": [],
		"claimable_quest_ids": [],
		"completed_quest_ids": [],
	}
	if _party_state == null:
		return summary

	for event_variant in event_variants:
		if event_variant is not Dictionary:
			continue
		var event_data: Dictionary = (event_variant as Dictionary).duplicate(true)
		if not _is_valid_quest_progress_event(event_data):
			continue
		var event_type := _read_required_string_name(event_data, "event_type")
		var quest_id := _read_required_string_name(event_data, "quest_id")
		var event_world_step := int(event_data["world_step"])
		match event_type:
			EVENT_ACCEPT:
				if quest_id == &"":
					continue
				if accept_quest(quest_id, event_world_step, bool(event_data.get("allow_reaccept", false))):
					_append_unique_string_name(summary["accepted_quest_ids"], quest_id)
			EVENT_COMPLETE:
				if quest_id == &"":
					continue
				if not _party_state.has_active_quest(quest_id) and bool(event_data.get("auto_accept", false)):
					if accept_quest(quest_id, event_world_step, bool(event_data.get("allow_reaccept", false))):
						_append_unique_string_name(summary["accepted_quest_ids"], quest_id)
				if complete_quest(quest_id, event_world_step):
					_append_unique_string_name(summary["claimable_quest_ids"], quest_id)
			EVENT_PROGRESS:
				var progressed_quest_ids := _apply_progress_event(event_data, event_world_step)
				for progressed_quest_id_variant in progressed_quest_ids:
					var progressed_quest_id := ProgressionDataUtils.to_string_name(progressed_quest_id_variant)
					if progressed_quest_id == &"":
						continue
					_append_unique_string_name(summary["progressed_quest_ids"], progressed_quest_id)
				for claimable_quest_id in _maybe_complete_quests_after_progress(event_data, event_world_step, progressed_quest_ids):
					_append_unique_string_name(summary["claimable_quest_ids"], claimable_quest_id)
	return summary


func _apply_progress_event(event_data: Dictionary, world_step: int) -> Array[StringName]:
	var progressed_quest_ids: Array[StringName] = []
	var quest_id := ProgressionDataUtils.to_string_name(event_data.get("quest_id", ""))
	var progress_delta := _resolve_progress_delta(event_data)
	if progress_delta <= 0:
		return progressed_quest_ids
	if quest_id != &"":
		var quest_state: QuestState = _party_state.get_active_quest_state(quest_id)
		if quest_state == null and bool(event_data.get("auto_accept", false)):
			if accept_quest(quest_id, world_step, bool(event_data.get("allow_reaccept", false))):
				quest_state = _party_state.get_active_quest_state(quest_id)
		if quest_state == null:
			return progressed_quest_ids
		var objective_id := ProgressionDataUtils.to_string_name(event_data.get("objective_id", ""))
		if objective_id == &"":
			return progressed_quest_ids
		var target_value := _resolve_event_target_value(event_data, quest_id, objective_id)
		if target_value <= 0:
			return progressed_quest_ids
		quest_state.record_objective_progress(
			objective_id,
			progress_delta,
			target_value,
			_build_event_context(event_data)
		)
		progressed_quest_ids.append(quest_id)
		return progressed_quest_ids

	for match_entry in _find_matching_active_objectives(event_data):
		var quest_state: QuestState = match_entry.get("quest_state")
		var objective_def: Dictionary = match_entry.get("objective_def", {})
		if quest_state == null or objective_def.is_empty():
			continue
		var objective_id := ProgressionDataUtils.to_string_name(objective_def.get("objective_id", ""))
		if objective_id == &"":
			continue
		var target_value := _resolve_objective_target_value(objective_def)
		if target_value <= 0:
			continue
		quest_state.record_objective_progress(
			objective_id,
			progress_delta,
			target_value,
			_build_event_context(event_data)
		)
		_append_unique_string_name(progressed_quest_ids, quest_state.quest_id)
	return progressed_quest_ids


func _did_progress_reach_target(event_data: Dictionary) -> bool:
	var quest_id := _read_required_string_name(event_data, "quest_id")
	var objective_id := _read_required_string_name(event_data, "objective_id")
	if quest_id == &"" or objective_id == &"":
		return false
	var target_value := _resolve_event_target_value(event_data, quest_id, objective_id)
	if target_value <= 0:
		return false
	var quest_state: QuestState = _party_state.get_active_quest_state(quest_id)
	return quest_state != null and quest_state.is_objective_complete(objective_id, target_value)


func _is_valid_quest_progress_event(event_data: Dictionary) -> bool:
	var event_type := _read_required_string_name(event_data, "event_type")
	if event_type != EVENT_ACCEPT and event_type != EVENT_PROGRESS and event_type != EVENT_COMPLETE:
		return false
	if not event_data.has("world_step") or event_data["world_step"] is not int:
		return false
	if event_data.has("allow_reaccept") and event_data["allow_reaccept"] is not bool:
		return false
	if event_data.has("auto_accept") and event_data["auto_accept"] is not bool:
		return false
	if event_data.has("context") and event_data["context"] is not Dictionary:
		return false
	match event_type:
		EVENT_ACCEPT, EVENT_COMPLETE:
			return _read_required_string_name(event_data, "quest_id") != &""
		EVENT_PROGRESS:
			return _is_valid_progress_event(event_data)
	return false


func _is_valid_progress_event(event_data: Dictionary) -> bool:
	if _resolve_progress_delta(event_data) <= 0:
		return false
	if event_data.has("target_value") and (event_data["target_value"] is not int or int(event_data["target_value"]) <= 0):
		return false
	if event_data.has("quest_id") or event_data.has("objective_id"):
		return _read_required_string_name(event_data, "quest_id") != &"" \
			and _read_required_string_name(event_data, "objective_id") != &""
	return _read_required_string_name(event_data, "objective_type") != &"" \
		and _read_required_string_name(event_data, "target_id") != &""


func _read_required_string_name(event_data: Dictionary, field_name: String) -> StringName:
	if not event_data.has(field_name):
		return &""
	var value: Variant = event_data[field_name]
	if value is StringName:
		return value
	if value is String:
		return StringName(value)
	return &""


func _resolve_progress_delta(event_data: Dictionary) -> int:
	if not event_data.has("progress_delta"):
		return 0
	var progress_delta_variant: Variant = event_data["progress_delta"]
	if progress_delta_variant is not int:
		return 0
	var progress_delta := int(progress_delta_variant)
	return progress_delta if progress_delta > 0 else 0


func _resolve_event_target_value(event_data: Dictionary, quest_id: StringName, objective_id: StringName) -> int:
	if objective_id == &"":
		return 0
	if event_data.has("target_value"):
		var target_value_variant: Variant = event_data["target_value"]
		if target_value_variant is not int:
			return 0
		return maxi(int(target_value_variant), 0)
	return _resolve_objective_target_value(_find_objective_def(quest_id, objective_id))


func _resolve_objective_target_value(objective_def: Dictionary) -> int:
	if objective_def.is_empty() or not objective_def.has("target_value"):
		return 0
	var target_value_variant: Variant = objective_def["target_value"]
	if target_value_variant is not int:
		return 0
	return maxi(int(target_value_variant), 0)


func _find_objective_def(quest_id: StringName, objective_id: StringName) -> Dictionary:
	if quest_id == &"" or objective_id == &"":
		return {}
	for objective_variant in _get_objective_defs(quest_id):
		if objective_variant is not Dictionary:
			continue
		var objective_def := objective_variant as Dictionary
		if ProgressionDataUtils.to_string_name(objective_def.get("objective_id", "")) == objective_id:
			return objective_def.duplicate(true)
	return {}


func _get_objective_defs(quest_id: StringName) -> Array:
	if quest_id == &"":
		return []
	var quest_def = _quest_defs.get(quest_id)
	if quest_def == null:
		return []
	var objective_defs_variant: Variant = []
	if quest_def is Dictionary:
		objective_defs_variant = (quest_def as Dictionary).get("objective_defs", [])
	elif quest_def is Object:
		objective_defs_variant = (quest_def as Object).get("objective_defs")
	if objective_defs_variant is not Array:
		return []
	return objective_defs_variant


func _find_matching_active_objectives(event_data: Dictionary) -> Array[Dictionary]:
	var matches: Array[Dictionary] = []
	var objective_type := ProgressionDataUtils.to_string_name(event_data.get("objective_type", ""))
	var target_id := ProgressionDataUtils.to_string_name(event_data.get("target_id", ""))
	if objective_type == &"":
		return matches
	for quest_state in get_active_quests():
		if quest_state == null or quest_state.quest_id == &"":
			continue
		var quest_def = _quest_defs.get(quest_state.quest_id)
		if quest_def == null:
			continue
		for objective_variant in _get_objective_defs(quest_state.quest_id):
			if objective_variant is not Dictionary:
				continue
			var objective_def: Dictionary = objective_variant
			if ProgressionDataUtils.to_string_name(objective_def.get("objective_type", "")) != objective_type:
				continue
			var objective_target_id := ProgressionDataUtils.to_string_name(objective_def.get("target_id", ""))
			if objective_target_id != &"" and target_id != &"" and objective_target_id != target_id:
				continue
			if objective_target_id != &"" and target_id == &"":
				continue
			matches.append({
				"quest_state": quest_state,
				"objective_def": objective_def.duplicate(true),
			})
	return matches


func _maybe_complete_quests_after_progress(event_data: Dictionary, world_step: int, progressed_quest_ids: Array[StringName]) -> Array[StringName]:
	var claimable_quest_ids: Array[StringName] = []
	for quest_id in progressed_quest_ids:
		var quest_state: QuestState = _party_state.get_active_quest_state(quest_id)
		var quest_def = _quest_defs.get(quest_id)
		if quest_state == null or quest_def == null:
			continue
		if quest_state.has_completed_all_objectives(quest_def):
			if complete_quest(quest_id, world_step):
				claimable_quest_ids.append(quest_id)
	return claimable_quest_ids


func _build_event_context(event_data: Dictionary) -> Dictionary:
	var context_variant = event_data.get("context", {})
	var context: Dictionary = context_variant.duplicate(true) if context_variant is Dictionary else {}
	for key in ["member_id", "action_id", "enemy_template_id", "settlement_id", "source_type", "source_id"]:
		if not event_data.has(key):
			continue
		context[key] = event_data.get(key)
	return context


func _append_unique_string_name(target_variant, value: StringName) -> void:
	if target_variant is not Array or value == &"":
		return
	var target: Array = target_variant
	if target.has(value):
		return
	target.append(value)
