extends SceneTree

const BATTLE_RUNTIME_MODULE_SCRIPT = preload("res://scripts/systems/battle/runtime/battle_runtime_module.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_wide_map_uses_top_and_bottom_long_edges()
	_test_tall_map_uses_left_and_right_long_edges()
	_test_spawn_placement_does_not_clear_existing_occupants_from_stale_coords()
	_test_failed_spawn_placement_rolls_back_partial_units()

	if _failures.is_empty():
		print("Battle spawn side regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Battle spawn side regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_wide_map_uses_top_and_bottom_long_edges() -> void:
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	var state = _build_flat_state(Vector2i(8, 4))
	runtime._state = state

	var ally_units := [
		_build_unit(&"ally_0"),
		_build_unit(&"ally_1"),
	]
	var enemy_units := [
		_build_unit(&"enemy_0"),
		_build_unit(&"enemy_1"),
	]

	var allies_placed := runtime._place_units(ally_units, [Vector2i(6, 3), Vector2i(7, 3)], true, BATTLE_RUNTIME_MODULE_SCRIPT.SPAWN_SIDE_NEAR_LONG_EDGE)
	var enemies_placed := runtime._place_units(enemy_units, [Vector2i(0, 0), Vector2i(1, 0)], false, BATTLE_RUNTIME_MODULE_SCRIPT.SPAWN_SIDE_FAR_LONG_EDGE)

	_assert_true(allies_placed, "宽图近长边约束应仍能放下所有友军。")
	_assert_true(enemies_placed, "宽图远长边约束应仍能放下所有敌军。")
	for unit in ally_units:
		_assert_true(unit.coord.y < 2, "宽图近长边应是上半边，不是固定左侧：%s coord=%s" % [String(unit.unit_id), str(unit.coord)])
	for unit in enemy_units:
		_assert_true(unit.coord.y >= 2, "宽图远长边应是下半边，不是固定右侧：%s coord=%s" % [String(unit.unit_id), str(unit.coord)])


func _test_tall_map_uses_left_and_right_long_edges() -> void:
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	var state = _build_flat_state(Vector2i(4, 8))
	runtime._state = state

	var ally_units := [_build_unit(&"tall_ally")]
	var enemy_units := [_build_unit(&"tall_enemy")]
	var allies_placed := runtime._place_units(ally_units, [Vector2i(3, 6)], true, BATTLE_RUNTIME_MODULE_SCRIPT.SPAWN_SIDE_NEAR_LONG_EDGE)
	var enemies_placed := runtime._place_units(enemy_units, [Vector2i(0, 1)], false, BATTLE_RUNTIME_MODULE_SCRIPT.SPAWN_SIDE_FAR_LONG_EDGE)

	_assert_true(allies_placed, "竖图近长边约束应能放下友军。")
	_assert_true(enemies_placed, "竖图远长边约束应能放下敌军。")
	_assert_true(ally_units[0].coord.x < 2, "竖图近长边应是左半边：coord=%s" % str(ally_units[0].coord))
	_assert_true(enemy_units[0].coord.x >= 2, "竖图远长边应是右半边：coord=%s" % str(enemy_units[0].coord))


func _test_spawn_placement_does_not_clear_existing_occupants_from_stale_coords() -> void:
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	var state = _build_flat_state(Vector2i(4, 4))
	runtime._state = state

	var first_unit = _build_unit(&"first_unit")
	var second_unit = _build_unit(&"second_unit")
	var placed := runtime._place_units([first_unit, second_unit], [Vector2i(0, 0), Vector2i(1, 0)], true)

	_assert_true(placed, "开战 placement 应能连续放下初始坐标相同的单位。")
	_assert_eq(state.ally_unit_ids.size(), 2, "成功 placement 后两个单位都应进入 ally ids。")
	_assert_true(first_unit.coord != second_unit.coord, "两个单位不应重叠：first=%s second=%s" % [str(first_unit.coord), str(second_unit.coord)])
	var first_cell = state.cells.get(first_unit.coord) as BATTLE_CELL_STATE_SCRIPT
	var second_cell = state.cells.get(second_unit.coord) as BATTLE_CELL_STATE_SCRIPT
	_assert_true(first_cell != null, "第一个单位坐标应有 cell。")
	_assert_true(second_cell != null, "第二个单位坐标应有 cell。")
	if first_cell != null:
		_assert_eq(first_cell.occupant_unit_id, first_unit.unit_id, "第二个单位 placement 不应清掉第一个单位的占用。")
	if second_cell != null:
		_assert_eq(second_cell.occupant_unit_id, second_unit.unit_id, "第二个单位应写入自己的占用。")


func _test_failed_spawn_placement_rolls_back_partial_units() -> void:
	var runtime = BATTLE_RUNTIME_MODULE_SCRIPT.new()
	var state = _build_flat_state(Vector2i(1, 1))
	runtime._state = state

	var first_unit = _build_unit(&"rollback_first")
	var second_unit = _build_unit(&"rollback_second")
	var placed := runtime._place_units([first_unit, second_unit], [Vector2i(0, 0)], true)

	_assert_true(not placed, "地图没有足够出生空间时 placement 应失败。")
	_assert_eq(state.ally_unit_ids.size(), 0, "失败 placement 不应留下部分 ally ids。")
	_assert_eq(state.units.size(), 0, "失败 placement 不应留下部分 units。")
	var only_cell = state.cells.get(Vector2i(0, 0)) as BATTLE_CELL_STATE_SCRIPT
	_assert_true(only_cell != null, "回滚测试 cell 应存在。")
	if only_cell != null:
		_assert_eq(only_cell.occupant_unit_id, &"", "失败 placement 不应留下部分占用。")


func _build_flat_state(map_size: Vector2i):
	var state = BATTLE_STATE_SCRIPT.new()
	state.battle_id = &"battle_spawn_side_regression"
	state.phase = &"timeline_running"
	state.map_size = map_size
	state.timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
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


func _build_unit(unit_id: StringName):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = String(unit_id)
	unit.faction_id = &"player"
	unit.control_mode = &"ai"
	unit.current_hp = 30
	unit.current_stamina = 10
	unit.current_ap = 2
	unit.current_move_points = BATTLE_UNIT_STATE_SCRIPT.DEFAULT_MOVE_POINTS_PER_TURN
	unit.is_alive = true
	unit.set_anchor_coord(Vector2i.ZERO)
	unit.attribute_snapshot.set_value(&"hp_max", 30)
	unit.attribute_snapshot.set_value(&"action_points", 2)
	return unit


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
