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
	open_party_management_window()
	return _command_ok()


func command_select_party_member(member_id: StringName) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	var party_state = _get_party_state()
	if party_state == null:
		return _command_error("当前不存在队伍数据。")
	if party_state.get_member_state(member_id) == null:
		return _command_error("未找到队伍成员 %s。" % String(member_id))
	if not party_state.active_member_ids.has(member_id) and not party_state.reserve_member_ids.has(member_id):
		return _command_error("%s 当前不在队伍编成中。" % _get_member_display_name(member_id))
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
	on_party_leader_change_requested(member_id)
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
	on_party_roster_change_requested(active_member_ids, reserve_member_ids)
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
	if member_id == _get_main_character_member_id(party_state):
		return _command_error("主角必须保持上阵，不能移至替补。")
	if party_state.active_member_ids.size() <= 1:
		return _command_error("队伍至少需要保留一名上阵成员。")
	var active_member_ids: Array[StringName] = ProgressionDataUtils.to_string_name_array(party_state.active_member_ids)
	var reserve_member_ids: Array[StringName] = ProgressionDataUtils.to_string_name_array(party_state.reserve_member_ids)
	active_member_ids.erase(member_id)
	reserve_member_ids.append(member_id)
	on_party_roster_change_requested(active_member_ids, reserve_member_ids)
	_set_party_selected_member_id(member_id)
	return _command_ok()


func command_party_equip_item(
	member_id: StringName,
	item_id: StringName,
	slot_id: StringName = &"",
	instance_id: StringName = &""
) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	if _get_party_state() == null:
		return _command_error("当前不存在队伍数据。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能调整装备。")
	var active_modal_id := _get_active_modal_id()
	if active_modal_id == "reward" or active_modal_id == "promotion" or active_modal_id == "settlement" or active_modal_id == "character_info":
		return _command_error("当前窗口会阻止装备切换。")

	var result: Dictionary = _equip_party_item(member_id, item_id, slot_id, instance_id)
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
	var party_state = _get_party_state()
	if party_state == null:
		return _command_error("当前不存在队伍数据。")
	var roster_error := _validate_main_character_roster(active_member_ids, reserve_member_ids, party_state)
	if not roster_error.is_empty():
		return _command_error(roster_error)
	on_party_roster_change_requested(active_member_ids, reserve_member_ids)
	return _command_ok()


func open_party_management_window() -> void:
	if not _has_runtime():
		return
	if _is_battle_active():
		return
	var party_state = _get_party_state()
	_set_active_modal_id("party")
	if _get_party_selected_member_id() == &"" and party_state != null and not party_state.active_member_ids.is_empty():
		_set_party_selected_member_id(party_state.active_member_ids[0])
	_update_status("已打开队伍管理窗口。")


func on_party_leader_change_requested(member_id: StringName) -> void:
	var party_state = _get_party_state()
	if not _has_runtime() or party_state == null:
		return
	party_state.leader_member_id = member_id
	apply_party_state_to_runtime("队长已切换为 %s。" % String(member_id))


func on_party_roster_change_requested(active_member_ids: Array[StringName], reserve_member_ids: Array[StringName]) -> void:
	var party_state = _get_party_state()
	if not _has_runtime() or party_state == null:
		return
	var roster_error := _validate_main_character_roster(active_member_ids, reserve_member_ids, party_state)
	if not roster_error.is_empty():
		_update_status(roster_error)
		return
	party_state.active_member_ids = active_member_ids.duplicate()
	party_state.reserve_member_ids = reserve_member_ids.duplicate()
	if not party_state.active_member_ids.has(party_state.leader_member_id) and not party_state.active_member_ids.is_empty():
		party_state.leader_member_id = party_state.active_member_ids[0]
	apply_party_state_to_runtime("队伍编成已更新。")


func on_party_management_window_closed() -> void:
	if not _has_runtime():
		return
	_set_active_modal_id("")
	_update_status("已关闭队伍管理窗口。")
	_present_pending_reward_if_ready()


func on_party_management_warehouse_requested() -> void:
	if not _has_runtime():
		return
	_set_active_modal_id("")
	_open_party_warehouse_window("队伍管理")
	_update_status("已从队伍管理打开共享仓库。")


func apply_party_state_to_runtime(success_message: String) -> void:
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
	return int(_runtime.persist_party_state())


func _get_item_display_name(item_id: StringName) -> String:
	if not _has_runtime():
		return String(item_id)
	return _runtime.get_item_display_name(item_id)


func _get_main_character_member_id(party_state) -> StringName:
	if party_state == null or not party_state.has_method("get_resolved_main_character_member_id"):
		return &""
	var member_id: StringName = party_state.get_resolved_main_character_member_id()
	if member_id == &"":
		return &""
	if party_state.has_method("is_member_dead") and bool(party_state.is_member_dead(member_id)):
		return &""
	return member_id


