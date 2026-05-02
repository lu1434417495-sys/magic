extends SceneTree

const GameRuntimeFacade = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")
const GameSessionScript = preload("res://scripts/systems/persistence/game_session.gd")
const SETTLEMENT_WINDOW_SCENE = preload("res://scenes/ui/settlement_window.tscn")
const SHOP_WINDOW_SCENE = preload("res://scenes/ui/shop_window.tscn")
const STAGECOACH_WINDOW_SCENE = preload("res://scenes/ui/shop_window.tscn")
const CHARACTER_INFO_WINDOW_SCENE = preload("res://scenes/ui/character_info_window.tscn")
const MASTERY_REWARD_WINDOW_SCENE = preload("res://scenes/ui/mastery_reward_window.tscn")
const SUBMAP_ENTRY_WINDOW_SCENE = preload("res://scenes/ui/submap_entry_window.tscn")
const PendingCharacterReward = preload("res://scripts/systems/progression/pending_character_reward.gd")
const PendingCharacterRewardEntry = preload("res://scripts/systems/progression/pending_character_reward_entry.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")
const PartyMemberState = preload("res://scripts/player/progression/party_member_state.gd")

const TEST_CONFIG_PATH := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_settlement_window_routes_all_services_through_action_requested()
	await _test_settlement_window_refreshes_member_scoped_service_state()
	await _test_settlement_window_rejects_invalid_top_level_payload()
	await _test_settlement_window_rejects_invalid_service_payload()
	await _test_settlement_window_rejects_invalid_facility_and_resident_payload()
	await _test_shop_window_preserves_confirm_contract_and_left_click_dismiss()
	await _test_stagecoach_window_preserves_confirm_contract_and_rejects_bad_entry_schema()
	await _test_service_windows_reject_bad_explicit_member_option_names()
	await _test_runtime_service_windows_render_real_member_summaries()
	await _test_character_info_window_rejects_legacy_payload()
	await _test_character_info_window_renders_explicit_sections()
	await _test_runtime_character_info_context_uses_explicit_sections()
	await _test_runtime_character_info_context_rejects_invalid_world_npc_schema()
	await _test_battle_end_reward_window_uses_large_modal_metrics()
	await _test_game_over_confirmation_uses_shared_large_modal_metrics()

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

	window.show_settlement(
		_make_settlement_window_data([
			_make_formal_settlement_service_entry({
				"action_id": "service:shop",
				"panel_kind": "shop",
				"facility_name": "集市",
				"npc_name": "商人",
				"service_type": "交易",
				"interaction_script_id": "service_basic_supply",
				"cost_label": "按商品计价",
				"summary_text": "集市 · 商人 · 交易",
			}),
			_make_formal_settlement_service_entry({
				"action_id": "service:stagecoach",
				"panel_kind": "stagecoach",
				"facility_name": "驿站",
				"npc_name": "车夫",
				"service_type": "行路",
				"interaction_script_id": "service_stagecoach",
				"cost_label": "10 金/格",
				"summary_text": "驿站 · 车夫 · 行路",
			}),
		])
	)
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


func _test_settlement_window_rejects_invalid_top_level_payload() -> void:
	var window = SETTLEMENT_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	var missing_display_name := _make_settlement_window_data([_make_formal_settlement_service_entry()])
	missing_display_name.erase("display_name")
	window.show_settlement(missing_display_name)
	await process_frame
	_assert_true(not window.visible, "缺少 display_name 的据点窗口 payload 不应回退成据点。")
	_assert_eq(window.title_label.text, "", "缺少 display_name 时不应渲染标题。")

	var numeric_tier_name := _make_settlement_window_data([_make_formal_settlement_service_entry()], {
		"tier_name": 2,
	})
	window.show_settlement(numeric_tier_name)
	await process_frame
	_assert_true(not window.visible, "非 String tier_name 不应被 String() 兼容转换。")
	_assert_eq(window.meta_label.text, "", "非 String tier_name 时不应渲染 meta。")

	var array_footprint_size := _make_settlement_window_data([_make_formal_settlement_service_entry()], {
		"footprint_size": [1, 1],
	})
	window.show_settlement(array_footprint_size)
	await process_frame
	_assert_true(not window.visible, "非 Vector2i footprint_size 不应回退成 Vector2i.ONE。")
	_assert_eq(window.meta_label.text, "", "非 Vector2i footprint_size 时不应渲染 meta。")

	var missing_faction_id := _make_settlement_window_data([_make_formal_settlement_service_entry()])
	missing_faction_id.erase("faction_id")
	window.show_settlement(missing_faction_id)
	await process_frame
	_assert_true(not window.visible, "缺少 faction_id 的据点窗口 payload 不应回退成 neutral。")
	_assert_eq(window.meta_label.text, "", "缺少 faction_id 时不应渲染 meta。")

	var missing_feedback_text := _make_settlement_window_data([_make_formal_settlement_service_entry()])
	missing_feedback_text.erase("feedback_text")
	window.show_settlement(missing_feedback_text)
	await process_frame
	_assert_true(not window.visible, "缺少 feedback_text 的据点窗口 payload 不应回退成默认提示。")
	_assert_eq(window.feedback_label.text, "", "缺少 feedback_text 时不应渲染反馈文本。")

	var missing_available_services := _make_settlement_window_data([_make_formal_settlement_service_entry()])
	missing_available_services.erase("available_services")
	window.show_settlement(missing_available_services)
	await process_frame
	_assert_true(not window.visible, "缺少 available_services 的据点窗口 payload 不应回退成空服务列表。")
	_assert_eq(window.services_container.get_child_count(), 0, "缺少 available_services 时不应渲染服务占位。")

	var missing_facilities := _make_settlement_window_data([_make_formal_settlement_service_entry()])
	missing_facilities.erase("facilities")
	window.show_settlement(missing_facilities)
	await process_frame
	_assert_true(not window.visible, "缺少 facilities 的据点窗口 payload 不应回退成空设施列表。")
	_assert_eq(window.facilities_label.text, "", "缺少 facilities 时不应渲染设施空态。")

	var missing_service_npcs := _make_settlement_window_data([_make_formal_settlement_service_entry()])
	missing_service_npcs.erase("service_npcs")
	window.show_settlement(missing_service_npcs)
	await process_frame
	_assert_true(not window.visible, "缺少 service_npcs 的据点窗口 payload 不应回退成空 NPC 列表。")
	_assert_eq(window.resident_label.text, "", "缺少 service_npcs 时不应渲染驻留 NPC 空态。")

	window.queue_free()
	await process_frame


