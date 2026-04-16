extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime_facade.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle_resolution_result.gd")
const PARTY_WAREHOUSE_SERVICE_SCRIPT = preload("res://scripts/systems/party_warehouse_service.gd")
const QuestDef = preload("res://scripts/player/progression/quest_def.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")

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
	_inject_item_reward_quest_defs(game_session)

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)
	_inject_pending_reward_quest_def(game_session, facade.get_party_state().leader_member_id)

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
	var gold_before_manual_claim: int = facade.get_party_state().get_gold()
	var manual_claim_result := facade.command_claim_quest(&"contract_manual_drill")
	_assert_true(bool(manual_claim_result.get("ok", false)), "claimable 任务应能通过正式 claim 命令领取奖励。")
	_assert_eq(String(manual_claim_result.get("message", "")), "已领取任务《训练记录》奖励，获得 30 金。", "领取金币奖励时应返回明确反馈。")
	_assert_eq(int(manual_claim_result.get("gold_delta", 0)), 30, "quest claim 结果应暴露 gold_delta。")
	_assert_eq(facade.get_party_state().get_gold(), gold_before_manual_claim + 30, "gold reward claim 后应把金币写入 PartyState。")
	_assert_true(not facade.get_party_state().has_claimable_quest(&"contract_manual_drill"), "领取奖励后任务应离开 claimable_quests。")
	_assert_true(facade.get_party_state().has_completed_quest(&"contract_manual_drill"), "领取奖励后任务应进入 completed_quest_ids。")

	var repeatable_accept_result := facade.command_accept_quest(&"contract_repeatable_patrol")
	_assert_true(bool(repeatable_accept_result.get("ok", false)), "repeatable 任务首次接取应成功。")
	_assert_true(bool(facade.command_complete_quest(&"contract_repeatable_patrol").get("ok", false)), "repeatable 任务应能先完成一次。")
	_assert_true(facade.get_party_state().has_claimable_quest(&"contract_repeatable_patrol"), "repeatable 任务完成后也应先进入 claimable_quests。")
	var repeatable_claimable_reaccept_result := facade.command_accept_quest(&"contract_repeatable_patrol")
	_assert_true(not bool(repeatable_claimable_reaccept_result.get("ok", true)), "repeatable 任务待领奖励时不应直接再次接取。")
	_assert_eq(String(repeatable_claimable_reaccept_result.get("message", "")), "任务《巡路值守》已完成，奖励待领取，当前不可再次接取。", "repeatable 任务待领奖励时应返回明确反馈。")
	var gold_before_repeatable_claim: int = facade.get_party_state().get_gold()
	var repeatable_claim_result := facade.command_claim_quest(&"contract_repeatable_patrol")
	_assert_true(bool(repeatable_claim_result.get("ok", false)), "repeatable 任务待领奖励时应能领取奖励。")
	_assert_eq(String(repeatable_claim_result.get("message", "")), "已领取任务《巡路值守》奖励，获得 15 金。", "repeatable 任务领取奖励时应返回明确反馈。")
	_assert_eq(int(repeatable_claim_result.get("gold_delta", 0)), 15, "repeatable 任务 claim 结果应暴露 gold_delta。")
	_assert_eq(facade.get_party_state().get_gold(), gold_before_repeatable_claim + 15, "repeatable 任务领奖后应增加金币。")
	_assert_true(facade.get_party_state().has_completed_quest(&"contract_repeatable_patrol"), "repeatable 任务领奖后应进入最终 completed 阶段。")
	var repeatable_reaccept_result := facade.command_accept_quest(&"contract_repeatable_patrol")
	_assert_true(bool(repeatable_reaccept_result.get("ok", false)), "repeatable 任务完成并领取后应可再次接取。")
	_assert_eq(String(repeatable_reaccept_result.get("message", "")), "已重新接取任务《巡路值守》。", "repeatable 任务再次接取应返回明确反馈。")
	_assert_true(facade.get_party_state().has_active_quest(&"contract_repeatable_patrol"), "repeatable 任务再次接取后应回到 active_quests。")
	_assert_true(not facade.get_party_state().has_claimable_quest(&"contract_repeatable_patrol"), "repeatable 任务再次接取后应移出 claimable_quests。")
	_assert_true(not facade.get_party_state().has_completed_quest(&"contract_repeatable_patrol"), "repeatable 任务再次接取后应移出 completed_quest_ids。")

	var item_reward_quest := QuestState.new()
	item_reward_quest.quest_id = &"contract_supply_receipt"
	item_reward_quest.mark_accepted(20)
	item_reward_quest.mark_completed(24)
	facade.get_party_state().set_claimable_quest_state(item_reward_quest)
	var runtime_warehouse_service = PARTY_WAREHOUSE_SERVICE_SCRIPT.new()
	runtime_warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())
	var item_claim_result := facade.command_claim_quest(&"contract_supply_receipt")
	runtime_warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())
	_assert_true(bool(item_claim_result.get("ok", false)), "item reward 任务应能通过正式 claim 命令写入共享仓库。")
	_assert_eq(String(item_claim_result.get("message", "")), "已领取任务《补给签收》奖励，获得 12 金、铁矿石 x2。", "item reward claim 成功时应返回明确奖励摘要。")
	_assert_eq(int(item_claim_result.get("gold_delta", 0)), 12, "item reward claim 结果应继续暴露 gold_delta。")
	_assert_eq(_extract_item_reward_quantity(item_claim_result.get("item_rewards", []), "iron_ore"), 2, "item reward claim 结果应暴露写入仓库的物品条目。")
	_assert_eq(runtime_warehouse_service.count_item(&"iron_ore"), 2, "item reward claim 后共享仓库应新增铁矿石。")
	_assert_true(facade.get_party_state().has_completed_quest(&"contract_supply_receipt"), "item reward claim 后任务应进入 completed_quest_ids。")

	var overflow_quest := QuestState.new()
	overflow_quest.quest_id = &"contract_reward_overflow"
	overflow_quest.mark_accepted(25)
	overflow_quest.mark_completed(29)
	facade.get_party_state().set_claimable_quest_state(overflow_quest)
	runtime_warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())
	var warehouse_capacity := runtime_warehouse_service.get_total_capacity()
	runtime_warehouse_service.add_item(&"bronze_sword", warehouse_capacity)
	var bronze_sword_count_before_overflow_claim := runtime_warehouse_service.count_item(&"bronze_sword")
	var overflow_claim_result := facade.command_claim_quest(&"contract_reward_overflow")
	_assert_true(not bool(overflow_claim_result.get("ok", true)), "容量不足时 item reward claim 应正式失败。")
	_assert_eq(String(overflow_claim_result.get("message", "")), "共享仓库空间不足，领取任务《仓储超额》奖励会溢出，当前无法领取。", "容量不足时应返回明确 overflow 反馈。")
	_assert_true(facade.get_party_state().has_claimable_quest(&"contract_reward_overflow"), "容量不足时任务应继续停留在 claimable_quests。")
	_assert_true(not facade.get_party_state().has_completed_quest(&"contract_reward_overflow"), "容量不足时任务不应误写入 completed_quest_ids。")
	_assert_eq(runtime_warehouse_service.count_item(&"bronze_sword"), bronze_sword_count_before_overflow_claim, "容量不足时不应额外写入奖励物品。")

	var growth_reward_member_id: StringName = facade.get_party_state().leader_member_id
	var growth_reward_quest := QuestState.new()
	growth_reward_quest.quest_id = &"contract_growth_drill"
	growth_reward_quest.mark_accepted(31)
	growth_reward_quest.mark_completed(33)
	facade.get_party_state().set_claimable_quest_state(growth_reward_quest)
	var growth_member_state = facade.get_party_state().get_member_state(growth_reward_member_id)
	var growth_strength_before: int = int(
		growth_member_state.progression.unit_base_attributes.get_attribute_value(UnitBaseAttributes.STRENGTH)
	)
	var growth_claim_result := facade.command_claim_quest(&"contract_growth_drill")
	_assert_true(bool(growth_claim_result.get("ok", false)), "pending_character_reward quest 应能通过正式 claim 命令成功领取。")
	_assert_eq((growth_claim_result.get("pending_character_rewards", []) as Array).size(), 1, "quest claim 结果应暴露 materialized 角色奖励。")
	_assert_text_contains(String(growth_claim_result.get("message", "")), "角色奖励", "pending_character_reward claim 应在反馈中注明角色奖励。")
	_assert_eq(facade.get_party_state().pending_character_rewards.size(), 1, "quest 的成长奖励应进入正式 pending_character_rewards 队列。")
	_assert_true(facade.get_party_state().has_completed_quest(&"contract_growth_drill"), "quest claim 后任务应进入 completed_quest_ids。")
	_assert_true(not facade.get_party_state().has_claimable_quest(&"contract_growth_drill"), "quest claim 后任务应移出 claimable_quests。")
	_assert_eq(
		facade.get_party_state().get_member_state(growth_reward_member_id).progression.unit_base_attributes.get_attribute_value(UnitBaseAttributes.STRENGTH),
		growth_strength_before,
		"quest claim 后角色奖励应只入队，不应立刻写入属性。"
	)
	_assert_true(facade.present_pending_reward_if_ready(), "quest claim 后应继续走现有 reward flow 呈现奖励。")
	_assert_eq(facade.get_active_modal_id(), "reward", "quest growth reward 应复用正式 reward modal。")
	var active_reward = facade.get_active_reward()
	_assert_true(active_reward != null, "quest growth reward 应被提为 active reward。")
	if active_reward != null:
		_assert_eq(active_reward.member_id, growth_reward_member_id, "quest growth reward 应保留目标成员。")
		_assert_eq(active_reward.source_id, &"contract_growth_drill", "quest growth reward 应保留 quest 来源 ID。")
	var growth_confirm_result := facade.confirm_active_reward()
	_assert_true(bool(growth_confirm_result.get("ok", false)), "quest growth reward 应能通过既有确认命令结算。")
	_assert_eq(
		facade.get_party_state().get_member_state(growth_reward_member_id).progression.unit_base_attributes.get_attribute_value(UnitBaseAttributes.STRENGTH),
		growth_strength_before + 2,
		"确认正式 reward flow 后角色奖励才应真正入账。"
	)
	_assert_true(facade.get_party_state().pending_character_rewards.is_empty(), "确认 quest growth reward 后正式队列应清空。")

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


