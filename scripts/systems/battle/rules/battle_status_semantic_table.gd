class_name BattleStatusSemanticTable
extends RefCounted

const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")

const STACK_REFRESH: StringName = &"refresh"
const STACK_ADD: StringName = &"add"

const TICK_NONE: StringName = &"none"
const TICK_TURN_START_AP_PENALTY: StringName = &"turn_start_ap_penalty"
const TICK_TURN_START_DAMAGE: StringName = &"turn_start_damage"
const TICK_TIMELINE_DAMAGE: StringName = &"timeline_damage"
const TU_GRANULARITY := 5

const STATUS_ARMOR_BREAK: StringName = &"armor_break"
const STATUS_ARCHER_PRE_AIM: StringName = &"archer_pre_aim"
const STATUS_ARCHER_RANGE_UP: StringName = &"archer_range_up"
const STATUS_ARCHER_SHOOTING_SPECIALIZATION: StringName = &"archer_shooting_specialization"
const STATUS_ATTACK_UP: StringName = &"attack_up"
const STATUS_ATTACK_ROLL_BONUS_UP: StringName = &"attack_roll_bonus_up"
const STATUS_BURNING: StringName = &"burning"
const STATUS_DEATH_WARD: StringName = &"death_ward"
const STATUS_DAMAGE_REDUCTION_UP: StringName = &"damage_reduction_up"
const STATUS_DODGE_BONUS_UP: StringName = &"dodge_bonus_up"
const STATUS_FROZEN: StringName = &"frozen"
const STATUS_GUARDING: StringName = &"guarding"
const STATUS_HEX_OF_FRAILTY: StringName = &"hex_of_frailty"
const STATUS_MAGIC_SHIELD: StringName = &"magic_shield"
const STATUS_MARKED: StringName = &"marked"
const STATUS_PINNED: StringName = &"pinned"
const STATUS_PRISMATIC_BARRIER: StringName = &"prismatic_barrier"
const STATUS_ROOTED: StringName = &"rooted"
const STATUS_SHOCKED: StringName = &"shocked"
const STATUS_SLOW: StringName = &"slow"
const STATUS_SPELLWARD: StringName = &"spellward"
const STATUS_STAGGERED: StringName = &"staggered"
const STATUS_TAUNTED: StringName = &"taunted"
const STATUS_TENDON_CUT: StringName = &"tendon_cut"
const STATUS_CROWN_BREAK_BROKEN_FANG: StringName = &"crown_break_broken_fang"
const STATUS_CROWN_BREAK_BROKEN_HAND: StringName = &"crown_break_broken_hand"
const STATUS_CROWN_BREAK_BLINDED_EYE: StringName = &"crown_break_blinded_eye"
const STATUS_DOOM_SENTENCE_VERDICT: StringName = &"doom_sentence_verdict"
const STATUS_LAST_STAND_ACTIVE: StringName = &"last_stand_active"
const STATUS_WILLPOWER_SAVE_BONUS_UP: StringName = &"willpower_save_bonus_up"


static func has_semantic(status_id: StringName) -> bool:
	return not get_semantic(status_id).is_empty()


static func is_harmful_status(status_id: StringName) -> bool:
	match ProgressionDataUtils.to_string_name(status_id):
		STATUS_ARMOR_BREAK, STATUS_FROZEN, STATUS_MARKED, STATUS_PINNED, STATUS_ROOTED, STATUS_SHOCKED, STATUS_TAUNTED, STATUS_TENDON_CUT, STATUS_BURNING, STATUS_SLOW, STATUS_STAGGERED, STATUS_HEX_OF_FRAILTY, STATUS_CROWN_BREAK_BROKEN_FANG, STATUS_CROWN_BREAK_BROKEN_HAND, STATUS_CROWN_BREAK_BLINDED_EYE, STATUS_DOOM_SENTENCE_VERDICT, &"black_star_brand_normal", &"black_star_brand_elite":
			return true
		_:
			return false


