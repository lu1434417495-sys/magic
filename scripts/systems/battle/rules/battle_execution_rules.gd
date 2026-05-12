class_name BattleExecutionRules
extends RefCounted

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BOSS_TARGET_STAT_ID: StringName = &"boss_target"
const FORTUNE_MARK_TARGET_STAT_ID: StringName = &"fortune_mark_target"


static func resolve_threshold(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	params: Dictionary
) -> int:
	var base := maxi(int(params.get("threshold_base_value", 0)), 0)
	var anchor := maxi(int(params.get("threshold_level_anchor", 17)), 0)
	var bonus_per := maxi(int(params.get("threshold_level_bonus_per_delta", 5)), 0)
	var ability_id := ProgressionDataUtils.to_string_name(
		params.get("threshold_ability_mod", "intelligence_modifier")
	)
	var ability_mult := maxi(int(params.get("threshold_ability_mod_multiplier", 5)), 0)
	var max_hp_ratio := maxi(int(params.get("threshold_max_hp_ratio_percent", 20)), 0)
	var cap_ratio := maxi(int(params.get("threshold_cap_max_hp_ratio_percent", 50)), 0)

	var skill_level := 0
	var skill_id := ProgressionDataUtils.to_string_name(params.get("skill_id", ""))
	if skill_id != &"" and source_unit != null and source_unit.known_skill_level_map != null:
		skill_level = int(source_unit.known_skill_level_map.get(skill_id, 0))
	var level_bonus := maxi(skill_level - anchor, 0) * bonus_per

	var ability_mod := 0
	if ability_id != &"" and source_unit != null and source_unit.attribute_snapshot != null:
		ability_mod = int(source_unit.attribute_snapshot.get_value(ability_id))

	var target_max_hp := 0
	if target_unit != null and target_unit.attribute_snapshot != null:
		target_max_hp = maxi(
			int(target_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)), 0
		)

	var hp_floor := maxi(target_max_hp * max_hp_ratio / 100, 0)
	var raw_threshold := maxi(base, hp_floor) + level_bonus + ability_mod * ability_mult
	var cap := maxi(target_max_hp * cap_ratio / 100, 0)

	if cap > 0:
		return mini(raw_threshold, cap)
	return raw_threshold


static func is_boss_target(target_unit: BattleUnitState) -> bool:
	if target_unit == null or target_unit.attribute_snapshot == null:
		return false
	return int(target_unit.attribute_snapshot.get_value(BOSS_TARGET_STAT_ID)) > 0 \
		or int(target_unit.attribute_snapshot.get_value(FORTUNE_MARK_TARGET_STAT_ID)) > 1


static func is_elite_or_boss_target(target_unit: BattleUnitState) -> bool:
	if target_unit == null or target_unit.attribute_snapshot == null:
		return false
	return int(target_unit.attribute_snapshot.get_value(BOSS_TARGET_STAT_ID)) > 0 \
		or int(target_unit.attribute_snapshot.get_value(FORTUNE_MARK_TARGET_STAT_ID)) > 0


static func resolve_non_lethal_damage(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	params: Dictionary,
	is_boss: bool = false
) -> int:
	if is_boss:
		var ratio := maxi(int(params.get("boss_non_lethal_damage_max_hp_ratio_percent", 12)), 0)
		var floor_val := maxi(int(params.get("boss_non_lethal_damage_floor", 25)), 1)
		var target_max_hp := 0
		if target_unit != null and target_unit.attribute_snapshot != null:
			target_max_hp = maxi(
				int(target_unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)), 0
			)
		return maxi(target_max_hp * ratio / 100, floor_val)

	var ratio := maxi(int(params.get("non_lethal_damage_ratio_percent", 30)), 0)
	var threshold := resolve_threshold(source_unit, target_unit, params)
	return maxi(threshold * ratio / 100, 1)
