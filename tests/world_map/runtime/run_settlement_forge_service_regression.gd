extends SceneTree

const SettlementForgeService = preload("res://scripts/systems/settlement_forge_service.gd")
const GameRuntimeSettlementCommandHandler = preload("res://scripts/systems/game_runtime_settlement_command_handler.gd")
const GameSessionScript = preload("res://scripts/systems/game_session.gd")
const PartyWarehouseService = preload("res://scripts/systems/party_warehouse_service.gd")
const ItemContentRegistry = preload("res://scripts/player/warehouse/item_content_registry.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")
const UnitProgress = preload("res://scripts/player/progression/unit_progress.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")

const TEST_CONFIG_PATH := "res://data/configs/world_map/test_world_map_config.tres"
const ASHEN_INTERSECTION_CONFIG_PATH := "res://data/configs/world_map/ashen_intersection_world_map_config.tres"

var _failures: Array[String] = []


class MockRuntime:
	extends RefCounted

	const PARTY_WAREHOUSE_INTERACTION_ID := "party_warehouse"

	var _selected_settlement: Dictionary = {}
	var _settlements_by_id: Dictionary = {}
	var _settlement_states: Dictionary = {}
	var _party_state = PartyState.new()
	var _warehouse_service = null
	var _item_defs: Dictionary = {}
	var _active_settlement_id := ""
	var _active_modal_id := "settlement"
	var _active_settlement_feedback_text := ""
	var _active_forge_context: Dictionary = {}
	var _current_status_message := ""
	var persist_calls := 0
	var sync_party_calls := 0
	var present_reward_calls := 0
	var achievement_events: Array[Dictionary] = []
	var applied_quest_event_batches: Array = []

	func build_command_ok(message: String = "", battle_refresh_mode: String = "") -> Dictionary:
		return {
			"ok": true,
			"message": message,
			"battle_refresh_mode": battle_refresh_mode,
		}

	func build_command_error(message: String) -> Dictionary:
		update_status(message)
		return {
			"ok": false,
			"message": message,
		}

	func is_battle_active() -> bool:
		return false

	func get_selected_settlement() -> Dictionary:
		return _selected_settlement.duplicate(true)

	func get_settlement_record(settlement_id: String) -> Dictionary:
		return _settlements_by_id.get(settlement_id, {}).duplicate(true)

	func get_settlement_state(settlement_id: String) -> Dictionary:
		return _settlement_states.get(settlement_id, {}).duplicate(true)

	func set_active_settlement_state(settlement_id: String, settlement_state: Dictionary) -> bool:
		_settlement_states[settlement_id] = settlement_state.duplicate(true)
		return true

	func get_party_state():
		return _party_state

	func get_party_warehouse_service():
		return _warehouse_service

	func get_game_session():
		return self

	func get_item_defs() -> Dictionary:
		return _item_defs

	func get_recipe_defs() -> Dictionary:
		return {
			&"master_reforge_iron_greatsword": load("res://data/configs/recipes/master_reforge_iron_greatsword.tres"),
			&"forge_smith_iron_greatsword": load("res://data/configs/recipes/forge_smith_iron_greatsword.tres"),
		}

	func get_active_settlement_id() -> String:
		return _active_settlement_id

	func set_active_settlement_id(settlement_id: String) -> void:
		_active_settlement_id = settlement_id

	func set_settlement_feedback_text(feedback_text: String) -> void:
		_active_settlement_feedback_text = feedback_text

	func set_runtime_active_modal_id(modal_id: String) -> void:
		_active_modal_id = modal_id

	func set_active_forge_context(context: Dictionary) -> void:
		_active_forge_context = context.duplicate(true)

	func clear_active_forge_context() -> void:
		_active_forge_context.clear()

	func get_active_forge_context() -> Dictionary:
		return _active_forge_context.duplicate(true)

	func update_status(message: String) -> void:
		_current_status_message = message

	func enqueue_pending_character_rewards(_reward_variants: Array) -> void:
		pass

	func apply_quest_progress_events_to_party(event_variants: Array, _source_domain: String = "settlement") -> Dictionary:
		applied_quest_event_batches.append(event_variants.duplicate(true))
		return {
			"accepted_quest_ids": [],
			"progressed_quest_ids": [],
			"completed_quest_ids": [],
		}

	func record_member_achievement_event(
		member_id: StringName,
		event_id: StringName,
		value: int,
		detail_id: StringName = &""
	) -> void:
		achievement_events.append({
			"member_id": String(member_id),
			"event_id": String(event_id),
			"value": value,
			"detail_id": String(detail_id),
		})

	func sync_party_state_from_character_management() -> void:
		sync_party_calls += 1

	func persist_party_state() -> int:
		persist_calls += 1
		return OK

	func present_pending_reward_if_ready() -> bool:
		present_reward_calls += 1
		return false

	func get_member_display_name(member_id: StringName) -> String:
		var member_state = _party_state.get_member_state(member_id)
		return String(member_state.display_name) if member_state != null else String(member_id)


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_master_reforge_service_success()
	_test_master_reforge_service_missing_materials()
	_test_settlement_handler_routes_master_reforge()
	_test_settlement_handler_routes_generic_forge()
	await _test_new_world_generation_exposes_master_reforge_service()
	await _test_ashen_intersection_generation_exposes_generic_forge_service()

	if _failures.is_empty():
		print("Settlement forge service regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Settlement forge service regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_master_reforge_service_success() -> void:
	var item_defs := _load_item_defs()
	var party_state := _build_party_state(6)
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	warehouse_service.add_item(&"bronze_sword", 1)
	warehouse_service.add_item(&"iron_ore", 2)

	var forge_service := SettlementForgeService.new()
	var settlement := _build_settlement_record()
	var payload := _build_reforge_payload()
	var result := forge_service.execute_master_reforge(
		settlement,
		payload,
		item_defs,
		{},
		warehouse_service,
		party_state,
		[
			{
				"event_type": "progress",
				"quest_id": "forge_trial",
				"objective_id": "reforge_once",
				"progress_delta": 1,
				"target_value": 1,
			},
		]
	)

	_assert_true(bool(result.get("success", false)), "重铸服务成功路径应返回 success=true。")
	_assert_true(bool(result.get("persist_party_state", false)), "重铸成功后应要求持久化队伍状态。")
	_assert_eq(warehouse_service.count_item(&"bronze_sword"), 0, "重铸成功后应消耗青铜短剑。")
	_assert_eq(warehouse_service.count_item(&"iron_ore"), 0, "重铸成功后应消耗两份铁矿石。")
	_assert_eq(warehouse_service.count_item(&"iron_greatsword"), 1, "重铸成功后应产出铁制大剑。")
	_assert_true(String(result.get("message", "")).find("铁制大剑") >= 0, "重铸成功文案应包含产出名称。")
	_assert_eq(String((result.get("inventory_delta", {}) as Dictionary).get("recipe_id", "")), "master_reforge_iron_greatsword", "inventory_delta 应记录 recipe_id。")
	_assert_eq((result.get("quest_progress_events", []) as Array).size(), 1, "重铸服务应保留调用方传入的 quest_progress_events。")


func _test_master_reforge_service_missing_materials() -> void:
	var item_defs := _load_item_defs()
	var party_state := _build_party_state(6)
	var warehouse_service := PartyWarehouseService.new()
	warehouse_service.setup(party_state, item_defs)
	warehouse_service.add_item(&"bronze_sword", 1)

	var forge_service := SettlementForgeService.new()
	var result := forge_service.execute_master_reforge(
		_build_settlement_record(),
		_build_reforge_payload(),
		item_defs,
		{},
		warehouse_service,
		party_state
	)

	_assert_true(not bool(result.get("success", true)), "缺少材料时重铸服务应失败。")
	_assert_true(not bool(result.get("persist_party_state", true)), "重铸失败时不应要求持久化队伍状态。")
	_assert_true(String(result.get("message", "")).find("铁矿石") >= 0, "缺少材料时应指出具体短缺材料。")
	_assert_eq(warehouse_service.count_item(&"bronze_sword"), 1, "失败时不应吞掉已有材料。")
	_assert_eq(warehouse_service.count_item(&"iron_greatsword"), 0, "失败时不应提前写入产物。")


func _test_settlement_handler_routes_master_reforge() -> void:
	var item_defs := _load_item_defs()
	var runtime := MockRuntime.new()
	runtime._party_state = _build_party_state(6)
	runtime._warehouse_service = PartyWarehouseService.new()
	runtime._warehouse_service.setup(runtime._party_state, item_defs)
	runtime._warehouse_service.add_item(&"bronze_sword", 1)
	runtime._warehouse_service.add_item(&"iron_ore", 2)
	runtime._item_defs = item_defs
	runtime._selected_settlement = {
		"settlement_id": "forge_town",
	}
	runtime._settlements_by_id = {
		"forge_town": _build_settlement_record(true),
	}
	runtime._settlement_states = {
		"forge_town": {
			"visited": true,
			"reputation": 0,
			"active_conditions": [],
			"cooldowns": {},
			"shop_inventory_seed": 0,
			"shop_last_refresh_step": 0,
			"shop_states": {},
		},
	}

	var handler := GameRuntimeSettlementCommandHandler.new()
	handler.setup(runtime)

	var window_data := handler.get_settlement_window_data("forge_town")
	var reforge_entry := _find_service_entry(window_data.get("available_services", []), "service_master_reforge")
	_assert_true(not reforge_entry.is_empty(), "据点窗口应暴露 service_master_reforge 服务入口。")
	_assert_true(bool(reforge_entry.get("is_enabled", false)), "存在可执行配方时，大师重铸入口应可用。")
	_assert_eq(String(reforge_entry.get("cost_label", "")), "按配方材料", "大师重铸入口应显示按配方材料计价。")

	var open_result := handler.command_execute_settlement_action("service:master_reforge")
	_assert_true(bool(open_result.get("ok", false)), "service:master_reforge 首次触发应成功打开 forge modal。")
	_assert_eq(runtime._active_modal_id, "forge", "首次点击大师重铸服务后应切换到 forge modal。")
	_assert_true(not handler.get_forge_window_data().is_empty(), "打开 forge modal 后应能读取 forge window data。")
	_assert_true((handler.get_forge_window_data().get("entries", []) as Array).size() > 0, "forge window data 应暴露可选配方。")
	_assert_eq(runtime._warehouse_service.count_item(&"iron_greatsword"), 0, "仅打开 forge modal 时不应提前产出铁制大剑。")

	var command_result := handler.command_execute_settlement_action("service:master_reforge", {
		"submission_source": "forge",
		"recipe_id": "master_reforge_iron_greatsword",
	})
	_assert_true(bool(command_result.get("ok", false)), "forge modal 提交配方后应成功执行重铸。")
	_assert_eq(runtime._active_modal_id, "forge", "执行重铸后应继续停留在 forge modal。")
	_assert_eq(runtime._warehouse_service.count_item(&"iron_greatsword"), 1, "通过 handler 执行后应真正产出铁制大剑。")
	_assert_eq(runtime.persist_calls, 1, "重铸成功后应持久化队伍状态。")
	_assert_eq(runtime.sync_party_calls, 1, "重铸成功后应同步角色管理侧队伍状态。")
	_assert_true(runtime._active_settlement_feedback_text.find("铁制大剑") >= 0, "handler 应把重铸反馈写入据点窗口。")
	_assert_true(runtime._current_status_message.find("铁制大剑") >= 0, "handler 应刷新重铸完成状态文案。")
	_assert_eq(runtime.applied_quest_event_batches.size(), 1, "重铸成功后应把默认 quest progress 事件应用到运行时。")
	_assert_eq(runtime.achievement_events.size(), 1, "重铸成功后应记录据点动作成就事件。")
	_assert_eq(runtime.achievement_events[0].get("detail_id", ""), "service:master_reforge", "成就事件应记录重铸动作 ID。")

	handler.on_forge_window_closed()
	_assert_eq(runtime._active_modal_id, "settlement", "关闭 forge modal 后应返回 settlement modal。")


func _test_settlement_handler_routes_generic_forge() -> void:
	var item_defs := _load_item_defs()
	var runtime := MockRuntime.new()
	runtime._party_state = _build_party_state(6)
	runtime._warehouse_service = PartyWarehouseService.new()
	runtime._warehouse_service.setup(runtime._party_state, item_defs)
	runtime._warehouse_service.add_item(&"bronze_sword", 1)
	runtime._warehouse_service.add_item(&"iron_ore", 3)
	runtime._item_defs = item_defs
	runtime._selected_settlement = {
		"settlement_id": "forge_town",
	}
	runtime._settlements_by_id = {
		"forge_town": _build_settlement_record(true, true),
	}
	runtime._settlement_states = {
		"forge_town": {
			"visited": true,
			"reputation": 0,
			"active_conditions": [],
			"cooldowns": {},
			"shop_inventory_seed": 0,
			"shop_last_refresh_step": 0,
			"shop_states": {},
		},
	}

	var handler := GameRuntimeSettlementCommandHandler.new()
	handler.setup(runtime)

	var window_data := handler.get_settlement_window_data("forge_town")
	var generic_entry := _find_service_entry(window_data.get("available_services", []), "service_repair_gear")
	_assert_true(not generic_entry.is_empty(), "据点窗口应暴露通用 forge 服务入口。")
	_assert_true(bool(generic_entry.get("is_enabled", false)), "存在通用 forge 配方时，service_repair_gear 应可用。")
	_assert_eq(String(generic_entry.get("cost_label", "")), "按配方材料", "通用 forge 入口应显示按配方材料计价。")

	var open_result := handler.command_execute_settlement_action("service:repair_gear")
	_assert_true(bool(open_result.get("ok", false)), "service:repair_gear 首次触发应成功打开 forge modal。")
	_assert_eq(runtime._active_modal_id, "forge", "首次点击通用 forge 服务后应切换到 forge modal。")
	_assert_eq(String(handler.get_forge_window_data().get("action_id", "")), "service:repair_gear", "通用 forge modal 应保留原始 action_id。")
	_assert_true(String(handler.get_forge_window_data().get("title", "")).find("重铸") == -1, "通用 forge modal 标题不应回退成大师重铸。")
	_assert_true((handler.get_forge_window_data().get("entries", []) as Array).size() > 0, "通用 forge window data 应暴露可选配方。")

	var command_result := handler.command_execute_settlement_action("service:repair_gear", {
		"submission_source": "forge",
		"recipe_id": "forge_smith_iron_greatsword",
	})
	_assert_true(bool(command_result.get("ok", false)), "forge modal 提交通用配方后应成功执行锻造。")
	_assert_eq(runtime._active_modal_id, "forge", "执行通用 forge 后应继续停留在 forge modal。")
	_assert_eq(runtime._warehouse_service.count_item(&"bronze_sword"), 0, "通用 forge 成功后应消耗青铜短剑。")
	_assert_eq(runtime._warehouse_service.count_item(&"iron_ore"), 0, "通用 forge 成功后应消耗三份铁矿石。")
	_assert_eq(runtime._warehouse_service.count_item(&"iron_greatsword"), 1, "通用 forge 成功后应真正产出铁制大剑。")
	_assert_eq(runtime.persist_calls, 1, "通用 forge 成功后应持久化队伍状态。")
	_assert_eq(runtime.sync_party_calls, 1, "通用 forge 成功后应同步角色管理侧队伍状态。")
	_assert_true(runtime._active_settlement_feedback_text.find("铁制大剑") >= 0, "handler 应把通用 forge 反馈写入据点窗口。")
	_assert_true(runtime._current_status_message.find("铁制大剑") >= 0, "handler 应刷新通用 forge 完成状态文案。")
	_assert_eq(runtime.applied_quest_event_batches.size(), 1, "通用 forge 成功后应把默认 quest progress 事件应用到运行时。")
	_assert_eq(runtime.achievement_events.size(), 1, "通用 forge 成功后应记录据点动作成就事件。")
	_assert_eq(runtime.achievement_events[0].get("detail_id", ""), "service:repair_gear", "成就事件应记录通用 forge 动作 ID。")

	handler.on_forge_window_closed()
	_assert_eq(runtime._active_modal_id, "settlement", "关闭通用 forge modal 后应返回 settlement modal。")


func _test_new_world_generation_exposes_master_reforge_service() -> void:
	var game_session = GameSessionScript.new()
	game_session.name = "ForgeGameSession"
	root.add_child(game_session)
	await process_frame

	var create_error := int(game_session.create_new_save(TEST_CONFIG_PATH, &"forge_spawn_service", "大师重铸入口验证"))
	_assert_eq(create_error, OK, "创建带重铸入口验证的新世界应成功。")
	if create_error == OK:
		var world_data: Dictionary = game_session.get_world_data()
		var found_reforge_service := false
		for settlement_variant in world_data.get("settlements", []):
			if settlement_variant is not Dictionary:
				continue
			for service_variant in (settlement_variant as Dictionary).get("available_services", []):
				if service_variant is not Dictionary:
					continue
				if String((service_variant as Dictionary).get("interaction_script_id", "")) == "service_master_reforge":
					found_reforge_service = true
					break
			if found_reforge_service:
				break
		_assert_true(found_reforge_service, "新生成世界的 available_services 应包含 service_master_reforge。")

	var clear_error := int(game_session.clear_persisted_game())
	_assert_eq(clear_error, OK, "清理重铸入口验证存档应成功。")
	if game_session.get_parent() != null:
		game_session.get_parent().remove_child(game_session)
	game_session.free()
	await process_frame


func _test_ashen_intersection_generation_exposes_generic_forge_service() -> void:
	var game_session = GameSessionScript.new()
	game_session.name = "AshenForgeGameSession"
	root.add_child(game_session)
	await process_frame

	var create_error := int(game_session.create_new_save(ASHEN_INTERSECTION_CONFIG_PATH, &"generic_forge_spawn_service", "通用 forge 入口验证"))
	_assert_eq(create_error, OK, "创建灰烬交界世界应成功。")
	if create_error == OK:
		var world_data: Dictionary = game_session.get_world_data()
		var player_start_coord: Vector2i = world_data.get("player_start_coord", Vector2i.ZERO)
		var start_settlement := _find_settlement_covering_coord(world_data.get("settlements", []), player_start_coord)
		var generic_entry := _find_service_entry(start_settlement.get("available_services", []), "service_repair_gear")
		_assert_true(not start_settlement.is_empty(), "灰烬交界的起始坐标应落在一个据点上。")
		_assert_true(not generic_entry.is_empty(), "灰烬交界的起始据点应暴露通用 forge 服务入口。")

	var clear_error := int(game_session.clear_persisted_game())
	_assert_eq(clear_error, OK, "清理通用 forge 入口验证存档应成功。")
	if game_session.get_parent() != null:
		game_session.get_parent().remove_child(game_session)
	game_session.free()
	await process_frame


func _build_settlement_record(include_master_service_entry: bool = false, include_generic_service_entry: bool = false) -> Dictionary:
	var facility := {
		"facility_id": "ash_forge",
		"display_name": "灰烬工坊",
		"category": "support",
		"interaction_type": "craft",
		"slot_tag": "support",
		"service_npcs": [
			{
				"npc_id": "npc_blacksmith",
				"display_name": "灰烬铁匠",
				"service_type": "锻火",
				"interaction_script_id": "service_repair_gear",
				"facility_id": "ash_forge",
				"facility_name": "灰烬工坊",
			},
			{
				"npc_id": "npc_master_smith",
				"display_name": "大师铁匠",
				"service_type": "重铸",
				"interaction_script_id": "service_master_reforge",
				"facility_id": "ash_forge",
				"facility_name": "灰烬工坊",
			},
		],
	}
	var settlement := {
		"settlement_id": "forge_town",
		"display_name": "灰烬镇",
		"origin": Vector2i.ZERO,
		"facilities": [facility],
		"available_services": [],
	}
	var available_services: Array[Dictionary] = []
	if include_generic_service_entry:
		available_services.append(
			{
				"action_id": "service:repair_gear",
				"facility_id": "ash_forge",
				"facility_name": "灰烬工坊",
				"npc_id": "npc_blacksmith",
				"npc_name": "灰烬铁匠",
				"service_type": "锻火",
				"interaction_script_id": "service_repair_gear",
			}
		)
	if include_master_service_entry:
		available_services.append(
			{
				"action_id": "service:master_reforge",
				"facility_id": "ash_forge",
				"facility_name": "灰烬工坊",
				"npc_id": "npc_master_smith",
				"npc_name": "大师铁匠",
				"service_type": "重铸",
				"interaction_script_id": "service_master_reforge",
			}
		)
	if not available_services.is_empty():
		settlement["available_services"] = available_services
	return settlement


func _build_reforge_payload() -> Dictionary:
	return {
		"facility_id": "ash_forge",
		"facility_name": "灰烬工坊",
		"npc_id": "npc_master_smith",
		"npc_name": "大师铁匠",
		"service_type": "重铸",
		"interaction_script_id": "service_master_reforge",
	}


func _find_settlement_record(settlements: Array, settlement_id: String) -> Dictionary:
	for settlement_variant in settlements:
		if settlement_variant is not Dictionary:
			continue
		var settlement: Dictionary = settlement_variant
		if String(settlement.get("settlement_id", "")) == settlement_id:
			return settlement
	return {}


func _find_settlement_covering_coord(settlements: Array, coord: Vector2i) -> Dictionary:
	for settlement_variant in settlements:
		if settlement_variant is not Dictionary:
			continue
		var settlement: Dictionary = settlement_variant
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var footprint_size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		var rect := Rect2i(origin, footprint_size)
		if rect.has_point(coord):
			return settlement
	return {}


func _build_party_state(storage_space: int) -> PartyState:
	var party_state := PartyState.new()
	party_state.leader_member_id = &"hero"
	party_state.active_member_ids = [&"hero"]

	var hero := PartyMemberState.new()
	hero.member_id = &"hero"
	hero.display_name = "Hero"

	var progression := UnitProgress.new()
	progression.unit_id = &"hero"
	progression.display_name = "Hero"
	var attributes := UnitBaseAttributes.new()
	attributes.custom_stats[&"storage_space"] = storage_space
	progression.unit_base_attributes = attributes
	hero.progression = progression

	party_state.member_states = {
		&"hero": hero,
	}
	return party_state


func _load_item_defs() -> Dictionary:
	var registry := ItemContentRegistry.new()
	return registry.get_item_defs()


func _find_service_entry(services: Array, interaction_script_id: String) -> Dictionary:
	for service_variant in services:
		if service_variant is not Dictionary:
			continue
		var service_data: Dictionary = service_variant
		if String(service_data.get("interaction_script_id", "")) == interaction_script_id:
			return service_data
	return {}


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
