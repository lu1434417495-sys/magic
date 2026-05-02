class_name GameRuntimeSettlementCommandHandler
extends RefCounted

const SETTLEMENT_SHOP_SERVICE_SCRIPT = preload("res://scripts/systems/settlement/settlement_shop_service.gd")
const SETTLEMENT_FORGE_SERVICE_SCRIPT = preload("res://scripts/systems/settlement/settlement_forge_service.gd")
const SETTLEMENT_RESEARCH_SERVICE_SCRIPT = preload("res://scripts/systems/settlement/settlement_research_service.gd")
const SETTLEMENT_SERVICE_RESULT_SCRIPT = preload("res://scripts/systems/settlement/settlement_service_result.gd")
const QUEST_DEF_SCRIPT = preload("res://scripts/player/progression/quest_def.gd")
const LOW_LUCK_RELIC_RULES_SCRIPT = preload("res://scripts/systems/fate/low_luck_relic_rules.gd")
const TRUE_RANDOM_SEED_SERVICE_SCRIPT = preload("res://scripts/utils/true_random_seed_service.gd")

const REST_FULL_COST := 50
const INTEL_NETWORK_COST := 50
const STAGECOACH_COST_PER_STEP := 10
const VILLAGE_RUMOR_RANGE := 5
const INTEL_NETWORK_RANGE := 8

const SHOP_INTERACTION_IDS := {
	"service_basic_supply": true,
	"service_local_trade": true,
	"service_city_market": true,
	"service_military_supply": true,
	"service_grand_auction": true,
}

const STAGECOACH_INTERACTION_IDS := {
	"service_stagecoach": true,
	"service_world_gate_travel": true,
}

const CONTRACT_BOARD_INTERACTION_IDS := {
	"service_contract_board": true,
	"service_bounty_registry": true,
}

const UNIMPLEMENTED_INTERACTION_IDS := {
	"service_join_guild": true,
	"service_identify_relic": true,
	"service_recruit_specialist": true,
	"service_issue_regional_edict": true,
	"service_unlock_archive": true,
	"service_diplomatic_clearance": true,
	"service_amnesty_review": true,
	"service_elite_recruitment": true,
	"service_respecialize_build": true,
	"service_manage_reputation": true,
	"service_open_trade_route": true,
	"service_legend_contracts": true,
	"service_hire_expert": true,
}

const AUTHORITATIVE_SETTLEMENT_ACTION_PAYLOAD_KEYS := {
	"action_id": true,
	"facility_id": true,
	"facility_template_id": true,
	"facility_name": true,
	"npc_id": true,
	"npc_template_id": true,
	"npc_name": true,
	"service_type": true,
	"interaction_script_id": true,
}

var _runtime_ref: WeakRef = null
var _runtime = null:
	get:
		return _runtime_ref.get_ref() if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null

var _shop_service = SETTLEMENT_SHOP_SERVICE_SCRIPT.new()
var _forge_service = SETTLEMENT_FORGE_SERVICE_SCRIPT.new()
var _research_service = SETTLEMENT_RESEARCH_SERVICE_SCRIPT.new()


func setup(runtime) -> void:
	_runtime = runtime


func dispose() -> void:
	_runtime = null
	_shop_service = SETTLEMENT_SHOP_SERVICE_SCRIPT.new()
	_forge_service = SETTLEMENT_FORGE_SERVICE_SCRIPT.new()
	_research_service = SETTLEMENT_RESEARCH_SERVICE_SCRIPT.new()


func get_settlement_window_data(settlement_id: String = "") -> Dictionary:
	if not _has_runtime():
		return {}
	var target_id := settlement_id if not settlement_id.is_empty() else resolve_command_settlement_id()
	var settlement: Dictionary = _get_settlement_record(target_id)
	if settlement.is_empty():
		return {}
	var settlement_state := _get_or_create_settlement_state(target_id)
	return {
		"settlement_id": settlement.get("settlement_id", ""),
		"display_name": settlement.get("display_name", ""),
		"tier_name": settlement.get("tier_name", ""),
		"footprint_size": settlement.get("footprint_size", null),
		"faction_id": settlement.get("faction_id", ""),
		"facilities": settlement.get("facilities", null),
		"available_services": _build_service_entries(settlement, settlement_state),
		"service_npcs": settlement.get("service_npcs", null),
		"member_options": _build_member_options(),
		"default_member_id": String(resolve_default_settlement_member_id()),
		"state_summary_text": _build_settlement_state_summary(settlement_state),
		"feedback_text": _build_settlement_window_feedback_text(),
	}


func get_shop_window_data() -> Dictionary:
	var context := _get_active_shop_context()
	if context.is_empty():
		return {}
	var entries: Array[Dictionary] = []
	for entry_variant in context.get("buy_entries", []):
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant.duplicate(true)
		entry["entry_id"] = "buy:%s" % String(entry.get("item_id", ""))
		entry["state_label"] = "状态：可购" if bool(entry.get("can_buy", false)) else "状态：不可购"
		entry["cost_label"] = "单价 %d 金" % int(entry.get("unit_price", 0))
		entry["summary_text"] = String(entry.get("stock_text", ""))
		entry["details_text"] = String(entry.get("description", ""))
		entry["is_enabled"] = bool(entry.get("can_buy", false))
		entry["disabled_reason"] = String(entry.get("disabled_reason", ""))
		entry["shop_action"] = "buy"
		entries.append(entry)
	for entry_variant in context.get("sell_entries", []):
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant.duplicate(true)
		var sell_instance_id := String(entry.get("instance_id", ""))
		if not sell_instance_id.is_empty():
			entry["entry_id"] = "sell:%s:%s" % [String(entry.get("item_id", "")), sell_instance_id]
		else:
			entry["entry_id"] = "sell:%s" % String(entry.get("item_id", ""))
		entry["state_label"] = "状态：可售" if bool(entry.get("can_sell", false)) else "状态：不可售"
		entry["cost_label"] = "回收 %d 金" % int(entry.get("unit_price", 0))
		entry["summary_text"] = String(entry.get("stock_text", ""))
		entry["details_text"] = String(entry.get("description", ""))
		entry["is_enabled"] = bool(entry.get("can_sell", false))
		entry["disabled_reason"] = String(entry.get("disabled_reason", ""))
		entry["shop_action"] = "sell"
		entries.append(entry)
	context["entries"] = entries
	context["summary_text"] = "持有金币：%d" % int(context.get("gold", 0))
	context["state_summary_text"] = String(context.get("feedback_text", ""))
	context["action_id"] = "shop:trade"
	context["panel_kind"] = "shop"
	context["show_member_selector"] = true
	context["party_state"] = _get_party_state()
	context["member_options"] = _build_member_options()
	context["default_member_id"] = String(resolve_default_settlement_member_id())
	return context


func get_contract_board_window_data() -> Dictionary:
	var context := _get_active_contract_board_context()
	if context.is_empty():
		return {}
	return context.duplicate(true)


func get_forge_window_data() -> Dictionary:
	var context := _get_active_forge_context()
	if context.is_empty():
		return {}
	return context.duplicate(true)


func get_stagecoach_window_data() -> Dictionary:
	var context := _get_active_stagecoach_context()
	if context.is_empty():
		return {}
	var entries: Array[Dictionary] = []
	for entry_variant in context.get("destinations", []):
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant.duplicate(true)
		entry["entry_id"] = "travel:%s" % String(entry.get("settlement_id", ""))
		entry["state_label"] = "状态：可出发" if bool(entry.get("can_travel", false)) else "状态：不可出发"
		entry["cost_label"] = "路费 %d 金" % int(entry.get("travel_cost", 0))
		entry["summary_text"] = String(entry.get("tier_name", ""))
		entry["details_text"] = "%s %s" % [
			String(entry.get("tier_name", "")),
			String(entry.get("disabled_reason", "")),
		]
		entry["is_enabled"] = bool(entry.get("can_travel", false))
		entry["target_settlement_id"] = String(entry.get("settlement_id", ""))
		entries.append(entry)
	context["entries"] = entries
	context["summary_text"] = "持有金币：%d" % int(context.get("gold", 0))
	context["state_summary_text"] = String(context.get("feedback_text", ""))
	context["action_id"] = "stagecoach:travel"
	context["panel_kind"] = "stagecoach"
	context["meta"] = "驿站：%s  |  金币：%d" % [
		String(context.get("origin_name", "")),
		int(context.get("gold", 0)),
	]
	context["confirm_label"] = "确认出发"
	context["cancel_label"] = "返回据点"
	context["show_member_selector"] = true
	context["entry_title"] = "可选路线"
	context["summary_title"] = "行程概况"
	context["state_title"] = "行程状态"
	context["cost_title"] = "行程费用"
	context["details_title"] = "行程说明"
	context["member_title"] = "出发成员"
	context["empty_state_label"] = "状态：暂无路线"
	context["empty_cost_label"] = "费用：暂无路线"
	context["empty_details_text"] = "当前没有可用路线。"
	context["party_state"] = _get_party_state()
	context["member_options"] = _build_member_options()
	context["default_member_id"] = String(resolve_default_settlement_member_id())
	return context


func command_execute_settlement_action(action_id: String, payload: Dictionary = {}) -> Dictionary:
	if not _has_runtime():
		return _command_error("运行时尚未初始化。")
	if action_id.is_empty():
		return _command_error("据点动作 ID 不能为空。")
	if _is_battle_active():
		return _command_error("当前处于战斗中，不能执行据点动作。")
	var settlement_id := resolve_command_settlement_id()
	if settlement_id.is_empty():
		return _command_error("当前没有可执行动作的据点。")
	var validation := _validate_settlement_action_request(settlement_id, action_id, payload)
	if not bool(validation.get("ok", false)):
		return _command_error(String(validation.get("message", "当前据点未开放该服务。")))
	var service_entry_variant = validation.get("service_entry", {})
	if service_entry_variant is not Dictionary:
		return _command_error("当前据点未开放该服务。")
	var merged_payload := _build_settlement_action_payload_from_service_entry(
		action_id,
		service_entry_variant as Dictionary,
		payload
	)
	if merged_payload.is_empty():
		return _command_error(_build_unknown_settlement_action_message(settlement_id, action_id))
	return _dispatch_settlement_action(settlement_id, action_id, merged_payload)


func command_shop_buy(item_id: StringName, quantity: int) -> Dictionary:
	if _get_active_modal_id() != "shop":
		return _command_error("当前没有打开据点商店。")
	var context := _get_active_shop_context()
	if context.is_empty():
		return _command_error("当前商店上下文缺失。")
	var settlement_id := String(context.get("settlement_id", ""))
	var settlement_state := _get_or_create_settlement_state(settlement_id)
	var result := _shop_service.buy(
		String(context.get("interaction_script_id", "")),
		_get_settlement_record(settlement_id),
		settlement_state,
		_get_item_defs(),
		_get_party_warehouse_service(),
		_get_party_state(),
		item_id,
		quantity
	)
	if not bool(result.get("success", false)):
		_refresh_active_shop_context()
		return _command_error(String(result.get("message", "购买失败。")))
	_set_active_settlement_state(settlement_id, settlement_state)
	var persist_result := _persist_changes(true, true, false)
	var message := String(result.get("message", "购买成功。"))
	if not bool(persist_result.get("ok", false)):
		message = "%s 但队伍或据点状态持久化失败。" % message
	_refresh_active_shop_context()
	_update_status(message)
	return _command_ok(message)


