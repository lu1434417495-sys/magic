## 文件说明：该脚本属于战斗棋盘控制器相关的控制器脚本，集中维护输入层、顶部层集合、悬崖东侧层集合等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name BattleBoardController
extends RefCounted

const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleEdgeFaceState = preload("res://scripts/systems/battle/core/battle_edge_face_state.gd")
const BATTLE_EDGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/terrain/battle_edge_service.gd")
const BattleEdgeService = preload("res://scripts/systems/battle/terrain/battle_edge_service.gd")
const BattleBoardProp = preload("res://scripts/ui/battle_board_prop.gd")
const BattleBoardPropCatalog = preload("res://scripts/utils/battle_board_prop_catalog.gd")
const BattleBoardRenderProfile = preload("res://scripts/ui/battle_board_render_profile.gd")
const BATTLE_BOARD_PROP_SCENE = preload("res://scenes/common/battle_board_prop.tscn")

const MAX_HEIGHT_LAYERS := 9
const TOP_LAYER_Z_BASE := 0
const LAYER_Z_STRIDE := 10
const EDGE_DROP_EAST_LAYER_Z_OFFSET := -4
const EDGE_DROP_SOUTH_LAYER_Z_OFFSET := -3
const WALL_EAST_LAYER_Z_OFFSET := -2
const WALL_SOUTH_LAYER_Z_OFFSET := -1
const OVERLAY_LAYER_Z_OFFSET := 6
const MARKER_LAYER_Z_OFFSET := OVERLAY_LAYER_Z_OFFSET + 1
const DYNAMIC_LAYER_Z_OFFSET := MARKER_LAYER_Z_OFFSET + 1
const PROP_LAYER_Z := 0
const UNIT_LAYER_Z := 0
const TARGET_HIGHLIGHT_LAYER_Z := 1300
const UNIT_GLYPH_LABEL_SIZE := Vector2(28.0, 28.0)
const UNIT_SPRITE_TILE_WIDTH_RATIO := 0.95
const UNIT_SPRITE_GROUND_ANCHOR_RATIO := 0.85
const UNIT_SPRITE_SHADOW_HALF_SIZE := Vector2(20.0, 6.0)
const UNIT_SPRITE_SHADOW_COLOR := Color(0.0, 0.0, 0.0, 0.45)
const UNIT_SPRITE_HIGHLIGHT_HALF_SIZE := Vector2(22.0, 8.0)
const UNIT_SPRITE_HIGHLIGHT_COLOR := Color(1.0, 0.94, 0.76, 0.92)
const UNIT_SPRITE_ELLIPSE_SEGMENT_COUNT := 28
const UNIT_HEALTH_BAR_SIZE := Vector2(56.0, 14.0)
const UNIT_HEALTH_BAR_Y_OFFSET := -50.0
const UNIT_HEALTH_BAR_BG_COLOR := Color(0.14, 0.09, 0.06, 0.92)
const UNIT_HEALTH_BAR_BORDER_COLOR := Color(0.95, 0.91, 0.8, 0.9)
const UNIT_HEALTH_BAR_HIGH_COLOR := Color(0.3, 0.86, 0.42, 0.96)
const UNIT_HEALTH_BAR_MID_COLOR := Color(0.9, 0.76, 0.24, 0.96)
const UNIT_HEALTH_BAR_LOW_COLOR := Color(0.9, 0.28, 0.22, 0.96)
const HP_MAX_ATTRIBUTE_ID := &"hp_max"
const ACTIVE_SELECTED_MARKER_COLOR := Color(0.0, 0.0, 1.0, 1.0)
const MOVE_REACHABLE_MARKER_COLOR_DARK := Color(0.14, 0.37, 0.5, 1.0)
const MOVE_REACHABLE_MARKER_COLOR_LIGHT := Color(0.46, 0.72, 0.84, 1.0)
const VALID_TARGET_HIGHLIGHT_COLOR := Color(0.92, 0.12, 0.08, 0.42)
const LOCKED_TARGET_HIGHLIGHT_COLOR := Color(0.96, 0.82, 0.28, 0.54)
const CONFIRM_READY_TARGET_HIGHLIGHT_COLOR := Color(0.28, 0.8, 0.5, 0.5)
const CONFIRM_READY_FOCUS_HALO_COLOR := Color(0.98, 0.9, 0.34, 0.35)
const HIT_BADGE_SIZE := Vector2(82.0, 26.0)
const HIT_BADGE_OFFSET := Vector2(-41.0, -76.0)
const HIT_BADGE_BG_COLOR := Color(0.08, 0.04, 0.02, 0.9)
const HIT_BADGE_EDGE_COLOR := Color(1.0, 0.84, 0.42, 0.95)
const HIT_BADGE_TEXT_COLOR := Color(1.0, 0.95, 0.82, 1.0)

const TERRAIN_LAND := &"land"
const TERRAIN_FOREST := &"forest"
const TERRAIN_WATER := &"water"
const TERRAIN_SHALLOW_WATER := &"shallow_water"
const TERRAIN_FLOWING_WATER := &"flowing_water"
const TERRAIN_DEEP_WATER := &"deep_water"
const TERRAIN_MUD := &"mud"
const TERRAIN_SPIKE := &"spike"

const SOURCE_LAND := &"land"
const SOURCE_WATER := &"water"
const SOURCE_MUD := &"mud"
const SOURCE_EDGE_DROP_EAST := &"edge_drop_east"
const SOURCE_EDGE_DROP_SOUTH := &"edge_drop_south"
const SOURCE_WALL_EAST := &"wall_east"
const SOURCE_WALL_SOUTH := &"wall_south"
const SOURCE_SCRUB := &"scrub"
const SOURCE_RUBBLE := &"rubble"
const SOURCE_SELECTED := &"selected"
const SOURCE_ACTIVE_SELECTED := &"active_selected"
const SOURCE_MOVE_REACHABLE := &"move_reachable"
const SOURCE_PREVIEW := &"preview"
const INVALID_VARIANT_COORD := Vector2i(-999999, -999999)

## 字段说明：缓存输入层节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
var _input_layer: TileMapLayer = null
## 字段说明：保存顶部层集合，便于顺序遍历、批量展示、批量运算和整体重建。
var _top_layers: Array[TileMapLayer] = []
## 字段说明：保存东侧落差面层集合，便于顺序遍历、批量展示、批量运算和整体重建。
var _edge_drop_east_layers: Array[TileMapLayer] = []
## 字段说明：保存南侧落差面层集合，便于顺序遍历、批量展示、批量运算和整体重建。
var _edge_drop_south_layers: Array[TileMapLayer] = []
## 字段说明：保存东侧人工边特征层集合，便于顺序遍历、批量展示、批量运算和整体重建。
var _wall_east_layers: Array[TileMapLayer] = []
## 字段说明：保存南侧人工边特征层集合，便于顺序遍历、批量展示、批量运算和整体重建。
var _wall_south_layers: Array[TileMapLayer] = []
## 字段说明：保存覆盖层层集合，便于顺序遍历、批量展示、批量运算和整体重建。
var _overlay_layers: Array[TileMapLayer] = []
## 字段说明：保存标记层集合，便于顺序遍历、批量展示、批量运算和整体重建。
var _marker_layers: Array[TileMapLayer] = []
## 字段说明：缓存场景装饰物层节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
var _prop_layer: Node2D = null
## 字段说明：缓存单位层节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
var _unit_layer: Node2D = null
## 字段说明：缓存技能合法目标高亮层节点，用于把可点击格绘制在最顶层。
var _target_highlight_layer: Node2D = null
## 字段说明：缓存瓦片集合实例，作为界面刷新、输入处理和窗口联动的重要依据。
var _tile_set: TileSet = null
## 字段说明：保存来源标识列表，便于批量遍历、交叉查找和界面展示。
var _source_ids: Dictionary = {}
## 字段说明：记录瓦片配置档唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var _tile_profile_id: StringName = &""
## 字段说明：缓存当前渲染 profile，统一提供视觉高度、TileSet 规格、素材目录与 anchor 偏移。
var _render_profile: BattleBoardRenderProfile = null
## 字段说明：缓存纹理缓存字典，集中保存可按键查询的运行时数据。
var _texture_cache: Dictionary = {}
## 字段说明：缓存按贴图目录构建好的 TileSet 与 source id 映射，避免切换战场主题时重复构建 atlas。
var _tileset_cache: Dictionary = {}
## 字段说明：缓存边缘面服务实例，统一处理落差面与人工边特征的渲染来源。
var _edge_service: BattleEdgeService = BATTLE_EDGE_SERVICE_SCRIPT.new()