func _test_settlement_window_refreshes_member_scoped_service_state() -> void:
	var window = SETTLEMENT_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	var service := _make_formal_settlement_service_entry({
		"action_id": "service:research",
		"facility_name": "图书馆",
		"npc_name": "研究员",
		"service_type": "研究",
		"interaction_script_id": "service_research",
		"cost_label": "200 金",
		"summary_text": "图书馆 · 研究员 · 研究",
		"member_availability": {
			"hero": {
				"member_id": "hero",
				"is_enabled": false,
				"has_available_research": false,
				"disabled_reason": "暂无可研究内容",
			},
			"mage": {
				"member_id": "mage",
				"is_enabled": true,
				"has_available_research": true,
				"disabled_reason": "",
			},
		},
	})
	window.show_settlement(_make_settlement_window_data([service], {
		"member_options": [
			_make_formal_member_option(),
			_make_formal_member_option({
				"member_id": "mage",
				"display_name": "Mage",
				"is_leader": false,
			}),
		],
		"default_member_id": "hero",
	}))
	await process_frame

	var hero_service: Dictionary = window._resolve_service_for_selected_member(window._services[0])
	_assert_true(not bool(hero_service.get("is_enabled", true)), "成员 scoped 服务应按当前 hero 选择禁用。")
	_assert_eq(String(hero_service.get("disabled_reason", "")), "暂无可研究内容", "成员 scoped 服务应显示当前成员禁用原因。")
	window._on_member_selected(1)
	var mage_service: Dictionary = window._resolve_service_for_selected_member(window._services[0])
	_assert_true(bool(mage_service.get("is_enabled", false)), "切换成员后服务可用性应按新成员刷新。")

	window.queue_free()
	await process_frame


func _test_settlement_window_rejects_invalid_service_payload() -> void:
	var window = SETTLEMENT_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	var missing_npc_name := _make_formal_settlement_service_entry()
	missing_npc_name.erase("npc_name")
	window.show_settlement(_make_settlement_window_data([missing_npc_name]))
	await process_frame
	_assert_true(not window.visible, "缺少 npc_name 的据点服务 payload 不应回退成 NPC。")
	_assert_eq(window.services_container.get_child_count(), 0, "缺少 npc_name 时不应渲染服务按钮。")

	var missing_cost_label := _make_formal_settlement_service_entry()
	missing_cost_label.erase("cost_label")
	window.show_settlement(_make_settlement_window_data([missing_cost_label]))
	await process_frame
	_assert_true(not window.visible, "缺少 cost_label 的据点服务 payload 不应回退成费用待定。")
	_assert_eq(window.services_container.get_child_count(), 0, "缺少 cost_label 时不应渲染服务按钮。")

	var numeric_facility_name := _make_formal_settlement_service_entry({"facility_name": 17})
	window.show_settlement(_make_settlement_window_data([numeric_facility_name]))
	await process_frame
	_assert_true(not window.visible, "非 String facility_name 不应被 String() 兼容转换。")
	_assert_eq(window.services_container.get_child_count(), 0, "非 String facility_name 时不应渲染服务按钮。")

	var missing_interaction_script_id := _make_formal_settlement_service_entry()
	missing_interaction_script_id.erase("interaction_script_id")
	window.show_settlement(_make_settlement_window_data([missing_interaction_script_id]))
	await process_frame
	_assert_true(not window.visible, "缺少 interaction_script_id 的据点服务 payload 应拒绝整份窗口。")
	_assert_eq(window.services_container.get_child_count(), 0, "缺少 interaction_script_id 时不应渲染服务按钮。")

	var numeric_is_enabled := _make_formal_settlement_service_entry({"is_enabled": 1})
	window.show_settlement(_make_settlement_window_data([numeric_is_enabled]))
	await process_frame
	_assert_true(not window.visible, "非 bool is_enabled 不应被 bool() 兼容转换。")
	_assert_eq(window.services_container.get_child_count(), 0, "非 bool is_enabled 时不应渲染服务按钮。")

	var disabled_without_reason := _make_formal_settlement_service_entry({
		"is_enabled": false,
		"disabled_reason": "",
	})
	window.show_settlement(_make_settlement_window_data([disabled_without_reason]))
	await process_frame
	_assert_true(not window.visible, "禁用服务缺少 disabled_reason 时应拒绝整份窗口。")
	_assert_eq(window.services_container.get_child_count(), 0, "禁用服务缺少 disabled_reason 时不应渲染服务按钮。")

	var legacy_panel_alias := _make_formal_settlement_service_entry({
		"window_kind": "shop",
		"service_window_kind": "shop",
	})
	window.show_settlement(_make_settlement_window_data([legacy_panel_alias]))
	await process_frame
	_assert_true(not window.visible, "包含旧 window_kind/service_window_kind alias 的服务 payload 应拒绝整份窗口。")
	_assert_eq(window.services_container.get_child_count(), 0, "旧 window_kind/service_window_kind alias 不应渲染服务按钮。")

	var legacy_panel_field := _make_formal_settlement_service_entry({
		"panel": "shop",
	})
	window.show_settlement(_make_settlement_window_data([legacy_panel_field]))
	await process_frame
	_assert_true(not window.visible, "包含旧 panel alias 的服务 payload 应拒绝整份窗口。")
	_assert_eq(window.services_container.get_child_count(), 0, "旧 panel alias 不应渲染服务按钮。")

	var empty_panel_kind := _make_formal_settlement_service_entry({
		"panel_kind": "",
	})
	window.show_settlement(_make_settlement_window_data([empty_panel_kind]))
	await process_frame
	_assert_true(not window.visible, "空 panel_kind 不应被当作缺省 panel 兼容处理。")
	_assert_eq(window.services_container.get_child_count(), 0, "空 panel_kind 时不应渲染服务按钮。")

	window.queue_free()
	await process_frame


