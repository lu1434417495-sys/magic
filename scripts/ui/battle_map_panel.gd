## 文件说明：该脚本属于战斗地图面板相关的界面面板脚本，集中维护战斗界面适配、地图子视口、战斗棋盘等顶层字段。
## 审查重点：重点核对字段含义、节点绑定、信号联动以及界面状态切换是否仍与对应场景保持一致。
## 备注：后续如果调整场景节点命名、层级或交互路径，需要同步检查成员字段与信号连接。

class_name BattleMapPanel
extends Control

const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleHudAdapter = preload("res://scripts/ui/battle_hud_adapter.gd")
const BattleBoard2D = preload("res://scripts/ui/battle_board_2d.gd")
const BATTLE_BOARD_SCENE = preload("res://scenes/ui/battle_board_2d.tscn")

## 信号说明：当战斗格子被点击时发出的信号，供外层接管选择、移动或交互逻辑。
signal battle_cell_clicked(coord: Vector2i)
## 信号说明：当战斗格子被右键点击时发出的信号，供外层执行二级交互、取消或上下文操作。
signal battle_cell_right_clicked(coord: Vector2i)
## 信号说明：当界面请求移动相关时发出的信号，具体处理由外层系统或控制器负责。
signal movement_reset_requested
## 信号说明：当界面请求结算时发出的信号，具体处理由外层系统或控制器负责。
signal resolve_requested
## 信号说明：当战斗技能槽位被选中时发出的信号，供外层同步当前选择结果。
signal battle_skill_slot_selected(index: int)
## 信号说明：当界面请求战斗技能变体循环时发出的信号，具体处理由外层系统或控制器负责。
signal battle_skill_variant_cycle_requested(step: int)
## 信号说明：当界面请求战斗技能清除时发出的信号，具体处理由外层系统或控制器负责。
signal battle_skill_clear_requested
## 信号说明：当 battle 首帧准备状态变化时发出的信号，供外层切图遮罩同步黑屏与进度条。
signal battle_loading_state_changed(is_loading: bool, progress_value: float)

const HUD_PANEL_BG := Color(0.16, 0.06, 0.03, 0.9)
const HUD_PANEL_BG_ALT := Color(0.2, 0.08, 0.04, 0.92)
const HUD_PANEL_EDGE := Color(0.82, 0.63, 0.35, 0.96)
const HUD_PANEL_EDGE_SOFT := Color(0.56, 0.39, 0.18, 0.92)
const HUD_TEXT_PRIMARY := Color(0.98, 0.93, 0.82, 1.0)
const HUD_TEXT_SECONDARY := Color(0.92, 0.82, 0.66, 0.94)
const HUD_TEXT_MUTED := Color(0.78, 0.66, 0.54, 0.86)
const HUD_DARK := Color(0.08, 0.03, 0.02, 0.9)
const LOADING_PROGRESS_PREPARE := 12.0
const LOADING_PROGRESS_DRAW_REQUESTED := 48.0
const LOADING_PROGRESS_FRAME_QUEUED := 82.0
const LOADING_PROGRESS_READY := 100.0
const MIN_BATTLE_LOADING_DURATION_SECONDS := 0.35
const MAX_BATTLE_RENDER_READY_FRAMES := 12

## 字段说明：记录战斗界面适配，作为界面刷新、输入处理和窗口联动的重要依据。
var _hud_adapter := BattleHudAdapter.new()
## 字段说明：缓存地图子视口节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
var _map_subviewport: SubViewport = null
## 字段说明：缓存战斗棋盘实例，作为界面刷新、输入处理和窗口联动的重要依据。
var _battle_board: BattleBoard2D = null
## 字段说明：记录当前正在等待首帧呈现的战斗唯一标识，避免重复触发加载遮罩或过早放开输入。
var _revealing_battle_id: StringName = &""
## 字段说明：记录最近一次已完成首帧呈现的战斗唯一标识，避免同一 battle 的普通刷新重复闪黑屏。
var _revealed_battle_id: StringName = &""
## 字段说明：记录首帧呈现握手版本号，用于丢弃过期的异步等待结果。
var _battle_reveal_ticket := 0
## 字段说明：记录当前 battle 首帧准备进度，供外层黑屏进度条同步显示。
var _battle_loading_progress := 0.0
## 字段说明：记录当前 battle loading 开始时间，用于保证黑屏过场至少可见一小段时间。
var _battle_reveal_started_at_msec := 0
## 字段说明：缓存首次进入 battle 时待应用的展示参数，确保黑屏先出图后再执行重型面板刷新。
var _pending_show_battle_payload: Dictionary = {}