## 字段说明：缓存战斗状态实例，作为界面刷新、输入处理和窗口联动的重要依据。
var _battle_state: BattleState = null
## 字段说明：记录选中坐标，用于定位对象、绘制内容或执行网格计算。
var _selected_coord := Vector2i(-1, -1)
## 字段说明：保存预览目标坐标列表，供范围判定、占位刷新、批量渲染或目标选择复用。
var _preview_target_coords: Array[Vector2i] = []
## 字段说明：保存当前技能合法目标坐标列表，供最顶层高亮渲染复用。
var _valid_target_coords: Array[Vector2i] = []
## 字段说明：保存当前技能目标选择模式，供可视化区分 multi_unit 与普通单目标。
var _target_selection_mode: StringName = &"single_unit"
## 字段说明：保存当前技能最小目标数量，供确认态高亮判断使用。
var _target_min_count := 1
## 字段说明：保存当前技能最大目标数量，供确认态高亮判断使用。
var _target_max_count := 1
## 字段说明：保存当前目标格上方的命中率浮标文本，key 为战斗格坐标。
var _target_hit_badges: Dictionary = {}


func bind_layers(
	input_layer: TileMapLayer,
	top_layers: Array[TileMapLayer],
	edge_drop_east_layers: Array[TileMapLayer],
	edge_drop_south_layers: Array[TileMapLayer],
	wall_east_layers: Array[TileMapLayer],
	wall_south_layers: Array[TileMapLayer],
	overlay_layers: Array[TileMapLayer],
	marker_layers: Array[TileMapLayer],
	prop_layer: Node2D,
	unit_layer: Node2D,
	target_highlight_layer: Node2D
) -> void:
	_input_layer = input_layer
	_top_layers = top_layers.duplicate()
	_edge_drop_east_layers = edge_drop_east_layers.duplicate()
	_edge_drop_south_layers = edge_drop_south_layers.duplicate()
	_wall_east_layers = wall_east_layers.duplicate()
	_wall_south_layers = wall_south_layers.duplicate()
	_overlay_layers = overlay_layers.duplicate()
	_marker_layers = marker_layers.duplicate()
	_prop_layer = prop_layer
	_unit_layer = unit_layer
	_target_highlight_layer = target_highlight_layer

	_ensure_tileset(BattleBoardRenderProfile.TERRAIN_PROFILE_DEFAULT)
	_apply_tileset_to_layers()
	_apply_layer_offsets()
	_apply_layer_draw_order()


func configure(
	battle_state: BattleState,
	selected_coord: Vector2i,
	preview_target_coords: Array[Vector2i] = [],
	target_selection_mode: StringName = &"single_unit",
	min_target_count: int = 1,
	max_target_count: int = 1,
	target_hit_badges: Dictionary = {}
) -> void:
	_battle_state = battle_state
	_selected_coord = selected_coord
	_preview_target_coords = preview_target_coords.duplicate()
	_target_selection_mode = target_selection_mode if target_selection_mode != &"" else &"single_unit"
	_target_min_count = maxi(min_target_count, 1)
	_target_max_count = maxi(max_target_count, _target_min_count)
	_set_target_hit_badges(target_hit_badges)
	_refresh_tileset_profile()
	_redraw()


func update_markers(
	selected_coord: Vector2i,
	preview_target_coords: Array[Vector2i] = [],
	valid_target_coords: Array[Vector2i] = [],
	target_selection_mode: StringName = &"single_unit",
	min_target_count: int = 1,
	max_target_count: int = 1,
	target_hit_badges: Dictionary = {}
) -> void:
	_selected_coord = selected_coord
	_preview_target_coords = preview_target_coords.duplicate()
	_valid_target_coords = valid_target_coords.duplicate()
	_target_selection_mode = target_selection_mode if target_selection_mode != &"" else &"single_unit"
	_target_min_count = maxi(min_target_count, 1)
	_target_max_count = maxi(max_target_count, _target_min_count)
	_set_target_hit_badges(target_hit_badges)
	_draw_marker_layer()
	_draw_target_highlights()


func clear() -> void:
	_battle_state = null
	_selected_coord = Vector2i(-1, -1)
	_preview_target_coords.clear()
	_valid_target_coords.clear()
	_target_hit_badges.clear()
	_clear_tile_layers()
	_clear_dynamic_nodes()


func _refresh_tileset_profile() -> void:
	var desired_profile := _resolve_tile_profile_id()
	if desired_profile == _tile_profile_id and _tile_set != null:
		return
	_ensure_tileset(desired_profile)
	_apply_tileset_to_layers()
	_apply_layer_offsets()


func has_layers_bound() -> bool:
	return _input_layer != null and not _marker_layers.is_empty() and _tile_set != null


func is_render_content_ready() -> bool:
	if not has_layers_bound():
		return false
	if _battle_state == null or _battle_state.is_empty() or _battle_state.map_size == Vector2i.ZERO:
		return false
	if _count_rendered_top_cells() < _count_expected_drawable_cells():
		return false
	if _count_rendered_units() != _count_expected_rendered_units():
		return false
	if _count_rendered_props() != _count_expected_rendered_props():
		return false
	return true


func _redraw() -> void:
	_clear_tile_layers()
	_clear_dynamic_nodes()

	if _battle_state == null or _battle_state.is_empty() or _battle_state.map_size == Vector2i.ZERO:
		return

	var cells := _collect_cells()
	_draw_terrain_layers(cells)
	_draw_marker_layer()
	_draw_props(cells)
	_draw_units()
	_draw_target_highlights()


func _draw_terrain_layers(cells: Array[BattleCellState]) -> void:
	for cell_state in cells:
		if cell_state == null:
			continue
		var coord := cell_state.coord
		if not _is_cell_inside_battle(coord):
			continue

		var height_index := clampi(int(cell_state.current_height), 0, MAX_HEIGHT_LAYERS - 1)
		var top_source_id := _get_top_source_id(String(cell_state.base_terrain), coord)
		if top_source_id >= 0 and height_index < _top_layers.size():
			_top_layers[height_index].set_cell(coord, top_source_id, Vector2i.ZERO, 0)

		var overlay_source_id := _get_overlay_source_id(String(cell_state.base_terrain), coord)
		if overlay_source_id >= 0 and height_index < _overlay_layers.size():
			_overlay_layers[height_index].set_cell(coord, overlay_source_id, Vector2i.ZERO, 0)

	_draw_edge_faces()


func _draw_edge_faces() -> void:
	if _battle_state == null:
		return
	for edge_face in _edge_service.get_all_edge_faces(_battle_state):
		if edge_face == null or not edge_face.has_any_face():
			continue
		_draw_drop_face(edge_face)
		_draw_feature_face(edge_face)


func _draw_drop_face(edge_face: BattleEdgeFaceState) -> void:
	if edge_face == null or not edge_face.has_drop_face():
		return
	var layers := _edge_drop_east_layers if edge_face.direction == Vector2i.RIGHT else _edge_drop_south_layers
	var source_key := SOURCE_EDGE_DROP_EAST if edge_face.direction == Vector2i.RIGHT else SOURCE_EDGE_DROP_SOUTH
	var render_coord := _get_edge_render_coord(edge_face)
	for render_height in edge_face.drop_face_layer_heights:
		var layer_index := int(render_height) - 1
		if layer_index < 0 or layer_index >= layers.size():
			continue
		layers[layer_index].set_cell(
			render_coord,
			_get_source_id(source_key, edge_face.origin_coord, layer_index),
			Vector2i.ZERO,
			0
		)


