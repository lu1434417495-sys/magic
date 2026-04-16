extends SceneTree

const QuestDef = preload("res://scripts/player/progression/quest_def.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_quest_def_round_trip_and_validation()
	_test_quest_state_progress_and_round_trip()

	if _failures.is_empty():
		print("Quest schema regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Quest schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_quest_def_round_trip_and_validation() -> void:
	var quest_def := QuestDef.new()
	quest_def.quest_id = &"contract_wolf_pack"
	quest_def.display_name = "清理狼群"
	quest_def.provider_interaction_id = &"service_contract_board"
	quest_def.tags = [&"contract", &"bounty"]
	quest_def.accept_requirements = [
		{"requirement_type": "settlement_reputation", "settlement_id": "spring_village_01", "minimum_value": 0},
	]
	quest_def.objective_defs = [
		{
			"objective_id": "defeat_wolves",
			"objective_type": QuestDef.OBJECTIVE_DEFEAT_ENEMY,
			"target_id": "wolf_raider",
			"target_value": 3,
		},
		{
			"objective_id": "report_back",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_id": "service_contract_board",
			"target_value": 1,
		},
	]
	quest_def.reward_entries = [
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 120},
		{"reward_type": QuestDef.REWARD_ITEM, "item_id": "iron_ore", "quantity": 2},
	]

	var restored: QuestDef = QuestDef.from_dict(quest_def.to_dict())
	_assert_eq(restored.quest_id, &"contract_wolf_pack", "QuestDef 应保留 quest_id。")
	_assert_eq(restored.get_objective_ids().size(), 2, "QuestDef 应保留 objective_defs。")
	_assert_true(restored.validate_schema().is_empty(), "合法 QuestDef 不应产生 schema 错误。")

	var invalid_quest := QuestDef.new()
	invalid_quest.quest_id = &"broken_contract"
	invalid_quest.objective_defs = [
		{"objective_id": "dup", "objective_type": QuestDef.OBJECTIVE_DEFEAT_ENEMY, "target_value": 1},
		{"objective_id": "dup", "objective_type": QuestDef.OBJECTIVE_DEFEAT_ENEMY, "target_value": 1},
	]
	var errors: Array[String] = invalid_quest.validate_schema()
	_assert_true(errors.size() >= 1, "重复 objective_id 应被 validate_schema() 拒绝。")

	var invalid_reward_quest := QuestDef.new()
	invalid_reward_quest.quest_id = &"broken_reward_contract"
	invalid_reward_quest.objective_defs = [
		{"objective_id": "report_back", "objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION, "target_value": 1},
	]
	invalid_reward_quest.reward_entries = [
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 0},
		{"reward_type": QuestDef.REWARD_ITEM, "item_id": "", "quantity": 0},
	]
	var reward_errors: Array[String] = invalid_reward_quest.validate_schema()
	_assert_true(reward_errors.size() >= 2, "无效 gold/item reward 应被 validate_schema() 拒绝。")


func _test_quest_state_progress_and_round_trip() -> void:
	var quest_def := QuestDef.new()
	quest_def.quest_id = &"contract_wolf_pack"
	quest_def.objective_defs = [
		{
			"objective_id": "defeat_wolves",
			"objective_type": QuestDef.OBJECTIVE_DEFEAT_ENEMY,
			"target_value": 3,
		},
		{
			"objective_id": "report_back",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_value": 1,
		},
	]

	var quest_state := QuestState.new()
	quest_state.quest_id = quest_def.quest_id
	quest_state.mark_accepted(12)
	quest_state.record_objective_progress(&"defeat_wolves", 2, 3, {"enemy_template_id": "wolf_raider"})
	quest_state.record_objective_progress(&"defeat_wolves", 2, 3, {"enemy_template_id": "wolf_raider"})
	quest_state.record_objective_progress(&"report_back", 1, 1, {"settlement_id": "spring_village_01"})

	_assert_eq(quest_state.get_objective_progress(&"defeat_wolves"), 3, "QuestState 进度应按 target_value 封顶。")
	_assert_true(quest_state.has_completed_all_objectives(quest_def), "所有 objective 完成后应通过完成检查。")

	quest_state.mark_completed(18)
	quest_state.mark_reward_claimed(19)
	var restored: QuestState = QuestState.from_dict(quest_state.to_dict())
	_assert_eq(restored.status_id, QuestState.STATUS_REWARDED, "QuestState 应保留完成后的状态。")
	_assert_eq(restored.reward_claimed_at_world_step, 19, "QuestState 应保留领奖时间。")
	_assert_eq(
		String(restored.last_progress_context.get("settlement_id", "")),
		"spring_village_01",
		"QuestState 应保留最近一次进度上下文。"
	)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
