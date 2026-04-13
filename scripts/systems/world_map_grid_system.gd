## 文件说明：该脚本属于世界地图网格系统相关的系统脚本，集中维护世界尺寸（格子）、区块尺寸、占用格子集合等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name WorldMapGridSystem
extends RefCounted

const WORLD_MAP_CELL_DATA_SCRIPT = preload("res://scripts/utils/world_map_cell_data.gd")

## 字段说明：保存世界尺寸（格子），便于顺序遍历、批量展示、批量运算和整体重建。
var _world_size_cells := Vector2i.ZERO
## 字段说明：记录区块尺寸，用于布局、碰撞、绘制或程序化生成时的尺寸计算。
var _chunk_size := Vector2i.ONE
## 字段说明：缓存占用格子集合字典，集中保存可按键查询的运行时数据。
var _occupied_cells: Dictionary = {}
## 字段说明：缓存占位信息集合字典，集中保存可按键查询的运行时数据。
var _footprints: Dictionary = {}


func setup(world_size_in_chunks: Vector2i, chunk_size: Vector2i) -> void:
	_chunk_size = chunk_size
	_world_size_cells = Vector2i(
		world_size_in_chunks.x * chunk_size.x,
		world_size_in_chunks.y * chunk_size.y
	)
	_occupied_cells.clear()
	_footprints.clear()


func get_world_size_cells() -> Vector2i:
	return _world_size_cells


func get_chunk_size() -> Vector2i:
	return _chunk_size


func get_cell(coord: Vector2i):
	if not is_cell_inside_world(coord):
		return null

	var cell = WORLD_MAP_CELL_DATA_SCRIPT.new(coord, get_chunk_coord(coord), get_terrain_visual_type(coord))
	var occupant_state: Dictionary = _occupied_cells.get(coord, {})
	if not occupant_state.is_empty():
		cell.occupant_id = occupant_state.get("occupant_id", "")
		cell.footprint_root_id = occupant_state.get("footprint_root_id", "")
	return cell


func get_terrain_visual_type(coord: Vector2i) -> String:
	if not is_cell_inside_world(coord):
		return ""
	return _get_default_terrain(coord)


func is_cell_inside_world(coord: Vector2i) -> bool:
	return coord.x >= 0 and coord.y >= 0 and coord.x < _world_size_cells.x and coord.y < _world_size_cells.y


func is_cell_walkable(coord: Vector2i) -> bool:
	return is_cell_inside_world(coord)


func get_occupant_root(coord: Vector2i) -> String:
	var occupant_state: Dictionary = _occupied_cells.get(coord, {})
	return occupant_state.get("footprint_root_id", "")


func get_cells_in_rect(origin: Vector2i, size: Vector2i) -> Array:
	var cells: Array = []

	for y in range(size.y):
		for x in range(size.x):
			var coord := origin + Vector2i(x, y)
			if is_cell_inside_world(coord):
				cells.append(get_cell(coord))

	return cells


func can_place_footprint(origin: Vector2i, size: Vector2i) -> bool:
	if size.x <= 0 or size.y <= 0:
		return false

	for y in range(size.y):
		for x in range(size.x):
			var coord := origin + Vector2i(x, y)
			if not is_cell_inside_world(coord):
				return false

			if _occupied_cells.has(coord):
				return false

	return true


func register_footprint(entity_id: String, origin: Vector2i, size: Vector2i) -> void:
	_footprints[entity_id] = {
		"origin": origin,
		"size": size,
	}

	for y in range(size.y):
		for x in range(size.x):
			var coord := origin + Vector2i(x, y)
			_occupied_cells[coord] = {
				"occupant_id": entity_id,
				"footprint_root_id": entity_id,
			}


func clear_footprint(entity_id: String) -> void:
	var footprint: Dictionary = _footprints.get(entity_id, {})
	if footprint.is_empty():
		return

	var origin: Vector2i = footprint.get("origin", Vector2i.ZERO)
	var size: Vector2i = footprint.get("size", Vector2i.ZERO)

	for y in range(size.y):
		for x in range(size.x):
			var coord := origin + Vector2i(x, y)
			var occupant_state: Dictionary = _occupied_cells.get(coord, {})
			if occupant_state.get("footprint_root_id", "") == entity_id:
				_occupied_cells.erase(coord)

	_footprints.erase(entity_id)


func get_neighbors_4(coord: Vector2i) -> Array[Vector2i]:
	var neighbors: Array[Vector2i] = []
	var directions := [
		Vector2i.LEFT,
		Vector2i.RIGHT,
		Vector2i.UP,
		Vector2i.DOWN,
	]

	for direction in directions:
		var candidate: Vector2i = coord + direction
		if is_cell_inside_world(candidate):
			neighbors.append(candidate)

	return neighbors


func get_chunk_coord(coord: Vector2i) -> Vector2i:
	if _chunk_size.x == 0 or _chunk_size.y == 0:
		return Vector2i.ZERO
	return Vector2i(
		int(coord.x / _chunk_size.x),
		int(coord.y / _chunk_size.y)
	)


func _get_default_terrain(coord: Vector2i) -> String:
	var midpoint_x: int = max(int(_world_size_cells.x / 2), 1)
	var midpoint_y: int = max(int(_world_size_cells.y / 2), 1)

	if coord.x < midpoint_x and coord.y < midpoint_y:
		return "plains"
	if coord.x >= midpoint_x and coord.y < midpoint_y:
		return "woods"
	if coord.x < midpoint_x and coord.y >= midpoint_y:
		return "steppe"
	return "highland"
