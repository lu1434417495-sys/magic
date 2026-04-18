## 文件说明：该脚本属于世界地图场景适配层，集中维护视图接线、输入捕获、渲染同步和窗口信号回调。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及运行时代理边界是否仍然可靠。

class_name WorldMapSystem
extends Control

const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime_facade.gd")
const WORLD_MAP_RUNTIME_PROXY_SCRIPT = preload("res://scripts/systems/world_map_runtime_proxy.gd")
const WORLD_MOVE_REPEAT_INTERVAL := 0.5
const BATTLE_LOADING_LABEL_TEXT := "LOADING..."
const BATTLE_LOADING_TEXT_COLOR := Color(1.0, 1.0, 1.0, 1.0)
const BATTLE_LOADING_PERCENT_TEXT_COLOR := Color(0.86, 0.92, 1.0, 0.95)
const BATTLE_LOADING_BAR_BG_COLOR := Color(0.1, 0.13, 0.19, 0.96)
const BATTLE_LOADING_BAR_FILL_COLOR := Color(0.39, 0.72, 0.98, 1.0)
const BATTLE_LOADING_BAR_BORDER_COLOR := Color(0.76, 0.87, 0.98, 0.78)
const BATTLE_LOADING_PROGRESS_MIN := 0.0
const BATTLE_LOADING_PROGRESS_MAX := 100.0

@onready var world_map_view = $MapViewport/WorldMapView
@onready var world_map_background := get_node_or_null("MapViewport/WorldMapBackground") as CanvasItem
@onready var battle_map_panel: BattleMapPanel = $MapViewport/BattleMapPanel
@onready var status_label := get_node_or_null("StatusPanel/StatusMargin/StatusLabel") as Label
@onready var settlement_window = $SettlementWindow
@onready var contract_board_window = $ContractBoardWindow
@onready var shop_window = $ShopWindow
@onready var forge_window = $ForgeWindow
@onready var stagecoach_window = $StagecoachWindow
@onready var character_info_window = $CharacterInfoWindow
@onready var party_management_window = $PartyManagementWindow
@onready var party_warehouse_window = $PartyWarehouseWindow
@onready var promotion_choice_window = $PromotionChoiceWindow
@onready var character_reward_window = $MasteryRewardWindow
@onready var submap_entry_window = $SubmapEntryWindow
@onready var submap_hint_panel: Control = %SubmapHintPanel
@onready var submap_hint_label: Label = %SubmapHintLabel
@onready var battle_loading_overlay: Control = %BattleLoadingOverlay
@onready var battle_loading_label: Label = %BattleLoadingLabel
@onready var battle_loading_progress_bar: ProgressBar = %BattleLoadingProgressBar
@onready var battle_loading_percent_label: Label = %BattleLoadingPercentLabel

var _game_session = null
var _runtime = null
var _runtime_proxy = WORLD_MAP_RUNTIME_PROXY_SCRIPT.new()
var _held_world_move_keys: Array[int] = []
var _world_move_repeat_timer := 0.0


