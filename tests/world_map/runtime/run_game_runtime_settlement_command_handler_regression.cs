using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_game_runtime_settlement_command_handler_regression : LifecycleTestSceneTree
{
    private const string TestConfigPath = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(RunAsync));
    }

    private async void RunAsync()
    {
        await TestFacadeUsesSettlementHandlerSurface();
        await TestSettlementHandlerRoutesResearchService();
        await TestSettlementHandlerRoutesActionsAndModalState();
        await TestContractBoardEvaluatorAndFeedback();
        await TestSettlementHandlerRejectsStringNameSubmissionFields();
        TestSettlementShopServiceRejectsBadEntrySchema();
        await TestSettlementHandlerRejectsInvalidOrSpoofedActions();
        await TestWorldGenerationExposesResearchService();

        RequestTestExit(_test.Finish("Game runtime settlement command handler regression"));
    }

    private async Task TestFacadeUsesSettlementHandlerSurface()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "facade",
            BuildPartyState(12, 100),
            new[] { BuildSettlementRecord("spring_village_01", "春泉村", Vector2I.Zero, BuildBasicSettlementServices(false)) },
            new Dictionary<StringName, QuestDefinition>()
        );
        try
        {
            GDictionary windowData = fixture.Runtime.GetSettlementWindowData("spring_village_01");
            _test.Eq(DictString(windowData, "settlement_id", ""), "spring_village_01", "get_settlement_window_data() 应委托到正式 settlement handler。");

            GameRuntimeFacade.RuntimeCommandResult commandResult =
                fixture.Runtime.CommandExecuteSettlementActionTyped(
                    "service:warehouse",
                    new GDictionary()
                );
            _test.True(commandResult.Ok, $"command_ExecuteSettlementAction() 应委托到正式 settlement handler。message={commandResult.Message}");
            _test.Eq(fixture.Runtime._active_modal_kind, RuntimeModalKind.Warehouse, "据点仓储动作应通过 handler 打开共享仓库。");

            fixture.Runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Settlement);
            fixture.Runtime.SetActiveSettlementId("spring_village_01");
            _test.Eq(fixture.Runtime.GetResolvedSettlementId(), "spring_village_01", "GetResolvedSettlementId() 应委托到正式 settlement handler。");
            fixture.Runtime._party_state.pending_character_rewards.Clear();
            fixture.Runtime._character_management.SetPartyState(fixture.Runtime._party_state);
            fixture.Runtime.OnSettlementWindowClosed();
            _test.Eq(fixture.Runtime._active_modal_kind, RuntimeModalKind.None, "OnSettlementWindowClosed() 应委托到正式 settlement handler。");
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
            new Dictionary<StringName, QuestDefinition>()
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            GDictionary settlementWindowData = handler.GetSettlementWindowData("graystone_town_01");
            GDictionary researchService = FindServiceEntry(DictArray(settlementWindowData, "available_services"), "service:research");
            _test.True(researchService.Count > 0, "据点窗口应暴露正式 research 服务入口。");
            _test.Eq(DictString(researchService, "interaction_script_id", ""), "service_research", "research 服务应使用正式 interaction_script_id。");
            _test.True(DictBool(researchService, "is_enabled", false), "金币充足时 research 服务入口应可点击。");
            _test.Eq(DictString(researchService, "cost_label", ""), "200 金", "research 服务应暴露正式金币成本。");
            GDictionary memberAvailability = DictDictionary(researchService, "member_availability");
            GDictionary heroAvailability = DictDictionary(memberAvailability, "hero");
            _test.True(
                DictBool(heroAvailability, "has_available_research", false),
                "research 服务 metadata projection 应暴露成员可研究状态。"
            );

            GameRuntimeFacade.RuntimeCommandResult researchResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:research",
                    new GDictionary()
                );
            _test.True(
                researchResult.Ok,
                $"research 服务应能走正式 settlement action dispatch。message={researchResult.Message}"
            );
            _test.Eq(runtime._party_state.GetGold(), 50, "research 服务成功后应扣除正式研究成本。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Settlement, "research 服务不应切走当前 settlement modal。");
            _test.True(!string.IsNullOrEmpty(runtime._active_settlement_feedback_text), "research 服务应写入正式据点反馈。");
            _test.True(!string.IsNullOrEmpty(runtime._current_status_message), "research 服务应刷新状态。");
            _test.False(fixture.GameSession.HasPendingSave(), "research 服务成功后应提交队伍状态持久化。");
            _test.Eq(CountPendingRewardsBySourceId(runtime._party_state, "research_field_manual"), 1, "research 服务成功后应正式排入 research_field_manual 奖励。");
            GDictionary firstResearchReward = FindPendingRewardBySourceId(runtime._party_state, "research_field_manual");
            _test.Eq(DictString(firstResearchReward, "member_id", ""), "hero", "research 奖励应写入目标成员。");
            _test.Eq(DictString(firstResearchReward, "member_name", ""), "Hero", "research 奖励应保留成员显示名。");
            _test.Eq(DictString(firstResearchReward, "source_type", ""), "npc_teach", "research 奖励应沿用正式 source_type 命名。");
            _test.Eq(DictString(firstResearchReward, "source_id", ""), "research_field_manual", "知识型 research 奖励应写入具体来源 ID。");
            _test.Eq(DictString(firstResearchReward, "source_label", ""), "大图书官·研究", "research 奖励应沿用正式 source_label 命名。");
            GDictionary firstRewardEntry = GetFirstRewardEntry(firstResearchReward);
            _test.Eq(DictString(firstRewardEntry, "entry_type", ""), "knowledge_unlock", "首条 research 奖励应先构造成知识奖励。");
            _test.Eq(DictString(firstRewardEntry, "target_id", ""), "field_manual", "首条 research 奖励应指向野外手册知识。");

            GDictionary refreshedWindowData = handler.GetSettlementWindowData("graystone_town_01");
            GDictionary refreshedResearchService = FindServiceEntry(DictArray(refreshedWindowData, "available_services"), "service:research");
            _test.False(DictBool(refreshedResearchService, "is_enabled", true), "扣费后金币不足时 research 服务应及时禁用。");
            _test.Eq(DictString(refreshedResearchService, "disabled_reason", ""), "金币不足", "research 服务禁用原因应明确显示金币不足。");

            runtime._party_state.SetGold(250);
            runtime._character_management.SetPartyState(runtime._party_state);
            GDictionary reenabledWindowData = handler.GetSettlementWindowData("graystone_town_01");
            GDictionary reenabledResearchService = FindServiceEntry(DictArray(reenabledWindowData, "available_services"), "service:research");
            _test.True(DictBool(reenabledResearchService, "is_enabled", false), "首条 research 奖励尚未确认时，也应切到下一条可研究内容，而不是重复给野外手册。");

            GameRuntimeFacade.RuntimeCommandResult secondResearchResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:research",
                    new GDictionary()
                );
            _test.True(
                secondResearchResult.Ok,
                $"第二次 research 服务应继续走正式 settlement action dispatch。message={secondResearchResult.Message}"
            );
            _test.Eq(CountPendingRewardsBySourceId(runtime._party_state, "research_guard_break"), 1, "第二次 research 服务应继续把 research_guard_break 奖励同步写回 party_state。");
            GDictionary secondResearchReward = FindPendingRewardBySourceId(runtime._party_state, "research_guard_break");
            _test.Eq(DictString(secondResearchReward, "source_type", ""), "npc_teach", "技能型 research 奖励也应沿用正式 source_type 命名。");
            _test.Eq(DictString(secondResearchReward, "source_id", ""), "research_guard_break", "技能型 research 奖励应写入具体来源 ID。");
            _test.Eq(DictString(secondResearchReward, "source_label", ""), "大图书官·研究", "技能型 research 奖励应保留统一来源标签。");
            GDictionary secondRewardEntry = GetFirstRewardEntry(secondResearchReward);
            _test.Eq(DictString(secondRewardEntry, "entry_type", ""), "skill_unlock", "第二条 research 奖励应构造成技能奖励。");
            _test.Eq(DictString(secondRewardEntry, "target_id", ""), "warrior_guard_break", "第二条 research 奖励应指向裂甲斩技能。");

            runtime._party_state.SetGold(250);
            runtime._character_management.SetPartyState(runtime._party_state);
            GDictionary exhaustedWindowData = handler.GetSettlementWindowData("graystone_town_01");
            GDictionary exhaustedResearchService = FindServiceEntry(DictArray(exhaustedWindowData, "available_services"), "service:research");
            _test.False(DictBool(exhaustedResearchService, "is_enabled", true), "同成员两条 research 奖励都已挂入 pending 队列后，服务应禁用。");
            _test.Eq(DictString(exhaustedResearchService, "disabled_reason", ""), "暂无可研究内容", "research 已被 pending 队列占满时应给出明确禁用原因。");
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestSettlementHandlerRoutesActionsAndModalState()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildQuestDefs();
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

            GDictionary settlementWindowData = handler.GetSettlementWindowData("spring_village_01");
            GDictionary contractService = FindServiceEntry(DictArray(settlementWindowData, "available_services"), "service:contract_board");
            GDictionary bountyService = FindServiceEntry(DictArray(settlementWindowData, "available_services"), "service:bounty_registry");
            _test.True(QuestProviderContentRules.IsSupportedProviderId("service_contract_board"), "任务板 provider 应来自共享 quest provider 白名单。");
            _test.True(QuestProviderContentRules.IsSupportedProviderId("service_bounty_registry"), "悬赏署 provider 应来自共享 quest provider 白名单。");
            _test.True(contractService.Count > 0, "据点窗口应暴露任务板服务入口。");
            _test.True(DictBool(contractService, "is_enabled", false), "任务板服务入口应为可点击状态。");
            _test.True(bountyService.Count > 0, "据点窗口应暴露悬赏署服务入口。");
            _test.True(DictBool(bountyService, "is_enabled", false), "悬赏署服务入口应为可点击状态。");

            var warehouseQuest = new QuestState { quest_id = "contract_warehouse_visit" };
            warehouseQuest.MarkAccepted(runtime.GetWorldStep());
            runtime._party_state.SetActiveQuestState(warehouseQuest);
            runtime._character_management.SetPartyState(runtime._party_state);
            GameRuntimeFacade.RuntimeCommandResult warehouseResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:warehouse",
                    new GDictionary()
                );
            _test.True(warehouseResult.Ok, "据点仓储动作应执行成功。");
            _test.Eq(runtime._active_settlement_id, "spring_village_01", "仓储动作后应记录当前据点 ID。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Warehouse, "仓储动作后应打开共享仓库 modal。");
            _test.True(!string.IsNullOrEmpty(runtime._active_warehouse_entry_label), "仓储动作后应记录仓库入口标签。");
            _test.True(!string.IsNullOrEmpty(runtime._current_status_message), "仓储动作后应刷新状态。");
            _test.True(runtime._party_state.HasClaimableQuest("contract_warehouse_visit"), "仓储动作应通过 typed SettlementServiceResult 应用默认 quest_progress_events。");
            _test.False(fixture.GameSession.HasPendingSave(), "仓储动作成功后应通过 typed SettlementServiceResult 提交队伍状态持久化。");
            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Settlement);
            runtime.SetActiveSettlementId("spring_village_01");

            GameRuntimeFacade.RuntimeCommandResult contractBoardResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary()
                );
            GDictionary contractBoardWindowData = handler.GetContractBoardWindowData();
            List<string> contractBoardEntryIds = ExtractContractBoardEntryIds(DictArray(contractBoardWindowData, "entries"));
            _test.True(contractBoardResult.Ok, "任务板服务应能切换到 contract_board modal。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.ContractBoard, "任务板服务后应切换到 contract_board modal。");
            _test.Eq(DictString(contractBoardWindowData, "action_id", ""), "service:contract_board", "任务板 modal 应保留原始 action_id。");
            _test.Eq(DictString(contractBoardWindowData, "provider_interaction_id", ""), "service_contract_board", "任务板 modal 应记录当前 provider_interaction_id。");
            AssertSequence(contractBoardEntryIds, new[] { "contract_first_hunt", "contract_manual_drill", "contract_repeatable_patrol", "contract_supply_drop" }, "任务板 modal 只应按 provider_interaction_id 暴露当前服务的契约条目。");
            _test.True(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_missing_display_name").Count == 0, "缺少 display_name 的契约不应回退成 quest_id 出现在任务板。");
            _test.True(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_missing_description").Count == 0, "缺少 description 的契约不应回退成暂无说明出现在任务板。");
            _test.True(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_missing_objectives").Count == 0, "缺少 objective_defs 的契约不应回退成暂无目标说明出现在任务板。");
            _test.True(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_missing_objective_target").Count == 0, "缺少 target_id 的据点事务目标不应回退成未命名出现在任务板。");
            _test.True(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_unknown_objective_type").Count == 0, "未知 objective_type 不应回退成 objective_id 出现在任务板。");
            _test.True(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_missing_rewards").Count == 0, "缺少 reward_entries 的契约不应回退成奖励待定出现在任务板。");
            _test.True(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_invalid_reward_amount").Count == 0, "非法 reward amount 的契约不应回退成奖励待定。");
            _test.True(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_typed_missing_objective_target").Count == 0, "QuestDefinition 中缺少 target_id 的据点事务目标不应出现在任务板。");
            _test.True(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_typed_invalid_reward_amount").Count == 0, "QuestDefinition 中非法 reward amount 的契约不应出现在任务板。");
            _test.True(FindContractBoardEntry(DictArray(contractBoardWindowData, "entries"), "contract_string_key_only").Count == 0, "String key-only 契约不应被任务板恢复。");

            _test.False(
                handler.CommandExecuteSettlementActionRuntimeTyped(
                        "service:contract_board",
                        new GDictionary
                        {
                            ["submission_source"] = "contract_board",
                            ["quest_id"] = "contract_missing_display_name",
                            ["provider_interaction_id"] = "service_contract_board",
                        }
                    )
                    .Ok,
                "缺少 display_name 的坏契约即使被构造提交也应拒绝。"
            );
            _test.False(
                handler.CommandExecuteSettlementActionRuntimeTyped(
                        "service:contract_board",
                        new GDictionary
                        {
                            ["submission_source"] = "contract_board",
                            ["quest_id"] = "contract_typed_missing_objective_target",
                            ["provider_interaction_id"] = "service_contract_board",
                        }
                    )
                    .Ok,
                "QuestDefinition 中缺少 target_id 的坏契约即使被构造提交也应拒绝。"
            );
            _test.False(
                handler.CommandExecuteSettlementActionRuntimeTyped(
                        "service:contract_board",
                        new GDictionary
                        {
                            ["panel_kind"] = "contract_board",
                            ["quest_id"] = "contract_manual_drill",
                            ["provider_interaction_id"] = "service_contract_board",
                        }
                    )
                    .Ok,
                "旧 panel_kind 字段不应再被识别为任务板提交或普通据点动作。"
            );
            GameRuntimeFacade.RuntimeCommandResult legacyEntrySubmission =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary
                    {
                        ["submission_source"] = "contract_board",
                        ["entry_id"] = "contract_manual_drill",
                        ["provider_interaction_id"] = "service_contract_board",
                    }
                );
            _test.False(legacyEntrySubmission.Ok, "旧 entry_id 字段不应回退成 quest_id。");
            _test.Eq(runtime._current_status_message, "当前契约条目缺少 quest_id，无法接取。", "旧 entry_id 提交应返回缺 quest_id 的反馈。");
            GameRuntimeFacade.RuntimeCommandResult legacyProviderSubmission =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary
                    {
                        ["submission_source"] = "contract_board",
                        ["quest_id"] = "contract_manual_drill",
                        ["interaction_script_id"] = "service_contract_board",
                    }
                );
            _test.False(
                legacyProviderSubmission.Ok,
                "旧 interaction_script_id 字段不应回退成 provider_interaction_id。"
            );
            _test.Eq(runtime._current_status_message, "当前契约条目缺少 provider_interaction_id，无法匹配任务板。", "旧 interaction_script_id 提交应返回缺 provider_interaction_id 的反馈。");
            GameRuntimeFacade.RuntimeCommandResult stringKeySubmission =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary
                    {
                        ["submission_source"] = "contract_board",
                        ["quest_id"] = "contract_string_key_only",
                        ["provider_interaction_id"] = "service_contract_board",
                    }
                );
            _test.False(stringKeySubmission.Ok, "String key-only 契约即使被构造提交也应拒绝。");
            _test.Eq(runtime._current_status_message, "当前任务板未找到契约 contract_string_key_only。", "String key-only 提交应按未找到任务处理。");
            GameRuntimeFacade.RuntimeCommandResult mismatchedContractSubmission =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:bounty_registry",
                    new GDictionary
                    {
                        ["submission_source"] = "contract_board",
                        ["quest_id"] = "contract_manual_drill",
                        ["provider_interaction_id"] = "service_contract_board",
                    }
                );
            _test.False(
                mismatchedContractSubmission.Ok,
                "任务板提交不应允许切到其他 action_id。"
            );
            _test.Eq(
                mismatchedContractSubmission.Message,
                "当前任务板与请求的服务入口不一致。",
                "任务板 action_id 不匹配时应返回明确反馈。"
            );

            GameRuntimeFacade.RuntimeCommandResult acceptContractResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary
                    {
                        ["submission_source"] = "contract_board",
                        ["quest_id"] = "contract_manual_drill",
                        ["provider_interaction_id"] = "service_contract_board",
                    }
                );
            GDictionary acceptedContractEntry = FindContractBoardEntry(DictArray(handler.GetContractBoardWindowData(), "entries"), "contract_manual_drill");
            _test.True(
                acceptContractResult.Ok,
                $"任务板提交应保持据点动作链路可执行。message={acceptContractResult.Message}"
            );
            _test.True(runtime._party_state.HasActiveQuest("contract_manual_drill"), "任务板接取后应把任务写入 PartyState.active_quests。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.ContractBoard, "接取契约后应继续停留在 contract_board modal。");
            _test.Eq(runtime._current_status_message, "已接取任务《训练记录》。", "任务板接取后应更新成功反馈。");
            _test.Eq(DictString(acceptedContractEntry, "state_id", ""), "active", "接取后的契约条目应刷新为 active。");
            _test.Eq(DictString(handler.GetContractBoardWindowData(), "summary_text", ""), "已接取任务《训练记录》。", "任务板 summary_text 应刷新为最新反馈。");

            handler.CommandExecuteSettlementActionRuntimeTyped(
                "service:contract_board",
                new GDictionary
                {
                    ["submission_source"] = "contract_board",
                    ["quest_id"] = "contract_manual_drill",
                    ["provider_interaction_id"] = "service_contract_board",
                }
            );
            _test.Eq(runtime._current_status_message, "任务《训练记录》已在进行中，不能重复接取。", "重复接取时应返回明确反馈。");

            _test.True(runtime._party_state.MarkQuestCompleted("contract_manual_drill", runtime.GetWorldStep()), "测试前置：普通契约应能标记完成。");
            runtime._character_management.SetPartyState(runtime._party_state);
            int manualClaimGoldBefore = runtime._party_state.GetGold();
            handler.CommandExecuteSettlementActionRuntimeTyped(
                "service:contract_board",
                new GDictionary
                {
                    ["submission_source"] = "contract_board",
                    ["quest_id"] = "contract_manual_drill",
                    ["provider_interaction_id"] = "service_contract_board",
                }
            );
            GDictionary claimedContractEntry = FindContractBoardEntry(DictArray(handler.GetContractBoardWindowData(), "entries"), "contract_manual_drill");
            _test.Eq(runtime._current_status_message, "已领取任务《训练记录》奖励，获得 30 金。", "claimable 契约提交时应返回领奖反馈。");
            _test.Eq(runtime._party_state.GetGold(), manualClaimGoldBefore + 30, "claimable 契约提交后应把金币奖励写入 PartyState。");
            _test.False(runtime._party_state.HasActiveQuest("contract_manual_drill"), "已完成非 repeatable 契约不应重新回到 active_quests。");
            _test.False(runtime._party_state.HasClaimableQuest("contract_manual_drill"), "领奖后的非 repeatable 契约不应继续停留在 claimable_quests。");
            _test.True(runtime._party_state.HasCompletedQuest("contract_manual_drill"), "领奖后的非 repeatable 契约应进入 completed_quest_ids。");
            _test.Eq(DictString(claimedContractEntry, "state_id", ""), "completed", "领奖后的普通契约条目应刷新为 completed。");

            var repeatableQuest = new QuestState { quest_id = "contract_repeatable_patrol" };
            repeatableQuest.MarkAccepted(runtime.GetWorldStep());
            runtime._party_state.SetActiveQuestState(repeatableQuest);
            _test.True(runtime._party_state.MarkQuestCompleted("contract_repeatable_patrol", runtime.GetWorldStep()), "测试前置：repeatable 契约应先进入待领奖励状态。");
            runtime._character_management.SetPartyState(runtime._party_state);
            int repeatableClaimGoldBefore = runtime._party_state.GetGold();
            handler.CommandExecuteSettlementActionRuntimeTyped(
                "service:contract_board",
                new GDictionary
                {
                    ["submission_source"] = "contract_board",
                    ["quest_id"] = "contract_repeatable_patrol",
                    ["provider_interaction_id"] = "service_contract_board",
                }
            );
            GDictionary repeatableEntry = FindContractBoardEntry(DictArray(handler.GetContractBoardWindowData(), "entries"), "contract_repeatable_patrol");
            _test.Eq(runtime._current_status_message, "已领取任务《巡路值守》奖励，获得 15 金。", "repeatable 契约领奖时应返回明确反馈。");
            _test.Eq(runtime._party_state.GetGold(), repeatableClaimGoldBefore + 15, "repeatable 契约领奖后应增加金币。");
            _test.True(runtime._party_state.HasCompletedQuest("contract_repeatable_patrol"), "repeatable 契约领奖后应进入 completed_quest_ids。");
            _test.Eq(DictString(repeatableEntry, "state_id", ""), "repeatable", "repeatable 契约领奖后条目应刷新为 repeatable。");

            fixture.WarehouseService.AddItemTyped("iron_ore", 2);
            var submitItemQuest = new QuestState { quest_id = "contract_supply_drop" };
            submitItemQuest.MarkAccepted(runtime.GetWorldStep());
            runtime._party_state.SetActiveQuestState(submitItemQuest);
            runtime._character_management.SetPartyState(runtime._party_state);
            handler.CommandExecuteSettlementActionRuntimeTyped(
                "service:contract_board",
                new GDictionary
                {
                    ["submission_source"] = "contract_board",
                    ["quest_id"] = "contract_supply_drop",
                    ["provider_interaction_id"] = "service_contract_board",
                }
            );
            GDictionary submitItemEntry = FindContractBoardEntry(DictArray(handler.GetContractBoardWindowData(), "entries"), "contract_supply_drop");
            _test.Eq(runtime._current_status_message, "已为任务《物资缴纳》提交 铁矿石 x2，奖励待领取。", "submit_item 提交后应刷新正式反馈。");
            _test.False(runtime._party_state.HasActiveQuest("contract_supply_drop"), "submit_item 提交完成后任务应离开 active_quests。");
            _test.True(runtime._party_state.HasClaimableQuest("contract_supply_drop"), "submit_item 提交完成后任务应进入 claimable_quests。");
            _test.Eq(DictString(submitItemEntry, "state_id", ""), "claimable", "submit_item 提交后条目应刷新为 claimable。");

            handler.OnContractBoardWindowClosed();
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Settlement, "关闭任务板后应返回 settlement modal。");
            _test.Eq(runtime._active_settlement_id, "spring_village_01", "关闭任务板后应继续保留当前据点。");

            GameRuntimeFacade.RuntimeCommandResult bountyBoardResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:bounty_registry",
                    new GDictionary()
                );
            GDictionary bountyBoardWindowData = handler.GetContractBoardWindowData();
            _test.True(bountyBoardResult.Ok, "悬赏署服务应复用 contract_board modal。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.ContractBoard, "悬赏署服务后仍应落到 contract_board modal。");
            _test.Eq(DictString(bountyBoardWindowData, "action_id", ""), "service:bounty_registry", "悬赏署 modal 应保留原始 action_id。");
            _test.Eq(DictString(bountyBoardWindowData, "provider_interaction_id", ""), "service_bounty_registry", "悬赏署 modal 应记录自己的 provider_interaction_id。");
            AssertSequence(ExtractContractBoardEntryIds(DictArray(bountyBoardWindowData, "entries")), new[] { "contract_regional_bounty" }, "悬赏署 modal 只应暴露自己的 bounty quest。");

            handler.OnContractBoardWindowClosed();
            handler.CommandExecuteSettlementActionRuntimeTyped(
                "service:contract_board",
                new GDictionary()
            );
            AssertSequence(ExtractContractBoardEntryIds(DictArray(handler.GetContractBoardWindowData(), "entries")), new[] { "contract_first_hunt", "contract_manual_drill", "contract_repeatable_patrol", "contract_supply_drop" }, "悬赏署 provider 不应污染正式 contract board 列表。");
            handler.OnContractBoardWindowClosed();

            GameRuntimeFacade.RuntimeCommandResult trainingResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:training",
                    new GDictionary
                    {
                        ["pending_character_rewards"] = new GArray
                        {
                            BuildTrainingRewardPayload()
                        },
                    }
            );
            _test.True(trainingResult.Ok, "普通据点动作应执行成功。");
            _test.True(!string.IsNullOrEmpty(runtime._active_settlement_feedback_text), "普通据点动作后应写入据点反馈。");
            _test.Eq(CountPendingRewardsBySourceId(runtime._party_state, "training"), 0, "普通据点命令入口不应接受 pending_character_rewards 注入。");
            _test.True(!string.IsNullOrEmpty(runtime._current_status_message), "普通据点动作完成后应刷新状态。");

            SettlementServiceResult questTrainingResult = handler.ExecuteSettlementActionTyped("spring_village_01", "service:training", new GDictionary
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
            GDictionary questTrainingPayload = SettlementServiceResultProjection.Project(questTrainingResult);
            var questState = new QuestState { quest_id = "contract_training" };
            questState.MarkAccepted(runtime.GetWorldStep());
            runtime._party_state.SetActiveQuestState(questState);
            runtime._character_management.SetPartyState(runtime._party_state);
            handler.OnSettlementActionRequested("spring_village_01", "service:training", new GDictionary
            {
                ["member_id"] = "hero",
            });
            QuestState trainingQuest = runtime._party_state.GetQuestState("contract_training");
            _test.Eq(DictArray(questTrainingPayload, "quest_progress_events").Count, 3, "据点服务结果应包含显式 quest_progress_events 与默认据点动作事件。");
            _test.True(trainingQuest != null, "据点动作应通过正式默认 settlement_action 事件写入 PartyState。");
            if (trainingQuest != null)
            {
                _test.Eq(trainingQuest.GetObjectiveProgress("train_once"), 1, "据点动作应推进任务目标进度。");
            }
            _test.True(runtime._party_state.HasClaimableQuest("contract_training"), "目标完成后据点动作应把 QuestDefinition 任务推进到 claimable_quests。");
            _test.Eq(questTrainingResult.QuestProgressEvents.Count, 3, "typed service result 应直接暴露 canonical quest_progress_events。");

            SettlementServiceResult canonicalTrainingResult = handler.ExecuteSettlementActionTyped("spring_village_01", "service:training", new GDictionary
            {
                ["interaction_script_id"] = "training_service",
                ["facility_name"] = "训练场",
                ["npc_name"] = "教官",
                ["service_type"] = "训练",
                ["member_id"] = "hero",
                ["pending_character_rewards"] = new GArray { BuildTrainingRewardPayload() },
            });
            GDictionary canonicalTrainingPayload = SettlementServiceResultProjection.Project(canonicalTrainingResult);
            _test.True(canonicalTrainingResult.Success, "据点服务结果应成功。");
            _test.True(canonicalTrainingPayload.ContainsKey("pending_character_rewards"), "据点服务结果应包含 canonical pending_character_rewards。");
            _test.True(canonicalTrainingPayload.ContainsKey("service_side_effects"), "据点服务结果应包含 service_side_effects。");
            _test.Eq(DictArray(canonicalTrainingPayload, "pending_character_rewards").Count, 1, "据点服务结果应输出 canonical 奖励数组。");
            _test.Eq(canonicalTrainingResult.PendingCharacterRewards.Count, 1, "typed service result 应直接暴露 canonical pending_character_rewards。");
            _test.False(canonicalTrainingPayload.ContainsKey("pending_mastery_rewards"), "据点服务结果不应再输出 legacy pending_mastery_rewards。");
            _test.False(canonicalTrainingPayload.ContainsKey("effects"), "据点服务结果不应再输出 legacy effects。");
            _test.Eq(canonicalTrainingResult.GoldDelta, 0, "普通据点服务不应修改金币字段。");

            GDictionary legacyRewardSourceResult = SettlementServiceResultProjection.Project(
                handler.ExecuteSettlementActionTyped("spring_village_01", "service:training", new GDictionary
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
                })
            );
            GArray legacyRewardEntries = DictArray(legacyRewardSourceResult, "pending_character_rewards");
            GDictionary legacyReward = legacyRewardEntries.Count > 0 && legacyRewardEntries[0].VariantType == Variant.Type.Dictionary
                ? legacyRewardEntries[0].AsGodotDictionary()
                : new GDictionary();
            _test.Eq(DictString(legacyReward, "source_type", ""), "training", "旧 mastery_source_type 不应回退成奖励 source_type。");
            _test.Eq(DictString(legacyReward, "source_id", ""), "training", "旧 mastery_source_type 不应回退成奖励 source_id。");

            runtime._party_state.SetGold(200);
            runtime._party_state.GetMemberState("hero").current_hp = 10;
            runtime._character_management.SetPartyState(runtime._party_state);
            GDictionary restResult = SettlementServiceResultProjection.Project(
                handler.ExecuteSettlementActionTyped("spring_village_01", "service:rest_full", new GDictionary
                {
                    ["interaction_script_id"] = "service_rest_full",
                    ["facility_name"] = "旅店",
                    ["npc_name"] = "店主",
                    ["service_type"] = "整备",
                    ["member_id"] = "hero",
                })
            );
            _test.True(DictBool(restResult, "success", false), "整备服务应执行成功。");
            _test.Eq(runtime._party_state.gold, 150, "整备服务应扣除 50 金。");
            _test.Eq(runtime.GetWorldStep(), 1, "整备服务应推进 1 点 world_step。");
            _test.Eq(runtime._party_state.GetMemberState("hero").current_hp, 40, "整备服务应把当前生命恢复到上限。");
            _test.Eq(DictInt(restResult, "gold_delta", 0), -50, "整备服务结果应记录金币变化。");
            _test.True(DictDictionary(restResult, "service_side_effects").ContainsKey("world_step_advanced"), "整备服务结果应记录 world_step_advanced。");
            _test.False(restResult.ContainsKey("effects"), "整备服务结果不应再输出 legacy effects。");

            GDictionary missingResult = SettlementServiceResultProjection.Project(
                handler.ExecuteSettlementActionTyped("missing_settlement", "service:training", new GDictionary())
            );
            _test.False(DictBool(missingResult, "success", true), "缺失据点时服务结果应失败。");
            _test.True(missingResult.ContainsKey("pending_character_rewards"), "失败结果也应包含 canonical pending_character_rewards。");
            _test.True(missingResult.ContainsKey("service_side_effects"), "失败结果也应包含 service_side_effects。");
            _test.False(missingResult.ContainsKey("pending_mastery_rewards"), "失败结果也不应保留 legacy pending_mastery_rewards。");
            _test.False(missingResult.ContainsKey("effects"), "失败结果也不应保留 legacy effects。");

            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Settlement);
            runtime.SetActiveSettlementId("spring_village_01");
            GameRuntimeFacade.RuntimeCommandResult stagecoachResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:stagecoach",
                    new GDictionary()
                );
            _test.True(
                stagecoachResult.Ok,
                $"驿站服务应能打开路线窗口。message={stagecoachResult.Message}"
            );
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Stagecoach, "打开驿站后应切换到驿站 modal。");
            GameRuntimeFacade.RuntimeCommandResult travelResult =
                handler.CommandStagecoachTravelTyped("graystone_town_01");
            _test.True(
                travelResult.Ok,
                $"驿站换乘应执行成功。message={travelResult.Message}"
            );
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Settlement, "驿站换乘后应回到目标据点窗口。");
            _test.Eq(runtime._active_settlement_id, "graystone_town_01", "驿站换乘后应记录目标据点。");
            _test.Eq(runtime._party_state.gold, 120, "驿站换乘应按距离扣除路费。");
            _test.Eq(runtime._player_coord, new Vector2I(2, 1), "驿站换乘后应更新玩家坐标。");

            handler.OnSettlementWindowClosed();
            _test.Eq(runtime._active_settlement_id, "", "关闭据点窗口应清空当前据点 ID。");
            _test.Eq(runtime._active_settlement_feedback_text, "", "关闭据点窗口应清空反馈文本。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Reward, "存在待确认角色奖励时，关闭据点窗口后应立即恢复奖励 modal。");
            _test.Eq(runtime._current_status_message, "已关闭据点窗口，返回世界地图。", "关闭据点窗口后应刷新状态文案。");
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestContractBoardEvaluatorAndFeedback()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildEvaluatorQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "evaluator",
            BuildPartyState(20, 200),
            new[]
            {
                BuildSettlementRecord("spring_village_01", "春泉村", Vector2I.Zero, BuildBasicSettlementServices(false)),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            GameRuntimeFacade.RuntimeCommandResult openResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary()
                );
            _test.True(openResult.Ok, "测试前置：任务板应能打开。");
            GDictionary windowData = handler.GetContractBoardWindowData();

            GDictionary lockedEntry = FindContractBoardEntry(DictArray(windowData, "entries"), "contract_locked_hunt");
            _test.False(DictBool(lockedEntry, "is_enabled", true), "未满足前置任务时，契约条目应被禁用。");
            _test.Eq(DictString(lockedEntry, "lock_reason_id", ""), "quest_not_completed", "禁用条目的 lock_reason_id 应来自 evaluator。");
            _test.True(!string.IsNullOrEmpty(DictString(lockedEntry, "disabled_reason", "")), "禁用条目应包含 disabled_reason。");

            GDictionary dialogueEntry = FindContractBoardEntry(DictArray(windowData, "entries"), "contract_dialogue_quest");
            string detailsText = DictString(dialogueEntry, "details_text", "");
            _test.True(detailsText.Contains("这是接取对话文案。"), "details_text 应展示 accept_dialogue_text。");
            _test.Eq(DictString(dialogueEntry, "accept_dialogue_text", ""), "这是接取对话文案。", "accept_dialogue_text 字段应原样暴露。");

            GDictionary confirmationEntry = FindContractBoardEntry(DictArray(windowData, "entries"), "contract_confirmation_quest");
            _test.Eq(DictString(confirmationEntry, "accept_feedback_success", ""), "已确认接取确认契约。", "accept_feedback_success 字段应原样暴露。");
            _test.Eq(DictString(confirmationEntry, "accept_confirmation_text", ""), "确认要接取这个契约吗？", "accept_confirmation_text 字段应原样暴露。");

            GameRuntimeFacade.RuntimeCommandResult confirmationBypassResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary
                    {
                        ["submission_source"] = "contract_board",
                        ["quest_id"] = "contract_confirmation_quest",
                        ["provider_interaction_id"] = "service_contract_board",
                        ["confirm_accept"] = true,
                    }
                );
            _test.False(
                confirmationBypassResult.Ok,
                "未进入确认态时提交 confirm_accept=true 应被拒绝。"
            );
            _test.False(
                runtime._party_state.HasActiveQuest("contract_confirmation_quest"),
                "确认态绕过不应接取任务。"
            );

            GameRuntimeFacade.RuntimeCommandResult lockedSubmitResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary
                    {
                        ["submission_source"] = "contract_board",
                        ["quest_id"] = "contract_locked_hunt",
                        ["provider_interaction_id"] = "service_contract_board",
                    }
                );
            _test.False(lockedSubmitResult.Ok, "未满足前置任务时提交接取应失败。");
            _test.Eq(runtime._active_settlement_feedback_text, "当前无法接取该前置契约。", "提交失败时应使用 accept_feedback_failure 更新据点反馈。");

            GDictionary refreshedData = handler.GetContractBoardWindowData();
            _test.Eq(DictString(refreshedData, "state_summary_text", ""), "当前无法接取该前置契约。", "feedback_text 应保留在 state_summary_text 中。");
            _test.Eq(DictString(refreshedData, "summary_text", ""), "当前无法接取该前置契约。", "feedback_text 应保留在 summary_text 中。");

            GameRuntimeFacade.RuntimeCommandResult confirmationResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary
                    {
                        ["submission_source"] = "contract_board",
                        ["quest_id"] = "contract_confirmation_quest",
                        ["provider_interaction_id"] = "service_contract_board",
                    }
                );
            _test.True(confirmationResult.Ok, "首次提交带确认文案的契约应返回确认状态而不是错误。");
            _test.False(runtime._party_state.HasActiveQuest("contract_confirmation_quest"), "确认弹窗期间不应接取任务。");
            GDictionary confirmationContext = handler.GetContractBoardWindowData();
            _test.Eq(DictString(confirmationContext, "pending_confirmation_quest_id", ""), "contract_confirmation_quest", "应写入 pending_confirmation_quest_id。");
            _test.Eq(DictString(confirmationContext, "pending_confirmation_text", ""), "确认要接取这个契约吗？", "应写入 pending_confirmation_text。");
            _test.Eq(DictString(confirmationContext, "pending_confirmation_source", ""), "contract_board", "应写入 pending_confirmation_source。");

            GameRuntimeFacade.RuntimeCommandResult confirmedAcceptResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary
                    {
                        ["submission_source"] = "contract_board",
                        ["quest_id"] = "contract_confirmation_quest",
                        ["provider_interaction_id"] = "service_contract_board",
                        ["confirm_accept"] = true,
                    }
                );
            _test.True(confirmedAcceptResult.Ok, "带 confirm_accept=true 的提交应成功接取。");
            _test.True(runtime._party_state.HasActiveQuest("contract_confirmation_quest"), "确认后应把任务写入 active_quests。");
            _test.Eq(runtime._active_settlement_feedback_text, "已确认接取确认契约。", "确认接取后应使用 accept_feedback_success 更新据点反馈。");
            _test.Eq(DictString(handler.GetContractBoardWindowData(), "summary_text", ""), "已确认接取确认契约。", "确认接取后 summary_text 应使用 accept_feedback_success。");
            _test.Eq(DictString(handler.GetContractBoardWindowData(), "state_summary_text", ""), "已确认接取确认契约。", "确认接取后 state_summary_text 应使用 accept_feedback_success。");
            _test.Eq(DictString(handler.GetContractBoardWindowData(), "pending_confirmation_quest_id", ""), "", "确认后应清空 pending_confirmation_quest_id。");

            runtime._party_state.AddCompletedQuestId("contract_prerequisite_hunt");
            runtime._character_management.SetPartyState(runtime._party_state);
            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Settlement);
            GameRuntimeFacade.RuntimeCommandResult reopenResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary()
                );
            _test.True(reopenResult.Ok, $"前置任务完成后重新打开任务板应成功。message={reopenResult.Message}");
            GDictionary unlockedEntry = FindContractBoardEntry(DictArray(handler.GetContractBoardWindowData(), "entries"), "contract_locked_hunt");
            _test.True(DictBool(unlockedEntry, "is_enabled", false), "前置任务完成后，被锁定的契约应变为可用。");
            _test.Eq(DictString(unlockedEntry, "lock_reason_id", ""), "", "可用条目的 lock_reason_id 应为空。");
            _test.Eq(DictString(unlockedEntry, "disabled_reason", ""), "", "可用条目的 disabled_reason 应为空。");

            GameRuntimeFacade.RuntimeCommandResult unlockedAcceptResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary
                    {
                        ["submission_source"] = "contract_board",
                        ["quest_id"] = "contract_locked_hunt",
                        ["provider_interaction_id"] = "service_contract_board",
                    }
                );
            _test.True(unlockedAcceptResult.Ok, "解锁后应能正常接取。");
            _test.True(runtime._party_state.HasActiveQuest("contract_locked_hunt"), "解锁接取后应把任务写入 active_quests。");
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private void TestSettlementShopServiceRejectsBadEntrySchema()
    {
        var shopService = new SettlementShopService();
        Dictionary<StringName, ItemDefinition> itemDefs = new(
            new ItemContentRegistry().GetItemDefsTyped()
        );
        GDictionary settlementRecord = MinimalSettlementRecord("spring_village_01", "春泉村", Vector2I.Zero, new GArray());
        PartyState validParty = BuildPartyState(10, 100);
        var validWarehouse = new PartyWarehouseService();
        validWarehouse.Setup(validParty, itemDefs);
        validWarehouse.AddItemTyped("travel_ration", 3);
        GDictionary validWindowData = shopService.BuildWindowDataTyped(
            "service_basic_supply",
            settlementRecord,
            BuildShopState(new GArray { new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = 2, ["unit_price"] = 12, ["sold_out"] = false } }),
            itemDefs,
            validWarehouse,
            100
        );
        _test.Eq(DictArray(validWindowData, "buy_entries").Count, 1, "正式 shop stock entry 应生成可购买条目。");
        _test.Eq(DictArray(validWindowData, "sell_entries").Count, 1, "正式 sell inventory entry 应生成可出售条目。");

        var invalidStockCases = new (string Label, GDictionary Entry)[]
        {
            ("字符串 quantity", new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = "2", ["unit_price"] = 12, ["sold_out"] = false }),
            ("字符串 unit_price", new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = 2, ["unit_price"] = "12", ["sold_out"] = false }),
            ("缺 unit_price", new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = 2, ["sold_out"] = false }),
            ("空 item_id", new GDictionary { ["item_id"] = "", ["quantity"] = 2, ["unit_price"] = 12, ["sold_out"] = false }),
            ("StringName item_id", new GDictionary { ["item_id"] = new StringName("healing_herb"), ["quantity"] = 2, ["unit_price"] = 12, ["sold_out"] = false }),
            ("旧 price 字段", new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = 2, ["unit_price"] = 12, ["price"] = 1, ["sold_out"] = false }),
            ("字符串 sold_out", new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = 2, ["unit_price"] = 12, ["sold_out"] = "false" }),
            ("非正 quantity", new GDictionary { ["item_id"] = "healing_herb", ["quantity"] = 0, ["unit_price"] = 12, ["sold_out"] = false }),
        };
        foreach ((string label, GDictionary entry) in invalidStockCases)
        {
            PartyState partyState = BuildPartyState(10, 100);
            var warehouse = new PartyWarehouseService();
            warehouse.Setup(partyState, itemDefs);
            GDictionary settlementState = BuildShopState(new GArray { entry.Duplicate(true) });
            GDictionary windowData = shopService.BuildWindowDataTyped(
                "service_basic_supply",
                settlementRecord,
                settlementState,
                itemDefs,
                warehouse,
                partyState.gold
            );
            _test.Eq(DictArray(windowData, "buy_entries").Count, 0, $"{label} 的坏 shop stock 不应生成购买窗口条目。");
            int goldBefore = partyState.gold;
            GDictionary buyResult = ProjectShopTradeResult(shopService
                .BuyTyped(
                    "service_basic_supply",
                    settlementRecord,
                    settlementState,
                    itemDefs,
                    warehouse,
                    partyState,
                    "healing_herb",
                    1,
                    ""
                ));
            _test.False(DictBool(buyResult, "success", true), $"{label} 的坏 shop stock 不应允许购买交易。");
            _test.Eq(partyState.gold, goldBefore, $"{label} 的坏 shop stock 不应扣除金币。");
            _test.Eq(warehouse.CountItem("healing_herb"), 0, $"{label} 的坏 shop stock 不应写入仓库。");
        }

        Dictionary<StringName, ItemDefinition> noPriceItemDefs = new(itemDefs)
        {
            ["no_price_sample"] = MakeShopItemDefinition(
                "no_price_sample",
                "无价样品",
                "没有正式回收价。",
                10,
                0,
                true
            ),
        };
        PartyState noPriceParty = BuildPartyState(10, 0);
        var noPriceWarehouse = new PartyWarehouseService();
        noPriceWarehouse.Setup(noPriceParty, noPriceItemDefs);
        noPriceWarehouse.AddItemTyped("no_price_sample", 1);
        GDictionary noPriceWindowData = shopService.BuildWindowDataTyped(
            "service_basic_supply",
            settlementRecord,
            BuildShopState(new GArray()),
            noPriceItemDefs,
            noPriceWarehouse,
            100
        );
        _test.Eq(DictArray(noPriceWindowData, "sell_entries").Count, 0, "缺少正式 sell_price 的物品不应补默认回收价。");
        GDictionary noPriceSellResult = ProjectShopTradeResult(shopService
            .SellTyped(
                "service_basic_supply",
                settlementRecord,
                BuildShopState(new GArray()),
                noPriceItemDefs,
                noPriceWarehouse,
                noPriceParty,
                "no_price_sample",
                1,
                ""
            ));
        _test.False(DictBool(noPriceSellResult, "success", true), "缺少正式 sell_price 的物品不应允许出售交易。");
        _test.Eq(noPriceParty.gold, 0, "缺少正式 sell_price 的出售失败不应增加金币。");
        _test.Eq(noPriceWarehouse.CountItem("no_price_sample"), 1, "缺少正式 sell_price 的出售失败不应移除仓库物品。");
    }

    private async Task TestSettlementHandlerRejectsStringNameSubmissionFields()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "stringname_submission_fields",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    BuildBasicSettlementServices(false)
                ),
            },
            BuildQuestDefs()
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            GameRuntimeFacade.RuntimeCommandResult openResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary()
                );
            _test.True(openResult.Ok, "测试前置：任务板应能打开。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.ContractBoard, "测试前置：任务板 modal 应处于打开状态。");

            GameRuntimeFacade.RuntimeCommandResult stringNameSourceResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary
                    {
                        ["submission_source"] = new StringName("contract_board"),
                        ["quest_id"] = "contract_manual_drill",
                        ["provider_interaction_id"] = "service_contract_board",
                    }
                );
            _test.False(stringNameSourceResult.Ok, "StringName submission_source 不应被当作正式提交来源。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.ContractBoard, "拒绝 StringName submission_source 后不应切换 modal。");
            _test.False(runtime._party_state.HasActiveQuest("contract_manual_drill"), "拒绝 StringName submission_source 后不应接取任务。");

            GameRuntimeFacade.RuntimeCommandResult stringNameQuestResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary
                    {
                        ["submission_source"] = "contract_board",
                        ["quest_id"] = new StringName("contract_manual_drill"),
                        ["provider_interaction_id"] = "service_contract_board",
                    }
                );
            _test.False(stringNameQuestResult.Ok, "StringName quest_id 不应被当作正式任务 ID。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.ContractBoard, "拒绝 StringName quest_id 后不应切换 modal。");
            _test.False(runtime._party_state.HasActiveQuest("contract_manual_drill"), "拒绝 StringName quest_id 后不应接取任务。");

            GameRuntimeFacade.RuntimeCommandResult stringNameProviderResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:contract_board",
                    new GDictionary
                    {
                        ["submission_source"] = "contract_board",
                        ["quest_id"] = "contract_manual_drill",
                        ["provider_interaction_id"] = new StringName("service_contract_board"),
                    }
                );
            _test.False(stringNameProviderResult.Ok, "StringName provider_interaction_id 不应被当作正式 provider ID。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.ContractBoard, "拒绝 StringName provider_interaction_id 后不应切换 modal。");
            _test.False(runtime._party_state.HasActiveQuest("contract_manual_drill"), "拒绝 StringName provider_interaction_id 后不应接取任务。");
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private static GDictionary ProjectShopTradeResult(SettlementShopTradeResult result)
    {
        var payload = new GDictionary
        {
            ["success"] = result?.Success ?? false,
            ["message"] = result?.Message ?? "",
        };

        if (result == null || !result.Success)
            return payload;

        payload["gold_delta"] = result.GoldDelta;
        payload["item_id"] = result.ItemId ?? "";
        payload["quantity"] = result.Quantity;
        if (!string.IsNullOrEmpty(result.InstanceId))
            payload["instance_id"] = result.InstanceId;
        return payload;
    }

    private async Task TestSettlementHandlerRejectsInvalidOrSpoofedActions()
    {
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "invalid",
            BuildPartyState(12, 100),
            new[] { BuildSettlementRecord("spring_village_01", "春泉村", Vector2I.Zero, BuildShopAndStagecoachServices()) },
            new Dictionary<StringName, QuestDefinition>()
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.None);
            GameRuntimeFacade.RuntimeCommandResult closedModalResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:basic_supply",
                    new GDictionary()
                );
            _test.False(closedModalResult.Ok, "未打开据点窗口时不应执行据点服务。");
            _test.Eq(
                closedModalResult.Message,
                "当前没有打开对应的据点窗口。",
                "未打开据点窗口应返回明确错误。"
            );
            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Settlement);

            runtime._fog_system.Setup(new Vector2I(8, 8));
            GameRuntimeFacade.RuntimeCommandResult hiddenSettlementResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:basic_supply",
                    new GDictionary()
                );
            _test.False(hiddenSettlementResult.Ok, "不可见据点不应执行据点服务。");
            _test.Eq(
                hiddenSettlementResult.Message,
                "当前据点不在视野中，不能执行据点服务。",
                "不可见据点应返回明确错误。"
            );
            MakeVisible(runtime, Vector2I.Zero);

            GameRuntimeFacade.RuntimeCommandResult missingActionResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:missing",
                    new GDictionary()
            );
            _test.False(missingActionResult.Ok, "未开放的 action_id 应被直接拒绝。");
            _test.True(
                !string.IsNullOrEmpty(missingActionResult.Message),
                "未开放 action_id 应返回错误信息。"
            );
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Settlement, "未开放 action_id 失败后不应切换 modal。");

            GameRuntimeFacade.RuntimeCommandResult disabledStagecoachResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:stagecoach",
                    new GDictionary()
                );
            _test.False(disabledStagecoachResult.Ok, "禁用的据点服务不应继续执行。");
            _test.Eq(
                disabledStagecoachResult.Message,
                "驿站 当前不可用：暂无已访问路线。",
                "禁用服务应返回明确 disabled_reason。"
            );
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Settlement, "禁用服务失败后不应切换 modal。");

            handler.OnSettlementActionRequested("spring_village_01", "service:basic_supply", new GDictionary
            {
                ["interaction_script_id"] = "service_research",
                ["facility_name"] = "伪造图书馆",
                ["npc_name"] = "伪造导师",
                ["service_type"] = "研究",
            });
            GDictionary signalShopWindowData = handler.GetShopWindowData();
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Shop, "UI 信号入口收到伪造 interaction_script_id 时仍应按真实商店入口打开 shop modal。");
            _test.Eq(DictString(signalShopWindowData, "interaction_script_id", ""), "service_basic_supply", "UI 信号入口应使用真实服务 interaction_script_id。");
            _test.Eq(runtime._current_status_message, "已打开 补给铺 的商店。", "UI 信号入口应使用真实服务 facility_name。");
            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Settlement);

            GameRuntimeFacade.RuntimeCommandResult spoofedShopResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service:basic_supply",
                    new GDictionary
                    {
                        ["interaction_script_id"] = "service_research",
                        ["facility_name"] = "伪造图书馆",
                        ["npc_name"] = "伪造导师",
                        ["service_type"] = "研究",
                    }
                );
            _test.True(spoofedShopResult.Ok, "合法 action_id 仍应按真实服务入口执行。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Shop, "伪造 interaction_script_id 时仍应按真实商店入口打开 shop modal。");
            _test.True(handler.GetShopWindowData().Count > 0, "按真实商店入口执行后应能读取 shop window data。");
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
            int createError = gameSession.CreateNewSave(TestConfigPath, "research_route_service", "研究入口验证");
            _test.Eq(createError, (int)Error.Ok, "创建 research 入口验证世界应成功。");
            if (createError == (int)Error.Ok)
            {
                GDictionary foundResearchService = new();
                bool foundLegacyUnlockArchive = false;
                foreach (GDictionary settlement in Dictionaries(DictArray(gameSession.GetWorldData(), "settlements")))
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
                _test.True(foundResearchService.Count > 0, "正式 world config 生成结果应包含 research 服务入口。");
                _test.Eq(DictString(foundResearchService, "action_id", ""), "service:research", "research 服务应映射到正式 action_id。");
                _test.False(foundLegacyUnlockArchive, "research 服务不应继续使用 legacy service_unlock_archive 入口。");
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
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs)
    {
        GameSession gameSession = await InstallGameSession($"SettlementHandlerGameSession_{suffix}");
        GDictionary worldData = BuildWorldData(settlements);
        ConfigureSessionForRuntimeTest(
            gameSession,
            $"settlement_handler_{suffix}",
            worldData,
            partyState,
            questDefs ?? new Dictionary<StringName, QuestDefinition>()
        );
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs = gameSession.GetItemDefsTyped();

        var runtime = new GameRuntimeFacade
        {
            _game_session = gameSession,
            _party_state = partyState,
            _player_coord = Vector2I.Zero,
            _selected_coord = Vector2I.Zero,
            _player_faction_id = "player",
        };
        runtime.SetActiveSettlementId(DictString(settlements[0], "settlement_id", ""));
        runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Settlement);
        runtime._world_map_data_context.BindRootWorldData(worldData);
        var contextGrid = new WorldMapGridSystem();
        runtime._world_map_data_context.SyncActiveWorldContext(
            gameSession._generation_config,
            contextGrid,
            Vector2I.Zero,
            Vector2I.Zero
        );
        runtime._fog_system.Setup(new Vector2I(8, 8));
        MakeVisible(runtime, Vector2I.Zero);
        runtime._character_management.setup(
            partyState,
            gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped(),
            gameSession.GetProfessionDefsTyped(),
            gameSession.GetAchievementDefsTyped(),
            itemDefs,
            gameSession.GetQuestDefsTyped(),
            gameSession.AllocateEquipmentInstanceId,
            gameSession.GetProgressionIdentityCatalogTyped()
        );
        runtime._party_warehouse_service.Setup(partyState, itemDefs, gameSession.AllocateEquipmentInstanceId);
        runtime._party_item_use_service.Setup(
            partyState,
            itemDefs,
            gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped(),
            runtime._party_warehouse_service,
            runtime._character_management
        );
        runtime._party_equipment_service.Setup(partyState, itemDefs, runtime._party_warehouse_service, gameSession.AllocateEquipmentInstanceId);
        runtime._settlement_command_handler.SetupRuntime(runtime);
        runtime._warehouse_handler.Setup(runtime);
        runtime._quest_command_handler.Setup(runtime);
        runtime._reward_flow_handler.Setup(runtime);

        return new RuntimeFixture(runtime, gameSession, runtime._settlement_command_handler, runtime._party_warehouse_service);
    }

    private static void ConfigureSessionForRuntimeTest(
        GameSession gameSession,
        string saveId,
        GDictionary worldData,
        PartyState partyState,
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs
    )
    {
        gameSession.ConfigureRuntimeWorldForTests(
            saveId,
            TestConfigPath,
            worldData,
            partyState,
            questDefs ?? new Dictionary<StringName, QuestDefinition>(),
            "settlement_handler_test",
            "Settlement Handler Test",
            new Vector2I(8, 8)
        );
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
        fixture.GameSession?.DiscardPendingSave();
        fixture.Runtime?.Dispose();
        await DisposeGameSession(fixture.GameSession, "清理 settlement handler 验证存档应成功。");
    }

    private async Task DisposeGameSession(GameSession gameSession, string clearMessage)
    {
        if (gameSession == null)
        {
            return;
        }
        int clearError = gameSession.ClearPersistedGame();
        _test.Eq(clearError, (int)Error.Ok, clearMessage);
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
            ["resource_nodes"] = new GArray(),
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

    private static IReadOnlyDictionary<StringName, QuestDefinition> BuildQuestDefs()
    {
        var questDefinitions = new Dictionary<StringName, QuestDefinition>();
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_first_hunt", "首轮狩猎", "击败任意一组敌对遭遇。", "service_contract_board", new QuestObjectiveDefinition[] { BuildObjective("defeat_enemy_once", "defeat_enemy", "", 1) }, new QuestRewardDefinition[] { BuildGoldReward(80) }));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_manual_drill", "训练记录", "在训练场完成两次记录。", "service_contract_board", new QuestObjectiveDefinition[] { BuildObjective("train_once", "settlement_action", "service:training", 2) }, new QuestRewardDefinition[] { BuildGoldReward(30) }, false, "", "已接取任务《训练记录》。"));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_repeatable_patrol", "巡路值守", "完成一次例行巡路，随后可再次接取。", "service_contract_board", new QuestObjectiveDefinition[] { BuildObjective("warehouse_visit", "settlement_action", "service:warehouse", 1) }, new QuestRewardDefinition[] { BuildGoldReward(15) }, true));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_warehouse_visit", "仓储访问追踪", "据点仓储动作进度测试。", "service_warehouse_hidden", new QuestObjectiveDefinition[] { BuildObjective("warehouse_visit", "settlement_action", "service:warehouse", 1) }, new QuestRewardDefinition[] { BuildGoldReward(1) }));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_regional_bounty", "地区悬赏", "仅应出现在悬赏署任务板。", "service_bounty_registry", new QuestObjectiveDefinition[] { BuildObjective("submit_report", "settlement_action", "service:report_bounty", 1) }, new QuestRewardDefinition[] { BuildGoldReward(120) }));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_missing_display_name", "", "缺少 display_name 的坏契约不应显示。", "service_contract_board", new QuestObjectiveDefinition[] { BuildObjective("bad_missing_name", "defeat_enemy", "", 1) }, new QuestRewardDefinition[] { BuildGoldReward(1) }));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_missing_description", "缺说明契约", "", "service_contract_board", new QuestObjectiveDefinition[] { BuildObjective("bad_missing_description", "defeat_enemy", "", 1) }, new QuestRewardDefinition[] { BuildGoldReward(1) }));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_missing_objectives", "缺目标契约", "缺少 objective_defs 的坏契约不应显示。", "service_contract_board", System.Array.Empty<QuestObjectiveDefinition>(), new QuestRewardDefinition[] { BuildGoldReward(1) }));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_missing_objective_target", "缺目标对象契约", "据点事务目标缺少 target_id 时不应回退成未命名。", "service_contract_board", new QuestObjectiveDefinition[] { BuildObjective("bad_missing_target", "settlement_action", "", 1) }, new QuestRewardDefinition[] { BuildGoldReward(1) }));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_unknown_objective_type", "未知目标契约", "未知 objective_type 不应直接显示 objective_id。", "service_contract_board", new QuestObjectiveDefinition[] { BuildObjective("bad_unknown_objective", "legacy_custom", "legacy_target", 1) }, new QuestRewardDefinition[] { BuildGoldReward(1) }));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_missing_rewards", "缺奖励契约", "缺少 reward_entries 的坏契约不应显示。", "service_contract_board", new QuestObjectiveDefinition[] { BuildObjective("bad_missing_rewards", "defeat_enemy", "", 1) }, System.Array.Empty<QuestRewardDefinition>()));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_invalid_reward_amount", "坏奖励契约", "非法奖励数值不应回退成奖励待定。", "service_contract_board", new QuestObjectiveDefinition[] { BuildObjective("bad_reward_amount", "defeat_enemy", "", 1) }, new QuestRewardDefinition[] { BuildGoldReward(0) }));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_typed_missing_objective_target", "typed 缺目标对象契约", "typed 据点事务目标缺少 target_id 时不应显示。", "service_contract_board", new QuestObjectiveDefinition[] { BuildObjective("typed_bad_missing_target", "settlement_action", "", 1) }, new QuestRewardDefinition[] { BuildGoldReward(1) }));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_typed_invalid_reward_amount", "typed 坏奖励契约", "typed 非法奖励数值不应回退成奖励待定。", "service_contract_board", new QuestObjectiveDefinition[] { BuildObjective("typed_bad_reward_amount", "defeat_enemy", "", 1) }, new QuestRewardDefinition[] { BuildGoldReward(0) }));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_supply_drop", "物资缴纳", "向任务板提交两份铁矿石。", "service_contract_board", new QuestObjectiveDefinition[] { BuildObjective("deliver_ore", "submit_item", "iron_ore", 2) }, new QuestRewardDefinition[] { BuildGoldReward(18) }));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_training", "训练追踪", "据点训练进度测试。", "service_training_hidden", new QuestObjectiveDefinition[] { BuildObjective("train_once", "settlement_action", "service:training", 1) }, new QuestRewardDefinition[] { BuildGoldReward(1) }));
        return questDefinitions;
    }

    private static IReadOnlyDictionary<StringName, QuestDefinition> BuildEvaluatorQuestDefs()
    {
        var questDefinitions = new Dictionary<StringName, QuestDefinition>();
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_prerequisite_hunt", "前置狩猎", "完成前置狩猎任务。", "service_contract_board", new QuestObjectiveDefinition[] { BuildObjective("defeat_enemy_once", "defeat_enemy", "", 1) }, new QuestRewardDefinition[] { BuildGoldReward(10) }));
        AddQuestDefinition(
            questDefinitions,
            BuildQuestDefinition(
                "contract_locked_hunt",
                "锁定狩猎",
                "需要前置任务完成后才能接取。",
                "service_contract_board",
                new QuestObjectiveDefinition[] { BuildObjective("defeat_enemy_locked", "defeat_enemy", "", 1) },
                new QuestRewardDefinition[] { BuildGoldReward(20) },
                acceptFeedbackFailure: "当前无法接取该前置契约。",
                acceptRequirements: new QuestAcceptRequirementDefinition[]
                {
                    new("quest_completed", "contract_prerequisite_hunt"),
                }
            )
        );
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_dialogue_quest", "对话契约", "展示接取对话文案。", "service_contract_board", new QuestObjectiveDefinition[] { BuildObjective("dialogue_objective", "defeat_enemy", "", 1) }, new QuestRewardDefinition[] { BuildGoldReward(5) }, false, "这是接取对话文案。"));
        AddQuestDefinition(questDefinitions, BuildQuestDefinition("contract_confirmation_quest", "确认契约", "需要确认才能接取。", "service_contract_board", new QuestObjectiveDefinition[] { BuildObjective("confirmation_objective", "defeat_enemy", "", 1) }, new QuestRewardDefinition[] { BuildGoldReward(7) }, false, "", "已确认接取确认契约。", "", "确认要接取这个契约吗？"));
        return questDefinitions;
    }

    private static void AddQuestDefinition(
        Dictionary<StringName, QuestDefinition> questDefinitions,
        QuestDefinition questDefinition
    )
    {
        questDefinitions[questDefinition.QuestId] = questDefinition;
    }

    private static QuestDefinition BuildQuestDefinition(string questId, string displayName, string description, string providerInteractionId, IReadOnlyList<QuestObjectiveDefinition> objectives, IReadOnlyList<QuestRewardDefinition> rewards, bool isRepeatable = false, string acceptDialogueText = "", string acceptFeedbackSuccess = "", string acceptFeedbackFailure = "", string acceptConfirmationText = "", IReadOnlyList<QuestAcceptRequirementDefinition> acceptRequirements = null)
    {
        StringName providerKind = providerInteractionId;
        IReadOnlyList<StringName> listingChannels = System.Array.Empty<StringName>();
        if (providerInteractionId == "service_contract_board")
        {
            listingChannels = new StringName[] { "contract_board" };
        }
        else if (providerInteractionId == "service_bounty_registry")
        {
            listingChannels = new StringName[] { "bounty_registry" };
        }

        return new QuestDefinition(
            questId,
            displayName,
            description,
            providerInteractionId,
            System.Array.Empty<StringName>(),
            acceptRequirements ?? System.Array.Empty<QuestAcceptRequirementDefinition>(),
            objectives,
            rewards,
            isRepeatable,
            providerKind,
            listingChannels,
            acceptDialogueText,
            acceptFeedbackSuccess,
            acceptFeedbackFailure,
            acceptConfirmationText
        );
    }

    private static QuestObjectiveDefinition BuildObjective(string objectiveId, string objectiveType, string targetId, int targetValue) =>
        new(objectiveId, objectiveType, targetId, targetValue);

    private static QuestRewardDefinition BuildGoldReward(int amount) =>
        new("gold", amount, "", 0, "", System.Array.Empty<QuestPendingRewardEntryDefinition>());

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
        partyState.SetMemberState(hero);
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

    private static ItemDefinition MakeShopItemDefinition(
        StringName itemId,
        string displayName,
        string description,
        int buyPrice,
        int sellPrice,
        bool sellable
    )
    {
        return new ItemDefinition(
            itemId,
            "",
            displayName,
            description,
            "",
            false,
            0,
            buyPrice,
            sellPrice,
            sellable,
            1,
            ItemDefinition.ToStringName(ItemCategoryKind.Misc),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<TraitRollGroupDefinition>(),
            System.Array.Empty<string>(),
            System.Array.Empty<AttributeModifierDefinition>(),
            "",
            System.Array.Empty<string>(),
            null,
            "",
            null,
            -1
        );
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
            GDictionary rewardData = reward != null
                ? PendingCharacterRewardPayload.Project(reward)
                : new GDictionary();
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
            GDictionary rewardData = reward != null
                ? PendingCharacterRewardPayload.Project(reward)
                : new GDictionary();
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

    private void AssertSequence(IReadOnlyList<string> actual, IReadOnlyList<string> expected, string message)
    {
        if (actual.Count != expected.Count)
        {
            _test.Fail($"{message} | actual=[{string.Join(", ", actual)}] expected=[{string.Join(", ", expected)}]");
            return;
        }
        for (int i = 0; i < actual.Count; i++)
        {
            if (actual[i] != expected[i])
            {
                _test.Fail($"{message} | actual=[{string.Join(", ", actual)}] expected=[{string.Join(", ", expected)}]");
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
