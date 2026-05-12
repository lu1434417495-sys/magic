extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var runtime := BattleRuntimeModule.new()
	runtime.setup(null, {}, {}, {})
	var state := _build_state(Vector2i(4, 2))
	var unit := _build_unit(&"mover", Vector2i(0, 0))
	state.units = {unit.unit_id: unit}
	state.ally_unit_ids = [unit.unit_id]
	state.active_unit_id = unit.unit_id
	_assert_true(runtime._grid_service.place_unit(state, unit, unit.coord, true), "terrain lifetime 测试单位应能放入战场。")
	runtime._state = state
	var batch := BattleEventBatch.new()

	var core_crater := _build_terrain_effect(&"meteor_swarm_crater_core", 3, &"battle", 0, 0)
	var rubble := _build_terrain_effect(&"meteor_swarm_rubble", 2, &"battle", 0, 0)
	var dust := _build_terrain_effect(&"meteor_swarm_dust", 1, &"timed", 50, 5)
	_assert_true(runtime._terrain_effect_system.upsert_timed_terrain_effect(Vector2i(1, 0), unit, null, core_crater, &"core_crater_1"), "battle lifetime crater 应能写入 timed_terrain_effects。")
	_assert_true(runtime._terrain_effect_system.upsert_timed_terrain_effect(Vector2i(1, 0), unit, null, rubble, &"rubble_1"), "battle lifetime rubble 应能写入 timed_terrain_effects。")
	_assert_true(runtime._terrain_effect_system.upsert_timed_terrain_effect(Vector2i(2, 0), unit, null, dust, &"dust_1"), "timed dust 应能写入 timed_terrain_effects。")

	_assert_eq(
		runtime._terrain_effect_system.get_move_cost_delta_for_unit_target(unit, Vector2i(1, 0)),
		3,
		"crater + rubble 移动成本应按 max stacking，不能叠成 5。"
	)
	state.timeline.current_tu = 55
	runtime._terrain_effect_system.process_timed_terrain_effects(batch)
	var crater_cell := state.cells.get(Vector2i(1, 0)) as BattleCellState
	var dust_cell := state.cells.get(Vector2i(2, 0)) as BattleCellState
	_assert_eq(crater_cell.timed_terrain_effects.size() if crater_cell != null else -1, 2, "battle lifetime crater/rubble 推进 55 TU 后仍应存在。")
	_assert_eq(dust_cell.timed_terrain_effects.size() if dust_cell != null else -1, 0, "timed dust 到期后应消失。")
	_assert_eq(
		runtime._terrain_effect_system.get_move_cost_delta_for_unit_target(unit, Vector2i(1, 0)),
		3,
		"battle lifetime terrain 推进后仍应影响移动成本。"
	)

	if _failures.is_empty():
		print("Battle terrain lifetime regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle terrain lifetime regression: FAIL (%d)" % _failures.size())
	quit(1)


func _build_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.map_size = map_size
	for y in range(map_size.y):
		for x in range(map_size.x):
			var coord := Vector2i(x, y)
			var cell := BattleCellState.new()
			cell.coord = coord
			cell.passable = true
			state.cells[coord] = cell
	return state


func _build_unit(unit_id: StringName, coord: Vector2i) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = &"player"
	unit.coord = coord
	unit.is_alive = true
	unit.refresh_footprint()
	return unit


func _build_terrain_effect(
	effect_id: StringName,
	move_cost_delta: int,
	lifetime_policy: StringName,
	duration_tu: int,
	tick_interval_tu: int
) -> CombatEffectDef:
	var effect := CombatEffectDef.new()
	effect.effect_type = &"terrain_effect"
	effect.tick_effect_type = &"none"
	effect.terrain_effect_id = effect_id
	effect.duration_tu = duration_tu
	effect.tick_interval_tu = tick_interval_tu
	effect.effect_target_team_filter = &"any"
	effect.params = {
		"lifetime_policy": lifetime_policy,
		"move_cost_delta": move_cost_delta,
		"display_name": String(effect_id),
		"render_overlay_id": String(effect_id),
	}
	return effect


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_test.fail("%s actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)
