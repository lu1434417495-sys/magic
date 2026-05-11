extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")
const BATTLE_RESOLUTION_RESULT_SCRIPT = preload("res://scripts/systems/battle/core/battle_resolution_result.gd")
const PARTY_WAREHOUSE_SERVICE_SCRIPT = preload("res://scripts/systems/inventory/party_warehouse_service.gd")
const QuestDef = preload("res://scripts/player/progression/quest_def.gd")
const QuestState = preload("res://scripts/player/progression/quest_state.gd")
const ItemDef = preload("res://scripts/player/warehouse/item_def.gd")
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
	_inject_submit_item_quest_defs(game_session)
	_inject_string_key_only_quest_def(game_session)

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
		"world_step": facade.get_world_step(),
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

	var string_key_accept_result := facade.command_accept_quest(&"contract_string_key_only")
	_assert_true(not bool(string_key_accept_result.get("ok", true)), "String key-only quest_def 不应被 runtime accept 恢复。")
	_assert_eq(String(string_key_accept_result.get("message", "")), "未找到任务 contract_string_key_only。", "runtime quest lookup 应只读取 StringName key。")
	_assert_true(not facade.get_party_state().has_active_quest(&"contract_string_key_only"), "String key-only quest_def accept 失败后不应写入 active_quests。")

	var missing_display_name_accept_result := facade.command_accept_quest(&"contract_missing_display_name_reward")
	_assert_true(not bool(missing_display_name_accept_result.get("ok", true)), "缺少 display_name 的 quest 不应被 accept 命令恢复。")
	_assert_eq(String(missing_display_name_accept_result.get("message", "")), "任务配置缺少 display_name，当前无法执行命令。", "缺少 display_name 时 accept 不应回退成 quest_id。")
	_assert_true(not facade.get_party_state().has_active_quest(&"contract_missing_display_name_reward"), "缺少 display_name 的 accept 失败后不应写入 active_quests。")

	var missing_display_name_active_quest := QuestState.new()
	missing_display_name_active_quest.quest_id = &"contract_missing_display_name_reward"
	missing_display_name_active_quest.mark_accepted(23)
	facade.get_party_state().set_active_quest_state(missing_display_name_active_quest)
	var missing_display_name_progress_result := facade.command_progress_quest(&"contract_missing_display_name_reward", &"warehouse_visit", 1)
	_assert_true(not bool(missing_display_name_progress_result.get("ok", true)), "缺少 display_name 的 quest 不应被 progress 命令恢复。")
	_assert_eq(String(missing_display_name_progress_result.get("message", "")), "任务配置缺少 display_name，当前无法执行命令。", "缺少 display_name 时 progress 不应回退成 quest_id。")
	var missing_display_name_active_after_progress: QuestState = facade.get_party_state().get_active_quest_state(&"contract_missing_display_name_reward")
	_assert_true(missing_display_name_active_after_progress != null, "缺少 display_name 的 progress 失败后任务应继续停留在 active_quests。")
	if missing_display_name_active_after_progress != null:
		_assert_eq(missing_display_name_active_after_progress.get_objective_progress(&"warehouse_visit"), 0, "缺少 display_name 的 progress 失败后不应推进 objective_progress。")
	var missing_display_name_complete_result := facade.command_complete_quest(&"contract_missing_display_name_reward")
	_assert_true(not bool(missing_display_name_complete_result.get("ok", true)), "缺少 display_name 的 quest 不应被 complete 命令恢复。")
	_assert_eq(String(missing_display_name_complete_result.get("message", "")), "任务配置缺少 display_name，当前无法执行命令。", "缺少 display_name 时 complete 不应回退成 quest_id。")
	_assert_true(facade.get_party_state().has_active_quest(&"contract_missing_display_name_reward"), "缺少 display_name 的 complete 失败后任务应继续停留在 active_quests。")
	_assert_true(not facade.get_party_state().has_claimable_quest(&"contract_missing_display_name_reward"), "缺少 display_name 的 complete 失败后不应进入 claimable_quests。")

	var missing_display_name_submit_result := facade.command_submit_quest_item(&"contract_missing_display_name_reward")
	_assert_true(not bool(missing_display_name_submit_result.get("ok", true)), "缺少 display_name 的 quest 不应被 submit_item 命令恢复。")
	_assert_eq(String(missing_display_name_submit_result.get("message", "")), "任务配置缺少 display_name，当前无法执行命令。", "缺少 display_name 时 submit_item 不应回退成 quest_id。")
	_assert_true(facade.get_party_state().has_active_quest(&"contract_missing_display_name_reward"), "缺少 display_name 的 submit_item 失败后任务应继续停留在 active_quests。")

	var missing_display_name_reward_quest := QuestState.new()
	missing_display_name_reward_quest.quest_id = &"contract_missing_display_name_reward"
	missing_display_name_reward_quest.mark_accepted(24)
	missing_display_name_reward_quest.mark_completed(25)
	facade.get_party_state().set_claimable_quest_state(missing_display_name_reward_quest)
	var gold_before_missing_display_name_claim: int = facade.get_party_state().get_gold()
	var missing_display_name_claim_result := facade.command_claim_quest(&"contract_missing_display_name_reward")
	_assert_true(not bool(missing_display_name_claim_result.get("ok", true)), "缺少 display_name 的 quest reward 不应被 runtime claim 恢复。")
	_assert_eq(String(missing_display_name_claim_result.get("message", "")), "任务配置缺少 display_name，当前无法执行命令。", "缺少 display_name 时领奖反馈不应回退成 quest_id。")
	_assert_true(facade.get_party_state().has_claimable_quest(&"contract_missing_display_name_reward"), "缺少 display_name 领奖失败后任务应继续停留在 claimable_quests。")
	_assert_true(not facade.get_party_state().has_completed_quest(&"contract_missing_display_name_reward"), "缺少 display_name 领奖失败后任务不应误写入 completed_quest_ids。")
	_assert_eq(facade.get_party_state().get_gold(), gold_before_missing_display_name_claim, "缺少 display_name 领奖失败时不应写入金币奖励。")

	var legacy_alias_reward_quest := QuestState.new()
	legacy_alias_reward_quest.quest_id = &"contract_legacy_item_reward_alias"
	legacy_alias_reward_quest.mark_accepted(24)
	legacy_alias_reward_quest.mark_completed(25)
	facade.get_party_state().set_claimable_quest_state(legacy_alias_reward_quest)
	var iron_ore_count_before_legacy_alias_claim := runtime_warehouse_service.count_item(&"iron_ore")
	var legacy_alias_claim_result := facade.command_claim_quest(&"contract_legacy_item_reward_alias")
	runtime_warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())
	_assert_true(not bool(legacy_alias_claim_result.get("ok", true)), "旧 target_id/amount item reward 不应被 runtime claim 恢复。")
	_assert_eq(String(legacy_alias_claim_result.get("message", "")), "任务《旧别名奖励》包含无效的物品奖励配置，当前无法领取。", "旧 item reward 别名应返回无效物品奖励反馈。")
	_assert_true(facade.get_party_state().has_claimable_quest(&"contract_legacy_item_reward_alias"), "旧 item reward 别名失败后任务应继续停留在 claimable_quests。")
	_assert_true(not facade.get_party_state().has_completed_quest(&"contract_legacy_item_reward_alias"), "旧 item reward 别名失败后任务不应误写入 completed_quest_ids。")
	_assert_eq(runtime_warehouse_service.count_item(&"iron_ore"), iron_ore_count_before_legacy_alias_claim, "旧 item reward 别名失败时不应写入共享仓库。")

	var invalid_item_display_name_quest := QuestState.new()
	invalid_item_display_name_quest.quest_id = &"contract_invalid_item_display_name_reward"
	invalid_item_display_name_quest.mark_accepted(24)
	invalid_item_display_name_quest.mark_completed(25)
	facade.get_party_state().set_claimable_quest_state(invalid_item_display_name_quest)
	runtime_warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())
	var nameless_item_count_before_claim := runtime_warehouse_service.count_item(&"nameless_reward_item")
	var gold_before_invalid_item_display_name_claim: int = facade.get_party_state().get_gold()
	var invalid_item_display_name_claim_result := facade.command_claim_quest(&"contract_invalid_item_display_name_reward")
	runtime_warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())
	_assert_true(not bool(invalid_item_display_name_claim_result.get("ok", true)), "空 display_name 的 item reward 不应被 runtime claim 恢复。")
	_assert_eq(String(invalid_item_display_name_claim_result.get("message", "")), "任务《空名物品奖励》引用的物品奖励缺少 display_name，当前无法领取。", "空 item display_name 应返回明确配置错误。")
	_assert_true(not String(invalid_item_display_name_claim_result.get("message", "")).contains("nameless_reward_item"), "空 display_name 的 item reward 失败反馈不应泄露 item_id。")
	_assert_true(facade.get_party_state().has_claimable_quest(&"contract_invalid_item_display_name_reward"), "空 item display_name 领奖失败后任务应继续停留在 claimable_quests。")
	_assert_true(not facade.get_party_state().has_completed_quest(&"contract_invalid_item_display_name_reward"), "空 item display_name 领奖失败后任务不应误写入 completed_quest_ids。")
	_assert_eq(facade.get_party_state().get_gold(), gold_before_invalid_item_display_name_claim, "空 item display_name 领奖失败时不应写入金币奖励。")
	_assert_eq(runtime_warehouse_service.count_item(&"nameless_reward_item"), nameless_item_count_before_claim, "空 item display_name 领奖失败时不应写入共享仓库。")
	var direct_character_management := CharacterManagementModule.new()
	direct_character_management.setup(
		facade.get_party_state(),
		game_session.get_skill_defs(),
		game_session.get_profession_defs(),
		game_session.get_achievement_defs(),
		game_session.get_item_defs(),
		game_session.get_quest_defs()
	)
	var invalid_item_display_name_direct_result := direct_character_management.claim_quest_reward(&"contract_invalid_item_display_name_reward", 26)
	_assert_eq(String(invalid_item_display_name_direct_result.get("error_code", "")), "invalid_item_display_name", "空 item display_name 领奖预览应返回明确 invalid_item_display_name。")
	var missing_display_summary := facade._build_quest_claim_reward_summary_text({
		"item_rewards": [{"item_id": "nameless_reward_item", "quantity": 1}],
	})
	_assert_eq(missing_display_summary, "", "缺少 display_name 的 reward summary 不应回退显示 item_id。")
	var empty_display_summary := facade._build_quest_claim_reward_summary_text({
		"item_rewards": [{"item_id": "nameless_reward_item", "display_name": "", "quantity": 1}],
	})
	_assert_eq(empty_display_summary, "", "空 display_name 的 reward summary 不应回退显示 item_id。")

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

	var submit_accept_result := facade.command_accept_quest(&"contract_supply_delivery")
	_assert_true(bool(submit_accept_result.get("ok", false)), "submit_item 任务接取应成功。")
	runtime_warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())
	var preexisting_iron_ore := runtime_warehouse_service.count_item(&"iron_ore")
	if preexisting_iron_ore > 0:
		runtime_warehouse_service.remove_item(&"iron_ore", preexisting_iron_ore)
	runtime_warehouse_service.add_item(&"iron_ore", 2)
	var submit_result := facade.command_submit_quest_item(&"contract_supply_delivery")
	runtime_warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())
	_assert_true(bool(submit_result.get("ok", false)), "submit_item 任务应能通过正式命令从共享仓库扣料。")
	_assert_eq(String(submit_result.get("message", "")), "已为任务《物资缴纳》提交 铁矿石 x2，奖励待领取。", "submit_item 成功时应返回明确反馈。")
	_assert_eq(int(submit_result.get("submitted_quantity", 0)), 2, "submit_item 结果应暴露实际扣除数量。")
	_assert_eq(runtime_warehouse_service.count_item(&"iron_ore"), 0, "submit_item 成功后共享仓库应扣除对应物资。")
	_assert_true(not facade.get_party_state().has_active_quest(&"contract_supply_delivery"), "submit_item 目标完成后任务应离开 active_quests。")
	_assert_true(facade.get_party_state().has_claimable_quest(&"contract_supply_delivery"), "submit_item 目标完成后任务应进入 claimable_quests。")
	var claimable_submit_item_quest: QuestState = facade.get_party_state().get_claimable_quest_state(&"contract_supply_delivery")
	_assert_true(claimable_submit_item_quest != null, "submit_item 成功后应保留 claimable QuestState。")
	if claimable_submit_item_quest != null:
		_assert_eq(claimable_submit_item_quest.get_objective_progress(&"deliver_ore"), 2, "submit_item 成功后应把对应 objective_progress 推到目标值。")
		_assert_eq(int(claimable_submit_item_quest.last_progress_context.get("submitted_quantity", 0)), 2, "submit_item 成功后 QuestState 应记录实际提交数量。")
		_assert_eq(String(claimable_submit_item_quest.last_progress_context.get("item_id", "")), "iron_ore", "submit_item 成功后 QuestState 应记录正式提交物品。")

	var submit_shortage_accept_result := facade.command_accept_quest(&"contract_supply_delivery_shortage")
	_assert_true(bool(submit_shortage_accept_result.get("ok", false)), "submit_item 缺料任务接取应成功。")
	runtime_warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())
	var iron_ore_to_clear := runtime_warehouse_service.count_item(&"iron_ore")
	if iron_ore_to_clear > 0:
		runtime_warehouse_service.remove_item(&"iron_ore", iron_ore_to_clear)
	var iron_ore_count_before_submit_failure := runtime_warehouse_service.count_item(&"iron_ore")
	var submit_shortage_result := facade.command_submit_quest_item(&"contract_supply_delivery_shortage")
	var shortage_quest: QuestState = facade.get_party_state().get_active_quest_state(&"contract_supply_delivery_shortage")
	runtime_warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())
	_assert_true(not bool(submit_shortage_result.get("ok", true)), "共享仓库缺料时 submit_item 命令应失败。")
	_assert_eq(String(submit_shortage_result.get("message", "")), "共享仓库缺少铁矿石 x2，无法提交给任务《物资缴纳缺料》。", "submit_item 缺料时应返回明确反馈。")
	_assert_eq(runtime_warehouse_service.count_item(&"iron_ore"), iron_ore_count_before_submit_failure, "submit_item 失败时不应吞掉共享仓库库存。")
	_assert_true(shortage_quest != null, "submit_item 失败后任务应继续停留在 active_quests。")
	if shortage_quest != null:
		_assert_eq(shortage_quest.get_objective_progress(&"deliver_ore"), 0, "submit_item 失败时不应推进 quest objective。")
	_assert_true(not facade.get_party_state().has_claimable_quest(&"contract_supply_delivery_shortage"), "submit_item 失败时任务不应误进入 claimable_quests。")

	var submit_wrong_item_accept_result := facade.command_accept_quest(&"contract_supply_delivery_wrong_item")
	_assert_true(bool(submit_wrong_item_accept_result.get("ok", false)), "submit_item 错货任务接取应成功。")
	runtime_warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())
	var wrong_item_iron_ore_to_clear := runtime_warehouse_service.count_item(&"iron_ore")
	if wrong_item_iron_ore_to_clear > 0:
		runtime_warehouse_service.remove_item(&"iron_ore", wrong_item_iron_ore_to_clear)
	var bronze_sword_to_clear := runtime_warehouse_service.count_item(&"bronze_sword")
	if bronze_sword_to_clear > 0:
		runtime_warehouse_service.remove_item(&"bronze_sword", bronze_sword_to_clear)
	runtime_warehouse_service.add_item(&"bronze_sword", 1)
	var bronze_sword_count_before_wrong_submit := runtime_warehouse_service.count_item(&"bronze_sword")
	var submit_wrong_item_result := facade.command_submit_quest_item(&"contract_supply_delivery_wrong_item")
	var wrong_item_quest: QuestState = facade.get_party_state().get_active_quest_state(&"contract_supply_delivery_wrong_item")
	runtime_warehouse_service.setup(facade.get_party_state(), game_session.get_item_defs())
	_assert_true(not bool(submit_wrong_item_result.get("ok", true)), "共享仓库只有错误物品时 submit_item 命令应失败。")
	_assert_eq(String(submit_wrong_item_result.get("message", "")), "共享仓库缺少铁矿石 x2，无法提交给任务《物资缴纳错货》。", "错误物品时 submit_item 应继续指向缺失的正式目标物资。")
	_assert_eq(runtime_warehouse_service.count_item(&"bronze_sword"), bronze_sword_count_before_wrong_submit, "错误物品时不应吞掉共享仓库中的其他物资。")
	_assert_true(wrong_item_quest != null, "错误物品时任务应继续停留在 active_quests。")
	if wrong_item_quest != null:
		_assert_eq(wrong_item_quest.get_objective_progress(&"deliver_ore"), 0, "错误物品时不应推进 quest objective。")
	_assert_true(not facade.get_party_state().has_claimable_quest(&"contract_supply_delivery_wrong_item"), "错误物品时任务不应误进入 claimable_quests。")

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
	facade.start_battle(encounter_anchor)
	var started_battle_state = facade.get_battle_state()
	_assert_true(started_battle_state != null and not started_battle_state.is_empty(), "battle quest 前置应能建立正式 BattleState。")
	if started_battle_state == null or started_battle_state.is_empty():
		facade.dispose()
		_cleanup_test_session(game_session)
		return
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

	var missing_display_name_quest := QuestDef.new()
	missing_display_name_quest.quest_id = &"contract_missing_display_name_reward"
	missing_display_name_quest.description = "用于验证领奖链不再把 quest_id 当成展示名。"
	missing_display_name_quest.provider_interaction_id = &"service_contract_board"
	missing_display_name_quest.objective_defs = item_reward_quest.objective_defs.duplicate(true)
	missing_display_name_quest.reward_entries = _build_reward_entries([
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 1},
	])
	game_session.get_quest_defs()[missing_display_name_quest.quest_id] = missing_display_name_quest

	var legacy_alias_quest := QuestDef.new()
	legacy_alias_quest.quest_id = &"contract_legacy_item_reward_alias"
	legacy_alias_quest.display_name = "旧别名奖励"
	legacy_alias_quest.description = "用于验证 item reward 不再接受 target_id/amount 旧别名。"
	legacy_alias_quest.provider_interaction_id = &"service_contract_board"
	legacy_alias_quest.objective_defs = item_reward_quest.objective_defs.duplicate(true)
	legacy_alias_quest.reward_entries = _build_reward_entries([
		{"reward_type": QuestDef.REWARD_ITEM, "target_id": "iron_ore", "amount": 2},
	])
	game_session.get_quest_defs()[legacy_alias_quest.quest_id] = legacy_alias_quest

	var nameless_reward_item := ItemDef.new()
	nameless_reward_item.item_id = &"nameless_reward_item"
	nameless_reward_item.display_name = ""
	nameless_reward_item.description = "用于验证 quest item reward 不再把 item_id 当展示名。"
	game_session.get_item_defs()[nameless_reward_item.item_id] = nameless_reward_item

	var invalid_item_display_name_quest_def := QuestDef.new()
	invalid_item_display_name_quest_def.quest_id = &"contract_invalid_item_display_name_reward"
	invalid_item_display_name_quest_def.display_name = "空名物品奖励"
	invalid_item_display_name_quest_def.description = "用于验证 item reward 要求 ItemDef.display_name。"
	invalid_item_display_name_quest_def.provider_interaction_id = &"service_contract_board"
	invalid_item_display_name_quest_def.objective_defs = item_reward_quest.objective_defs.duplicate(true)
	invalid_item_display_name_quest_def.reward_entries = _build_reward_entries([
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 7},
		{"reward_type": QuestDef.REWARD_ITEM, "item_id": "nameless_reward_item", "quantity": 1},
	])
	game_session.get_quest_defs()[invalid_item_display_name_quest_def.quest_id] = invalid_item_display_name_quest_def


