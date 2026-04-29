## 文件说明：该脚本属于战斗水体拓扑相关的服务脚本，集中维护水域连通分量与浅水/流水/深水的重分类。
## 审查重点：重点核对局部重分类边界、连通分量收集和 outlet 判定是否与高度变化保持一致。
## 备注：该服务只负责重分类地形，不负责移动、渲染或技能命令本身。

class_name BattleTerrainTopologyService
extends RefCounted

const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleTerrainRules = preload("res://scripts/systems/battle/terrain/battle_terrain_rules.gd")


func reclassify_all_water_terrain(cells: Dictionary, map_size: Vector2i) -> Array[Dictionary]:
	return _reclassify_components(cells, map_size, _collect_all_water_coords(cells))


func reclassify_water_terrain_near_coords(
	cells: Dictionary,
	map_size: Vector2i,
	seed_coords: Array[Vector2i]
) -> Array[Dictionary]:
	return _reclassify_components(cells, map_size, _collect_seed_water_coords(cells, map_size, seed_coords))


func _reclassify_components(cells: Dictionary, map_size: Vector2i, start_coords: Array[Vector2i]) -> Array[Dictionary]:
	var changes: Array[Dictionary] = []
	if cells.is_empty() or map_size == Vector2i.ZERO or start_coords.is_empty():
		return changes

	var visited: Dictionary = {}
	for start in start_coords:
		if visited.has(start):
			continue
		var component := _collect_component(cells, map_size, start, visited)
		if component.is_empty():
			continue
		var component_lookup: Dictionary = {}
		for coord in component:
			component_lookup[coord] = true
		var component_has_outlet := _component_has_outlet(cells, map_size, component)
		for coord in component:
			var cell := cells.get(coord) as BattleCellState
			if cell == null:
				continue
			var next_flow_direction := Vector2i.ZERO
			var next_terrain := BattleTerrainRules.TERRAIN_DEEP_WATER
			if component_has_outlet:
				next_flow_direction = _resolve_flow_direction(cells, map_size, coord, component_lookup)
			if next_flow_direction != Vector2i.ZERO:
				next_terrain = BattleTerrainRules.TERRAIN_FLOWING_WATER
			elif _is_shallow_cell(cells, map_size, coord):
				next_terrain = BattleTerrainRules.TERRAIN_SHALLOW_WATER
			if cell.base_terrain != next_terrain or cell.flow_direction != next_flow_direction:
				changes.append({
					"coord": coord,
					"before_terrain": cell.base_terrain,
					"after_terrain": next_terrain,
					"before_flow_direction": cell.flow_direction,
					"after_flow_direction": next_flow_direction,
				})
	return changes


func _collect_all_water_coords(cells: Dictionary) -> Array[Vector2i]:
	var results: Array[Vector2i] = []
	for coord_variant in cells.keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		var cell := cells.get(coord) as BattleCellState
		if _is_water_like(cell):
			results.append(coord)
	return results


func _collect_seed_water_coords(cells: Dictionary, map_size: Vector2i, seed_coords: Array[Vector2i]) -> Array[Vector2i]:
	var results: Array[Vector2i] = []
	var seen: Dictionary = {}
	for seed in seed_coords:
		for coord in _get_coord_and_neighbors(map_size, seed):
			if seen.has(coord):
				continue
			seen[coord] = true
			var cell := cells.get(coord) as BattleCellState
			if _is_water_like(cell):
				results.append(coord)
	return results


func _collect_component(
	cells: Dictionary,
	map_size: Vector2i,
	start: Vector2i,
	visited: Dictionary
) -> Array[Vector2i]:
	var start_cell := cells.get(start) as BattleCellState
	if not _is_water_like(start_cell):
		return []

	var component: Array[Vector2i] = []
	var frontier: Array[Vector2i] = [start]
	while not frontier.is_empty():
		var current: Vector2i = frontier.pop_front()
		if visited.has(current):
			continue
		var current_cell := cells.get(current) as BattleCellState
		if not _is_water_like(current_cell):
			continue
		visited[current] = true
		component.append(current)
		for neighbor in _get_neighbors_4(map_size, current):
			if not visited.has(neighbor):
				frontier.append(neighbor)
	return component


