class_name GameRuntimePartyCommandHandler
extends RefCounted

const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

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


func command_open_party() -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	if _get_generation_config() == null:
		return _command_error("世界地图尚未初始化。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能打开队伍管理。")
	if _is_modal_window_open():
		return _command_error("当前有窗口打开，不能打开队伍管理。")
	_open_party_management_window()
	return _command_ok()


func command_select_party_member(member_id: StringName) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	var party_state = _get_party_state()
	if party_state == null:
		return _command_error("当前不存在队伍数据。")
	if party_state.get_member_state(member_id) == null:
		return _command_error("未找到队伍成员 %s。" % String(member_id))
	if _get_active_modal_id() == "":
		_set_active_modal_id("party")
	_set_party_selected_member_id(member_id)
	_update_status("已选中队员 %s。" % _get_member_display_name(member_id))
	return _command_ok()


func command_set_party_leader(member_id: StringName) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	var party_state = _get_party_state()
	if party_state == null:
		return _command_error("当前不存在队伍数据。")
	if not party_state.active_member_ids.has(member_id):
		return _command_error("只有上阵成员才能成为队长。")
	_on_party_leader_change_requested(member_id)
	_set_party_selected_member_id(member_id)
	return _command_ok()


func command_move_member_to_active(member_id: StringName) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	var party_state = _get_party_state()
	if party_state == null:
		return _command_error("当前不存在队伍数据。")
	if not party_state.reserve_member_ids.has(member_id):
		return _command_error("%s 当前不在替补列表中。" % _get_member_display_name(member_id))
	if party_state.active_member_ids.size() >= 4:
		return _command_error("上阵人数已达到上限。")
	var active_member_ids: Array[StringName] = ProgressionDataUtils.to_string_name_array(party_state.active_member_ids)
	var reserve_member_ids: Array[StringName] = ProgressionDataUtils.to_string_name_array(party_state.reserve_member_ids)
	reserve_member_ids.erase(member_id)
	active_member_ids.append(member_id)
	_on_party_roster_change_requested(active_member_ids, reserve_member_ids)
	_set_party_selected_member_id(member_id)
	return _command_ok()


func command_move_member_to_reserve(member_id: StringName) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	var party_state = _get_party_state()
	if party_state == null:
		return _command_error("当前不存在队伍数据。")
	if not party_state.active_member_ids.has(member_id):
		return _command_error("%s 当前不在上阵列表中。" % _get_member_display_name(member_id))
	if party_state.active_member_ids.size() <= 1:
		return _command_error("队伍至少需要保留一名上阵成员。")
	var active_member_ids: Array[StringName] = ProgressionDataUtils.to_string_name_array(party_state.active_member_ids)
	var reserve_member_ids: Array[StringName] = ProgressionDataUtils.to_string_name_array(party_state.reserve_member_ids)
	active_member_ids.erase(member_id)
	reserve_member_ids.append(member_id)
	_on_party_roster_change_requested(active_member_ids, reserve_member_ids)
	_set_party_selected_member_id(member_id)
	return _command_ok()


func command_party_equip_item(member_id: StringName, item_id: StringName, slot_id: StringName = &"") -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	if _get_party_state() == null:
		return _command_error("当前不存在队伍数据。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能调整装备。")
	var active_modal_id := _get_active_modal_id()
	if active_modal_id == "reward" or active_modal_id == "promotion" or active_modal_id == "settlement" or active_modal_id == "character_info":
		return _command_error("当前窗口会阻止装备切换。")

	var result: Dictionary = _equip_party_item(member_id, item_id, slot_id)
	if not bool(result.get("success", false)):
		return _command_error(_build_equipment_error_message(result, true))

	_set_party_selected_member_id(member_id)
	var success_message := "已为 %s 装备 %s（%s）。" % [
		_get_member_display_name(member_id),
		_get_item_display_name(ProgressionDataUtils.to_string_name(result.get("item_id", ""))),
		String(result.get("slot_label", "")),
	]
	var previous_item_id := ProgressionDataUtils.to_string_name(result.get("previous_item_id", ""))
	if previous_item_id != &"":
		success_message = "已为 %s 装备 %s（%s），并卸下 %s。" % [
			_get_member_display_name(member_id),
			_get_item_display_name(ProgressionDataUtils.to_string_name(result.get("item_id", ""))),
			String(result.get("slot_label", "")),
			_get_item_display_name(previous_item_id),
		]

	var persist_error := _persist_party_state()
	if persist_error == OK:
		_update_status(success_message)
	else:
		_update_status("%s 但队伍状态持久化失败。" % success_message)
	return _command_ok()


