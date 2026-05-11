extends SceneTree

const BATTLE_SIM_REPORT_BUILDER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_report_builder.gd")
const BATTLE_SIM_PROFILE_DEF_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_profile_def.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_profile_summary_exposes_skill_attempt_and_failure_totals()
	_test_profile_comparisons_expose_attempt_and_failure_deltas()
	if _failures.is_empty():
		print("Battle sim report builder regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle sim report builder regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_profile_summary_exposes_skill_attempt_and_failure_totals() -> void:
	var builder = BATTLE_SIM_REPORT_BUILDER_SCRIPT.new()
	var profile = _build_profile(&"baseline", "Baseline")
	var summary: Dictionary = builder.build_profile_summary(profile, [_build_run_a(), _build_run_b()])
	_assert_eq(int(summary.get("skill_usage_totals", {}).get("skill_alpha", -1)), 3, "skill_alpha 成功次数应汇总两场 run。")
	_assert_eq(int(summary.get("skill_usage_totals", {}).get("skill_beta", -1)), 1, "skill_beta 成功次数应被正确保留。")
	_assert_eq(int(summary.get("skill_attempt_totals", {}).get("skill_alpha", -1)), 4, "skill_alpha 尝试次数应汇总两场 run。")
	_assert_eq(int(summary.get("skill_attempt_totals", {}).get("skill_beta", -1)), 1, "skill_beta 尝试次数应被正确保留。")
	_assert_eq(int(summary.get("skill_attempt_totals", {}).get("skill_gamma", -1)), 2, "skill_gamma 纯失败尝试也应被汇总。")
	_assert_eq(int(summary.get("skill_failure_totals", {}).get("skill_alpha", -1)), 1, "skill_alpha 失败次数应等于 attempt-success。")
	_assert_eq(int(summary.get("skill_failure_totals", {}).get("skill_gamma", -1)), 2, "skill_gamma 全失败时应保留全部失败次数。")
	_assert_true(not summary.get("skill_failure_totals", {}).has("skill_beta"), "零失败技能不应写入 failure_totals。")
	_assert_eq(float(summary.get("average_timeline_steps", -1.0)), 3.0, "timeline_steps 应按 run 平均汇总。")


func _test_profile_comparisons_expose_attempt_and_failure_deltas() -> void:
	var builder = BATTLE_SIM_REPORT_BUILDER_SCRIPT.new()
	var baseline_summary: Dictionary = builder.build_profile_summary(_build_profile(&"baseline", "Baseline"), [_build_run_a(), _build_run_b()])
	var candidate_summary: Dictionary = builder.build_profile_summary(_build_profile(&"candidate", "Candidate"), [_build_run_candidate()])
	var comparisons: Array[Dictionary] = builder.build_profile_comparisons([
		{
			"summary": baseline_summary,
		},
		{
			"summary": candidate_summary,
		},
	])
	_assert_eq(comparisons.size(), 1, "两组 summary 应产出一条 comparison。")
	if comparisons.is_empty():
		return
	var comparison: Dictionary = comparisons[0]
	_assert_eq(int(comparison.get("skill_attempt_delta", {}).get("skill_alpha", 999)), -1, "candidate 的 skill_alpha 尝试次数较 baseline 应少 1。")
	_assert_eq(int(comparison.get("skill_failure_delta", {}).get("skill_alpha", 999)), -1, "candidate 的 skill_alpha 失败次数较 baseline 应少 1。")
	_assert_eq(int(comparison.get("skill_attempt_delta", {}).get("skill_gamma", 999)), -2, "candidate 不再尝试 skill_gamma 时，attempt delta 应为 -2。")
	_assert_eq(int(comparison.get("skill_failure_delta", {}).get("skill_gamma", 999)), -2, "candidate 不再失败 skill_gamma 时，failure delta 应为 -2。")
	_assert_eq(float(comparison.get("average_timeline_steps_delta", 999.0)), -1.0, "candidate 的平均 timeline_steps 应比 baseline 少 1。")


func _build_profile(profile_id: StringName, display_name: String):
	var profile = BATTLE_SIM_PROFILE_DEF_SCRIPT.new()
	profile.profile_id = profile_id
	profile.display_name = display_name
	return profile


func _build_run_a() -> Dictionary:
	return {
		"winner_faction_id": "player",
		"final_tu": 10,
		"iterations": 5,
		"timeline_steps": 2,
		"metrics": {
			"units": {
				"unit_a": {
					"skill_attempt_counts": {
						"skill_alpha": 3,
						"skill_beta": 1,
					},
					"skill_success_counts": {
						"skill_alpha": 2,
						"skill_beta": 1,
					},
				},
			},
			"factions": {},
		},
		"ai_turn_traces": [],
	}


func _build_run_b() -> Dictionary:
	return {
		"winner_faction_id": "hostile",
		"final_tu": 20,
		"iterations": 8,
		"timeline_steps": 4,
		"metrics": {
			"units": {
				"unit_b": {
					"skill_attempt_counts": {
						"skill_alpha": 1,
						"skill_gamma": 2,
					},
					"skill_success_counts": {
						"skill_alpha": 1,
					},
				},
			},
			"factions": {},
		},
		"ai_turn_traces": [],
	}


func _build_run_candidate() -> Dictionary:
	return {
		"winner_faction_id": "player",
		"final_tu": 9,
		"iterations": 4,
		"timeline_steps": 2,
		"metrics": {
			"units": {
				"unit_candidate": {
					"skill_attempt_counts": {
						"skill_alpha": 3,
						"skill_beta": 1,
					},
					"skill_success_counts": {
						"skill_alpha": 3,
						"skill_beta": 1,
					},
				},
			},
			"factions": {},
		},
		"ai_turn_traces": [],
	}


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
