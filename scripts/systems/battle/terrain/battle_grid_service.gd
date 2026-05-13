## 文件说明：该脚本属于战斗网格服务相关的服务脚本，主要封装当前领域所需的辅助逻辑。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleGridService
extends RefCounted

const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleEdgeFeatureState = preload("res://scripts/systems/battle/core/battle_edge_feature_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BATTLE_EDGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/terrain/battle_edge_service.gd")
const BattleEdgeService = preload("res://scripts/systems/battle/terrain/battle_edge_service.gd")
const BattleTerrainRules = preload("res://scripts/systems/battle/terrain/battle_terrain_rules.gd")
const TERRAIN_LAND := &"land"
const TERRAIN_FOREST := &"forest"
const TERRAIN_WATER := &"water"
const TERRAIN_SHALLOW_WATER := &"shallow_water"
const TERRAIN_FLOWING_WATER := &"flowing_water"
const TERRAIN_DEEP_WATER := &"deep_water"
const TERRAIN_MUD := &"mud"
const TERRAIN_SPIKE := &"spike"
const MIN_RUNTIME_HEIGHT := -5
const MAX_RUNTIME_HEIGHT := 8
const JUMP_REDISTRIBUTION_FACTOR := 0.7
const JUMP_SIZE_STR_COST := 2
const JUMP_SMALL_AGILITY_BONUS := 1
const JUMP_STRENGTH_ATTRIBUTE: StringName = &"strength"
var _edge_service: BattleEdgeService = BATTLE_EDGE_SERVICE_SCRIPT.new()


func get_cell(state: BattleState, coord: Vector2i) -> BattleCellState:
	if state == null:
		return null
	return state.cells.get(coord)


func get_column_cells(state: BattleState, coord: Vector2i) -> Array[BattleCellState]:
	if state == null:
		return []
	_ensure_cell_columns(state)
	var results: Array[BattleCellState] = []
	var column_variant: Variant = state.cell_columns.get(coord, [])
	if column_variant is Array:
		for cell_variant in column_variant:
			var layer_cell := cell_variant as BattleCellState
			if layer_cell != null:
				results.append(layer_cell)
	return results


func get_unit_at_coord(state: BattleState, coord: Vector2i) -> BattleUnitState:
	if state == null:
		return null
	var cell := get_cell(state, coord)
	if cell == null or cell.occupant_unit_id == &"":
		return null
	return state.units.get(cell.occupant_unit_id) as BattleUnitState


func is_inside(state: BattleState, coord: Vector2i) -> bool:
	if state == null:
		return false
	return coord.x >= 0 and coord.y >= 0 and coord.x < state.map_size.x and coord.y < state.map_size.y


func get_neighbors_4(state: BattleState, coord: Vector2i) -> Array[Vector2i]:
	var neighbors: Array[Vector2i] = []
	for direction in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
		var candidate: Vector2i = coord + direction
		if is_inside(state, candidate):
			neighbors.append(candidate)
	return neighbors


func get_footprint_coords(anchor_coord: Vector2i, footprint_size: Vector2i) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	var normalized_size := Vector2i(maxi(footprint_size.x, 1), maxi(footprint_size.y, 1))
	for y in range(normalized_size.y):
		for x in range(normalized_size.x):
			coords.append(anchor_coord + Vector2i(x, y))
	return coords


func get_unit_target_coords(unit_state: BattleUnitState, anchor_coord: Vector2i) -> Array[Vector2i]:
	if unit_state == null:
		return []
	var footprint_size := unit_state.footprint_size
	if footprint_size == Vector2i.ZERO:
		footprint_size = BattleUnitState.get_footprint_size_for_body_size(unit_state.body_size)
	return get_footprint_coords(anchor_coord, footprint_size)


func get_height_difference(state: BattleState, from_coord: Vector2i, to_coord: Vector2i) -> int:
	var from_cell := get_cell(state, from_coord)
	var to_cell := get_cell(state, to_coord)
	if from_cell == null or to_cell == null:
		return 999
	return absi(int(from_cell.current_height) - int(to_cell.current_height))


func is_height_passable(state: BattleState, from_coord: Vector2i, to_coord: Vector2i) -> bool:
	return get_height_difference(state, from_coord, to_coord) <= 1


func get_movement_cost(state: BattleState, coord: Vector2i) -> int:
	var cell := get_cell(state, coord)
	if cell == null:
		return 1
	return maxi(int(cell.move_cost), 1)


func get_distance(from_coord: Vector2i, to_coord: Vector2i) -> int:
	return absi(from_coord.x - to_coord.x) + absi(from_coord.y - to_coord.y)


