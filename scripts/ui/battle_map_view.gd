class_name BattleMapView
extends Control

signal cell_clicked(coord: Vector2i)

const BATTLE_MAP_GENERATION_SYSTEM_SCRIPT = preload("res://scripts/systems/battle_map_generation_system.gd")

@export_range(24, 80, 1) var max_cell_size := 54
@export_range(16, 64, 1) var min_cell_size := 28
@export_range(8, 48, 1) var board_padding := 18

var _battle_state: Dictionary = {}
var _selected_coord := Vector2i(-1, -1)


func configure(battle_state: Dictionary, selected_coord: Vector2i) -> void:
	_battle_state = battle_state
	_selected_coord = selected_coord
	queue_redraw()


func _gui_input(event: InputEvent) -> void:
	if _battle_state.is_empty():
		return
	if event is not InputEventMouseButton:
		return

	var mouse_event := event as InputEventMouseButton
	if not mouse_event.pressed or mouse_event.button_index != MOUSE_BUTTON_LEFT:
		return

	var clicked_coord := _local_to_cell(mouse_event.position)
	if clicked_coord == Vector2i(-1, -1):
		return

	cell_clicked.emit(clicked_coord)
	accept_event()


func _draw() -> void:
	if _battle_state.is_empty():
		_draw_placeholder()
		return

	var map_size: Vector2i = _battle_state.get("size", Vector2i.ZERO)
	if map_size == Vector2i.ZERO:
		_draw_placeholder()
		return

	var board_rect := _get_board_rect(map_size)
	draw_rect(board_rect, Color(0.03, 0.05, 0.08, 0.95), true, 1.0)
	draw_rect(board_rect, Color(0.24, 0.36, 0.54, 0.9), false, 2.0)

	for y in range(map_size.y):
		for x in range(map_size.x):
			var coord := Vector2i(x, y)
			var cell: Dictionary = _get_cell(coord)
			if cell.is_empty():
				continue

			var cell_rect := _get_cell_rect(coord)
			var terrain_color := _get_terrain_color(String(cell.get("terrain", BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_LAND)))
			var height: int = int(cell.get("height", 0))
			terrain_color = terrain_color.lightened(min(height * 0.06, 0.24))
			draw_rect(cell_rect, terrain_color)
			draw_rect(cell_rect, Color(0.05, 0.08, 0.12, 0.72), false, 1.0)

			if String(cell.get("terrain", "")) == BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_WATER:
				_draw_water_accent(cell_rect)
			elif String(cell.get("terrain", "")) == BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_FOREST:
				_draw_forest_accent(cell_rect)
			elif String(cell.get("terrain", "")) == BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_MUD:
				_draw_mud_accent(cell_rect)
			elif String(cell.get("terrain", "")) == BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_SPIKE:
				_draw_spike_accent(cell_rect)

			_draw_height_label(cell_rect, height)

	_draw_enemy_marker()
	_draw_player_marker()
	_draw_selection_marker()


func _draw_placeholder() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Color(0.03, 0.05, 0.08, 0.92), true)
	var font := get_theme_default_font()
	if font == null:
		return
	draw_string(
		font,
		Vector2(24, max(size.y * 0.5, 32.0)),
		"等待战斗地图生成",
		HORIZONTAL_ALIGNMENT_LEFT,
		-1.0,
		20,
		Color(0.78, 0.86, 0.95, 0.92)
	)


func _draw_height_label(cell_rect: Rect2, height: int) -> void:
	var font := get_theme_default_font()
	if font == null:
		return

	draw_string(
		font,
		cell_rect.position + Vector2(6, 18),
		str(height),
		HORIZONTAL_ALIGNMENT_LEFT,
		-1.0,
		13,
		Color(0.96, 0.98, 1.0, 0.9)
	)


func _draw_water_accent(cell_rect: Rect2) -> void:
	var inset_rect := cell_rect.grow(-cell_rect.size.x * 0.22)
	draw_rect(inset_rect, Color(0.74, 0.9, 1.0, 0.18))
	draw_line(
		inset_rect.position + Vector2(0, inset_rect.size.y * 0.5),
		inset_rect.position + Vector2(inset_rect.size.x, inset_rect.size.y * 0.5),
		Color(0.9, 0.98, 1.0, 0.28),
		2.0
	)


func _draw_forest_accent(cell_rect: Rect2) -> void:
	var center := cell_rect.get_center()
	draw_circle(center + Vector2(-cell_rect.size.x * 0.12, 0), cell_rect.size.x * 0.1, Color(0.09, 0.2, 0.12, 0.72))
	draw_circle(center + Vector2(cell_rect.size.x * 0.1, -cell_rect.size.y * 0.08), cell_rect.size.x * 0.12, Color(0.09, 0.23, 0.13, 0.74))


func _draw_mud_accent(cell_rect: Rect2) -> void:
	var mud_rect := Rect2(
		cell_rect.position + cell_rect.size * 0.18,
		cell_rect.size * 0.64
	)
	draw_rect(mud_rect, Color(0.29, 0.17, 0.1, 0.34))
	draw_circle(mud_rect.get_center(), cell_rect.size.x * 0.12, Color(0.21, 0.12, 0.06, 0.42))


