## Guards the correctness invariants that the A* optimization in
## `battle_grid_service.gd::resolve_unit_move_path` depends on. If any of these
## fail, the Manhattan heuristic may stop being admissible and AI / player
## movement can silently start choosing sub-optimal paths.
##
## Invariants enforced here:
##   1. `get_unit_move_cost` returns >= 1 for every base terrain id (so each step
##      contributes at least 1 to the true path cost — the floor Manhattan
##      heuristic relies on).
##   2. On a featureless map, A* returns cost == Manhattan distance (no detour).
##   3. On a map seeded with mixed-cost terrain, A* matches a naive reference
##      Dijkstra implementation embedded in this test.
##   4. Randomized differential test: A* and the reference agree across multiple
##      random maps with sprinkled mud cells.
extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const BattleGridService = preload("res://scripts/systems/battle/terrain/battle_grid_service.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

const ALL_TERRAINS: Array[StringName] = [
	&"land",
	&"forest",
	&"water",
	&"shallow_water",
	&"flowing_water",
	&"deep_water",
	&"mud",
	&"spike",
]

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures
var _grid: BattleGridService


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_grid = BattleGridService.new()

	_test_step_cost_floor()
	_test_a_star_simple_optimality()
	_test_a_star_matches_reference_with_mud_stripe()
	_test_a_star_matches_reference_randomized()
	_test_path_tree_matches_reference_with_mud_stripe()
	_test_path_tree_respects_occupant_blocks()

	if _failures.is_empty():
		print("Battle grid service pathfinding invariants: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Battle grid service pathfinding invariants: FAIL (%d)" % _failures.size())
	quit(1)


# Invariant 1: Manhattan heuristic relies on step_cost >= 1 for every reachable cell.
# If a future terrain returns cost < 1, the A* optimization will start picking
# sub-optimal paths silently.
func _test_step_cost_floor() -> void:
	var state := _build_state(Vector2i(3, 3))
	var unit := _build_unit(Vector2i(0, 0))
	for terrain_id in ALL_TERRAINS:
		var cell := state.cells.get(Vector2i(2, 2)) as BattleCellState
		if cell == null:
			_test.fail("setup error: cell (2,2) missing while seeding terrain %s." % String(terrain_id))
			continue
		cell.base_terrain = terrain_id
		var cost := _grid.get_unit_move_cost(state, unit, Vector2i(2, 2))
		if cost < 1:
			_test.fail("get_unit_move_cost returned %d for terrain=%s. Manhattan A* heuristic requires step_cost >= 1." % [
				cost, String(terrain_id),
			])


# Invariant 2: On a flat, fully passable map, A* returns Manhattan distance.
# Anything else means the heuristic is over-estimating or the search prunes a
# legal direct path.
func _test_a_star_simple_optimality() -> void:
	var state := _build_state(Vector2i(8, 8))
	var unit := _build_unit(Vector2i(0, 0))
	state.units[unit.unit_id] = unit
	if not _grid.place_unit(state, unit, unit.coord, true):
		_test.fail("simple optimality: failed to place unit at origin.")
		return
	var to_coord := Vector2i(5, 3)
	var result := _grid.resolve_unit_move_path(state, unit, unit.coord, to_coord, 99)
	if not bool(result.get("allowed", false)):
		_test.fail("simple optimality: expected straight path to be allowed, got %s." % str(result))
		return
	var expected := absi(to_coord.x - unit.coord.x) + absi(to_coord.y - unit.coord.y)
	if int(result.get("cost", -1)) != expected:
		_test.fail("simple optimality: expected cost=%d (Manhattan), got %d." % [
			expected, int(result.get("cost", -1)),
		])


# Invariant 3: Mixed-cost terrain. A* must match the embedded reference
# Dijkstra (which has no heuristic and is the simplest correct implementation).
func _test_a_star_matches_reference_with_mud_stripe() -> void:
	var state := _build_state(Vector2i(5, 5))
	var unit := _build_unit(Vector2i(0, 0))
	state.units[unit.unit_id] = unit
	if not _grid.place_unit(state, unit, unit.coord, true):
		_test.fail("mud stripe: failed to place unit at origin.")
		return
	# A horizontal stripe of mud (cost 2) at y=1 forcing the optimizer to choose
	# between going around or paying the extra cost.
	for x in range(5):
		var cell := state.cells.get(Vector2i(x, 1)) as BattleCellState
		if cell != null:
			cell.base_terrain = &"mud"
	var to_coord := Vector2i(4, 4)
	var a_star := _grid.resolve_unit_move_path(state, unit, unit.coord, to_coord, 99)
	var reference_cost := _reference_dijkstra_cost(state, unit, unit.coord, to_coord)
	if not bool(a_star.get("allowed", false)):
		_test.fail("mud stripe: expected destination to be reachable.")
		return
	if int(a_star.get("cost", -1)) != reference_cost:
		_test.fail("mud stripe: A* cost=%d does not match reference Dijkstra cost=%d." % [
			int(a_star.get("cost", -1)), reference_cost,
		])


# Invariant 4: Differential test against a reference Dijkstra on randomized maps.
# Catches subtle heuristic / tie-break bugs that handcrafted scenarios miss.
func _test_a_star_matches_reference_randomized() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = 0xA11C0DE
	var trials_run := 0
	for trial in range(12):
		var size := Vector2i(rng.randi_range(4, 8), rng.randi_range(4, 8))
		var state := _build_state(size)
		var unit := _build_unit(Vector2i(0, 0))
		state.units[unit.unit_id] = unit
		if not _grid.place_unit(state, unit, unit.coord, true):
			continue
		var mud_count := rng.randi_range(0, (size.x * size.y) / 4)
		for _i in range(mud_count):
			var mx := rng.randi_range(0, size.x - 1)
			var my := rng.randi_range(0, size.y - 1)
			var mud_coord := Vector2i(mx, my)
			if mud_coord == unit.coord:
				continue
			var cell := state.cells.get(mud_coord) as BattleCellState
			if cell != null:
				cell.base_terrain = &"mud"
		var dest := Vector2i(rng.randi_range(0, size.x - 1), rng.randi_range(0, size.y - 1))
		if dest == unit.coord:
			continue
		var reference_cost := _reference_dijkstra_cost(state, unit, unit.coord, dest)
		var a_star := _grid.resolve_unit_move_path(state, unit, unit.coord, dest, 9999)
		if reference_cost < 0:
			if bool(a_star.get("allowed", false)):
				_test.fail("randomized trial %d: reference says unreachable but A* allows path." % trial)
			continue
		if not bool(a_star.get("allowed", false)):
			_test.fail("randomized trial %d size=%s dest=%s: A* refused a path reference reaches at cost %d." % [
				trial, str(size), str(dest), reference_cost,
			])
			continue
		var a_star_cost := int(a_star.get("cost", -1))
		if a_star_cost != reference_cost:
			_test.fail("randomized trial %d size=%s dest=%s: A* cost=%d != reference=%d." % [
				trial, str(size), str(dest), a_star_cost, reference_cost,
			])
		trials_run += 1
	if trials_run == 0:
		_test.fail("randomized differential test ran 0 trials — RNG/setup degenerate, invariant unverified.")


func _test_path_tree_matches_reference_with_mud_stripe() -> void:
	var state := _build_state(Vector2i(5, 5))
	var unit := _build_unit(Vector2i(0, 0))
	state.units[unit.unit_id] = unit
	if not _grid.place_unit(state, unit, unit.coord, true):
		_test.fail("path tree mud stripe: failed to place unit at origin.")
		return
	for x in range(5):
		var cell := state.cells.get(Vector2i(x, 1)) as BattleCellState
		if cell != null:
			cell.base_terrain = &"mud"
	var tree := _grid.build_unit_move_path_tree(state, unit, unit.coord, 99)
	var costs: Dictionary = tree.get("costs", {})
	for y in range(state.map_size.y):
		for x in range(state.map_size.x):
			var dest := Vector2i(x, y)
			var reference_cost := _reference_dijkstra_cost(state, unit, unit.coord, dest)
			if reference_cost < 0:
				if costs.has(dest):
					_test.fail("path tree mud stripe: dest=%s should be unreachable, got cost=%d." % [
						str(dest), int(costs.get(dest, -1)),
					])
				continue
			var tree_cost := int(costs.get(dest, -1))
			if tree_cost != reference_cost:
				_test.fail("path tree mud stripe: dest=%s cost=%d != reference=%d." % [
					str(dest), tree_cost, reference_cost,
				])


func _test_path_tree_respects_occupant_blocks() -> void:
	var state := _build_state(Vector2i(3, 1))
	var unit := _build_unit(Vector2i(0, 0))
	var blocker := _build_unit(Vector2i(1, 0))
	blocker.unit_id = &"path_tree_blocker"
	state.units[unit.unit_id] = unit
	state.units[blocker.unit_id] = blocker
	if not _grid.place_unit(state, unit, unit.coord, true):
		_test.fail("path tree occupant: failed to place unit.")
		return
	if not _grid.place_unit(state, blocker, blocker.coord, true):
		_test.fail("path tree occupant: failed to place blocker.")
		return
	var tree := _grid.build_unit_move_path_tree(state, unit, unit.coord, 99)
	var costs: Dictionary = tree.get("costs", {})
	if costs.has(Vector2i(1, 0)):
		_test.fail("path tree occupant: blocker coord should not be reachable.")
	if costs.has(Vector2i(2, 0)):
		_test.fail("path tree occupant: coord behind blocker should not be reachable on a 1-row map.")


# Reference Dijkstra: no heuristic, O(N^2) frontier scan. This is intentionally the
# simplest possible correct implementation so it can serve as ground truth.
# Returns -1 if `to_coord` is unreachable.
func _reference_dijkstra_cost(state, unit_state, from_coord: Vector2i, to_coord: Vector2i) -> int:
	if from_coord == to_coord:
		return 0
	var best: Dictionary = {from_coord: 0}
	var frontier: Array[Vector2i] = [from_coord]
	while not frontier.is_empty():
		var pick_index := 0
		var pick_cost := int(best[frontier[0]])
		for i in range(1, frontier.size()):
			var c := int(best[frontier[i]])
			if c < pick_cost:
				pick_cost = c
				pick_index = i
		var current_coord: Vector2i = frontier[pick_index]
		frontier.remove_at(pick_index)
		var current_cost := int(best[current_coord])
		if current_cost > pick_cost:
			continue
		if current_coord == to_coord:
			return current_cost
		for neighbor in _grid.get_neighbors_4(state, current_coord):
			if not _grid.can_unit_step_between_anchors(state, unit_state, current_coord, neighbor):
				continue
			var step_cost := _grid.get_unit_move_cost(state, unit_state, neighbor)
			var next_cost := current_cost + step_cost
			if next_cost >= int(best.get(neighbor, 2147483647)):
				continue
			best[neighbor] = next_cost
			frontier.append(neighbor)
	return -1


func _build_state(map_size: Vector2i) -> BattleState:
	var state := BattleState.new()
	state.map_size = map_size
	for y in range(map_size.y):
		for x in range(map_size.x):
			var coord := Vector2i(x, y)
			var cell := BattleCellState.new()
			cell.coord = coord
			cell.base_terrain = &"land"
			cell.passable = true
			state.cells[coord] = cell
	return state


func _build_unit(coord: Vector2i) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = &"path_invariant_unit"
	unit.display_name = "PathInvariant"
	unit.faction_id = &"player"
	unit.coord = coord
	unit.is_alive = true
	unit.refresh_footprint()
	return unit