func _test_settlement_window_rejects_invalid_facility_and_resident_payload() -> void:
	var window = SETTLEMENT_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	var missing_facility_name := _make_formal_facility_entry()
	missing_facility_name.erase("display_name")
	window.show_settlement(_make_settlement_window_data([_make_formal_settlement_service_entry()], {
		"facilities": [missing_facility_name],
	}))
	await process_frame
	_assert_true(not window.visible, "缺少 display_name 的设施 payload 不应回退成设施。")
	_assert_eq(window.facilities_label.text, "", "缺少设施 display_name 时不应渲染设施列表。")

	var numeric_slot_tag := _make_formal_facility_entry({"slot_tag": 12})
	window.show_settlement(_make_settlement_window_data([_make_formal_settlement_service_entry()], {
		"facilities": [numeric_slot_tag],
	}))
	await process_frame
	_assert_true(not window.visible, "非 String slot_tag 不应被 String() 兼容转换。")
	_assert_eq(window.facilities_label.text, "", "非 String slot_tag 时不应渲染设施列表。")

	var missing_resident_name := _make_formal_resident_entry()
	missing_resident_name.erase("display_name")
	window.show_settlement(_make_settlement_window_data([_make_formal_settlement_service_entry()], {
		"service_npcs": [missing_resident_name],
	}))
	await process_frame
	_assert_true(not window.visible, "缺少 display_name 的驻留 NPC payload 不应回退成 NPC。")
	_assert_eq(window.resident_label.text, "", "缺少驻留 NPC display_name 时不应渲染驻留列表。")

	var numeric_service_type := _make_formal_resident_entry({"service_type": 17})
	window.show_settlement(_make_settlement_window_data([_make_formal_settlement_service_entry()], {
		"service_npcs": [numeric_service_type],
	}))
	await process_frame
	_assert_true(not window.visible, "非 String service_type 不应被 String() 兼容转换。")
	_assert_eq(window.resident_label.text, "", "非 String service_type 时不应渲染驻留列表。")

	window.show_settlement(_make_settlement_window_data([_make_formal_settlement_service_entry()], {
		"facilities": [_make_formal_facility_entry()],
		"service_npcs": [_make_formal_resident_entry()],
	}))
	await process_frame
	_assert_true(window.visible, "正式设施和驻留 NPC payload 应能打开据点窗口。")
	_assert_true(window.facilities_label.text.contains("集市"), "正式设施 payload 应渲染设施名。")
	_assert_true(window.resident_label.text.contains("商人"), "正式驻留 NPC payload 应渲染 NPC 名。")

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

	var base_window_data := _make_formal_shop_window_data([
		_make_formal_shop_window_entry({
			"entry_id": "healing_potion",
			"display_name": "治疗药水",
		}),
	], {
		"show_member_selector": false,
		"member_options": [
			_make_formal_member_option({
				"member_id": "mage",
				"display_name": "Mage",
				"is_leader": false,
			}),
		],
		"default_member_id": "mage",
	})

	window.show_shop(base_window_data)
	await process_frame

	window._on_shade_gui_input(_make_mouse_button_event(MOUSE_BUTTON_RIGHT))
	_assert_true(window.visible, "ShopWindow 右键点击遮罩不应关闭窗口。")

	window._on_confirm_button_pressed()
	_assert_true(not emitted_calls.is_empty(), "ShopWindow 确认后应发出 action_requested。")
	if not emitted_calls.is_empty():
		_assert_eq(emitted_calls[0].get("settlement_id", ""), "spring_village_01", "ShopWindow action_requested 应保留 settlement_id。")
		_assert_eq(emitted_calls[0].get("action_id", ""), "service:shop", "ShopWindow action_requested 应保留 action_id。")
		_assert_eq(String(emitted_calls[0].get("payload", {}).get("member_id", "")), "mage", "隐藏成员选择器时 ShopWindow 仍应保留默认成员归属。")
	_assert_true(not window.visible, "ShopWindow 确认后应隐藏窗口。")

	var missing_title_data := base_window_data.duplicate(true)
	missing_title_data.erase("title")
	window.show_shop(missing_title_data)
	await process_frame
	_assert_true(not window.visible, "缺少 title 的 ShopWindow payload 不应回退成交易窗口标题。")
	_assert_eq(window.title_label.text, "", "缺少 title 时 ShopWindow 不应渲染标题。")

	var missing_cost_entry := _make_formal_shop_window_entry()
	missing_cost_entry.erase("cost_label")
	var invalid_window_data := base_window_data.duplicate(true)
	invalid_window_data["entries"] = [missing_cost_entry]
	window.show_shop(invalid_window_data)
	await process_frame
	_assert_true(not window.visible, "缺少 cost_label 的 ShopWindow 条目不应回退成费用待定。")
	_assert_eq(window.entry_list.item_count, 0, "缺少 cost_label 时 ShopWindow 不应渲染交易条目。")

	window.show_shop(base_window_data)
	await process_frame
	window._on_shade_gui_input(_make_mouse_button_event(MOUSE_BUTTON_LEFT))
	_assert_true(not window.visible, "ShopWindow 左键点击遮罩应关闭窗口。")

	window.queue_free()
	await process_frame