static func get_semantic(status_id: StringName) -> Dictionary:
	match ProgressionDataUtils.to_string_name(status_id):
		STATUS_ARCHER_PRE_AIM, STATUS_ARCHER_RANGE_UP, STATUS_ARCHER_SHOOTING_SPECIALIZATION, STATUS_ATTACK_UP, STATUS_ATTACK_ROLL_BONUS_UP, STATUS_DAMAGE_REDUCTION_UP, STATUS_DEATH_WARD, STATUS_DODGE_BONUS_UP, STATUS_GUARDING, STATUS_HEX_OF_FRAILTY, STATUS_MAGIC_SHIELD, STATUS_PRISMATIC_BARRIER, STATUS_SPELLWARD, STATUS_LAST_STAND_ACTIVE, STATUS_WILLPOWER_SAVE_BONUS_UP:
			return _build_refresh_timeline_semantic()
		STATUS_ARMOR_BREAK, STATUS_FROZEN, STATUS_MARKED, STATUS_PINNED, STATUS_ROOTED, STATUS_SHOCKED, STATUS_TAUNTED, STATUS_TENDON_CUT, STATUS_CROWN_BREAK_BROKEN_FANG, STATUS_CROWN_BREAK_BROKEN_HAND, STATUS_CROWN_BREAK_BLINDED_EYE, STATUS_DOOM_SENTENCE_VERDICT:
			return _build_refresh_timeline_semantic()
		STATUS_BURNING:
			return {
				"stack_mode": STACK_ADD,
				"max_stacks": 3,
				"tick_mode": TICK_TIMELINE_DAMAGE,
			}
		STATUS_SLOW:
			return {
				"stack_mode": STACK_REFRESH,
				"max_stacks": 1,
				"tick_mode": TICK_NONE,
				"move_cost_delta": 1,
			}
		STATUS_STAGGERED:
			return _build_refresh_timeline_semantic(TICK_TURN_START_AP_PENALTY)
		_:
			return {}


static func merge_status(effect_def, source_unit_id: StringName, existing_entry: BattleStatusEffectState = null) -> BattleStatusEffectState:
	if effect_def == null or ProgressionDataUtils.to_string_name(effect_def.status_id) == &"":
		return null

	var semantic := get_semantic(effect_def.status_id)
	var status_entry := existing_entry.duplicate_state() if existing_entry != null else BattleStatusEffectState.new()
	status_entry.status_id = ProgressionDataUtils.to_string_name(effect_def.status_id)
	status_entry.source_unit_id = source_unit_id
	status_entry.params = _clone_effect_params(effect_def)

	var incoming_power := maxi(int(effect_def.power), 1)
	var previous_power := maxi(int(status_entry.power), 0)
	var previous_stacks := maxi(int(status_entry.stacks), 0)
	if semantic.is_empty():
		status_entry.power = int(effect_def.power)
		status_entry.stacks = maxi(previous_stacks + 1, 1)
		var source_duration := _resolve_duration_tu(effect_def)
		if source_duration >= 0:
			status_entry.duration = source_duration
		return status_entry

	var stack_mode := ProgressionDataUtils.to_string_name(semantic.get("stack_mode", STACK_REFRESH))
	var max_stacks := maxi(int(semantic.get("max_stacks", 0)), 0)
	status_entry.power = maxi(previous_power, incoming_power)
	match stack_mode:
		STACK_ADD:
			var next_stacks := maxi(previous_stacks + 1, 1)
			status_entry.stacks = mini(next_stacks, max_stacks) if max_stacks > 0 else next_stacks
		_:
			status_entry.stacks = 1

	var semantic_duration := _resolve_duration_tu(effect_def)
	if semantic_duration >= 0:
		var previous_duration := int(status_entry.duration) if status_entry.has_duration() else -1
		status_entry.duration = maxi(semantic_duration, previous_duration)
	var tick_interval_tu := _resolve_tick_interval_tu(effect_def)
	if tick_interval_tu > 0:
		status_entry.tick_interval_tu = tick_interval_tu
		if status_entry.next_tick_at_tu <= 0:
			status_entry.next_tick_at_tu = tick_interval_tu
	return status_entry