func _ready() -> void:
	_game_session = get_tree().root.get_node_or_null("GameSession") if get_tree() != null else null
	if _game_session == null:
		push_error("World map requires the GameSession autoload.")
		return
	if not _game_session.has_active_world():
		push_error("World map requires an active save loaded in GameSession.")
		return
	if _game_session.get_generation_config() == null:
		push_error("GameSession is missing an active world generation config.")
		return

	_runtime = GAME_RUNTIME_FACADE_SCRIPT.new()
	_runtime.setup(_game_session)
	_runtime_proxy.setup(_runtime, Callable(self, "_render_from_runtime"))

	battle_map_panel.battle_loading_state_changed.connect(_on_battle_loading_state_changed)
	battle_map_panel.hide_battle()
	_apply_battle_loading_overlay_skin()
	_set_battle_loading_overlay(false, 0.0)
	party_management_window.set_achievement_defs(_game_session.get_achievement_defs())
	party_management_window.set_item_defs(_game_session.get_item_defs())

	settlement_window.action_requested.connect(_on_settlement_action_requested)
	settlement_window.closed.connect(_on_settlement_window_closed)
	contract_board_window.action_requested.connect(_on_contract_board_action_requested)
	contract_board_window.closed.connect(_on_contract_board_window_closed)
	shop_window.action_requested.connect(_on_shop_action_requested)
	shop_window.closed.connect(_on_shop_window_closed)
	forge_window.action_requested.connect(_on_forge_action_requested)
	forge_window.closed.connect(_on_forge_window_closed)
	stagecoach_window.action_requested.connect(_on_stagecoach_action_requested)
	stagecoach_window.closed.connect(_on_stagecoach_window_closed)
	character_info_window.closed.connect(_on_character_info_window_closed)
	party_management_window.leader_change_requested.connect(_on_party_leader_change_requested)
	party_management_window.roster_change_requested.connect(_on_party_roster_change_requested)
	party_management_window.warehouse_requested.connect(_on_party_management_warehouse_requested)
	party_management_window.closed.connect(_on_party_management_window_closed)
	party_warehouse_window.discard_one_requested.connect(_on_party_warehouse_discard_one_requested)
	party_warehouse_window.discard_all_requested.connect(_on_party_warehouse_discard_all_requested)
	party_warehouse_window.use_requested.connect(_on_party_warehouse_use_requested)
	party_warehouse_window.closed.connect(_on_party_warehouse_window_closed)
	promotion_choice_window.choice_submitted.connect(_on_promotion_choice_submitted)
	promotion_choice_window.cancelled.connect(_on_promotion_choice_cancelled)
	character_reward_window.confirmed.connect(_on_character_reward_confirmed)
	submap_entry_window.confirmed.connect(_on_submap_entry_confirmed)
	submap_entry_window.cancelled.connect(_on_submap_entry_cancelled)
	world_map_view.cell_clicked.connect(_on_world_map_cell_clicked)
	world_map_view.cell_right_clicked.connect(_on_world_map_cell_right_clicked)
	battle_map_panel.battle_cell_clicked.connect(_on_battle_cell_clicked)
	battle_map_panel.battle_cell_right_clicked.connect(_on_battle_cell_right_clicked)
	battle_map_panel.movement_reset_requested.connect(_reset_battle_movement)
	battle_map_panel.resolve_requested.connect(_resolve_active_battle)
	battle_map_panel.battle_skill_slot_selected.connect(_on_battle_skill_slot_selected)
	battle_map_panel.battle_skill_variant_cycle_requested.connect(_on_battle_skill_variant_cycle_requested)
	battle_map_panel.battle_skill_clear_requested.connect(_on_battle_skill_clear_requested)

	world_map_view.configure(
		_runtime_proxy.get_grid_system(),
		_runtime_proxy.get_fog_system(),
		_runtime_proxy.get_world_data(),
		_runtime_proxy.get_player_coord(),
		_runtime_proxy.get_selected_coord(),
		_runtime_proxy.is_player_visible_on_world_map(),
		_runtime_proxy.get_player_faction_id()
	)
	_render_from_runtime(true)


func _exit_tree() -> void:
	if _runtime_proxy != null:
		_runtime_proxy.dispose()
	if _runtime != null:
		_runtime.dispose()
	_runtime = null