## 字段说明：缓存地图框架节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var map_frame: PanelContainer = %MapFrame
## 字段说明：缓存地图视口容器节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var map_viewport_container: SubViewportContainer = %MapViewportContainer
## 字段说明：缓存顶部条节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var top_bar: PanelContainer = %TopBar
## 字段说明：缓存底部面板节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var bottom_panel: PanelContainer = %BottomPanel
## 字段说明：缓存头部标题标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var header_title_label: Label = %HeaderTitleLabel
## 字段说明：缓存头部副标题标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var header_subtitle_label: Label = %HeaderSubtitleLabel
## 字段说明：缓存回合标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var round_label: Label = %RoundLabel
## 字段说明：缓存模式数值标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var mode_value_label: Label = %ModeValueLabel
## 字段说明：缓存相关快捷按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var reset_quick_button: Button = %ResetQuickButton
## 字段说明：缓存上一个快捷按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var prev_quick_button: Button = %PrevQuickButton
## 字段说明：缓存下一个快捷按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var next_quick_button: Button = %NextQuickButton
## 字段说明：缓存清除快捷按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var clear_quick_button: Button = %ClearQuickButton
## 字段说明：缓存单位卡片节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var unit_card: PanelContainer = %UnitCard
## 字段说明：缓存头像框架节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var portrait_frame: PanelContainer = %PortraitFrame
## 字段说明：缓存头像字形标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var portrait_glyph_label: Label = %PortraitGlyphLabel
## 字段说明：缓存头像键标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var portrait_key_label: Label = %PortraitKeyLabel
## 字段说明：缓存单位名称标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var unit_name_label: Label = %UnitNameLabel
## 字段说明：缓存单位角色定位标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var unit_role_label: Label = %UnitRoleLabel
## 字段说明：缓存生命值条，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var hp_bar: ProgressBar = %HpBar
## 字段说明：缓存生命值数值标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var hp_value_label: Label = %HpValueLabel
## 字段说明：缓存法力值条，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var mp_bar: ProgressBar = %MpBar
## 字段说明：缓存法力值数值标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var mp_value_label: Label = %MpValueLabel
## 字段说明：缓存行动点条，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var ap_bar: ProgressBar = %ApBar
## 字段说明：缓存行动点数值标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var ap_value_label: Label = %ApValueLabel
## 字段说明：缓存单位详情标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var unit_detail_label: Label = %UnitDetailLabel
## 字段说明：缓存技能面板节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var skill_panel: PanelContainer = %SkillPanel
## 字段说明：缓存技能标题标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var skill_title_label: Label = %SkillTitleLabel
## 字段说明：缓存技能副标题标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var skill_subtitle_label: Label = %SkillSubtitleLabel
## 字段说明：缓存技能网格节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var skill_grid: GridContainer = %SkillGrid
## 字段说明：缓存瓦片标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var tile_label: Label = %TileLabel
## 字段说明：缓存提示标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var hint_label: Label = %HintLabel
## 字段说明：缓存日志标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var log_label: Label = %LogLabel
## 字段说明：缓存指令停靠区节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var command_dock: PanelContainer = %CommandDock
## 字段说明：缓存指令摘要标签节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var command_summary_label: Label = %CommandSummaryLabel
## 字段说明：缓存相关移动按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var reset_movement_button: Button = %ResetMovementButton
## 字段说明：缓存上一个变体按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var prev_variant_button: Button = %PrevVariantButton
## 字段说明：缓存下一个变体按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var next_variant_button: Button = %NextVariantButton
## 字段说明：缓存清除技能按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var clear_skill_button: Button = %ClearSkillButton
## 字段说明：缓存结算按钮节点，避免运行时重复查找场景树，并作为当前脚本直接读写的节点入口。
@onready var resolve_button: Button = %ResolveBattleButton


func _ready() -> void:
	visible = false
	_ensure_battle_board()
	map_viewport_container.gui_input.connect(_on_map_viewport_container_gui_input)
	_connect_button_pair(reset_quick_button, reset_movement_button, _emit_movement_reset_requested)
	_connect_button_pair(clear_quick_button, clear_skill_button, _emit_skill_clear_requested)
	_connect_variant_button(prev_quick_button, -1)
	_connect_variant_button(prev_variant_button, -1)
	_connect_variant_button(next_quick_button, 1)
	_connect_variant_button(next_variant_button, 1)
	resolve_button.pressed.connect(_on_resolve_button_pressed)
	_apply_static_skin()
	_set_placeholder_state()
	_update_battle_loading_state(false, 0.0)
	_resize_map_viewport()


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_resize_map_viewport()


func is_loading_battle() -> bool:
	return _revealing_battle_id != &""


func get_loading_progress() -> float:
	return _battle_loading_progress


func is_battle_render_content_ready() -> bool:
	return _battle_board != null and _battle_board.is_render_content_ready()