static func get_turn_start_ap_penalty(status_entry: BattleStatusEffectState) -> int:
	if status_entry == null:
		return 0
	var semantic := get_semantic(status_entry.status_id)
	if ProgressionDataUtils.to_string_name(semantic.get("tick_mode", TICK_NONE)) != TICK_TURN_START_AP_PENALTY:
		return 0
	return _get_effect_intensity(status_entry)


static func get_turn_start_damage(status_entry: BattleStatusEffectState) -> int:
	if status_entry == null:
		return 0
	var semantic := get_semantic(status_entry.status_id)
	if ProgressionDataUtils.to_string_name(semantic.get("tick_mode", TICK_NONE)) != TICK_TURN_START_DAMAGE:
		return 0
	return _get_effect_intensity(status_entry)


static func get_timeline_tick_damage(status_entry: BattleStatusEffectState) -> int:
	if status_entry == null or status_entry.tick_interval_tu <= 0:
		return 0
	var semantic := get_semantic(status_entry.status_id)
	if ProgressionDataUtils.to_string_name(semantic.get("tick_mode", TICK_NONE)) != TICK_TIMELINE_DAMAGE:
		return 0
	return _get_effect_intensity(status_entry)


static func get_move_cost_delta(status_entry: BattleStatusEffectState) -> int:
	if status_entry == null:
		return 0
	var semantic := get_semantic(status_entry.status_id)
	var base_delta := maxi(int(semantic.get("move_cost_delta", 0)), 0)
	if base_delta <= 0:
		return 0
	return base_delta * _get_effect_intensity(status_entry)


static func advance_timeline_duration(status_entry: BattleStatusEffectState, elapsed_tu: int) -> Dictionary:
	if status_entry == null or elapsed_tu <= 0 or not status_entry.has_duration():
		return {"expired": false, "changed": false}
	var previous_duration := int(status_entry.duration)
	var remaining_duration := maxi(previous_duration - elapsed_tu, 0)
	if remaining_duration <= 0:
		return {"expired": true, "changed": true}
	status_entry.duration = remaining_duration
	return {"expired": false, "changed": remaining_duration != previous_duration}


static func _build_refresh_timeline_semantic(tick_mode: StringName = TICK_NONE) -> Dictionary:
	return {
		"stack_mode": STACK_REFRESH,
		"max_stacks": 1,
		"tick_mode": tick_mode,
	}


static func _resolve_duration_tu(effect_def) -> int:
	if effect_def == null:
		return -1
	if effect_def.params != null and effect_def.params.has("duration_tu"):
		return _normalize_positive_tu_value(int(effect_def.params.get("duration_tu", 0)), "status params.duration_tu")
	if int(effect_def.duration_tu) > 0:
		return _normalize_positive_tu_value(int(effect_def.duration_tu), "status duration_tu")
	return -1


static func _resolve_tick_interval_tu(effect_def) -> int:
	if effect_def == null:
		return 0
	if int(effect_def.tick_interval_tu) > 0:
		return _normalize_positive_tu_value(int(effect_def.tick_interval_tu), "status tick_interval_tu")
	if effect_def.params != null and effect_def.params.has("tick_interval_tu"):
		return _normalize_positive_tu_value(int(effect_def.params.get("tick_interval_tu", 0)), "status params.tick_interval_tu")
	return 0


static func _clone_effect_params(effect_def) -> Dictionary:
	if effect_def == null or effect_def.params == null:
		return {}
	return effect_def.params.duplicate(true)


static func _get_effect_intensity(status_entry: BattleStatusEffectState) -> int:
	if status_entry == null:
		return 0
	return maxi(maxi(int(status_entry.power), int(status_entry.stacks)), 1)


static func _normalize_positive_tu_value(value: int, field_label: String) -> int:
	if value <= 0:
		return -1
	if value % TU_GRANULARITY != 0:
		var clamped_value := ((value + TU_GRANULARITY - 1) / TU_GRANULARITY) * TU_GRANULARITY
		push_error("%s must use %d TU steps, got %d; clamping up to %d." % [field_label, TU_GRANULARITY, value, clamped_value])
		return clamped_value
	return value
