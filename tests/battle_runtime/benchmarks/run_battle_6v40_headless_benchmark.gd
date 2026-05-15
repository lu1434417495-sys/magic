extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BattleRuntimeTestHelpers = preload("res://tests/shared/battle_runtime_test_helpers.gd")

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BATTLE_SIM_EXECUTION_LOOP_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_execution_loop.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BATTLE_HUD_ADAPTER_SCRIPT = preload("res://scripts/systems/battle/presentation/battle_hud_adapter.gd")
const ENEMY_AI_BRAIN_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_brain_def.gd")
const ENEMY_AI_STATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_state_def.gd")
const USE_GROUND_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_ground_skill_action.gd")
const WAIT_ACTION_SCRIPT = preload("res://scripts/enemies/actions/wait_action.gd")
const SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/skill_def.gd")
const COMBAT_SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/combat_skill_def.gd")
const COMBAT_CAST_VARIANT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")

const MAP_SIZE := Vector2i(20, 14)
const DEFAULT_TARGET_TU := 200
const MAX_ITERATIONS := 5000
const TIMELINE_TICKS_PER_STEP := 1
const TIMELINE_TU_PER_TICK := 5
const ACTION_THRESHOLD := 120
const ALLY_COUNT := 6
const ENEMY_COUNT := 40
const SCENARIO_MIXED_PRESSURE := &"mixed_pressure"
const SCENARIO_GROUND_SKILL_HEAVY := &"ground_skill_heavy"
const HEAVY_GROUND_SKILL_ID := &"benchmark_ground_barrage"
const HEAVY_GROUND_BRAIN_ID := &"benchmark_ground_barrage_brain"

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var target_tu := _resolve_target_tu()
	var results: Array[Dictionary] = []
	var comparisons: Array[Dictionary] = []
	for scenario_id in [SCENARIO_MIXED_PRESSURE, SCENARIO_GROUND_SKILL_HEAVY]:
		var logic_only := _run_pass(scenario_id, "logic_only", false, target_tu)
		var logic_plus_hud := _run_pass(scenario_id, "logic_plus_hud_snapshot", true, target_tu)
		_assert_consistent_outcome(scenario_id, logic_only, logic_plus_hud)
		results.append(logic_only)
		results.append(logic_plus_hud)
		comparisons.append({
			"scenario_id": scenario_id,
			"logic_only": logic_only,
			"logic_plus_hud": logic_plus_hud,
		})

	if not _failures.is_empty():
		for failure in _failures:
			push_error(failure)
		print("Battle 6v40 headless benchmark: FAIL (%d)" % _failures.size())
		quit(1)
		return

	for result in results:
		print(_format_result(result))
	for comparison in comparisons:
		print(
			_format_comparison(
				StringName(comparison.get("scenario_id", &"")),
				comparison.get("logic_only", {}),
				comparison.get("logic_plus_hud", {})
			)
		)
	print("Battle 6v40 headless benchmark: PASS")
	quit(0)


