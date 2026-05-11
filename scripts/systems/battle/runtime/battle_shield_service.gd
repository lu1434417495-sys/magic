class_name BattleShieldService
extends RefCounted

const TRUE_RANDOM_SEED_SERVICE_SCRIPT = preload("res://scripts/utils/true_random_seed_service.gd")

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

var _runtime_ref: WeakRef = null
var _runtime = null:
	get:
		return _runtime_ref.get_ref() if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null

func setup(runtime) -> void:
	_runtime = runtime


func dispose() -> void:
	_runtime = null


func _apply_unit_shield_effects(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	shield_roll_context: Dictionary = {}
) -> Dictionary:
	var result = {
		"applied": false,
		"current_shield_hp": 0,
		"shield_max_hp": 0,
		"shield_duration": -1,
		"shield_family": &"",
	}
	if target_unit == null or effect_defs.is_empty():
		return result

	for effect_def in effect_defs:
		if effect_def == null or effect_def.effect_type != &"shield":
			continue
		var shield_apply_result = _apply_shield_effect_to_target(
			source_unit,
			target_unit,
			skill_def,
			effect_def,
			shield_roll_context
		)
		if not bool(shield_apply_result.get("applied", false)):
			continue
		result = shield_apply_result
	return result

func _apply_shield_effect_to_target(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_def: CombatEffectDef,
	shield_roll_context: Dictionary = {}
) -> Dictionary:
	var result = {
		"applied": false,
		"current_shield_hp": int(target_unit.current_shield_hp) if target_unit != null else 0,
		"shield_max_hp": int(target_unit.shield_max_hp) if target_unit != null else 0,
		"shield_duration": int(target_unit.shield_duration) if target_unit != null else -1,
		"shield_family": target_unit.shield_family if target_unit != null else &"",
	}
	if target_unit == null or effect_def == null:
		return result

	var shield_hp = _resolve_shield_hp(effect_def, shield_roll_context)
	if shield_hp <= 0:
		return result
	var shield_duration = _resolve_shield_duration_tu(effect_def)
	if shield_duration <= 0:
		return result
	var shield_family = _resolve_shield_family(skill_def, effect_def)
	var shield_params = effect_def.params.duplicate(true) if effect_def.params != null else {}
	shield_params["resolved_shield_hp"] = shield_hp
	var shield_source_unit_id = source_unit.unit_id if source_unit != null else &""
	var shield_source_skill_id = skill_def.skill_id if skill_def != null else &""

	target_unit.normalize_shield_state()
	if not target_unit.has_shield():
		_write_unit_shield(
			target_unit,
			shield_hp,
			shield_duration,
			shield_family,
			shield_source_unit_id,
			shield_source_skill_id,
			shield_params
		)
		return _build_unit_shield_result(target_unit, true)

	if target_unit.shield_family == shield_family:
		var next_shield_max_hp = maxi(target_unit.shield_max_hp, shield_hp)
		var next_current_shield_hp = maxi(target_unit.current_shield_hp, shield_hp)
		var next_shield_duration = maxi(target_unit.shield_duration, shield_duration)
		if next_shield_max_hp == target_unit.shield_max_hp \
				and next_current_shield_hp == target_unit.current_shield_hp \
				and next_shield_duration == target_unit.shield_duration:
			return result
		target_unit.shield_max_hp = next_shield_max_hp
		target_unit.current_shield_hp = next_current_shield_hp
		target_unit.shield_duration = next_shield_duration
		target_unit.shield_source_unit_id = shield_source_unit_id
		target_unit.shield_source_skill_id = shield_source_skill_id
		target_unit.shield_params = shield_params.duplicate(true)
		target_unit.normalize_shield_state()
		return _build_unit_shield_result(target_unit, true)

	var should_replace = false
	if shield_hp > target_unit.current_shield_hp:
		should_replace = true
	elif shield_hp == target_unit.current_shield_hp:
		if shield_duration > target_unit.shield_duration:
			should_replace = true

	if not should_replace:
		return result

	_write_unit_shield(
		target_unit,
		shield_hp,
		shield_duration,
		shield_family,
		shield_source_unit_id,
		shield_source_skill_id,
		shield_params
	)
	return _build_unit_shield_result(target_unit, true)