func command_shop_sell(item_id: StringName, quantity: int, instance_id: StringName = &"") -> Dictionary:
	if _get_active_modal_id() != "shop":
		return _command_error("当前没有打开据点商店。")
	var context := _get_active_shop_context()
	if context.is_empty():
		return _command_error("当前商店上下文缺失。")
	var settlement_id := String(context.get("settlement_id", ""))
	var settlement_state := _get_or_create_settlement_state(settlement_id)
	var result := _shop_service.sell(
		String(context.get("interaction_script_id", "")),
		_get_settlement_record(settlement_id),
		settlement_state,
		_get_item_defs(),
		_get_party_warehouse_service(),
		_get_party_state(),
		item_id,
		quantity,
		instance_id
	)
	if not bool(result.get("success", false)):
		_refresh_active_shop_context()
		return _command_error(String(result.get("message", "出售失败。")))
	_set_active_settlement_state(settlement_id, settlement_state)
	var persist_result := _persist_changes(true, true, false)
	var message := String(result.get("message", "出售成功。"))
	if not bool(persist_result.get("ok", false)):
		message = "%s 但队伍或据点状态持久化失败。" % message
	_refresh_active_shop_context()
	_update_status(message)
	return _command_ok(message)


func command_stagecoach_travel(settlement_id: String) -> Dictionary:
	if _get_active_modal_id() != "stagecoach":
		return _command_error("当前没有打开驿站路线窗口。")
	var context := _get_active_stagecoach_context()
	if context.is_empty():
		return _command_error("当前没有可用的驿站路线。")
	var destination := _find_stagecoach_destination(context, settlement_id)
	if destination.is_empty():
		return _command_error("当前驿站路线中不存在该目的地。")
	if not bool(destination.get("can_travel", false)):
		return _command_error(String(destination.get("disabled_reason", "当前无法前往该据点。")))
	var party_state = _get_party_state()
	if party_state == null:
		return _command_error("当前不存在队伍数据。")
	var travel_cost := int(destination.get("travel_cost", 0))
	if not party_state.spend_gold(travel_cost):
		return _command_error("金币不足，无法启程。")
	var destination_id := String(destination.get("settlement_id", ""))
	var destination_record := _get_settlement_record(destination_id)
	if destination_record.is_empty():
		return _command_error("未找到目标据点。")
	var destination_coord: Vector2i = destination_record.get("origin", Vector2i.ZERO)
	_clear_settlement_entry_context(false)
	_set_player_coord(destination_coord)
	_set_selected_coord(destination_coord)
	_mark_settlement_visited(destination_id)
	_clear_active_stagecoach_context()
	_set_active_modal_id("settlement")
	_set_active_settlement_id(destination_id)
	_set_settlement_feedback_text("驿队将你送到了 %s。" % String(destination_record.get("display_name", destination_id)))
	_refresh_world_visibility()
	var persist_result := _persist_changes(true, true, true)
	var message := "已从 %s 抵达 %s，花费 %d 金。" % [
		String(context.get("origin_name", "当前据点")),
		String(destination_record.get("display_name", destination_id)),
		travel_cost,
	]
	if not bool(persist_result.get("ok", false)):
		message = "%s 但队伍或世界状态持久化失败。" % message
	_update_status(message)
	return _command_ok(message)


func on_settlement_action_requested(settlement_id: String, action_id: String, payload: Dictionary) -> void:
	var validation := _validate_settlement_action_request(settlement_id, action_id, payload)
	if not bool(validation.get("ok", false)):
		var message := String(validation.get("message", "当前据点未开放该服务。"))
		_set_settlement_feedback_text(message)
		_update_status(message)
		return
	var service_entry_variant = validation.get("service_entry", {})
	if service_entry_variant is not Dictionary:
		var service_error_message := "当前据点未开放该服务。"
		_set_settlement_feedback_text(service_error_message)
		_update_status(service_error_message)
		return
	var merged_payload := _build_settlement_action_payload_from_service_entry(
		action_id,
		service_entry_variant as Dictionary,
		payload
	)
	if merged_payload.is_empty():
		var unknown_message := _build_unknown_settlement_action_message(settlement_id, action_id)
		_set_settlement_feedback_text(unknown_message)
		_update_status(unknown_message)
		return
	_dispatch_settlement_action(settlement_id, action_id, merged_payload)


func _dispatch_settlement_action(settlement_id: String, action_id: String, payload: Dictionary) -> Dictionary:
	if not _has_runtime():
		return _command_error("运行时尚未初始化。")
	var interaction_script_id := String(payload.get("interaction_script_id", ""))
	if interaction_script_id == _runtime.PARTY_WAREHOUSE_INTERACTION_ID:
		_finalize_successful_action(action_id, payload, {
			"persist_party_state": true,
			"quest_progress_events": _extract_quest_progress_events(payload, action_id, settlement_id),
		})
		_clear_settlement_entry_context()
		_set_active_settlement_id(settlement_id)
		_set_active_modal_id("")
		_open_party_warehouse_window("据点服务：%s·%s" % [
			String(payload.get("facility_name", "设施")),
			String(payload.get("npc_name", "值守人员")),
		])
		var warehouse_message := "已从据点服务打开共享仓库。"
		_update_status(warehouse_message)
		return _command_ok(warehouse_message)
	if CONTRACT_BOARD_INTERACTION_IDS.has(interaction_script_id):
		if _is_contract_board_modal_submission(payload):
			return _submit_contract_board_quest_action(settlement_id, action_id, payload)
		_open_contract_board_modal(settlement_id, payload)
		return _command_ok("已打开 %s 的任务板。" % String(payload.get("facility_name", "据点任务板")))
	if SHOP_INTERACTION_IDS.has(interaction_script_id):
		_open_shop_modal(settlement_id, payload)
		return _command_ok("已打开 %s 的商店。" % String(payload.get("facility_name", "据点商店")))
	if _is_forge_interaction(interaction_script_id) and not _is_forge_modal_submission(payload):
		_open_forge_modal(settlement_id, payload)
		return _command_ok("已打开 %s 的锻造界面。" % String(payload.get("facility_name", "锻造设施")))
	if STAGECOACH_INTERACTION_IDS.has(interaction_script_id):
		_open_stagecoach_modal(settlement_id, payload)
		return _command_ok("已打开 %s 的驿站路线。" % String(payload.get("facility_name", "驿站")))
	var result := execute_settlement_action(settlement_id, action_id, payload)
	var message := String(result.get("message", "交互已完成。"))
	_set_settlement_feedback_text(message)
	var action_succeeded := bool(result.get("success", false))
	if _is_forge_interaction(interaction_script_id):
		_refresh_active_forge_context(message)
		if action_succeeded:
			var forge_persist_result := _finalize_successful_action(action_id, payload, result)
			if bool(forge_persist_result.get("ok", false)):
				_update_status(message)
				return _command_ok(message)
			var forge_persist_message := "%s 但队伍或据点状态持久化失败。" % message
			_update_status(forge_persist_message)
			return _command_error(forge_persist_message)
		_update_status(message)
		return _command_error(message)
	if action_succeeded:
		var persist_result := _finalize_successful_action(action_id, payload, result)
		if bool(persist_result.get("ok", false)):
			_update_status(message)
			return _command_ok(message)
		var persist_message := "%s 但队伍或据点状态持久化失败。" % message
		_update_status(persist_message)
		return _command_error(persist_message)
	_update_status(message)
	return _command_error(message)


func on_settlement_window_closed() -> void:
	if not _has_runtime():
		return
	_clear_settlement_entry_context()
	_set_active_settlement_id("")
	_set_settlement_feedback_text("")
	_clear_active_contract_board_context()
	_clear_active_shop_context()
	_clear_active_forge_context()
	_clear_active_stagecoach_context()
	_set_active_modal_id("")
	_update_status("已关闭据点窗口，返回世界地图。")
	_present_pending_reward_if_ready()


func on_shop_window_closed() -> void:
	_clear_active_shop_context()
	_set_active_modal_id("settlement")
	_update_status("已关闭商店，返回据点服务。")


func on_contract_board_window_closed() -> void:
	_clear_active_contract_board_context()
	_set_active_modal_id("settlement")
	_update_status("已关闭任务板，返回据点服务。")


func on_forge_window_closed() -> void:
	var context := _get_active_forge_context()
	var forge_label := _resolve_forge_service_label(context)
	_clear_active_forge_context()
	_set_active_modal_id("settlement")
	_update_status("已关闭%s，返回据点服务。" % forge_label)


func on_stagecoach_window_closed() -> void:
	_clear_active_stagecoach_context()
	_set_active_modal_id("settlement")
	_update_status("已关闭驿站路线，返回据点服务。")


func resolve_command_settlement_id() -> String:
	if not _has_runtime():
		return ""
	var active_settlement_id := _get_active_settlement_id()
	if not active_settlement_id.is_empty():
		return active_settlement_id
	var settlement: Dictionary = _get_selected_settlement()
	return String(settlement.get("settlement_id", ""))


func build_settlement_action_payload(settlement_id: String, action_id: String, overrides: Dictionary) -> Dictionary:
	var service_entry := _resolve_settlement_service_entry(settlement_id, action_id)
	if service_entry.is_empty():
		return {}
	return _build_settlement_action_payload_from_service_entry(action_id, service_entry, overrides)


func _build_settlement_action_payload_from_service_entry(action_id: String, service_data: Dictionary, overrides: Dictionary) -> Dictionary:
	if service_data.is_empty():
		return {}
	var payload: Dictionary = {
		"action_id": action_id,
		"facility_id": service_data.get("facility_id", ""),
		"facility_template_id": service_data.get("facility_template_id", ""),
		"facility_name": service_data.get("facility_name", ""),
		"npc_id": service_data.get("npc_id", ""),
		"npc_template_id": service_data.get("npc_template_id", ""),
		"npc_name": service_data.get("npc_name", ""),
		"service_type": service_data.get("service_type", ""),
		"interaction_script_id": service_data.get("interaction_script_id", ""),
	}
	for key in overrides.keys():
		if AUTHORITATIVE_SETTLEMENT_ACTION_PAYLOAD_KEYS.has(String(key)):
			continue
		payload[key] = overrides[key]
	if String(payload.get("member_id", "")).is_empty():
		var member_id := resolve_default_settlement_member_id()
		if member_id != &"":
			payload["member_id"] = String(member_id)
	return payload


