class_name BattleAiRuntimeActionPlan
extends RefCounted

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

var unit_id: StringName = &""
var brain_id: StringName = &""
var fingerprint: String = ""
var state_ids: Array[StringName] = []
var actions_by_state: Dictionary = {}
var generated_actions_by_state: Dictionary = {}
var metadata_by_instance_id: Dictionary = {}
var skill_affordance_records_by_skill_id: Dictionary = {}
var warnings: Array[String] = []
var errors: Array[String] = []


func set_source(unit_state: BattleUnitState, brain, skill_defs: Dictionary) -> void:
	unit_id = unit_state.unit_id if unit_state != null else &""
	brain_id = brain.brain_id if brain != null else &""
	fingerprint = build_fingerprint(unit_state, brain, skill_defs)


func add_state_actions(state_id: StringName, actions: Array) -> void:
	var normalized_state_id := ProgressionDataUtils.to_string_name(state_id)
	if normalized_state_id == &"":
		return
	_ensure_state(normalized_state_id)
	var copied_actions: Array = []
	for action in actions:
		if action == null:
			continue
		copied_actions.append(action)
		if get_action_metadata(action).is_empty():
			set_action_metadata(action, {
				"generated": false,
				"state_id": normalized_state_id,
				"action_id": ProgressionDataUtils.to_string_name(action.get("action_id")),
				"score_bucket_id": ProgressionDataUtils.to_string_name(action.get("score_bucket_id")),
			})
	actions_by_state[normalized_state_id] = copied_actions


func add_action(state_id: StringName, action, metadata: Dictionary = {}) -> void:
	if action == null:
		return
	var normalized_state_id := ProgressionDataUtils.to_string_name(state_id)
	if normalized_state_id == &"":
		return
	_ensure_state(normalized_state_id)
	actions_by_state[normalized_state_id].append(action)
	var resolved_metadata := metadata.duplicate(true)
	resolved_metadata["state_id"] = normalized_state_id
	if not resolved_metadata.has("action_id"):
		resolved_metadata["action_id"] = ProgressionDataUtils.to_string_name(action.get("action_id"))
	if not resolved_metadata.has("score_bucket_id"):
		resolved_metadata["score_bucket_id"] = ProgressionDataUtils.to_string_name(action.get("score_bucket_id"))
	set_action_metadata(action, resolved_metadata)
	if bool(resolved_metadata.get("generated", false)):
		if not generated_actions_by_state.has(normalized_state_id):
			generated_actions_by_state[normalized_state_id] = []
		generated_actions_by_state[normalized_state_id].append(action)


func get_actions(state_id: StringName) -> Array:
	var normalized_state_id := ProgressionDataUtils.to_string_name(state_id)
	var actions = actions_by_state.get(normalized_state_id, [])
	return actions.duplicate() if actions is Array else []


func has_state(state_id: StringName) -> bool:
	return actions_by_state.has(ProgressionDataUtils.to_string_name(state_id))


func is_empty_state(state_id: StringName) -> bool:
	return has_state(state_id) and get_actions(state_id).is_empty()


func set_action_metadata(action, metadata: Dictionary) -> void:
	if action == null:
		return
	metadata_by_instance_id[action.get_instance_id()] = metadata.duplicate(true)


func get_action_metadata(action) -> Dictionary:
	if action == null:
		return {}
	var metadata = metadata_by_instance_id.get(action.get_instance_id(), {})
	return metadata.duplicate(true) if metadata is Dictionary else {}


func set_skill_affordance_record(skill_id: StringName, record: Dictionary) -> void:
	var normalized_skill_id := ProgressionDataUtils.to_string_name(skill_id)
	if normalized_skill_id == &"":
		return
	var copied_record := record.duplicate(true)
	copied_record["skill_id"] = normalized_skill_id
	skill_affordance_records_by_skill_id[normalized_skill_id] = copied_record


func get_skill_affordance_record(skill_id: StringName) -> Dictionary:
	var normalized_skill_id := ProgressionDataUtils.to_string_name(skill_id)
	if normalized_skill_id == &"":
		return {}
	var record = skill_affordance_records_by_skill_id.get(normalized_skill_id, {})
	return record.duplicate(true) if record is Dictionary else {}


