class_name EnemyAiBrainDef
extends Resource

const ENEMY_AI_STATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_state_def.gd")
const ENEMY_AI_TRANSITION_RULE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_transition_rule_def.gd")

@export var brain_id: StringName = &""
@export var default_state_id: StringName = &"engage"
@export var states: Variant = []
@export var transition_rules: Array = []


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


func get_transition_rules() -> Array:
	var result: Array = []
	for rule_variant in transition_rules:
		if rule_variant != null:
			result.append(rule_variant)
	return result


func validate_schema(skill_defs: Dictionary = {}) -> Array[String]:
	var errors: Array[String] = []
	if brain_id == &"":
		errors.append("Enemy brain is missing brain_id.")
		return errors
	if default_state_id == &"":
		errors.append("Enemy brain %s is missing default_state_id." % String(brain_id))
	if states is Array and (states as Array).is_empty():
		errors.append("Enemy brain %s must declare at least one state." % String(brain_id))
	elif states is Dictionary and (states as Dictionary).is_empty():
		errors.append("Enemy brain %s must declare at least one state." % String(brain_id))
	elif states is not Array and states is not Dictionary:
		errors.append("Enemy brain %s states must be Array or Dictionary." % String(brain_id))
	if transition_rules is not Array:
		errors.append("Enemy brain %s transition_rules must be Array." % String(brain_id))

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
		errors.append_array(state.validate_schema(brain_id, skill_defs))
	if default_state_id != &"" and not default_state_found:
		errors.append("Enemy brain %s default_state_id %s is not declared in states." % [String(brain_id), String(default_state_id)])
	errors.append_array(_validate_transition_rules(seen_state_ids))
	return errors


func _validate_transition_rules(declared_state_ids: Dictionary) -> Array[String]:
	var errors: Array[String] = []
	if transition_rules is not Array:
		return errors
	var seen_rule_ids: Dictionary = {}
	var seen_orders: Dictionary = {}
	for rule_variant in transition_rules:
		if rule_variant == null:
			errors.append("Enemy brain %s contains a null transition rule resource." % String(brain_id))
			continue
		if rule_variant.get_script() == null or rule_variant.get_script().resource_path != ENEMY_AI_TRANSITION_RULE_DEF_SCRIPT.resource_path:
			errors.append("Enemy brain %s contains an invalid transition rule resource." % String(brain_id))
			continue
		var rule = rule_variant
		var rule_id := ProgressionDataUtils.to_string_name(rule.rule_id)
		if rule_id != &"":
			if seen_rule_ids.has(rule_id):
				errors.append("Enemy brain %s declares duplicate transition rule_id %s." % [String(brain_id), String(rule_id)])
			else:
				seen_rule_ids[rule_id] = true
		var order := int(rule.order)
		if seen_orders.has(order):
			errors.append("Enemy brain %s declares duplicate transition order %d." % [String(brain_id), order])
		else:
			seen_orders[order] = true
		errors.append_array(rule.validate_schema(brain_id, declared_state_ids))
	return errors
