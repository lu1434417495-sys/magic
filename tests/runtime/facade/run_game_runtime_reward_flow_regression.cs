using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_game_runtime_reward_flow_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestRewardQueueConfirmationShowsNextReward();
        TestDictionaryRewardQueueAndPresentation();
        TestCloseActiveModalStillPresentsPendingReward();

        if (_failures.Count == 0)
        {
            GD.Print("Game runtime reward flow regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Game runtime reward flow regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestRewardQueueConfirmationShowsNextReward()
    {
        PartyState partyState = BuildPartyStateWithRewards("reward_a", "reward_b");
        GameRuntimeFacade runtime = BuildRuntime(partyState);
        try
        {
            GameRuntimeRewardFlowHandler handler = runtime._reward_flow_handler;
            handler.enqueue_pending_character_rewards(new GArray { BuildReward("reward_c") });
            AssertEq(
                runtime._party_state.pending_character_rewards.Count,
                3,
                "奖励入队应同步到 PartyState。"
            );

            AssertTrue(handler.present_pending_reward_if_ready(), "第一条奖励应可进入 reward modal。");
            AssertEq(runtime._active_reward.reward_id.ToString(), "reward_a", "应先展示队首奖励。");

            GDictionary result = handler.command_confirm_pending_reward();
            AssertTrue(DictBool(result, "ok", false), "确认奖励命令应成功。");
            AssertEq(runtime._active_reward.reward_id.ToString(), "reward_b", "确认第一条奖励后应自动展示下一条。");
            AssertEq(runtime._active_modal_id, "reward", "确认奖励后应继续停留在 reward modal。");
            AssertEq(runtime._party_state.pending_character_rewards.Count, 2, "已确认奖励应从队列移除。");
        }
        finally
        {
            runtime.dispose();
        }
    }

    private void TestDictionaryRewardQueueAndPresentation()
    {
        PartyState partyState = BuildPartyState();
        GameRuntimeFacade runtime = BuildRuntime(partyState);
        try
        {
            GameRuntimeRewardFlowHandler handler = runtime._reward_flow_handler;
            runtime._active_modal_id = "settlement";

            handler.enqueue_pending_character_rewards(new GArray { BuildResearchRewardData() });
            AssertEq(runtime._party_state.pending_character_rewards.Count, 1, "research 生成的字典奖励应能正式入队。");

            PendingCharacterReward queuedReward = runtime._party_state.get_next_pending_character_reward();
            AssertTrue(queuedReward != null, "research 奖励入队后应能读取到正式 PendingCharacterReward。");
            if (queuedReward != null)
            {
                AssertEq(queuedReward.source_type.ToString(), "npc_teach", "research 奖励应保留正式 source_type。");
                AssertEq(queuedReward.source_id.ToString(), "research_field_manual", "research 奖励应保留具体 source_id。");
                AssertEq(queuedReward.source_label.ToString(), "大图书官·研究", "research 奖励应保留正式 source_label。");
                AssertEq(
                    queuedReward.summary_text.ToString(),
                    "大图书官 为 Hero 整理出新的研究成果：野外手册。",
                    "research 奖励应保留摘要文本。"
                );
                AssertEq(queuedReward.entries[0].entry_type.ToString(), "knowledge_unlock", "research 奖励条目应保留知识解锁类型。");
                AssertEq(queuedReward.entries[0].target_id.ToString(), "field_manual", "research 奖励条目应指向野外手册。");
            }

            AssertFalse(handler.present_pending_reward_if_ready(), "settlement modal 打开时 research 奖励不应抢占当前窗口。");
            AssertTrue(runtime._active_reward == null, "reward flow 被 settlement 阻塞时不应提前设置 active reward。");
            AssertEq(runtime._active_modal_id, "settlement", "reward flow 被 settlement 阻塞时 modal 应保持 settlement。");

            runtime._active_modal_id = "";
            AssertTrue(handler.present_pending_reward_if_ready(), "research 奖励在无阻塞 modal 时应进入正式 reward flow。");
            AssertEq(runtime._active_modal_id, "reward", "research 奖励呈现时 modal 应切换为 reward。");
            AssertTrue(runtime._active_reward != null, "research 奖励呈现时应设置 active reward。");
            if (runtime._active_reward != null)
            {
                AssertEq(runtime._active_reward.source_id.ToString(), "research_field_manual", "active reward 应沿用 research source_id。");
                AssertEq(runtime._active_reward.entries[0].entry_type.ToString(), "knowledge_unlock", "active reward 应沿用 research 奖励条目类型。");
            }

            GDictionary confirmResult = handler.command_confirm_pending_reward();
            AssertTrue(DictBool(confirmResult, "ok", false), "research active reward 应能通过正式确认命令结算。");
            AssertTrue(runtime._active_reward == null, "research 奖励确认后 active reward 应清空。");
            AssertEq(runtime._party_state.pending_character_rewards.Count, 0, "research 奖励确认后待处理队列应清空。");
            AssertEq(runtime._active_modal_id, "", "research 奖励确认完成后不应残留 reward modal。");
        }
        finally
        {
            runtime.dispose();
        }
    }

    private void TestCloseActiveModalStillPresentsPendingReward()
    {
        PartyState partyState = BuildPartyStateWithRewards("reward_a");
        GameRuntimeFacade runtime = BuildRuntime(partyState);
        try
        {
            GameRuntimeRewardFlowHandler handler = runtime._reward_flow_handler;
            runtime._active_character_info_context = new GDictionary { ["display_name"] = "侦察兵" };
            runtime._active_modal_id = "character_info";

            GDictionary closeResult = handler.command_close_active_modal();
            AssertTrue(DictBool(closeResult, "ok", false), "关闭人物信息窗应成功。");
            AssertEq(runtime._active_character_info_context.Count, 0, "关闭人物信息窗后上下文应清空。");
            AssertEq(runtime._active_modal_id, "reward", "关闭人物信息窗后应继续展示待领奖励。");

            GDictionary blockedResult = handler.command_close_active_modal();
            AssertFalse(DictBool(blockedResult, "ok", true), "reward modal 不应直接关闭。");
            AssertEq(runtime._active_modal_id, "reward", "reward modal 被阻止时应保持打开。");
            AssertEq(
                runtime._current_status_message,
                "当前角色奖励必须确认后才能继续。",
                "reward modal 被阻止时应给出明确提示。"
            );
        }
        finally
        {
            runtime.dispose();
        }
    }

    private static GameRuntimeFacade BuildRuntime(PartyState partyState)
    {
        GameRuntimeFacade runtime = new()
        {
            _party_state = partyState,
        };
        runtime._character_management.setup(
            partyState,
            BuildSkillDefs(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary()
        );
        runtime._settlement_command_handler.setup(runtime);
        runtime._warehouse_handler.setup(runtime);
        runtime._party_command_handler.setup(runtime);
        runtime._reward_flow_handler.setup(runtime);
        runtime._quest_command_handler.setup(runtime);
        return runtime;
    }

    private static PartyState BuildPartyStateWithRewards(params string[] rewardIds)
    {
        PartyState partyState = BuildPartyState();
        foreach (string rewardId in rewardIds)
        {
            partyState.enqueue_pending_character_reward(BuildReward(rewardId));
        }
        return partyState;
    }

    private static PartyState BuildPartyState()
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
        };
        partyState.set_member_state(
            new PartyMemberState
            {
                member_id = "hero",
                display_name = "Hero",
                current_hp = 20,
                current_mp = 4,
                progression = new UnitProgress
                {
                    unit_id = "hero",
                    display_name = "Hero",
                    unit_base_attributes = new UnitBaseAttributes(),
                },
            }
        );
        return partyState;
    }

    private static GDictionary BuildSkillDefs()
    {
        return new GDictionary
        {
            ["field_manual"] = new SkillDef
            {
                skill_id = "field_manual",
                display_name = "野外手册",
                skill_type = "knowledge",
            },
            ["skill_reward_a"] = new SkillDef { skill_id = "skill_reward_a", display_name = "A" },
            ["skill_reward_b"] = new SkillDef { skill_id = "skill_reward_b", display_name = "B" },
            ["skill_reward_c"] = new SkillDef { skill_id = "skill_reward_c", display_name = "C" },
        };
    }

    private static PendingCharacterReward BuildReward(string rewardId)
    {
        PendingCharacterRewardEntry entry = new()
        {
            entry_type = "skill_mastery",
            target_id = $"skill_{rewardId}",
            target_label = rewardId,
            amount = 1,
            reason_text = "测试熟练度奖励",
        };
        return new PendingCharacterReward
        {
            reward_id = rewardId,
            member_id = "hero",
            member_name = "Hero",
            source_type = "test_reward",
            source_id = rewardId,
            source_label = rewardId,
            summary_text = $"测试奖励 {rewardId}",
            entries = new Godot.Collections.Array<PendingCharacterRewardEntry> { entry },
        };
    }

    private static GDictionary BuildResearchRewardData()
    {
        return new GDictionary
        {
            ["reward_id"] = "hero_research_field_manual_reward",
            ["member_id"] = "hero",
            ["member_name"] = "Hero",
            ["source_type"] = "npc_teach",
            ["source_id"] = "research_field_manual",
            ["source_label"] = "大图书官·研究",
            ["summary_text"] = "大图书官 为 Hero 整理出新的研究成果：野外手册。",
            ["entries"] = new GArray
            {
                new GDictionary
                {
                    ["entry_type"] = "knowledge_unlock",
                    ["target_id"] = "field_manual",
                    ["target_label"] = "野外手册",
                    ["amount"] = 1,
                    ["reason_text"] = "研究员整理出一份可长期翻阅的野外手册抄本。",
                },
            },
        };
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        return dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsBool()
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
