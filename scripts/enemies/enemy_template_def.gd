class_name EnemyTemplateDef
extends Resource

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const ItemContentRegistry = preload("res://scripts/player/warehouse/item_content_registry.gd")
const ItemDef = preload("res://scripts/player/warehouse/item_def.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")

const DROP_TYPE_ITEM: StringName = &"item"
const DROP_TYPE_RANDOM_EQUIPMENT: StringName = &"random_equipment"
const DROP_ENTRY_REQUIRED_FIELDS := [
	"drop_entry_id",
	"drop_type",
	"item_id",
	"quantity",
]
const UNSUPPORTED_WEAPON_ATTRIBUTE_OVERRIDE_KEYS: Array[StringName] = [
	&"weapon_attack_range",
	&"weapon_physical_damage_tag",
]
const TAG_BEAST: StringName = &"beast"
const NATURAL_WEAPON_PROFILE_TYPE_ID: StringName = &"natural_weapon"
const NATURAL_WEAPON_DEFAULT_DAMAGE_TAG: StringName = &"physical_blunt"
const NATURAL_WEAPON_DEFAULT_ATTACK_RANGE := 1

@export var template_id: StringName = &""
@export var display_name: String = ""
@export var battle_sprite_texture: Texture2D = null
@export var brain_id: StringName = &""
@export var initial_state_id: StringName = &""
@export var enemy_count := 1
@export var body_size := BattleUnitState.BODY_SIZE_MEDIUM
@export var action_threshold := BattleUnitState.DEFAULT_ACTION_THRESHOLD
@export var tags: Array[StringName] = []
@export var attack_equipment_item_id: StringName = &""
@export var natural_weapon_damage_tag: StringName = &""
@export var natural_weapon_attack_range := NATURAL_WEAPON_DEFAULT_ATTACK_RANGE
@export var base_attribute_overrides: Dictionary = {}
@export var skill_ids: Array[StringName] = []
@export var skill_level_map: Dictionary = {}
@export var attribute_overrides: Dictionary = {}
@export var target_rank: StringName = &"normal"
@export var drop_entries: Array[Dictionary] = []


func get_initial_state_id(brain) -> StringName:
	if initial_state_id != &"":
		return initial_state_id
	if brain != null and brain.has_method("has_state") and brain.has_state(brain.default_state_id):
		return brain.default_state_id
	return &"engage"


func get_drop_entries() -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for entry_variant in drop_entries:
		if entry_variant is not Dictionary:
			continue
		result.append((entry_variant as Dictionary).duplicate(true))
	return result


func has_tag(tag: StringName) -> bool:
	return tag != &"" and tags.has(tag)


func get_attack_equipment_item_id() -> StringName:
	return ProgressionDataUtils.to_string_name(attack_equipment_item_id)


func get_weapon_projection(item_defs: Dictionary = {}) -> Dictionary:
	if has_tag(TAG_BEAST):
		return get_natural_weapon_projection()
	var attack_equipment_projection := get_attack_equipment_projection(item_defs)
	if not attack_equipment_projection.is_empty():
		return attack_equipment_projection
	return get_unarmed_weapon_projection()


func get_attack_equipment_projection(item_defs: Dictionary = {}) -> Dictionary:
	var item_id := get_attack_equipment_item_id()
	if item_id == &"":
		return {}
	var item_def: ItemDef = _resolve_attack_equipment_item_def(item_id, item_defs)
	if item_def == null:
		return {}
	return _build_weapon_projection_from_item_def(item_def)


func get_natural_weapon_projection() -> Dictionary:
	if not has_tag(TAG_BEAST):
		return {}
	var attack_range := maxi(int(natural_weapon_attack_range), 1)
	return {
		"weapon_profile_kind": String(BattleUnitState.WEAPON_PROFILE_KIND_NATURAL),
		"weapon_item_id": "",
		"weapon_profile_type_id": String(NATURAL_WEAPON_PROFILE_TYPE_ID),
		"weapon_current_grip": String(BattleUnitState.WEAPON_GRIP_ONE_HANDED),
		"weapon_attack_range": attack_range,
		"weapon_one_handed_dice": _build_natural_weapon_dice(),
		"weapon_two_handed_dice": {},
		"weapon_is_versatile": false,
		"weapon_uses_two_hands": false,
		"weapon_physical_damage_tag": String(get_natural_weapon_damage_tag()),
	}


