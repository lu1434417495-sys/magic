class_name TraitTriggerHooks
extends RefCounted

const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const TRAIT_TRIGGER_CONTENT_RULES = preload("res://scripts/player/progression/trait_trigger_content_rules.gd")
const TRUE_RANDOM_SEED_SERVICE_SCRIPT = preload("res://scripts/utils/true_random_seed_service.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

const TRIGGER_PASSIVE: StringName = TRAIT_TRIGGER_CONTENT_RULES.TRIGGER_PASSIVE
const TRIGGER_ON_NATURAL_ONE: StringName = TRAIT_TRIGGER_CONTENT_RULES.TRIGGER_ON_NATURAL_ONE
const TRIGGER_ON_CRIT: StringName = TRAIT_TRIGGER_CONTENT_RULES.TRIGGER_ON_CRIT
const TRIGGER_ON_FATAL_DAMAGE: StringName = TRAIT_TRIGGER_CONTENT_RULES.TRIGGER_ON_FATAL_DAMAGE
const TRIGGER_ON_BATTLE_START: StringName = TRAIT_TRIGGER_CONTENT_RULES.TRIGGER_ON_BATTLE_START
const TRIGGER_ON_TURN_START: StringName = TRAIT_TRIGGER_CONTENT_RULES.TRIGGER_ON_TURN_START

const TRAIT_HALFLING_LUCK: StringName = TRAIT_TRIGGER_CONTENT_RULES.TRAIT_HALFLING_LUCK
const TRAIT_SAVAGE_ATTACKS: StringName = TRAIT_TRIGGER_CONTENT_RULES.TRAIT_SAVAGE_ATTACKS
const TRAIT_RELENTLESS_ENDURANCE: StringName = TRAIT_TRIGGER_CONTENT_RULES.TRAIT_RELENTLESS_ENDURANCE

const VALID_TRIGGER_TYPES := TRAIT_TRIGGER_CONTENT_RULES.VALID_TRIGGER_TYPES

const _DISPATCH := {
	TRAIT_HALFLING_LUCK: {
		TRIGGER_ON_NATURAL_ONE: "_handle_halfling_luck",
	},
	TRAIT_SAVAGE_ATTACKS: {
		TRIGGER_ON_CRIT: "_handle_savage_attacks",
	},
	TRAIT_RELENTLESS_ENDURANCE: {
		TRIGGER_ON_FATAL_DAMAGE: "_handle_relentless_endurance",
	},
}


static func has_dispatch_for_trait_trigger(trait_id: StringName, trigger_type: StringName) -> bool:
	return TRAIT_TRIGGER_CONTENT_RULES.has_dispatch_for_trait_trigger(
		ProgressionDataUtils.to_string_name(trait_id),
		ProgressionDataUtils.to_string_name(trigger_type)
	)


static func get_dispatch_trait_ids() -> Array[StringName]:
	return TRAIT_TRIGGER_CONTENT_RULES.get_dispatch_trait_ids()


func on_natural_one(unit_state: BattleUnitState, context: Dictionary = {}) -> Dictionary:
	return _dispatch_first(unit_state, TRIGGER_ON_NATURAL_ONE, context)


func on_crit(source_unit: BattleUnitState, target_unit: BattleUnitState, context: Dictionary = {}) -> Dictionary:
	var event_context := context.duplicate(true)
	event_context["target_unit"] = target_unit
	return _dispatch_first(source_unit, TRIGGER_ON_CRIT, event_context)


func on_fatal_damage(target_unit: BattleUnitState, source_unit: BattleUnitState, context: Dictionary = {}) -> Dictionary:
	var event_context := context.duplicate(true)
	event_context["source_unit"] = source_unit
	return _dispatch_first(target_unit, TRIGGER_ON_FATAL_DAMAGE, event_context)


func on_battle_start(unit_state: BattleUnitState, context: Dictionary = {}) -> Dictionary:
	var changed := false
	if _unit_has_trait(unit_state, TRAIT_HALFLING_LUCK):
		_set_charge(unit_state, _get_trait_charge_key(TRAIT_HALFLING_LUCK), 1, true, true)
		changed = true
	if _unit_has_trait(unit_state, TRAIT_RELENTLESS_ENDURANCE):
		_set_charge(unit_state, _get_trait_charge_key(TRAIT_RELENTLESS_ENDURANCE), 1, false, true)
		changed = true
	var dispatch_result := _dispatch_all(unit_state, TRIGGER_ON_BATTLE_START, context)
	return {
		"triggered": bool(dispatch_result.get("triggered", false)),
		"changed": changed or bool(dispatch_result.get("changed", false)),
		"event": TRIGGER_ON_BATTLE_START,
		"results": dispatch_result.get("results", []),
	}


func on_turn_start(unit_state: BattleUnitState, context: Dictionary = {}) -> Dictionary:
	var changed := false
	if _unit_has_trait(unit_state, TRAIT_HALFLING_LUCK):
		_set_charge(unit_state, _get_trait_charge_key(TRAIT_HALFLING_LUCK), 1, true, true)
		changed = true
	var dispatch_result := _dispatch_all(unit_state, TRIGGER_ON_TURN_START, context)
	return {
		"triggered": bool(dispatch_result.get("triggered", false)),
		"changed": changed or bool(dispatch_result.get("changed", false)),
		"event": TRIGGER_ON_TURN_START,
		"results": dispatch_result.get("results", []),
	}


func _dispatch_first(unit_state: BattleUnitState, trigger_type: StringName, context: Dictionary) -> Dictionary:
	for trait_id in _get_unit_trait_ids(unit_state):
		var dispatch_entry: Dictionary = _DISPATCH.get(trait_id, {})
		if not dispatch_entry.has(trigger_type):
			continue
		var method_name := String(dispatch_entry.get(trigger_type, ""))
		if method_name.is_empty() or not has_method(method_name):
			continue
		var result: Dictionary = call(method_name, unit_state, context)
		if not bool(result.get("triggered", false)):
			continue
		result["trait_id"] = trait_id
		result["event"] = trigger_type
		return result
	return _build_empty_result(trigger_type)


func _dispatch_all(unit_state: BattleUnitState, trigger_type: StringName, context: Dictionary) -> Dictionary:
	var results: Array[Dictionary] = []
	for trait_id in _get_unit_trait_ids(unit_state):
		var dispatch_entry: Dictionary = _DISPATCH.get(trait_id, {})
		if not dispatch_entry.has(trigger_type):
			continue
		var method_name := String(dispatch_entry.get(trigger_type, ""))
		if method_name.is_empty() or not has_method(method_name):
			continue
		var result: Dictionary = call(method_name, unit_state, context)
		if not bool(result.get("triggered", false)):
			continue
		result["trait_id"] = trait_id
		result["event"] = trigger_type
		results.append(result)
	return {
		"triggered": not results.is_empty(),
		"changed": not results.is_empty(),
		"event": trigger_type,
		"results": results,
	}


func _handle_halfling_luck(unit_state: BattleUnitState, context: Dictionary) -> Dictionary:
	var roll := int(context.get("roll", 0))
	if roll != 1:
		return _build_empty_result(TRIGGER_ON_NATURAL_ONE)
	var charge_key := _get_trait_charge_key(TRAIT_HALFLING_LUCK)
	if not _consume_charge(unit_state, charge_key, true, 1):
		return _build_empty_result(TRIGGER_ON_NATURAL_ONE)
	return {
		"triggered": true,
		"effect_type": TRAIT_HALFLING_LUCK,
		"original_roll": roll,
		"reroll_die": true,
		"die_size": maxi(int(context.get("die_size", 20)), 1),
		"charge_key": charge_key,
		"charges_remaining": _get_charge(unit_state, charge_key, true),
	}


func _handle_savage_attacks(unit_state: BattleUnitState, context: Dictionary) -> Dictionary:
	if not bool(context.get("critical_hit", false)):
		return _build_empty_result(TRIGGER_ON_CRIT)
	if not bool(context.get("add_weapon_dice", false)):
		return _build_empty_result(TRIGGER_ON_CRIT)
	var weapon_attack_range := int(context.get("weapon_attack_range", 0))
	if weapon_attack_range > 1:
		return _build_empty_result(TRIGGER_ON_CRIT)
	var weapon_dice: Dictionary = context.get("weapon_dice", {})
	var dice_sides := maxi(int(weapon_dice.get("dice_sides", 0)), 0)
	if dice_sides <= 0:
		return _build_empty_result(TRIGGER_ON_CRIT)
	return {
		"triggered": true,
		"effect_type": TRAIT_SAVAGE_ATTACKS,
		"extra_weapon_dice_count": 1,
		"extra_weapon_dice_sides": dice_sides,
	}


func _handle_relentless_endurance(unit_state: BattleUnitState, context: Dictionary) -> Dictionary:
	if unit_state == null:
		return _build_empty_result(TRIGGER_ON_FATAL_DAMAGE)
	var projected_hp := int(context.get("projected_hp", unit_state.current_hp))
	if projected_hp > 0:
		return _build_empty_result(TRIGGER_ON_FATAL_DAMAGE)
	var charge_key := _get_trait_charge_key(TRAIT_RELENTLESS_ENDURANCE)
	if not _consume_charge(unit_state, charge_key, false, 1):
		return _build_empty_result(TRIGGER_ON_FATAL_DAMAGE)
	return {
		"triggered": true,
		"effect_type": TRAIT_RELENTLESS_ENDURANCE,
		"clamp_to_hp": 1,
		"projected_hp": projected_hp,
		"hp_damage": int(context.get("hp_damage", 0)),
		"charge_key": charge_key,
		"charges_remaining": _get_charge(unit_state, charge_key, false),
	}


func _get_unit_trait_ids(unit_state: BattleUnitState) -> Array[StringName]:
	var trait_ids: Array[StringName] = []
	if unit_state == null:
		return trait_ids
	_append_unique_traits(trait_ids, unit_state.race_trait_ids)
	_append_unique_traits(trait_ids, unit_state.subrace_trait_ids)
	_append_unique_traits(trait_ids, unit_state.bloodline_trait_ids)
	_append_unique_traits(trait_ids, unit_state.ascension_trait_ids)
	return trait_ids


func _append_unique_traits(target: Array[StringName], values: Array[StringName]) -> void:
	for raw_value in values:
		var value := ProgressionDataUtils.to_string_name(raw_value)
		if value == &"" or target.has(value):
			continue
		target.append(value)


func _unit_has_trait(unit_state: BattleUnitState, trait_id: StringName) -> bool:
	return _get_unit_trait_ids(unit_state).has(trait_id)


func _get_trait_charge_key(trait_id: StringName) -> StringName:
	return StringName("trait_%s" % String(trait_id))


func _set_charge(
	unit_state: BattleUnitState,
	charge_key: StringName,
	value: int,
	per_turn: bool,
	force: bool = false
) -> void:
	if unit_state == null or charge_key == &"":
		return
	var charges := unit_state.per_turn_charges if per_turn else unit_state.per_battle_charges
	if force or not charges.has(charge_key):
		charges[charge_key] = maxi(value, 0)


func _consume_charge(unit_state: BattleUnitState, charge_key: StringName, per_turn: bool, default_value: int) -> bool:
	if unit_state == null or charge_key == &"":
		return false
	var charges := unit_state.per_turn_charges if per_turn else unit_state.per_battle_charges
	if not charges.has(charge_key):
		charges[charge_key] = maxi(default_value, 0)
	var remaining := maxi(int(charges.get(charge_key, 0)), 0)
	if remaining <= 0:
		return false
	charges[charge_key] = remaining - 1
	return true


func _get_charge(unit_state: BattleUnitState, charge_key: StringName, per_turn: bool) -> int:
	if unit_state == null or charge_key == &"":
		return 0
	var charges := unit_state.per_turn_charges if per_turn else unit_state.per_battle_charges
	return maxi(int(charges.get(charge_key, 0)), 0)


func _build_empty_result(trigger_type: StringName) -> Dictionary:
	return {
		"triggered": false,
		"event": trigger_type,
	}