func get_area_coords(
	state: BattleState,
	center_coord: Vector2i,
	area_pattern: StringName,
	area_value: int,
	facing_direction: Vector2i = Vector2i.ZERO
) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	if state == null or not is_inside(state, center_coord):
		return coords

	var radius := maxi(int(area_value), 0)
	if area_pattern == &"" or area_pattern == &"single" or area_pattern == &"self" or radius <= 0:
		coords.append(center_coord)
		return coords

	match area_pattern:
		&"diamond":
			for y in range(center_coord.y - radius, center_coord.y + radius + 1):
				for x in range(center_coord.x - radius, center_coord.x + radius + 1):
					var coord := Vector2i(x, y)
					if not is_inside(state, coord):
						continue
					var dx := absi(coord.x - center_coord.x)
					var dy := absi(coord.y - center_coord.y)
					if dx + dy <= radius:
						coords.append(coord)
		&"square", &"radius":
			for y in range(center_coord.y - radius, center_coord.y + radius + 1):
				for x in range(center_coord.x - radius, center_coord.x + radius + 1):
					var coord := Vector2i(x, y)
					if not is_inside(state, coord):
						continue
					var dx := absi(coord.x - center_coord.x)
					var dy := absi(coord.y - center_coord.y)
					if maxi(dx, dy) <= radius:
						coords.append(coord)
		&"cross":
			for y in range(center_coord.y - radius, center_coord.y + radius + 1):
				for x in range(center_coord.x - radius, center_coord.x + radius + 1):
					var coord := Vector2i(x, y)
					if not is_inside(state, coord):
						continue
					var dx := absi(coord.x - center_coord.x)
					var dy := absi(coord.y - center_coord.y)
					if (dx == 0 and dy <= radius) or (dy == 0 and dx <= radius):
						coords.append(coord)
		&"line":
			return _build_line_coords(state, center_coord, radius, facing_direction)
		&"cone":
			return _build_cone_coords(state, center_coord, radius, facing_direction)
		&"narrow_cone":
			return _build_narrow_cone_coords(state, center_coord, radius, facing_direction)
		&"front_arc":
			return _build_front_arc_coords(state, center_coord, radius, facing_direction)
		_:
			coords.append(center_coord)
	return _sort_unique_coords(coords)


func _build_line_coords(
	state: BattleState,
	center_coord: Vector2i,
	radius: int,
	facing_direction: Vector2i
) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	if radius <= 0:
		coords.append(center_coord)
		return coords

	if _get_directional_line_axis(state, center_coord, facing_direction) == 0:
		for x in range(center_coord.x - radius, center_coord.x + radius + 1):
			var coord := Vector2i(x, center_coord.y)
			if is_inside(state, coord):
				coords.append(coord)
	else:
		for y in range(center_coord.y - radius, center_coord.y + radius + 1):
			var coord := Vector2i(center_coord.x, y)
			if is_inside(state, coord):
				coords.append(coord)
	return _sort_unique_coords(coords)


func _build_cone_coords(
	state: BattleState,
	center_coord: Vector2i,
	radius: int,
	facing_direction: Vector2i
) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	coords.append(center_coord)
	if radius <= 0:
		return coords

	match _resolve_area_direction(state, center_coord, facing_direction):
		Vector2i.RIGHT:
			for step in range(1, radius + 1):
				var x := center_coord.x + step
				for offset in range(-step, step + 1):
					var coord := Vector2i(x, center_coord.y + offset)
					if is_inside(state, coord):
						coords.append(coord)
		Vector2i.LEFT:
			for step in range(1, radius + 1):
				var x := center_coord.x - step
				for offset in range(-step, step + 1):
					var coord := Vector2i(x, center_coord.y + offset)
					if is_inside(state, coord):
						coords.append(coord)
		Vector2i.DOWN:
			for step in range(1, radius + 1):
				var y := center_coord.y + step
				for offset in range(-step, step + 1):
					var coord := Vector2i(center_coord.x + offset, y)
					if is_inside(state, coord):
						coords.append(coord)
		Vector2i.UP:
			for step in range(1, radius + 1):
				var y := center_coord.y - step
				for offset in range(-step, step + 1):
					var coord := Vector2i(center_coord.x + offset, y)
					if is_inside(state, coord):
						coords.append(coord)
	return _sort_unique_coords(coords)


func _build_narrow_cone_coords(
	state: BattleState,
	center_coord: Vector2i,
	radius: int,
	facing_direction: Vector2i
) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	if radius <= 0:
		coords.append(center_coord)
		return coords

	var wide_steps := mini(radius, 1)
	match _resolve_area_direction(state, center_coord, facing_direction):
		Vector2i.RIGHT:
			for step in range(0, radius + 1):
				var x := center_coord.x + step
				var half_width := 1 if step <= wide_steps else 0
				for offset in range(-half_width, half_width + 1):
					var coord := Vector2i(x, center_coord.y + offset)
					if is_inside(state, coord):
						coords.append(coord)
		Vector2i.LEFT:
			for step in range(0, radius + 1):
				var x := center_coord.x - step
				var half_width := 1 if step <= wide_steps else 0
				for offset in range(-half_width, half_width + 1):
					var coord := Vector2i(x, center_coord.y + offset)
					if is_inside(state, coord):
						coords.append(coord)
		Vector2i.DOWN:
			for step in range(0, radius + 1):
				var y := center_coord.y + step
				var half_width := 1 if step <= wide_steps else 0
				for offset in range(-half_width, half_width + 1):
					var coord := Vector2i(center_coord.x + offset, y)
					if is_inside(state, coord):
						coords.append(coord)
		Vector2i.UP:
			for step in range(0, radius + 1):
				var y := center_coord.y - step
				var half_width := 1 if step <= wide_steps else 0
				for offset in range(-half_width, half_width + 1):
					var coord := Vector2i(center_coord.x + offset, y)
					if is_inside(state, coord):
						coords.append(coord)
	return _sort_unique_coords(coords)


func _build_front_arc_coords(
	state: BattleState,
	center_coord: Vector2i,
	radius: int,
	facing_direction: Vector2i
) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	var arc_radius := maxi(radius, 0)
	var direction := _resolve_area_direction(state, center_coord, facing_direction)
	if direction == Vector2i.ZERO:
		direction = Vector2i.RIGHT

	if direction.x != 0:
		for offset in range(-arc_radius, arc_radius + 1):
			var coord := Vector2i(center_coord.x, center_coord.y + offset)
			if is_inside(state, coord):
				coords.append(coord)
	else:
		for offset in range(-arc_radius, arc_radius + 1):
			var coord := Vector2i(center_coord.x + offset, center_coord.y)
			if is_inside(state, coord):
				coords.append(coord)
	return _sort_unique_coords(coords)