func _validate_settlement_action_request(settlement_id: String, action_id: String, payload: Dictionary) -> Dictionary:
	var modal_validation := _validate_settlement_action_modal_context(settlement_id, action_id, payload)
	if not bool(modal_validation.get("ok", false)):
		return modal_validation
	var visibility_validation := _validate_settlement_visibility_context(settlement_id)
	if not bool(visibility_validation.get("ok", false)):
		return visibility_validation
	var service_entry := _resolve_settlement_service_entry(settlement_id, action_id)
	if service_entry.is_empty():
		return {
			"ok": false,
			"message": _build_unknown_settlement_action_message(settlement_id, action_id),
		}
	if _settlement_action_requires_enabled_service(payload) and not bool(service_entry.get("is_enabled", true)):
		return {
			"ok": false,
			"message": _build_disabled_settlement_action_message(service_entry),
		}
	return {
		"ok": true,
		"service_entry": service_entry,
	}


func _validate_settlement_action_modal_context(settlement_id: String, action_id: String, payload: Dictionary) -> Dictionary:
	if _is_contract_board_modal_submission(payload):
		if _get_active_modal_id() != "contract_board":
			return {"ok": false, "message": "当前没有打开对应的任务板。"}
		var contract_board_context := _get_active_contract_board_context()
		if String(contract_board_context.get("settlement_id", "")).strip_edges() != settlement_id:
			return {"ok": false, "message": "当前任务板与请求的据点不一致。"}
		if String(contract_board_context.get("action_id", "")).strip_edges() != action_id:
			return {"ok": false, "message": "当前任务板与请求的服务入口不一致。"}
		return {"ok": true}
	if _is_forge_modal_submission(payload):
		if _get_active_modal_id() != "forge":
			return {"ok": false, "message": "当前没有打开对应的锻造界面。"}
		var forge_context := _get_active_forge_context()
		if String(forge_context.get("settlement_id", "")).strip_edges() != settlement_id:
			return {"ok": false, "message": "当前锻造界面与请求的据点不一致。"}
		if String(forge_context.get("action_id", "")).strip_edges() != action_id:
			return {"ok": false, "message": "当前锻造界面与请求的服务入口不一致。"}
		return {"ok": true}
	if _get_active_modal_id() != "settlement":
		return {"ok": false, "message": "当前没有打开对应的据点窗口。"}
	var active_settlement_id := _get_active_settlement_id()
	if active_settlement_id.is_empty() or active_settlement_id != settlement_id:
		return {"ok": false, "message": "当前据点窗口与请求的据点不一致。"}
	return {"ok": true}


func _validate_settlement_visibility_context(settlement_id: String) -> Dictionary:
	var settlement := _get_settlement_record(settlement_id)
	if settlement.is_empty():
		return {"ok": false, "message": "未找到据点数据。"}
	if not _is_settlement_visible_to_player(settlement):
		return {"ok": false, "message": "当前据点不在视野中，不能执行据点服务。"}
	return {"ok": true}


func _settlement_action_requires_enabled_service(payload: Dictionary) -> bool:
	return not _is_contract_board_modal_submission(payload) and not _is_forge_modal_submission(payload)


func _resolve_settlement_service_entry(settlement_id: String, action_id: String) -> Dictionary:
	var service_variants = get_settlement_window_data(settlement_id).get("available_services", [])
	if service_variants is not Array:
		return {}
	for service_variant in service_variants:
		if service_variant is not Dictionary:
			continue
		var service_data: Dictionary = service_variant
		if String(service_data.get("action_id", "")).strip_edges() != action_id:
			continue
		return service_data.duplicate(true)
	return {}


func _build_unknown_settlement_action_message(settlement_id: String, action_id: String) -> String:
	var settlement := _get_settlement_record(settlement_id)
	var settlement_label := String(settlement.get("display_name", settlement_id)).strip_edges()
	if settlement_label.is_empty():
		settlement_label = "当前据点"
	return "%s 未开放该服务：%s。" % [settlement_label, action_id]


func _build_disabled_settlement_action_message(service_entry: Dictionary) -> String:
	var service_label := String(service_entry.get("service_type", "")).strip_edges()
	if service_label.is_empty():
		service_label = String(service_entry.get("facility_name", "")).strip_edges()
	if service_label.is_empty():
		service_label = String(service_entry.get("action_id", "该服务")).strip_edges()
	var disabled_reason := String(service_entry.get("disabled_reason", "")).strip_edges()
	if disabled_reason.is_empty():
		return "%s 当前不可用。" % service_label
	return "%s 当前不可用：%s。" % [service_label, disabled_reason]


func resolve_default_settlement_member_id() -> StringName:
	var party_state = _get_party_state()
	if party_state == null:
		return &""
	if party_state.leader_member_id != &"" and party_state.get_member_state(party_state.leader_member_id) != null:
		return party_state.leader_member_id
	for member_id_variant in party_state.active_member_ids:
		var member_id := ProgressionDataUtils.to_string_name(member_id_variant)
		if member_id != &"" and party_state.get_member_state(member_id) != null:
			return member_id
	return &""


func execute_settlement_action(settlement_id: String, action_id: String, payload: Dictionary) -> Dictionary:
	var settlement := _get_settlement_record(settlement_id)
	if settlement.is_empty():
		return _build_settlement_service_result(false, "未找到据点数据。")
	var interaction_script_id := String(payload.get("interaction_script_id", ""))
	if interaction_script_id == "service_rest_basic":
		return _execute_rest_basic(settlement, action_id, payload)
	if interaction_script_id == "service_rest_full":
		return _execute_rest_full(settlement, action_id, payload)
	if interaction_script_id == "service_village_rumor":
		return _execute_fog_reveal(settlement, action_id, payload, VILLAGE_RUMOR_RANGE, 0, "乡野传闻让周边地貌更加清晰。")
	if interaction_script_id == "service_intel_network":
		return _execute_fog_reveal(settlement, action_id, payload, INTEL_NETWORK_RANGE, INTEL_NETWORK_COST, "情报网更新了周边的行路信息。")
	if _is_forge_interaction(interaction_script_id):
		return _forge_service.execute_recipe(
			settlement,
			payload,
			_get_item_defs(),
			_get_recipe_defs(),
			_get_party_warehouse_service(),
			_get_party_state(),
			_extract_quest_progress_events(payload, action_id, settlement_id)
		)
	if _is_research_interaction(interaction_script_id):
		return _research_service.execute(
			settlement,
			payload,
			_get_party_state(),
			_extract_quest_progress_events(payload, action_id, settlement_id)
		)
	if UNIMPLEMENTED_INTERACTION_IDS.has(interaction_script_id):
		return _build_settlement_service_result(true, "该据点服务入口已接通，但其配套系统尚未开放。", [], false, false, false, 0, {}, _extract_quest_progress_events(payload, action_id, settlement_id))
	var pending_character_rewards := extract_pending_character_rewards(
		action_id,
		payload,
		String(payload.get("facility_name", "")),
		String(payload.get("npc_name", "")),
		String(payload.get("service_type", "服务"))
	)
	return _build_settlement_service_result(
		true,
		"%s 的 %s 在 %s 中为你处理了“%s”事务。首版窗口流程已接通。" % [
			String(settlement.get("display_name", settlement_id)),
			String(payload.get("npc_name", "值守人员")),
			String(payload.get("facility_name", "设施")),
			String(payload.get("service_type", "服务")),
		],
		pending_character_rewards,
		true,
		false,
		false,
		0,
		{},
		_extract_quest_progress_events(payload, action_id, settlement_id)
	)


func extract_pending_character_rewards(action_id: String, payload: Dictionary, facility_name: String, npc_name: String, service_type: String) -> Array[Dictionary]:
	var rewards: Array[Dictionary] = []
	var default_source_type := resolve_default_reward_source_type(action_id, service_type, payload)
	var default_source_label := resolve_default_reward_source_label(facility_name, npc_name, service_type)
	var explicit_rewards_variant = payload.get("pending_character_rewards", [])
	if explicit_rewards_variant is Array:
		for reward_variant in explicit_rewards_variant:
			if reward_variant is not Dictionary:
				continue
			var reward_data: Dictionary = reward_variant.duplicate(true)
			if String(reward_data.get("member_id", "")).is_empty():
				reward_data["member_id"] = String(payload.get("member_id", ""))
			if String(reward_data.get("source_type", "")).is_empty():
				reward_data["source_type"] = String(default_source_type)
			if String(reward_data.get("source_id", "")).is_empty():
				reward_data["source_id"] = String(default_source_type)
			if String(reward_data.get("source_label", "")).is_empty():
				reward_data["source_label"] = default_source_label
			if reward_data.get("entries", []) is not Array:
				reward_data["entries"] = []
			rewards.append(reward_data)
	if _runtime != null and _runtime.has_method("resolve_low_luck_settlement_event_rewards"):
		var low_luck_result_variant = _runtime.resolve_low_luck_settlement_event_rewards({
			"action_id": action_id,
			"facility_id": String(payload.get("facility_id", "")),
			"facility_name": facility_name,
			"interaction_script_id": String(payload.get("interaction_script_id", "")),
			"npc_name": npc_name,
			"payload": payload.duplicate(true),
			"service_type": service_type,
		})
		if low_luck_result_variant is Dictionary:
			var low_luck_rewards_variant: Variant = (low_luck_result_variant as Dictionary).get("pending_character_rewards", [])
			if low_luck_rewards_variant is Array:
				for reward_variant in low_luck_rewards_variant:
					if reward_variant is Dictionary:
						rewards.append((reward_variant as Dictionary).duplicate(true))
	return rewards


func resolve_default_reward_source_type(action_id: String, service_type: String, payload: Dictionary) -> StringName:
	var explicit_source_type := String(payload.get("reward_source_type", "")).strip_edges()
	if not explicit_source_type.is_empty():
		return StringName(explicit_source_type)
	var combined_label := "%s %s" % [action_id, service_type]
	if combined_label.contains("传授") or combined_label.contains("指点"):
		return &"npc_teach"
	return &"training"


func resolve_default_reward_source_label(facility_name: String, npc_name: String, service_type: String) -> String:
	if not service_type.is_empty():
		return "%s·%s" % [npc_name, service_type]
	if not facility_name.is_empty():
		return facility_name
	return "据点服务"


func _execute_rest_basic(settlement: Dictionary, action_id: String, payload: Dictionary) -> Dictionary:
	var member_effects := _restore_party_resources(0.3, false)
	var summary_lines: PackedStringArray = []
	for member_id in member_effects.keys():
		var effect: Dictionary = member_effects.get(member_id, {})
		summary_lines.append("%s +%d HP" % [_get_member_display_name(StringName(member_id)), int(effect.get("hp_restored", 0))])
	return _build_settlement_service_result(
		true,
		"%s 的篝火让全队稍作歇脚。%s" % [
			String(settlement.get("display_name", "据点")),
			"；".join(summary_lines) if not summary_lines.is_empty() else "体力恢复有限。",
		],
		extract_pending_character_rewards(action_id, payload, String(payload.get("facility_name", "")), String(payload.get("npc_name", "")), String(payload.get("service_type", "歇脚"))),
		true,
		false,
		false,
		0,
		{},
		_extract_quest_progress_events(payload, action_id, String(settlement.get("settlement_id", ""))),
		{"hp_restored": _build_member_effect_value_map(member_effects, "hp_restored")}
	)


