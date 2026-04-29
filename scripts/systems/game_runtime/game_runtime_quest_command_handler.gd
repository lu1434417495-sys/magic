class_name GameRuntimeQuestCommandHandler
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


func command_accept_quest(quest_id: StringName, allow_reaccept: bool = false) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	var character_management = _get_character_management()
	if character_management == null:
		return _command_error("运行时尚未初始化。")
	if quest_id == &"":
		return _command_error("任务 ID 不能为空。")
	var quest_data := _get_quest_def_data(quest_id)
	if quest_data.is_empty():
		return _command_error("未找到任务 %s。" % String(quest_id))
	var quest_label := _resolve_quest_label(quest_id, quest_data)
	var party_state = _get_party_state()
	if party_state != null and party_state.has_active_quest(quest_id):
		return _command_error("任务《%s》已在进行中，不能重复接取。" % quest_label)
	if party_state != null and party_state.has_claimable_quest(quest_id):
		return _command_error("任务《%s》已完成，奖励待领取，当前不可再次接取。" % quest_label)
	var has_completed: bool = party_state != null and party_state.has_completed_quest(quest_id)
	var is_repeatable := bool(quest_data.get("is_repeatable", false))
	var effective_allow_reaccept: bool = allow_reaccept or (has_completed and is_repeatable)
	if has_completed and not effective_allow_reaccept:
		return _command_error("任务《%s》已完成，当前不可再次接取。" % quest_label)
	if not character_management.accept_quest(quest_id, _get_world_step(), effective_allow_reaccept):
		return _command_error("当前无法接取任务《%s》。" % quest_label)
	_set_party_state(character_management.get_party_state())
	var persist_error := _persist_party_state()
	var message := "已重新接取任务《%s》。" % quest_label if has_completed and effective_allow_reaccept else "已接取任务《%s》。" % quest_label
	if persist_error != OK:
		message = "%s 但队伍状态持久化失败。" % message
		_update_status(message)
		return _command_error(message)
	_update_status(message)
	return _command_ok(message)


func command_progress_quest(quest_id: StringName, objective_id: StringName, progress_delta: int = 1, payload: Dictionary = {}) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	if _get_character_management() == null:
		return _command_error("运行时尚未初始化。")
	if quest_id == &"" or objective_id == &"":
		return _command_error("任务 ID 和目标 ID 不能为空。")
	var event_data := {
		"event_type": "progress",
		"quest_id": String(quest_id),
		"objective_id": String(objective_id),
		"progress_delta": maxi(progress_delta, 0),
	}
	for key in payload.keys():
		event_data[key] = payload[key]
	var summary := _apply_quest_progress_events_to_party([event_data], "quest")
	if not (summary.get("progressed_quest_ids", []) as Array).has(quest_id):
		return _command_error("当前无法推进任务 %s 的目标 %s。" % [String(quest_id), String(objective_id)])
	var persist_error := _persist_party_state()
	var message := "已推进任务 %s 的目标 %s。" % [String(quest_id), String(objective_id)]
	if persist_error != OK:
		message = "%s 但队伍状态持久化失败。" % message
		_update_status(message)
		return _command_error(message)
	_update_status(message)
	return _command_ok(message)


func command_complete_quest(quest_id: StringName) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	var character_management = _get_character_management()
	if character_management == null:
		return _command_error("运行时尚未初始化。")
	if quest_id == &"":
		return _command_error("任务 ID 不能为空。")
	var quest_data := _get_quest_def_data(quest_id)
	var quest_label := _resolve_quest_label(quest_id, quest_data)
	if not character_management.complete_quest(quest_id, _get_world_step()):
		return _command_error("当前无法完成任务《%s》。" % quest_label)
	_set_party_state(character_management.get_party_state())
	var persist_error := _persist_party_state()
	var message := "已完成任务《%s》，奖励待领取。" % quest_label
	if persist_error != OK:
		message = "%s 但队伍状态持久化失败。" % message
		_update_status(message)
		return _command_error(message)
	_update_status(message)
	return _command_ok(message)


