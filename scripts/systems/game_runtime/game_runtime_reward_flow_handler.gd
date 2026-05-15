class_name GameRuntimeRewardFlowHandler
extends RefCounted

const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")

const RUNTIME_UNAVAILABLE_MESSAGE := "运行时尚未初始化。"
const INVALID_PROMOTION_CHOICE_MESSAGE := "晋升提交无效，当前选择仍需确认。"

var _runtime_ref: WeakRef = null
var _runtime = null:
	get:
		return _runtime_ref.get_ref() if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null


func setup(runtime) -> void:
	_runtime = runtime


func dispose() -> void:
	_runtime = null


func get_current_promotion_prompt() -> Dictionary:
	if not _has_runtime():
		return {}
	if not _get_pending_promotion_prompt().is_empty():
		return _get_pending_promotion_prompt()
	if not _get_pending_world_promotion_prompt().is_empty():
		return _get_pending_world_promotion_prompt()
	return {}


func command_confirm_pending_reward() -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	if _get_active_reward() == null and not present_pending_reward_if_ready():
		return _command_error("当前没有待确认的角色奖励。")
	if _get_active_reward() == null:
		return _command_error("当前没有待确认的角色奖励。")
	on_character_reward_confirmed()
	return _command_ok()


func command_choose_promotion(profession_id: StringName) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	var prompt := get_current_promotion_prompt()
	if prompt.is_empty():
		return _command_error("当前没有待确认的职业晋升选择。")
	var member_id := ProgressionDataUtils.to_string_name(prompt.get("member_id", ""))
	for choice_variant in prompt.get("choices", []):
		if choice_variant is not Dictionary:
			continue
		var choice_data: Dictionary = choice_variant
		var candidate_profession_id := ProgressionDataUtils.to_string_name(choice_data.get("profession_id", ""))
		if candidate_profession_id != profession_id:
			continue
		var selection: Dictionary = choice_data.get("selection", {}).duplicate(true)
		if on_promotion_choice_submitted(member_id, candidate_profession_id, selection):
			return _command_ok()
		return _command_error(INVALID_PROMOTION_CHOICE_MESSAGE)
	return _command_error("当前晋升列表中不存在职业 %s。" % String(profession_id))


func command_close_active_modal() -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	match _get_active_modal_id():
		"settlement":
			_close_settlement_modal()
			return _command_ok()
		"contract_board":
			_close_contract_board_modal()
			return _command_ok()
		"shop":
			_close_shop_modal()
			return _command_ok()
		"forge":
			_close_forge_modal()
			return _command_ok()
		"stagecoach":
			_close_stagecoach_modal()
			return _command_ok()
		"character_info":
			on_character_info_window_closed()
			return _command_ok()
		"party":
			_close_party_management_modal()
			return _command_ok()
		"warehouse":
			_close_party_warehouse_modal()
			return _command_ok()
		"submap_confirm":
			_cancel_submap_entry_prompt()
			return _command_ok()
		"battle_start_confirm":
			return _command_error("当前战斗开始确认必须点击“开始战斗”。")
		"promotion":
			return _command_error("当前晋升选择必须确认后才能继续。")
		"reward":
			return _command_error("当前角色奖励必须确认后才能继续。")
		_:
			return _command_error("当前没有可关闭的窗口。")


func submit_promotion_choice(member_id: StringName, profession_id: StringName, selection: Dictionary) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	if not on_promotion_choice_submitted(member_id, profession_id, selection):
		return _command_error(INVALID_PROMOTION_CHOICE_MESSAGE)
	return _command_ok()


func cancel_promotion_choice() -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	on_promotion_choice_cancelled()
	return _command_ok()


func confirm_active_reward() -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	on_character_reward_confirmed()
	return _command_ok()


func on_character_info_window_closed() -> void:
	if not _has_runtime():
		return
	_clear_active_character_info_context()
	_set_active_modal_id("")
	_update_status("已关闭人物信息窗。")
	present_pending_reward_if_ready()