func _get_directional_line_axis(
	state: BattleState,
	center_coord: Vector2i,
	facing_direction: Vector2i
) -> int:
	var normalized_direction := _normalize_area_direction(facing_direction)
	if normalized_direction != Vector2i.ZERO:
		return 0 if normalized_direction.x != 0 else 1
	return _get_stable_line_axis(state, center_coord)


func _get_stable_line_axis(state: BattleState, center_coord: Vector2i) -> int:
	var horizontal_span := mini(center_coord.x, state.map_size.x - 1 - center_coord.x)
	var vertical_span := mini(center_coord.y, state.map_size.y - 1 - center_coord.y)
	if horizontal_span >= vertical_span:
		return 0
	return 1


func _resolve_area_direction(
	state: BattleState,
	center_coord: Vector2i,
	facing_direction: Vector2i
) -> Vector2i:
	var normalized_direction := _normalize_area_direction(facing_direction)
	if normalized_direction != Vector2i.ZERO:
		return normalized_direction
	return _get_stable_cone_direction(state, center_coord)


func _get_stable_cone_direction(state: BattleState, center_coord: Vector2i) -> Vector2i:
	var right_span := maxi(state.map_size.x - 1 - center_coord.x, 0)
	var left_span := maxi(center_coord.x, 0)
	var down_span := maxi(state.map_size.y - 1 - center_coord.y, 0)
	var up_span := maxi(center_coord.y, 0)
	var best_direction := Vector2i.RIGHT
	var best_span := right_span
	if left_span > best_span:
		best_direction = Vector2i.LEFT
		best_span = left_span
	if down_span > best_span:
		best_direction = Vector2i.DOWN
		best_span = down_span
	if up_span > best_span:
		best_direction = Vector2i.UP
	return best_direction


func _normalize_area_direction(direction: Vector2i) -> Vector2i:
	if direction == Vector2i.ZERO:
		return Vector2i.ZERO
	var abs_x := absi(direction.x)
	var abs_y := absi(direction.y)
	if abs_x >= abs_y and abs_x > 0:
		return Vector2i(1 if direction.x > 0 else -1, 0)
	if abs_y > 0:
		return Vector2i(0, 1 if direction.y > 0 else -1)
	return Vector2i.ZERO


func _sort_unique_coords(coords: Array[Vector2i]) -> Array[Vector2i]:
	if coords.is_empty():
		return coords
	var unique_coords: Array[Vector2i] = []
	var seen: Dictionary = {}
	for coord in coords:
		if seen.has(coord):
			continue
		seen[coord] = true
		unique_coords.append(coord)
	return unique_coords


func get_distance_from_unit_to_coord(unit_state: BattleUnitState, target_coord: Vector2i) -> int:
	if unit_state == null:
		return 999999
	unit_state.refresh_footprint()
	var best_distance := 999999
	for occupied_coord in unit_state.occupied_coords:
		best_distance = mini(best_distance, get_distance(occupied_coord, target_coord))
	return best_distance


func get_distance_between_units(first_unit: BattleUnitState, second_unit: BattleUnitState) -> int:
	if first_unit == null or second_unit == null:
		return 999999
	first_unit.refresh_footprint()
	second_unit.refresh_footprint()
	var best_distance := 999999
	for first_coord in first_unit.occupied_coords:
		for second_coord in second_unit.occupied_coords:
			best_distance = mini(best_distance, get_distance(first_coord, second_coord))
	return best_distance


func is_walkable(state: BattleState, coord: Vector2i) -> bool:
	return can_place_footprint(state, coord, Vector2i.ONE)


func can_enter_cell(state: BattleState, coord: Vector2i) -> bool:
	return can_place_footprint(state, coord, Vector2i.ONE)


func can_unit_enter_coord(state: BattleState, coord: Vector2i, unit_state: BattleUnitState) -> bool:
	return can_place_footprint(state, coord, Vector2i.ONE, &"", unit_state)


func can_place_footprint(
	state: BattleState,
	anchor_coord: Vector2i,
	footprint_size: Vector2i,
	ignored_unit_id: StringName = &"",
	unit_state: BattleUnitState = null
) -> bool:
	var footprint_coords := get_footprint_coords(anchor_coord, footprint_size)
	var footprint_lookup: Dictionary = {}
	for footprint_coord in footprint_coords:
		footprint_lookup[footprint_coord] = true
		if not is_inside(state, footprint_coord):
			return false
		var cell := get_cell(state, footprint_coord)
		if cell == null:
			return false
		if unit_state != null:
			if not _can_unit_enter_cell(cell, unit_state):
				return false
		elif not cell.passable:
			return false
		if cell.occupant_unit_id != &"" and cell.occupant_unit_id != ignored_unit_id:
			return false
	for footprint_coord in footprint_coords:
		for direction in [Vector2i.RIGHT, Vector2i.DOWN]:
			var neighbor_coord: Vector2i = footprint_coord + direction
			if not footprint_lookup.has(neighbor_coord):
				continue
			if _edge_service.blocks_occupancy_between(state, footprint_coord, neighbor_coord):
				return false
	return true