func _render_from_runtime(refresh_world: bool = true, command_result: Dictionary = {}) -> void:
	if _runtime == null:
		return
	if status_label != null:
		status_label.text = _runtime_proxy.get_status_text()

	if _runtime_proxy.is_battle_active():
		if world_map_background != null:
			world_map_background.visible = false
		world_map_view.visible = false
		if submap_hint_panel != null:
			submap_hint_panel.visible = false
		var refresh_mode := String(command_result.get("battle_refresh_mode", "full"))
		var battle_state = _runtime_proxy.get_battle_state()
		var selected_coord = _runtime_proxy.get_battle_selected_coord()
		var selected_skill_id = _runtime_proxy.get_selected_battle_skill_id()
		var selected_skill_name = _runtime_proxy.get_selected_battle_skill_name()
		var selected_skill_variant_name = _runtime_proxy.get_selected_battle_skill_variant_name()
		var selected_target_coords = _runtime_proxy.get_selected_battle_skill_target_coords()
		var selected_target_unit_ids = _runtime_proxy.get_selected_battle_skill_target_unit_ids()
		var valid_target_coords = _runtime_proxy.get_battle_overlay_target_coords()
		var required_coord_count = _runtime_proxy.get_selected_battle_skill_required_coord_count()
		if battle_map_panel.visible and refresh_mode == "overlay":
			battle_map_panel.refresh_overlay(
				battle_state,
				selected_coord,
				selected_skill_id,
				selected_skill_name,
				selected_skill_variant_name,
				selected_target_coords,
				valid_target_coords,
				required_coord_count,
				selected_target_unit_ids
			)
		else:
			battle_map_panel.show_battle(
				battle_state,
				selected_coord,
				selected_skill_id,
				selected_skill_name,
				selected_skill_variant_name,
				selected_target_coords,
				valid_target_coords,
				required_coord_count,
				selected_target_unit_ids
			)
		_set_battle_loading_overlay(
			battle_map_panel.is_loading_battle(),
			battle_map_panel.get_loading_progress()
		)
	else:
		if world_map_background != null:
			world_map_background.visible = true
		world_map_view.visible = true
		battle_map_panel.hide_battle()
		_set_battle_loading_overlay(false, 0.0)
		if refresh_world:
			world_map_view.refresh_world(_runtime_proxy.get_world_data())
		world_map_view.set_runtime_state(
			_runtime_proxy.get_player_coord(),
			_runtime_proxy.get_selected_coord(),
			_runtime_proxy.is_player_visible_on_world_map()
		)
		if submap_hint_panel != null:
			submap_hint_panel.visible = _runtime_proxy.is_submap_active()
		if submap_hint_label != null:
			submap_hint_label.text = _runtime_proxy.get_submap_return_hint_text()

	var modal_id: String = _runtime_proxy.get_active_modal_id()
	if modal_id == "settlement":
		settlement_window.show_settlement(_runtime_proxy.get_settlement_window_data())
		var settlement_feedback := _runtime_proxy.get_settlement_feedback_text()
		if not settlement_feedback.is_empty():
			settlement_window.set_feedback(settlement_feedback)
	else:
		settlement_window.hide_window()

	if modal_id == "shop":
		shop_window.show_shop(_runtime_proxy.get_shop_window_data())
	else:
		shop_window.hide_window()

	if modal_id == "contract_board":
		contract_board_window.show_shop(_runtime_proxy.get_contract_board_window_data())
	else:
		contract_board_window.hide_window()

	if modal_id == "forge":
		forge_window.show_shop(_runtime_proxy.get_forge_window_data())
	else:
		forge_window.hide_window()

	if modal_id == "stagecoach":
		stagecoach_window.show_stagecoach(_runtime_proxy.get_stagecoach_window_data())
	else:
		stagecoach_window.hide_window()

	if modal_id == "character_info":
		character_info_window.show_character(_runtime_proxy.get_character_info_context())
	else:
		character_info_window.hide_window()

	if modal_id == "party":
		party_management_window.show_party(_runtime_proxy.get_party_state())
		var selected_member_id: StringName = _runtime_proxy.get_party_selected_member_id()
		if selected_member_id != &"":
			party_management_window.select_member(selected_member_id)
	else:
		party_management_window.hide_window()

	if modal_id == "warehouse":
		party_warehouse_window.show_warehouse(_runtime_proxy.get_warehouse_window_data())
	else:
		party_warehouse_window.hide_window()

	if modal_id == "promotion":
		promotion_choice_window.show_promotion(_runtime_proxy.get_current_promotion_prompt())
	else:
		promotion_choice_window.hide_window()

	if modal_id == "reward":
		character_reward_window.show_reward(
			_runtime_proxy.get_active_reward(),
			_runtime_proxy.get_pending_reward_count()
		)
	else:
		character_reward_window.hide_window()

	if modal_id == "submap_confirm":
		submap_entry_window.show_prompt(_runtime_proxy.get_pending_submap_prompt())
	elif modal_id == "battle_start_confirm":
		submap_entry_window.show_prompt(_runtime_proxy.get_pending_battle_start_prompt())
	else:
		submap_entry_window.hide_window()


func _process(delta: float) -> void:
	if _runtime == null:
		return
	var changed := _runtime_proxy.advance(delta)
	if changed:
		var render_result: Dictionary = {}
		if _runtime_proxy.is_battle_active():
			var battle_refresh_mode := _runtime_proxy.get_last_advance_battle_refresh_mode()
			if not battle_refresh_mode.is_empty():
				render_result["battle_refresh_mode"] = battle_refresh_mode
		_render_from_runtime(true, render_result)
	if _runtime_proxy.is_battle_active() or _runtime_proxy.is_modal_window_open():
		_clear_world_move_hold()
		return
	_process_world_held_movement(delta)


