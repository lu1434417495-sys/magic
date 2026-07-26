using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_game_runtime_reward_flow_handler_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestFacadeUsesRewardFlowHandlerSurface();
        TestRewardHandlerRoutesModalCloseAndRewardPresentation();
        TestNonCloseModalTransitionsClearCharacterInfoContext();

        RequestTestExit(_test.Finish("Game runtime reward flow handler regression"));
    }

    private void TestFacadeUsesRewardFlowHandlerSurface()
    {
        GameRuntimeFacade runtime = BuildRuntime(BuildPartyState());
        try
        {
            runtime.SetPendingWorldPromotionPromptState(BuildPromotionPrompt("hero"));
            IReadOnlyDictionary<string, object> promotionPrompt =
                runtime.GetCurrentPromotionPromptSnapshotPlain();
            _test.Eq(
                PlainString(promotionPrompt, "member_id", ""),
                "hero",
                "GetCurrentPromotionPromptSnapshotPlain() 应返回正式 plain prompt。"
            );
            _test.True(runtime.PresentPendingRewardIfReady(), "PresentPendingRewardIfReady() 应通过 reward handler 打开 promotion modal。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Promotion, "存在 world promotion prompt 时应切换到 promotion modal。");

            RuntimeCommandResult missingChoiceResult =
                runtime.CommandChoosePromotionTyped("warrior");
            _test.False(missingChoiceResult.Ok, "command_choose_promotion() 应委托给正式 reward handler 并拒绝不存在的职业。");
            _test.Eq(missingChoiceResult.Message, "当前晋升列表中不存在职业 warrior。", "缺失晋升选项应返回正式错误文案。");

            RuntimeCommandResult cancelResult = runtime.CommandCancelPromotionChoiceTyped();
            _test.True(
                cancelResult.Ok,
                "cancel_promotion_choice() 应委托给正式 reward handler。"
            );
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Promotion, "world promotion 取消后仍应停留在 promotion modal。");
            _test.Eq(runtime.GetStatusText(), "当前晋升选择必须确认后才能继续结算奖励。", "world promotion 取消应刷新正式状态文案。");

            runtime.ClearPendingWorldPromotionPromptState();
            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.None);
            RuntimeCommandResult confirmMissingReward =
                runtime.CommandConfirmPendingRewardTyped();
            _test.False(confirmMissingReward.Ok, "command_confirm_pending_reward() 应委托给 reward handler 并拒绝空奖励。");
            _test.Eq(confirmMissingReward.Message, "当前没有待确认的角色奖励。", "空奖励确认应返回正式错误文案。");

            runtime.SetActiveCharacterInfoContext(BuildCharacterInfoContext("测试人物"));
            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.CharacterInfo);
            RuntimeCommandResult closeResult =
                runtime.CommandCloseActiveModalTyped();
            _test.True(closeResult.Ok, "command_close_active_modal() 应委托给 reward handler。");
            using GodotProjectionLease<GDictionary> characterInfoLease =
                runtime.GetCharacterInfoContextLease();
            _test.Eq(characterInfoLease.Value.Count, 0, "character_info 关闭应清空人物信息上下文。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.None, "character_info 关闭后应清空 modal。");
            _test.Eq(runtime.GetStatusText(), "已关闭人物信息窗。", "character_info 关闭应刷新状态文案。");
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestRewardHandlerRoutesModalCloseAndRewardPresentation()
    {
        PartyState partyState = BuildPartyState();
        GameRuntimeFacade runtime = BuildRuntime(partyState);
        GameRuntimeRewardFlowHandler handler = runtime._reward_flow_handler;
        try
        {
            PendingCharacterReward reward = BuildPendingReward();
            partyState.pending_character_rewards.Add(reward);
            _test.True(handler.PresentPendingRewardIfReady(), "存在待领奖励时应进入 reward modal。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Reward, "奖励弹窗应切换 modal 到 reward。");
            _test.True(runtime.GetActiveReward() == reward, "奖励呈现应把队首奖励提为 active reward。");

            _test.False(handler.PresentPendingRewardIfReady(), "已经处于 reward modal 时不应重复呈现奖励。");

            runtime.ClearActiveRewardState();
            partyState.pending_character_rewards.Clear();

            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Settlement);
            runtime.SetActiveSettlementId("spring_village_01");
            runtime.SetSettlementFeedbackText("测试反馈");
            RuntimeCommandResult settlementClose =
                handler.CommandCloseActiveModalTyped();
            _test.True(settlementClose.Ok, "settlement modal 应可通过 reward handler 路由关闭。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.None, "settlement close 后应清空 modal。");
            _test.Eq(runtime.GetActiveSettlementId(), "", "settlement close 后应清空当前据点。");
            _test.Eq(runtime.GetSettlementFeedbackText(), "", "settlement close 后应清空反馈文本。");
            _test.Eq(runtime.GetStatusText(), "已关闭据点窗口，返回世界地图。", "settlement close 应刷新正式状态文案。");

            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Warehouse);
            runtime.SetActiveWarehouseEntryLabel("队伍管理");
            RuntimeCommandResult warehouseClose =
                handler.CommandCloseActiveModalTyped();
            _test.True(warehouseClose.Ok, "warehouse modal 应可通过 reward handler 路由关闭。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.None, "warehouse close 后应清空 modal。");
            _test.Eq(runtime.GetActiveWarehouseEntryLabel(), "", "warehouse close 后应清空仓库入口标签。");
            _test.Eq(runtime.GetStatusText(), "已关闭共享仓库。", "warehouse close 应刷新正式状态文案。");

            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Party);
            RuntimeCommandResult partyClose =
                handler.CommandCloseActiveModalTyped();
            _test.True(partyClose.Ok, "party modal 应可通过 reward handler 路由关闭。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.None, "party close 后应清空 modal。");
            _test.Eq(runtime.GetStatusText(), "已关闭队伍管理窗口。", "party close 应刷新正式状态文案。");

            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.SubmapConfirm);
            runtime.GetPendingSubmapPromptState().Set(
                "",
                "",
                Vector2I.Zero,
                "ancient_tower",
                "古塔",
                "",
                ""
            );
            RuntimeCommandResult submapClose =
                handler.CommandCloseActiveModalTyped();
            _test.True(submapClose.Ok, "submap_confirm modal 应可通过 reward handler 路由取消。");
            _test.Eq(runtime.GetPendingSubmapPrompt().Count, 0, "submap_confirm close 后应清空 pending submap prompt。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.None, "submap_confirm close 后应清空 modal。");
            _test.Eq(runtime.GetStatusText(), "已取消进入 古塔。", "submap_confirm close 应刷新正式状态文案。");

            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Reward);
            RuntimeCommandResult rewardClose =
                handler.CommandCloseActiveModalTyped();
            _test.False(rewardClose.Ok, "reward modal 不能被普通关闭命令跳过。");
            _test.Eq(rewardClose.Message, "当前角色奖励必须确认后才能继续。", "reward modal 普通关闭应返回正式错误文案。");

            runtime.SetActiveRewardState(BuildPendingReward());
            RuntimeCommandResult confirmActiveReward =
                handler.CommandConfirmActiveRewardTyped();
            _test.True(confirmActiveReward.Ok, "active reward 应能通过 typed confirm helper 结算。");
            _test.True(runtime.GetActiveReward() == null, "confirm active reward 后应清空 active reward。");

            runtime.SetPendingWorldPromotionPromptState(BuildPromotionPrompt("hero"));
            RuntimeCommandResult cancelPromotionChoice =
                handler.CommandCancelPromotionChoiceTyped();
            _test.True(cancelPromotionChoice.Ok, "cancel promotion choice 应走 typed helper。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Promotion, "cancel promotion choice 后应仍停留在 promotion modal。");
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestNonCloseModalTransitionsClearCharacterInfoContext()
    {
        GameRuntimeFacade runtime = BuildRuntime(BuildPartyState());
        try
        {
            runtime.SetActiveCharacterInfoContext(BuildCharacterInfoContext("战斗单位"));
            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.CharacterInfo);
            _test.True(
                runtime.GetCharacterInfoContextSnapshotPlain().Count > 0,
                "测试前置：character_info context 应已写入。"
            );
            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Promotion);

            using (
                GodotProjectionLease<GDictionary> promotionLease =
                    runtime.GetCharacterInfoContextLease()
            )
            {
                _test.Eq(
                    promotionLease.Value.Count,
                    0,
                    "promotion 覆盖 character_info 时应清空隐藏的人物信息上下文。"
                );
            }
            _test.Eq(
                runtime.GetActiveModalKind(),
                RuntimeModalKind.Promotion,
                "清空 character_info context 不应改变目标 promotion modal。"
            );

            runtime.SetActiveCharacterInfoContext(BuildCharacterInfoContext("已结算战斗单位"));
            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.CharacterInfo);
            _test.True(
                runtime.GetCharacterInfoContextSnapshotPlain().Count > 0,
                "测试前置：battle resolution 前应存在 character_info context。"
            );
            runtime.ClearResolvedBattleRuntimeContext();

            using (
                GodotProjectionLease<GDictionary> resolutionLease =
                    runtime.GetCharacterInfoContextLease()
            )
            {
                _test.Eq(
                    resolutionLease.Value.Count,
                    0,
                    "battle resolution 应清空仍打开的人物信息上下文。"
                );
            }
            _test.Eq(
                runtime.GetActiveModalKind(),
                RuntimeModalKind.None,
                "battle resolution 清理后应回到 none modal。"
            );
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static GameRuntimeCharacterInfoContext BuildCharacterInfoContext(
        string displayName
    ) =>
        new(
            GameRuntimeCharacterInfoSource.World,
            displayName,
            "测试人物",
            "可见提示单位",
            new[]
            {
                new GameRuntimeCharacterInfoSection(
                    "基础概览",
                    new[] { GameRuntimeCharacterInfoEntry.Pair("类型", "测试") }
                ),
            }
        );

    private static GameRuntimePromotionPromptContext BuildPromotionPrompt(StringName memberId) =>
        new(
            memberId,
            memberId.ToString(),
            new[]
            {
                new GameRuntimePromotionChoiceContext(
                    "test_profession",
                    "Test Profession",
                    "Rank 1",
                    "",
                    System.Array.Empty<StringName>(),
                    "",
                    PromotionSelectionData.Empty
                ),
            }
        );

    private static GameRuntimeFacade BuildRuntime(PartyState partyState)
    {
        var runtime = new GameRuntimeFacade
        {
            _party_state = partyState,
        };
        runtime._settlement_command_handler.SetupRuntime(runtime);
        runtime._warehouse_handler.Setup(runtime);
        runtime._party_command_handler.Setup(runtime);
        runtime._reward_flow_handler.Setup(runtime);
        return runtime;
    }

    private static string PlainString(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        string fallback
    ) =>
        dictionary != null
        && dictionary.TryGetValue(key, out object value)
        && value is string text
            ? text
            : fallback;

    private static PartyState BuildPartyState()
    {
        var partyState = new PartyState
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new Godot.Collections.Array<StringName> { "hero" },
        };
        var hero = new PartyMemberState
        {
            member_id = "hero",
            display_name = "Hero",
            current_hp = 20,
            current_mp = 4,
        };
        hero.progression = new UnitProgress
        {
            unit_id = "hero",
            display_name = "Hero",
        };
        partyState.SetMemberState(hero);
        return partyState;
    }

    private static PendingCharacterReward BuildPendingReward()
    {
        var entry = new PendingCharacterRewardEntry
        {
            entry_type = "skill_mastery",
            target_id = "test_skill",
            target_label = "测试技能",
            amount = 1,
            reason_text = "测试奖励",
        };
        return new PendingCharacterReward
        {
            reward_id = "test_reward",
            member_id = "hero",
            member_name = "Hero",
            source_type = "test_reward",
            source_id = "test_reward",
            source_label = "测试奖励",
            summary_text = "测试奖励",
            entries = new List<PendingCharacterRewardEntry> { entry },
        };
    }
}
