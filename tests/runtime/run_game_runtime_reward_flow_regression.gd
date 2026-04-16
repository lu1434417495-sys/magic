extends SceneTree

const GAME_RUNTIME_REWARD_FLOW_HANDLER_SCRIPT = preload("res://scripts/systems/game_runtime_reward_flow_handler.gd")
const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const PENDING_CHARACTER_REWARD_SCRIPT = preload("res://scripts/systems/pending_character_reward.gd")
const PENDING_CHARACTER_REWARD_ENTRY_SCRIPT = preload("res://scripts/systems/pending_character_reward_entry.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_reward_queue_confirmation()
	_test_reward_confirmation_promotion_follow_up()
	_test_world_and_battle_promotion_routes()
	_test_close_active_modal_paths()

	if _failures.is_empty():
		print("Game runtime reward flow regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Game runtime reward flow regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_reward_queue_confirmation() -> void:
	var context := _create_context()
	var runtime = context.get("runtime")
	var handler = context.get("handler")

	runtime._party_state = _build_party_state_with_rewards(["reward_a", "reward_b"])
	runtime._character_management = _FakeCharacterManagement.new(runtime._party_state, false)
	handler.enqueue_pending_character_rewards([_build_reward("reward_c")])
	_assert_eq(runtime._party_state.pending_character_rewards.size(), 3, "奖励入队应同步到 PartyState。")

	var result: Dictionary = handler.command_confirm_pending_reward()
	_assert_true(bool(result.get("ok", false)), "确认奖励命令应成功。")
	_assert_eq(String(runtime._active_reward.reward_id), "reward_b", "确认第一条奖励后应自动展示下一条。")
	_assert_eq(String(runtime._active_modal_id), "reward", "确认奖励后应继续停留在 reward modal。")
	_assert_eq(runtime._party_state.pending_character_rewards.size(), 2, "已确认奖励应从队列移除。")


func _test_reward_confirmation_promotion_follow_up() -> void:
	var context := _create_context()
	var runtime = context.get("runtime")
	var handler = context.get("handler")

	runtime._party_state = _build_party_state_with_rewards(["reward_a"])
	runtime._character_management = _FakeCharacterManagement.new(runtime._party_state, true)
	runtime._active_reward = runtime._party_state.get_next_pending_character_reward()
	runtime._active_modal_id = "reward"

	var result: Dictionary = handler.confirm_active_reward()
	_assert_true(bool(result.get("ok", false)), "直接确认激活奖励应成功。")
	_assert_true(not runtime._pending_world_promotion_prompt.is_empty(), "奖励确认后若需要晋升应生成世界晋升 prompt。")
	_assert_eq(String(runtime._active_modal_id), "promotion", "晋升待确认时 modal 应切换为 promotion。")
	_assert_true(runtime._active_reward == null, "奖励确认后 active reward 应被清空。")
	_assert_true(String(runtime._current_status_message).contains("职业晋升待确认"), "奖励晋升分支应更新提示文案。")


func _test_world_and_battle_promotion_routes() -> void:
	var context := _create_context()
	var runtime = context.get("runtime")
	var handler = context.get("handler")

	runtime._party_state = PARTY_STATE_SCRIPT.new()
	runtime._character_management = _FakeCharacterManagement.new(runtime._party_state, false)
	runtime._pending_world_promotion_prompt = {
		"member_id": "hero",
		"choices": [
			{
				"profession_id": "mage",
				"selection": {"mode": "world"},
			},
		],
	}
	var world_result: Dictionary = handler.command_choose_promotion(&"mage")
	_assert_true(bool(world_result.get("ok", false)), "世界晋升选择命令应成功。")
	_assert_eq(runtime._character_management.last_promote_member_id, &"hero", "世界晋升应路由到角色管理模块。")
	_assert_eq(String(runtime._current_status_message), "hero 完成职业晋升。", "世界晋升应更新状态文本。")
	_assert_true(runtime._pending_world_promotion_prompt.is_empty(), "世界晋升确认后 prompt 应清空。")

	runtime._battle_state = _FakeBattleState.new()
	runtime._battle_runtime = _FakeBattleRuntime.new()
	runtime._pending_promotion_prompt = {
		"member_id": "hero",
		"choices": [
			{
				"profession_id": "warrior",
				"selection": {"mode": "battle"},
			},
		],
	}
	runtime._active_modal_id = "promotion"
	var battle_result: Dictionary = handler.submit_promotion_choice(&"hero", &"warrior", {"mode": "battle"})
	_assert_true(bool(battle_result.get("ok", false)), "战斗晋升提交命令应成功。")
	_assert_eq(runtime._battle_runtime.last_submit_member_id, &"hero", "战斗晋升应提交给战斗运行时。")
	_assert_eq(runtime._battle_runtime.last_submit_profession_id, &"warrior", "战斗晋升应提交正确职业。")
	_assert_eq(runtime._applied_batches.size(), 1, "战斗晋升应触发批次应用。")
	_assert_true(runtime._pending_promotion_prompt.is_empty(), "战斗晋升确认后 prompt 应清空。")
	_assert_eq(String(runtime._active_modal_id), "", "战斗晋升提交后 modal 应关闭。")

	runtime._pending_promotion_prompt = {
		"member_id": "hero",
		"choices": [
			{
				"profession_id": "warrior",
				"selection": {},
			},
		],
	}
	runtime._active_modal_id = "promotion"
	var cancel_result: Dictionary = handler.cancel_promotion_choice()
	_assert_true(bool(cancel_result.get("ok", false)), "取消晋升命令应返回成功结果。")
	_assert_eq(String(runtime._active_modal_id), "promotion", "取消战斗晋升时 modal 应保持打开。")
	_assert_eq(String(runtime._current_status_message), "当前晋升选择必须确认后才能继续战斗。", "取消战斗晋升应提示必须确认。")


func _test_close_active_modal_paths() -> void:
	var context := _create_context()
	var runtime = context.get("runtime")
	var handler = context.get("handler")

	runtime._party_state = _build_party_state_with_rewards(["reward_a"])
	runtime._character_management = _FakeCharacterManagement.new(runtime._party_state, false)
	runtime._active_character_info_context = {
		"display_name": "侦察兵",
	}
	runtime._active_modal_id = "character_info"

	var close_result: Dictionary = handler.command_close_active_modal()
	_assert_true(bool(close_result.get("ok", false)), "关闭人物信息窗应成功。")
	_assert_true(runtime._active_character_info_context.is_empty(), "关闭人物信息窗后上下文应清空。")
	_assert_eq(String(runtime._active_modal_id), "reward", "关闭人物信息窗后应继续展示待领奖励。")

	runtime._active_modal_id = "reward"
	var blocked_result: Dictionary = handler.command_close_active_modal()
	_assert_true(not bool(blocked_result.get("ok", false)), "reward modal 不应直接关闭。")
	_assert_eq(String(runtime._active_modal_id), "reward", "reward modal 被阻止时应保持打开。")
	_assert_eq(String(runtime._current_status_message), "当前角色奖励必须确认后才能继续。", "reward modal 被阻止时应给出明确提示。")


func _create_context() -> Dictionary:
	var runtime := _FakeRuntime.new()
	var handler = GAME_RUNTIME_REWARD_FLOW_HANDLER_SCRIPT.new()
	handler.setup(runtime)
	return {
		"runtime": runtime,
		"handler": handler,
	}


func _build_party_state_with_rewards(reward_ids: Array[String]) -> PartyState:
	var party_state := PARTY_STATE_SCRIPT.new()
	for reward_id in reward_ids:
		party_state.enqueue_pending_character_reward(_build_reward(reward_id))
	return party_state


func _build_reward(reward_id: String) -> PendingCharacterReward:
	var reward := PENDING_CHARACTER_REWARD_SCRIPT.new()
	reward.reward_id = StringName(reward_id)
	reward.member_id = &"hero"
	reward.member_name = "Hero"
	reward.source_id = StringName(reward_id)
	reward.source_label = reward_id
	var entry := PENDING_CHARACTER_REWARD_ENTRY_SCRIPT.new()
	entry.entry_type = &"skill_mastery"
	entry.target_id = StringName("skill_%s" % reward_id)
	entry.target_label = reward_id
	entry.amount = 1
	reward.entries.append(entry)
	return reward


class _FakePromotionDelta extends RefCounted:
	var needs_promotion_modal := false
	var mastery_changes: Array = []
	var knowledge_changes: Array = []
	var attribute_changes: Array = []


class _FakeCharacterManagement extends RefCounted:
	var _party_state: PartyState
	var _needs_promotion_modal := false
	var last_promote_member_id: StringName = &""
	var last_promote_profession_id: StringName = &""
	var last_promote_selection: Dictionary = {}
	var last_applied_reward_id: StringName = &""


	func _init(party_state: PartyState, needs_promotion_modal: bool) -> void:
		_party_state = party_state
		_needs_promotion_modal = needs_promotion_modal


	func get_party_state() -> PartyState:
		return _party_state


	func enqueue_pending_character_rewards(reward_variants: Array) -> void:
		for reward_variant in reward_variants:
			if reward_variant is PendingCharacterReward and not reward_variant.is_empty():
				_party_state.enqueue_pending_character_reward(reward_variant)


	func promote_profession(member_id: StringName, profession_id: StringName, selection: Dictionary):
		last_promote_member_id = member_id
		last_promote_profession_id = profession_id
		last_promote_selection = selection.duplicate(true)
		var delta := _FakePromotionDelta.new()
		delta.needs_promotion_modal = _needs_promotion_modal
		return delta


	func apply_pending_character_reward(reward: PendingCharacterReward):
		last_applied_reward_id = reward.reward_id
		_party_state.remove_pending_character_reward(reward.reward_id)
		var delta := _FakePromotionDelta.new()
		delta.needs_promotion_modal = _needs_promotion_modal
		return delta


class _FakeBattleRuntime extends RefCounted:
	var last_submit_member_id: StringName = &""
	var last_submit_profession_id: StringName = &""
	var last_submit_selection: Dictionary = {}


	func submit_promotion_choice(member_id: StringName, profession_id: StringName, selection: Dictionary):
		last_submit_member_id = member_id
		last_submit_profession_id = profession_id
		last_submit_selection = selection.duplicate(true)
		return {
			"batch": "battle_promotion",
			"member_id": String(member_id),
		}


class _FakeBattleState extends RefCounted:
	pass


class _FakeRuntime extends RefCounted:
	var _active_modal_id := ""
	var _active_character_info_context: Dictionary = {}
	var _pending_promotion_prompt: Dictionary = {}
	var _pending_world_promotion_prompt: Dictionary = {}
	var _active_reward: PendingCharacterReward = null
	var _party_state: PartyState = null
	var _character_management = null
	var _battle_runtime = null
	var _battle_state = null
	var _current_status_message := ""
	var _applied_batches: Array = []


	func get_pending_promotion_prompt() -> Dictionary:
		return _pending_promotion_prompt.duplicate(true)


	func clear_pending_promotion_prompt() -> void:
		_pending_promotion_prompt.clear()


	func get_pending_world_promotion_prompt_state() -> Dictionary:
		return _pending_world_promotion_prompt.duplicate(true)


	func clear_pending_world_promotion_prompt_state() -> void:
		_pending_world_promotion_prompt.clear()


	func set_runtime_active_modal_id(modal_id: String) -> void:
		_active_modal_id = modal_id


	func get_active_reward_state():
		return _active_reward


	func clear_active_reward_state() -> void:
		_active_reward = null


	func set_active_reward_state(reward) -> void:
		_active_reward = reward


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


	func update_status(message: String) -> void:
		_update_status(message)


	func _persist_party_state() -> int:
		return OK


	func persist_party_state() -> int:
		return _persist_party_state()


	func sync_party_state_from_character_management() -> void:
		if _character_management != null:
			_party_state = _character_management.get_party_state()


	func promote_profession(member_id: StringName, profession_id: StringName, selection: Dictionary):
		return _character_management.promote_profession(member_id, profession_id, selection) if _character_management != null else null


	func _build_promotion_prompt(delta, selection_hint: String = "") -> Dictionary:
		return {
			"member_id": "hero",
			"selection_hint": selection_hint,
			"choices": [
				{
					"profession_id": "archer",
					"selection": {"follow_up": true},
				},
			],
		}


	func build_runtime_promotion_prompt(delta, selection_hint: String = "") -> Dictionary:
		return _build_promotion_prompt(delta, selection_hint)


	func _get_member_display_name(member_id: StringName) -> String:
		return String(member_id)


	func get_member_display_name(member_id: StringName) -> String:
		return _get_member_display_name(member_id)


	func _is_battle_active() -> bool:
		return _battle_state != null


	func is_battle_active() -> bool:
		return _is_battle_active()


	func _apply_battle_batch(batch) -> void:
		_applied_batches.append(batch)


	func apply_battle_batch(batch) -> void:
		_apply_battle_batch(batch)


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
