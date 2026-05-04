class_name IdentityContentRegistryBase
extends RefCounted

const AttributeModifierScript = preload("res://scripts/player/progression/attribute_modifier.gd")
const ProgressionDataUtilsScript = preload("res://scripts/player/progression/progression_data_utils.gd")
const RacialGrantedSkillScript = preload("res://scripts/player/progression/racial_granted_skill.gd")

var _registry_label := "IdentityContentRegistry"
var _validation_errors: Array[String] = []


func validate() -> Array[String]:
	return _validation_errors.duplicate()


func _scan_directory(directory_path: String) -> void:
	if not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(directory_path)):
		_validation_errors.append("%s could not find %s." % [_registry_label, directory_path])
		return

	var directory := DirAccess.open(directory_path)
	if directory == null:
		_validation_errors.append("%s could not open %s." % [_registry_label, directory_path])
		return

	directory.list_dir_begin()
	while true:
		var entry_name := directory.get_next()
		if entry_name.is_empty():
			break
		if entry_name == "." or entry_name == "..":
			continue

		var entry_path := "%s/%s" % [directory_path, entry_name]
		if directory.current_is_dir():
			_scan_directory(entry_path)
			continue
		if not entry_name.ends_with(".tres") and not entry_name.ends_with(".res"):
			continue
		_register_resource(entry_path)
	directory.list_dir_end()


func _register_resource(resource_path: String) -> void:
	_validation_errors.append("%s does not implement resource registration for %s." % [_registry_label, resource_path])


func _sorted_registry_keys(registry: Dictionary) -> Array:
	return ProgressionDataUtilsScript.sorted_string_keys(registry)


func _append_string_name_field_error(
	errors: Array[String],
	owner_label: String,
	field_label: String,
	value: Variant,
	allow_empty: bool = false
) -> void:
	if typeof(value) != TYPE_STRING_NAME:
		errors.append("%s.%s must be a StringName." % [owner_label, field_label])
		return
	if not allow_empty and StringName(value) == &"":
		errors.append("%s.%s must be a non-empty StringName." % [owner_label, field_label])


func _append_string_field_error(
	errors: Array[String],
	owner_label: String,
	field_label: String,
	value: Variant
) -> void:
	if typeof(value) != TYPE_STRING:
		errors.append("%s.%s must be a String." % [owner_label, field_label])


func _append_int_field_error(
	errors: Array[String],
	owner_label: String,
	field_label: String,
	value: Variant
) -> void:
	if typeof(value) != TYPE_INT:
		errors.append("%s.%s must be an int." % [owner_label, field_label])


func _append_bool_field_error(
	errors: Array[String],
	owner_label: String,
	field_label: String,
	value: Variant
) -> void:
	if typeof(value) != TYPE_BOOL:
		errors.append("%s.%s must be a bool." % [owner_label, field_label])


func _append_string_name_array_errors(
	errors: Array[String],
	owner_label: String,
	values: Array,
	field_label: String,
	allow_empty_values: bool = false
) -> void:
	for index in range(values.size()):
		var value: Variant = values[index]
		if typeof(value) != TYPE_STRING_NAME:
			errors.append("%s.%s[%d] must be a StringName." % [owner_label, field_label, index])
			continue
		if not allow_empty_values and StringName(value) == &"":
			errors.append("%s.%s[%d] must be a non-empty StringName." % [owner_label, field_label, index])


func _append_string_array_errors(
	errors: Array[String],
	owner_label: String,
	values: Array,
	field_label: String
) -> void:
	for index in range(values.size()):
		var value: Variant = values[index]
		if typeof(value) != TYPE_STRING:
			errors.append("%s.%s[%d] must be a String." % [owner_label, field_label, index])


func _append_string_name_to_string_name_dictionary_errors(
	errors: Array[String],
	owner_label: String,
	values: Dictionary,
	field_label: String
) -> void:
	for key in values.keys():
		if not _is_string_or_string_name(key) or String(key).strip_edges().is_empty():
			errors.append("%s.%s key %s must be a non-empty String or StringName." % [owner_label, field_label, str(key)])
		var value: Variant = values.get(key, null)
		if not _is_string_or_string_name(value) or String(value).strip_edges().is_empty():
			errors.append("%s.%s[%s] must be a non-empty String or StringName." % [owner_label, field_label, str(key)])


