class_name EnemyAiStateDef
extends Resource

const ENEMY_AI_ACTION_SCRIPT = preload("res://scripts/enemies/enemy_ai_action.gd")
const ENEMY_AI_GENERATION_SLOT_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_generation_slot_def.gd")

@export var state_id: StringName = &""
@export var actions: Array = []
@export var generation_slots: Array = []


func get_actions() -> Array:
	var result: Array = []
	for action_variant in actions:
		if action_variant != null:
			result.append(action_variant)
	return result


func validate_schema(brain_id: StringName = &"", skill_defs: Dictionary = {}) -> Array[String]:
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
		if action_variant.has_method("validate_skill_references"):
			for action_skill_error in action_variant.validate_skill_references(skill_defs):
				errors.append("%s %s: %s" % [context_label, String(state_id), action_skill_error])
	errors.append_array(_validate_generation_slots(context_label))
	return errors


func get_generation_slots() -> Array:
	var result: Array = []
	for slot_variant in generation_slots:
		if slot_variant != null:
			result.append(slot_variant)
	return result


func _validate_generation_slots(context_label: String) -> Array[String]:
	var errors: Array[String] = []
	var seen_slot_ids: Dictionary = {}
	var seen_orders: Dictionary = {}
	for slot_variant in generation_slots:
		if slot_variant == null:
			errors.append("%s %s contains a null generation slot resource." % [context_label, String(state_id)])
			continue
		if slot_variant.get_script() == null or not slot_variant.has_method("matches_affordance"):
			errors.append("%s %s contains an invalid generation slot resource." % [context_label, String(state_id)])
			continue
		if slot_variant.get_script().get_path() != ENEMY_AI_GENERATION_SLOT_DEF_SCRIPT.resource_path:
			errors.append("%s %s contains unsupported generation slot type." % [context_label, String(state_id)])
			continue
		var slot_id := ProgressionDataUtils.to_string_name(slot_variant.slot_id)
		if slot_id != &"":
			if seen_slot_ids.has(slot_id):
				errors.append("%s %s declares duplicate generation slot_id %s." % [context_label, String(state_id), String(slot_id)])
			else:
				seen_slot_ids[slot_id] = true
		var order := int(slot_variant.order)
		if seen_orders.has(order):
			errors.append("%s %s declares duplicate generation slot order %d." % [context_label, String(state_id), order])
		else:
			seen_orders[order] = true
		for slot_error in slot_variant.validate_schema("%s %s" % [context_label, String(state_id)], get_actions()):
			errors.append("%s %s: %s" % [context_label, String(state_id), slot_error])
	return errors
