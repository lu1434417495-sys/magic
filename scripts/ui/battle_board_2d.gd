## 文件说明：该脚本属于战斗棋盘二维视图相关的界面脚本，集中维护输入层、顶部高度零层、顶部高度一层等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name BattleBoard2D
extends Node2D

## 信号说明：当战斗格子被点击时发出的信号，供外层接管选择、移动或交互逻辑。
signal battle_cell_clicked(coord: Vector2i)
## 信号说明：当战斗格子被右键点击时发出的信号，供外层执行二级交互、取消或上下文操作。
signal battle_cell_right_clicked(coord: Vector2i)

const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const BattleBoardController = preload("res://scripts/ui/battle_board_controller.gd")
const TILE_HALF_SIZE := Vector2(32.0, 16.0)
const HEIGHT_STEP := 16.0
const MAX_RENDER_HEIGHT := 8
const DEFAULT_CAMERA_ZOOM := 2.0
const MIN_CAMERA_ZOOM := 1.25
const MAX_CAMERA_ZOOM := 4.0
const CAMERA_ZOOM_STEP := 0.2
const CAMERA_EDGE_MARGIN_X := 0.0
const CAMERA_EDGE_MARGIN_Y := 72.0
const FOCUS_VIEWPORT_RATIO := Vector2(0.5, 0.44)

## 字段说明：缓存输入层节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var input_layer: TileMapLayer = %InputLayer
## 字段说明：缓存顶部层集合，便于统一适配 0..8 高度渲染并减少重复节点绑定。
@onready var top_layers: Array[TileMapLayer] = _collect_tile_layers("TopH", 0, MAX_RENDER_HEIGHT)
## 字段说明：缓存东侧落差面层集合，便于统一适配 1..8 高度差渲染并减少重复节点绑定。
@onready var edge_drop_east_layers: Array[TileMapLayer] = _collect_tile_layers("EdgeDropEastH", 1, MAX_RENDER_HEIGHT)
## 字段说明：缓存南侧落差面层集合，便于统一适配 1..8 高度差渲染并减少重复节点绑定。
@onready var edge_drop_south_layers: Array[TileMapLayer] = _collect_tile_layers("EdgeDropSouthH", 1, MAX_RENDER_HEIGHT)
## 字段说明：缓存东侧人工边特征层集合，便于统一适配 0..8 高度渲染并减少重复节点绑定。
@onready var wall_east_layers: Array[TileMapLayer] = _collect_tile_layers("WallEastH", 0, MAX_RENDER_HEIGHT)
## 字段说明：缓存南侧人工边特征层集合，便于统一适配 0..8 高度渲染并减少重复节点绑定。
@onready var wall_south_layers: Array[TileMapLayer] = _collect_tile_layers("WallSouthH", 0, MAX_RENDER_HEIGHT)
## 字段说明：缓存覆盖层集合，便于统一适配 0..8 高度渲染并减少重复节点绑定。
@onready var overlay_layers: Array[TileMapLayer] = _collect_tile_layers("OverlayH", 0, MAX_RENDER_HEIGHT)
## 字段说明：缓存标记层集合，便于统一适配 0..8 高度渲染并减少重复节点绑定。
@onready var marker_layers: Array[TileMapLayer] = _collect_tile_layers("MarkerH", 0, MAX_RENDER_HEIGHT)
## 字段说明：缓存场景装饰物层节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var prop_layer: Node2D = %PropLayer
## 字段说明：缓存单位层节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var unit_layer: Node2D = %UnitLayer
## 字段说明：缓存技能合法目标高亮层节点，用于把当前可点击地格绘制在最顶层。
@onready var target_highlight_layer: Node2D = %TargetHighlightLayer

