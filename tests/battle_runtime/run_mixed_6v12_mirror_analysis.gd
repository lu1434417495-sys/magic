extends SceneTree

const BATTLE_SIM_SCENARIO_DEF_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_scenario_def.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BATTLE_SIM_CONTENT_PROVIDER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_content_provider.gd")
const BATTLE_SIM_OVERRIDE_APPLIER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_override_applier.gd")
const BATTLE_SIM_PROFILE_DEF_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_profile_def.gd")
const BATTLE_SIM_TERRAIN_GENERATOR_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_terrain_generator.gd")
const BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_formal_combat_fixture.gd")
const BATTLE_SIM_EXECUTION_LOOP_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_execution_loop.gd")
const BATTLE_SIM_TRACE_SUMMARY_BUILDER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_trace_summary_builder.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/world/encounter_anchor_data.gd")
const PROGRESSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/progression_content_registry.gd")
const ITEM_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/warehouse/item_content_registry.gd")
const TRUE_RANDOM_SEED_SERVICE_SCRIPT = preload("res://scripts/utils/true_random_seed_service.gd")

const MAX_IDLE_LOOPS := 25
const DEFAULT_SIMULATION_TIMEOUT_SECONDS := 30 * 60
const ENV_SIMULATION_TIMEOUT_SECONDS := "SIM_TIMEOUT_SECONDS"
const DEFAULT_PROGRESS_INTERVAL_SECONDS := 5.0
const ENV_PROGRESS_ENABLED := "PROGRESS"
const ENV_PROGRESS_INTERVAL_SECONDS := "PROGRESS_SECONDS"

var _progress_enabled := true
var _progress_interval_msec := int(DEFAULT_PROGRESS_INTERVAL_SECONDS * 1000.0)
var _last_progress_print_msec := 0


