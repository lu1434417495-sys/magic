## Layer 2-C entry: deep AI profile (function-level top-N + Chrome Tracing JSON).
##
## Uses the same probes / fixtures as Layer 1 baseline, but additionally activates
## AiTraceRecorder so that:
##   - score_service internal helpers (_populate_hit_metrics, _populate_special_profile_metrics,
##     _populate_target_effect_metrics, _build_target_effect_metrics,
##     _resolve_target_role_threat_multiplier_basis_points, _resolve_meteor_threat_rank)
##     emit enter/exit events (production wraps stay no-op when instance is null);
##   - service / assembler probes also emit choose_command / build_*_score_input /
##     build_unit_action_plan events for top-level zones.
##
## Outputs (default dir: tests/battle_runtime/benchmarks/profiles/):
##   ai_profile_<scenario>_<timestamp>.hotspots.txt   — cProfile top-N
##   ai_profile_<scenario>_<timestamp>.functions.csv  — every function, sortable
##   ai_profile_<scenario>_<timestamp>.trace.json     — Chrome Tracing (only when AI_TRACE_JSON=1)
##
## Env vars:
##   BASELINE_SCENARIOS=small_4v8,medium_6v20,large_6v40  (default: medium_6v20 only)
##   BASELINE_REPEAT_COUNT=2  (1 warmup + (N-1) measured; default 2)
##   AI_PROFILE_TOP_N=20
##   AI_PROFILE_SORT=self_usec | total_usec | ncalls   (default self_usec)
##   AI_PROFILE_FILTER=  (substring filter for top-N text report; empty = no filter)
##   AI_PROFILE_OUTPUT_DIR=res://tests/battle_runtime/benchmarks/profiles/
##   AI_TRACE_JSON=1  to dump trace JSON (off by default — file can be large)

extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const AI_SERVICE_PROBE_SCRIPT = preload("res://tests/battle_runtime/benchmarks/ai_service_probe.gd")
const AI_ASSEMBLER_PROBE_SCRIPT = preload("res://tests/battle_runtime/benchmarks/ai_assembler_probe.gd")
const AiTraceRecorderScript = preload("res://scripts/dev_tools/ai_trace_recorder.gd")
const AiHotspotsFormatterScript = preload("res://tests/battle_runtime/benchmarks/ai_hotspots_formatter.gd")

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BATTLE_SIM_EXECUTION_LOOP_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_execution_loop.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")

const ACTION_THRESHOLD := 120
const TIMELINE_TICKS_PER_STEP := 1
const TIMELINE_TU_PER_TICK := 5
const MAX_ITERATIONS := 5000

const DEFAULT_OUTPUT_DIR := "res://tests/battle_runtime/benchmarks/profiles/"

const SCENARIOS := {
	&"small_4v8": {
		"map_size": Vector2i(20, 14),
		"target_tu": 200,
		"ally_count": 4,
		"enemy_count": 8,
	},
	&"medium_6v20": {
		"map_size": Vector2i(20, 14),
		"target_tu": 200,
		"ally_count": 6,
		"enemy_count": 20,
	},
	&"large_6v40": {
		"map_size": Vector2i(20, 14),
		"target_tu": 200,
		"ally_count": 6,
		"enemy_count": 40,
	},
}

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var repeat_count := maxi(_resolve_int_env("BASELINE_REPEAT_COUNT", 2), 2)
	var top_n := maxi(_resolve_int_env("AI_PROFILE_TOP_N", 20), 1)
	var sort_by := _resolve_str_env("AI_PROFILE_SORT", "self_usec")
	var name_filter := _resolve_str_env("AI_PROFILE_FILTER", "")
	var output_dir := _resolve_str_env("AI_PROFILE_OUTPUT_DIR", DEFAULT_OUTPUT_DIR)
	if not output_dir.ends_with("/"):
		output_dir += "/"
	var dump_trace_json := _resolve_str_env("AI_TRACE_JSON", "") == "1"
	var scenario_filter := _resolve_scenario_filter()

	print("[AiProfile] config: scenarios=%s repeat=%d top_n=%d sort=%s filter='%s' trace_json=%s output=%s" % [
		str(scenario_filter), repeat_count, top_n, sort_by, name_filter, str(dump_trace_json), output_dir,
	])

	for scenario_id in scenario_filter:
		var spec: Dictionary = SCENARIOS.get(scenario_id, {})
		if spec.is_empty():
			print("[AiProfile] WARN scenario %s not defined, skipping" % String(scenario_id))
			continue
		_run_scenario(scenario_id, spec, repeat_count, top_n, sort_by, name_filter, output_dir, dump_trace_json)

	if not _failures.is_empty():
		for failure in _failures:
			push_error(failure)
		print("[AiProfile] FAIL (%d failures)" % _failures.size())
		quit(1)
		return
	quit(0)


