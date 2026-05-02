## 文件说明：该脚本属于世界地图视图相关的界面视图脚本，集中维护格子尺寸、视口边距格子集合、格子背景纹理等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name WorldMapView
extends Control

## 信号说明：当格子被点击时发出的信号，供外层接管选择、移动或交互逻辑。
signal cell_clicked(coord: Vector2i)
## 信号说明：当格子被右键点击时发出的信号，供外层执行二级交互、取消或上下文操作。
signal cell_right_clicked(coord: Vector2i)

## 字段说明：在编辑器中暴露格子尺寸参数，便于直接调整尺寸、范围、间距或视图表现。
@export_range(24, 128, 1) var cell_size := 96
## 字段说明：在编辑器中暴露视口边距格子集合参数，便于直接调整尺寸、范围、间距或视图表现。
@export_range(0, 8, 1) var viewport_padding_cells := 2
## 字段说明：在编辑器中暴露格子背景纹理配置，用来控制界面的贴图外观、地图绘制效果或图标资源来源。
@export var cell_background_texture: Texture2D
## 字段说明：在编辑器中暴露玩家纹理配置，用来控制界面的贴图外观、地图绘制效果或图标资源来源。
@export var player_texture: Texture2D
## 字段说明：在编辑器中暴露村级据点纹理配置，用来控制村庄据点在世界地图上的贴图表现。
@export var village_settlement_texture: Texture2D
## 字段说明：在编辑器中暴露玩家贴图绘制尺寸，便于控制世界地图上的角色显示比例。
@export_range(16, 256, 1) var player_texture_draw_size := 128
## 字段说明：在编辑器中暴露格子背景裁切量参数，便于直接调整尺寸、范围、间距或视图表现。
@export_range(0, 256, 1) var cell_background_trim := 96
## 字段说明：在编辑器中暴露已探索迷雾暗度配置，便于策划或关卡制作者在不改代码的情况下调整该脚本行为。
@export_range(0.0, 1.0, 0.05) var explored_fog_darkness := 0.45
## 字段说明：在编辑器中暴露选中框颜色配置，便于在场景层直接调整当前地图焦点高亮。
@export var selection_outline_color: Color = Color(0.98, 0.9, 0.42, 0.95)
## 字段说明：在编辑器中暴露世界事件标记主体颜色配置，便于维持主线视觉或做轻量调色。
@export var world_event_marker_fill_color: Color = Color(0.95, 0.78, 0.28, 0.96)
## 字段说明：在编辑器中暴露世界事件标记描边颜色配置，便于控制图标边缘对比度。
@export var world_event_marker_outline_color: Color = Color(0.25, 0.11, 0.02, 1.0)
## 字段说明：在编辑器中暴露世界事件标记中心点颜色配置，便于控制事件标记层次。
@export var world_event_marker_center_color: Color = Color(0.32, 0.06, 0.02, 1.0)
## 字段说明：在编辑器中暴露遭遇锚点外环颜色配置，便于控制敌对遭遇的视觉提示强度。
@export var encounter_marker_outer_color: Color = Color(0.87, 0.28, 0.23, 0.95)
## 字段说明：在编辑器中暴露遭遇锚点内核颜色配置，便于维持遭遇图标的中心对比。
@export var encounter_marker_inner_color: Color = Color(0.15, 0.02, 0.02, 0.95)
## 字段说明：在编辑器中暴露世界 NPC 标记主体颜色配置，便于区分可交互角色。
@export var npc_marker_body_color: Color = Color(0.42, 0.77, 0.87, 0.95)
## 字段说明：在编辑器中暴露世界 NPC 标记高光颜色配置，便于保持头像层次感。
@export var npc_marker_head_color: Color = Color(0.88, 0.94, 0.98, 1.0)
## 字段说明：在编辑器中暴露村庄 tier 颜色配置，便于在场景层直接微调据点层级表现。
@export var village_tier_color: Color = Color(0.57, 0.75, 0.43, 1.0)
## 字段说明：在编辑器中暴露城镇 tier 颜色配置，便于在场景层直接微调据点层级表现。
@export var town_tier_color: Color = Color(0.51, 0.7, 0.84, 1.0)
## 字段说明：在编辑器中暴露城市 tier 颜色配置，便于在场景层直接微调据点层级表现。
@export var city_tier_color: Color = Color(0.78, 0.63, 0.42, 1.0)
## 字段说明：在编辑器中暴露主城 tier 颜色配置，便于在场景层直接微调据点层级表现。
@export var capital_tier_color: Color = Color(0.74, 0.48, 0.76, 1.0)
## 字段说明：在编辑器中暴露世界据点 tier 颜色配置，便于在场景层直接微调据点层级表现。
@export var world_stronghold_tier_color: Color = Color(0.9, 0.43, 0.31, 1.0)
## 字段说明：在编辑器中暴露都会 tier 颜色配置，便于在场景层直接微调据点层级表现。
@export var metropolis_tier_color: Color = Color(0.95, 0.82, 0.45, 1.0)
## 字段说明：在编辑器中暴露未知 tier 兜底颜色配置，避免未识别层级退回硬编码。
@export var fallback_tier_color: Color = Color(0.5, 0.5, 0.5, 1.0)