func _initialize() -> void:
	var start_seed_source := "environment" if OS.has_environment("START_SEED") else "true_random"
	var start_seed := int(OS.get_environment("START_SEED")) if OS.has_environment("START_SEED") else TRUE_RANDOM_SEED_SERVICE_SCRIPT.generate_seed()
	var run_count := int(OS.get_environment("COUNT")) if OS.has_environment("COUNT") else 10
	var output_path := OS.get_environment("OUTPUT_FILE") if OS.has_environment("OUTPUT_FILE") else ""
	var trace_ai := _read_bool_environment("TRACE_AI", false)
	var timeout_seconds := _read_int_environment(ENV_SIMULATION_TIMEOUT_SECONDS, DEFAULT_SIMULATION_TIMEOUT_SECONDS)
	_progress_enabled = _read_bool_environment(ENV_PROGRESS_ENABLED, true)
	_progress_interval_msec = maxi(int(_read_float_environment(ENV_PROGRESS_INTERVAL_SECONDS, DEFAULT_PROGRESS_INTERVAL_SECONDS) * 1000.0), 250)
	var roster_options := _build_roster_options_from_environment()

	var scenario_path := "res://data/configs/battle_sim/scenarios/mixed_6v12_mirror_simulation.tres"
	var scenario_def = load(scenario_path)
	if scenario_def == null:
		print("[ERROR] Failed to load scenario")
		quit()
		return

	var content_provider = BATTLE_SIM_CONTENT_PROVIDER_SCRIPT.new()
	var override_applier = BATTLE_SIM_OVERRIDE_APPLIER_SCRIPT.new()
	var terrain_generator = BATTLE_SIM_TERRAIN_GENERATOR_SCRIPT.new()
	var progression_registry = PROGRESSION_CONTENT_REGISTRY_SCRIPT.new()
	var item_registry = ITEM_CONTENT_REGISTRY_SCRIPT.new()

	var skill_defs: Dictionary = _get_content_dictionary(content_provider, &"get_skill_defs")
	var enemy_ai_brains: Dictionary = _get_content_dictionary(content_provider, &"get_enemy_ai_brains")
	var baseline = BATTLE_SIM_PROFILE_DEF_SCRIPT.new()
	baseline.profile_id = &"baseline"
	baseline.display_name = "Baseline"
	var overrides := override_applier.apply_profile(skill_defs, enemy_ai_brains, baseline)

	var rng := RandomNumberGenerator.new()
	rng.seed = start_seed

	# Global totals
	var total_charge_attempts := 0
	var total_charge_successes := 0
	var total_heavy_attempts := 0
	var total_heavy_successes := 0
	var total_aimed_attempts := 0
	var total_aimed_successes := 0
	var total_multishot_attempts := 0
	var total_multishot_successes := 0
	var total_basic_attempts := 0
	var total_basic_successes := 0

	var total_charge_mastery := 0
	var total_heavy_mastery := 0
	var total_aimed_mastery := 0
	var total_multishot_mastery := 0
	var total_basic_mastery := 0

	# Per-faction accumulators
	var player_charge_attempts := 0
	var player_charge_successes := 0
	var player_heavy_attempts := 0
	var player_heavy_successes := 0
	var player_aimed_attempts := 0
	var player_aimed_successes := 0
	var player_multishot_attempts := 0
	var player_multishot_successes := 0
	var player_basic_attempts := 0
	var player_basic_successes := 0
	var player_damage_done := 0
	var player_damage_taken := 0

	var hostile_charge_attempts := 0
	var hostile_charge_successes := 0
	var hostile_heavy_attempts := 0
	var hostile_heavy_successes := 0
	var hostile_aimed_attempts := 0
	var hostile_aimed_successes := 0
	var hostile_multishot_attempts := 0
	var hostile_multishot_successes := 0
	var hostile_basic_attempts := 0
	var hostile_basic_successes := 0
	var hostile_damage_done := 0
	var hostile_damage_taken := 0

	var ended_count := 0
	var total_iterations := 0
	var total_timeline_steps := 0
	var total_wins_player := 0
	var total_wins_hostile := 0
	var total_draws := 0

	var start_time := Time.get_ticks_msec()
	var completed_run_count := 0
	var timed_out := false

	var per_unit_summary: Dictionary = {}
	var run_details: Array = []

	_print_progress("[Progress] start 6v12 runs=%d start_seed=%d source=%s timeout=%ds output=%s" % [
		run_count,
		start_seed,
		start_seed_source,
		timeout_seconds,
		output_path if not output_path.is_empty() else "<stdout>",
	])
	for run_index in range(run_count):
		if _has_reached_timeout(start_time, timeout_seconds):
			timed_out = true
			break
		var seed := rng.randi()
		var run_start_time := Time.get_ticks_msec()
		_last_progress_print_msec = 0
		_print_progress("[Progress] run %d/%d start seed=%d batch_elapsed=%.1fs" % [
			run_index + 1,
			run_count,
			seed,
			(Time.get_ticks_msec() - start_time) / 1000.0,
		])
		var fixture = _build_formal_fixture(scenario_def, overrides, progression_registry, item_registry, roster_options, seed)
		var result := _run_single_simulation(
			scenario_def,
			overrides,
			content_provider,
			terrain_generator,
			fixture,
			seed,
			trace_ai,
			{
				"run_index": run_index,
				"run_count": run_count,
				"run_start_time": run_start_time,
				"batch_start_time": start_time,
				"seed": seed,
				"max_iterations": int(scenario_def.max_iterations) if scenario_def != null else 0,
			}
		)
		var metrics: Dictionary = result.get("metrics", {})
		var factions: Dictionary = metrics.get("factions", {})
		var units: Dictionary = result.get("units", {})

		# Collect per-unit summary
		for unit_id in units.keys():
			var unit_data = units.get(unit_id)
			if unit_data is not Dictionary:
				continue
			if not per_unit_summary.has(unit_id):
				per_unit_summary[unit_id] = {
					"display_name": unit_data.get("display_name", ""),
					"faction_id": unit_data.get("faction_id", ""),
					"runs": 0,
					"turn_count": 0,
					"total_damage_done": 0,
					"total_damage_taken": 0,
					"total_healing_done": 0,
					"total_healing_received": 0,
					"kill_count": 0,
					"death_count": 0,
					"skill_attempts": {},
					"skill_successes": {},
				}
			var summary = per_unit_summary[unit_id]
			summary["runs"] += 1
			summary["turn_count"] += int(unit_data.get("turn_count", 0))
			summary["total_damage_done"] += int(unit_data.get("total_damage_done", 0))
			summary["total_damage_taken"] += int(unit_data.get("total_damage_taken", 0))
			summary["total_healing_done"] += int(unit_data.get("total_healing_done", 0))
			summary["total_healing_received"] += int(unit_data.get("total_healing_received", 0))
			summary["kill_count"] += int(unit_data.get("kill_count", 0))
			summary["death_count"] += int(unit_data.get("death_count", 0))
			for skill_id in unit_data.get("skill_attempt_counts", {}).keys():
				summary["skill_attempts"][skill_id] = int(summary["skill_attempts"].get(skill_id, 0)) + int(unit_data["skill_attempt_counts"][skill_id])
			for skill_id in unit_data.get("skill_success_counts", {}).keys():
				summary["skill_successes"][skill_id] = int(summary["skill_successes"].get(skill_id, 0)) + int(unit_data["skill_success_counts"][skill_id])

		# Collect run details
		var run_factions: Dictionary = {}
		for faction_key in factions.keys():
			var faction_data = factions.get(faction_key)
			if faction_data is not Dictionary:
				continue
			run_factions[faction_key] = {
				"total_damage_done": int(faction_data.get("total_damage_done", 0)),
				"total_damage_taken": int(faction_data.get("total_damage_taken", 0)),
				"kill_count": int(faction_data.get("kill_count", 0)),
				"death_count": int(faction_data.get("death_count", 0)),
				"turn_count": int(faction_data.get("turn_count", 0)),
			}
		var run_units: Dictionary = {}
		for unit_id in units.keys():
			var unit_data = units.get(unit_id)
			if unit_data is not Dictionary:
				continue
			run_units[unit_id] = {
				"display_name": unit_data.get("display_name", ""),
				"faction_id": unit_data.get("faction_id", ""),
				"turn_count": int(unit_data.get("turn_count", 0)),
				"total_damage_done": int(unit_data.get("total_damage_done", 0)),
				"total_damage_taken": int(unit_data.get("total_damage_taken", 0)),
				"kill_count": int(unit_data.get("kill_count", 0)),
				"death_count": int(unit_data.get("death_count", 0)),
				"skill_attempts": unit_data.get("skill_attempt_counts", {}),
				"skill_successes": unit_data.get("skill_success_counts", {}),
			}
		run_details.append({
			"run_index": run_index,
			"seed": seed,
			"winner_faction_id": result.get("winner_faction_id", ""),
			"iterations": result.get("iterations", 0),
			"timeline_steps": result.get("timeline_steps", 0),
			"factions": run_factions,
			"units": run_units,
		})
		if trace_ai:
			run_details[run_details.size() - 1]["ai_turn_traces"] = result.get("ai_turn_traces", [])

		var charge_attempts := 0
		var charge_successes := 0
		var heavy_attempts := 0
		var heavy_successes := 0
		var aimed_attempts := 0
		var aimed_successes := 0
		var multishot_attempts := 0
		var multishot_successes := 0
		var basic_attempts := 0
		var basic_successes := 0

		for faction_key in factions.keys():
			var faction_data = factions.get(faction_key)
			if faction_data is not Dictionary:
				continue
			var skill_attempts = faction_data.get("skill_attempt_counts", {})
			var skill_successes = faction_data.get("skill_success_counts", {})
			var fac_charge_a = int(skill_attempts.get("charge", 0))
			var fac_charge_s = int(skill_successes.get("charge", 0))
			var fac_heavy_a = int(skill_attempts.get("warrior_heavy_strike", 0))
			var fac_heavy_s = int(skill_successes.get("warrior_heavy_strike", 0))
			var fac_aimed_a = int(skill_attempts.get("archer_aimed_shot", 0))
			var fac_aimed_s = int(skill_successes.get("archer_aimed_shot", 0))
			var fac_multi_a = int(skill_attempts.get("archer_multishot", 0))
			var fac_multi_s = int(skill_successes.get("archer_multishot", 0))
			var fac_basic_a = int(skill_attempts.get("basic_attack", 0))
			var fac_basic_s = int(skill_successes.get("basic_attack", 0))
			var fac_dmg_done = int(faction_data.get("total_damage_done", 0))
			var fac_dmg_taken = int(faction_data.get("total_damage_taken", 0))

			charge_attempts += fac_charge_a
			charge_successes += fac_charge_s
			heavy_attempts += fac_heavy_a
			heavy_successes += fac_heavy_s
			aimed_attempts += fac_aimed_a
			aimed_successes += fac_aimed_s
			multishot_attempts += fac_multi_a
			multishot_successes += fac_multi_s
			basic_attempts += fac_basic_a
			basic_successes += fac_basic_s

			if faction_key == "player":
				player_charge_attempts += fac_charge_a
				player_charge_successes += fac_charge_s
				player_heavy_attempts += fac_heavy_a
				player_heavy_successes += fac_heavy_s
				player_aimed_attempts += fac_aimed_a
				player_aimed_successes += fac_aimed_s
				player_multishot_attempts += fac_multi_a
				player_multishot_successes += fac_multi_s
				player_basic_attempts += fac_basic_a
				player_basic_successes += fac_basic_s
				player_damage_done += fac_dmg_done
				player_damage_taken += fac_dmg_taken
			else:
				hostile_charge_attempts += fac_charge_a
				hostile_charge_successes += fac_charge_s
				hostile_heavy_attempts += fac_heavy_a
				hostile_heavy_successes += fac_heavy_s
				hostile_aimed_attempts += fac_aimed_a
				hostile_aimed_successes += fac_aimed_s
				hostile_multishot_attempts += fac_multi_a
				hostile_multishot_successes += fac_multi_s
				hostile_basic_attempts += fac_basic_a
				hostile_basic_successes += fac_basic_s
				hostile_damage_done += fac_dmg_done
				hostile_damage_taken += fac_dmg_taken

		total_charge_attempts += charge_attempts
		total_charge_successes += charge_successes
		total_heavy_attempts += heavy_attempts
		total_heavy_successes += heavy_successes
		total_aimed_attempts += aimed_attempts
		total_aimed_successes += aimed_successes
		total_multishot_attempts += multishot_attempts
		total_multishot_successes += multishot_successes
		total_basic_attempts += basic_attempts
		total_basic_successes += basic_successes

		total_charge_mastery += fixture.charge_mastery
		total_heavy_mastery += fixture.heavy_mastery
		total_aimed_mastery += fixture.aimed_mastery
		total_multishot_mastery += fixture.multishot_mastery
		total_basic_mastery += fixture.basic_mastery

		if result.get("battle_ended", false):
			ended_count += 1
			var winner = String(result.get("winner_faction_id", ""))
			match winner:
				"player": total_wins_player += 1
				"hostile": total_wins_hostile += 1
				_: total_draws += 1
		total_iterations += result.get("iterations", 0)
		total_timeline_steps += result.get("timeline_steps", 0)
		completed_run_count += 1

		var elapsed := (Time.get_ticks_msec() - start_time) / 1000.0
		var run_elapsed := (Time.get_ticks_msec() - run_start_time) / 1000.0
		_print_progress("[Progress] run %d/%d done winner=%s ended=%s iterations=%d timeline_steps=%d run_elapsed=%.1fs batch_elapsed=%.1fs rate=%.2f runs/s" % [
			run_index + 1,
			run_count,
			String(result.get("winner_faction_id", "")),
			str(result.get("battle_ended", false)),
			int(result.get("iterations", 0)),
			int(result.get("timeline_steps", 0)),
			run_elapsed,
			elapsed,
			float(run_index + 1) / maxf(elapsed, 0.001),
		])
		if _has_reached_timeout(start_time, timeout_seconds) and completed_run_count < run_count:
			timed_out = true
			break

	var elapsed_total := (Time.get_ticks_msec() - start_time) / 1000.0
	var n := maxf(float(completed_run_count), 1.0)

	var report := {
		"batch_id": start_seed,
		"start_seed": start_seed,
		"start_seed_source": start_seed_source,
		"run_count": completed_run_count,
		"requested_run_count": run_count,
		"completed_run_count": completed_run_count,
		"timeout_seconds": timeout_seconds,
		"timed_out": timed_out,
		"elapsed_seconds": elapsed_total,
		"ended_count": ended_count,
		"avg_iterations": float(total_iterations) / n,
		"avg_timeline_steps": float(total_timeline_steps) / n,
		"win_rate": {
			"player": total_wins_player,
			"hostile": total_wins_hostile,
			"draw": total_draws,
		},
		"global": {
			"charge": _build_skill_report(total_charge_attempts, total_charge_successes, total_charge_mastery, n),
			"warrior_heavy_strike": _build_skill_report(total_heavy_attempts, total_heavy_successes, total_heavy_mastery, n),
			"archer_aimed_shot": _build_skill_report(total_aimed_attempts, total_aimed_successes, total_aimed_mastery, n),
			"archer_multishot": _build_skill_report(total_multishot_attempts, total_multishot_successes, total_multishot_mastery, n),
			"basic_attack": _build_skill_report(total_basic_attempts, total_basic_successes, total_basic_mastery, n),
		},
		"player": {
			"total_damage_done": player_damage_done,
			"total_damage_taken": player_damage_taken,
			"avg_damage_done_per_run": float(player_damage_done) / n,
			"avg_damage_taken_per_run": float(player_damage_taken) / n,
			"charge": _build_skill_report(player_charge_attempts, player_charge_successes, 0, n),
			"warrior_heavy_strike": _build_skill_report(player_heavy_attempts, player_heavy_successes, 0, n),
			"archer_aimed_shot": _build_skill_report(player_aimed_attempts, player_aimed_successes, 0, n),
			"archer_multishot": _build_skill_report(player_multishot_attempts, player_multishot_successes, 0, n),
			"basic_attack": _build_skill_report(player_basic_attempts, player_basic_successes, 0, n),
		},
		"hostile": {
			"total_damage_done": hostile_damage_done,
			"total_damage_taken": hostile_damage_taken,
			"avg_damage_done_per_run": float(hostile_damage_done) / n,
			"avg_damage_taken_per_run": float(hostile_damage_taken) / n,
			"charge": _build_skill_report(hostile_charge_attempts, hostile_charge_successes, 0, n),
			"warrior_heavy_strike": _build_skill_report(hostile_heavy_attempts, hostile_heavy_successes, 0, n),
			"archer_aimed_shot": _build_skill_report(hostile_aimed_attempts, hostile_aimed_successes, 0, n),
			"archer_multishot": _build_skill_report(hostile_multishot_attempts, hostile_multishot_successes, 0, n),
			"basic_attack": _build_skill_report(hostile_basic_attempts, hostile_basic_successes, 0, n),
		},
		"per_unit_summary": per_unit_summary,
		"runs": run_details,
	}
	if trace_ai:
		report["trace_summary_file"] = _resolve_trace_summary_path(output_path)

	if output_path.is_empty():
		print(JSON.stringify(report, "\t"))
	else:
		if not _write_json_file(output_path, report):
			print("[ERROR] Failed to write: %s" % output_path)
		else:
			_print_progress("[Progress] wrote report %s" % output_path)
	if trace_ai:
		var trace_summary_path := String(report.get("trace_summary_file", ""))
		var compact_report := BATTLE_SIM_TRACE_SUMMARY_BUILDER_SCRIPT.new().build(report, output_path)
		if not _write_json_file(trace_summary_path, compact_report):
			print("[ERROR] Failed to write trace summary: %s" % trace_summary_path)
		else:
			_print_progress("[Progress] wrote trace summary %s" % trace_summary_path)
	quit()


