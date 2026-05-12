class_name BattleTimelineDriver
extends RefCounted

const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

const MISFORTUNE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/fate/misfortune_service.gd")

const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")

const TU_GRANULARITY = 5

const STAMINA_RECOVERY_PROGRESS_BASE = 11

const STAMINA_RECOVERY_PROGRESS_DENOMINATOR = 10

const STAMINA_RESTING_RECOVERY_MULTIPLIER = 2

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


func advance_timeline(tick_count: int, batch: BattleEventBatch) -> void:
	if _runtime._state == null or _runtime._state.timeline == null or tick_count <= 0:
		return
	var resolved_tick_count := maxi(int(tick_count), 0)
	for _tick_index in range(resolved_tick_count):
		_apply_timeline_step(batch, _runtime._state.timeline.tu_per_tick)
		if _check_battle_end(batch):
			return


func _record_turn_started(unit_state: BattleUnitState) -> void:
	if _runtime == null:
		return
	_runtime._record_turn_started(unit_state)

func _get_unit_stamina_max(unit_state: BattleUnitState) -> int:
	if _runtime == null:
		return 0
	return _runtime._get_unit_stamina_max(unit_state)

func _append_changed_unit_id(batch: BattleEventBatch, unit_id: StringName) -> void:
	if _runtime == null:
		return
	_runtime._append_changed_unit_id(batch, unit_id)

func _collect_defeated_unit_loot(unit_state: BattleUnitState, killer_unit: BattleUnitState = null) -> void:
	if _runtime == null:
		return
	_runtime._collect_defeated_unit_loot(unit_state, killer_unit)

func _clear_defeated_unit(unit_state: BattleUnitState, batch: BattleEventBatch = null) -> void:
	if _runtime == null:
		return
	_runtime._clear_defeated_unit(unit_state, batch)

func _advance_unit_turn_timers(unit_state: BattleUnitState, batch: BattleEventBatch) -> void:
	if _runtime == null:
		return
	_runtime._advance_unit_turn_timers(unit_state, batch)

func _apply_turn_start_statuses(unit_state: BattleUnitState, batch: BattleEventBatch) -> Dictionary:
	if _runtime == null:
		return {}
	return _runtime._apply_turn_start_statuses(unit_state, batch)

func _apply_unit_status_periodic_ticks(
	unit_state: BattleUnitState,
	elapsed_tu: int,
	batch: BattleEventBatch
) -> Dictionary:
	if _runtime == null:
		return {}
	return _runtime._apply_unit_status_periodic_ticks(unit_state, elapsed_tu, batch)

func _advance_unit_status_durations(unit_state: BattleUnitState, elapsed_tu: int, batch: BattleEventBatch = null) -> bool:
	if _runtime == null:
		return false
	return _runtime._advance_unit_status_durations(unit_state, elapsed_tu, batch)

func _prepare_ai_turn(unit_state: BattleUnitState) -> void:
	if _runtime == null:
		return
	_runtime._prepare_ai_turn(unit_state)

func _cleanup_ai_turn(unit_state: BattleUnitState) -> void:
	if _runtime == null:
		return
	_runtime._cleanup_ai_turn(unit_state)

func _build_battle_resolution_result():
	if _runtime == null:
		return null
	return _runtime._build_battle_resolution_result()


func _use_discrete_timeline_ticks() -> bool:
	return _runtime._state != null \
		and _runtime._state.timeline != null \
		and _runtime._state.timeline.tu_per_tick > 0

func _apply_timeline_step(batch: BattleEventBatch, tu_delta: int) -> void:
	if _runtime._state == null or _runtime._state.timeline == null:
		return
	if tu_delta > 0 and tu_delta % TU_GRANULARITY != 0:
		push_error("Battle timeline can only advance in %d TU steps, got %d." % [TU_GRANULARITY, tu_delta])
		return
	if tu_delta > 0:
		_runtime._state.timeline.current_tu += tu_delta
		_resolve_timeline_status_phase(batch, tu_delta)
	_runtime._terrain_effect_system.process_timed_terrain_effects(batch)
	if _runtime._layered_barrier_service != null:
		_runtime._layered_barrier_service.advance_barrier_durations(tu_delta, batch)
	if tu_delta > 0:
		_collect_timeline_ready_units(batch, tu_delta)
	_sort_ready_unit_ids_by_action_priority()

