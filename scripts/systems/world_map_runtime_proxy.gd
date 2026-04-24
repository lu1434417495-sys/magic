class_name WorldMapRuntimeProxy
extends RefCounted

const RUNTIME_UNAVAILABLE_MESSAGE := "运行时尚未初始化。"

var _runtime = null
var _render_callback: Callable = Callable()


func setup(runtime, render_callback: Callable = Callable()) -> void:
	_runtime = runtime
	_render_callback = render_callback


func dispose() -> void:
	_runtime = null
	_render_callback = Callable()


func get_status_text() -> String:
	return String(_call_runtime_read(&"get_status_text", ""))


func get_active_modal_id() -> String:
	return String(_call_runtime_read(&"get_active_modal_id", ""))


func get_game_over_context() -> Dictionary:
	return _call_runtime_read(&"get_game_over_context", {})


func get_active_settlement_id() -> String:
	return String(_call_runtime_read(&"get_active_settlement_id", ""))


func get_active_map_id() -> String:
	return String(_call_runtime_read(&"get_active_map_id", ""))


func get_active_map_display_name() -> String:
	return String(_call_runtime_read(&"get_active_map_display_name", ""))


func get_submap_return_hint_text() -> String:
	return String(_call_runtime_read(&"get_submap_return_hint_text", ""))


func get_pending_submap_prompt() -> Dictionary:
	return _call_runtime_read(&"get_pending_submap_prompt", {})


func get_pending_battle_start_prompt() -> Dictionary:
	return _call_runtime_read(&"get_pending_battle_start_prompt", {})


func get_log_snapshot(limit: int = 80) -> Dictionary:
	return _call_runtime_read(&"get_log_snapshot", {}, [limit])


func build_headless_snapshot() -> Dictionary:
	return _call_runtime_read(&"build_headless_snapshot", {})


func build_text_snapshot() -> String:
	return String(_call_runtime_read(&"build_text_snapshot", ""))


func advance(delta: float) -> bool:
	return bool(_call_runtime_read(&"advance", false, [delta]))


func get_grid_system():
	return _call_runtime_read(&"get_grid_system", null)


func get_fog_system():
	return _call_runtime_read(&"get_fog_system", null)


func get_world_data() -> Dictionary:
	return _call_runtime_read(&"get_world_data", {})


func get_player_coord() -> Vector2i:
	return _call_runtime_read(&"get_player_coord", Vector2i.ZERO)


func is_player_visible_on_world_map() -> bool:
	return bool(_call_runtime_read(&"is_player_visible_on_world_map", true))


func get_selected_coord() -> Vector2i:
	return _call_runtime_read(&"get_selected_coord", Vector2i.ZERO)


func get_player_faction_id() -> String:
	return String(_call_runtime_read(&"get_player_faction_id", "player"))


func get_battle_state() -> BattleState:
	return _call_runtime_read(&"get_battle_state", null)


func get_battle_selected_coord() -> Vector2i:
	return _call_runtime_read(&"get_battle_selected_coord", Vector2i(-1, -1))


func get_last_advance_battle_refresh_mode() -> String:
	return String(_call_runtime_read(&"get_last_advance_battle_refresh_mode", ""))


func get_selected_battle_skill_id() -> StringName:
	return _call_runtime_read(&"get_selected_battle_skill_id", &"")


func get_selected_battle_skill_name() -> String:
	return String(_call_runtime_read(&"get_selected_battle_skill_name", ""))


func get_selected_battle_skill_variant_name() -> String:
	return String(_call_runtime_read(&"get_selected_battle_skill_variant_name", ""))


func get_selected_battle_skill_variant_id() -> StringName:
	return _call_runtime_read(&"get_selected_battle_skill_variant_id", &"")


func get_selected_battle_skill_target_coords() -> Array[Vector2i]:
	return _call_runtime_read(&"get_selected_battle_skill_target_coords", [])


func get_selected_battle_skill_target_unit_ids() -> Array[StringName]:
	return _call_runtime_read(&"get_selected_battle_skill_target_unit_ids", [])


func get_battle_overlay_target_coords() -> Array[Vector2i]:
	return _call_runtime_read(&"get_battle_overlay_target_coords", [])


func get_selected_battle_skill_required_coord_count() -> int:
	return int(_call_runtime_read(&"get_selected_battle_skill_required_coord_count", 0))


func get_settlement_window_data(settlement_id: String = "") -> Dictionary:
	return _call_runtime_read(&"get_settlement_window_data", {}, [settlement_id])


func get_settlement_feedback_text() -> String:
	return String(_call_runtime_read(&"get_settlement_feedback_text", ""))


