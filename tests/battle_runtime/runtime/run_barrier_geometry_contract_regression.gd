extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleGridService = preload("res://scripts/systems/battle/terrain/battle_grid_service.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_geometry_service_exists()
	_test_footprint_transition_detects_large_unit_boundary_crossing()
	_test_footprint_transition_does_not_block_inside_to_inside()
	_test_projected_line_contract()
	_test.finish(self, "Barrier geometry contract regression")


func _test_geometry_service_exists() -> void:
	_new_geometry_service()


func _test_footprint_transition_detects_large_unit_boundary_crossing() -> void:
	var geometry = _new_geometry_service()
	if geometry == null or not _assert_has_method(geometry, "classify_footprint_transition", "BattleBarrierGeometryService must expose classify_footprint_transition(state, from_footprint, to_footprint, barrier_coords)."):
		return
	var state := _build_state(Vector2i(8, 6))
	var barrier_coords := _diamond_area(Vector2i(2, 2), 2)
	var from_footprint := _footprint(Vector2i(5, 1), Vector2i(2, 2))
	var to_footprint := _footprint(Vector2i(4, 1), Vector2i(2, 2))
	var transition: Dictionary = geometry.classify_footprint_transition(state, from_footprint, to_footprint, barrier_coords)
	_assert_true(bool(transition.get("crosses_boundary", false)), "Large footprint crossing must trigger even when anchor-only checks would miss it.")
	_assert_true(not bool(transition.get("from_inside", false)), "The large unit source footprint starts outside the barrier.")
	_assert_true(bool(transition.get("to_inside", false)), "The large unit destination footprint overlaps the barrier.")


func _test_footprint_transition_does_not_block_inside_to_inside() -> void:
	var geometry = _new_geometry_service()
	if geometry == null or not geometry.has_method("classify_footprint_transition"):
		return
	var state := _build_state(Vector2i(8, 6))
	var barrier_coords := _diamond_area(Vector2i(2, 2), 2)
	var from_footprint := _footprint(Vector2i(2, 2), Vector2i(1, 1))
	var to_footprint := _footprint(Vector2i(3, 2), Vector2i(1, 1))
	var transition: Dictionary = geometry.classify_footprint_transition(state, from_footprint, to_footprint, barrier_coords)
	_assert_true(not bool(transition.get("crosses_boundary", true)), "Inside-to-inside footprint transitions must not trigger barrier passage.")
	_assert_true(bool(transition.get("from_inside", false)), "Source footprint must be classified inside.")
	_assert_true(bool(transition.get("to_inside", false)), "Destination footprint must be classified inside.")


func _test_projected_line_contract() -> void:
	var geometry = _new_geometry_service()
	if geometry == null or not _assert_has_method(geometry, "line_crosses_barrier_area", "BattleBarrierGeometryService must expose line_crosses_barrier_area(state, source_coord, target_coord, barrier_coords)."):
		return
	var state := _build_state(Vector2i(8, 6))
	var barrier_coords := _diamond_area(Vector2i(2, 2), 2)
	_assert_true(
		geometry.line_crosses_barrier_area(state, Vector2i(5, 2), Vector2i(-1, 2), barrier_coords),
		"Outside-to-outside projected lines that pass through the barrier must be blocked."
	)
	_assert_true(
		not geometry.line_crosses_barrier_area(state, Vector2i(5, 4), Vector2i(6, 4), barrier_coords),
		"Outside-to-outside projected lines that miss the barrier must not be blocked."
	)
	_assert_true(
		not geometry.line_crosses_barrier_area(state, Vector2i(2, 2), Vector2i(3, 2), barrier_coords),
		"Inside-to-inside projected lines must not be blocked by the boundary."
	)


func _new_geometry_service():
	var geometry_path := "res://scripts/systems/battle/runtime/battle_barrier_geometry_service.gd"
	if not FileAccess.file_exists(geometry_path):
		_failures.append("BattleBarrierGeometryService script is missing.")
		return null
	var geometry_script = load(geometry_path)
	if geometry_script == null:
		_failures.append("BattleBarrierGeometryService script is missing.")
		return null
	return geometry_script.new()


func _build_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"barrier_geometry_contract"
	state.map_size = map_size
	for y in range(map_size.y):
		for x in range(map_size.x):
			var cell := BattleCellState.new()
			cell.coord = Vector2i(x, y)
			cell.base_terrain = BattleCellState.TERRAIN_LAND
			cell.base_height = 4
			cell.recalculate_runtime_values()
			state.cells[cell.coord] = cell
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)
	return state


func _footprint(anchor: Vector2i, size: Vector2i) -> Array[Vector2i]:
	return BattleGridService.new().get_footprint_coords(anchor, size)


func _diamond_area(center: Vector2i, radius: int) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for y in range(center.y - radius, center.y + radius + 1):
		for x in range(center.x - radius, center.x + radius + 1):
			var coord := Vector2i(x, y)
			if absi(coord.x - center.x) + absi(coord.y - center.y) <= radius:
				coords.append(coord)
	return coords


func _assert_has_method(object, method_name: String, message: String) -> bool:
	if object == null or not object.has_method(method_name):
		_failures.append(message)
		return false
	return true


func _assert_true(condition: bool, message: String) -> void:
	_test.assert_true(condition, message)
