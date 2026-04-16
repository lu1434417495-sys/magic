class_name GameRuntimeSettlementCommandHandler
extends RefCounted

const SETTLEMENT_SHOP_SERVICE_SCRIPT = preload("res://scripts/systems/settlement_shop_service.gd")
const SETTLEMENT_FORGE_SERVICE_SCRIPT = preload("res://scripts/systems/settlement_forge_service.gd")
const SETTLEMENT_SERVICE_RESULT_SCRIPT = preload("res://scripts/systems/settlement_service_result.gd")

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
}

const UNIMPLEMENTED_INTERACTION_IDS := {
	"service_join_guild": true,
	"service_identify_relic": true,
	"service_bounty_registry": true,
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

var _runtime_ref: WeakRef = null
var _runtime = null:
	get:
		return _runtime_ref.get_ref() if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null

var _shop_service = SETTLEMENT_SHOP_SERVICE_SCRIPT.new()
var _forge_service = SETTLEMENT_FORGE_SERVICE_SCRIPT.new()


func setup(runtime) -> void:
	_runtime = runtime


func dispose() -> void:
	_runtime = null
	_shop_service = SETTLEMENT_SHOP_SERVICE_SCRIPT.new()
	_forge_service = SETTLEMENT_FORGE_SERVICE_SCRIPT.new()


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
		"footprint_size": settlement.get("footprint_size", Vector2i.ONE),
		"faction_id": settlement.get("faction_id", "neutral"),
		"facilities": settlement.get("facilities", []),
		"available_services": _build_service_entries(settlement, settlement_state),
		"service_npcs": settlement.get("service_npcs", []),
		"member_options": _build_member_options(),
		"default_member_id": String(resolve_default_settlement_member_id()),
		"state_summary_text": _build_settlement_state_summary(settlement_state),
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
	var merged_payload := build_settlement_action_payload(settlement_id, action_id, payload)
	merged_payload["action_id"] = action_id
	on_settlement_action_requested(settlement_id, action_id, merged_payload)
	return _command_ok()


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


func command_shop_sell(item_id: StringName, quantity: int) -> Dictionary:
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
		quantity
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
	if not _has_runtime():
		return
	var interaction_script_id := String(payload.get("interaction_script_id", ""))
	if interaction_script_id == _runtime.PARTY_WAREHOUSE_INTERACTION_ID:
		_finalize_successful_action(action_id, payload, {
			"persist_party_state": true,
			"quest_progress_events": _extract_quest_progress_events(payload, action_id, settlement_id),
		})
		_set_active_settlement_id(settlement_id)
		_set_active_modal_id("")
		_open_party_warehouse_window("据点服务：%s·%s" % [
			String(payload.get("facility_name", "设施")),
			String(payload.get("npc_name", "值守人员")),
		])
		_update_status("已从据点服务打开共享仓库。")
		return
	if CONTRACT_BOARD_INTERACTION_IDS.has(interaction_script_id):
		_open_contract_board_modal(settlement_id, payload)
		return
	if SHOP_INTERACTION_IDS.has(interaction_script_id):
		_open_shop_modal(settlement_id, payload)
		return
	if _is_forge_interaction(interaction_script_id) and not _is_forge_modal_submission(payload):
		_open_forge_modal(settlement_id, payload)
		return
	if STAGECOACH_INTERACTION_IDS.has(interaction_script_id):
		_open_stagecoach_modal(settlement_id, payload)
		return
	var result := execute_settlement_action(settlement_id, action_id, payload)
	var message := String(result.get("message", "交互已完成。"))
	_set_settlement_feedback_text(message)
	if _is_forge_interaction(interaction_script_id):
		_refresh_active_forge_context(message)
		if bool(result.get("success", false)):
			var forge_persist_result := _finalize_successful_action(action_id, payload, result)
			if bool(forge_persist_result.get("ok", false)):
				_update_status(message)
			else:
				_update_status("%s 但队伍或据点状态持久化失败。" % message)
			return
		_update_status(message)
		return
	if bool(result.get("success", false)):
		var persist_result := _finalize_successful_action(action_id, payload, result)
		if bool(persist_result.get("ok", false)):
			_update_status(message)
		else:
			_update_status("%s 但队伍或据点状态持久化失败。" % message)
		return
	_update_status(message)


func on_settlement_window_closed() -> void:
	if not _has_runtime():
		return
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
	var payload: Dictionary = {}
	for service_variant in get_settlement_window_data(settlement_id).get("available_services", []):
		if service_variant is not Dictionary:
			continue
		var service_data: Dictionary = service_variant
		if String(service_data.get("action_id", "")) != action_id:
			continue
		payload = {
			"action_id": action_id,
			"facility_id": service_data.get("facility_id", ""),
			"facility_name": service_data.get("facility_name", ""),
			"npc_id": service_data.get("npc_id", ""),
			"npc_name": service_data.get("npc_name", ""),
			"service_type": service_data.get("service_type", ""),
			"interaction_script_id": service_data.get("interaction_script_id", ""),
		}
		break
	for key in overrides.keys():
		payload[key] = overrides[key]
	if String(payload.get("member_id", "")).is_empty():
		var member_id := resolve_default_settlement_member_id()
		if member_id != &"":
			payload["member_id"] = String(member_id)
	return payload


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
	return rewards


func resolve_default_reward_source_type(action_id: String, service_type: String, payload: Dictionary) -> StringName:
	var explicit_source_type := String(payload.get("reward_source_type", payload.get("mastery_source_type", "")))
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
		false,
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
		service_data["cost_label"] = String(metadata.get("cost_label", ""))
		service_data["is_enabled"] = bool(metadata.get("is_enabled", true))
		service_data["disabled_reason"] = String(metadata.get("disabled_reason", ""))
		entries.append(service_data)
	return entries


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
		options.append({"member_id": String(member_id), "display_name": _get_member_display_name(member_id), "roster_role": "active"})
	for member_id_variant in party_state.reserve_member_ids:
		var member_id := ProgressionDataUtils.to_string_name(member_id_variant)
		if member_id == &"" or seen_member_ids.has(member_id) or party_state.get_member_state(member_id) == null:
			continue
		seen_member_ids[member_id] = true
		options.append({"member_id": String(member_id), "display_name": _get_member_display_name(member_id), "roster_role": "reserve"})
	return options


func _open_contract_board_modal(settlement_id: String, payload: Dictionary) -> void:
	var window_data := _build_contract_board_window_data(settlement_id, payload)
	_set_active_contract_board_context(window_data)
	_set_active_modal_id("contract_board")
	_update_status("已打开 %s 的任务板。" % String(payload.get("facility_name", "据点任务板")))


func _build_contract_board_window_data(settlement_id: String, payload: Dictionary) -> Dictionary:
	var settlement := _get_settlement_record(settlement_id)
	var entries := _build_contract_board_entries(String(payload.get("interaction_script_id", "")))
	return {
		"title": "%s · 任务板" % String(settlement.get("display_name", settlement_id)),
		"meta": "%s · %s · %s" % [
			String(payload.get("facility_name", "任务板")),
			String(payload.get("npc_name", "值守人员")),
			String(payload.get("service_type", "契约")),
		],
		"summary_text": "当前契约板已接入正式 modal；本轮先开放查看链路。",
		"state_summary_text": _build_contract_board_state_summary(entries),
		"service_name": String(payload.get("service_type", "任务板")),
		"settlement_id": settlement_id,
		"action_id": String(payload.get("action_id", "")),
		"interaction_script_id": String(payload.get("interaction_script_id", "")),
		"facility_id": String(payload.get("facility_id", "")),
		"facility_name": String(payload.get("facility_name", "")),
		"npc_id": String(payload.get("npc_id", "")),
		"npc_name": String(payload.get("npc_name", "")),
		"service_type": String(payload.get("service_type", "")),
		"panel_kind": "contract_board",
		"show_member_selector": false,
		"confirm_label": "待开放",
		"cancel_label": "返回据点",
		"entries": entries,
	}


func _build_contract_board_entries(interaction_script_id: String) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	for quest_variant in _get_quest_defs().values():
		var quest_entry := _build_contract_board_entry(quest_variant, interaction_script_id)
		if not quest_entry.is_empty():
			entries.append(quest_entry)
	if entries.is_empty():
		entries.append({
			"entry_id": "placeholder",
			"display_name": "当前暂无可展示契约",
			"summary_text": "任务定义尚未挂到这块任务板上。",
			"details_text": "当前没有 provider_interaction_id 绑定到 %s 的任务定义。" % interaction_script_id,
			"state_id": "empty",
			"state_label": "状态：空",
			"cost_label": "奖励：无",
			"is_enabled": false,
			"disabled_reason": "暂无可查看任务。",
		})
	return entries


func _build_contract_board_entry(quest_variant, interaction_script_id: String) -> Dictionary:
	var quest_data: Dictionary = {}
	if quest_variant is Dictionary:
		quest_data = (quest_variant as Dictionary).duplicate(true)
	elif quest_variant is Object and quest_variant.has_method("to_dict"):
		var quest_data_variant = quest_variant.to_dict()
		if quest_data_variant is Dictionary:
			quest_data = (quest_data_variant as Dictionary).duplicate(true)
	if quest_data.is_empty():
		return {}
	if String(quest_data.get("provider_interaction_id", "")) != interaction_script_id:
		return {}
	var quest_id := ProgressionDataUtils.to_string_name(quest_data.get("quest_id", ""))
	if quest_id == &"":
		return {}
	var state_id := _resolve_contract_board_quest_state_id(quest_id)
	return {
		"entry_id": String(quest_id),
		"quest_id": String(quest_id),
		"display_name": String(quest_data.get("display_name", String(quest_id))),
		"summary_text": _build_contract_board_objective_summary(quest_id, quest_data),
		"details_text": _build_contract_board_entry_details(quest_id, quest_data),
		"state_id": state_id,
		"state_label": _build_contract_board_state_label(state_id),
		"cost_label": _build_contract_board_reward_label(quest_data.get("reward_entries", [])),
		"is_enabled": false,
		"disabled_reason": "任务接取与结算链路待后续 story 开放。",
	}


func _resolve_contract_board_quest_state_id(quest_id: StringName) -> String:
	var party_state = _get_party_state()
	if party_state == null:
		return "available"
	if party_state.has_method("get_active_quest_state") and party_state.get_active_quest_state(quest_id) != null:
		return "active"
	if party_state.has_method("has_completed_quest") and party_state.has_completed_quest(quest_id):
		return "completed"
	return "available"


func _build_contract_board_state_label(state_id: String) -> String:
	match state_id:
		"active":
			return "状态：进行中"
		"completed":
			return "状态：已完成"
		"empty":
			return "状态：空"
		_:
			return "状态：待接取"


func _build_contract_board_state_summary(entries: Array[Dictionary]) -> String:
	var active_count := 0
	var available_count := 0
	var completed_count := 0
	for entry in entries:
		match String(entry.get("state_id", "")):
			"active":
				active_count += 1
			"completed":
				completed_count += 1
			"empty":
				pass
			_:
				available_count += 1
	return "进行中 %d  |  待接取 %d  |  已完成 %d" % [active_count, available_count, completed_count]


func _build_contract_board_objective_summary(quest_id: StringName, quest_data: Dictionary) -> String:
	var objective_lines := _build_contract_board_objective_lines(quest_id, quest_data)
	return "暂无目标说明。" if objective_lines.is_empty() else "目标：" + " / ".join(PackedStringArray(objective_lines))


func _build_contract_board_entry_details(quest_id: StringName, quest_data: Dictionary) -> String:
	var lines := PackedStringArray([
		String(quest_data.get("description", "暂无说明。")),
		_build_contract_board_objective_summary(quest_id, quest_data),
		_build_contract_board_reward_label(quest_data.get("reward_entries", [])),
	])
	return "\n".join(lines)


func _build_contract_board_objective_lines(quest_id: StringName, quest_data: Dictionary) -> Array[String]:
	var objective_lines: Array[String] = []
	var objective_defs_variant = quest_data.get("objective_defs", [])
	if objective_defs_variant is not Array:
		return objective_lines
	var quest_state = _get_active_quest_state(quest_id)
	var is_completed := _resolve_contract_board_quest_state_id(quest_id) == "completed"
	for objective_variant in objective_defs_variant:
		if objective_variant is not Dictionary:
			continue
		var objective_data := objective_variant as Dictionary
		var objective_id := ProgressionDataUtils.to_string_name(objective_data.get("objective_id", ""))
		var target_value := maxi(int(objective_data.get("target_value", 1)), 1)
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
	var objective_type := ProgressionDataUtils.to_string_name(objective_data.get("objective_type", ""))
	var target_id := String(objective_data.get("target_id", ""))
	match objective_type:
		&"settlement_action":
			return "据点事务 %s" % (target_id if not target_id.is_empty() else "未命名")
		&"defeat_enemy":
			return "击败敌对遭遇"
		&"submit_item":
			return "提交物资 %s" % (target_id if not target_id.is_empty() else "未命名")
		_:
			return String(objective_data.get("objective_id", objective_type))


func _build_contract_board_reward_label(reward_entries_variant) -> String:
	if reward_entries_variant is not Array or (reward_entries_variant as Array).is_empty():
		return "奖励：待定"
	var reward_parts: Array[String] = []
	for reward_variant in reward_entries_variant:
		if reward_variant is not Dictionary:
			continue
		var reward_data := reward_variant as Dictionary
		var reward_type := ProgressionDataUtils.to_string_name(reward_data.get("reward_type", ""))
		match reward_type:
			&"gold":
				reward_parts.append("%d 金" % int(reward_data.get("amount", 0)))
			&"item":
				reward_parts.append("%s x%d" % [
					_get_item_display_name(ProgressionDataUtils.to_string_name(reward_data.get("target_id", ""))),
					maxi(int(reward_data.get("amount", 1)), 1),
				])
			&"pending_character_reward":
				reward_parts.append("角色奖励")
			_:
				reward_parts.append(String(reward_type))
	return "奖励：%s" % ("、".join(PackedStringArray(reward_parts)) if not reward_parts.is_empty() else "待定")


func _get_active_quest_state(quest_id: StringName):
	var party_state = _get_party_state()
	if party_state == null or not party_state.has_method("get_active_quest_state"):
		return null
	return party_state.get_active_quest_state(quest_id)


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
		member_state.current_hp = maxi(hp_max, 1) if restore_full else mini(old_hp + int(ceil(float(hp_max) * restore_ratio)), hp_max)
		if restore_full:
			member_state.current_mp = maxi(mp_max, 0)
		effects[String(member_id)] = {"hp_restored": maxi(int(member_state.current_hp) - old_hp, 0), "mp_restored": maxi(int(member_state.current_mp) - old_mp, 0)}
	return effects


func _reveal_world_fog(center: Vector2i, reveal_range: int) -> Array[Vector2i]:
	var fog_system = _get_fog_system()
	return fog_system.reveal_diamond(center, reveal_range, _get_player_faction_id()) if fog_system != null and fog_system.has_method("reveal_diamond") else []


func _mark_settlement_visited(settlement_id: String) -> void:
	var settlement_state := _get_or_create_settlement_state(settlement_id)
	if bool(settlement_state.get("visited", false)):
		return
	settlement_state["visited"] = true
	_set_active_settlement_state(settlement_id, settlement_state)


func _get_or_create_settlement_state(settlement_id: String) -> Dictionary:
	var settlement_state := _get_settlement_state(settlement_id)
	if settlement_state.is_empty():
		settlement_state = {"visited": false, "reputation": 0, "active_conditions": [], "cooldowns": {}, "shop_inventory_seed": 0, "shop_last_refresh_step": 0, "shop_states": {}}
		_set_active_settlement_state(settlement_id, settlement_state)
	return settlement_state


func _finalize_successful_action(action_id: String, payload: Dictionary, result: Dictionary) -> Dictionary:
	_enqueue_pending_character_rewards(_result_pending_character_rewards(result))
	_apply_quest_progress_events(_result_quest_progress_events(result))
	var member_id := ProgressionDataUtils.to_string_name(payload.get("member_id", ""))
	if member_id != &"":
		_record_member_achievement_event(member_id, &"settlement_action_completed", 1, ProgressionDataUtils.to_string_name(action_id))
	_sync_party_state_from_character_management()
	return _persist_changes(bool(result.get("persist_party_state", true)), bool(result.get("persist_world_data", false)), bool(result.get("persist_player_coord", false)))


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
	if not bool(payload.get("emit_default_quest_progress_event", true)):
		return quest_progress_events
	var default_event := {
		"event_type": "progress",
		"objective_type": "settlement_action",
		"target_id": action_id,
		"progress_delta": 1,
		"action_id": action_id,
		"settlement_id": settlement_id,
		"member_id": String(payload.get("member_id", "")),
	}
	quest_progress_events.append(default_event)
	return quest_progress_events


func _apply_quest_progress_events(event_variants: Array) -> void:
	if not _has_runtime() or event_variants.is_empty():
		return
	if _runtime.has_method("apply_quest_progress_events_to_party"):
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
	return _runtime.build_command_ok(message) if _has_runtime() and _runtime.has_method("build_command_ok") else {"ok": true, "message": message, "battle_refresh_mode": ""}


func _command_error(message: String) -> Dictionary:
	if _has_runtime() and _runtime.has_method("build_command_error"):
		return _runtime.build_command_error(message)
	if _has_runtime() and not message.is_empty():
		_update_status(message)
	return {"ok": false, "message": message}


func _is_battle_active() -> bool:
	if not _has_runtime():
		return false
	return _runtime.is_battle_active() if _runtime.has_method("is_battle_active") else (_runtime._is_battle_active() if _runtime.has_method("_is_battle_active") else false)


func _update_status(message: String) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("update_status"):
		_runtime.update_status(message)
	elif _runtime.has_method("_update_status"):
		_runtime._update_status(message)


func _get_active_settlement_id() -> String:
	if not _has_runtime():
		return ""
	if _runtime.has_method("get_active_settlement_id"):
		return _runtime.get_active_settlement_id()
	return String(_runtime._active_settlement_id) if "_active_settlement_id" in _runtime else ""


func _set_active_settlement_id(settlement_id: String) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("set_active_settlement_id"):
		_runtime.set_active_settlement_id(settlement_id)
	elif "_active_settlement_id" in _runtime:
		_runtime._active_settlement_id = settlement_id


func _set_settlement_feedback_text(feedback_text: String) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("set_settlement_feedback_text"):
		_runtime.set_settlement_feedback_text(feedback_text)
	elif "_active_settlement_feedback_text" in _runtime:
		_runtime._active_settlement_feedback_text = feedback_text


func _get_selected_settlement() -> Dictionary:
	if not _has_runtime():
		return {}
	if _runtime.has_method("get_selected_settlement"):
		return _runtime.get_selected_settlement()
	return {}


func _get_party_state():
	if not _has_runtime():
		return null
	if _runtime.has_method("get_party_state"):
		return _runtime.get_party_state()
	return _runtime._party_state if "_party_state" in _runtime else null


func _get_party_gold() -> int:
	var party_state = _get_party_state()
	if party_state == null:
		return 0
	return party_state.get_gold() if party_state.has_method("get_gold") else maxi(int(party_state.gold), 0)


func _get_settlement_record(settlement_id: String) -> Dictionary:
	if not _has_runtime():
		return {}
	if _runtime.has_method("get_settlement_record"):
		return _runtime.get_settlement_record(settlement_id)
	return _runtime._settlements_by_id.get(settlement_id, {}) if "_settlements_by_id" in _runtime else {}


func _get_all_settlement_records() -> Array[Dictionary]:
	if not _has_runtime():
		return []
	if _runtime.has_method("get_all_settlement_records"):
		return _runtime.get_all_settlement_records()
	return []


func _get_settlement_state(settlement_id: String) -> Dictionary:
	if not _has_runtime():
		return {}
	if _runtime.has_method("get_settlement_state"):
		return _runtime.get_settlement_state(settlement_id)
	return _get_settlement_record(settlement_id).get("settlement_state", {}).duplicate(true)


func _set_active_settlement_state(settlement_id: String, settlement_state: Dictionary) -> bool:
	if not _has_runtime():
		return false
	if _runtime.has_method("set_active_settlement_state"):
		return _runtime.set_active_settlement_state(settlement_id, settlement_state)
	return false


func _get_party_warehouse_service():
	if not _has_runtime():
		return null
	if _runtime.has_method("get_party_warehouse_service"):
		return _runtime.get_party_warehouse_service()
	return _runtime._party_warehouse_service if "_party_warehouse_service" in _runtime else null


func _get_item_defs() -> Dictionary:
	if not _has_runtime():
		return {}
	if _runtime.has_method("get_game_session"):
		var game_session = _runtime.get_game_session()
		return game_session.get_item_defs() if game_session != null and game_session.has_method("get_item_defs") else {}
	return _runtime._game_session.get_item_defs() if "_game_session" in _runtime and _runtime._game_session != null else {}


func _get_item_display_name(item_id: StringName) -> String:
	if _has_runtime() and _runtime.has_method("get_item_display_name"):
		return _runtime.get_item_display_name(item_id)
	var item_def = _get_item_defs().get(item_id, null)
	if item_def != null and not String(item_def.display_name).is_empty():
		return String(item_def.display_name)
	return String(item_id)


func _get_recipe_defs() -> Dictionary:
	if not _has_runtime():
		return {}
	if _runtime.has_method("get_game_session"):
		var game_session = _runtime.get_game_session()
		return game_session.get_recipe_defs() if game_session != null and game_session.has_method("get_recipe_defs") else {}
	return _runtime._game_session.get_recipe_defs() if "_game_session" in _runtime and _runtime._game_session != null else {}


func _get_quest_defs() -> Dictionary:
	if not _has_runtime():
		return {}
	if _runtime.has_method("get_game_session"):
		var game_session = _runtime.get_game_session()
		return game_session.get_quest_defs() if game_session != null and game_session.has_method("get_quest_defs") else {}
	return _runtime._game_session.get_quest_defs() if "_game_session" in _runtime and _runtime._game_session != null else {}


func _is_forge_modal_submission(payload: Dictionary) -> bool:
	return String(payload.get("submission_source", "")) == "forge"


func _is_forge_interaction(interaction_script_id: String) -> bool:
	return _forge_service != null and _forge_service.is_supported_interaction(interaction_script_id)


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
	if _runtime.has_method("get_member_attribute_snapshot"):
		return _runtime.get_member_attribute_snapshot(member_id)
	return null


func _get_member_display_name(member_id: StringName) -> String:
	if not _has_runtime():
		return String(member_id)
	if _runtime.has_method("get_member_display_name"):
		return _runtime.get_member_display_name(member_id)
	return String(member_id)


func _open_party_warehouse_window(entry_label: String) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("open_party_warehouse_window"):
		_runtime.open_party_warehouse_window(entry_label)
	elif "_warehouse_handler" in _runtime and _runtime._warehouse_handler != null:
		_runtime._warehouse_handler.open_party_warehouse_window(entry_label)


func _enqueue_pending_character_rewards(reward_variants: Array) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("enqueue_pending_character_rewards"):
		_runtime.enqueue_pending_character_rewards(reward_variants)
	elif _runtime.has_method("_enqueue_pending_character_rewards"):
		_runtime._enqueue_pending_character_rewards(reward_variants)


func _record_member_achievement_event(member_id: StringName, event_id: StringName, value: int, detail_id: StringName = &"") -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("record_member_achievement_event"):
		_runtime.record_member_achievement_event(member_id, event_id, value, detail_id)
	elif "_character_management" in _runtime and _runtime._character_management != null:
		_runtime._character_management.record_achievement_event(member_id, event_id, value, detail_id)


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


func _persist_world_data() -> int:
	if not _has_runtime():
		return ERR_UNAVAILABLE
	if _runtime.has_method("persist_world_data"):
		return int(_runtime.persist_world_data())
	return int(_runtime._game_session.set_world_data(_runtime._world_data)) if "_game_session" in _runtime and "_world_data" in _runtime and _runtime._game_session != null else ERR_UNAVAILABLE


func _persist_player_coord() -> int:
	if not _has_runtime():
		return ERR_UNAVAILABLE
	if _runtime.has_method("persist_player_coord"):
		return int(_runtime.persist_player_coord())
	return int(_runtime._game_session.set_player_coord(_runtime._player_coord)) if "_game_session" in _runtime and "_player_coord" in _runtime and _runtime._game_session != null else ERR_UNAVAILABLE


func _get_fog_system():
	if not _has_runtime():
		return null
	if _runtime.has_method("get_fog_system"):
		return _runtime.get_fog_system()
	return _runtime._fog_system if "_fog_system" in _runtime else null


func _get_player_faction_id() -> String:
	if not _has_runtime():
		return "player"
	if _runtime.has_method("get_player_faction_id"):
		return _runtime.get_player_faction_id()
	return String(_runtime._player_faction_id) if "_player_faction_id" in _runtime else "player"


func _advance_world_time_by_steps(delta_steps: int) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("advance_world_time_by_steps"):
		_runtime.advance_world_time_by_steps(delta_steps)
	elif _runtime.has_method("_advance_world_time_by_steps"):
		_runtime._advance_world_time_by_steps(delta_steps)


func _refresh_world_visibility() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("refresh_world_visibility"):
		_runtime.refresh_world_visibility()
	elif _runtime.has_method("_refresh_fog"):
		_runtime._refresh_fog()


func _get_world_step() -> int:
	if not _has_runtime():
		return 0
	if _runtime.has_method("get_world_step"):
		return int(_runtime.get_world_step())
	return 0


func _set_player_coord(coord: Vector2i) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("set_player_coord"):
		_runtime.set_player_coord(coord)
	elif "_player_coord" in _runtime:
		_runtime._player_coord = coord


func _set_selected_coord(coord: Vector2i) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("set_selected_coord"):
		_runtime.set_selected_coord(coord)
	elif "_selected_coord" in _runtime:
		_runtime._selected_coord = coord


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


func _present_pending_reward_if_ready() -> bool:
	if not _has_runtime():
		return false
	if _runtime.has_method("present_pending_reward_if_ready"):
		return _runtime.present_pending_reward_if_ready()
	return _runtime._present_pending_reward_if_ready() if _runtime.has_method("_present_pending_reward_if_ready") else false


func _set_active_shop_context(context: Dictionary) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("set_active_shop_context"):
		_runtime.set_active_shop_context(context)
	elif "_active_shop_context" in _runtime:
		_runtime._active_shop_context = context.duplicate(true)


func _set_active_contract_board_context(context: Dictionary) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("set_active_contract_board_context"):
		_runtime.set_active_contract_board_context(context)
	elif "_active_contract_board_context" in _runtime:
		_runtime._active_contract_board_context = context.duplicate(true)


func _set_active_forge_context(context: Dictionary) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("set_active_forge_context"):
		_runtime.set_active_forge_context(context)
	elif "_active_forge_context" in _runtime:
		_runtime._active_forge_context = context.duplicate(true)


func _clear_active_shop_context() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("clear_active_shop_context"):
		_runtime.clear_active_shop_context()
	elif "_active_shop_context" in _runtime:
		_runtime._active_shop_context.clear()


func _clear_active_contract_board_context() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("clear_active_contract_board_context"):
		_runtime.clear_active_contract_board_context()
	elif "_active_contract_board_context" in _runtime:
		_runtime._active_contract_board_context.clear()


func _clear_active_forge_context() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("clear_active_forge_context"):
		_runtime.clear_active_forge_context()
	elif "_active_forge_context" in _runtime:
		_runtime._active_forge_context.clear()


func _get_active_shop_context() -> Dictionary:
	if not _has_runtime():
		return {}
	if _runtime.has_method("get_active_shop_context"):
		return _runtime.get_active_shop_context()
	return _runtime._active_shop_context.duplicate(true) if "_active_shop_context" in _runtime else {}


func _get_active_contract_board_context() -> Dictionary:
	if not _has_runtime():
		return {}
	if _runtime.has_method("get_active_contract_board_context"):
		return _runtime.get_active_contract_board_context()
	return _runtime._active_contract_board_context.duplicate(true) if "_active_contract_board_context" in _runtime else {}


func _get_active_forge_context() -> Dictionary:
	if not _has_runtime():
		return {}
	if _runtime.has_method("get_active_forge_context"):
		return _runtime.get_active_forge_context()
	return _runtime._active_forge_context.duplicate(true) if "_active_forge_context" in _runtime else {}


func _set_active_stagecoach_context(context: Dictionary) -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("set_active_stagecoach_context"):
		_runtime.set_active_stagecoach_context(context)
	elif "_active_stagecoach_context" in _runtime:
		_runtime._active_stagecoach_context = context.duplicate(true)


func _clear_active_stagecoach_context() -> void:
	if not _has_runtime():
		return
	if _runtime.has_method("clear_active_stagecoach_context"):
		_runtime.clear_active_stagecoach_context()
	elif "_active_stagecoach_context" in _runtime:
		_runtime._active_stagecoach_context.clear()


func _get_active_stagecoach_context() -> Dictionary:
	if not _has_runtime():
		return {}
	if _runtime.has_method("get_active_stagecoach_context"):
		return _runtime.get_active_stagecoach_context()
	return _runtime._active_stagecoach_context.duplicate(true) if "_active_stagecoach_context" in _runtime else {}