func collect_blocking_unit_ids(
	state: BattleState,
	unit_state: BattleUnitState,
	target_coord: Vector2i
) -> Array[StringName]:
	var blocking_ids: Array[StringName] = []
	var seen_ids: Dictionary = {}
	for footprint_coord in get_unit_target_coords(unit_state, target_coord):
		var cell := get_cell(state, footprint_coord)
		if cell == null:
			continue
		var occupant_unit_id := cell.occupant_unit_id
		if occupant_unit_id == &"" or occupant_unit_id == unit_state.unit_id or seen_ids.has(occupant_unit_id):
			continue
		seen_ids[occupant_unit_id] = true
		blocking_ids.append(occupant_unit_id)
	return blocking_ids


func can_place_unit(
	state: BattleState,
	unit_state: BattleUnitState,
	target_coord: Vector2i,
	ignore_height: bool = false
) -> bool:
	if state == null or unit_state == null:
		return false
	if not can_place_footprint(state, target_coord, unit_state.footprint_size, unit_state.unit_id, unit_state):
		return false
	if ignore_height:
		return true

	unit_state.refresh_footprint()
	var delta := target_coord - unit_state.coord
	if delta == Vector2i.ZERO:
		return true
	if get_distance(Vector2i.ZERO, delta) == 1:
		return _can_unit_step_across_edges(state, unit_state, delta)
	var current_coords: Dictionary = {}
	for occupied_coord in unit_state.occupied_coords:
		current_coords[occupied_coord] = true

	for footprint_coord in get_unit_target_coords(unit_state, target_coord):
		var target_cell := get_cell(state, footprint_coord)
		if target_cell == null:
			return false
		var reference_coord := footprint_coord - delta if delta != Vector2i.ZERO else unit_state.coord
		if not current_coords.has(reference_coord):
			reference_coord = unit_state.coord
		var reference_cell := get_cell(state, reference_coord)
		if reference_cell == null:
			return false
		if absi(int(reference_cell.current_height) - int(target_cell.current_height)) > 1:
			return false
	return true


func get_edge_face(state: BattleState, from_coord: Vector2i, to_coord: Vector2i):
	return _edge_service.get_edge_face(state, from_coord, to_coord)


func _can_unit_step_across_edges(state: BattleState, unit_state: BattleUnitState, delta: Vector2i) -> bool:
	if state == null or unit_state == null:
		return false
	unit_state.refresh_footprint()
	return _can_anchor_step_across_edges(state, unit_state.footprint_size, unit_state.coord, delta)


func _can_anchor_step_across_edges(
	state: BattleState,
	footprint_size: Vector2i,
	anchor_coord: Vector2i,
	delta: Vector2i
) -> bool:
	match delta:
		Vector2i.RIGHT:
			for y in range(footprint_size.y):
				var from_coord := anchor_coord + Vector2i(footprint_size.x - 1, y)
				if not _edge_service.is_traversable_between(state, from_coord, from_coord + Vector2i.RIGHT):
					return false
		Vector2i.LEFT:
			for y in range(footprint_size.y):
				var from_coord := anchor_coord + Vector2i(0, y)
				if not _edge_service.is_traversable_between(state, from_coord, from_coord + Vector2i.LEFT):
					return false
		Vector2i.DOWN:
			for x in range(footprint_size.x):
				var from_coord := anchor_coord + Vector2i(x, footprint_size.y - 1)
				if not _edge_service.is_traversable_between(state, from_coord, from_coord + Vector2i.DOWN):
					return false
		Vector2i.UP:
			for x in range(footprint_size.x):
				var from_coord := anchor_coord + Vector2i(x, 0)
				if not _edge_service.is_traversable_between(state, from_coord, from_coord + Vector2i.UP):
					return false
		_:
			return false
	return true


func is_wall_blocked(state: BattleState, from_coord: Vector2i, to_coord: Vector2i) -> bool:
	return _edge_service.has_feature_between(state, from_coord, to_coord)


func can_traverse(
	state: BattleState,
	from_coord: Vector2i,
	to_coord: Vector2i,
	unit_state: BattleUnitState = null
) -> bool:
	if get_distance(from_coord, to_coord) != 1:
		return false
	if is_wall_blocked(state, from_coord, to_coord):
		return false
	if unit_state != null:
		return can_place_unit(state, unit_state, to_coord)
	if not is_inside(state, to_coord):
		return false
	if not can_enter_cell(state, to_coord):
		return false
	return _edge_service.is_traversable_between(state, from_coord, to_coord)


func can_unit_step_between_anchors(
	state: BattleState,
	unit_state: BattleUnitState,
	from_anchor: Vector2i,
	to_anchor: Vector2i
) -> bool:
	if state == null or unit_state == null:
		return false
	unit_state.refresh_footprint()
	var delta := to_anchor - from_anchor
	if get_distance(from_anchor, to_anchor) != 1:
		return false
	if not can_place_footprint(state, to_anchor, unit_state.footprint_size, unit_state.unit_id, unit_state):
		return false
	if not _can_anchor_step_across_edges(state, unit_state.footprint_size, from_anchor, delta):
		return false

	for footprint_coord in get_unit_target_coords(unit_state, to_anchor):
		var target_cell := get_cell(state, footprint_coord)
		if target_cell == null:
			return false
		var reference_cell := get_cell(state, footprint_coord - delta)
		if reference_cell == null:
			return false
		if absi(int(reference_cell.current_height) - int(target_cell.current_height)) > 1:
			return false
	return true


