class_name WorldMapFogSystem
extends RefCounted

const FOG_UNEXPLORED := 0
const FOG_EXPLORED := 1
const FOG_VISIBLE := 2

var _world_size_cells := Vector2i.ZERO
var _states_by_faction: Dictionary = {}


func setup(world_size_cells: Vector2i) -> void:
	_world_size_cells = world_size_cells
	_states_by_faction.clear()


func rebuild_visibility_for_faction(faction_id: String, sources: Array) -> void:
	var faction_state := _get_or_create_state(faction_id)
	var visible_now: Dictionary = {}

	for source in sources:
		for offset_y in range(-source.range, source.range + 1):
			for offset_x in range(-source.range, source.range + 1):
				if abs(offset_x) + abs(offset_y) > source.range:
					continue

				var coord: Vector2i = source.center + Vector2i(offset_x, offset_y)
				if not _is_inside_world(coord):
					continue

				visible_now[coord] = true
				faction_state["explored"][coord] = true

	faction_state["visible_now"] = visible_now


func is_visible(coord: Vector2i, faction_id: String) -> bool:
	var faction_state := _get_or_create_state(faction_id)
	return faction_state["visible_now"].has(coord)


func is_explored(coord: Vector2i, faction_id: String) -> bool:
	var faction_state := _get_or_create_state(faction_id)
	return faction_state["explored"].has(coord)


func get_fog_state(coord: Vector2i, faction_id: String) -> int:
	if is_visible(coord, faction_id):
		return FOG_VISIBLE
	if is_explored(coord, faction_id):
		return FOG_EXPLORED
	return FOG_UNEXPLORED


func _get_or_create_state(faction_id: String) -> Dictionary:
	if _states_by_faction.has(faction_id):
		return _states_by_faction[faction_id]

	var state := {
		"visible_now": {},
		"explored": {},
	}
	_states_by_faction[faction_id] = state
	return state


func _is_inside_world(coord: Vector2i) -> bool:
	return coord.x >= 0 and coord.y >= 0 and coord.x < _world_size_cells.x and coord.y < _world_size_cells.y