func show_battle(
	battle_state: BattleState,
	selected_coord: Vector2i,
	selected_skill_id: StringName = &"",
	selected_skill_name: String = "",
	selected_skill_variant_name: String = "",
	selected_skill_target_coords: Array[Vector2i] = [],
	selected_skill_valid_target_coords: Array[Vector2i] = [],
	selected_skill_required_coord_count: int = 0,
	selected_skill_target_unit_ids: Array[StringName] = []
) -> void:
	var battle_id := _resolve_battle_id(battle_state)
	_store_pending_show_battle_payload(
		battle_state,
		selected_coord,
		selected_skill_id,
		selected_skill_name,
		selected_skill_variant_name,
		selected_skill_target_coords,
		selected_skill_valid_target_coords,
		selected_skill_required_coord_count,
		selected_skill_target_unit_ids
	)
	var reveal_ticket := _begin_battle_reveal_if_needed(battle_id)
	visible = true
	if reveal_ticket > 0:
		_begin_battle_first_presented_frame(reveal_ticket, battle_id)
		return
	if _revealing_battle_id == battle_id:
		return
	refresh(
		battle_state,
		selected_coord,
		selected_skill_id,
		selected_skill_name,
		selected_skill_variant_name,
		selected_skill_target_coords,
		selected_skill_valid_target_coords,
		selected_skill_required_coord_count,
		selected_skill_target_unit_ids
	)


func _store_pending_show_battle_payload(
	battle_state: BattleState,
	selected_coord: Vector2i,
	selected_skill_id: StringName,
	selected_skill_name: String,
	selected_skill_variant_name: String,
	selected_skill_target_coords: Array[Vector2i],
	selected_skill_valid_target_coords: Array[Vector2i],
	selected_skill_required_coord_count: int,
	selected_skill_target_unit_ids: Array[StringName]
) -> void:
	_pending_show_battle_payload = {
		"battle_state": battle_state,
		"selected_coord": selected_coord,
		"selected_skill_id": selected_skill_id,
		"selected_skill_name": selected_skill_name,
		"selected_skill_variant_name": selected_skill_variant_name,
		"selected_skill_target_coords": selected_skill_target_coords.duplicate(),
		"selected_skill_valid_target_coords": selected_skill_valid_target_coords.duplicate(),
		"selected_skill_required_coord_count": selected_skill_required_coord_count,
		"selected_skill_target_unit_ids": selected_skill_target_unit_ids.duplicate(),
	}


func refresh_overlay(
	battle_state: BattleState,
	selected_coord: Vector2i,
	selected_skill_id: StringName = &"",
	selected_skill_name: String = "",
	selected_skill_variant_name: String = "",
	selected_skill_target_coords: Array[Vector2i] = [],
	selected_skill_valid_target_coords: Array[Vector2i] = [],
	selected_skill_required_coord_count: int = 0,
	selected_skill_target_unit_ids: Array[StringName] = []
) -> void:
	_refresh_internal(
		battle_state,
		selected_coord,
		selected_skill_id,
		selected_skill_name,
		selected_skill_variant_name,
		selected_skill_target_coords,
		selected_skill_valid_target_coords,
		selected_skill_required_coord_count,
		selected_skill_target_unit_ids,
		false
	)


func refresh(
	battle_state: BattleState,
	selected_coord: Vector2i,
	selected_skill_id: StringName = &"",
	selected_skill_name: String = "",
	selected_skill_variant_name: String = "",
	selected_skill_target_coords: Array[Vector2i] = [],
	selected_skill_valid_target_coords: Array[Vector2i] = [],
	selected_skill_required_coord_count: int = 0,
	selected_skill_target_unit_ids: Array[StringName] = []
) -> void:
	_refresh_internal(
		battle_state,
		selected_coord,
		selected_skill_id,
		selected_skill_name,
		selected_skill_variant_name,
		selected_skill_target_coords,
		selected_skill_valid_target_coords,
		selected_skill_required_coord_count,
		selected_skill_target_unit_ids,
		true
	)


func _refresh_internal(
	battle_state: BattleState,
	selected_coord: Vector2i,
	selected_skill_id: StringName = &"",
	selected_skill_name: String = "",
	selected_skill_variant_name: String = "",
	selected_skill_target_coords: Array[Vector2i] = [],
	selected_skill_valid_target_coords: Array[Vector2i] = [],
	selected_skill_required_coord_count: int = 0,
	selected_skill_target_unit_ids: Array[StringName] = [],
	redraw_board: bool = true
) -> void:
	if battle_state == null:
		hide_battle()
		return

	var snapshot := _hud_adapter.build_snapshot(
		battle_state,
		selected_coord,
		selected_skill_id,
		selected_skill_name,
		selected_skill_variant_name,
		selected_skill_target_coords,
		selected_skill_required_coord_count,
		selected_skill_target_unit_ids
	)
	_apply_snapshot(snapshot)
	_update_button_states(selected_skill_id)
	if _battle_board != null:
		var selected_skill_target_selection_mode := StringName(snapshot.get("selected_skill_target_selection_mode", &"single_unit"))
		if selected_skill_id == &"" and not selected_skill_valid_target_coords.is_empty():
			selected_skill_target_selection_mode = &"movement"
		var selected_skill_target_min_count := int(snapshot.get("selected_skill_target_min_count", 1))
		var selected_skill_target_max_count := int(snapshot.get("selected_skill_target_max_count", 1))
		if redraw_board:
			_battle_board.configure(
				battle_state,
				selected_coord,
				selected_skill_target_coords,
				selected_skill_valid_target_coords,
				selected_skill_target_selection_mode,
				selected_skill_target_min_count,
				selected_skill_target_max_count
			)
		else:
			_battle_board.update_selection(
				selected_coord,
				selected_skill_target_coords,
				selected_skill_valid_target_coords,
				selected_skill_target_selection_mode,
				selected_skill_target_min_count,
				selected_skill_target_max_count
			)
		_request_map_viewport_update()
	if redraw_board:
		_resize_map_viewport()


