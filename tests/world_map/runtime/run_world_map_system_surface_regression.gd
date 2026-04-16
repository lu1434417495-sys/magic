extends SceneTree

const WorldMapRuntimeProxy = preload("res://scripts/systems/world_map_runtime_proxy.gd")
const WorldMapSystem = preload("res://scripts/systems/world_map_system.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_world_map_system_does_not_expose_runtime_passthrough_surface()
	_test_world_map_runtime_proxy_keeps_expected_contract()

	if _failures.is_empty():
		print("World map system surface regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("World map system surface regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_world_map_system_does_not_expose_runtime_passthrough_surface() -> void:
	var system := WorldMapSystem.new()

	_assert_true(system.has_method("_render_from_runtime"), "WorldMapSystem 应保留渲染同步入口。")
	_assert_true(system.has_method("_on_world_map_cell_clicked"), "WorldMapSystem 应保留场景回调。")
	_assert_true(system.has_method("_on_character_reward_confirmed"), "WorldMapSystem 应保留窗口回调。")

	var forbidden_methods: Array[String] = [
		"get_status_text",
		"get_active_modal_id",
		"get_active_settlement_id",
		"build_headless_snapshot",
		"build_text_snapshot",
		"command_world_move",
		"command_world_select",
		"command_open_settlement",
		"command_world_inspect",
		"command_open_party",
		"command_select_party_member",
		"command_set_party_leader",
		"command_move_member_to_active",
		"command_move_member_to_reserve",
		"command_open_party_warehouse",
		"command_warehouse_discard_one",
		"command_warehouse_discard_all",
		"command_warehouse_use_item",
		"command_execute_settlement_action",
		"command_battle_tick",
		"command_battle_select_skill",
		"command_battle_cycle_variant",
		"command_battle_clear_skill",
		"command_battle_move_to",
		"command_battle_move_direction",
		"command_battle_wait_or_resolve",
		"command_battle_inspect",
		"command_confirm_pending_reward",
		"command_choose_promotion",
		"command_close_active_modal",
		"apply_party_roster",
		"submit_promotion_choice",
		"cancel_promotion_choice",
		"confirm_active_reward",
		"reset_battle_focus",
		"select_world_cell",
		"inspect_world_cell",
		"select_battle_cell",
		"inspect_battle_cell",
	]

	for method_name in forbidden_methods:
		_assert_true(
			not system.has_method(method_name),
			"WorldMapSystem 不应再暴露 %s 这类 runtime 透传 API。" % method_name
		)

	system.free()


func _test_world_map_runtime_proxy_keeps_expected_contract() -> void:
	var proxy := WorldMapRuntimeProxy.new()

	var expected_methods: Array[String] = [
		"get_status_text",
		"get_active_modal_id",
		"get_active_settlement_id",
		"get_active_map_id",
		"get_pending_battle_start_prompt",
		"build_headless_snapshot",
		"build_text_snapshot",
		"command_world_move",
		"command_confirm_submap_entry",
		"command_confirm_battle_start",
		"command_return_from_submap",
		"command_battle_wait_or_resolve",
		"reset_battle_focus",
		"select_world_cell",
		"select_battle_cell",
	]

	for method_name in expected_methods:
		_assert_true(
			proxy.has_method(method_name),
			"WorldMapRuntimeProxy 应保留 %s 作为场景层正式边界。" % method_name
		)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)