func command_party_unequip_item(member_id: StringName, slot_id: StringName) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	if _get_party_state() == null:
		return _command_error("当前不存在队伍数据。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能调整装备。")
	var active_modal_id := _get_active_modal_id()
	if active_modal_id == "reward" or active_modal_id == "promotion" or active_modal_id == "settlement" or active_modal_id == "character_info":
		return _command_error("当前窗口会阻止装备切换。")

	var result: Dictionary = _unequip_party_item(member_id, slot_id)
	if not bool(result.get("success", false)):
		return _command_error(_build_equipment_error_message(result, false))

	_set_party_selected_member_id(member_id)
	var success_message := "已从 %s 的 %s 卸下 %s。" % [
		_get_member_display_name(member_id),
		String(result.get("slot_label", "")),
		_get_item_display_name(ProgressionDataUtils.to_string_name(result.get("item_id", ""))),
	]
	var persist_error := _persist_party_state()
	if persist_error == OK:
		_update_status(success_message)
	else:
		_update_status("%s 但队伍状态持久化失败。" % success_message)
	return _command_ok()


func apply_party_roster(active_member_ids: Array[StringName], reserve_member_ids: Array[StringName]) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	if _get_party_state() == null:
		return _command_error("当前不存在队伍数据。")
	_on_party_roster_change_requested(active_member_ids, reserve_member_ids)
	return _command_ok()


func _open_party_management_window() -> void:
	if not _has_runtime():
		return
	if _is_battle_active():
		return
	var party_state = _get_party_state()
	_set_active_modal_id("party")
	if _get_party_selected_member_id() == &"" and party_state != null and not party_state.active_member_ids.is_empty():
		_set_party_selected_member_id(party_state.active_member_ids[0])
	_update_status("已打开队伍管理窗口。")


func _on_party_leader_change_requested(member_id: StringName) -> void:
	var party_state = _get_party_state()
	if not _has_runtime() or party_state == null:
		return
	party_state.leader_member_id = member_id
	_apply_party_state_to_runtime("队长已切换为 %s。" % String(member_id))


func _on_party_roster_change_requested(active_member_ids: Array[StringName], reserve_member_ids: Array[StringName]) -> void:
	var party_state = _get_party_state()
	if not _has_runtime() or party_state == null:
		return
	party_state.active_member_ids = active_member_ids.duplicate()
	party_state.reserve_member_ids = reserve_member_ids.duplicate()
	if not party_state.active_member_ids.has(party_state.leader_member_id) and not party_state.active_member_ids.is_empty():
		party_state.leader_member_id = party_state.active_member_ids[0]
	_apply_party_state_to_runtime("队伍编成已更新。")


func _on_party_management_window_closed() -> void:
	if not _has_runtime():
		return
	_set_active_modal_id("")
	_update_status("已关闭队伍管理窗口。")
	_present_pending_reward_if_ready()


func _on_party_management_warehouse_requested() -> void:
	if not _has_runtime():
		return
	_set_active_modal_id("")
	_open_party_warehouse_window("队伍管理")
	_update_status("已从队伍管理打开共享仓库。")


func _apply_party_state_to_runtime(success_message: String) -> void:
	if not _has_runtime():
		return
	_sync_character_management_party_state()
	var persist_error := _persist_party_state()
	if persist_error == OK:
		_update_status(success_message)
	else:
		_update_status("%s 但队伍状态持久化失败。" % success_message)


func _persist_party_state() -> int:
	if not _has_runtime():
		return ERR_UNAVAILABLE
	if _runtime.has_method("persist_party_state"):
		return int(_runtime.persist_party_state())
	if _get_game_session() == null:
		return ERR_UNAVAILABLE
	var party_state = _get_party_state()
	var game_session = _get_game_session()
	var persist_error: int = int(game_session.set_party_state(party_state))
	_set_party_state(game_session.get_party_state())
	_sync_character_management_party_state()
	if _get_party_warehouse_service() != null:
		_get_party_warehouse_service().setup(_get_party_state(), game_session.get_item_defs())
	if _get_party_item_use_service() != null:
		_get_party_item_use_service().setup(
			_get_party_state(),
			game_session.get_item_defs(),
			game_session.get_skill_defs(),
			_get_party_warehouse_service(),
			_get_character_management()
		)
	if _get_party_equipment_service() != null:
		_get_party_equipment_service().setup(_get_party_state(), game_session.get_item_defs(), _get_party_warehouse_service())
	_refresh_fog()
	return persist_error


