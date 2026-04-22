class_name BattleSimRunner
extends RefCounted

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle_runtime_module.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle_command.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")
const BATTLE_SIM_PROFILE_DEF_SCRIPT = preload("res://scripts/systems/battle_sim_profile_def.gd")
const BATTLE_SIM_OVERRIDE_APPLIER_SCRIPT = preload("res://scripts/systems/battle_sim_override_applier.gd")
const BATTLE_SIM_REPORT_BUILDER_SCRIPT = preload("res://scripts/systems/battle_sim_report_builder.gd")
const BattleCommand = preload("res://scripts/systems/battle_command.gd")
const BattleSimProfileDef = preload("res://scripts/systems/battle_sim_profile_def.gd")

const REPORT_DIRECTORY := "user://simulation_reports"
const MAX_IDLE_LOOPS := 25

var _override_applier = BATTLE_SIM_OVERRIDE_APPLIER_SCRIPT.new()
var _report_builder = BATTLE_SIM_REPORT_BUILDER_SCRIPT.new()


func run_scenario(scenario_def, profile_defs: Array = []) -> Dictionary:
	var resolved_profiles := _resolve_profiles(profile_defs)
	var report := {
		"scenario": scenario_def.to_dict(),
		"generated_at_unix": int(Time.get_unix_time_from_system()),
		"profile_entries": [],
		"comparisons": [],
		"output_files": {},
	}
	for profile in resolved_profiles:
		var runs: Array = []
		for seed in scenario_def.resolve_seeds():
			runs.append(_run_single_simulation(scenario_def, profile, seed))
		report["profile_entries"].append({
			"profile": profile.to_dict(),
			"runs": runs,
			"summary": _report_builder.build_profile_summary(profile, runs),
		})
	report["comparisons"] = _report_builder.build_profile_comparisons(report.get("profile_entries", []))
	report["output_files"] = _write_report_files(scenario_def, report)
	return report


func _resolve_profiles(profile_defs: Array) -> Array:
	var resolved: Array = []
	for profile_def in profile_defs:
		var profile = profile_def as BattleSimProfileDef
		if profile != null:
			resolved.append(profile)
	if resolved.is_empty():
		var baseline = BATTLE_SIM_PROFILE_DEF_SCRIPT.new()
		baseline.profile_id = &"baseline"
		baseline.display_name = "Baseline"
		resolved.append(baseline)
	return resolved


func _run_single_simulation(scenario_def, profile, seed: int) -> Dictionary:
	var game_session = GAME_SESSION_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	var overrides := _override_applier.apply_profile(
		game_session.get_skill_defs(),
		game_session.get_enemy_ai_brains(),
		profile
	)
	runtime.setup(
		null,
		overrides.get("skill_defs", {}),
		game_session.get_enemy_templates(),
		overrides.get("enemy_ai_brains", {}),
		null
	)
	runtime.set_ai_trace_enabled(true)
	runtime.set_ai_score_profile(overrides.get("ai_score_profile", null))
	var encounter_anchor = _build_encounter_anchor(scenario_def)
	var state = runtime.start_battle(encounter_anchor, seed, scenario_def.build_start_context())
	var iterations := 0
	var idle_loops := 0
	while state != null and state.phase != &"battle_ended" and iterations < int(scenario_def.max_iterations):
		iterations += 1
		var previous_signature := _build_progress_signature(state)
		if state.phase == &"unit_acting":
			var active_unit = state.units.get(state.active_unit_id)
			if active_unit != null and active_unit.is_alive and active_unit.control_mode == &"manual":
				_issue_manual_policy(runtime, scenario_def.manual_policy, active_unit.unit_id)
			else:
				runtime.advance(0.0)
		else:
			runtime.advance(float(scenario_def.tick_interval_seconds))
		var next_signature := _build_progress_signature(state)
		if previous_signature == next_signature:
			idle_loops += 1
			if idle_loops >= MAX_IDLE_LOOPS:
				break
		else:
			idle_loops = 0
	var run_result := {
		"scenario_id": String(scenario_def.scenario_id),
		"profile_id": String(profile.profile_id),
		"seed": seed,
		"battle_id": String(state.battle_id) if state != null else "",
		"battle_ended": state != null and state.phase == &"battle_ended",
		"winner_faction_id": String(state.winner_faction_id) if state != null else "",
		"final_tu": int(state.timeline.current_tu) if state != null and state.timeline != null else 0,
		"iterations": iterations,
		"idle_loops": idle_loops,
		"ally_alive": _count_living_units(state, state.ally_unit_ids if state != null else []),
		"enemy_alive": _count_living_units(state, state.enemy_unit_ids if state != null else []),
		"metrics": runtime.get_battle_metrics().duplicate(true),
		"ai_turn_traces": runtime.get_ai_turn_traces().duplicate(true),
		"final_units": _build_final_unit_snapshots(state),
	}
	runtime.dispose()
	game_session.free()
	return run_result


