class_name GameRuntimeRewardFlowHandler
extends RefCounted

const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")

const RUNTIME_UNAVAILABLE_MESSAGE := "运行时尚未初始化。"

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
		on_promotion_choice_submitted(member_id, candidate_profession_id, selection)
		return _command_ok()
	return _command_error("当前晋升列表中不存在职业 %s。" % String(profession_id))


func command_close_active_modal() -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	match _get_active_modal_id():
		"settlement":
			_close_settlement_modal()
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
	on_promotion_choice_submitted(member_id, profession_id, selection)
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


func on_promotion_choice_submitted(member_id: StringName, profession_id: StringName, selection: Dictionary) -> void:
	if not _has_runtime():
		return
	if _is_battle_active():
		_clear_pending_promotion_prompt()
		_set_active_modal_id("")
		var batch = _submit_battle_promotion_choice(member_id, profession_id, selection)
		_apply_battle_batch(batch)
		return

	if _get_pending_world_promotion_prompt().is_empty():
		return
	_clear_pending_world_promotion_prompt()
	_set_active_modal_id("")
	var delta = _promote_profession(member_id, profession_id, selection)
	_sync_party_state_from_character_management()
	var persist_error := int(_persist_party_state())
	if delta.needs_promotion_modal:
		_set_pending_world_promotion_prompt(_build_runtime_promotion_prompt(delta, "确认后将在世界地图立即生效。"))
		_set_active_modal_id("promotion")
		if persist_error == OK:
			_update_status("%s 完成晋升后还有后续抉择待确认。" % _get_member_display_name(member_id))
		else:
			_update_status("%s 的晋升已应用，但队伍状态持久化失败。" % _get_member_display_name(member_id))
		return

	if persist_error == OK:
		_update_status("%s 完成职业晋升。" % _get_member_display_name(member_id))
	else:
		_update_status("%s 完成职业晋升，但队伍状态持久化失败。" % _get_member_display_name(member_id))
	present_pending_reward_if_ready()


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
	if active_modal_id == "settlement" or active_modal_id == "shop" or active_modal_id == "forge" or active_modal_id == "stagecoach" or active_modal_id == "character_info" or active_modal_id == "party" or active_modal_id == "warehouse" or active_modal_id == "submap_confirm" or active_modal_id == "battle_start_confirm":
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
	return _runtime.build_command_ok(message) if _has_runtime() and _runtime.has_method("build_command_ok") else {
		"ok": true,
		"message": message,
	}


func _command_error(message: String) -> Dictionary:
	if _has_runtime() and _runtime.has_method("build_command_error"):
		return _runtime.build_command_error(message)
	if not message.is_empty():
		_update_status(message)
	return {
		"ok": false,
		"message": message,
	}


func _get_pending_promotion_prompt() -> Dictionary:
	if not _has_runtime():
		return {}
	if _runtime.has_method("get_pending_promotion_prompt"):
		return _runtime.get_pending_promotion_prompt()
	return _runtime._pending_promotion_prompt if "_pending_promotion_prompt" in _runtime else {}


func _get_pending_world_promotion_prompt() -> Dictionary:
	if not _has_runtime():
		return {}
	if _runtime.has_method("get_pending_world_promotion_prompt_state"):
		return _runtime.get_pending_world_promotion_prompt_state()
	return _runtime._pending_world_promotion_prompt if "_pending_world_promotion_prompt" in _runtime else {}


func _get_active_reward():
	if not _has_runtime():
		return null
	if _runtime.has_method("get_active_reward_state"):
		return _runtime.get_active_reward_state()
	return _runtime._active_reward if "_active_reward" in _runtime else null


func _get_active_modal_id() -> String:
	if not _has_runtime():
		return ""
	if _runtime.has_method("get_active_modal_id"):
		return _runtime.get_active_modal_id()
	return String(_runtime._active_modal_id) if "_active_modal_id" in _runtime else ""


func _set_active_modal_id(modal_id: String) -> void:
	if _has_runtime() and _runtime.has_method("set_runtime_active_modal_id"):
		_runtime.set_runtime_active_modal_id(modal_id)
	elif _has_runtime() and "_active_modal_id" in _runtime:
		_runtime._active_modal_id = modal_id


func _update_status(message: String) -> void:
	if _has_runtime() and _runtime.has_method("update_status"):
		_runtime.update_status(message)
	elif _has_runtime() and _runtime.has_method("_update_status"):
		_runtime._update_status(message)


func _is_battle_active() -> bool:
	if not _has_runtime():
		return false
	if _runtime.has_method("is_battle_active"):
		return _runtime.is_battle_active()
	return _runtime._is_battle_active() if _runtime.has_method("_is_battle_active") else false


func _clear_active_character_info_context() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("clear_active_character_info_context"):
		_runtime.clear_active_character_info_context()
	elif "_active_character_info_context" in _runtime:
		_runtime._active_character_info_context.clear()


func _close_settlement_modal() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("close_settlement_modal"):
		_runtime.close_settlement_modal()
	elif "_settlement_command_handler" in _runtime and _runtime._settlement_command_handler != null:
		_runtime._settlement_command_handler.on_settlement_window_closed()


func _close_shop_modal() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("close_shop_modal"):
		_runtime.close_shop_modal()
	elif "_settlement_command_handler" in _runtime and _runtime._settlement_command_handler != null:
		_runtime._settlement_command_handler.on_shop_window_closed()


func _close_forge_modal() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("close_forge_modal"):
		_runtime.close_forge_modal()
	elif "_settlement_command_handler" in _runtime and _runtime._settlement_command_handler != null:
		_runtime._settlement_command_handler.on_forge_window_closed()


