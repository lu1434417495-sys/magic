class_name BattleMapGenerationSystem
extends RefCounted

const TERRAIN_LAND := "land"
const TERRAIN_FOREST := "forest"
const TERRAIN_WATER := "water"
const TERRAIN_MUD := "mud"
const TERRAIN_SPIKE := "spike"

const DEFAULT_MOVE_POINTS := 6

var _rng := RandomNumberGenerator.new()


func build_battle(encounter_context: Dictionary) -> Dictionary:
	var battle_seed := _build_battle_seed(encounter_context)
	var initial_action_points := maxi(int(encounter_context.get("action_points", DEFAULT_MOVE_POINTS)), 1)
	var size_options: Array[Vector2i] = [
		Vector2i(11, 9),
		Vector2i(13, 9),
		Vector2i(13, 11),
		Vector2i(15, 11),
	]

	for attempt in range(12):
		_rng.seed = battle_seed + attempt * 1013
		var map_size: Vector2i = size_options[_rng.randi_range(0, size_options.size() - 1)]
		var cells: Dictionary = _generate_cells(map_size)
		var spawn_pair: Dictionary = _find_spawn_pair(cells, map_size)
		if spawn_pair.is_empty():
			continue

		return {
			"seed": battle_seed + attempt * 1013,
			"size": map_size,
			"cells": cells,
			"terrain_counts": _count_terrain(cells),
			"player_coord": spawn_pair.get("player_coord", Vector2i.ZERO),
			"enemy_coord": spawn_pair.get("enemy_coord", Vector2i.ZERO),
			"selected_coord": spawn_pair.get("player_coord", Vector2i.ZERO),
			"remaining_move": initial_action_points,
			"max_move": initial_action_points,
			"action_points": initial_action_points,
			"player_attributes": encounter_context.get("player_attributes", {}).duplicate(true),
			"monster": encounter_context.get("monster", {}).duplicate(true),
			"world_coord": encounter_context.get("world_coord", Vector2i.ZERO),
		}

	_rng.seed = battle_seed
	return _build_fallback_battle(encounter_context, battle_seed)


func get_cell(battle_state: Dictionary, coord: Vector2i) -> Dictionary:
	var cells: Dictionary = battle_state.get("cells", {})
	return cells.get(coord, {})


func is_coord_inside(battle_state: Dictionary, coord: Vector2i) -> bool:
	var map_size: Vector2i = battle_state.get("size", Vector2i.ZERO)
	return coord.x >= 0 and coord.y >= 0 and coord.x < map_size.x and coord.y < map_size.y


func evaluate_move(battle_state: Dictionary, from_coord: Vector2i, to_coord: Vector2i) -> Dictionary:
	if not is_coord_inside(battle_state, to_coord):
		return {
			"allowed": false,
			"message": "已到达战斗地图边界。",
		}

	var from_cell: Dictionary = get_cell(battle_state, from_coord)
	var to_cell: Dictionary = get_cell(battle_state, to_coord)
	if from_cell.is_empty() or to_cell.is_empty():
		return {
			"allowed": false,
			"message": "目标格数据不可用。",
		}

	if not bool(to_cell.get("passable", false)):
		return {
			"allowed": false,
			"message": "水域不可通行。",
		}

	var height_diff := absi(int(from_cell.get("height", 0)) - int(to_cell.get("height", 0)))
	if height_diff > 1:
		return {
			"allowed": false,
			"height_diff": height_diff,
			"message": "高度差为 %d，超过 1，无法通行。" % height_diff,
		}

	var move_cost := int(to_cell.get("move_cost", 1))
	var remaining_move := int(battle_state.get("remaining_move", 0))
	if remaining_move < move_cost:
		return {
			"allowed": false,
			"cost": move_cost,
			"remaining_move": remaining_move,
			"message": "进入%s需要 %d 点行动点，当前剩余 %d。" % [
				get_terrain_display_name(String(to_cell.get("terrain", TERRAIN_LAND))),
				move_cost,
				remaining_move,
			],
		}

	return {
		"allowed": true,
		"cost": move_cost,
		"height_diff": height_diff,
		"message": "可移动。",
	}


func get_terrain_display_name(terrain: String) -> String:
	match terrain:
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
			return "未知地形"


func _build_battle_seed(encounter_context: Dictionary) -> int:
	var monster: Dictionary = encounter_context.get("monster", {})
	var world_coord: Vector2i = encounter_context.get("world_coord", Vector2i.ZERO)
	var world_seed: int = int(encounter_context.get("world_seed", 0))
	var monster_hash: int = String(monster.get("entity_id", "wild")).hash()
	return world_seed + monster_hash + world_coord.x * 92821 + world_coord.y * 68917