func _execute_rest_full(settlement: Dictionary, action_id: String, payload: Dictionary) -> Dictionary:
	var party_state = _get_party_state()
	if party_state == null:
		return _build_settlement_service_result(false, "当前不存在队伍数据。")
	if not party_state.spend_gold(REST_FULL_COST):
		return _build_settlement_service_result(false, "金币不足，无法在旅店整备。")
	var member_effects := _restore_party_resources(1.0, true)
	_advance_world_time_by_steps(1)
	var summary_lines: PackedStringArray = []
	for member_id in member_effects.keys():
		var effect: Dictionary = member_effects.get(member_id, {})
		summary_lines.append("%s HP+%d MP+%d" % [_get_member_display_name(StringName(member_id)), int(effect.get("hp_restored", 0)), int(effect.get("mp_restored", 0))])
	return _build_settlement_service_result(
		true,
		"%s 的旅店让全队完成整备，花费 %d 金。%s" % [
			String(settlement.get("display_name", "据点")),
			REST_FULL_COST,
			"；".join(summary_lines) if not summary_lines.is_empty() else "状态恢复如初。",
		],
		extract_pending_character_rewards(action_id, payload, String(payload.get("facility_name", "")), String(payload.get("npc_name", "")), String(payload.get("service_type", "整备"))),
		true,
		true,
		false,
		-REST_FULL_COST,
		{},
		_extract_quest_progress_events(payload, action_id, String(settlement.get("settlement_id", ""))),
		{
			"hp_restored": _build_member_effect_value_map(member_effects, "hp_restored"),
			"mp_restored": _build_member_effect_value_map(member_effects, "mp_restored"),
			"world_step_advanced": 1,
		}
	)


func _execute_fog_reveal(settlement: Dictionary, action_id: String, payload: Dictionary, reveal_range: int, gold_cost: int, message_prefix: String) -> Dictionary:
	var party_state = _get_party_state()
	if party_state == null:
		return _build_settlement_service_result(false, "当前不存在队伍数据。")
	if gold_cost > 0 and not party_state.spend_gold(gold_cost):
		return _build_settlement_service_result(false, "金币不足，无法购买情报。")
	var revealed_coords := _reveal_world_fog(settlement.get("origin", Vector2i.ZERO), reveal_range)
	var message := "%s 共揭示了 %d 个周边格子。" % [message_prefix, revealed_coords.size()]
	if gold_cost > 0:
		message = "%s 花费 %d 金。%s" % [String(settlement.get("display_name", "据点")), gold_cost, message]
	return _build_settlement_service_result(
		true,
		message,
		extract_pending_character_rewards(action_id, payload, String(payload.get("facility_name", "")), String(payload.get("npc_name", "")), String(payload.get("service_type", "情报"))),
		gold_cost > 0,
		true,
		false,
		-gold_cost,
		{},
		_extract_quest_progress_events(payload, action_id, String(settlement.get("settlement_id", ""))),
		{"fog_revealed": revealed_coords}
	)


func _build_service_entries(settlement: Dictionary, settlement_state: Dictionary) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	for service_variant in settlement.get("available_services", []):
		if service_variant is not Dictionary:
			continue
		var service_data: Dictionary = service_variant.duplicate(true)
		var metadata := _build_service_metadata(settlement, service_data, settlement_state)
		var is_enabled := bool(metadata.get("is_enabled", true))
		var disabled_reason := String(metadata.get("disabled_reason", "")).strip_edges()
		service_data["cost_label"] = String(metadata.get("cost_label", "")).strip_edges()
		service_data["is_enabled"] = is_enabled
		service_data["disabled_reason"] = disabled_reason
		service_data["state_label"] = _build_service_state_label(is_enabled, disabled_reason)
		service_data["summary_text"] = _build_service_summary_text(service_data)
		var panel_kind := _resolve_service_panel_kind(service_data)
		if not panel_kind.is_empty():
			service_data["panel_kind"] = panel_kind
		entries.append(service_data)
	return entries


func _build_service_state_label(is_enabled: bool, disabled_reason: String) -> String:
	if is_enabled:
		return "状态：可用"
	if not disabled_reason.is_empty():
		return "状态：%s" % disabled_reason
	return "状态：不可用"


func _build_service_summary_text(service_data: Dictionary) -> String:
	return "%s · %s · %s" % [
		String(service_data.get("facility_name", "")).strip_edges(),
		String(service_data.get("npc_name", "")).strip_edges(),
		String(service_data.get("service_type", "")).strip_edges(),
	]


func _resolve_service_panel_kind(service_data: Dictionary) -> String:
	var interaction_script_id := String(service_data.get("interaction_script_id", "")).strip_edges()
	if SHOP_INTERACTION_IDS.has(interaction_script_id):
		return "shop"
	if STAGECOACH_INTERACTION_IDS.has(interaction_script_id):
		return "stagecoach"
	if CONTRACT_BOARD_INTERACTION_IDS.has(interaction_script_id):
		return "contract_board"
	if _is_forge_interaction(interaction_script_id):
		return "forge"
	return ""


func _build_service_metadata(settlement: Dictionary, service_data: Dictionary, _settlement_state: Dictionary) -> Dictionary:
	var interaction_script_id := String(service_data.get("interaction_script_id", ""))
	var party_state = _get_party_state()
	if interaction_script_id == "party_warehouse":
		return {"cost_label": "免费", "is_enabled": true}
	if interaction_script_id == "service_rest_basic":
		return {"cost_label": "免费", "is_enabled": true}
	if interaction_script_id == "service_rest_full":
		var can_afford_rest: bool = party_state != null and party_state.can_afford(REST_FULL_COST)
		return {"cost_label": "%d 金" % REST_FULL_COST, "is_enabled": can_afford_rest, "disabled_reason": "" if can_afford_rest else "金币不足"}
	if interaction_script_id == "service_village_rumor":
		return {"cost_label": "免费", "is_enabled": true}
	if interaction_script_id == "service_intel_network":
		var can_afford_intel: bool = party_state != null and party_state.can_afford(INTEL_NETWORK_COST)
		return {"cost_label": "%d 金" % INTEL_NETWORK_COST, "is_enabled": can_afford_intel, "disabled_reason": "" if can_afford_intel else "金币不足"}
	if CONTRACT_BOARD_INTERACTION_IDS.has(interaction_script_id):
		return {"cost_label": "查看任务", "is_enabled": true, "disabled_reason": ""}
	if SHOP_INTERACTION_IDS.has(interaction_script_id):
		return {"cost_label": "按商品计价", "is_enabled": true}
	if STAGECOACH_INTERACTION_IDS.has(interaction_script_id):
		var destinations := _build_stagecoach_destinations(settlement, interaction_script_id)
		return {"cost_label": "%d 金/格" % STAGECOACH_COST_PER_STEP, "is_enabled": not destinations.is_empty(), "disabled_reason": "" if not destinations.is_empty() else "暂无已访问路线"}
	if _is_forge_interaction(interaction_script_id):
		var has_recipe := _forge_service.has_available_recipe(
			settlement,
			service_data,
			_get_item_defs(),
			_get_recipe_defs()
		)
		return {
			"cost_label": "按配方材料",
			"is_enabled": has_recipe,
			"disabled_reason": "" if has_recipe else _build_forge_unavailable_reason(interaction_script_id),
		}
	if _is_research_interaction(interaction_script_id):
		return _research_service.build_service_metadata(party_state)
	if UNIMPLEMENTED_INTERACTION_IDS.has(interaction_script_id):
		return {"cost_label": "未开放", "is_enabled": false, "disabled_reason": "系统未开放"}
	return {"cost_label": "", "is_enabled": true, "disabled_reason": ""}


func _build_settlement_state_summary(settlement_state: Dictionary) -> String:
	var active_conditions: Array = settlement_state.get("active_conditions", [])
	return "\n".join([
		"访问：%s" % ("是" if bool(settlement_state.get("visited", false)) else "否"),
		"声望：%d" % int(settlement_state.get("reputation", 0)),
		"活跃条件：%s" % ("、".join(active_conditions) if not active_conditions.is_empty() else "无"),
	])


func _build_settlement_window_feedback_text() -> String:
	var feedback_text := _get_settlement_feedback_text().strip_edges()
	if not feedback_text.is_empty():
		return feedback_text
	return "点击服务继续，或切换成员后再操作。"


func _build_member_options() -> Array[Dictionary]:
	var options: Array[Dictionary] = []
	var party_state = _get_party_state()
	if party_state == null:
		return options
	var seen_member_ids: Dictionary = {}
	for member_id_variant in party_state.active_member_ids:
		var member_id := ProgressionDataUtils.to_string_name(member_id_variant)
		if member_id == &"" or seen_member_ids.has(member_id) or party_state.get_member_state(member_id) == null:
			continue
		seen_member_ids[member_id] = true
		options.append(_build_member_option(party_state, member_id, "上阵"))
	for member_id_variant in party_state.reserve_member_ids:
		var member_id := ProgressionDataUtils.to_string_name(member_id_variant)
		if member_id == &"" or seen_member_ids.has(member_id) or party_state.get_member_state(member_id) == null:
			continue
		seen_member_ids[member_id] = true
		options.append(_build_member_option(party_state, member_id, "替补"))
	return options


func _build_member_option(party_state, member_id: StringName, roster_role: String) -> Dictionary:
	var member_state = party_state.get_member_state(member_id)
	if member_state == null:
		return {}
	return {
		"member_id": String(member_id),
		"display_name": _get_member_display_name(member_id),
		"roster_role": roster_role,
		"is_leader": party_state.leader_member_id == member_id,
		"current_hp": int(member_state.current_hp),
		"current_mp": int(member_state.current_mp),
	}


func _open_contract_board_modal(settlement_id: String, payload: Dictionary) -> void:
	var window_data := _build_contract_board_window_data(settlement_id, payload)
	_set_active_contract_board_context(window_data)
	_set_active_modal_id("contract_board")
	_update_status("已打开 %s 的任务板。" % String(payload.get("facility_name", "据点任务板")))