func _get_item_display_name(item_id: StringName) -> String:
	if _has_runtime() and _runtime.has_method("get_item_display_name"):
		return _runtime.get_item_display_name(item_id)
	var warehouse_service = _get_party_warehouse_service()
	if warehouse_service == null:
		return String(item_id)
	var item_def = warehouse_service.get_item_def(item_id)
	if item_def != null and not item_def.display_name.is_empty():
		return item_def.display_name
	return String(item_id)


func _get_member_display_name(member_id: StringName) -> String:
	if _has_runtime() and _runtime.has_method("get_member_display_name"):
		return _runtime.get_member_display_name(member_id)
	var party_state = _get_party_state()
	if party_state == null:
		return String(member_id)
	var member_state = party_state.get_member_state(member_id)
	if member_state != null and not String(member_state.display_name).is_empty():
		return String(member_state.display_name)
	return String(member_id)


func _get_skill_display_name(skill_id: StringName) -> String:
	var skill_def: SkillDef = null
	var game_session = _get_game_session()
	if game_session != null:
		skill_def = game_session.get_skill_defs().get(skill_id) as SkillDef
	if skill_def != null and not skill_def.display_name.is_empty():
		return skill_def.display_name
	return String(skill_id)


func _build_equipment_error_message(result: Dictionary, is_equip_action: bool) -> String:
	var member_id := ProgressionDataUtils.to_string_name(result.get("member_id", ""))
	var slot_label := String(result.get("slot_label", "装备槽"))
	var item_id := ProgressionDataUtils.to_string_name(result.get("item_id", ""))
	match String(result.get("error_code", "")):
		"member_not_found":
			return "未找到队伍成员 %s。" % String(member_id)
		"item_not_found":
			return "未找到物品定义 %s。" % String(item_id)
		"item_not_equipment":
			return "%s 不是可装备物品。" % _get_item_display_name(item_id)
		"slot_unresolved":
			return "%s 当前没有可用装备槽。" % _get_item_display_name(item_id)
		"slot_not_allowed":
			return "%s 不能装备到 %s。" % [_get_item_display_name(item_id), slot_label]
		"warehouse_missing_item":
			return "共享仓库中没有可用于装备的 %s。" % _get_item_display_name(item_id)
		"warehouse_blocked_swap":
			return "%s 当前没有空间接回被替换下来的装备。" % slot_label
		"slot_invalid":
			return "装备槽无效。"
		"slot_empty":
			return "%s 当前没有已装备物品。" % slot_label
		"warehouse_full":
			return "共享仓库空间不足，无法卸下 %s。" % _get_item_display_name(item_id)
		"missing_profession":
			return "%s 当前职业不满足 %s 的装备要求。" % [_get_member_display_name(member_id), _get_item_display_name(item_id)]
		"body_size_too_small":
			return "%s 体型过小，无法装备 %s。" % [_get_member_display_name(member_id), _get_item_display_name(item_id)]
		"body_size_too_large":
			return "%s 体型过大，无法装备 %s。" % [_get_member_display_name(member_id), _get_item_display_name(item_id)]
		"requirement_failed":
			return "%s 不满足装备要求。" % _get_item_display_name(item_id)
		_:
			return "装备操作失败。" if is_equip_action else "卸装操作失败。"


func _has_runtime() -> bool:
	return _runtime != null


func _command_ok(message: String = "") -> Dictionary:
	var resolved_message := message
	if _has_runtime() and _runtime.has_method("build_command_ok"):
		return _runtime.build_command_ok(resolved_message)
	if resolved_message.is_empty() and _has_runtime():
		resolved_message = _get_status_text()
	return {
		"ok": true,
		"message": resolved_message,
		"battle_refresh_mode": "",
	}


func _command_error(message: String) -> Dictionary:
	if _has_runtime() and _runtime.has_method("build_command_error"):
		return _runtime.build_command_error(message)
	if _has_runtime() and not message.is_empty():
		_update_status(message)
	return {
		"ok": false,
		"message": message,
	}


func _runtime_unavailable_error() -> Dictionary:
	return {
		"ok": false,
		"message": RUNTIME_UNAVAILABLE_MESSAGE,
	}


func _get_generation_config():
	if not _has_runtime():
		return null
	if _runtime.has_method("get_generation_config"):
		return _runtime.get_generation_config()
	return _runtime._generation_config if "_generation_config" in _runtime else null


func _is_battle_active() -> bool:
	if not _has_runtime():
		return false
	if _runtime.has_method("is_battle_active"):
		return _runtime.is_battle_active()
	return _runtime._is_battle_active() if _runtime.has_method("_is_battle_active") else false


func _is_modal_window_open() -> bool:
	if not _has_runtime():
		return false
	if _runtime.has_method("is_modal_window_open"):
		return _runtime.is_modal_window_open()
	return _runtime._is_modal_window_open() if _runtime.has_method("_is_modal_window_open") else false