func _unhandled_input(event: InputEvent) -> void:
	if _runtime == null or event is not InputEventKey:
		return

	var key_event := event as InputEventKey
	if _runtime_proxy.is_battle_active():
		if battle_map_panel != null and battle_map_panel.is_loading_battle():
			return
		if _runtime_proxy.is_modal_window_open():
			return
		if not key_event.pressed or key_event.echo:
			return
		if _handle_battle_input(key_event):
			get_viewport().set_input_as_handled()
		return

	if _handle_world_input(key_event):
		get_viewport().set_input_as_handled()


func _handle_world_input(key_event: InputEventKey) -> bool:
	var movement := _get_world_move_direction_for_key(key_event.keycode)
	if not key_event.pressed:
		if movement == Vector2i.ZERO:
			return false
		_release_world_move_key(key_event.keycode)
		return true

	if key_event.echo:
		return movement != Vector2i.ZERO
	if _runtime_proxy.is_modal_window_open():
		return false

	if movement != Vector2i.ZERO:
		_press_world_move_key(key_event.keycode)
		_runtime_proxy.command_world_move(movement)
		if _runtime_proxy.is_battle_active():
			_clear_world_move_hold()
		return true

	match key_event.keycode:
		KEY_P:
			_runtime_proxy.command_open_party()
			return true
		KEY_ENTER, KEY_KP_ENTER, KEY_SPACE:
			_runtime_proxy.command_open_settlement()
			return true
		_:
			return false


func _process_world_held_movement(delta: float) -> void:
	if _held_world_move_keys.is_empty():
		_world_move_repeat_timer = 0.0
		return

	_world_move_repeat_timer -= delta
	while _world_move_repeat_timer <= 0.0:
		var movement := _get_active_world_move_direction()
		if movement == Vector2i.ZERO:
			_clear_world_move_hold()
			return

		_runtime_proxy.command_world_move(movement)
		if _runtime_proxy.is_battle_active() or _runtime_proxy.is_modal_window_open():
			_clear_world_move_hold()
			return
		_world_move_repeat_timer += WORLD_MOVE_REPEAT_INTERVAL


func _get_world_move_direction_for_key(keycode: int) -> Vector2i:
	match keycode:
		KEY_LEFT, KEY_A:
			return Vector2i.LEFT
		KEY_RIGHT, KEY_D:
			return Vector2i.RIGHT
		KEY_UP, KEY_W:
			return Vector2i.UP
		KEY_DOWN, KEY_S:
			return Vector2i.DOWN
		_:
			return Vector2i.ZERO


func _press_world_move_key(keycode: int) -> void:
	_held_world_move_keys.erase(keycode)
	_held_world_move_keys.append(keycode)
	_world_move_repeat_timer = WORLD_MOVE_REPEAT_INTERVAL


func _release_world_move_key(keycode: int) -> void:
	var was_active := keycode == _get_active_world_move_keycode()
	_held_world_move_keys.erase(keycode)
	if _held_world_move_keys.is_empty():
		_world_move_repeat_timer = 0.0
	elif was_active:
		_world_move_repeat_timer = WORLD_MOVE_REPEAT_INTERVAL


func _get_active_world_move_direction() -> Vector2i:
	var keycode := _get_active_world_move_keycode()
	if keycode == KEY_NONE:
		return Vector2i.ZERO
	return _get_world_move_direction_for_key(keycode)


func _get_active_world_move_keycode() -> int:
	if _held_world_move_keys.is_empty():
		return KEY_NONE
	return _held_world_move_keys[-1]


func _clear_world_move_hold() -> void:
	_held_world_move_keys.clear()
	_world_move_repeat_timer = 0.0