func _build_encounter_anchor(scenario_def):
	var encounter_anchor = ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = scenario_def.scenario_id if scenario_def.scenario_id != &"" else &"battle_sim"
	encounter_anchor.display_name = scenario_def.display_name if not scenario_def.display_name.is_empty() else String(scenario_def.scenario_id)
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.world_coord = Vector2i.ZERO
	encounter_anchor.region_tag = &"simulation"
	return encounter_anchor


func _issue_manual_policy(runtime, manual_policy: StringName, unit_id: StringName) -> void:
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.unit_id = unit_id
	command.command_type = BattleCommand.TYPE_WAIT
	match manual_policy:
		&"wait":
			runtime.issue_command(command)
		_:
			runtime.issue_command(command)


func _build_progress_signature(state) -> String:
	if state == null:
		return ""
	return "%s|%s|%s|%d|%d" % [
		String(state.phase),
		String(state.active_unit_id),
		String(state.winner_faction_id),
		int(state.timeline.current_tu) if state.timeline != null else 0,
		state.log_entries.size(),
	]


func _count_living_units(state, unit_ids: Array) -> int:
	if state == null:
		return 0
	var count := 0
	for unit_id in unit_ids:
		var unit_state = state.units.get(unit_id)
		if unit_state != null and unit_state.is_alive:
			count += 1
	return count


func _build_final_unit_snapshots(state) -> Array:
	var snapshots: Array = []
	if state == null:
		return snapshots
	for unit_id_str in ProgressionDataUtils.sorted_string_keys(state.units):
		var unit_state = state.units.get(StringName(unit_id_str))
		if unit_state == null:
			continue
		snapshots.append(unit_state.to_dict())
	return snapshots


func _write_report_files(scenario_def, report: Dictionary) -> Dictionary:
	var scenario_key := String(scenario_def.scenario_id) if scenario_def != null and scenario_def.scenario_id != &"" else "battle_sim"
	var timestamp := int(Time.get_unix_time_from_system())
	var report_dir := "%s/%s" % [REPORT_DIRECTORY, scenario_key]
	var ensure_dir_error := DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(report_dir))
	if ensure_dir_error != OK:
		return {}
	var report_path := "%s/%s_%d_report.json" % [report_dir, scenario_key, timestamp]
	var trace_path := "%s/%s_%d_turn_traces.jsonl" % [report_dir, scenario_key, timestamp]
	var report_file = FileAccess.open(report_path, FileAccess.WRITE)
	if report_file != null:
		report_file.store_string(JSON.stringify(_normalize_variant(report), "\t"))
		report_file.close()
	var trace_file = FileAccess.open(trace_path, FileAccess.WRITE)
	if trace_file != null:
		for profile_entry in report.get("profile_entries", []):
			if profile_entry is not Dictionary:
				continue
			var profile_id := String((profile_entry as Dictionary).get("profile", {}).get("profile_id", ""))
			for run_entry in (profile_entry as Dictionary).get("runs", []):
				if run_entry is not Dictionary:
					continue
				for trace_entry in (run_entry as Dictionary).get("ai_turn_traces", []):
					if trace_entry is not Dictionary:
						continue
					var flattened_trace := (trace_entry as Dictionary).duplicate(true)
					flattened_trace["scenario_id"] = scenario_key
					flattened_trace["profile_id"] = profile_id
					flattened_trace["seed"] = int((run_entry as Dictionary).get("seed", 0))
					trace_file.store_line(JSON.stringify(_normalize_variant(flattened_trace)))
		trace_file.close()
	return {
		"report_json": report_path,
		"turn_trace_jsonl": trace_path,
	}


func _normalize_variant(value):
	if value is StringName:
		return String(value)
	if value is Vector2i:
		return {"x": value.x, "y": value.y}
	if value is Array:
		var normalized_array: Array = []
		for entry in value:
			normalized_array.append(_normalize_variant(entry))
		return normalized_array
	if value is Dictionary:
		var normalized_dict: Dictionary = {}
		for key in value.keys():
			normalized_dict[String(key)] = _normalize_variant(value.get(key))
		return normalized_dict
	if value is Object and value.has_method("to_dict"):
		return _normalize_variant(value.to_dict())
	return value