func _test_stagecoach_window_preserves_confirm_contract_and_rejects_bad_entry_schema() -> void:
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
	var base_window_data := _make_formal_stagecoach_window_data([
		_make_formal_stagecoach_window_entry({
			"entry_id": "route:north",
			"display_name": "北境路线",
			"target_settlement_id": "north_outpost",
		}),
	], {
		"party_state": party_state,
	})

	window.show_stagecoach(base_window_data)
	await process_frame

	_assert_eq(window.entry_title_label.text, "可选路线", "驿站应通过共享 ShopWindow shell 渲染路线列表标题。")
	_assert_eq(window.summary_title_label.text, "行程概况", "驿站应通过共享 ShopWindow shell 渲染行程概况标题。")
	_assert_eq(window.confirm_button.text, "确认出发", "驿站应通过共享 ShopWindow shell 渲染确认出发按钮文案。")
	window._on_shade_gui_input(_make_mouse_button_event(MOUSE_BUTTON_RIGHT))
	_assert_true(window.visible, "StagecoachWindow 右键点击遮罩不应关闭窗口。")

	window._on_confirm_button_pressed()
	_assert_true(not emitted_calls.is_empty(), "StagecoachWindow 确认后应发出 action_requested。")
	if not emitted_calls.is_empty():
		_assert_eq(emitted_calls[0].get("settlement_id", ""), "spring_village_01", "StagecoachWindow action_requested 应保留 settlement_id。")
		_assert_eq(emitted_calls[0].get("action_id", ""), "service:stagecoach", "StagecoachWindow action_requested 应保留 action_id。")
	_assert_true(not window.visible, "StagecoachWindow 确认后应隐藏窗口。")

	window.show_stagecoach(_make_formal_stagecoach_window_data([], {
		"party_state": party_state,
	}))
	await process_frame
	_assert_eq(window.entry_list.item_count, 0, "显式空 entries 时 StagecoachWindow 应展示空态而不是注入 fallback 条目。")
	_assert_true(window.confirm_button.disabled, "显式空 entries 时 StagecoachWindow 确认按钮应禁用。")

	var missing_entries_data := _make_formal_stagecoach_window_data([], {
		"party_state": party_state,
	})
	missing_entries_data.erase("entries")
	window.show_stagecoach(missing_entries_data)
	await process_frame
	_assert_true(not window.visible, "缺少 entries 的 StagecoachWindow payload 不应回退到默认条目。")
	_assert_eq(window.entry_list.item_count, 0, "缺少 entries 时 StagecoachWindow 不应渲染路线条目。")

	var missing_state_label := _make_formal_stagecoach_window_entry()
	missing_state_label.erase("state_label")
	var invalid_entry_window_data := base_window_data.duplicate(true)
	invalid_entry_window_data["entries"] = [missing_state_label]
	window.show_stagecoach(invalid_entry_window_data)
	await process_frame
	_assert_true(not window.visible, "缺少 state_label 的 StagecoachWindow 条目不应回退成可出发状态。")
	_assert_eq(window.entry_list.item_count, 0, "缺少 state_label 时 StagecoachWindow 不应渲染路线条目。")

	var missing_panel_kind := base_window_data.duplicate(true)
	missing_panel_kind.erase("panel_kind")
	window.show_stagecoach(missing_panel_kind)
	await process_frame
	_assert_true(not window.visible, "缺少 panel_kind=stagecoach 的 StagecoachWindow payload 不应由 wrapper 自动补齐。")

	window.show_stagecoach(base_window_data)
	await process_frame
	window._on_shade_gui_input(_make_mouse_button_event(MOUSE_BUTTON_LEFT))
	_assert_true(not window.visible, "StagecoachWindow 左键点击遮罩应关闭窗口。")

	window.queue_free()
	await process_frame