## 字段说明：缓存控制器实例，作为界面刷新、输入处理和窗口联动的重要依据。
var _controller: BattleBoardController = BattleBoardController.new()
## 字段说明：用于标记当前是否处于绑定状态，避免在不合适的时机重复触发流程，作为界面刷新、输入处理和窗口联动的重要依据。
var _is_bound := false
## 字段说明：缓存待处理战斗状态实例，作为界面刷新、输入处理和窗口联动的重要依据。
var _pending_battle_state: BattleState = null
## 字段说明：记录待处理的选中坐标，用于定位对象、绘制内容或执行网格计算。
var _pending_selected_coord := Vector2i(-1, -1)
## 字段说明：保存待处理的预览目标坐标列表，供范围判定、占位刷新、批量渲染或目标选择复用。
var _pending_preview_target_coords: Array[Vector2i] = []
## 字段说明：保存待处理的技能合法目标坐标列表，用于渲染当前技能可点击格。
var _pending_valid_target_coords: Array[Vector2i] = []
## 字段说明：保存待处理的目标选择模式，供高亮表现层区分单目标与 multi_unit 语义。
var _pending_target_selection_mode: StringName = &"single_unit"
## 字段说明：保存待处理的最小目标数量，供高亮表现层表达确认阈值。
var _pending_target_min_count := 1
## 字段说明：保存待处理的最大目标数量，供高亮表现层表达自动施放阈值。
var _pending_target_max_count := 1
## 字段说明：记录视口尺寸，用于布局、碰撞、绘制或程序化生成时的尺寸计算。
var _viewport_size := Vector2.ZERO
## 字段说明：记录相机缩放，作为界面刷新、输入处理和窗口联动的重要依据。
var _camera_zoom := DEFAULT_CAMERA_ZOOM
## 字段说明：保存内容边界，便于顺序遍历、批量展示、批量运算和整体重建。
var _content_bounds := Rect2()
## 字段说明：用于标记当前是否已经具备内容边界，便于后续分支快速判断，作为界面刷新、输入处理和窗口联动的重要依据。
var _has_content_bounds := false
## 字段说明：用于标记相机已初始化当前是否成立或生效，供脚本后续分支判断使用，作为界面刷新、输入处理和窗口联动的重要依据。
var _camera_initialized := false
## 字段说明：记录上一次焦点坐标，用于定位对象、绘制内容或执行网格计算。
var _last_focus_coord := Vector2i(-9999, -9999)
## 字段说明：记录当前是否已进入手动缩放模式，避免每次刷新都覆盖玩家主动设置的镜头比例。
var _has_manual_zoom_override := false
## 字段说明：用于标记当前是否处于平移中状态，避免在不合适的时机重复触发流程，作为界面刷新、输入处理和窗口联动的重要依据。
var _is_panning := false
## 字段说明：记录上一次平移视口位置，作为界面刷新、输入处理和窗口联动的重要依据。
var _last_pan_viewport_position := Vector2.ZERO


func _ready() -> void:
	# Terrain layers rely on explicit per-layer z-order; only PropLayer/UnitLayer y-sort internally.
	y_sort_enabled = false
	scale = Vector2.ONE * _camera_zoom
	_bind_controller()
	_apply_pending_configuration()


func configure(
	battle_state: BattleState,
	selected_coord: Vector2i,
	preview_target_coords: Array[Vector2i] = [],
	valid_target_coords: Array[Vector2i] = [],
	target_selection_mode: StringName = &"single_unit",
	min_target_count: int = 1,
	max_target_count: int = 1
) -> void:
	_pending_battle_state = battle_state
	_pending_selected_coord = selected_coord
	_pending_preview_target_coords = preview_target_coords.duplicate()
	_pending_valid_target_coords = valid_target_coords.duplicate()
	_pending_target_selection_mode = target_selection_mode if target_selection_mode != &"" else &"single_unit"
	_pending_target_min_count = maxi(min_target_count, 1)
	_pending_target_max_count = maxi(max_target_count, _pending_target_min_count)
	_apply_pending_configuration()
	_fit_to_viewport(true)


func update_selection(
	selected_coord: Vector2i,
	preview_target_coords: Array[Vector2i] = [],
	valid_target_coords: Array[Vector2i] = [],
	target_selection_mode: StringName = &"single_unit",
	min_target_count: int = 1,
	max_target_count: int = 1
) -> void:
	_pending_selected_coord = selected_coord
	_pending_preview_target_coords = preview_target_coords.duplicate()
	_pending_valid_target_coords = valid_target_coords.duplicate()
	_pending_target_selection_mode = target_selection_mode if target_selection_mode != &"" else &"single_unit"
	_pending_target_min_count = maxi(min_target_count, 1)
	_pending_target_max_count = maxi(max_target_count, _pending_target_min_count)
	_apply_pending_marker_update()