func get_unit_move_cost(
	state: BattleState,
	unit_state: BattleUnitState,
	target_coord: Vector2i
) -> int:
	if state == null or unit_state == null:
		return 1
	var movement_tags := _get_unit_movement_tags(unit_state)
	var move_cost := 1
	for occupied_coord in get_unit_target_coords(unit_state, target_coord):
		var cell := get_cell(state, occupied_coord)
		if cell == null:
			continue
		move_cost = maxi(move_cost, BattleTerrainRules.get_unit_move_cost(cell.base_terrain, movement_tags))
	return move_cost


func resolve_unit_move_path(
	state: BattleState,
	unit_state: BattleUnitState,
	from_coord: Vector2i,
	to_coord: Vector2i,
	max_move_points: int,
	first_step_cost_discount: int = 0,
	move_cost_provider: Callable = Callable()
) -> Dictionary:
	if state == null:
		return {
			"allowed": false,
			"cost": 0,
			"path": [],
			"message": "战斗状态不可用。",
		}
	if unit_state == null:
		return {
			"allowed": false,
			"cost": 0,
			"path": [],
			"message": "当前单位数据不可用。",
		}
	if not is_inside(state, from_coord):
		return {
			"allowed": false,
			"cost": 0,
			"path": [],
			"message": "当前单位不在有效战斗格上。",
		}
	if not is_inside(state, to_coord):
		return {
			"allowed": false,
			"cost": 0,
			"path": [],
			"message": "已到达战斗地图边界。",
		}
	if from_coord == to_coord:
		return {
			"allowed": true,
			"cost": 0,
			"path": [from_coord],
			"message": "可移动。",
		}

	var sanitized_max_move_points := maxi(max_move_points, 0)
	var has_initial_discount := first_step_cost_discount > 0

	# A* with Manhattan heuristic (admissible on a 4-connected grid where step_cost >= 1).
	# State is the cell coord. `has_discount` only matters for the start cell — after leaving
	# the start, the per-call first-step discount is exhausted, so subsequent states do not
	# need to be keyed on it.
	# best_costs[Vector2i] = best known g-cost from start to that cell.
	# previous[Vector2i]   = predecessor cell on the best known path.
	# visited[Vector2i]    = true once popped at its best cost (locked).
	# Heap entries: [f_score, g_cost, coord, has_discount].
	var best_costs: Dictionary = {from_coord: 0}
	var previous: Dictionary = {}
	var visited: Dictionary = {}
	var heap: Array = []
	var initial_pending_discount := first_step_cost_discount if has_initial_discount else 0
	_move_heap_push(heap, [
		_move_path_heuristic(from_coord, to_coord, initial_pending_discount),
		0,
		from_coord,
		has_initial_discount,
	])

	var found_target := false

	while not heap.is_empty():
		var entry: Array = _move_heap_pop(heap)
		var current_cost: int = int(entry[1])
		var current_coord: Vector2i = entry[2]
		var current_has_discount: bool = bool(entry[3])

		# Stale entry — a better g-cost finalized this coord earlier.
		if visited.has(current_coord):
			continue
		visited[current_coord] = true

		if current_coord == to_coord:
			found_target = true
			break

		for neighbor_coord in get_neighbors_4(state, current_coord):
			if visited.has(neighbor_coord):
				continue
			if not can_unit_step_between_anchors(state, unit_state, current_coord, neighbor_coord):
				continue
			var step_cost := get_unit_move_cost(state, unit_state, neighbor_coord)
			if move_cost_provider.is_valid():
				step_cost = int(move_cost_provider.call(unit_state, neighbor_coord))
			if current_has_discount and first_step_cost_discount > 0:
				step_cost = maxi(step_cost - first_step_cost_discount, 0)
			var next_cost := current_cost + step_cost
			if next_cost >= int(best_costs.get(neighbor_coord, 2147483647)):
				continue
			best_costs[neighbor_coord] = next_cost
			previous[neighbor_coord] = current_coord
			# Discount is consumed after leaving the start cell; no pending discount downstream.
			var h := _move_path_heuristic(neighbor_coord, to_coord, 0)
			_move_heap_push(heap, [next_cost + h, next_cost, neighbor_coord, false])

	if not found_target:
		if not can_place_footprint(state, to_coord, unit_state.footprint_size, unit_state.unit_id, unit_state):
			return {
				"allowed": false,
				"cost": 0,
				"path": [],
				"message": "目标区域不可放置当前单位。",
			}
		if get_distance(from_coord, to_coord) == 1:
			var direct_result := evaluate_move(state, from_coord, to_coord, unit_state)
			return {
				"allowed": false,
				"cost": int(direct_result.get("cost", 0)),
				"path": [],
				"message": String(direct_result.get("message", "该移动不可执行。")),
			}
		return {
			"allowed": false,
			"cost": 0,
			"path": [],
			"message": "目标地格当前不可到达。",
		}

	var final_cost := int(best_costs.get(to_coord, 2147483647))
	var anchor_path := _reconstruct_move_path(previous, from_coord, to_coord)
	if final_cost > sanitized_max_move_points:
		return {
			"allowed": false,
			"cost": final_cost,
			"path": anchor_path,
			"message": "移动力不足，无法移动。",
		}
	return {
		"allowed": true,
		"cost": final_cost,
		"path": anchor_path,
		"message": "可移动。",
	}


