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


func get_completed_quest_ids() -> Array[StringName]:
	return _party_state.get_completed_quest_ids() if _party_state != null else []


func accept_quest(quest_id: StringName, world_step: int = -1, allow_reaccept: bool = false) -> bool:
	if _party_state == null or quest_id == &"":
		return false
	if not _quest_defs.is_empty() and not _quest_defs.has(quest_id):
		return false
	if _party_state.has_active_quest(quest_id):
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
	if _party_state.has_completed_quest(quest_id):
		return false
	return _party_state.mark_quest_completed(quest_id, world_step)


func apply_quest_progress_events(event_variants: Array, default_world_step: int = -1) -> Dictionary:
	var summary := {
		"accepted_quest_ids": [],
		"progressed_quest_ids": [],
		"completed_quest_ids": [],
	}
	if _party_state == null:
		return summary

	for event_variant in event_variants:
		if event_variant is not Dictionary:
			continue
		var event_data: Dictionary = (event_variant as Dictionary).duplicate(true)
		var event_type := ProgressionDataUtils.to_string_name(event_data.get("event_type", EVENT_PROGRESS))
		var quest_id := ProgressionDataUtils.to_string_name(event_data.get("quest_id", ""))
		var event_world_step := int(event_data.get("world_step", default_world_step))
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
					_append_unique_string_name(summary["completed_quest_ids"], quest_id)
			_:
				var progressed_quest_ids := _apply_progress_event(event_data, event_world_step)
				for progressed_quest_id_variant in progressed_quest_ids:
					var progressed_quest_id := ProgressionDataUtils.to_string_name(progressed_quest_id_variant)
					if progressed_quest_id == &"":
						continue
					_append_unique_string_name(summary["progressed_quest_ids"], progressed_quest_id)
				for completed_quest_id in _maybe_complete_quests_after_progress(event_data, event_world_step, progressed_quest_ids):
					_append_unique_string_name(summary["completed_quest_ids"], completed_quest_id)
	return summary


func _apply_progress_event(event_data: Dictionary, world_step: int) -> Array[StringName]:
	var progressed_quest_ids: Array[StringName] = []
	var quest_id := ProgressionDataUtils.to_string_name(event_data.get("quest_id", ""))
	var progress_delta := maxi(int(event_data.get("progress_delta", event_data.get("amount", 1))), 0)
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
		var target_value := maxi(int(event_data.get("target_value", 1)), 1)
		if objective_id == &"":
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
		var target_value := maxi(int(objective_def.get("target_value", event_data.get("target_value", 1))), 1)
		if objective_id == &"":
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
	var quest_id := ProgressionDataUtils.to_string_name(event_data.get("quest_id", ""))
	var objective_id := ProgressionDataUtils.to_string_name(event_data.get("objective_id", ""))
	var target_value := maxi(int(event_data.get("target_value", 1)), 1)
	if quest_id == &"" or objective_id == &"":
		return false
	var quest_state: QuestState = _party_state.get_active_quest_state(quest_id)
	return quest_state != null and quest_state.is_objective_complete(objective_id, target_value)


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
		for objective_variant in quest_def.objective_defs:
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
	var completed_quest_ids: Array[StringName] = []
	for quest_id in progressed_quest_ids:
		var quest_state: QuestState = _party_state.get_active_quest_state(quest_id)
		var quest_def = _quest_defs.get(quest_id)
		if quest_state == null or quest_def == null:
			continue
		if quest_state.has_completed_all_objectives(quest_def):
			if complete_quest(quest_id, world_step):
				completed_quest_ids.append(quest_id)
	return completed_quest_ids


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