func get_unarmed_weapon_projection() -> Dictionary:
	return {
		"weapon_profile_kind": String(BattleUnitState.WEAPON_PROFILE_KIND_UNARMED),
		"weapon_item_id": "",
		"weapon_profile_type_id": "unarmed",
		"weapon_current_grip": String(BattleUnitState.WEAPON_GRIP_ONE_HANDED),
		"weapon_attack_range": 1,
		"weapon_one_handed_dice": {"dice_count": 1, "dice_sides": 4, "flat_bonus": 0},
		"weapon_two_handed_dice": {},
		"weapon_is_versatile": false,
		"weapon_uses_two_hands": false,
		"weapon_physical_damage_tag": "physical_blunt",
	}


func get_natural_weapon_damage_tag() -> StringName:
	var explicit_tag := ProgressionDataUtils.to_string_name(natural_weapon_damage_tag)
	if explicit_tag != &"":
		return explicit_tag
	for template_tag in tags:
		var mapped_tag := _natural_weapon_damage_tag_for_template_tag(template_tag)
		if mapped_tag != &"":
			return mapped_tag
	return NATURAL_WEAPON_DEFAULT_DAMAGE_TAG


func get_base_attribute_overrides() -> Dictionary:
	var resolved: Dictionary = {}
	for attribute_id in UnitBaseAttributes.BASE_ATTRIBUTE_IDS:
		if base_attribute_overrides.has(attribute_id):
			resolved[attribute_id] = int(base_attribute_overrides.get(attribute_id, 0))
			continue
		var attribute_key := String(attribute_id)
		if base_attribute_overrides.has(attribute_key):
			resolved[attribute_id] = int(base_attribute_overrides.get(attribute_key, 0))
	return resolved


func validate_schema(known_brains: Dictionary = {}, item_defs: Dictionary = {}, skill_defs: Dictionary = {}) -> Array[String]:
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
	if action_threshold <= 0:
		errors.append("Enemy template %s action_threshold must be > 0." % String(template_id))
	elif action_threshold % 5 != 0:
		errors.append("Enemy template %s action_threshold must be a multiple of 5 TU." % String(template_id))
	var normalized_target_rank := ProgressionDataUtils.to_string_name(target_rank)
	if normalized_target_rank != &"normal" and normalized_target_rank != &"elite" and normalized_target_rank != &"boss":
		errors.append(
			"Enemy template %s target_rank must be normal, elite, or boss; got %s." % [
				String(template_id),
				String(target_rank),
			]
		)
	for forbidden_key in [&"boss_target", &"fortune_mark_target"]:
		if _dictionary_has_unsupported_key(attribute_overrides, forbidden_key):
			errors.append(
				"Enemy template %s attribute_overrides must not declare %s; use target_rank instead." % [
					String(template_id),
					String(forbidden_key),
				]
			)
	if _dictionary_has_unsupported_key(attribute_overrides, &"armor_class"):
		errors.append(
			"Enemy template %s must not declare attribute_overrides.armor_class; use base attributes and AC component bonuses." % String(template_id)
		)
	errors.append_array(_validate_template_skill_ids(skill_defs))
	for unsupported_key in UNSUPPORTED_WEAPON_ATTRIBUTE_OVERRIDE_KEYS:
		if _dictionary_has_unsupported_key(attribute_overrides, unsupported_key):
			errors.append(
				"Enemy template %s must not declare attribute_overrides.%s; use attack_equipment_item_id or beast natural weapon config." % [
					String(template_id),
					String(unsupported_key),
				]
			)
	var explicit_base_attributes := get_base_attribute_overrides()
	for attribute_id in UnitBaseAttributes.BASE_ATTRIBUTE_IDS:
		if not explicit_base_attributes.has(attribute_id):
			errors.append(
				"Enemy template %s is missing base attribute %s." % [String(template_id), String(attribute_id)]
			)
			continue
		if int(explicit_base_attributes.get(attribute_id, 0)) <= 0:
			errors.append(
				"Enemy template %s base attribute %s must be > 0." % [String(template_id), String(attribute_id)]
			)
	if has_tag(TAG_BEAST):
		if int(natural_weapon_attack_range) < 1:
			errors.append(
				"Enemy template %s natural_weapon_attack_range must be >= 1." % String(template_id)
			)
		var explicit_natural_damage_tag := ProgressionDataUtils.to_string_name(natural_weapon_damage_tag)
		if explicit_natural_damage_tag != &"" and not _is_valid_weapon_physical_damage_tag(explicit_natural_damage_tag):
			errors.append(
				"Enemy template %s natural_weapon_damage_tag %s is not supported." % [
					String(template_id),
					String(explicit_natural_damage_tag),
				]
			)
	else:
		errors.append_array(_validate_attack_equipment(item_defs))
	for entry_variant in drop_entries:
		if entry_variant is not Dictionary:
			errors.append("Enemy template %s contains a non-Dictionary drop entry." % String(template_id))
			continue
		var entry_data := entry_variant as Dictionary
		if entry_data.has("drop_id"):
			errors.append("Enemy template %s drop entry must use drop_entry_id; drop_id is not supported." % String(template_id))
		if not _has_exact_drop_entry_fields(entry_data):
			errors.append("Enemy template %s drop entry must contain exactly drop_entry_id, drop_type, item_id, quantity." % String(template_id))
			continue
		var drop_entry_id := _read_required_string_name(entry_data["drop_entry_id"])
		var drop_type := _read_required_string_name(entry_data["drop_type"])
		var item_id := _read_required_string_name(entry_data["item_id"])
		if drop_entry_id == &"":
			errors.append("Enemy template %s contains a drop entry without drop_entry_id." % String(template_id))
		if drop_type != DROP_TYPE_ITEM and drop_type != DROP_TYPE_RANDOM_EQUIPMENT:
			errors.append("Enemy template %s drop %s declares unsupported drop_type %s." % [
				String(template_id),
				String(drop_entry_id),
				String(drop_type),
			])
		if item_id == &"":
			errors.append("Enemy template %s drop %s is missing item_id." % [String(template_id), String(drop_entry_id)])
		elif _resolve_item_def(item_id, item_defs) == null:
			errors.append(
				"Enemy template %s drop %s references missing item_id %s." % [
					String(template_id),
					String(drop_entry_id),
					String(item_id),
				]
			)
		if entry_data["quantity"] is not int:
			errors.append("Enemy template %s drop %s quantity must be int." % [String(template_id), String(drop_entry_id)])
		elif int(entry_data["quantity"]) <= 0:
			errors.append("Enemy template %s drop %s must have quantity >= 1." % [String(template_id), String(drop_entry_id)])
	return errors


