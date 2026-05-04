class_name BloodlineContentRegistry
extends "res://scripts/player/progression/identity_content_registry_base.gd"

const BLOODLINE_CONFIG_DIRECTORY := "res://data/configs/bloodlines"
const BLOODLINE_DEF_SCRIPT = preload("res://scripts/player/progression/bloodline_def.gd")
const BLOODLINE_STAGE_DEF_SCRIPT = preload("res://scripts/player/progression/bloodline_stage_def.gd")

var _bloodline_defs: Dictionary = {}
var _bloodline_stage_defs: Dictionary = {}


func _init() -> void:
	_registry_label = "BloodlineContentRegistry"
	rebuild()


func rebuild() -> void:
	load_from_directory(BLOODLINE_CONFIG_DIRECTORY)


func load_from_directory(directory_path: String) -> void:
	_bloodline_defs.clear()
	_bloodline_stage_defs.clear()
	_validation_errors.clear()
	_scan_directory(directory_path)
	_validation_errors.append_array(_collect_validation_errors())


func get_bloodline_defs() -> Dictionary:
	return _bloodline_defs


func get_bloodline_stage_defs() -> Dictionary:
	return _bloodline_stage_defs


func _register_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	if resource == null:
		_validation_errors.append("Failed to load bloodline config %s." % resource_path)
		return
	if resource.get_script() == BLOODLINE_DEF_SCRIPT:
		_register_bloodline(resource_path, resource as BloodlineDef)
		return
	if resource.get_script() == BLOODLINE_STAGE_DEF_SCRIPT:
		_register_bloodline_stage(resource_path, resource as BloodlineStageDef)
		return
	_validation_errors.append("Bloodline config %s is not a BloodlineDef or BloodlineStageDef." % resource_path)


func _register_bloodline(resource_path: String, bloodline_def: BloodlineDef) -> void:
	if bloodline_def == null:
		_validation_errors.append("Bloodline config %s failed to cast to BloodlineDef." % resource_path)
		return
	if bloodline_def.bloodline_id == &"":
		_validation_errors.append("Bloodline config %s is missing bloodline_id." % resource_path)
		return
	if _bloodline_defs.has(bloodline_def.bloodline_id):
		_validation_errors.append("Duplicate bloodline_id registered: %s" % String(bloodline_def.bloodline_id))
		return
	_bloodline_defs[bloodline_def.bloodline_id] = bloodline_def


func _register_bloodline_stage(resource_path: String, stage_def: BloodlineStageDef) -> void:
	if stage_def == null:
		_validation_errors.append("Bloodline stage config %s failed to cast to BloodlineStageDef." % resource_path)
		return
	if stage_def.stage_id == &"":
		_validation_errors.append("Bloodline stage config %s is missing stage_id." % resource_path)
		return
	if _bloodline_stage_defs.has(stage_def.stage_id):
		_validation_errors.append("Duplicate bloodline stage_id registered: %s" % String(stage_def.stage_id))
		return
	_bloodline_stage_defs[stage_def.stage_id] = stage_def


func _collect_validation_errors() -> Array[String]:
	var errors: Array[String] = []
	for bloodline_key in _sorted_registry_keys(_bloodline_defs):
		var bloodline_id := StringName(bloodline_key)
		var bloodline_def := _bloodline_defs.get(bloodline_id) as BloodlineDef
		if bloodline_def != null:
			_append_bloodline_validation_errors(errors, bloodline_id, bloodline_def)
	for stage_key in _sorted_registry_keys(_bloodline_stage_defs):
		var stage_id := StringName(stage_key)
		var stage_def := _bloodline_stage_defs.get(stage_id) as BloodlineStageDef
		if stage_def != null:
			_append_bloodline_stage_validation_errors(errors, stage_id, stage_def)
	return errors


func _append_bloodline_validation_errors(errors: Array[String], bloodline_id: StringName, bloodline_def: BloodlineDef) -> void:
	var owner_label := "Bloodline %s" % String(bloodline_id)
	_append_string_name_field_error(errors, owner_label, "bloodline_id", bloodline_def.bloodline_id)
	_append_string_field_error(errors, owner_label, "display_name", bloodline_def.display_name)
	_append_string_field_error(errors, owner_label, "description", bloodline_def.description)
	_append_string_name_array_errors(errors, owner_label, bloodline_def.stage_ids, "stage_ids")
	_append_string_name_array_errors(errors, owner_label, bloodline_def.trait_ids, "trait_ids")
	_append_racial_granted_skill_array_errors(errors, owner_label, bloodline_def.racial_granted_skills, "racial_granted_skills")
	_append_attribute_modifier_array_errors(errors, owner_label, bloodline_def.attribute_modifiers, "attribute_modifiers")
	_append_string_array_errors(errors, owner_label, bloodline_def.trait_summary, "trait_summary")


func _append_bloodline_stage_validation_errors(errors: Array[String], stage_id: StringName, stage_def: BloodlineStageDef) -> void:
	var owner_label := "BloodlineStage %s" % String(stage_id)
	_append_string_name_field_error(errors, owner_label, "stage_id", stage_def.stage_id)
	_append_string_name_field_error(errors, owner_label, "bloodline_id", stage_def.bloodline_id)
	_append_string_field_error(errors, owner_label, "display_name", stage_def.display_name)
	_append_string_field_error(errors, owner_label, "description", stage_def.description)
	_append_attribute_modifier_array_errors(errors, owner_label, stage_def.attribute_modifiers, "attribute_modifiers")
	_append_string_name_array_errors(errors, owner_label, stage_def.trait_ids, "trait_ids")
	_append_racial_granted_skill_array_errors(errors, owner_label, stage_def.racial_granted_skills, "racial_granted_skills")
	_append_string_array_errors(errors, owner_label, stage_def.trait_summary, "trait_summary")