func evaluate_move(
	state: BattleState,
	from_coord: Vector2i,
	to_coord: Vector2i,
	unit_state: BattleUnitState = null
) -> Dictionary:
	if state == null:
		return {
			"allowed": false,
			"message": "战斗状态不可用。",
		}
	if not is_inside(state, to_coord):
		return {
			"allowed": false,
			"message": "已到达战斗地图边界。",
		}
	if get_distance(from_coord, to_coord) != 1:
		return {
			"allowed": false,
			"message": "普通移动只能前往相邻地格。",
		}
	if is_wall_blocked(state, from_coord, to_coord):
		return {
			"allowed": false,
			"message": "通道被墙壁阻挡。",
		}

	var move_unit := unit_state
	if move_unit == null:
		move_unit = get_unit_at_coord(state, from_coord)
	if move_unit == null:
		return {
			"allowed": false,
			"message": "当前单位数据不可用。",
		}

	if not can_place_footprint(state, to_coord, move_unit.footprint_size, move_unit.unit_id, move_unit):
		return {
			"allowed": false,
			"message": "目标区域不可放置当前单位。",
		}

	if not can_place_unit(state, move_unit, to_coord):
		return {
			"allowed": false,
			"message": "目标区域高度差超过 1，无法通行。",
		}

	var move_cost := get_unit_move_cost(state, move_unit, to_coord)
	return {
		"allowed": true,
		"cost": move_cost,
		"message": "可移动。",
	}


func recalculate_cell(cell_state: BattleCellState) -> void:
	if cell_state == null:
		return
	cell_state.base_terrain = BattleTerrainRules.normalize_terrain_id(cell_state.base_terrain)
	if cell_state.base_terrain != TERRAIN_FLOWING_WATER:
		cell_state.flow_direction = Vector2i.ZERO
	cell_state.current_height = clampi(
		int(cell_state.base_height) + int(cell_state.height_offset),
		MIN_RUNTIME_HEIGHT,
		MAX_RUNTIME_HEIGHT
	)
	cell_state.stack_layer = int(cell_state.current_height)
	cell_state.passable = BattleTerrainRules.get_global_passable(cell_state.base_terrain)
	cell_state.move_cost = BattleTerrainRules.get_base_move_cost(cell_state.base_terrain)


func recalculate_cells(cells: Dictionary) -> void:
	if cells == null:
		return
	for cell_state in cells.values():
		if cell_state is BattleCellState:
			recalculate_cell(cell_state)


func rebuild_all_cell_columns(state: BattleState) -> void:
	if state == null:
		return
	state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)


func sync_column_from_surface_cell(state: BattleState, coord: Vector2i) -> void:
	if state == null:
		return
	var surface_cell := get_cell(state, coord)
	if surface_cell == null:
		state.cell_columns.erase(coord)
		return
	state.cell_columns[coord] = BattleCellState.build_stacked_cells_from_surface_cell(surface_cell)


func _ensure_cell_columns(state: BattleState) -> void:
	if state == null:
		return
	if state.cell_columns.is_empty() and not state.cells.is_empty():
		rebuild_all_cell_columns(state)


func set_base_terrain(state: BattleState, coord: Vector2i, terrain: StringName) -> bool:
	var cell := get_cell(state, coord)
	if cell == null:
		return false
	cell.base_terrain = BattleTerrainRules.normalize_terrain_id(terrain)
	if cell.base_terrain != TERRAIN_FLOWING_WATER:
		cell.flow_direction = Vector2i.ZERO
	recalculate_cell(cell)
	sync_column_from_surface_cell(state, coord)
	_edge_service.mark_runtime_edge_faces_dirty(state)
	return true


func set_height_offset(state: BattleState, coord: Vector2i, height_offset: int) -> bool:
	var cell := get_cell(state, coord)
	if cell == null:
		return false
	cell.height_offset = clampi(
		height_offset,
		MIN_RUNTIME_HEIGHT - int(cell.base_height),
		MAX_RUNTIME_HEIGHT - int(cell.base_height)
	)
	recalculate_cell(cell)
	sync_column_from_surface_cell(state, coord)
	_edge_service.mark_runtime_edge_faces_dirty(state)
	return true


func set_edge_feature(state: BattleState, coord: Vector2i, direction: Vector2i, feature_state: BattleEdgeFeatureState) -> bool:
	var cell := get_cell(state, coord)
	if cell == null:
		return false
	cell.set_edge_feature(direction, feature_state)
	sync_column_from_surface_cell(state, coord)
	_edge_service.mark_runtime_edge_faces_dirty(state)
	return true


func clear_edge_feature(state: BattleState, coord: Vector2i, direction: Vector2i) -> bool:
	return set_edge_feature(state, coord, direction, BattleEdgeFeatureState.make_none())


func apply_height_delta_result(state: BattleState, coord: Vector2i, height_delta: int) -> Dictionary:
	var cell := get_cell(state, coord)
	if cell == null:
		return {
			"changed": false,
			"before_height": 0,
			"after_height": 0,
			"applied_delta": 0,
		}

	var before_height := int(cell.current_height)
	var changed := set_height_offset(state, coord, int(cell.height_offset) + height_delta)
	var after_height := int(cell.current_height)
	return {
		"changed": changed and before_height != after_height,
		"before_height": before_height,
		"after_height": after_height,
		"applied_delta": after_height - before_height,
	}


func apply_height_delta(state: BattleState, coord: Vector2i, height_delta: int) -> bool:
	return bool(apply_height_delta_result(state, coord, height_delta).get("changed", false))