func _validate_main_character_roster(
	active_member_ids: Array[StringName],
	reserve_member_ids: Array[StringName],
	party_state
) -> String:
	var member_id := _get_main_character_member_id(party_state)
	if member_id == &"":
		return ""
	if reserve_member_ids.has(member_id) or not active_member_ids.has(member_id):
		return "主角必须保持上阵，不能移至替补。"
	return ""


func _get_member_display_name(member_id: StringName) -> String:
	if not _has_runtime():
		return String(member_id)
	return _runtime.get_member_display_name(member_id)


func _get_skill_display_name(skill_id: StringName) -> String:
	var game_session = _get_game_session()
	if game_session == null:
		return String(skill_id)
	var skill_def: SkillDef = game_session.get_skill_defs().get(skill_id) as SkillDef
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
		"warehouse_missing_instance":
			return "共享仓库中没有指定的 %s 装备实例。" % _get_item_display_name(item_id)
		"equipment_instance_id_required":
			return "共享仓库中有多件 %s，请指定装备实例。" % _get_item_display_name(item_id)
		"equipment_instance_item_mismatch":
			return "指定装备实例不属于 %s。" % _get_item_display_name(item_id)
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
	if not _has_runtime():
		return {"ok": true, "message": message, "battle_refresh_mode": ""}
	return _runtime.build_command_ok(message)


func _command_error(message: String) -> Dictionary:
	if not _has_runtime():
		return {"ok": false, "message": message}
	return _runtime.build_command_error(message)


func _runtime_unavailable_error() -> Dictionary:
	return {"ok": false, "message": RUNTIME_UNAVAILABLE_MESSAGE}


func _get_generation_config():
	if not _has_runtime():
		return null
	return _runtime.get_generation_config()


func _is_battle_active() -> bool:
	if not _has_runtime():
		return false
	return _runtime.is_battle_active()


func _is_modal_window_open() -> bool:
	if not _has_runtime():
		return false
	return _runtime.is_modal_window_open()


func _get_party_state():
	if not _has_runtime():
		return null
	return _runtime.get_party_state()


func _set_party_state(party_state) -> void:
	if _has_runtime():
		_runtime.set_party_state(party_state)


func _get_active_modal_id() -> String:
	if not _has_runtime():
		return ""
	return _runtime.get_active_modal_id()


func _set_active_modal_id(modal_id: String) -> void:
	if _has_runtime():
		_runtime.set_runtime_active_modal_id(modal_id)


func _get_party_selected_member_id() -> StringName:
	if not _has_runtime():
		return &""
	return _runtime.get_party_selected_member_id()


func _set_party_selected_member_id(member_id: StringName) -> void:
	if _has_runtime():
		_runtime.set_party_selected_member_id(member_id)


func _equip_party_item(member_id: StringName, item_id: StringName, slot_id: StringName, instance_id: StringName = &"") -> Dictionary:
	if not _has_runtime():
		return {}
	return _runtime.equip_party_item(member_id, item_id, slot_id, instance_id)


func _unequip_party_item(member_id: StringName, slot_id: StringName) -> Dictionary:
	if not _has_runtime():
		return {}
	return _runtime.unequip_party_item(member_id, slot_id)


func _sync_character_management_party_state() -> void:
	if _has_runtime():
		_runtime.sync_character_management_party_state()


func _open_party_warehouse_window(entry_label: String) -> void:
	if _has_runtime():
		_runtime.open_party_warehouse_window(entry_label)


func _present_pending_reward_if_ready() -> bool:
	if not _has_runtime():
		return false
	return _runtime.present_pending_reward_if_ready()


func _update_status(message: String) -> void:
	if _has_runtime():
		_runtime.update_status(message)


func _get_status_text() -> String:
	if not _has_runtime():
		return ""
	return _runtime.get_status_text()


func _get_game_session():
	if not _has_runtime():
		return null
	return _runtime.get_game_session()


func _get_party_warehouse_service():
	if not _has_runtime():
		return null
	return _runtime.get_party_warehouse_service()


func _get_party_item_use_service():
	if not _has_runtime():
		return null
	return _runtime.get_party_item_use_service()


func _get_party_equipment_service():
	if not _has_runtime():
		return null
	return _runtime.get_party_equipment_service()


func _get_character_management():
	if not _has_runtime():
		return null
	return _runtime.get_character_management()


func _get_warehouse_handler():
	if not _has_runtime():
		return null
	return _runtime.get_warehouse_handler()


func _refresh_fog() -> void:
	if _has_runtime():
		_runtime.refresh_fog()