func _run_scenario(scenario_id: StringName, spec: Dictionary, repeat_count: int, top_n: int, sort_by: String, name_filter: String, output_dir: String, dump_trace_json: bool) -> void:
	print("[AiProfile] scenario=%s starting (target_tu=%d)" % [String(scenario_id), int(spec.get("target_tu", 0))])
	var aggregate_stats: Dictionary = {}
	var measured_ai_turns := 0
	var balanced := true
	var truncated := false
	var trace_events_sample: Array = []

	for run_index in range(repeat_count):
		var is_warmup := (run_index == 0)
		# Create a fresh tracer per measured run; warmup uses null tracer to avoid recording overhead during JIT/cache warmup.
		var recorder = null
		if not is_warmup:
			recorder = AiTraceRecorderScript.new()
			AiTraceRecorderScript.instance = recorder
		var run_meta := _run_pass(scenario_id, spec)
		# Detach recorder before next run.
		AiTraceRecorderScript.instance = null
		if not is_warmup and recorder != null:
			_merge_stats(aggregate_stats, recorder.get_func_stats())
			measured_ai_turns += int(run_meta.get("ai_turns", 0))
			if not recorder.assert_balanced():
				balanced = false
			if recorder.is_truncated():
				truncated = true
			if trace_events_sample.is_empty() and dump_trace_json:
				trace_events_sample = recorder.get_events()
		var phase := "warmup" if is_warmup else "measured"
		print("[AiProfile]   run %d/%d (%s): ai_turns=%d final_tu=%d" % [
			run_index + 1, repeat_count, phase,
			int(run_meta.get("ai_turns", 0)), int(run_meta.get("final_tu", 0)),
		])

	if not balanced:
		_test.fail("scenario %s tracer call stack not balanced (enter/exit pairing broken)" % String(scenario_id))
	if truncated:
		print("[AiProfile] WARN scenario=%s trace events truncated (max_events cap reached)" % String(scenario_id))

	var timestamp := _format_timestamp()
	var basename := "ai_profile_%s_%s" % [String(scenario_id), timestamp]

	# Print top-N to stdout.
	var header := AiHotspotsFormatterScript.format_header(
		String(scenario_id),
		measured_ai_turns,
		AiHotspotsFormatterScript.total_self_usec(aggregate_stats),
		sort_by,
		Engine.get_version_info().get("string", "unknown"),
		_git_commit(),
	)
	var body := AiHotspotsFormatterScript.format_top_n(aggregate_stats, sort_by, top_n, name_filter)
	print(header)
	print(body)

	# Write hotspots.txt + functions.csv (always).
	var hotspots_path := output_dir + basename + ".hotspots.txt"
	var csv_path := output_dir + basename + ".functions.csv"
	var ok_txt := AiHotspotsFormatterScript.write_text_report(hotspots_path, header, body)
	var ok_csv := AiHotspotsFormatterScript.write_csv(csv_path, aggregate_stats)
	if ok_txt:
		print("[AiProfile] wrote %s" % hotspots_path)
	else:
		push_error("[AiProfile] failed to write %s" % hotspots_path)
	if ok_csv:
		print("[AiProfile] wrote %s" % csv_path)
	else:
		push_error("[AiProfile] failed to write %s" % csv_path)

	# Optionally write Chrome Tracing JSON.
	if dump_trace_json and not trace_events_sample.is_empty():
		var trace_path := output_dir + basename + ".trace.json"
		var trace_doc := {
			"traceEvents": trace_events_sample,
			"displayTimeUnit": "us",
			"metadata": {
				"scenario": String(scenario_id),
				"godot_version": Engine.get_version_info().get("string", ""),
				"git_commit": _git_commit(),
			},
		}
		var dir_part := trace_path.get_base_dir()
		if not DirAccess.dir_exists_absolute(dir_part):
			DirAccess.make_dir_recursive_absolute(dir_part)
		var fh := FileAccess.open(trace_path, FileAccess.WRITE)
		if fh != null:
			fh.store_string(JSON.stringify(trace_doc))
			fh.close()
			print("[AiProfile] wrote %s" % trace_path)
		else:
			push_error("[AiProfile] failed to write %s" % trace_path)


