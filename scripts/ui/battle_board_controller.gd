## 文件说明：该脚本属于战斗棋盘控制器相关的控制器脚本，集中维护输入层、顶部层集合、悬崖东侧层集合等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name BattleBoardController
extends RefCounted

const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const BattleEdgeFaceState = preload("res://scripts/systems/battle_edge_face_state.gd")
const BATTLE_EDGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle_edge_service.gd")
const BattleEdgeService = preload("res://scripts/systems/battle_edge_service.gd")
const BattleBoardProp = preload("res://scripts/ui/battle_board_prop.gd")
const BattleBoardPropCatalog = preload("res://scripts/utils/battle_board_prop_catalog.gd")
const BATTLE_BOARD_PROP_SCENE = preload("res://scenes/common/battle_board_prop.tscn")

const TILE_SIZE := Vector2i(64, 32)
const HEIGHT_STEP := 16.0
const MAX_HEIGHT_LAYERS := 9
const TOP_LAYER_Z_BASE := 0
const LAYER_Z_STRIDE := 10
const EDGE_DROP_EAST_LAYER_Z_OFFSET := -4
const EDGE_DROP_SOUTH_LAYER_Z_OFFSET := -3
const WALL_EAST_LAYER_Z_OFFSET := -2
const WALL_SOUTH_LAYER_Z_OFFSET := -1
const OVERLAY_LAYER_Z_OFFSET := 6
const MARKER_LAYER_Z_BASE := 1000
const PROP_LAYER_Z := 1100
const UNIT_LAYER_Z := 1200
const TARGET_HIGHLIGHT_LAYER_Z := 1300
const PROFILE_DEFAULT := &"default"
const PROFILE_CANYON := &"canyon"
const SHARED_TILE_DIR := "res://assets/main/battle/terrain/canyon"
const TEXTURED_TOP_LAND_FILES := [
	"top_land_01.png",
	"top_land_02.png",
	"top_land_03.png",
]
const TEXTURED_TOP_WATER_FILES := [
	"top_water_01.png",
	"top_water_02.png",
	"top_water_03.png",
]
const TEXTURED_TOP_MUD_FILES := [
	"top_mud_01.png",
	"top_mud_02.png",
	"top_mud_03.png",
]
const TEXTURED_OVERLAY_SCRUB_FILES := [
	"overlay_scrub_01.png",
	"overlay_scrub_02.png",
	"overlay_scrub_03.png",
]
const TEXTURED_OVERLAY_RUBBLE_FILES := [
	"overlay_rubble_01.png",
	"overlay_rubble_02.png",
	"overlay_rubble_03.png",
]
const TEXTURED_EDGE_DROP_EAST_FILES := [
	"cliff_east_01.png",
	"cliff_east_02.png",
	"cliff_east_03.png",
]
const TEXTURED_EDGE_DROP_SOUTH_FILES := [
	"cliff_south_01.png",
	"cliff_south_02.png",
	"cliff_south_03.png",
]
const TEXTURED_WALL_EAST_FILES := [
	"wall_east_01.png",
	"wall_east_02.png",
	"wall_east_03.png",
]
const TEXTURED_WALL_SOUTH_FILES := [
	"wall_south_01.png",
	"wall_south_02.png",
	"wall_south_03.png",
]
const TEXTURED_SELECTED_FILES := [
	"marker_selected.png",
]
const TEXTURED_PREVIEW_FILES := [
	"marker_preview.png",
]
const ACTIVE_SELECTED_MARKER_COLOR := Color(0.0, 0.0, 1.0, 1.0)
const MOVE_REACHABLE_MARKER_COLOR_DARK := Color(0.14, 0.37, 0.5, 1.0)
const MOVE_REACHABLE_MARKER_COLOR_LIGHT := Color(0.46, 0.72, 0.84, 1.0)
const VALID_TARGET_HIGHLIGHT_COLOR := Color(0.92, 0.12, 0.08, 0.42)
const LOCKED_TARGET_HIGHLIGHT_COLOR := Color(0.96, 0.82, 0.28, 0.54)
const CONFIRM_READY_TARGET_HIGHLIGHT_COLOR := Color(0.28, 0.8, 0.5, 0.5)
const CONFIRM_READY_FOCUS_HALO_COLOR := Color(0.98, 0.9, 0.34, 0.35)

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

	_ensure_tileset(PROFILE_DEFAULT)
	_apply_tileset_to_layers()
	_apply_layer_offsets()
	_apply_layer_draw_order()


