## 文件说明：该脚本属于战斗地形生成器相关的业务脚本，集中维护地形生成、网格服务、随机数生成器等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleTerrainGenerator
extends RefCounted

const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle_cell_state.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleEdgeFeatureState = preload("res://scripts/systems/battle_edge_feature_state.gd")
const BATTLE_GRID_SERVICE_SCRIPT = preload("res://scripts/systems/battle_grid_service.gd")
const BATTLE_EDGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle_edge_service.gd")
const BATTLE_TERRAIN_TOPOLOGY_SERVICE_SCRIPT = preload("res://scripts/systems/battle_terrain_topology_service.gd")
const BattleEdgeService = preload("res://scripts/systems/battle_edge_service.gd")
const BattleTerrainRules = preload("res://scripts/systems/battle_terrain_rules.gd")
const BattleBoardPropCatalog = preload("res://scripts/utils/battle_board_prop_catalog.gd")

const TERRAIN_LAND := &"land"
const TERRAIN_FOREST := &"forest"
const TERRAIN_WATER := &"water"
const TERRAIN_SHALLOW_WATER := &"shallow_water"
const TERRAIN_FLOWING_WATER := &"flowing_water"
const TERRAIN_DEEP_WATER := &"deep_water"
const TERRAIN_MUD := &"mud"
const TERRAIN_SPIKE := &"spike"
const PROFILE_DEFAULT := &"default"
const PROFILE_CANYON := &"canyon"
const DEFAULT_MIN_HEIGHT := 4
const DEFAULT_MAX_HEIGHT := 8
const DEFAULT_FORMAL_SIZES := [
	Vector2i(11, 9),
	Vector2i(13, 9),
	Vector2i(13, 11),
	Vector2i(15, 11),
]
const CANYON_TEST_SIZE := Vector2i(10, 10)
const CANYON_MIN_HEIGHT := 4
const CANYON_MAX_HEIGHT := 8
const CANYON_FORMAL_SIZES := [
	Vector2i(19, 11),
	Vector2i(21, 13),
	Vector2i(23, 13),
]

## 字段说明：记录网格服务，会参与运行时状态流转、系统协作和存档恢复。
var _grid_service := BATTLE_GRID_SERVICE_SCRIPT.new()
## 字段说明：缓存随机数生成器实例，保证生成逻辑集中使用同一套随机来源并保持可复现性。
var _rng := RandomNumberGenerator.new()
## 字段说明：缓存边缘面服务实例，统一生成器内的连通性、出生点和部署位边规则。
var _edge_service: BattleEdgeService = BATTLE_EDGE_SERVICE_SCRIPT.new()
## 字段说明：缓存水体拓扑服务实例，用于把水体重分类为浅水/流水/深水。
var _terrain_topology_service = BATTLE_TERRAIN_TOPOLOGY_SERVICE_SCRIPT.new()
## 字段说明：缓存不同地图尺寸下的坐标列表，避免生成链路重复构建同一批 Vector2i。
var _coord_cache: Dictionary = {}


func generate(encounter_anchor_or_context, seed: int = 0, context: Dictionary = {}) -> Dictionary:
	var encounter_context := _build_encounter_context(encounter_anchor_or_context, seed, context)
	var terrain_profile_id := _resolve_terrain_profile_id(encounter_context, context)
	if terrain_profile_id == &"":
		return {}
	if terrain_profile_id == PROFILE_CANYON:
		return _generate_canyon(encounter_context, terrain_profile_id)
	return _generate_default(encounter_context, terrain_profile_id)


func _build_encounter_context(encounter_anchor_or_context, seed: int, context: Dictionary) -> Dictionary:
	if encounter_anchor_or_context is Dictionary and seed == 0 and context.is_empty():
		return (encounter_anchor_or_context as Dictionary).duplicate(true)

	var encounter_anchor = encounter_anchor_or_context
	return {
		"monster": {
			"entity_id": String(encounter_anchor.entity_id),
			"display_name": encounter_anchor.display_name,
			"faction_id": String(encounter_anchor.faction_id),
			"region_tag": String(encounter_anchor.region_tag),
		},
		"world_coord": context.get("world_coord", encounter_anchor.world_coord),
		"world_seed": seed,
		"action_points": context.get("action_points", 6),
		"battle_terrain_profile": context.get("battle_terrain_profile", ""),
		"battle_map_size": context.get("battle_map_size", null),
		"battle_test_vertical_slice": context.get("battle_test_vertical_slice", false),
	}


func _generate_default(encounter_context: Dictionary, terrain_profile_id: StringName) -> Dictionary:
	var battle_seed := _build_battle_seed(encounter_context)
	_rng.seed = battle_seed

	for attempt in range(12):
		_rng.seed = battle_seed + attempt * 1013
		var map_size: Vector2i = DEFAULT_FORMAL_SIZES[_rng.randi_range(0, DEFAULT_FORMAL_SIZES.size() - 1)]
		var cells := _build_default_cells(map_size)
		var cell_columns := BattleCellState.build_columns_from_surface_cells(cells)
		var edge_faces := _edge_service.build_edge_faces_for_cells(cells, map_size, cell_columns)
		var spawn_pair := _find_spawn_pair(cells, map_size, edge_faces)
		if spawn_pair.is_empty():
			continue

		return {
			"map_size": map_size,
			"cells": cells,
			"cell_columns": cell_columns,
			"terrain_counts": _count_terrain_cells(cells),
			"ally_spawns": _collect_spawn_ring(cells, spawn_pair.get("player_coord", Vector2i.ZERO), edge_faces),
			"enemy_spawns": _collect_spawn_ring(cells, spawn_pair.get("enemy_coord", Vector2i.ZERO), edge_faces),
			"player_coord": spawn_pair.get("player_coord", Vector2i.ZERO),
			"enemy_coord": spawn_pair.get("enemy_coord", Vector2i.ZERO),
			"terrain_profile_id": terrain_profile_id,
		}

	return {}