func _draw_feature_face(edge_face: BattleEdgeFaceState) -> void:
	if edge_face == null or not edge_face.has_feature_face():
		return
	match edge_face.feature_render_kind:
		BattleEdgeFaceState.RENDER_WALL:
			var layers := _wall_east_layers if edge_face.direction == Vector2i.RIGHT else _wall_south_layers
			var source_key := SOURCE_WALL_EAST if edge_face.direction == Vector2i.RIGHT else SOURCE_WALL_SOUTH
			var render_coord := _get_edge_render_coord(edge_face)
			for layer_offset in range(edge_face.feature_layers):
				var layer_index := clampi(int(edge_face.from_height) - layer_offset, 0, MAX_HEIGHT_LAYERS - 1)
				if layer_index < 0 or layer_index >= layers.size():
					continue
				layers[layer_index].set_cell(
					render_coord,
					_get_source_id(source_key, edge_face.origin_coord, layer_index),
					Vector2i.ZERO,
					0
				)
		_:
			return


func _get_edge_render_coord(edge_face: BattleEdgeFaceState) -> Vector2i:
	if edge_face == null:
		return Vector2i.ZERO
	if edge_face.direction == Vector2i.RIGHT:
		return edge_face.neighbor_coord
	if edge_face.direction == Vector2i.DOWN:
		return edge_face.neighbor_coord
	return edge_face.origin_coord


func _draw_marker_layer() -> void:
	if _marker_layers.is_empty():
		return
	_clear_marker_layers()

	if _selected_coord != Vector2i(-1, -1) and _is_cell_inside_battle(_selected_coord):
		_set_marker_cell(_selected_coord, _get_selected_marker_source_id(_selected_coord))

	if _target_selection_mode == &"movement":
		for reachable_coord in _valid_target_coords:
			if reachable_coord == _selected_coord:
				continue
			if not _is_cell_inside_battle(reachable_coord):
				continue
			_set_marker_cell(reachable_coord, _get_move_reachable_marker_source_id())
		return

	for preview_coord in _preview_target_coords:
		if preview_coord == _selected_coord:
			continue
		if not _is_cell_inside_battle(preview_coord):
			continue
		_set_marker_cell(preview_coord, _get_source_id(SOURCE_PREVIEW))


func _draw_props(cells: Array[BattleCellState]) -> void:
	if _prop_layer == null or _battle_state == null:
		return

	for cell_state in cells:
		if cell_state == null or not _is_cell_inside_battle(cell_state.coord):
			continue
		var prop_ids := _collect_prop_ids_for_cell(cell_state)
		for index in range(prop_ids.size()):
			var prop_id := prop_ids[index]
			var prop_node := _create_prop_node(cell_state, prop_id, index)
			if prop_node == null:
				continue
			_prop_layer.add_child(prop_node)


func _draw_units() -> void:
	if _unit_layer == null or _battle_state == null:
		return

	var unit_ids := _battle_state.units.keys()
	unit_ids.sort_custom(func(a, b) -> bool:
		var left_unit := _battle_state.units.get(a) as BattleUnitState
		var right_unit := _battle_state.units.get(b) as BattleUnitState
		if left_unit == null:
			return false
		if right_unit == null:
			return true
		return _get_unit_sort_key(left_unit) < _get_unit_sort_key(right_unit)
	)

	for unit_id_variant in unit_ids:
		var unit_state := _battle_state.units.get(unit_id_variant) as BattleUnitState
		if unit_state == null or not unit_state.is_alive:
			continue
		unit_state.refresh_footprint()
		var unit_node := _create_unit_token(unit_state)
		if unit_node == null:
			continue
		_unit_layer.add_child(unit_node)


func _create_unit_token(unit_state: BattleUnitState) -> Node2D:
	if unit_state == null:
		return null

	var anchor := _get_unit_anchor_position(unit_state)
	var render_depth := _get_unit_render_depth(unit_state)
	var token := Node2D.new()
	token.name = String(unit_state.unit_id)
	token.position = anchor + _get_unit_anchor_bias()
	token.z_index = render_depth
	token.set_meta("sort_anchor_y", anchor.y)
	token.set_meta("sort_depth", render_depth)
	token.set_meta("board_coord", unit_state.coord)

	if unit_state.battle_sprite_texture != null:
		_attach_unit_sprite_visuals(token, unit_state)
	else:
		var body := Polygon2D.new()
		body.polygon = PackedVector2Array([
			Vector2(0.0, -14.0),
			Vector2(12.0, 0.0),
			Vector2(0.0, 14.0),
			Vector2(-12.0, 0.0),
		])
		body.color = _get_unit_color(unit_state)
		body.antialiased = true
		token.add_child(body)

		var outline := Line2D.new()
		outline.points = PackedVector2Array([
			Vector2(0.0, -14.0),
			Vector2(12.0, 0.0),
			Vector2(0.0, 14.0),
			Vector2(-12.0, 0.0),
			Vector2(0.0, -14.0),
		])
		outline.width = 2.0
		outline.default_color = Color(0.18, 0.11, 0.06, 0.96)
		outline.antialiased = true
		token.add_child(outline)

	if unit_state.battle_sprite_texture == null and unit_state.unit_id == _battle_state.active_unit_id:
		var active_outline := Line2D.new()
		active_outline.points = PackedVector2Array([
			Vector2(0.0, -18.0),
			Vector2(16.0, 0.0),
			Vector2(0.0, 18.0),
			Vector2(-16.0, 0.0),
			Vector2(0.0, -18.0),
		])
		active_outline.width = 2.0
		active_outline.default_color = Color(1.0, 0.94, 0.76, 0.96)
		active_outline.antialiased = true
		token.add_child(active_outline)

	var label := Label.new()
	label.name = "UnitGlyphLabel"
	label.text = _build_unit_short_name(unit_state)
	label.position = Vector2(-UNIT_GLYPH_LABEL_SIZE.x * 0.5, -UNIT_GLYPH_LABEL_SIZE.y * 0.5)
	label.size = UNIT_GLYPH_LABEL_SIZE
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.add_theme_font_size_override("font_size", 15)
	label.add_theme_color_override("font_color", Color(0.98, 0.96, 0.9, 0.98))
	label.add_theme_color_override("font_shadow_color", Color(0.16, 0.1, 0.06, 0.92))
	label.add_theme_constant_override("shadow_offset_x", 1)
	label.add_theme_constant_override("shadow_offset_y", 1)
	token.add_child(label)

	var health_bar := _create_unit_health_bar(unit_state)
	if health_bar != null:
		token.add_child(health_bar)

	return token


func _attach_unit_sprite_visuals(token: Node2D, unit_state: BattleUnitState) -> void:
	if token == null or unit_state == null or unit_state.battle_sprite_texture == null:
		return

	# Cancel the diamond-era anchor bias so the sprite's feet land on the tile surface.
	var ground_y := -_get_unit_anchor_bias().y

	var shadow := Polygon2D.new()
	shadow.name = "UnitSpriteShadow"
	shadow.polygon = _build_unit_ellipse_polygon(UNIT_SPRITE_SHADOW_HALF_SIZE)
	shadow.color = UNIT_SPRITE_SHADOW_COLOR
	shadow.position = Vector2(0.0, ground_y)
	shadow.antialiased = true
	shadow.z_index = -2
	token.add_child(shadow)

	if _battle_state != null and unit_state.unit_id == _battle_state.active_unit_id:
		var highlight := Polygon2D.new()
		highlight.name = "UnitSpriteActiveHighlight"
		highlight.polygon = _build_unit_ellipse_polygon(UNIT_SPRITE_HIGHLIGHT_HALF_SIZE)
		highlight.color = UNIT_SPRITE_HIGHLIGHT_COLOR
		highlight.position = Vector2(0.0, ground_y)
		highlight.antialiased = true
		highlight.z_index = -1
		token.add_child(highlight)

	var texture_size := unit_state.battle_sprite_texture.get_size()
	if texture_size.x <= 0.0 or texture_size.y <= 0.0:
		return

	var tile_size := _get_board_tile_size()
	var target_width := maxf(float(tile_size.x) * UNIT_SPRITE_TILE_WIDTH_RATIO, 1.0)
	var sprite_scale := target_width / texture_size.x
	var sprite := Sprite2D.new()
	sprite.name = "UnitSprite"
	sprite.texture = unit_state.battle_sprite_texture
	sprite.centered = true
	sprite.scale = Vector2.ONE * sprite_scale
	# Front-paws line at GROUND_ANCHOR_RATIO of image; offset sprite center so that line sits on ground_y.
	sprite.position = Vector2(0.0, ground_y + (0.5 - UNIT_SPRITE_GROUND_ANCHOR_RATIO) * texture_size.y * sprite_scale)
	sprite.z_index = 0
	token.add_child(sprite)


