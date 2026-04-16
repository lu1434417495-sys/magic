extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime_facade.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle_resolution_result.gd")
const QuestDef = preload("res://scripts/player/progression/quest_def.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_runtime_quest_commands_and_battle_progress_pipeline()

	if _failures.is_empty():
		print("Game runtime quest progress regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Game runtime quest progress regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_runtime_quest_commands_and_battle_progress_pipeline() -> void:
	var game_session = _create_test_session()
	if game_session == null:
		return
	_inject_repeatable_quest_def(game_session)

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var accept_result := facade.command_accept_quest(&"contract_manual_drill")
	_assert_true(bool(accept_result.get("ok", false)), "quest accept 命令应成功。")
	_assert_eq(String(accept_result.get("message", "")), "已接取任务《训练记录》。", "首次接取任务时应返回明确成功反馈。")
	_assert_true(facade.get_party_state().has_active_quest(&"contract_manual_drill"), "接取任务后 PartyState 应包含激活任务。")
	var duplicate_accept_result := facade.command_accept_quest(&"contract_manual_drill")
	_assert_true(not bool(duplicate_accept_result.get("ok", true)), "重复接取中的任务应失败。")
	_assert_eq(String(duplicate_accept_result.get("message", "")), "任务《训练记录》已在进行中，不能重复接取。", "重复接取中的任务应返回明确反馈。")

	var progress_result := facade.command_progress_quest(&"contract_manual_drill", &"train_once", 1, {
		"target_value": 2,
		"action_id": "service:training",
	})
	_assert_true(bool(progress_result.get("ok", false)), "quest progress 命令应成功。")
	var active_quest: QuestState = facade.get_party_state().get_active_quest_state(&"contract_manual_drill")
	_assert_true(active_quest != null, "推进任务后激活任务应仍可读取。")
	if active_quest != null:
		_assert_eq(active_quest.get_objective_progress(&"train_once"), 1, "quest progress 命令应写入 objective_progress。")
		_assert_eq(String(active_quest.last_progress_context.get("action_id", "")), "service:training", "quest progress 命令应保留事件上下文。")

	var complete_result := facade.command_complete_quest(&"contract_manual_drill")
	_assert_true(bool(complete_result.get("ok", false)), "quest complete 命令应成功。")
	_assert_eq(String(complete_result.get("message", "")), "已完成任务《训练记录》，奖励待领取。", "quest complete 命令应返回待领奖励反馈。")
	_assert_true(not facade.get_party_state().has_active_quest(&"contract_manual_drill"), "完成任务后应从 active_quests 移除。")
	_assert_true(facade.get_party_state().has_claimable_quest(&"contract_manual_drill"), "完成任务后应进入 claimable_quests。")
	_assert_true(not facade.get_party_state().has_completed_quest(&"contract_manual_drill"), "完成任务后不应直接进入 completed_quest_ids。")
	var completed_accept_result := facade.command_accept_quest(&"contract_manual_drill")
	_assert_true(not bool(completed_accept_result.get("ok", true)), "已完成的非 repeatable 任务不应再次接取。")
	_assert_eq(String(completed_accept_result.get("message", "")), "任务《训练记录》已完成，奖励待领取，当前不可再次接取。", "待领奖励任务再次接取时应返回明确反馈。")

	var repeatable_accept_result := facade.command_accept_quest(&"contract_repeatable_patrol")
	_assert_true(bool(repeatable_accept_result.get("ok", false)), "repeatable 任务首次接取应成功。")
	_assert_true(bool(facade.command_complete_quest(&"contract_repeatable_patrol").get("ok", false)), "repeatable 任务应能先完成一次。")
	_assert_true(facade.get_party_state().has_claimable_quest(&"contract_repeatable_patrol"), "repeatable 任务完成后也应先进入 claimable_quests。")
	var repeatable_claimable_reaccept_result := facade.command_accept_quest(&"contract_repeatable_patrol")
	_assert_true(not bool(repeatable_claimable_reaccept_result.get("ok", true)), "repeatable 任务待领奖励时不应直接再次接取。")
	_assert_eq(String(repeatable_claimable_reaccept_result.get("message", "")), "任务《巡路值守》已完成，奖励待领取，当前不可再次接取。", "repeatable 任务待领奖励时应返回明确反馈。")
	_assert_true(facade.get_party_state().mark_quest_reward_claimed(&"contract_repeatable_patrol", facade.get_world_step()), "repeatable 任务领奖后应能进入最终 completed 阶段。")
	var repeatable_reaccept_result := facade.command_accept_quest(&"contract_repeatable_patrol")
	_assert_true(bool(repeatable_reaccept_result.get("ok", false)), "repeatable 任务完成并领取后应可再次接取。")
	_assert_eq(String(repeatable_reaccept_result.get("message", "")), "已重新接取任务《巡路值守》。", "repeatable 任务再次接取应返回明确反馈。")
	_assert_true(facade.get_party_state().has_active_quest(&"contract_repeatable_patrol"), "repeatable 任务再次接取后应回到 active_quests。")
	_assert_true(not facade.get_party_state().has_claimable_quest(&"contract_repeatable_patrol"), "repeatable 任务再次接取后应移出 claimable_quests。")
	_assert_true(not facade.get_party_state().has_completed_quest(&"contract_repeatable_patrol"), "repeatable 任务再次接取后应移出 completed_quest_ids。")

	var encounter_anchor = _find_any_uncleared_encounter_anchor(game_session.get_world_data())
	_assert_true(encounter_anchor != null, "battle quest 前置：测试世界应存在一个遭遇锚点。")
	if encounter_anchor == null:
		facade.dispose()
		_cleanup_test_session(game_session)
		return
	facade._active_battle_encounter_id = encounter_anchor.entity_id
	facade._active_battle_encounter_name = encounter_anchor.display_name

	var battle_accept_result := facade.command_accept_quest(&"contract_first_hunt")
	_assert_true(bool(battle_accept_result.get("ok", false)), "battle quest 前置接取应成功。")
	var battle_resolution_result = BATTLE_RESOLUTION_RESULT_SCRIPT.new()
	battle_resolution_result.winner_faction_id = &"player"
	facade.finalize_battle_resolution(battle_resolution_result)
	_assert_true(facade.get_party_state().has_claimable_quest(&"contract_first_hunt"), "battle finalize 应通过默认 quest_progress_event 把首轮狩猎任务推进到待领奖励。")

	facade.dispose()
	_cleanup_test_session(game_session)


func _create_test_session():
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "GameSession 应能基于测试世界配置创建新存档。")
	if create_error != OK:
		_cleanup_test_session(game_session)
		return null
	return game_session


func _cleanup_test_session(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _inject_repeatable_quest_def(game_session) -> void:
	if game_session == null:
		return
	var repeatable_quest := QuestDef.new()
	repeatable_quest.quest_id = &"contract_repeatable_patrol"
	repeatable_quest.display_name = "巡路值守"
	repeatable_quest.description = "完成一次例行巡路后可重新接取。"
	repeatable_quest.provider_interaction_id = &"service_contract_board"
	repeatable_quest.objective_defs = [
		{
			"objective_id": "warehouse_visit",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_id": "service:warehouse",
			"target_value": 1,
		},
	]
	repeatable_quest.reward_entries = [
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 15},
	]
	repeatable_quest.is_repeatable = true
	game_session.get_quest_defs()[repeatable_quest.quest_id] = repeatable_quest


func _find_any_uncleared_encounter_anchor(world_data: Dictionary):
	for encounter_variant in world_data.get("encounter_anchors", []):
		if encounter_variant == null or bool(encounter_variant.is_cleared):
			continue
		return encounter_variant
	return null


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