func hide_battle() -> void:
	_cancel_battle_reveal()
	_pending_show_battle_payload.clear()
	visible = false
	if _battle_board != null:
		_battle_board.clear_board()
	_request_map_viewport_update()


func _resolve_battle_id(battle_state: BattleState) -> StringName:
	return battle_state.battle_id if battle_state != null else &""


func _begin_battle_reveal_if_needed(battle_id: StringName) -> int:
	if battle_id == &"":
		return 0
	if battle_id == _revealed_battle_id or battle_id == _revealing_battle_id:
		return 0
	_battle_reveal_ticket += 1
	var reveal_ticket := _battle_reveal_ticket
	_revealing_battle_id = battle_id
	_revealed_battle_id = &""
	_battle_reveal_started_at_msec = Time.get_ticks_msec()
	_update_battle_loading_state(true, LOADING_PROGRESS_PREPARE)
	return reveal_ticket


func _cancel_battle_reveal() -> void:
	_battle_reveal_ticket += 1
	_revealing_battle_id = &""
	_revealed_battle_id = &""
	_update_battle_loading_state(false, 0.0)


func _begin_battle_first_presented_frame(reveal_ticket: int, battle_id: StringName) -> void:
	_complete_battle_reveal_async(reveal_ticket, battle_id)


func _complete_battle_reveal_async(reveal_ticket: int, battle_id: StringName) -> void:
	if not _is_battle_reveal_current(reveal_ticket, battle_id):
		return
	_update_battle_loading_state(true, LOADING_PROGRESS_DRAW_REQUESTED)
	await get_tree().process_frame
	if not _is_battle_reveal_current(reveal_ticket, battle_id):
		return
	_apply_pending_show_battle_payload()
	if not _is_battle_reveal_current(reveal_ticket, battle_id):
		return
	var content_ready := is_battle_render_content_ready()
	var waited_frames := 0
	while not content_ready and waited_frames < MAX_BATTLE_RENDER_READY_FRAMES:
		await get_tree().process_frame
		if not _is_battle_reveal_current(reveal_ticket, battle_id):
			return
		_request_map_viewport_update()
		content_ready = is_battle_render_content_ready()
		waited_frames += 1
	if not _is_battle_reveal_current(reveal_ticket, battle_id):
		return
	_update_battle_loading_state(true, LOADING_PROGRESS_FRAME_QUEUED)
	if DisplayServer.get_name() == "headless":
		await get_tree().process_frame
	else:
		await RenderingServer.frame_post_draw
	if not _is_battle_reveal_current(reveal_ticket, battle_id):
		return
	var elapsed_seconds := float(Time.get_ticks_msec() - _battle_reveal_started_at_msec) / 1000.0
	var remaining_seconds := MIN_BATTLE_LOADING_DURATION_SECONDS - elapsed_seconds
	if remaining_seconds > 0.0:
		var target_time_msec := _battle_reveal_started_at_msec + int(round(MIN_BATTLE_LOADING_DURATION_SECONDS * 1000.0))
		while Time.get_ticks_msec() < target_time_msec:
			await get_tree().process_frame
			if not _is_battle_reveal_current(reveal_ticket, battle_id):
				return
	if not _is_battle_reveal_current(reveal_ticket, battle_id):
		return
	_revealing_battle_id = &""
	_revealed_battle_id = battle_id
	visible = true
	_update_battle_loading_state(false, LOADING_PROGRESS_READY)


func _apply_pending_show_battle_payload() -> void:
	if _pending_show_battle_payload.is_empty():
		return
	refresh(
		_pending_show_battle_payload.get("battle_state", null),
		_pending_show_battle_payload.get("selected_coord", Vector2i.ZERO),
		_pending_show_battle_payload.get("selected_skill_id", &""),
		String(_pending_show_battle_payload.get("selected_skill_name", "")),
		String(_pending_show_battle_payload.get("selected_skill_variant_name", "")),
		_pending_show_battle_payload.get("selected_skill_target_coords", []),
		_pending_show_battle_payload.get("selected_skill_valid_target_coords", []),
		int(_pending_show_battle_payload.get("selected_skill_required_coord_count", 0)),
		_pending_show_battle_payload.get("selected_skill_target_unit_ids", [])
	)


func _is_battle_reveal_current(reveal_ticket: int, battle_id: StringName) -> bool:
	return reveal_ticket == _battle_reveal_ticket and battle_id != &"" and battle_id == _revealing_battle_id


func _update_battle_loading_state(is_loading: bool, progress_value: float) -> void:
	_battle_loading_progress = clampf(progress_value, 0.0, LOADING_PROGRESS_READY)
	battle_loading_state_changed.emit(is_loading, _battle_loading_progress)