func _build_formal_fixture(
	scenario_def,
	overrides: Dictionary,
	progression_registry,
	item_registry,
	roster_options: Dictionary = {},
	attribute_roll_seed: int = 0
):
	var fixture = BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.new()
	fixture.setup_content({
		"skill_defs": overrides.get("skill_defs", {}),
		"profession_defs": progression_registry.get_profession_defs(),
		"achievement_defs": progression_registry.get_achievement_defs(),
		"item_defs": item_registry.get_item_defs(),
		"progression_content_bundle": progression_registry.get_bundle(),
	})
	var effective_roster_options := roster_options.duplicate(true)
	if not effective_roster_options.has(BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_OPTION_ATTRIBUTE_ROLL_SEED) \
			and not effective_roster_options.has(StringName(BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_OPTION_ATTRIBUTE_ROLL_SEED)):
		effective_roster_options[BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_OPTION_ATTRIBUTE_ROLL_SEED] = attribute_roll_seed
	if not fixture.build_roster(scenario_def.scenario_id, effective_roster_options):
		push_error("Unsupported formal battle sim roster: %s" % String(scenario_def.scenario_id))
	return fixture


func _build_roster_options_from_environment() -> Dictionary:
	var options := {}
	if OS.has_environment("MAIN_CHARACTER_MEMBER_ID"):
		var member_id := OS.get_environment("MAIN_CHARACTER_MEMBER_ID").strip_edges()
		if not member_id.is_empty():
			options[BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_OPTION_MAIN_CHARACTER_MEMBER_ID] = StringName(member_id)
	if OS.has_environment("LEADER_MEMBER_ID"):
		var leader_id := OS.get_environment("LEADER_MEMBER_ID").strip_edges()
		if not leader_id.is_empty():
			options[BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_OPTION_LEADER_MEMBER_ID] = StringName(leader_id)
	if OS.has_environment("MAIN_CHARACTER_REROLL_COUNT"):
		options[BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_OPTION_MAIN_CHARACTER_REROLL_COUNT] = int(OS.get_environment("MAIN_CHARACTER_REROLL_COUNT"))
	if OS.has_environment("ATTRIBUTE_ROLL_SEED"):
		options[BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.ROSTER_OPTION_ATTRIBUTE_ROLL_SEED] = int(OS.get_environment("ATTRIBUTE_ROLL_SEED"))
	return options


