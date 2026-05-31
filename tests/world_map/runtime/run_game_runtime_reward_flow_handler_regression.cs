using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_game_runtime_reward_flow_handler_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestFacadeUsesRewardFlowHandlerSurface();
        TestRewardHandlerRoutesModalCloseAndRewardPresentation();

        if (_failures.Count == 0)
        {
            GD.Print("Game runtime reward flow handler regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Game runtime reward flow handler regression: FAIL ({_failures.Count})");
        Quit(1);
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
            runtime.set_pending_world_promotion_prompt_state(prompt);
            AssertEq(DictString(runtime._get_current_promotion_prompt(), "member_id", ""), "hero", "_get_current_promotion_prompt() 应走正式 reward handler。");
            AssertTrue(runtime._present_pending_reward_if_ready(), "_present_pending_reward_if_ready() 应通过 reward handler 打开 promotion modal。");
            AssertEq(runtime._active_modal_id, "promotion", "存在 world promotion prompt 时应切换到 promotion modal。");

            GDictionary missingChoiceResult = runtime.command_choose_promotion("warrior");
            AssertFalse(DictBool(missingChoiceResult, "ok", true), "command_choose_promotion() 应委托给正式 reward handler 并拒绝不存在的职业。");
            AssertEq(DictString(missingChoiceResult, "message", ""), "当前晋升列表中不存在职业 warrior。", "缺失晋升选项应返回正式错误文案。");

            GDictionary cancelResult = runtime.cancel_promotion_choice();
            AssertTrue(DictBool(cancelResult, "ok", false), "cancel_promotion_choice() 应委托给正式 reward handler。");
            AssertEq(runtime._active_modal_id, "promotion", "world promotion 取消后仍应停留在 promotion modal。");
            AssertEq(runtime._current_status_message, "当前晋升选择必须确认后才能继续结算奖励。", "world promotion 取消应刷新正式状态文案。");

            runtime.clear_pending_world_promotion_prompt_state();
            runtime._active_modal_id = "";
            GDictionary confirmMissingReward = runtime.command_confirm_pending_reward();
            AssertFalse(DictBool(confirmMissingReward, "ok", true), "command_confirm_pending_reward() 应委托给 reward handler 并拒绝空奖励。");
            AssertEq(DictString(confirmMissingReward, "message", ""), "当前没有待确认的角色奖励。", "空奖励确认应返回正式错误文案。");

            runtime._active_character_info_context = new GDictionary { ["visible"] = true };
            runtime._active_modal_id = "character_info";
            GDictionary closeResult = runtime.command_close_active_modal();
            AssertTrue(DictBool(closeResult, "ok", false), "command_close_active_modal() 应委托给 reward handler。");
            AssertEq(runtime._active_character_info_context.Count, 0, "character_info 关闭应清空人物信息上下文。");
            AssertEq(runtime._active_modal_id, "", "character_info 关闭后应清空 modal。");
            AssertEq(runtime._current_status_message, "已关闭人物信息窗。", "character_info 关闭应刷新状态文案。");
        }
        finally
        {
            runtime.dispose();
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
            AssertTrue(handler.present_pending_reward_if_ready(), "存在待领奖励时应进入 reward modal。");
            AssertEq(runtime._active_modal_id, "reward", "奖励弹窗应切换 modal 到 reward。");
            AssertTrue(runtime._active_reward == reward, "奖励呈现应把队首奖励提为 active reward。");

            AssertFalse(handler.present_pending_reward_if_ready(), "已经处于 reward modal 时不应重复呈现奖励。");

            runtime._active_reward = null;
            partyState.pending_character_rewards.Clear();

            runtime._active_modal_id = "settlement";
            runtime._active_settlement_id = "spring_village_01";
            runtime._active_settlement_feedback_text = "测试反馈";
            GDictionary settlementClose = handler.command_close_active_modal();
            AssertTrue(DictBool(settlementClose, "ok", false), "settlement modal 应可通过 reward handler 路由关闭。");
            AssertEq(runtime._active_modal_id, "", "settlement close 后应清空 modal。");
            AssertEq(runtime._active_settlement_id, "", "settlement close 后应清空当前据点。");
            AssertEq(runtime._active_settlement_feedback_text, "", "settlement close 后应清空反馈文本。");
            AssertEq(runtime._current_status_message, "已关闭据点窗口，返回世界地图。", "settlement close 应刷新正式状态文案。");

            runtime._active_modal_id = "warehouse";
            runtime._active_warehouse_entry_label = "队伍管理";
            GDictionary warehouseClose = handler.command_close_active_modal();
            AssertTrue(DictBool(warehouseClose, "ok", false), "warehouse modal 应可通过 reward handler 路由关闭。");
            AssertEq(runtime._active_modal_id, "", "warehouse close 后应清空 modal。");
            AssertEq(runtime._active_warehouse_entry_label, "", "warehouse close 后应清空仓库入口标签。");
            AssertEq(runtime._current_status_message, "已关闭共享仓库。", "warehouse close 应刷新正式状态文案。");

            runtime._active_modal_id = "party";
            GDictionary partyClose = handler.command_close_active_modal();
            AssertTrue(DictBool(partyClose, "ok", false), "party modal 应可通过 reward handler 路由关闭。");
            AssertEq(runtime._active_modal_id, "", "party close 后应清空 modal。");
            AssertEq(runtime._current_status_message, "已关闭队伍管理窗口。", "party close 应刷新正式状态文案。");

            runtime._active_modal_id = "submap_confirm";
            runtime._pending_submap_prompt = new GDictionary { ["target_display_name"] = "古塔" };
            GDictionary submapClose = handler.command_close_active_modal();
            AssertTrue(DictBool(submapClose, "ok", false), "submap_confirm modal 应可通过 reward handler 路由取消。");
            AssertEq(runtime._pending_submap_prompt.Count, 0, "submap_confirm close 后应清空 pending submap prompt。");
            AssertEq(runtime._active_modal_id, "", "submap_confirm close 后应清空 modal。");
            AssertEq(runtime._current_status_message, "已取消进入 古塔。", "submap_confirm close 应刷新正式状态文案。");

            runtime._active_modal_id = "reward";
            GDictionary rewardClose = handler.command_close_active_modal();
            AssertFalse(DictBool(rewardClose, "ok", true), "reward modal 不能被普通关闭命令跳过。");
            AssertEq(DictString(rewardClose, "message", ""), "当前角色奖励必须确认后才能继续。", "reward modal 普通关闭应返回正式错误文案。");
        }
        finally
        {
            runtime.dispose();
        }
    }

    private static GameRuntimeFacade BuildRuntime(PartyState partyState)
    {
        var runtime = new GameRuntimeFacade
        {
            _party_state = partyState,
        };
        runtime._settlement_command_handler.setup(runtime);
        runtime._warehouse_handler.setup(runtime);
        runtime._party_command_handler.setup(runtime);
        runtime._reward_flow_handler.setup(runtime);
        return runtime;
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
        partyState.set_member_state(hero);
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

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsBool()
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
}