func configure(
	battle_state: BattleState,
	selected_coord: Vector2i,
	preview_target_coords: Array[Vector2i] = [],
	target_selection_mode: StringName = &"single_unit",
	min_target_count: int = 1,
	max_target_count: int = 1
) -> void:
	_battle_state = battle_state
	_selected_coord = selected_coord
	_preview_target_coords = preview_target_coords.duplicate()
	_target_selection_mode = target_selection_mode if target_selection_mode != &"" else &"single_unit"
	_target_min_count = maxi(min_target_count, 1)
	_target_max_count = maxi(max_target_count, _target_min_count)
	_refresh_tileset_profile()
	_redraw()


func update_markers(
	selected_coord: Vector2i,
	preview_target_coords: Array[Vector2i] = [],
	valid_target_coords: Array[Vector2i] = [],
	target_selection_mode: StringName = &"single_unit",
	min_target_count: int = 1,
	max_target_count: int = 1
) -> void:
	_selected_coord = selected_coord
	_preview_target_coords = preview_target_coords.duplicate()
	_valid_target_coords = valid_target_coords.duplicate()
	_target_selection_mode = target_selection_mode if target_selection_mode != &"" else &"single_unit"
	_target_min_count = maxi(min_target_count, 1)
	_target_max_count = maxi(max_target_count, _target_min_count)
	_draw_marker_layer()
	_draw_target_highlights()


func clear() -> void:
	_battle_state = null
	_selected_coord = Vector2i(-1, -1)
	_preview_target_coords.clear()
	_valid_target_coords.clear()
	_clear_tile_layers()
	_clear_dynamic_nodes()


func _refresh_tileset_profile() -> void:
	var desired_profile := _resolve_tile_profile_id()
	if desired_profile == _tile_profile_id and _tile_set != null:
		return
	_ensure_tileset(desired_profile)
	_apply_tileset_to_layers()


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
	token.position = anchor + Vector2(0.0, -8.0)
	token.z_index = render_depth
	token.set_meta("sort_anchor_y", anchor.y)
	token.set_meta("sort_depth", render_depth)
	token.set_meta("board_coord", unit_state.coord)

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

	if unit_state.unit_id == _battle_state.active_unit_id:
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
	label.text = _build_unit_short_name(unit_state)
	label.position = Vector2(-10.0, -34.0)
	label.add_theme_font_size_override("font_size", 14)
	label.add_theme_color_override("font_color", Color(0.98, 0.96, 0.9, 0.98))
	label.add_theme_color_override("font_shadow_color", Color(0.16, 0.1, 0.06, 0.92))
	label.add_theme_constant_override("shadow_offset_x", 1)
	label.add_theme_constant_override("shadow_offset_y", 1)
	token.add_child(label)

	return token


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
	for index in range(_top_layers.size()):
		var top_layer := _top_layers[index]
		if top_layer != null:
			top_layer.position = Vector2(0.0, -float(index) * HEIGHT_STEP)
	for index in range(_edge_drop_east_layers.size()):
		var east_layer := _edge_drop_east_layers[index]
		if east_layer != null:
			east_layer.position = Vector2(0.0, -float(index + 1) * HEIGHT_STEP)
	for index in range(_edge_drop_south_layers.size()):
		var south_layer := _edge_drop_south_layers[index]
		if south_layer != null:
			south_layer.position = Vector2(0.0, -float(index + 1) * HEIGHT_STEP)
	for index in range(_wall_east_layers.size()):
		var wall_east_layer := _wall_east_layers[index]
		if wall_east_layer != null:
			wall_east_layer.position = Vector2(0.0, -float(index) * HEIGHT_STEP)
	for index in range(_wall_south_layers.size()):
		var wall_south_layer := _wall_south_layers[index]
		if wall_south_layer != null:
			wall_south_layer.position = Vector2(0.0, -float(index) * HEIGHT_STEP)
	for index in range(_overlay_layers.size()):
		var overlay_layer := _overlay_layers[index]
		if overlay_layer != null:
			overlay_layer.position = Vector2(0.0, -float(index) * HEIGHT_STEP)
	for index in range(_marker_layers.size()):
		var marker_layer := _marker_layers[index]
		if marker_layer != null:
			marker_layer.position = Vector2(0.0, -float(index) * HEIGHT_STEP)


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
			marker_layer.z_index = MARKER_LAYER_Z_BASE + index
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

	var anchor := _get_cell_anchor_position(cell_state.coord, int(cell_state.current_height))
	var ground_anchor_y := anchor.y
	prop_node.name = "%s_%d_%d_%d" % [String(prop_id), cell_state.coord.x, cell_state.coord.y, stack_index]
	prop_node.position = anchor + _get_prop_offset(prop_id, cell_state.coord, stack_index)
	prop_node.z_index = int(round(ground_anchor_y))
	prop_node.set_meta("sort_anchor_y", ground_anchor_y)
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
	match prop_id:
		BattleBoardPropCatalog.PROP_TENT:
			return Vector2(side_sign * 11.0, 0.0)
		BattleBoardPropCatalog.PROP_TORCH:
			return Vector2(side_sign * 14.0, -2.0)
		BattleBoardPropCatalog.PROP_OBJECTIVE_MARKER:
			return Vector2(0.0, -4.0)
		_:
			return Vector2(0.0, -2.0)