func _generate_cells(map_size: Vector2i) -> Dictionary:
	var heights: Dictionary = _generate_height_map(map_size)
	var terrain_by_coord: Dictionary = _generate_base_terrain_map(map_size)
	var water_cells: Dictionary = _generate_water_cells(map_size)

	for coord in water_cells.keys():
		terrain_by_coord[coord] = TERRAIN_WATER

	_apply_hazard_terrain(terrain_by_coord, map_size)

	var cells: Dictionary = {}
	for coord in _collect_all_coords(map_size):
		var terrain: String = String(terrain_by_coord.get(coord, TERRAIN_LAND))
		var passable := terrain != TERRAIN_WATER
		var move_cost := 2 if terrain == TERRAIN_MUD or terrain == TERRAIN_SPIKE else 1
		var cell_height := 0 if terrain == TERRAIN_WATER else int(heights.get(coord, 0))
		cells[coord] = {
			"coord": coord,
			"terrain": terrain,
			"height": cell_height,
			"passable": passable,
			"move_cost": move_cost,
		}

	return cells


func _generate_height_map(map_size: Vector2i) -> Dictionary:
	var heights: Dictionary = {}
	var all_coords := _collect_all_coords(map_size)

	for coord in all_coords:
		heights[coord] = _rng.randi_range(0, 2)

	for _smooth_pass in range(2):
		var smoothed: Dictionary = {}
		for coord in all_coords:
			var total := int(heights.get(coord, 0))
			var count := 1
			for neighbor in _get_neighbors_8(map_size, coord):
				total += int(heights.get(neighbor, 0))
				count += 1

			var next_height := int(round(float(total) / float(count)))
			if _rng.randf() < 0.16:
				next_height += _rng.randi_range(-1, 1)
			smoothed[coord] = clampi(next_height, 0, 4)
		heights = smoothed

	for _ridge_index in range(3):
		var ridge_center := _pick_random_coord(map_size)
		var ridge_radius := _rng.randi_range(1, 2)
		var ridge_height := _rng.randi_range(1, 2)
		for coord in all_coords:
			var dx := absi(coord.x - ridge_center.x)
			var dy := absi(coord.y - ridge_center.y)
			if maxi(dx, dy) <= ridge_radius:
				heights[coord] = clampi(int(heights.get(coord, 0)) + ridge_height, 0, 5)

	for _valley_index in range(1):
		var valley_center := _pick_random_coord(map_size)
		for coord in all_coords:
			var distance := absi(coord.x - valley_center.x) + absi(coord.y - valley_center.y)
			if distance <= 2:
				heights[coord] = clampi(int(heights.get(coord, 0)) - 1, 0, 5)

	return heights


func _generate_base_terrain_map(map_size: Vector2i) -> Dictionary:
	var terrain_by_coord: Dictionary = {}
	for coord in _collect_all_coords(map_size):
		terrain_by_coord[coord] = TERRAIN_FOREST if _rng.randf() < 0.28 else TERRAIN_LAND
	return terrain_by_coord


func _generate_water_cells(map_size: Vector2i) -> Dictionary:
	var water_cells: Dictionary = {}
	var region_count := _rng.randi_range(1, 2)
	var target_max := maxi(int(round(float(map_size.x * map_size.y) * 0.08)), 4)

	for _region_index in range(region_count):
		var start := _pick_water_seed(map_size)
		var frontier: Array[Vector2i] = [start]
		var local_visited: Dictionary = {}
		var target_size := _rng.randi_range(4, target_max)
		var region_size := 0

		while not frontier.is_empty() and region_size < target_size:
			var current: Vector2i = frontier.pop_back()
			if local_visited.has(current):
				continue
			local_visited[current] = true
			if water_cells.has(current):
				continue

			water_cells[current] = true
			region_size += 1

			var neighbors := _get_neighbors_4(map_size, current)
			_shuffle_array(neighbors)
			for neighbor in neighbors:
				if local_visited.has(neighbor) or water_cells.has(neighbor):
					continue
				if _rng.randf() <= 0.72:
					frontier.append(neighbor)

		if water_cells.size() >= target_max * region_count:
			break

	return water_cells


func _apply_hazard_terrain(terrain_by_coord: Dictionary, map_size: Vector2i) -> void:
	var candidates: Array[Vector2i] = []
	for coord in _collect_all_coords(map_size):
		var terrain: String = String(terrain_by_coord.get(coord, TERRAIN_LAND))
		if terrain == TERRAIN_WATER:
			continue
		if _is_adjacent_to_water(terrain_by_coord, map_size, coord):
			continue
		candidates.append(coord)

	_shuffle_array(candidates)

	var mud_count := mini(candidates.size(), _rng.randi_range(4, 6))
	for index in range(mud_count):
		terrain_by_coord[candidates[index]] = TERRAIN_MUD

	var remaining := maxi(candidates.size() - mud_count, 0)
	var spike_count := mini(remaining, _rng.randi_range(3, 5))
	for index in range(mud_count, mud_count + spike_count):
		terrain_by_coord[candidates[index]] = TERRAIN_SPIKE