func set_viewport_size(viewport_size: Vector2) -> void:
	_viewport_size = viewport_size
	_fit_to_viewport()


func begin_viewport_pan(viewport_position: Vector2) -> void:
	_is_panning = true
	_last_pan_viewport_position = viewport_position


func end_viewport_pan() -> void:
	_is_panning = false


func is_viewport_panning() -> bool:
	return _is_panning


func handle_viewport_mouse_motion(viewport_position: Vector2, button_mask: int) -> bool:
	if not _is_panning:
		return false
	if (button_mask & MOUSE_BUTTON_MASK_MIDDLE) == 0:
		_is_panning = false
		return false
	var delta := viewport_position - _last_pan_viewport_position
	_last_pan_viewport_position = viewport_position
	position += delta
	_clamp_camera_position()
	return true


func zoom_viewport(step: int, viewport_position: Vector2) -> bool:
	var next_zoom := clampf(
		_camera_zoom + float(step) * CAMERA_ZOOM_STEP,
		MIN_CAMERA_ZOOM,
		MAX_CAMERA_ZOOM
	)
	if is_equal_approx(next_zoom, _camera_zoom):
		return false
	var local_anchor := (viewport_position - position) / _camera_zoom
	_camera_zoom = next_zoom
	_has_manual_zoom_override = true
	scale = Vector2.ONE * _camera_zoom
	position = viewport_position - local_anchor * _camera_zoom
	_camera_initialized = true
	_clamp_camera_position()
	return true


func handle_viewport_mouse_button(viewport_position: Vector2, button_index: int) -> bool:
	if _controller == null or _pending_battle_state == null or _is_panning:
		return false

	var clicked_coord := _viewport_position_to_board_coord(viewport_position)
	if clicked_coord == Vector2i(-1, -1):
		return false

	match button_index:
		MOUSE_BUTTON_LEFT:
			battle_cell_clicked.emit(clicked_coord)
			return true
		MOUSE_BUTTON_RIGHT:
			battle_cell_right_clicked.emit(clicked_coord)
			return true
		_:
			return false


func clear_board() -> void:
	_pending_battle_state = null
	_pending_selected_coord = Vector2i(-1, -1)
	_pending_preview_target_coords.clear()
	_pending_valid_target_coords.clear()
	_pending_target_selection_mode = &"single_unit"
	_pending_target_min_count = 1
	_pending_target_max_count = 1
	_is_panning = false
	_has_content_bounds = false
	_camera_initialized = false
	_has_manual_zoom_override = false
	_camera_zoom = DEFAULT_CAMERA_ZOOM
	_last_focus_coord = Vector2i(-9999, -9999)
	if _controller != null:
		_controller.clear()
	_fit_to_viewport()


func is_render_content_ready() -> bool:
	return _is_bound and _pending_battle_state != null and _controller != null and _controller.is_render_content_ready()


func _bind_controller() -> void:
	if _is_bound:
		return
	_controller.bind_layers(
		input_layer,
		top_layers,
		edge_drop_east_layers,
		edge_drop_south_layers,
		wall_east_layers,
		wall_south_layers,
		overlay_layers,
		marker_layers,
		prop_layer,
		unit_layer,
		target_highlight_layer
	)
	_is_bound = true


func _apply_pending_configuration() -> void:
	if not _is_bound:
		return
	_controller.configure(
		_pending_battle_state,
		_pending_selected_coord,
		_pending_preview_target_coords,
		_pending_target_selection_mode,
		_pending_target_min_count,
		_pending_target_max_count
	)
	_controller.update_markers(
		_pending_selected_coord,
		_pending_preview_target_coords,
		_pending_valid_target_coords,
		_pending_target_selection_mode,
		_pending_target_min_count,
		_pending_target_max_count
	)
	_refresh_content_bounds()
	_fit_to_viewport()


