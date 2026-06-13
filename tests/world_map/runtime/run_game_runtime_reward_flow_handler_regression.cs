using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_game_runtime_reward_flow_handler_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestFacadeUsesRewardFlowHandlerSurface();
        TestRewardHandlerRoutesModalCloseAndRewardPresentation();
        TestRewardHandlerRejectsStringNamePromotionPromptValues();

        Quit(_test.Finish("Game runtime reward flow handler regression"));
    }

    private void TestFacadeUsesRewardFlowHandlerSurface()
    {
        GameRuntimeFacade runtime = BuildRuntime(BuildPartyState());
        try
        {
            GDictionary prompt = new()
            {
                ["member_id"] = "hero",
                ["choices"] = new GArray(),
            };
            runtime.SetPendingWorldPromotionPromptState(prompt);
            _test.Eq(DictString(runtime.GetCurrentPromotionPrompt(), "member_id", ""), "hero", "GetCurrentPromotionPrompt() 应走正式 reward handler。");
            _test.True(runtime.PresentPendingRewardIfReady(), "PresentPendingRewardIfReady() 应通过 reward handler 打开 promotion modal。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Promotion, "存在 world promotion prompt 时应切换到 promotion modal。");

            GameRuntimeFacade.RuntimeCommandResult missingChoiceResult =
                runtime.CommandChoosePromotionTyped("warrior");
            _test.False(missingChoiceResult.Ok, "command_choose_promotion() 应委托给正式 reward handler 并拒绝不存在的职业。");
            _test.Eq(missingChoiceResult.Message, "当前晋升列表中不存在职业 warrior。", "缺失晋升选项应返回正式错误文案。");

            GameRuntimeFacade.RuntimeCommandResult cancelResult = runtime.CommandCancelPromotionChoiceTyped();
            _test.True(
                cancelResult.Ok,
                "cancel_promotion_choice() 应委托给正式 reward handler。"
            );
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Promotion, "world promotion 取消后仍应停留在 promotion modal。");
            _test.Eq(runtime._current_status_message, "当前晋升选择必须确认后才能继续结算奖励。", "world promotion 取消应刷新正式状态文案。");

            runtime.ClearPendingWorldPromotionPromptState();
            runtime._active_modal_kind = RuntimeModalKind.None;
            GameRuntimeFacade.RuntimeCommandResult confirmMissingReward =
                runtime.CommandConfirmPendingRewardTyped();
            _test.False(confirmMissingReward.Ok, "command_confirm_pending_reward() 应委托给 reward handler 并拒绝空奖励。");
            _test.Eq(confirmMissingReward.Message, "当前没有待确认的角色奖励。", "空奖励确认应返回正式错误文案。");

            runtime._active_character_info_context = new GDictionary { ["visible"] = true };
            runtime._active_modal_kind = RuntimeModalKind.CharacterInfo;
            GameRuntimeFacade.RuntimeCommandResult closeResult =
                runtime.CommandCloseActiveModalTyped();
            _test.True(closeResult.Ok, "command_close_active_modal() 应委托给 reward handler。");
            _test.Eq(runtime._active_character_info_context.Count, 0, "character_info 关闭应清空人物信息上下文。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.None, "character_info 关闭后应清空 modal。");
            _test.Eq(runtime._current_status_message, "已关闭人物信息窗。", "character_info 关闭应刷新状态文案。");
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
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Reward, "奖励弹窗应切换 modal 到 reward。");
            _test.True(runtime._active_reward == reward, "奖励呈现应把队首奖励提为 active reward。");

            _test.False(handler.PresentPendingRewardIfReady(), "已经处于 reward modal 时不应重复呈现奖励。");

            runtime._active_reward = null;
            partyState.pending_character_rewards.Clear();

            runtime._active_modal_kind = RuntimeModalKind.Settlement;
            runtime._active_settlement_id = "spring_village_01";
            runtime._active_settlement_feedback_text = "测试反馈";
            GameRuntimeFacade.RuntimeCommandResult settlementClose =
                handler.CommandCloseActiveModalTyped();
            _test.True(settlementClose.Ok, "settlement modal 应可通过 reward handler 路由关闭。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.None, "settlement close 后应清空 modal。");
            _test.Eq(runtime._active_settlement_id, "", "settlement close 后应清空当前据点。");
            _test.Eq(runtime._active_settlement_feedback_text, "", "settlement close 后应清空反馈文本。");
            _test.Eq(runtime._current_status_message, "已关闭据点窗口，返回世界地图。", "settlement close 应刷新正式状态文案。");

            runtime._active_modal_kind = RuntimeModalKind.Warehouse;
            runtime._active_warehouse_entry_label = "队伍管理";
            GameRuntimeFacade.RuntimeCommandResult warehouseClose =
                handler.CommandCloseActiveModalTyped();
            _test.True(warehouseClose.Ok, "warehouse modal 应可通过 reward handler 路由关闭。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.None, "warehouse close 后应清空 modal。");
            _test.Eq(runtime._active_warehouse_entry_label, "", "warehouse close 后应清空仓库入口标签。");
            _test.Eq(runtime._current_status_message, "已关闭共享仓库。", "warehouse close 应刷新正式状态文案。");

            runtime._active_modal_kind = RuntimeModalKind.Party;
            GameRuntimeFacade.RuntimeCommandResult partyClose =
                handler.CommandCloseActiveModalTyped();
            _test.True(partyClose.Ok, "party modal 应可通过 reward handler 路由关闭。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.None, "party close 后应清空 modal。");
            _test.Eq(runtime._current_status_message, "已关闭队伍管理窗口。", "party close 应刷新正式状态文案。");

            runtime._active_modal_kind = RuntimeModalKind.SubmapConfirm;
            runtime.GetPendingSubmapPromptState().Set(
                "",
                "",
                Vector2I.Zero,
                "ancient_tower",
                "古塔",
                "",
                ""
            );
            GameRuntimeFacade.RuntimeCommandResult submapClose =
                handler.CommandCloseActiveModalTyped();
            _test.True(submapClose.Ok, "submap_confirm modal 应可通过 reward handler 路由取消。");
            _test.Eq(runtime.GetPendingSubmapPrompt().Count, 0, "submap_confirm close 后应清空 pending submap prompt。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.None, "submap_confirm close 后应清空 modal。");
            _test.Eq(runtime._current_status_message, "已取消进入 古塔。", "submap_confirm close 应刷新正式状态文案。");

            runtime._active_modal_kind = RuntimeModalKind.Reward;
            GameRuntimeFacade.RuntimeCommandResult rewardClose =
                handler.CommandCloseActiveModalTyped();
            _test.False(rewardClose.Ok, "reward modal 不能被普通关闭命令跳过。");
            _test.Eq(rewardClose.Message, "当前角色奖励必须确认后才能继续。", "reward modal 普通关闭应返回正式错误文案。");

            runtime._active_reward = BuildPendingReward();
            GameRuntimeFacade.RuntimeCommandResult confirmActiveReward =
                handler.CommandConfirmActiveRewardTyped();
            _test.True(confirmActiveReward.Ok, "active reward 应能通过 typed confirm helper 结算。");
            _test.True(runtime._active_reward == null, "confirm active reward 后应清空 active reward。");

            runtime.SetPendingWorldPromotionPromptState(
                new GDictionary
                {
                    ["member_id"] = "hero",
                    ["choices"] = new GArray(),
                }
            );
            GameRuntimeFacade.RuntimeCommandResult cancelPromotionChoice =
                handler.CommandCancelPromotionChoiceTyped();
            _test.True(cancelPromotionChoice.Ok, "cancel promotion choice 应走 typed helper。");
            _test.Eq(runtime._active_modal_kind, RuntimeModalKind.Promotion, "cancel promotion choice 后应仍停留在 promotion modal。");
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestRewardHandlerRejectsStringNamePromotionPromptValues()
    {
        GameRuntimeFacade runtime = BuildRuntime(BuildPartyState());
        try
        {
            runtime.SetPendingWorldPromotionPromptState(
                new GDictionary
                {
                    ["member_id"] = new StringName("hero"),
                    ["choices"] = new GArray
                    {
                        new GDictionary
                        {
                            ["profession_id"] = new StringName("warrior"),
                            ["selection"] = new GDictionary(),
                        },
                    },
                }
            );

            GameRuntimeFacade.RuntimeCommandResult result = runtime.CommandChoosePromotionTyped(
                "warrior"
            );
            _test.False(
                result.Ok,
                "StringName-valued promotion ids 不应继续被 reward handler 当成正式字符串消费。"
            );
            _test.Eq(
                result.Message,
                "当前晋升列表中不存在职业 warrior。",
                "StringName-valued promotion ids 应走正式缺失职业错误文案。"
            );
        }
        finally
        {
            runtime.Dispose();
        }
    }

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

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : fallback;
    }

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
            entries = new Godot.Collections.Array<PendingCharacterRewardEntry> { entry },
        };
    }
}