func _run_single_simulation(
	scenario_def,
	overrides: Dictionary,
	content_provider,
	terrain_generator,
	fixture,
	seed: int,
	trace_ai: bool = false,
	progress_context: Dictionary = {}
) -> Dictionary:
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	var use_formal_terrain := bool(scenario_def.use_formal_terrain_generation) if scenario_def != null else false
	runtime.setup(
		fixture,
		overrides.get("skill_defs", {}),
		_get_content_dictionary(content_provider, &"get_enemy_templates"),
		overrides.get("enemy_ai_brains", {}),
		null, null, fixture.get_item_defs(),
		null if use_formal_terrain else terrain_generator
	)
	runtime.set_ai_trace_enabled(trace_ai)
	runtime.set_ai_score_profile(overrides.get("ai_score_profile", null))

	var encounter_anchor = ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = scenario_def.scenario_id if scenario_def.scenario_id != &"" else &"battle_sim"
	encounter_anchor.display_name = scenario_def.display_name if not scenario_def.display_name.is_empty() else String(scenario_def.scenario_id)
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.world_coord = Vector2i.ZERO
	encounter_anchor.region_tag = &"simulation"

	var context: Dictionary = fixture.build_runtime_context(runtime, scenario_def.build_start_context())
	var state = runtime.start_battle(encounter_anchor, seed, context)
	fixture.apply_started_battle_metadata(state)

	var execution_loop = BATTLE_SIM_EXECUTION_LOOP_SCRIPT.new()
	var loop_result: Dictionary = execution_loop.run(runtime, state, scenario_def, {
		"max_idle_loops": MAX_IDLE_LOOPS,
		"progress_iteration_interval": 1 if _progress_enabled else 0,
		"progress_callback": Callable(self, "_handle_single_run_progress"),
		"progress_context": progress_context,
	})
	var iterations := int(loop_result.get("iterations", 0))
	var timeline_steps := int(loop_result.get("timeline_steps", 0))

	var metrics := runtime.get_battle_metrics().duplicate(true)
	var run_result := {
		"battle_ended": state != null and state.phase == &"battle_ended",
		"winner_faction_id": String(state.winner_faction_id) if state != null else "",
		"iterations": iterations,
		"timeline_steps": timeline_steps,
		"metrics": metrics,
		"units": metrics.get("units", {}),
		"factions": metrics.get("factions", {}),
	}
	if trace_ai:
		run_result["ai_turn_traces"] = runtime.get_ai_turn_traces().duplicate(true)
	runtime.dispose()
	return run_result