func _build_contract_board_window_data(settlement_id: String, payload: Dictionary) -> Dictionary:
	var settlement := _get_settlement_record(settlement_id)
	var provider_interaction_id := String(payload.get("interaction_script_id", "")).strip_edges()
	var entries := _build_contract_board_entries(provider_interaction_id)
	var summary_text := String(payload.get("feedback_text", "")).strip_edges()
	if summary_text.is_empty():
		summary_text = "选择契约后会按当前状态执行接取或领奖；重复接取、待领奖励和可重复任务都会返回明确反馈。"
	return {
		"title": "%s · 任务板" % String(settlement.get("display_name", settlement_id)),
		"meta": "%s · %s · %s" % [
			String(payload.get("facility_name", "任务板")),
			String(payload.get("npc_name", "值守人员")),
			String(payload.get("service_type", "契约")),
		],
		"summary_text": summary_text,
		"state_summary_text": _build_contract_board_state_summary(entries),
		"service_name": String(payload.get("service_type", "任务板")),
		"settlement_id": settlement_id,
		"action_id": String(payload.get("action_id", "")),
		"interaction_script_id": provider_interaction_id,
		"provider_interaction_id": provider_interaction_id,
		"facility_id": String(payload.get("facility_id", "")),
		"facility_name": String(payload.get("facility_name", "")),
		"npc_id": String(payload.get("npc_id", "")),
		"npc_name": String(payload.get("npc_name", "")),
		"service_type": String(payload.get("service_type", "")),
		"panel_kind": "contract_board",
		"show_member_selector": false,
		"confirm_label": "确认操作",
		"cancel_label": "返回据点",
		"entry_title": "可选契约",
		"summary_title": "任务板概况",
		"state_title": "契约状态",
		"cost_title": "契约奖励",
		"details_title": "契约说明",
		"member_title": "执行成员",
		"empty_state_label": "状态：暂无契约",
		"empty_cost_label": "奖励：无",
		"empty_details_text": "当前没有可查看契约。",
		"entries": entries,
	}


func _build_contract_board_entries(interaction_script_id: String) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	var normalized_interaction_id := interaction_script_id.strip_edges()
	var quest_defs := _get_quest_defs()
	var quest_ids: Array[StringName] = []
	for quest_key_variant in quest_defs.keys():
		if typeof(quest_key_variant) != TYPE_STRING_NAME:
			continue
		quest_ids.append(ProgressionDataUtils.to_string_name(quest_key_variant))
	quest_ids.sort_custom(func(a: StringName, b: StringName) -> bool:
		return String(a) < String(b)
	)
	for quest_id in quest_ids:
		var quest_variant = quest_defs.get(quest_id)
		var quest_entry := _build_contract_board_entry(quest_variant, normalized_interaction_id)
		if not quest_entry.is_empty():
			entries.append(quest_entry)
	if entries.is_empty():
		var missing_provider_text := "当前没有 provider_interaction_id 绑定到 %s 的任务定义。" % normalized_interaction_id
		if normalized_interaction_id.is_empty():
			missing_provider_text = "当前任务板缺少 interaction_script_id，无法匹配 provider_interaction_id。"
		entries.append({
			"entry_id": "placeholder",
			"display_name": "当前暂无可展示契约",
			"summary_text": "任务定义尚未挂到这块任务板上。",
			"details_text": missing_provider_text,
			"state_id": "empty",
			"state_label": "状态：空",
			"cost_label": "奖励：无",
			"is_enabled": false,
			"disabled_reason": "暂无可查看任务。",
		})
	return entries


func _build_contract_board_entry(quest_variant, interaction_script_id: String) -> Dictionary:
	var quest_data := _normalize_contract_board_quest_data(quest_variant)
	if quest_data.is_empty():
		return {}
	var provider_interaction_id := String(quest_data.get("provider_interaction_id", "")).strip_edges()
	if provider_interaction_id.is_empty() or provider_interaction_id != interaction_script_id:
		return {}
	var quest_id := ProgressionDataUtils.to_string_name(quest_data.get("quest_id", ""))
	if quest_id == &"":
		return {}
	var state_id := _resolve_contract_board_quest_state_id(quest_id, quest_data)
	return {
		"entry_id": String(quest_id),
		"quest_id": String(quest_id),
		"provider_interaction_id": provider_interaction_id,
		"display_name": String(quest_data["display_name"]),
		"summary_text": _build_contract_board_objective_summary(quest_id, quest_data),
		"details_text": _build_contract_board_entry_details(quest_id, quest_data),
		"state_id": state_id,
		"state_label": _build_contract_board_state_label(state_id),
		"cost_label": _build_contract_board_reward_label(quest_data["reward_entries"]),
		"is_enabled": true,
		"disabled_reason": "",
		"is_repeatable": bool(quest_data.get("is_repeatable", false)),
	}


func _resolve_contract_board_quest_state_id(quest_id: StringName, quest_data: Dictionary = {}) -> String:
	var party_state = _get_party_state()
	if party_state == null:
		return "available"
	if party_state.has_method("get_active_quest_state") and party_state.get_active_quest_state(quest_id) != null:
		return "active"
	if party_state.has_method("has_claimable_quest") and party_state.has_claimable_quest(quest_id):
		return "claimable"
	if party_state.has_method("has_completed_quest") and party_state.has_completed_quest(quest_id):
		if bool(quest_data.get("is_repeatable", false)):
			return "repeatable"
		return "completed"
	return "available"


func _build_contract_board_state_label(state_id: String) -> String:
	match state_id:
		"active":
			return "状态：进行中"
		"claimable":
			return "状态：待领奖励"
		"repeatable":
			return "状态：可重复接取"
		"completed":
			return "状态：已完成"
		"empty":
			return "状态：空"
		_:
			return "状态：待接取"


func _build_contract_board_state_summary(entries: Array[Dictionary]) -> String:
	var active_count := 0
	var available_count := 0
	var claimable_count := 0
	var repeatable_count := 0
	var completed_count := 0
	for entry in entries:
		match String(entry.get("state_id", "")):
			"active":
				active_count += 1
			"claimable":
				claimable_count += 1
			"repeatable":
				repeatable_count += 1
			"completed":
				completed_count += 1
			"empty":
				pass
			_:
				available_count += 1
	var parts := PackedStringArray([
		"进行中 %d" % active_count,
		"待接取 %d" % available_count,
	])
	if claimable_count > 0:
		parts.append("待领奖励 %d" % claimable_count)
	if repeatable_count > 0:
		parts.append("可重复 %d" % repeatable_count)
	parts.append("已完成 %d" % completed_count)
	return "  |  ".join(parts)


func _build_contract_board_objective_summary(quest_id: StringName, quest_data: Dictionary) -> String:
	var objective_lines := _build_contract_board_objective_lines(quest_id, quest_data)
	return "目标：" + " / ".join(PackedStringArray(objective_lines))


func _build_contract_board_entry_details(quest_id: StringName, quest_data: Dictionary) -> String:
	var lines := PackedStringArray([
		String(quest_data["description"]),
		_build_contract_board_objective_summary(quest_id, quest_data),
		_build_contract_board_reward_label(quest_data["reward_entries"]),
	])
	if bool(quest_data.get("is_repeatable", false)):
		lines.append("说明：该契约完成后可再次接取。")
	return "\n".join(lines)


func _build_contract_board_objective_lines(quest_id: StringName, quest_data: Dictionary) -> Array[String]:
	var objective_lines: Array[String] = []
	var objective_defs_variant = quest_data["objective_defs"]
	var quest_state = _get_active_quest_state(quest_id)
	var state_id := _resolve_contract_board_quest_state_id(quest_id, quest_data)
	var is_completed := _is_contract_board_completed_state(state_id)
	for objective_variant in objective_defs_variant:
		var objective_data := objective_variant as Dictionary
		var objective_id := ProgressionDataUtils.to_string_name(objective_data["objective_id"])
		var target_value := int(objective_data["target_value"])
		var current_value: int = target_value if is_completed else 0
		if not is_completed and quest_state != null:
			current_value = int(quest_state.get_objective_progress(objective_id))
		objective_lines.append("%s %d/%d" % [
			_describe_contract_board_objective(objective_data),
			current_value,
			target_value,
		])
	return objective_lines


func _describe_contract_board_objective(objective_data: Dictionary) -> String:
	var objective_type := ProgressionDataUtils.to_string_name(objective_data["objective_type"])
	var target_id := String(objective_data["target_id"])
	match objective_type:
		&"settlement_action":
			return "据点事务 %s" % target_id
		&"defeat_enemy":
			return "击败敌对遭遇"
		&"submit_item":
			var item_id := ProgressionDataUtils.to_string_name(target_id)
			return "提交物资 %s" % _get_item_display_name(item_id)
		_:
			return ""


func _build_contract_board_reward_label(reward_entries: Array) -> String:
	var reward_parts: Array[String] = []
	for reward_variant in reward_entries:
		var reward_data := reward_variant as Dictionary
		var reward_type := ProgressionDataUtils.to_string_name(reward_data["reward_type"])
		match reward_type:
			&"gold":
				reward_parts.append("%d 金" % int(reward_data["amount"]))
			&"item":
				var reward_item_id := QUEST_DEF_SCRIPT.get_reward_item_id(reward_data)
				var reward_quantity := QUEST_DEF_SCRIPT.get_reward_quantity(reward_data)
				reward_parts.append("%s x%d" % [
					_get_item_display_name(reward_item_id),
					reward_quantity,
				])
			&"pending_character_reward":
				reward_parts.append("角色奖励")
			_:
				pass
	return "奖励：%s" % "、".join(PackedStringArray(reward_parts))


func _get_active_quest_state(quest_id: StringName):
	var party_state = _get_party_state()
	if party_state == null or not party_state.has_method("get_active_quest_state"):
		return null
	return party_state.get_active_quest_state(quest_id)


func _resolve_active_submit_item_objective_id(quest_id: StringName, quest_data: Dictionary) -> StringName:
	var quest_state = _get_active_quest_state(quest_id)
	if quest_state == null:
		return &""
	var objective_defs_variant = quest_data["objective_defs"]
	for objective_variant in objective_defs_variant:
		var objective_data := objective_variant as Dictionary
		if ProgressionDataUtils.to_string_name(objective_data["objective_type"]) != QUEST_DEF_SCRIPT.OBJECTIVE_SUBMIT_ITEM:
			continue
		var objective_id := ProgressionDataUtils.to_string_name(objective_data["objective_id"])
		var target_value := int(objective_data["target_value"])
		if quest_state.is_objective_complete(objective_id, target_value):
			continue
		return objective_id
	return &""


func _quest_has_submit_item_objective(quest_data: Dictionary) -> bool:
	var objective_defs_variant = quest_data["objective_defs"]
	for objective_variant in objective_defs_variant:
		if ProgressionDataUtils.to_string_name((objective_variant as Dictionary)["objective_type"]) == QUEST_DEF_SCRIPT.OBJECTIVE_SUBMIT_ITEM:
			return true
	return false


func _open_shop_modal(settlement_id: String, payload: Dictionary) -> void:
	var settlement_state := _get_or_create_settlement_state(settlement_id)
	settlement_state["world_step"] = _get_world_step()
	var window_data := _shop_service.build_window_data(String(payload.get("interaction_script_id", "")), _get_settlement_record(settlement_id), settlement_state, _get_item_defs(), _get_party_warehouse_service(), _get_party_gold())
	_set_active_settlement_state(settlement_id, settlement_state)
	window_data["settlement_id"] = settlement_id
	window_data["interaction_script_id"] = String(payload.get("interaction_script_id", ""))
	_set_active_shop_context(window_data)
	_set_active_modal_id("shop")
	_update_status("已打开 %s 的商店。" % String(payload.get("facility_name", "据点商店")))