func _is_adjacent_to_water(terrain_by_coord: Dictionary, map_size: Vector2i, coord: Vector2i) -> bool:
	for neighbor in _get_neighbors_4(map_size, coord):
		if String(terrain_by_coord.get(neighbor, TERRAIN_LAND)) == TERRAIN_WATER:
			return true
	return false


func _find_spawn_pair(cells: Dictionary, map_size: Vector2i) -> Dictionary:
	var visited: Dictionary = {}
	var largest_component: Array[Vector2i] = []

	for coord in _collect_all_coords(map_size):
		if visited.has(coord):
			continue
		var cell: Dictionary = cells.get(coord, {})
		if not bool(cell.get("passable", false)):
			continue

		var component := _collect_connected_component(cells, map_size, coord, visited)
		if component.size() > largest_component.size():
			largest_component = component

	if largest_component.size() < 8:
		return {}

	var anchor: Vector2i = largest_component[_rng.randi_range(0, largest_component.size() - 1)]
	var distance_from_anchor := _build_distance_map(cells, map_size, anchor)
	var player_coord := _pick_farthest_coord(largest_component, distance_from_anchor, cells, Vector2i(-1, -1), true)
	if player_coord == Vector2i(-1, -1):
		return {}

	var distance_from_player := _build_distance_map(cells, map_size, player_coord)
	var enemy_coord := _pick_farthest_coord(largest_component, distance_from_player, cells, player_coord, false)
	if enemy_coord == Vector2i(-1, -1):
		return {}

	if int(distance_from_player.get(enemy_coord, 0)) < 4:
		return {}

	return {
		"player_coord": player_coord,
		"enemy_coord": enemy_coord,
	}


func _collect_connected_component(cells: Dictionary, map_size: Vector2i, start: Vector2i, visited: Dictionary) -> Array[Vector2i]:
	var component: Array[Vector2i] = []
	var frontier: Array[Vector2i] = [start]
	visited[start] = true

	while not frontier.is_empty():
		var current: Vector2i = frontier.pop_back()
		component.append(current)

		for neighbor in _get_neighbors_4(map_size, current):
			if visited.has(neighbor):
				continue
			if not _can_traverse_edge(cells, current, neighbor):
				continue
			visited[neighbor] = true
			frontier.append(neighbor)

	return component


func _build_distance_map(cells: Dictionary, map_size: Vector2i, start: Vector2i) -> Dictionary:
	var distances: Dictionary = {start: 0}
	var frontier: Array[Vector2i] = [start]

	while not frontier.is_empty():
		var current: Vector2i = frontier.pop_front()
		var current_distance := int(distances.get(current, 0))

		for neighbor in _get_neighbors_4(map_size, current):
			if distances.has(neighbor):
				continue
			if not _can_traverse_edge(cells, current, neighbor):
				continue
			distances[neighbor] = current_distance + 1
			frontier.append(neighbor)

	return distances


func _pick_farthest_coord(
	component: Array[Vector2i],
	distances: Dictionary,
	cells: Dictionary,
	excluded_coord: Vector2i,
	prefer_safe_terrain: bool
) -> Vector2i:
	var best_coord := Vector2i(-1, -1)
	var best_score := -1

	for coord in component:
		if coord == excluded_coord:
			continue
		if not distances.has(coord):
			continue

		var cell: Dictionary = cells.get(coord, {})
		var terrain: String = String(cell.get("terrain", TERRAIN_LAND))
		var is_safe_terrain := terrain == TERRAIN_LAND or terrain == TERRAIN_FOREST
		var terrain_bonus := 2 if is_safe_terrain else 0
		if not prefer_safe_terrain:
			terrain_bonus = 1 if is_safe_terrain else 0

		var score := int(distances.get(coord, 0)) * 10 + terrain_bonus
		if score > best_score:
			best_score = score
			best_coord = coord

	return best_coord


func _can_traverse_edge(cells: Dictionary, from_coord: Vector2i, to_coord: Vector2i) -> bool:
	var from_cell: Dictionary = cells.get(from_coord, {})
	var to_cell: Dictionary = cells.get(to_coord, {})
	if from_cell.is_empty() or to_cell.is_empty():
		return false
	if not bool(from_cell.get("passable", false)) or not bool(to_cell.get("passable", false)):
		return false

	var height_diff := absi(int(from_cell.get("height", 0)) - int(to_cell.get("height", 0)))
	return height_diff <= 1


