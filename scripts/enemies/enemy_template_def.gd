class_name EnemyTemplateDef
extends Resource

@export var template_id: StringName = &""
@export var display_name: String = ""
@export var brain_id: StringName = &""
@export var initial_state_id: StringName = &""
@export var enemy_count := 1
@export var body_size := 1
@export var skill_ids: Array[StringName] = []
@export var skill_level_map: Dictionary = {}
@export var attribute_overrides: Dictionary = {}


func get_initial_state_id(brain) -> StringName:
	if initial_state_id != &"":
		return initial_state_id
	if brain != null and brain.has_method("has_state") and brain.has_state(brain.default_state_id):
		return brain.default_state_id
	return &"engage"


func validate_schema(known_brains: Dictionary = {}) -> Array[String]:
	var errors: Array[String] = []
	if template_id == &"":
		errors.append("Enemy template is missing template_id.")
		return errors
	if display_name.strip_edges().is_empty():
		errors.append("Enemy template %s is missing display_name." % String(template_id))
	if brain_id == &"":
		errors.append("Enemy template %s is missing brain_id." % String(template_id))
	else:
		var brain = known_brains.get(brain_id)
		if brain == null:
			errors.append("Enemy template %s references missing brain %s." % [String(template_id), String(brain_id)])
		elif initial_state_id != &"" and not brain.has_state(initial_state_id):
			errors.append(
				"Enemy template %s initial_state_id %s is not declared by brain %s." % [
					String(template_id),
					String(initial_state_id),
					String(brain_id),
				]
			)
	if enemy_count <= 0:
		errors.append("Enemy template %s must have enemy_count >= 1." % String(template_id))
	if body_size <= 0:
		errors.append("Enemy template %s must have body_size >= 1." % String(template_id))
	return errors