func _on_battle_board_cell_clicked(coord: Vector2i) -> void:
	battle_cell_clicked.emit(coord)


func _on_battle_board_cell_right_clicked(coord: Vector2i) -> void:
	battle_cell_right_clicked.emit(coord)


func _on_map_viewport_container_gui_input(event: InputEvent) -> void:
	if _battle_board == null:
		return
	if event is InputEventMouseMotion:
		var motion_event := event as InputEventMouseMotion
		if _battle_board.handle_viewport_mouse_motion(motion_event.position, motion_event.button_mask):
			_request_map_viewport_update()
			accept_event()
		return
	if event is not InputEventMouseButton:
		return

	var mouse_event := event as InputEventMouseButton
	if mouse_event.button_index == MOUSE_BUTTON_MIDDLE:
		if mouse_event.pressed:
			_battle_board.begin_viewport_pan(mouse_event.position)
		else:
			_battle_board.end_viewport_pan()
		_request_map_viewport_update()
		accept_event()
		return
	if mouse_event.pressed and mouse_event.button_index == MOUSE_BUTTON_WHEEL_UP:
		if _battle_board.zoom_viewport(1, mouse_event.position):
			_request_map_viewport_update()
			accept_event()
		return
	if mouse_event.pressed and mouse_event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
		if _battle_board.zoom_viewport(-1, mouse_event.position):
			_request_map_viewport_update()
			accept_event()
		return
	if not mouse_event.pressed or _battle_board.is_viewport_panning():
		return
	if _battle_board.handle_viewport_mouse_button(mouse_event.position, mouse_event.button_index):
		accept_event()


func _ensure_battle_board() -> void:
	if _battle_board != null:
		return

	_map_subviewport = SubViewport.new()
	_map_subviewport.name = "MapSubViewport"
	_map_subviewport.disable_3d = true
	_map_subviewport.transparent_bg = true
	_map_subviewport.handle_input_locally = false
	_map_subviewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
	_map_subviewport.render_target_clear_mode = SubViewport.CLEAR_MODE_ALWAYS
	map_viewport_container.add_child(_map_subviewport)

	var board_instance := BATTLE_BOARD_SCENE.instantiate()
	_battle_board = board_instance as BattleBoard2D
	if _battle_board == null:
		return
	_map_subviewport.add_child(_battle_board)
	_battle_board.battle_cell_clicked.connect(_on_battle_board_cell_clicked)
	_battle_board.battle_cell_right_clicked.connect(_on_battle_board_cell_right_clicked)


func _resize_map_viewport() -> void:
	if map_viewport_container == null or _map_subviewport == null or _battle_board == null:
		return
	var container_size := map_viewport_container.size
	var viewport_size := Vector2i(
		maxi(int(round(container_size.x)), 1),
		maxi(int(round(container_size.y)), 1)
	)
	_map_subviewport.size = viewport_size
	_battle_board.set_viewport_size(Vector2(viewport_size))
	_request_map_viewport_update()


func _request_map_viewport_update() -> void:
	if _map_subviewport == null:
		return
	_map_subviewport.render_target_update_mode = SubViewport.UPDATE_ONCE


func _emit_movement_reset_requested() -> void:
	movement_reset_requested.emit()


func _emit_skill_clear_requested() -> void:
	battle_skill_clear_requested.emit()


func _on_resolve_button_pressed() -> void:
	resolve_requested.emit()


func _connect_button_pair(primary_button: Button, secondary_button: Button, callable: Callable) -> void:
	primary_button.pressed.connect(callable)
	secondary_button.pressed.connect(callable)


func _connect_variant_button(button: Button, step: int) -> void:
	button.pressed.connect(func() -> void:
		battle_skill_variant_cycle_requested.emit(step)
	)


func _apply_static_skin() -> void:
	add_theme_color_override("font_color", HUD_TEXT_PRIMARY)

	for panel in [map_frame, top_bar, bottom_panel, unit_card, skill_panel, command_dock]:
		panel.add_theme_stylebox_override("panel", _build_panel_style(HUD_PANEL_BG, HUD_PANEL_EDGE))

	portrait_frame.add_theme_stylebox_override("panel", _build_panel_style(Color(0.3, 0.14, 0.08, 1.0), HUD_PANEL_EDGE, 18, 2))

	for compact_button in [reset_quick_button, prev_quick_button, next_quick_button, clear_quick_button]:
		_apply_button_skin(compact_button, true)

	for command_button in [reset_movement_button, prev_variant_button, next_variant_button, clear_skill_button]:
		_apply_button_skin(command_button, false)
	_apply_button_skin(resolve_button, false, true)

	_style_header_label(header_title_label, 24, HUD_TEXT_PRIMARY)
	_style_header_label(header_subtitle_label, 15, HUD_TEXT_SECONDARY)
	_style_header_label(round_label, 14, HUD_TEXT_PRIMARY)
	_style_header_label(mode_value_label, 15, HUD_TEXT_PRIMARY)
	_style_header_label(unit_name_label, 22, HUD_TEXT_PRIMARY)
	_style_header_label(unit_role_label, 13, HUD_TEXT_SECONDARY)
	_style_header_label(unit_detail_label, 12, HUD_TEXT_MUTED)
	_style_header_label(skill_title_label, 20, HUD_TEXT_PRIMARY)
	_style_header_label(skill_subtitle_label, 13, HUD_TEXT_SECONDARY)
	_style_header_label(tile_label, 12, HUD_TEXT_SECONDARY)
	_style_header_label(hint_label, 12, HUD_TEXT_MUTED)
	_style_header_label(log_label, 11, HUD_TEXT_MUTED)
	_style_header_label(command_summary_label, 13, HUD_TEXT_SECONDARY)
	_style_header_label(portrait_glyph_label, 34, Color(1.0, 0.96, 0.9, 0.98))
	_style_header_label(portrait_key_label, 11, HUD_TEXT_SECONDARY)
	_style_stat_label(hp_value_label)
	_style_stat_label(mp_value_label)
	_style_stat_label(ap_value_label)

	_apply_progress_bar_skin(hp_bar, Color(0.62, 0.86, 0.24, 1.0))
	_apply_progress_bar_skin(mp_bar, Color(0.32, 0.74, 0.96, 1.0))
	_apply_progress_bar_skin(ap_bar, Color(0.96, 0.78, 0.28, 1.0))