func _merge_stats(target: Dictionary, source: Dictionary) -> void:
	for name_variant in source.keys():
		var src: Dictionary = source[name_variant]
		var dst: Dictionary
		if target.has(name_variant):
			dst = target[name_variant]
		else:
			dst = {"ncalls": 0, "self_usec": 0, "total_usec": 0, "max_usec": 0}
		dst["ncalls"] = int(dst["ncalls"]) + int(src.get("ncalls", 0))
		dst["self_usec"] = int(dst["self_usec"]) + int(src.get("self_usec", 0))
		dst["total_usec"] = int(dst["total_usec"]) + int(src.get("total_usec", 0))
		dst["max_usec"] = max(int(dst["max_usec"]), int(src.get("max_usec", 0)))
		target[name_variant] = dst


func _run_pass(scenario_id: StringName, spec: Dictionary) -> Dictionary:
	var runtime = _build_runtime()
	var ai_probe = AI_SERVICE_PROBE_SCRIPT.new()
	ai_probe.setup(runtime._enemy_ai_brains, runtime._damage_resolver)
	runtime._ai_service = ai_probe
	var assembler_probe = AI_ASSEMBLER_PROBE_SCRIPT.new()
	runtime._ai_action_assembler = assembler_probe

	var state = _build_flat_state(spec.get("map_size", Vector2i(20, 14)), scenario_id)
	_populate_units(runtime, state, spec)
	runtime._state = state
	runtime._build_ai_action_plans()

	var execution_loop = BATTLE_SIM_EXECUTION_LOOP_SCRIPT.new()
	var target_tu := int(spec.get("target_tu", 200))
	var iterations := 0
	var ai_turns := 0
	var manual_turns := 0
	var idle_loops := 0

	while iterations < MAX_ITERATIONS:
		iterations += 1
		if state.phase == &"battle_ended" or int(state.timeline.current_tu) >= target_tu:
			break

		var previous_tu := int(state.timeline.current_tu)
		var previous_phase := StringName(state.phase)
		var previous_active_unit_id := StringName(state.active_unit_id)
		var previous_log_count: int = state.log_entries.size()

		if state.phase == &"unit_acting":
			var active_unit = state.units.get(state.active_unit_id) as BattleUnitState
			if active_unit == null or not active_unit.is_alive:
				_test.fail("scenario %s invalid active unit" % String(scenario_id))
				break
			if active_unit.control_mode == &"manual":
				manual_turns += 1
			else:
				ai_turns += 1
			execution_loop.advance_step(runtime, state, &"wait", TIMELINE_TICKS_PER_STEP)
		else:
			execution_loop.advance_step(runtime, state, &"wait", TIMELINE_TICKS_PER_STEP)

		var made_progress: bool = (
			int(state.timeline.current_tu) != previous_tu
			or StringName(state.phase) != previous_phase
			or StringName(state.active_unit_id) != previous_active_unit_id
			or state.log_entries.size() != previous_log_count
		)
		if made_progress:
			idle_loops = 0
		else:
			idle_loops += 1
			if idle_loops >= 25:
				_test.fail("scenario %s stalled at TU=%d phase=%s" % [
					String(scenario_id), int(state.timeline.current_tu), String(state.phase),
				])
				break

	var final_tu := int(state.timeline.current_tu)
	runtime.dispose()

	return {
		"ai_turns": ai_turns,
		"manual_turns": manual_turns,
		"final_tu": final_tu,
		"iterations": iterations,
	}