func _build_unit_ellipse_polygon(half_size: Vector2) -> PackedVector2Array:
	var safe_half_size := Vector2(maxf(half_size.x, 1.0), maxf(half_size.y, 1.0))
	var points := PackedVector2Array()
	for index in range(UNIT_SPRITE_ELLIPSE_SEGMENT_COUNT):
		var angle := TAU * float(index) / float(UNIT_SPRITE_ELLIPSE_SEGMENT_COUNT)
		points.append(Vector2(cos(angle) * safe_half_size.x, sin(angle) * safe_half_size.y))
	return points


func _create_unit_health_bar(unit_state: BattleUnitState) -> Control:
	if unit_state == null:
		return null

	var hp_max := _get_unit_hp_max(unit_state)
	var clamped_hp := clampi(int(unit_state.current_hp), 0, hp_max)
	var hp_ratio := clampf(float(clamped_hp) / float(hp_max), 0.0, 1.0)
	var max_fill_width := maxf(UNIT_HEALTH_BAR_SIZE.x - 2.0, 0.0)
	var fill_width := max_fill_width * hp_ratio
	if clamped_hp > 0 and fill_width > 0.0 and fill_width < 1.0:
		fill_width = 1.0

	var health_bar := Panel.new()
	health_bar.name = "HealthBarRoot"
	health_bar.position = Vector2(-UNIT_HEALTH_BAR_SIZE.x * 0.5, UNIT_HEALTH_BAR_Y_OFFSET)
	health_bar.size = UNIT_HEALTH_BAR_SIZE
	health_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	health_bar.clip_contents = true

	var panel_style := StyleBoxFlat.new()
	panel_style.bg_color = UNIT_HEALTH_BAR_BG_COLOR
	panel_style.border_color = UNIT_HEALTH_BAR_BORDER_COLOR
	panel_style.border_width_left = 1
	panel_style.border_width_top = 1
	panel_style.border_width_right = 1
	panel_style.border_width_bottom = 1
	panel_style.corner_radius_top_left = 2
	panel_style.corner_radius_top_right = 2
	panel_style.corner_radius_bottom_right = 2
	panel_style.corner_radius_bottom_left = 2
	health_bar.add_theme_stylebox_override("panel", panel_style)

	var fill := ColorRect.new()
	fill.name = "HealthBarFill"
	fill.position = Vector2.ONE
	fill.size = Vector2(fill_width, maxf(UNIT_HEALTH_BAR_SIZE.y - 2.0, 0.0))
	fill.color = _get_unit_health_bar_fill_color(hp_ratio)
	health_bar.add_child(fill)

	var value_label := Label.new()
	value_label.name = "HealthBarTextLabel"
	value_label.text = "%d/%d" % [clamped_hp, hp_max]
	value_label.position = Vector2.ZERO
	value_label.size = UNIT_HEALTH_BAR_SIZE
	value_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	value_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	value_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	value_label.add_theme_font_size_override("font_size", 9)
	value_label.add_theme_color_override("font_color", Color(0.98, 0.97, 0.94, 1.0))
	value_label.add_theme_color_override("font_shadow_color", Color(0.08, 0.05, 0.04, 0.94))
	value_label.add_theme_constant_override("shadow_offset_x", 1)
	value_label.add_theme_constant_override("shadow_offset_y", 1)
	health_bar.add_child(value_label)

	return health_bar


func _get_unit_anchor_position(unit_state: BattleUnitState) -> Vector2:
	if unit_state == null or _input_layer == null or _battle_state == null:
		return Vector2.ZERO

	var total := Vector2.ZERO
	var count := 0
	for occupied_coord in unit_state.occupied_coords:
		var cell := _battle_state.cells.get(occupied_coord) as BattleCellState
		if cell == null:
			continue
		var cell_position := _get_cell_anchor_position(occupied_coord, int(cell.current_height))
		total += cell_position
		count += 1

	if count <= 0:
		return _get_cell_anchor_position(unit_state.coord, 0)
	return total / float(count)


func _get_unit_render_depth(unit_state: BattleUnitState) -> int:
	if unit_state == null or _input_layer == null or _battle_state == null:
		return 0

	unit_state.refresh_footprint()
	var best_depth := -2147483648
	for occupied_coord in unit_state.occupied_coords:
		var cell := _battle_state.cells.get(occupied_coord) as BattleCellState
		var height_value := 0
		if cell != null:
			height_value = clampi(int(cell.current_height), 0, MAX_HEIGHT_LAYERS - 1)
		var depth := _get_cell_render_depth(occupied_coord, height_value)
		best_depth = maxi(best_depth, depth)

	if best_depth == -2147483648:
		return _get_cell_render_depth(unit_state.coord, 0)
	return best_depth


func _get_unit_sort_key(unit_state: BattleUnitState) -> float:
	if unit_state == null:
		return 0.0
	unit_state.refresh_footprint()
	var best_key := float(unit_state.coord.y) * 1000.0 + float(unit_state.coord.x)
	for occupied_coord in unit_state.occupied_coords:
		var cell := _battle_state.cells.get(occupied_coord) as BattleCellState
		var height_value := 0.0
		if cell != null:
			height_value = float(clampi(int(cell.current_height), 0, MAX_HEIGHT_LAYERS - 1))
		best_key = maxf(best_key, float(occupied_coord.y) * 1000.0 + float(occupied_coord.x) + height_value * 0.01)
	return best_key


func _clear_tile_layers() -> void:
	for layer in _top_layers:
		if layer != null:
			layer.clear()
	for layer in _edge_drop_east_layers:
		if layer != null:
			layer.clear()
	for layer in _edge_drop_south_layers:
		if layer != null:
			layer.clear()
	for layer in _wall_east_layers:
		if layer != null:
			layer.clear()
	for layer in _wall_south_layers:
		if layer != null:
			layer.clear()
	for layer in _overlay_layers:
		if layer != null:
			layer.clear()
	_clear_marker_layers()


func _clear_marker_layers() -> void:
	for layer in _marker_layers:
		if layer != null:
			layer.clear()


func _clear_dynamic_nodes() -> void:
	_clear_child_nodes(_prop_layer)
	_clear_child_nodes(_unit_layer)
	_clear_child_nodes(_target_highlight_layer)


func _draw_target_highlights() -> void:
	if _target_highlight_layer == null:
		return
	_clear_child_nodes(_target_highlight_layer)
	if _target_selection_mode == &"movement":
		return
	var preview_coord_set: Dictionary = {}
	var is_multi_unit_selection := _target_selection_mode == &"multi_unit"
	if is_multi_unit_selection:
		for preview_coord in _preview_target_coords:
			preview_coord_set[preview_coord] = true
			var locked_highlight := _create_target_highlight(preview_coord, LOCKED_TARGET_HIGHLIGHT_COLOR, 0.88, 0.68)
			if locked_highlight != null:
				locked_highlight.name = "LockedTarget_%d_%d" % [preview_coord.x, preview_coord.y]
				_target_highlight_layer.add_child(locked_highlight)
	else:
		for preview_coord in _preview_target_coords:
			preview_coord_set[preview_coord] = true
	var is_multi_unit_confirm_ready := is_multi_unit_selection \
		and _preview_target_coords.size() >= _target_min_count \
		and _preview_target_coords.size() < _target_max_count
	for target_coord in _valid_target_coords:
		if preview_coord_set.has(target_coord):
			continue
		if not _is_cell_inside_battle(target_coord):
			continue
		var target_color := CONFIRM_READY_TARGET_HIGHLIGHT_COLOR if is_multi_unit_confirm_ready else VALID_TARGET_HIGHLIGHT_COLOR
		var target_scale := 0.92 if is_multi_unit_confirm_ready else 0.88
		var highlight := _create_target_highlight(target_coord, target_color, target_scale, 0.0)
		if highlight == null:
			continue
		highlight.name = "ValidTarget_%d_%d" % [target_coord.x, target_coord.y]
		_target_highlight_layer.add_child(highlight)
	var confirm_focus_coord := _resolve_multi_unit_confirm_focus_coord() if is_multi_unit_confirm_ready else Vector2i(-1, -1)
	if is_multi_unit_confirm_ready and _is_cell_inside_battle(confirm_focus_coord):
		var confirm_halo := _create_target_highlight(confirm_focus_coord, CONFIRM_READY_FOCUS_HALO_COLOR, 1.14, 0.0)
		if confirm_halo != null:
			confirm_halo.name = "ConfirmReady_%d_%d" % [confirm_focus_coord.x, confirm_focus_coord.y]
			_target_highlight_layer.add_child(confirm_halo)
	_draw_target_hit_badges()


