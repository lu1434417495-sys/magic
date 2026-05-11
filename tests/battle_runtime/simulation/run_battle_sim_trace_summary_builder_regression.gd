extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BATTLE_SIM_TRACE_SUMMARY_BUILDER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_trace_summary_builder.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_compacts_top_level_analysis_report()
	_test_compacts_runner_profile_report()
	if _failures.is_empty():
		print("Battle sim trace summary builder regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle sim trace summary builder regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_compacts_top_level_analysis_report() -> void:
	var builder = BATTLE_SIM_TRACE_SUMMARY_BUILDER_SCRIPT.new()
	var report := {
		"batch_id": 7,
		"run_count": 1,
		"win_rate": {"player": 0, "hostile": 1},
		"runs": [_build_run()],
	}
	var summary: Dictionary = builder.build(report, "res://full_report.json")
	_assert_true(builder.has_traces(report), "builder 应能识别 top-level runs 中的 trace。")
	_assert_eq(String(summary.get("source_report", "")), "res://full_report.json", "summary 应保留完整报告路径。")
	_assert_eq(int(summary.get("trace_count", 0)), 2, "summary 应统计 trace 数。")
	var compact_run: Dictionary = (summary.get("runs", []) as Array)[0]
	_assert_eq(int(compact_run.get("trace_count", 0)), 2, "compact run 应统计自身 trace 数。")
	_assert_eq(int(compact_run.get("command_counts_by_faction", {}).get("player", {}).get("wait", 0)), 1, "player wait command 应被聚合。")
	_assert_eq(int(compact_run.get("block_reasons_by_faction", {}).get("player", {}).get("体力不足，无法施放该技能。", 0)), 1, "player 体力不足阻断应被聚合。")
	_assert_eq((compact_run.get("focus_turns", []) as Array).size(), 1, "默认 focus faction 应只保留 player 回合。")
	_assert_eq((compact_run.get("focus_wait_turns", []) as Array).size(), 1, "默认 focus wait 应只保留 player wait。")
	var focus_turn: Dictionary = (compact_run.get("focus_turns", []) as Array)[0]
	var compact_score_input: Dictionary = focus_turn.get("score", {})
	var save_estimates_by_target: Dictionary = compact_score_input.get("save_estimates_by_target_id", {})
	_assert_true(save_estimates_by_target.has("enemy_sword"), "summary 应保留 score_input 中的目标豁免概率估算。")
	if save_estimates_by_target.has("enemy_sword"):
		var save_estimates: Array = save_estimates_by_target.get("enemy_sword", [])
		_assert_eq(int((save_estimates[0] as Dictionary).get("save_success_rate_percent", 0)), 50, "summary 应保留豁免成功率。")
		_assert_eq(int((save_estimates[0] as Dictionary).get("damage_after_save_estimate", 0)), 9, "summary 应保留豁免加权后的期望伤害。")
	_assert_true(bool(compact_score_input.get("has_post_action_threat_projection", false)), "summary 应保留行动后威胁投影标记。")
	_assert_eq(int(compact_score_input.get("post_action_remaining_threat_expected_damage", 0)), 7, "summary 应保留行动后剩余威胁期望伤害。")
	_assert_eq((compact_score_input.get("post_action_remaining_threat_unit_ids", []) as Array).size(), 1, "summary 应保留剩余威胁单位列表。")
	var decision_target_snapshots: Array = focus_turn.get("decision_target_snapshots", [])
	_assert_eq(decision_target_snapshots.size(), 1, "summary 应保留决策目标执行前快照。")
	if not decision_target_snapshots.is_empty():
		_assert_eq(int((decision_target_snapshots[0] as Dictionary).get("hp", 0)), 12, "目标快照应保留执行前 HP。")
	var execution_result: Dictionary = focus_turn.get("execution_result", {})
	var unit_results: Array = execution_result.get("unit_results", [])
	_assert_eq(unit_results.size(), 1, "summary 应保留执行结果中的单位资源变化。")
	if not unit_results.is_empty():
		var unit_result: Dictionary = unit_results[0]
		_assert_eq(int(unit_result.get("hp_damage", 0)), 7, "执行结果应保留实际 HP 伤害。")
		_assert_eq(int((unit_result.get("after", {}) as Dictionary).get("hp", 0)), 5, "执行结果应保留执行后 HP。")
	var action_trace: Dictionary = (focus_turn.get("action_traces", []) as Array)[0]
	_assert_eq((action_trace.get("top_candidates", []) as Array).size(), 2, "action trace 默认只保留前 2 个 candidate。")
	var top_candidate: Dictionary = (action_trace.get("top_candidates", []) as Array)[0]
	_assert_eq(int(top_candidate.get("screening_bonus", 0)), 45, "summary 应保留 screening_bonus 便于分析守线命中。")
	_assert_eq(String(top_candidate.get("screening_threat_unit_id", "")), "enemy_sword", "summary 应保留守线威胁单位。")
	_assert_eq(String(top_candidate.get("screening_protected_unit_id", "")), "hero_archer", "summary 应保留被保护单位。")
	_assert_true(bool(top_candidate.get("screening_can_counterattack", false)), "summary 应保留守线后能否反击。")
	_assert_true(bool(top_candidate.get("screening_hard_block", false)), "summary 应保留守线是否造成硬阻断。")
	_assert_true(bool(top_candidate.get("screening_distance_band_capped", false)), "summary 应保留守线分是否被接敌距离带截断。")


func _test_compacts_runner_profile_report() -> void:
	var builder = BATTLE_SIM_TRACE_SUMMARY_BUILDER_SCRIPT.new()
	var report := {
		"scenario": {"scenario_id": "sample"},
		"profile_entries": [
			{
				"profile": {"profile_id": "baseline"},
				"summary": {"average_iterations": 3.0},
				"runs": [_build_run()],
			},
		],
	}
	var summary: Dictionary = builder.build(report, "user://sample_report.json", {
		"top_candidates_per_action": 1,
	})
	_assert_true(builder.has_traces(report), "builder 应能识别 profile_entries 中的 trace。")
	_assert_eq(int(summary.get("profile_count", 0)), 1, "profile_count 应来自 profile_entries。")
	var compact_run: Dictionary = (summary.get("runs", []) as Array)[0]
	_assert_eq(String(compact_run.get("profile_id", "")), "baseline", "compact run 应保留 profile_id。")
	var action_trace: Dictionary = ((compact_run.get("focus_turns", []) as Array)[0].get("action_traces", []) as Array)[0]
	_assert_eq((action_trace.get("top_candidates", []) as Array).size(), 1, "自定义 top candidate 上限应生效。")


func _build_run() -> Dictionary:
	return {
		"run_index": 0,
		"seed": 123,
		"winner_faction_id": "hostile",
		"iterations": 9,
		"timeline_steps": 4,
		"factions": {"player": {"turn_count": 1}, "hostile": {"turn_count": 1}},
		"units": {},
		"ai_turn_traces": [
			{
				"turn_started_tu": 10,
				"unit_id": "hero",
				"unit_name": "Hero",
				"faction_id": "player",
				"brain_id": "melee_aggressor",
				"state_id": "engage",
				"action_id": "wait_action",
				"reason_text": "wait",
				"command": {"command_type": "wait", "unit_id": "hero"},
				"score_input": {
					"total_score": -40,
					"command_type": "wait",
					"estimated_lethal_target_ids": ["enemy_sword"],
					"estimated_lethal_threat_target_ids": ["enemy_sword"],
					"estimated_control_target_ids": ["enemy_archer"],
					"estimated_control_threat_target_ids": ["enemy_archer"],
					"has_post_action_threat_projection": true,
					"projected_actor_coord": "(2, 2)",
					"pre_action_threat_unit_ids": ["enemy_sword", "enemy_archer"],
					"pre_action_threat_count": 2,
					"pre_action_threat_expected_damage": 19,
					"pre_action_survival_margin": -4,
					"pre_action_is_lethal_survival_risk": true,
					"post_action_remaining_threat_unit_ids": ["enemy_archer"],
					"post_action_remaining_threat_count": 1,
					"post_action_remaining_threat_expected_damage": 7,
					"post_action_survival_margin": 8,
					"post_action_is_lethal_survival_risk": false,
					"save_estimates_by_target_id": {
						"enemy_sword": [
							{
								"damage_before_save": 12,
								"damage_after_save_estimate": 9,
								"damage_on_save_success": 6,
								"save_success_rate_percent": 50,
								"dc": 11,
								"ability": "agility",
								"save_tag": "fireball",
								"advantage_state": "normal",
								"immune": false,
								"hit_count": 1,
							},
						],
					},
				},
				"decision_target_snapshots": [
					{"unit_id": "enemy_sword", "display_name": "Enemy Sword", "faction_id": "hostile", "coord": "(1, 1)", "alive": true, "hp": 12, "hp_max": 20, "shield_hp": 0, "shield_max_hp": 0, "ap": 1, "move_points": 2},
				],
				"execution_result": {
					"command_type": "skill",
					"skill_id": "sample_skill",
					"changed_unit_ids": ["enemy_sword"],
					"tracked_unit_ids": ["enemy_sword"],
					"unit_results": [
						{
							"unit_id": "enemy_sword",
							"before": {"unit_id": "enemy_sword", "display_name": "Enemy Sword", "faction_id": "hostile", "coord": "(1, 1)", "alive": true, "hp": 12, "hp_max": 20, "shield_hp": 0, "shield_max_hp": 0, "ap": 1, "move_points": 2},
							"after": {"unit_id": "enemy_sword", "display_name": "Enemy Sword", "faction_id": "hostile", "coord": "(1, 1)", "alive": true, "hp": 5, "hp_max": 20, "shield_hp": 0, "shield_max_hp": 0, "ap": 1, "move_points": 2},
							"hp_delta": -7,
							"hp_damage": 7,
							"hp_healing": 0,
							"shield_delta": 0,
							"shield_damage": 0,
							"shield_restored": 0,
							"killed": false,
							"revived": false,
							"moved": false,
						},
					],
					"log_lines": ["Hero 对 Enemy Sword 造成 7 点伤害。"],
					"report_entries": [],
				},
				"action_traces": [
					{
						"trace_id": "skill_1",
						"action_id": "skill_action",
						"block_reasons": {"体力不足，无法施放该技能。": 1},
						"blocked_count": 1,
						"candidate_count": 3,
						"top_candidates": [
							{
								"label": "a",
								"total_score": 3,
								"command": {"command_type": "skill", "skill_id": "a"},
								"score_input": {"total_score": 3},
								"screening_bonus": 45,
								"screening_penalty": 0,
								"screening_path_cost_delta": 1,
								"screening_base_path_cost": 3,
								"screening_blocked_path_cost": 4,
								"screening_current_bonus": 0,
								"screening_candidate_bonus": 45,
								"screening_uncapped_bonus": 75,
								"screening_threat_unit_id": "enemy_sword",
								"screening_protected_unit_id": "hero_archer",
								"screening_on_shortest_path": false,
								"screening_keeps_contact": true,
								"screening_can_counterattack": true,
								"screening_hard_block": true,
								"screening_distance_band_capped": true,
							},
							{"label": "b", "total_score": 2, "command": {"command_type": "skill", "skill_id": "b"}, "score_input": {"total_score": 2}},
							{"label": "c", "total_score": 1, "command": {"command_type": "skill", "skill_id": "c"}, "score_input": {"total_score": 1}},
						],
					},
				],
			},
			{
				"turn_started_tu": 15,
				"unit_id": "enemy",
				"unit_name": "Enemy",
				"faction_id": "hostile",
				"brain_id": "melee_aggressor",
				"state_id": "engage",
				"action_id": "attack",
				"reason_text": "attack",
				"command": {"command_type": "skill", "unit_id": "enemy", "skill_id": "basic_attack"},
				"score_input": {"total_score": 10, "command_type": "skill", "skill_id": "basic_attack"},
				"action_traces": [],
			},
		],
	}


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