func _apply_pending_marker_update() -> void:
	if not _is_bound or _pending_battle_state == null:
		return
	_controller.update_markers(
		_pending_selected_coord,
		_pending_preview_target_coords,
		_pending_valid_target_coords,
		_pending_target_selection_mode,
		_pending_target_min_count,
		_pending_target_max_count
	)


func _viewport_position_to_board_coord(viewport_position: Vector2) -> Vector2i:
	if input_layer == null or _pending_battle_state == null:
		return Vector2i(-1, -1)
	var board_local := to_local(viewport_position)
	var visual_coord := _pick_visual_surface_coord(board_local)
	if visual_coord != Vector2i(-1, -1):
		return visual_coord
	var input_local := input_layer.to_local(to_global(board_local))
	var coord := input_layer.local_to_map(input_local)
	if not _pending_battle_state.cells.has(coord):
		return Vector2i(-1, -1)
	return coord


func _pick_visual_surface_coord(board_local: Vector2) -> Vector2i:
	if _pending_battle_state == null or _pending_battle_state.cells.is_empty():
		return Vector2i(-1, -1)
	var best_coord := Vector2i(-1, -1)
	var best_sort_key := -1e20
	var best_distance_sq := INF
	for cell_variant in _pending_battle_state.cells.values():
		var cell_state := cell_variant as BattleCellState
		if cell_state == null:
			continue
		var anchor := _get_coord_anchor(cell_state.coord)
		if not _point_hits_cell_top_surface(board_local, anchor):
			continue
		var plane_anchor := input_layer.map_to_local(cell_state.coord)
		var sort_key := _build_visual_pick_sort_key(int(cell_state.current_height), plane_anchor.y)
		var distance_sq := board_local.distance_squared_to(anchor)
		if sort_key > best_sort_key:
			best_coord = cell_state.coord
			best_sort_key = sort_key
			best_distance_sq = distance_sq
			continue
		if is_equal_approx(sort_key, best_sort_key) and distance_sq < best_distance_sq:
			best_coord = cell_state.coord
			best_distance_sq = distance_sq
	return best_coord


func _point_hits_cell_top_surface(point: Vector2, anchor: Vector2) -> bool:
	var delta := point - anchor
	var normalized_x := absf(delta.x) / maxf(TILE_HALF_SIZE.x, 1.0)
	var normalized_y := absf(delta.y) / maxf(TILE_HALF_SIZE.y, 1.0)
	return normalized_x + normalized_y <= 1.0


func _build_visual_pick_sort_key(height_value: int, plane_anchor_y: float) -> float:
	var clamped_height := clampi(height_value, 0, MAX_RENDER_HEIGHT)
	return float(clamped_height) * 1000000.0 + plane_anchor_y


func _fit_to_viewport(force_focus: bool = false) -> void:
	if _viewport_size == Vector2.ZERO:
		return
	if _pending_battle_state == null or _pending_battle_state.cells.is_empty():
		scale = Vector2.ONE * _camera_zoom
		position = _viewport_size * 0.5
		return
	scale = Vector2.ONE * _camera_zoom
	if not _has_content_bounds:
		_refresh_content_bounds()
	if not _has_content_bounds:
		position = _viewport_size * 0.5
		return
	if not _has_manual_zoom_override:
		_camera_zoom = _compute_horizontal_fit_zoom()

	var focus_coord := _resolve_focus_coord()
	scale = Vector2.ONE * _camera_zoom
	if force_focus or not _camera_initialized or focus_coord != _last_focus_coord:
		_center_camera_on_coord(focus_coord)
		_last_focus_coord = focus_coord
		_camera_initialized = true
	else:
		_clamp_camera_position()


func _refresh_content_bounds() -> void:
	_has_content_bounds = false
	_content_bounds = Rect2()
	if _pending_battle_state == null or _pending_battle_state.cells.is_empty():
		return

	for cell_variant in _pending_battle_state.cells.values():
		var cell_state := cell_variant as BattleCellState
		if cell_state == null:
			continue
		var anchor := _get_coord_anchor(cell_state.coord)
		var cell_rect := Rect2(anchor - TILE_HALF_SIZE, TILE_HALF_SIZE * 2.0)
		if not _has_content_bounds:
			_content_bounds = cell_rect
			_has_content_bounds = true
		else:
			_content_bounds = _content_bounds.merge(cell_rect)

	if _has_content_bounds:
		_content_bounds = _content_bounds.grow_individual(64.0, 72.0, 64.0, 120.0)


