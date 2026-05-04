class_name StageAdvancementContentRegistry
extends "res://scripts/player/progression/identity_content_registry_base.gd"

const STAGE_ADVANCEMENT_CONFIG_DIRECTORY := "res://data/configs/stage_advancements"
const STAGE_ADVANCEMENT_MODIFIER_SCRIPT = preload("res://scripts/player/progression/stage_advancement_modifier.gd")

var _stage_advancement_defs: Dictionary = {}


func _init() -> void:
	_registry_label = "StageAdvancementContentRegistry"
	rebuild()


func rebuild() -> void:
	load_from_directory(STAGE_ADVANCEMENT_CONFIG_DIRECTORY)


func load_from_directory(directory_path: String) -> void:
	_stage_advancement_defs.clear()
	_validation_errors.clear()
	_scan_directory(directory_path)
	_validation_errors.append_array(_collect_validation_errors())


func get_stage_advancement_defs() -> Dictionary:
	return _stage_advancement_defs


func _register_resource(resource_path: String) -> void:
	var resource := load(resource_path)
	if resource == null:
		_validation_errors.append("Failed to load stage advancement config %s." % resource_path)
		return
	if resource.get_script() != STAGE_ADVANCEMENT_MODIFIER_SCRIPT:
		_validation_errors.append("Stage advancement config %s is not a StageAdvancementModifier." % resource_path)
		return

	var modifier := resource as StageAdvancementModifier
	if modifier == null:
		_validation_errors.append("Stage advancement config %s failed to cast to StageAdvancementModifier." % resource_path)
		return
	if modifier.modifier_id == &"":
		_validation_errors.append("Stage advancement config %s is missing modifier_id." % resource_path)
		return
	if _stage_advancement_defs.has(modifier.modifier_id):
		_validation_errors.append("Duplicate stage advancement modifier_id registered: %s" % String(modifier.modifier_id))
		return

	_stage_advancement_defs[modifier.modifier_id] = modifier


func _collect_validation_errors() -> Array[String]:
	var errors: Array[String] = []
	for modifier_key in _sorted_registry_keys(_stage_advancement_defs):
		var modifier_id := StringName(modifier_key)
		var modifier := _stage_advancement_defs.get(modifier_id) as StageAdvancementModifier
		if modifier == null:
			continue
		_append_stage_advancement_validation_errors(errors, modifier_id, modifier)
	return errors


func _append_stage_advancement_validation_errors(
	errors: Array[String],
	modifier_id: StringName,
	modifier: StageAdvancementModifier
) -> void:
	var owner_label := "StageAdvancement %s" % String(modifier_id)
	_append_string_name_field_error(errors, owner_label, "modifier_id", modifier.modifier_id)
	_append_string_field_error(errors, owner_label, "display_name", modifier.display_name)
	_append_string_name_field_error(errors, owner_label, "target_axis", modifier.target_axis)
	if not StageAdvancementModifier.VALID_TARGET_AXES.has(modifier.target_axis):
		errors.append("%s uses unsupported target_axis %s." % [owner_label, String(modifier.target_axis)])
	_append_int_field_error(errors, owner_label, "stage_offset", modifier.stage_offset)
	_append_string_name_field_error(errors, owner_label, "max_stage_id", modifier.max_stage_id, true)
	_append_string_name_array_errors(errors, owner_label, modifier.applies_to_race_ids, "applies_to_race_ids")
	_append_string_name_array_errors(errors, owner_label, modifier.applies_to_subrace_ids, "applies_to_subrace_ids")
	_append_string_name_array_errors(errors, owner_label, modifier.applies_to_bloodline_ids, "applies_to_bloodline_ids")
	_append_string_name_array_errors(errors, owner_label, modifier.applies_to_ascension_ids, "applies_to_ascension_ids")
	_append_bool_field_error(errors, owner_label, "grants_attributes", modifier.grants_attributes)
	_append_bool_field_error(errors, owner_label, "grants_traits", modifier.grants_traits)
	_append_bool_field_error(errors, owner_label, "grants_body_size_change", modifier.grants_body_size_change)