func _get_content_dictionary(content_provider, method_name: StringName) -> Dictionary:
	if content_provider == null or not content_provider.has_method(method_name):
		return {}
	var value = content_provider.call(method_name)
	return value if value is Dictionary else {}


func _read_bool_environment(name: String, default_value: bool = false) -> bool:
	if not OS.has_environment(name):
		return default_value
	var value := OS.get_environment(name).strip_edges().to_lower()
	return value in ["1", "true", "yes", "on"]


func _read_int_environment(name: String, default_value: int) -> int:
	if not OS.has_environment(name):
		return default_value
	var value := OS.get_environment(name).strip_edges()
	if value.is_empty():
		return default_value
	return int(value)


func _read_float_environment(name: String, default_value: float) -> float:
	if not OS.has_environment(name):
		return default_value
	var value := OS.get_environment(name).strip_edges()
	if value.is_empty():
		return default_value
	return float(value)


func _has_reached_timeout(start_time_msec: int, timeout_seconds: int) -> bool:
	if timeout_seconds <= 0:
		return false
	var elapsed_seconds := (Time.get_ticks_msec() - start_time_msec) / 1000.0
	return elapsed_seconds >= float(timeout_seconds)


func _handle_single_run_progress(progress_data: Dictionary) -> void:
	if not _progress_enabled:
		return
	var now := Time.get_ticks_msec()
	if _last_progress_print_msec > 0 and now - _last_progress_print_msec < _progress_interval_msec:
		return
	_last_progress_print_msec = now

	var context: Dictionary = progress_data.get("context", {}) if progress_data.get("context", {}) is Dictionary else {}
	var state = progress_data.get("state", null)
	var iterations := int(progress_data.get("iterations", 0))
	var max_iterations := int(context.get("max_iterations", 0))
	var progress_ratio := 0.0
	if max_iterations > 0:
		progress_ratio = clampf(float(iterations) / float(max_iterations), 0.0, 1.0)
	var run_start_time := int(context.get("run_start_time", now))
	var batch_start_time := int(context.get("batch_start_time", run_start_time))
	var run_elapsed := float(now - run_start_time) / 1000.0
	var batch_elapsed := float(now - batch_start_time) / 1000.0
	var max_eta := 0.0
	if iterations > 0 and max_iterations > iterations:
		max_eta = run_elapsed * float(max_iterations - iterations) / float(iterations)

	var phase := ""
	var active_unit_id := ""
	var current_tu := 0
	var player_alive := 0
	var hostile_alive := 0
	if state != null:
		phase = String(state.phase)
		active_unit_id = String(state.active_unit_id)
		current_tu = int(state.timeline.current_tu) if state.timeline != null else 0
		player_alive = _count_living_units(state, state.ally_unit_ids)
		hostile_alive = _count_living_units(state, state.enemy_unit_ids)

	_print_progress("[Progress] run %d/%d seed=%d elapsed=%.1fs batch=%.1fs iter=%d/%d %.1f%% max_eta=%.1fs timeline_steps=%d tu=%d phase=%s active=%s alive=%d/%d" % [
		int(context.get("run_index", 0)) + 1,
		int(context.get("run_count", 0)),
		int(context.get("seed", 0)),
		run_elapsed,
		batch_elapsed,
		iterations,
		max_iterations,
		progress_ratio * 100.0,
		max_eta,
		int(progress_data.get("timeline_steps", 0)),
		current_tu,
		phase,
		active_unit_id,
		player_alive,
		hostile_alive,
	])