func _build_runtime():
	var game_session = GAME_SESSION_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(
		null,
		game_session.get_skill_defs(),
		game_session.get_enemy_templates(),
		game_session.get_enemy_ai_brains(),
		null
	)
	game_session.free()
	return runtime


func _build_flat_state(map_size: Vector2i, scenario_id: StringName):
	var state = BATTLE_STATE_SCRIPT.new()
	state.battle_id = StringName("ai_profile_%s" % [String(scenario_id)])
	state.phase = &"timeline_running"
	state.map_size = map_size
	state.timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
	state.timeline.tu_per_tick = TIMELINE_TU_PER_TICK
	state.timeline.frozen = false
	for y in range(map_size.y):
		for x in range(map_size.x):
			var cell = BATTLE_CELL_STATE_SCRIPT.new()
			cell.coord = Vector2i(x, y)
			cell.base_terrain = BATTLE_CELL_STATE_SCRIPT.TERRAIN_LAND
			cell.base_height = 4
			cell.height_offset = 0
			cell.recalculate_runtime_values()
			state.cells[cell.coord] = cell
	state.cell_columns = BATTLE_CELL_STATE_SCRIPT.build_columns_from_surface_cells(state.cells)
	return state


func _populate_units(runtime, state, spec: Dictionary) -> void:
	var ally_count := int(spec.get("ally_count", 2))
	var enemy_count := int(spec.get("enemy_count", 4))
	var ally_anchor_x := 1
	for index in range(ally_count):
		var ally_y := 3 + (index % 6)
		var ally_coord := Vector2i(ally_anchor_x + (index / 6), ally_y)
		var ally_unit = _build_manual_unit(
			StringName("ai_profile_ally_%02d" % [index + 1]),
			"友军%02d" % [index + 1],
			ally_coord
		)
		_add_unit_to_state(runtime, state, ally_unit, false)

	var enemy_positions: Array[Vector2i] = []
	var ex_min := 10
	var ex_max := mini(ex_min + 8, state.map_size.x - 1)
	var ey_min := 3
	var ey_max := mini(ey_min + 6, state.map_size.y - 1)
	for y in range(ey_min, ey_max + 1):
		for x in range(ex_min, ex_max + 1):
			enemy_positions.append(Vector2i(x, y))
	enemy_positions.sort_custom(func(left: Vector2i, right: Vector2i) -> bool:
		return left.y < right.y or (left.y == right.y and left.x < right.x)
	)

	for index in range(enemy_count):
		var coord = enemy_positions[index]
		var enemy_unit: BattleUnitState
		if index % 2 == 0:
			enemy_unit = _build_ai_unit(
				StringName("ai_profile_enemy_%02d" % [index + 1]),
				"狼群%02d" % [index + 1],
				coord,
				&"melee_aggressor",
				[&"charge", &"warrior_heavy_strike"]
			)
		else:
			enemy_unit = _build_ai_unit(
				StringName("ai_profile_enemy_%02d" % [index + 1]),
				"猎压者%02d" % [index + 1],
				coord,
				&"ranged_suppressor",
				[&"archer_suppressive_fire", &"archer_pinning_shot"]
			)
		_add_unit_to_state(runtime, state, enemy_unit, true)


