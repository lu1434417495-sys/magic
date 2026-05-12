extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const AI_SERVICE_PROBE_SCRIPT = preload("res://tests/battle_runtime/benchmarks/ai_service_probe.gd")
const AI_ASSEMBLER_PROBE_SCRIPT = preload("res://tests/battle_runtime/benchmarks/ai_assembler_probe.gd")
const AiBaselineDiffScript = preload("res://tests/battle_runtime/benchmarks/ai_baseline_diff.gd")

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

const BASELINE_PATH := "res://tests/battle_runtime/benchmarks/baselines/ai_baseline.json"

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
	var update_baseline := OS.get_environment("UPDATE_BASELINE") == "1"
	var tolerance_pct := _resolve_float_env("BASELINE_TOLERANCE_PCT", AiBaselineDiffScript.DEFAULT_TOLERANCE_PCT)
	var repeat_count := maxi(_resolve_int_env("BASELINE_REPEAT_COUNT", 3), 2)  # 1 warmup + (N-1) measured
	var scenario_filter := _resolve_scenario_filter()

	print("[AiBaseline] config: update_baseline=%s tolerance=±%.1f%% repeat=%d scenarios=%s" % [
		str(update_baseline), tolerance_pct, repeat_count, str(scenario_filter),
	])

	var scenarios_doc: Dictionary = {}
	for scenario_id in scenario_filter:
		var spec: Dictionary = SCENARIOS.get(scenario_id, {})
		if spec.is_empty():
			print("[AiBaseline] WARN scenario %s not defined, skipping" % String(scenario_id))
			continue
		print("[AiBaseline] scenario=%s starting (target_tu=%d)" % [String(scenario_id), int(spec.get("target_tu", 0))])
		var scenario_result := _run_scenario(scenario_id, spec, repeat_count)
		scenarios_doc[String(scenario_id)] = scenario_result
		print(_format_scenario_summary(scenario_id, scenario_result))

	if not _failures.is_empty():
		for failure in _failures:
			push_error(failure)
		print("[AiBaseline] FAIL (%d failures)" % _failures.size())
		quit(1)
		return

	var current_doc := AiBaselineDiffScript.build_baseline_doc(scenarios_doc, _git_commit())

	if update_baseline:
		var ok := AiBaselineDiffScript.write_baseline(BASELINE_PATH, current_doc)
		if not ok:
			push_error("[AiBaseline] failed to write baseline at %s" % BASELINE_PATH)
			quit(1)
			return
		print("[BASELINE] wrote %s" % BASELINE_PATH)
		quit(0)
		return

	# Diff mode.
	var baseline := AiBaselineDiffScript.read_baseline(BASELINE_PATH)
	if baseline.is_empty():
		print("[AiBaseline] no baseline found at %s — run with UPDATE_BASELINE=1 first." % BASELINE_PATH)
		# Print current numbers anyway so user can see them.
		print(JSON.stringify(current_doc, "\t", true))
		quit(0)
		return

	if int(baseline.get("schema_version", 0)) != AiBaselineDiffScript.SCHEMA_VERSION:
		push_error("[AiBaseline] baseline schema mismatch (file=%d, expected=%d). Set UPDATE_BASELINE=1 to rewrite." % [
			int(baseline.get("schema_version", 0)),
			AiBaselineDiffScript.SCHEMA_VERSION,
		])
		quit(1)
		return

	var diffs := AiBaselineDiffScript.compare(baseline, current_doc, tolerance_pct)
	var report := AiBaselineDiffScript.format_diff_report(diffs, tolerance_pct)
	print(report)
	var regressions := AiBaselineDiffScript.count_regressions(diffs)
	if regressions > 0:
		push_error("[AiBaseline] %d regression(s) over tolerance" % regressions)
		quit(1)
		return
	print("[AiBaseline] OK (no regressions over tolerance)")
	quit(0)


func _run_scenario(scenario_id: StringName, spec: Dictionary, repeat_count: int) -> Dictionary:
	var per_run_choose: Array = []
	var per_run_skill: Array = []
	var per_run_action: Array = []
	var per_run_assemble: Array = []
	var per_run_meta: Array = []

	for run_index in range(repeat_count):
		var is_warmup := (run_index == 0 and repeat_count >= 2)
		var run_result := _run_pass(scenario_id, spec)
		if not is_warmup:
			per_run_choose.append(run_result["stats_choose"])
			per_run_skill.append(run_result["stats_skill_input"])
			per_run_action.append(run_result["stats_action_input"])
			per_run_assemble.append(run_result["stats_assemble"])
			per_run_meta.append({
				"ai_turns": run_result["ai_turns"],
				"final_tu": run_result["final_tu"],
				"battle_ended": run_result["battle_ended"],
				"winner": run_result["winner_faction_id"],
			})
		var phase := "warmup" if is_warmup else "measured"
		print("[AiBaseline]   run %d/%d (%s): ai_turns=%d final_tu=%d battle_ended=%s" % [
			run_index + 1, repeat_count, phase,
			int(run_result["ai_turns"]), int(run_result["final_tu"]),
			str(bool(run_result["battle_ended"])),
		])

	var merged_choose := AiBaselineDiffScript.merge_runs(per_run_choose)
	var merged_skill := AiBaselineDiffScript.merge_runs(per_run_skill)
	var merged_action := AiBaselineDiffScript.merge_runs(per_run_action)
	var merged_assemble := AiBaselineDiffScript.merge_runs(per_run_assemble)

	var summary_choose := AiBaselineDiffScript.summarize_stats(merged_choose)
	var summary_skill := AiBaselineDiffScript.summarize_stats(merged_skill)
	var summary_action := AiBaselineDiffScript.summarize_stats(merged_action)
	var summary_assemble := AiBaselineDiffScript.summarize_stats(merged_assemble)

	# Inclusive / self approximation: choose_command is the top wrap and includes the score sub-layers.
	var choose_inclusive_usec := int(merged_choose.get("total_usec", 0))
	var skill_total := int(merged_skill.get("total_usec", 0))
	var action_total := int(merged_action.get("total_usec", 0))
	summary_choose["total_inclusive_usec"] = choose_inclusive_usec
	summary_choose["total_self_usec"] = max(choose_inclusive_usec - skill_total - action_total, 0)

	var ai_turns_total := 0
	var ai_turns_per_run: Array = []
	for meta in per_run_meta:
		ai_turns_total += int(meta.get("ai_turns", 0))
		ai_turns_per_run.append(int(meta.get("ai_turns", 0)))

	return {
		"target_tu": int(spec.get("target_tu", 0)),
		"repeat_measured": per_run_meta.size(),
		"ai_turns_total": ai_turns_total,
		"ai_turns_per_run": ai_turns_per_run,
		"runs_meta": per_run_meta,
		"layers": {
			"choose_command": summary_choose,
			"build_skill_score_input": summary_skill,
			"build_action_score_input": summary_action,
			"build_unit_action_plan": summary_assemble,
		},
	}


