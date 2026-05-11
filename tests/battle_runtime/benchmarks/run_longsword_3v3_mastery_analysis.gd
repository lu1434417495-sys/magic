extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BATTLE_SIM_SCENARIO_DEF_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_scenario_def.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BATTLE_SIM_CONTENT_PROVIDER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_content_provider.gd")
const BATTLE_SIM_OVERRIDE_APPLIER_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_override_applier.gd")
const BATTLE_SIM_PROFILE_DEF_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_profile_def.gd")
const BATTLE_SIM_TERRAIN_GENERATOR_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_terrain_generator.gd")
const BATTLE_SIM_EXECUTION_LOOP_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_execution_loop.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/world/encounter_anchor_data.gd")
const CHARACTER_PROGRESSION_DELTA_SCRIPT = preload("res://scripts/systems/progression/character_progression_delta.gd")

const MAX_IDLE_LOOPS := 25


class TestMasteryGateway extends RefCounted:
	var charge_mastery := 0
	var heavy_mastery := 0

	func grant_battle_mastery(member_id: StringName, skill_id: StringName, amount: int):
		var s := String(skill_id)
		if s == "charge":
			charge_mastery += amount
		elif s == "warrior_heavy_strike":
			heavy_mastery += amount
		var delta = CHARACTER_PROGRESSION_DELTA_SCRIPT.new()
		delta.member_id = member_id
		delta.mastery_changes.append({"skill_id": skill_id, "amount": amount, "source_type": &"battle"})
		return delta

	func grant_skill_mastery_from_source(
		member_id: StringName, skill_id: StringName, amount: int,
		source_type: StringName, source_label: String = "",
		reason_text: String = "", emit_achievement_event: bool = true
	):
		var s := String(skill_id)
		if s == "charge":
			charge_mastery += amount
		elif s == "warrior_heavy_strike":
			heavy_mastery += amount
		var delta = CHARACTER_PROGRESSION_DELTA_SCRIPT.new()
		delta.member_id = member_id
		delta.mastery_changes.append({"skill_id": skill_id, "amount": amount, "source_type": source_type})
		return delta

	func record_achievement_event(
		member_id: StringName, event_type: StringName,
		amount: int = 1, subject_id: StringName = &"", meta: Dictionary = {}
	) -> Array[StringName]:
		return []

	func get_member_state(member_id: StringName):
		return null


func _initialize() -> void:
	var start_seed := int(OS.get_environment("START_SEED")) if OS.has_environment("START_SEED") else 0
	var run_count := int(OS.get_environment("COUNT")) if OS.has_environment("COUNT") else 1000
	var output_path := OS.get_environment("OUTPUT_FILE") if OS.has_environment("OUTPUT_FILE") else ""

	var scenario_path := "res://data/configs/battle_sim/scenarios/longsword_3v3_mirror_simulation.tres"
	var scenario_def = load(scenario_path)
	if scenario_def == null:
		print("[ERROR] Failed to load scenario")
		quit()
		return

	var content_provider = BATTLE_SIM_CONTENT_PROVIDER_SCRIPT.new()
	var override_applier = BATTLE_SIM_OVERRIDE_APPLIER_SCRIPT.new()
	var terrain_generator = BATTLE_SIM_TERRAIN_GENERATOR_SCRIPT.new()

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
	var total_charge_mastery := 0
	var total_heavy_mastery := 0
	var ended_count := 0
	var total_iterations := 0
	var total_timeline_steps := 0
	var heavy_mastery_nonzero_count := 0

	var start_time := Time.get_ticks_msec()

	for run_index in range(run_count):
		var seed := rng.randi()
		var gateway := TestMasteryGateway.new()
		var result := _run_single_simulation(scenario_def, overrides, content_provider, terrain_generator, gateway, seed)
		var metrics: Dictionary = result.get("metrics", {})
		var factions: Dictionary = metrics.get("factions", {})

		var charge_attempts := 0
		var charge_successes := 0
		var heavy_attempts := 0
		var heavy_successes := 0
		for faction_data in factions.values():
			if faction_data is Dictionary:
				charge_attempts += int(faction_data.get("skill_attempt_counts", {}).get("charge", 0))
				charge_successes += int(faction_data.get("skill_success_counts", {}).get("charge", 0))
				heavy_attempts += int(faction_data.get("skill_attempt_counts", {}).get("warrior_heavy_strike", 0))
				heavy_successes += int(faction_data.get("skill_success_counts", {}).get("warrior_heavy_strike", 0))

		total_charge_attempts += charge_attempts
		total_charge_successes += charge_successes
		total_heavy_attempts += heavy_attempts
		total_heavy_successes += heavy_successes
		total_charge_mastery += gateway.charge_mastery
		total_heavy_mastery += gateway.heavy_mastery
		if result.get("battle_ended", false):
			ended_count += 1
		total_iterations += result.get("iterations", 0)
		total_timeline_steps += result.get("timeline_steps", 0)
		if gateway.heavy_mastery > 0:
			heavy_mastery_nonzero_count += 1

		if (run_index + 1) % 10 == 0 and output_path.is_empty():
			var elapsed := (Time.get_ticks_msec() - start_time) / 1000.0
			print("[Progress] %d/%d (%.1fs, %.2f runs/s)" % [
				run_index + 1, run_count, elapsed,
				float(run_index + 1) / maxf(elapsed, 0.001)
			])

	var elapsed_total := (Time.get_ticks_msec() - start_time) / 1000.0

	var report := {
		"batch_id": start_seed,
		"run_count": run_count,
		"elapsed_seconds": elapsed_total,
		"ended_count": ended_count,
		"avg_iterations": float(total_iterations) / float(run_count),
		"avg_timeline_steps": float(total_timeline_steps) / float(run_count),
		"charge": {
			"attempts": total_charge_attempts,
			"successes": total_charge_successes,
			"mastery": total_charge_mastery,
			"avg_attempts_per_run": float(total_charge_attempts) / float(run_count),
			"avg_mastery_per_run": float(total_charge_mastery) / float(run_count),
			"avg_mastery_per_person": float(total_charge_mastery) / float(run_count) / 6.0,
		},
		"warrior_heavy_strike": {
			"attempts": total_heavy_attempts,
			"successes": total_heavy_successes,
			"mastery": total_heavy_mastery,
			"avg_attempts_per_run": float(total_heavy_attempts) / float(run_count),
			"avg_mastery_per_run": float(total_heavy_mastery) / float(run_count),
			"avg_mastery_per_person": float(total_heavy_mastery) / float(run_count) / 6.0,
			"nonzero_runs": heavy_mastery_nonzero_count,
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


func _run_single_simulation(scenario_def, overrides: Dictionary, content_provider, terrain_generator, gateway: TestMasteryGateway, seed: int) -> Dictionary:
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	var use_formal_terrain := bool(scenario_def.use_formal_terrain_generation) if scenario_def != null else false
	runtime.setup(
		null,
		overrides.get("skill_defs", {}),
		_get_content_dictionary(content_provider, &"get_enemy_templates"),
		overrides.get("enemy_ai_brains", {}),
		null, null, {},
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

	var state = runtime.start_battle(encounter_anchor, seed, scenario_def.build_start_context())
	runtime._character_gateway = gateway

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
