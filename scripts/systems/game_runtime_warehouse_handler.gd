class_name GameRuntimeWarehouseHandler
extends RefCounted

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


func get_warehouse_window_data() -> Dictionary:
	if not _has_runtime():
		return {}
	if _get_party_state() == null or _get_party_warehouse_service() == null:
		return {}
	return _build_warehouse_window_data()


func command_open_party_warehouse() -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	if _get_party_state() == null:
		return _command_error("当前不存在队伍数据。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能打开共享仓库。")

	if _get_active_modal_id() == "settlement":
		open_party_warehouse_window("据点服务")
		_update_status("已从据点窗口打开共享仓库。")
	else:
		open_party_warehouse_window("队伍管理")
		_update_status("已打开共享仓库。")
	return _command_ok()


func command_discard_one(item_id: StringName) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	if _get_active_modal_id() != "warehouse":
		return _command_error("共享仓库当前未打开。")
	if _get_party_warehouse_service() == null:
		return _command_error("共享仓库服务尚未准备完成。")
	on_party_warehouse_discard_one_requested(item_id)
	return _command_ok()


func command_discard_all(item_id: StringName) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	if _get_active_modal_id() != "warehouse":
		return _command_error("共享仓库当前未打开。")
	if _get_party_warehouse_service() == null:
		return _command_error("共享仓库服务尚未准备完成。")
	on_party_warehouse_discard_all_requested(item_id)
	return _command_ok()


func command_use_item(item_id: StringName, member_id: StringName = &"") -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	if _get_active_modal_id() != "warehouse":
		return _command_error("共享仓库当前未打开。")
	var resolved_member_id := _resolve_warehouse_target_member_id(member_id)
	if resolved_member_id == &"":
		return _command_error("当前没有可使用技能书的目标角色。")
	var use_result := on_party_warehouse_use_requested(item_id, resolved_member_id)
	if not bool(use_result.get("success", false)):
		return _command_error(String(use_result.get("message", "当前无法使用该物品。")))
	return _command_ok()


func command_add_item(item_id: StringName, quantity: int) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	if _get_party_state() == null:
		return _command_error("当前不存在队伍数据。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能直接改动共享仓库。")
	if quantity <= 0:
		return _command_error("加入数量必须大于 0。")
	if _get_party_warehouse_service() == null:
		return _command_error("共享仓库服务尚未准备完成。")

	var normalized_item_id := ProgressionDataUtils.to_string_name(item_id)
	var result: Dictionary = _get_party_warehouse_service().add_item(normalized_item_id, quantity)
	var added_quantity := int(result.get("added_quantity", 0))
	if added_quantity <= 0:
		return _command_error("%s 当前无法加入共享仓库。" % _get_item_display_name(normalized_item_id))

	var success_message := "已向共享仓库加入 %d 件 %s。" % [
		added_quantity,
		_get_item_display_name(normalized_item_id),
	]
	var remaining_quantity := int(result.get("remaining_quantity", 0))
	if remaining_quantity > 0:
		success_message = "已向共享仓库加入 %d 件 %s，仍有 %d 件未能放入。" % [
			added_quantity,
			_get_item_display_name(normalized_item_id),
			remaining_quantity,
	]

	var persist_error := int(_persist_party_state())
	if persist_error == OK:
		_update_status(success_message)
	else:
		_update_status("%s 但队伍状态持久化失败。" % success_message)
	return _command_ok()


func open_party_warehouse_window(entry_label: String) -> void:
	if not _has_runtime():
		return
	if _is_battle_active():
		return

	_set_active_modal_id("warehouse")
	_set_active_warehouse_entry_label(entry_label if not entry_label.is_empty() else "共享入口")

	var party_warehouse_service = _get_party_warehouse_service()
	var game_session = _get_game_session()
	if party_warehouse_service != null:
		var item_defs: Dictionary = {}
		if game_session != null and game_session.has_method("get_item_defs"):
			item_defs = game_session.get_item_defs()
		party_warehouse_service.setup(_get_party_state(), item_defs)
	_refresh_party_warehouse_window()


func on_party_warehouse_discard_one_requested(item_id: StringName) -> void:
	var party_warehouse_service = _get_party_warehouse_service()
	if not _has_runtime() or party_warehouse_service == null:
		return

	var item_name := _get_item_display_name(item_id)
	var result: Dictionary = party_warehouse_service.remove_item(item_id, 1)
	if int(result.get("removed_quantity", 0)) <= 0:
		_refresh_party_warehouse_window()
		_update_status("%s 当前没有可丢弃的库存。" % item_name)
		return

	var persist_error := int(_persist_party_state())
	if persist_error == OK:
		_update_status("已从共享仓库丢弃 1 件 %s。" % item_name)
	else:
		_update_status("已从共享仓库丢弃 1 件 %s，但队伍状态持久化失败。" % item_name)