func _resolve_timeline_status_phase(batch: BattleEventBatch, tu_delta: int) -> void:
	if _runtime._state == null or _runtime._state.timeline == null or tu_delta <= 0:
		return
	for unit_id in _get_units_in_order():
		var unit_state = _runtime._state.units.get(unit_id) as BattleUnitState
		if unit_state == null or not unit_state.is_alive:
			continue
		var status_tick_result = _apply_unit_status_periodic_ticks(unit_state, tu_delta, batch)
		if bool(status_tick_result.get("changed", false)):
			_append_changed_unit_id(batch, unit_state.unit_id)
		if not unit_state.is_alive:
			var defeat_source_unit_id = ProgressionDataUtils.to_string_name(status_tick_result.get("defeat_source_unit_id", ""))
			var defeat_source_unit = _runtime._state.units.get(defeat_source_unit_id) as BattleUnitState if defeat_source_unit_id != &"" else null
			_runtime.handle_unit_defeated_by_runtime_effect(
				unit_state,
				defeat_source_unit,
				batch,
				"%s 因持续效果倒下。" % unit_state.display_name,
				{"record_enemy_defeated_achievement": defeat_source_unit != null}
			)
			continue
		if _advance_unit_status_durations(unit_state, tu_delta, batch):
			_append_changed_unit_id(batch, unit_state.unit_id)

func _collect_timeline_ready_units(batch: BattleEventBatch, tu_delta: int) -> void:
	if _runtime._state == null or _runtime._state.timeline == null or tu_delta <= 0:
		return
	for unit_id in _get_units_in_order():
		var unit_state = _runtime._state.units.get(unit_id) as BattleUnitState
		if unit_state == null or not unit_state.is_alive:
			continue
		if _apply_stamina_recovery(unit_state, tu_delta):
			_append_changed_unit_id(batch, unit_state.unit_id)
		if not unit_state.is_alive:
			continue
		unit_state.action_progress += tu_delta
		var action_threshold = _resolve_unit_action_threshold(unit_state)
		while unit_state.action_progress >= action_threshold:
			unit_state.action_progress -= action_threshold
			if not _runtime._state.timeline.ready_unit_ids.has(unit_id):
				_runtime._state.timeline.ready_unit_ids.append(unit_id)

func _apply_stamina_recovery(unit_state: BattleUnitState, tu_delta: int) -> bool:
	if unit_state == null or tu_delta <= 0:
		return false
	var tick_count = int(tu_delta / TU_GRANULARITY)
	if tick_count <= 0:
		return false
	var stamina_max = _get_unit_stamina_max(unit_state)
	if stamina_max <= 0:
		if unit_state.current_stamina != 0 or unit_state.stamina_recovery_progress != 0:
			unit_state.current_stamina = 0
			unit_state.stamina_recovery_progress = 0
			return true
		return false

	var changed = false
	if unit_state.current_stamina >= stamina_max:
		if unit_state.current_stamina != stamina_max or unit_state.stamina_recovery_progress != 0:
			unit_state.current_stamina = stamina_max
			unit_state.stamina_recovery_progress = 0
			changed = true
		return changed

	var constitution = _get_unit_constitution(unit_state)
	var progress_gain_per_tick = STAMINA_RECOVERY_PROGRESS_BASE + constitution
	progress_gain_per_tick = _apply_stamina_recovery_percent_bonus(unit_state, progress_gain_per_tick)
	if unit_state.is_resting:
		progress_gain_per_tick *= STAMINA_RESTING_RECOVERY_MULTIPLIER

	for _tick_index in range(tick_count):
		unit_state.stamina_recovery_progress += progress_gain_per_tick
		var recovered = int(unit_state.stamina_recovery_progress / STAMINA_RECOVERY_PROGRESS_DENOMINATOR)
		if recovered <= 0:
			continue
		unit_state.current_stamina = mini(unit_state.current_stamina + recovered, stamina_max)
		unit_state.stamina_recovery_progress %= STAMINA_RECOVERY_PROGRESS_DENOMINATOR
		changed = true
		if unit_state.current_stamina >= stamina_max:
			unit_state.current_stamina = stamina_max
			unit_state.stamina_recovery_progress = 0
			break

	return changed

