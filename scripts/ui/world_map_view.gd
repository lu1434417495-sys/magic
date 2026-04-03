class_name WorldMapView
extends Control

signal cell_clicked(coord: Vector2i)

@export_range(24, 64, 1) var cell_size := 34
@export_range(0, 8, 1) var viewport_padding_cells := 2

const WORLD_MAP_FOG_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_fog_system.gd")
const SETTLEMENT_CONFIG_SCRIPT = preload("res://scripts/utils/settlement_config.gd")

var _grid_system
var _fog_system
var _world_data: Dictionary = {}
var _player_coord := Vector2i.ZERO
var _selected_coord := Vector2i.ZERO
var _player_faction_id := "player"


func configure(grid_system, fog_system, world_data: Dictionary, player_coord: Vector2i, selected_coord: Vector2i, player_faction_id: String) -> void:
	_grid_system = grid_system
	_fog_system = fog_system
	_world_data = world_data
	_player_coord = player_coord
	_selected_coord = selected_coord
	_player_faction_id = player_faction_id
	queue_redraw()


func set_runtime_state(player_coord: Vector2i, selected_coord: Vector2i) -> void:
	_player_coord = player_coord
	_selected_coord = selected_coord
	queue_redraw()


func refresh_world(world_data: Dictionary) -> void:
	_world_data = world_data
	queue_redraw()


func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var mouse_event := event as InputEventMouseButton
		var coord := _local_to_cell(mouse_event.position)
		if _grid_system != null and _grid_system.is_cell_inside_world(coord):
			cell_clicked.emit(coord)
			accept_event()


func _draw() -> void:
	if _grid_system == null or _fog_system == null:
		return

	var visible_rect := _get_visible_world_rect()
	_draw_cells(visible_rect)
	_draw_chunk_lines(visible_rect)
	_draw_settlements(visible_rect)
	_draw_mobile_entities(visible_rect)
	_draw_player(visible_rect)
	_draw_selection(visible_rect)


func _draw_cells(visible_rect: Rect2i) -> void:
	for y in range(visible_rect.position.y, visible_rect.end.y):
		for x in range(visible_rect.position.x, visible_rect.end.x):
			var coord := Vector2i(x, y)
			var rect := _get_cell_rect(coord)
			var base_color := _get_terrain_color(_grid_system.get_terrain_visual_type(coord))
			draw_rect(rect, base_color)

			var fog_state: int = _fog_system.get_fog_state(coord, _player_faction_id)
			if fog_state == WORLD_MAP_FOG_SYSTEM_SCRIPT.FOG_UNEXPLORED:
				draw_rect(rect, Color(0.02, 0.03, 0.06, 0.96))
			elif fog_state == WORLD_MAP_FOG_SYSTEM_SCRIPT.FOG_EXPLORED:
				draw_rect(rect, Color(0.03, 0.05, 0.09, 0.55))

			draw_rect(rect, Color(0.12, 0.16, 0.26, 0.7), false, 1.0)


func _draw_chunk_lines(visible_rect: Rect2i) -> void:
	var chunk_size: Vector2i = _grid_system.get_chunk_size()
	var line_color := Color(0.55, 0.68, 0.93, 0.55)
	var font := get_theme_default_font()
	var font_size := 13

	var start_chunk_x: int = int(floor(float(visible_rect.position.x) / float(chunk_size.x)))
	var end_chunk_x: int = int(floor(float(max(visible_rect.end.x - 1, visible_rect.position.x)) / float(chunk_size.x)))
	var start_chunk_y: int = int(floor(float(visible_rect.position.y) / float(chunk_size.y)))
	var end_chunk_y: int = int(floor(float(max(visible_rect.end.y - 1, visible_rect.position.y)) / float(chunk_size.y)))

	for chunk_x in range(start_chunk_x, end_chunk_x + 2):
		var x := chunk_x * chunk_size.x
		var x_pos := x * cell_size
		var local_x := x_pos - visible_rect.position.x * cell_size
		draw_line(Vector2(local_x, 0), Vector2(local_x, size.y), line_color, 2.0)

	for chunk_y in range(start_chunk_y, end_chunk_y + 2):
		var y := chunk_y * chunk_size.y
		var y_pos := y * cell_size
		var local_y := y_pos - visible_rect.position.y * cell_size
		draw_line(Vector2(0, local_y), Vector2(size.x, local_y), line_color, 2.0)

	if font == null:
		return

	for chunk_y in range(start_chunk_y, end_chunk_y + 1):
		for chunk_x in range(start_chunk_x, end_chunk_x + 1):
			var label_pos := Vector2(
				(chunk_x * chunk_size.x - visible_rect.position.x) * cell_size + 8,
				(chunk_y * chunk_size.y - visible_rect.position.y) * cell_size + 18
			)
			draw_string(font, label_pos, "Chunk %d,%d" % [chunk_x, chunk_y], HORIZONTAL_ALIGNMENT_LEFT, -1.0, font_size, Color(0.82, 0.9, 1.0, 0.85))