func _test_service_windows_reject_bad_explicit_member_option_names() -> void:
	var settlement_window = SETTLEMENT_WINDOW_SCENE.instantiate()
	var shop_window = SHOP_WINDOW_SCENE.instantiate()
	root.add_child(settlement_window)
	root.add_child(shop_window)
	await process_frame

	var missing_display_name := _make_formal_member_option({"member_id": "legacy_member"})
	missing_display_name.erase("display_name")
	var numeric_display_name := _make_formal_member_option({
		"member_id": "legacy_member",
		"display_name": 17,
	})
	var empty_display_name := _make_formal_member_option({
		"member_id": "legacy_member",
		"display_name": "  ",
	})
	var bad_payloads := [
		{"label": "缺少 display_name", "member_options": [missing_display_name]},
		{"label": "非 String display_name", "member_options": [numeric_display_name]},
		{"label": "空 display_name", "member_options": [empty_display_name]},
		{"label": "非 Array member_options", "member_options": "legacy_member"},
	]

	for payload_case in bad_payloads:
		var case_label := String(payload_case["label"])
		var member_options_variant: Variant = payload_case["member_options"]
		var settlement_data := _make_settlement_window_data([_make_formal_settlement_service_entry()], {
			"member_options": member_options_variant,
		})
		settlement_window.show_settlement(settlement_data)
		await process_frame
		_assert_true(settlement_window.visible, "%s 的 settlement payload 仍可打开窗口但不应渲染坏成员选项。" % case_label)
		_assert_eq(settlement_window.member_selector.item_count, 0, "%s 的 settlement payload 不应把坏 member option 加进选择器。" % case_label)
		_assert_eq(settlement_window.member_state_label.text, "成员：暂无可用成员。", "%s 的 settlement payload 不应从 member_id 或 party_state 回退成员展示名。" % case_label)
		_assert_true(not settlement_window.member_state_label.text.contains("legacy_member"), "%s 的 settlement 成员摘要不应显示 member_id fallback。" % case_label)
		_assert_true(not settlement_window.member_state_label.text.contains("Hero"), "%s 的 settlement 成员摘要不应回退到 party_state。" % case_label)
		_assert_true(not settlement_window.member_state_label.text.contains("17"), "%s 的 settlement 成员摘要不应跨类型转换 display_name。" % case_label)
		_assert_true(not settlement_window.member_state_label.text.contains("成员：成员"), "%s 的 settlement 成员摘要不应显示默认成员 fallback。" % case_label)

		var shop_data := _make_formal_shop_window_data([_make_formal_shop_window_entry()], {
			"party_state": _make_party_state(),
			"member_options": member_options_variant,
		})
		shop_window.show_shop(shop_data)
		await process_frame
		_assert_true(shop_window.visible, "%s 的 shop payload 仍可打开窗口但不应渲染坏成员选项。" % case_label)
		_assert_eq(shop_window.member_selector.item_count, 0, "%s 的 shop payload 不应把坏 member option 加进选择器。" % case_label)
		_assert_eq(shop_window.member_state_label.text, "成员：暂无可用成员。", "%s 的 shop payload 不应从 member_id 或 party_state 回退成员展示名。" % case_label)
		_assert_true(not shop_window.member_state_label.text.contains("legacy_member"), "%s 的 shop 成员摘要不应显示 member_id fallback。" % case_label)
		_assert_true(not shop_window.member_state_label.text.contains("Hero"), "%s 的 shop 成员摘要不应回退到 party_state。" % case_label)
		_assert_true(not shop_window.member_state_label.text.contains("17"), "%s 的 shop 成员摘要不应跨类型转换 display_name。" % case_label)
		_assert_true(not shop_window.member_state_label.text.contains("成员：成员"), "%s 的 shop 成员摘要不应显示默认成员 fallback。" % case_label)
		_assert_true(not shop_window.details_label.text.contains("legacy_member"), "%s 的 shop 明细不应显示 member_id fallback。" % case_label)
		_assert_true(not shop_window.details_label.text.contains("Hero"), "%s 的 shop 明细不应回退到 party_state。" % case_label)
		_assert_true(not shop_window.details_label.text.contains("17"), "%s 的 shop 明细不应跨类型转换 display_name。" % case_label)

	var formal_member_option := _make_formal_member_option({
		"member_id": "explicit_member",
		"display_name": "正式队员",
		"roster_role": "远征",
	})
	settlement_window.show_settlement(_make_settlement_window_data([_make_formal_settlement_service_entry()], {
		"member_options": [formal_member_option],
	}))
	await process_frame
	_assert_eq(settlement_window.member_selector.item_count, 1, "正式 explicit settlement member option 应渲染一个选择项。")
	if settlement_window.member_selector.item_count > 0:
		_assert_true(settlement_window.member_selector.get_item_text(0).contains("正式队员"), "正式 explicit settlement member option 应使用 display_name 渲染选择项。")
		_assert_true(not settlement_window.member_selector.get_item_text(0).contains("explicit_member"), "正式 explicit settlement member option 不应显示 member_id。")
	_assert_true(settlement_window.member_state_label.text.contains("成员：正式队员"), "正式 explicit settlement member option 应渲染成员摘要。")
	_assert_true(not settlement_window.member_state_label.text.contains("Hero"), "正式 explicit settlement member option 不应回退到 party_state。")

	shop_window.show_shop(_make_formal_shop_window_data([_make_formal_shop_window_entry()], {
		"party_state": _make_party_state(),
		"member_options": [formal_member_option],
	}))
	await process_frame
	_assert_eq(shop_window.member_selector.item_count, 1, "正式 explicit shop member option 应渲染一个选择项。")
	if shop_window.member_selector.item_count > 0:
		_assert_true(shop_window.member_selector.get_item_text(0).contains("正式队员"), "正式 explicit shop member option 应使用 display_name 渲染选择项。")
		_assert_true(not shop_window.member_selector.get_item_text(0).contains("explicit_member"), "正式 explicit shop member option 不应显示 member_id。")
	_assert_true(shop_window.member_state_label.text.contains("成员：正式队员"), "正式 explicit shop member option 应渲染成员摘要。")
	_assert_true(shop_window.details_label.text.contains("当前成员：正式队员"), "正式 explicit shop member option 应渲染明细中的当前成员。")
	_assert_true(not shop_window.member_state_label.text.contains("Hero"), "正式 explicit shop member option 不应回退到 party_state。")
	_assert_true(not shop_window.details_label.text.contains("explicit_member"), "正式 explicit shop member option 明细不应显示 member_id。")

	settlement_window.queue_free()
	shop_window.queue_free()
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
		_assert_true(not String(settlement_window_data.get("feedback_text", "")).is_empty(), "正式 settlement window data 应显式提供 feedback_text。")
		_assert_true(settlement_window_data.get("footprint_size", null) is Vector2i, "正式 settlement window data 应显式提供 Vector2i footprint_size。")
		_assert_true(not String(settlement_window_data.get("faction_id", "")).is_empty(), "正式 settlement window data 应显式提供 faction_id。")
		_assert_member_option_payload(settlement_window_data, "settlement window data")
		settlement_window.show_settlement(settlement_window_data)
		await process_frame
		_assert_true(settlement_window.member_selector.visible, "正式 settlement window data 应显示成员选择器。")
		_assert_true(settlement_window.member_state_label.text.contains("HP "), "正式 settlement window data 应渲染成员 HP/MP 摘要。")
		_assert_true(settlement_window.member_state_label.text.contains("状态：当前队长"), "正式 settlement window data 应标出当前队长。")

		var shop_open_result: Dictionary = settlement_facade.command_execute_settlement_action(String(settlement_service.get("action_id", "")))
		_assert_true(bool(shop_open_result.get("ok", false)), "正式 runtime 应能从据点窗口打开 shop modal。")
		var shop_window_data := settlement_facade.get_shop_window_data()
		_assert_shop_modal_top_level_payload(shop_window_data, "shop", "shop window data")
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
		_assert_shop_modal_top_level_payload(stagecoach_window_data, "stagecoach", "stagecoach window data")
		_assert_member_option_payload(stagecoach_window_data, "stagecoach window data")
		_assert_eq(String(stagecoach_window_data.get("panel_kind", "")), "stagecoach", "正式 stagecoach window data 应显式标记 panel_kind=stagecoach。")
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