const WORLD_MAP_FOG_SYSTEM_SCRIPT = preload("res://scripts/systems/world/world_map_fog_system.gd")
const SETTLEMENT_CONFIG_SCRIPT = preload("res://scripts/utils/settlement_config.gd")

## 字段说明：记录网格系统，作为界面刷新、输入处理和窗口联动的重要依据。
var _grid_system
## 字段说明：记录迷雾系统，作为界面刷新、输入处理和窗口联动的重要依据。
var _fog_system
## 字段说明：缓存世界数据字典，集中保存可按键查询的运行时数据。
var _world_data: Dictionary = {}
## 字段说明：记录玩家坐标，用于定位对象、绘制内容或执行网格计算。
var _player_coord := Vector2i.ZERO
## 字段说明：记录选中坐标，用于定位对象、绘制内容或执行网格计算。
var _selected_coord := Vector2i.ZERO
## 字段说明：记录玩家当前是否应在世界地图上显示，供据点等模态上下文复用。
var _player_visible_on_map := true
## 字段说明：记录玩家阵营唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _player_faction_id := "player"


func configure(grid_system, fog_system, world_data: Dictionary, player_coord: Vector2i, selected_coord: Vector2i, player_visible_on_map: bool, player_faction_id: String) -> void:
	_grid_system = grid_system
	_fog_system = fog_system
	_world_data = world_data
	_player_coord = player_coord
	_selected_coord = selected_coord
	_player_visible_on_map = player_visible_on_map
	_player_faction_id = player_faction_id
	queue_redraw()


func set_runtime_state(player_coord: Vector2i, selected_coord: Vector2i, player_visible_on_map: bool = true) -> void:
	_player_coord = player_coord
	_selected_coord = selected_coord
	_player_visible_on_map = player_visible_on_map
	queue_redraw()


func refresh_world(world_data: Dictionary) -> void:
	_world_data = world_data
	queue_redraw()


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		queue_redraw()


func _gui_input(event: InputEvent) -> void:
	if event is not InputEventMouseButton:
		return

	var mouse_event := event as InputEventMouseButton
	if not mouse_event.pressed:
		return

	var coord := _local_to_cell(mouse_event.position)
	if _grid_system == null or not _grid_system.is_cell_inside_world(coord):
		return

	match mouse_event.button_index:
		MOUSE_BUTTON_LEFT:
			cell_clicked.emit(coord)
			accept_event()
		MOUSE_BUTTON_RIGHT:
			cell_right_clicked.emit(coord)
			accept_event()


func _draw() -> void:
	if _grid_system == null or _fog_system == null:
		return

	var camera_origin := _get_camera_origin_cells()
	var visible_rect := _get_visible_world_rect(camera_origin)
	_draw_cells(camera_origin, visible_rect)
	_draw_settlements(camera_origin, visible_rect)
	_draw_mobile_entities(camera_origin, visible_rect)
	_draw_player(camera_origin)
	_draw_selection(camera_origin)