func _set_placeholder_state() -> void:
	header_title_label.text = "战斗地图"
	header_subtitle_label.text = "等待战斗开始"
	round_label.text = "TU --\nREADY 0"
	mode_value_label.text = "手动"
	unit_name_label.text = "待命"
	unit_role_label.text = "未选中单位"
	unit_detail_label.text = "左键选择地格或技能。"
	portrait_glyph_label.text = "?"
	portrait_key_label.text = "portrait://pending"
	_set_progress_bar_values(hp_bar, hp_value_label, 0, 1, "HP")
	_set_progress_bar_values(mp_bar, mp_value_label, 0, 1, "MP")
	_set_progress_bar_values(ap_bar, ap_value_label, 0, 1, "AP")
	skill_title_label.text = "技能矩阵"
	skill_subtitle_label.text = "等待战斗数据"
	tile_label.text = "地格 (--, --)  ·  无  ·  高度 0  ·  占位 无"
	hint_label.text = "左键地格移动或攻击，右键单位查看信息。滚轮缩放，中键拖拽平移。"
	log_label.text = "战报：暂无记录"
	command_summary_label.text = "等待行动单位"
	_rebuild_skill_grid([])


func _apply_snapshot(snapshot: Dictionary) -> void:
	header_title_label.text = String(snapshot.get("header_title", "战斗地图"))
	header_subtitle_label.text = String(snapshot.get("header_subtitle", ""))
	round_label.text = String(snapshot.get("round_badge", "TU --\nREADY 0"))
	mode_value_label.text = String(snapshot.get("mode_text", "手动"))
	command_summary_label.text = String(snapshot.get("command_text", ""))
	_refresh_focus_unit_card(snapshot.get("focus_unit", {}))
	_rebuild_skill_grid(snapshot.get("skill_slots", []))
	skill_title_label.text = String(snapshot.get("skill_title", "技能矩阵"))
	skill_subtitle_label.text = String(snapshot.get("skill_subtitle", ""))
	tile_label.text = String(snapshot.get("tile_text", ""))
	hint_label.text = String(snapshot.get("hint_text", ""))
	log_label.text = String(snapshot.get("log_text", ""))


func _refresh_focus_unit_card(focus_unit: Dictionary) -> void:
	var edge_color := focus_unit.get("edge_color", HUD_PANEL_EDGE) as Color
	var primary_color := focus_unit.get("primary_color", Color(0.42, 0.3, 0.22, 1.0)) as Color
	var secondary_color := focus_unit.get("secondary_color", HUD_DARK) as Color
	portrait_frame.add_theme_stylebox_override(
		"panel",
		_build_panel_style(primary_color.darkened(0.16), edge_color, 18, 2, secondary_color)
	)
	portrait_glyph_label.text = String(focus_unit.get("glyph", "?"))
	portrait_key_label.text = "portrait://%s" % String(focus_unit.get("portrait_key", "pending"))
	unit_name_label.text = String(focus_unit.get("name", "待命"))
	unit_role_label.text = String(focus_unit.get("role_text", "未选中单位"))
	unit_detail_label.text = String(focus_unit.get("detail_text", ""))

	_set_progress_bar_values(
		hp_bar,
		hp_value_label,
		int(focus_unit.get("hp_current", 0)),
		int(focus_unit.get("hp_max", 1)),
		"HP",
		Color(0.64, 0.9, 0.28, 1.0)
	)
	_set_progress_bar_values(
		mp_bar,
		mp_value_label,
		int(focus_unit.get("mp_current", 0)),
		int(focus_unit.get("mp_max", 1)),
		"MP",
		Color(0.32, 0.78, 0.98, 1.0)
	)
	_set_progress_bar_values(
		ap_bar,
		ap_value_label,
		int(focus_unit.get("ap_current", 0)),
		int(focus_unit.get("ap_max", 1)),
		"AP",
		Color(0.98, 0.8, 0.34, 1.0)
	)