func get_shop_window_data() -> Dictionary:
	return _call_runtime_read(&"get_shop_window_data", {})


func get_contract_board_window_data() -> Dictionary:
	return _call_runtime_read(&"get_contract_board_window_data", {})


func get_forge_window_data() -> Dictionary:
	return _call_runtime_read(&"get_forge_window_data", {})


func get_stagecoach_window_data() -> Dictionary:
	return _call_runtime_read(&"get_stagecoach_window_data", {})


func get_character_info_context() -> Dictionary:
	return _call_runtime_read(&"get_character_info_context", {})


func get_party_state():
	return _call_runtime_read(&"get_party_state", null)


func get_party_selected_member_id() -> StringName:
	return _call_runtime_read(&"get_party_selected_member_id", &"")


func get_warehouse_window_data() -> Dictionary:
	return _call_runtime_read(&"get_warehouse_window_data", {})


func get_current_promotion_prompt() -> Dictionary:
	return _call_runtime_read(&"get_current_promotion_prompt", {})


func get_active_reward():
	return _call_runtime_read(&"get_active_reward", null)


func get_pending_reward_count() -> int:
	return int(_call_runtime_read(&"get_pending_reward_count", 0))


func is_battle_active() -> bool:
	return bool(_call_runtime_read(&"is_battle_active", false))


func is_modal_window_open() -> bool:
	return bool(_call_runtime_read(&"is_modal_window_open", false))


func is_submap_active() -> bool:
	return bool(_call_runtime_read(&"is_submap_active", false))


func command_world_move(direction: Vector2i, count: int = 1) -> Dictionary:
	return _call_runtime_command(&"command_world_move", [direction, count])


func command_world_select(coord: Vector2i) -> Dictionary:
	return _call_runtime_command(&"command_world_select", [coord])


func command_open_settlement(coord: Vector2i = Vector2i(-1, -1)) -> Dictionary:
	return _call_runtime_command(&"command_open_settlement", [coord])


func command_world_inspect(coord: Vector2i) -> Dictionary:
	return _call_runtime_command(&"command_world_inspect", [coord])


func command_open_party() -> Dictionary:
	return _call_runtime_command(&"command_open_party")


func command_accept_quest(quest_id: StringName, allow_reaccept: bool = false) -> Dictionary:
	return _call_runtime_command(&"command_accept_quest", [quest_id, allow_reaccept])


func command_progress_quest(quest_id: StringName, objective_id: StringName, progress_delta: int = 1, payload: Dictionary = {}) -> Dictionary:
	return _call_runtime_command(&"command_progress_quest", [quest_id, objective_id, progress_delta, payload])


func command_complete_quest(quest_id: StringName) -> Dictionary:
	return _call_runtime_command(&"command_complete_quest", [quest_id])


func command_select_party_member(member_id: StringName) -> Dictionary:
	return _call_runtime_command(&"command_select_party_member", [member_id])


func command_set_party_leader(member_id: StringName) -> Dictionary:
	return _call_runtime_command(&"command_set_party_leader", [member_id])


func command_move_member_to_active(member_id: StringName) -> Dictionary:
	return _call_runtime_command(&"command_move_member_to_active", [member_id])


func command_move_member_to_reserve(member_id: StringName) -> Dictionary:
	return _call_runtime_command(&"command_move_member_to_reserve", [member_id])


func command_open_party_warehouse() -> Dictionary:
	return _call_runtime_command(&"command_open_party_warehouse")


func command_warehouse_discard_one(item_id: StringName) -> Dictionary:
	return _call_runtime_command(&"command_warehouse_discard_one", [item_id])


func command_warehouse_discard_all(item_id: StringName) -> Dictionary:
	return _call_runtime_command(&"command_warehouse_discard_all", [item_id])


func command_warehouse_use_item(item_id: StringName, member_id: StringName = &"") -> Dictionary:
	return _call_runtime_command(&"command_warehouse_use_item", [item_id, member_id])


func command_execute_settlement_action(action_id: String, payload: Dictionary = {}) -> Dictionary:
	return _call_runtime_command(&"command_execute_settlement_action", [action_id, payload])


func command_shop_buy(item_id: StringName, quantity: int = 1) -> Dictionary:
	return _call_runtime_command(&"command_shop_buy", [item_id, quantity])


func command_shop_sell(item_id: StringName, quantity: int = 1) -> Dictionary:
	return _call_runtime_command(&"command_shop_sell", [item_id, quantity])


func command_stagecoach_travel(settlement_id: String) -> Dictionary:
	return _call_runtime_command(&"command_stagecoach_travel", [settlement_id])


