class_name RaceContentRegistry
extends "res://scripts/player/progression/identity_content_registry_base.gd"

const RACE_CONFIG_DIRECTORY := "res://data/configs/races"
const RACE_DEF_SCRIPT = preload("res://scripts/player/progression/race_def.gd")

var _race_defs: Dictionary = {}


func _init() -> void:
	_registry_label = "RaceContentRegistry"
	rebuild()


func rebuild() -> void:
	load_from_directory(RACE_CONFIG_DIRECTORY)


func load_from_directory(directory_path: String) -> void:
	_race_defs.clear()
	_validation_errors.clear()
	_scan_directory(directory_path)
	_validation_errors.append_array(_collect_validation_errors())


func get_race_defs() -> Dictionary:
	return _race_defs.duplicate()


func _register_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	if resource == null:
		_validation_errors.append("Failed to load race config %s." % resource_path)
		return
	if resource.get_script() != RACE_DEF_SCRIPT:
		_validation_errors.append("Race config %s is not a RaceDef." % resource_path)
		return

	var race_def := resource as RaceDef
	if race_def == null:
		_validation_errors.append("Race config %s failed to cast to RaceDef." % resource_path)
		return
	if race_def.race_id == &"":
		_validation_errors.append("Race config %s is missing race_id." % resource_path)
		return
	if _race_defs.has(race_def.race_id):
		_validation_errors.append("Duplicate race_id registered: %s" % String(race_def.race_id))
		return

	_race_defs[race_def.race_id] = race_def


func _collect_validation_errors() -> Array[String]:
	var errors: Array[String] = []
	for race_key in _sorted_registry_keys(_race_defs):
		var race_id := StringName(race_key)
		var race_def := _race_defs.get(race_id) as RaceDef
		if race_def == null:
			continue
		_append_race_validation_errors(errors, race_id, race_def)
	return errors


func _append_race_validation_errors(errors: Array[String], race_id: StringName, race_def: RaceDef) -> void:
	var owner_label := "Race %s" % String(race_id)
	_append_string_name_field_error(errors, owner_label, "race_id", race_def.race_id)
	_append_string_field_error(errors, owner_label, "display_name", race_def.display_name)
	_append_string_field_error(errors, owner_label, "description", race_def.description)
	_append_string_name_field_error(errors, owner_label, "age_profile_id", race_def.age_profile_id)
	_append_string_name_field_error(errors, owner_label, "default_subrace_id", race_def.default_subrace_id)
	_append_string_name_array_errors(errors, owner_label, race_def.subrace_ids, "subrace_ids")
	_append_string_name_field_error(errors, owner_label, "body_size_category", race_def.body_size_category)
	_append_int_field_error(errors, owner_label, "base_speed", race_def.base_speed)
	_append_attribute_modifier_array_errors(errors, owner_label, race_def.attribute_modifiers, "attribute_modifiers")
	_append_string_name_array_errors(errors, owner_label, race_def.trait_ids, "trait_ids")
	_append_racial_granted_skill_array_errors(errors, owner_label, race_def.racial_granted_skills, "racial_granted_skills")
	_append_string_name_array_errors(errors, owner_label, race_def.proficiency_tags, "proficiency_tags")
	_append_string_name_array_errors(errors, owner_label, race_def.vision_tags, "vision_tags")
	_append_string_name_array_errors(errors, owner_label, race_def.save_advantage_tags, "save_advantage_tags")
	_append_string_name_to_string_name_dictionary_errors(errors, owner_label, race_def.damage_resistances, "damage_resistances")
	_append_string_name_array_errors(errors, owner_label, race_def.dialogue_tags, "dialogue_tags")
	_append_string_array_errors(errors, owner_label, race_def.racial_trait_summary, "racial_trait_summary")
