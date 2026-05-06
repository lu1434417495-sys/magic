class_name BattleSimRunner
extends RefCounted

const BATTLE_SIM_CONTENT_PROVIDER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_content_provider.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/world/encounter_anchor_data.gd")
const BATTLE_SIM_PROFILE_DEF_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_profile_def.gd")
const BATTLE_SIM_OVERRIDE_APPLIER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_override_applier.gd")
const BATTLE_SIM_REPORT_BUILDER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_report_builder.gd")
const BATTLE_SIM_TERRAIN_GENERATOR_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_terrain_generator.gd")
const BATTLE_SIM_EXECUTION_LOOP_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_execution_loop.gd")
const BATTLE_SIM_TRACE_SUMMARY_BUILDER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_trace_summary_builder.gd")
const BattleSimProfileDef = preload("res://scripts/systems/battle/sim/battle_sim_profile_def.gd")

const REPORT_DIRECTORY := "user://simulation_reports"
const MAX_IDLE_LOOPS := 25
const PROGRESS_ITERATION_INTERVAL := 100

var _override_applier = BATTLE_SIM_OVERRIDE_APPLIER_SCRIPT.new()
var _report_builder = BATTLE_SIM_REPORT_BUILDER_SCRIPT.new()
var _content_provider = BATTLE_SIM_CONTENT_PROVIDER_SCRIPT.new()
var _terrain_generator = BATTLE_SIM_TERRAIN_GENERATOR_SCRIPT.new()
var _execution_loop = BATTLE_SIM_EXECUTION_LOOP_SCRIPT.new()
var _trace_summary_builder = BATTLE_SIM_TRACE_SUMMARY_BUILDER_SCRIPT.new()
var progress_logging_enabled := false
var progress_log_path := ""
var _progress_log_file: FileAccess = null


func setup(content_provider: Object = null, terrain_generator: Object = null) -> void:
	_content_provider = content_provider if content_provider != null else BATTLE_SIM_CONTENT_PROVIDER_SCRIPT.new()
	_terrain_generator = terrain_generator if terrain_generator != null else BATTLE_SIM_TERRAIN_GENERATOR_SCRIPT.new()


func set_progress_logging_enabled(enabled: bool) -> void:
	progress_logging_enabled = enabled


func set_progress_log_path(path: String) -> void:
	progress_log_path = path


func run_scenario(scenario_def, profile_defs: Array = []) -> Dictionary:
	var resolved_profiles: Array = _resolve_profiles(profile_defs)
	var resolved_seeds: Array[int] = scenario_def.resolve_seeds()
	var report := {
		"scenario": scenario_def.to_dict(),
		"generated_at_unix": int(Time.get_unix_time_from_system()),
		"profile_entries": [],
		"comparisons": [],
		"output_files": {},
	}
	if progress_logging_enabled:
		_open_progress_log()
		_log_progress("[BattleSim] progress_log=%s" % ProjectSettings.globalize_path(progress_log_path))
		_log_progress("[BattleSim] start scenario=%s profiles=%d seeds=%d max_iterations=%d" % [
			String(scenario_def.scenario_id),
			resolved_profiles.size(),
			resolved_seeds.size(),
			int(scenario_def.max_iterations),
		])
	for profile_index in range(resolved_profiles.size()):
		var profile = resolved_profiles[profile_index]
		var runs: Array = []
		for seed_index in range(resolved_seeds.size()):
			var seed := int(resolved_seeds[seed_index])
			if progress_logging_enabled:
				_log_progress("[BattleSim] run-start profile=%s profile_index=%d/%d seed=%d seed_index=%d/%d" % [
					String(profile.profile_id),
					profile_index + 1,
					resolved_profiles.size(),
					seed,
					seed_index + 1,
					resolved_seeds.size(),
				])
			var run_result := _run_single_simulation(scenario_def, profile, seed)
			runs.append(run_result)
			if progress_logging_enabled:
				_log_progress("[BattleSim] run-done profile=%s seed=%d ended=%s winner=%s final_tu=%d iterations=%d timeline_steps=%d idle_loops=%d ally_alive=%d enemy_alive=%d" % [
					String(profile.profile_id),
					seed,
					str(bool(run_result.get("battle_ended", false))),
					String(run_result.get("winner_faction_id", "")),
					int(run_result.get("final_tu", 0)),
					int(run_result.get("iterations", 0)),
					int(run_result.get("timeline_steps", 0)),
					int(run_result.get("idle_loops", 0)),
					int(run_result.get("ally_alive", 0)),
					int(run_result.get("enemy_alive", 0)),
				])
		report["profile_entries"].append({
			"profile": profile.to_dict(),
			"runs": runs,
			"summary": _report_builder.build_profile_summary(profile, runs),
		})
	report["comparisons"] = _report_builder.build_profile_comparisons(report.get("profile_entries", []))
	report["output_files"] = _write_report_files(scenario_def, report)
	if progress_logging_enabled:
		_log_progress("[BattleSim] report-written report_json=%s traces_jsonl=%s" % [
			String(report.get("output_files", {}).get("report_json", "")),
			String(report.get("output_files", {}).get("turn_trace_jsonl", "")),
		])
		_close_progress_log()
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
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	var skill_defs: Dictionary = _get_content_dictionary(&"get_skill_defs")
	var enemy_ai_brains: Dictionary = _get_content_dictionary(&"get_enemy_ai_brains")
	var overrides := _override_applier.apply_profile(
		skill_defs,
		enemy_ai_brains,
		profile
	)
	var use_formal_terrain := bool(scenario_def.use_formal_terrain_generation) if scenario_def != null else false
	runtime.setup(
		null,
		overrides.get("skill_defs", {}),
		_get_content_dictionary(&"get_enemy_templates"),
		overrides.get("enemy_ai_brains", {}),
		null,
		null,
		{},
		null if use_formal_terrain else _terrain_generator
	)
	runtime.set_ai_trace_enabled(bool(scenario_def.trace_enabled))
	runtime.set_ai_score_profile(overrides.get("ai_score_profile", null))
	var encounter_anchor = _build_encounter_anchor(scenario_def)
	var state = runtime.start_battle(encounter_anchor, seed, scenario_def.build_start_context())
	var loop_result: Dictionary = _execution_loop.run(runtime, state, scenario_def, {
		"max_idle_loops": MAX_IDLE_LOOPS,
		"progress_iteration_interval": PROGRESS_ITERATION_INTERVAL if progress_logging_enabled else 0,
		"progress_callback": Callable(self, "_handle_run_progress"),
		"progress_context": {
			"profile_id": String(profile.profile_id),
			"seed": seed,
		},
	})
	var iterations := int(loop_result.get("iterations", 0))
	var idle_loops := int(loop_result.get("idle_loops", 0))
	var timeline_steps := int(loop_result.get("timeline_steps", 0))
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
		"timeline_steps": timeline_steps,
		"ally_alive": _count_living_units(state, state.ally_unit_ids if state != null else []),
		"enemy_alive": _count_living_units(state, state.enemy_unit_ids if state != null else []),
		"metrics": runtime.get_battle_metrics().duplicate(true),
		"ai_turn_traces": runtime.get_ai_turn_traces().duplicate(true),
		"final_units": _build_final_unit_snapshots(state),
	}
	runtime.dispose()
	return run_result