func _write_unit_shield(
	target_unit: BattleUnitState,
	shield_hp: int,
	shield_duration: int,
	shield_family: StringName,
	shield_source_unit_id: StringName,
	shield_source_skill_id: StringName,
	shield_params: Dictionary
) -> void:
	if target_unit == null:
		return
	target_unit.current_shield_hp = maxi(shield_hp, 0)
	target_unit.shield_max_hp = maxi(shield_hp, 0)
	target_unit.shield_duration = shield_duration
	target_unit.shield_family = shield_family
	target_unit.shield_source_unit_id = shield_source_unit_id
	target_unit.shield_source_skill_id = shield_source_skill_id
	target_unit.shield_params = shield_params.duplicate(true)
	target_unit.normalize_shield_state()

func _build_unit_shield_result(target_unit: BattleUnitState, applied: bool) -> Dictionary:
	return {
		"applied": applied,
		"current_shield_hp": int(target_unit.current_shield_hp) if target_unit != null else 0,
		"shield_max_hp": int(target_unit.shield_max_hp) if target_unit != null else 0,
		"shield_duration": int(target_unit.shield_duration) if target_unit != null else -1,
		"shield_family": target_unit.shield_family if target_unit != null else &"",
	}

func _resolve_shield_hp(effect_def: CombatEffectDef, shield_roll_context: Dictionary = {}) -> int:
	if effect_def == null:
		return 0
	var fallback_shield_hp = maxi(int(effect_def.power), 0)
	if not _has_shield_dice_config(effect_def):
		return fallback_shield_hp
	var cache_key = _get_shield_roll_cache_key(effect_def)
	if shield_roll_context.has(cache_key):
		return maxi(int(shield_roll_context.get(cache_key, fallback_shield_hp)), 0)
	var rolled_shield_hp = _roll_shield_hp(effect_def)
	shield_roll_context[cache_key] = rolled_shield_hp
	return maxi(rolled_shield_hp, 0)

func _roll_shield_hp(effect_def: CombatEffectDef) -> int:
	if effect_def == null:
		return 0
	var shield_hp = maxi(int(effect_def.power), 0)
	if effect_def.params == null:
		return shield_hp
	var dice_count = maxi(int(effect_def.params.get("dice_count", 0)), 0)
	var dice_sides = maxi(int(effect_def.params.get("dice_sides", 0)), 0)
	if dice_count <= 0 or dice_sides <= 0:
		return shield_hp
	shield_hp += int(effect_def.params.get("dice_bonus", 0))
	for _roll_index in range(dice_count):
		shield_hp += _roll_battle_effect_die(dice_sides)
	return maxi(shield_hp, 0)

func _has_shield_dice_config(effect_def: CombatEffectDef) -> bool:
	return effect_def != null \
		and effect_def.params != null \
		and int(effect_def.params.get("dice_count", 0)) > 0 \
		and int(effect_def.params.get("dice_sides", 0)) > 0

func _get_shield_roll_cache_key(effect_def: CombatEffectDef) -> int:
	return effect_def.get_instance_id() if effect_def != null else 0

func _roll_battle_effect_die(dice_sides: int) -> int:
	if dice_sides <= 0:
		return 0
	if _runtime._state == null:
		return 1

	return int(TRUE_RANDOM_SEED_SERVICE_SCRIPT.randi_range(1, dice_sides))

func _resolve_shield_duration_tu(effect_def: CombatEffectDef) -> int:
	if effect_def == null:
		return 0
	if int(effect_def.duration_tu) > 0:
		return int(effect_def.duration_tu)
	if effect_def.params == null:
		return 0
	if effect_def.params.has("duration_tu"):
		return maxi(int(effect_def.params.get("duration_tu", 0)), 0)
	return 0

func _resolve_shield_family(skill_def: SkillDef, effect_def: CombatEffectDef) -> StringName:
	if effect_def != null and effect_def.params != null:
		var explicit_family = ProgressionDataUtils.to_string_name(effect_def.params.get("shield_family", ""))
		if explicit_family != &"":
			return explicit_family
		explicit_family = ProgressionDataUtils.to_string_name(effect_def.params.get("family", ""))
		if explicit_family != &"":
			return explicit_family
	if skill_def != null and skill_def.skill_id != &"":
		return skill_def.skill_id
	return &"shield"
