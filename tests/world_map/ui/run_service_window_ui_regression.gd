extends SceneTree

const SETTLEMENT_WINDOW_SCENE = preload("res://scenes/ui/settlement_window.tscn")
const SHOP_WINDOW_SCENE = preload("res://scenes/ui/shop_window.tscn")
const STAGECOACH_WINDOW_SCENE = preload("res://scenes/ui/stagecoach_window.tscn")
const CHARACTER_INFO_WINDOW_SCENE = preload("res://scenes/ui/character_info_window.tscn")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_settlement_window_routes_all_services_through_action_requested()
	await _test_shop_window_preserves_confirm_contract_and_left_click_dismiss()
	await _test_stagecoach_window_preserves_confirm_contract_and_respects_empty_entries()
	await _test_character_info_window_falls_back_to_legacy_payload()
	await _test_character_info_window_renders_explicit_sections()

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