func _close_stagecoach_modal() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("close_stagecoach_modal"):
		_runtime.close_stagecoach_modal()
	elif "_settlement_command_handler" in _runtime and _runtime._settlement_command_handler != null:
		_runtime._settlement_command_handler.on_stagecoach_window_closed()


func _close_party_management_modal() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("close_party_management_modal"):
		_runtime.close_party_management_modal()
	elif "_party_command_handler" in _runtime and _runtime._party_command_handler != null:
		_runtime._party_command_handler._on_party_management_window_closed()


func _close_party_warehouse_modal() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("close_party_warehouse_modal"):
		_runtime.close_party_warehouse_modal()
	elif "_warehouse_handler" in _runtime and _runtime._warehouse_handler != null:
		_runtime._warehouse_handler.on_party_warehouse_window_closed()


func _cancel_submap_entry_prompt() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("command_cancel_submap_entry"):
		_runtime.command_cancel_submap_entry()


func _clear_pending_promotion_prompt() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("clear_pending_promotion_prompt"):
		_runtime.clear_pending_promotion_prompt()
	elif "_pending_promotion_prompt" in _runtime:
		_runtime._pending_promotion_prompt.clear()


func _clear_pending_world_promotion_prompt() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("clear_pending_world_promotion_prompt_state"):
		_runtime.clear_pending_world_promotion_prompt_state()
	elif "_pending_world_promotion_prompt" in _runtime:
		_runtime._pending_world_promotion_prompt.clear()


func _submit_battle_promotion_choice(member_id: StringName, profession_id: StringName, selection: Dictionary):
	if not _has_runtime():
		return null
	if _runtime.has_method("submit_battle_promotion_choice"):
		return _runtime.submit_battle_promotion_choice(member_id, profession_id, selection)
	if "_battle_runtime" in _runtime and _runtime._battle_runtime != null:
		return _runtime._battle_runtime.submit_promotion_choice(member_id, profession_id, selection)
	return null


func _apply_battle_batch(batch) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("apply_battle_batch"):
		_runtime.apply_battle_batch(batch)
	elif _runtime.has_method("_apply_battle_batch"):
		_runtime._apply_battle_batch(batch)


func _promote_profession(member_id: StringName, profession_id: StringName, selection: Dictionary):
	if not _has_runtime():
		return null
	if _runtime.has_method("promote_profession"):
		return _runtime.promote_profession(member_id, profession_id, selection)
	if "_character_management" in _runtime and _runtime._character_management != null:
		return _runtime._character_management.promote_profession(member_id, profession_id, selection)
	return null


func _sync_party_state_from_character_management() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("sync_party_state_from_character_management"):
		_runtime.sync_party_state_from_character_management()
	elif "_character_management" in _runtime and _runtime._character_management != null and "_party_state" in _runtime:
		_runtime._party_state = _runtime._character_management.get_party_state()


func _persist_party_state() -> int:
	if not _has_runtime():
		return ERR_UNAVAILABLE
	if _runtime.has_method("persist_party_state"):
		return int(_runtime.persist_party_state())
	return int(_runtime._persist_party_state()) if _runtime.has_method("_persist_party_state") else ERR_UNAVAILABLE


func _build_runtime_promotion_prompt(delta, selection_hint: String) -> Dictionary:
	if not _has_runtime():
		return {}
	if _runtime.has_method("build_runtime_promotion_prompt"):
		return _runtime.build_runtime_promotion_prompt(delta, selection_hint)
	return _runtime._build_promotion_prompt(delta, selection_hint) if _runtime.has_method("_build_promotion_prompt") else {}


func _set_pending_world_promotion_prompt(prompt: Dictionary) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("set_pending_world_promotion_prompt_state"):
		_runtime.set_pending_world_promotion_prompt_state(prompt)
	elif "_pending_world_promotion_prompt" in _runtime:
		_runtime._pending_world_promotion_prompt = prompt.duplicate(true)


func _get_member_display_name(member_id: StringName) -> String:
	if not _has_runtime():
		return String(member_id)
	if _runtime.has_method("get_member_display_name"):
		return _runtime.get_member_display_name(member_id)
	return _runtime._get_member_display_name(member_id) if _runtime.has_method("_get_member_display_name") else String(member_id)


func _clear_active_reward() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("clear_active_reward_state"):
		_runtime.clear_active_reward_state()
	elif "_active_reward" in _runtime:
		_runtime._active_reward = null


func _apply_pending_character_reward_to_party(reward):
	if not _has_runtime():
		return null
	if _runtime.has_method("apply_pending_character_reward_to_party"):
		return _runtime.apply_pending_character_reward_to_party(reward)
	if "_character_management" in _runtime and _runtime._character_management != null:
		return _runtime._character_management.apply_pending_character_reward(reward)
	return null


func _enqueue_character_rewards(reward_variants: Array) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("enqueue_character_rewards"):
		_runtime.enqueue_character_rewards(reward_variants)
	elif "_character_management" in _runtime and _runtime._character_management != null:
		_runtime._character_management.enqueue_pending_character_rewards(reward_variants)
		_sync_party_state_from_character_management()


func _get_party_state():
	if not _has_runtime():
		return null
	if _runtime.has_method("get_party_state"):
		return _runtime.get_party_state()
	return _runtime._party_state if "_party_state" in _runtime else null


func _set_active_reward(reward) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("set_active_reward_state"):
		_runtime.set_active_reward_state(reward)
	elif "_active_reward" in _runtime:
		_runtime._active_reward = reward