func _set_progress_bar_values(
	progress_bar: ProgressBar,
	value_label: Label,
	current_value: int,
	max_value: int,
	label_prefix: String,
	fill_color: Color = Color.WHITE
) -> void:
	progress_bar.max_value = maxi(max_value, 1)
	progress_bar.value = clampi(current_value, 0, maxi(max_value, 1))
	_apply_progress_bar_skin(progress_bar, fill_color)
	value_label.text = "%s %d/%d" % [label_prefix, current_value, maxi(max_value, 1)]


func _rebuild_skill_grid(slots: Array) -> void:
	_clear_container(skill_grid)
	if slots.is_empty():
		for index in range(20):
			skill_grid.add_child(_create_skill_slot({"index": index, "is_empty": true}))
		return
	for slot_variant in slots:
		if slot_variant is not Dictionary:
			continue
		skill_grid.add_child(_create_skill_slot(slot_variant))


func _create_skill_slot(slot: Dictionary) -> Control:
	var panel := PanelContainer.new()
	panel.custom_minimum_size = Vector2(92, 84)
	panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	panel.add_theme_stylebox_override("panel", _build_skill_slot_style(slot))
	panel.tooltip_text = _build_skill_slot_tooltip(slot)

	var margin := MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 8)
	margin.add_theme_constant_override("margin_top", 6)
	margin.add_theme_constant_override("margin_right", 8)
	margin.add_theme_constant_override("margin_bottom", 6)
	panel.add_child(margin)

	var layout := VBoxContainer.new()
	layout.add_theme_constant_override("separation", 2)
	margin.add_child(layout)

	var hotkey_row := HBoxContainer.new()
	layout.add_child(hotkey_row)

	var hotkey_label := Label.new()
	hotkey_label.text = String(slot.get("hotkey", ""))
	hotkey_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	hotkey_label.add_theme_font_size_override("font_size", 10)
	hotkey_label.add_theme_color_override("font_color", HUD_TEXT_SECONDARY)
	hotkey_row.add_child(hotkey_label)

	var footer_top_label := Label.new()
	footer_top_label.text = "CD" if int(slot.get("cooldown", 0)) > 0 else ""
	footer_top_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	footer_top_label.add_theme_font_size_override("font_size", 10)
	footer_top_label.add_theme_color_override("font_color", HUD_TEXT_MUTED)
	hotkey_row.add_child(footer_top_label)

	var glyph_label := Label.new()
	glyph_label.text = "--" if bool(slot.get("is_empty", false)) else String(slot.get("short_name", "--"))
	glyph_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	glyph_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	glyph_label.size_flags_vertical = Control.SIZE_EXPAND_FILL
	glyph_label.add_theme_font_size_override("font_size", 22)
	glyph_label.add_theme_color_override(
		"font_color",
		HUD_TEXT_MUTED if bool(slot.get("is_empty", false)) else Color(1.0, 0.96, 0.9, 0.98)
	)
	layout.add_child(glyph_label)

	var name_label := Label.new()
	name_label.text = "" if bool(slot.get("is_empty", false)) else String(slot.get("display_name", ""))
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	name_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	name_label.add_theme_font_size_override("font_size", 11)
	name_label.add_theme_color_override("font_color", HUD_TEXT_SECONDARY)
	layout.add_child(name_label)

	var footer_label := Label.new()
	footer_label.text = "" if bool(slot.get("is_empty", false)) else String(slot.get("footer_text", ""))
	footer_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	footer_label.add_theme_font_size_override("font_size", 10)
	footer_label.add_theme_color_override("font_color", HUD_TEXT_MUTED)
	layout.add_child(footer_label)

	var click_target := Button.new()
	click_target.flat = true
	click_target.focus_mode = Control.FOCUS_NONE
	click_target.layout_mode = 1
	click_target.anchors_preset = PRESET_FULL_RECT
	click_target.anchor_right = 1.0
	click_target.anchor_bottom = 1.0
	click_target.grow_horizontal = Control.GROW_DIRECTION_BOTH
	click_target.grow_vertical = Control.GROW_DIRECTION_BOTH
	click_target.disabled = bool(slot.get("is_empty", false)) or bool(slot.get("is_disabled", false))
	click_target.text = ""
	click_target.mouse_default_cursor_shape = CURSOR_POINTING_HAND
	click_target.tooltip_text = panel.tooltip_text
	click_target.pressed.connect(_on_skill_slot_pressed.bind(int(slot.get("index", -1))))
	panel.add_child(click_target)

	return panel


func _on_skill_slot_pressed(index: int) -> void:
	if index < 0:
		return
	battle_skill_slot_selected.emit(index)


func _update_button_states(selected_skill_id: StringName) -> void:
	var has_skill := selected_skill_id != &""
	for button in [prev_quick_button, next_quick_button, clear_quick_button, prev_variant_button, next_variant_button, clear_skill_button]:
		button.disabled = not has_skill