func _open_forge_modal(settlement_id: String, payload: Dictionary) -> void:
	var window_data := _forge_service.build_window_data(
		String(payload.get("interaction_script_id", "")),
		_get_settlement_record(settlement_id),
		payload,
		_get_item_defs(),
		_get_recipe_defs(),
		_get_party_warehouse_service()
	)
	window_data["settlement_id"] = settlement_id
	window_data["interaction_script_id"] = String(payload.get("interaction_script_id", ""))
	window_data["service_payload"] = payload.duplicate(true)
	window_data["member_options"] = _build_member_options()
	var selected_member_id := ProgressionDataUtils.to_string_name(payload.get("member_id", ""))
	if selected_member_id == &"":
		selected_member_id = resolve_default_settlement_member_id()
	window_data["default_member_id"] = String(selected_member_id)
	window_data["selected_member_id"] = String(selected_member_id)
	_set_active_forge_context(window_data)
	_set_active_modal_id("forge")
	_update_status("已打开 %s 的%s窗口。" % [
		String(payload.get("facility_name", "据点工坊")),
		_resolve_forge_service_label(payload),
	])


func _open_stagecoach_modal(settlement_id: String, payload: Dictionary) -> void:
	var settlement := _get_settlement_record(settlement_id)
	_set_active_stagecoach_context({
		"title": "%s · 驿站路线" % String(settlement.get("display_name", "据点")),
		"settlement_id": settlement_id,
		"origin_name": String(settlement.get("display_name", "据点")),
		"interaction_script_id": String(payload.get("interaction_script_id", "")),
		"gold": _get_party_gold(),
		"destinations": _build_stagecoach_destinations(settlement, String(payload.get("interaction_script_id", ""))),
		"feedback_text": "选择一个已访问据点并支付路费后即可启程。",
	})
	_set_active_modal_id("stagecoach")
	_update_status("已打开驿站路线。")


func _refresh_active_shop_context() -> void:
	var context := _get_active_shop_context()
	if context.is_empty():
		return
	var settlement_id := String(context.get("settlement_id", ""))
	var settlement_state := _get_or_create_settlement_state(settlement_id)
	settlement_state["world_step"] = _get_world_step()
	var next_context := _shop_service.build_window_data(String(context.get("interaction_script_id", "")), _get_settlement_record(settlement_id), settlement_state, _get_item_defs(), _get_party_warehouse_service(), _get_party_gold())
	_set_active_settlement_state(settlement_id, settlement_state)
	next_context["settlement_id"] = settlement_id
	next_context["interaction_script_id"] = String(context.get("interaction_script_id", ""))
	_set_active_shop_context(next_context)


func _refresh_active_contract_board_context(feedback_text: String = "") -> void:
	var context := _get_active_contract_board_context()
	if context.is_empty():
		return
	var settlement_id := String(context.get("settlement_id", ""))
	var next_payload := context.duplicate(true)
	if not feedback_text.is_empty():
		next_payload["feedback_text"] = feedback_text
	var next_context := _build_contract_board_window_data(settlement_id, next_payload)
	_set_active_contract_board_context(next_context)


func _refresh_active_forge_context(feedback_text: String = "") -> void:
	var context := _get_active_forge_context()
	if context.is_empty():
		return
	var settlement_id := String(context.get("settlement_id", ""))
	var service_payload: Dictionary = context.get("service_payload", {}).duplicate(true) if context.get("service_payload", {}) is Dictionary else {}
	var next_context := _forge_service.build_window_data(
		String(context.get("interaction_script_id", service_payload.get("interaction_script_id", ""))),
		_get_settlement_record(settlement_id),
		service_payload,
		_get_item_defs(),
		_get_recipe_defs(),
		_get_party_warehouse_service(),
		feedback_text if not feedback_text.is_empty() else String(context.get("feedback_text", ""))
	)
	next_context["settlement_id"] = settlement_id
	next_context["interaction_script_id"] = String(context.get("interaction_script_id", service_payload.get("interaction_script_id", "")))
	next_context["service_payload"] = service_payload
	next_context["member_options"] = context.get("member_options", [])
	next_context["default_member_id"] = String(context.get("default_member_id", service_payload.get("member_id", "")))
	next_context["selected_member_id"] = String(context.get("selected_member_id", next_context.get("default_member_id", "")))
	_set_active_forge_context(next_context)


func _build_stagecoach_destinations(origin_settlement: Dictionary, interaction_script_id: String) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	var origin_settlement_id := String(origin_settlement.get("settlement_id", ""))
	var origin_coord: Vector2i = origin_settlement.get("origin", Vector2i.ZERO)
	for settlement in _get_all_settlement_records():
		var settlement_id := String(settlement.get("settlement_id", ""))
		if settlement_id.is_empty() or settlement_id == origin_settlement_id:
			continue
		if not bool(_get_or_create_settlement_state(settlement_id).get("visited", false)):
			continue
		var target_coord: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var travel_cost := (absi(target_coord.x - origin_coord.x) + absi(target_coord.y - origin_coord.y)) * STAGECOACH_COST_PER_STEP
		var can_travel := _get_party_gold() >= travel_cost
		entries.append({
			"settlement_id": settlement_id,
			"display_name": String(settlement.get("display_name", settlement_id)),
			"tier_name": String(settlement.get("tier_name", "")),
			"travel_cost": travel_cost,
			"can_travel": can_travel,
			"disabled_reason": "" if can_travel else "金币不足",
			"coord": {"x": target_coord.x, "y": target_coord.y},
			"interaction_script_id": interaction_script_id,
		})
	return entries


func _find_stagecoach_destination(stagecoach_context: Dictionary, settlement_id: String) -> Dictionary:
	for destination_variant in stagecoach_context.get("destinations", []):
		if destination_variant is Dictionary and String(destination_variant.get("settlement_id", "")) == settlement_id:
			return destination_variant
	return {}


func _restore_party_resources(restore_ratio: float, restore_full: bool) -> Dictionary:
	var effects: Dictionary = {}
	var party_state = _get_party_state()
	if party_state == null:
		return effects
	for member_id_variant in party_state.active_member_ids:
		var member_id := ProgressionDataUtils.to_string_name(member_id_variant)
		var member_state = party_state.get_member_state(member_id)
		if member_state == null:
			continue
		var attribute_snapshot = _get_member_attribute_snapshot(member_id)
		var hp_max := int(attribute_snapshot.get_value(&"hp_max")) if attribute_snapshot != null else maxi(member_state.current_hp, 1)
		var mp_max := int(attribute_snapshot.get_value(&"mp_max")) if attribute_snapshot != null else maxi(member_state.current_mp, 0)
		var old_hp := int(member_state.current_hp)
		var old_mp := int(member_state.current_mp)
		var recovery_multiplier := 1.0
		if attribute_snapshot != null and int(attribute_snapshot.get_value(LOW_LUCK_RELIC_RULES_SCRIPT.ATTR_BLOOD_DEBT_SHAWL)) > 0:
			recovery_multiplier = LOW_LUCK_RELIC_RULES_SCRIPT.BLOOD_DEBT_RECOVERY_MULTIPLIER
		var hp_restore_amount := hp_max - old_hp if restore_full else int(ceil(float(hp_max) * restore_ratio))
		hp_restore_amount = int(ceil(float(maxi(hp_restore_amount, 0)) * recovery_multiplier))
		member_state.current_hp = mini(old_hp + hp_restore_amount, hp_max)
		var mp_restore_amount := mp_max - old_mp if restore_full else 0
		mp_restore_amount = int(ceil(float(maxi(mp_restore_amount, 0)) * recovery_multiplier))
		member_state.current_mp = mini(old_mp + mp_restore_amount, mp_max)
		effects[String(member_id)] = {"hp_restored": maxi(int(member_state.current_hp) - old_hp, 0), "mp_restored": maxi(int(member_state.current_mp) - old_mp, 0)}
	return effects


func _reveal_world_fog(center: Vector2i, reveal_range: int) -> Array[Vector2i]:
	var fog_system = _get_fog_system()
	var revealed_coords: Array[Vector2i] = fog_system.reveal_diamond(center, reveal_range, _get_player_faction_id()) if fog_system != null else []
	if not revealed_coords.is_empty():
		_refresh_world_visibility()
	return revealed_coords


func _mark_settlement_visited(settlement_id: String) -> void:
	var settlement_state := _get_or_create_settlement_state(settlement_id)
	if bool(settlement_state.get("visited", false)):
		return
	settlement_state["visited"] = true
	_set_active_settlement_state(settlement_id, settlement_state)


func _get_or_create_settlement_state(settlement_id: String) -> Dictionary:
	var settlement_state := _get_settlement_state(settlement_id)
	if settlement_state.is_empty():
		settlement_state = {"visited": false, "reputation": 0, "active_conditions": [], "cooldowns": {}, "shop_inventory_seed": TRUE_RANDOM_SEED_SERVICE_SCRIPT.generate_seed(), "shop_last_refresh_step": 0, "shop_states": {}}
		_set_active_settlement_state(settlement_id, settlement_state)
	return settlement_state


func _finalize_successful_action(action_id: String, payload: Dictionary, result: Dictionary) -> Dictionary:
	_enqueue_pending_character_rewards(_result_pending_character_rewards(result))
	_apply_quest_progress_events(_result_quest_progress_events(result))
	var member_id := ProgressionDataUtils.to_string_name(payload.get("member_id", ""))
	if member_id != &"":
		_notify_misfortune_guidance_of_forge_result(member_id, result)
		_record_member_achievement_event(member_id, &"settlement_action_completed", 1, ProgressionDataUtils.to_string_name(action_id))
	_sync_party_state_from_character_management()
	return _persist_changes(bool(result.get("persist_party_state", true)), bool(result.get("persist_world_data", false)), bool(result.get("persist_player_coord", false)))


func _notify_misfortune_guidance_of_forge_result(member_id: StringName, result: Dictionary) -> void:
	if member_id == &"" or _runtime == null or result.is_empty():
		return
	var inventory_delta_variant: Variant = result.get("inventory_delta", {})
	if inventory_delta_variant is not Dictionary:
		return
	if ProgressionDataUtils.to_string_name((inventory_delta_variant as Dictionary).get("recipe_id", "")) == &"":
		return
	if _runtime.has_method("handle_misfortune_forge_result"):
		_runtime.handle_misfortune_forge_result(member_id, result)


