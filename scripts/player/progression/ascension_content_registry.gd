class_name AscensionContentRegistry
extends "res://scripts/player/progression/identity_content_registry_base.gd"

const ASCENSION_CONFIG_DIRECTORY := "res://data/configs/ascensions"
const ASCENSION_DEF_SCRIPT = preload("res://scripts/player/progression/ascension_def.gd")
const ASCENSION_STAGE_DEF_SCRIPT = preload("res://scripts/player/progression/ascension_stage_def.gd")

var _ascension_defs: Dictionary = {}
var _ascension_stage_defs: Dictionary = {}


func _init() -> void:
	_registry_label = "AscensionContentRegistry"
	rebuild()


func rebuild() -> void:
	load_from_directory(ASCENSION_CONFIG_DIRECTORY)


func load_from_directory(directory_path: String) -> void:
	_ascension_defs.clear()
	_ascension_stage_defs.clear()
	_validation_errors.clear()
	_scan_directory(directory_path)
	_validation_errors.append_array(_collect_validation_errors())


func get_ascension_defs() -> Dictionary:
	return _ascension_defs


func get_ascension_stage_defs() -> Dictionary:
	return _ascension_stage_defs


func _register_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	if resource == null:
		_validation_errors.append("Failed to load ascension config %s." % resource_path)
		return
	if resource.get_script() == ASCENSION_DEF_SCRIPT:
		_register_ascension(resource_path, resource as AscensionDef)
		return
	if resource.get_script() == ASCENSION_STAGE_DEF_SCRIPT:
		_register_ascension_stage(resource_path, resource as AscensionStageDef)
		return
	_validation_errors.append("Ascension config %s is not an AscensionDef or AscensionStageDef." % resource_path)


func _register_ascension(resource_path: String, ascension_def: AscensionDef) -> void:
	if ascension_def == null:
		_validation_errors.append("Ascension config %s failed to cast to AscensionDef." % resource_path)
		return
	if ascension_def.ascension_id == &"":
		_validation_errors.append("Ascension config %s is missing ascension_id." % resource_path)
		return
	if _ascension_defs.has(ascension_def.ascension_id):
		_validation_errors.append("Duplicate ascension_id registered: %s" % String(ascension_def.ascension_id))
		return
	_ascension_defs[ascension_def.ascension_id] = ascension_def


func _register_ascension_stage(resource_path: String, stage_def: AscensionStageDef) -> void:
	if stage_def == null:
		_validation_errors.append("Ascension stage config %s failed to cast to AscensionStageDef." % resource_path)
		return
	if stage_def.stage_id == &"":
		_validation_errors.append("Ascension stage config %s is missing stage_id." % resource_path)
		return
	if _ascension_stage_defs.has(stage_def.stage_id):
		_validation_errors.append("Duplicate ascension stage_id registered: %s" % String(stage_def.stage_id))
		return
	_ascension_stage_defs[stage_def.stage_id] = stage_def


func _collect_validation_errors() -> Array[String]:
	var errors: Array[String] = []
	for ascension_key in _sorted_registry_keys(_ascension_defs):
		var ascension_id := StringName(ascension_key)
		var ascension_def := _ascension_defs.get(ascension_id) as AscensionDef
		if ascension_def != null:
			_append_ascension_validation_errors(errors, ascension_id, ascension_def)
	for stage_key in _sorted_registry_keys(_ascension_stage_defs):
		var stage_id := StringName(stage_key)
		var stage_def := _ascension_stage_defs.get(stage_id) as AscensionStageDef
		if stage_def != null:
			_append_ascension_stage_validation_errors(errors, stage_id, stage_def)
	return errors


func _append_ascension_validation_errors(errors: Array[String], ascension_id: StringName, ascension_def: AscensionDef) -> void:
	var owner_label := "Ascension %s" % String(ascension_id)
	_append_string_name_field_error(errors, owner_label, "ascension_id", ascension_def.ascension_id)
	_append_string_field_error(errors, owner_label, "display_name", ascension_def.display_name)
	_append_string_field_error(errors, owner_label, "description", ascension_def.description)
	_append_string_name_array_errors(errors, owner_label, ascension_def.stage_ids, "stage_ids")
	_append_string_name_array_errors(errors, owner_label, ascension_def.trait_ids, "trait_ids")
	_append_racial_granted_skill_array_errors(errors, owner_label, ascension_def.racial_granted_skills, "racial_granted_skills")
	_append_string_name_array_errors(errors, owner_label, ascension_def.allowed_race_ids, "allowed_race_ids")
	_append_string_name_array_errors(errors, owner_label, ascension_def.allowed_subrace_ids, "allowed_subrace_ids")
	_append_string_name_array_errors(errors, owner_label, ascension_def.allowed_bloodline_ids, "allowed_bloodline_ids")
	_append_string_array_errors(errors, owner_label, ascension_def.trait_summary, "trait_summary")
	_append_bool_field_error(errors, owner_label, "replaces_age_growth", ascension_def.replaces_age_growth)
	_append_bool_field_error(errors, owner_label, "suppresses_original_race_traits", ascension_def.suppresses_original_race_traits)


func _append_ascension_stage_validation_errors(errors: Array[String], stage_id: StringName, stage_def: AscensionStageDef) -> void:
	var owner_label := "AscensionStage %s" % String(stage_id)
	_append_string_name_field_error(errors, owner_label, "stage_id", stage_def.stage_id)
	_append_string_name_field_error(errors, owner_label, "ascension_id", stage_def.ascension_id)
	_append_string_field_error(errors, owner_label, "display_name", stage_def.display_name)
	_append_string_field_error(errors, owner_label, "description", stage_def.description)
	_append_attribute_modifier_array_errors(errors, owner_label, stage_def.attribute_modifiers, "attribute_modifiers")
	_append_string_name_array_errors(errors, owner_label, stage_def.trait_ids, "trait_ids")
	_append_racial_granted_skill_array_errors(errors, owner_label, stage_def.racial_granted_skills, "racial_granted_skills")
	_append_int_field_error(errors, owner_label, "body_size_override", stage_def.body_size_override)
	_append_string_array_errors(errors, owner_label, stage_def.trait_summary, "trait_summary")