func command_submit_quest_item(quest_id: StringName, objective_id: StringName = &"") -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	var character_management = _get_character_management()
	if character_management == null:
		return _command_error("运行时尚未初始化。")
	if quest_id == &"":
		return _command_error("任务 ID 不能为空。")
	var quest_data := _get_quest_def_data(quest_id)
	if quest_data.is_empty():
		return _command_error("未找到任务 %s。" % String(quest_id))
	var quest_label := _resolve_quest_label(quest_id, quest_data)
	var submit_result: Dictionary = character_management.submit_item_objective(quest_id, objective_id, _get_world_step())
	if not bool(submit_result.get("ok", false)):
		var item_id := ProgressionDataUtils.to_string_name(submit_result.get("item_id", ""))
		var item_label := _get_item_display_name(item_id)
		var required_quantity := maxi(int(submit_result.get("required_quantity", 0)), 0)
		match String(submit_result.get("error_code", "")):
			"invalid_quest_id":
				return _command_error("任务 ID 不能为空。")
			"quest_not_active":
				return _command_error("当前没有进行中的任务《%s》。" % quest_label)
			"quest_def_missing":
				return _command_error("任务《%s》缺少目标配置，当前无法提交。" % quest_label)
			"invalid_submit_item_objective":
				return _command_error("任务《%s》包含无效的物资提交目标，当前无法提交。" % quest_label)
			"objective_already_complete":
				return _command_error("任务《%s》的物资目标已完成，无需重复提交。" % quest_label)
			"submit_item_missing_inventory":
				return _command_error("共享仓库缺少%s x%d，无法提交给任务《%s》。" % [
					item_label,
					required_quantity,
					quest_label,
				])
			"submit_item_commit_failed":
				return _command_error("当前无法从共享仓库扣除任务《%s》所需物资。" % quest_label)
			"quest_progress_failed":
				return _command_error("共享仓库扣除已回滚，当前无法推进任务《%s》。" % quest_label)
			_:
				return _command_error("任务《%s》当前没有可提交的物资目标。" % quest_label)
	_set_party_state(character_management.get_party_state())
	var item_id := ProgressionDataUtils.to_string_name(submit_result.get("item_id", ""))
	var item_label := _get_item_display_name(item_id)
	var submitted_quantity := maxi(int(submit_result.get("submitted_quantity", 0)), 0)
	var claimable_quest_ids: Array = submit_result.get("claimable_quest_ids", [])
	var message := "已为任务《%s》提交 %s x%d。" % [quest_label, item_label, submitted_quantity]
	if claimable_quest_ids.has(quest_id):
		message = "已为任务《%s》提交 %s x%d，奖励待领取。" % [quest_label, item_label, submitted_quantity]
	var persist_error := _persist_party_state()
	if persist_error != OK:
		message = "%s 但队伍状态持久化失败。" % message
		_update_status(message)
		return _command_error(message)
	_update_status(message)
	var result := _command_ok(message)
	result["objective_id"] = String(submit_result.get("objective_id", ""))
	result["item_id"] = String(item_id)
	result["submitted_quantity"] = submitted_quantity
	return result