func _draw_cells(camera_origin: Vector2, visible_rect: Rect2i) -> void:
	for y in range(visible_rect.position.y, visible_rect.end.y):
		for x in range(visible_rect.position.x, visible_rect.end.x):
			var coord := Vector2i(x, y)
			var rect := _get_cell_rect_for_origin(coord, camera_origin)
			var fog_state: int = _fog_system.get_fog_state(coord, _player_faction_id)
			if fog_state == WORLD_MAP_FOG_SYSTEM_SCRIPT.FOG_UNEXPLORED:
				draw_rect(rect, Color.BLACK)
			else:
				_draw_cell_background(rect)
				if fog_state == WORLD_MAP_FOG_SYSTEM_SCRIPT.FOG_EXPLORED:
					draw_rect(rect, Color(0.0, 0.0, 0.0, explored_fog_darkness))

			draw_rect(rect, Color(0.12, 0.16, 0.26, 0.7), false, 1.0)


func _draw_cell_background(rect: Rect2) -> void:
	if cell_background_texture == null:
		draw_rect(rect, Color(0.11, 0.14, 0.18, 1.0))
		return

	var texture_size: Vector2 = cell_background_texture.get_size()
	var trim := minf(float(cell_background_trim), minf(texture_size.x, texture_size.y) * 0.45)
	var source_rect := Rect2(
		Vector2(trim, trim),
		texture_size - Vector2(trim * 2.0, trim * 2.0)
	)
	if source_rect.size.x <= 0.0 or source_rect.size.y <= 0.0:
		draw_texture_rect(cell_background_texture, rect, false)
		return

	draw_texture_rect_region(cell_background_texture, rect, source_rect, Color.WHITE, false)


func _draw_settlements(camera_origin: Vector2, visible_rect: Rect2i) -> void:
	var settlements: Array = _world_data.get("settlements", [])
	var font := get_theme_default_font()
	var font_size := 16
	var can_draw_labels := font != null

	for settlement in settlements:
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var footprint_size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		if not Rect2i(origin, footprint_size).intersects(visible_rect):
			continue
		var color := _get_settlement_color(settlement.get("tier", 0))
		var visible_cells := _draw_settlement_footprint_cells(origin, footprint_size, camera_origin, visible_rect, settlement.get("tier", 0), color)
		if visible_cells.is_empty():
			continue

		if not can_draw_labels:
			continue
		var origin_fog_state: int = _fog_system.get_fog_state(origin, _player_faction_id)
		if origin_fog_state == WORLD_MAP_FOG_SYSTEM_SCRIPT.FOG_UNEXPLORED:
			continue

		var rect := _get_cell_rect_for_origin(origin, camera_origin).grow(-3.0)
		var label: String = settlement.get("display_name", "据点")
		var label_pos := rect.position + Vector2(8, min(24, rect.size.y - 6))
		draw_string(font, label_pos, label, HORIZONTAL_ALIGNMENT_LEFT, rect.size.x - 12.0, font_size, Color.WHITE)


func _draw_settlement_footprint_cells(
	origin: Vector2i,
	footprint_size: Vector2i,
	camera_origin: Vector2,
	visible_rect: Rect2i,
	tier: int,
	base_color: Color
) -> Array[Vector2i]:
	var drawn_cells: Array[Vector2i] = []
	var width := maxi(footprint_size.x, 1)
	var height := maxi(footprint_size.y, 1)
	for y in range(height):
		for x in range(width):
			var cell_coord := origin + Vector2i(x, y)
			if not visible_rect.has_point(cell_coord):
				continue
			var fog_state: int = _fog_system.get_fog_state(cell_coord, _player_faction_id)
			if fog_state == WORLD_MAP_FOG_SYSTEM_SCRIPT.FOG_UNEXPLORED:
				continue
			var cell_rect := _get_cell_rect_for_origin(cell_coord, camera_origin).grow(-3.0)
			var color := base_color
			if fog_state == WORLD_MAP_FOG_SYSTEM_SCRIPT.FOG_EXPLORED:
				color = color.darkened(0.45)
				color.a = 0.85
			_draw_settlement_body(cell_rect, tier, color, fog_state == WORLD_MAP_FOG_SYSTEM_SCRIPT.FOG_EXPLORED)
			draw_rect(cell_rect, Color(0.05, 0.08, 0.14, 0.95), false, 2.0)
			drawn_cells.append(cell_coord)
	return drawn_cells


