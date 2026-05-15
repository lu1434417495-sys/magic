extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BATTLE_SIM_RUNNER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_runner.gd")
const BATTLE_SIM_SCENARIO_DEF_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_scenario_def.gd")
const BATTLE_SIM_UNIT_SPEC_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_unit_spec.gd")
const BATTLE_SIM_PROFILE_DEF_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_profile_def.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_ready_queue_does_not_consume_timeline_ticks()
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
				_assert_true(first_trace.has("decision_target_snapshots"), "AI turn trace 应包含决策目标执行前快照。")
				_assert_true(first_trace.has("execution_result"), "AI turn trace 应包含命令执行结果摘要。")
				var execution_result: Dictionary = first_trace.get("execution_result", {}) if first_trace.get("execution_result", {}) is Dictionary else {}
				_assert_true(execution_result.has("unit_results"), "AI turn trace 执行结果应包含单位前后资源变化。")
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


func _test_ready_queue_does_not_consume_timeline_ticks() -> void:
	var runner = BATTLE_SIM_RUNNER_SCRIPT.new()
	var report: Dictionary = runner.run_scenario(_build_ready_queue_scenario(), [_build_baseline_profile()])
	var profile_entries: Array = report.get("profile_entries", [])
	_assert_true(profile_entries.size() == 1, "ready queue regression 应产出单 profile entry。")
	if profile_entries.is_empty():
		return
	var runs: Array = (profile_entries[0] as Dictionary).get("runs", [])
	_assert_true(runs.size() == 1, "ready queue regression 应只跑 1 个 seed。")
	if runs.is_empty():
		return
	var ai_turn_traces: Array = (runs[0] as Dictionary).get("ai_turn_traces", [])
	var first_ready_turns: Array[int] = []
	for trace_entry in ai_turn_traces:
		if trace_entry is not Dictionary:
			continue
		var trace: Dictionary = trace_entry
		var unit_id := String(trace.get("unit_id", ""))
		if unit_id == "aa_hostile_one" or unit_id == "ab_hostile_two":
			first_ready_turns.append(int(trace.get("turn_started_tu", -1)))
			if first_ready_turns.size() >= 2:
				break
	_assert_true(first_ready_turns.size() == 2, "ready queue regression 应记录到两个同批 ready AI 回合。")
	if first_ready_turns.size() < 2:
		return
	_assert_true(
		first_ready_turns[0] == 5 and first_ready_turns[1] == 5,
		"同一批 ready 单位应在同一 TU 被依次激活，不应被模拟循环额外推进到 %s。" % str(first_ready_turns)
	)


func _build_ready_queue_scenario():
	var scenario = BATTLE_SIM_SCENARIO_DEF_SCRIPT.new()
	scenario.scenario_id = &"simulation_ready_queue_regression"
	scenario.display_name = "Simulation Ready Queue Regression"
	scenario.map_size = Vector2i(5, 3)
	scenario.timeline_ticks_per_step = 1
	scenario.tu_per_tick = 5
	scenario.max_iterations = 4
	scenario.manual_policy = &"wait"
	scenario.trace_enabled = true
	scenario.seeds = PackedInt32Array([707])
	scenario.ally_units = [
		_build_ready_queue_unit(&"zz_player_dummy", "玩家木桩", &"player", &"manual", Vector2i(4, 1)),
	]
	scenario.enemy_units = [
		_build_ready_queue_unit(&"aa_hostile_one", "敌方一号", &"hostile", &"ai", Vector2i(0, 1)),
		_build_ready_queue_unit(&"ab_hostile_two", "敌方二号", &"hostile", &"ai", Vector2i(1, 1)),
	]
	return scenario


func _build_ready_queue_unit(
	unit_id: StringName,
	display_name: String,
	faction_id: StringName,
	control_mode: StringName,
	coord: Vector2i
):
	var unit_spec = BATTLE_SIM_UNIT_SPEC_SCRIPT.new()
	unit_spec.unit_id = unit_id
	unit_spec.display_name = display_name
	unit_spec.faction_id = faction_id
	unit_spec.control_mode = control_mode
	unit_spec.coord = coord
	unit_spec.action_threshold = 5
	unit_spec.current_hp = 30
	unit_spec.current_ap = 1
	unit_spec.base_attributes = _build_base_attributes(10, 10, 10, 10, 10, 10)
	unit_spec.attribute_overrides = {
		"hp_max": 30,
		"action_points": 1,
		"armor_ac_bonus": 4,
	}
	return unit_spec


func _build_scenario():
	var scenario = BATTLE_SIM_SCENARIO_DEF_SCRIPT.new()
	scenario.scenario_id = &"simulation_regression_archer"
	scenario.display_name = "Simulation Regression Archer"
	scenario.map_size = Vector2i(7, 5)
	scenario.timeline_ticks_per_step = 1
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
	unit_spec.base_attributes = _build_base_attributes(10, 10, 12, 10, 10, 10)
	unit_spec.attribute_overrides = {
		"hp_max": 80,
		"action_points": 2,
		"armor_ac_bonus": 8,
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
	unit_spec.base_attributes = _build_base_attributes(10, 12, 12, 14, 10, 10)
	unit_spec.attribute_overrides = {
		"hp_max": 60,
		"mp_max": 20,
		"stamina_max": 20,
		"action_points": 2,
		"attack_bonus": 6,
		"armor_ac_bonus": 5,
	}
	return unit_spec


func _build_base_attributes(
	strength: int,
	agility: int,
	constitution: int,
	perception: int,
	intelligence: int,
	willpower: int
) -> Dictionary:
	return {
		"strength": strength,
		"agility": agility,
		"constitution": constitution,
		"perception": perception,
		"intelligence": intelligence,
		"willpower": willpower,
	}


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
		_test.fail(message)
