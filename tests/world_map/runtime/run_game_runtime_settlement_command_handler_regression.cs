using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_game_runtime_settlement_command_handler_regression : SceneTree
{
    private const string TestConfigPath = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(RunAsync));
    }

    private async void RunAsync()
    {
        await TestFacadeUsesSettlementHandlerSurface();
        await TestSettlementHandlerRoutesResearchService();
        await TestSettlementHandlerRoutesActionsAndModalState();
        TestSettlementShopServiceRejectsBadEntrySchema();
        await TestSettlementHandlerRejectsInvalidOrSpoofedActions();
        await TestWorldGenerationExposesResearchService();

        if (_failures.Count == 0)
        {
            GD.Print("Game runtime settlement command handler regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Game runtime settlement command handler regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private async Task TestFacadeUsesSettlementHandlerSurface()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "facade",
            BuildPartyState(12, 100),
            new[] { BuildSettlementRecord("spring_village_01", "春泉村", Vector2I.Zero, BuildBasicSettlementServices(false)) },
            new GDictionary()
        );
        try
        {
            GDictionary windowData = fixture.Runtime.get_settlement_window_data("spring_village_01");
            AssertEq(DictString(windowData, "settlement_id", ""), "spring_village_01", "get_settlement_window_data() 应委托到正式 settlement handler。");

            GDictionary commandResult = fixture.Runtime.command_execute_settlement_action("service:warehouse");
            AssertTrue(DictBool(commandResult, "ok", false), $"command_execute_settlement_action() 应委托到正式 settlement handler。message={DictString(commandResult, "message", "")}");
            AssertEq(fixture.Runtime._active_modal_id, "warehouse", "据点仓储动作应通过 handler 打开共享仓库。");

            fixture.Runtime._active_modal_id = "settlement";
            fixture.Runtime._active_settlement_id = "spring_village_01";
            AssertEq(fixture.Runtime.get_resolved_settlement_id(), "spring_village_01", "get_resolved_settlement_id() 应委托到正式 settlement handler。");
            fixture.Runtime._party_state.pending_character_rewards.Clear();
            fixture.Runtime._character_management.set_party_state(fixture.Runtime._party_state);
            fixture.Runtime._on_settlement_window_closed();
            AssertEq(fixture.Runtime._active_modal_id, "", "_on_settlement_window_closed() 应委托到正式 settlement handler。");
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestSettlementHandlerRoutesResearchService()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "research",
            BuildPartyState(12, 250),
            new[] { BuildSettlementRecord("graystone_town_01", "灰石镇", Vector2I.Zero, BuildResearchServices()) },
            new GDictionary()
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            GDictionary settlementWindowData = handler.get_settlement_window_data("graystone_town_01");
            GDictionary researchService = FindServiceEntry(DictArray(settlementWindowData, "available_services"), "service:research");
            AssertTrue(researchService.Count > 0, "据点窗口应暴露正式 research 服务入口。");
            AssertEq(DictString(researchService, "interaction_script_id", ""), "service_research", "research 服务应使用正式 interaction_script_id。");
            AssertTrue(DictBool(researchService, "is_enabled", false), "金币充足时 research 服务入口应可点击。");
            AssertEq(DictString(researchService, "cost_label", ""), "200 金", "research 服务应暴露正式金币成本。");

            GDictionary researchResult = handler.command_execute_settlement_action("service:research", new GDictionary());
            AssertTrue(DictBool(researchResult, "ok", false), $"research 服务应能走正式 settlement action dispatch。message={DictString(researchResult, "message", "")}");
            AssertEq(runtime._party_state.get_gold(), 50, "research 服务成功后应扣除正式研究成本。");
            AssertEq(runtime._active_modal_id, "settlement", "research 服务不应切走当前 settlement modal。");
            AssertTrue(runtime._active_settlement_feedback_text.Contains("研究"), "research 服务应写入正式据点反馈。");
            AssertTrue(runtime._current_status_message.Contains("研究"), "research 服务应刷新状态文案。");
            AssertFalse(runtime._current_status_message.Contains("尚未开放"), "research 服务不应继续使用未开放占位文案。");
            AssertFalse(fixture.GameSession.has_pending_save(), "research 服务成功后应提交队伍状态持久化。");
            AssertEq(CountPendingRewardsBySourceId(runtime._party_state, "research_field_manual"), 1, "research 服务成功后应正式排入 research_field_manual 奖励。");
            GDictionary firstResearchReward = FindPendingRewardBySourceId(runtime._party_state, "research_field_manual");
            AssertEq(DictString(firstResearchReward, "member_id", ""), "hero", "research 奖励应写入目标成员。");
            AssertEq(DictString(firstResearchReward, "member_name", ""), "Hero", "research 奖励应保留成员显示名。");
            AssertEq(DictString(firstResearchReward, "source_type", ""), "npc_teach", "research 奖励应沿用正式 source_type 命名。");
            AssertEq(DictString(firstResearchReward, "source_id", ""), "research_field_manual", "知识型 research 奖励应写入具体来源 ID。");
            AssertEq(DictString(firstResearchReward, "source_label", ""), "大图书官·研究", "research 奖励应沿用正式 source_label 命名。");
            GDictionary firstRewardEntry = GetFirstRewardEntry(firstResearchReward);
            AssertEq(DictString(firstRewardEntry, "entry_type", ""), "knowledge_unlock", "首条 research 奖励应先构造成知识奖励。");
            AssertEq(DictString(firstRewardEntry, "target_id", ""), "field_manual", "首条 research 奖励应指向野外手册知识。");

            GDictionary refreshedWindowData = handler.get_settlement_window_data("graystone_town_01");
            GDictionary refreshedResearchService = FindServiceEntry(DictArray(refreshedWindowData, "available_services"), "service:research");
            AssertFalse(DictBool(refreshedResearchService, "is_enabled", true), "扣费后金币不足时 research 服务应及时禁用。");
            AssertEq(DictString(refreshedResearchService, "disabled_reason", ""), "金币不足", "research 服务禁用原因应明确显示金币不足。");

            runtime._party_state.set_gold(250);
            runtime._character_management.set_party_state(runtime._party_state);
            GDictionary reenabledWindowData = handler.get_settlement_window_data("graystone_town_01");
            GDictionary reenabledResearchService = FindServiceEntry(DictArray(reenabledWindowData, "available_services"), "service:research");
            AssertTrue(DictBool(reenabledResearchService, "is_enabled", false), "首条 research 奖励尚未确认时，也应切到下一条可研究内容，而不是重复给野外手册。");

            GDictionary secondResearchResult = handler.command_execute_settlement_action("service:research", new GDictionary());
            AssertTrue(DictBool(secondResearchResult, "ok", false), $"第二次 research 服务应继续走正式 settlement action dispatch。message={DictString(secondResearchResult, "message", "")}");
            AssertEq(CountPendingRewardsBySourceId(runtime._party_state, "research_guard_break"), 1, "第二次 research 服务应继续把 research_guard_break 奖励同步写回 party_state。");
            GDictionary secondResearchReward = FindPendingRewardBySourceId(runtime._party_state, "research_guard_break");
            AssertEq(DictString(secondResearchReward, "source_type", ""), "npc_teach", "技能型 research 奖励也应沿用正式 source_type 命名。");
            AssertEq(DictString(secondResearchReward, "source_id", ""), "research_guard_break", "技能型 research 奖励应写入具体来源 ID。");
            AssertEq(DictString(secondResearchReward, "source_label", ""), "大图书官·研究", "技能型 research 奖励应保留统一来源标签。");
            GDictionary secondRewardEntry = GetFirstRewardEntry(secondResearchReward);
            AssertEq(DictString(secondRewardEntry, "entry_type", ""), "skill_unlock", "第二条 research 奖励应构造成技能奖励。");
            AssertEq(DictString(secondRewardEntry, "target_id", ""), "warrior_guard_break", "第二条 research 奖励应指向裂甲斩技能。");

            runtime._party_state.set_gold(250);
            runtime._character_management.set_party_state(runtime._party_state);
            GDictionary exhaustedWindowData = handler.get_settlement_window_data("graystone_town_01");
            GDictionary exhaustedResearchService = FindServiceEntry(DictArray(exhaustedWindowData, "available_services"), "service:research");
            AssertFalse(DictBool(exhaustedResearchService, "is_enabled", true), "同成员两条 research 奖励都已挂入 pending 队列后，服务应禁用。");
            AssertEq(DictString(exhaustedResearchService, "disabled_reason", ""), "暂无可研究内容", "research 已被 pending 队列占满时应给出明确禁用原因。");
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestSettlementHandlerRoutesActionsAndModalState()
    {
        GDictionary questDefs = BuildQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "actions",
            BuildPartyState(20, 200),
            new[]
            {
                BuildSettlementRecord("spring_village_01", "春泉村", Vector2I.Zero, BuildBasicSettlementServices(true)),
                BuildSettlementRecord("graystone_town_01", "灰石镇", new Vector2I(2, 1), new GArray()),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            GDictionary settlementWindowData = handler.get_settlement_window_data("spring_village_01");
            GDictionary contractService = FindServiceEntry(DictArray(settlementWindowData, "available_services"), "service:contract_board");
            GDictionary bountyService = FindServiceEntry(DictArray(settlementWindowData, "available_services"), "service:bounty_registry");
            AssertTrue(QuestProviderContentRules.is_supported_provider_id("service_contract_board"), "任务板 provider 应来自共享 quest provider 白名单。");
            AssertTrue(QuestProviderContentRules.is_supported_provider_id("service_bounty_registry"), "悬赏署 provider 应来自共享 quest provider 白名单。");
            AssertTrue(contractService.Count > 0, "据点窗口应暴露任务板服务入口。");
            AssertTrue(DictBool(contractService, "is_enabled", false), "任务板服务入口应为可点击状态。");
            AssertTrue(bountyService.Count > 0, "据点窗口应暴露悬赏署服务入口。");
            AssertTrue(DictBool(bountyService, "is_enabled", false), "悬赏署服务入口应为可点击状态。");

            var warehouseQuest = new QuestState { quest_id = "contract_warehouse_visit" };
            warehouseQuest.mark_accepted(runtime.get_world_step());
            runtime._party_state.set_active_quest_state(warehouseQuest);
            runtime._character_management.set_party_state(runtime._party_state);
            GDictionary warehouseResult = handler.command_execute_settlement_action("service:warehouse", new GDictionary());
            AssertTrue(DictBool(warehouseResult, "ok", false), "据点仓储动作应执行成功。");
            AssertEq(runtime._active_settlement_id, "spring_village_01", "仓储动作后应记录当前据点 ID。");
            AssertEq(runtime._active_modal_id, "warehouse", "仓储动作后应打开共享仓库 modal。");
            AssertTrue(runtime._active_warehouse_entry_label.Contains("据点服务"), "仓储入口标签应包含据点服务来源。");
            AssertEq(runtime._current_status_message, "已从据点服务打开共享仓库。", "仓储动作后应刷新状态文案。");
            AssertTrue(runtime._party_state.has_claimable_quest("contract_warehouse_visit"), "仓储动作应通过 typed SettlementServiceResult 应用默认 quest_progress_events。");
            AssertFalse(fixture.GameSession.has_pending_save(), "仓储动作成功后应通过 typed SettlementServiceResult 提交队伍状态持久化。");
            runtime._active_modal_id = "settlement";
            runtime._active_settlement_id = "spring_village_01";

            GDictionary contractBoardResult = handler.command_execute_settlement_action("service:contract_board", new GDictionary());
            GDictionary contractBoardWindowData = handler.get_contract_board_window_data();
            List<string> contractBoardEntryIds = ExtractContractBoardEntryIds(DictArray(contractBoardWindowData, "entries"));
            AssertTrue(DictBool(contractBoardResult, "ok", false), "任务板服务应能切换到 contract_board modal。");
            AssertEq(runtime._active_modal_id, "contract_board", "任务板服务后应切换到 contract_board modal。");
            AssertEq(DictString(contractBoardWindowData, "action_id", ""), "service:contract_board", "任务板 modal 应保留原始 action_id。");
            AssertEq(DictString(contractBoardWindowData, "provider_interaction_id", ""), "service_contract_board", "任务板 modal 应记录当前 provider_interaction_id。");
            AssertSequence(contractBoardEntryIds, new[] { "contract_first_hunt", "contract_manual_drill", "contract_repeatable_patrol", "contract_supply_drop" }, "任务板 modal 只应按 provider_interaction_id 暴露当前服务的契约条目。");
            AssertTrue(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_missing_display_name").Count == 0, "缺少 display_name 的契约不应回退成 quest_id 出现在任务板。");
            AssertTrue(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_missing_description").Count == 0, "缺少 description 的契约不应回退成暂无说明出现在任务板。");
            AssertTrue(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_missing_objectives").Count == 0, "缺少 objective_defs 的契约不应回退成暂无目标说明出现在任务板。");
            AssertTrue(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_missing_objective_target").Count == 0, "缺少 target_id 的据点事务目标不应回退成未命名出现在任务板。");
            AssertTrue(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_unknown_objective_type").Count == 0, "未知 objective_type 不应回退成 objective_id 出现在任务板。");
            AssertTrue(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_missing_rewards").Count == 0, "缺少 reward_entries 的契约不应回退成奖励待定出现在任务板。");
            AssertTrue(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_invalid_reward_amount").Count == 0, "非法 reward amount 的契约不应回退成奖励待定。");
            AssertTrue(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_string_key_only").Count == 0, "String key-only 契约不应被任务板恢复。");

            AssertFalse(DictBool(handler.command_execute_settlement_action("service:contract_board", new GDictionary
            {
                ["submission_source"] = "contract_board",
                ["quest_id"] = "contract_missing_display_name",
                ["provider_interaction_id"] = "service_contract_board",
            }), "ok", true), "缺少 display_name 的坏契约即使被构造提交也应拒绝。");
            AssertFalse(DictBool(handler.command_execute_settlement_action("service:contract_board", new GDictionary
            {
                ["panel_kind"] = "contract_board",
                ["quest_id"] = "contract_manual_drill",
                ["provider_interaction_id"] = "service_contract_board",
            }), "ok", false), "旧 panel_kind 字段不应再被识别为任务板提交或普通据点动作。");
            GDictionary legacyEntrySubmission = handler.command_execute_settlement_action("service:contract_board", new GDictionary
            {
                ["submission_source"] = "contract_board",
                ["entry_id"] = "contract_manual_drill",
                ["provider_interaction_id"] = "service_contract_board",
            });
            AssertFalse(DictBool(legacyEntrySubmission, "ok", true), "旧 entry_id 字段不应回退成 quest_id。");
            AssertEq(runtime._current_status_message, "当前契约条目缺少 quest_id，无法接取。", "旧 entry_id 提交应返回缺 quest_id 的反馈。");
            GDictionary legacyProviderSubmission = handler.command_execute_settlement_action("service:contract_board", new GDictionary
            {
                ["submission_source"] = "contract_board",
                ["quest_id"] = "contract_manual_drill",
                ["interaction_script_id"] = "service_contract_board",
            });
            AssertFalse(DictBool(legacyProviderSubmission, "ok", true), "旧 interaction_script_id 字段不应回退成 provider_interaction_id。");
            AssertEq(runtime._current_status_message, "当前契约条目缺少 provider_interaction_id，无法匹配任务板。", "旧 interaction_script_id 提交应返回缺 provider_interaction_id 的反馈。");
            GDictionary stringKeySubmission = handler.command_execute_settlement_action("service:contract_board", new GDictionary
            {
                ["submission_source"] = "contract_board",
                ["quest_id"] = "contract_string_key_only",
                ["provider_interaction_id"] = "service_contract_board",
            });
            AssertFalse(DictBool(stringKeySubmission, "ok", true), "String key-only 契约即使被构造提交也应拒绝。");
            AssertEq(runtime._current_status_message, "当前任务板未找到契约 contract_string_key_only。", "String key-only 提交应按未找到任务处理。");
            GDictionary mismatchedContractSubmission = handler.command_execute_settlement_action("service:bounty_registry", new GDictionary
            {
                ["submission_source"] = "contract_board",
                ["quest_id"] = "contract_manual_drill",
                ["provider_interaction_id"] = "service_contract_board",
            });
            AssertFalse(DictBool(mismatchedContractSubmission, "ok", true), "任务板提交不应允许切到其他 action_id。");
            AssertEq(runtime._current_status_message, "当前任务板与请求的服务入口不一致。", "任务板 action_id 不匹配时应返回明确反馈。");

            GDictionary acceptContractResult = handler.command_execute_settlement_action("service:contract_board", new GDictionary
            {
                ["submission_source"] = "contract_board",
                ["quest_id"] = "contract_manual_drill",
                ["provider_interaction_id"] = "service_contract_board",
            });
            GDictionary acceptedContractEntry = FindContractBoardEntry(DictArray(handler.get_contract_board_window_data(), "entries"), "contract_manual_drill");
            AssertTrue(DictBool(acceptContractResult, "ok", false), $"任务板提交应保持据点动作链路可执行。message={DictString(acceptContractResult, "message", "")}");
            AssertTrue(runtime._party_state.has_active_quest("contract_manual_drill"), "任务板接取后应把任务写入 PartyState.active_quests。");
            AssertEq(runtime._active_modal_id, "contract_board", "接取契约后应继续停留在 contract_board modal。");
            AssertEq(runtime._current_status_message, "已接取任务《训练记录》。", "任务板接取后应更新成功反馈。");
            AssertEq(DictString(acceptedContractEntry, "state_id", ""), "active", "接取后的契约条目应刷新为 active。");
            AssertEq(DictString(handler.get_contract_board_window_data(), "summary_text", ""), "已接取任务《训练记录》。", "任务板 summary_text 应刷新为最新反馈。");

            handler.command_execute_settlement_action("service:contract_board", new GDictionary
            {
                ["submission_source"] = "contract_board",
                ["quest_id"] = "contract_manual_drill",
                ["provider_interaction_id"] = "service_contract_board",
            });
            AssertEq(runtime._current_status_message, "任务《训练记录》已在进行中，不能重复接取。", "重复接取时应返回明确反馈。");

            AssertTrue(runtime._party_state.mark_quest_completed("contract_manual_drill", runtime.get_world_step()), "测试前置：普通契约应能标记完成。");
            runtime._character_management.set_party_state(runtime._party_state);
            int manualClaimGoldBefore = runtime._party_state.get_gold();
            handler.command_execute_settlement_action("service:contract_board", new GDictionary
            {
                ["submission_source"] = "contract_board",
                ["quest_id"] = "contract_manual_drill",
                ["provider_interaction_id"] = "service_contract_board",
            });
            GDictionary claimedContractEntry = FindContractBoardEntry(DictArray(handler.get_contract_board_window_data(), "entries"), "contract_manual_drill");
            AssertEq(runtime._current_status_message, "已领取任务《训练记录》奖励，获得 30 金。", "claimable 契约提交时应返回领奖反馈。");
            AssertEq(runtime._party_state.get_gold(), manualClaimGoldBefore + 30, "claimable 契约提交后应把金币奖励写入 PartyState。");
            AssertFalse(runtime._party_state.has_active_quest("contract_manual_drill"), "已完成非 repeatable 契约不应重新回到 active_quests。");
            AssertFalse(runtime._party_state.has_claimable_quest("contract_manual_drill"), "领奖后的非 repeatable 契约不应继续停留在 claimable_quests。");
            AssertTrue(runtime._party_state.has_completed_quest("contract_manual_drill"), "领奖后的非 repeatable 契约应进入 completed_quest_ids。");
            AssertEq(DictString(claimedContractEntry, "state_id", ""), "completed", "领奖后的普通契约条目应刷新为 completed。");

            var repeatableQuest = new QuestState { quest_id = "contract_repeatable_patrol" };
            repeatableQuest.mark_accepted(runtime.get_world_step());
            runtime._party_state.set_active_quest_state(repeatableQuest);
            AssertTrue(runtime._party_state.mark_quest_completed("contract_repeatable_patrol", runtime.get_world_step()), "测试前置：repeatable 契约应先进入待领奖励状态。");
            runtime._character_management.set_party_state(runtime._party_state);
            int repeatableClaimGoldBefore = runtime._party_state.get_gold();
            handler.command_execute_settlement_action("service:contract_board", new GDictionary
            {
                ["submission_source"] = "contract_board",
                ["quest_id"] = "contract_repeatable_patrol",
                ["provider_interaction_id"] = "service_contract_board",
            });
            GDictionary repeatableEntry = FindContractBoardEntry(DictArray(handler.get_contract_board_window_data(), "entries"), "contract_repeatable_patrol");
            AssertEq(runtime._current_status_message, "已领取任务《巡路值守》奖励，获得 15 金。", "repeatable 契约领奖时应返回明确反馈。");
            AssertEq(runtime._party_state.get_gold(), repeatableClaimGoldBefore + 15, "repeatable 契约领奖后应增加金币。");
            AssertTrue(runtime._party_state.has_completed_quest("contract_repeatable_patrol"), "repeatable 契约领奖后应进入 completed_quest_ids。");
            AssertEq(DictString(repeatableEntry, "state_id", ""), "repeatable", "repeatable 契约领奖后条目应刷新为 repeatable。");

            fixture.WarehouseService.add_item("iron_ore", 2);
            var submitItemQuest = new QuestState { quest_id = "contract_supply_drop" };
            submitItemQuest.mark_accepted(runtime.get_world_step());
            runtime._party_state.set_active_quest_state(submitItemQuest);
            runtime._character_management.set_party_state(runtime._party_state);
            handler.command_execute_settlement_action("service:contract_board", new GDictionary
            {
                ["submission_source"] = "contract_board",
                ["quest_id"] = "contract_supply_drop",
                ["provider_interaction_id"] = "service_contract_board",
            });
            GDictionary submitItemEntry = FindContractBoardEntry(DictArray(handler.get_contract_board_window_data(), "entries"), "contract_supply_drop");
            AssertEq(runtime._current_status_message, "已为任务《物资缴纳》提交 铁矿石 x2，奖励待领取。", "submit_item 提交后应刷新正式反馈。");
            AssertFalse(runtime._party_state.has_active_quest("contract_supply_drop"), "submit_item 提交完成后任务应离开 active_quests。");
            AssertTrue(runtime._party_state.has_claimable_quest("contract_supply_drop"), "submit_item 提交完成后任务应进入 claimable_quests。");
            AssertEq(DictString(submitItemEntry, "state_id", ""), "claimable", "submit_item 提交后条目应刷新为 claimable。");

            handler.on_contract_board_window_closed();
            AssertEq(runtime._active_modal_id, "settlement", "关闭任务板后应返回 settlement modal。");
            AssertEq(runtime._active_settlement_id, "spring_village_01", "关闭任务板后应继续保留当前据点。");

            GDictionary bountyBoardResult = handler.command_execute_settlement_action("service:bounty_registry", new GDictionary());
            GDictionary bountyBoardWindowData = handler.get_contract_board_window_data();
            AssertTrue(DictBool(bountyBoardResult, "ok", false), "悬赏署服务应复用 contract_board modal。");
            AssertEq(runtime._active_modal_id, "contract_board", "悬赏署服务后仍应落到 contract_board modal。");
            AssertEq(DictString(bountyBoardWindowData, "action_id", ""), "service:bounty_registry", "悬赏署 modal 应保留原始 action_id。");
            AssertEq(DictString(bountyBoardWindowData, "provider_interaction_id", ""), "service_bounty_registry", "悬赏署 modal 应记录自己的 provider_interaction_id。");
            AssertSequence(ExtractContractBoardEntryIds(DictArray(bountyBoardWindowData, "entries")), new[] { "contract_regional_bounty" }, "悬赏署 modal 只应暴露自己的 bounty quest。");

            handler.on_contract_board_window_closed();
            handler.command_execute_settlement_action("service:contract_board", new GDictionary());
            AssertSequence(ExtractContractBoardEntryIds(DictArray(handler.get_contract_board_window_data(), "entries")), new[] { "contract_first_hunt", "contract_manual_drill", "contract_repeatable_patrol", "contract_supply_drop" }, "悬赏署 provider 不应污染正式 contract board 列表。");
            handler.on_contract_board_window_closed();

            GDictionary trainingResult = handler.command_execute_settlement_action("service:training", new GDictionary
            {
                ["pending_character_rewards"] = new GArray { BuildTrainingRewardPayload() },
            });
            AssertTrue(DictBool(trainingResult, "ok", false), "普通据点动作应执行成功。");
            AssertTrue(runtime._active_settlement_feedback_text.Contains("训练"), "普通据点动作后应写入据点反馈文本。");
            AssertTrue(runtime._party_state.pending_character_rewards.Count >= 1, "带 pending_character_rewards 的据点动作应归并出待领奖励。");
            AssertTrue(runtime._current_status_message.Contains("事务"), "普通据点动作完成后应刷新状态文案。");

            GDictionary questTrainingResult = handler.execute_settlement_action("spring_village_01", "service:training", new GDictionary
            {
                ["interaction_script_id"] = "training_service",
                ["facility_name"] = "训练场",
                ["npc_name"] = "教官",
                ["service_type"] = "训练",
                ["member_id"] = "hero",
                ["quest_progress_events"] = new GArray
                {
                    new GDictionary { ["event_type"] = "accept", ["quest_id"] = "contract_training" },
                    new GDictionary
                    {
                        ["event_type"] = "progress",
                        ["quest_id"] = "contract_training",
                        ["objective_id"] = "train_once",
                        ["progress_delta"] = 1,
                        ["target_value"] = 1,
                        ["settlement_id"] = "spring_village_01",
                    },
                },
            });
            handler.on_settlement_action_requested("spring_village_01", "service:training", new GDictionary
            {
                ["interaction_script_id"] = "training_service",
                ["facility_name"] = "训练场",
                ["npc_name"] = "教官",
                ["service_type"] = "训练",
                ["member_id"] = "hero",
                ["quest_progress_events"] = DictArray(questTrainingResult, "quest_progress_events"),
            });
            QuestState trainingQuest = runtime._party_state.get_quest_state("contract_training");
            AssertEq(DictArray(questTrainingResult, "quest_progress_events").Count, 3, "据点服务结果应包含显式 quest_progress_events 与默认据点动作事件。");
            AssertTrue(trainingQuest != null, "据点动作应能把 quest_progress_events 写入 PartyState。");
            if (trainingQuest != null)
            {
                AssertEq(trainingQuest.get_objective_progress("train_once"), 1, "据点动作应推进任务目标进度。");
            }
            AssertTrue(runtime._party_state.has_claimable_quest("contract_training"), "目标完成后据点动作应把 QuestDef 任务推进到 claimable_quests。");
            AssertEq(handler._result_quest_progress_events(questTrainingResult).Count, 3, "typed service result helper 应读取 canonical quest_progress_events。");
            AssertEq(handler._result_quest_progress_events(new GDictionary()).Count, 0, "typed service result helper 应拒绝非 canonical result。");

            GDictionary canonicalTrainingResult = handler.execute_settlement_action("spring_village_01", "service:training", new GDictionary
            {
                ["interaction_script_id"] = "training_service",
                ["facility_name"] = "训练场",
                ["npc_name"] = "教官",
                ["service_type"] = "训练",
                ["member_id"] = "hero",
                ["pending_character_rewards"] = new GArray { BuildTrainingRewardPayload() },
            });
            AssertTrue(DictBool(canonicalTrainingResult, "success", false), "据点服务结果应成功。");
            AssertTrue(canonicalTrainingResult.ContainsKey("pending_character_rewards"), "据点服务结果应包含 canonical pending_character_rewards。");
            AssertTrue(canonicalTrainingResult.ContainsKey("service_side_effects"), "据点服务结果应包含 service_side_effects。");
            AssertEq(DictArray(canonicalTrainingResult, "pending_character_rewards").Count, 1, "据点服务结果应输出 canonical 奖励数组。");
            AssertEq(handler._result_pending_character_rewards(canonicalTrainingResult).Count, 1, "typed service result helper 应读取 canonical pending_character_rewards。");
            AssertEq(handler._result_pending_character_rewards(new GDictionary()).Count, 0, "typed service result helper 应拒绝非 canonical result。");
            AssertFalse(canonicalTrainingResult.ContainsKey("pending_mastery_rewards"), "据点服务结果不应再输出 legacy pending_mastery_rewards。");
            AssertFalse(canonicalTrainingResult.ContainsKey("effects"), "据点服务结果不应再输出 legacy effects。");
            AssertEq(DictInt(canonicalTrainingResult, "gold_delta", 0), 0, "普通据点服务不应修改金币字段。");

            GDictionary legacyRewardSourceResult = handler.execute_settlement_action("spring_village_01", "service:training", new GDictionary
            {
                ["interaction_script_id"] = "training_service",
                ["facility_name"] = "训练场",
                ["npc_name"] = "教官",
                ["service_type"] = "训练",
                ["member_id"] = "hero",
                ["mastery_source_type"] = "legacy_mastery",
                ["pending_character_rewards"] = new GArray
                {
                    new GDictionary
                    {
                        ["member_id"] = "hero",
                        ["entries"] = new GArray
                        {
                            new GDictionary { ["entry_type"] = "skill_mastery", ["target_id"] = "warrior_heavy_strike", ["amount"] = 1 },
                        },
                    },
                },
            });
            GArray legacyRewardEntries = DictArray(legacyRewardSourceResult, "pending_character_rewards");
            GDictionary legacyReward = legacyRewardEntries.Count > 0 && legacyRewardEntries[0].VariantType == Variant.Type.Dictionary
                ? legacyRewardEntries[0].AsGodotDictionary()
                : new GDictionary();
            AssertEq(DictString(legacyReward, "source_type", ""), "training", "旧 mastery_source_type 不应回退成奖励 source_type。");
            AssertEq(DictString(legacyReward, "source_id", ""), "training", "旧 mastery_source_type 不应回退成奖励 source_id。");

            runtime._party_state.set_gold(200);
            runtime._party_state.get_member_state("hero").current_hp = 10;
            runtime._character_management.set_party_state(runtime._party_state);
            GDictionary restResult = handler.execute_settlement_action("spring_village_01", "service:rest_full", new GDictionary
            {
                ["interaction_script_id"] = "service_rest_full",
                ["facility_name"] = "旅店",
                ["npc_name"] = "店主",
                ["service_type"] = "整备",
                ["member_id"] = "hero",
            });
            AssertTrue(DictBool(restResult, "success", false), "整备服务应执行成功。");
            AssertEq(runtime._party_state.gold, 150, "整备服务应扣除 50 金。");
            AssertEq(runtime.get_world_step(), 1, "整备服务应推进 1 点 world_step。");
            AssertEq(runtime._party_state.get_member_state("hero").current_hp, 40, "整备服务应把当前生命恢复到上限。");
            AssertEq(DictInt(restResult, "gold_delta", 0), -50, "整备服务结果应记录金币变化。");
            AssertTrue(DictDictionary(restResult, "service_side_effects").ContainsKey("world_step_advanced"), "整备服务结果应记录 world_step_advanced。");
            AssertFalse(restResult.ContainsKey("effects"), "整备服务结果不应再输出 legacy effects。");

            GDictionary missingResult = handler.execute_settlement_action("missing_settlement", "service:training", new GDictionary());
            AssertFalse(DictBool(missingResult, "success", true), "缺失据点时服务结果应失败。");
            AssertTrue(missingResult.ContainsKey("pending_character_rewards"), "失败结果也应包含 canonical pending_character_rewards。");
            AssertTrue(missingResult.ContainsKey("service_side_effects"), "失败结果也应包含 service_side_effects。");
            AssertFalse(missingResult.ContainsKey("pending_mastery_rewards"), "失败结果也不应保留 legacy pending_mastery_rewards。");
            AssertFalse(missingResult.ContainsKey("effects"), "失败结果也不应保留 legacy effects。");

            runtime._active_modal_id = "settlement";
            runtime._active_settlement_id = "spring_village_01";
            GDictionary stagecoachResult = handler.command_execute_settlement_action("service:stagecoach", new GDictionary());
            AssertTrue(DictBool(stagecoachResult, "ok", false), $"驿站服务应能打开路线窗口。message={DictString(stagecoachResult, "message", "")}");
            AssertEq(runtime._active_modal_id, "stagecoach", "打开驿站后应切换到驿站 modal。");
            GDictionary travelResult = handler.command_stagecoach_travel("graystone_town_01");
            AssertTrue(DictBool(travelResult, "ok", false), $"驿站换乘应执行成功。message={DictString(travelResult, "message", "")}");
            AssertEq(runtime._active_modal_id, "settlement", "驿站换乘后应回到目标据点窗口。");
            AssertEq(runtime._active_settlement_id, "graystone_town_01", "驿站换乘后应记录目标据点。");
            AssertEq(runtime._party_state.gold, 120, "驿站换乘应按距离扣除路费。");
            AssertEq(runtime._player_coord, new Vector2I(2, 1), "驿站换乘后应更新玩家坐标。");

            handler.on_settlement_window_closed();
            AssertEq(runtime._active_settlement_id, "", "关闭据点窗口应清空当前据点 ID。");
            AssertEq(runtime._active_settlement_feedback_text, "", "关闭据点窗口应清空反馈文本。");
            AssertEq(runtime._active_modal_id, "reward", "存在待确认角色奖励时，关闭据点窗口后应立即恢复奖励 modal。");
            AssertEq(runtime._current_status_message, "已关闭据点窗口，返回世界地图。", "关闭据点窗口后应刷新状态文案。");
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private void TestSettlementShopServiceRejectsBadEntrySchema()
    {
        var shopService = new SettlementShopService();
        GDictionary itemDefs = new ItemContentRegistry().get_item_defs();
        GDictionary settlementRecord = MinimalSettlementRecord("spring_village_01", "春泉村", Vector2I.Zero, new GArray());
        PartyState validParty = BuildPartyState(10, 100);
        var validWarehouse = new PartyWarehouseService();
        validWarehouse.setup(validParty, itemDefs);
        validWarehouse.add_item("travel_ration", 3);
        GDictionary validWindowData = shopService.build_window_data(
            "service_basic_supply",
            settlementRecord,
            BuildShopState(new GArray { new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = 2, ["unit_price"] = 12, ["sold_out"] = false } }),
            itemDefs,
            validWarehouse,
            100
        );
        AssertEq(DictArray(validWindowData, "buy_entries").Count, 1, "正式 shop stock entry 应生成可购买条目。");
        AssertEq(DictArray(validWindowData, "sell_entries").Count, 1, "正式 sell inventory entry 应生成可出售条目。");

        var invalidStockCases = new (string Label, GDictionary Entry)[]
        {
            ("字符串 quantity", new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = "2", ["unit_price"] = 12, ["sold_out"] = false }),
            ("字符串 unit_price", new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = 2, ["unit_price"] = "12", ["sold_out"] = false }),
            ("缺 unit_price", new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = 2, ["sold_out"] = false }),
            ("空 item_id", new GDictionary { ["item_id"] = "", ["quantity"] = 2, ["unit_price"] = 12, ["sold_out"] = false }),
            ("旧 price 字段", new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = 2, ["unit_price"] = 12, ["price"] = 1, ["sold_out"] = false }),
            ("字符串 sold_out", new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = 2, ["unit_price"] = 12, ["sold_out"] = "false" }),
            ("非正 quantity", new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = 0, ["unit_price"] = 12, ["sold_out"] = false }),
        };
        foreach ((string label, GDictionary entry) in invalidStockCases)
        {
            PartyState partyState = BuildPartyState(10, 100);
            var warehouse = new PartyWarehouseService();
            warehouse.setup(partyState, itemDefs);
            GDictionary settlementState = BuildShopState(new GArray { entry.Duplicate(true) });
            GDictionary windowData = shopService.build_window_data("service_basic_supply", settlementRecord, settlementState, itemDefs, warehouse, partyState.gold);
            AssertEq(DictArray(windowData, "buy_entries").Count, 0, $"{label} 的坏 shop stock 不应生成购买窗口条目。");
            int goldBefore = partyState.gold;
            GDictionary buyResult = shopService.buy("service_basic_supply", settlementRecord, settlementState, itemDefs, warehouse, partyState, "healing_herb", 1, "");
            AssertFalse(DictBool(buyResult, "success", true), $"{label} 的坏 shop stock 不应允许购买交易。");
            AssertEq(partyState.gold, goldBefore, $"{label} 的坏 shop stock 不应扣除金币。");
            AssertEq(warehouse.count_item("healing_herb"), 0, $"{label} 的坏 shop stock 不应写入仓库。");
        }

        GDictionary noPriceItemDefs = itemDefs.Duplicate();
        noPriceItemDefs["no_price_sample"] = MakeShopItemDef("无价样品", "没有正式回收价。", 10, 0, true);
        PartyState noPriceParty = BuildPartyState(10, 0);
        var noPriceWarehouse = new PartyWarehouseService();
        noPriceWarehouse.setup(noPriceParty, noPriceItemDefs);
        noPriceWarehouse.add_item("no_price_sample", 1);
        GDictionary noPriceWindowData = shopService.build_window_data("service_basic_supply", settlementRecord, BuildShopState(new GArray()), noPriceItemDefs, noPriceWarehouse, 100);
        AssertEq(DictArray(noPriceWindowData, "sell_entries").Count, 0, "缺少正式 sell_price 的物品不应补默认回收价。");
        GDictionary noPriceSellResult = shopService.sell("service_basic_supply", settlementRecord, BuildShopState(new GArray()), noPriceItemDefs, noPriceWarehouse, noPriceParty, "no_price_sample", 1, "");
        AssertFalse(DictBool(noPriceSellResult, "success", true), "缺少正式 sell_price 的物品不应允许出售交易。");
        AssertEq(noPriceParty.gold, 0, "缺少正式 sell_price 的出售失败不应增加金币。");
        AssertEq(noPriceWarehouse.count_item("no_price_sample"), 1, "缺少正式 sell_price 的出售失败不应移除仓库物品。");
    }

    private async Task TestSettlementHandlerRejectsInvalidOrSpoofedActions()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "invalid",
            BuildPartyState(12, 100),
            new[] { BuildSettlementRecord("spring_village_01", "春泉村", Vector2I.Zero, BuildShopAndStagecoachServices()) },
            new GDictionary()
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            runtime._active_modal_id = "";
            GDictionary closedModalResult = handler.command_execute_settlement_action("service:basic_supply", new GDictionary());
            AssertFalse(DictBool(closedModalResult, "ok", true), "未打开据点窗口时不应执行据点服务。");
            AssertEq(DictString(closedModalResult, "message", ""), "当前没有打开对应的据点窗口。", "未打开据点窗口应返回明确错误。");
            runtime._active_modal_id = "settlement";

            runtime._fog_system.setup(new Vector2I(8, 8));
            GDictionary hiddenSettlementResult = handler.command_execute_settlement_action("service:basic_supply", new GDictionary());
            AssertFalse(DictBool(hiddenSettlementResult, "ok", true), "不可见据点不应执行据点服务。");
            AssertEq(DictString(hiddenSettlementResult, "message", ""), "当前据点不在视野中，不能执行据点服务。", "不可见据点应返回明确错误。");
            MakeVisible(runtime, Vector2I.Zero);

            GDictionary missingActionResult = handler.command_execute_settlement_action("service:missing", new GDictionary());
            AssertFalse(DictBool(missingActionResult, "ok", true), "未开放的 action_id 应被直接拒绝。");
            AssertTrue(DictString(missingActionResult, "message", "").Contains("未开放该服务"), "未开放 action_id 的错误信息应明确指出未开放。");
            AssertEq(runtime._active_modal_id, "settlement", "未开放 action_id 失败后不应切换 modal。");

            GDictionary disabledStagecoachResult = handler.command_execute_settlement_action("service:stagecoach", new GDictionary());
            AssertFalse(DictBool(disabledStagecoachResult, "ok", true), "禁用的据点服务不应继续执行。");
            AssertEq(DictString(disabledStagecoachResult, "message", ""), "驿站 当前不可用：暂无已访问路线。", "禁用服务应返回明确 disabled_reason。");
            AssertEq(runtime._active_modal_id, "settlement", "禁用服务失败后不应切换 modal。");

            handler.on_settlement_action_requested("spring_village_01", "service:basic_supply", new GDictionary
            {
                ["interaction_script_id"] = "service_research",
                ["facility_name"] = "伪造图书馆",
                ["npc_name"] = "伪造导师",
                ["service_type"] = "研究",
            });
            GDictionary signalShopWindowData = handler.get_shop_window_data();
            AssertEq(runtime._active_modal_id, "shop", "UI 信号入口收到伪造 interaction_script_id 时仍应按真实商店入口打开 shop modal。");
            AssertEq(DictString(signalShopWindowData, "interaction_script_id", ""), "service_basic_supply", "UI 信号入口应使用真实服务 interaction_script_id。");
            AssertEq(runtime._current_status_message, "已打开 补给铺 的商店。", "UI 信号入口应使用真实服务 facility_name。");
            runtime._active_modal_id = "settlement";

            GDictionary spoofedShopResult = handler.command_execute_settlement_action("service:basic_supply", new GDictionary
            {
                ["interaction_script_id"] = "service_research",
                ["facility_name"] = "伪造图书馆",
                ["npc_name"] = "伪造导师",
                ["service_type"] = "研究",
            });
            AssertTrue(DictBool(spoofedShopResult, "ok", false), "合法 action_id 仍应按真实服务入口执行。");
            AssertEq(runtime._active_modal_id, "shop", "伪造 interaction_script_id 时仍应按真实商店入口打开 shop modal。");
            AssertTrue(handler.get_shop_window_data().Count > 0, "按真实商店入口执行后应能读取 shop window data。");
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestWorldGenerationExposesResearchService()
    {
        GameSession gameSession = await InstallGameSession("ResearchRouteGameSession");
        try
        {
            int createError = gameSession.create_new_save(TestConfigPath, "research_route_service", "研究入口验证");
            AssertEq(createError, (int)Error.Ok, "创建 research 入口验证世界应成功。");
            if (createError == (int)Error.Ok)
            {
                GDictionary foundResearchService = new();
                bool foundLegacyUnlockArchive = false;
                foreach (GDictionary settlement in Dictionaries(DictArray(gameSession.get_world_data(), "settlements")))
                {
                    foreach (GDictionary serviceData in Dictionaries(DictArray(settlement, "available_services")))
                    {
                        string interactionScriptId = DictString(serviceData, "interaction_script_id", "");
                        if (interactionScriptId == "service_research" && foundResearchService.Count == 0)
                        {
                            foundResearchService = (GDictionary)serviceData.Duplicate(true);
                        }
                        if (interactionScriptId == "service_unlock_archive")
                        {
                            foundLegacyUnlockArchive = true;
                        }
                    }
                }
                AssertTrue(foundResearchService.Count > 0, "正式 world config 生成结果应包含 research 服务入口。");
                AssertEq(DictString(foundResearchService, "action_id", ""), "service:research", "research 服务应映射到正式 action_id。");
                AssertFalse(foundLegacyUnlockArchive, "research 服务不应继续使用 legacy service_unlock_archive 入口。");
            }
        }
        finally
        {
            await DisposeGameSession(gameSession, "清理 research 入口验证存档应成功。");
        }
    }

    private async Task<RuntimeFixture> BuildRuntimeFixture(
        string suffix,
        PartyState partyState,
        IReadOnlyList<GDictionary> settlements,
        GDictionary questDefs)
    {
        GameSession gameSession = await InstallGameSession($"SettlementHandlerGameSession_{suffix}");
        GDictionary worldData = BuildWorldData(settlements);
        ConfigureSessionForRuntimeTest(gameSession, $"settlement_handler_{suffix}", worldData, partyState, questDefs ?? new GDictionary());
        GDictionary itemDefs = gameSession.get_item_defs();

        var runtime = new GameRuntimeFacade
        {
            _game_session = gameSession,
            _party_state = partyState,
            _player_coord = Vector2I.Zero,
            _selected_coord = Vector2I.Zero,
            _active_settlement_id = DictString(settlements[0], "settlement_id", ""),
            _active_modal_id = "settlement",
            _player_faction_id = "player",
        };
        runtime._world_map_data_context.bind_root_world_data(worldData);
        runtime._world_map_data_context.active_world_data = worldData;
        foreach (GDictionary settlement in settlements)
        {
            runtime._world_map_data_context.settlements_by_id[DictString(settlement, "settlement_id", "")] = settlement;
        }
        runtime._fog_system.setup(new Vector2I(8, 8));
        MakeVisible(runtime, Vector2I.Zero);
        runtime._character_management.setup(
            partyState,
            gameSession.get_skill_defs(),
            gameSession.get_profession_defs(),
            gameSession.get_achievement_defs(),
            itemDefs,
            gameSession.get_quest_defs(),
            gameSession.allocate_equipment_instance_id,
            gameSession.get_progression_content_bundle()
        );
        runtime._party_warehouse_service.setup(partyState, itemDefs, gameSession.allocate_equipment_instance_id);
        runtime._party_item_use_service.setup(partyState, itemDefs, gameSession.get_skill_defs(), runtime._party_warehouse_service, runtime._character_management);
        runtime._party_equipment_service.setup(partyState, itemDefs, runtime._party_warehouse_service, gameSession.allocate_equipment_instance_id);
        runtime._settlement_command_handler.setup(runtime);
        runtime._warehouse_handler.setup(runtime);
        runtime._quest_command_handler.setup(runtime);
        runtime._reward_flow_handler.setup(runtime);

        return new RuntimeFixture(runtime, gameSession, runtime._settlement_command_handler, runtime._party_warehouse_service);
    }

    private static void ConfigureSessionForRuntimeTest(GameSession gameSession, string saveId, GDictionary worldData, PartyState partyState, GDictionary questDefs)
    {
        int now = (int)Time.GetUnixTimeFromSystem();
        gameSession._active_save_id = saveId;
        gameSession._active_save_path = gameSession._build_save_file_path(saveId);
        gameSession._generation_config_path = TestConfigPath;
        gameSession._generation_config = ResourceLoader.Load<WorldMapGenerationConfig>(TestConfigPath);
        gameSession._world_data = worldData;
        gameSession._player_coord = Vector2I.Zero;
        gameSession._player_faction_id = "player";
        gameSession._party_state = partyState;
        gameSession._quest_defs = questDefs ?? new GDictionary();
        gameSession._has_active_world = true;
        gameSession._battle_save_lock_enabled = false;
        gameSession._active_save_meta = gameSession._build_save_meta(saveId, saveId, TestConfigPath, "settlement_handler_test", "Settlement Handler Test", new Vector2I(8, 8), now, now);
        gameSession.discard_pending_save();
    }

    private async Task<GameSession> InstallGameSession(string nodeName)
    {
        foreach (Node child in Root.GetChildren())
        {
            if (child.Name == nodeName)
            {
                child.QueueFree();
            }
        }
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        var gameSession = new GameSession { Name = nodeName };
        Root.AddChild(gameSession);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        return gameSession;
    }

    private async Task DisposeFixture(RuntimeFixture fixture)
    {
        fixture.Runtime?.dispose();
        await DisposeGameSession(fixture.GameSession, "清理 settlement handler 验证存档应成功。");
    }

    private async Task DisposeGameSession(GameSession gameSession, string clearMessage)
    {
        if (gameSession == null)
        {
            return;
        }
        int clearError = gameSession.clear_persisted_game();
        AssertEq(clearError, (int)Error.Ok, clearMessage);
        gameSession.QueueFree();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private static GDictionary BuildWorldData(IReadOnlyList<GDictionary> settlements)
    {
        var settlementArray = new GArray();
        foreach (GDictionary settlement in settlements)
        {
            settlementArray.Add(settlement);
        }
        return new GDictionary
        {
            ["map_seed"] = 1,
            ["world_step"] = 0,
            ["next_equipment_instance_serial"] = 1,
            ["active_submap_id"] = "",
            ["submap_return_stack"] = new GArray(),
            ["settlements"] = settlementArray,
            ["world_events"] = new GArray(),
            ["encounter_anchors"] = new GArray(),
            ["mounted_submaps"] = new GDictionary(),
            ["world_npcs"] = new GArray(),
            ["player_start_coord"] = Vector2I.Zero,
            ["player_start_settlement_id"] = DictString(settlements[0], "settlement_id", ""),
            ["player_start_settlement_name"] = DictString(settlements[0], "display_name", ""),
            ["fog_states"] = new GDictionary(),
        };
    }

    private static GDictionary BuildSettlementRecord(string settlementId, string displayName, Vector2I origin, GArray services)
    {
        return MinimalSettlementRecord(settlementId, displayName, origin, services);
    }

    private static GDictionary MinimalSettlementRecord(string settlementId, string displayName, Vector2I origin, GArray services)
    {
        return new GDictionary
        {
            ["entity_id"] = $"settlement_{settlementId}",
            ["template_id"] = $"template_{settlementId}",
            ["settlement_id"] = settlementId,
            ["display_name"] = displayName,
            ["tier"] = 1,
            ["tier_name"] = "村镇",
            ["faction_id"] = "neutral",
            ["origin"] = origin,
            ["footprint_size"] = Vector2I.One,
            ["facilities"] = new GArray(),
            ["service_npcs"] = new GArray(),
            ["available_services"] = services,
            ["is_player_start"] = origin == Vector2I.Zero,
            ["settlement_state"] = BuildSettlementState(),
        };
    }

    private static GDictionary BuildSettlementState()
    {
        return new GDictionary
        {
            ["visited"] = true,
            ["reputation"] = 0,
            ["active_conditions"] = new GArray(),
            ["cooldowns"] = new GDictionary(),
            ["shop_inventory_seed"] = 0,
            ["shop_last_refresh_step"] = 0,
            ["shop_states"] = new GDictionary(),
        };
    }

    private static GArray BuildResearchServices()
    {
        return new GArray
        {
            new GDictionary
            {
                ["action_id"] = "service:research",
                ["facility_name"] = "大图书馆",
                ["npc_name"] = "大图书官",
                ["service_type"] = "研究",
                ["interaction_script_id"] = "service_research",
            },
        };
    }

    private static GArray BuildBasicSettlementServices(bool includeStagecoach)
    {
        var services = new GArray
        {
            new GDictionary { ["action_id"] = "service:warehouse", ["facility_name"] = "据点服务台", ["npc_name"] = "军需官", ["service_type"] = "仓储", ["interaction_script_id"] = "party_warehouse" },
            new GDictionary { ["action_id"] = "service:training", ["facility_name"] = "训练场", ["npc_name"] = "教官", ["service_type"] = "训练", ["interaction_script_id"] = "training_service" },
            new GDictionary { ["action_id"] = "service:rest_full", ["facility_name"] = "旅店", ["npc_name"] = "店主", ["service_type"] = "整备", ["interaction_script_id"] = "service_rest_full" },
            new GDictionary { ["action_id"] = "service:contract_board", ["facility_name"] = "公告台", ["npc_name"] = "记录员", ["service_type"] = "契约板", ["interaction_script_id"] = "service_contract_board" },
            new GDictionary { ["action_id"] = "service:bounty_registry", ["facility_name"] = "悬赏署", ["npc_name"] = "悬赏文书", ["service_type"] = "悬赏", ["interaction_script_id"] = "service_bounty_registry" },
        };
        if (includeStagecoach)
        {
            services.Add(new GDictionary { ["action_id"] = "service:stagecoach", ["facility_name"] = "驿站", ["npc_name"] = "驿夫", ["service_type"] = "驿站", ["interaction_script_id"] = "service_stagecoach" });
        }
        return services;
    }

    private static GArray BuildShopAndStagecoachServices()
    {
        return new GArray
        {
            new GDictionary { ["action_id"] = "service:basic_supply", ["facility_name"] = "补给铺", ["npc_name"] = "行商", ["service_type"] = "补给", ["interaction_script_id"] = "service_basic_supply" },
            new GDictionary { ["action_id"] = "service:stagecoach", ["facility_name"] = "驿站", ["npc_name"] = "驿夫", ["service_type"] = "驿站", ["interaction_script_id"] = "service_stagecoach" },
        };
    }

    private static GDictionary BuildQuestDefs()
    {
        var questDefs = new GDictionary();
        AddQuestDef(questDefs, BuildQuestDef("contract_first_hunt", "首轮狩猎", "击败任意一组敌对遭遇。", "service_contract_board", new GArray { BuildObjective("defeat_enemy_once", "defeat_enemy", "", 1) }, new GArray { BuildGoldReward(80) }));
        AddQuestDef(questDefs, BuildQuestDef("contract_manual_drill", "训练记录", "在训练场完成两次记录。", "service_contract_board", new GArray { BuildObjective("train_once", "settlement_action", "service:training", 2) }, new GArray { BuildGoldReward(30) }));
        AddQuestDef(questDefs, BuildQuestDef("contract_repeatable_patrol", "巡路值守", "完成一次例行巡路，随后可再次接取。", "service_contract_board", new GArray { BuildObjective("warehouse_visit", "settlement_action", "service:warehouse", 1) }, new GArray { BuildGoldReward(15) }, true));
        AddQuestDef(questDefs, BuildQuestDef("contract_warehouse_visit", "仓储访问追踪", "据点仓储动作进度测试。", "service_warehouse_hidden", new GArray { BuildObjective("warehouse_visit", "settlement_action", "service:warehouse", 1) }, new GArray { BuildGoldReward(1) }));
        AddQuestDef(questDefs, BuildQuestDef("contract_regional_bounty", "地区悬赏", "仅应出现在悬赏署任务板。", "service_bounty_registry", new GArray { BuildObjective("submit_report", "settlement_action", "service:report_bounty", 1) }, new GArray { BuildGoldReward(120) }));
        AddQuest(questDefs, "contract_missing_display_name", new GDictionary { ["quest_id"] = "contract_missing_display_name", ["description"] = "缺少 display_name 的坏契约不应显示。", ["provider_interaction_id"] = "service_contract_board", ["objective_defs"] = new GArray { BuildObjective("bad_missing_name", "defeat_enemy", "", 1) }, ["reward_entries"] = new GArray { BuildGoldReward(1) } });
        AddQuest(questDefs, "contract_missing_description", new GDictionary { ["quest_id"] = "contract_missing_description", ["display_name"] = "缺说明契约", ["provider_interaction_id"] = "service_contract_board", ["objective_defs"] = new GArray { BuildObjective("bad_missing_description", "defeat_enemy", "", 1) }, ["reward_entries"] = new GArray { BuildGoldReward(1) } });
        AddQuest(questDefs, "contract_missing_objectives", new GDictionary { ["quest_id"] = "contract_missing_objectives", ["display_name"] = "缺目标契约", ["description"] = "缺少 objective_defs 的坏契约不应显示。", ["provider_interaction_id"] = "service_contract_board", ["reward_entries"] = new GArray { BuildGoldReward(1) } });
        AddQuest(questDefs, "contract_missing_objective_target", BuildQuest("contract_missing_objective_target", "缺目标对象契约", "据点事务目标缺少 target_id 时不应回退成未命名。", "service_contract_board", new GArray { BuildObjective("bad_missing_target", "settlement_action", "", 1) }, new GArray { BuildGoldReward(1) }));
        AddQuest(questDefs, "contract_unknown_objective_type", BuildQuest("contract_unknown_objective_type", "未知目标契约", "未知 objective_type 不应直接显示 objective_id。", "service_contract_board", new GArray { BuildObjective("bad_unknown_objective", "legacy_custom", "legacy_target", 1) }, new GArray { BuildGoldReward(1) }));
        AddQuest(questDefs, "contract_missing_rewards", new GDictionary { ["quest_id"] = "contract_missing_rewards", ["display_name"] = "缺奖励契约", ["description"] = "缺少 reward_entries 的坏契约不应显示。", ["provider_interaction_id"] = "service_contract_board", ["objective_defs"] = new GArray { BuildObjective("bad_missing_rewards", "defeat_enemy", "", 1) } });
        AddQuest(questDefs, "contract_invalid_reward_amount", BuildQuest("contract_invalid_reward_amount", "坏奖励契约", "非法奖励数值不应回退成奖励待定。", "service_contract_board", new GArray { BuildObjective("bad_reward_amount", "defeat_enemy", "", 1) }, new GArray { BuildGoldReward(0) }));
        questDefs["contract_string_key_only"] = BuildQuest("contract_string_key_only", "旧 String key 契约", "用于验证任务板不再按 String key 恢复契约。", "service_contract_board", new GArray { BuildObjective("string_key_objective", "settlement_action", "service:training", 1) }, new GArray { BuildGoldReward(1) });
        AddQuestDef(questDefs, BuildQuestDef("contract_supply_drop", "物资缴纳", "向任务板提交两份铁矿石。", "service_contract_board", new GArray { BuildObjective("deliver_ore", "submit_item", "iron_ore", 2) }, new GArray { BuildGoldReward(18) }));
        AddQuestDef(questDefs, BuildQuestDef("contract_training", "训练追踪", "据点训练进度测试。", "service_training_hidden", new GArray { BuildObjective("train_once", "settlement_action", "service:training", 1) }, new GArray { BuildGoldReward(1) }));
        return questDefs;
    }

    private static void AddQuest(GDictionary questDefs, string questId, GDictionary questData)
    {
        questDefs[new StringName(questId)] = questData;
    }

    private static void AddQuestDef(GDictionary questDefs, QuestDef questDef)
    {
        questDefs[questDef.quest_id] = questDef;
    }

    private static GDictionary BuildQuest(string questId, string displayName, string description, string providerInteractionId, GArray objectiveDefs, GArray rewardEntries, bool isRepeatable = false)
    {
        var quest = new GDictionary
        {
            ["quest_id"] = questId,
            ["display_name"] = displayName,
            ["description"] = description,
            ["provider_interaction_id"] = providerInteractionId,
            ["objective_defs"] = objectiveDefs,
            ["reward_entries"] = rewardEntries,
        };
        if (isRepeatable)
        {
            quest["is_repeatable"] = true;
        }
        return quest;
    }

    private static QuestDef BuildQuestDef(string questId, string displayName, string description, string providerInteractionId, GArray objectiveDefs, GArray rewardEntries, bool isRepeatable = false)
    {
        var quest = new QuestDef
        {
            quest_id = questId,
            display_name = displayName,
            description = description,
            provider_interaction_id = providerInteractionId,
            is_repeatable = isRepeatable,
        };
        foreach (GDictionary objective in Dictionaries(objectiveDefs))
        {
            quest.objective_defs.Add((GDictionary)objective.Duplicate(true));
        }
        foreach (GDictionary reward in Dictionaries(rewardEntries))
        {
            quest.reward_entries.Add((GDictionary)reward.Duplicate(true));
        }
        return quest;
    }

    private static GDictionary BuildObjective(string objectiveId, string objectiveType, string targetId, int targetValue)
    {
        return new GDictionary { ["objective_id"] = objectiveId, ["objective_type"] = objectiveType, ["target_id"] = targetId, ["target_value"] = targetValue };
    }

    private static GDictionary BuildGoldReward(int amount)
    {
        return new GDictionary { ["reward_type"] = "gold", ["amount"] = amount };
    }

    private static GDictionary BuildTrainingRewardPayload()
    {
        return new GDictionary
        {
            ["member_id"] = "hero",
            ["source_type"] = "training",
            ["source_id"] = "training",
            ["source_label"] = "训练",
            ["entries"] = new GArray
            {
                new GDictionary { ["entry_type"] = "skill_mastery", ["target_id"] = "warrior_heavy_strike", ["amount"] = 1 },
            },
        };
    }

    private static PartyState BuildPartyState(int storageSpace, int gold)
    {
        var partyState = new PartyState
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
            gold = gold,
        };
        var hero = new PartyMemberState
        {
            member_id = "hero",
            display_name = "Hero",
            current_hp = 20,
            current_mp = 4,
        };
        var progression = new UnitProgress
        {
            unit_id = "hero",
            display_name = "Hero",
            unit_base_attributes = new UnitBaseAttributes(),
        };
        progression.unit_base_attributes.custom_stats["storage_space"] = storageSpace;
        progression.unit_base_attributes.custom_stats["hp_max"] = 40;
        progression.unit_base_attributes.custom_stats["mp_max"] = 12;
        hero.progression = progression;
        partyState.set_member_state(hero);
        return partyState;
    }

    private static GDictionary BuildShopState(GArray currentInventory)
    {
        return new GDictionary
        {
            ["visited"] = true,
            ["reputation"] = 0,
            ["active_conditions"] = new GArray(),
            ["cooldowns"] = new GDictionary(),
            ["world_step"] = 0,
            ["shop_inventory_seed"] = 0,
            ["shop_last_refresh_step"] = 0,
            ["shop_states"] = new GDictionary
            {
                ["village_basic_supply"] = new GDictionary
                {
                    ["shop_id"] = "village_basic_supply",
                    ["current_inventory"] = currentInventory,
                    ["seed"] = 1,
                    ["last_refresh_step"] = 0,
                },
            },
        };
    }

    private static ItemDef MakeShopItemDef(string displayName, string description, int buyPrice, int sellPrice, bool sellable)
    {
        return new ItemDef
        {
            display_name = displayName,
            description = description,
            icon = "",
            buy_price = buyPrice,
            sell_price = sellPrice,
            sellable = sellable,
        };
    }

    private static void MakeVisible(GameRuntimeFacade runtime, Vector2I center)
    {
        runtime._fog_system.RebuildVisibilityForFaction(
            "player",
            new[] { new VisionSourceData("settlement_handler_visibility", center, 6, "player") }
        );
    }

    private static GDictionary FindServiceEntry(GArray serviceOptions, string actionId)
    {
        foreach (GDictionary serviceData in Dictionaries(serviceOptions))
        {
            if (DictString(serviceData, "action_id", "") == actionId)
            {
                return (GDictionary)serviceData.Duplicate(true);
            }
        }
        return new GDictionary();
    }

    private static List<string> ExtractContractBoardEntryIds(GArray entryOptions)
    {
        var result = new List<string>();
        foreach (GDictionary entry in Dictionaries(entryOptions))
        {
            result.Add(DictString(entry, "quest_id", ""));
        }
        return result;
    }

    private static GDictionary FindContractBoardEntry(GArray entryOptions, string questId)
    {
        foreach (GDictionary entry in Dictionaries(entryOptions))
        {
            if (DictString(entry, "quest_id", "") == questId)
            {
                return (GDictionary)entry.Duplicate(true);
            }
        }
        return new GDictionary();
    }

    private static GDictionary GetFirstRewardEntry(GDictionary rewardData)
    {
        GArray entries = DictArray(rewardData, "entries");
        return entries.Count > 0 && entries[0].VariantType == Variant.Type.Dictionary
            ? entries[0].AsGodotDictionary()
            : new GDictionary();
    }

    private static GDictionary FindPendingRewardBySourceId(PartyState partyState, string sourceId)
    {
        if (partyState == null)
        {
            return new GDictionary();
        }
        foreach (PendingCharacterReward reward in partyState.pending_character_rewards)
        {
            GDictionary rewardData = reward?.to_dict() ?? new GDictionary();
            if (DictString(rewardData, "source_id", "") == sourceId)
            {
                return rewardData;
            }
        }
        return new GDictionary();
    }

    private static int CountPendingRewardsBySourceId(PartyState partyState, string sourceId)
    {
        int count = 0;
        if (partyState == null)
        {
            return count;
        }
        foreach (PendingCharacterReward reward in partyState.pending_character_rewards)
        {
            GDictionary rewardData = reward?.to_dict() ?? new GDictionary();
            if (DictString(rewardData, "source_id", "") == sourceId)
            {
                count++;
            }
        }
        return count;
    }

    private static IEnumerable<GDictionary> Dictionaries(GArray values)
    {
        if (values == null)
        {
            yield break;
        }
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Dictionary)
            {
                yield return value.AsGodotDictionary();
            }
        }
    }

    private static GArray DictArray(GDictionary dictionary, string key)
    {
        return dictionary != null
            && dictionary.ContainsKey(key)
            && dictionary[key].VariantType == Variant.Type.Array
                ? dictionary[key].AsGodotArray()
                : new GArray();
    }

    private static GDictionary DictDictionary(GDictionary dictionary, string key)
    {
        return dictionary != null
            && dictionary.ContainsKey(key)
            && dictionary[key].VariantType == Variant.Type.Dictionary
                ? dictionary[key].AsGodotDictionary()
                : new GDictionary();
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsBool()
            : fallback;
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsInt32()
            : fallback;
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : fallback;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }

    private void AssertSequence(IReadOnlyList<string> actual, IReadOnlyList<string> expected, string message)
    {
        if (actual.Count != expected.Count)
        {
            _failures.Add($"{message} | actual=[{string.Join(", ", actual)}] expected=[{string.Join(", ", expected)}]");
            return;
        }
        for (int i = 0; i < actual.Count; i++)
        {
            if (actual[i] != expected[i])
            {
                _failures.Add($"{message} | actual=[{string.Join(", ", actual)}] expected=[{string.Join(", ", expected)}]");
                return;
            }
        }
    }

    private sealed class RuntimeFixture
    {
        public RuntimeFixture(GameRuntimeFacade runtime, GameSession gameSession, GameRuntimeSettlementCommandHandler handler, PartyWarehouseService warehouseService)
        {
            Runtime = runtime;
            GameSession = gameSession;
            Handler = handler;
            WarehouseService = warehouseService;
        }

        public GameRuntimeFacade Runtime { get; }
        public GameSession GameSession { get; }
        public GameRuntimeSettlementCommandHandler Handler { get; }
        public PartyWarehouseService WarehouseService { get; }
    }
}