func _count_living_units(state, unit_ids: Array) -> int:
	if state == null:
		return 0
	var count := 0
	for unit_id in unit_ids:
		var unit_state = state.units.get(unit_id)
		if unit_state != null and bool(unit_state.is_alive):
			count += 1
	return count


func _print_progress(message: String) -> void:
	if _progress_enabled:
		print(message)


func _resolve_trace_summary_path(output_path: String) -> String:
	if OS.has_environment("TRACE_SUMMARY_FILE"):
		var explicit_path := OS.get_environment("TRACE_SUMMARY_FILE").strip_edges()
		if not explicit_path.is_empty():
			return explicit_path
	if not output_path.is_empty():
		return "%s_trace_summary.json" % output_path.get_basename()
	return "user://simulation_reports/mixed_6v12_trace_summary_%d.json" % int(Time.get_unix_time_from_system())


func _write_json_file(path: String, payload: Dictionary) -> bool:
	if path.is_empty():
		return false
	var abs_path := ProjectSettings.globalize_path(path) if path.begins_with("res://") or path.begins_with("user://") else path
	var dir := abs_path.get_base_dir()
	if not dir.is_empty():
		DirAccess.make_dir_recursive_absolute(dir)
	var f = FileAccess.open(abs_path, FileAccess.WRITE)
	if f == null:
		return false
	f.store_string(JSON.stringify(payload, "\t"))
	f.close()
	return true


func _build_skill_report(attempts: int, successes: int, mastery: int, run_count_f: float) -> Dictionary:
	return {
		"attempts": attempts,
		"successes": successes,
		"mastery": mastery,
		"avg_attempts_per_run": float(attempts) / run_count_f,
		"avg_mastery_per_run": float(mastery) / run_count_f,
	}
