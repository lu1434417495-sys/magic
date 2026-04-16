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
	settlement_window.shop_requested.connect(_on_settlement_shop_requested)
	settlement_window.stagecoach_requested.connect(_on_settlement_stagecoach_requested)
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
		_runtime_proxy.get_player_faction_id()
	)
	_render_from_runtime(true)


func _exit_tree() -> void:
	if _runtime_proxy != null and _runtime_proxy.has_method("dispose"):
		_runtime_proxy.dispose()
	if _runtime != null and _runtime.has_method("dispose"):
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
			_runtime_proxy.get_selected_coord()
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
		_render_from_runtime()
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
	battle_loading_label.text = BATTLE_LOADING_LABEL_TEXT
	battle_loading_overlay.visible = is_visible


func _apply_battle_loading_overlay_skin() -> void:
	if battle_loading_label == null:
		return
	_style_loading_label(battle_loading_label, 24, BATTLE_LOADING_TEXT_COLOR)


func _style_loading_label(label: Label, font_size: int, font_color: Color) -> void:
	if label == null:
		return
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", font_color)


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


func _on_settlement_shop_requested(settlement_id: String, action_id: String, payload: Dictionary) -> void:
	if _runtime == null:
		return
	if _runtime_proxy.get_active_modal_id() != "settlement":
		return
	_runtime_proxy.command_execute_settlement_action(action_id, payload)


func _on_settlement_stagecoach_requested(settlement_id: String, action_id: String, payload: Dictionary) -> void:
	if _runtime == null:
		return
	if _runtime_proxy.get_active_modal_id() != "settlement":
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


func _open_local_service_window(panel_kind: String, settlement_id: String, action_id: String, payload: Dictionary) -> void:
	var window_data := _build_local_service_window_data(panel_kind, settlement_id, action_id, payload)
	_hide_local_service_windows()
	match panel_kind:
		"shop":
			if shop_window != null:
				shop_window.show_shop(window_data)
			if settlement_window != null:
				settlement_window.set_feedback("已打开交易窗口。")
		"stagecoach":
			if stagecoach_window != null:
				stagecoach_window.show_stagecoach(window_data)
			if settlement_window != null:
				settlement_window.set_feedback("已打开驿站窗口。")


func _hide_local_service_windows() -> void:
	if shop_window != null and shop_window.visible:
		shop_window.hide_window()
	if stagecoach_window != null and stagecoach_window.visible:
		stagecoach_window.hide_window()


func _build_settlement_window_data() -> Dictionary:
	var window_data: Dictionary = _runtime_proxy.get_settlement_window_data().duplicate(true)
	var party_state = _runtime_proxy.get_party_state()
	if not window_data.has("party_state"):
		window_data["party_state"] = party_state
	if not window_data.has("member_options") or _get_dictionary_array(window_data.get("member_options", [])).is_empty():
		window_data["member_options"] = _build_member_options_from_party_state(party_state)
	var selected_member_id: StringName = _runtime_proxy.get_party_selected_member_id()
	if String(window_data.get("default_member_id", "")).is_empty() and selected_member_id != &"":
		window_data["default_member_id"] = String(selected_member_id)
	if String(window_data.get("selected_member_id", "")).is_empty() and selected_member_id != &"":
		window_data["selected_member_id"] = String(selected_member_id)
	if String(window_data.get("state_summary_text", "")).is_empty():
		window_data["state_summary_text"] = _runtime_proxy.get_settlement_feedback_text()
	return window_data


func _build_member_options_from_party_state(party_state) -> Array[Dictionary]:
	var member_options: Array[Dictionary] = []
	if party_state == null:
		return member_options

	var seen_ids: Dictionary = {}
	for member_id_variant in party_state.active_member_ids:
		_append_member_option_from_party_state(member_options, seen_ids, party_state, ProgressionDataUtils.to_string_name(member_id_variant), "上阵")
	for member_id_variant in party_state.reserve_member_ids:
		_append_member_option_from_party_state(member_options, seen_ids, party_state, ProgressionDataUtils.to_string_name(member_id_variant), "替补")
	return member_options


func _append_member_option_from_party_state(
	member_options: Array[Dictionary],
	seen_ids: Dictionary,
	party_state,
	member_id: StringName,
	default_role: String
) -> void:
	if member_id == &"" or seen_ids.has(member_id):
		return
	var member_state = party_state.get_member_state(member_id)
	if member_state == null:
		return
	seen_ids[member_id] = true
	member_options.append({
		"member_id": String(member_id),
		"display_name": String(member_state.display_name),
		"roster_role": default_role,
		"is_leader": party_state.leader_member_id == member_id,
		"current_hp": int(member_state.current_hp),
		"current_mp": int(member_state.current_mp),
	})


