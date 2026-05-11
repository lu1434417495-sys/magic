class_name AgeContentRegistry
extends "res://scripts/player/progression/identity_content_registry_base.gd"

const AGE_PROFILE_CONFIG_DIRECTORY := "res://data/configs/age_profiles"
const AGE_PROFILE_DEF_SCRIPT = preload("res://scripts/player/progression/age_profile_def.gd")
const AGE_STAGE_RULE_SCRIPT = preload("res://scripts/player/progression/age_stage_rule.gd")

var _age_profile_defs: Dictionary = {}


func _init() -> void:
	_registry_label = "AgeContentRegistry"
	rebuild()


func rebuild() -> void:
	load_from_directory(AGE_PROFILE_CONFIG_DIRECTORY)


func load_from_directory(directory_path: String) -> void:
	_age_profile_defs.clear()
	_validation_errors.clear()
	_scan_directory(directory_path)
	_validation_errors.append_array(_collect_validation_errors())


func get_age_profile_defs() -> Dictionary:
	return _age_profile_defs.duplicate()


func _register_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	if resource == null:
		_validation_errors.append("Failed to load age profile config %s." % resource_path)
		return
	if resource.get_script() != AGE_PROFILE_DEF_SCRIPT:
		_validation_errors.append("Age profile config %s is not an AgeProfileDef." % resource_path)
		return

	var profile_def := resource as AgeProfileDef
	if profile_def == null:
		_validation_errors.append("Age profile config %s failed to cast to AgeProfileDef." % resource_path)
		return
	if profile_def.profile_id == &"":
		_validation_errors.append("Age profile config %s is missing profile_id." % resource_path)
		return
	if _age_profile_defs.has(profile_def.profile_id):
		_validation_errors.append("Duplicate age profile_id registered: %s" % String(profile_def.profile_id))
		return

	_age_profile_defs[profile_def.profile_id] = profile_def


func _collect_validation_errors() -> Array[String]:
	var errors: Array[String] = []
	for profile_key in _sorted_registry_keys(_age_profile_defs):
		var profile_id := StringName(profile_key)
		var profile_def := _age_profile_defs.get(profile_id) as AgeProfileDef
		if profile_def == null:
			continue
		_append_age_profile_validation_errors(errors, profile_id, profile_def)
	return errors


func _append_age_profile_validation_errors(errors: Array[String], profile_id: StringName, profile_def: AgeProfileDef) -> void:
	var owner_label := "AgeProfile %s" % String(profile_id)
	_append_string_name_field_error(errors, owner_label, "profile_id", profile_def.profile_id)
	_append_string_name_field_error(errors, owner_label, "race_id", profile_def.race_id)
	for field_label in [
		"child_age",
		"teen_age",
		"young_adult_age",
		"adult_age",
		"middle_age",
		"old_age",
		"venerable_age",
		"max_natural_age",
	]:
		var value: Variant = profile_def.get(field_label)
		_append_int_field_error(errors, owner_label, field_label, value)
	_append_age_stage_rule_errors(errors, owner_label, profile_def.stage_rules, "stage_rules")
	_append_string_name_array_errors(errors, owner_label, profile_def.creation_stage_ids, "creation_stage_ids")
	_append_string_name_to_int_dictionary_errors(errors, owner_label, profile_def.default_age_by_stage, "default_age_by_stage")


func _append_age_stage_rule_errors(
	errors: Array[String],
	owner_label: String,
	stage_rules: Array,
	field_label: String
) -> void:
	var seen_stage_ids: Dictionary = {}
	for index in range(stage_rules.size()):
		var stage_rule := stage_rules[index] as AgeStageRule
		var stage_label := "%s.%s[%d]" % [owner_label, field_label, index]
		if stage_rule == null or stage_rule.get_script() != AGE_STAGE_RULE_SCRIPT:
			errors.append("%s must be an AgeStageRule." % stage_label)
			continue
		_append_string_name_field_error(errors, stage_label, "stage_id", stage_rule.stage_id)
		if stage_rule.stage_id != &"":
			if seen_stage_ids.has(stage_rule.stage_id):
				errors.append("%s declares duplicate stage_id %s." % [owner_label, String(stage_rule.stage_id)])
			else:
				seen_stage_ids[stage_rule.stage_id] = true
		_append_string_field_error(errors, stage_label, "display_name", stage_rule.display_name)
		_append_string_field_error(errors, stage_label, "description", stage_rule.description)
		_append_attribute_modifier_array_errors(errors, stage_label, stage_rule.attribute_modifiers, "attribute_modifiers")
		_append_string_name_array_errors(errors, stage_label, stage_rule.trait_ids, "trait_ids")
		_append_string_array_errors(errors, stage_label, stage_rule.trait_summary, "trait_summary")
		_append_bool_field_error(errors, stage_label, "selectable_in_creation", stage_rule.selectable_in_creation)
		_append_bool_field_error(errors, stage_label, "reachable_by_aging", stage_rule.reachable_by_aging)