func _build_default_cells(map_size: Vector2i) -> Dictionary:
	var heights := _generate_default_heights(map_size)
	var terrain_by_coord := _generate_default_terrain(map_size)
	var water_cells := _generate_default_water_cells(map_size, heights)
	var cells: Dictionary = {}

	for coord in water_cells.keys():
		terrain_by_coord[coord] = TERRAIN_WATER
	_normalize_water_heights(heights, water_cells)
	_apply_default_hazard_terrain(terrain_by_coord, map_size)

	for coord in _collect_all_coords(map_size):
		var terrain := StringName(String(terrain_by_coord.get(coord, TERRAIN_LAND)))
		var cell_state := BATTLE_CELL_STATE_SCRIPT.new()
		cell_state.coord = coord
		cell_state.base_height = int(heights.get(coord, DEFAULT_MIN_HEIGHT))
		cell_state.base_terrain = terrain
		cell_state.height_offset = 0
		cell_state.prop_ids = []
		cell_state.terrain_effect_ids = []
		_grid_service.recalculate_cell(cell_state)
		cells[coord] = cell_state

	_finalize_water_terrain(cells, map_size)
	return cells


func _generate_default_heights(map_size: Vector2i) -> Dictionary:
	var heights: Dictionary = {}
	var all_coords := _collect_all_coords(map_size)

	for coord in all_coords:
		heights[coord] = _rng.randi_range(DEFAULT_MIN_HEIGHT, DEFAULT_MIN_HEIGHT + 2)

	for _smooth_pass in range(2):
		var smoothed: Dictionary = {}
		for coord in all_coords:
			var total := int(heights.get(coord, DEFAULT_MIN_HEIGHT))
			var count := 1
			for neighbor in _get_neighbors_8(map_size, coord):
				total += int(heights.get(neighbor, DEFAULT_MIN_HEIGHT))
				count += 1

			var next_height := int(round(float(total) / float(count)))
			if _rng.randf() < 0.16:
				next_height += _rng.randi_range(-1, 1)
			smoothed[coord] = clampi(next_height, DEFAULT_MIN_HEIGHT, DEFAULT_MAX_HEIGHT - 1)
		heights = smoothed

	for _ridge_index in range(3):
		var ridge_center := _pick_random_coord(map_size)
		var ridge_radius := _rng.randi_range(1, 2)
		var ridge_height := _rng.randi_range(1, 2)
		for coord in all_coords:
			var dx := absi(coord.x - ridge_center.x)
			var dy := absi(coord.y - ridge_center.y)
			if maxi(dx, dy) <= ridge_radius:
				heights[coord] = clampi(int(heights.get(coord, DEFAULT_MIN_HEIGHT)) + ridge_height, DEFAULT_MIN_HEIGHT, DEFAULT_MAX_HEIGHT)

	for _valley_index in range(1):
		var valley_center := _pick_random_coord(map_size)
		for coord in all_coords:
			var distance := absi(coord.x - valley_center.x) + absi(coord.y - valley_center.y)
			if distance <= 2:
				heights[coord] = clampi(int(heights.get(coord, DEFAULT_MIN_HEIGHT)) - 1, DEFAULT_MIN_HEIGHT, DEFAULT_MAX_HEIGHT)

	return heights


func _generate_default_terrain(map_size: Vector2i) -> Dictionary:
	var terrain_by_coord: Dictionary = {}
	for coord in _collect_all_coords(map_size):
		terrain_by_coord[coord] = TERRAIN_FOREST if _rng.randf() < 0.28 else TERRAIN_LAND
	return terrain_by_coord


func _generate_default_water_cells(map_size: Vector2i, heights: Dictionary) -> Dictionary:
	var water_cells: Dictionary = {}
	var region_count := _rng.randi_range(1, 2)
	var target_max := maxi(int(round(float(map_size.x * map_size.y) * 0.08)), 4)

	for _region_index in range(region_count):
		var start := _pick_water_seed_from_heights(map_size, heights, water_cells)
		var frontier: Array[Vector2i] = [start]
		var local_visited: Dictionary = {}
		var target_size := _rng.randi_range(4, target_max)
		var region_size := 0

		while not frontier.is_empty() and region_size < target_size:
			var current: Vector2i = frontier.pop_front()
			if local_visited.has(current):
				continue
			local_visited[current] = true
			if water_cells.has(current):
				continue

			water_cells[current] = true
			region_size += 1

			var neighbors := _get_neighbors_4(map_size, current)
			_shuffle_array(neighbors)
			var current_h := int(heights.get(current, DEFAULT_MIN_HEIGHT))
			for neighbor in neighbors:
				if local_visited.has(neighbor) or water_cells.has(neighbor):
					continue
				var neighbor_h := int(heights.get(neighbor, DEFAULT_MIN_HEIGHT))
				if neighbor_h > current_h + 1:
					continue
				if _rng.randf() <= 0.72:
					frontier.append(neighbor)

		if water_cells.size() >= target_max * region_count:
			break

	return water_cells