func _handle_battle_input(key_event: InputEventKey) -> bool:
	match key_event.keycode:
		KEY_1, KEY_2, KEY_3, KEY_4, KEY_5, KEY_6, KEY_7, KEY_8, KEY_9:
			_runtime_proxy.command_battle_select_skill(int(key_event.keycode - KEY_1))
		KEY_Q:
			_runtime_proxy.command_battle_cycle_variant(-1)
		KEY_E:
			_runtime_proxy.command_battle_cycle_variant(1)
		KEY_ESCAPE, KEY_R:
			_runtime_proxy.command_battle_clear_skill()
		KEY_LEFT, KEY_A:
			_runtime_proxy.command_battle_move_direction(Vector2i.LEFT)
		KEY_RIGHT, KEY_D:
			_runtime_proxy.command_battle_move_direction(Vector2i.RIGHT)
		KEY_UP, KEY_W:
			_runtime_proxy.command_battle_move_direction(Vector2i.UP)
		KEY_DOWN, KEY_S:
			_runtime_proxy.command_battle_move_direction(Vector2i.DOWN)
		KEY_ENTER, KEY_KP_ENTER, KEY_SPACE:
			_runtime_proxy.command_battle_wait_or_resolve()
		_:
			return false
	return true


func _on_battle_loading_state_changed(is_loading: bool, progress_value: float) -> void:
	_set_battle_loading_overlay(is_loading, progress_value)


func _set_battle_loading_overlay(is_visible: bool, progress_value: float) -> void:
	if battle_loading_overlay == null or battle_loading_label == null:
		return
	var clamped_progress := clampf(progress_value, BATTLE_LOADING_PROGRESS_MIN, BATTLE_LOADING_PROGRESS_MAX)
	battle_loading_label.text = BATTLE_LOADING_LABEL_TEXT
	if battle_loading_progress_bar != null:
		battle_loading_progress_bar.min_value = BATTLE_LOADING_PROGRESS_MIN
		battle_loading_progress_bar.max_value = BATTLE_LOADING_PROGRESS_MAX
		battle_loading_progress_bar.value = clamped_progress
	if battle_loading_percent_label != null:
		battle_loading_percent_label.text = _format_battle_loading_percent(clamped_progress)
	battle_loading_overlay.visible = is_visible


func _apply_battle_loading_overlay_skin() -> void:
	if battle_loading_label == null:
		return
	_style_loading_label(battle_loading_label, 24, BATTLE_LOADING_TEXT_COLOR)
	_style_loading_label(battle_loading_percent_label, 16, BATTLE_LOADING_PERCENT_TEXT_COLOR)
	_style_loading_progress_bar()


func _style_loading_label(label: Label, font_size: int, font_color: Color) -> void:
	if label == null:
		return
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", font_color)


func _style_loading_progress_bar() -> void:
	if battle_loading_progress_bar == null:
		return
	var background_style := StyleBoxFlat.new()
	background_style.bg_color = BATTLE_LOADING_BAR_BG_COLOR
	background_style.border_width_left = 1
	background_style.border_width_top = 1
	background_style.border_width_right = 1
	background_style.border_width_bottom = 1
	background_style.border_color = BATTLE_LOADING_BAR_BORDER_COLOR
	background_style.corner_radius_top_left = 7
	background_style.corner_radius_top_right = 7
	background_style.corner_radius_bottom_right = 7
	background_style.corner_radius_bottom_left = 7

	var fill_style := StyleBoxFlat.new()
	fill_style.bg_color = BATTLE_LOADING_BAR_FILL_COLOR
	fill_style.corner_radius_top_left = 7
	fill_style.corner_radius_top_right = 7
	fill_style.corner_radius_bottom_right = 7
	fill_style.corner_radius_bottom_left = 7

	battle_loading_progress_bar.add_theme_stylebox_override("background", background_style)
	battle_loading_progress_bar.add_theme_stylebox_override("fill", fill_style)


func _format_battle_loading_percent(progress_value: float) -> String:
	return "%d%%" % int(round(progress_value))


func _resolve_active_battle() -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_battle_wait_or_resolve()


func _reset_battle_movement() -> void:
	if _runtime == null:
		return
	_runtime_proxy.reset_battle_focus()


func _on_world_map_cell_clicked(coord: Vector2i) -> void:
	if _runtime == null:
		return
	_runtime_proxy.select_world_cell(coord)


func _on_world_map_cell_right_clicked(coord: Vector2i) -> void:
	if _runtime == null:
		return
	_runtime_proxy.inspect_world_cell(coord)


func _on_battle_cell_clicked(coord: Vector2i) -> void:
	if _runtime == null:
		return
	_runtime_proxy.select_battle_cell(coord)