func _run_pass(
	scenario_id: StringName,
	pass_id: String,
	include_hud_snapshot: bool,
	target_tu: int
) -> Dictionary:
	var runtime = _build_runtime(scenario_id)
	var state = _build_flat_state(MAP_SIZE, scenario_id)
	_populate_units(runtime, state, scenario_id)
	runtime._state = state
	var execution_loop = BATTLE_SIM_EXECUTION_LOOP_SCRIPT.new()

	var hud_adapter = BATTLE_HUD_ADAPTER_SCRIPT.new() if include_hud_snapshot else null
	var logic_usec := 0
	var hud_usec := 0
	var timeline_steps := 0
	var ai_turns := 0
	var manual_turns := 0
	var max_ready_queue := 0
	var idle_loops := 0
	var iterations := 0

	while iterations < MAX_ITERATIONS:
		iterations += 1
		max_ready_queue = maxi(max_ready_queue, state.timeline.ready_unit_ids.size())
		if state.phase == &"battle_ended" or int(state.timeline.current_tu) >= target_tu:
			break

		var previous_tu := int(state.timeline.current_tu)
		var previous_phase := StringName(state.phase)
		var previous_active_unit_id := StringName(state.active_unit_id)
		var previous_log_count: int = state.log_entries.size()

		var logic_start := Time.get_ticks_usec()
		if state.phase == &"unit_acting":
			var active_unit = state.units.get(state.active_unit_id) as BattleUnitState
			if active_unit == null or not active_unit.is_alive:
				_test.fail(
					"Benchmark scenario %s pass %s hit an invalid active unit state." % [
						String(scenario_id),
						pass_id,
					]
				)
				break
			if active_unit.control_mode == &"manual":
				manual_turns += 1
			else:
				ai_turns += 1
			execution_loop.advance_step(runtime, state, &"wait", TIMELINE_TICKS_PER_STEP)
		elif execution_loop.has_ready_units(state):
			execution_loop.advance_step(runtime, state, &"wait", TIMELINE_TICKS_PER_STEP)
		else:
			execution_loop.advance_step(runtime, state, &"wait", TIMELINE_TICKS_PER_STEP)
			timeline_steps += 1
		logic_usec += Time.get_ticks_usec() - logic_start

		if hud_adapter != null:
			var hud_start := Time.get_ticks_usec()
			var selected_coord := _resolve_selected_coord(state)
			hud_adapter.build_snapshot(state, selected_coord)
			hud_usec += Time.get_ticks_usec() - hud_start

		var made_progress: bool = (
			int(state.timeline.current_tu) != previous_tu
			or StringName(state.phase) != previous_phase
			or StringName(state.active_unit_id) != previous_active_unit_id
			or state.log_entries.size() != previous_log_count
		)
		if made_progress:
			idle_loops = 0
		else:
			idle_loops += 1
			if idle_loops >= 25:
				_test.fail(
					"Benchmark scenario %s pass %s stalled for %d loops at TU=%d phase=%s." % [
						String(scenario_id),
						pass_id,
						idle_loops,
						int(state.timeline.current_tu),
						String(state.phase),
					]
				)
				break

	var result := {
		"scenario_id": String(scenario_id),
		"pass_id": pass_id,
		"target_tu": target_tu,
		"include_hud_snapshot": include_hud_snapshot,
		"iterations": iterations,
		"final_tu": int(state.timeline.current_tu),
		"battle_ended": state.phase == &"battle_ended",
		"phase": String(state.phase),
		"logic_usec": logic_usec,
		"hud_usec": hud_usec,
		"total_usec": logic_usec + hud_usec,
		"timeline_steps": timeline_steps,
		"ai_turns": ai_turns,
		"manual_turns": manual_turns,
		"max_ready_queue": max_ready_queue,
		"ally_alive": _count_living_units(state, state.ally_unit_ids),
		"enemy_alive": _count_living_units(state, state.enemy_unit_ids),
		"winner_faction_id": String(state.winner_faction_id),
	}
	var resolution_result = runtime.get_battle_resolution_result()
	result["resolution_winner_faction_id"] = String(resolution_result.winner_faction_id) if resolution_result != null else ""
	result["resolution_loot_count"] = resolution_result.loot_entries.size() if resolution_result != null else 0
	runtime.dispose()
	return result


func _build_runtime(scenario_id: StringName):
	var game_session = GAME_SESSION_SCRIPT.new()
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	runtime.setup(
		null,
		game_session.get_skill_defs(),
		game_session.get_enemy_templates(),
		game_session.get_enemy_ai_brains(),
		null
	)
	if scenario_id == SCENARIO_GROUND_SKILL_HEAVY:
		_install_ground_skill_heavy_content(runtime)
	game_session.free()
	return runtime


func _build_flat_state(map_size: Vector2i, scenario_id: StringName):
	var state = BATTLE_STATE_SCRIPT.new()
	state.battle_id = StringName("battle_6v40_%s" % [String(scenario_id)])
	state.phase = &"timeline_running"
	state.map_size = map_size
	state.timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
	state.timeline.tu_per_tick = TIMELINE_TU_PER_TICK
	state.timeline.frozen = false
	for y in range(map_size.y):
		for x in range(map_size.x):
			var cell = BATTLE_CELL_STATE_SCRIPT.new()
			cell.coord = Vector2i(x, y)
			cell.base_terrain = BATTLE_CELL_STATE_SCRIPT.TERRAIN_LAND
			cell.base_height = 4
			cell.height_offset = 0
			cell.recalculate_runtime_values()
			state.cells[cell.coord] = cell
	state.cell_columns = BATTLE_CELL_STATE_SCRIPT.build_columns_from_surface_cells(state.cells)
	return state


func _populate_units(runtime, state, scenario_id: StringName) -> void:
	match scenario_id:
		SCENARIO_GROUND_SKILL_HEAVY:
			_populate_ground_skill_heavy_units(runtime, state)
		_:
			_populate_mixed_pressure_units(runtime, state)


