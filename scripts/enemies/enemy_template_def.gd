class_name EnemyTemplateDef
extends RefCounted

var template_id: StringName = &""
var display_name: String = ""
var brain_id: StringName = &""
var initial_state_id: StringName = &""
var enemy_count := 1
var body_size := 1
var skill_ids: Array[StringName] = []
var skill_level_map: Dictionary = {}
var attribute_overrides: Dictionary = {}


func get_initial_state_id(brain) -> StringName:
	if initial_state_id != &"":
		return initial_state_id
	if brain != null and brain.has_method("has_state") and brain.has_state(brain.default_state_id):
		return brain.default_state_id
	return &"engage"
