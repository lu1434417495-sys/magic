class_name BattleRangeService
extends RefCounted

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")

const STATUS_ARCHER_RANGE_UP: StringName = &"archer_range_up"
const STATUS_ARCHER_SHOOTING_SPECIALIZATION: StringName = &"archer_shooting_specialization"


static func get_weapon_attack_range(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return 0
	return unit_state.get_weapon_attack_range()


static func unit_has_melee_weapon(unit_state: BattleUnitState) -> bool:
	return unit_state != null \
		and unit_state.weapon_profile_kind == BattleUnitState.WEAPON_PROFILE_KIND_EQUIPPED \
		and get_weapon_attack_range(unit_state) > 0 \
		and ProgressionDataUtils.to_string_name(unit_state.weapon_physical_damage_tag if unit_state != null else &"") != &""


static func unit_matches_required_weapon_families(unit_state: BattleUnitState, required_weapon_families: Array) -> bool:
	if required_weapon_families.is_empty():
		return true
	if not unit_has_melee_weapon(unit_state):
		return false
	var current_family := ProgressionDataUtils.to_string_name(unit_state.weapon_family)
	if current_family == &"":
		return false
	for family in required_weapon_families:
		if ProgressionDataUtils.to_string_name(family) == current_family:
			return true
	return false


static func get_effective_skill_range(unit_state: BattleUnitState, skill_def) -> int:
	if skill_def == null or skill_def.combat_profile == null:
		return 0
	var skill_range := resolve_base_skill_range(unit_state, skill_def)
	skill_range += _get_range_modifier_bonus(unit_state, skill_def)
	return maxi(skill_range, 0)


static func get_effective_skill_threat_range(unit_state: BattleUnitState, skill_def) -> int:
	var skill_range := get_effective_skill_range(unit_state, skill_def)
	skill_range += _get_ground_effect_reach_bonus(unit_state, skill_def)
	return maxi(skill_range, 0)


static func requires_current_melee_weapon(skill_def) -> bool:
	if skill_def == null or skill_def.combat_profile == null:
		return false
	if not skill_def.combat_profile.required_weapon_families.is_empty():
		return true
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
	if skill_def == null or skill_def.combat_profile == null:
		return 0
	var skill_level := 0
	if unit_state != null:
		skill_level = int(unit_state.known_skill_level_map.get(skill_def.skill_id, 0))
	var configured_range := maxi(int(skill_def.combat_profile.get_effective_range_value(skill_level)), 0)
	if is_ground_relocation_skill(skill_def):
		return configured_range
	if requires_current_melee_weapon(skill_def):
		return get_weapon_attack_range(unit_state)
	if is_weapon_range_skill(skill_def):
		var weapon_range := get_weapon_attack_range(unit_state)
		if weapon_range > 0:
			return weapon_range
		if _skill_has_tag(skill_def, &"melee"):
			return 1
	return configured_range


static func is_ground_jump_skill(skill_def) -> bool:
	return is_ground_relocation_skill(skill_def)


static func is_ground_relocation_skill(skill_def) -> bool:
	if skill_def == null or skill_def.combat_profile == null:
		return false
	if ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_mode) != &"ground":
		return false
	for effect_def in skill_def.combat_profile.effect_defs:
		if _is_ground_relocation_effect(effect_def):
			return true
	for cast_variant in skill_def.combat_profile.cast_variants:
		if cast_variant == null:
			continue
		for effect_def in cast_variant.effect_defs:
			if _is_ground_relocation_effect(effect_def):
				return true
	return false


static func _get_range_modifier_bonus(unit_state: BattleUnitState, _skill_def) -> int:
	if unit_state == null:
		return 0
	var bonus := 0
	if unit_state.has_status_effect(STATUS_ARCHER_RANGE_UP):
		bonus += 1
	var shooting_specialization = unit_state.get_status_effect(STATUS_ARCHER_SHOOTING_SPECIALIZATION)
	if shooting_specialization != null \
			and unit_matches_required_weapon_families(unit_state, [&"bow"]) \
			and (requires_current_melee_weapon(_skill_def) or is_weapon_range_skill(_skill_def)):
		bonus += maxi(int(shooting_specialization.params.get("range_bonus", shooting_specialization.power)), 0)
	return bonus


static func _get_ground_effect_reach_bonus(unit_state: BattleUnitState, skill_def) -> int:
	if skill_def == null or skill_def.combat_profile == null:
		return 0
	if ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_mode) != &"ground":
		return 0
	if is_ground_relocation_skill(skill_def):
		return 0
	var area_pattern := ProgressionDataUtils.to_string_name(skill_def.combat_profile.area_pattern)
	if area_pattern == &"" or area_pattern == &"single" or area_pattern == &"self":
		return 0
	var skill_level := _get_unit_skill_level(unit_state, skill_def.skill_id)
	return maxi(int(skill_def.combat_profile.get_effective_area_value(skill_level)), 0)


static func _get_unit_skill_level(unit_state: BattleUnitState, skill_id: StringName) -> int:
	if unit_state == null or skill_id == &"":
		return 0
	if unit_state.known_skill_level_map.has(skill_id):
		return int(unit_state.known_skill_level_map.get(skill_id, 0))
	return 1 if unit_state.known_active_skill_ids.has(skill_id) else 0


static func effect_uses_weapon_physical_damage_tag(effect_def) -> bool:
	return effect_def != null \
		and effect_def.params != null \
		and bool(effect_def.params.get("use_weapon_physical_damage_tag", false))


static func effect_requires_weapon(effect_def) -> bool:
	return effect_def != null \
		and effect_def.params != null \
		and bool(effect_def.params.get("requires_weapon", false))


static func _is_jump_forced_move_effect(effect_def) -> bool:
	return effect_def != null \
		and ProgressionDataUtils.to_string_name(effect_def.effect_type) == &"forced_move" \
		and ProgressionDataUtils.to_string_name(effect_def.forced_move_mode) == &"jump"


static func _is_ground_relocation_effect(effect_def) -> bool:
	var mode := ProgressionDataUtils.to_string_name(effect_def.forced_move_mode) if effect_def != null else &""
	return effect_def != null \
		and ProgressionDataUtils.to_string_name(effect_def.effect_type) == &"forced_move" \
		and (mode == &"jump" or mode == &"blink")


static func _skill_has_tag(skill_def, expected_tag: StringName) -> bool:
	if skill_def == null or expected_tag == &"":
		return false
	for tag in skill_def.tags:
		if ProgressionDataUtils.to_string_name(tag) == expected_tag:
			return true
	return false