func _build_skill_slot_tooltip(slot: Dictionary) -> String:
	if bool(slot.get("is_empty", false)):
		return ""
	var lines: Array[String] = [String(slot.get("display_name", ""))]
	var disabled_reason := String(slot.get("disabled_reason", ""))
	if not disabled_reason.is_empty():
		lines.append("不可用：%s" % disabled_reason)
	else:
		var footer_text := String(slot.get("footer_text", ""))
		if not footer_text.is_empty() and footer_text != "READY":
			lines.append("信息：%s" % footer_text)
	return "\n".join(PackedStringArray(lines))


func _clear_container(container: Node) -> void:
	for child in container.get_children():
		container.remove_child(child)
		child.queue_free()


func _apply_button_skin(button: Button, is_compact: bool, is_primary: bool = false) -> void:
	button.add_theme_font_size_override("font_size", 12 if is_compact else 14)
	button.add_theme_color_override("font_color", HUD_TEXT_PRIMARY)
	button.add_theme_color_override("font_focus_color", HUD_TEXT_PRIMARY)
	button.add_theme_stylebox_override(
		"normal",
		_build_button_style(
			Color(0.28, 0.09, 0.04, 0.96) if not is_primary else Color(0.46, 0.14, 0.08, 0.98),
			HUD_PANEL_EDGE,
			18 if is_compact else 16
		)
	)
	button.add_theme_stylebox_override(
		"hover",
		_build_button_style(
			Color(0.38, 0.12, 0.06, 0.98) if not is_primary else Color(0.56, 0.18, 0.09, 0.98),
			HUD_PANEL_EDGE.lightened(0.08),
			18 if is_compact else 16
		)
	)
	button.add_theme_stylebox_override(
		"pressed",
		_build_button_style(
			Color(0.2, 0.06, 0.03, 0.98),
			HUD_PANEL_EDGE.darkened(0.12),
			18 if is_compact else 16
		)
	)
	button.add_theme_stylebox_override(
		"disabled",
		_build_button_style(
			Color(0.14, 0.06, 0.04, 0.82),
			HUD_PANEL_EDGE_SOFT.darkened(0.24),
			18 if is_compact else 16
		)
	)


func _build_skill_slot_style(slot: Dictionary) -> StyleBoxFlat:
	if bool(slot.get("is_empty", false)):
		return _build_panel_style(Color(0.09, 0.05, 0.03, 0.78), Color(0.24, 0.15, 0.1, 0.72), 10, 1)

	var accent_color := slot.get("accent_color", Color(0.96, 0.78, 0.3, 1.0)) as Color
	var dark_color := slot.get("accent_dark", accent_color.darkened(0.5)) as Color
	var edge_color := slot.get("edge_color", accent_color.lightened(0.12)) as Color
	if bool(slot.get("is_disabled", false)):
		return _build_panel_style(dark_color.darkened(0.26), edge_color.darkened(0.22), 10, 2)
	if bool(slot.get("is_selected", false)):
		return _build_panel_style(accent_color.darkened(0.34), HUD_PANEL_EDGE.lightened(0.06), 10, 2)
	return _build_panel_style(dark_color, edge_color, 10, 2)


func _apply_progress_bar_skin(progress_bar: ProgressBar, fill_color: Color) -> void:
	progress_bar.show_percentage = false
	progress_bar.add_theme_stylebox_override(
		"background",
		_build_button_style(Color(0.08, 0.03, 0.02, 0.96), Color(0.3, 0.18, 0.1, 0.92), 6, 1)
	)
	progress_bar.add_theme_stylebox_override(
		"fill",
		_build_button_style(fill_color.darkened(0.12), fill_color.lightened(0.04), 6, 1)
	)


func _style_header_label(label: Label, font_size: int, font_color: Color) -> void:
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", font_color)


func _style_stat_label(label: Label) -> void:
	label.add_theme_font_size_override("font_size", 12)
	label.add_theme_color_override("font_color", HUD_TEXT_PRIMARY)


func _build_panel_style(
	background_color: Color,
	border_color: Color,
	radius: int = 20,
	border_width: int = 2,
	shadow_color: Color = Color(0.0, 0.0, 0.0, 0.34)
) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = background_color
	style.border_width_left = border_width
	style.border_width_top = border_width
	style.border_width_right = border_width
	style.border_width_bottom = border_width
	style.border_color = border_color
	style.corner_radius_top_left = radius
	style.corner_radius_top_right = radius
	style.corner_radius_bottom_right = radius
	style.corner_radius_bottom_left = radius
	style.shadow_color = shadow_color
	style.shadow_size = 10
	return style


func _build_button_style(
	background_color: Color,
	border_color: Color,
	radius: int = 14,
	border_width: int = 2
) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = background_color
	style.border_width_left = border_width
	style.border_width_top = border_width
	style.border_width_right = border_width
	style.border_width_bottom = border_width
	style.border_color = border_color
	style.corner_radius_top_left = radius
	style.corner_radius_top_right = radius
	style.corner_radius_bottom_right = radius
	style.corner_radius_bottom_left = radius
	return style
