class_name BattleSaveResolver
extends RefCounted

const TRUE_RANDOM_SEED_SERVICE_SCRIPT = preload("res://scripts/utils/true_random_seed_service.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

const SAVE_TAG_SLEEP: StringName = &"sleep"
const SAVE_TAG_PARALYSIS: StringName = &"paralysis"
const SAVE_TAG_CHARM: StringName = &"charm"
const SAVE_TAG_POISON: StringName = &"poison"
const SAVE_TAG_DRAGON_BREATH: StringName = &"dragon_breath"
const SAVE_TAG_FIREBALL: StringName = &"fireball"
const SAVE_TAG_CHAIN_LIGHTNING: StringName = &"chain_lightning"

const ADVANTAGE_STATE_NORMAL: StringName = &"normal"
const ADVANTAGE_STATE_ADVANTAGE: StringName = &"advantage"
const ADVANTAGE_STATE_DISADVANTAGE: StringName = &"disadvantage"
const SAVE_DC_MODE_STATIC: StringName = &"static"
const SAVE_DC_MODE_CASTER_SPELL: StringName = &"caster_spell"
const SPELL_SAVE_DC_BASE := 8

const VALID_SAVE_TAGS := {
	SAVE_TAG_SLEEP: true,
	SAVE_TAG_PARALYSIS: true,
	SAVE_TAG_CHARM: true,
	SAVE_TAG_POISON: true,
	SAVE_TAG_DRAGON_BREATH: true,
	SAVE_TAG_FIREBALL: true,
	SAVE_TAG_CHAIN_LIGHTNING: true,
}

const VALID_SAVE_ABILITIES := {
	UNIT_BASE_ATTRIBUTES_SCRIPT.STRENGTH: true,
	UNIT_BASE_ATTRIBUTES_SCRIPT.AGILITY: true,
	UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION: true,
	UNIT_BASE_ATTRIBUTES_SCRIPT.PERCEPTION: true,
	UNIT_BASE_ATTRIBUTES_SCRIPT.INTELLIGENCE: true,
	UNIT_BASE_ATTRIBUTES_SCRIPT.WILLPOWER: true,
}

const CONTROL_SAVE_TAGS := {
	SAVE_TAG_SLEEP: true,
	SAVE_TAG_PARALYSIS: true,
	SAVE_TAG_CHARM: true,
}


static func resolve_save(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	effect_def: CombatEffectDef,
	context: Dictionary = {}
) -> Dictionary:
	var resolved_dc := _resolve_save_dc(source_unit, effect_def, context)
	if target_unit == null or effect_def == null or resolved_dc <= 0:
		return _empty_result()
	var save_tag := ProgressionDataUtils.to_string_name(effect_def.save_tag)
	var save_ability := ProgressionDataUtils.to_string_name(effect_def.save_ability)
	var tag_state := _collect_save_tag_state(target_unit, save_tag)
	if bool(tag_state.get("immune", false)):
		return {
			"has_save": true,
			"immune": true,
			"success": true,
			"natural_roll": 0,
			"roll_total": 0,
			"dc": resolved_dc,
			"ability": String(save_ability),
			"save_tag": String(save_tag),
			"advantage_state": String(ADVANTAGE_STATE_NORMAL),
			"ability_value": _get_target_ability_value(target_unit, save_ability),
			"ability_modifier": _get_target_ability_modifier(target_unit, save_ability),
			"bonus": 0,
			"sources": tag_state.get("sources", []),
		}

	var advantage_state := _resolve_advantage_state(tag_state)
	var natural_roll := _roll_save_die(advantage_state, context)
	var ability_value := _get_target_ability_value(target_unit, save_ability)
	var ability_modifier := _get_target_ability_modifier(target_unit, save_ability)
	var save_bonus := _get_status_save_bonus(target_unit, save_tag)
	var roll_total := natural_roll + ability_modifier + save_bonus
	var success := roll_total >= resolved_dc
	if natural_roll <= 1:
		success = false
	elif natural_roll >= 20:
		success = true
	return {
		"has_save": true,
		"immune": false,
		"success": success,
		"natural_roll": natural_roll,
		"roll_total": roll_total,
		"dc": resolved_dc,
		"ability": String(save_ability),
		"save_tag": String(save_tag),
		"advantage_state": String(advantage_state),
		"ability_value": ability_value,
		"ability_modifier": ability_modifier,
		"bonus": save_bonus,
		"sources": tag_state.get("sources", []),
	}


static func _resolve_save_dc(source_unit: BattleUnitState, effect_def: CombatEffectDef, context: Dictionary = {}) -> int:
	if effect_def == null:
		return 0
	var save_dc_mode := ProgressionDataUtils.to_string_name(effect_def.save_dc_mode)
	match save_dc_mode:
		SAVE_DC_MODE_CASTER_SPELL:
			return _resolve_caster_spell_save_dc(source_unit, effect_def, context)
		_:
			return maxi(int(effect_def.save_dc), 0)


static func _resolve_caster_spell_save_dc(source_unit: BattleUnitState, effect_def: CombatEffectDef, context: Dictionary = {}) -> int:
	if source_unit == null or source_unit.attribute_snapshot == null or effect_def == null:
		return 0
	var source_ability := ProgressionDataUtils.to_string_name(effect_def.save_dc_source_ability)
	if source_ability == &"" and context != null:
		source_ability = ProgressionDataUtils.to_string_name(context.get("save_dc_source_ability", ""))
	if source_ability == &"":
		return 0
	var ability_modifier := _get_source_ability_modifier(source_unit, source_ability)
	var proficiency_bonus := _get_source_spell_proficiency_bonus(source_unit)
	return maxi(SPELL_SAVE_DC_BASE + ability_modifier + proficiency_bonus, 1)


static func is_immune(unit_state: BattleUnitState, save_tag: StringName) -> bool:
	return bool(_collect_save_tag_state(unit_state, save_tag).get("immune", false))


static func _empty_result() -> Dictionary:
	return {
		"has_save": false,
		"immune": false,
		"success": false,
		"natural_roll": 0,
		"roll_total": 0,
		"dc": 0,
		"ability": "",
		"save_tag": "",
		"advantage_state": String(ADVANTAGE_STATE_NORMAL),
		"ability_value": 0,
		"ability_modifier": 0,
		"bonus": 0,
		"sources": [],
	}


static func _collect_save_tag_state(unit_state: BattleUnitState, save_tag: StringName) -> Dictionary:
	var state := {
		"immune": false,
		"advantage": false,
		"disadvantage": false,
		"sources": [],
	}
	if unit_state == null or save_tag == &"":
		return state
	_apply_save_tag_values(state, unit_state.save_advantage_tags, save_tag, &"unit", "save_advantage_tags", &"")
	for status_id_variant in unit_state.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		var status_entry = unit_state.get_status_effect(status_id)
		if status_entry == null or status_entry.params == null:
			continue
		_apply_save_tag_values(
			state,
			_get_array_param(status_entry.params, "save_advantage_tags"),
			save_tag,
			status_id,
			"save_advantage_tags",
			ADVANTAGE_STATE_ADVANTAGE
		)
		_apply_save_tag_values(
			state,
			_get_array_param(status_entry.params, "save_disadvantage_tags"),
			save_tag,
			status_id,
			"save_disadvantage_tags",
			ADVANTAGE_STATE_DISADVANTAGE
		)
		_apply_save_tag_values(
			state,
			_get_array_param(status_entry.params, "save_immunity_tags"),
			save_tag,
			status_id,
			"save_immunity_tags",
			&"immunity"
		)
		_apply_save_tag_values(
			state,
			_get_array_param(status_entry.params, "save_tags"),
			save_tag,
			status_id,
			"save_tags",
			&""
		)
	return state


static func _apply_save_tag_values(
	state: Dictionary,
	values: Array,
	save_tag: StringName,
	source_id: StringName,
	source_type: String,
	forced_mode: StringName
) -> void:
	for raw_value in values:
		var parsed_value := ProgressionDataUtils.to_string_name(raw_value)
		var mode := _resolve_save_tag_mode(parsed_value, save_tag, forced_mode)
		if mode == &"":
			continue
		match mode:
			ADVANTAGE_STATE_ADVANTAGE:
				state["advantage"] = true
			ADVANTAGE_STATE_DISADVANTAGE:
				state["disadvantage"] = true
			&"immunity":
				state["immune"] = true
		var sources: Array = state.get("sources", [])
		sources.append({
			"source_id": String(source_id),
			"type": source_type,
			"tag": String(parsed_value),
			"mode": String(mode),
		})
		state["sources"] = sources


static func _resolve_save_tag_mode(value: StringName, save_tag: StringName, forced_mode: StringName) -> StringName:
	if value == &"" or save_tag == &"":
		return &""
	var save_tag_text := String(save_tag)
	if forced_mode != &"":
		if value == save_tag \
				or value == StringName("%s_advantage" % save_tag_text) \
				or value == StringName("%s_disadvantage" % save_tag_text) \
				or value == StringName("%s_immunity" % save_tag_text):
			return forced_mode
		return &""
	if value == save_tag or value == StringName("%s_advantage" % save_tag_text):
		return ADVANTAGE_STATE_ADVANTAGE
	if value == StringName("%s_disadvantage" % save_tag_text):
		return ADVANTAGE_STATE_DISADVANTAGE
	if value == StringName("%s_immunity" % save_tag_text):
		return &"immunity"
	return &""


static func _resolve_advantage_state(tag_state: Dictionary) -> StringName:
	var has_advantage := bool(tag_state.get("advantage", false))
	var has_disadvantage := bool(tag_state.get("disadvantage", false))
	if has_advantage and not has_disadvantage:
		return ADVANTAGE_STATE_ADVANTAGE
	if has_disadvantage and not has_advantage:
		return ADVANTAGE_STATE_DISADVANTAGE
	return ADVANTAGE_STATE_NORMAL


static func _roll_save_die(advantage_state: StringName, context: Dictionary) -> int:
	var rolls := _get_save_roll_overrides(context)
	if not rolls.is_empty():
		if advantage_state == ADVANTAGE_STATE_ADVANTAGE and rolls.size() >= 2:
			return maxi(int(rolls[0]), int(rolls[1]))
		if advantage_state == ADVANTAGE_STATE_DISADVANTAGE and rolls.size() >= 2:
			return mini(int(rolls[0]), int(rolls[1]))
		return clampi(int(rolls[0]), 1, 20)
	var first_roll := int(TRUE_RANDOM_SEED_SERVICE_SCRIPT.randi_range(1, 20))
	if advantage_state == ADVANTAGE_STATE_NORMAL:
		return first_roll
	var second_roll := int(TRUE_RANDOM_SEED_SERVICE_SCRIPT.randi_range(1, 20))
	if advantage_state == ADVANTAGE_STATE_ADVANTAGE:
		return maxi(first_roll, second_roll)
	return mini(first_roll, second_roll)


static func _get_save_roll_overrides(context: Dictionary) -> Array[int]:
	var result: Array[int] = []
	if context == null:
		return result
	if context.has("save_roll_override"):
		result.append(clampi(int(context.get("save_roll_override", 0)), 1, 20))
		return result
	var raw_rolls = context.get("save_roll_overrides", [])
	if raw_rolls is Array:
		for raw_roll in raw_rolls:
			result.append(clampi(int(raw_roll), 1, 20))
	return result


static func _get_target_ability_value(target_unit: BattleUnitState, save_ability: StringName) -> int:
	if target_unit == null or target_unit.attribute_snapshot == null or save_ability == &"":
		return 0
	return int(target_unit.attribute_snapshot.get_value(save_ability))


static func _get_target_ability_modifier(target_unit: BattleUnitState, save_ability: StringName) -> int:
	if target_unit == null or target_unit.attribute_snapshot == null or save_ability == &"":
		return 0
	var modifier_id := ATTRIBUTE_SNAPSHOT_SCRIPT.get_base_attribute_modifier_id(save_ability)
	if modifier_id == &"":
		return 0
	return int(target_unit.attribute_snapshot.get_value(modifier_id))


static func _get_source_ability_modifier(source_unit: BattleUnitState, source_ability: StringName) -> int:
	if source_unit == null or source_unit.attribute_snapshot == null or source_ability == &"":
		return 0
	var modifier_id := ATTRIBUTE_SNAPSHOT_SCRIPT.get_base_attribute_modifier_id(source_ability)
	if modifier_id == &"":
		return 0
	return int(source_unit.attribute_snapshot.get_value(modifier_id))


static func _get_source_spell_proficiency_bonus(source_unit: BattleUnitState) -> int:
	if source_unit == null or source_unit.attribute_snapshot == null:
		return 0
	return maxi(int(source_unit.attribute_snapshot.get_value(ATTRIBUTE_SNAPSHOT_SCRIPT.SPELL_PROFICIENCY_BONUS)), 0)


static func _get_status_save_bonus(target_unit: BattleUnitState, save_tag: StringName) -> int:
	if target_unit == null:
		return 0
	var bonus := 0
	for status_id_variant in target_unit.status_effects.keys():
		var status_id := ProgressionDataUtils.to_string_name(status_id_variant)
		var status_entry = target_unit.get_status_effect(status_id)
		if status_entry == null or status_entry.params == null:
			continue
		bonus = maxi(bonus, int(status_entry.params.get("save_bonus", 0)))
		if CONTROL_SAVE_TAGS.has(save_tag):
			bonus = maxi(bonus, int(status_entry.params.get("control_save_bonus", 0)))
	return bonus


static func _get_array_param(params: Dictionary, key: String) -> Array:
	if params == null:
		return []
	var value = params.get(key, [])
	return value if value is Array else []