func _count_terrain(cells: Dictionary) -> Dictionary:
	var counts := {
		TERRAIN_LAND: 0,
		TERRAIN_FOREST: 0,
		TERRAIN_WATER: 0,
		TERRAIN_MUD: 0,
		TERRAIN_SPIKE: 0,
	}

	for cell in cells.values():
		var terrain: String = String(cell.get("terrain", TERRAIN_LAND))
		counts[terrain] = int(counts.get(terrain, 0)) + 1

	return counts


func _build_fallback_battle(encounter_context: Dictionary, battle_seed: int) -> Dictionary:
	var map_size := Vector2i(11, 9)
	var cells: Dictionary = {}
	var initial_action_points := maxi(int(encounter_context.get("action_points", DEFAULT_MOVE_POINTS)), 1)

	for coord in _collect_all_coords(map_size):
		var terrain := TERRAIN_LAND
		var height := 1
		if coord.x == 5 and coord.y >= 2 and coord.y <= 6:
			terrain = TERRAIN_WATER
			height = 0
		elif coord.x >= 7 and coord.y >= 1 and coord.y <= 3:
			terrain = TERRAIN_FOREST
			height = 2
		elif coord.x >= 2 and coord.x <= 3 and coord.y >= 5:
			terrain = TERRAIN_MUD
			height = 1
		elif coord.x >= 8 and coord.y >= 6:
			terrain = TERRAIN_SPIKE
			height = 3

		cells[coord] = {
			"coord": coord,
			"terrain": terrain,
			"height": height,
			"passable": terrain != TERRAIN_WATER,
			"move_cost": 2 if terrain == TERRAIN_MUD or terrain == TERRAIN_SPIKE else 1,
		}

	return {
		"seed": battle_seed,
		"size": map_size,
		"cells": cells,
		"terrain_counts": _count_terrain(cells),
		"player_coord": Vector2i(1, 1),
		"enemy_coord": Vector2i(9, 7),
		"selected_coord": Vector2i(1, 1),
		"remaining_move": initial_action_points,
		"max_move": initial_action_points,
		"action_points": initial_action_points,
		"player_attributes": encounter_context.get("player_attributes", {}).duplicate(true),
		"monster": encounter_context.get("monster", {}).duplicate(true),
		"world_coord": encounter_context.get("world_coord", Vector2i.ZERO),
	}


func _pick_water_seed(map_size: Vector2i) -> Vector2i:
	if _rng.randf() < 0.3:
		if _rng.randf() < 0.5:
			return Vector2i(_rng.randi_range(0, map_size.x - 1), 0 if _rng.randf() < 0.5 else map_size.y - 1)
		return Vector2i(0 if _rng.randf() < 0.5 else map_size.x - 1, _rng.randi_range(0, map_size.y - 1))

	return Vector2i(
		_rng.randi_range(1, map_size.x - 2),
		_rng.randi_range(1, map_size.y - 2)
	)


func _collect_all_coords(map_size: Vector2i) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for y in range(map_size.y):
		for x in range(map_size.x):
			coords.append(Vector2i(x, y))
	return coords


func _pick_random_coord(map_size: Vector2i) -> Vector2i:
	return Vector2i(
		_rng.randi_range(0, map_size.x - 1),
		_rng.randi_range(0, map_size.y - 1)
	)


func _get_neighbors_4(map_size: Vector2i, coord: Vector2i) -> Array[Vector2i]:
	var neighbors: Array[Vector2i] = []
	var directions: Array[Vector2i] = [
		Vector2i.LEFT,
		Vector2i.RIGHT,
		Vector2i.UP,
		Vector2i.DOWN,
	]

	for direction in directions:
		var candidate := coord + direction
		if candidate.x < 0 or candidate.y < 0 or candidate.x >= map_size.x or candidate.y >= map_size.y:
			continue
		neighbors.append(candidate)

	return neighbors


func _get_neighbors_8(map_size: Vector2i, coord: Vector2i) -> Array[Vector2i]:
	var neighbors: Array[Vector2i] = []
	for y_offset in range(-1, 2):
		for x_offset in range(-1, 2):
			if x_offset == 0 and y_offset == 0:
				continue
			var candidate := coord + Vector2i(x_offset, y_offset)
			if candidate.x < 0 or candidate.y < 0 or candidate.x >= map_size.x or candidate.y >= map_size.y:
				continue
			neighbors.append(candidate)
	return neighbors


func _shuffle_array(values: Array) -> void:
	for index in range(values.size() - 1, 0, -1):
		var swap_index := _rng.randi_range(0, index)
		var current = values[index]
		values[index] = values[swap_index]
		values[swap_index] = current
