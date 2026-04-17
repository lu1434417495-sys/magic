class_name EnemyAiBrainDef
extends Resource

const ENEMY_AI_STATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_state_def.gd")

@export var brain_id: StringName = &""
@export var default_state_id: StringName = &"engage"
@export var retreat_hp_ratio := 0.35
@export var support_hp_ratio := 0.55
@export var pressure_distance := 2
@export var states: Variant = []


func get_state(state_id: StringName):
	for state in get_states():
		if state.state_id == state_id:
			return state
	return null


func has_state(state_id: StringName) -> bool:
	return get_state(state_id) != null


func get_states() -> Array[EnemyAiStateDef]:
	var result: Array[EnemyAiStateDef] = []
	if states is Dictionary:
		for state_variant in (states as Dictionary).values():
			var state = state_variant as EnemyAiStateDef
			if state != null:
				result.append(state)
		return result
	if states is not Array:
		return result
	for state_variant in states:
		var state = state_variant as EnemyAiStateDef
		if state != null:
			result.append(state)
	return result


func validate_schema() -> Array[String]:
	var errors: Array[String] = []
	if brain_id == &"":
		errors.append("Enemy brain is missing brain_id.")
		return errors
	if default_state_id == &"":
		errors.append("Enemy brain %s is missing default_state_id." % String(brain_id))
	if retreat_hp_ratio < 0.0 or retreat_hp_ratio > 1.0:
		errors.append("Enemy brain %s retreat_hp_ratio must be within [0, 1]." % String(brain_id))
	if support_hp_ratio < 0.0 or support_hp_ratio > 1.0:
		errors.append("Enemy brain %s support_hp_ratio must be within [0, 1]." % String(brain_id))
	if pressure_distance < 0:
		errors.append("Enemy brain %s pressure_distance must be >= 0." % String(brain_id))
	if states is Array and (states as Array).is_empty():
		errors.append("Enemy brain %s must declare at least one state." % String(brain_id))
	elif states is Dictionary and (states as Dictionary).is_empty():
		errors.append("Enemy brain %s must declare at least one state." % String(brain_id))
	elif states is not Array and states is not Dictionary:
		errors.append("Enemy brain %s states must be Array or Dictionary." % String(brain_id))

	var seen_state_ids: Dictionary = {}
	var default_state_found := false
	for state_variant in get_states():
		if state_variant == null:
			errors.append("Enemy brain %s contains a null state resource." % String(brain_id))
			continue
		if state_variant.get_script() != ENEMY_AI_STATE_DEF_SCRIPT:
			errors.append("Enemy brain %s contains a non-EnemyAiStateDef state resource." % String(brain_id))
			continue
		var state := state_variant as EnemyAiStateDef
		if state.state_id == &"":
			errors.append("Enemy brain %s contains a state without state_id." % String(brain_id))
		elif seen_state_ids.has(state.state_id):
			errors.append("Enemy brain %s declares duplicate state_id %s." % [String(brain_id), String(state.state_id)])
		else:
			seen_state_ids[state.state_id] = true
		if state.state_id == default_state_id:
			default_state_found = true
		errors.append_array(state.validate_schema(brain_id))
	if default_state_id != &"" and not default_state_found:
		errors.append("Enemy brain %s default_state_id %s is not declared in states." % [String(brain_id), String(default_state_id)])
	return errors