func _get_party_state():
	if not _has_runtime():
		return null
	if _runtime.has_method("get_party_state"):
		return _runtime.get_party_state()
	return _runtime._party_state if "_party_state" in _runtime else null


func _set_party_state(party_state) -> void:
	if not _has_runtime():
		return
	if "_party_state" in _runtime:
		_runtime._party_state = party_state


func _get_active_modal_id() -> String:
	if not _has_runtime():
		return ""
	if _runtime.has_method("get_active_modal_id"):
		return _runtime.get_active_modal_id()
	return String(_runtime._active_modal_id) if "_active_modal_id" in _runtime else ""


func _set_active_modal_id(modal_id: String) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("set_runtime_active_modal_id"):
		_runtime.set_runtime_active_modal_id(modal_id)
	elif "_active_modal_id" in _runtime:
		_runtime._active_modal_id = modal_id


func _get_party_selected_member_id() -> StringName:
	if not _has_runtime():
		return &""
	if _runtime.has_method("get_party_selected_member_id"):
		return _runtime.get_party_selected_member_id()
	return _runtime._party_selected_member_id if "_party_selected_member_id" in _runtime else &""


func _set_party_selected_member_id(member_id: StringName) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("set_party_selected_member_id"):
		_runtime.set_party_selected_member_id(member_id)
	elif "_party_selected_member_id" in _runtime:
		_runtime._party_selected_member_id = member_id


func _equip_party_item(member_id: StringName, item_id: StringName, slot_id: StringName) -> Dictionary:
	if not _has_runtime():
		return {}
	if _runtime.has_method("equip_party_item"):
		return _runtime.equip_party_item(member_id, item_id, slot_id)
	var party_equipment_service = _get_party_equipment_service()
	return party_equipment_service.equip_item(member_id, item_id, slot_id) if party_equipment_service != null else {}


func _unequip_party_item(member_id: StringName, slot_id: StringName) -> Dictionary:
	if not _has_runtime():
		return {}
	if _runtime.has_method("unequip_party_item"):
		return _runtime.unequip_party_item(member_id, slot_id)
	var party_equipment_service = _get_party_equipment_service()
	return party_equipment_service.unequip_item(member_id, slot_id) if party_equipment_service != null else {}


func _sync_character_management_party_state() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("sync_character_management_party_state"):
		_runtime.sync_character_management_party_state()
		return
	var character_management = _get_character_management()
	if character_management != null:
		character_management.set_party_state(_get_party_state())


func _open_party_warehouse_window(entry_label: String) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("open_party_warehouse_window"):
		_runtime.open_party_warehouse_window(entry_label)
		return
	if _get_warehouse_handler() != null:
		_get_warehouse_handler().open_party_warehouse_window(entry_label)


func _present_pending_reward_if_ready() -> bool:
	if not _has_runtime():
		return false
	if _runtime.has_method("present_pending_reward_if_ready"):
		return _runtime.present_pending_reward_if_ready()
	return _runtime._present_pending_reward_if_ready() if _runtime.has_method("_present_pending_reward_if_ready") else false


func _update_status(message: String) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("update_status"):
		_runtime.update_status(message)
	elif _runtime.has_method("_update_status"):
		_runtime._update_status(message)


func _get_status_text() -> String:
	if not _has_runtime():
		return ""
	if _runtime.has_method("get_status_text"):
		return _runtime.get_status_text()
	return String(_runtime._current_status_message) if "_current_status_message" in _runtime else ""


func _get_game_session():
	if not _has_runtime():
		return null
	if _runtime.has_method("get_game_session"):
		return _runtime.get_game_session()
	return _runtime._game_session if "_game_session" in _runtime else null


func _get_party_warehouse_service():
	if not _has_runtime():
		return null
	return _runtime._party_warehouse_service if "_party_warehouse_service" in _runtime else null


func _get_party_item_use_service():
	if not _has_runtime():
		return null
	return _runtime._party_item_use_service if "_party_item_use_service" in _runtime else null


func _get_party_equipment_service():
	if not _has_runtime():
		return null
	return _runtime._party_equipment_service if "_party_equipment_service" in _runtime else null


func _get_character_management():
	if not _has_runtime():
		return null
	return _runtime._character_management if "_character_management" in _runtime else null


func _get_warehouse_handler():
	if not _has_runtime():
		return null
	return _runtime._warehouse_handler if "_warehouse_handler" in _runtime else null


func _refresh_fog() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("_refresh_fog"):
		_runtime._refresh_fog()