func _build_settlement_service_result(
	success: bool,
	message: String,
	pending_character_rewards: Array = [],
	persist_party_state: bool = false,
	persist_world_data: bool = false,
	persist_player_coord: bool = false,
	gold_delta: int = 0,
	inventory_delta: Dictionary = {},
	quest_progress_events: Array = [],
	service_side_effects: Dictionary = {}
) -> Dictionary:
	var result := SETTLEMENT_SERVICE_RESULT_SCRIPT.new()
	result.success = success
	result.message = message
	result.persist_party_state = persist_party_state
	result.persist_world_data = persist_world_data
	result.persist_player_coord = persist_player_coord
	result.gold_delta = gold_delta
	result.inventory_delta = inventory_delta.duplicate(true) if inventory_delta is Dictionary else {}
	result.pending_character_rewards = _duplicate_dictionary_array(pending_character_rewards)
	result.quest_progress_events = _duplicate_dictionary_array(quest_progress_events)
	result.service_side_effects = service_side_effects.duplicate(true) if service_side_effects is Dictionary else {}
	return result.to_dictionary()


func _result_pending_character_rewards(result: Dictionary) -> Array:
	var rewards_variant = result.get("pending_character_rewards", [])
	return rewards_variant if rewards_variant is Array else []


func _result_quest_progress_events(result: Dictionary) -> Array:
	var events_variant = result.get("quest_progress_events", [])
	return events_variant if events_variant is Array else []


func _extract_quest_progress_events(payload: Dictionary, action_id: String, settlement_id: String) -> Array[Dictionary]:
	var quest_progress_events := _duplicate_dictionary_array(payload.get("quest_progress_events", []))
	var world_step := _get_world_step()
	for event_data in quest_progress_events:
		if not event_data.has("world_step"):
			event_data["world_step"] = world_step
	if not bool(payload.get("emit_default_quest_progress_event", true)):
		return quest_progress_events
	var default_event := {
		"event_type": "progress",
		"objective_type": "settlement_action",
		"target_id": action_id,
		"progress_delta": 1,
		"world_step": world_step,
		"action_id": action_id,
		"settlement_id": settlement_id,
		"member_id": String(payload.get("member_id", "")),
	}
	quest_progress_events.append(default_event)
	return quest_progress_events


func _apply_quest_progress_events(event_variants: Array) -> void:
	if not _has_runtime() or event_variants.is_empty():
		return
	_runtime.apply_quest_progress_events_to_party(event_variants, "settlement")


func _duplicate_dictionary_array(value) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if value is not Array:
		return result
	for entry_variant in value:
		if entry_variant is Dictionary:
			result.append((entry_variant as Dictionary).duplicate(true))
	return result


func _build_member_effect_value_map(member_effects: Dictionary, value_key: String) -> Dictionary:
	var values: Dictionary = {}
	for member_id_variant in member_effects.keys():
		var member_id := String(member_id_variant)
		var effect_variant = member_effects.get(member_id_variant, {})
		if effect_variant is not Dictionary:
			continue
		var effect: Dictionary = effect_variant
		values[member_id] = int(effect.get(value_key, 0))
	return values


func _persist_changes(persist_party_state: bool, persist_world_data: bool, persist_player_coord: bool) -> Dictionary:
	var party_error := OK
	var world_error := OK
	var player_error := OK
	if persist_party_state:
		party_error = int(_persist_party_state())
	if persist_world_data:
		world_error = int(_persist_world_data())
	if persist_player_coord:
		player_error = int(_persist_player_coord())
	return {"ok": party_error == OK and world_error == OK and player_error == OK, "party_error": party_error, "world_error": world_error, "player_error": player_error}


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


func _is_battle_active() -> bool:
	if not _has_runtime():
		return false
	return _runtime.is_battle_active()


func _update_status(message: String) -> void:
	if _has_runtime():
		_runtime.update_status(message)


func _get_active_settlement_id() -> String:
	if not _has_runtime():
		return ""
	return _runtime.get_active_settlement_id()


func _set_active_settlement_id(settlement_id: String) -> void:
	if _has_runtime():
		_runtime.set_active_settlement_id(settlement_id)


func _set_settlement_feedback_text(feedback_text: String) -> void:
	if _has_runtime():
		_runtime.set_settlement_feedback_text(feedback_text)


func _get_settlement_feedback_text() -> String:
	if not _has_runtime() or not _runtime.has_method("get_settlement_feedback_text"):
		return ""
	return String(_runtime.get_settlement_feedback_text())


func _get_selected_settlement() -> Dictionary:
	if not _has_runtime():
		return {}
	return _runtime.get_selected_settlement()


func _get_party_state():
	if not _has_runtime():
		return null
	return _runtime.get_party_state()


func _get_party_gold() -> int:
	var party_state = _get_party_state()
	if party_state == null:
		return 0
	return party_state.get_gold()


func _get_settlement_record(settlement_id: String) -> Dictionary:
	if not _has_runtime():
		return {}
	return _runtime.get_settlement_record(settlement_id)


func _get_all_settlement_records() -> Array[Dictionary]:
	if not _has_runtime():
		return []
	return _runtime.get_all_settlement_records()


func _get_settlement_state(settlement_id: String) -> Dictionary:
	if not _has_runtime():
		return {}
	return _runtime.get_settlement_state(settlement_id)


func _set_active_settlement_state(settlement_id: String, settlement_state: Dictionary) -> bool:
	if not _has_runtime():
		return false
	return _runtime.set_active_settlement_state(settlement_id, settlement_state)


func _get_party_warehouse_service():
	if not _has_runtime():
		return null
	return _runtime.get_party_warehouse_service()


func _get_item_defs() -> Dictionary:
	if not _has_runtime():
		return {}
	var game_session = _runtime.get_game_session()
	return game_session.get_item_defs() if game_session != null else {}


func _get_item_display_name(item_id: StringName) -> String:
	if not _has_runtime():
		return String(item_id)
	return _runtime.get_item_display_name(item_id)


func _get_recipe_defs() -> Dictionary:
	if not _has_runtime():
		return {}
	var game_session = _runtime.get_game_session()
	return game_session.get_recipe_defs() if game_session != null else {}


func _get_quest_defs() -> Dictionary:
	if not _has_runtime():
		return {}
	var game_session = _runtime.get_game_session()
	return game_session.get_quest_defs() if game_session != null else {}


func _is_forge_modal_submission(payload: Dictionary) -> bool:
	return String(payload.get("submission_source", "")) == "forge"


func _is_contract_board_modal_submission(payload: Dictionary) -> bool:
	var submission_source := String(payload.get("submission_source", "")).strip_edges()
	return submission_source == "contract_board"


func _submit_contract_board_quest_action(settlement_id: String, _action_id: String, payload: Dictionary) -> Dictionary:
	if not _has_runtime():
		return _command_error("运行时尚未初始化。")
	var quest_id := ProgressionDataUtils.to_string_name(payload.get("quest_id", ""))
	if quest_id == &"":
		var missing_id_message := "当前契约条目缺少 quest_id，无法接取。"
		_set_settlement_feedback_text(missing_id_message)
		_refresh_active_contract_board_context(missing_id_message)
		_update_status(missing_id_message)
		return _command_error(missing_id_message)
	var quest_data := _resolve_contract_board_submission_quest_data(quest_id)
	if quest_data.is_empty():
		var missing_quest_message := "当前任务板未找到契约 %s。" % String(quest_id)
		_set_settlement_feedback_text(missing_quest_message)
		_refresh_active_contract_board_context(missing_quest_message)
		_update_status(missing_quest_message)
		return _command_error(missing_quest_message)
	var provider_interaction_id := String(payload.get("provider_interaction_id", "")).strip_edges()
	if provider_interaction_id.is_empty():
		var missing_provider_message := "当前契约条目缺少 provider_interaction_id，无法匹配任务板。"
		_set_settlement_feedback_text(missing_provider_message)
		_refresh_active_contract_board_context(missing_provider_message)
		_update_status(missing_provider_message)
		return _command_error(missing_provider_message)
	var quest_provider_interaction_id := String(quest_data.get("provider_interaction_id", "")).strip_edges()
	if quest_provider_interaction_id != provider_interaction_id:
		var provider_mismatch_message := "契约 %s 不属于当前任务板。" % String(quest_data["display_name"])
		_set_settlement_feedback_text(provider_mismatch_message)
		_refresh_active_contract_board_context(provider_mismatch_message)
		_update_status(provider_mismatch_message)
		return _command_error(provider_mismatch_message)
	var state_id := _resolve_contract_board_quest_state_id(quest_id, quest_data)
	var command_result: Dictionary = {}
	if state_id == "claimable":
		command_result = _runtime.command_claim_quest(quest_id)
	elif state_id == "active":
		var submit_item_objective_id := _resolve_active_submit_item_objective_id(quest_id, quest_data)
		if submit_item_objective_id != &"" or _quest_has_submit_item_objective(quest_data):
			command_result = _runtime.command_submit_quest_item(quest_id, submit_item_objective_id)
		else:
			var allow_reaccept := bool(quest_data.get("is_repeatable", false))
			command_result = _runtime.command_accept_quest(quest_id, allow_reaccept)
	else:
		var allow_reaccept := bool(quest_data.get("is_repeatable", false))
		command_result = _runtime.command_accept_quest(quest_id, allow_reaccept)
	var message := String(command_result.get("message", "任务处理失败。"))
	_set_active_settlement_id(settlement_id)
	_set_active_modal_id("contract_board")
	_set_settlement_feedback_text(message)
	_refresh_active_contract_board_context(message)
	if bool(command_result.get("ok", false)):
		return _command_ok(message)
	return _command_error(message)


func _resolve_contract_board_submission_quest_data(quest_id: StringName) -> Dictionary:
	var quest_defs := _get_quest_defs()
	var quest_variant = _get_string_name_keyed_value(quest_defs, quest_id)
	return _normalize_contract_board_quest_data(quest_variant)


func _get_string_name_keyed_value(values: Dictionary, key: StringName) -> Variant:
	if key == &"":
		return null
	for value_key in values.keys():
		if typeof(value_key) != TYPE_STRING_NAME:
			continue
		if value_key == key:
			return values[value_key]
	return null


func _normalize_contract_board_quest_data(quest_variant) -> Dictionary:
	var quest_data: Dictionary = {}
	if quest_variant is Dictionary:
		quest_data = (quest_variant as Dictionary).duplicate(true)
	elif quest_variant is Object and quest_variant.has_method("to_dict"):
		var quest_data_variant = quest_variant.to_dict()
		if quest_data_variant is Dictionary:
			quest_data = (quest_data_variant as Dictionary).duplicate(true)
	if quest_data.is_empty():
		return {}
	for field_name in ["quest_id", "provider_interaction_id", "display_name", "description"]:
		if not quest_data.has(field_name) or quest_data[field_name] is not String:
			return {}
		var field_value := String(quest_data[field_name]).strip_edges()
		if field_value.is_empty():
			return {}
		quest_data[field_name] = field_value
	var objective_defs := _normalize_contract_board_objective_defs(quest_data)
	if objective_defs.is_empty():
		return {}
	var reward_entries := _normalize_contract_board_reward_entries(quest_data)
	if reward_entries.is_empty():
		return {}
	quest_data["objective_defs"] = objective_defs
	quest_data["reward_entries"] = reward_entries
	return quest_data


