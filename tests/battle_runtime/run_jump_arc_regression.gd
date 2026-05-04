## 文件说明：跳跃抛物线判定 BattleGridService.can_jump_arc 的回归测试。
## 审查重点：覆盖平地跳、爬高、跳崖、越障成功/失败、落点占用、超距、体型加成等场景。
## 备注：与 warrior_jump_slash.tres 的字段保持一致，调整公式时同步更新数值预期。

extends SceneTree

const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleGridService = preload("res://scripts/systems/battle/terrain/battle_grid_service.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

var _failures: Array[String] = []
var _grid_service: BattleGridService = BattleGridService.new()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_flat_jump_succeeds_within_range()
	_test_jump_up_a_step_succeeds()
	_test_jump_down_cliff_succeeds()
	_test_jump_clears_low_obstacle()
	_test_jump_blocked_by_tall_obstacle()
	_test_jump_landing_on_occupied_cell_fails()
	_test_jump_beyond_max_range_fails()
	_test_jump_blocked_by_friendly_in_path()
	_test_small_unit_gets_agility_bonus()
	_test_huge_unit_takes_size_penalty()
	_test_short_jump_clears_taller_obstacle_via_redistribution()
	_test_zero_distance_target_rejected()

	if _failures.is_empty():
		print("Jump arc regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Jump arc regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_flat_jump_succeeds_within_range() -> void:
	var state := _build_flat_state(Vector2i(8, 4))
	var unit := _build_jumper(Vector2i(1, 1), 12, BattleUnitState.BODY_SIZE_MEDIUM)
	state.units[unit.unit_id] = unit
	_register_unit_on_cells(state, unit)
	var effect := _build_jump_effect()
	var ok := _grid_service.can_jump_arc(state, unit, Vector2i(3, 1), effect)
	_assert_true(ok, "平地跳 (1,1)->(3,1) 距离 2 弧高足够，应该成功。")


func _test_jump_up_a_step_succeeds() -> void:
	var state := _build_flat_state(Vector2i(8, 4))
	_set_cell_height(state, Vector2i(3, 1), 2)
	var unit := _build_jumper(Vector2i(1, 1), 12, BattleUnitState.BODY_SIZE_MEDIUM)
	state.units[unit.unit_id] = unit
	_register_unit_on_cells(state, unit)
	var effect := _build_jump_effect()
	var ok := _grid_service.can_jump_arc(state, unit, Vector2i(3, 1), effect)
	_assert_true(ok, "跳上高 2 的台阶，弧高足够，应该成功。")


func _test_jump_down_cliff_succeeds() -> void:
	var state := _build_flat_state(Vector2i(8, 4))
	_set_cell_height(state, Vector2i(1, 1), 4)
	var unit := _build_jumper(Vector2i(1, 1), 12, BattleUnitState.BODY_SIZE_MEDIUM)
	state.units[unit.unit_id] = unit
	_register_unit_on_cells(state, unit)
	var effect := _build_jump_effect()
	var ok := _grid_service.can_jump_arc(state, unit, Vector2i(3, 1), effect)
	_assert_true(ok, "从高 4 跳下到地面，应该成功。")


func _test_jump_clears_low_obstacle() -> void:
	var state := _build_flat_state(Vector2i(8, 4))
	_set_cell_height(state, Vector2i(2, 1), 1)
	var unit := _build_jumper(Vector2i(1, 1), 12, BattleUnitState.BODY_SIZE_MEDIUM)
	state.units[unit.unit_id] = unit
	_register_unit_on_cells(state, unit)
	var effect := _build_jump_effect()
	var ok := _grid_service.can_jump_arc(state, unit, Vector2i(3, 1), effect)
	_assert_true(ok, "跳过中间高 1 的低障碍，弧顶应足够。")


func _test_jump_blocked_by_tall_obstacle() -> void:
	var state := _build_flat_state(Vector2i(8, 4))
	_set_cell_height(state, Vector2i(2, 1), 8)
	var unit := _build_jumper(Vector2i(1, 1), 12, BattleUnitState.BODY_SIZE_MEDIUM)
	state.units[unit.unit_id] = unit
	_register_unit_on_cells(state, unit)
	var effect := _build_jump_effect()
	var ok := _grid_service.can_jump_arc(state, unit, Vector2i(3, 1), effect)
	_assert_true(not ok, "中间格高 8 远超弧顶，应该撞墙失败。")


func _test_jump_landing_on_occupied_cell_fails() -> void:
	var state := _build_flat_state(Vector2i(8, 4))
	var unit := _build_jumper(Vector2i(1, 1), 12, BattleUnitState.BODY_SIZE_MEDIUM)
	state.units[unit.unit_id] = unit
	_register_unit_on_cells(state, unit)
	var blocker := _build_jumper(Vector2i(3, 1), 8, BattleUnitState.BODY_SIZE_MEDIUM)
	blocker.unit_id = &"blocker"
	state.units[blocker.unit_id] = blocker
	_register_unit_on_cells(state, blocker)
	var effect := _build_jump_effect()
	var ok := _grid_service.can_jump_arc(state, unit, Vector2i(3, 1), effect)
	_assert_true(not ok, "落点已被其他单位占用，应该失败。")


func _test_jump_beyond_max_range_fails() -> void:
	var state := _build_flat_state(Vector2i(12, 4))
	# STR=4 + medium 体型 → budget=3.2, range_budget=1.92, max_range=2
	var unit := _build_jumper(Vector2i(1, 1), 4, BattleUnitState.BODY_SIZE_MEDIUM)
	state.units[unit.unit_id] = unit
	_register_unit_on_cells(state, unit)
	var effect := _build_jump_effect()
	var ok := _grid_service.can_jump_arc(state, unit, Vector2i(8, 1), effect)
	_assert_true(not ok, "目标距离 7 远超 max_range，应该失败。")


func _test_jump_blocked_by_friendly_in_path() -> void:
	var state := _build_flat_state(Vector2i(8, 4))
	var unit := _build_jumper(Vector2i(1, 1), 12, BattleUnitState.BODY_SIZE_MEDIUM)
	state.units[unit.unit_id] = unit
	_register_unit_on_cells(state, unit)
	# 在路径中间的高地放一个友军，叠加 presence_height 后净空不够
	_set_cell_height(state, Vector2i(2, 1), 3)
	var ally := _build_jumper(Vector2i(2, 1), 8, BattleUnitState.BODY_SIZE_MEDIUM)
	ally.unit_id = &"ally"
	ally.faction_id = unit.faction_id
	state.units[ally.unit_id] = ally
	_register_unit_on_cells(state, ally)
	var effect := _build_jump_effect()
	var ok := _grid_service.can_jump_arc(state, unit, Vector2i(3, 1), effect)
	_assert_true(not ok, "友军站在 3 格高地上 (含 presence_height=4)，应被阻挡。")


func _test_small_unit_gets_agility_bonus() -> void:
	var state := _build_flat_state(Vector2i(12, 4))
	# STR=4 + small 体型 → +1 modifier → effective 5
	var unit := _build_jumper(Vector2i(1, 1), 4, BattleUnitState.BODY_SIZE_SMALL)
	state.units[unit.unit_id] = unit
	_register_unit_on_cells(state, unit)
	var effect := _build_jump_effect()
	# small 加成后 budget=3.5, range_budget=2.1, max_range≈2
	var ok := _grid_service.can_jump_arc(state, unit, Vector2i(3, 1), effect)
	_assert_true(ok, "小体型 +1 STR 加成应让 STR=4 角色跳出 2 格。")


func _test_huge_unit_takes_size_penalty() -> void:
	var state := _build_flat_state(Vector2i(12, 4))
	# STR=12 + huge 体型 → -10 modifier → effective 2
	var unit := _build_jumper(Vector2i(1, 1), 12, BattleUnitState.BODY_SIZE_HUGE)
	state.units[unit.unit_id] = unit
	_register_unit_on_cells(state, unit)
	var effect := _build_jump_effect()
	# huge 惩罚后 budget=2.6, range_budget=1.56, max_range≈2
	var ok := _grid_service.can_jump_arc(state, unit, Vector2i(8, 1), effect)
	_assert_true(not ok, "Huge 体型扣 10 STR 后远距离应失败。")


func _test_short_jump_clears_taller_obstacle_via_redistribution() -> void:
	var state := _build_flat_state(Vector2i(8, 4))
	_set_cell_height(state, Vector2i(2, 1), 3)
	var unit := _build_jumper(Vector2i(1, 1), 22, BattleUnitState.BODY_SIZE_LARGE)
	state.units[unit.unit_id] = unit
	_register_unit_on_cells(state, unit)
	var effect := _build_jump_effect()
	# Large 体型 STR 22-6=16, budget=6.8。短跳红利让弧高更高。
	var ok := _grid_service.can_jump_arc(state, unit, Vector2i(3, 1), effect)
	_assert_true(ok, "短距离 (距离 2) 跳跃配合短跳红利应能跨过高 3 障碍。")


func _test_zero_distance_target_rejected() -> void:
	var state := _build_flat_state(Vector2i(8, 4))
	var unit := _build_jumper(Vector2i(1, 1), 12, BattleUnitState.BODY_SIZE_MEDIUM)
	state.units[unit.unit_id] = unit
	_register_unit_on_cells(state, unit)
	var effect := _build_jump_effect()
	var ok := _grid_service.can_jump_arc(state, unit, unit.coord, effect)
	_assert_true(not ok, "目标即为起点（距离 0）应该被拒绝。")


func _build_flat_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.map_size = map_size
	state.cells = {}
	for y in range(map_size.y):
		for x in range(map_size.x):
			var cell := BattleCellState.new()
			cell.coord = Vector2i(x, y)
			cell.base_terrain = BattleCellState.TERRAIN_LAND
			cell.base_height = 0
			cell.height_offset = 0
			cell.recalculate_runtime_values()
			state.cells[cell.coord] = cell
	return state


func _set_cell_height(state: BattleState, coord: Vector2i, height: int) -> void:
	var cell := state.cells.get(coord) as BattleCellState
	if cell == null:
		return
	cell.base_height = height
	cell.recalculate_runtime_values()


func _build_jumper(coord: Vector2i, strength: int, body_size: int) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = StringName("jumper_%d_%d" % [coord.x, coord.y])
	unit.display_name = String(unit.unit_id)
	unit.faction_id = &"player"
	unit.body_size = body_size
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"strength", strength)
	unit.is_alive = true
	return unit


func _register_unit_on_cells(state: BattleState, unit: BattleUnitState) -> void:
	for coord in unit.occupied_coords:
		var cell := state.cells.get(coord) as BattleCellState
		if cell != null:
			cell.occupant_unit_id = unit.unit_id


func _build_jump_effect() -> CombatEffectDef:
	var effect := CombatEffectDef.new()
	effect.effect_type = &"forced_move"
	effect.forced_move_mode = &"jump"
	effect.forced_move_distance = 4
	effect.jump_base_budget = 2
	effect.jump_str_scale = 0.3
	effect.jump_arc_ratio = 0.4
	effect.jump_range_multiplier = 1
	return effect


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)