func command_claim_quest(quest_id: StringName) -> Dictionary:
	if not _has_runtime():
		return _runtime_unavailable_error()
	var character_management = _get_character_management()
	if character_management == null:
		return _command_error("运行时尚未初始化。")
	if quest_id == &"":
		return _command_error("任务 ID 不能为空。")
	var quest_data := _get_quest_def_data(quest_id)
	if quest_data.is_empty():
		return _command_error("未找到任务 %s。" % String(quest_id))
	var quest_label := _resolve_quest_label(quest_id, quest_data)
	var claim_result: Dictionary = character_management.claim_quest_reward(quest_id, _get_world_step())
	if not bool(claim_result.get("ok", false)):
		var error_code := String(claim_result.get("error_code", ""))
		match error_code:
			"quest_not_claimable":
				return _command_error("当前没有可领取的任务《%s》奖励。" % quest_label)
			"quest_def_missing":
				return _command_error("任务《%s》缺少奖励配置，当前无法领取。" % quest_label)
			"invalid_gold_amount":
				return _command_error("任务《%s》包含无效的金币奖励配置，当前无法领取。" % quest_label)
			"invalid_item_reward":
				return _command_error("任务《%s》包含无效的物品奖励配置，当前无法领取。" % quest_label)
			"invalid_pending_character_reward":
				return _command_error("任务《%s》包含无效的角色奖励配置，当前无法领取。" % quest_label)
			"item_reward_missing_def":
				return _command_error("任务《%s》引用了缺失的物品奖励配置，当前无法领取。" % quest_label)
			"reward_overflow":
				return _command_error("共享仓库空间不足，领取任务《%s》奖励会溢出，当前无法领取。" % quest_label)
			"quest_reward_commit_failed":
				return _command_error("任务《%s》奖励写入共享仓库失败，当前无法领取。" % quest_label)
			"unsupported_reward_types":
				var unsupported_types := _string_name_array_to_string_array(claim_result.get("unsupported_reward_types", []))
				var unsupported_text := "、".join(unsupported_types) if not unsupported_types.is_empty() else "未知奖励"
				return _command_error("任务《%s》包含暂不支持的奖励类型：%s。" % [quest_label, unsupported_text])
			_:
				return _command_error("当前无法领取任务《%s》奖励。" % quest_label)
	_set_party_state(character_management.get_party_state())
	var persist_error := _persist_party_state()
	var gold_delta := int(claim_result.get("gold_delta", 0))
	var reward_summary := _build_quest_claim_reward_summary_text(claim_result)
	var message := "已领取任务《%s》奖励。" % quest_label
	if not reward_summary.is_empty():
		message = "已领取任务《%s》奖励，获得 %s。" % [quest_label, reward_summary]
	if persist_error != OK:
		message = "%s 但队伍状态持久化失败。" % message
		_update_status(message)
		return _command_error(message)
	_update_status(message)
	var result := _command_ok(message)
	result["gold_delta"] = gold_delta
	result["item_rewards"] = (claim_result.get("item_rewards", []) as Array).duplicate(true)
	result["pending_character_rewards"] = (claim_result.get("pending_character_rewards", []) as Array).duplicate(true)
	return result


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


func _get_character_management():
	return _runtime.get_character_management() if _has_runtime() else null


func _get_party_state():
	return _runtime.get_party_state() if _has_runtime() else null


func _set_party_state(party_state) -> void:
	if _has_runtime():
		_runtime.set_party_state(party_state)


func _get_world_step() -> int:
	return _runtime.get_world_step() if _has_runtime() else 0


func _persist_party_state() -> int:
	return _runtime.persist_party_state() if _has_runtime() else ERR_UNAVAILABLE


func _update_status(message: String) -> void:
	if _has_runtime():
		_runtime.update_status(message)


func _get_item_display_name(item_id: StringName) -> String:
	return _runtime.get_item_display_name(item_id) if _has_runtime() else String(item_id)


func _apply_quest_progress_events_to_party(event_variants: Array, source_domain: String = "quest") -> Dictionary:
	return _runtime.apply_quest_progress_events_to_party(event_variants, source_domain) if _has_runtime() else {}


func _get_quest_def_data(quest_id: StringName) -> Dictionary:
	return _runtime._get_quest_def_data(quest_id) if _has_runtime() else {}


func _resolve_quest_label(quest_id: StringName, quest_data: Dictionary) -> String:
	return _runtime._resolve_quest_label(quest_id, quest_data) if _has_runtime() else String(quest_id)


func _build_quest_claim_reward_summary_text(claim_result: Dictionary) -> String:
	return _runtime._build_quest_claim_reward_summary_text(claim_result) if _has_runtime() else ""


func _string_name_array_to_string_array(values: Array) -> Array[String]:
	return _runtime._string_name_array_to_string_array(values) if _has_runtime() else []
