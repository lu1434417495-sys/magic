class_name SubraceContentRegistry
extends "res://scripts/player/progression/identity_content_registry_base.gd"

const SUBRACE_CONFIG_DIRECTORY := "res://data/configs/subraces"
const SUBRACE_DEF_SCRIPT = preload("res://scripts/player/progression/subrace_def.gd")

var _subrace_defs: Dictionary = {}


func _init() -> void:
	_registry_label = "SubraceContentRegistry"
	rebuild()


func rebuild() -> void:
	load_from_directory(SUBRACE_CONFIG_DIRECTORY)


func load_from_directory(directory_path: String) -> void:
	_subrace_defs.clear()
	_validation_errors.clear()
	_scan_directory(directory_path)
	_validation_errors.append_array(_collect_validation_errors())


func get_subrace_defs() -> Dictionary:
	return _subrace_defs


func _register_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	if resource == null:
		_validation_errors.append("Failed to load subrace config %s." % resource_path)
		return
	if resource.get_script() != SUBRACE_DEF_SCRIPT:
		_validation_errors.append("Subrace config %s is not a SubraceDef." % resource_path)
		return

	var subrace_def := resource as SubraceDef
	if subrace_def == null:
		_validation_errors.append("Subrace config %s failed to cast to SubraceDef." % resource_path)
		return
	if subrace_def.subrace_id == &"":
		_validation_errors.append("Subrace config %s is missing subrace_id." % resource_path)
		return
	if _subrace_defs.has(subrace_def.subrace_id):
		_validation_errors.append("Duplicate subrace_id registered: %s" % String(subrace_def.subrace_id))
		return

	_subrace_defs[subrace_def.subrace_id] = subrace_def


func _collect_validation_errors() -> Array[String]:
	var errors: Array[String] = []
	for subrace_key in _sorted_registry_keys(_subrace_defs):
		var subrace_id := StringName(subrace_key)
		var subrace_def := _subrace_defs.get(subrace_id) as SubraceDef
		if subrace_def == null:
			continue
		_append_subrace_validation_errors(errors, subrace_id, subrace_def)
	return errors


func _append_subrace_validation_errors(errors: Array[String], subrace_id: StringName, subrace_def: SubraceDef) -> void:
	var owner_label := "Subrace %s" % String(subrace_id)
	_append_string_name_field_error(errors, owner_label, "subrace_id", subrace_def.subrace_id)
	_append_string_name_field_error(errors, owner_label, "parent_race_id", subrace_def.parent_race_id)
	_append_string_field_error(errors, owner_label, "display_name", subrace_def.display_name)
	_append_string_field_error(errors, owner_label, "description", subrace_def.description)
	_append_string_name_field_error(errors, owner_label, "body_size_category_override", subrace_def.body_size_category_override, true)
	_append_int_field_error(errors, owner_label, "speed_bonus", subrace_def.speed_bonus)
	_append_attribute_modifier_array_errors(errors, owner_label, subrace_def.attribute_modifiers, "attribute_modifiers")
	_append_string_name_array_errors(errors, owner_label, subrace_def.trait_ids, "trait_ids")
	_append_racial_granted_skill_array_errors(errors, owner_label, subrace_def.racial_granted_skills, "racial_granted_skills")
	_append_string_name_array_errors(errors, owner_label, subrace_def.proficiency_tags, "proficiency_tags")
	_append_string_name_array_errors(errors, owner_label, subrace_def.vision_tags, "vision_tags")
	_append_string_name_array_errors(errors, owner_label, subrace_def.save_advantage_tags, "save_advantage_tags")
	_append_string_name_to_string_name_dictionary_errors(errors, owner_label, subrace_def.damage_resistances, "damage_resistances")
	_append_string_name_array_errors(errors, owner_label, subrace_def.dialogue_tags, "dialogue_tags")
	_append_string_array_errors(errors, owner_label, subrace_def.racial_trait_summary, "racial_trait_summary")
