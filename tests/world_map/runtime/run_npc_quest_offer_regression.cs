using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_npc_quest_offer_regression : LifecycleTestSceneTree
{
    private const string TestConfigPath = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(RunAsync);
    }

    private async void RunAsync()
    {
        await TestNpcQuestOfferOpensForMatchingInteraction();
        await TestNpcQuestOfferSkipsNonNpcProvider();
        await TestNpcQuestOfferRequiresNpcOfferChannel();
        await TestNpcQuestOfferRespectsAcceptRequirements();
        await TestNpcQuestOfferAcceptsQuest();
        await TestNpcQuestOfferSubmitsItemsAndClaimsReward();
        await TestNpcQuestOfferConfirmationFlow();
        await TestNpcQuestOfferRejectsLockedQuest();
        await TestNpcQuestOfferRejectsSubmissionWithoutModal();
        await TestNpcQuestOfferRejectsWrongSettlement();
        await TestNpcQuestOfferRejectsWrongAction();
        await TestNpcQuestOfferFallsBackWhenNoMatchingQuests();
        await TestNpcQuestOfferMultipleQuests();
        await TestNpcQuestOfferRejectsMissingQuestId();
        await TestNpcQuestOfferRejectsWrongNpcQuest();
        await TestNpcQuestOfferRejectsWrongListingChannel();
        await TestNpcQuestOfferRejectsNonNpcProviderKind();
        await TestNpcQuestOfferCloseModalLifecycle();
        await TestNpcQuestOfferRefreshesEntriesAfterAccept();
        await TestNpcQuestOfferRejectsConfirmBypass();

        RequestTestExit(_test.Finish("NPC quest offer regression"));
    }

    private async Task TestNpcQuestOfferOpensForMatchingInteraction()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_offer",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_blacksmith_hrothgar",
                            "npc_blacksmith_hrothgar",
                            "铁匠铺",
                            "霍斯加尔"
                        ),
                        BuildNpcServiceEntry(
                            "npc_elder_locked",
                            "npc_elder_locked",
                            "长老厅",
                            "长老"
                        ),
                        BuildNpcServiceEntry(
                            "npc_merchant_no_offer",
                            "npc_merchant_no_offer",
                            "杂货摊",
                            "旅行商人"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            GameRuntimeFacade.RuntimeCommandResult result =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["interaction_script_id"] = "npc_blacksmith_hrothgar",
                        ["facility_name"] = "铁匠铺",
                        ["npc_name"] = "霍斯加尔",
                    }
                );

            _test.True(result.Ok, $"NPC 委托动作应成功打开 offer。message={result.Message}");
            _test.Eq(
                runtime._active_modal_kind,
                RuntimeModalKind.NpcQuestOffer,
                "NPC 委托动作应将当前 modal 切换为 NpcQuestOffer。"
            );

            NpcQuestOfferWindowData windowData = runtime.GetActiveNpcQuestOfferData();
            _test.Eq(
                windowData.SettlementId,
                "spring_village_01",
                "NPC offer 窗口应保留 settlement_id。"
            );
            _test.Eq(
                windowData.ActionId,
                "npc_blacksmith_hrothgar",
                "NPC offer 窗口应保留打开它的服务入口。"
            );
            _test.Eq(
                windowData.NpcInteractionId,
                "npc_blacksmith_hrothgar",
                "NPC offer 窗口应保留 npc_interaction_id。"
            );
            _test.Eq(
                windowData.NpcName,
                "blacksmith hrothgar",
                "NPC offer 窗口应将下划线转换为可读名称。"
            );
            _test.Eq(
                windowData.SelectedQuestId,
                "npc_blacksmith_hrothgar_cave_beasts",
                "NPC offer 窗口应默认选中首个任务。"
            );

            _test.Eq(windowData.Entries.Count, 2, "NPC offer 窗口应包含两个匹配的任务条目。");
            NpcQuestOfferEntryData entry = windowData.Entries.FirstOrDefault(
                e => e.QuestId == "npc_blacksmith_hrothgar_cave_beasts"
            );
            _test.True(entry != null, "应能找到 cave_beasts 任务条目。");
            _test.Eq(entry.DisplayName, "洞穴野兽", "条目应保留 display_name。");
            _test.Eq(
                entry.AcceptDialogueText,
                "帮我清理峡谷北面的野兽。",
                "条目应保留 accept_dialogue_text。"
            );
            _test.Eq(
                entry.AcceptFeedbackSuccess,
                "接受委托：洞穴野兽。",
                "条目应保留 accept_feedback_success。"
            );
            _test.Eq(
                entry.AcceptConfirmationText,
                "接受霍斯加尔的委托？",
                "条目应保留 accept_confirmation_text。"
            );
            _test.True(entry.IsEnabled, "无前置锁定时条目应可接受。");
            _test.Eq(entry.DisabledReason, "", "可接受条目的 disabled_reason 应为空。");
            _test.True(entry.CostLabel.Contains("80"), "条目应渲染奖励标签。");
            _test.True(!string.IsNullOrEmpty(entry.SummaryText), "条目应渲染目标摘要。");
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferSkipsNonNpcProvider()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_skip_service",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "service_contract_board",
                            "service_contract_board",
                            "任务板",
                            "值守人员"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            GameRuntimeFacade.RuntimeCommandResult result =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "service_contract_board",
                    new GDictionary
                    {
                        ["interaction_script_id"] = "service_contract_board",
                        ["facility_name"] = "任务板",
                        ["npc_name"] = "值守人员",
                    }
                );

            _test.True(result.Ok, $"据点任务板动作应正常打开。message={result.Message}");
            _test.Eq(
                runtime._active_modal_kind,
                RuntimeModalKind.ContractBoard,
                "service_contract_board 不应被 NPC 分支截获。"
            );
            _test.Eq(
                runtime.GetActiveNpcQuestOfferData()?.Entries.Count ?? 0,
                0,
                "任务板打开时不应写入 NPC offer 上下文。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferRequiresNpcOfferChannel()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_channel",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_merchant_no_offer",
                            "npc_merchant_no_offer",
                            "杂货摊",
                            "旅行商人"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            GameRuntimeFacade.RuntimeCommandResult result =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_merchant_no_offer",
                    new GDictionary
                    {
                        ["interaction_script_id"] = "npc_merchant_no_offer",
                        ["facility_name"] = "杂货摊",
                        ["npc_name"] = "旅行商人",
                    }
                );

            _test.True(result.Ok, $"无 offer 的 NPC 动作应返回成功。message={result.Message}");
            _test.True(
                runtime._active_modal_kind != RuntimeModalKind.NpcQuestOffer,
                "未挂载 npc_offer 频道的 NPC 不应打开 NpcQuestOffer。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferRespectsAcceptRequirements()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_locked",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_elder_locked",
                            "npc_elder_locked",
                            "长老厅",
                            "长老"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            GameRuntimeFacade.RuntimeCommandResult result =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_elder_locked",
                    new GDictionary
                    {
                        ["interaction_script_id"] = "npc_elder_locked",
                        ["facility_name"] = "长老厅",
                        ["npc_name"] = "长老",
                    }
                );

            _test.True(result.Ok, $"有前置锁定的 NPC 动作应成功打开 offer。message={result.Message}");
            _test.Eq(
                runtime._active_modal_kind,
                RuntimeModalKind.NpcQuestOffer,
                "有前置锁定的 NPC 动作仍应打开 NpcQuestOffer。"
            );

            NpcQuestOfferWindowData windowData = runtime.GetActiveNpcQuestOfferData();
            NpcQuestOfferEntryData entry = windowData.Entries[0];
            _test.False(entry.IsEnabled, "未满足前置时条目应被禁用。");
            _test.True(
                !string.IsNullOrEmpty(entry.DisabledReason),
                "禁用条目应提供 disabled_reason。"
            );
            _test.Eq(
                entry.LockReasonId,
                "quest_not_completed",
                "禁用条目应暴露 lock_reason_id。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferAcceptsQuest()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_accept",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_blacksmith_hrothgar",
                            "npc_blacksmith_hrothgar",
                            "铁匠铺",
                            "霍斯加尔"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            GameRuntimeFacade.RuntimeCommandResult openResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["interaction_script_id"] = "npc_blacksmith_hrothgar",
                        ["facility_name"] = "铁匠铺",
                        ["npc_name"] = "霍斯加尔",
                    }
                );
            _test.True(openResult.Ok, $"打开 NPC offer 应成功。message={openResult.Message}");

            GameRuntimeFacade.RuntimeCommandResult firstSubmit =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_blacksmith_hrothgar_cave_beasts",
                    }
                );
            _test.True(
                firstSubmit.Ok,
                $"首次提交应进入确认状态。message={firstSubmit.Message}"
            );

            GameRuntimeFacade.RuntimeCommandResult secondSubmit =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_blacksmith_hrothgar_cave_beasts",
                        ["confirm_accept"] = true,
                    }
                );
            _test.True(
                secondSubmit.Ok,
                $"确认后提交应成功接受 NPC 委托。message={secondSubmit.Message}"
            );
            _test.Eq(
                secondSubmit.Message,
                "接受委托：洞穴野兽。",
                "成功接受应返回 accept_feedback_success。"
            );
            _test.True(
                runtime._party_state.HasActiveQuest("npc_blacksmith_hrothgar_cave_beasts"),
                "接受后任务应进入 active_quests。"
            );

            NpcQuestOfferWindowData windowData = runtime.GetActiveNpcQuestOfferData();
            _test.Eq(
                windowData.FeedbackText,
                "接受委托：洞穴野兽。",
                "窗口数据应保留成功反馈文本。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferSubmitsItemsAndClaimsReward()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_submit_claim",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_village_healer",
                            "npc_village_healer",
                            "篝烟灶",
                            "村医"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;
            PartyWarehouseService.WarehouseAddItemResult addResult =
                fixture.WarehouseService.AddItemTyped("healing_herb", 3);
            _test.Eq(addResult.AddedQuantity, 3, "NPC 提交物品回归应能准备三份 healing_herb。");
            _test.Eq(addResult.RemainingQuantity, 0, "准备药草时不应剩余未放入仓库的数量。");

            GameRuntimeFacade.RuntimeCommandResult openResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_village_healer",
                    new GDictionary
                    {
                        ["interaction_script_id"] = "npc_village_healer",
                        ["facility_name"] = "篝烟灶",
                        ["npc_name"] = "村医",
                    }
                );
            _test.True(openResult.Ok, $"村医委托应能打开。message={openResult.Message}");

            NpcQuestOfferEntryData entry = runtime
                .GetActiveNpcQuestOfferData()
                .Entries.FirstOrDefault(e => e.QuestId == "npc_village_healer_herbs");
            _test.True(entry != null, "村医面板应包含药草任务。");
            _test.Eq(entry?.StateId ?? "", "available", "未接取药草任务应处于 available。");
            _test.Eq(entry?.ActionLabel ?? "", "接受委托", "available 状态应显示接受动作。");

            GameRuntimeFacade.RuntimeCommandResult acceptResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_village_healer",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_village_healer_herbs",
                    }
                );
            _test.True(acceptResult.Ok, $"药草任务应能接取。message={acceptResult.Message}");
            entry = runtime
                .GetActiveNpcQuestOfferData()
                .Entries.FirstOrDefault(e => e.QuestId == "npc_village_healer_herbs");
            _test.Eq(entry?.StateId ?? "", "active", "接取后药草任务应处于 active。");
            _test.Eq(entry?.ActionLabel ?? "", "提交物品", "采集任务进行中应显示提交物品。");
            _test.True(entry?.IsEnabled ?? false, "采集任务进行中应允许尝试提交物品。");

            GameRuntimeFacade.RuntimeCommandResult submitResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_village_healer",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_village_healer_herbs",
                    }
                );
            _test.True(submitResult.Ok, $"三份药草应能提交。message={submitResult.Message}");
            _test.Eq(fixture.WarehouseService.CountItem("healing_herb"), 0, "提交应消耗三份药草。");
            _test.True(
                runtime._party_state.HasClaimableQuest("npc_village_healer_herbs"),
                "提交完成后药草任务应进入待领奖状态。"
            );
            entry = runtime
                .GetActiveNpcQuestOfferData()
                .Entries.FirstOrDefault(e => e.QuestId == "npc_village_healer_herbs");
            _test.Eq(entry?.StateId ?? "", "claimable", "提交后面板应刷新为 claimable。");
            _test.Eq(entry?.ActionLabel ?? "", "领取奖励", "claimable 状态应显示领取奖励。");

            GameRuntimeFacade.RuntimeCommandResult claimResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_village_healer",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_village_healer_herbs",
                    }
                );
            _test.True(claimResult.Ok, $"村医奖励应能领取。message={claimResult.Message}");
            _test.True(
                runtime._party_state.HasCompletedQuest("npc_village_healer_herbs"),
                "领奖后药草任务应进入 completed。"
            );
            _test.Eq(runtime._party_state.gold, 130, "领奖应发放任务配置的 30 金。");
            entry = runtime
                .GetActiveNpcQuestOfferData()
                .Entries.FirstOrDefault(e => e.QuestId == "npc_village_healer_herbs");
            _test.Eq(entry?.StateId ?? "", "completed", "领奖后面板应刷新为 completed。");
            _test.Eq(entry?.ActionLabel ?? "", "已完成", "completed 状态应显示已完成。");
            _test.False(entry?.IsEnabled ?? true, "completed 状态不应允许重复操作。");
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferConfirmationFlow()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_confirm",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_blacksmith_hrothgar",
                            "npc_blacksmith_hrothgar",
                            "铁匠铺",
                            "霍斯加尔"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            handler.CommandExecuteSettlementActionRuntimeTyped(
                "npc_blacksmith_hrothgar",
                new GDictionary
                {
                    ["interaction_script_id"] = "npc_blacksmith_hrothgar",
                    ["facility_name"] = "铁匠铺",
                    ["npc_name"] = "霍斯加尔",
                }
            );

            GameRuntimeFacade.RuntimeCommandResult firstSubmit =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_blacksmith_hrothgar_cave_beasts",
                    }
                );
            _test.True(
                firstSubmit.Ok,
                $"首次提交应进入确认状态。message={firstSubmit.Message}"
            );
            _test.Eq(
                firstSubmit.Message,
                "请确认是否接受该委托。",
                "首次提交应返回确认提示。"
            );

            NpcQuestOfferWindowData context = runtime.GetActiveNpcQuestOfferData();
            _test.Eq(
                context.PendingConfirmationQuestId,
                "npc_blacksmith_hrothgar_cave_beasts",
                "上下文应记录待确认 quest_id。"
            );
            _test.Eq(
                context.PendingConfirmationSource,
                "npc_quest_offer",
                "上下文应记录 npc_quest_offer 确认来源。"
            );

            GameRuntimeFacade.RuntimeCommandResult resubmitWithoutConfirm =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_blacksmith_hrothgar_cave_beasts",
                    }
                );
            _test.True(
                resubmitWithoutConfirm.Ok,
                $"pending 后未带 confirm_accept 应再次提示。message={resubmitWithoutConfirm.Message}"
            );
            _test.Eq(
                resubmitWithoutConfirm.Message,
                "请确认是否接受该委托。",
                "pending 后未带 confirm_accept 应返回确认提示。"
            );

            GameRuntimeFacade.RuntimeCommandResult secondSubmit =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_blacksmith_hrothgar_cave_beasts",
                        ["confirm_accept"] = true,
                    }
                );
            _test.True(
                secondSubmit.Ok,
                $"二次提交应成功接受委托。message={secondSubmit.Message}"
            );
            _test.True(
                runtime._party_state.HasActiveQuest("npc_blacksmith_hrothgar_cave_beasts"),
                "确认后任务应进入 active_quests。"
            );

            NpcQuestOfferWindowData clearedContext = runtime.GetActiveNpcQuestOfferData();
            _test.Eq(
                clearedContext.PendingConfirmationQuestId,
                "",
                "接受后待确认 quest_id 应被清除。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferRejectsLockedQuest()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_reject_locked",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_elder_locked",
                            "npc_elder_locked",
                            "长老厅",
                            "长老"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            handler.CommandExecuteSettlementActionRuntimeTyped(
                "npc_elder_locked",
                new GDictionary
                {
                    ["interaction_script_id"] = "npc_elder_locked",
                    ["facility_name"] = "长老厅",
                    ["npc_name"] = "长老",
                }
            );

            GameRuntimeFacade.RuntimeCommandResult submitResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_elder_locked",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_elder_secret",
                    }
                );

            _test.False(submitResult.Ok, "未满足前置的委托提交应失败。");
            _test.Eq(
                submitResult.Message,
                "你还没有获得长老的信任。",
                "失败应返回 accept_feedback_failure。"
            );
            _test.False(
                runtime._party_state.HasActiveQuest("npc_elder_secret"),
                "失败后任务不应进入 active_quests。"
            );

            NpcQuestOfferWindowData windowData = runtime.GetActiveNpcQuestOfferData();
            _test.Eq(
                windowData.FeedbackText,
                "你还没有获得长老的信任。",
                "窗口数据应保留失败反馈文本。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferRejectsSubmissionWithoutModal()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_no_modal",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_blacksmith_hrothgar",
                            "npc_blacksmith_hrothgar",
                            "铁匠铺",
                            "霍斯加尔"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;

            GameRuntimeFacade.RuntimeCommandResult submitResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_blacksmith_hrothgar_cave_beasts",
                    }
                );

            _test.False(submitResult.Ok, "未打开 offer 时提交应失败。");
            _test.Eq(
                submitResult.Message,
                "当前没有打开 NPC 委托面板。",
                "失败应提示当前没有打开 NPC 委托面板。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferRejectsWrongSettlement()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_wrong_settlement",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_blacksmith_hrothgar",
                            "npc_blacksmith_hrothgar",
                            "铁匠铺",
                            "霍斯加尔"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            handler.CommandExecuteSettlementActionRuntimeTyped(
                "npc_blacksmith_hrothgar",
                new GDictionary
                {
                    ["interaction_script_id"] = "npc_blacksmith_hrothgar",
                    ["facility_name"] = "铁匠铺",
                    ["npc_name"] = "霍斯加尔",
                }
            );

            runtime.SetActiveSettlementId("other_settlement");
            GameRuntimeFacade.RuntimeCommandResult submitResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_blacksmith_hrothgar_cave_beasts",
                    }
                );

            _test.False(submitResult.Ok, "据点不一致时提交应失败。");
            _test.Eq(
                submitResult.Message,
                "当前 NPC 委托面板与请求的据点不一致。",
                "失败应提示据点不一致。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferRejectsWrongAction()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_wrong_action",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_blacksmith_hrothgar",
                            "npc_blacksmith_hrothgar",
                            "铁匠铺",
                            "霍斯加尔"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;

            handler.CommandExecuteSettlementActionRuntimeTyped(
                "npc_blacksmith_hrothgar",
                new GDictionary
                {
                    ["interaction_script_id"] = "npc_blacksmith_hrothgar",
                    ["facility_name"] = "铁匠铺",
                    ["npc_name"] = "霍斯加尔",
                }
            );

            GameRuntimeFacade.RuntimeCommandResult submitResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_other_service",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_blacksmith_hrothgar_cave_beasts",
                    }
                );

            _test.False(submitResult.Ok, "服务入口不一致时提交应失败。");
            _test.Eq(
                submitResult.Message,
                "当前 NPC 委托面板与请求的服务入口不一致。",
                "失败应提示服务入口不一致。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferFallsBackWhenNoMatchingQuests()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_no_match",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_no_quests",
                            "npc_no_quests",
                            "空屋",
                            "无名人"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            GameRuntimeFacade.RuntimeCommandResult result =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_no_quests",
                    new GDictionary
                    {
                        ["interaction_script_id"] = "npc_no_quests",
                        ["facility_name"] = "空屋",
                        ["npc_name"] = "无名人",
                    }
                );

            _test.True(result.Ok, $"无匹配任务时应返回默认成功。message={result.Message}");
            _test.True(
                runtime._active_modal_kind != RuntimeModalKind.NpcQuestOffer,
                "无匹配任务时不应打开 NpcQuestOffer。"
            );
            _test.Eq(
                runtime.GetActiveNpcQuestOfferData()?.Entries.Count ?? 0,
                0,
                "无匹配任务时不应写入 NPC offer 上下文。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferMultipleQuests()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_multi",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_blacksmith_hrothgar",
                            "npc_blacksmith_hrothgar",
                            "铁匠铺",
                            "霍斯加尔"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            handler.CommandExecuteSettlementActionRuntimeTyped(
                "npc_blacksmith_hrothgar",
                new GDictionary
                {
                    ["interaction_script_id"] = "npc_blacksmith_hrothgar",
                    ["facility_name"] = "铁匠铺",
                    ["npc_name"] = "霍斯加尔",
                }
            );

            NpcQuestOfferWindowData windowData = runtime.GetActiveNpcQuestOfferData();
            _test.Eq(windowData.Entries.Count, 2, "NPC 应展示两个可接任务。");
            _test.Eq(
                windowData.SelectedQuestId,
                "npc_blacksmith_hrothgar_cave_beasts",
                "默认应选中首个任务。"
            );

            GameRuntimeFacade.RuntimeCommandResult submitResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_blacksmith_hrothgar_iron_delivery",
                    }
                );
            _test.True(
                submitResult.Ok,
                $"应能接受第二个任务。message={submitResult.Message}"
            );
            _test.True(
                runtime._party_state.HasActiveQuest("npc_blacksmith_hrothgar_iron_delivery"),
                "第二个任务应进入 active_quests。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferRejectsMissingQuestId()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_missing_id",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_blacksmith_hrothgar",
                            "npc_blacksmith_hrothgar",
                            "铁匠铺",
                            "霍斯加尔"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;

            handler.CommandExecuteSettlementActionRuntimeTyped(
                "npc_blacksmith_hrothgar",
                new GDictionary
                {
                    ["interaction_script_id"] = "npc_blacksmith_hrothgar",
                    ["facility_name"] = "铁匠铺",
                    ["npc_name"] = "霍斯加尔",
                }
            );

            GameRuntimeFacade.RuntimeCommandResult submitResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                    }
                );

            _test.False(submitResult.Ok, "缺少 quest_id 时应失败。");
            _test.Eq(
                submitResult.Message,
                "NPC 委托提交缺少 quest_id。",
                "失败应提示缺少 quest_id。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferRejectsWrongNpcQuest()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_wrong_npc",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_blacksmith_hrothgar",
                            "npc_blacksmith_hrothgar",
                            "铁匠铺",
                            "霍斯加尔"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;

            handler.CommandExecuteSettlementActionRuntimeTyped(
                "npc_blacksmith_hrothgar",
                new GDictionary
                {
                    ["interaction_script_id"] = "npc_blacksmith_hrothgar",
                    ["facility_name"] = "铁匠铺",
                    ["npc_name"] = "霍斯加尔",
                }
            );

            GameRuntimeFacade.RuntimeCommandResult submitResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_merchant_no_offer_quest",
                    }
                );

            _test.False(submitResult.Ok, "不属于当前 NPC 的任务应被拒绝。");
            _test.Eq(
                submitResult.Message,
                "该任务不属于当前 NPC。",
                "失败应提示任务不属于当前 NPC。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferRejectsWrongListingChannel()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_wrong_channel",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_blacksmith_hrothgar",
                            "npc_blacksmith_hrothgar",
                            "铁匠铺",
                            "霍斯加尔"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;

            handler.CommandExecuteSettlementActionRuntimeTyped(
                "npc_blacksmith_hrothgar",
                new GDictionary
                {
                    ["interaction_script_id"] = "npc_blacksmith_hrothgar",
                    ["facility_name"] = "铁匠铺",
                    ["npc_name"] = "霍斯加尔",
                }
            );

            GameRuntimeFacade.RuntimeCommandResult submitResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_blacksmith_hrothgar_contract_only",
                    }
                );

            _test.False(submitResult.Ok, "未配置 npc_offer 渠道的任务应被拒绝。");
            _test.Eq(
                submitResult.Message,
                "该任务未配置为 NPC 委托。",
                "失败应提示任务未配置为 NPC 委托。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferRejectsNonNpcProviderKind()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_wrong_kind",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_blacksmith_hrothgar",
                            "npc_blacksmith_hrothgar",
                            "铁匠铺",
                            "霍斯加尔"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;

            handler.CommandExecuteSettlementActionRuntimeTyped(
                "npc_blacksmith_hrothgar",
                new GDictionary
                {
                    ["interaction_script_id"] = "npc_blacksmith_hrothgar",
                    ["facility_name"] = "铁匠铺",
                    ["npc_name"] = "霍斯加尔",
                }
            );

            GameRuntimeFacade.RuntimeCommandResult submitResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_blacksmith_hrothgar_service_kind",
                    }
                );

            _test.False(submitResult.Ok, "provider_kind 不是 npc 的任务应被拒绝。");
            _test.Eq(
                submitResult.Message,
                "该任务不是 NPC 委托。",
                "失败应提示该任务不是 NPC 委托。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferCloseModalLifecycle()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_close",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_blacksmith_hrothgar",
                            "npc_blacksmith_hrothgar",
                            "铁匠铺",
                            "霍斯加尔"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            handler.CommandExecuteSettlementActionRuntimeTyped(
                "npc_blacksmith_hrothgar",
                new GDictionary
                {
                    ["interaction_script_id"] = "npc_blacksmith_hrothgar",
                    ["facility_name"] = "铁匠铺",
                    ["npc_name"] = "霍斯加尔",
                }
            );

            handler.OnNpcQuestOfferWindowClosed();

            _test.Eq(
                runtime._active_modal_kind,
                RuntimeModalKind.Settlement,
                "关闭 NPC offer 后应返回据点服务。"
            );
            _test.True(
                runtime.GetActiveNpcQuestOfferData() == null,
                "关闭后 NPC offer 上下文应被清除。"
            );

            GameRuntimeFacade.RuntimeCommandResult submitResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_blacksmith_hrothgar_cave_beasts",
                    }
                );
            _test.False(submitResult.Ok, "关闭后提交应失败。");
            _test.Eq(
                submitResult.Message,
                "当前没有打开 NPC 委托面板。",
                "关闭后提交应提示没有打开面板。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferRefreshesEntriesAfterAccept()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_refresh",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_blacksmith_hrothgar",
                            "npc_blacksmith_hrothgar",
                            "铁匠铺",
                            "霍斯加尔"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;
            GameRuntimeFacade runtime = fixture.Runtime;

            handler.CommandExecuteSettlementActionRuntimeTyped(
                "npc_blacksmith_hrothgar",
                new GDictionary
                {
                    ["interaction_script_id"] = "npc_blacksmith_hrothgar",
                    ["facility_name"] = "铁匠铺",
                    ["npc_name"] = "霍斯加尔",
                }
            );

            handler.CommandExecuteSettlementActionRuntimeTyped(
                "npc_blacksmith_hrothgar",
                new GDictionary
                {
                    ["submission_source"] = "npc_quest_offer",
                    ["quest_id"] = "npc_blacksmith_hrothgar_cave_beasts",
                }
            );
            handler.CommandExecuteSettlementActionRuntimeTyped(
                "npc_blacksmith_hrothgar",
                new GDictionary
                {
                    ["submission_source"] = "npc_quest_offer",
                    ["quest_id"] = "npc_blacksmith_hrothgar_cave_beasts",
                    ["confirm_accept"] = true,
                }
            );

            NpcQuestOfferWindowData windowData = runtime.GetActiveNpcQuestOfferData();
            _test.Eq(
                windowData.FeedbackText,
                "接受委托：洞穴野兽。",
                "接受后窗口数据应保留成功反馈。"
            );
            _test.Eq(windowData.Entries.Count, 2, "刷新后条目列表应保持完整。");
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task TestNpcQuestOfferRejectsConfirmBypass()
    {
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs = BuildNpcQuestDefs();
        RuntimeFixture fixture = await BuildRuntimeFixture(
            "npc_confirm_bypass",
            BuildPartyState(12, 100),
            new[]
            {
                BuildSettlementRecord(
                    "spring_village_01",
                    "春泉村",
                    Vector2I.Zero,
                    new GArray
                    {
                        BuildNpcServiceEntry(
                            "npc_blacksmith_hrothgar",
                            "npc_blacksmith_hrothgar",
                            "铁匠铺",
                            "霍斯加尔"
                        ),
                    }
                ),
            },
            questDefs
        );
        try
        {
            GameRuntimeSettlementCommandHandler handler = fixture.Handler;

            handler.CommandExecuteSettlementActionRuntimeTyped(
                "npc_blacksmith_hrothgar",
                new GDictionary
                {
                    ["interaction_script_id"] = "npc_blacksmith_hrothgar",
                    ["facility_name"] = "铁匠铺",
                    ["npc_name"] = "霍斯加尔",
                }
            );

            GameRuntimeFacade.RuntimeCommandResult submitResult =
                handler.CommandExecuteSettlementActionRuntimeTyped(
                    "npc_blacksmith_hrothgar",
                    new GDictionary
                    {
                        ["submission_source"] = "npc_quest_offer",
                        ["quest_id"] = "npc_blacksmith_hrothgar_cave_beasts",
                        ["confirm_accept"] = true,
                    }
                );

            _test.False(submitResult.Ok, "未 pending 时直接 confirm_accept 应失败。");
            _test.Eq(
                submitResult.Message,
                "该委托需要先在面板中确认。",
                "失败应提示需要先确认。"
            );
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private async Task<RuntimeFixture> BuildRuntimeFixture(
        string suffix,
        PartyState partyState,
        IReadOnlyList<GDictionary> settlements,
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs
    )
    {
        IReadOnlyDictionary<StringName, QuestDefinition> contentQuestDefs =
            questDefs ?? new Dictionary<StringName, QuestDefinition>();
        GameSession gameSession = await InstallGameSession(
            $"NpcQuestOfferGameSession_{suffix}",
            contentQuestDefs
        );
        GDictionary worldData = BuildWorldData(settlements);
        ConfigureSessionForRuntimeTest(
            gameSession,
            $"npc_quest_offer_{suffix}",
            worldData,
            partyState
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
            gameSession._generation_definition,
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
        PartyState partyState
    )
    {
        gameSession.ConfigureRuntimeWorldForTests(
            saveId,
            TestConfigPath,
            worldData,
            partyState,
            "npc_quest_offer_test",
            "NPC Quest Offer Test",
            new Vector2I(8, 8),
            TestWorldGenerationDefinitionFactory.Load(TestConfigPath)
        );
    }

    private async Task<GameSession> InstallGameSession(
        string nodeName,
        IReadOnlyDictionary<StringName, QuestDefinition> questDefs
    )
    {
        foreach (Node child in Root.GetChildren())
        {
            if (child.Name == nodeName)
            {
                child.QueueFree();
            }
        }
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        GameSession gameSession = GameSessionTestFactory.CreateSyntheticFromProcessSnapshot(
            seed => seed.Quests = questDefs
        );
        gameSession.Name = nodeName;
        Root.AddChild(gameSession);
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
        return gameSession;
    }

    private async Task DisposeFixture(RuntimeFixture fixture)
    {
        fixture.GameSession?.DiscardPendingSave();
        fixture.Runtime?.Dispose();
        await DisposeGameSession(fixture.GameSession, "清理 NPC quest offer 验证存档应成功。");
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

    private static GDictionary BuildSettlementRecord(
        string settlementId,
        string displayName,
        Vector2I origin,
        GArray services
    )
    {
        return MinimalSettlementRecord(settlementId, displayName, origin, services);
    }

    private static GDictionary MinimalSettlementRecord(
        string settlementId,
        string displayName,
        Vector2I origin,
        GArray services
    )
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
            ["shop_states"] = new GDictionary(),
        };
    }

    private static GDictionary BuildNpcServiceEntry(
        string actionId,
        string interactionScriptId,
        string facilityName,
        string npcName
    )
    {
        return new GDictionary
        {
            ["action_id"] = actionId,
            ["interaction_script_id"] = interactionScriptId,
            ["facility_id"] = "",
            ["facility_name"] = facilityName,
            ["npc_id"] = "",
            ["npc_name"] = npcName,
            ["service_type"] = "npc_quest",
        };
    }

    private static IReadOnlyDictionary<StringName, QuestDefinition> BuildNpcQuestDefs()
    {
        var questDefinitions = new Dictionary<StringName, QuestDefinition>();

        QuestDefinition normalQuest = BuildQuestDefinition(
            "npc_blacksmith_hrothgar_cave_beasts",
            "洞穴野兽",
            "峡谷北面的野兽又在骚扰商队。",
            "npc_blacksmith_hrothgar",
            new QuestObjectiveDefinition[]
            {
                BuildObjective("defeat_cave_beasts", "defeat_enemy", "wolf_pack", 1),
            },
            new QuestRewardDefinition[] { BuildGoldReward(80) },
            "npc",
            new StringName[] { "npc_offer" },
            System.Array.Empty<QuestAcceptRequirementDefinition>(),
            false,
            "帮我清理峡谷北面的野兽。",
            "接受委托：洞穴野兽。",
            "",
            "接受霍斯加尔的委托？"
        );
        questDefinitions[normalQuest.QuestId] = normalQuest;

        QuestDefinition noOfferQuest = BuildQuestDefinition(
            "npc_merchant_no_offer_quest",
            "旅行商人的烦恼",
            "没有挂在 npc_offer 频道的任务。",
            "npc_merchant_no_offer",
            new QuestObjectiveDefinition[]
            {
                BuildObjective(
                    "deliver_goods",
                    "settlement_action",
                    "service:warehouse",
                    1
                ),
            },
            new QuestRewardDefinition[] { BuildGoldReward(10) },
            "npc",
            new StringName[] { "contract_board" },
            System.Array.Empty<QuestAcceptRequirementDefinition>()
        );
        questDefinitions[noOfferQuest.QuestId] = noOfferQuest;

        QuestDefinition healerQuest = BuildQuestDefinition(
            "npc_village_healer_herbs",
            "采集药材",
            "为村医提交三份药草。",
            "npc_village_healer",
            new QuestObjectiveDefinition[]
            {
                BuildObjective("gather_herbs", "submit_item", "healing_herb", 3),
            },
            new QuestRewardDefinition[] { BuildGoldReward(30) },
            "npc",
            new StringName[] { "npc_offer" },
            System.Array.Empty<QuestAcceptRequirementDefinition>(),
            false,
            "带三份药草回来。",
            "村医请你采集三份药草。"
        );
        questDefinitions[healerQuest.QuestId] = healerQuest;

        QuestDefinition lockedQuest = BuildQuestDefinition(
            "npc_elder_secret",
            "长老的秘密",
            "需要先完成洞穴野兽委托。",
            "npc_elder_locked",
            new QuestObjectiveDefinition[]
            {
                BuildObjective("elder_secret", "defeat_enemy", "shadow_cultist", 1),
            },
            new QuestRewardDefinition[] { BuildGoldReward(120) },
            "npc",
            new StringName[] { "npc_offer" },
            new QuestAcceptRequirementDefinition[]
            {
                new(
                    "quest_completed",
                    "npc_blacksmith_hrothgar_cave_beasts"
                ),
            },
            false,
            "",
            "",
            "你还没有获得长老的信任。"
        );
        questDefinitions[lockedQuest.QuestId] = lockedQuest;

        QuestDefinition secondQuest = BuildQuestDefinition(
            "npc_blacksmith_hrothgar_iron_delivery",
            "铁料运送",
            "把一批铁料送到村口哨塔。",
            "npc_blacksmith_hrothgar",
            new QuestObjectiveDefinition[]
            {
                BuildObjective(
                    "deliver_iron",
                    "settlement_action",
                    "service:warehouse",
                    1
                ),
            },
            new QuestRewardDefinition[] { BuildGoldReward(50) },
            "npc",
            new StringName[] { "npc_offer" },
            System.Array.Empty<QuestAcceptRequirementDefinition>()
        );
        questDefinitions[secondQuest.QuestId] = secondQuest;

        QuestDefinition contractOnlyQuest = BuildQuestDefinition(
            "npc_blacksmith_hrothgar_contract_only",
            "铁匠的契约",
            "这是一个只挂在契约板的任务，用于测试错误渠道拒绝。",
            "npc_blacksmith_hrothgar",
            new QuestObjectiveDefinition[]
            {
                BuildObjective("contract_obj", "defeat_enemy", "wolf_pack", 1),
            },
            new QuestRewardDefinition[] { BuildGoldReward(30) },
            "npc",
            new StringName[] { "contract_board" },
            System.Array.Empty<QuestAcceptRequirementDefinition>()
        );
        questDefinitions[contractOnlyQuest.QuestId] = contractOnlyQuest;

        QuestDefinition serviceKindQuest = BuildQuestDefinition(
            "npc_blacksmith_hrothgar_service_kind",
            "铁匠的服务任务",
            "provider_kind 不是 npc，用于测试 provider_kind 拒绝。",
            "npc_blacksmith_hrothgar",
            new QuestObjectiveDefinition[]
            {
                BuildObjective("service_obj", "defeat_enemy", "wolf_pack", 1),
            },
            new QuestRewardDefinition[] { BuildGoldReward(30) },
            "service_contract_board",
            new StringName[] { "npc_offer" },
            System.Array.Empty<QuestAcceptRequirementDefinition>()
        );
        questDefinitions[serviceKindQuest.QuestId] = serviceKindQuest;

        return questDefinitions;
    }

    private static QuestDefinition BuildQuestDefinition(
        string questId,
        string displayName,
        string description,
        string providerInteractionId,
        IReadOnlyList<QuestObjectiveDefinition> objectives,
        IReadOnlyList<QuestRewardDefinition> rewards,
        StringName providerKind,
        IReadOnlyList<StringName> listingChannels,
        IReadOnlyList<QuestAcceptRequirementDefinition> acceptRequirements,
        bool isRepeatable = false,
        string acceptDialogueText = "",
        string acceptFeedbackSuccess = "",
        string acceptFeedbackFailure = "",
        string acceptConfirmationText = ""
    )
    {
        return new QuestDefinition(
            questId,
            displayName,
            description,
            providerInteractionId,
            System.Array.Empty<StringName>(),
            acceptRequirements,
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

    private static QuestObjectiveDefinition BuildObjective(
        string objectiveId,
        string objectiveType,
        string targetId,
        int targetValue
    ) => new(objectiveId, objectiveType, targetId, targetValue);

    private static QuestRewardDefinition BuildGoldReward(int amount) =>
        new(
            "gold",
            amount,
            "",
            0,
            "",
            System.Array.Empty<QuestPendingRewardEntryDefinition>()
        );

    private static PartyState BuildPartyState(int storageSpace, int gold)
    {
        var partyState = new PartyState
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new StringNameList { "hero" },
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

    private static void MakeVisible(GameRuntimeFacade runtime, Vector2I center)
    {
        runtime._fog_system.RebuildVisibilityForFaction(
            "player",
            new[] { new VisionSourceData("npc_quest_offer_visibility", center, 6, "player") }
        );
    }

    private static string DictString(GDictionary dict, string key, string defaultValue = "")
    {
        if (dict == null || !dict.ContainsKey(key))
            return defaultValue;
        return dict[key].AsString();
    }

    private static IEnumerable<GDictionary> Dictionaries(GArray array)
    {
        if (array == null)
            yield break;
        foreach (Variant value in array)
        {
            if (value.VariantType == Variant.Type.Dictionary)
                yield return value.AsGodotDictionary();
        }
    }

    private sealed class RuntimeFixture
    {
        public GameRuntimeFacade Runtime { get; }
        public GameSession GameSession { get; }
        public GameRuntimeSettlementCommandHandler Handler { get; }
        public PartyWarehouseService WarehouseService { get; }

        public RuntimeFixture(
            GameRuntimeFacade runtime,
            GameSession gameSession,
            GameRuntimeSettlementCommandHandler handler,
            PartyWarehouseService warehouseService
        )
        {
            Runtime = runtime;
            GameSession = gameSession;
            Handler = handler;
            WarehouseService = warehouseService;
        }
    }
}
