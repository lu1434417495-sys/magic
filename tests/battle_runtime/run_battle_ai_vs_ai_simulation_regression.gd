extends SceneTree

const BATTLE_SIM_RUNNER_SCRIPT = preload("res://scripts/systems/battle_sim_runner.gd")

const AI_VS_AI_SCENARIO_PATH := "res://data/configs/battle_sim/scenarios/ai_vs_ai_duel_example.tres"
const BASELINE_PROFILE_PATH := "res://data/configs/battle_sim/profiles/baseline.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var scenario = load(AI_VS_AI_SCENARIO_PATH)
	var baseline_profile = load(BASELINE_PROFILE_PATH)
	_assert_true(scenario != null and scenario.has_method("build_start_context"), "AI vs AI 示例场景资源应能被 BattleSimScenarioDef 正常加载。")
	_assert_true(baseline_profile != null, "AI vs AI regression 应能加载 baseline profile。")
	if scenario == null or baseline_profile == null:
		_finish()
		return

	var runner = BATTLE_SIM_RUNNER_SCRIPT.new()
	var report: Dictionary = runner.run_scenario(scenario, [baseline_profile])
	var profile_entries: Array = report.get("profile_entries", [])
	_assert_true(profile_entries.size() == 1, "单 profile 的 AI vs AI 示例应只产出 1 个 profile entry。")
	_assert_true((report.get("comparisons", []) as Array).is_empty(), "单 profile 的 AI vs AI 示例不应生成 comparison。")
	_assert_true(String(report.get("output_files", {}).get("report_json", "")) != "", "AI vs AI simulation 应写出主 report json。")
	_assert_true(String(report.get("output_files", {}).get("turn_trace_jsonl", "")) != "", "AI vs AI simulation 应写出 AI trace jsonl。")

	var saw_player_ai_trace := false
	var saw_hostile_ai_trace := false
	if not profile_entries.is_empty():
		var baseline_entry: Dictionary = profile_entries[0]
		var runs: Array = baseline_entry.get("runs", [])
		_assert_true(runs.size() == 2, "AI vs AI 示例应按场景中的 2 个 seeds 跑满 2 场战斗。")
		for run_entry in runs:
			if run_entry is not Dictionary:
				continue
			var run: Dictionary = run_entry
			_assert_true(bool(run.get("battle_ended", false)), "AI vs AI 示例场次应能在 max_iterations 内正常结束。")
			_assert_true(String(run.get("winner_faction_id", "")) != "", "AI vs AI 示例场次结束后应有明确的 winner_faction_id。")
			var ai_turn_traces: Array = run.get("ai_turn_traces", [])
			_assert_true(not ai_turn_traces.is_empty(), "AI vs AI 示例场次应至少收集到 1 条 AI turn trace。")
			for trace_entry in ai_turn_traces:
				if trace_entry is not Dictionary:
					continue
				var trace: Dictionary = trace_entry
				var faction_id := String(trace.get("faction_id", ""))
				if faction_id == "player":
					saw_player_ai_trace = true
				elif faction_id == "hostile":
					saw_hostile_ai_trace = true
			var final_units: Array = run.get("final_units", [])
			for unit_entry in final_units:
				if unit_entry is not Dictionary:
					continue
				_assert_true(String((unit_entry as Dictionary).get("control_mode", "")) == "ai", "AI vs AI 示例中的最终单位快照不应出现 manual control_mode。")

		var summary: Dictionary = baseline_entry.get("summary", {})
		var wins_by_faction: Dictionary = summary.get("wins_by_faction", {})
		var faction_metric_totals: Dictionary = summary.get("faction_metric_totals", {})
		_assert_true(not wins_by_faction.is_empty(), "AI vs AI 示例应产出非空的 wins_by_faction 统计。")
		_assert_true(faction_metric_totals.has("player"), "AI vs AI summary 应包含 player 阵营 metrics。")
		_assert_true(faction_metric_totals.has("hostile"), "AI vs AI summary 应包含 hostile 阵营 metrics。")
		if faction_metric_totals.has("player"):
			_assert_true(int((faction_metric_totals.get("player", {}) as Dictionary).get("turn_count", 0)) > 0, "player AI 在 AI vs AI 示例中应至少行动 1 次。")
		if faction_metric_totals.has("hostile"):
			_assert_true(int((faction_metric_totals.get("hostile", {}) as Dictionary).get("turn_count", 0)) > 0, "hostile AI 在 AI vs AI 示例中应至少行动 1 次。")
		_assert_true(not (summary.get("action_choice_counts", {}) as Dictionary).is_empty(), "AI vs AI summary 应包含 action_choice_counts。")
	_assert_true(saw_player_ai_trace, "AI vs AI 示例应记录到 player 阵营的 AI turn trace。")
	_assert_true(saw_hostile_ai_trace, "AI vs AI 示例应记录到 hostile 阵营的 AI turn trace。")
	_finish()


func _finish() -> void:
	if _failures.is_empty():
		print("Battle AI vs AI simulation regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle AI vs AI simulation regression: FAIL (%d)" % _failures.size())
	quit(1)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)
