class_name BattleSimExecutionLoop
extends RefCounted

const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")

const DEFAULT_MAX_IDLE_LOOPS := 25
const DEFAULT_TIMELINE_TICKS_PER_STEP := 1
const DEFAULT_MANUAL_POLICY: StringName = &"wait"


func run(runtime, state, scenario_def, options: Dictionary = {}) -> Dictionary:
	var iterations := 0
	var idle_loops := 0
	var timeline_steps := 0
	var stalled := false
	var max_iterations := _resolve_max_iterations(scenario_def, options)
	var max_idle_loops := _resolve_max_idle_loops(options)
	var manual_policy := _resolve_manual_policy(scenario_def, options)
	var timeline_ticks_per_step := _resolve_timeline_ticks_per_step(scenario_def, options)
	var progress_iteration_interval := maxi(int(options.get("progress_iteration_interval", 0)), 0)
	var progress_callback: Callable = options.get("progress_callback", Callable())
	var progress_context: Dictionary = options.get("progress_context", {}) if options.get("progress_context", {}) is Dictionary else {}

	while state != null and state.phase != &"battle_ended" and iterations < max_iterations:
		iterations += 1
		var previous_tu := int(state.timeline.current_tu) if state.timeline != null else 0
		var previous_signature := build_progress_signature(state)
		advance_step(runtime, state, manual_policy, timeline_ticks_per_step)
		var next_tu := int(state.timeline.current_tu) if state != null and state.timeline != null else previous_tu
		if next_tu != previous_tu:
			timeline_steps += 1
		if progress_iteration_interval > 0 \
				and progress_callback.is_valid() \
				and iterations % progress_iteration_interval == 0:
			progress_callback.call({
				"iterations": iterations,
				"idle_loops": idle_loops,
				"timeline_steps": timeline_steps,
				"state": state,
				"context": progress_context,
			})
		var next_signature := build_progress_signature(state)
		if previous_signature == next_signature:
			idle_loops += 1
			if idle_loops >= max_idle_loops:
				stalled = true
				break
		else:
			idle_loops = 0

	return {
		"iterations": iterations,
		"idle_loops": idle_loops,
		"timeline_steps": timeline_steps,
		"stalled": stalled,
	}


func advance_step(
	runtime,
	state,
	manual_policy: StringName = DEFAULT_MANUAL_POLICY,
	timeline_ticks_per_step: int = DEFAULT_TIMELINE_TICKS_PER_STEP
) -> void:
	if runtime == null or state == null:
		return
	if state.phase == &"unit_acting":
		var active_unit = state.units.get(state.active_unit_id)
		if active_unit != null and active_unit.is_alive and active_unit.control_mode == &"manual":
			_issue_manual_policy(runtime, manual_policy, active_unit.unit_id)
		else:
			runtime.advance(0)
		return
	if has_ready_units(state):
		runtime.advance(0)
		return
	runtime.advance(maxi(int(timeline_ticks_per_step), DEFAULT_TIMELINE_TICKS_PER_STEP))


func has_ready_units(state) -> bool:
	return state != null \
		and state.timeline != null \
		and not state.timeline.ready_unit_ids.is_empty()


func build_progress_signature(state) -> String:
	if state == null:
		return ""
	var unit_parts: Array[String] = []
	for unit_id_str in ProgressionDataUtils.sorted_string_keys(state.units):
		var unit_state = state.units.get(StringName(unit_id_str))
		if unit_state == null:
			continue
		unit_parts.append("%s:%d,%d:%d:%d:%d:%d:%d" % [
			unit_id_str,
			unit_state.coord.x,
			unit_state.coord.y,
			1 if bool(unit_state.is_alive) else 0,
			int(unit_state.current_hp),
			int(unit_state.current_ap),
			int(unit_state.current_stamina),
			int(unit_state.current_move_points),
		])
	return "%s|%s|%s|%d|%s" % [
		String(state.phase),
		String(state.active_unit_id),
		String(state.winner_faction_id),
		int(state.timeline.current_tu) if state.timeline != null else 0,
		";".join(unit_parts),
	]


func _issue_manual_policy(runtime, manual_policy: StringName, unit_id: StringName) -> void:
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.unit_id = unit_id
	command.command_type = BattleCommand.TYPE_WAIT
	match manual_policy:
		&"wait":
			runtime.issue_command(command)
		_:
			runtime.issue_command(command)


func _resolve_max_iterations(scenario_def, options: Dictionary) -> int:
	if options.has("max_iterations"):
		return maxi(int(options.get("max_iterations", 0)), 0)
	return maxi(int(scenario_def.max_iterations), 0) if scenario_def != null else 0


func _resolve_max_idle_loops(options: Dictionary) -> int:
	return maxi(int(options.get("max_idle_loops", DEFAULT_MAX_IDLE_LOOPS)), 1)


func _resolve_manual_policy(scenario_def, options: Dictionary) -> StringName:
	if options.has("manual_policy"):
		return ProgressionDataUtils.to_string_name(options.get("manual_policy", DEFAULT_MANUAL_POLICY))
	return ProgressionDataUtils.to_string_name(scenario_def.manual_policy) if scenario_def != null else DEFAULT_MANUAL_POLICY


func _resolve_timeline_ticks_per_step(scenario_def, options: Dictionary) -> int:
	var value := DEFAULT_TIMELINE_TICKS_PER_STEP
	if options.has("timeline_ticks_per_step"):
		value = int(options.get("timeline_ticks_per_step", DEFAULT_TIMELINE_TICKS_PER_STEP))
	elif scenario_def != null:
		value = int(scenario_def.timeline_ticks_per_step)
	return maxi(value, DEFAULT_TIMELINE_TICKS_PER_STEP)
