## 文件说明：该脚本属于战斗结算 contract 回归相关的测试脚本，集中覆盖 canonical 结果对象、战斗结束生成时机和 battle session handoff。
## 审查重点：重点核对 battle end 时是否生成 BattleResolutionResult，以及 session facade 是否稳定传递 canonical reward queue。
## 备注：该回归只验证 battle-side contract，不触碰旧 pending_mastery_rewards 兼容链。

extends SceneTree

const BattleEventBatch = preload("res://scripts/systems/battle_event_batch.gd")
const BattleResolutionResult = preload("res://scripts/systems/battle_resolution_result.gd")
const BattleRuntimeModule = preload("res://scripts/systems/battle_runtime_module.gd")
const BattleSessionFacade = preload("res://scripts/systems/battle_session_facade.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleTimelineState = preload("res://scripts/systems/battle_timeline_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const PendingCharacterReward = preload("res://scripts/systems/pending_character_reward.gd")
const PendingCharacterRewardEntry = preload("res://scripts/systems/pending_character_reward_entry.gd")

var _failures: Array[String] = []


class _FakeBattleSelection extends RefCounted:
	func clear_battle_skill_selection(_keep_overlay: bool = false) -> void:
		pass


class _FakeBattleRuntimeWithResult extends RefCounted:
	var state: BattleState = null
	var resolution_result = null
	var consume_result_called := false

	func get_state() -> BattleState:
		return state

	func consume_battle_resolution_result():
		consume_result_called = true
		var result = resolution_result
		resolution_result = null
		return result


class _FakeBattleRuntimeWithoutResult extends RefCounted:
	var state: BattleState = null

	func get_state() -> BattleState:
		return state

	func consume_battle_resolution_result():
		return null


class _FakeRuntimeBridge extends RefCounted:
	var battle_selection = _FakeBattleSelection.new()
	var battle_runtime = null
	var finalization_calls: Array[Dictionary] = []
	var status_updates: Array[String] = []

	func get_battle_selection():
		return battle_selection

	func get_battle_runtime():
		return battle_runtime

	func is_battle_active() -> bool:
		return true

	func get_battle_state() -> BattleState:
		return battle_runtime.get_state() if battle_runtime != null and battle_runtime.has_method("get_state") else null

	func finalize_battle_resolution(battle_resolution_result) -> void:
		finalization_calls.append({
			"battle_resolution_result": battle_resolution_result,
			"winner_faction_id": String(battle_resolution_result.winner_faction_id) if battle_resolution_result != null else "",
			"pending_character_rewards": battle_resolution_result.get_pending_character_rewards_copy() if battle_resolution_result != null else [],
			"quest_progress_events": battle_resolution_result.quest_progress_events.duplicate(true) if battle_resolution_result != null else [],
		})

	func build_command_ok(message: String = "", battle_refresh_mode: String = "") -> Dictionary:
		return {
			"ok": true,
			"message": message,
			"battle_refresh_mode": battle_refresh_mode,
		}

	func build_command_error(message: String) -> Dictionary:
		return {
			"ok": false,
			"message": message,
		}

	func update_status(_message: String) -> void:
		status_updates.append(_message)


class _FakeBattleGateway extends RefCounted:
	func build_pending_skill_mastery_reward(
		member_id: StringName,
		source_type: StringName,
		source_label: String,
		entry_variants: Array,
		summary_text: String = ""
	) -> PendingCharacterReward:
		var reward := PendingCharacterReward.new()
		reward.reward_id = StringName("%s_%s" % [String(member_id), String(source_type)])
		reward.member_id = member_id
		reward.member_name = String(member_id)
		reward.source_type = source_type
		reward.source_id = source_type
		reward.source_label = source_label
		reward.summary_text = summary_text
		for entry_variant in entry_variants:
			if entry_variant is not Dictionary:
				continue
			var entry_data: Dictionary = entry_variant
			var entry := PendingCharacterRewardEntry.new()
			entry.entry_type = &"skill_mastery"
			entry.target_id = ProgressionDataUtils.to_string_name(entry_data.get("target_id", ""))
			entry.target_label = String(entry_data.get("target_label", String(entry.target_id)))
			entry.amount = int(entry_data.get("amount", 0))
			entry.reason_text = String(entry_data.get("reason_text", ""))
			if not entry.is_empty():
				reward.entries.append(entry)
		return reward


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_battle_resolution_result_round_trip()
	_test_battle_runtime_builds_resolution_result_on_battle_end()
	_test_battle_session_facade_prefers_canonical_resolution_result()
	_test_battle_session_facade_requires_canonical_resolution_result()
	if _failures.is_empty():
		print("Battle resolution contract regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle resolution contract regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_battle_resolution_result_round_trip() -> void:
	var reward := _build_canonical_reward(&"hero", &"battle_skill")

	var result := BattleResolutionResult.new()
	result.battle_id = &"battle_contract"
	result.seed = 77
	result.world_coord = Vector2i(4, 9)
	result.encounter_anchor_id = &"encounter_contract"
	result.terrain_profile_id = &"canyon"
	result.winner_faction_id = &"player"
	result.encounter_resolution = &"player_victory"
	result.set_loot_entries([_build_raw_loot_entry()])
	result.set_overflow_entries([_build_raw_overflow_entry()])
	result.pending_character_rewards = [reward]
	result.quest_progress_events = [{"quest_id": "quest_contract", "amount": 1}]
	result.world_mutations = [{"kind": "clear_anchor"}]
	result.party_resource_commit = {"gold_delta": 12}

	_assert_true(not result.is_empty(), "填充字段后，BattleResolutionResult 不应为空。")
	_assert_true(result.has_pending_character_rewards(), "填充奖励后，BattleResolutionResult 应报告存在待处理奖励。")
	_assert_eq(result.get_pending_character_rewards_copy().size(), 1, "奖励访问器应返回同一批奖励。")
	_assert_eq(result.loot_entries.size(), 1, "BattleResolutionResult 应归一化 loot_entries。")
	if result.loot_entries.size() > 0 and result.loot_entries[0] is Dictionary:
		_assert_canonical_loot_entry(result.loot_entries[0], "BattleResolutionResult.set_loot_entries()")

	var round_tripped: BattleResolutionResult = BattleResolutionResult.from_dict(result.to_dict())
	_assert_true(round_tripped != null, "BattleResolutionResult.to_dict()/from_dict() 应支持 round trip。")
	_assert_eq(String(round_tripped.battle_id), "battle_contract", "battle_id 应保持稳定。")
	_assert_eq(String(round_tripped.winner_faction_id), "player", "winner_faction_id 应保持稳定。")
	_assert_eq(String(round_tripped.encounter_resolution), "player_victory", "encounter_resolution 应保持稳定。")
	_assert_eq(round_tripped.loot_entries.size(), 1, "loot_entries 应保持稳定。")
	if round_tripped.loot_entries.size() > 0 and round_tripped.loot_entries[0] is Dictionary:
		_assert_canonical_loot_entry(round_tripped.loot_entries[0], "BattleResolutionResult round trip")
	_assert_eq(round_tripped.overflow_entries.size(), 1, "overflow_entries 应保持稳定。")
	if round_tripped.overflow_entries.size() > 0 and round_tripped.overflow_entries[0] is Dictionary:
		_assert_canonical_overflow_entry(round_tripped.overflow_entries[0], "BattleResolutionResult overflow round trip")
	_assert_eq(round_tripped.pending_character_rewards.size(), 1, "pending_character_rewards 应保持稳定。")
	_assert_true(
		round_tripped.pending_character_rewards[0] is PendingCharacterReward,
		"pending_character_rewards 的元素在 round trip 后应仍是正式角色奖励。"
	)


func _test_battle_runtime_builds_resolution_result_on_battle_end() -> void:
	var runtime := BattleRuntimeModule.new()
	var gateway := _FakeBattleGateway.new()
	runtime.setup(gateway, {}, {}, {}, null)
	runtime._state = _build_battle_state_for_end_test()
	runtime._battle_rating_stats = _build_battle_rating_stats()
	runtime._active_loot_entries = [_build_raw_loot_entry()]

	var batch := BattleEventBatch.new()
	_assert_true(runtime._check_battle_end(batch), "_check_battle_end() 应在一方清场后结束战斗。")
	var result: BattleResolutionResult = runtime.get_battle_resolution_result()
	_assert_true(result != null, "战斗结束后应缓存 BattleResolutionResult。")
	_assert_eq(String(result.winner_faction_id), "player", "战斗胜利方应写入结算结果。")
	_assert_eq(String(result.encounter_resolution), "player_victory", "战斗结算类型应与胜利方一致。")
	_assert_eq(result.pending_character_rewards.size(), 1, "战斗结算结果应包含已构建的正式奖励。")
	_assert_true(
		result.pending_character_rewards[0] is PendingCharacterReward,
		"战斗结算结果中的奖励应为正式角色奖励对象。"
	)
	_assert_eq(result.loot_entries.size(), 1, "战斗结算结果应包含 canonical 掉落条目。")
	if result.loot_entries.size() > 0 and result.loot_entries[0] is Dictionary:
		_assert_canonical_loot_entry(result.loot_entries[0], "BattleRuntimeModule._build_battle_resolution_result()")
	_assert_true(runtime.consume_battle_resolution_result() == result, "consume_battle_resolution_result() 应返回已构建的结果。")
	_assert_true(runtime.consume_battle_resolution_result() == null, "consume_battle_resolution_result() 第二次调用后应清空缓存。")


func _test_battle_session_facade_prefers_canonical_resolution_result() -> void:
	var runtime_bridge := _FakeRuntimeBridge.new()
	var battle_runtime := _FakeBattleRuntimeWithResult.new()
	battle_runtime.state = _build_ended_battle_state()
	var expected_result := _build_resolution_result_with_reward(_build_canonical_reward(&"hero", &"battle_skill"))
	battle_runtime.resolution_result = expected_result
	runtime_bridge.battle_runtime = battle_runtime

	var facade := BattleSessionFacade.new()
	facade.setup(runtime_bridge)
	facade.resolve_active_battle()

	_assert_eq(runtime_bridge.finalization_calls.size(), 1, "resolve_active_battle() 应触发一次战后回写。")
	var call: Dictionary = runtime_bridge.finalization_calls[0]
	_assert_true(call.get("battle_resolution_result") is BattleResolutionResult, "resolve_active_battle() 应把 canonical result 整包传给运行时。")
	_assert_true(call.get("battle_resolution_result") == expected_result, "传给运行时的 canonical result 应保持原对象。")
	_assert_eq(String(call.get("winner_faction_id", "")), "player", "resolve_active_battle() 应使用 canonical result 的胜利方。")
	_assert_eq((call.get("pending_character_rewards", []) as Array).size(), 1, "resolve_active_battle() 应把 canonical result 的奖励传给运行时。")
	_assert_true(
		(call.get("pending_character_rewards", []) as Array)[0] is PendingCharacterReward,
		"resolve_active_battle() 应传递正式角色奖励对象。"
	)
	_assert_eq((call.get("quest_progress_events", []) as Array).size(), 1, "resolve_active_battle() 应把 canonical result 的 quest_progress_events 一并传给运行时。")
	_assert_true(battle_runtime.consume_result_called, "resolve_active_battle() 应消费 canonical battle result。")


func _test_battle_session_facade_requires_canonical_resolution_result() -> void:
	var runtime_bridge := _FakeRuntimeBridge.new()
	var battle_runtime := _FakeBattleRuntimeWithoutResult.new()
	battle_runtime.state = _build_ended_battle_state()
	runtime_bridge.battle_runtime = battle_runtime

	var facade := BattleSessionFacade.new()
	facade.setup(runtime_bridge)
	facade.resolve_active_battle()

	_assert_eq(runtime_bridge.finalization_calls.size(), 0, "缺少 canonical result 时不应继续战后回写。")
	_assert_true(
		not runtime_bridge.status_updates.is_empty() and runtime_bridge.status_updates[-1].contains("缺少正式结算结果"),
		"缺少 canonical result 时应显式报告错误状态。"
	)


func _build_canonical_reward(member_id: StringName, skill_id: StringName) -> PendingCharacterReward:
	var reward := PendingCharacterReward.new()
	reward.reward_id = StringName("%s_reward" % String(member_id))
	reward.member_id = member_id
	reward.member_name = String(member_id)
	reward.source_type = &"battle_rating"
	reward.source_id = &"battle_rating"
	reward.source_label = "战斗结算"
	reward.summary_text = "战斗评分结算。"
	var entry := PendingCharacterRewardEntry.new()
	entry.entry_type = &"skill_mastery"
	entry.target_id = skill_id
	entry.target_label = String(skill_id)
	entry.amount = 4
	entry.reason_text = "战斗评分 4 · 渐入佳境"
	reward.entries = [entry]
	return reward


func _build_resolution_result_with_reward(reward: PendingCharacterReward) -> BattleResolutionResult:
	var result := BattleResolutionResult.new()
	result.battle_id = &"battle_session"
	result.seed = 99
	result.world_coord = Vector2i(6, 12)
	result.encounter_anchor_id = &"encounter_session"
	result.terrain_profile_id = &"default"
	result.winner_faction_id = &"player"
	result.encounter_resolution = &"player_victory"
	result.pending_character_rewards = [reward]
	result.quest_progress_events = [{"quest_id": "battle_contract", "objective_id": "defeat_enemy", "progress_delta": 1}]
	return result


func _build_raw_loot_entry() -> Dictionary:
	return {
		"drop_type": &"item",
		"drop_source_kind": &"encounter_roster",
		"drop_source_id": &"wolf_den",
		"drop_source_label": "荒狼巢穴",
		"drop_id": &"wolf_den_hide_bundle",
		"item_id": &"beast_hide",
		"quantity": 2,
		"debug_only_flag": true,
	}


func _build_raw_overflow_entry() -> Dictionary:
	return {
		"drop_type": &"item",
		"drop_source_kind": &"encounter_roster",
		"drop_source_id": &"wolf_den",
		"drop_source_label": "荒狼巢穴",
		"drop_entry_id": &"wolf_den_hide_bundle",
		"item_id": &"beast_hide",
		"quantity": 1,
		"debug_only_flag": true,
	}


func _assert_canonical_loot_entry(loot_entry: Dictionary, message_scope: String) -> void:
	_assert_true(not loot_entry.has("drop_id"), "%s 应只暴露 canonical drop_entry_id 字段。" % message_scope)
	_assert_true(not loot_entry.has("debug_only_flag"), "%s 不应泄露原始调试字段。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_type", "")), "item", "%s 应保留稳定 drop_type。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_source_kind", "")), "encounter_roster", "%s 应保留稳定来源类型。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_source_id", "")), "wolf_den", "%s 应保留稳定掉落来源标识。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_entry_id", "")), "wolf_den_hide_bundle", "%s 应保留稳定掉落 entry 标识。" % message_scope)
	_assert_eq(String(loot_entry.get("item_id", "")), "beast_hide", "%s 应保留稳定物品标识。" % message_scope)
	_assert_eq(int(loot_entry.get("quantity", 0)), 2, "%s 应保留稳定数量。" % message_scope)


