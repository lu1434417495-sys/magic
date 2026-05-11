extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BATTLE_SIM_SCENARIO_DEF_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_scenario_def.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BATTLE_SIM_CONTENT_PROVIDER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_content_provider.gd")
const BATTLE_SIM_OVERRIDE_APPLIER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_override_applier.gd")
const BATTLE_SIM_PROFILE_DEF_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_profile_def.gd")
const BATTLE_SIM_TERRAIN_GENERATOR_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_terrain_generator.gd")
const BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_formal_combat_fixture.gd")
const BATTLE_SIM_EXECUTION_LOOP_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_execution_loop.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/world/encounter_anchor_data.gd")
const PROGRESSION_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/progression/progression_content_registry.gd")
const ITEM_CONTENT_REGISTRY_SCRIPT = preload("res://scripts/player/warehouse/item_content_registry.gd")
const TRUE_RANDOM_SEED_SERVICE_SCRIPT = preload("res://scripts/utils/true_random_seed_service.gd")

const MAX_IDLE_LOOPS := 25


func _initialize() -> void:
	var start_seed_source := "environment" if OS.has_environment("START_SEED") else "true_random"
	var start_seed := int(OS.get_environment("START_SEED")) if OS.has_environment("START_SEED") else TRUE_RANDOM_SEED_SERVICE_SCRIPT.generate_seed()
	var run_count := int(OS.get_environment("COUNT")) if OS.has_environment("COUNT") else 10
	var output_path := OS.get_environment("OUTPUT_FILE") if OS.has_environment("OUTPUT_FILE") else ""
	var roster_options := _build_roster_options_from_environment()

	var scenario_path := "res://data/configs/battle_sim/scenarios/mixed_2sword_1arch_mirror_simulation.tres"
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

	var ended_count := 0
	var total_iterations := 0
	var total_timeline_steps := 0
	var total_wins_player := 0
	var total_wins_hostile := 0
	var total_draws := 0

	var start_time := Time.get_ticks_msec()

	for run_index in range(run_count):
		var seed := rng.randi()
		var fixture = _build_formal_fixture(scenario_def, overrides, progression_registry, item_registry, roster_options)
		var result := _run_single_simulation(scenario_def, overrides, content_provider, terrain_generator, fixture, seed)
		var metrics: Dictionary = result.get("metrics", {})
		var factions: Dictionary = metrics.get("factions", {})

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

		for faction_data in factions.values():
			if faction_data is Dictionary:
				var skill_attempts = faction_data.get("skill_attempt_counts", {})
				var skill_successes = faction_data.get("skill_success_counts", {})
				charge_attempts += int(skill_attempts.get("charge", 0))
				charge_successes += int(skill_successes.get("charge", 0))
				heavy_attempts += int(skill_attempts.get("warrior_heavy_strike", 0))
				heavy_successes += int(skill_successes.get("warrior_heavy_strike", 0))
				aimed_attempts += int(skill_attempts.get("archer_aimed_shot", 0))
				aimed_successes += int(skill_successes.get("archer_aimed_shot", 0))
				multishot_attempts += int(skill_attempts.get("archer_multishot", 0))
				multishot_successes += int(skill_successes.get("archer_multishot", 0))
				basic_attempts += int(skill_attempts.get("basic_attack", 0))
				basic_successes += int(skill_successes.get("basic_attack", 0))

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

		if (run_index + 1) % 1 == 0 and output_path.is_empty():
			var elapsed := (Time.get_ticks_msec() - start_time) / 1000.0
			print("[Progress] %d/%d (%.1fs, %.2f runs/s)" % [
				run_index + 1, run_count, elapsed,
				float(run_index + 1) / maxf(elapsed, 0.001)
			])

	var elapsed_total := (Time.get_ticks_msec() - start_time) / 1000.0
	var n := float(run_count)

	var report := {
		"batch_id": start_seed,
		"start_seed": start_seed,
		"start_seed_source": start_seed_source,
		"run_count": run_count,
		"elapsed_seconds": elapsed_total,
		"ended_count": ended_count,
		"avg_iterations": float(total_iterations) / n,
		"avg_timeline_steps": float(total_timeline_steps) / n,
		"win_rate": {
			"player": total_wins_player,
			"hostile": total_wins_hostile,
			"draw": total_draws,
		},
		"charge": {
			"attempts": total_charge_attempts,
			"successes": total_charge_successes,
			"mastery": total_charge_mastery,
			"avg_attempts_per_run": float(total_charge_attempts) / n,
			"avg_mastery_per_run": float(total_charge_mastery) / n,
		},
		"warrior_heavy_strike": {
			"attempts": total_heavy_attempts,
			"successes": total_heavy_successes,
			"mastery": total_heavy_mastery,
			"avg_attempts_per_run": float(total_heavy_attempts) / n,
			"avg_mastery_per_run": float(total_heavy_mastery) / n,
		},
		"archer_aimed_shot": {
			"attempts": total_aimed_attempts,
			"successes": total_aimed_successes,
			"mastery": total_aimed_mastery,
			"avg_attempts_per_run": float(total_aimed_attempts) / n,
			"avg_mastery_per_run": float(total_aimed_mastery) / n,
		},
		"archer_multishot": {
			"attempts": total_multishot_attempts,
			"successes": total_multishot_successes,
			"mastery": total_multishot_mastery,
			"avg_attempts_per_run": float(total_multishot_attempts) / n,
			"avg_mastery_per_run": float(total_multishot_mastery) / n,
		},
		"basic_attack": {
			"attempts": total_basic_attempts,
			"successes": total_basic_successes,
			"mastery": total_basic_mastery,
			"avg_attempts_per_run": float(total_basic_attempts) / n,
			"avg_mastery_per_run": float(total_basic_mastery) / n,
		},
	}

	if output_path.is_empty():
		print(JSON.stringify(report, "\t"))
	else:
		var abs_path := ProjectSettings.globalize_path(output_path) if output_path.begins_with("res://") or output_path.begins_with("user://") else output_path
		var dir := abs_path.get_base_dir()
		if not dir.is_empty():
			DirAccess.make_dir_recursive_absolute(dir)
		var f = FileAccess.open(abs_path, FileAccess.WRITE)
		if f != null:
			f.store_string(JSON.stringify(report, "\t"))
			f.close()
		else:
			print("[ERROR] Failed to write: %s" % abs_path)
	quit()


func _build_formal_fixture(scenario_def, overrides: Dictionary, progression_registry, item_registry, roster_options: Dictionary = {}):
	var fixture = BATTLE_SIM_FORMAL_COMBAT_FIXTURE_SCRIPT.new()
	fixture.setup_content({
		"skill_defs": overrides.get("skill_defs", {}),
		"profession_defs": progression_registry.get_profession_defs(),
		"achievement_defs": progression_registry.get_achievement_defs(),
		"item_defs": item_registry.get_item_defs(),
		"progression_content_bundle": progression_registry.get_bundle(),
	})
	if not fixture.build_roster(scenario_def.scenario_id, roster_options):
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
	return options


func _run_single_simulation(scenario_def, overrides: Dictionary, content_provider, terrain_generator, fixture, seed: int) -> Dictionary:
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
	runtime.set_ai_trace_enabled(false)
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
	}
	runtime.dispose()
	return run_result


func _get_content_dictionary(content_provider, method_name: StringName) -> Dictionary:
	if content_provider == null or not content_provider.has_method(method_name):
		return {}
	var value = content_provider.call(method_name)
	return value if value is Dictionary else {}