func _get_unit_constitution(unit_state: BattleUnitState) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 0
	return maxi(unit_state.attribute_snapshot.get_value(UNIT_BASE_ATTRIBUTES_SCRIPT.CONSTITUTION), 0)

func _apply_stamina_recovery_percent_bonus(unit_state: BattleUnitState, base_progress_gain: int) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return base_progress_gain
	var percent_bonus = maxi(unit_state.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.STAMINA_RECOVERY_PERCENT_BONUS), 0)
	if percent_bonus <= 0:
		return base_progress_gain
	return int((base_progress_gain * (100 + percent_bonus)) / 100)

func _normalize_unit_action_threshold(action_threshold: int) -> int:
	if action_threshold <= 0:
		push_error("Battle unit action_threshold must be positive, got %d." % [action_threshold])
		return BattleUnitState.DEFAULT_ACTION_THRESHOLD
	if action_threshold % TU_GRANULARITY != 0:
		push_error("Battle unit action_threshold must be a multiple of %d, got %d." % [TU_GRANULARITY, action_threshold])
		return BattleUnitState.DEFAULT_ACTION_THRESHOLD
	return action_threshold

func _initialize_unit_action_thresholds() -> void:
	if _runtime._state == null or _runtime._state.units == null:
		return
	for unit_variant in _runtime._state.units.values():
		_resolve_unit_action_threshold(unit_variant as BattleUnitState)

func _initialize_unit_trait_hooks() -> void:
	if _runtime._state == null or _runtime._state.units == null or _runtime._trait_trigger_hooks == null:
		return
	for unit_id_str in ProgressionDataUtils.sorted_string_keys(_runtime._state.units):
		var unit_state = _runtime._state.units.get(StringName(unit_id_str)) as BattleUnitState
		if unit_state == null:
			continue
		_runtime._trait_trigger_hooks.on_battle_start(unit_state, {"battle_state": _runtime._state})