func _component_has_outlet(cells: Dictionary, map_size: Vector2i, component: Array[Vector2i]) -> bool:
	for coord in component:
		if _is_edge_coord(map_size, coord):
			return true
		var cell := cells.get(coord) as BattleCellState
		if cell == null:
			continue
		for neighbor in _get_neighbors_4(map_size, coord):
			var neighbor_cell := cells.get(neighbor) as BattleCellState
			if _is_water_like(neighbor_cell):
				continue
			if neighbor_cell != null and int(neighbor_cell.current_height) <= int(cell.current_height):
				return true
	return false


func _resolve_flow_direction(
	cells: Dictionary,
	map_size: Vector2i,
	coord: Vector2i,
	component_lookup: Dictionary
) -> Vector2i:
	var cell := cells.get(coord) as BattleCellState
	if cell == null:
		return Vector2i.ZERO

	var best_direction := Vector2i.ZERO
	var best_neighbor_height := 2147483647
	for direction in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
		var neighbor_coord: Vector2i = coord + direction
		if not _is_inside(map_size, neighbor_coord):
			return direction
		var neighbor_cell := cells.get(neighbor_coord) as BattleCellState
		if _is_water_like(neighbor_cell):
			continue
		if neighbor_cell == null:
			continue
		var neighbor_height := int(neighbor_cell.current_height)
		if neighbor_height > int(cell.current_height):
			continue
		if neighbor_height < best_neighbor_height:
			best_neighbor_height = neighbor_height
			best_direction = direction
	if best_direction != Vector2i.ZERO:
		return best_direction

	for direction in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
		var neighbor_coord: Vector2i = coord + direction
		if component_lookup.has(neighbor_coord):
			var neighbor_cell := cells.get(neighbor_coord) as BattleCellState
			if neighbor_cell != null and neighbor_cell.base_terrain == BattleTerrainRules.TERRAIN_FLOWING_WATER:
				return direction
	return Vector2i.ZERO


func _is_shallow_cell(cells: Dictionary, map_size: Vector2i, coord: Vector2i) -> bool:
	var cell := cells.get(coord) as BattleCellState
	if cell == null:
		return false
	var min_bank_delta := 2147483647
	for neighbor in _get_neighbors_4(map_size, coord):
		var neighbor_cell := cells.get(neighbor) as BattleCellState
		if _is_water_like(neighbor_cell):
			continue
		if neighbor_cell == null:
			min_bank_delta = 0
			continue
		min_bank_delta = mini(min_bank_delta, int(neighbor_cell.current_height) - int(cell.current_height))
	if min_bank_delta == 2147483647:
		return false
	return min_bank_delta <= 1


func _is_water_like(cell: BattleCellState) -> bool:
	return cell != null and BattleTerrainRules.is_water_terrain(cell.base_terrain)


func _get_coord_and_neighbors(map_size: Vector2i, coord: Vector2i) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	if _is_inside(map_size, coord):
		coords.append(coord)
	for neighbor in _get_neighbors_4(map_size, coord):
		coords.append(neighbor)
	return coords


func _get_neighbors_4(map_size: Vector2i, coord: Vector2i) -> Array[Vector2i]:
	var neighbors: Array[Vector2i] = []
	for direction in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
		var candidate: Vector2i = coord + direction
		if _is_inside(map_size, candidate):
			neighbors.append(candidate)
	return neighbors


func _is_inside(map_size: Vector2i, coord: Vector2i) -> bool:
	return coord.x >= 0 and coord.y >= 0 and coord.x < map_size.x and coord.y < map_size.y


func _is_edge_coord(map_size: Vector2i, coord: Vector2i) -> bool:
	return coord.x <= 0 or coord.y <= 0 or coord.x >= map_size.x - 1 or coord.y >= map_size.y - 1