func set_occupant(state: BattleState, coord: Vector2i, unit_id: StringName) -> void:
	var cell := get_cell(state, coord)
	if cell == null:
		return
	cell.occupant_unit_id = unit_id


func set_occupants(state: BattleState, coords: Array[Vector2i], unit_id: StringName) -> void:
	for coord in coords:
		set_occupant(state, coord, unit_id)


func clear_unit_occupancy(state: BattleState, unit_state: BattleUnitState) -> void:
	if state == null or unit_state == null:
		return
	unit_state.refresh_footprint()
	set_occupants(state, unit_state.occupied_coords, &"")


func place_unit(
	state: BattleState,
	unit_state: BattleUnitState,
	target_coord: Vector2i,
	ignore_height: bool = false
) -> bool:
	if state == null or unit_state == null:
		return false
	if not can_place_unit(state, unit_state, target_coord, ignore_height):
		return false
	clear_unit_occupancy(state, unit_state)
	unit_state.set_anchor_coord(target_coord)
	set_occupants(state, unit_state.occupied_coords, unit_state.unit_id)
	return true


func move_unit(state: BattleState, unit_state: BattleUnitState, target_coord: Vector2i) -> bool:
	return place_unit(state, unit_state, target_coord)


func move_unit_force(state: BattleState, unit_state: BattleUnitState, target_coord: Vector2i) -> bool:
	return place_unit(state, unit_state, target_coord, true)


func get_terrain_display_name(terrain: String) -> String:
	return BattleTerrainRules.get_display_name(StringName(terrain))


func _can_unit_enter_cell(cell: BattleCellState, unit_state: BattleUnitState) -> bool:
	if cell == null or unit_state == null:
		return false
	return BattleTerrainRules.can_unit_enter_terrain(cell.base_terrain, _get_unit_movement_tags(unit_state))


func _get_unit_movement_tags(unit_state: BattleUnitState) -> Array[StringName]:
	return unit_state.movement_tags if unit_state != null else []


func _move_path_heuristic(from_coord: Vector2i, to_coord: Vector2i, pending_discount: int) -> int:
	# Manhattan distance: admissible on a 4-connected grid because every step adds at least 1
	# to the true cost. When a first-step discount is still pending, subtract it (clamped at 0)
	# so the heuristic remains admissible even if the next step's cost could be reduced to 0.
	var raw := absi(to_coord.x - from_coord.x) + absi(to_coord.y - from_coord.y)
	return maxi(0, raw - pending_discount)


func _move_heap_push(heap: Array, entry: Array) -> void:
	# Binary min-heap push, ordered by entry[0] (f_score). entry layout:
	# [f_score, g_cost, coord, has_discount].
	heap.append(entry)
	var index := heap.size() - 1
	while index > 0:
		var parent_index := (index - 1) >> 1
		if int(heap[parent_index][0]) <= int(heap[index][0]):
			break
		var tmp = heap[parent_index]
		heap[parent_index] = heap[index]
		heap[index] = tmp
		index = parent_index


func _move_heap_pop(heap: Array) -> Array:
	# Binary min-heap pop. Returns the root and reheapifies in place.
	var top: Array = heap[0]
	var last: Array = heap.pop_back()
	if heap.is_empty():
		return top
	heap[0] = last
	var index := 0
	var size := heap.size()
	while true:
		var left := (index << 1) + 1
		var right := left + 1
		var smallest := index
		if left < size and int(heap[left][0]) < int(heap[smallest][0]):
			smallest = left
		if right < size and int(heap[right][0]) < int(heap[smallest][0]):
			smallest = right
		if smallest == index:
			break
		var tmp = heap[index]
		heap[index] = heap[smallest]
		heap[smallest] = tmp
		index = smallest
	return top


func _reconstruct_move_path(previous: Dictionary, start: Vector2i, end: Vector2i) -> Array[Vector2i]:
	# Walk predecessor chain back from `end` to `start`; return start→end order.
	var reversed_path: Array[Vector2i] = []
	var current: Vector2i = end
	while current != start:
		reversed_path.append(current)
		var prev_variant = previous.get(current, null)
		if not (prev_variant is Vector2i):
			return []
		current = prev_variant
	reversed_path.append(start)
	reversed_path.reverse()
	return reversed_path


func get_chebyshev_distance(from_coord: Vector2i, to_coord: Vector2i) -> int:
	return maxi(absi(to_coord.x - from_coord.x), absi(to_coord.y - from_coord.y))


func compute_jump_params(unit_state: BattleUnitState, effect_def: CombatEffectDef) -> Dictionary:
	if unit_state == null or effect_def == null:
		return {}
	var jump_str := _get_jump_effective_str(unit_state)
	var budget := float(effect_def.jump_base_budget) + float(effect_def.jump_str_scale) * float(jump_str)
	var arc_ratio_raw := float(effect_def.jump_arc_ratio)
	var arc_ratio := clampf(arc_ratio_raw, CombatEffectDef.MIN_JUMP_ARC_RATIO, 1.0)
	var range_multiplier := maxi(int(effect_def.jump_range_multiplier), 1)
	var min_arc := maxi(1, int(round(budget * arc_ratio)))
	var range_budget := maxf(0.0, budget * (1.0 - arc_ratio))
	var max_range := maxi(1, int(round(range_budget * float(range_multiplier))))
	if int(effect_def.forced_move_distance) > 0:
		max_range = mini(max_range, int(effect_def.forced_move_distance))
	return {
		"budget": budget,
		"min_arc": min_arc,
		"range_budget": range_budget,
		"max_range": max_range,
		"arc_ratio": arc_ratio,
		"range_multiplier": range_multiplier,
	}