func _inject_item_reward_quest_defs(game_session) -> void:
	if game_session == null:
		return
	var item_reward_quest := QuestDef.new()
	item_reward_quest.quest_id = &"contract_supply_receipt"
	item_reward_quest.display_name = "补给签收"
	item_reward_quest.description = "完成补给交接后领取金币与素材奖励。"
	item_reward_quest.provider_interaction_id = &"service_contract_board"
	item_reward_quest.objective_defs = [
		{
			"objective_id": "warehouse_visit",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_id": "service:warehouse",
			"target_value": 1,
		},
	]
	item_reward_quest.reward_entries = _build_reward_entries([
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 12},
		{"reward_type": QuestDef.REWARD_ITEM, "item_id": "iron_ore", "quantity": 2},
	])
	game_session.get_quest_defs()[item_reward_quest.quest_id] = item_reward_quest

	var overflow_quest := QuestDef.new()
	overflow_quest.quest_id = &"contract_reward_overflow"
	overflow_quest.display_name = "仓储超额"
	overflow_quest.description = "用于验证奖励写入共享仓库时的 overflow 反馈。"
	overflow_quest.provider_interaction_id = &"service_contract_board"
	overflow_quest.objective_defs = item_reward_quest.objective_defs.duplicate(true)
	overflow_quest.reward_entries = _build_reward_entries([
		{"reward_type": QuestDef.REWARD_ITEM, "item_id": "bronze_sword", "quantity": 1},
	])
	game_session.get_quest_defs()[overflow_quest.quest_id] = overflow_quest