func on_party_warehouse_discard_all_requested(item_id: StringName) -> void:
	var party_warehouse_service = _get_party_warehouse_service()
	if not _has_runtime() or party_warehouse_service == null:
		return

	var item_name := _get_item_display_name(item_id)
	var total_quantity: int = party_warehouse_service.count_item(item_id)
	if total_quantity <= 0:
		_refresh_party_warehouse_window()
		_update_status("%s 当前没有可丢弃的库存。" % item_name)
		return

	var result: Dictionary = party_warehouse_service.remove_item(item_id, total_quantity)
	var removed_quantity := int(result.get("removed_quantity", 0))
	if removed_quantity <= 0:
		_refresh_party_warehouse_window()
		_update_status("%s 当前没有可丢弃的库存。" % item_name)
		return

	var persist_error := int(_persist_party_state())
	if persist_error == OK:
		_update_status("已从共享仓库丢弃全部 %s，共 %d 件。" % [item_name, removed_quantity])
	else:
		_update_status("已从共享仓库丢弃全部 %s，但队伍状态持久化失败。" % item_name)


func on_party_warehouse_use_requested(item_id: StringName, member_id: StringName) -> Dictionary:
	if not _has_runtime():
		return {
			"success": false,
			"reason": &"service_unavailable",
			"item_id": String(ProgressionDataUtils.to_string_name(item_id)),
			"member_id": String(ProgressionDataUtils.to_string_name(member_id)),
			"skill_id": StringName(""),
			"consumed_quantity": 0,
			"message": RUNTIME_UNAVAILABLE_MESSAGE,
		}

	var resolved_member_id := _resolve_warehouse_target_member_id(member_id)
	if resolved_member_id == &"":
		var missing_member_result := {
			"success": false,
			"reason": &"missing_member",
			"item_id": String(ProgressionDataUtils.to_string_name(item_id)),
			"member_id": StringName(""),
			"skill_id": StringName(""),
			"consumed_quantity": 0,
		}
		missing_member_result["message"] = _build_warehouse_use_failure_message(missing_member_result)
		_refresh_party_warehouse_window()
		_update_status(String(missing_member_result.get("message", "")))
		return missing_member_result

	var party_item_use_service = _get_party_item_use_service()
	if party_item_use_service == null:
		var unavailable_result := {
			"success": false,
			"reason": &"service_unavailable",
			"item_id": String(ProgressionDataUtils.to_string_name(item_id)),
			"member_id": String(resolved_member_id),
			"skill_id": StringName(""),
			"consumed_quantity": 0,
		}
		unavailable_result["message"] = _build_warehouse_use_failure_message(unavailable_result)
		_refresh_party_warehouse_window()
		_update_status(String(unavailable_result.get("message", "")))
		return unavailable_result

	var use_result: Dictionary = party_item_use_service.use_item(item_id, resolved_member_id)
	if not bool(use_result.get("success", false)):
		var failure_message := _build_warehouse_use_failure_message(use_result)
		use_result["message"] = failure_message
		_refresh_party_warehouse_window()
		_update_status(failure_message)
		return use_result

	_set_party_selected_member_id(resolved_member_id)
	var item_name := _get_item_display_name(item_id)
	var skill_name := _get_skill_display_name(ProgressionDataUtils.to_string_name(use_result.get("skill_id", "")))
	var member_name := _get_member_display_name(resolved_member_id)
	var persist_error := int(_persist_party_state())
	if persist_error == OK:
		use_result["message"] = "已让 %s 使用 %s，学会 %s。" % [member_name, item_name, skill_name]
	else:
		use_result["message"] = "已让 %s 使用 %s，学会 %s，但队伍状态持久化失败。" % [
			member_name,
			item_name,
			skill_name,
		]
	_update_status(String(use_result.get("message", "")))
	return use_result


func on_party_warehouse_window_closed() -> void:
	if not _has_runtime():
		return

	_set_active_modal_id("")
	_set_active_warehouse_entry_label("")
	_update_status("已关闭共享仓库。")
	_present_pending_reward_if_ready()