func _handle_run_progress(progress_data: Dictionary) -> void:
	if not progress_logging_enabled:
		return
	var state = progress_data.get("state", null)
	if state == null:
		return
	var context: Dictionary = progress_data.get("context", {}) if progress_data.get("context", {}) is Dictionary else {}
	_log_progress("[BattleSim] progress profile=%s seed=%d iteration=%d timeline_steps=%d phase=%s active_unit=%s tu=%d idle_loops=%d %s last_log=\"%s\"" % [
		String(context.get("profile_id", "")),
		int(context.get("seed", 0)),
		int(progress_data.get("iterations", 0)),
		int(progress_data.get("timeline_steps", 0)),
		String(state.phase),
		String(state.active_unit_id),
		int(state.timeline.current_tu) if state.timeline != null else 0,
		int(progress_data.get("idle_loops", 0)),
		_build_active_unit_progress_summary(state),
		_get_last_log_line(state),
	])


func _get_content_dictionary(method_name: StringName) -> Dictionary:
	if _content_provider == null or not _content_provider.has_method(method_name):
		return {}
	var value = _content_provider.call(method_name)
	return value if value is Dictionary else {}


func _build_encounter_anchor(scenario_def):
	var encounter_anchor = ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = scenario_def.scenario_id if scenario_def.scenario_id != &"" else &"battle_sim"
	encounter_anchor.display_name = scenario_def.display_name if not scenario_def.display_name.is_empty() else String(scenario_def.scenario_id)
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.world_coord = Vector2i.ZERO
	encounter_anchor.region_tag = &"simulation"
	return encounter_anchor


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
	var trace_summary_path := "%s/%s_%d_trace_summary.json" % [report_dir, scenario_key, timestamp]
	var output_files := {
		"report_json": report_path,
		"turn_trace_jsonl": trace_path,
	}
	var has_traces := _trace_summary_builder.has_traces(report)
	if has_traces:
		output_files["trace_summary_json"] = trace_summary_path
	report["output_files"] = output_files
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
	if has_traces:
		var summary_file = FileAccess.open(trace_summary_path, FileAccess.WRITE)
		if summary_file != null:
			var trace_summary := _trace_summary_builder.build(report, report_path)
			summary_file.store_string(JSON.stringify(_normalize_variant(trace_summary), "\t"))
			summary_file.close()
	return output_files


func _open_progress_log() -> void:
	if progress_log_path.is_empty():
		return
	var base_dir := progress_log_path.get_base_dir()
	if not base_dir.is_empty():
		DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(base_dir))
	_progress_log_file = FileAccess.open(progress_log_path, FileAccess.WRITE)


func _close_progress_log() -> void:
	if _progress_log_file != null:
		_progress_log_file.close()
		_progress_log_file = null


func _log_progress(message: String) -> void:
	print(message)
	if _progress_log_file != null:
		_progress_log_file.store_line(message)
		_progress_log_file.flush()


func _build_active_unit_progress_summary(state) -> String:
	if state == null or state.active_unit_id == &"":
		return ""
	var active_unit = state.units.get(state.active_unit_id)
	if active_unit == null:
		return ""
	return "coord=(%d,%d) hp=%d ap=%d stamina=%d move=%d last_action=%s decisions=%d" % [
		active_unit.coord.x,
		active_unit.coord.y,
		int(active_unit.current_hp),
		int(active_unit.current_ap),
		int(active_unit.current_stamina),
		int(active_unit.current_move_points),
		String(active_unit.ai_blackboard.get("last_action_id", "")),
		int(active_unit.ai_blackboard.get("turn_decision_count", 0)),
	]


func _get_last_log_line(state) -> String:
	if state == null or state.log_entries.is_empty():
		return ""
	return String(state.log_entries[-1]).replace("\n", " ")


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