func _inject_pending_reward_quest_def(game_session, member_id: StringName) -> void:
	if game_session == null or member_id == &"":
		return
	var growth_reward_quest := QuestDef.new()
	growth_reward_quest.quest_id = &"contract_growth_drill"
	growth_reward_quest.display_name = "成长演练"
	growth_reward_quest.description = "用于验证任务奖励会走正式角色奖励队列。"
	growth_reward_quest.provider_interaction_id = &"service_contract_board"
	growth_reward_quest.objective_defs = [
		{
			"objective_id": "report_back",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_id": "service_contract_board",
			"target_value": 1,
		},
	]
	growth_reward_quest.reward_entries = _build_reward_entries([
		{
			"reward_type": QuestDef.REWARD_PENDING_CHARACTER_REWARD,
			"member_id": String(member_id),
			"summary_text": "完成演练后将获得成长奖励。",
			"entries": [
				{
					"entry_type": "attribute_delta",
					"target_id": String(UnitBaseAttributes.STRENGTH),
					"target_label": "力量",
					"amount": 2,
				},
			],
		},
	])
	game_session.get_quest_defs()[growth_reward_quest.quest_id] = growth_reward_quest


func _find_any_uncleared_encounter_anchor(world_data: Dictionary):
	for encounter_variant in world_data.get("encounter_anchors", []):
		if encounter_variant == null or bool(encounter_variant.is_cleared):
			continue
		return encounter_variant
	return null


func _extract_item_reward_quantity(item_reward_variants, item_id: String) -> int:
	if item_reward_variants is not Array:
		return 0
	for reward_variant in item_reward_variants:
		if reward_variant is not Dictionary:
			continue
		var reward_data := reward_variant as Dictionary
		if String(reward_data.get("item_id", "")) != item_id:
			continue
		return int(reward_data.get("quantity", 0))
	return 0


func _build_reward_entries(reward_variants: Array) -> Array[Dictionary]:
	var reward_entries: Array[Dictionary] = []
	for reward_variant in reward_variants:
		if reward_variant is Dictionary:
			reward_entries.append((reward_variant as Dictionary).duplicate(true))
	return reward_entries


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _assert_text_contains(text: String, expected_fragment: String, message: String) -> void:
	if text.contains(expected_fragment):
		return
	_failures.append("%s | missing=%s text=%s" % [message, expected_fragment, text])