func _refresh_party_warehouse_window() -> void:
	if not _has_runtime():
		return
	var window = _runtime.party_warehouse_window
	if window == null or not is_instance_valid(window) or not window.visible:
		return
	if window.has_method("set_window_data"):
		window.set_window_data(get_warehouse_window_data())


func _resolve_warehouse_target_member_id(preferred_member_id: StringName = &"") -> StringName:
	var party_state = _get_party_state()
	if not _has_runtime() or party_state == null:
		return &""
	var normalized_member_id := ProgressionDataUtils.to_string_name(preferred_member_id)
	if normalized_member_id != &"" and party_state.get_member_state(normalized_member_id) != null:
		return normalized_member_id
	var selected_member_id := _get_party_selected_member_id()
	if selected_member_id != &"" and party_state.get_member_state(selected_member_id) != null:
		return selected_member_id
	if party_state.leader_member_id != &"" and party_state.get_member_state(party_state.leader_member_id) != null:
		return party_state.leader_member_id
	for member_id in party_state.active_member_ids:
		if party_state.get_member_state(member_id) != null:
			return member_id
	for member_id in party_state.reserve_member_ids:
		if party_state.get_member_state(member_id) != null:
			return member_id
	return &""


func _build_warehouse_target_member_entries() -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	var seen_member_ids: Dictionary = {}
	var party_state = _get_party_state()
	if not _has_runtime() or party_state == null:
		return entries

	for member_id in party_state.active_member_ids:
		if member_id == &"" or seen_member_ids.has(member_id):
			continue
		if party_state.get_member_state(member_id) == null:
			continue
		seen_member_ids[member_id] = true
		entries.append({
			"member_id": String(member_id),
			"display_name": _get_member_display_name(member_id),
			"roster_role": "active",
		})

	for member_id in party_state.reserve_member_ids:
		if member_id == &"" or seen_member_ids.has(member_id):
			continue
		if party_state.get_member_state(member_id) == null:
			continue
		seen_member_ids[member_id] = true
		entries.append({
			"member_id": String(member_id),
			"display_name": _get_member_display_name(member_id),
			"roster_role": "reserve",
		})
	return entries


func _build_warehouse_use_failure_message(use_result: Dictionary) -> String:
	var item_id := ProgressionDataUtils.to_string_name(use_result.get("item_id", ""))
	var member_id := ProgressionDataUtils.to_string_name(use_result.get("member_id", ""))
	var reason := ProgressionDataUtils.to_string_name(use_result.get("reason", ""))
	var item_name := _get_item_display_name(item_id)
	var member_name := _get_member_display_name(member_id)
	match reason:
		&"missing_item_def":
			return "%s 的物品定义缺失，当前无法使用。" % item_name
		&"item_not_usable":
			return "%s 当前不是可使用的技能书。" % item_name
		&"missing_member":
			return "当前找不到可使用 %s 的目标角色。" % item_name
		&"missing_inventory":
			return "%s 当前没有可使用的库存。" % item_name
		&"missing_skill_def":
			return "%s 对应的技能定义缺失，当前无法使用。" % item_name
		&"learn_failed":
			return "%s 当前无法让 %s 学会，可能已学会或未满足前置条件。" % [item_name, member_name]
		&"consume_failed":
			return "%s 已触发学习，但库存扣减失败。" % item_name
		&"service_unavailable":
			return "当前技能书服务尚未准备完成。"
		_:
			return "%s 当前无法使用。" % item_name


func _build_warehouse_window_data() -> Dictionary:
	var total_capacity := 0
	var used_slots := 0
	var free_slots := 0
	var is_over_capacity := false
	var inventory_entries: Array[Dictionary] = []
	var target_members := _build_warehouse_target_member_entries()

	var party_warehouse_service = _get_party_warehouse_service()
	if party_warehouse_service != null:
		total_capacity = party_warehouse_service.get_total_capacity()
		used_slots = party_warehouse_service.get_used_slots()
		free_slots = party_warehouse_service.get_free_slots()
		is_over_capacity = party_warehouse_service.is_over_capacity()

		for entry_variant in party_warehouse_service.get_inventory_entries():
			if entry_variant is not Dictionary:
				continue
			var entry_data: Dictionary = entry_variant.duplicate(true)
			var granted_skill_id := ProgressionDataUtils.to_string_name(entry_data.get("granted_skill_id", ""))
			entry_data["granted_skill_name"] = _get_skill_display_name(granted_skill_id)
			inventory_entries.append(entry_data)

	var summary_text := "容量 %d 格  |  已用 %d 格  |  空余 %d 格" % [
		total_capacity,
		used_slots,
		free_slots,
	]
	var status_text := "当前版本支持查看、丢弃和让指定角色使用技能书。非装备物品会优先补满同类堆栈，装备则按实例独立占格。"
	if is_over_capacity:
		status_text = "仓库当前超容 %d 格。已存物品不会被删除，但此时不能继续新增条目，只能整理和移除。" % [
			used_slots - total_capacity
		]

	return {
		"title": "共享仓库",
		"meta": "入口：%s  |  规则：全队共享、按堆栈/实例占格、不计重量。" % _get_active_warehouse_meta_label(),
		"summary_text": summary_text,
		"status_text": status_text,
		"target_members": target_members,
		"default_target_member_id": String(_resolve_warehouse_target_member_id()),
		"entries": inventory_entries,
	}