func _resolve_unit_action_threshold(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return BattleUnitState.DEFAULT_ACTION_THRESHOLD
	var threshold = int(unit_state.action_threshold)
	if threshold <= 0:
		threshold = BattleUnitState.DEFAULT_ACTION_THRESHOLD
		unit_state.action_threshold = threshold
	var normalized_threshold = _normalize_unit_action_threshold(threshold)
	if normalized_threshold != threshold:
		unit_state.action_threshold = normalized_threshold
	return normalized_threshold

func _resolve_timeline_tu_per_tick(context: Dictionary) -> int:
	var tu_per_tick = int(context.get("tu_per_tick", TU_GRANULARITY))
	if tu_per_tick <= 0:
		return TU_GRANULARITY
	if tu_per_tick % TU_GRANULARITY != 0:
		push_error("timeline.tu_per_tick must be a multiple of %d, got %d." % [TU_GRANULARITY, tu_per_tick])
		return TU_GRANULARITY
	return tu_per_tick

func _check_battle_end(batch: BattleEventBatch) -> bool:
	if _runtime._state == null or batch == null:
		return false
	var living_allies = _count_living_units(_runtime._state.ally_unit_ids)
	var living_enemies = _count_living_units(_runtime._state.enemy_unit_ids)
	if living_allies > 0 and living_enemies > 0:
		return false

	_runtime._state.phase = &"battle_ended"
	if living_allies <= 0 and living_enemies <= 0:
		_runtime._state.winner_faction_id = &"draw"
	elif living_allies > 0:
		_runtime._state.winner_faction_id = &"player"
	else:
		_runtime._state.winner_faction_id = &"hostile"
	_runtime._state.active_unit_id = &""
	_runtime._state.timeline.ready_unit_ids.clear()
	_runtime._state.timeline.frozen = true
	_runtime._battle_rating_system.record_battle_won_achievements()
	_runtime._battle_rating_system.finalize_battle_rating_rewards()
	if _runtime._battle_resolution_result == null:
		_runtime._battle_resolution_result = _build_battle_resolution_result()
	_runtime._battle_resolution_result_consumed = false
	batch.phase_changed = true
	batch.battle_ended = true
	batch.log_lines.append("战斗结束，胜利方：%s。" % String(_runtime._state.winner_faction_id))
	return true

func _count_living_units(unit_ids: Array[StringName]) -> int:
	var count = 0
	for unit_id in unit_ids:
		var unit_state = _runtime._state.units.get(unit_id) as BattleUnitState
		if unit_state != null and unit_state.is_alive:
			count += 1
	return count

func _end_active_turn(batch: BattleEventBatch) -> void:
	if _runtime._state == null or batch == null:
		return
	var active_unit = _runtime._state.units.get(_runtime._state.active_unit_id) as BattleUnitState
	if active_unit != null and active_unit.is_alive and not active_unit.has_taken_action_this_turn:
		active_unit.is_resting = true
		_append_changed_unit_id(batch, active_unit.unit_id)
	if active_unit != null and _runtime.has_method("handle_misfortune_trigger"):
		_runtime.handle_misfortune_trigger(
			MISFORTUNE_SERVICE_SCRIPT.CALAMITY_REASON_LOW_HP_END_TURN,
			{"unit_state": active_unit}
		)
	if active_unit != null and active_unit.control_mode != &"manual":
		_cleanup_ai_turn(active_unit)
	elif active_unit != null \
			and _runtime._skill_turn_resolver != null \
			and _runtime._skill_turn_resolver.is_turn_ai_override_active(active_unit):
		_cleanup_ai_turn(active_unit)
	_runtime._state.phase = &"timeline_running"
	_runtime._state.active_unit_id = &""
	batch.phase_changed = true

func _activate_next_ready_unit(batch: BattleEventBatch) -> void:
	while not _runtime._state.timeline.ready_unit_ids.is_empty():
		var next_unit_id: StringName = _runtime._state.timeline.ready_unit_ids.pop_front()
		var unit_state = _runtime._state.units.get(next_unit_id) as BattleUnitState
		if unit_state == null or not unit_state.is_alive:
			continue
		_runtime._state.phase = &"unit_acting"
		_runtime._state.active_unit_id = next_unit_id
		unit_state.has_taken_action_this_turn = false
		unit_state.has_moved_this_turn = false
		unit_state.can_use_locked_move_points_this_turn = false
		unit_state.reset_per_turn_charges()
		var trait_turn_start_result: Dictionary = _runtime._trait_trigger_hooks.on_turn_start(unit_state, {"battle_state": _runtime._state}) if _runtime._trait_trigger_hooks != null else {}
		if bool(trait_turn_start_result.get("changed", false)):
			_append_changed_unit_id(batch, unit_state.unit_id)
		_advance_unit_turn_timers(unit_state, batch)
		_record_turn_started(unit_state)
		var action_points = 1
		if unit_state.attribute_snapshot != null:
			action_points = maxi(unit_state.attribute_snapshot.get_value(&"action_points"), 1)
		unit_state.current_ap = action_points
		unit_state.current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN
		var turn_start_result = _apply_turn_start_statuses(unit_state, batch)
		if not unit_state.is_alive:
			var defeat_source_unit_id = ProgressionDataUtils.to_string_name(turn_start_result.get("defeat_source_unit_id", ""))
			var defeat_source_unit = _runtime._state.units.get(defeat_source_unit_id) as BattleUnitState if defeat_source_unit_id != &"" else null
			_runtime.handle_unit_defeated_by_runtime_effect(
				unit_state,
				defeat_source_unit,
				batch,
				"%s 因持续效果倒下。" % unit_state.display_name,
				{"record_enemy_defeated_achievement": defeat_source_unit != null, "check_battle_end": false}
			)
			_runtime._state.phase = &"timeline_running"
			_runtime._state.active_unit_id = &""
			batch.phase_changed = true
			batch.changed_unit_ids.append(next_unit_id)
			_runtime._state.append_log_entry(String(batch.log_lines[-1]))
			if _check_battle_end(batch):
				return
			continue
		var control_status_result: Dictionary = _runtime._skill_turn_resolver.resolve_turn_control_status(unit_state, batch) if _runtime._skill_turn_resolver != null else {}
		if bool(control_status_result.get("skip_turn", false)):
			_runtime._state.phase = &"timeline_running"
			_runtime._state.active_unit_id = &""
			batch.phase_changed = true
			batch.changed_unit_ids.append(next_unit_id)
			continue
		if unit_state.control_mode != &"manual" or bool(control_status_result.get("ai_controlled", false)):
			_prepare_ai_turn(unit_state)
		batch.phase_changed = true
		batch.changed_unit_ids.append(next_unit_id)
		batch.log_lines.append("轮到 %s 行动。" % unit_state.display_name)
		_runtime._state.append_log_entry(String(batch.log_lines[-1]))
		return

func _sort_ready_unit_ids_by_action_priority() -> void:
	if _runtime._state == null or _runtime._state.timeline == null:
		return
	var ordered_ready_ids: Array[StringName] = []
	var seen_ids: Dictionary = {}
	for unit_id_variant in _runtime._state.timeline.ready_unit_ids:
		var unit_id = ProgressionDataUtils.to_string_name(unit_id_variant)
		if unit_id == &"" or seen_ids.has(unit_id):
			continue
		var unit_state = _runtime._state.units.get(unit_id) as BattleUnitState
		if unit_state == null or not unit_state.is_alive:
			continue
		seen_ids[unit_id] = true
		ordered_ready_ids.append(unit_id)
	ordered_ready_ids.sort_custom(Callable(self, "_is_left_ready_unit_higher_priority"))
	_runtime._state.timeline.ready_unit_ids = ordered_ready_ids

func _is_left_ready_unit_higher_priority(left_unit_id: StringName, right_unit_id: StringName) -> bool:
	var left_unit = _runtime._state.units.get(left_unit_id) as BattleUnitState if _runtime._state != null else null
	var right_unit = _runtime._state.units.get(right_unit_id) as BattleUnitState if _runtime._state != null else null
	if left_unit == null or not left_unit.is_alive:
		return false
	if right_unit == null or not right_unit.is_alive:
		return true
	var left_agility = _get_unit_turn_order_attribute(left_unit, UNIT_BASE_ATTRIBUTES_SCRIPT.AGILITY)
	var right_agility = _get_unit_turn_order_attribute(right_unit, UNIT_BASE_ATTRIBUTES_SCRIPT.AGILITY)
	if left_agility != right_agility:
		return left_agility > right_agility
	var left_action_points = _get_unit_turn_order_action_points(left_unit)
	var right_action_points = _get_unit_turn_order_action_points(right_unit)
	if left_action_points != right_action_points:
		return left_action_points > right_action_points
	var left_move_points = maxi(int(left_unit.current_move_points), 0)
	var right_move_points = maxi(int(right_unit.current_move_points), 0)
	if left_move_points != right_move_points:
		return left_move_points > right_move_points
	return String(left_unit_id) < String(right_unit_id)

func _get_unit_turn_order_attribute(unit_state: BattleUnitState, attribute_id: StringName) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 0
	return int(unit_state.attribute_snapshot.get_value(attribute_id))

func _get_unit_turn_order_action_points(unit_state: BattleUnitState) -> int:
	var snapshot_action_points = _get_unit_turn_order_attribute(unit_state, ATTRIBUTE_SERVICE_SCRIPT.ACTION_POINTS)
	if snapshot_action_points > 0:
		return snapshot_action_points
	return maxi(int(unit_state.current_ap), 0)

func _get_units_in_order() -> Array[StringName]:
	var ordered_ids: Array[StringName] = []
	for unit_id_str in ProgressionDataUtils.sorted_string_keys(_runtime._state.units):
		ordered_ids.append(StringName(unit_id_str))
	return ordered_ids