func _populate_mixed_pressure_units(runtime, state) -> void:
	var ally_positions := [
		Vector2i(1, 4),
		Vector2i(1, 6),
		Vector2i(1, 8),
		Vector2i(2, 5),
		Vector2i(2, 7),
		Vector2i(2, 9),
	]
	for index in range(ALLY_COUNT):
		var ally_unit = _build_manual_benchmark_unit(
			StringName("benchmark_ally_%02d" % [index + 1]),
			"基准友军%02d" % [index + 1],
			ally_positions[index]
		)
		_add_unit_to_state(runtime, state, ally_unit, false)

	var enemy_positions: Array[Vector2i] = []
	for y in range(4, 9):
		for x in range(10, 18):
			enemy_positions.append(Vector2i(x, y))
	enemy_positions.sort_custom(func(left: Vector2i, right: Vector2i) -> bool:
		return left.y < right.y or (left.y == right.y and left.x < right.x)
	)

	for index in range(ENEMY_COUNT):
		var enemy_unit: BattleUnitState = null
		if index % 2 == 0:
			enemy_unit = _build_ai_benchmark_unit(
				StringName("benchmark_enemy_%02d" % [index + 1]),
				"狼群%02d" % [index + 1],
				enemy_positions[index],
				&"melee_aggressor",
				[&"charge", &"warrior_heavy_strike"]
			)
		else:
			enemy_unit = _build_ai_benchmark_unit(
				StringName("benchmark_enemy_%02d" % [index + 1]),
				"猎压者%02d" % [index + 1],
				enemy_positions[index],
				&"ranged_suppressor",
				[&"archer_suppressive_fire", &"archer_pinning_shot"]
			)
		_add_unit_to_state(runtime, state, enemy_unit, true)


func _populate_ground_skill_heavy_units(runtime, state) -> void:
	var ally_positions := [
		Vector2i(4, 5),
		Vector2i(5, 5),
		Vector2i(6, 5),
		Vector2i(4, 6),
		Vector2i(5, 6),
		Vector2i(6, 6),
	]
	for index in range(ALLY_COUNT):
		var ally_unit = _build_manual_benchmark_unit(
			StringName("ground_heavy_ally_%02d" % [index + 1]),
			"压测友军%02d" % [index + 1],
			ally_positions[index]
		)
		ally_unit.current_hp = 720
		ally_unit.attribute_snapshot.set_value(&"hp_max", 720)
		ally_unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 30)
		ally_unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 30)
		_add_unit_to_state(runtime, state, ally_unit, false)

	var enemy_positions: Array[Vector2i] = []
	for y in range(3, 8):
		for x in range(11, 19):
			enemy_positions.append(Vector2i(x, y))
	enemy_positions.sort_custom(func(left: Vector2i, right: Vector2i) -> bool:
		return left.y < right.y or (left.y == right.y and left.x < right.x)
	)

	for index in range(ENEMY_COUNT):
		var enemy_unit = _build_ai_benchmark_unit(
			StringName("ground_heavy_enemy_%02d" % [index + 1]),
			"轰压者%02d" % [index + 1],
			enemy_positions[index],
			HEAVY_GROUND_BRAIN_ID,
			[HEAVY_GROUND_SKILL_ID]
		)
		enemy_unit.ai_state_id = &"pressure"
		enemy_unit.current_hp = 220
		enemy_unit.current_mp = 240
		enemy_unit.current_stamina = 120
		enemy_unit.attribute_snapshot.set_value(&"hp_max", 220)
		enemy_unit.attribute_snapshot.set_value(&"mp_max", 240)
		enemy_unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 14)
		enemy_unit.known_skill_level_map[HEAVY_GROUND_SKILL_ID] = 5
		_add_unit_to_state(runtime, state, enemy_unit, true)


func _install_ground_skill_heavy_content(runtime) -> void:
	var skill_def = _build_heavy_ground_skill_def()
	var brain = _build_heavy_ground_brain()
	runtime._skill_defs[skill_def.skill_id] = skill_def
	runtime._enemy_ai_brains[brain.brain_id] = brain
	runtime._ai_service.setup(runtime._enemy_ai_brains, runtime._damage_resolver)


