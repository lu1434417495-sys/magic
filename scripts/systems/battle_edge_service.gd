## 文件说明：该脚本属于战斗边缘面服务相关的服务脚本，集中维护边缓存重建、统一边查询和边阻挡语义。
## 审查重点：重点核对 canonical key、边解析结果、运行时脏标记以及跨系统消费的一致性。
## 备注：该服务只统一“边”的解释，不负责 authoring 数据本身；authoring 仍来源于 cell 高度与 edge feature 字段。

class_name BattleEdgeService
extends RefCounted

const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleEdgeFeatureState = preload("res://scripts/systems/battle_edge_feature_state.gd")
const BattleEdgeFaceState = preload("res://scripts/systems/battle_edge_face_state.gd")

const DIRECTION_EAST := Vector2i.RIGHT
const DIRECTION_SOUTH := Vector2i.DOWN
const DIRECTION_INDEX_EAST := 0
const DIRECTION_INDEX_SOUTH := 1
const BOUNDARY_RENDER_HEIGHT := 0


func ensure_runtime_edge_faces(state: BattleState) -> void:
	if state == null:
		return
	_ensure_cell_columns(state)
	if not bool(state.runtime_edges_dirty):
		if state.runtime_edge_faces is Dictionary and not state.runtime_edge_faces.is_empty():
			return
	state.runtime_edge_faces = build_edge_faces_for_cells(state.cells, state.map_size, state.cell_columns)
	state.runtime_edges_dirty = false


func rebuild_runtime_edge_faces(state: BattleState) -> void:
	if state == null:
		return
	_ensure_cell_columns(state)
	state.runtime_edge_faces = build_edge_faces_for_cells(state.cells, state.map_size, state.cell_columns)
	state.runtime_edges_dirty = false


func mark_runtime_edge_faces_dirty(state: BattleState) -> void:
	if state == null:
		return
	state.runtime_edges_dirty = true


func clear_runtime_edge_faces(state: BattleState) -> void:
	if state == null:
		return
	state.runtime_edge_faces.clear()
	state.runtime_edges_dirty = true


func build_edge_faces_for_cells(cells: Dictionary, map_size: Vector2i, cell_columns: Dictionary = {}) -> Dictionary:
	var edge_faces: Dictionary = {}
	var resolved_columns := cell_columns if not cell_columns.is_empty() else BattleCellState.build_columns_from_surface_cells(cells)
	for y in range(maxi(map_size.y, 0)):
		for x in range(maxi(map_size.x, 0)):
			var origin_coord := Vector2i(x, y)
			var origin_cell := cells.get(origin_coord) as BattleCellState
			if origin_cell == null:
				continue
			edge_faces[_build_edge_key(origin_coord, DIRECTION_INDEX_EAST)] = _build_edge_face(cells, resolved_columns, origin_coord, origin_cell, DIRECTION_EAST)
			edge_faces[_build_edge_key(origin_coord, DIRECTION_INDEX_SOUTH)] = _build_edge_face(cells, resolved_columns, origin_coord, origin_cell, DIRECTION_SOUTH)
	return edge_faces


func get_all_edge_faces(state: BattleState) -> Array[BattleEdgeFaceState]:
	var results: Array[BattleEdgeFaceState] = []
	if state == null:
		return results
	ensure_runtime_edge_faces(state)
	for edge_face_variant in state.runtime_edge_faces.values():
		var edge_face := edge_face_variant as BattleEdgeFaceState
		if edge_face != null:
			results.append(edge_face)
	return results


func get_edge_face(state: BattleState, from_coord: Vector2i, to_coord: Vector2i) -> BattleEdgeFaceState:
	if state == null:
		return null
	ensure_runtime_edge_faces(state)
	return get_edge_face_from_cache(state.runtime_edge_faces, from_coord, to_coord)


func get_edge_face_by_origin(state: BattleState, origin_coord: Vector2i, direction: Vector2i) -> BattleEdgeFaceState:
	if state == null:
		return null
	ensure_runtime_edge_faces(state)
	return state.runtime_edge_faces.get(_build_edge_key(origin_coord, _get_direction_index(direction))) as BattleEdgeFaceState


func get_edge_face_from_cache(edge_faces: Dictionary, from_coord: Vector2i, to_coord: Vector2i) -> BattleEdgeFaceState:
	var lookup := _resolve_lookup_key(from_coord, to_coord)
	if not bool(lookup.get("valid", false)):
		return null
	return edge_faces.get(lookup.get("key", Vector3i.ZERO)) as BattleEdgeFaceState


func is_traversable_between(state: BattleState, from_coord: Vector2i, to_coord: Vector2i, max_height_difference: int = 1) -> bool:
	var edge_face := get_edge_face(state, from_coord, to_coord)
	return is_edge_face_traversable(edge_face, max_height_difference)


func is_traversable_in_cache(edge_faces: Dictionary, from_coord: Vector2i, to_coord: Vector2i, max_height_difference: int = 1) -> bool:
	var edge_face := get_edge_face_from_cache(edge_faces, from_coord, to_coord)
	return is_edge_face_traversable(edge_face, max_height_difference)


func is_edge_face_traversable(edge_face: BattleEdgeFaceState, max_height_difference: int = 1) -> bool:
	if edge_face == null:
		return false
	if edge_face.blocks_move():
		return false
	return edge_face.height_difference <= maxi(max_height_difference, 0)