func _normalize_water_heights(heights: Dictionary, water_cells: Dictionary) -> void:
	if water_cells.is_empty():
		return
	var visited: Dictionary = {}
	var neighbor_offsets: Array[Vector2i] = [
		Vector2i.LEFT,
		Vector2i.RIGHT,
		Vector2i.UP,
		Vector2i.DOWN,
	]
	for coord_variant in water_cells.keys():
		if coord_variant is not Vector2i:
			continue
		var start: Vector2i = coord_variant
		if visited.has(start):
			continue

		var frontier: Array[Vector2i] = [start]
		var component: Array[Vector2i] = []
		var min_height := DEFAULT_MAX_HEIGHT

		while not frontier.is_empty():
			var current: Vector2i = frontier.pop_front()
			if visited.has(current) or not water_cells.has(current):
				continue
			visited[current] = true
			component.append(current)
			min_height = mini(min_height, int(heights.get(current, DEFAULT_MAX_HEIGHT)))

			for offset in neighbor_offsets:
				var neighbor := current + offset
				if not visited.has(neighbor) and water_cells.has(neighbor):
					frontier.append(neighbor)

		for coord in component:
			heights[coord] = min_height


func _apply_default_hazard_terrain(terrain_by_coord: Dictionary, map_size: Vector2i) -> void:
	var candidates: Array[Vector2i] = []
	for coord in _collect_all_coords(map_size):
		var terrain := StringName(String(terrain_by_coord.get(coord, TERRAIN_LAND)))
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
		if StringName(String(terrain_by_coord.get(neighbor, TERRAIN_LAND))) == TERRAIN_WATER:
			return true
	return false


func _generate_canyon(encounter_context: Dictionary, terrain_profile_id: StringName) -> Dictionary:
	var battle_seed := _build_battle_seed(encounter_context)
	_rng.seed = battle_seed
	var map_size := _resolve_canyon_map_size(encounter_context)

	for attempt in range(8):
		_rng.seed = battle_seed + attempt * 1777
		var cells := _build_canyon_cells(map_size)
		_generate_walls(cells, map_size)
		var cell_columns := BattleCellState.build_columns_from_surface_cells(cells)
		var edge_faces := _edge_service.build_edge_faces_for_cells(cells, map_size, cell_columns)
		var spawn_pair := _find_spawn_pair(cells, map_size, edge_faces)
		if spawn_pair.is_empty():
			continue
		_populate_canyon_props(
			cells,
			map_size,
			spawn_pair.get("player_coord", Vector2i.ZERO),
			spawn_pair.get("enemy_coord", Vector2i.ZERO)
		)

		return {
			"map_size": map_size,
			"cells": cells,
			"cell_columns": cell_columns,
			"terrain_counts": _count_terrain_cells(cells),
			"ally_spawns": _collect_spawn_ring(cells, spawn_pair.get("player_coord", Vector2i.ZERO), edge_faces),
			"enemy_spawns": _collect_spawn_ring(cells, spawn_pair.get("enemy_coord", Vector2i.ZERO), edge_faces),
			"player_coord": spawn_pair.get("player_coord", Vector2i.ZERO),
			"enemy_coord": spawn_pair.get("enemy_coord", Vector2i.ZERO),
			"terrain_profile_id": terrain_profile_id,
		}

	return {}


func _build_canyon_cells(map_size: Vector2i) -> Dictionary:
	var heights := _generate_canyon_heights(map_size)
	var terrain_by_coord := _generate_canyon_terrain(heights, map_size)
	var cells: Dictionary = {}

	for coord in _collect_all_coords(map_size):
		var cell_state := BATTLE_CELL_STATE_SCRIPT.new()
		cell_state.coord = coord
		cell_state.base_height = int(heights.get(coord, 0))
		cell_state.base_terrain = StringName(String(terrain_by_coord.get(coord, TERRAIN_LAND)))
		cell_state.height_offset = 0
		cell_state.prop_ids = []
		cell_state.terrain_effect_ids = []
		_grid_service.recalculate_cell(cell_state)
		cells[coord] = cell_state

	_finalize_water_terrain(cells, map_size)
	return cells


func _generate_canyon_heights(map_size: Vector2i) -> Dictionary:
	var heights: Dictionary = {}
	var center_x := float(map_size.x - 1) * 0.5 + _rng.randf_range(-0.35, 0.35)
	var half_width := maxf(float(map_size.x - 1) * 0.5, 1.0)
	var all_coords := _collect_all_coords(map_size)

	for coord in all_coords:
		var lateral_ratio := absf(float(coord.x) - center_x) / half_width
		var wall_bias := clampf(pow(lateral_ratio, 0.82) * 4.1, 0.0, 4.0)
		var ridge_wave := sin((float(coord.y) * 0.55) + _rng.randf_range(-0.3, 0.3)) * 0.45
		var noise := _rng.randf_range(-0.45, 0.45)
		heights[coord] = clampi(
			int(round(float(CANYON_MIN_HEIGHT) + wall_bias + ridge_wave + noise)),
			CANYON_MIN_HEIGHT,
			CANYON_MAX_HEIGHT
		)

	for _smooth_pass in range(2):
		var smoothed: Dictionary = {}
		for coord in all_coords:
			var total := int(heights.get(coord, CANYON_MIN_HEIGHT))
			var count := 1
			for neighbor in _get_neighbors_4(map_size, coord):
				total += int(heights.get(neighbor, CANYON_MIN_HEIGHT))
				count += 1
			var averaged_height := int(round(float(total) / float(count)))
			var lateral_ratio := absf(float(coord.x) - center_x) / half_width
			if lateral_ratio < 0.22:
				averaged_height = mini(averaged_height, CANYON_MIN_HEIGHT + 1)
			elif lateral_ratio > 0.75:
				averaged_height = maxi(averaged_height, CANYON_MIN_HEIGHT + 2)
			smoothed[coord] = clampi(averaged_height, CANYON_MIN_HEIGHT, CANYON_MAX_HEIGHT)
		heights = smoothed

	return heights