func _build_heavy_ground_skill_def():
	var skill_def = SKILL_DEF_SCRIPT.new()
	skill_def.skill_id = HEAVY_GROUND_SKILL_ID
	skill_def.display_name = "压测地毯轰击"
	skill_def.icon_id = HEAVY_GROUND_SKILL_ID
	skill_def.max_level = 5
	skill_def.combat_profile = COMBAT_SKILL_DEF_SCRIPT.new()
	skill_def.combat_profile.skill_id = skill_def.skill_id
	skill_def.combat_profile.target_mode = &"ground"
	skill_def.combat_profile.range_value = 99
	skill_def.combat_profile.area_pattern = &"single"
	skill_def.combat_profile.area_value = 0
	skill_def.combat_profile.ap_cost = 2
	skill_def.combat_profile.mp_cost = 0
	skill_def.combat_profile.stamina_cost = 0
	skill_def.combat_profile.cooldown_tu = 0
	skill_def.combat_profile.cast_variants.append(
		_build_heavy_ground_variant(&"single_probe", "单点试探", &"single", 1, 1, 3)
	)
	skill_def.combat_profile.cast_variants.append(
		_build_heavy_ground_variant(&"line_sweep", "双格扫线", &"line2", 2, 2, 4)
	)
	skill_def.combat_profile.cast_variants.append(
		_build_heavy_ground_variant(&"square_barrage", "四格轰炸", &"square2", 4, 3, 4)
	)
	return skill_def


func _build_heavy_ground_variant(
	variant_id: StringName,
	display_name: String,
	footprint_pattern: StringName,
	required_coord_count: int,
	min_skill_level: int,
	damage_power: int
):
	var cast_variant = COMBAT_CAST_VARIANT_DEF_SCRIPT.new()
	cast_variant.variant_id = variant_id
	cast_variant.display_name = display_name
	cast_variant.target_mode = &"ground"
	cast_variant.footprint_pattern = footprint_pattern
	cast_variant.required_coord_count = required_coord_count
	cast_variant.min_skill_level = min_skill_level
	cast_variant.effect_defs.append(_build_heavy_ground_damage_effect(damage_power))
	return cast_variant


func _build_heavy_ground_damage_effect(damage_power: int):
	var effect_def = COMBAT_EFFECT_DEF_SCRIPT.new()
	effect_def.effect_type = &"damage"
	effect_def.power = damage_power
	return effect_def


func _build_heavy_ground_brain():
	var ground_action = USE_GROUND_SKILL_ACTION_SCRIPT.new()
	ground_action.action_id = &"benchmark_ground_barrage"
	ground_action.score_bucket_id = &"benchmark_ground"
	ground_action.skill_ids.append(HEAVY_GROUND_SKILL_ID)
	ground_action.minimum_hit_count = 2
	ground_action.desired_min_distance = 0
	ground_action.desired_max_distance = 6
	ground_action.distance_reference = &"target_coord"

	var wait_action = WAIT_ACTION_SCRIPT.new()
	wait_action.action_id = &"benchmark_ground_wait"

	var pressure_state = ENEMY_AI_STATE_DEF_SCRIPT.new()
	pressure_state.state_id = &"pressure"
	pressure_state.actions = [ground_action, wait_action]

	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = HEAVY_GROUND_BRAIN_ID
	brain.default_state_id = &"pressure"
	brain.states = [pressure_state]
	return brain


func _build_manual_benchmark_unit(unit_id: StringName, display_name: String, coord: Vector2i) -> BattleUnitState:
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = &"player"
	unit.control_mode = &"manual"
	unit.current_hp = 260
	unit.current_mp = 120
	unit.current_stamina = 120
	unit.current_aura = 120
	unit.current_ap = 2
	unit.action_threshold = ACTION_THRESHOLD
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 260)
	unit.attribute_snapshot.set_value(&"mp_max", 120)
	unit.attribute_snapshot.set_value(&"stamina_max", 120)
	unit.attribute_snapshot.set_value(&"aura_max", 120)
	unit.attribute_snapshot.set_value(&"action_points", 2)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 18)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 12)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 24)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 20)
	return unit


func _build_ai_benchmark_unit(
	unit_id: StringName,
	display_name: String,
	coord: Vector2i,
	brain_id: StringName,
	skill_ids: Array[StringName]
) -> BattleUnitState:
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = &"enemy"
	unit.control_mode = &"ai"
	unit.ai_brain_id = brain_id
	unit.ai_state_id = &""
	unit.current_hp = 180
	unit.current_mp = 120
	unit.current_stamina = 120
	unit.current_aura = 120
	unit.current_ap = 2
	unit.action_threshold = ACTION_THRESHOLD
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 180)
	unit.attribute_snapshot.set_value(&"mp_max", 120)
	unit.attribute_snapshot.set_value(&"stamina_max", 120)
	unit.attribute_snapshot.set_value(&"aura_max", 120)
	unit.attribute_snapshot.set_value(&"action_points", 2)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 16)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 16)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 18)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 18)
	unit.known_active_skill_ids = skill_ids.duplicate()
	for skill_id in unit.known_active_skill_ids:
		unit.known_skill_level_map[skill_id] = 1
	return unit