func _draw_settlement_body(rect: Rect2, tier: int, color: Color, is_explored: bool) -> void:
	if tier == SETTLEMENT_CONFIG_SCRIPT.SettlementTier.VILLAGE and village_settlement_texture != null:
		draw_texture_rect(village_settlement_texture, rect, false, Color(1.0, 1.0, 1.0, color.a))
		if is_explored:
			draw_rect(rect, Color(0.0, 0.0, 0.0, 0.35))
		return
	draw_rect(rect, color)


func _draw_mobile_entities(camera_origin: Vector2, visible_rect: Rect2i) -> void:
	var world_events: Array = _world_data.get("world_events", [])
	for world_event_variant in world_events:
		if world_event_variant is not Dictionary:
			continue
		var world_event: Dictionary = world_event_variant
		if not bool(world_event.get("is_discovered", false)):
			continue
		var event_coord: Vector2i = world_event.get("world_coord", Vector2i.ZERO)
		if not visible_rect.has_point(event_coord):
			continue
		if not _fog_system.is_visible(event_coord, _player_faction_id):
			continue
		_draw_world_event_marker(_get_cell_rect_for_origin(event_coord, camera_origin).get_center())

	var encounter_anchors: Array = _world_data.get("encounter_anchors", [])
	for encounter_anchor_data in encounter_anchors:
		var encounter_anchor = encounter_anchor_data
		if encounter_anchor == null:
			continue
		var coord: Vector2i = encounter_anchor.world_coord
		if not visible_rect.has_point(coord):
			continue
		if not _fog_system.is_visible(coord, _player_faction_id):
			continue

		var center := _get_cell_rect_for_origin(coord, camera_origin).get_center()
		draw_circle(center, cell_size * 0.22, encounter_marker_outer_color)
		draw_circle(center, cell_size * 0.12, encounter_marker_inner_color)

	var npcs: Array = _world_data.get("world_npcs", [])
	for npc in npcs:
		var coord: Vector2i = npc.get("coord", Vector2i.ZERO)
		if not visible_rect.has_point(coord):
			continue
		if not _fog_system.is_visible(coord, _player_faction_id):
			continue

		var center := _get_cell_rect_for_origin(coord, camera_origin).get_center()
		draw_circle(center, cell_size * 0.18, npc_marker_body_color)
		draw_circle(center + Vector2(0, -4), cell_size * 0.06, npc_marker_head_color)


func _draw_world_event_marker(center: Vector2) -> void:
	var radius := cell_size * 0.2
	var diamond := PackedVector2Array([
		center + Vector2(0, -radius),
		center + Vector2(radius, 0),
		center + Vector2(0, radius),
		center + Vector2(-radius, 0),
	])
	draw_colored_polygon(diamond, world_event_marker_fill_color)
	draw_polyline(diamond + PackedVector2Array([diamond[0]]), world_event_marker_outline_color, 2.0)
	draw_circle(center, cell_size * 0.05, world_event_marker_center_color)


func _draw_player(camera_origin: Vector2) -> void:
	if not _player_visible_on_map:
		return
	if not _is_coord_visible_in_viewport(_player_coord, camera_origin):
		return
	var center := _get_cell_rect_for_origin(_player_coord, camera_origin).get_center()
	if player_texture != null:
		var draw_rect := _get_player_draw_rect_for_origin(_player_coord, camera_origin)
		if draw_rect.size.x > 0.0 and draw_rect.size.y > 0.0:
			draw_texture_rect(player_texture, draw_rect, false)
			return

	var points := PackedVector2Array([
		center + Vector2(0, -cell_size * 0.26),
		center + Vector2(cell_size * 0.18, 0),
		center + Vector2(0, cell_size * 0.26),
		center + Vector2(-cell_size * 0.18, 0),
	])
	draw_colored_polygon(points, Color(0.97, 0.89, 0.39, 1.0))
	draw_polyline(points + PackedVector2Array([points[0]]), Color(0.18, 0.12, 0.02, 1.0), 2.0)