func _build_manual_unit(unit_id: StringName, display_name: String, coord: Vector2i) -> BattleUnitState:
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = &"player"
	unit.control_mode = &"manual"
	unit.current_hp = 260
	unit.current_mp = 120
	unit.current_stamina = 120
	unit.current_aura = 120
	unit.current_ap = 2
	unit.action_threshold = ACTION_THRESHOLD
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 260)
	unit.attribute_snapshot.set_value(&"mp_max", 120)
	unit.attribute_snapshot.set_value(&"stamina_max", 120)
	unit.attribute_snapshot.set_value(&"aura_max", 120)
	unit.attribute_snapshot.set_value(&"action_points", 2)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 12)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 20)
	return unit


func _build_ai_unit(unit_id: StringName, display_name: String, coord: Vector2i, brain_id: StringName, skill_ids: Array[StringName]) -> BattleUnitState:
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = &"enemy"
	unit.control_mode = &"ai"
	unit.ai_brain_id = brain_id
	unit.ai_state_id = &""
	unit.current_hp = 180
	unit.current_mp = 120
	unit.current_stamina = 120
	unit.current_aura = 120
	unit.current_ap = 2
	unit.action_threshold = ACTION_THRESHOLD
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 180)
	unit.attribute_snapshot.set_value(&"mp_max", 120)
	unit.attribute_snapshot.set_value(&"stamina_max", 120)
	unit.attribute_snapshot.set_value(&"aura_max", 120)
	unit.attribute_snapshot.set_value(&"action_points", 2)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 16)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 18)
	unit.known_active_skill_ids = skill_ids.duplicate()
	for skill_id in unit.known_active_skill_ids:
		unit.known_skill_level_map[skill_id] = 1
	return unit


func _add_unit_to_state(runtime, state, unit: BattleUnitState, is_enemy: bool) -> void:
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)
	var placed: bool = runtime._grid_service.place_unit(state, unit, unit.coord, true)
	if not placed:
		_test.fail("AI profile unit %s could not be placed." % String(unit.unit_id))


func _resolve_scenario_filter() -> Array:
	var raw := OS.get_environment("BASELINE_SCENARIOS")
	if raw.is_empty():
		return [&"medium_6v20"]
	var ids: Array = []
	for token in raw.split(","):
		var t := token.strip_edges()
		if t.is_empty():
			continue
		ids.append(StringName(t))
	return ids


func _resolve_int_env(name: String, default_value: int) -> int:
	var raw := OS.get_environment(name)
	if raw.is_empty():
		return default_value
	return int(raw)


func _resolve_str_env(name: String, default_value: String) -> String:
	var raw := OS.get_environment(name)
	if raw.is_empty():
		return default_value
	return raw


func _git_commit() -> String:
	var head := FileAccess.open("res://.git/HEAD", FileAccess.READ)
	if head == null:
		return "unknown"
	var line := head.get_as_text().strip_edges()
	head.close()
	if line.begins_with("ref: "):
		var ref_path := "res://.git/" + line.substr(5).strip_edges()
		var ref_file := FileAccess.open(ref_path, FileAccess.READ)
		if ref_file == null:
			return "unknown"
		var sha := ref_file.get_as_text().strip_edges()
		ref_file.close()
		if sha.length() >= 7:
			return sha.substr(0, 7)
		return sha
	if line.length() >= 7:
		return line.substr(0, 7)
	return "unknown"


func _format_timestamp() -> String:
	var t := Time.get_datetime_dict_from_system()
	return "%04d%02d%02d_%02d%02d%02d" % [
		int(t["year"]), int(t["month"]), int(t["day"]),
		int(t["hour"]), int(t["minute"]), int(t["second"]),
	]