func _resolve_focus_coord() -> Vector2i:
	if _pending_battle_state == null:
		return Vector2i.ZERO
	if _pending_selected_coord != Vector2i(-1, -1) and _pending_battle_state.cells.has(_pending_selected_coord):
		return _pending_selected_coord
	var active_unit := _pending_battle_state.units.get(_pending_battle_state.active_unit_id) as BattleUnitState
	if active_unit != null and active_unit.is_alive and _pending_battle_state.cells.has(active_unit.coord):
		return active_unit.coord
	for ally_unit_id in _pending_battle_state.ally_unit_ids:
		var ally_unit := _pending_battle_state.units.get(ally_unit_id) as BattleUnitState
		if ally_unit != null and ally_unit.is_alive and _pending_battle_state.cells.has(ally_unit.coord):
			return ally_unit.coord
	for cell_coord_variant in _pending_battle_state.cells.keys():
		if cell_coord_variant is Vector2i:
			return cell_coord_variant
	return Vector2i.ZERO


func _get_coord_anchor(coord: Vector2i) -> Vector2:
	var anchor := input_layer.map_to_local(coord)
	var cell_state: BattleCellState = null
	if _pending_battle_state != null:
		cell_state = _pending_battle_state.cells.get(coord) as BattleCellState
	if cell_state != null:
		anchor.y -= float(clampi(int(cell_state.current_height), 0, MAX_RENDER_HEIGHT)) * HEIGHT_STEP
	return anchor


func _get_focus_viewport_position() -> Vector2:
	return Vector2(
		_viewport_size.x * FOCUS_VIEWPORT_RATIO.x,
		_viewport_size.y * FOCUS_VIEWPORT_RATIO.y
	)


func _center_camera_on_coord(coord: Vector2i) -> void:
	position = _get_focus_viewport_position() - _get_coord_anchor(coord) * _camera_zoom
	_clamp_camera_position()


func _clamp_camera_position() -> void:
	if _viewport_size == Vector2.ZERO or not _has_content_bounds:
		return
	var bounds_end := _content_bounds.position + _content_bounds.size
	position.x = _clamp_camera_axis(
		position.x,
		_content_bounds.position.x,
		bounds_end.x,
		_viewport_size.x,
		CAMERA_EDGE_MARGIN_X
	)
	position.y = _clamp_camera_axis(
		position.y,
		_content_bounds.position.y,
		bounds_end.y,
		_viewport_size.y,
		CAMERA_EDGE_MARGIN_Y
	)


func _compute_horizontal_fit_zoom() -> float:
	if not _has_content_bounds:
		return DEFAULT_CAMERA_ZOOM
	var available_width := maxf(_viewport_size.x - CAMERA_EDGE_MARGIN_X * 2.0, 1.0)
	var content_width := maxf(_content_bounds.size.x, 1.0)
	return clampf(available_width / content_width, MIN_CAMERA_ZOOM, MAX_CAMERA_ZOOM)


func _clamp_camera_axis(
	current_position: float,
	bounds_start: float,
	bounds_end: float,
	viewport_extent: float,
	margin: float
) -> float:
	var min_position := viewport_extent - margin - bounds_end * _camera_zoom
	var max_position := margin - bounds_start * _camera_zoom
	if min_position > max_position:
		return (viewport_extent - (bounds_start + bounds_end) * _camera_zoom) * 0.5
	return clampf(current_position, min_position, max_position)


func _collect_tile_layers(prefix: String, start_height: int, end_height: int) -> Array[TileMapLayer]:
	var layers: Array[TileMapLayer] = []
	for height in range(start_height, end_height + 1):
		var layer := get_node_or_null("%s%d" % [prefix, height]) as TileMapLayer
		if layer != null:
			layers.append(layer)
	return layers