func _build_local_service_window_data(panel_kind: String, settlement_id: String, action_id: String, payload: Dictionary) -> Dictionary:
	var window_data := payload.duplicate(true)
	var title := "交易窗口" if panel_kind == "shop" else "驿站窗口"
	if String(window_data.get("title", "")).is_empty():
		window_data["title"] = title
	if String(window_data.get("meta", "")).is_empty():
		window_data["meta"] = _build_local_service_meta_text(payload)
	if String(window_data.get("summary_text", "")).is_empty():
		window_data["summary_text"] = _build_local_service_summary_text(payload)
	if String(window_data.get("details_text", "")).is_empty():
		window_data["details_text"] = _build_local_service_details_text(payload, panel_kind)
	if String(window_data.get("state_summary_text", "")).is_empty():
		window_data["state_summary_text"] = _runtime_proxy.get_settlement_feedback_text()
	if String(window_data.get("state_label", "")).is_empty():
		window_data["state_label"] = _build_default_local_state_label(panel_kind, payload)
	if String(window_data.get("cost_label", "")).is_empty():
		window_data["cost_label"] = _build_default_local_cost_label(payload)
	if String(window_data.get("service_name", "")).is_empty():
		window_data["service_name"] = String(payload.get("service_type", title))
	window_data["settlement_id"] = settlement_id
	window_data["action_id"] = action_id
	window_data["panel_kind"] = panel_kind
	if String(window_data.get("submission_source", "")).is_empty():
		window_data["submission_source"] = String(payload.get("submission_source", "settlement"))
	var party_state = _runtime_proxy.get_party_state()
	if not window_data.has("party_state"):
		window_data["party_state"] = party_state
	if not window_data.has("member_options") or _get_dictionary_array(window_data.get("member_options", [])).is_empty():
		window_data["member_options"] = _build_member_options_from_party_state(party_state)
	var default_member_id := _resolve_default_member_id_from_party_state(party_state)
	if default_member_id != &"":
		if String(window_data.get("default_member_id", "")).is_empty():
			window_data["default_member_id"] = String(default_member_id)
		if String(window_data.get("selected_member_id", "")).is_empty():
			window_data["selected_member_id"] = String(default_member_id)
	if not window_data.has("entries") or _get_dictionary_array(window_data.get("entries", [])).is_empty():
		window_data["entries"] = [_build_local_entry_data(panel_kind, payload)]
	return window_data


func _build_local_entry_data(panel_kind: String, payload: Dictionary) -> Dictionary:
	var display_name := String(payload.get("display_name", payload.get("facility_name", payload.get("service_type", "条目"))))
	var summary_text := String(payload.get("summary_text", _build_local_service_summary_text(payload)))
	var details_text := String(payload.get("details_text", _build_local_service_details_text(payload, panel_kind)))
	return {
		"entry_id": String(payload.get("action_id", panel_kind)),
		"display_name": display_name,
		"summary_text": summary_text,
		"details_text": details_text,
		"state_label": String(payload.get("state_label", _build_default_local_state_label(panel_kind, payload))),
		"cost_label": String(payload.get("cost_label", _build_default_local_cost_label(payload))),
		"is_enabled": bool(payload.get("is_enabled", true)),
		"disabled_reason": String(payload.get("disabled_reason", "")),
	}


func _build_local_service_meta_text(payload: Dictionary) -> String:
	var facility_name := String(payload.get("facility_name", "设施"))
	var npc_name := String(payload.get("npc_name", "NPC"))
	var service_type := String(payload.get("service_type", "服务"))
	var state_summary_text := String(payload.get("state_summary_text", ""))
	var base_text := "%s · %s · %s" % [facility_name, npc_name, service_type]
	if state_summary_text.is_empty():
		return base_text
	return "%s\n%s" % [base_text, state_summary_text]


func _build_local_service_summary_text(payload: Dictionary) -> String:
	var facility_name := String(payload.get("facility_name", "设施"))
	var npc_name := String(payload.get("npc_name", "NPC"))
	var service_type := String(payload.get("service_type", "服务"))
	return "%s · %s · %s" % [facility_name, npc_name, service_type]


func _build_local_service_details_text(payload: Dictionary, panel_kind: String) -> String:
	var lines := PackedStringArray([
		"设施：%s" % String(payload.get("facility_name", "设施")),
		"NPC：%s" % String(payload.get("npc_name", "NPC")),
		"服务：%s" % String(payload.get("service_type", "服务")),
		"交互：%s" % String(payload.get("interaction_type", panel_kind)),
		"状态：%s" % _build_default_local_state_label(panel_kind, payload),
		"费用：%s" % _build_default_local_cost_label(payload),
	])
	var disabled_reason := String(payload.get("disabled_reason", ""))
	if not disabled_reason.is_empty():
		lines.append("不可用原因：%s" % disabled_reason)
	return "\n".join(lines)


func _build_default_local_state_label(panel_kind: String, payload: Dictionary) -> String:
	var explicit_state_label := String(payload.get("state_label", payload.get("state_text", "")))
	if not explicit_state_label.is_empty():
		return explicit_state_label
	if not bool(payload.get("is_enabled", true)):
		var disabled_reason := String(payload.get("disabled_reason", ""))
		if not disabled_reason.is_empty():
			return "状态：%s" % disabled_reason
		return "状态：不可用"
	return "状态：%s" % ("可交易" if panel_kind == "shop" else "可出发")


func _build_default_local_cost_label(payload: Dictionary) -> String:
	var cost_label := String(payload.get("cost_label", ""))
	if not cost_label.is_empty():
		return cost_label
	return "费用：待定"


func _resolve_default_member_id_from_party_state(party_state) -> StringName:
	if party_state == null:
		return &""
	if party_state.leader_member_id != &"":
		return party_state.leader_member_id
	for member_id_variant in party_state.active_member_ids:
		var member_id := ProgressionDataUtils.to_string_name(member_id_variant)
		if member_id != &"":
			return member_id
	for member_id_variant in party_state.reserve_member_ids:
		var member_id := ProgressionDataUtils.to_string_name(member_id_variant)
		if member_id != &"":
			return member_id
	return &""


func _get_dictionary_array(value: Variant) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if value is not Array:
		return result
	for entry_variant in value:
		if entry_variant is Dictionary:
			result.append(entry_variant)
	return result