func _run_pass(scenario_id: StringName, spec: Dictionary) -> Dictionary:
	var runtime = _build_runtime()
	var ai_probe = AI_SERVICE_PROBE_SCRIPT.new()
	ai_probe.setup(runtime._enemy_ai_brains, runtime._damage_resolver)
	runtime._ai_service = ai_probe
	var assembler_probe = AI_ASSEMBLER_PROBE_SCRIPT.new()
	runtime._ai_action_assembler = assembler_probe

	var state = _build_flat_state(spec.get("map_size", Vector2i(20, 14)), scenario_id)
	_populate_units(runtime, state, scenario_id, spec)
	runtime._state = state
	# Trigger assembler probe so build_unit_action_plan gets called.
	runtime._build_ai_action_plans()

	var execution_loop = BATTLE_SIM_EXECUTION_LOOP_SCRIPT.new()
	var target_tu := int(spec.get("target_tu", 100))
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

	var winner_faction_id := String(state.winner_faction_id)
	var battle_ended: bool = state.phase == &"battle_ended"
	var final_tu := int(state.timeline.current_tu)
	runtime.dispose()

	return {
		"stats_choose": ai_probe.stats_choose,
		"stats_skill_input": ai_probe.stats_skill_input,
		"stats_action_input": ai_probe.stats_action_input,
		"stats_assemble": assembler_probe.stats_assemble,
		"ai_turns": ai_turns,
		"manual_turns": manual_turns,
		"final_tu": final_tu,
		"battle_ended": battle_ended,
		"winner_faction_id": winner_faction_id,
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
	state.battle_id = StringName("ai_baseline_%s" % [String(scenario_id)])
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


func _populate_units(runtime, state, scenario_id: StringName, spec: Dictionary) -> void:
	var ally_count := int(spec.get("ally_count", 2))
	var enemy_count := int(spec.get("enemy_count", 4))

	var ally_anchor_x := 1
	for index in range(ally_count):
		var ally_y := 3 + (index % 6)
		var ally_coord := Vector2i(ally_anchor_x + (index / 6), ally_y)
		var ally_unit = _build_manual_unit(
			StringName("ai_baseline_ally_%02d" % [index + 1]),
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
				StringName("ai_baseline_enemy_%02d" % [index + 1]),
				"狼群%02d" % [index + 1],
				coord,
				&"melee_aggressor",
				[&"charge", &"warrior_heavy_strike"]
			)
		else:
			enemy_unit = _build_ai_unit(
				StringName("ai_baseline_enemy_%02d" % [index + 1]),
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
		_test.fail("AI baseline unit %s could not be placed." % String(unit.unit_id))


func _format_scenario_summary(scenario_id: StringName, scenario: Dictionary) -> String:
	var layers: Dictionary = scenario.get("layers", {})
	var choose: Dictionary = layers.get("choose_command", {})
	var skill: Dictionary = layers.get("build_skill_score_input", {})
	var action: Dictionary = layers.get("build_action_score_input", {})
	var assemble: Dictionary = layers.get("build_unit_action_plan", {})
	return "[AiBaseline] %s  ai_turns=%d  choose: n=%d avg=%dus p50=%dus p95=%dus max=%dus self=%dus  skill: n=%d avg=%dus p95=%dus  action: n=%d avg=%dus p95=%dus  assemble: n=%d avg=%dus" % [
		String(scenario_id),
		int(scenario.get("ai_turns_total", 0)),
		int(choose.get("call_count", 0)),
		int(choose.get("avg_usec", 0)),
		int(choose.get("p50_usec", 0)),
		int(choose.get("p95_usec", 0)),
		int(choose.get("max_usec", 0)),
		int(choose.get("total_self_usec", 0)),
		int(skill.get("call_count", 0)),
		int(skill.get("avg_usec", 0)),
		int(skill.get("p95_usec", 0)),
		int(action.get("call_count", 0)),
		int(action.get("avg_usec", 0)),
		int(action.get("p95_usec", 0)),
		int(assemble.get("call_count", 0)),
		int(assemble.get("avg_usec", 0)),
	]


func _resolve_scenario_filter() -> Array:
	var raw := OS.get_environment("BASELINE_SCENARIOS")
	if raw.is_empty():
		return SCENARIOS.keys()
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


func _resolve_float_env(name: String, default_value: float) -> float:
	var raw := OS.get_environment(name)
	if raw.is_empty():
		return default_value
	return float(raw)


func _git_commit() -> String:
	# Best-effort: read .git/HEAD then resolve.
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