func on_promotion_choice_submitted(member_id: StringName, profession_id: StringName, selection: Dictionary) -> bool:
	if not _has_runtime():
		return false
	if _is_battle_active():
		if not _promotion_prompt_contains_choice(_get_pending_promotion_prompt(), member_id, profession_id, selection):
			_reject_invalid_promotion_choice()
			return false
		var batch = _submit_battle_promotion_choice(member_id, profession_id, selection)
		_apply_battle_batch(batch)
		if not _battle_promotion_batch_applied(batch, member_id, profession_id):
			_reject_invalid_promotion_choice()
			return false
		if not _battle_promotion_batch_needs_follow_up(batch, member_id):
			_clear_pending_promotion_prompt()
			_set_active_modal_id("")
		return true

	var prompt := _get_pending_world_promotion_prompt()
	if prompt.is_empty():
		_reject_invalid_promotion_choice()
		return false
	if not _promotion_prompt_contains_choice(prompt, member_id, profession_id, selection):
		_reject_invalid_promotion_choice()
		return false
	var delta = _promote_profession(member_id, profession_id, selection)
	if not _promotion_delta_applied(delta, member_id, profession_id):
		_reject_invalid_promotion_choice()
		return false
	_clear_pending_world_promotion_prompt()
	_set_active_modal_id("")
	_sync_party_state_from_character_management()
	var persist_error := int(_persist_party_state())
	if delta.needs_promotion_modal:
		_set_pending_world_promotion_prompt(_build_runtime_promotion_prompt(delta, "确认后将在世界地图立即生效。"))
		_set_active_modal_id("promotion")
		if persist_error == OK:
			_update_status("%s 完成晋升后还有后续抉择待确认。" % _get_member_display_name(member_id))
		else:
			_update_status("%s 的晋升已应用，但队伍状态持久化失败。" % _get_member_display_name(member_id))
		return true

	if persist_error == OK:
		_update_status("%s 完成职业晋升。" % _get_member_display_name(member_id))
	else:
		_update_status("%s 完成职业晋升，但队伍状态持久化失败。" % _get_member_display_name(member_id))
	present_pending_reward_if_ready()
	return true


func on_promotion_choice_cancelled() -> void:
	if not _has_runtime():
		return
	if _is_battle_active():
		if _get_pending_promotion_prompt().is_empty():
			_update_status("当前晋升选择无法取消。")
			return
		_set_active_modal_id("promotion")
		_update_status("当前晋升选择必须确认后才能继续战斗。")
		return

	if _get_pending_world_promotion_prompt().is_empty():
		_update_status("当前晋升选择无法取消。")
		return
	_set_active_modal_id("promotion")
	_update_status("当前晋升选择必须确认后才能继续结算奖励。")


func on_character_reward_confirmed() -> void:
	if not _has_runtime() or _get_active_reward() == null:
		return
	var reward = _get_active_reward()
	_clear_active_reward()
	_set_active_modal_id("")

	var delta = _apply_pending_character_reward_to_party(reward)
	_sync_party_state_from_character_management()
	var persist_error := int(_persist_party_state())
	if delta.needs_promotion_modal:
		_set_pending_world_promotion_prompt(_build_runtime_promotion_prompt(delta, "确认后将在世界地图立即生效。"))
		_set_active_modal_id("promotion")
		if persist_error == OK:
			_update_status("%s 的角色奖励已入账，职业晋升待确认。" % reward.member_name)
		else:
			_update_status("%s 的角色奖励已入账，但队伍状态持久化失败。" % reward.member_name)
		return

	if delta.mastery_changes.is_empty() and delta.knowledge_changes.is_empty() and delta.attribute_changes.is_empty():
		if persist_error == OK:
			_update_status("%s 的本批奖励当前没有可入账项目。" % reward.member_name)
		else:
			_update_status("%s 的奖励处理完成，但队伍状态持久化失败。" % reward.member_name)
	else:
		if persist_error == OK:
			_update_status("%s 的角色奖励已结算。" % reward.member_name)
		else:
			_update_status("%s 的角色奖励已结算，但队伍状态持久化失败。" % reward.member_name)
	present_pending_reward_if_ready()


