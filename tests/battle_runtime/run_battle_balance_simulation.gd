extends SceneTree

const BATTLE_SIM_RUNNER_SCRIPT = preload("res://scripts/systems/battle_sim_runner.gd")
const BATTLE_SIM_SCENARIO_DEF_SCRIPT = preload("res://scripts/systems/battle_sim_scenario_def.gd")
const BATTLE_SIM_PROFILE_DEF_SCRIPT = preload("res://scripts/systems/battle_sim_profile_def.gd")


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var args := OS.get_cmdline_user_args()
	if args.is_empty():
		push_error("Usage: godot --headless --script tests/battle_runtime/run_battle_balance_simulation.gd -- <scenario.tres> [profile.tres ...]")
		quit(1)
		return
	var scenario = load(args[0])
	if scenario == null or not scenario.has_method("build_start_context"):
		push_error("Failed to load BattleSimScenarioDef from %s." % args[0])
		quit(1)
		return
	var profiles: Array = []
	for index in range(1, args.size()):
		var profile = load(args[index])
		if profile == null:
			push_error("Failed to load BattleSimProfileDef from %s." % args[index])
			quit(1)
			return
		profiles.append(profile)
	var runner = BATTLE_SIM_RUNNER_SCRIPT.new()
	var report: Dictionary = runner.run_scenario(scenario, profiles)
	print("[BattleSim] scenario=%s profiles=%d comparisons=%d report_json=%s traces_jsonl=%s" % [
		String(report.get("scenario", {}).get("scenario_id", "")),
		(report.get("profile_entries", []) as Array).size(),
		(report.get("comparisons", []) as Array).size(),
		String(report.get("output_files", {}).get("report_json", "")),
		String(report.get("output_files", {}).get("turn_trace_jsonl", "")),
	])
	quit(0)