func _draw_selection(camera_origin: Vector2) -> void:
	if not _is_coord_visible_in_viewport(_selected_coord, camera_origin):
		return
	var rect := _get_cell_rect_for_origin(_selected_coord, camera_origin).grow(-2.0)
	draw_rect(rect, selection_outline_color, false, 3.0)


func _get_cell_rect_for_origin(coord: Vector2i, camera_origin: Vector2) -> Rect2:
	return Rect2((Vector2(coord) - camera_origin) * cell_size, Vector2.ONE * cell_size)


func _get_player_draw_rect_for_origin(coord: Vector2i, camera_origin: Vector2) -> Rect2:
	var center := _get_cell_rect_for_origin(coord, camera_origin).get_center()
	var draw_size := Vector2.ONE * float(player_texture_draw_size)
	return Rect2(center - draw_size * 0.5, draw_size)


func _local_to_cell(position: Vector2) -> Vector2i:
	var camera_origin := _get_camera_origin_cells()
	return Vector2i(
		int(floor(camera_origin.x + position.x / cell_size)),
		int(floor(camera_origin.y + position.y / cell_size))
	)


func _get_camera_origin_cells() -> Vector2:
	var world_size_cells := Vector2(_grid_system.get_world_size_cells())
	var cell_span := _get_viewport_cell_span()
	var origin := Vector2(_player_coord) + Vector2.ONE * 0.5 - cell_span * 0.5
	origin.x = clampf(origin.x, 0.0, maxf(world_size_cells.x - cell_span.x, 0.0))
	origin.y = clampf(origin.y, 0.0, maxf(world_size_cells.y - cell_span.y, 0.0))
	return origin


func _get_viewport_cell_span() -> Vector2:
	var world_size_cells := Vector2(_grid_system.get_world_size_cells())
	var span := Vector2(
		maxf(size.x / float(cell_size), 1.0),
		maxf(size.y / float(cell_size), 1.0)
	)
	span.x = minf(span.x, world_size_cells.x)
	span.y = minf(span.y, world_size_cells.y)
	return span


func _get_visible_world_rect(camera_origin: Vector2) -> Rect2i:
	var cell_span := _get_viewport_cell_span()
	var padding := Vector2i.ONE * viewport_padding_cells
	var start := Vector2i(
		int(floor(camera_origin.x)) - padding.x,
		int(floor(camera_origin.y)) - padding.y
	)
	var end := Vector2i(
		int(ceil(camera_origin.x + cell_span.x)) + padding.x,
		int(ceil(camera_origin.y + cell_span.y)) + padding.y
	)
	var world_size_cells: Vector2i = _grid_system.get_world_size_cells()
	start.x = clampi(start.x, 0, world_size_cells.x)
	start.y = clampi(start.y, 0, world_size_cells.y)
	end.x = clampi(end.x, start.x, world_size_cells.x)
	end.y = clampi(end.y, start.y, world_size_cells.y)
	return Rect2i(start, end - start)


func _is_coord_visible_in_viewport(coord: Vector2i, camera_origin: Vector2) -> bool:
	var viewport_bounds := Rect2(camera_origin, _get_viewport_cell_span())
	var cell_center := Vector2(coord) + Vector2.ONE * 0.5
	return viewport_bounds.has_point(cell_center)


func _get_settlement_color(tier: int) -> Color:
	match tier:
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.VILLAGE:
			return village_tier_color
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.TOWN:
			return town_tier_color
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.CITY:
			return city_tier_color
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.CAPITAL:
			return capital_tier_color
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.WORLD_STRONGHOLD:
			return world_stronghold_tier_color
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.METROPOLIS:
			return metropolis_tier_color
		_:
			return fallback_tier_color