func enqueue_pending_character_rewards(reward_variants: Array) -> void:
	if not _has_runtime():
		return
	_enqueue_character_rewards(reward_variants)


func present_pending_reward_if_ready() -> bool:
	var active_modal_id := _get_active_modal_id()
	if not _has_runtime() or _is_battle_active():
		return false
	if not _get_pending_world_promotion_prompt().is_empty():
		if active_modal_id != "promotion":
			_set_active_modal_id("promotion")
			return true
		return false
	if _get_active_reward() != null:
		if active_modal_id != "reward":
			_set_active_modal_id("reward")
			return true
		return false
	if active_modal_id == "settlement" or active_modal_id == "contract_board" or active_modal_id == "shop" or active_modal_id == "forge" or active_modal_id == "stagecoach" or active_modal_id == "character_info" or active_modal_id == "party" or active_modal_id == "warehouse" or active_modal_id == "submap_confirm" or active_modal_id == "battle_start_confirm" or active_modal_id == "game_over":
		return false
	var party_state = _get_party_state()
	if party_state == null or party_state.pending_character_rewards.is_empty():
		return false

	_set_active_reward(party_state.get_next_pending_character_reward())
	if _get_active_reward() == null:
		return false
	_set_active_modal_id("reward")
	return true


func _has_runtime() -> bool:
	return _runtime != null


func _runtime_unavailable_error() -> Dictionary:
	return {
		"ok": false,
		"message": RUNTIME_UNAVAILABLE_MESSAGE,
	}


func _command_ok(message: String = "") -> Dictionary:
	if not _has_runtime():
		return {"ok": true, "message": message}
	return _runtime.build_command_ok(message)


func _command_error(message: String) -> Dictionary:
	if not _has_runtime():
		return {"ok": false, "message": message}
	return _runtime.build_command_error(message)


func _reject_invalid_promotion_choice() -> void:
	_set_active_modal_id("promotion")
	_update_status(INVALID_PROMOTION_CHOICE_MESSAGE)


func _promotion_prompt_contains_choice(
	prompt: Dictionary,
	member_id: StringName,
	profession_id: StringName,
	selection: Dictionary
) -> bool:
	if prompt.is_empty():
		return false
	if ProgressionDataUtils.to_string_name(prompt.get("member_id", "")) != member_id:
		return false
	var choices_variant: Variant = prompt.get("choices", [])
	if choices_variant is not Array:
		return false
	for choice_variant in choices_variant:
		if choice_variant is not Dictionary:
			continue
		var choice_data: Dictionary = choice_variant
		if ProgressionDataUtils.to_string_name(choice_data.get("profession_id", "")) != profession_id:
			continue
		var choice_selection_variant: Variant = choice_data.get("selection", {})
		if choice_selection_variant is not Dictionary:
			continue
		var choice_selection: Dictionary = choice_selection_variant
		if choice_selection == selection:
			return true
	return false


func _battle_promotion_batch_applied(batch, member_id: StringName, profession_id: StringName) -> bool:
	if batch == null:
		return false
	for delta in batch.progression_deltas:
		if _promotion_delta_applied(delta, member_id, profession_id):
			return true
	return false


func _battle_promotion_batch_needs_follow_up(batch, member_id: StringName) -> bool:
	if batch == null:
		return false
	for delta in batch.progression_deltas:
		if delta != null and delta.member_id == member_id and delta.needs_promotion_modal:
			return true
	return false


func _promotion_delta_applied(delta, member_id: StringName, profession_id: StringName) -> bool:
	if delta == null:
		return false
	if delta.member_id != member_id:
		return false
	if delta.needs_promotion_modal:
		return true
	return delta.changed_profession_ids.has(profession_id)


func _get_pending_promotion_prompt() -> Dictionary:
	if not _has_runtime():
		return {}
	return _runtime.get_pending_promotion_prompt()