func _test_character_info_window_rejects_legacy_payload() -> void:
	var window = CHARACTER_INFO_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	window.show_character({
		"display_name": "流浪斥候",
		"meta_label": "",
		"type_label": "世界 NPC",
		"faction_label": "中立",
		"coord": Vector2i(4, 9),
		"status_label": "可见提示单位",
	})
	await process_frame

	_assert_true(not window.visible, "旧 payload 缺少正式 sections 时应直接拒绝展示。")
	_assert_eq(window.sections_container.get_child_count(), 0, "旧 payload 不应再补出身份信息 section。")
	_assert_eq(window.meta_label.text, "", "旧 payload 不应再通过 type/faction/coord 拼接 meta。")
	_assert_eq(window.status_label.text, "", "旧 payload 被拒绝后不应渲染 status_label。")
	_assert_true(not window.status_block.visible, "旧 payload 被拒绝后状态块应保持隐藏。")

	window.show_character({
		"display_name": "旧段落斥候",
		"meta_label": "世界 NPC",
		"sections": [
			{
				"title": "技能摘要",
				"body": "旧 body 字段不属于当前 section schema。",
			},
		],
		"status_label": "",
	})
	await process_frame

	_assert_true(not window.visible, "section 缺少正式 entries 数组时应直接拒绝展示。")
	_assert_eq(window.sections_container.get_child_count(), 0, "旧 body section 不应被转换成 text entry。")

	window.show_character({
		"display_name": "旧条目斥候",
		"meta_label": "世界 NPC",
		"sections": [
			{
				"title": "装备摘要",
				"entries": [
					"塔盾",
				],
			},
		],
		"status_label": "",
	})
	await process_frame

	_assert_true(not window.visible, "字符串 entry 不属于当前 section schema，应直接拒绝展示。")
	_assert_eq(window.sections_container.get_child_count(), 0, "字符串 entry 不应被转换成 text entry。")

	window.show_character({
		"meta_label": "世界 NPC",
		"sections": [
			{
				"title": "基础概览",
				"entries": [
					{
						"text": "缺少 display_name。",
					},
				],
			},
		],
		"status_label": "",
	})
	await process_frame

	_assert_true(not window.visible, "缺少 display_name 的人物信息 payload 应拒绝。")

	window.show_character({
		"display_name": "数字 meta 斥候",
		"meta_label": 17,
		"sections": [
			{
				"title": "基础概览",
				"entries": [
					{
						"text": "meta_label 不应跨类型转字符串。",
					},
				],
			},
		],
		"status_label": "",
	})
	await process_frame

	_assert_true(not window.visible, "非 String meta_label 不应被 String() 兼容转换。")

	window.show_character({
		"display_name": "数字状态斥候",
		"meta_label": "",
		"sections": [
			{
				"title": "基础概览",
				"entries": [
					{
						"text": "status_label 不应跨类型转字符串。",
					},
				],
			},
		],
		"status_label": 0,
	})
	await process_frame

	_assert_true(not window.visible, "非 String status_label 不应被 String() 兼容转换。")

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
				"entries": [
					{
						"text": "持盾反击后为相邻友军提供掩护。",
					},
				],
			},
			{
				"title": "装备摘要",
				"entries": [
					{
						"text": "塔盾",
					},
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
	_assert_eq(window.meta_label.text, "战斗单位  |  玩家前排", "显式 meta_label 应作为正式 meta 文案渲染。")
	_assert_eq(window.status_label.text, "准备格挡", "section payload 仍应复用 status_label。")

	var rendered_texts := _collect_label_texts(window.sections_container)
	_assert_true(rendered_texts.has("基础概览"), "section payload 应渲染基础概览标题。")
	_assert_true(rendered_texts.has("职业："), "section payload 应渲染 label/value 条目。")
	_assert_true(rendered_texts.has("守卫"), "section payload 应渲染职业值。")
	_assert_true(rendered_texts.has("技能摘要"), "section payload 应渲染 text section 标题。")
	_assert_true(rendered_texts.has("持盾反击后为相邻友军提供掩护。"), "section payload 应渲染 text entry。")
	_assert_true(rendered_texts.has("装备摘要"), "section payload 应渲染多条 text entry。")
	_assert_true(rendered_texts.has("塔盾"), "section payload 应渲染 text entry。")
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
		_assert_true(not context.has("type_label"), "正式 runtime character info context 不应再输出 legacy type_label。")
		_assert_true(not context.has("faction_label"), "正式 runtime character info context 不应再输出 legacy faction_label。")
		_assert_true(not context.has("coord"), "正式 runtime character info context 不应再输出 legacy coord。")

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


func _test_runtime_character_info_context_rejects_invalid_world_npc_schema() -> void:
	var runtime_bundle := await _create_runtime_bundle("service_ui_character_info_bad_world_npc")
	if runtime_bundle.is_empty():
		return

	var facade: GameRuntimeFacade = runtime_bundle.get("facade") as GameRuntimeFacade
	var game_session = runtime_bundle.get("game_session")
	var npc_record := _find_first_world_npc_record(game_session.get_world_data())
	_assert_true(not npc_record.is_empty(), "坏 world NPC schema 回归前置：测试世界应至少生成一个 world NPC。")
	if not npc_record.is_empty():
		var npc_coord: Vector2i = npc_record.get("coord", Vector2i(-1, -1))
		var original_display_name := String(npc_record.get("display_name", ""))
		var original_faction_id := String(npc_record.get("faction_id", ""))

		npc_record.erase("display_name")
		var opened_missing_name := facade._try_open_character_info_at_world_coord(npc_coord)
		_assert_true(not opened_missing_name, "缺少 display_name 的 world NPC 不应回退成 NPC 打开人物信息窗。")
		_assert_true(facade.get_character_info_context().is_empty(), "缺少 display_name 的 world NPC 不应留下人物信息 context。")

		npc_record["display_name"] = original_display_name
		npc_record.erase("faction_id")
		var opened_missing_faction := facade._try_open_character_info_at_world_coord(npc_coord)
		_assert_true(not opened_missing_faction, "缺少 faction_id 的 world NPC 不应回退成 neutral 打开人物信息窗。")
		_assert_true(facade.get_character_info_context().is_empty(), "缺少 faction_id 的 world NPC 不应留下人物信息 context。")

		npc_record["faction_id"] = original_faction_id
		npc_record["faction_id"] = 17
		var opened_numeric_faction := facade._try_open_character_info_at_world_coord(npc_coord)
		_assert_true(not opened_numeric_faction, "非 String faction_id 的 world NPC 不应回退成 neutral 打开人物信息窗。")
		_assert_true(facade.get_character_info_context().is_empty(), "非 String faction_id 的 world NPC 不应留下人物信息 context。")

		npc_record["faction_id"] = original_faction_id

	await _cleanup_runtime_bundle(runtime_bundle)


func _test_battle_end_reward_window_uses_large_modal_metrics() -> void:
	var window = MASTERY_REWARD_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	var panel := window.get_node("CenterContainer/Panel") as PanelContainer
	var title_label := window.get_node("%TitleLabel") as Label
	var meta_label := window.get_node("%MetaLabel") as Label
	var details_label := window.get_node("%DetailsLabel") as RichTextLabel
	var confirm_button := window.get_node("%ConfirmButton") as Button

	window.show_reward(_build_reward_stub(), 2)
	await process_frame

	_assert_true(panel != null and panel.custom_minimum_size.x >= 900.0 and panel.custom_minimum_size.y >= 600.0, "战后奖励确认窗应使用更大的 modal 面板尺寸。")
	_assert_true(int(title_label.get("theme_override_font_sizes/font_size")) >= 38, "战后奖励确认窗标题字号应明显放大。")
	_assert_true(int(meta_label.get("theme_override_font_sizes/font_size")) >= 20, "战后奖励确认窗副标题字号应明显放大。")
	_assert_true(details_label != null and details_label.custom_minimum_size.y >= 360.0, "战后奖励确认窗正文区域高度应明显增加。")
	_assert_true(int(details_label.get("theme_override_font_sizes/normal_font_size")) >= 20, "战后奖励确认窗正文字号应明显放大。")
	_assert_true(confirm_button != null and confirm_button.custom_minimum_size.x >= 200.0 and confirm_button.custom_minimum_size.y >= 60.0, "战后奖励确认窗确认按钮应更大。")
	_assert_true(int(confirm_button.get("theme_override_font_sizes/font_size")) >= 22, "战后奖励确认窗确认按钮字号应明显放大。")

	window.queue_free()
	await process_frame


func _test_game_over_confirmation_uses_shared_large_modal_metrics() -> void:
	var window = SUBMAP_ENTRY_WINDOW_SCENE.instantiate()
	root.add_child(window)
	await process_frame

	var panel := window.get_node("CenterContainer/Panel") as PanelContainer
	var title_label := window.get_node("CenterContainer/Panel/MarginContainer/Layout/TitleLabel") as Label
	var description_label := window.get_node("CenterContainer/Panel/MarginContainer/Layout/DescriptionLabel") as Label
	var return_button := window.get_node("CenterContainer/Panel/MarginContainer/Layout/ButtonRow/ConfirmButton") as Button
	var cancel_button := window.get_node("CenterContainer/Panel/MarginContainer/Layout/ButtonRow/CancelButton") as Button

	window.show_prompt({
		"title": "战斗失败",
		"description": "主角已阵亡，本次旅程结束。",
		"confirm_text": "返回标题",
		"cancel_visible": false,
		"dismiss_on_shade": false,
		"accept_input_enabled": true,
		"panel_min_size": Vector2(760, 320),
		"title_font_size": 40,
		"description_font_size": 24,
		"confirm_button_min_size": Vector2(240, 64),
		"confirm_button_font_size": 24,
		"margin_left": 40,
		"margin_top": 34,
		"margin_right": 40,
		"margin_bottom": 34,
		"layout_separation": 26,
	})
	await process_frame

	_assert_true(panel != null and panel.custom_minimum_size.x >= 700.0 and panel.custom_minimum_size.y >= 300.0, "game over 应复用更大的共享确认面板尺寸。")
	_assert_true(int(title_label.get("theme_override_font_sizes/font_size")) >= 38, "game over 共享确认窗标题字号应明显放大。")
	_assert_true(int(description_label.get("theme_override_font_sizes/font_size")) >= 22, "game over 共享确认窗描述字号应明显放大。")
	_assert_true(return_button != null and return_button.custom_minimum_size.x >= 220.0 and return_button.custom_minimum_size.y >= 60.0, "game over 共享确认窗确认按钮应更大。")
	_assert_true(int(return_button.get("theme_override_font_sizes/font_size")) >= 22, "game over 共享确认窗确认按钮字号应明显放大。")
	_assert_true(cancel_button != null and not cancel_button.visible and cancel_button.disabled, "game over 共享确认窗应隐藏取消按钮。")

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


func _make_formal_member_option(overrides: Dictionary = {}) -> Dictionary:
	var option := {
		"member_id": "hero",
		"display_name": "Hero",
		"roster_role": "上阵",
		"is_leader": true,
		"current_hp": 18,
		"current_mp": 6,
	}
	for key in overrides.keys():
		option[key] = overrides[key]
	return option


func _make_settlement_window_data(available_services: Array, overrides: Dictionary = {}) -> Dictionary:
	var data := {
		"settlement_id": "spring_village_01",
		"display_name": "春泉村",
		"tier_name": "村庄",
		"footprint_size": Vector2i(1, 1),
		"faction_id": "neutral",
		"party_state": _make_party_state(),
		"available_services": available_services,
		"facilities": [],
		"service_npcs": [],
		"state_summary_text": "访问：是",
		"feedback_text": "点击服务继续，或切换成员后再操作。",
	}
	for key in overrides.keys():
		data[key] = overrides[key]
	return data


func _make_formal_shop_window_data(entries: Array, overrides: Dictionary = {}) -> Dictionary:
	var data := {
		"settlement_id": "spring_village_01",
		"action_id": "service:shop",
		"panel_kind": "shop",
		"title": "春泉村 · 集市",
		"meta": "商店：集市  |  金币：100",
		"summary_text": "持有金币：100",
		"state_summary_text": "",
		"confirm_label": "确认交易",
		"cancel_label": "返回据点",
		"show_member_selector": true,
		"entry_title": "交易条目",
		"summary_title": "交易概况",
		"state_title": "交易状态",
		"cost_title": "交易费用",
		"details_title": "交易说明",
		"member_title": "交易成员",
		"empty_state_label": "状态：暂无商品",
		"empty_cost_label": "费用：暂无商品",
		"empty_details_text": "当前没有可交易条目。",
		"entries": entries,
	}
	for key in overrides.keys():
		data[key] = overrides[key]
	return data


func _make_formal_shop_window_entry(overrides: Dictionary = {}) -> Dictionary:
	var entry := {
		"entry_id": "buy:healing_potion",
		"display_name": "治疗药水",
		"summary_text": "库存 3",
		"details_text": "恢复少量生命。",
		"state_label": "状态：可购",
		"cost_label": "单价 10 金",
		"is_enabled": true,
		"disabled_reason": "",
		"shop_action": "buy",
	}
	for key in overrides.keys():
		entry[key] = overrides[key]
	return entry


func _make_formal_stagecoach_window_data(entries: Array, overrides: Dictionary = {}) -> Dictionary:
	var data := {
		"settlement_id": "spring_village_01",
		"action_id": "service:stagecoach",
		"panel_kind": "stagecoach",
		"title": "春泉村 · 驿站路线",
		"meta": "驿站：春泉村  |  金币：100",
		"summary_text": "持有金币：100",
		"state_summary_text": "选择一个已访问据点并支付路费后即可启程。",
		"confirm_label": "确认出发",
		"cancel_label": "返回据点",
		"show_member_selector": true,
		"entry_title": "可选路线",
		"summary_title": "行程概况",
		"state_title": "行程状态",
		"cost_title": "行程费用",
		"details_title": "行程说明",
		"member_title": "出发成员",
		"empty_state_label": "状态：暂无路线",
		"empty_cost_label": "费用：暂无路线",
		"empty_details_text": "当前没有可用路线。",
		"entries": entries,
	}
	for key in overrides.keys():
		data[key] = overrides[key]
	return data


func _make_formal_stagecoach_window_entry(overrides: Dictionary = {}) -> Dictionary:
	var entry := {
		"entry_id": "travel:north_outpost",
		"display_name": "北境路线",
		"target_settlement_id": "north_outpost",
		"summary_text": "边境哨站",
		"details_text": "前往已访问的北境哨站。",
		"state_label": "状态：可出发",
		"cost_label": "路费 10 金",
		"is_enabled": true,
		"disabled_reason": "",
	}
	for key in overrides.keys():
		entry[key] = overrides[key]
	return entry


func _make_formal_settlement_service_entry(overrides: Dictionary = {}) -> Dictionary:
	var service := {
		"action_id": "service:shop",
		"panel_kind": "shop",
		"facility_name": "集市",
		"npc_name": "商人",
		"service_type": "交易",
		"interaction_script_id": "service_basic_supply",
		"is_enabled": true,
		"disabled_reason": "",
		"cost_label": "按商品计价",
		"state_label": "状态：可用",
		"summary_text": "集市 · 商人 · 交易",
	}
	for key in overrides.keys():
		service[key] = overrides[key]
	return service


func _make_formal_facility_entry(overrides: Dictionary = {}) -> Dictionary:
	var facility := {
		"facility_id": "facility_market_01",
		"display_name": "集市",
		"slot_tag": "trade",
		"interaction_type": "market",
	}
	for key in overrides.keys():
		facility[key] = overrides[key]
	return facility


func _make_formal_resident_entry(overrides: Dictionary = {}) -> Dictionary:
	var resident := {
		"npc_id": "npc_merchant_01",
		"display_name": "商人",
		"service_type": "交易",
		"facility_name": "集市",
	}
	for key in overrides.keys():
		resident[key] = overrides[key]
	return resident


func _build_reward_stub():
	var reward := PendingCharacterReward.new()
	reward.reward_id = &"ui_reward_stub"
	reward.member_id = &"hero"
	reward.member_name = "Hero"
	reward.source_type = &"battle_skill"
	reward.source_id = &"battle_skill"
	reward.source_label = "战斗评分"
	reward.summary_text = "本次战斗中表现出色，获得成长奖励。"
	var entry := PendingCharacterRewardEntry.new()
	entry.entry_type = &"skill_mastery"
	entry.target_id = &"sword_mastery"
	entry.target_label = "剑术"
	entry.amount = 3
	entry.reason_text = "连战后稳步成长"
	reward.entries = [entry]
	return reward


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


func _find_first_world_npc_record(world_data: Dictionary) -> Dictionary:
	for npc_variant in world_data.get("world_npcs", []):
		if npc_variant is not Dictionary:
			continue
		return npc_variant as Dictionary
	return {}


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
	_assert_true(first_option.has("display_name") and first_option["display_name"] is String and not String(first_option["display_name"]).strip_edges().is_empty(), "%s 的成员选项应包含非空 String display_name。" % label)
	_assert_true(first_option.has("is_leader"), "%s 的成员选项应包含 is_leader。" % label)
	_assert_true(first_option.has("current_hp"), "%s 的成员选项应包含 current_hp。" % label)
	_assert_true(first_option.has("current_mp"), "%s 的成员选项应包含 current_mp。" % label)
	_assert_true(String(first_option.get("roster_role", "")) != "", "%s 的成员选项应包含可读 roster_role。" % label)


func _assert_shop_modal_top_level_payload(window_data: Dictionary, expected_panel_kind: String, label: String) -> void:
	for field_name in [
		"settlement_id",
		"action_id",
		"panel_kind",
		"title",
		"meta",
		"summary_text",
		"confirm_label",
		"cancel_label",
		"entry_title",
		"summary_title",
		"state_title",
		"cost_title",
		"details_title",
		"member_title",
		"empty_state_label",
		"empty_cost_label",
		"empty_details_text",
	]:
		_assert_true(window_data.has(field_name) and window_data[field_name] is String and not String(window_data[field_name]).strip_edges().is_empty(), "%s 应显式提供非空 String %s。" % [label, field_name])
	_assert_true(window_data.has("state_summary_text") and window_data["state_summary_text"] is String, "%s 应显式提供 String state_summary_text。" % label)
	_assert_true(window_data.has("show_member_selector") and window_data["show_member_selector"] is bool, "%s 应显式提供 bool show_member_selector。" % label)
	_assert_eq(String(window_data.get("panel_kind", "")), expected_panel_kind, "%s 应显式标记 panel_kind=%s。" % [label, expected_panel_kind])


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
