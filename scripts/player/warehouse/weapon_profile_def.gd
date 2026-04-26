class_name WeaponProfileDef
extends Resource

const SCRIPT = preload("res://scripts/player/warehouse/weapon_profile_def.gd")
const WeaponDamageDiceDef = preload("res://scripts/player/warehouse/weapon_damage_dice_def.gd")
const ATTACK_RANGE_INHERIT := -1

enum PropertyMergeMode {
	INHERIT,
	REPLACE,
	ADD,
	REMOVE,
}

@export var weapon_type_id: StringName = &""
@export var training_group: StringName = &""
@export var range_type: StringName = &""
@export var family: StringName = &""
@export var damage_tag: StringName = &""
@export_range(-1, 99, 1) var attack_range := ATTACK_RANGE_INHERIT
@export var one_handed_dice: WeaponDamageDiceDef = null
@export var two_handed_dice: WeaponDamageDiceDef = null
@export_enum("Inherit", "Replace", "Add", "Remove") var properties_mode: int = PropertyMergeMode.INHERIT
@export var properties: Array[StringName] = []


func merge_with_template(template_profile: WeaponProfileDef) -> WeaponProfileDef:
	return merge_profiles(template_profile, self)


func duplicate_profile() -> WeaponProfileDef:
	return merge_profiles(null, self)


func has_attack_range_override() -> bool:
	return int(attack_range) != ATTACK_RANGE_INHERIT


func get_properties() -> Array[StringName]:
	return _normalize_properties(properties)


static func merge(template_profile: WeaponProfileDef, instance_profile: WeaponProfileDef) -> WeaponProfileDef:
	return merge_profiles(template_profile, instance_profile)


static func merge_profiles(template_profile: WeaponProfileDef, instance_profile: WeaponProfileDef) -> WeaponProfileDef:
	if template_profile == null and instance_profile == null:
		return null

	var merged: WeaponProfileDef = SCRIPT.new()
	if template_profile == null:
		_copy_profile_fields(instance_profile, merged)
		merged.properties_mode = PropertyMergeMode.REPLACE
		return merged
	if instance_profile == null:
		_copy_profile_fields(template_profile, merged)
		merged.properties_mode = PropertyMergeMode.REPLACE
		return merged

	merged.weapon_type_id = _inherit_string_name(template_profile.weapon_type_id, instance_profile.weapon_type_id)
	merged.training_group = _inherit_string_name(template_profile.training_group, instance_profile.training_group)
	merged.range_type = _inherit_string_name(template_profile.range_type, instance_profile.range_type)
	merged.family = _inherit_string_name(template_profile.family, instance_profile.family)
	merged.damage_tag = _inherit_string_name(template_profile.damage_tag, instance_profile.damage_tag)
	merged.attack_range = int(instance_profile.attack_range) if instance_profile.has_attack_range_override() else int(template_profile.attack_range)
	merged.one_handed_dice = _inherit_dice(template_profile.one_handed_dice, instance_profile.one_handed_dice)
	merged.two_handed_dice = _inherit_dice(template_profile.two_handed_dice, instance_profile.two_handed_dice)
	merged.properties = _resolve_properties(template_profile.properties, instance_profile.properties, instance_profile.properties_mode)
	merged.properties_mode = PropertyMergeMode.REPLACE
	return merged


static func normalize_properties_mode(mode: Variant) -> int:
	var normalized := int(mode)
	if normalized < PropertyMergeMode.INHERIT or normalized > PropertyMergeMode.REMOVE:
		return PropertyMergeMode.INHERIT
	return normalized


static func _copy_profile_fields(source: WeaponProfileDef, target: WeaponProfileDef) -> void:
	target.weapon_type_id = source.weapon_type_id
	target.training_group = source.training_group
	target.range_type = source.range_type
	target.family = source.family
	target.damage_tag = source.damage_tag
	target.attack_range = int(source.attack_range)
	target.one_handed_dice = _duplicate_dice(source.one_handed_dice)
	target.two_handed_dice = _duplicate_dice(source.two_handed_dice)
	target.properties_mode = normalize_properties_mode(source.properties_mode)
	target.properties = _normalize_properties(source.properties)


static func _inherit_string_name(template_value: StringName, instance_value: StringName) -> StringName:
	if instance_value != &"":
		return instance_value
	return template_value


static func _inherit_dice(template_dice: WeaponDamageDiceDef, instance_dice: WeaponDamageDiceDef) -> WeaponDamageDiceDef:
	if instance_dice != null:
		return _duplicate_dice(instance_dice)
	return _duplicate_dice(template_dice)


static func _duplicate_dice(source: WeaponDamageDiceDef) -> WeaponDamageDiceDef:
	if source == null:
		return null
	return source.duplicate_dice()


static func _resolve_properties(template_properties: Array, instance_properties: Array, mode: int) -> Array[StringName]:
	match normalize_properties_mode(mode):
		PropertyMergeMode.REPLACE:
			return _normalize_properties(instance_properties)
		PropertyMergeMode.ADD:
			return _add_properties(template_properties, instance_properties)
		PropertyMergeMode.REMOVE:
			return _remove_properties(template_properties, instance_properties)
		_:
			return _normalize_properties(template_properties)


static func _add_properties(template_properties: Array, instance_properties: Array) -> Array[StringName]:
	var result := _normalize_properties(template_properties)
	var seen: Dictionary = {}
	for value in result:
		seen[value] = true
	for raw_value in instance_properties:
		var normalized := _to_string_name(raw_value)
		if normalized == &"" or seen.has(normalized):
			continue
		seen[normalized] = true
		result.append(normalized)
	return result


static func _remove_properties(template_properties: Array, instance_properties: Array) -> Array[StringName]:
	var remove_set: Dictionary = {}
	for raw_value in instance_properties:
		var normalized := _to_string_name(raw_value)
		if normalized == &"":
			continue
		remove_set[normalized] = true

	var result: Array[StringName] = []
	for raw_value in template_properties:
		var normalized := _to_string_name(raw_value)
		if normalized == &"" or remove_set.has(normalized):
			continue
		result.append(normalized)
	return result


static func _normalize_properties(raw_properties: Array) -> Array[StringName]:
	var result: Array[StringName] = []
	var seen: Dictionary = {}
	for raw_value in raw_properties:
		var normalized := _to_string_name(raw_value)
		if normalized == &"" or seen.has(normalized):
			continue
		seen[normalized] = true
		result.append(normalized)
	return result


static func _to_string_name(value: Variant) -> StringName:
	if value == null:
		return &""
	var text := str(value)
	if text.is_empty() or text == "<null>":
		return &""
	return StringName(text)