func validate() -> Array[String]:
	var validation_errors: Array[String] = []
	if unit_id == &"":
		validation_errors.append("Runtime action plan is missing unit_id.")
	if brain_id == &"":
		validation_errors.append("Runtime action plan is missing brain_id.")
	for state_id in state_ids:
		var actions = actions_by_state.get(state_id, [])
		if actions is not Array:
			validation_errors.append("Runtime action plan state %s actions payload is invalid." % String(state_id))
			continue
		for action in actions:
			if action == null:
				validation_errors.append("Runtime action plan state %s contains null action." % String(state_id))
				continue
			if get_action_metadata(action).is_empty():
				validation_errors.append("Runtime action plan action %s is missing metadata." % String(action.get("action_id")))
	errors = validation_errors.duplicate()
	return validation_errors


func is_stale_for(unit_state: BattleUnitState, brain, skill_defs: Dictionary) -> bool:
	return fingerprint != build_fingerprint(unit_state, brain, skill_defs)


static func build_fingerprint(unit_state: BattleUnitState, brain, skill_defs: Dictionary) -> String:
	var parts: Array[String] = []
	parts.append("unit=%s" % String(unit_state.unit_id if unit_state != null else &""))
	parts.append("brain=%s" % String(brain.brain_id if brain != null else &""))
	parts.append("skills=%s" % _build_skill_signature(unit_state))
	parts.append("brain_shape=%s" % _build_brain_shape_signature(brain))
	return "|".join(parts)


func _ensure_state(state_id: StringName) -> void:
	if not state_ids.has(state_id):
		state_ids.append(state_id)
	if not actions_by_state.has(state_id):
		actions_by_state[state_id] = []


static func _build_skill_signature(unit_state: BattleUnitState) -> String:
	if unit_state == null:
		return ""
	var entries: Array[String] = []
	for raw_skill_id in unit_state.known_active_skill_ids:
		var skill_id := ProgressionDataUtils.to_string_name(raw_skill_id)
		if skill_id == &"":
			continue
		var level := int(unit_state.known_skill_level_map.get(skill_id, 1))
		entries.append("%s:%d" % [String(skill_id), level])
	entries.sort()
	return ",".join(entries)


static func _build_brain_shape_signature(brain) -> String:
	if brain == null or not brain.has_method("get_states"):
		return ""
	var state_entries: Array[String] = []
	for state_def in brain.get_states():
		if state_def == null:
			continue
		var action_entries: Array[String] = []
		for action in state_def.get_actions():
			if action == null:
				continue
			var declared_skill_ids: Array[String] = []
			if action.has_method("get_declared_skill_ids"):
				for skill_id in action.get_declared_skill_ids():
					declared_skill_ids.append(String(skill_id))
				declared_skill_ids.sort()
			var script_path: String = action.get_script().resource_path if action.get_script() != null else ""
			action_entries.append("%s:%s:%s:%s" % [
				String(ProgressionDataUtils.to_string_name(action.get("action_id"))),
				script_path,
				String(ProgressionDataUtils.to_string_name(action.get("score_bucket_id"))),
				",".join(declared_skill_ids),
			])
		var slot_entries: Array[String] = []
		var generation_slots = state_def.get("generation_slots")
		if generation_slots is Array:
			for slot in generation_slots:
				if slot != null and slot.has_method("to_signature"):
					slot_entries.append(str(slot.to_signature()))
		state_entries.append("%s{actions=[%s];slots=[%s]}" % [
			String(state_def.state_id),
			";".join(action_entries),
			";".join(slot_entries),
		])
	var transition_entries: Array[String] = []
	if brain.has_method("get_transition_rules"):
		for rule in brain.get_transition_rules():
			if rule != null and rule.has_method("to_signature"):
				transition_entries.append(str(rule.to_signature()))
	transition_entries.sort()
	return "states=%s|transitions=%s" % [
		"||".join(state_entries),
		"||".join(transition_entries),
	]
