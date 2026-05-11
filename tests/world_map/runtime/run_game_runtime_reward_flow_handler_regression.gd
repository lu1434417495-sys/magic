extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const GameRuntimeFacade = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")
const GameRuntimeRewardFlowHandler = preload("res://scripts/systems/game_runtime/game_runtime_reward_flow_handler.gd")
const PendingCharacterReward = preload("res://scripts/systems/progression/pending_character_reward.gd")
const PendingCharacterRewardEntry = preload("res://scripts/systems/progression/pending_character_reward_entry.gd")
const PartyState = preload("res://scripts/player/progression/party_state.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


class MockRewardFlowHandler:
	extends RefCounted

	var calls: Array[Dictionary] = []

	func get_current_promotion_prompt() -> Dictionary:
		calls.append({"method": "get_current_promotion_prompt", "args": []})
		return {"member_id": "hero"}

	func command_confirm_pending_reward() -> Dictionary:
		return _record("command_confirm_pending_reward")

	func command_choose_promotion(profession_id: StringName) -> Dictionary:
		return _record("command_choose_promotion", [profession_id])

	func command_close_active_modal() -> Dictionary:
		return _record("command_close_active_modal")

	func submit_promotion_choice(member_id: StringName, profession_id: StringName, selection: Dictionary) -> Dictionary:
		return _record("submit_promotion_choice", [member_id, profession_id, selection.duplicate(true)])

	func cancel_promotion_choice() -> Dictionary:
		return _record("cancel_promotion_choice")

	func confirm_active_reward() -> Dictionary:
		return _record("confirm_active_reward")

	func on_character_info_window_closed() -> void:
		_record_void("on_character_info_window_closed")

	func on_promotion_choice_submitted(member_id: StringName, profession_id: StringName, selection: Dictionary) -> void:
		_record_void("on_promotion_choice_submitted", [member_id, profession_id, selection.duplicate(true)])

	func on_promotion_choice_cancelled() -> void:
		_record_void("on_promotion_choice_cancelled")

	func on_character_reward_confirmed() -> void:
		_record_void("on_character_reward_confirmed")

	func enqueue_pending_character_rewards(reward_variants: Array) -> void:
		_record_void("enqueue_pending_character_rewards", [reward_variants.duplicate(true)])

	func present_pending_reward_if_ready() -> bool:
		calls.append({"method": "present_pending_reward_if_ready", "args": []})
		return true

	func _record(method_name: String, args: Array = []) -> Dictionary:
		calls.append({"method": method_name, "args": args.duplicate(true)})
		return {
			"ok": true,
			"message": method_name,
			"battle_refresh_mode": "",
		}

	func _record_void(method_name: String, args: Array = []) -> void:
		calls.append({"method": method_name, "args": args.duplicate(true)})


class MockSettlementHandler:
	extends RefCounted

	var close_calls := 0

	func on_settlement_window_closed() -> void:
		close_calls += 1


class MockWarehouseHandler:
	extends RefCounted

	var close_calls := 0

	func on_party_warehouse_window_closed() -> void:
		close_calls += 1


class MockPartyHandler:
	extends RefCounted

	var close_calls := 0

	func on_party_management_window_closed() -> void:
		close_calls += 1


class MockRuntime:
	extends RefCounted

	var _active_modal_id := ""
	var _pending_promotion_prompt: Dictionary = {}
	var _pending_world_promotion_prompt: Dictionary = {}
	var _active_reward = null
	var _party_state := PartyState.new()
	var _active_character_info_context := {"visible": true}
	var _current_status_message := ""
	var _settlement_command_handler = MockSettlementHandler.new()
	var _warehouse_handler = MockWarehouseHandler.new()
	var _party_command_handler = MockPartyHandler.new()
	var update_status_calls: Array[String] = []

	func _is_battle_active() -> bool:
		return false

	func _command_ok(message: String = "", battle_refresh_mode: String = "") -> Dictionary:
		return {
			"ok": true,
			"message": message,
			"battle_refresh_mode": battle_refresh_mode,
		}

	func _command_error(message: String) -> Dictionary:
		_update_status(message)
		return {
			"ok": false,
			"message": message,
		}

	func _update_status(message: String) -> void:
		_current_status_message = message
		update_status_calls.append(message)

	func build_command_ok(message: String = "", battle_refresh_mode: String = "") -> Dictionary:
		return _command_ok(message, battle_refresh_mode)

	func build_command_error(message: String) -> Dictionary:
		return _command_error(message)

	func get_pending_world_promotion_prompt_state() -> Dictionary:
		return _pending_world_promotion_prompt.duplicate(true)

	func get_active_reward_state():
		return _active_reward

	func set_active_reward_state(reward) -> void:
		_active_reward = reward

	func get_active_modal_id() -> String:
		return _active_modal_id

	func set_runtime_active_modal_id(modal_id: String) -> void:
		_active_modal_id = modal_id

	func update_status(message: String) -> void:
		_update_status(message)

	func is_battle_active() -> bool:
		return _is_battle_active()

	func clear_active_character_info_context() -> void:
		_active_character_info_context.clear()

	func close_settlement_modal() -> void:
		_settlement_command_handler.on_settlement_window_closed()
		_active_modal_id = ""

	func close_contract_board_modal() -> void:
		_active_modal_id = "settlement"

	func close_shop_modal() -> void:
		_active_modal_id = "settlement"

	func close_forge_modal() -> void:
		_active_modal_id = "settlement"

	func close_stagecoach_modal() -> void:
		_active_modal_id = "settlement"

	func close_party_management_modal() -> void:
		_party_command_handler.on_party_management_window_closed()
		_active_modal_id = ""

	func close_party_warehouse_modal() -> void:
		_warehouse_handler.on_party_warehouse_window_closed()
		_active_modal_id = ""

	func get_party_state():
		return _party_state


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_facade_delegates_reward_surface_to_handler()
	_test_reward_handler_routes_modal_close_and_reward_presentation()

	if _failures.is_empty():
		print("Game runtime reward flow handler regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Game runtime reward flow handler regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_facade_delegates_reward_surface_to_handler() -> void:
	var facade := GameRuntimeFacade.new()
	var handler := MockRewardFlowHandler.new()
	facade._reward_flow_handler = handler

	_assert_eq(String(facade.command_confirm_pending_reward().get("message", "")), "command_confirm_pending_reward", "command_confirm_pending_reward() 应委托给 reward handler。")
	_assert_eq(String(facade.command_choose_promotion(&"warrior").get("message", "")), "command_choose_promotion", "command_choose_promotion() 应委托给 reward handler。")
	_assert_eq(String(facade.command_close_active_modal().get("message", "")), "command_close_active_modal", "command_close_active_modal() 应委托给 reward handler。")
	_assert_eq(String(facade.submit_promotion_choice(&"hero", &"warrior", {}).get("message", "")), "submit_promotion_choice", "submit_promotion_choice() 应委托给 reward handler。")
	_assert_eq(String(facade.cancel_promotion_choice().get("message", "")), "cancel_promotion_choice", "cancel_promotion_choice() 应委托给 reward handler。")
	_assert_eq(String(facade.confirm_active_reward().get("message", "")), "confirm_active_reward", "confirm_active_reward() 应委托给 reward handler。")
	_assert_eq(String(facade._get_current_promotion_prompt().get("member_id", "")), "hero", "_get_current_promotion_prompt() 应委托给 reward handler。")

	facade._on_character_info_window_closed()
	facade._on_promotion_choice_submitted(&"hero", &"warrior", {})
	facade._on_promotion_choice_cancelled()
	facade._on_character_reward_confirmed()
	facade._enqueue_pending_character_rewards([{"member_id": "hero"}])
	_assert_true(facade._present_pending_reward_if_ready(), "_present_pending_reward_if_ready() 应委托给 reward handler。")

	_assert_true(_has_call(handler.calls, "on_character_info_window_closed"), "_on_character_info_window_closed() 应委托给 reward handler。")
	_assert_true(_has_call(handler.calls, "on_promotion_choice_submitted"), "_on_promotion_choice_submitted() 应委托给 reward handler。")
	_assert_true(_has_call(handler.calls, "on_promotion_choice_cancelled"), "_on_promotion_choice_cancelled() 应委托给 reward handler。")
	_assert_true(_has_call(handler.calls, "on_character_reward_confirmed"), "_on_character_reward_confirmed() 应委托给 reward handler。")
	_assert_true(_has_call(handler.calls, "enqueue_pending_character_rewards"), "_enqueue_pending_character_rewards() 应委托给 reward handler。")


func _test_reward_handler_routes_modal_close_and_reward_presentation() -> void:
	var runtime := MockRuntime.new()
	var handler := GameRuntimeRewardFlowHandler.new()
	handler.setup(runtime)

	var reward := PendingCharacterReward.new()
	reward.reward_id = &"test_reward"
	reward.member_id = &"hero"
	reward.member_name = "Hero"
	reward.source_type = &"test_reward"
	reward.source_id = &"test_reward"
	reward.source_label = "测试奖励"
	reward.summary_text = "测试奖励"
	var entry := PendingCharacterRewardEntry.new()
	entry.entry_type = &"skill_mastery"
	entry.target_id = &"test_skill"
	entry.target_label = "测试技能"
	entry.amount = 1
	entry.reason_text = "测试奖励"
	reward.entries = [entry]
	runtime._party_state.pending_character_rewards = [reward]
	_assert_true(handler.present_pending_reward_if_ready(), "存在待领奖励时应进入 reward modal。")
	_assert_eq(runtime._active_modal_id, "reward", "奖励弹窗应切换 modal 到 reward。")
	_assert_true(runtime._active_reward == reward, "奖励呈现应把队首奖励提为 active reward。")

	runtime._active_modal_id = "settlement"
	_assert_true(bool(handler.command_close_active_modal().get("ok", false)), "settlement modal 应可通过 reward handler 路由关闭。")
	_assert_eq(runtime._settlement_command_handler.close_calls, 1, "settlement close 应转发给 settlement handler。")

	runtime._active_modal_id = "warehouse"
	handler.command_close_active_modal()
	_assert_eq(runtime._warehouse_handler.close_calls, 1, "warehouse close 应转发给 warehouse handler。")

	runtime._active_modal_id = "party"
	handler.command_close_active_modal()
	_assert_eq(runtime._party_command_handler.close_calls, 1, "party close 应转发给 party handler。")

	runtime._active_modal_id = "character_info"
	handler.command_close_active_modal()
	_assert_true(runtime._active_character_info_context.is_empty(), "character_info close 应清空人物信息上下文。")
	_assert_eq(runtime._current_status_message, "已关闭人物信息窗。", "character_info close 应刷新状态文案。")


func _has_call(calls: Array[Dictionary], method_name: String) -> bool:
	for call in calls:
		if String(call.get("method", "")) == method_name:
			return true
	return false


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