func _get_pending_world_promotion_prompt() -> Dictionary:
	if not _has_runtime():
		return {}
	return _runtime.get_pending_world_promotion_prompt_state()


func _get_active_reward():
	if not _has_runtime():
		return null
	return _runtime.get_active_reward_state()


func _get_active_modal_id() -> String:
	if not _has_runtime():
		return ""
	return _runtime.get_active_modal_id()


func _set_active_modal_id(modal_id: String) -> void:
	if _has_runtime():
		_runtime.set_runtime_active_modal_id(modal_id)


func _update_status(message: String) -> void:
	if _has_runtime():
		_runtime.update_status(message)


func _is_battle_active() -> bool:
	if not _has_runtime():
		return false
	return _runtime.is_battle_active()


func _clear_active_character_info_context() -> void:
	if _has_runtime():
		_runtime.clear_active_character_info_context()


func _close_settlement_modal() -> void:
	if _has_runtime():
		_runtime.close_settlement_modal()


func _close_contract_board_modal() -> void:
	if _has_runtime():
		_runtime.close_contract_board_modal()


func _close_shop_modal() -> void:
	if _has_runtime():
		_runtime.close_shop_modal()


func _close_forge_modal() -> void:
	if _has_runtime():
		_runtime.close_forge_modal()


func _close_stagecoach_modal() -> void:
	if _has_runtime():
		_runtime.close_stagecoach_modal()


func _close_party_management_modal() -> void:
	if _has_runtime():
		_runtime.close_party_management_modal()


func _close_party_warehouse_modal() -> void:
	if _has_runtime():
		_runtime.close_party_warehouse_modal()


func _cancel_submap_entry_prompt() -> void:
	if _has_runtime():
		_runtime.command_cancel_submap_entry()


func _clear_pending_promotion_prompt() -> void:
	if _has_runtime():
		_runtime.clear_pending_promotion_prompt()


func _clear_pending_world_promotion_prompt() -> void:
	if _has_runtime():
		_runtime.clear_pending_world_promotion_prompt_state()


func _submit_battle_promotion_choice(member_id: StringName, profession_id: StringName, selection: Dictionary):
	if not _has_runtime():
		return null
	return _runtime.submit_battle_promotion_choice(member_id, profession_id, selection)


func _apply_battle_batch(batch) -> void:
	if _has_runtime():
		_runtime.apply_battle_batch(batch)


func _promote_profession(member_id: StringName, profession_id: StringName, selection: Dictionary):
	if not _has_runtime():
		return null
	return _runtime.promote_profession(member_id, profession_id, selection)


func _sync_party_state_from_character_management() -> void:
	if _has_runtime():
		_runtime.sync_party_state_from_character_management()


func _persist_party_state() -> int:
	if not _has_runtime():
		return ERR_UNAVAILABLE
	return int(_runtime.persist_party_state())


func _build_runtime_promotion_prompt(delta, selection_hint: String) -> Dictionary:
	if not _has_runtime():
		return {}
	return _runtime.build_runtime_promotion_prompt(delta, selection_hint)


func _set_pending_world_promotion_prompt(prompt: Dictionary) -> void:
	if _has_runtime():
		_runtime.set_pending_world_promotion_prompt_state(prompt)


func _get_member_display_name(member_id: StringName) -> String:
	if not _has_runtime():
		return String(member_id)
	return _runtime.get_member_display_name(member_id)


func _clear_active_reward() -> void:
	if _has_runtime():
		_runtime.clear_active_reward_state()


func _apply_pending_character_reward_to_party(reward):
	if not _has_runtime():
		return null
	return _runtime.apply_pending_character_reward_to_party(reward)


func _enqueue_character_rewards(reward_variants: Array) -> void:
	if _has_runtime():
		_runtime.enqueue_character_rewards(reward_variants)


func _get_party_state():
	if not _has_runtime():
		return null
	return _runtime.get_party_state()


func _set_active_reward(reward) -> void:
	if _has_runtime():
		_runtime.set_active_reward_state(reward)
