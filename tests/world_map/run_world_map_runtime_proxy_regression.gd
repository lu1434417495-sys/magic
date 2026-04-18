extends SceneTree

const WorldMapRuntimeProxy = preload("res://scripts/systems/world_map_runtime_proxy.gd")

var _failures: Array[String] = []


class MockRuntime:
	extends RefCounted

	var status_text := "runtime-status"
	var active_modal_id := "party"
	var active_settlement_id := "settlement_alpha"
	var active_map_id := "ashen_ashlands"
	var active_map_display_name := "灰烬地图"
	var submap_return_hint_text := "点击任意地点返回原位置。"
	var player_visible_on_world_map := false
	var pending_submap_prompt := {
		"title": "进入灰烬地图",
		"target_display_name": "灰烬地图",
	}
	var pending_battle_start_prompt := {
		"title": "开始战斗",
		"confirm_text": "开始战斗",
	}
	var selected_battle_skill_target_unit_ids: Array[StringName] = [&"enemy_alpha", &"enemy_beta"]
	var headless_snapshot := {
		"status": {
			"view": "world",
			"text": "runtime-status",
		},
	}
	var text_snapshot := "runtime-text"
	var calls: Array[Dictionary] = []

	func get_status_text() -> String:
		return status_text

	func get_active_modal_id() -> String:
		return active_modal_id

	func get_active_settlement_id() -> String:
		return active_settlement_id

	func get_active_map_id() -> String:
		return active_map_id

	func get_active_map_display_name() -> String:
		return active_map_display_name

	func get_submap_return_hint_text() -> String:
		return submap_return_hint_text

	func is_player_visible_on_world_map() -> bool:
		return player_visible_on_world_map

	func get_pending_submap_prompt() -> Dictionary:
		return pending_submap_prompt

	func get_pending_battle_start_prompt() -> Dictionary:
		return pending_battle_start_prompt

	func get_selected_battle_skill_target_unit_ids() -> Array[StringName]:
		return selected_battle_skill_target_unit_ids

	func build_headless_snapshot() -> Dictionary:
		calls.append({
			"method": "build_headless_snapshot",
			"args": [],
		})
		return headless_snapshot

	func build_text_snapshot() -> String:
		calls.append({
			"method": "build_text_snapshot",
			"args": [],
		})
		return text_snapshot

	func command_world_move(direction: Vector2i, count: int = 1) -> Dictionary:
		var result := {
			"ok": true,
			"message": "moved",
			"battle_refresh_mode": "",
		}
		calls.append({
			"method": "command_world_move",
			"args": [direction, count],
			"result": result,
		})
		return result

	func command_battle_select_skill(slot_index: int) -> Dictionary:
		var result := {
			"ok": true,
			"message": "skill-selected",
			"battle_refresh_mode": "overlay",
		}
		calls.append({
			"method": "command_battle_select_skill",
			"args": [slot_index],
			"result": result,
		})
		return result

	func command_invalid_result():
		calls.append({
			"method": "command_invalid_result",
			"args": [],
		})
		return "invalid-result"

	func is_submap_active() -> bool:
		return true

	func command_confirm_submap_entry() -> Dictionary:
		var result := {
			"ok": true,
			"message": "submap-entered",
			"battle_refresh_mode": "",
		}
		calls.append({
			"method": "command_confirm_submap_entry",
			"args": [],
			"result": result,
		})
		return result

	func command_confirm_battle_start() -> Dictionary:
		var result := {
			"ok": true,
			"message": "battle-start-confirmed",
			"battle_refresh_mode": "",
		}
		calls.append({
			"method": "command_confirm_battle_start",
			"args": [],
			"result": result,
		})
		return result