func _assert_canonical_overflow_entry(loot_entry: Dictionary, message_scope: String) -> void:
	_assert_true(not loot_entry.has("debug_only_flag"), "%s 不应泄露原始调试字段。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_type", "")), "item", "%s 应保留稳定 drop_type。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_source_kind", "")), "encounter_roster", "%s 应保留稳定来源类型。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_source_id", "")), "wolf_den", "%s 应保留稳定掉落来源标识。" % message_scope)
	_assert_eq(String(loot_entry.get("drop_entry_id", "")), "wolf_den_hide_bundle", "%s 应保留稳定掉落 entry 标识。" % message_scope)
	_assert_eq(String(loot_entry.get("item_id", "")), "beast_hide", "%s 应保留稳定物品标识。" % message_scope)
	_assert_eq(int(loot_entry.get("quantity", 0)), 1, "%s 应保留稳定溢出数量。" % message_scope)


func _build_battle_state_for_end_test() -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"battle_end_contract"
	state.phase = &"timeline_running"
	state.timeline = BattleTimelineState.new()
	state.ally_unit_ids = [&"hero_unit"]
	state.enemy_unit_ids = [&"enemy_unit"]
	var ally_unit := _build_unit(&"hero_unit", &"hero", true)
	var enemy_unit := _build_unit(&"enemy_unit", &"enemy", false)
	state.units[ally_unit.unit_id] = ally_unit
	state.units[enemy_unit.unit_id] = enemy_unit
	return state


func _build_battle_rating_stats() -> Dictionary:
	return {
		&"hero": {
			"member_id": &"hero",
			"member_name": "Hero",
			"cast_counts": {
				&"battle_skill": 1,
			},
			"successful_skill_count": 1,
			"total_damage_done": 1,
			"total_healing_done": 0,
			"kill_count": 0,
		},
	}


func _build_ended_battle_state() -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"battle_session_end"
	state.phase = &"battle_ended"
	state.winner_faction_id = &"player"
	state.timeline = BattleTimelineState.new()
	return state


func _build_unit(unit_id: StringName, member_id: StringName, is_alive: bool) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.source_member_id = member_id
	unit.display_name = String(unit_id)
	unit.faction_id = &"player" if String(member_id) == "hero" else &"hostile"
	unit.control_mode = &"manual"
	unit.is_alive = is_alive
	return unit


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