func _set_target_hit_badges(target_hit_badges: Dictionary) -> void:
	_target_hit_badges.clear()
	for coord_variant in target_hit_badges.keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		var badge_text := String(target_hit_badges.get(coord_variant, ""))
		if badge_text.is_empty():
			continue
		_target_hit_badges[coord] = badge_text


func _draw_target_hit_badges() -> void:
	if _target_highlight_layer == null or _target_hit_badges.is_empty():
		return
	for coord_variant in _target_hit_badges.keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		if not _is_cell_inside_battle(coord):
			continue
		var badge := _create_target_hit_badge(coord, String(_target_hit_badges.get(coord, "")))
		if badge == null:
			continue
		badge.name = "HitBadge_%d_%d" % [coord.x, coord.y]
		_target_highlight_layer.add_child(badge)


func _create_target_hit_badge(target_coord: Vector2i, badge_text: String) -> Control:
	if badge_text.is_empty() or not _is_cell_inside_battle(target_coord):
		return null
	var badge := PanelContainer.new()
	badge.position = _get_cell_anchor_position(target_coord, _get_cell_height_index(target_coord)) + HIT_BADGE_OFFSET
	badge.custom_minimum_size = HIT_BADGE_SIZE
	badge.size = HIT_BADGE_SIZE
	badge.mouse_filter = Control.MOUSE_FILTER_IGNORE
	badge.add_theme_stylebox_override("panel", _build_hit_badge_style())

	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 8)
	margin.add_theme_constant_override("margin_top", 3)
	margin.add_theme_constant_override("margin_right", 8)
	margin.add_theme_constant_override("margin_bottom", 3)
	badge.add_child(margin)

	var label := Label.new()
	label.text = badge_text
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.size_flags_vertical = Control.SIZE_EXPAND_FILL
	label.add_theme_font_size_override("font_size", 12)
	label.add_theme_color_override("font_color", HIT_BADGE_TEXT_COLOR)
	margin.add_child(label)
	return badge


func _build_hit_badge_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = HIT_BADGE_BG_COLOR
	style.border_color = HIT_BADGE_EDGE_COLOR
	style.border_width_left = 1
	style.border_width_top = 1
	style.border_width_right = 1
	style.border_width_bottom = 1
	style.corner_radius_top_left = 5
	style.corner_radius_top_right = 5
	style.corner_radius_bottom_right = 5
	style.corner_radius_bottom_left = 5
	return style


func _resolve_multi_unit_confirm_focus_coord() -> Vector2i:
	if _battle_state != null:
		var active_unit := _battle_state.units.get(_battle_state.active_unit_id) as BattleUnitState
		if active_unit != null and active_unit.is_alive and _is_cell_inside_battle(active_unit.coord):
			return active_unit.coord
	return _selected_coord


func _create_target_highlight(
	target_coord: Vector2i,
	color: Color,
	scale: float,
	alpha_scale: float
) -> Polygon2D:
	if not _is_cell_inside_battle(target_coord):
		return null
	var highlight := Polygon2D.new()
	highlight.position = _get_cell_anchor_position(target_coord, _get_cell_height_index(target_coord))
	highlight.polygon = _build_target_highlight_polygon(scale)
	if alpha_scale > 0.0:
		highlight.color = Color(color.r, color.g, color.b, color.a * alpha_scale)
	else:
		highlight.color = color
	highlight.antialiased = true
	highlight.set_meta("board_coord", target_coord)
	return highlight


func _set_marker_cell(coord: Vector2i, source_id: int) -> void:
	if source_id < 0:
		return
	var height_index := _get_cell_height_index(coord)
	if height_index < 0 or height_index >= _marker_layers.size():
		return
	var marker_layer := _marker_layers[height_index]
	if marker_layer != null:
		marker_layer.set_cell(coord, source_id, Vector2i.ZERO, 0)


func _build_target_highlight_polygon(scale: float) -> PackedVector2Array:
	var safe_scale := maxf(scale, 0.2)
	return PackedVector2Array([
		Vector2(0.0, -13.0) * safe_scale,
		Vector2(28.0, 0.0) * safe_scale,
		Vector2(0.0, 13.0) * safe_scale,
		Vector2(-28.0, 0.0) * safe_scale,
	])


func _clear_child_nodes(container: Node) -> void:
	if container == null:
		return
	for child in container.get_children():
		container.remove_child(child)
		child.queue_free()


func _count_expected_drawable_cells() -> int:
	if _battle_state == null:
		return 0
	var count := 0
	for coord_variant in _battle_state.cells.keys():
		if coord_variant is Vector2i and _is_cell_inside_battle(coord_variant):
			count += 1
	return count


func _count_rendered_top_cells() -> int:
	var count := 0
	for layer in _top_layers:
		if layer != null:
			count += layer.get_used_cells().size()
	return count


func _count_expected_rendered_units() -> int:
	if _battle_state == null:
		return 0
	var count := 0
	for unit_variant in _battle_state.units.values():
		var unit_state := unit_variant as BattleUnitState
		if unit_state != null and unit_state.is_alive:
			count += 1
	return count


func _count_rendered_units() -> int:
	return _unit_layer.get_child_count() if _unit_layer != null else 0


func _count_expected_rendered_props() -> int:
	if _battle_state == null:
		return 0
	var count := 0
	for cell_variant in _battle_state.cells.values():
		var cell_state := cell_variant as BattleCellState
		if cell_state == null or not _is_cell_inside_battle(cell_state.coord):
			continue
		count += _collect_prop_ids_for_cell(cell_state).size()
	return count


func _count_rendered_props() -> int:
	return _prop_layer.get_child_count() if _prop_layer != null else 0


func _collect_cells() -> Array[BattleCellState]:
	var cells: Array[BattleCellState] = []
	if _battle_state == null:
		return cells

	for cell_variant in _battle_state.cells.values():
		var cell_state := cell_variant as BattleCellState
		if cell_state != null:
			cells.append(cell_state)

	cells.sort_custom(func(a: BattleCellState, b: BattleCellState) -> bool:
		if a.coord.y == b.coord.y:
			return a.coord.x < b.coord.x
		return a.coord.y < b.coord.y
	)
	return cells


func _apply_tileset_to_layers() -> void:
	if _tile_set == null:
		return

	if _input_layer != null:
		_input_layer.tile_set = _tile_set
	for layer in _top_layers:
		if layer != null:
			layer.tile_set = _tile_set
	for layer in _edge_drop_east_layers:
		if layer != null:
			layer.tile_set = _tile_set
	for layer in _edge_drop_south_layers:
		if layer != null:
			layer.tile_set = _tile_set
	for layer in _wall_east_layers:
		if layer != null:
			layer.tile_set = _tile_set
	for layer in _wall_south_layers:
		if layer != null:
			layer.tile_set = _tile_set
	for layer in _overlay_layers:
		if layer != null:
			layer.tile_set = _tile_set
	for layer in _marker_layers:
		if layer != null:
			layer.tile_set = _tile_set