func _draw_settlements(visible_rect: Rect2i) -> void:
	var settlements: Array = _world_data.get("settlements", [])
	var font := get_theme_default_font()
	var font_size := 16

	for settlement in settlements:
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var footprint_size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		if not Rect2i(origin, footprint_size).intersects(visible_rect):
			continue
		var fog_state: int = _fog_system.get_fog_state(origin, _player_faction_id)
		if fog_state == WORLD_MAP_FOG_SYSTEM_SCRIPT.FOG_UNEXPLORED:
			continue

		var rect := Rect2(
			Vector2(origin - visible_rect.position) * cell_size,
			Vector2(footprint_size) * cell_size
		).grow(-3.0)
		var color := _get_settlement_color(settlement.get("tier", 0))
		if fog_state == WORLD_MAP_FOG_SYSTEM_SCRIPT.FOG_EXPLORED:
			color = color.darkened(0.45)
			color.a = 0.85

		draw_rect(rect, color)
		draw_rect(rect, Color(0.05, 0.08, 0.14, 0.95), false, 2.0)

		if font == null:
			continue

		var label: String = settlement.get("display_name", "据点")
		var label_pos := rect.position + Vector2(8, min(24, rect.size.y - 6))
		draw_string(font, label_pos, label, HORIZONTAL_ALIGNMENT_LEFT, rect.size.x - 12.0, font_size, Color.WHITE)


func _draw_mobile_entities(visible_rect: Rect2i) -> void:
	var monsters: Array = _world_data.get("wild_monsters", [])
	for monster in monsters:
		var coord: Vector2i = monster.get("coord", Vector2i.ZERO)
		if not visible_rect.has_point(coord):
			continue
		if not _fog_system.is_visible(coord, _player_faction_id):
			continue

		var center := _get_cell_rect(coord).get_center()
		draw_circle(center, cell_size * 0.22, Color(0.87, 0.28, 0.23, 0.95))
		draw_circle(center, cell_size * 0.12, Color(0.15, 0.02, 0.02, 0.95))

	var npcs: Array = _world_data.get("world_npcs", [])
	for npc in npcs:
		var coord: Vector2i = npc.get("coord", Vector2i.ZERO)
		if not visible_rect.has_point(coord):
			continue
		if not _fog_system.is_visible(coord, _player_faction_id):
			continue

		var center := _get_cell_rect(coord).get_center()
		draw_circle(center, cell_size * 0.18, Color(0.42, 0.77, 0.87, 0.95))
		draw_circle(center + Vector2(0, -4), cell_size * 0.06, Color(0.88, 0.94, 0.98, 1.0))


func _draw_player(visible_rect: Rect2i) -> void:
	if not visible_rect.has_point(_player_coord):
		return
	var center := _get_cell_rect(_player_coord).get_center()
	var points := PackedVector2Array([
		center + Vector2(0, -cell_size * 0.26),
		center + Vector2(cell_size * 0.18, 0),
		center + Vector2(0, cell_size * 0.26),
		center + Vector2(-cell_size * 0.18, 0),
	])
	draw_colored_polygon(points, Color(0.97, 0.89, 0.39, 1.0))
	draw_polyline(points + PackedVector2Array([points[0]]), Color(0.18, 0.12, 0.02, 1.0), 2.0)


func _draw_selection(visible_rect: Rect2i) -> void:
	if not visible_rect.has_point(_selected_coord):
		return
	var rect := _get_cell_rect(_selected_coord).grow(-2.0)
	draw_rect(rect, Color(1.0, 1.0, 1.0, 0.0), false, 2.0)


func _get_cell_rect(coord: Vector2i) -> Rect2:
	var visible_rect := _get_visible_world_rect()
	return Rect2(Vector2(coord - visible_rect.position) * cell_size, Vector2.ONE * cell_size)


func _local_to_cell(position: Vector2) -> Vector2i:
	var visible_rect := _get_visible_world_rect()
	return visible_rect.position + Vector2i(floor(position.x / cell_size), floor(position.y / cell_size))


func _get_visible_world_rect() -> Rect2i:
	var world_size_cells: Vector2i = _grid_system.get_world_size_cells()
	var visible_width: int = max(int(ceil(size.x / float(cell_size))) + viewport_padding_cells * 2, 1)
	var visible_height: int = max(int(ceil(size.y / float(cell_size))) + viewport_padding_cells * 2, 1)

	visible_width = min(visible_width, world_size_cells.x)
	visible_height = min(visible_height, world_size_cells.y)

	var origin := Vector2i(
		_player_coord.x - int(visible_width / 2),
		_player_coord.y - int(visible_height / 2)
	)
	origin.x = clampi(origin.x, 0, max(world_size_cells.x - visible_width, 0))
	origin.y = clampi(origin.y, 0, max(world_size_cells.y - visible_height, 0))

	return Rect2i(origin, Vector2i(visible_width, visible_height))


func _get_terrain_color(terrain_visual_type: String) -> Color:
	match terrain_visual_type:
		"plains":
			return Color(0.2, 0.31, 0.23, 1.0)
		"woods":
			return Color(0.14, 0.26, 0.2, 1.0)
		"steppe":
			return Color(0.36, 0.29, 0.18, 1.0)
		"highland":
			return Color(0.24, 0.24, 0.31, 1.0)
		_:
			return Color(0.18, 0.2, 0.25, 1.0)


func _get_settlement_color(tier: int) -> Color:
	match tier:
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.VILLAGE:
			return Color(0.57, 0.75, 0.43, 1.0)
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.TOWN:
			return Color(0.51, 0.7, 0.84, 1.0)
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.CITY:
			return Color(0.78, 0.63, 0.42, 1.0)
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.CAPITAL:
			return Color(0.74, 0.48, 0.76, 1.0)
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.WORLD_STRONGHOLD:
			return Color(0.9, 0.43, 0.31, 1.0)
		_:
			return Color(0.5, 0.5, 0.5, 1.0)