func _generate_canyon_terrain(heights: Dictionary, map_size: Vector2i) -> Dictionary:
	var terrain_by_coord: Dictionary = {}
	var coords := _collect_mutable_coords(map_size)
	_shuffle_array(coords)

	for coord in coords:
		terrain_by_coord[coord] = TERRAIN_LAND

	var total_cells := maxi(map_size.x * map_size.y, 1)
	var water_target := maxi(int(round(float(total_cells) * _rng.randf_range(0.04, 0.07))), 4)
	var scrub_target := maxi(int(round(float(total_cells) * _rng.randf_range(0.03, 0.06))), 3)
	var mud_target := maxi(int(round(float(total_cells) * _rng.randf_range(0.06, 0.10))), 4)
	var rubble_target := maxi(int(round(float(total_cells) * _rng.randf_range(0.06, 0.10))), 4)
	var used_coords: Dictionary = {}

	_generate_canyon_water(terrain_by_coord, heights, map_size, water_target, used_coords)
	_assign_terrain_from_candidates(
		terrain_by_coord,
		_build_mud_candidates(coords, heights, terrain_by_coord, map_size),
		mud_target,
		TERRAIN_MUD,
		used_coords
	)
	_assign_terrain_from_candidates(
		terrain_by_coord,
		_build_scrub_candidates(coords, heights, terrain_by_coord),
		scrub_target,
		TERRAIN_FOREST,
		used_coords
	)
	_assign_terrain_from_candidates(
		terrain_by_coord,
		_build_rubble_candidates(coords, heights, terrain_by_coord, map_size),
		rubble_target,
		TERRAIN_SPIKE,
		used_coords
	)

	return terrain_by_coord


func _generate_canyon_water(
	terrain_by_coord: Dictionary,
	heights: Dictionary,
	map_size: Vector2i,
	target_count: int,
	used_coords: Dictionary
) -> void:
	var center_x := float(map_size.x - 1) * 0.5
	var lateral_limit := float(map_size.x) * 0.18 + 0.5
	var best_seed := Vector2i(-1, -1)
	var best_height := CANYON_MAX_HEIGHT + 1

	for coord in _collect_all_coords(map_size):
		if coord.x <= 0 or coord.y <= 0 or coord.x >= map_size.x - 1 or coord.y >= map_size.y - 1:
			continue
		if absf(float(coord.x) - center_x) > lateral_limit:
			continue
		var h := int(heights.get(coord, CANYON_MAX_HEIGHT))
		if h < best_height:
			best_height = h
			best_seed = coord

	if best_seed == Vector2i(-1, -1):
		return

	var frontier: Array[Vector2i] = [best_seed]
	var visited: Dictionary = {}
	var placed := 0

	while not frontier.is_empty() and placed < target_count:
		var current: Vector2i = frontier.pop_front()
		if visited.has(current):
			continue
		visited[current] = true
		if used_coords.has(current):
			continue

		terrain_by_coord[current] = TERRAIN_WATER
		used_coords[current] = true
		placed += 1

		var neighbors := _get_neighbors_4(map_size, current)
		_shuffle_array(neighbors)
		for neighbor in neighbors:
			if visited.has(neighbor) or used_coords.has(neighbor):
				continue
			if neighbor.x <= 0 or neighbor.y <= 0 or neighbor.x >= map_size.x - 1 or neighbor.y >= map_size.y - 1:
				continue
			var nh := int(heights.get(neighbor, CANYON_MAX_HEIGHT))
			if nh > best_height + 1:
				continue
			if absf(float(neighbor.x) - center_x) > lateral_limit:
				continue
			if _rng.randf() <= 0.80:
				frontier.append(neighbor)


func _build_mud_candidates(
	coords: Array[Vector2i],
	heights: Dictionary,
	terrain_by_coord: Dictionary,
	map_size: Vector2i
) -> Array[Vector2i]:
	var candidates: Array[Vector2i] = []
	for coord in coords:
		if StringName(String(terrain_by_coord.get(coord, TERRAIN_LAND))) != TERRAIN_LAND:
			continue
		var height_value := int(heights.get(coord, 0))
		if height_value > CANYON_MIN_HEIGHT + 1 and not _is_adjacent_to_terrain(terrain_by_coord, map_size, coord, TERRAIN_WATER):
			continue
		candidates.append(coord)
	return candidates


func _build_scrub_candidates(coords: Array[Vector2i], heights: Dictionary, terrain_by_coord: Dictionary) -> Array[Vector2i]:
	var candidates: Array[Vector2i] = []
	for coord in coords:
		if StringName(String(terrain_by_coord.get(coord, TERRAIN_LAND))) != TERRAIN_LAND:
			continue
		if int(heights.get(coord, 0)) < CANYON_MIN_HEIGHT + 1:
			continue
		candidates.append(coord)
	return candidates


func _build_rubble_candidates(
	coords: Array[Vector2i],
	heights: Dictionary,
	terrain_by_coord: Dictionary,
	map_size: Vector2i
) -> Array[Vector2i]:
	var candidates: Array[Vector2i] = []
	for coord in coords:
		if StringName(String(terrain_by_coord.get(coord, TERRAIN_LAND))) != TERRAIN_LAND:
			continue
		var height_value := int(heights.get(coord, 0))
		if height_value < CANYON_MIN_HEIGHT + 2 and not _is_edge_coord(map_size, coord):
			continue
		candidates.append(coord)
	return candidates