func _apply_layer_offsets() -> void:
	var height_step := _get_visual_height_step()
	for index in range(_top_layers.size()):
		var top_layer := _top_layers[index]
		if top_layer != null:
			top_layer.position = Vector2(0.0, -float(index) * height_step)
	for index in range(_edge_drop_east_layers.size()):
		var east_layer := _edge_drop_east_layers[index]
		if east_layer != null:
			east_layer.position = Vector2(0.0, -float(index + 1) * height_step)
	for index in range(_edge_drop_south_layers.size()):
		var south_layer := _edge_drop_south_layers[index]
		if south_layer != null:
			south_layer.position = Vector2(0.0, -float(index + 1) * height_step)
	for index in range(_wall_east_layers.size()):
		var wall_east_layer := _wall_east_layers[index]
		if wall_east_layer != null:
			wall_east_layer.position = Vector2(0.0, -float(index) * height_step)
	for index in range(_wall_south_layers.size()):
		var wall_south_layer := _wall_south_layers[index]
		if wall_south_layer != null:
			wall_south_layer.position = Vector2(0.0, -float(index) * height_step)
	for index in range(_overlay_layers.size()):
		var overlay_layer := _overlay_layers[index]
		if overlay_layer != null:
			overlay_layer.position = Vector2(0.0, -float(index) * height_step)
	for index in range(_marker_layers.size()):
		var marker_layer := _marker_layers[index]
		if marker_layer != null:
			marker_layer.position = Vector2(0.0, -float(index) * height_step)


func _apply_layer_draw_order() -> void:
	if _input_layer != null:
		_input_layer.z_index = TOP_LAYER_Z_BASE - LAYER_Z_STRIDE
	for index in range(_top_layers.size()):
		var top_layer := _top_layers[index]
		if top_layer != null:
			top_layer.z_index = TOP_LAYER_Z_BASE + index * LAYER_Z_STRIDE
	for index in range(_edge_drop_east_layers.size()):
		var east_layer := _edge_drop_east_layers[index]
		if east_layer != null:
			east_layer.z_index = TOP_LAYER_Z_BASE + (index + 1) * LAYER_Z_STRIDE + EDGE_DROP_EAST_LAYER_Z_OFFSET
	for index in range(_edge_drop_south_layers.size()):
		var south_layer := _edge_drop_south_layers[index]
		if south_layer != null:
			south_layer.z_index = TOP_LAYER_Z_BASE + (index + 1) * LAYER_Z_STRIDE + EDGE_DROP_SOUTH_LAYER_Z_OFFSET
	for index in range(_wall_east_layers.size()):
		var wall_east_layer := _wall_east_layers[index]
		if wall_east_layer != null:
			wall_east_layer.z_index = TOP_LAYER_Z_BASE + index * LAYER_Z_STRIDE + WALL_EAST_LAYER_Z_OFFSET
	for index in range(_wall_south_layers.size()):
		var wall_south_layer := _wall_south_layers[index]
		if wall_south_layer != null:
			wall_south_layer.z_index = TOP_LAYER_Z_BASE + index * LAYER_Z_STRIDE + WALL_SOUTH_LAYER_Z_OFFSET
	for index in range(_overlay_layers.size()):
		var overlay_layer := _overlay_layers[index]
		if overlay_layer != null:
			overlay_layer.z_index = TOP_LAYER_Z_BASE + index * LAYER_Z_STRIDE + OVERLAY_LAYER_Z_OFFSET
	for index in range(_marker_layers.size()):
		var marker_layer := _marker_layers[index]
		if marker_layer != null:
			marker_layer.z_index = TOP_LAYER_Z_BASE + index * LAYER_Z_STRIDE + MARKER_LAYER_Z_OFFSET
	if _prop_layer != null:
		_prop_layer.z_index = PROP_LAYER_Z
	if _unit_layer != null:
		_unit_layer.z_index = UNIT_LAYER_Z
	if _target_highlight_layer != null:
		_target_highlight_layer.z_index = TARGET_HIGHLIGHT_LAYER_Z


func _create_prop_node(cell_state: BattleCellState, prop_id: StringName, stack_index: int) -> BattleBoardProp:
	var prop_instance := BATTLE_BOARD_PROP_SCENE.instantiate()
	var prop_node := prop_instance as BattleBoardProp
	if prop_node == null:
		return null

	var height_value := clampi(int(cell_state.current_height), 0, MAX_HEIGHT_LAYERS - 1)
	var anchor := _get_cell_anchor_position(cell_state.coord, height_value)
	var render_depth := _get_cell_render_depth(cell_state.coord, height_value)
	prop_node.name = "%s_%d_%d_%d" % [String(prop_id), cell_state.coord.x, cell_state.coord.y, stack_index]
	prop_node.position = anchor + _get_prop_offset(prop_id, cell_state.coord, stack_index)
	prop_node.z_index = render_depth
	prop_node.set_meta("sort_anchor_y", anchor.y)
	prop_node.set_meta("sort_depth", render_depth)
	prop_node.set_meta("board_coord", cell_state.coord)
	prop_node.set_meta("prop_id", prop_id)
	prop_node.configure(
		prop_id,
		_build_coord_hash(cell_state.coord, stack_index + BattleBoardPropCatalog.get_sort_priority(prop_id)),
		BattleBoardPropCatalog.requires_interaction_shape(prop_id)
	)
	return prop_node


func _collect_prop_ids_for_cell(cell_state: BattleCellState) -> Array[StringName]:
	var prop_ids: Array[StringName] = []
	if cell_state == null:
		return prop_ids
	if cell_state.base_terrain == TERRAIN_SPIKE:
		prop_ids.append(BattleBoardPropCatalog.PROP_SPIKE_BARRICADE)
	for prop_id in cell_state.prop_ids:
		if not BattleBoardPropCatalog.is_supported(prop_id):
			continue
		if prop_ids.has(prop_id):
			continue
		prop_ids.append(prop_id)
	prop_ids.sort_custom(func(a: StringName, b: StringName) -> bool:
		return BattleBoardPropCatalog.get_sort_priority(a) < BattleBoardPropCatalog.get_sort_priority(b)
	)
	return prop_ids


func _get_prop_offset(prop_id: StringName, coord: Vector2i, stack_index: int) -> Vector2:
	var side_sign := 1.0 if _get_variant_index(coord, 2, stack_index + 1) == 0 else -1.0
	if _render_profile == null:
		_render_profile = BattleBoardRenderProfile.for_terrain_profile_id(_tile_profile_id)
	return _render_profile.get_prop_anchor_bias(prop_id, side_sign)


func _get_cell_anchor_position(coord: Vector2i, height_value: int) -> Vector2:
	if _input_layer == null:
		return Vector2.ZERO
	var anchor := _get_cell_plane_position(coord)
	anchor.y -= float(clampi(height_value, 0, MAX_HEIGHT_LAYERS - 1)) * _get_visual_height_step()
	return anchor


func _get_cell_plane_position(coord: Vector2i) -> Vector2:
	if _input_layer == null:
		return Vector2.ZERO
	return _input_layer.map_to_local(coord)


func _get_cell_render_depth(coord: Vector2i, height_value: int) -> int:
	var plane_position := _get_cell_plane_position(coord)
	var clamped_height := clampi(height_value, 0, MAX_HEIGHT_LAYERS - 1)
	var height_step := maxf(_get_visual_height_step(), 1.0)
	var plane_depth := plane_position.y / height_step * float(LAYER_Z_STRIDE)
	return int(round(plane_depth + float(clamped_height) * float(LAYER_Z_STRIDE) + float(DYNAMIC_LAYER_Z_OFFSET)))


func _get_cell_height_index(coord: Vector2i) -> int:
	if _battle_state == null:
		return 0
	var cell := _battle_state.cells.get(coord) as BattleCellState
	if cell == null:
		return 0
	return clampi(int(cell.current_height), 0, MAX_HEIGHT_LAYERS - 1)


func _ensure_tileset(profile_id: StringName) -> void:
	var render_profile := BattleBoardRenderProfile.for_terrain_profile_id(profile_id)
	var cache_key := render_profile.get_cache_key()
	if _tile_set != null and _tile_profile_id == render_profile.terrain_profile_id:
		_render_profile = render_profile
		return
	if _tileset_cache.has(cache_key):
		var cached_profile: Variant = _tileset_cache.get(cache_key, {})
		if cached_profile is Dictionary:
			_tile_profile_id = render_profile.terrain_profile_id
			_render_profile = render_profile
			_tile_set = cached_profile.get("tile_set", null) as TileSet
			_source_ids = (cached_profile.get("source_ids", {}) as Dictionary).duplicate(true)
			return

	_tile_profile_id = render_profile.terrain_profile_id
	_render_profile = render_profile
	_tile_set = null
	_source_ids.clear()
	_tile_set = TileSet.new()
	_tile_set.tile_size = render_profile.board_tile_size
	_tile_set.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC
	_tile_set.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_DOWN
	_tile_set.tile_offset_axis = TileSet.TILE_OFFSET_AXIS_HORIZONTAL
	_register_profile_textures(render_profile)
	_tileset_cache[cache_key] = {
		"tile_set": _tile_set,
		"source_ids": _source_ids.duplicate(true),
	}


