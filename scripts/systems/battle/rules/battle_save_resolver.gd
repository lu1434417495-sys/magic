class_name BattleSaveResolver
extends RefCounted

const TRUE_RANDOM_SEED_SERVICE_SCRIPT = preload("res://scripts/utils/true_random_seed_service.gd")
const BATTLE_SAVE_CONTENT_RULES = preload("res://scripts/player/progression/battle_save_content_rules.gd")
const ATTRIBUTE_SNAPSHOT_SCRIPT = preload("res://scripts/player/progression/attribute_snapshot.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

const SAVE_TAG_SLEEP: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_SLEEP
const SAVE_TAG_PARALYSIS: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_PARALYSIS
const SAVE_TAG_CHARM: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_CHARM
const SAVE_TAG_POISON: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_POISON
const SAVE_TAG_DRAGON_BREATH: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_DRAGON_BREATH
const SAVE_TAG_FIREBALL: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_FIREBALL
const SAVE_TAG_CHAIN_LIGHTNING: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_CHAIN_LIGHTNING
const SAVE_TAG_EQUIPMENT_DISJUNCTION: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_EQUIPMENT_DISJUNCTION
const SAVE_TAG_MAGIC: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_MAGIC
const SAVE_TAG_ILLUSION: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_ILLUSION
const SAVE_TAG_FRIGHTENED: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_FRIGHTENED
const SAVE_TAG_STRENGTH: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_STRENGTH
const SAVE_TAG_AGILITY: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_AGILITY
const SAVE_TAG_CONSTITUTION: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_CONSTITUTION
const SAVE_TAG_PERCEPTION: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_PERCEPTION
const SAVE_TAG_INTELLIGENCE: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_INTELLIGENCE
const SAVE_TAG_WILLPOWER: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_WILLPOWER

const ADVANTAGE_STATE_NORMAL: StringName = BATTLE_SAVE_CONTENT_RULES.ADVANTAGE_STATE_NORMAL
const ADVANTAGE_STATE_ADVANTAGE: StringName = BATTLE_SAVE_CONTENT_RULES.ADVANTAGE_STATE_ADVANTAGE
const ADVANTAGE_STATE_DISADVANTAGE: StringName = BATTLE_SAVE_CONTENT_RULES.ADVANTAGE_STATE_DISADVANTAGE
const SAVE_DC_MODE_STATIC: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_DC_MODE_STATIC
const SAVE_DC_MODE_CASTER_SPELL: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_DC_MODE_CASTER_SPELL
const SPELL_SAVE_DC_BASE := 8

const VALID_SAVE_TAGS := BATTLE_SAVE_CONTENT_RULES.VALID_SAVE_TAGS
const VALID_SAVE_ABILITIES := BATTLE_SAVE_CONTENT_RULES.VALID_SAVE_ABILITIES
const CONTROL_SAVE_TAGS := BATTLE_SAVE_CONTENT_RULES.CONTROL_SAVE_TAGS


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
	var success := _does_natural_save_roll_succeed(natural_roll, resolved_dc, ability_modifier, save_bonus)
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


static func estimate_save_success_probability(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	effect_def: CombatEffectDef,
	context: Dictionary = {}
) -> Dictionary:
	var resolved_dc := _resolve_save_dc(source_unit, effect_def, context)
	if target_unit == null or effect_def == null or resolved_dc <= 0:
		return _empty_probability_result()
	var save_tag := ProgressionDataUtils.to_string_name(effect_def.save_tag)
	var save_ability := ProgressionDataUtils.to_string_name(effect_def.save_ability)
	var ability_value := _get_target_ability_value(target_unit, save_ability)
	var ability_modifier := _get_target_ability_modifier(target_unit, save_ability)
	var tag_state := _collect_save_tag_state(target_unit, save_tag)
	if bool(tag_state.get("immune", false)):
		return {
			"has_save": true,
			"immune": true,
			"success_probability_basis_points": 10000,
			"failure_probability_basis_points": 0,
			"dc": resolved_dc,
			"ability": String(save_ability),
			"save_tag": String(save_tag),
			"advantage_state": String(ADVANTAGE_STATE_NORMAL),
			"ability_value": ability_value,
			"ability_modifier": ability_modifier,
			"bonus": 0,
			"sources": tag_state.get("sources", []),
		}
	var advantage_state := _resolve_advantage_state(tag_state)
	var save_bonus := _get_status_save_bonus(target_unit, save_tag)
	var success_basis_points := _estimate_success_probability_basis_points(
		advantage_state,
		resolved_dc,
		ability_modifier,
		save_bonus,
		context
	)
	return {
		"has_save": true,
		"immune": false,
		"success_probability_basis_points": success_basis_points,
		"failure_probability_basis_points": maxi(10000 - success_basis_points, 0),
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
	var locked_skill_hit_bonus := _get_skill_lock_hit_bonus_from_context(source_unit, context)
	var save_dc_mode := ProgressionDataUtils.to_string_name(effect_def.save_dc_mode)
	match save_dc_mode:
		SAVE_DC_MODE_CASTER_SPELL:
			var caster_dc := _resolve_caster_spell_save_dc(source_unit, effect_def, context)
			return caster_dc + locked_skill_hit_bonus if caster_dc > 0 else 0
		_:
			var static_dc := maxi(int(effect_def.save_dc), 0)
			return static_dc + locked_skill_hit_bonus if static_dc > 0 else 0


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


static func _empty_probability_result() -> Dictionary:
	return {
		"has_save": false,
		"immune": false,
		"success_probability_basis_points": 0,
		"failure_probability_basis_points": 10000,
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
		return _select_save_roll_for_advantage_state(advantage_state, rolls)
	var first_roll := int(TRUE_RANDOM_SEED_SERVICE_SCRIPT.randi_range(1, 20))
	if advantage_state == ADVANTAGE_STATE_NORMAL:
		return first_roll
	var second_roll := int(TRUE_RANDOM_SEED_SERVICE_SCRIPT.randi_range(1, 20))
	if advantage_state == ADVANTAGE_STATE_ADVANTAGE:
		return maxi(first_roll, second_roll)
	return mini(first_roll, second_roll)


static func _select_save_roll_for_advantage_state(advantage_state: StringName, rolls: Array[int]) -> int:
	if rolls.is_empty():
		return 1
	if advantage_state == ADVANTAGE_STATE_ADVANTAGE and rolls.size() >= 2:
		return maxi(int(rolls[0]), int(rolls[1]))
	if advantage_state == ADVANTAGE_STATE_DISADVANTAGE and rolls.size() >= 2:
		return mini(int(rolls[0]), int(rolls[1]))
	return clampi(int(rolls[0]), 1, 20)


static func _estimate_success_probability_basis_points(
	advantage_state: StringName,
	dc: int,
	ability_modifier: int,
	save_bonus: int,
	context: Dictionary
) -> int:
	var rolls := _get_save_roll_overrides(context)
	if not rolls.is_empty():
		var selected_roll := _select_save_roll_for_advantage_state(advantage_state, rolls)
		return 10000 if _does_natural_save_roll_succeed(selected_roll, dc, ability_modifier, save_bonus) else 0
	var success_count := 0
	var total_count := 0
	match advantage_state:
		ADVANTAGE_STATE_ADVANTAGE:
			for first_roll in range(1, 21):
				for second_roll in range(1, 21):
					total_count += 1
					var natural_roll := maxi(first_roll, second_roll)
					if _does_natural_save_roll_succeed(natural_roll, dc, ability_modifier, save_bonus):
						success_count += 1
		ADVANTAGE_STATE_DISADVANTAGE:
			for first_roll in range(1, 21):
				for second_roll in range(1, 21):
					total_count += 1
					var natural_roll := mini(first_roll, second_roll)
					if _does_natural_save_roll_succeed(natural_roll, dc, ability_modifier, save_bonus):
						success_count += 1
		_:
			for natural_roll in range(1, 21):
				total_count += 1
				if _does_natural_save_roll_succeed(natural_roll, dc, ability_modifier, save_bonus):
					success_count += 1
	if total_count <= 0:
		return 0
	return clampi(int(round(float(success_count) * 10000.0 / float(total_count))), 0, 10000)


static func _does_natural_save_roll_succeed(
	natural_roll: int,
	dc: int,
	ability_modifier: int,
	save_bonus: int
) -> bool:
	if natural_roll <= 1:
		return false
	if natural_roll >= 20:
		return true
	return natural_roll + ability_modifier + save_bonus >= dc


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


static func _get_skill_lock_hit_bonus_from_context(source_unit: BattleUnitState, context: Dictionary) -> int:
	if source_unit == null or context == null:
		return 0
	var skill_id := ProgressionDataUtils.to_string_name(context.get("skill_id", ""))
	if skill_id == &"":
		return 0
	return maxi(int(source_unit.known_skill_lock_hit_bonus_map.get(skill_id, 0)), 0)


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
