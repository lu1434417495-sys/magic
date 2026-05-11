## 文件说明：该脚本属于世界地图迷雾系统相关的系统脚本，集中维护世界尺寸（格子）、按阵营索引的状态集合等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name WorldMapFogSystem
extends RefCounted

const WORLD_MAP_FOG_FACTION_STATE_SCRIPT = preload("res://scripts/systems/world/world_map_fog_faction_state.gd")

const FOG_UNEXPLORED := 0
const FOG_EXPLORED := 1
const FOG_VISIBLE := 2
const WORLD_DATA_FOG_STATES_KEY := "fog_states"
const PERSISTENT_STATE_VERSION := 1

## 字段说明：保存世界尺寸（格子），便于顺序遍历、批量展示、批量运算和整体重建。
var _world_size_cells := Vector2i.ZERO
## 字段说明：缓存按阵营索引的状态集合字典，内部 value 使用 WorldMapFogFactionState。
var _states_by_faction: Dictionary = {}
var _revealed_by_faction: Dictionary = {}


func setup(world_size_cells: Vector2i, persistent_state: Dictionary = {}) -> void:
	_world_size_cells = world_size_cells
	_states_by_faction.clear()
	_revealed_by_faction.clear()
	if not persistent_state.is_empty():
		load_persistent_state(persistent_state)


func get_world_size_cells() -> Vector2i:
	return _world_size_cells


func rebuild_visibility_for_faction(faction_id: String, sources: Array) -> void:
	var faction_state = _get_or_create_state(faction_id)
	faction_state.clear_visible()

	for source in sources:
		if source == null or String(source.faction_id) != faction_id:
			continue
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
			_get_revealed_state(faction_id)[coord] = true
			revealed_coords.append(coord)
	return revealed_coords


func is_visible(coord: Vector2i, faction_id: String) -> bool:
	return _get_or_create_state(faction_id).is_visible(coord)


func is_explored(coord: Vector2i, faction_id: String) -> bool:
	return _get_or_create_state(faction_id).is_explored(coord) or _get_revealed_state(faction_id).has(coord)


func get_fog_state(coord: Vector2i, faction_id: String) -> int:
	if is_visible(coord, faction_id):
		return FOG_VISIBLE
	if is_explored(coord, faction_id):
		return FOG_EXPLORED
	return FOG_UNEXPLORED


func export_persistent_state() -> Dictionary:
	var factions: Dictionary = {}
	var faction_ids := _collect_faction_ids()
	faction_ids.sort()
	for faction_id in faction_ids:
		var faction_state = _get_or_create_state(faction_id)
		var revealed_state: Dictionary = _get_revealed_state(faction_id)
		factions[faction_id] = {
			"explored": _serialize_coord_keys(faction_state.explored),
			"revealed": _serialize_coord_keys(revealed_state),
		}
	return {
		"version": PERSISTENT_STATE_VERSION,
		"factions": factions,
	}


func load_persistent_state(persistent_state: Dictionary) -> bool:
	_states_by_faction.clear()
	_revealed_by_faction.clear()
	if persistent_state.is_empty():
		return true
	if not persistent_state.has("version") or persistent_state["version"] is not int:
		push_error("Invalid world fog state: version must be an int.")
		return false
	if int(persistent_state["version"]) != PERSISTENT_STATE_VERSION:
		push_error("Invalid world fog state: unsupported version %s." % str(persistent_state["version"]))
		return false
	if not persistent_state.has("factions") or persistent_state["factions"] is not Dictionary:
		push_error("Invalid world fog state: factions must be a Dictionary.")
		return false

	var next_states: Dictionary = {}
	var next_revealed: Dictionary = {}
	var factions: Dictionary = persistent_state["factions"]
	for faction_key in factions.keys():
		var faction_key_type := typeof(faction_key)
		if faction_key_type != TYPE_STRING and faction_key_type != TYPE_STRING_NAME:
			push_error("Invalid world fog state: faction keys must be String.")
			return false
		var faction_id := String(faction_key).strip_edges()
		if faction_id.is_empty():
			push_error("Invalid world fog state: faction id must be non-empty.")
			return false
		var faction_payload_variant = factions[faction_key]
		if faction_payload_variant is not Dictionary:
			push_error("Invalid world fog state: faction payload must be a Dictionary.")
			return false
		var faction_payload: Dictionary = faction_payload_variant
		if not faction_payload.has("explored") or not faction_payload.has("revealed"):
			push_error("Invalid world fog state: faction payload requires explored and revealed arrays.")
			return false
		var explored_result := _parse_coord_array(faction_payload["explored"])
		if not bool(explored_result.get("ok", false)):
			push_error("Invalid world fog state: explored must contain current coordinate payloads.")
			return false
		var revealed_result := _parse_coord_array(faction_payload["revealed"])
		if not bool(revealed_result.get("ok", false)):
			push_error("Invalid world fog state: revealed must contain current coordinate payloads.")
			return false
		var faction_state = WORLD_MAP_FOG_FACTION_STATE_SCRIPT.new()
		for coord in explored_result.get("coords", []):
			faction_state.explored[coord] = true
		var revealed_state: Dictionary = {}
		for coord in revealed_result.get("coords", []):
			faction_state.explored[coord] = true
			revealed_state[coord] = true
		next_states[faction_id] = faction_state
		next_revealed[faction_id] = revealed_state
	_states_by_faction = next_states
	_revealed_by_faction = next_revealed
	return true