func _assign_terrain_from_candidates(
	terrain_by_coord: Dictionary,
	candidates: Array[Vector2i],
	target_count: int,
	terrain_id: StringName,
	used_coords: Dictionary
) -> void:
	var placed := 0
	for coord in candidates:
		if placed >= target_count:
			return
		if used_coords.has(coord):
			continue
		terrain_by_coord[coord] = terrain_id
		used_coords[coord] = true
		placed += 1


func _find_spawn_pair(cells: Dictionary, map_size: Vector2i, edge_faces: Dictionary = {}) -> Dictionary:
	var visited: Dictionary = {}
	var largest_component: Array[Vector2i] = []
	var resolved_edge_faces := edge_faces if not edge_faces.is_empty() else _edge_service.build_edge_faces_for_cells(cells, map_size, BattleCellState.build_columns_from_surface_cells(cells))

	for coord in _collect_all_coords(map_size):
		if visited.has(coord):
			continue
		var cell := cells.get(coord) as BattleCellState
		if cell == null or not cell.passable:
			continue

		var component := _collect_connected_component(cells, map_size, coord, visited, resolved_edge_faces)
		if component.size() > largest_component.size():
			largest_component = component

	if largest_component.size() < 8:
		return {}

	var anchor: Vector2i = largest_component[_rng.randi_range(0, largest_component.size() - 1)]
	var distance_from_anchor := _build_distance_map(cells, map_size, anchor, resolved_edge_faces)
	var player_coord := _pick_farthest_coord(largest_component, distance_from_anchor, cells, Vector2i(-1, -1), true)
	if player_coord == Vector2i(-1, -1):
		return {}

	var distance_from_player := _build_distance_map(cells, map_size, player_coord, resolved_edge_faces)
	var enemy_coord := _pick_farthest_coord(largest_component, distance_from_player, cells, player_coord, false)
	if enemy_coord == Vector2i(-1, -1):
		return {}
	if int(distance_from_player.get(enemy_coord, 0)) < 4:
		return {}

	return {
		"player_coord": player_coord,
		"enemy_coord": enemy_coord,
	}


func _populate_canyon_props(cells: Dictionary, map_size: Vector2i, player_coord: Vector2i, enemy_coord: Vector2i) -> void:
	for cell_variant in cells.values():
		var cell_state := cell_variant as BattleCellState
		if cell_state == null:
			continue
		cell_state.prop_ids.clear()

	var reserved_coords := _build_spawn_buffer_coords(map_size, player_coord)
	for coord in _build_spawn_buffer_coords(map_size, enemy_coord).keys():
		reserved_coords[coord] = true

	var objective_coord := _pick_objective_marker_coord(cells, map_size, player_coord, enemy_coord)
	if objective_coord != Vector2i(-1, -1):
		_append_prop_id(cells, objective_coord, BattleBoardPropCatalog.PROP_OBJECTIVE_MARKER)
		reserved_coords[objective_coord] = true

	var ally_tent_coord := _pick_spawn_prop_coord(cells, map_size, player_coord, reserved_coords, 1)
	if ally_tent_coord != Vector2i(-1, -1):
		_append_prop_id(cells, ally_tent_coord, BattleBoardPropCatalog.PROP_TENT)
		reserved_coords[ally_tent_coord] = true

	var enemy_tent_coord := _pick_spawn_prop_coord(cells, map_size, enemy_coord, reserved_coords, 2)
	if enemy_tent_coord != Vector2i(-1, -1):
		_append_prop_id(cells, enemy_tent_coord, BattleBoardPropCatalog.PROP_TENT)
		reserved_coords[enemy_tent_coord] = true

	var left_torch_coord := _pick_side_torch_coord(cells, map_size, reserved_coords, true, 3)
	if left_torch_coord != Vector2i(-1, -1):
		_append_prop_id(cells, left_torch_coord, BattleBoardPropCatalog.PROP_TORCH)
		reserved_coords[left_torch_coord] = true

	var right_torch_coord := _pick_side_torch_coord(cells, map_size, reserved_coords, false, 4)
	if right_torch_coord != Vector2i(-1, -1):
		_append_prop_id(cells, right_torch_coord, BattleBoardPropCatalog.PROP_TORCH)


func _pick_objective_marker_coord(
	cells: Dictionary,
	map_size: Vector2i,
	player_coord: Vector2i,
	enemy_coord: Vector2i
) -> Vector2i:
	if player_coord == Vector2i(-1, -1) or enemy_coord == Vector2i(-1, -1):
		return Vector2i(-1, -1)

	var edge_faces := _edge_service.build_edge_faces_for_cells(cells, map_size, BattleCellState.build_columns_from_surface_cells(cells))
	var distance_from_player := _build_distance_map(cells, map_size, player_coord, edge_faces)
	var distance_from_enemy := _build_distance_map(cells, map_size, enemy_coord, edge_faces)
	var board_center := Vector2(float(map_size.x - 1) * 0.5, float(map_size.y - 1) * 0.5)
	var best_coord := Vector2i(-1, -1)
	var best_score := -999999

	for coord in _collect_all_coords(map_size):
		var cell := cells.get(coord) as BattleCellState
		if not _can_host_tent(cell):
			continue
		if coord == player_coord or coord == enemy_coord:
			continue
		if not distance_from_player.has(coord) or not distance_from_enemy.has(coord):
			continue

		var player_distance := int(distance_from_player.get(coord, 0))
		var enemy_distance := int(distance_from_enemy.get(coord, 0))
		var midpoint_score := mini(player_distance, enemy_distance) * 80
		var symmetry_penalty := absi(player_distance - enemy_distance) * 18
		var center_penalty := int(round(Vector2(float(coord.x), float(coord.y)).distance_to(board_center) * 7.0))
		var height_bonus := int(cell.current_height) * 6
		var score := midpoint_score - symmetry_penalty - center_penalty + height_bonus
		if score > best_score:
			best_score = score
			best_coord = coord

	return best_coord