func _add_unit_to_state(runtime, state, unit: BattleUnitState, is_enemy: bool) -> void:
	BattleRuntimeTestHelpers.register_unit_in_state(state, unit, is_enemy)
	var placed: bool = runtime._grid_service.place_unit(state, unit, unit.coord, true)
	if not placed:
		_test.fail("Benchmark unit %s could not be placed on the benchmark map." % String(unit.unit_id))


func _resolve_selected_coord(state) -> Vector2i:
	if state == null:
		return Vector2i.ZERO
	var active_unit = state.units.get(state.active_unit_id) as BattleUnitState
	if active_unit != null:
		return active_unit.coord
	return Vector2i.ZERO


func _count_living_units(state, unit_ids: Array[StringName]) -> int:
	if state == null:
		return 0
	var count := 0
	for unit_id in unit_ids:
		var unit_state = state.units.get(unit_id) as BattleUnitState
		if unit_state != null and unit_state.is_alive:
			count += 1
	return count


func _assert_consistent_outcome(scenario_id: StringName, logic_only: Dictionary, logic_plus_hud: Dictionary) -> void:
	var fields_to_compare := [
		"battle_ended",
		"phase",
		"final_tu",
		"ally_alive",
		"enemy_alive",
		"winner_faction_id",
		"resolution_winner_faction_id",
		"resolution_loot_count",
		"timeline_steps",
		"ai_turns",
		"manual_turns",
	]
	for field_name_variant in fields_to_compare:
		var field_name := String(field_name_variant)
		if logic_only.get(field_name) == logic_plus_hud.get(field_name):
			continue
		_test.fail(
			"Benchmark scenario %s produced divergent battle outcomes between logic_only and logic_plus_hud_snapshot for %s. logic_only=%s logic_plus_hud=%s" % [
				String(scenario_id),
				field_name,
				str(logic_only.get(field_name)),
				str(logic_plus_hud.get(field_name)),
			]
		)


func _format_result(result: Dictionary) -> String:
	return "[Battle6v40Benchmark] scenario=%s pass=%s target_tu=%d final_tu=%d phase=%s battle_ended=%s total_ms=%.3f logic_ms=%.3f hud_ms=%.3f timeline_steps=%d ai_turns=%d manual_turns=%d max_ready=%d ally_alive=%d enemy_alive=%d iterations=%d" % [
		String(result.get("scenario_id", "")),
		String(result.get("pass_id", "")),
		int(result.get("target_tu", DEFAULT_TARGET_TU)),
		int(result.get("final_tu", 0)),
		String(result.get("phase", "")),
		str(bool(result.get("battle_ended", false))),
		_usec_to_msec(float(result.get("total_usec", 0.0))),
		_usec_to_msec(float(result.get("logic_usec", 0.0))),
		_usec_to_msec(float(result.get("hud_usec", 0.0))),
		int(result.get("timeline_steps", 0)),
		int(result.get("ai_turns", 0)),
		int(result.get("manual_turns", 0)),
		int(result.get("max_ready_queue", 0)),
		int(result.get("ally_alive", 0)),
		int(result.get("enemy_alive", 0)),
		int(result.get("iterations", 0)),
	]


func _format_comparison(scenario_id: StringName, logic_only: Dictionary, logic_plus_hud: Dictionary) -> String:
	var logic_total := float(logic_only.get("total_usec", 0.0))
	var hud_total := float(logic_plus_hud.get("total_usec", 0.0))
	var additional_hud_usec := maxi(int(hud_total - logic_total), 0)
	var target_tu_divisor := maxi(int(logic_only.get("target_tu", DEFAULT_TARGET_TU)), 1)
	return "[Battle6v40Benchmark] scenario=%s comparison additional_hud_ms=%.3f logic_ms_per_tu=%.3f logic_plus_hud_ms_per_tu=%.3f" % [
		String(scenario_id),
		_usec_to_msec(additional_hud_usec),
		_usec_to_msec(logic_total) / float(target_tu_divisor),
		_usec_to_msec(hud_total) / float(target_tu_divisor),
	]


func _resolve_target_tu() -> int:
	var raw_target_tu := OS.get_environment("BENCHMARK_TARGET_TU")
	if raw_target_tu.is_empty():
		return DEFAULT_TARGET_TU
	var parsed_target_tu := int(raw_target_tu)
	return maxi(parsed_target_tu, 1)


func _usec_to_msec(value_usec: float) -> float:
	return value_usec / 1000.0
