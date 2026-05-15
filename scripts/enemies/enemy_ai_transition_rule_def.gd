class_name EnemyAiTransitionRuleDef
extends Resource

const ENEMY_AI_TRANSITION_CONDITION_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_transition_condition_def.gd")

@export var rule_id: StringName = &""
@export var order := 0
@export var from_state_ids: Array[StringName] = []
@export var target_state_id: StringName = &""
@export var conditions: Array = []
@export_multiline var designer_note: String = ""


func get_conditions() -> Array:
	var result: Array = []
	for condition_variant in conditions:
		if condition_variant != null:
			result.append(condition_variant)
	return result


func applies_to_state(state_id: StringName) -> bool:
	return from_state_ids.is_empty() or from_state_ids.has(state_id)


func validate_schema(brain_id: StringName, declared_state_ids: Dictionary) -> Array[String]:
	var errors: Array[String] = []
	var owner_label := "Enemy brain %s transition rule %s" % [String(brain_id), String(rule_id)]
	if rule_id == &"":
		errors.append("Enemy brain %s contains a transition rule without rule_id." % String(brain_id))
	if target_state_id == &"":
		errors.append("%s is missing target_state_id." % owner_label)
	elif not declared_state_ids.has(target_state_id):
		errors.append("%s target_state_id %s is not declared in states." % [owner_label, String(target_state_id)])
	for from_state_id in from_state_ids:
		if from_state_id == &"":
			errors.append("%s contains empty from_state_id." % owner_label)
		elif not declared_state_ids.has(from_state_id):
			errors.append("%s from_state_id %s is not declared in states." % [owner_label, String(from_state_id)])
	if conditions.is_empty():
		errors.append("%s must declare at least one condition." % owner_label)
	for condition_variant in conditions:
		if condition_variant == null:
			errors.append("%s contains a null condition resource." % owner_label)
			continue
		if condition_variant.get_script() == null or condition_variant.get_script().resource_path != ENEMY_AI_TRANSITION_CONDITION_DEF_SCRIPT.resource_path:
			errors.append("%s contains an invalid condition resource." % owner_label)
			continue
		errors.append_array(condition_variant.validate_schema(owner_label, declared_state_ids))
	return errors


func to_signature() -> String:
	var condition_entries: Array[String] = []
	for condition in get_conditions():
		condition_entries.append(condition.to_signature())
	return "%d:%s:%s:from=[%s]:conditions=[%s]" % [
		int(order),
		String(rule_id),
		String(target_state_id),
		",".join(_string_name_array_to_strings(from_state_ids)),
		";".join(condition_entries),
	]


static func _string_name_array_to_strings(values: Array[StringName]) -> Array[String]:
	var results: Array[String] = []
	for value in values:
		results.append(String(value))
	return results