func _on_battle_cell_right_clicked(coord: Vector2i) -> void:
	if _runtime == null:
		return
	_runtime_proxy.inspect_battle_cell(coord)


func _on_battle_skill_slot_selected(index: int) -> void:
	if _runtime == null or _runtime_proxy.is_modal_window_open():
		return
	_runtime_proxy.command_battle_select_skill(index)


func _on_battle_skill_variant_cycle_requested(step: int) -> void:
	if _runtime == null or _runtime_proxy.is_modal_window_open():
		return
	_runtime_proxy.command_battle_cycle_variant(step)


func _on_battle_skill_clear_requested() -> void:
	if _runtime == null or _runtime_proxy.is_modal_window_open():
		return
	_runtime_proxy.command_battle_clear_skill()


func _on_settlement_action_requested(_settlement_id: String, action_id: String, payload: Dictionary) -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_execute_settlement_action(action_id, payload)


func _on_settlement_window_closed() -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_close_active_modal()


func _on_shop_window_closed() -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_close_active_modal()


func _on_contract_board_window_closed() -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_close_active_modal()


func _on_contract_board_action_requested(_settlement_id: String, action_id: String, payload: Dictionary) -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_execute_settlement_action(action_id, payload)


func _on_forge_window_closed() -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_close_active_modal()


func _on_stagecoach_window_closed() -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_close_active_modal()


func _on_shop_action_requested(_settlement_id: String, _action_id: String, payload: Dictionary) -> void:
	if _runtime == null:
		return
	var quantity := maxi(int(payload.get("request_quantity", 1)), 1)
	var item_id := StringName(String(payload.get("item_id", "")))
	match String(payload.get("shop_action", "buy")):
		"sell":
			_runtime_proxy.command_shop_sell(item_id, quantity)
		_:
			_runtime_proxy.command_shop_buy(item_id, quantity)


func _on_forge_action_requested(_settlement_id: String, action_id: String, payload: Dictionary) -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_execute_settlement_action(action_id, payload)


func _on_stagecoach_action_requested(_settlement_id: String, _action_id: String, payload: Dictionary) -> void:
	if _runtime == null:
		return
	var target_settlement_id := String(payload.get("target_settlement_id", payload.get("settlement_id", "")))
	_runtime_proxy.command_stagecoach_travel(target_settlement_id)


func _on_character_info_window_closed() -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_close_active_modal()


func _on_party_leader_change_requested(member_id: StringName) -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_set_party_leader(member_id)


func _on_party_roster_change_requested(active_member_ids: Array[StringName], reserve_member_ids: Array[StringName]) -> void:
	if _runtime == null:
		return
	_runtime_proxy.apply_party_roster(active_member_ids, reserve_member_ids)


func _on_party_management_window_closed() -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_close_active_modal()


func _on_party_management_warehouse_requested() -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_open_party_warehouse()


func _on_party_warehouse_discard_one_requested(item_id: StringName) -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_warehouse_discard_one(item_id)


func _on_party_warehouse_discard_all_requested(item_id: StringName) -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_warehouse_discard_all(item_id)


func _on_party_warehouse_use_requested(item_id: StringName, member_id: StringName) -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_warehouse_use_item(item_id, member_id)


func _on_party_warehouse_window_closed() -> void:
	if _runtime == null:
		return
	_runtime_proxy.command_close_active_modal()


func _on_promotion_choice_submitted(member_id: StringName, profession_id: StringName, selection: Dictionary) -> void:
	if _runtime == null:
		return
	_runtime_proxy.submit_promotion_choice(member_id, profession_id, selection)


func _on_promotion_choice_cancelled() -> void:
	if _runtime == null:
		return
	_runtime_proxy.cancel_promotion_choice()


func _on_character_reward_confirmed() -> void:
	if _runtime == null:
		return
	_runtime_proxy.confirm_active_reward()


func _on_submap_entry_confirmed() -> void:
	if _runtime == null:
		return
	var modal_id := _runtime_proxy.get_active_modal_id()
	if modal_id == "battle_start_confirm":
		_runtime_proxy.command_confirm_battle_start()
		return
	_runtime_proxy.command_confirm_submap_entry()


func _on_submap_entry_cancelled() -> void:
	if _runtime == null:
		return
	if _runtime_proxy.get_active_modal_id() == "battle_start_confirm":
		return
	_runtime_proxy.command_cancel_submap_entry()