func _register_profile_textures(render_profile: BattleBoardRenderProfile) -> void:
	if render_profile == null:
		render_profile = BattleBoardRenderProfile.for_terrain_profile_id(BattleBoardRenderProfile.TERRAIN_PROFILE_DEFAULT)
	var tile_dir := render_profile.asset_dir
	for source_spec in render_profile.get_source_specs():
		var file_names := source_spec.get("files", []) as Array
		var textures: Array = []
		for file_name_variant in file_names:
			var file_name := String(file_name_variant)
			var texture := _load_texture_from_png("%s/%s" % [tile_dir, file_name])
			if texture == null:
				push_error("BattleBoardController 缺少地形贴图：%s/%s" % [tile_dir, file_name])
				continue
			textures.append(texture)
		if textures.is_empty() and bool(source_spec.get("allow_generated_fallback", true)):
			var fallback_texture := _build_missing_source_texture(StringName(source_spec.get("key", &"")), source_spec)
			if fallback_texture != null:
				textures.append(fallback_texture)
		_register_source_variants(StringName(source_spec.get("key", "")), textures, source_spec)
	var generated_marker_spec := _build_generated_marker_source_spec(render_profile)
	_register_source_variants(
		SOURCE_ACTIVE_SELECTED,
		[_build_active_selected_marker_texture(render_profile)],
		generated_marker_spec
	)
	_register_source_variants(
		SOURCE_MOVE_REACHABLE,
		[_build_move_reachable_marker_texture(render_profile)],
		generated_marker_spec
	)


func _add_atlas_source(texture: Texture2D, source_spec: Dictionary) -> int:
	var source := TileSetAtlasSource.new()
	source.texture = texture
	source.texture_region_size = source_spec.get("atlas_region_size", _get_board_tile_size())
	source.use_texture_padding = false
	source.create_tile(Vector2i.ZERO, Vector2i.ONE)
	var tile_data := source.get_tile_data(Vector2i.ZERO, 0)
	if tile_data != null:
		tile_data.texture_origin = source_spec.get("visual_origin", source_spec.get("texture_origin", Vector2i.ZERO))
	return _tile_set.add_source(source)


func _register_source_variants(source_key: StringName, textures: Array, source_spec: Dictionary) -> void:
	var source_ids: Array[int] = []
	for texture_variant in textures:
		var texture := texture_variant as Texture2D
		if texture == null:
			continue
		source_ids.append(_add_atlas_source(texture, source_spec))
	_source_ids[source_key] = source_ids


func _build_active_selected_marker_texture(render_profile: BattleBoardRenderProfile) -> Texture2D:
	var tile_dir := render_profile.asset_dir
	var cache_key := "__generated_active_selected__%s" % render_profile.get_cache_key()
	if _texture_cache.has(cache_key):
		return _texture_cache.get(cache_key) as Texture2D

	# Active-unit highlighting should read as a solid tile-cover, not a translucent frame.
	var base_texture := _load_texture_from_png("%s/%s" % [tile_dir, render_profile.get_primary_land_file()])
	if base_texture == null:
		base_texture = _load_texture_from_png("%s/%s" % [tile_dir, render_profile.get_selected_marker_file()])
	if base_texture == null:
		return _build_diamond_texture(ACTIVE_SELECTED_MARKER_COLOR, 1.0, render_profile.board_tile_size)

	var image := base_texture.get_image()
	if image == null or image.is_empty():
		return null
	image = image.duplicate()
	image.convert(Image.FORMAT_RGBA8)
	for y in range(image.get_height()):
		for x in range(image.get_width()):
			var pixel := image.get_pixel(x, y)
			if pixel.a <= 0.0:
				continue
			image.set_pixel(
				x,
				y,
				Color(
					ACTIVE_SELECTED_MARKER_COLOR.r,
					ACTIVE_SELECTED_MARKER_COLOR.g,
					ACTIVE_SELECTED_MARKER_COLOR.b,
					1.0
				)
			)

	var generated_texture := ImageTexture.create_from_image(image)
	_texture_cache[cache_key] = generated_texture
	return generated_texture


func _build_move_reachable_marker_texture(render_profile: BattleBoardRenderProfile) -> Texture2D:
	var tile_dir := render_profile.asset_dir
	var cache_key := "__generated_move_reachable__%s" % render_profile.get_cache_key()
	if _texture_cache.has(cache_key):
		return _texture_cache.get(cache_key) as Texture2D

	var base_texture := _load_texture_from_png("%s/%s" % [tile_dir, render_profile.get_primary_land_file()])
	if base_texture == null:
		base_texture = _load_texture_from_png("%s/%s" % [tile_dir, render_profile.get_selected_marker_file()])
	if base_texture == null:
		return _build_diamond_texture(MOVE_REACHABLE_MARKER_COLOR_LIGHT, 0.42, render_profile.board_tile_size)

	var image := base_texture.get_image()
	if image == null or image.is_empty():
		return null
	image = image.duplicate()
	image.convert(Image.FORMAT_RGBA8)
	for y in range(image.get_height()):
		for x in range(image.get_width()):
			var pixel := image.get_pixel(x, y)
			if pixel.a <= 0.0:
				continue
			var shade := clampf(pixel.get_luminance(), 0.0, 1.0)
			var mix_ratio := clampf(0.25 + shade * 0.5, 0.0, 1.0)
			var tinted_color := MOVE_REACHABLE_MARKER_COLOR_DARK.lerp(MOVE_REACHABLE_MARKER_COLOR_LIGHT, mix_ratio)
			var alpha := lerpf(0.3, 0.5, shade)
			image.set_pixel(
				x,
				y,
				Color(tinted_color.r, tinted_color.g, tinted_color.b, alpha)
			)

	var generated_texture := ImageTexture.create_from_image(image)
	_texture_cache[cache_key] = generated_texture
	return generated_texture


func _load_texture_from_png(path: String) -> Texture2D:
	if path.is_empty():
		return null
	if _texture_cache.has(path):
		return _texture_cache.get(path) as Texture2D

	var texture: Texture2D = null
	if FileAccess.file_exists("%s.import" % path):
		texture = ResourceLoader.load(path, "Texture2D", ResourceLoader.CACHE_MODE_REUSE) as Texture2D
	if texture == null and FileAccess.file_exists(path):
		var image := Image.new()
		var error := image.load_png_from_buffer(FileAccess.get_file_as_bytes(path))
		if error == OK:
			texture = ImageTexture.create_from_image(image)
	if texture == null:
		texture = ResourceLoader.load(path, "Texture2D", ResourceLoader.CACHE_MODE_REUSE) as Texture2D
	_texture_cache[path] = texture
	return texture


func _build_missing_source_texture(source_key: StringName, source_spec: Dictionary) -> Texture2D:
	var color := Color(0.5, 0.42, 0.32, 0.9)
	match source_key:
		SOURCE_WATER:
			color = Color(0.24, 0.47, 0.66, 0.86)
		SOURCE_MUD:
			color = Color(0.43, 0.31, 0.18, 0.88)
		SOURCE_EDGE_DROP_EAST, SOURCE_EDGE_DROP_SOUTH, SOURCE_WALL_EAST, SOURCE_WALL_SOUTH:
			color = Color(0.31, 0.25, 0.2, 0.92)
		SOURCE_SCRUB:
			color = Color(0.22, 0.45, 0.24, 0.68)
		SOURCE_RUBBLE:
			color = Color(0.42, 0.39, 0.34, 0.72)
		SOURCE_SELECTED:
			color = Color(0.98, 0.92, 0.42, 0.42)
		SOURCE_PREVIEW:
			color = Color(0.88, 0.82, 0.36, 0.34)
		_:
			color = Color(0.5, 0.42, 0.32, 0.9)
	var tile_size: Vector2i = source_spec.get("board_tile_size", _get_board_tile_size())
	return _build_diamond_texture(color, color.a, tile_size)