func _get_or_create_state(faction_id: String):
	if _states_by_faction.has(faction_id):
		var existing_state: Variant = _states_by_faction[faction_id]
		if existing_state is Object and existing_state.has_method("clear_visible"):
			return existing_state

	var state = WORLD_MAP_FOG_FACTION_STATE_SCRIPT.new()
	_states_by_faction[faction_id] = state
	return state


func _get_revealed_state(faction_id: String) -> Dictionary:
	var normalized_faction_id := faction_id.strip_edges()
	if normalized_faction_id.is_empty():
		normalized_faction_id = "neutral"
	if _revealed_by_faction.has(normalized_faction_id) and _revealed_by_faction[normalized_faction_id] is Dictionary:
		return _revealed_by_faction[normalized_faction_id]
	var state: Dictionary = {}
	_revealed_by_faction[normalized_faction_id] = state
	return state


func _collect_faction_ids() -> Array[String]:
	var faction_ids: Array[String] = []
	for faction_key in _states_by_faction.keys():
		var faction_id := String(faction_key).strip_edges()
		if not faction_id.is_empty() and not faction_ids.has(faction_id):
			faction_ids.append(faction_id)
	for faction_key in _revealed_by_faction.keys():
		var faction_id := String(faction_key).strip_edges()
		if not faction_id.is_empty() and not faction_ids.has(faction_id):
			faction_ids.append(faction_id)
	return faction_ids


func _serialize_coord_keys(coord_set: Dictionary) -> Array[Dictionary]:
	var coords: Array[Vector2i] = []
	for coord_variant in coord_set.keys():
		if coord_variant is Vector2i and _is_inside_world(coord_variant):
			coords.append(coord_variant)
	coords.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
		if a.y == b.y:
			return a.x < b.x
		return a.y < b.y
	)
	var serialized: Array[Dictionary] = []
	for coord in coords:
		serialized.append({"x": coord.x, "y": coord.y})
	return serialized


func _parse_coord_array(value: Variant) -> Dictionary:
	var coords: Array[Vector2i] = []
	if value is not Array:
		return {"ok": false, "coords": coords}
	for coord_variant in value:
		if coord_variant is not Dictionary:
			return {"ok": false, "coords": coords}
		var coord_payload: Dictionary = coord_variant
		if not coord_payload.has("x") or not coord_payload.has("y"):
			return {"ok": false, "coords": coords}
		if coord_payload["x"] is not int or coord_payload["y"] is not int:
			return {"ok": false, "coords": coords}
		var coord := Vector2i(int(coord_payload["x"]), int(coord_payload["y"]))
		if not _is_inside_world(coord):
			return {"ok": false, "coords": coords}
		if not coords.has(coord):
			coords.append(coord)
	return {"ok": true, "coords": coords}


func _is_inside_world(coord: Vector2i) -> bool:
	return coord.x >= 0 and coord.y >= 0 and coord.x < _world_size_cells.x and coord.y < _world_size_cells.y