func command_battle_tick(total_seconds: float, step_seconds: float = 1.0 / 60.0) -> Dictionary:
	return _call_runtime_command(&"command_battle_tick", [total_seconds, step_seconds])


func command_battle_select_skill(slot_index: int) -> Dictionary:
	return _call_runtime_command(&"command_battle_select_skill", [slot_index])


func command_battle_cycle_variant(step: int) -> Dictionary:
	return _call_runtime_command(&"command_battle_cycle_variant", [step])


func command_battle_clear_skill() -> Dictionary:
	return _call_runtime_command(&"command_battle_clear_skill")


func command_battle_move_to(target_coord: Vector2i) -> Dictionary:
	return _call_runtime_command(&"command_battle_move_to", [target_coord])


func command_battle_move_direction(direction: Vector2i) -> Dictionary:
	return _call_runtime_command(&"command_battle_move_direction", [direction])


func command_battle_wait_or_resolve() -> Dictionary:
	return _call_runtime_command(&"command_battle_wait_or_resolve")


func command_battle_inspect(coord: Vector2i) -> Dictionary:
	return _call_runtime_command(&"command_battle_inspect", [coord])


func command_confirm_pending_reward() -> Dictionary:
	return _call_runtime_command(&"command_confirm_pending_reward")


func command_choose_promotion(profession_id: StringName) -> Dictionary:
	return _call_runtime_command(&"command_choose_promotion", [profession_id])


func command_confirm_submap_entry() -> Dictionary:
	return _call_runtime_command(&"command_confirm_submap_entry")


func command_confirm_battle_start() -> Dictionary:
	return _call_runtime_command(&"command_confirm_battle_start")


func command_cancel_submap_entry() -> Dictionary:
	return _call_runtime_command(&"command_cancel_submap_entry")


func command_return_from_submap() -> Dictionary:
	return _call_runtime_command(&"command_return_from_submap")


func command_close_active_modal() -> Dictionary:
	return _call_runtime_command(&"command_close_active_modal")


func apply_party_roster(active_member_ids: Array[StringName], reserve_member_ids: Array[StringName]) -> Dictionary:
	return _call_runtime_command(&"apply_party_roster", [active_member_ids, reserve_member_ids])


func submit_promotion_choice(member_id: StringName, profession_id: StringName, selection: Dictionary) -> Dictionary:
	return _call_runtime_command(&"submit_promotion_choice", [member_id, profession_id, selection])


func cancel_promotion_choice() -> Dictionary:
	return _call_runtime_command(&"cancel_promotion_choice")


func confirm_active_reward() -> Dictionary:
	return _call_runtime_command(&"confirm_active_reward")


func reset_battle_focus() -> Dictionary:
	return _call_runtime_command(&"reset_battle_focus")


func select_world_cell(coord: Vector2i) -> Dictionary:
	return _call_runtime_command(&"select_world_cell", [coord])


func inspect_world_cell(coord: Vector2i) -> Dictionary:
	return _call_runtime_command(&"inspect_world_cell", [coord])


func select_battle_cell(coord: Vector2i) -> Dictionary:
	return _call_runtime_command(&"select_battle_cell", [coord])


func inspect_battle_cell(coord: Vector2i) -> Dictionary:
	return _call_runtime_command(&"inspect_battle_cell", [coord])


func _call_runtime_read(method_name: StringName, default_value: Variant, args: Array = []) -> Variant:
	if _runtime == null:
		return default_value
	var method := String(method_name)
	if not _runtime.has_method(method):
		return default_value
	return _runtime.callv(method, args)


func _call_runtime_command(method_name: StringName, args: Array = []) -> Dictionary:
	if _runtime == null:
		return _runtime_unavailable_error()
	var method := String(method_name)
	if not _runtime.has_method(method):
		return {
			"ok": false,
			"message": "运行时缺少接口 %s。" % method,
		}
	var result_variant = _runtime.callv(method, args)
	var result: Dictionary = {}
	if result_variant is Dictionary:
		result = result_variant
	else:
		var type_name := type_string(typeof(result_variant))
		push_warning("WorldMapRuntimeProxy.%s 返回了非 Dictionary 结果（%s），已改为错误结果。" % [method, type_name])
		result = {
			"ok": false,
			"message": "运行时接口 %s 返回了非 Dictionary 结果。" % method,
			"invalid_result_type": type_name,
		}
	if _render_callback.is_valid():
		_render_callback.call(true, result)
	return result


func _runtime_unavailable_error() -> Dictionary:
	return {
		"ok": false,
		"message": RUNTIME_UNAVAILABLE_MESSAGE,
	}