func _pick_spawn_prop_coord(
	cells: Dictionary,
	map_size: Vector2i,
	anchor_coord: Vector2i,
	reserved_coords: Dictionary,
	salt: int
) -> Vector2i:
	var best_coord := Vector2i(-1, -1)
	var best_score := -999999
	for radius in range(2, 5):
		for y in range(anchor_coord.y - radius, anchor_coord.y + radius + 1):
			for x in range(anchor_coord.x - radius, anchor_coord.x + radius + 1):
				var coord := Vector2i(x, y)
				if coord.x < 0 or coord.y < 0 or coord.x >= map_size.x or coord.y >= map_size.y:
					continue
				if reserved_coords.has(coord):
					continue
				var cell := cells.get(coord) as BattleCellState
				if not _can_host_tent(cell):
					continue

				var distance := absi(coord.x - anchor_coord.x) + absi(coord.y - anchor_coord.y)
				var score := 120 - distance * 18 + int(cell.current_height) * 9 + (_stable_coord_hash(coord, salt) % 11)
				if score > best_score:
					best_score = score
					best_coord = coord
	return best_coord


func _pick_side_torch_coord(
	cells: Dictionary,
	map_size: Vector2i,
	reserved_coords: Dictionary,
	prefer_left_side: bool,
	salt: int
) -> Vector2i:
	var best_coord := Vector2i(-1, -1)
	var best_score := -999999
	var midpoint_x := float(map_size.x - 1) * 0.5

	for coord in _collect_all_coords(map_size):
		if reserved_coords.has(coord):
			continue
		if prefer_left_side and float(coord.x) > midpoint_x:
			continue
		if not prefer_left_side and float(coord.x) < midpoint_x:
			continue

		var cell := cells.get(coord) as BattleCellState
		if not _can_host_torch(cell):
			continue

		var edge_bonus := 0
		if coord.x <= 1 or coord.x >= map_size.x - 2:
			edge_bonus += 24
		if coord.y <= 1 or coord.y >= map_size.y - 2:
			edge_bonus += 12
		var height_bonus := int(cell.current_height) * 22
		var cliff_bonus := _measure_visual_drop(cells, map_size, coord) * 9
		var score := edge_bonus + height_bonus + cliff_bonus + (_stable_coord_hash(coord, salt) % 13)
		if score > best_score:
			best_score = score
			best_coord = coord

	return best_coord


func _build_spawn_buffer_coords(map_size: Vector2i, center_coord: Vector2i) -> Dictionary:
	var reserved: Dictionary = {}
	for y in range(center_coord.y - 1, center_coord.y + 2):
		for x in range(center_coord.x - 1, center_coord.x + 2):
			if x < 0 or y < 0 or x >= map_size.x or y >= map_size.y:
				continue
			reserved[Vector2i(x, y)] = true
	return reserved


func _can_host_tent(cell: BattleCellState) -> bool:
	if cell == null or not cell.passable:
		return false
	return BattleTerrainRules.can_host_tent(cell.base_terrain)


func _can_host_torch(cell: BattleCellState) -> bool:
	if cell == null or not cell.passable:
		return false
	return BattleTerrainRules.can_host_torch(cell.base_terrain)


func _measure_visual_drop(cells: Dictionary, map_size: Vector2i, coord: Vector2i) -> int:
	var cell := cells.get(coord) as BattleCellState
	if cell == null:
		return 0
	var best_drop := 0
	for neighbor_coord in _get_neighbors_4(map_size, coord):
		var neighbor := cells.get(neighbor_coord) as BattleCellState
		if neighbor == null:
			continue
		best_drop = maxi(best_drop, int(cell.current_height) - int(neighbor.current_height))
	return maxi(best_drop, 0)


func _append_prop_id(cells: Dictionary, coord: Vector2i, prop_id: StringName) -> void:
	var cell := cells.get(coord) as BattleCellState
	if cell == null or not BattleBoardPropCatalog.is_supported(prop_id):
		return
	if not cell.prop_ids.has(prop_id):
		cell.prop_ids.append(prop_id)


func _stable_coord_hash(coord: Vector2i, salt: int = 0) -> int:
	var hash_value := coord.x * 73856093 + coord.y * 19349663 + salt * 83492791
	return absi(hash_value)


func _collect_connected_component(
	cells: Dictionary,
	map_size: Vector2i,
	start: Vector2i,
	visited: Dictionary,
	edge_faces: Dictionary
) -> Array[Vector2i]:
	var component: Array[Vector2i] = []
	var frontier: Array[Vector2i] = [start]
	visited[start] = true

	while not frontier.is_empty():
		var current: Vector2i = frontier.pop_back()
		component.append(current)

		for neighbor in _get_neighbors_4(map_size, current):
			if visited.has(neighbor):
				continue
			if not _can_traverse_edge(cells, edge_faces, current, neighbor):
				continue
			visited[neighbor] = true
			frontier.append(neighbor)

	return component