func _normalize_contract_board_objective_defs(quest_data: Dictionary) -> Array[Dictionary]:
	var normalized_objectives: Array[Dictionary] = []
	if not quest_data.has("objective_defs") or quest_data["objective_defs"] is not Array:
		return normalized_objectives
	var objective_defs: Array = quest_data["objective_defs"]
	if objective_defs.is_empty():
		return normalized_objectives
	var seen_objective_ids := {}
	for objective_variant in objective_defs:
		if objective_variant is not Dictionary:
			return []
		var objective_data: Dictionary = (objective_variant as Dictionary).duplicate(true)
		var objective_id := _read_contract_board_required_string_name(objective_data, "objective_id")
		if objective_id == &"" or seen_objective_ids.has(objective_id):
			return []
		seen_objective_ids[objective_id] = true
		var objective_type := _read_contract_board_required_string_name(objective_data, "objective_type")
		if objective_type == &"":
			return []
		if not objective_data.has("target_id"):
			return []
		var target_id_variant: Variant = objective_data["target_id"]
		var target_id_type := typeof(target_id_variant)
		if target_id_type != TYPE_STRING and target_id_type != TYPE_STRING_NAME:
			return []
		var target_id := ProgressionDataUtils.to_string_name(target_id_variant)
		if not objective_data.has("target_value") or objective_data["target_value"] is not int:
			return []
		var target_value := int(objective_data["target_value"])
		if target_value <= 0:
			return []
		match objective_type:
			QUEST_DEF_SCRIPT.OBJECTIVE_SETTLEMENT_ACTION:
				if target_id == &"":
					return []
			QUEST_DEF_SCRIPT.OBJECTIVE_SUBMIT_ITEM:
				if target_id == &"":
					return []
			QUEST_DEF_SCRIPT.OBJECTIVE_DEFEAT_ENEMY:
				pass
			_:
				return []
		objective_data["objective_id"] = String(objective_id)
		objective_data["objective_type"] = String(objective_type)
		objective_data["target_id"] = String(target_id)
		objective_data["target_value"] = target_value
		normalized_objectives.append(objective_data)
	return normalized_objectives


func _normalize_contract_board_reward_entries(quest_data: Dictionary) -> Array[Dictionary]:
	var normalized_rewards: Array[Dictionary] = []
	if not quest_data.has("reward_entries") or quest_data["reward_entries"] is not Array:
		return normalized_rewards
	var reward_entries: Array = quest_data["reward_entries"]
	if reward_entries.is_empty():
		return normalized_rewards
	for reward_variant in reward_entries:
		if reward_variant is not Dictionary:
			return []
		var reward_data: Dictionary = (reward_variant as Dictionary).duplicate(true)
		var reward_type := _read_contract_board_required_string_name(reward_data, "reward_type")
		if reward_type == &"":
			return []
		match reward_type:
			QUEST_DEF_SCRIPT.REWARD_GOLD:
				if not reward_data.has("amount") or reward_data["amount"] is not int:
					return []
				var gold_amount := int(reward_data["amount"])
				if gold_amount <= 0:
					return []
				reward_data["amount"] = gold_amount
			QUEST_DEF_SCRIPT.REWARD_ITEM:
				var reward_item_id := _read_contract_board_required_string_name(reward_data, "item_id")
				if reward_item_id == &"":
					return []
				if not reward_data.has("quantity") or reward_data["quantity"] is not int:
					return []
				var reward_quantity := int(reward_data["quantity"])
				if reward_quantity <= 0:
					return []
				reward_data["item_id"] = String(reward_item_id)
				reward_data["quantity"] = reward_quantity
			QUEST_DEF_SCRIPT.REWARD_PENDING_CHARACTER_REWARD:
				if not _is_contract_board_pending_character_reward_valid(reward_data):
					return []
			_:
				return []
		reward_data["reward_type"] = String(reward_type)
		normalized_rewards.append(reward_data)
	return normalized_rewards


func _is_contract_board_pending_character_reward_valid(reward_data: Dictionary) -> bool:
	if _read_contract_board_required_string_name(reward_data, "member_id") == &"":
		return false
	if not reward_data.has("entries") or reward_data["entries"] is not Array:
		return false
	var entries: Array = reward_data["entries"]
	if entries.is_empty():
		return false
	for entry_variant in entries:
		if entry_variant is not Dictionary:
			return false
		var entry_data := entry_variant as Dictionary
		if _read_contract_board_required_string_name(entry_data, "entry_type") == &"":
			return false
		if _read_contract_board_required_string_name(entry_data, "target_id") == &"":
			return false
		if not entry_data.has("amount") or entry_data["amount"] is not int or int(entry_data["amount"]) == 0:
			return false
	return true


func _read_contract_board_required_string_name(data: Dictionary, field_name: String) -> StringName:
	if not data.has(field_name):
		return &""
	var value: Variant = data[field_name]
	var value_type := typeof(value)
	if value_type != TYPE_STRING and value_type != TYPE_STRING_NAME:
		return &""
	return ProgressionDataUtils.to_string_name(value)


func _is_contract_board_completed_state(state_id: String) -> bool:
	return state_id == "claimable" or state_id == "completed" or state_id == "repeatable"


func _is_forge_interaction(interaction_script_id: String) -> bool:
	return _forge_service != null and _forge_service.is_supported_interaction(interaction_script_id)


func _is_research_interaction(interaction_script_id: String) -> bool:
	return _research_service != null and _research_service.is_supported_interaction(interaction_script_id)


func _build_forge_unavailable_reason(interaction_script_id: String) -> String:
	return "当前没有可用重铸配方" if interaction_script_id == "service_master_reforge" else "当前没有可用锻造配方"


func _resolve_forge_service_label(payload: Dictionary) -> String:
	var service_type := String(payload.get("service_type", "")).strip_edges()
	if not service_type.is_empty():
		return service_type
	return "大师重铸" if String(payload.get("interaction_script_id", "")).strip_edges() == "service_master_reforge" else "锻造"


func _get_member_attribute_snapshot(member_id: StringName):
	if not _has_runtime():
		return null
	return _runtime.get_member_attribute_snapshot(member_id)


func _get_member_display_name(member_id: StringName) -> String:
	if not _has_runtime():
		return String(member_id)
	return _runtime.get_member_display_name(member_id)


func _open_party_warehouse_window(entry_label: String) -> void:
	if _has_runtime():
		_runtime.open_party_warehouse_window(entry_label)


func _enqueue_pending_character_rewards(reward_variants: Array) -> void:
	if _has_runtime():
		_runtime.enqueue_pending_character_rewards(reward_variants)


func _record_member_achievement_event(member_id: StringName, event_id: StringName, value: int, detail_id: StringName = &"") -> void:
	if _has_runtime():
		_runtime.record_member_achievement_event(member_id, event_id, value, detail_id)


func _sync_party_state_from_character_management() -> void:
	if _has_runtime():
		_runtime.sync_party_state_from_character_management()


func _persist_party_state() -> int:
	if not _has_runtime():
		return ERR_UNAVAILABLE
	return int(_runtime.persist_party_state())


func _persist_world_data() -> int:
	if not _has_runtime():
		return ERR_UNAVAILABLE
	return int(_runtime.persist_world_data())


func _persist_player_coord() -> int:
	if not _has_runtime():
		return ERR_UNAVAILABLE
	return int(_runtime.persist_player_coord())


func _get_fog_system():
	if not _has_runtime():
		return null
	return _runtime.get_fog_system()


func _is_settlement_visible_to_player(settlement: Dictionary) -> bool:
	var fog_system = _get_fog_system()
	if fog_system == null:
		return false
	var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
	var footprint_size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
	var width := maxi(footprint_size.x, 1)
	var height := maxi(footprint_size.y, 1)
	var faction_id := _get_player_faction_id()
	for y in range(height):
		for x in range(width):
			if fog_system.is_visible(origin + Vector2i(x, y), faction_id):
				return true
	return false


func _get_player_faction_id() -> String:
	if not _has_runtime():
		return "player"
	return _runtime.get_player_faction_id()


func _advance_world_time_by_steps(delta_steps: int) -> void:
	if _has_runtime():
		_runtime.advance_world_time_by_steps(delta_steps)


func _refresh_world_visibility() -> void:
	if _has_runtime():
		_runtime.refresh_world_visibility()


func _get_world_step() -> int:
	if not _has_runtime():
		return 0
	return int(_runtime.get_world_step())


func _set_player_coord(coord: Vector2i) -> void:
	if _has_runtime():
		_runtime.set_player_coord(coord)


func _set_selected_coord(coord: Vector2i) -> void:
	if _has_runtime():
		_runtime.set_selected_coord(coord)


func _clear_settlement_entry_context(reset_selected: bool = true) -> void:
	if _has_runtime():
		_runtime.clear_settlement_entry_context(reset_selected)


func _get_active_modal_id() -> String:
	if not _has_runtime():
		return ""
	return _runtime.get_active_modal_id()


func _set_active_modal_id(modal_id: String) -> void:
	if _has_runtime():
		_runtime.set_runtime_active_modal_id(modal_id)


func _present_pending_reward_if_ready() -> bool:
	if not _has_runtime():
		return false
	return _runtime.present_pending_reward_if_ready()


func _set_active_shop_context(context: Dictionary) -> void:
	if _has_runtime():
		_runtime.set_active_shop_context(context)


func _set_active_contract_board_context(context: Dictionary) -> void:
	if _has_runtime():
		_runtime.set_active_contract_board_context(context)


func _set_active_forge_context(context: Dictionary) -> void:
	if _has_runtime():
		_runtime.set_active_forge_context(context)


func _clear_active_shop_context() -> void:
	if _has_runtime():
		_runtime.clear_active_shop_context()


func _clear_active_contract_board_context() -> void:
	if _has_runtime():
		_runtime.clear_active_contract_board_context()


func _clear_active_forge_context() -> void:
	if _has_runtime():
		_runtime.clear_active_forge_context()


func _get_active_shop_context() -> Dictionary:
	if not _has_runtime():
		return {}
	return _runtime.get_active_shop_context()


func _get_active_contract_board_context() -> Dictionary:
	if not _has_runtime():
		return {}
	return _runtime.get_active_contract_board_context()


func _get_active_forge_context() -> Dictionary:
	if not _has_runtime():
		return {}
	return _runtime.get_active_forge_context()


func _set_active_stagecoach_context(context: Dictionary) -> void:
	if _has_runtime():
		_runtime.set_active_stagecoach_context(context)


func _clear_active_stagecoach_context() -> void:
	if _has_runtime():
		_runtime.clear_active_stagecoach_context()


func _get_active_stagecoach_context() -> Dictionary:
	if not _has_runtime():
		return {}
	return _runtime.get_active_stagecoach_context()
