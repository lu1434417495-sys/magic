class_name BattleStatusSemanticTable
extends RefCounted

const BattleStatusEffectState = preload("res://scripts/systems/battle_status_effect_state.gd")

const STACK_REFRESH: StringName = &"refresh"
const STACK_ADD: StringName = &"add"

const DURATION_NONE: StringName = &"none"
const DURATION_DECREMENT_ON_TURN_END: StringName = &"decrement_on_turn_end"
const DURATION_CONSUME_ON_TURN_END: StringName = &"consume_on_turn_end"

const TICK_NONE: StringName = &"none"
const TICK_TURN_START_AP_PENALTY: StringName = &"turn_start_ap_penalty"
const TICK_TURN_START_DAMAGE: StringName = &"turn_start_damage"

const STATUS_ARMOR_BREAK: StringName = &"armor_break"
const STATUS_ARCHER_PRE_AIM: StringName = &"archer_pre_aim"
const STATUS_ATTACK_UP: StringName = &"attack_up"
const STATUS_BURNING: StringName = &"burning"
const STATUS_DAMAGE_REDUCTION_UP: StringName = &"damage_reduction_up"
const STATUS_EVASION_UP: StringName = &"evasion_up"
const STATUS_FROZEN: StringName = &"frozen"
const STATUS_GUARDING: StringName = &"guarding"
const STATUS_MARKED: StringName = &"marked"
const STATUS_PINNED: StringName = &"pinned"
const STATUS_ROOTED: StringName = &"rooted"
const STATUS_SHOCKED: StringName = &"shocked"
const STATUS_SLOW: StringName = &"slow"
const STATUS_STAGGERED: StringName = &"staggered"
const STATUS_TAUNTED: StringName = &"taunted"
const STATUS_TENDON_CUT: StringName = &"tendon_cut"


static func has_semantic(status_id: StringName) -> bool:
	return not get_semantic(status_id).is_empty()


static func get_semantic(status_id: StringName) -> Dictionary:
	match ProgressionDataUtils.to_string_name(status_id):
		STATUS_ARMOR_BREAK, STATUS_ARCHER_PRE_AIM, STATUS_ATTACK_UP, STATUS_DAMAGE_REDUCTION_UP, STATUS_EVASION_UP, STATUS_FROZEN, STATUS_GUARDING, STATUS_MARKED, STATUS_PINNED, STATUS_ROOTED, STATUS_SHOCKED, STATUS_TAUNTED, STATUS_TENDON_CUT:
			return _build_refresh_turn_end_semantic(
				DURATION_CONSUME_ON_TURN_END if ProgressionDataUtils.to_string_name(status_id) == STATUS_TAUNTED else DURATION_DECREMENT_ON_TURN_END
			)
		STATUS_BURNING:
			return {
				"stack_mode": STACK_ADD,
				"max_stacks": 3,
				"default_duration": 3,
				"duration_mode": DURATION_DECREMENT_ON_TURN_END,
				"tick_mode": TICK_TURN_START_DAMAGE,
			}
		STATUS_SLOW:
			return {
				"stack_mode": STACK_REFRESH,
				"max_stacks": 1,
				"default_duration": 1,
				"duration_mode": DURATION_DECREMENT_ON_TURN_END,
				"tick_mode": TICK_NONE,
				"move_cost_delta": 1,
			}
		STATUS_STAGGERED:
			return {
				"stack_mode": STACK_REFRESH,
				"max_stacks": 1,
				"default_duration": 1,
				"duration_mode": DURATION_CONSUME_ON_TURN_END,
				"tick_mode": TICK_TURN_START_AP_PENALTY,
			}
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
		var legacy_duration := _resolve_duration(effect_def, -1)
		if legacy_duration >= 0:
			status_entry.duration = legacy_duration
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

	var semantic_duration := _resolve_duration(effect_def, int(semantic.get("default_duration", -1)))
	if semantic_duration >= 0:
		var previous_duration := int(status_entry.duration) if status_entry.has_duration() else -1
		status_entry.duration = maxi(semantic_duration, previous_duration)
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


static func get_move_cost_delta(status_entry: BattleStatusEffectState) -> int:
	if status_entry == null:
		return 0
	var semantic := get_semantic(status_entry.status_id)
	var base_delta := maxi(int(semantic.get("move_cost_delta", 0)), 0)
	if base_delta <= 0:
		return 0
	return base_delta * _get_effect_intensity(status_entry)


static func advance_turn_end_duration(status_entry: BattleStatusEffectState) -> Dictionary:
	if status_entry == null:
		return {"expired": false, "changed": false}
	var duration_mode := _resolve_duration_mode(status_entry)
	match duration_mode:
		DURATION_CONSUME_ON_TURN_END:
			return {"expired": true, "changed": true}
		DURATION_DECREMENT_ON_TURN_END:
			if not status_entry.has_duration():
				return {"expired": false, "changed": false}
			var previous_duration := int(status_entry.duration)
			var remaining_duration := maxi(previous_duration - 1, 0)
			if remaining_duration <= 0:
				return {"expired": true, "changed": true}
			status_entry.duration = remaining_duration
			return {"expired": false, "changed": remaining_duration != previous_duration}
		_:
			return {"expired": false, "changed": false}


static func _build_refresh_turn_end_semantic(duration_mode: StringName, default_duration: int = 1) -> Dictionary:
	return {
		"stack_mode": STACK_REFRESH,
		"max_stacks": 1,
		"default_duration": default_duration,
		"duration_mode": duration_mode,
		"tick_mode": TICK_NONE,
	}


static func _resolve_duration_mode(status_entry: BattleStatusEffectState) -> StringName:
	if status_entry == null:
		return DURATION_NONE
	var semantic := get_semantic(status_entry.status_id)
	if not semantic.is_empty():
		return ProgressionDataUtils.to_string_name(semantic.get("duration_mode", DURATION_NONE))
	if status_entry.has_duration():
		return DURATION_DECREMENT_ON_TURN_END
	return DURATION_NONE


static func _resolve_duration(effect_def, fallback_duration: int) -> int:
	if effect_def == null:
		return fallback_duration
	if effect_def.params != null and effect_def.params.has("duration"):
		return maxi(int(effect_def.params.get("duration", fallback_duration)), 1)
	if int(effect_def.duration_tu) > 0:
		return maxi(int(effect_def.duration_tu), 1)
	return fallback_duration


static func _clone_effect_params(effect_def) -> Dictionary:
	if effect_def == null or effect_def.params == null:
		return {}
	return effect_def.params.duplicate(true)


static func _get_effect_intensity(status_entry: BattleStatusEffectState) -> int:
	if status_entry == null:
		return 0
	return maxi(maxi(int(status_entry.power), int(status_entry.stacks)), 1)