func _get_cell_anchor_position(coord: Vector2i, height_value: int) -> Vector2:
	if _input_layer == null:
		return Vector2.ZERO
	var anchor := _get_cell_plane_position(coord)
	anchor.y -= float(clampi(height_value, 0, MAX_HEIGHT_LAYERS - 1)) * HEIGHT_STEP
	return anchor


func _get_cell_plane_position(coord: Vector2i) -> Vector2:
	if _input_layer == null:
		return Vector2.ZERO
	return _input_layer.map_to_local(coord)


func _get_cell_render_depth(coord: Vector2i, height_value: int) -> int:
	var plane_position := _get_cell_plane_position(coord)
	var clamped_height := clampi(height_value, 0, MAX_HEIGHT_LAYERS - 1)
	return int(round(plane_position.y + float(clamped_height) * HEIGHT_STEP))


func _get_cell_height_index(coord: Vector2i) -> int:
	if _battle_state == null:
		return 0
	var cell := _battle_state.cells.get(coord) as BattleCellState
	if cell == null:
		return 0
	return clampi(int(cell.current_height), 0, MAX_HEIGHT_LAYERS - 1)


func _ensure_tileset(profile_id: StringName) -> void:
	var cache_key := _resolve_tile_cache_key(profile_id)
	if _tile_set != null and _tile_profile_id == profile_id:
		return
	if _tileset_cache.has(cache_key):
		var cached_profile: Variant = _tileset_cache.get(cache_key, {})
		if cached_profile is Dictionary:
			_tile_profile_id = profile_id
			_tile_set = cached_profile.get("tile_set", null) as TileSet
			_source_ids = (cached_profile.get("source_ids", {}) as Dictionary).duplicate(true)
			return

	_tile_profile_id = profile_id
	_tile_set = null
	_source_ids.clear()
	_tile_set = TileSet.new()
	_tile_set.tile_size = TILE_SIZE
	_tile_set.tile_shape = TileSet.TILE_SHAPE_ISOMETRIC
	_tile_set.tile_layout = TileSet.TILE_LAYOUT_DIAMOND_DOWN
	_tile_set.tile_offset_axis = TileSet.TILE_OFFSET_AXIS_HORIZONTAL
	_register_profile_textures(profile_id)
	_tileset_cache[cache_key] = {
		"tile_set": _tile_set,
		"source_ids": _source_ids.duplicate(true),
	}