func _build_distance_map(cells: Dictionary, map_size: Vector2i, start: Vector2i, edge_faces: Dictionary) -> Dictionary:
	var distances: Dictionary = {start: 0}
	var frontier: Array[Vector2i] = [start]
	var frontier_index := 0

	while frontier_index < frontier.size():
		var current: Vector2i = frontier[frontier_index]
		frontier_index += 1
		var current_distance := int(distances.get(current, 0))
		for neighbor in _get_neighbors_4(map_size, current):
			if distances.has(neighbor):
				continue
			if not _can_traverse_edge(cells, edge_faces, current, neighbor):
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

		var cell := cells.get(coord) as BattleCellState
		if cell == null:
			continue
		var is_safe_terrain := cell.base_terrain == TERRAIN_LAND or cell.base_terrain == TERRAIN_FOREST
		var terrain_bonus := 2 if is_safe_terrain else 0
		if not prefer_safe_terrain:
			terrain_bonus = 1 if is_safe_terrain else 0

		var score := int(distances.get(coord, 0)) * 10 + terrain_bonus
		if score > best_score:
			best_score = score
			best_coord = coord

	return best_coord


func _can_traverse_edge(cells: Dictionary, edge_faces: Dictionary, from_coord: Vector2i, to_coord: Vector2i) -> bool:
	var from_cell := cells.get(from_coord) as BattleCellState
	var to_cell := cells.get(to_coord) as BattleCellState
	if from_cell == null or to_cell == null:
		return false
	if not from_cell.passable or not to_cell.passable:
		return false
	return _edge_service.is_traversable_in_cache(edge_faces, from_coord, to_coord)


func _generate_walls(cells: Dictionary, map_size: Vector2i) -> void:
	var wall_density := _rng.randf_range(0.08, 0.15)
	var all_coords := _collect_all_coords(map_size)

	for coord in all_coords:
		var cell := cells.get(coord) as BattleCellState
		if cell == null or not cell.passable:
			continue

		if _rng.randf() < wall_density:
			var east_coord := coord + Vector2i.RIGHT
			var east_cell := cells.get(east_coord) as BattleCellState
			if east_cell != null and east_cell.passable and cell.current_height == east_cell.current_height:
				if _count_open_edges(cells, coord, map_size) > 2 and _count_open_edges(cells, east_coord, map_size) > 2:
					cell.set_edge_feature(Vector2i.RIGHT, BattleEdgeFeatureState.make_wall())

		if _rng.randf() < wall_density:
			var south_coord := coord + Vector2i.DOWN
			var south_cell := cells.get(south_coord) as BattleCellState
			if south_cell != null and south_cell.passable and cell.current_height == south_cell.current_height:
				if _count_open_edges(cells, coord, map_size) > 2 and _count_open_edges(cells, south_coord, map_size) > 2:
					cell.set_edge_feature(Vector2i.DOWN, BattleEdgeFeatureState.make_wall())


func _count_open_edges(cells: Dictionary, coord: Vector2i, map_size: Vector2i) -> int:
	var open := 0
	var cell := cells.get(coord) as BattleCellState
	if cell == null:
		return 0

	# East edge: edge_feature_east on this cell
	if not _is_edge_closed(cell.edge_feature_east):
		open += 1
	# South edge: edge_feature_south on this cell
	if not _is_edge_closed(cell.edge_feature_south):
		open += 1
	# West edge: edge_feature_east on the cell to the west
	var west_cell := cells.get(coord + Vector2i.LEFT) as BattleCellState
	if west_cell == null or not _is_edge_closed(west_cell.edge_feature_east):
		open += 1
	# North edge: edge_feature_south on the cell to the north
	var north_cell := cells.get(coord + Vector2i.UP) as BattleCellState
	if north_cell == null or not _is_edge_closed(north_cell.edge_feature_south):
		open += 1

	return open


func _is_edge_closed(feature_state: BattleEdgeFeatureState) -> bool:
	return feature_state != null and feature_state.blocks_occupancy


func _count_terrain_cells(cells: Dictionary) -> Dictionary:
	var counts := {
		TERRAIN_LAND: 0,
		TERRAIN_FOREST: 0,
		TERRAIN_SHALLOW_WATER: 0,
		TERRAIN_FLOWING_WATER: 0,
		TERRAIN_DEEP_WATER: 0,
		TERRAIN_MUD: 0,
		TERRAIN_SPIKE: 0,
	}

	for cell_variant in cells.values():
		var cell := cell_variant as BattleCellState
		if cell == null:
			continue
		counts[cell.base_terrain] = int(counts.get(cell.base_terrain, 0)) + 1

	return counts


func _finalize_water_terrain(cells: Dictionary, map_size: Vector2i) -> void:
	var changes: Array[Dictionary] = _terrain_topology_service.reclassify_all_water_terrain(cells, map_size)
	for change in changes:
		var coord: Vector2i = change.get("coord", Vector2i.ZERO)
		var cell := cells.get(coord) as BattleCellState
		if cell == null:
			continue
		cell.base_terrain = change.get("after_terrain", cell.base_terrain)
		cell.flow_direction = change.get("after_flow_direction", Vector2i.ZERO)
		_grid_service.recalculate_cell(cell)


func _collect_spawn_ring(cells: Dictionary, center: Vector2i, edge_faces: Dictionary = {}) -> Array[Vector2i]:
	var coords: Array[Vector2i] = [center]
	var resolved_edge_faces := edge_faces if not edge_faces.is_empty() else _edge_service.build_edge_faces_for_cells(cells, _infer_map_size_from_cells(cells), BattleCellState.build_columns_from_surface_cells(cells))
	for direction in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
		var candidate: Vector2i = center + direction
		var cell: BattleCellState = cells.get(candidate) as BattleCellState
		if cell != null and cell.passable and _can_traverse_edge(cells, resolved_edge_faces, center, candidate):
			coords.append(candidate)
	return coords