func blocks_occupancy_between(state: BattleState, from_coord: Vector2i, to_coord: Vector2i, max_height_difference: int = 1) -> bool:
	var edge_face := get_edge_face(state, from_coord, to_coord)
	return blocks_occupancy_for_edge_face(edge_face, max_height_difference)


func blocks_occupancy_in_cache(edge_faces: Dictionary, from_coord: Vector2i, to_coord: Vector2i, max_height_difference: int = 1) -> bool:
	var edge_face := get_edge_face_from_cache(edge_faces, from_coord, to_coord)
	return blocks_occupancy_for_edge_face(edge_face, max_height_difference)


func blocks_occupancy_for_edge_face(edge_face: BattleEdgeFaceState, max_height_difference: int = 1) -> bool:
	if edge_face == null:
		return true
	if edge_face.blocks_occupancy():
		return true
	return edge_face.height_difference > maxi(max_height_difference, 0)


func has_feature_between(state: BattleState, from_coord: Vector2i, to_coord: Vector2i, feature_kind: StringName = BattleEdgeFaceState.FEATURE_WALL) -> bool:
	var edge_face := get_edge_face(state, from_coord, to_coord)
	if edge_face == null:
		return false
	return edge_face.feature_kind == feature_kind


func _build_edge_face(
	cells: Dictionary,
	cell_columns: Dictionary,
	origin_coord: Vector2i,
	origin_cell: BattleCellState,
	direction: Vector2i
) -> BattleEdgeFaceState:
	var edge_face := BattleEdgeFaceState.new()
	var neighbor_coord := origin_coord + direction
	var neighbor_cell := cells.get(neighbor_coord) as BattleCellState
	var from_height := _get_column_top_height(cell_columns.get(origin_coord, []), origin_cell)
	var to_height := _get_column_top_height(cell_columns.get(neighbor_coord, []), neighbor_cell)
	edge_face.origin_coord = origin_coord
	edge_face.neighbor_coord = neighbor_coord
	edge_face.direction = direction
	edge_face.from_height = from_height
	edge_face.to_height = to_height
	edge_face.height_difference = absi(from_height - to_height)
	edge_face.drop_face_layer_heights = _build_exposed_layer_heights(from_height, to_height)
	edge_face.drop_layers = edge_face.drop_face_layer_heights.size()
	match direction:
		DIRECTION_EAST:
			_apply_authored_feature(edge_face, origin_cell.edge_feature_east)
		DIRECTION_SOUTH:
			_apply_authored_feature(edge_face, origin_cell.edge_feature_south)
	return edge_face


func _apply_authored_feature(edge_face: BattleEdgeFaceState, feature_state: BattleEdgeFeatureState) -> void:
	if edge_face == null or feature_state == null or feature_state.is_empty():
		return
	edge_face.feature_kind = feature_state.feature_kind
	edge_face.feature_render_kind = feature_state.render_kind
	edge_face.feature_layers = maxi(int(feature_state.render_layers), 0)
	edge_face.feature_blocks_move = feature_state.blocks_move
	edge_face.feature_blocks_occupancy = feature_state.blocks_occupancy
	edge_face.feature_blocks_los = feature_state.blocks_los
	edge_face.feature_interaction_kind = feature_state.interaction_kind
	edge_face.feature_state_tag = feature_state.state_tag


func _resolve_lookup_key(from_coord: Vector2i, to_coord: Vector2i) -> Dictionary:
	var delta := to_coord - from_coord
	match delta:
		Vector2i.RIGHT:
			return {"valid": true, "key": _build_edge_key(from_coord, DIRECTION_INDEX_EAST)}
		Vector2i.LEFT:
			return {"valid": true, "key": _build_edge_key(to_coord, DIRECTION_INDEX_EAST)}
		Vector2i.DOWN:
			return {"valid": true, "key": _build_edge_key(from_coord, DIRECTION_INDEX_SOUTH)}
		Vector2i.UP:
			return {"valid": true, "key": _build_edge_key(to_coord, DIRECTION_INDEX_SOUTH)}
		_:
			return {"valid": false}


func _build_edge_key(origin_coord: Vector2i, direction_index: int) -> Vector3i:
	return Vector3i(origin_coord.x, origin_coord.y, direction_index)


func _get_direction_index(direction: Vector2i) -> int:
	match direction:
		DIRECTION_EAST:
			return DIRECTION_INDEX_EAST
		DIRECTION_SOUTH:
			return DIRECTION_INDEX_SOUTH
		_:
			return DIRECTION_INDEX_EAST


func _ensure_cell_columns(state: BattleState) -> void:
	if state == null:
		return
	if state.cell_columns.is_empty() and not state.cells.is_empty():
		state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells)


func _get_column_top_height(column_variant: Variant, fallback_surface_cell: BattleCellState = null) -> int:
	if column_variant is Array:
		var column: Array = column_variant
		for index in range(column.size() - 1, -1, -1):
			var layer_cell := column[index] as BattleCellState
			if layer_cell != null:
				return int(layer_cell.stack_layer)
	if fallback_surface_cell != null:
		return int(fallback_surface_cell.current_height)
	return BOUNDARY_RENDER_HEIGHT


func _build_exposed_layer_heights(from_height: int, to_height: int) -> Array[int]:
	var exposed_layers: Array[int] = []
	if from_height <= to_height:
		return exposed_layers
	var lowest_exposed_height := maxi(to_height + 1, 1)
	for layer_height in range(from_height, lowest_exposed_height - 1, -1):
		exposed_layers.append(layer_height)
	return exposed_layers