func _inject_submit_item_quest_defs(game_session) -> void:
	if game_session == null:
		return
	var submit_item_quest := QuestDef.new()
	submit_item_quest.quest_id = &"contract_supply_delivery"
	submit_item_quest.display_name = "物资缴纳"
	submit_item_quest.description = "向任务板提交两份铁矿石。"
	submit_item_quest.provider_interaction_id = &"service_contract_board"
	submit_item_quest.objective_defs = [
		{
			"objective_id": "deliver_ore",
			"objective_type": QuestDef.OBJECTIVE_SUBMIT_ITEM,
			"target_id": "iron_ore",
			"target_value": 2,
		},
	]
	submit_item_quest.reward_entries = _build_reward_entries([
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 18},
	])
	game_session.get_quest_defs()[submit_item_quest.quest_id] = submit_item_quest

	var submit_item_shortage_quest := QuestDef.new()
	submit_item_shortage_quest.quest_id = &"contract_supply_delivery_shortage"
	submit_item_shortage_quest.display_name = "物资缴纳缺料"
	submit_item_shortage_quest.description = "用于验证缺料时 submit_item 不会吞库存。"
	submit_item_shortage_quest.provider_interaction_id = &"service_contract_board"
	submit_item_shortage_quest.objective_defs = submit_item_quest.objective_defs.duplicate(true)
	submit_item_shortage_quest.reward_entries = _build_reward_entries([
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 10},
	])
	game_session.get_quest_defs()[submit_item_shortage_quest.quest_id] = submit_item_shortage_quest

	var submit_item_wrong_item_quest := QuestDef.new()
	submit_item_wrong_item_quest.quest_id = &"contract_supply_delivery_wrong_item"
	submit_item_wrong_item_quest.display_name = "物资缴纳错货"
	submit_item_wrong_item_quest.description = "用于验证仓库只有错误物品时 submit_item 不会误推进或吞库存。"
	submit_item_wrong_item_quest.provider_interaction_id = &"service_contract_board"
	submit_item_wrong_item_quest.objective_defs = submit_item_quest.objective_defs.duplicate(true)
	submit_item_wrong_item_quest.reward_entries = _build_reward_entries([
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 11},
	])
	game_session.get_quest_defs()[submit_item_wrong_item_quest.quest_id] = submit_item_wrong_item_quest


func _inject_string_key_only_quest_def(game_session) -> void:
	if game_session == null:
		return
	var string_key_quest := QuestDef.new()
	string_key_quest.quest_id = &"contract_string_key_only"
	string_key_quest.display_name = "旧 String key 契约"
	string_key_quest.description = "用于验证 quest_defs 不再按 String key 恢复。"
	string_key_quest.provider_interaction_id = &"service_contract_board"
	string_key_quest.objective_defs = [
		{
			"objective_id": "string_key_objective",
			"objective_type": QuestDef.OBJECTIVE_SETTLEMENT_ACTION,
			"target_id": "service:training",
			"target_value": 1,
		},
	]
	string_key_quest.reward_entries = _build_reward_entries([
		{"reward_type": QuestDef.REWARD_GOLD, "amount": 1},
	])
	game_session.get_quest_defs()[String(string_key_quest.quest_id)] = string_key_quest


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