func _get_item_display_name(item_id: StringName) -> String:
	if _has_runtime() and _runtime.has_method("get_item_display_name"):
		return _runtime.get_item_display_name(item_id)
	var party_warehouse_service = _get_party_warehouse_service()
	if party_warehouse_service == null:
		return String(item_id)
	var item_def = party_warehouse_service.get_item_def(item_id)
	if item_def != null and not item_def.display_name.is_empty():
		return item_def.display_name
	return String(item_id)


func _get_skill_display_name(skill_id: StringName) -> String:
	var skill_def: SkillDef = null
	var game_session = _get_game_session()
	if game_session != null and game_session.has_method("get_skill_defs"):
		skill_def = game_session.get_skill_defs().get(skill_id) as SkillDef
	if skill_def != null and not skill_def.display_name.is_empty():
		return skill_def.display_name
	return String(skill_id)


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


func _get_party_state():
	if not _has_runtime():
		return null
	if _runtime.has_method("get_party_state"):
		return _runtime.get_party_state()
	return _runtime._party_state if "_party_state" in _runtime else null


func _get_party_warehouse_service():
	if not _has_runtime():
		return null
	if _runtime.has_method("get_party_warehouse_service"):
		return _runtime.get_party_warehouse_service()
	return _runtime._party_warehouse_service if "_party_warehouse_service" in _runtime else null


func _get_party_item_use_service():
	if not _has_runtime():
		return null
	if _runtime.has_method("get_party_item_use_service"):
		return _runtime.get_party_item_use_service()
	return _runtime._party_item_use_service if "_party_item_use_service" in _runtime else null


func _get_game_session():
	if not _has_runtime():
		return null
	if _runtime.has_method("get_game_session"):
		return _runtime.get_game_session()
	return _runtime._game_session if "_game_session" in _runtime else null


func _is_battle_active() -> bool:
	if not _has_runtime():
		return false
	if _runtime.has_method("is_battle_active"):
		return _runtime.is_battle_active()
	return _runtime._is_battle_active() if _runtime.has_method("_is_battle_active") else false


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


func _set_active_warehouse_entry_label(entry_label: String) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("set_active_warehouse_entry_label"):
		_runtime.set_active_warehouse_entry_label(entry_label)
	elif "_active_warehouse_entry_label" in _runtime:
		_runtime._active_warehouse_entry_label = entry_label


func _get_active_warehouse_meta_label() -> String:
	if not _has_runtime():
		return ""
	if _runtime.has_method("get_active_warehouse_entry_label"):
		return String(_runtime.get_active_warehouse_entry_label())
	return String(_runtime._active_warehouse_entry_label) if "_active_warehouse_entry_label" in _runtime else ""


func _persist_party_state() -> int:
	if not _has_runtime():
		return ERR_UNAVAILABLE
	if _runtime.has_method("persist_party_state"):
		return int(_runtime.persist_party_state())
	return int(_runtime._persist_party_state()) if _runtime.has_method("_persist_party_state") else ERR_UNAVAILABLE


func _set_party_selected_member_id(member_id: StringName) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("set_party_selected_member_id"):
		_runtime.set_party_selected_member_id(member_id)
	elif "_party_selected_member_id" in _runtime:
		_runtime._party_selected_member_id = member_id


func _get_party_selected_member_id() -> StringName:
	if not _has_runtime():
		return &""
	if _runtime.has_method("get_party_selected_member_id"):
		return _runtime.get_party_selected_member_id()
	return _runtime._party_selected_member_id if "_party_selected_member_id" in _runtime else &""


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
