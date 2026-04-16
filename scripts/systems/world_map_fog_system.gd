## 文件说明：该脚本属于世界地图迷雾系统相关的系统脚本，集中维护世界尺寸（格子）、按阵营索引的状态集合等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name WorldMapFogSystem
extends RefCounted

const WORLD_MAP_FOG_FACTION_STATE_SCRIPT = preload("res://scripts/systems/world_map_fog_faction_state.gd")

const FOG_UNEXPLORED := 0
const FOG_EXPLORED := 1
const FOG_VISIBLE := 2

## 字段说明：保存世界尺寸（格子），便于顺序遍历、批量展示、批量运算和整体重建。
var _world_size_cells := Vector2i.ZERO
## 字段说明：缓存按阵营索引的状态集合字典，内部 value 使用 WorldMapFogFactionState。
var _states_by_faction: Dictionary = {}


func setup(world_size_cells: Vector2i) -> void:
	_world_size_cells = world_size_cells
	_states_by_faction.clear()


func rebuild_visibility_for_faction(faction_id: String, sources: Array) -> void:
	var faction_state = _get_or_create_state(faction_id)
	faction_state.clear_visible()

	for source in sources:
		for offset_y in range(-source.range, source.range + 1):
			for offset_x in range(-source.range, source.range + 1):
				if abs(offset_x) + abs(offset_y) > source.range:
					continue

				var coord: Vector2i = source.center + Vector2i(offset_x, offset_y)
				if not _is_inside_world(coord):
					continue

				faction_state.mark_visible(coord)


func mark_explored(coord: Vector2i, faction_id: String) -> void:
	if not _is_inside_world(coord):
		return
	_get_or_create_state(faction_id).explored[coord] = true


func reveal_diamond(center: Vector2i, reveal_range: int, faction_id: String) -> Array[Vector2i]:
	var revealed_coords: Array[Vector2i] = []
	var radius := maxi(reveal_range, 0)
	var faction_state = _get_or_create_state(faction_id)
	for offset_y in range(-radius, radius + 1):
		for offset_x in range(-radius, radius + 1):
			if abs(offset_x) + abs(offset_y) > radius:
				continue
			var coord := center + Vector2i(offset_x, offset_y)
			if not _is_inside_world(coord):
				continue
			faction_state.explored[coord] = true
			revealed_coords.append(coord)
	return revealed_coords


func is_visible(coord: Vector2i, faction_id: String) -> bool:
	return _get_or_create_state(faction_id).is_visible(coord)


func is_explored(coord: Vector2i, faction_id: String) -> bool:
	return _get_or_create_state(faction_id).is_explored(coord)


func get_fog_state(coord: Vector2i, faction_id: String) -> int:
	if is_visible(coord, faction_id):
		return FOG_VISIBLE
	if is_explored(coord, faction_id):
		return FOG_EXPLORED
	return FOG_UNEXPLORED


func _get_or_create_state(faction_id: String):
	if _states_by_faction.has(faction_id):
		var existing_state: Variant = _states_by_faction[faction_id]
		if existing_state is Object and existing_state.has_method("clear_visible"):
			return existing_state

	var state = WORLD_MAP_FOG_FACTION_STATE_SCRIPT.new()
	_states_by_faction[faction_id] = state
	return state


func _is_inside_world(coord: Vector2i) -> bool:
	return coord.x >= 0 and coord.y >= 0 and coord.x < _world_size_cells.x and coord.y < _world_size_cells.y
