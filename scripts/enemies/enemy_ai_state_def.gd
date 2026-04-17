class_name EnemyAiStateDef
extends Resource

const ENEMY_AI_ACTION_SCRIPT = preload("res://scripts/enemies/enemy_ai_action.gd")

@export var state_id: StringName = &""
@export var actions: Array = []


func get_actions() -> Array:
	var result: Array = []
	for action_variant in actions:
		if action_variant != null:
			result.append(action_variant)
	return result


func validate_schema(brain_id: StringName = &"") -> Array[String]:
	var errors: Array[String] = []
	var context_label := "Enemy state"
	if brain_id != &"":
		context_label = "Enemy brain %s state" % String(brain_id)
	if state_id == &"":
		errors.append("%s is missing state_id." % context_label)
		return errors
	if actions.is_empty():
		errors.append("%s %s must declare at least one action." % [context_label, String(state_id)])
		return errors

	var seen_action_ids: Dictionary = {}
	for action_variant in actions:
		if action_variant == null:
			errors.append("%s %s contains a null action resource." % [context_label, String(state_id)])
			continue
		if action_variant.get_script() == null or not action_variant.has_method("decide"):
			errors.append("%s %s contains an invalid action resource." % [context_label, String(state_id)])
			continue
		if action_variant.get_script().get_path() == ENEMY_AI_ACTION_SCRIPT.resource_path:
			errors.append("%s %s contains base EnemyAiAction without a concrete action type." % [context_label, String(state_id)])
			continue
		var action_id := ProgressionDataUtils.to_string_name(action_variant.action_id)
		if action_id != &"":
			if seen_action_ids.has(action_id):
				errors.append("%s %s declares duplicate action_id %s." % [context_label, String(state_id), String(action_id)])
			else:
				seen_action_ids[action_id] = true
		for action_error in action_variant.validate_schema():
			errors.append("%s %s: %s" % [context_label, String(state_id), action_error])
	return errors
