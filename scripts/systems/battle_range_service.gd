class_name BattleRangeService
extends RefCounted

const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")

const STATUS_ARCHER_RANGE_UP: StringName = &"archer_range_up"


static func get_weapon_attack_range(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return 0
	return unit_state.get_weapon_attack_range()


static func unit_has_melee_weapon(unit_state: BattleUnitState) -> bool:
	return unit_state != null \
		and unit_state.weapon_profile_kind == BattleUnitState.WEAPON_PROFILE_KIND_EQUIPPED \
		and get_weapon_attack_range(unit_state) > 0 \
		and ProgressionDataUtils.to_string_name(unit_state.weapon_physical_damage_tag if unit_state != null else &"") != &""


static func get_effective_skill_range(unit_state: BattleUnitState, skill_def) -> int:
	if skill_def == null or skill_def.combat_profile == null:
		return 0
	var skill_range := resolve_base_skill_range(unit_state, skill_def)
	skill_range += _get_range_modifier_bonus(unit_state, skill_def)
	return maxi(skill_range, 0)


static func requires_current_melee_weapon(skill_def) -> bool:
	if skill_def == null or skill_def.combat_profile == null:
		return false
	for effect_def in skill_def.combat_profile.effect_defs:
		if effect_requires_weapon(effect_def):
			return true
	for cast_variant in skill_def.combat_profile.cast_variants:
		if cast_variant == null:
			continue
		for effect_def in cast_variant.effect_defs:
			if effect_requires_weapon(effect_def):
				return true
	return false


static func is_weapon_range_skill(skill_def) -> bool:
	return _skill_has_tag(skill_def, &"melee") or _skill_has_tag(skill_def, &"bow") or _skill_has_tag(skill_def, &"weapon")


static func resolve_base_skill_range(unit_state: BattleUnitState, skill_def) -> int:
	var configured_range := maxi(int(skill_def.combat_profile.range_value), 0)
	if requires_current_melee_weapon(skill_def):
		return get_weapon_attack_range(unit_state)
	if is_weapon_range_skill(skill_def):
		var weapon_range := get_weapon_attack_range(unit_state)
		if weapon_range > 0:
			return weapon_range
		if _skill_has_tag(skill_def, &"melee"):
			return 1
	return configured_range


static func _get_range_modifier_bonus(unit_state: BattleUnitState, _skill_def) -> int:
	if unit_state == null:
		return 0
	var bonus := 0
	if unit_state.has_status_effect(STATUS_ARCHER_RANGE_UP):
		bonus += 1
	return bonus


static func effect_uses_weapon_physical_damage_tag(effect_def) -> bool:
	return effect_def != null \
		and effect_def.params != null \
		and bool(effect_def.params.get("use_weapon_physical_damage_tag", false))


static func effect_requires_weapon(effect_def) -> bool:
	return effect_def != null \
		and effect_def.params != null \
		and bool(effect_def.params.get("requires_weapon", false))


static func _skill_has_tag(skill_def, expected_tag: StringName) -> bool:
	if skill_def == null or expected_tag == &"":
		return false
	for tag in skill_def.tags:
		if ProgressionDataUtils.to_string_name(tag) == expected_tag:
			return true
	return false