func _build_generated_marker_source_spec(render_profile: BattleBoardRenderProfile) -> Dictionary:
	return {
		"key": SOURCE_ACTIVE_SELECTED,
		"files": [],
		"atlas_region_size": render_profile.board_tile_size,
		"board_tile_size": render_profile.board_tile_size,
		"texture_origin": Vector2i.ZERO,
		"visual_origin": Vector2i.ZERO,
		"layer_role": BattleBoardRenderProfile.LAYER_ROLE_MARKER,
	}


func _build_diamond_texture(color: Color, alpha: float, tile_size: Vector2i) -> Texture2D:
	var safe_tile_size := Vector2i(maxi(tile_size.x, 2), maxi(tile_size.y, 2))
	var image := Image.create(safe_tile_size.x, safe_tile_size.y, false, Image.FORMAT_RGBA8)
	image.fill(Color(0.0, 0.0, 0.0, 0.0))
	var center := Vector2(float(safe_tile_size.x - 1) * 0.5, float(safe_tile_size.y - 1) * 0.5)
	var half_size := Vector2(maxf(float(safe_tile_size.x) * 0.5, 1.0), maxf(float(safe_tile_size.y) * 0.5, 1.0))
	for y in range(safe_tile_size.y):
		for x in range(safe_tile_size.x):
			var delta := Vector2(float(x), float(y)) - center
			if absf(delta.x) / half_size.x + absf(delta.y) / half_size.y <= 1.0:
				image.set_pixel(x, y, Color(color.r, color.g, color.b, alpha))
	return ImageTexture.create_from_image(image)


func _get_visual_height_step() -> float:
	if _render_profile == null:
		_render_profile = BattleBoardRenderProfile.for_terrain_profile_id(_tile_profile_id)
	return _render_profile.visual_height_step


func _get_unit_anchor_bias() -> Vector2:
	if _render_profile == null:
		_render_profile = BattleBoardRenderProfile.for_terrain_profile_id(_tile_profile_id)
	return _render_profile.unit_anchor_bias


func _get_board_tile_size() -> Vector2i:
	if _render_profile == null:
		_render_profile = BattleBoardRenderProfile.for_terrain_profile_id(_tile_profile_id)
	return _render_profile.board_tile_size


func _resolve_tile_profile_id() -> StringName:
	if _battle_state == null:
		return BattleBoardRenderProfile.TERRAIN_PROFILE_DEFAULT
	return BattleBoardRenderProfile.normalize_terrain_profile_id(_battle_state.terrain_profile_id)


func _get_source_id(source_key: StringName, coord: Vector2i = INVALID_VARIANT_COORD, salt: int = 0) -> int:
	if _source_ids.has(source_key):
		var source_variants_raw: Variant = _source_ids[source_key]
		if source_variants_raw is Array:
			var source_variants: Array = source_variants_raw
			if source_variants.is_empty():
				return -1
			if coord == INVALID_VARIANT_COORD or source_variants.size() == 1:
				return int(source_variants[0])
			return int(source_variants[_get_variant_index(coord, source_variants.size(), salt)])
	return -1


func _get_selected_marker_source_id(coord: Vector2i) -> int:
	if _is_active_unit_coord(coord):
		var active_source_id := _get_source_id(SOURCE_ACTIVE_SELECTED)
		if active_source_id >= 0:
			return active_source_id
	return _get_source_id(SOURCE_SELECTED)


func _get_move_reachable_marker_source_id() -> int:
	var move_source_id := _get_source_id(SOURCE_MOVE_REACHABLE)
	if move_source_id >= 0:
		return move_source_id
	return _get_source_id(SOURCE_SELECTED)


func _is_active_unit_coord(coord: Vector2i) -> bool:
	if _battle_state == null:
		return false
	var active_unit := _battle_state.units.get(_battle_state.active_unit_id) as BattleUnitState
	if active_unit == null or not active_unit.is_alive:
		return false
	active_unit.refresh_footprint()
	return active_unit.occupied_coords.has(coord)


func _get_top_source_id(terrain: String, coord: Vector2i) -> int:
	match StringName(terrain):
		TERRAIN_LAND:
			return _get_source_id(SOURCE_LAND, coord)
		TERRAIN_FOREST:
			return _get_source_id(SOURCE_LAND, coord, 1)
		TERRAIN_WATER, TERRAIN_SHALLOW_WATER, TERRAIN_FLOWING_WATER, TERRAIN_DEEP_WATER:
			return _get_source_id(SOURCE_WATER, coord)
		TERRAIN_MUD:
			return _get_source_id(SOURCE_MUD, coord)
		TERRAIN_SPIKE:
			return _get_source_id(SOURCE_LAND, coord, 2)
		_:
			return _get_source_id(SOURCE_LAND, coord)


func _get_overlay_source_id(terrain: String, coord: Vector2i) -> int:
	match StringName(terrain):
		TERRAIN_FOREST:
			return _get_source_id(SOURCE_SCRUB, coord)
		TERRAIN_SPIKE:
			return _get_source_id(SOURCE_RUBBLE, coord)
		_:
			return -1


func _get_variant_index(coord: Vector2i, variant_count: int, salt: int = 0) -> int:
	if variant_count <= 1:
		return 0
	return _build_coord_hash(coord, salt) % variant_count


func _build_coord_hash(coord: Vector2i, salt: int = 0) -> int:
	var hash_value := coord.x * 73856093
	hash_value += coord.y * 19349663
	hash_value += String(_tile_profile_id).hash() * 83492791
	hash_value += salt * 1640531513
	return absi(hash_value)


func _get_neighbor_height(coord: Vector2i, offset: Vector2i) -> int:
	if _battle_state == null:
		return 0
	var neighbor_height := 0
	var neighbor := _battle_state.cells.get(coord + offset) as BattleCellState
	if neighbor != null:
		neighbor_height = int(neighbor.current_height)
	return neighbor_height


func _build_unit_short_name(unit_state: BattleUnitState) -> String:
	if unit_state == null:
		return "?"
	if not unit_state.display_name.is_empty():
		return unit_state.display_name.substr(0, 1)
	return String(unit_state.unit_id).substr(0, 1)


func _get_unit_hp_max(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return 1
	var snapshot_hp_max := 0
	if unit_state.attribute_snapshot != null:
		snapshot_hp_max = int(unit_state.attribute_snapshot.get_value(HP_MAX_ATTRIBUTE_ID))
	return maxi(maxi(snapshot_hp_max, int(unit_state.current_hp)), 1)


func _get_unit_health_bar_fill_color(hp_ratio: float) -> Color:
	var clamped_ratio := clampf(hp_ratio, 0.0, 1.0)
	if clamped_ratio <= 0.35:
		return UNIT_HEALTH_BAR_LOW_COLOR.lerp(
			UNIT_HEALTH_BAR_MID_COLOR,
			inverse_lerp(0.0, 0.35, clamped_ratio)
		)
	if clamped_ratio <= 0.7:
		return UNIT_HEALTH_BAR_MID_COLOR
	return UNIT_HEALTH_BAR_MID_COLOR.lerp(
		UNIT_HEALTH_BAR_HIGH_COLOR,
		inverse_lerp(0.7, 1.0, clamped_ratio)
	)


func _get_unit_color(unit_state: BattleUnitState) -> Color:
	if unit_state == null:
		return Color(0.78, 0.8, 0.84, 0.94)
	if String(unit_state.faction_id) == "player":
		return Color(0.96, 0.86, 0.38, 0.96)
	if String(unit_state.faction_id) == "hostile":
		return Color(0.9, 0.32, 0.22, 0.96)
	return Color(0.7, 0.74, 0.78, 0.92)


func _is_cell_inside_battle(coord: Vector2i) -> bool:
	if _battle_state == null:
		return false
	return coord.x >= 0 and coord.y >= 0 and coord.x < _battle_state.map_size.x and coord.y < _battle_state.map_size.y
