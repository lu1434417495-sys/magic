extends SceneTree

const GameRuntimeFacade = preload("res://scripts/systems/game_runtime_facade.gd")
const GameSessionScript = preload("res://scripts/systems/game_session.gd")
const SETTLEMENT_WINDOW_SCENE = preload("res://scenes/ui/settlement_window.tscn")
const SHOP_WINDOW_SCENE = preload("res://scenes/ui/shop_window.tscn")
const STAGECOACH_WINDOW_SCENE = preload("res://scenes/ui/stagecoach_window.tscn")
const CHARACTER_INFO_WINDOW_SCENE = preload("res://scenes/ui/character_info_window.tscn")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")

const TEST_CONFIG_PATH := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_settlement_window_routes_all_services_through_action_requested()
	await _test_shop_window_preserves_confirm_contract_and_left_click_dismiss()
	await _test_stagecoach_window_preserves_confirm_contract_and_respects_empty_entries()
	await _test_runtime_service_windows_render_real_member_summaries()
	await _test_character_info_window_falls_back_to_legacy_payload()
	await _test_character_info_window_renders_explicit_sections()
	await _test_runtime_character_info_context_uses_explicit_sections()

	if _failures.is_empty():
		print("Service window UI regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Service window UI regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_settlement_window_routes_all_services_through_action_requested() -> void:
	var window = SETTLEMENT_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	var action_calls: Array[Dictionary] = []
	window.action_requested.connect(func(settlement_id: String, action_id: String, payload: Dictionary) -> void:
		action_calls.append({
			"settlement_id": settlement_id,
			"action_id": action_id,
			"payload": payload,
		})
	)
	_assert_true(not window.has_signal("shop_requested"), "SettlementWindow 不应再暴露 shop_requested。")
	_assert_true(not window.has_signal("stagecoach_requested"), "SettlementWindow 不应再暴露 stagecoach_requested。")

	window.show_settlement({
		"settlement_id": "spring_village_01",
		"party_state": _make_party_state(),
		"available_services": [
			{
				"action_id": "service:shop",
				"panel_kind": "shop",
				"facility_name": "集市",
				"npc_name": "商人",
				"service_type": "交易",
			},
			{
				"action_id": "service:stagecoach",
				"panel_kind": "stagecoach",
				"facility_name": "驿站",
				"npc_name": "车夫",
				"service_type": "行路",
			},
		],
	})
	await process_frame

	window._on_service_button_pressed(0)
	window._on_service_button_pressed(1)

	_assert_eq(action_calls.size(), 2, "SettlementWindow 应只通过 action_requested 发出服务请求。")
	if action_calls.size() >= 2:
		_assert_eq(action_calls[0].get("settlement_id", ""), "spring_village_01", "SettlementWindow action_requested 应保留 settlement_id。")
		_assert_eq(action_calls[0].get("action_id", ""), "service:shop", "SettlementWindow 应透传商店 action_id。")
		_assert_eq(action_calls[0].get("payload", {}).get("panel_kind", ""), "shop", "商店服务 payload 应保留 panel_kind=shop。")
		_assert_eq(action_calls[1].get("action_id", ""), "service:stagecoach", "SettlementWindow 应透传驿站 action_id。")
		_assert_eq(action_calls[1].get("payload", {}).get("panel_kind", ""), "stagecoach", "驿站服务 payload 应保留 panel_kind=stagecoach。")

	window.queue_free()
	await process_frame


func _test_shop_window_preserves_confirm_contract_and_left_click_dismiss() -> void:
	var window = SHOP_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	var emitted_calls: Array[Dictionary] = []
	window.action_requested.connect(func(settlement_id: String, action_id: String, payload: Dictionary) -> void:
		emitted_calls.append({
			"settlement_id": settlement_id,
			"action_id": action_id,
			"payload": payload,
		})
	)

	var base_window_data := {
		"settlement_id": "spring_village_01",
		"action_id": "service:shop",
		"show_member_selector": false,
		"entries": [
			{
				"entry_id": "healing_potion",
				"display_name": "治疗药水",
				"is_enabled": true,
			},
		],
	}

	window.show_shop(base_window_data)
	await process_frame

	window._on_shade_gui_input(_make_mouse_button_event(MOUSE_BUTTON_RIGHT))
	_assert_true(window.visible, "ShopWindow 右键点击遮罩不应关闭窗口。")

	window._on_confirm_button_pressed()
	_assert_true(not emitted_calls.is_empty(), "ShopWindow 确认后应发出 action_requested。")
	if not emitted_calls.is_empty():
		_assert_eq(emitted_calls[0].get("settlement_id", ""), "spring_village_01", "ShopWindow action_requested 应保留 settlement_id。")
		_assert_eq(emitted_calls[0].get("action_id", ""), "service:shop", "ShopWindow action_requested 应保留 action_id。")
	_assert_true(not window.visible, "ShopWindow 确认后应隐藏窗口。")

	window.show_shop(base_window_data)
	await process_frame
	window._on_shade_gui_input(_make_mouse_button_event(MOUSE_BUTTON_LEFT))
	_assert_true(not window.visible, "ShopWindow 左键点击遮罩应关闭窗口。")

	window.queue_free()
	await process_frame


func _test_stagecoach_window_preserves_confirm_contract_and_respects_empty_entries() -> void:
	var window = STAGECOACH_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	var emitted_calls: Array[Dictionary] = []
	window.action_requested.connect(func(settlement_id: String, action_id: String, payload: Dictionary) -> void:
		emitted_calls.append({
			"settlement_id": settlement_id,
			"action_id": action_id,
			"payload": payload,
		})
	)

	var party_state := _make_party_state()
	var base_window_data := {
		"settlement_id": "spring_village_01",
		"action_id": "service:stagecoach",
		"party_state": party_state,
		"entries": [
			{
				"entry_id": "route:north",
				"display_name": "北境路线",
				"target_settlement_id": "north_outpost",
				"is_enabled": true,
			},
		],
	}

	window.show_stagecoach(base_window_data)
	await process_frame

	window._on_shade_gui_input(_make_mouse_button_event(MOUSE_BUTTON_RIGHT))
	_assert_true(window.visible, "StagecoachWindow 右键点击遮罩不应关闭窗口。")

	window._on_confirm_button_pressed()
	_assert_true(not emitted_calls.is_empty(), "StagecoachWindow 确认后应发出 action_requested。")
	if not emitted_calls.is_empty():
		_assert_eq(emitted_calls[0].get("settlement_id", ""), "spring_village_01", "StagecoachWindow action_requested 应保留 settlement_id。")
		_assert_eq(emitted_calls[0].get("action_id", ""), "service:stagecoach", "StagecoachWindow action_requested 应保留 action_id。")
	_assert_true(not window.visible, "StagecoachWindow 确认后应隐藏窗口。")

	window.show_stagecoach({
		"settlement_id": "spring_village_01",
		"action_id": "service:stagecoach",
		"party_state": party_state,
		"entries": [],
		"allow_empty_entries": true,
	})
	await process_frame
	_assert_eq(window.entry_list.item_count, 0, "allow_empty_entries=true 时 StagecoachWindow 不应注入 fallback 条目。")
	_assert_true(window.confirm_button.disabled, "无条目且允许空列表时 StagecoachWindow 确认按钮应禁用。")

	window.show_stagecoach({
		"settlement_id": "spring_village_01",
		"action_id": "service:stagecoach",
		"party_state": party_state,
		"entries": [],
		"allow_empty_entries": false,
	})
	await process_frame
	_assert_eq(window.entry_list.item_count, 1, "allow_empty_entries=false 时 StagecoachWindow 应回退到默认条目。")
	window._on_shade_gui_input(_make_mouse_button_event(MOUSE_BUTTON_LEFT))
	_assert_true(not window.visible, "StagecoachWindow 左键点击遮罩应关闭窗口。")

	window.queue_free()
	await process_frame


func _test_runtime_service_windows_render_real_member_summaries() -> void:
	var settlement_bundle := await _create_runtime_bundle("service_ui_settlement_shop")
	if settlement_bundle.is_empty():
		return

	var settlement_window = SETTLEMENT_WINDOW_SCENE.instantiate()
	var shop_window = SHOP_WINDOW_SCENE.instantiate()
	root.add_child(settlement_window)
	root.add_child(shop_window)
	await process_frame

	var settlement_facade: GameRuntimeFacade = settlement_bundle.get("facade") as GameRuntimeFacade
	var settlement_service := _find_runtime_service_entry(
		settlement_facade,
		["service:basic_supply", "service:local_trade", "service:city_market", "service:grand_auction"],
		true
	)
	_assert_true(not settlement_service.is_empty(), "正式 runtime 应能生成当前可见的商店服务入口。")
	if not settlement_service.is_empty():
		var open_result: Dictionary = settlement_facade.command_open_settlement(settlement_service.get("coord", Vector2i.ZERO))
		_assert_true(bool(open_result.get("ok", false)), "正式 runtime 应能打开 shop 所在据点。")
		var settlement_window_data := settlement_facade.get_settlement_window_data()
		_assert_member_option_payload(settlement_window_data, "settlement window data")
		settlement_window.show_settlement(settlement_window_data)
		await process_frame
		_assert_true(settlement_window.member_selector.visible, "正式 settlement window data 应显示成员选择器。")
		_assert_true(settlement_window.member_state_label.text.contains("HP "), "正式 settlement window data 应渲染成员 HP/MP 摘要。")
		_assert_true(settlement_window.member_state_label.text.contains("状态：当前队长"), "正式 settlement window data 应标出当前队长。")

		var shop_open_result: Dictionary = settlement_facade.command_execute_settlement_action(String(settlement_service.get("action_id", "")))
		_assert_true(bool(shop_open_result.get("ok", false)), "正式 runtime 应能从据点窗口打开 shop modal。")
		var shop_window_data := settlement_facade.get_shop_window_data()
		_assert_member_option_payload(shop_window_data, "shop window data")
		shop_window.show_shop(shop_window_data)
		await process_frame
		_assert_true(shop_window.member_selector.visible, "正式 shop window data 应显示成员选择器。")
		_assert_true(shop_window.member_state_label.text.contains("HP "), "正式 shop window data 应渲染成员 HP/MP 摘要。")
		_assert_true(shop_window.member_state_label.text.contains("状态：当前队长"), "正式 shop window data 应标出当前队长。")

	settlement_window.queue_free()
	shop_window.queue_free()
	await process_frame
	await _cleanup_runtime_bundle(settlement_bundle)

	var stagecoach_bundle := await _create_runtime_bundle("service_ui_stagecoach")
	if stagecoach_bundle.is_empty():
		return

	var stagecoach_window = STAGECOACH_WINDOW_SCENE.instantiate()
	root.add_child(stagecoach_window)
	await process_frame

	var stagecoach_facade: GameRuntimeFacade = stagecoach_bundle.get("facade") as GameRuntimeFacade
	var visited_destination_service := _find_runtime_service_entry(
		stagecoach_facade,
		["service:basic_supply", "service:local_trade", "service:city_market", "service:grand_auction"],
		true
	)
	_assert_true(not visited_destination_service.is_empty(), "驿站回归前置：应能找到一个当前可见的已访问目的地据点。")
	if not visited_destination_service.is_empty():
		var open_origin_result: Dictionary = stagecoach_facade.command_open_settlement(visited_destination_service.get("coord", Vector2i.ZERO))
		_assert_true(bool(open_origin_result.get("ok", false)), "驿站回归前置：应能打开起始据点并标记 visited。")
		var close_origin_result: Dictionary = stagecoach_facade.command_close_active_modal()
		_assert_true(bool(close_origin_result.get("ok", false)), "驿站回归前置：关闭起始据点窗口应成功。")

	var stagecoach_service := _find_runtime_service_entry(
		stagecoach_facade,
		["service:stagecoach", "service:world_gate_travel"]
	)
	_assert_true(not stagecoach_service.is_empty(), "正式 runtime 应能生成 stagecoach 服务入口。")
	if not stagecoach_service.is_empty():
		var stagecoach_coord: Vector2i = stagecoach_service.get("coord", Vector2i.ZERO)
		stagecoach_facade.set_player_coord(stagecoach_coord)
		stagecoach_facade.set_selected_coord(stagecoach_coord)
		stagecoach_facade.refresh_world_visibility()
		var stagecoach_open_result: Dictionary = stagecoach_facade.command_open_settlement(stagecoach_service.get("coord", Vector2i.ZERO))
		_assert_true(bool(stagecoach_open_result.get("ok", false)), "正式 runtime 应能打开 stagecoach 所在据点。")
		var open_modal_result: Dictionary = stagecoach_facade.command_execute_settlement_action(String(stagecoach_service.get("action_id", "")))
		_assert_true(bool(open_modal_result.get("ok", false)), "正式 runtime 应能从据点窗口打开 stagecoach modal。")
		var stagecoach_window_data := stagecoach_facade.get_stagecoach_window_data()
		_assert_member_option_payload(stagecoach_window_data, "stagecoach window data")
		var stagecoach_entries_variant = stagecoach_window_data.get("entries", [])
		_assert_true(stagecoach_entries_variant is Array and not (stagecoach_entries_variant as Array).is_empty(), "正式 stagecoach window data 应包含至少一个已访问目的地。")
		stagecoach_window.show_stagecoach(stagecoach_window_data)
		await process_frame
		_assert_true(stagecoach_window.member_selector.visible, "正式 stagecoach window data 应显示成员选择器。")
		_assert_true(stagecoach_window.member_state_label.text.contains("HP "), "正式 stagecoach window data 应渲染成员 HP/MP 摘要。")
		_assert_true(stagecoach_window.member_state_label.text.contains("状态：当前队长"), "正式 stagecoach window data 应标出当前队长。")

	stagecoach_window.queue_free()
	await process_frame
	await _cleanup_runtime_bundle(stagecoach_bundle)


func _test_character_info_window_falls_back_to_legacy_payload() -> void:
	var window = CHARACTER_INFO_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	window.show_character({
		"display_name": "流浪斥候",
		"type_label": "世界 NPC",
		"faction_label": "中立",
		"coord": Vector2i(4, 9),
		"status_label": "可见提示单位",
	})
	await process_frame

	_assert_eq(window.sections_container.get_child_count(), 1, "旧 payload 应回退为单个身份信息 section。")
	_assert_eq(window.meta_label.text, "世界 NPC  |  阵营 中立  |  坐标 (4, 9)", "旧 payload 应继续渲染 legacy meta 文案。")
	_assert_eq(window.status_label.text, "可见提示单位", "旧 payload 应继续渲染 status_label。")
	_assert_true(window.status_block.visible, "status_label 非空时状态块应保持可见。")

	var rendered_texts := _collect_label_texts(window.sections_container)
	_assert_true(rendered_texts.has("身份信息"), "旧 payload fallback section 应包含默认标题。")
	_assert_true(rendered_texts.has("姓名："), "旧 payload fallback section 应包含姓名标签。")
	_assert_true(rendered_texts.has("流浪斥候"), "旧 payload fallback section 应包含姓名值。")
	_assert_true(rendered_texts.has("世界 NPC"), "旧 payload fallback section 应包含类型值。")
	_assert_true(rendered_texts.has("中立"), "旧 payload fallback section 应包含阵营值。")
	_assert_true(rendered_texts.has("(4, 9)"), "旧 payload fallback section 应包含坐标值。")

	window.queue_free()
	await process_frame


func _test_character_info_window_renders_explicit_sections() -> void:
	var window = CHARACTER_INFO_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	window.show_character({
		"display_name": "铁壁卫士",
		"meta_label": "战斗单位  |  玩家前排",
		"sections": [
			{
				"title": "基础概览",
				"entries": [
					{
						"label": "职业",
						"value": "守卫",
					},
					{
						"label": "护甲",
						"value": "12",
					},
				],
			},
			{
				"title": "技能摘要",
				"body": "持盾反击后为相邻友军提供掩护。",
			},
			{
				"title": "装备摘要",
				"entries": [
					"塔盾",
					{
						"text": "短枪·列阵姿态",
					},
				],
			},
		],
		"status_label": "准备格挡",
	})
	await process_frame

	_assert_eq(window.sections_container.get_child_count(), 3, "section payload 应按段落逐个渲染。")
	_assert_eq(window.meta_label.text, "战斗单位  |  玩家前排", "显式 meta_label 应覆盖 legacy meta 拼接。")
	_assert_eq(window.status_label.text, "准备格挡", "section payload 仍应复用 status_label。")

	var rendered_texts := _collect_label_texts(window.sections_container)
	_assert_true(rendered_texts.has("基础概览"), "section payload 应渲染基础概览标题。")
	_assert_true(rendered_texts.has("职业："), "section payload 应渲染 label/value 条目。")
	_assert_true(rendered_texts.has("守卫"), "section payload 应渲染职业值。")
	_assert_true(rendered_texts.has("技能摘要"), "section payload 应渲染 body section 标题。")
	_assert_true(rendered_texts.has("持盾反击后为相邻友军提供掩护。"), "section payload 应渲染 body 文本。")
	_assert_true(rendered_texts.has("装备摘要"), "section payload 应渲染多种 entry 形态。")
	_assert_true(rendered_texts.has("塔盾"), "section payload 应渲染字符串 entry。")
	_assert_true(rendered_texts.has("短枪·列阵姿态"), "section payload 应渲染 text entry。")

	window.queue_free()
	await process_frame


func _test_runtime_character_info_context_uses_explicit_sections() -> void:
	var runtime_bundle := await _create_runtime_bundle("service_ui_character_info")
	if runtime_bundle.is_empty():
		return

	var facade: GameRuntimeFacade = runtime_bundle.get("facade") as GameRuntimeFacade
	var game_session = runtime_bundle.get("game_session")
	var npc_coord := _find_first_world_npc_coord(game_session.get_world_data())
	_assert_true(npc_coord != Vector2i(-1, -1), "正式 runtime 回归前置：测试世界应至少生成一个 world NPC。")
	if npc_coord != Vector2i(-1, -1):
		var opened := facade._try_open_character_info_at_world_coord(npc_coord)
		_assert_true(opened, "正式 runtime 应能打开 world NPC 的 character info。")
		var context := facade.get_character_info_context()
		_assert_true(context.get("sections", null) is Array, "正式 runtime character info context 应输出显式 sections。")
		_assert_true(not (context.get("sections", []) as Array).is_empty(), "正式 runtime character info context 的 sections 不应为空。")
		_assert_true(not String(context.get("meta_label", "")).is_empty(), "正式 runtime character info context 应输出显式 meta_label。")

		var window = CHARACTER_INFO_WINDOW_SCENE.instantiate()
		root.add_child(window)
		await process_frame
		window.show_character(context)
		await process_frame

		_assert_true(window.sections_container.get_child_count() >= 1, "正式 runtime character info context 应驱动窗口渲染显式 sections。")
		_assert_true(window.meta_label.text.contains("阵营"), "正式 runtime character info meta_label 应包含阵营信息。")
		var rendered_texts := _collect_label_texts(window.sections_container)
		_assert_true(rendered_texts.has("基础概览"), "正式 runtime character info sections 应包含基础概览标题。")
		_assert_true(rendered_texts.has("世界 NPC"), "正式 runtime character info sections 应包含类型值。")

		window.queue_free()
		await process_frame

	await _cleanup_runtime_bundle(runtime_bundle)


func _make_party_state() -> PartyState:
	var party_state := PartyState.new()
	var member_state := PartyMemberState.new()
	member_state.member_id = &"hero"
	member_state.display_name = "Hero"
	member_state.current_hp = 18
	member_state.current_mp = 6
	party_state.set_member_state(member_state)
	party_state.active_member_ids.append(&"hero")
	party_state.leader_member_id = &"hero"
	return party_state


func _make_mouse_button_event(button_index: MouseButton) -> InputEventMouseButton:
	var event := InputEventMouseButton.new()
	event.button_index = button_index
	event.pressed = true
	return event


func _create_runtime_bundle(save_label: String) -> Dictionary:
	var game_session = GameSessionScript.new()
	game_session.name = "ServiceUiGameSession_%s" % save_label
	root.add_child(game_session)
	await process_frame
	var create_error := int(game_session.create_new_save(TEST_CONFIG_PATH, &"service_ui", save_label))
	_assert_eq(create_error, OK, "创建 service UI runtime 测试存档应成功。")
	if create_error != OK:
		await _cleanup_runtime_bundle({"game_session": game_session})
		return {}
	var facade := GameRuntimeFacade.new()
	facade.setup(game_session)
	return {
		"game_session": game_session,
		"facade": facade,
	}


func _cleanup_runtime_bundle(bundle: Dictionary) -> void:
	var game_session = bundle.get("game_session", null)
	if game_session != null:
		game_session.clear_persisted_game()
		if game_session.get_parent() != null:
			game_session.get_parent().remove_child(game_session)
		game_session.free()
	await process_frame


func _find_runtime_service_entry(facade: GameRuntimeFacade, action_ids: Array[String], require_visible: bool = false) -> Dictionary:
	if facade == null:
		return {}
	var allowed_action_ids: Dictionary = {}
	for action_id in action_ids:
		var normalized_action_id := String(action_id).strip_edges()
		if normalized_action_id.is_empty():
			continue
		allowed_action_ids[normalized_action_id] = true
	if allowed_action_ids.is_empty():
		return {}
	var fog_system = facade.get_fog_system()
	var player_faction_id := facade.get_player_faction_id()
	for settlement_variant in facade.get_all_settlement_records():
		if settlement_variant is not Dictionary:
			continue
		var settlement: Dictionary = settlement_variant
		var settlement_coord: Vector2i = settlement.get("origin", Vector2i.ZERO)
		if require_visible and (fog_system == null or not fog_system.is_visible(settlement_coord, player_faction_id)):
			continue
		for service_variant in settlement.get("available_services", []):
			if service_variant is not Dictionary:
				continue
			var service: Dictionary = service_variant
			var service_action_id := String(service.get("action_id", "")).strip_edges()
			if not allowed_action_ids.has(service_action_id):
				continue
			return {
				"settlement_id": String(settlement.get("settlement_id", "")),
				"coord": settlement_coord,
				"action_id": service_action_id,
			}
	return {}


func _find_first_world_npc_coord(world_data: Dictionary) -> Vector2i:
	for npc_variant in world_data.get("world_npcs", []):
		if npc_variant is not Dictionary:
			continue
		return (npc_variant as Dictionary).get("coord", Vector2i(-1, -1))
	return Vector2i(-1, -1)


func _assert_member_option_payload(window_data: Dictionary, label: String) -> void:
	var member_options_variant = window_data.get("member_options", [])
	_assert_true(member_options_variant is Array and not (member_options_variant as Array).is_empty(), "%s 应包含至少一个成员选项。" % label)
	if member_options_variant is not Array or (member_options_variant as Array).is_empty():
		return
	var first_option_variant = (member_options_variant as Array)[0]
	_assert_true(first_option_variant is Dictionary, "%s 的首个成员选项应为 Dictionary。" % label)
	if first_option_variant is not Dictionary:
		return
	var first_option := first_option_variant as Dictionary
	_assert_true(first_option.has("is_leader"), "%s 的成员选项应包含 is_leader。" % label)
	_assert_true(first_option.has("current_hp"), "%s 的成员选项应包含 current_hp。" % label)
	_assert_true(first_option.has("current_mp"), "%s 的成员选项应包含 current_mp。" % label)
	_assert_true(String(first_option.get("roster_role", "")) != "", "%s 的成员选项应包含可读 roster_role。" % label)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s Expected=%s Actual=%s" % [message, str(expected), str(actual)])


func _collect_label_texts(node: Node) -> Array[String]:
	var texts: Array[String] = []
	if node is Label:
		texts.append((node as Label).text)
	for child in node.get_children():
		texts.append_array(_collect_label_texts(child))
	return texts