func _register_profile_textures(profile_id: StringName) -> void:
	var tile_dir := _resolve_tile_dir(profile_id)
	var source_specs := [
		{"key": SOURCE_LAND, "files": TEXTURED_TOP_LAND_FILES},
		{"key": SOURCE_WATER, "files": TEXTURED_TOP_WATER_FILES},
		{"key": SOURCE_MUD, "files": TEXTURED_TOP_MUD_FILES},
		{"key": SOURCE_EDGE_DROP_EAST, "files": TEXTURED_EDGE_DROP_EAST_FILES},
		{"key": SOURCE_EDGE_DROP_SOUTH, "files": TEXTURED_EDGE_DROP_SOUTH_FILES},
		{"key": SOURCE_WALL_EAST, "files": TEXTURED_WALL_EAST_FILES},
		{"key": SOURCE_WALL_SOUTH, "files": TEXTURED_WALL_SOUTH_FILES},
		{"key": SOURCE_SCRUB, "files": TEXTURED_OVERLAY_SCRUB_FILES},
		{"key": SOURCE_RUBBLE, "files": TEXTURED_OVERLAY_RUBBLE_FILES},
		{"key": SOURCE_SELECTED, "files": TEXTURED_SELECTED_FILES},
		{"key": SOURCE_PREVIEW, "files": TEXTURED_PREVIEW_FILES},
	]

	for source_spec in source_specs:
		var file_names := source_spec.get("files", []) as Array
		var textures: Array = []
		for file_name_variant in file_names:
			var file_name := String(file_name_variant)
			var texture := _load_texture_from_png("%s/%s" % [tile_dir, file_name])
			if texture == null:
				push_error("BattleBoardController 缺少地形贴图：%s/%s" % [tile_dir, file_name])
				continue
			textures.append(texture)
		_register_source_variants(StringName(source_spec.get("key", "")), textures)
	_register_source_variants(SOURCE_ACTIVE_SELECTED, [_build_active_selected_marker_texture(tile_dir)])
	_register_source_variants(SOURCE_MOVE_REACHABLE, [_build_move_reachable_marker_texture(tile_dir)])


func _resolve_tile_dir(profile_id: StringName) -> String:
	match profile_id:
		PROFILE_DEFAULT, PROFILE_CANYON:
			return SHARED_TILE_DIR
		_:
			return SHARED_TILE_DIR


func _resolve_tile_cache_key(profile_id: StringName) -> StringName:
	return StringName(_resolve_tile_dir(profile_id))


func _add_atlas_source(texture: Texture2D) -> int:
	var source := TileSetAtlasSource.new()
	source.texture = texture
	source.texture_region_size = TILE_SIZE
	source.use_texture_padding = false
	source.create_tile(Vector2i.ZERO, Vector2i.ONE)
	return _tile_set.add_source(source)


func _register_source_variants(source_key: StringName, textures: Array) -> void:
	var source_ids: Array[int] = []
	for texture_variant in textures:
		var texture := texture_variant as Texture2D
		if texture == null:
			continue
		source_ids.append(_add_atlas_source(texture))
	_source_ids[source_key] = source_ids


func _build_active_selected_marker_texture(tile_dir: String) -> Texture2D:
	var cache_key := "__generated_active_selected__%s" % tile_dir
	if _texture_cache.has(cache_key):
		return _texture_cache.get(cache_key) as Texture2D

	# Active-unit highlighting should read as a solid tile-cover, not a translucent frame.
	var base_texture := _load_texture_from_png("%s/%s" % [tile_dir, TEXTURED_TOP_LAND_FILES[0]])
	if base_texture == null:
		base_texture = _load_texture_from_png("%s/%s" % [tile_dir, TEXTURED_SELECTED_FILES[0]])
	if base_texture == null:
		return null

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


func _build_move_reachable_marker_texture(tile_dir: String) -> Texture2D:
	var cache_key := "__generated_move_reachable__%s" % tile_dir
	if _texture_cache.has(cache_key):
		return _texture_cache.get(cache_key) as Texture2D

	var base_texture := _load_texture_from_png("%s/%s" % [tile_dir, TEXTURED_TOP_LAND_FILES[0]])
	if base_texture == null:
		base_texture = _load_texture_from_png("%s/%s" % [tile_dir, TEXTURED_SELECTED_FILES[0]])
	if base_texture == null:
		return null

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

	var texture := ResourceLoader.load(path, "Texture2D", ResourceLoader.CACHE_MODE_REUSE) as Texture2D
	_texture_cache[path] = texture
	return texture


func _resolve_tile_profile_id() -> StringName:
	if _battle_state == null:
		return PROFILE_DEFAULT
	return PROFILE_CANYON if _battle_state.terrain_profile_id == PROFILE_CANYON else PROFILE_DEFAULT


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