func _validate_template_skill_ids(skill_defs: Dictionary) -> Array[String]:
	var errors: Array[String] = []
	var seen_skill_ids: Dictionary = {}
	for raw_skill_id in skill_ids:
		var skill_id := ProgressionDataUtils.to_string_name(raw_skill_id)
		if skill_id == &"":
			errors.append("Enemy template %s contains an empty skill_id." % String(template_id))
			continue
		if seen_skill_ids.has(skill_id):
			errors.append("Enemy template %s declares duplicate skill_id %s." % [String(template_id), String(skill_id)])
			continue
		seen_skill_ids[skill_id] = true
		if not skill_defs.has(skill_id):
			errors.append("Enemy template %s references missing skill %s." % [String(template_id), String(skill_id)])
	return errors


func _has_exact_drop_entry_fields(entry_data: Dictionary) -> bool:
	if entry_data.size() != DROP_ENTRY_REQUIRED_FIELDS.size():
		return false
	for field_name in DROP_ENTRY_REQUIRED_FIELDS:
		if not entry_data.has(field_name):
			return false
	return true


func _read_required_string_name(value: Variant) -> StringName:
	if value is not String and value is not StringName:
		return &""
	var text := String(value).strip_edges()
	if text.is_empty():
		return &""
	return StringName(text)


func _validate_attack_equipment(item_defs: Dictionary) -> Array[String]:
	var errors: Array[String] = []
	var item_id := get_attack_equipment_item_id()
	if item_id == &"":
		errors.append(
			"Enemy template %s must declare attack_equipment_item_id for non-beast attack equipment." % String(template_id)
		)
		return errors
	var item_def: ItemDef = _resolve_attack_equipment_item_def(item_id, item_defs)
	if item_def == null:
		errors.append(
			"Enemy template %s references missing attack_equipment_item_id %s." % [
				String(template_id),
				String(item_id),
			]
		)
		return errors
	if not item_def.is_weapon():
		errors.append(
			"Enemy template %s attack_equipment_item_id %s must reference a weapon equipment item." % [
				String(template_id),
				String(item_id),
			]
		)
		return errors
	if item_def.get_weapon_attack_range() <= 0:
		errors.append(
			"Enemy template %s attack_equipment_item_id %s must project weapon attack range >= 1." % [
				String(template_id),
				String(item_id),
			]
		)
	if item_def.get_weapon_physical_damage_tag() == &"":
		errors.append(
			"Enemy template %s attack_equipment_item_id %s must project a weapon physical damage tag." % [
				String(template_id),
				String(item_id),
			]
		)
	return errors


func _resolve_attack_equipment_item_def(item_id: StringName, item_defs: Dictionary):
	return _resolve_item_def(item_id, item_defs)


func _resolve_item_def(item_id: StringName, item_defs: Dictionary):
	if item_defs != null and item_defs.has(item_id):
		return item_defs.get(item_id) as ItemDef
	var registry := ItemContentRegistry.new()
	return registry.get_item_defs().get(item_id) as ItemDef