func _draw_spike_accent(cell_rect: Rect2) -> void:
	var points := PackedVector2Array([
		cell_rect.position + Vector2(cell_rect.size.x * 0.22, cell_rect.size.y * 0.78),
		cell_rect.position + Vector2(cell_rect.size.x * 0.35, cell_rect.size.y * 0.28),
		cell_rect.position + Vector2(cell_rect.size.x * 0.48, cell_rect.size.y * 0.78),
		cell_rect.position + Vector2(cell_rect.size.x * 0.58, cell_rect.size.y * 0.24),
		cell_rect.position + Vector2(cell_rect.size.x * 0.72, cell_rect.size.y * 0.78),
	])
	draw_polyline(points, Color(0.26, 0.05, 0.07, 0.85), 3.0)


func _draw_player_marker() -> void:
	var player_coord: Vector2i = _battle_state.get("player_coord", Vector2i.ZERO)
	var cell_rect := _get_cell_rect(player_coord)
	var center := cell_rect.get_center()
	var points := PackedVector2Array([
		center + Vector2(0, -cell_rect.size.y * 0.28),
		center + Vector2(cell_rect.size.x * 0.18, 0),
		center + Vector2(0, cell_rect.size.y * 0.28),
		center + Vector2(-cell_rect.size.x * 0.18, 0),
	])
	draw_colored_polygon(points, Color(0.98, 0.89, 0.41, 1.0))
	draw_polyline(points + PackedVector2Array([points[0]]), Color(0.19, 0.12, 0.02, 0.98), 2.0)


func _draw_enemy_marker() -> void:
	var enemy_coord: Vector2i = _battle_state.get("enemy_coord", Vector2i.ZERO)
	var cell_rect := _get_cell_rect(enemy_coord)
	var center := cell_rect.get_center()
	draw_circle(center, cell_rect.size.x * 0.2, Color(0.88, 0.23, 0.2, 0.96))
	draw_circle(center, cell_rect.size.x * 0.09, Color(0.18, 0.02, 0.03, 0.98))


func _draw_selection_marker() -> void:
	if _selected_coord == Vector2i(-1, -1):
		return
	if not _has_coord(_selected_coord):
		return

	var selection_rect := _get_cell_rect(_selected_coord).grow(-2.0)
	draw_rect(selection_rect, Color(1.0, 1.0, 1.0, 0.0), false, 2.0)


func _get_cell(coord: Vector2i) -> Dictionary:
	var cells: Dictionary = _battle_state.get("cells", {})
	return cells.get(coord, {})


func _has_coord(coord: Vector2i) -> bool:
	var map_size: Vector2i = _battle_state.get("size", Vector2i.ZERO)
	return coord.x >= 0 and coord.y >= 0 and coord.x < map_size.x and coord.y < map_size.y


func _local_to_cell(position: Vector2) -> Vector2i:
	var map_size: Vector2i = _battle_state.get("size", Vector2i.ZERO)
	var board_rect := _get_board_rect(map_size)
	if not board_rect.has_point(position):
		return Vector2i(-1, -1)

	var runtime_cell_size := _get_runtime_cell_size(map_size)
	var local := position - board_rect.position
	return Vector2i(
		int(floor(local.x / runtime_cell_size)),
		int(floor(local.y / runtime_cell_size))
	)


func _get_cell_rect(coord: Vector2i) -> Rect2:
	var map_size: Vector2i = _battle_state.get("size", Vector2i.ZERO)
	var board_rect := _get_board_rect(map_size)
	var runtime_cell_size := _get_runtime_cell_size(map_size)
	return Rect2(
		board_rect.position + Vector2(coord) * runtime_cell_size,
		Vector2.ONE * runtime_cell_size
	)


func _get_board_rect(map_size: Vector2i) -> Rect2:
	var runtime_cell_size: float = _get_runtime_cell_size(map_size)
	var board_size: Vector2 = Vector2(map_size) * runtime_cell_size
	var origin: Vector2 = (size - board_size) * 0.5
	origin.x = maxf(origin.x, float(board_padding))
	origin.y = maxf(origin.y, float(board_padding))
	return Rect2(origin, board_size)


func _get_runtime_cell_size(map_size: Vector2i) -> float:
	if map_size == Vector2i.ZERO:
		return float(max_cell_size)

	var available_width: float = maxf(size.x - board_padding * 2.0, float(min_cell_size))
	var available_height: float = maxf(size.y - board_padding * 2.0, float(min_cell_size))
	var width_based: float = floor(available_width / float(maxi(map_size.x, 1)))
	var height_based: float = floor(available_height / float(maxi(map_size.y, 1)))
	return clampf(minf(width_based, height_based), float(min_cell_size), float(max_cell_size))


func _get_terrain_color(terrain: String) -> Color:
	match terrain:
		BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_LAND:
			return Color(0.34, 0.45, 0.27, 1.0)
		BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_FOREST:
			return Color(0.18, 0.33, 0.2, 1.0)
		BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_WATER:
			return Color(0.16, 0.31, 0.56, 1.0)
		BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_MUD:
			return Color(0.41, 0.28, 0.18, 1.0)
		BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_SPIKE:
			return Color(0.43, 0.34, 0.36, 1.0)
		_:
			return Color(0.3, 0.35, 0.4, 1.0)