class RenderSpy:
	extends RefCounted

	var calls: Array[Dictionary] = []

	func capture(refresh_world: bool, command_result: Dictionary) -> void:
		calls.append({
			"refresh_world": refresh_world,
			"command_result": command_result.duplicate(true),
		})


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_getters_forward_to_runtime()
	_test_snapshot_methods_forward_to_runtime()
	_test_world_command_delegates_and_renders()
	_test_battle_command_preserves_overlay_refresh_mode()
	_test_invalid_runtime_command_result_surfaces_error_and_renders()
	_test_submap_command_delegates_and_renders()
	_test_battle_start_confirm_command_delegates_and_renders()
	_test_missing_runtime_returns_error_without_render()

	if _failures.is_empty():
		print("World map runtime proxy regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("World map runtime proxy regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_getters_forward_to_runtime() -> void:
	var runtime := MockRuntime.new()
	var render_spy := RenderSpy.new()
	var proxy := WorldMapRuntimeProxy.new()
	proxy.setup(runtime, Callable(render_spy, "capture"))

	_assert_eq(proxy.get_status_text(), "runtime-status", "get_status_text() 应直接读取 runtime。")
	_assert_eq(proxy.get_active_modal_id(), "party", "get_active_modal_id() 应直接读取 runtime。")
	_assert_eq(
		proxy.get_active_settlement_id(),
		"settlement_alpha",
		"get_active_settlement_id() 应直接读取 runtime。"
	)
	_assert_eq(proxy.get_active_map_id(), "ashen_ashlands", "get_active_map_id() 应直接读取 runtime。")
	_assert_eq(proxy.get_active_map_display_name(), "灰烬地图", "get_active_map_display_name() 应直接读取 runtime。")
	_assert_eq(proxy.get_submap_return_hint_text(), "点击任意地点返回原位置。", "get_submap_return_hint_text() 应直接读取 runtime。")
	_assert_true(not proxy.is_player_visible_on_world_map(), "is_player_visible_on_world_map() 应直接读取 runtime。")
	_assert_eq(proxy.get_pending_battle_start_prompt().get("confirm_text", ""), "开始战斗", "get_pending_battle_start_prompt() 应直接读取 runtime。")
	_assert_eq(
		_string_name_array_to_string_array(proxy.get_selected_battle_skill_target_unit_ids()),
		["enemy_alpha", "enemy_beta"],
		"get_selected_battle_skill_target_unit_ids() 应直接读取 runtime。"
	)
	_assert_true(proxy.is_submap_active(), "is_submap_active() 应直接读取 runtime。")
	_assert_eq(render_spy.calls.size(), 0, "纯 getter 调用不应触发 render。")


func _test_snapshot_methods_forward_to_runtime() -> void:
	var runtime := MockRuntime.new()
	var proxy := WorldMapRuntimeProxy.new()
	proxy.setup(runtime, Callable(RenderSpy.new(), "capture"))

	var headless_snapshot: Dictionary = proxy.build_headless_snapshot()
	var text_snapshot := proxy.build_text_snapshot()

	_assert_eq(
		headless_snapshot.get("status", {}).get("text", ""),
		"runtime-status",
		"build_headless_snapshot() 应直接返回 runtime 快照。"
	)
	_assert_eq(text_snapshot, "runtime-text", "build_text_snapshot() 应直接返回 runtime 文本快照。")
	_assert_true(
		_has_call(runtime.calls, "build_headless_snapshot"),
		"build_headless_snapshot() 应调用 runtime。"
	)
	_assert_true(
		_has_call(runtime.calls, "build_text_snapshot"),
		"build_text_snapshot() 应调用 runtime。"
	)


func _test_world_command_delegates_and_renders() -> void:
	var runtime := MockRuntime.new()
	var render_spy := RenderSpy.new()
	var proxy := WorldMapRuntimeProxy.new()
	proxy.setup(runtime, Callable(render_spy, "capture"))

	var result: Dictionary = proxy.command_world_move(Vector2i.LEFT, 2)

	_assert_true(bool(result.get("ok", false)), "world 命令应回传 runtime 的成功结果。")
	_assert_true(_has_call(runtime.calls, "command_world_move"), "world 命令应委托给 runtime。")
	_assert_eq(render_spy.calls.size(), 1, "world 命令执行后应触发一次 render。")
	if render_spy.calls.size() == 1:
		var render_call: Dictionary = render_spy.calls[0]
		_assert_true(bool(render_call.get("refresh_world", false)), "world 命令应请求刷新场景。")
		_assert_eq(
			String(render_call.get("command_result", {}).get("message", "")),
			"moved",
			"render 回调应收到 runtime 返回的命令结果。"
		)


func _test_battle_command_preserves_overlay_refresh_mode() -> void:
	var runtime := MockRuntime.new()
	var render_spy := RenderSpy.new()
	var proxy := WorldMapRuntimeProxy.new()
	proxy.setup(runtime, Callable(render_spy, "capture"))

	var result: Dictionary = proxy.command_battle_select_skill(3)

	_assert_true(_has_call(runtime.calls, "command_battle_select_skill"), "battle 命令应委托给 runtime。")
	_assert_eq(
		String(result.get("battle_refresh_mode", "")),
		"overlay",
		"battle 命令应保留 runtime 返回的 overlay 刷新模式。"
	)
	_assert_eq(render_spy.calls.size(), 1, "battle 命令执行后应触发一次 render。")


func _test_invalid_runtime_command_result_surfaces_error_and_renders() -> void:
	var runtime := MockRuntime.new()
	var render_spy := RenderSpy.new()
	var proxy := WorldMapRuntimeProxy.new()
	proxy.setup(runtime, Callable(render_spy, "capture"))

	var result: Dictionary = proxy._call_runtime_command(&"command_invalid_result")

	_assert_true(_has_call(runtime.calls, "command_invalid_result"), "proxy 应调用 runtime 并处理非 Dictionary 返回。")
	_assert_true(not bool(result.get("ok", true)), "非 Dictionary 返回应转成失败结果。")
	_assert_eq(
		String(result.get("invalid_result_type", "")),
		"String",
		"非 Dictionary 返回应暴露原始返回类型。"
	)
	_assert_eq(render_spy.calls.size(), 1, "非 Dictionary 返回也应触发一次 render。")
	if render_spy.calls.size() == 1:
		_assert_eq(
			render_spy.calls[0].get("command_result", {}),
			result,
			"render 回调应收到转换后的错误结果。"
		)


func _test_submap_command_delegates_and_renders() -> void:
	var runtime := MockRuntime.new()
	var render_spy := RenderSpy.new()
	var proxy := WorldMapRuntimeProxy.new()
	proxy.setup(runtime, Callable(render_spy, "capture"))

	var result: Dictionary = proxy.command_confirm_submap_entry()

	_assert_true(bool(result.get("ok", false)), "submap 命令应回传 runtime 的成功结果。")
	_assert_true(_has_call(runtime.calls, "command_confirm_submap_entry"), "submap 命令应委托给 runtime。")
	_assert_eq(render_spy.calls.size(), 1, "submap 命令执行后应触发一次 render。")


func _test_battle_start_confirm_command_delegates_and_renders() -> void:
	var runtime := MockRuntime.new()
	var render_spy := RenderSpy.new()
	var proxy := WorldMapRuntimeProxy.new()
	proxy.setup(runtime, Callable(render_spy, "capture"))

	var result: Dictionary = proxy.command_confirm_battle_start()

	_assert_true(bool(result.get("ok", false)), "battle start confirm 命令应回传 runtime 的成功结果。")
	_assert_true(_has_call(runtime.calls, "command_confirm_battle_start"), "battle start confirm 命令应委托给 runtime。")
	_assert_eq(render_spy.calls.size(), 1, "battle start confirm 命令执行后应触发一次 render。")


func _test_missing_runtime_returns_error_without_render() -> void:
	var render_spy := RenderSpy.new()
	var proxy := WorldMapRuntimeProxy.new()
	proxy.setup(null, Callable(render_spy, "capture"))

	var result: Dictionary = proxy.command_world_move(Vector2i.RIGHT, 1)

	_assert_true(not bool(result.get("ok", true)), "缺少 runtime 时命令应返回失败。")
	_assert_eq(render_spy.calls.size(), 0, "缺少 runtime 时不应触发 render。")


func _has_call(calls: Array[Dictionary], method_name: String) -> bool:
	for call in calls:
		if String(call.get("method", "")) == method_name:
			return true
	return false


func _string_name_array_to_string_array(values: Array[StringName]) -> Array[String]:
	var result: Array[String] = []
	for value in values:
		result.append(String(value))
	return result


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s 实际=%s 预期=%s" % [message, str(actual), str(expected)])