func _build_weapon_projection_from_item_def(item_def: ItemDef) -> Dictionary:
	if item_def == null or not item_def.is_weapon():
		return {}
	var profile = item_def.get("weapon_profile")
	if profile == null:
		return {}
	var one_handed_dice := _weapon_dice_to_dict(profile.get("one_handed_dice"))
	var two_handed_dice := _weapon_dice_to_dict(profile.get("two_handed_dice"))
	var properties := _weapon_profile_properties(profile)
	var is_versatile := properties.has(&"versatile")
	var uses_two_hands := _resolve_weapon_uses_two_hands(item_def, one_handed_dice, two_handed_dice, is_versatile)
	return {
		"weapon_profile_kind": String(BattleUnitState.WEAPON_PROFILE_KIND_EQUIPPED),
		"weapon_item_id": String(item_def.item_id),
		"weapon_profile_type_id": String(ProgressionDataUtils.to_string_name(profile.get("weapon_type_id"))),
		"weapon_current_grip": String(_resolve_weapon_current_grip(one_handed_dice, two_handed_dice, uses_two_hands)),
		"weapon_attack_range": maxi(int(profile.get("attack_range")), 0),
		"weapon_one_handed_dice": one_handed_dice,
		"weapon_two_handed_dice": two_handed_dice,
		"weapon_is_versatile": is_versatile,
		"weapon_uses_two_hands": uses_two_hands,
		"weapon_physical_damage_tag": String(item_def.get_weapon_physical_damage_tag()),
	}


func _resolve_weapon_uses_two_hands(
	item_def: ItemDef,
	one_handed_dice: Dictionary,
	two_handed_dice: Dictionary,
	is_versatile: bool
) -> bool:
	if item_def == null:
		return false
	if item_def.get_final_occupied_slot_ids(&"main_hand").has(&"off_hand"):
		return true
	if one_handed_dice.is_empty() and not two_handed_dice.is_empty():
		return true
	return is_versatile and not two_handed_dice.is_empty()


func _resolve_weapon_current_grip(
	one_handed_dice: Dictionary,
	two_handed_dice: Dictionary,
	uses_two_hands: bool
) -> StringName:
	if uses_two_hands:
		return BattleUnitState.WEAPON_GRIP_TWO_HANDED
	if not one_handed_dice.is_empty():
		return BattleUnitState.WEAPON_GRIP_ONE_HANDED
	if not two_handed_dice.is_empty():
		return BattleUnitState.WEAPON_GRIP_TWO_HANDED
	return BattleUnitState.WEAPON_GRIP_NONE


func _weapon_profile_properties(profile) -> Array[StringName]:
	var result: Array[StringName] = []
	var raw_properties: Array = []
	if profile != null and profile.has_method("get_properties"):
		raw_properties = profile.call("get_properties")
	elif profile != null:
		raw_properties = profile.get("properties")
	for raw_property in raw_properties:
		var property_id := ProgressionDataUtils.to_string_name(raw_property)
		if property_id == &"" or result.has(property_id):
			continue
		result.append(property_id)
	return result


func _weapon_dice_to_dict(dice_resource) -> Dictionary:
	if dice_resource == null:
		return {}
	var dice_count := int(dice_resource.get("dice_count"))
	var dice_sides := int(dice_resource.get("dice_sides"))
	if dice_count <= 0 or dice_sides <= 0:
		return {}
	return {
		"dice_count": dice_count,
		"dice_sides": dice_sides,
		"flat_bonus": int(dice_resource.get("flat_bonus")),
	}


func _build_natural_weapon_dice() -> Dictionary:
	return {
		"dice_count": 1,
		"dice_sides": 6,
		"flat_bonus": 0,
	}


func _natural_weapon_damage_tag_for_template_tag(tag: StringName) -> StringName:
	match ProgressionDataUtils.to_string_name(tag):
		&"bite", &"sting", &"horn":
			return &"physical_pierce"
		&"claw", &"tear":
			return &"physical_slash"
		&"slam", &"charge", &"trample":
			return &"physical_blunt"
		_:
			return &""


func _dictionary_has_key(data: Dictionary, key: StringName) -> bool:
	return data.has(String(key))


func _dictionary_has_unsupported_key(data: Dictionary, key: StringName) -> bool:
	return _dictionary_has_key(data, key) or data.has(key)


func _is_valid_weapon_physical_damage_tag(damage_tag: StringName) -> bool:
	return damage_tag == &"physical_slash" \
		or damage_tag == &"physical_pierce" \
		or damage_tag == &"physical_blunt"