func compute_jump_arc_height_for_range(params: Dictionary, actual_range: int) -> int:
	if params.is_empty() or actual_range < 1:
		return 0
	var range_multiplier := maxi(int(params.get("range_multiplier", 1)), 1)
	var distance_cost := float(actual_range) / float(range_multiplier)
	var range_budget := float(params.get("range_budget", 0.0))
	var saved_budget := maxf(0.0, range_budget - distance_cost)
	var extra_arc := int(round(saved_budget * JUMP_REDISTRIBUTION_FACTOR))
	return int(params.get("min_arc", 0)) + extra_arc


func can_jump_arc(
	state: BattleState,
	unit_state: BattleUnitState,
	target_coord: Vector2i,
	effect_def: CombatEffectDef
) -> bool:
	if state == null or unit_state == null or effect_def == null:
		return false
	if target_coord == unit_state.coord:
		return false
	if not is_inside(state, target_coord):
		return false
	var params := compute_jump_params(unit_state, effect_def)
	if params.is_empty():
		return false
	var max_range := int(params.get("max_range", 0))
	var actual_range := get_chebyshev_distance(unit_state.coord, target_coord)
	if actual_range < 1 or actual_range > max_range:
		return false
	if not can_place_unit(state, unit_state, target_coord, true):
		return false
	var from_cell := get_cell(state, unit_state.coord)
	var to_cell := get_cell(state, target_coord)
	if from_cell == null or to_cell == null:
		return false
	var arc_height := compute_jump_arc_height_for_range(params, actual_range)
	var h0 := int(from_cell.current_height)
	var h1 := int(to_cell.current_height)
	var apex := float(maxi(h0, h1) + arc_height)
	var path := _supercover_jump_path(unit_state.coord, target_coord)
	var path_n := path.size() - 1
	if path_n <= 1:
		return true
	for i in range(1, path_n):
		var t := float(i) / float(path_n)
		var chord_h := lerpf(float(h0), float(h1), t)
		var arc_h_at_t := chord_h + 4.0 * (apex - chord_h) * t * (1.0 - t)
		var cell := get_cell(state, path[i])
		if cell == null:
			return false
		var blocker_h := int(cell.current_height)
		if cell.occupant_unit_id != &"" and cell.occupant_unit_id != unit_state.unit_id:
			var occupant := state.units.get(cell.occupant_unit_id) as BattleUnitState
			if occupant != null:
				blocker_h += _get_unit_presence_height(occupant)
		if arc_h_at_t <= float(blocker_h):
			return false
	return true


func can_blink_to_coord(
	state: BattleState,
	unit_state: BattleUnitState,
	target_coord: Vector2i,
	effect_def: CombatEffectDef
) -> bool:
	if state == null or unit_state == null or effect_def == null:
		return false
	if target_coord == unit_state.coord:
		return false
	if not is_inside(state, target_coord):
		return false
	var max_range := int(effect_def.forced_move_distance)
	var actual_range := get_chebyshev_distance(unit_state.coord, target_coord)
	if max_range > 0 and actual_range > max_range:
		return false
	return actual_range >= 1 and can_place_unit(state, unit_state, target_coord, true)


func _supercover_jump_path(from_coord: Vector2i, to_coord: Vector2i) -> Array[Vector2i]:
	var path: Array[Vector2i] = []
	var dx := to_coord.x - from_coord.x
	var dy := to_coord.y - from_coord.y
	var steps := maxi(absi(dx), absi(dy))
	if steps <= 0:
		path.append(from_coord)
		return path
	var prev := Vector2i(-99999, -99999)
	for i in range(steps + 1):
		var t := float(i) / float(steps)
		var x := int(round(float(from_coord.x) + float(dx) * t))
		var y := int(round(float(from_coord.y) + float(dy) * t))
		var current := Vector2i(x, y)
		if current != prev:
			path.append(current)
			prev = current
	if path.is_empty() or path[path.size() - 1] != to_coord:
		path.append(to_coord)
	return path


func _get_jump_effective_str(unit_state: BattleUnitState) -> int:
	var raw_str := 0
	if unit_state != null and unit_state.attribute_snapshot != null:
		raw_str = int(unit_state.attribute_snapshot.get_value(JUMP_STRENGTH_ATTRIBUTE))
	var modifier := _get_jump_size_str_modifier(unit_state)
	return maxi(0, raw_str + modifier)


func _get_jump_size_str_modifier(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return 0
	match int(unit_state.body_size):
		BattleUnitState.BODY_SIZE_SMALL:
			return JUMP_SMALL_AGILITY_BONUS
		BattleUnitState.BODY_SIZE_MEDIUM:
			return 0
		BattleUnitState.BODY_SIZE_LARGE:
			return -JUMP_SIZE_STR_COST * 2
		BattleUnitState.BODY_SIZE_HUGE:
			return -JUMP_SIZE_STR_COST * 5
		BattleUnitState.BODY_SIZE_GARGANTUAN:
			return -JUMP_SIZE_STR_COST * 8
		BattleUnitState.BODY_SIZE_BOSS:
			return -JUMP_SIZE_STR_COST * 8
		_:
			return 0


func _get_unit_presence_height(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return 1
	var fp := unit_state.footprint_size
	if fp == Vector2i.ZERO:
		fp = BattleUnitState.get_footprint_size_for_body_size(unit_state.body_size)
	return mini(maxi(fp.x, 1), maxi(fp.y, 1))
