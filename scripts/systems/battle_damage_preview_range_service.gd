class_name BattleDamagePreviewRangeService
extends RefCounted

const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")


static func build_skill_damage_preview(source_unit: BattleUnitState, effect_defs: Array) -> Dictionary:
	var damage_ranges: Array[Dictionary] = []
	var min_damage := 0
	var max_damage := 0

	for effect_index in range(effect_defs.size()):
		var effect_def := effect_defs[effect_index] as CombatEffectDef
		if effect_def == null or effect_def.effect_type != &"damage":
			continue
		var effect_range := _build_damage_effect_range(source_unit, effect_def, effect_index)
		damage_ranges.append(effect_range)
		min_damage += int(effect_range.get("min_damage", 0))
		max_damage += int(effect_range.get("max_damage", 0))

	var preview := {
		"has_damage": not damage_ranges.is_empty(),
		"min_damage": min_damage,
		"max_damage": max_damage,
		"summary_text": "",
		"damage_ranges": damage_ranges,
	}
	preview["summary_text"] = format_damage_range_text(preview)
	return preview


static func format_damage_range_text(preview: Dictionary) -> String:
	if preview.is_empty() or not bool(preview.get("has_damage", false)):
		return ""
	var min_damage := int(preview.get("min_damage", 0))
	var max_damage := int(preview.get("max_damage", min_damage))
	if min_damage == max_damage:
		return "伤害 %d" % min_damage
	return "伤害 %d-%d" % [min_damage, max_damage]


static func _build_damage_effect_range(
	source_unit: BattleUnitState,
	effect_def: CombatEffectDef,
	effect_index: int
) -> Dictionary:
	var power := maxi(int(effect_def.power), 0)
	var skill_dice_range := _build_skill_dice_range(effect_def)
	var weapon_dice_range := _build_weapon_dice_range(source_unit) if _should_add_weapon_dice(effect_def) else _build_empty_dice_range()
	var effect_min_damage := power \
		+ int(skill_dice_range.get("min_damage", 0)) \
		+ int(weapon_dice_range.get("min_damage", 0))
	var effect_max_damage := power \
		+ int(skill_dice_range.get("max_damage", 0)) \
		+ int(weapon_dice_range.get("max_damage", 0))

	return {
		"effect_index": effect_index,
		"power": power,
		"add_weapon_dice": _should_add_weapon_dice(effect_def),
		"min_damage": effect_min_damage,
		"max_damage": effect_max_damage,
		"damage_dice_count": int(skill_dice_range.get("dice_count", 0)),
		"damage_dice_sides": int(skill_dice_range.get("dice_sides", 0)),
		"damage_dice_bonus": int(skill_dice_range.get("dice_bonus", 0)),
		"damage_dice_min": int(skill_dice_range.get("min_damage", 0)),
		"damage_dice_max": int(skill_dice_range.get("max_damage", 0)),
		"weapon_damage_dice_count": int(weapon_dice_range.get("dice_count", 0)),
		"weapon_damage_dice_sides": int(weapon_dice_range.get("dice_sides", 0)),
		"weapon_damage_dice_bonus": int(weapon_dice_range.get("dice_bonus", 0)),
		"weapon_damage_dice_min": int(weapon_dice_range.get("min_damage", 0)),
		"weapon_damage_dice_max": int(weapon_dice_range.get("max_damage", 0)),
	}


static func _build_skill_dice_range(effect_def: CombatEffectDef) -> Dictionary:
	if effect_def == null or effect_def.params == null:
		return _build_empty_dice_range()
	var params := effect_def.params
	var dice_count := maxi(int(params.get("dice_count", params.get("damage_dice_count", 0))), 0)
	var dice_sides := maxi(int(params.get("dice_sides", params.get("damage_dice_sides", 0))), 0)
	var dice_bonus := int(params.get("dice_bonus", params.get("damage_dice_bonus", 0)))
	return _build_dice_range(dice_count, dice_sides, dice_bonus)


static func _build_weapon_dice_range(source_unit: BattleUnitState) -> Dictionary:
	var dice := _get_current_weapon_damage_dice(source_unit)
	if dice.is_empty():
		return _build_empty_dice_range()
	var dice_count := maxi(int(dice.get("dice_count", 0)), 0)
	var dice_sides := maxi(int(dice.get("dice_sides", 0)), 0)
	var dice_bonus := int(dice.get("flat_bonus", 0))
	return _build_dice_range(dice_count, dice_sides, dice_bonus)


static func _build_dice_range(dice_count: int, dice_sides: int, dice_bonus: int) -> Dictionary:
	if dice_count <= 0 or dice_sides <= 0:
		return _build_empty_dice_range()
	return {
		"dice_count": dice_count,
		"dice_sides": dice_sides,
		"dice_bonus": dice_bonus,
		"min_damage": dice_count + dice_bonus,
		"max_damage": dice_count * dice_sides + dice_bonus,
	}


static func _build_empty_dice_range() -> Dictionary:
	return {
		"dice_count": 0,
		"dice_sides": 0,
		"dice_bonus": 0,
		"min_damage": 0,
		"max_damage": 0,
	}


static func _should_add_weapon_dice(effect_def: CombatEffectDef) -> bool:
	if effect_def == null or effect_def.params == null:
		return false
	return bool(effect_def.params.get("add_weapon_dice", false))


static func _get_current_weapon_damage_dice(unit_state: BattleUnitState) -> Dictionary:
	if unit_state == null:
		return {}
	if unit_state.weapon_uses_two_hands:
		return unit_state.weapon_two_handed_dice
	return unit_state.weapon_one_handed_dice
