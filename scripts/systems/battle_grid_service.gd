## 文件说明：该脚本属于战斗网格服务相关的服务脚本，主要封装当前领域所需的辅助逻辑。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleGridService
extends RefCounted

const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleEdgeFeatureState = preload("res://scripts/systems/battle_edge_feature_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const BATTLE_EDGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle_edge_service.gd")
const BattleEdgeService = preload("res://scripts/systems/battle_edge_service.gd")
const TERRAIN_LAND := &"land"
const TERRAIN_FOREST := &"forest"
const TERRAIN_WATER := &"water"
const TERRAIN_MUD := &"mud"
const TERRAIN_SPIKE := &"spike"
const MIN_RUNTIME_HEIGHT := -5
const MAX_RUNTIME_HEIGHT := 8
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
	area_value: int
) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	if state == null or not is_inside(state, center_coord):
		return coords

	var radius := maxi(int(area_value), 0)
	if area_pattern == &"" or area_pattern == &"single" or radius <= 0:
		coords.append(center_coord)
		return coords

	for y in range(center_coord.y - radius, center_coord.y + radius + 1):
		for x in range(center_coord.x - radius, center_coord.x + radius + 1):
			var coord := Vector2i(x, y)
			if not is_inside(state, coord):
				continue

			var dx := absi(coord.x - center_coord.x)
			var dy := absi(coord.y - center_coord.y)
			match area_pattern:
				&"diamond":
					if dx + dy <= radius:
						coords.append(coord)
				&"square":
					if maxi(dx, dy) <= radius:
						coords.append(coord)
				&"cross":
					if (dx == 0 and dy <= radius) or (dy == 0 and dx <= radius):
						coords.append(coord)
				_:
					if coord == center_coord:
						coords.append(coord)
	return coords


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


func can_place_footprint(
	state: BattleState,
	anchor_coord: Vector2i,
	footprint_size: Vector2i,
	ignored_unit_id: StringName = &""
) -> bool:
	var footprint_coords := get_footprint_coords(anchor_coord, footprint_size)
	var footprint_lookup: Dictionary = {}
	for footprint_coord in footprint_coords:
		footprint_lookup[footprint_coord] = true
		if not is_inside(state, footprint_coord):
			return false
		var cell := get_cell(state, footprint_coord)
		if cell == null or not cell.passable:
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
	if not can_place_footprint(state, target_coord, unit_state.footprint_size, unit_state.unit_id):
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
	var footprint_size := unit_state.footprint_size
	var anchor_coord := unit_state.coord
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

	if not can_place_footprint(state, to_coord, move_unit.footprint_size, move_unit.unit_id):
		return {
			"allowed": false,
			"message": "目标区域不可放置当前单位。",
		}

	if not can_place_unit(state, move_unit, to_coord):
		return {
			"allowed": false,
			"message": "目标区域高度差超过 1，无法通行。",
		}

	var move_cost := get_movement_cost(state, to_coord)
	return {
		"allowed": true,
		"cost": move_cost,
		"message": "可移动。",
	}


func recalculate_cell(cell_state: BattleCellState) -> void:
	if cell_state == null:
		return
	cell_state.current_height = clampi(
		int(cell_state.base_height) + int(cell_state.height_offset),
		MIN_RUNTIME_HEIGHT,
		MAX_RUNTIME_HEIGHT
	)
	cell_state.stack_layer = int(cell_state.current_height)
	cell_state.passable = cell_state.base_terrain != TERRAIN_WATER
	cell_state.move_cost = 2 if cell_state.base_terrain == TERRAIN_MUD or cell_state.base_terrain == TERRAIN_SPIKE else 1


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
	cell.base_terrain = terrain
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
	match StringName(terrain):
		TERRAIN_LAND:
			return "陆地"
		TERRAIN_FOREST:
			return "森林"
		TERRAIN_WATER:
			return "水域"
		TERRAIN_MUD:
			return "泥沼"
		TERRAIN_SPIKE:
			return "地刺"
		_:
			return terrain