func _infer_map_size_from_cells(cells: Dictionary) -> Vector2i:
	var max_x := -1
	var max_y := -1
	for coord_variant in cells.keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		max_x = maxi(max_x, coord.x)
		max_y = maxi(max_y, coord.y)
	return Vector2i(max_x + 1, max_y + 1)


func _resolve_canyon_map_size(encounter_context: Dictionary) -> Vector2i:
	if encounter_context.get("battle_map_size", null) is Vector2i:
		return encounter_context.get("battle_map_size", CANYON_TEST_SIZE)
	if bool(encounter_context.get("battle_test_vertical_slice", false)):
		return CANYON_TEST_SIZE
	return CANYON_FORMAL_SIZES[_rng.randi_range(0, CANYON_FORMAL_SIZES.size() - 1)]


func _resolve_terrain_profile_id(encounter_anchor_or_context, context: Dictionary) -> StringName:
	var raw_profile_id := ""
	if context.has("battle_terrain_profile"):
		raw_profile_id = String(context.get("battle_terrain_profile", ""))
	elif encounter_anchor_or_context is Dictionary:
		var encounter_context: Dictionary = encounter_anchor_or_context
		if encounter_context.has("battle_terrain_profile"):
			raw_profile_id = String(encounter_context.get("battle_terrain_profile", ""))
		else:
			var monster: Variant = encounter_context.get("monster", {})
			if monster is Dictionary:
				raw_profile_id = String(monster.get("region_tag", ""))
	else:
		var encounter_anchor = encounter_anchor_or_context
		if encounter_anchor != null:
			raw_profile_id = String(encounter_anchor.region_tag)

	return _normalize_terrain_profile_id(raw_profile_id)


func _normalize_terrain_profile_id(raw_profile_id: String) -> StringName:
	match raw_profile_id.strip_edges().to_lower():
		"", "default":
			return PROFILE_DEFAULT
		"canyon":
			return PROFILE_CANYON
		_:
			return &""


func _build_battle_seed(encounter_context: Dictionary) -> int:
	var monster: Dictionary = encounter_context.get("monster", {})
	var world_coord: Vector2i = encounter_context.get("world_coord", Vector2i.ZERO)
	var world_seed: int = int(encounter_context.get("world_seed", 0))
	var monster_hash := String(monster.get("entity_id", "wild")).hash()
	return world_seed + monster_hash + world_coord.x * 92821 + world_coord.y * 68917


func _pick_water_seed_from_heights(
	map_size: Vector2i,
	heights: Dictionary,
	occupied_cells: Dictionary = {}
) -> Vector2i:
	var min_height := DEFAULT_MAX_HEIGHT
	var all_coords := _collect_all_coords(map_size)
	for coord in all_coords:
		if occupied_cells.has(coord):
			continue
		min_height = mini(min_height, int(heights.get(coord, DEFAULT_MAX_HEIGHT)))
	var threshold := min_height + 1
	var low_coords: Array[Vector2i] = []
	var open_coords: Array[Vector2i] = []
	for coord in all_coords:
		if occupied_cells.has(coord):
			continue
		open_coords.append(coord)
		if int(heights.get(coord, DEFAULT_MAX_HEIGHT)) <= threshold:
			low_coords.append(coord)
	if low_coords.is_empty():
		if open_coords.is_empty():
			return _pick_random_coord(map_size)
		return open_coords[_rng.randi_range(0, open_coords.size() - 1)]
	return low_coords[_rng.randi_range(0, low_coords.size() - 1)]


func _collect_all_coords(map_size: Vector2i) -> Array[Vector2i]:
	if _coord_cache.has(map_size):
		return _coord_cache.get(map_size, []) as Array[Vector2i]

	var coords: Array[Vector2i] = []
	for y in range(map_size.y):
		for x in range(map_size.x):
			coords.append(Vector2i(x, y))
	_coord_cache[map_size] = coords
	return coords


func _collect_mutable_coords(map_size: Vector2i) -> Array[Vector2i]:
	return _collect_all_coords(map_size).duplicate()


func _pick_random_coord(map_size: Vector2i) -> Vector2i:
	return Vector2i(
		_rng.randi_range(0, map_size.x - 1),
		_rng.randi_range(0, map_size.y - 1)
	)


func _get_neighbors_4(map_size: Vector2i, coord: Vector2i) -> Array[Vector2i]:
	var neighbors: Array[Vector2i] = []
	for direction in [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]:
		var candidate: Vector2i = coord + direction
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


func _is_adjacent_to_terrain(terrain_by_coord: Dictionary, map_size: Vector2i, coord: Vector2i, terrain_id: StringName) -> bool:
	for neighbor in _get_neighbors_4(map_size, coord):
		if StringName(String(terrain_by_coord.get(neighbor, TERRAIN_LAND))) == terrain_id:
			return true
	return false


func _is_edge_coord(map_size: Vector2i, coord: Vector2i) -> bool:
	return coord.x <= 0 or coord.y <= 0 or coord.x >= map_size.x - 1 or coord.y >= map_size.y - 1


func _shuffle_array(values: Array) -> void:
	for index in range(values.size() - 1, 0, -1):
		var swap_index := _rng.randi_range(0, index)
		var current = values[index]
		values[index] = values[swap_index]
		values[swap_index] = current