func _append_string_name_to_int_dictionary_errors(
	errors: Array[String],
	owner_label: String,
	values: Dictionary,
	field_label: String,
	require_non_negative: bool = false
) -> void:
	for key in values.keys():
		if not _is_string_or_string_name(key) or String(key).strip_edges().is_empty():
			errors.append("%s.%s key %s must be a non-empty String or StringName." % [owner_label, field_label, str(key)])
		var value: Variant = values.get(key, null)
		if typeof(value) != TYPE_INT:
			errors.append("%s.%s[%s] must be an int." % [owner_label, field_label, str(key)])
			continue
		if require_non_negative and int(value) < 0:
			errors.append("%s.%s[%s] must be >= 0." % [owner_label, field_label, str(key)])


func _append_attribute_modifier_array_errors(
	errors: Array[String],
	owner_label: String,
	modifiers: Array,
	field_label: String
) -> void:
	for index in range(modifiers.size()):
		var modifier := modifiers[index] as AttributeModifier
		var modifier_label := "%s.%s[%d]" % [owner_label, field_label, index]
		if modifier == null or modifier.get_script() != AttributeModifierScript:
			errors.append("%s must be an AttributeModifier." % modifier_label)
			continue
		_append_string_name_field_error(errors, modifier_label, "attribute_id", modifier.attribute_id)
		_append_string_name_field_error(errors, modifier_label, "mode", modifier.mode)
		if not [AttributeModifier.MODE_FLAT, AttributeModifier.MODE_PERCENT].has(modifier.mode):
			errors.append("%s.mode uses unsupported value %s." % [modifier_label, String(modifier.mode)])
		_append_int_field_error(errors, modifier_label, "value", modifier.value)
		_append_int_field_error(errors, modifier_label, "value_per_rank", modifier.value_per_rank)
		_append_string_name_field_error(errors, modifier_label, "source_type", modifier.source_type, true)
		_append_string_name_field_error(errors, modifier_label, "source_id", modifier.source_id, true)


func _append_racial_granted_skill_array_errors(
	errors: Array[String],
	owner_label: String,
	granted_skills: Array,
	field_label: String
) -> void:
	for index in range(granted_skills.size()):
		var granted_skill := granted_skills[index] as RacialGrantedSkill
		var skill_label := "%s.%s[%d]" % [owner_label, field_label, index]
		if granted_skill == null or granted_skill.get_script() != RacialGrantedSkillScript:
			errors.append("%s must be a RacialGrantedSkill." % skill_label)
			continue
		_append_string_name_field_error(errors, skill_label, "skill_id", granted_skill.skill_id)
		_append_int_field_error(errors, skill_label, "minimum_skill_level", granted_skill.minimum_skill_level)
		_append_int_field_error(errors, skill_label, "grant_level", granted_skill.grant_level)
		_append_string_name_field_error(errors, skill_label, "charge_kind", granted_skill.charge_kind)
		if not RacialGrantedSkill.VALID_CHARGE_KINDS.has(granted_skill.charge_kind):
			errors.append("%s.charge_kind uses unsupported value %s." % [skill_label, String(granted_skill.charge_kind)])
		_append_int_field_error(errors, skill_label, "charges", granted_skill.charges)
		if typeof(granted_skill.charges) == TYPE_INT \
				and (granted_skill.charge_kind == RacialGrantedSkill.CHARGE_KIND_PER_BATTLE \
					or granted_skill.charge_kind == RacialGrantedSkill.CHARGE_KIND_PER_TURN) \
				and int(granted_skill.charges) <= 0:
			errors.append("%s.charges must be > 0 for charge_kind %s." % [skill_label, String(granted_skill.charge_kind)])


func _is_string_or_string_name(value: Variant) -> bool:
	return typeof(value) == TYPE_STRING or typeof(value) == TYPE_STRING_NAME
