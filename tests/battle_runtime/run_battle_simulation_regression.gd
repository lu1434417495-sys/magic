extends SceneTree

const BATTLE_SIM_RUNNER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_runner.gd")
const BATTLE_SIM_SCENARIO_DEF_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_scenario_def.gd")
const BATTLE_SIM_UNIT_SPEC_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_unit_spec.gd")
const BATTLE_SIM_PROFILE_DEF_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_profile_def.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var runner = BATTLE_SIM_RUNNER_SCRIPT.new()
	var report: Dictionary = runner.run_scenario(_build_scenario(), [_build_baseline_profile(), _build_suppressive_fire_blocked_profile()])
	_assert_true((report.get("profile_entries", []) as Array).size() == 2, "simulation report 应包含 baseline 与 patch 两组 profile 结果。")
	_assert_true((report.get("comparisons", []) as Array).size() == 1, "两组 profile 应产出 1 组 comparison。")
	_assert_true(String(report.get("output_files", {}).get("report_json", "")) != "", "simulation runner 应写出主 report json。")
	_assert_true(String(report.get("output_files", {}).get("turn_trace_jsonl", "")) != "", "simulation runner 应写出 AI trace jsonl。")
	if (report.get("profile_entries", []) as Array).size() >= 2:
		var baseline_entry: Dictionary = (report.get("profile_entries", []) as Array)[0]
		var patched_entry: Dictionary = (report.get("profile_entries", []) as Array)[1]
		var baseline_runs: Array = baseline_entry.get("runs", [])
		var patched_runs: Array = patched_entry.get("runs", [])
		_assert_true(baseline_runs.size() == 2 and patched_runs.size() == 2, "每个 profile 应按 scenario seeds 跑满 2 场战斗。")
		if not baseline_runs.is_empty():
			var first_run: Dictionary = baseline_runs[0]
			_assert_true((first_run.get("ai_turn_traces", []) as Array).size() > 0, "simulation run 应收集到至少 1 条 AI turn trace。")
			if (first_run.get("ai_turn_traces", []) as Array).size() > 0:
				var first_trace: Dictionary = (first_run.get("ai_turn_traces", []) as Array)[0]
				_assert_true(first_trace.has("action_traces"), "AI turn trace 应包含候选动作 trace 列表。")
				_assert_true(first_trace.has("score_input"), "AI turn trace 应包含最终选择的评分摘要。")
		var baseline_summary: Dictionary = baseline_entry.get("summary", {})
		var patched_summary: Dictionary = patched_entry.get("summary", {})
		var baseline_skills: Dictionary = baseline_summary.get("skill_usage_totals", {})
		var patched_skills: Dictionary = patched_summary.get("skill_usage_totals", {})
		_assert_true(
			int(baseline_skills.get("archer_suppressive_fire", 0)) > 0,
			"baseline profile 面对成线目标时应允许 AI 使用 archer_suppressive_fire。"
		)
		_assert_true(
			int(patched_skills.get("archer_suppressive_fire", 0)) == 0,
			"高 stamina patch 后，AI 不应再使用 archer_suppressive_fire。"
		)
		_assert_true(
			int(patched_skills.get("archer_pinning_shot", 0)) > 0,
			"压制射击被资源阻断后，AI 应回落到 archer_pinning_shot。"
		)
	if _failures.is_empty():
		print("Battle simulation regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle simulation regression: FAIL (%d)" % _failures.size())
	quit(1)


func _build_scenario():
	var scenario = BATTLE_SIM_SCENARIO_DEF_SCRIPT.new()
	scenario.scenario_id = &"simulation_regression_archer"
	scenario.display_name = "Simulation Regression Archer"
	scenario.map_size = Vector2i(7, 5)
	scenario.tick_interval_seconds = 1.0
	scenario.tu_per_tick = 5
	scenario.max_iterations = 40
	scenario.manual_policy = &"wait"
	scenario.seeds = PackedInt32Array([101, 102])
	scenario.ally_units = [
		_build_manual_unit(&"player_a", "玩家A", Vector2i(4, 2)),
		_build_manual_unit(&"player_b", "玩家B", Vector2i(5, 2)),
	]
	scenario.enemy_units = [
		_build_enemy_archer(&"mist_harrier_sim", "雾沼猎压者", Vector2i(1, 2)),
	]
	return scenario


func _build_manual_unit(unit_id: StringName, display_name: String, coord: Vector2i):
	var unit_spec = BATTLE_SIM_UNIT_SPEC_SCRIPT.new()
	unit_spec.unit_id = unit_id
	unit_spec.display_name = display_name
	unit_spec.faction_id = &"player"
	unit_spec.control_mode = &"manual"
	unit_spec.coord = coord
	unit_spec.current_hp = 80
	unit_spec.current_ap = 2
	unit_spec.attribute_overrides = {
		"hp_max": 80,
		"action_points": 2,
		"armor_class": 16,
		"armor_ac_bonus": 0,
	}
	return unit_spec


func _build_enemy_archer(unit_id: StringName, display_name: String, coord: Vector2i):
	var unit_spec = BATTLE_SIM_UNIT_SPEC_SCRIPT.new()
	unit_spec.unit_id = unit_id
	unit_spec.display_name = display_name
	unit_spec.faction_id = &"hostile"
	unit_spec.control_mode = &"ai"
	unit_spec.ai_brain_id = &"ranged_suppressor"
	unit_spec.ai_state_id = &"pressure"
	unit_spec.coord = coord
	unit_spec.current_hp = 60
	unit_spec.current_mp = 20
	unit_spec.current_stamina = 20
	unit_spec.current_ap = 2
	unit_spec.skill_ids = [&"archer_suppressive_fire", &"archer_pinning_shot"]
	unit_spec.skill_level_map = {
		"archer_suppressive_fire": 1,
		"archer_pinning_shot": 1,
	}
	unit_spec.attribute_overrides = {
		"hp_max": 60,
		"mp_max": 20,
		"stamina_max": 20,
		"action_points": 2,
		"attack_bonus": 6,
		"armor_class": 14,
		"armor_ac_bonus": 0,
	}
	return unit_spec


func _build_baseline_profile():
	var profile = BATTLE_SIM_PROFILE_DEF_SCRIPT.new()
	profile.profile_id = &"baseline"
	profile.display_name = "Baseline"
	return profile


func _build_suppressive_fire_blocked_profile():
	var profile = BATTLE_SIM_PROFILE_DEF_SCRIPT.new()
	profile.profile_id = &"pinning_only"
	profile.display_name = "Pinning Only"
	profile.override_patches = [{
		"target_type": "skill",
		"target_id": "archer_suppressive_fire",
		"path": "combat_profile.stamina_cost",
		"value": 999,
	}]
	return profile


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)
