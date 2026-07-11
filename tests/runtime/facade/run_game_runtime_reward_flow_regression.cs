using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_game_runtime_reward_flow_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestRewardQueueConfirmationShowsNextReward();
        TestTypedResearchRewardQueueAndPresentation();
        TestCloseActiveModalStillPresentsPendingReward();

        RequestTestExit(_test.Finish("Game runtime reward flow regression"));
    }

    private void TestRewardQueueConfirmationShowsNextReward()
    {
        PartyState partyState = BuildPartyStateWithRewards("reward_a", "reward_b");
        GameRuntimeFacade runtime = BuildRuntime(partyState);
        try
        {
            GameRuntimeRewardFlowHandler handler = runtime._reward_flow_handler;
            handler.EnqueuePendingCharacterRewardsTyped(new[] { BuildReward("reward_c") });
            _test.Eq(
                runtime.GetPendingRewardCount(),
                3,
                "奖励入队应同步到 PartyState。"
            );

            _test.True(handler.PresentPendingRewardIfReady(), "第一条奖励应可进入 reward modal。");
            _test.Eq(runtime.GetActiveReward()?.reward_id.ToString() ?? "", "reward_a", "应先展示队首奖励。");

            GameRuntimeFacade.RuntimeCommandResult result = handler.CommandConfirmPendingRewardTyped();
            _test.True(result.Ok, "确认奖励命令应成功。");
            _test.Eq(runtime.GetActiveReward()?.reward_id.ToString() ?? "", "reward_b", "确认第一条奖励后应自动展示下一条。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Reward, "确认奖励后应继续停留在 reward modal。");
            _test.Eq(runtime.GetPendingRewardCount(), 2, "已确认奖励应从队列移除。");
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestTypedResearchRewardQueueAndPresentation()
    {
        PartyState partyState = BuildPartyState();
        GameRuntimeFacade runtime = BuildRuntime(partyState);
        try
        {
            GameRuntimeRewardFlowHandler handler = runtime._reward_flow_handler;
            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Settlement);

            handler.EnqueuePendingCharacterRewardsTyped(new[] { BuildResearchReward() });
            _test.Eq(runtime.GetPendingRewardCount(), 1, "research 生成的 typed 奖励应能正式入队。");

            PendingCharacterReward queuedReward = runtime.GetPartyState().GetNextPendingCharacterReward();
            _test.True(queuedReward != null, "research 奖励入队后应能读取到正式 PendingCharacterReward。");
            if (queuedReward != null)
            {
                _test.Eq(queuedReward.source_type.ToString(), "npc_teach", "research 奖励应保留正式 source_type。");
                _test.Eq(queuedReward.source_id.ToString(), "research_field_manual", "research 奖励应保留具体 source_id。");
                _test.Eq(queuedReward.source_label.ToString(), "大图书官·研究", "research 奖励应保留正式 source_label。");
                _test.Eq(
                    queuedReward.summary_text.ToString(),
                    "大图书官 为 Hero 整理出新的研究成果：野外手册。",
                    "research 奖励应保留摘要文本。"
                );
                _test.Eq(queuedReward.entries[0].entry_type.ToString(), "knowledge_unlock", "research 奖励条目应保留知识解锁类型。");
                _test.Eq(queuedReward.entries[0].target_id.ToString(), "field_manual", "research 奖励条目应指向野外手册。");
            }

            _test.False(handler.PresentPendingRewardIfReady(), "settlement modal 打开时 research 奖励不应抢占当前窗口。");
            _test.True(runtime.GetActiveReward() == null, "reward flow 被 settlement 阻塞时不应提前设置 active reward。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Settlement, "reward flow 被 settlement 阻塞时 modal 应保持 settlement。");

            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.None);
            _test.True(handler.PresentPendingRewardIfReady(), "research 奖励在无阻塞 modal 时应进入正式 reward flow。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Reward, "research 奖励呈现时 modal 应切换为 reward。");
            PendingCharacterReward activeReward = runtime.GetActiveReward();
            _test.True(activeReward != null, "research 奖励呈现时应设置 active reward。");
            if (activeReward != null)
            {
                _test.Eq(activeReward.source_id.ToString(), "research_field_manual", "active reward 应沿用 research source_id。");
                _test.Eq(activeReward.entries[0].entry_type.ToString(), "knowledge_unlock", "active reward 应沿用 research 奖励条目类型。");
            }

            GameRuntimeFacade.RuntimeCommandResult confirmResult =
                handler.CommandConfirmPendingRewardTyped();
            _test.True(confirmResult.Ok, "research active reward 应能通过正式确认命令结算。");
            _test.True(runtime.GetActiveReward() == null, "research 奖励确认后 active reward 应清空。");
            _test.Eq(runtime.GetPendingRewardCount(), 0, "research 奖励确认后待处理队列应清空。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.None, "research 奖励确认完成后不应残留 reward modal。");
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestCloseActiveModalStillPresentsPendingReward()
    {
        PartyState partyState = BuildPartyStateWithRewards("reward_a");
        GameRuntimeFacade runtime = BuildRuntime(partyState);
        try
        {
            GameRuntimeRewardFlowHandler handler = runtime._reward_flow_handler;
            runtime.SetActiveCharacterInfoContext(new GDictionary { ["display_name"] = "侦察兵" });
            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.CharacterInfo);

            GameRuntimeFacade.RuntimeCommandResult closeResult =
                handler.CommandCloseActiveModalTyped();
            _test.True(closeResult.Ok, "关闭人物信息窗应成功。");
            using GodotProjectionLease<GDictionary> characterInfoLease =
                runtime.GetCharacterInfoContextLease();
            _test.Eq(characterInfoLease.Value.Count, 0, "关闭人物信息窗后上下文应清空。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Reward, "关闭人物信息窗后应继续展示待领奖励。");

            GameRuntimeFacade.RuntimeCommandResult blockedResult =
                handler.CommandCloseActiveModalTyped();
            _test.False(blockedResult.Ok, "reward modal 不应直接关闭。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Reward, "reward modal 被阻止时应保持打开。");
            _test.Eq(
                blockedResult.Code,
                GameRuntimeFacade.RuntimeCommandCode.InvalidState,
                "reward modal 被阻止时 typed result 应给出 InvalidState code。"
            );
        }
        finally
        {
            runtime.Dispose();
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
            BuildSkillDefinitions(),
            new Dictionary<StringName, ProfessionDefinition>(),
            new Dictionary<StringName, AchievementDefinition>(),
            new Dictionary<StringName, ItemDefinition>(),
            new Dictionary<StringName, QuestDefinition>()
        );
        runtime._settlement_command_handler.SetupRuntime(runtime);
        runtime._warehouse_handler.Setup(runtime);
        runtime._party_command_handler.Setup(runtime);
        runtime._reward_flow_handler.Setup(runtime);
        runtime._quest_command_handler.Setup(runtime);
        return runtime;
    }

    private static PartyState BuildPartyStateWithRewards(params string[] rewardIds)
    {
        PartyState partyState = BuildPartyState();
        foreach (string rewardId in rewardIds)
        {
            partyState.EnqueuePendingCharacterReward(BuildReward(rewardId));
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
        partyState.SetMemberState(
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

    private static Dictionary<StringName, SkillDefinition> BuildSkillDefinitions()
    {
        return new Dictionary<StringName, SkillDefinition>
        {
            ["field_manual"] = BuildSkillDefinition("field_manual", "野外手册", "knowledge"),
            ["skill_reward_a"] = BuildSkillDefinition("skill_reward_a", "A", ""),
            ["skill_reward_b"] = BuildSkillDefinition("skill_reward_b", "B", ""),
            ["skill_reward_c"] = BuildSkillDefinition("skill_reward_c", "C", ""),
        };
    }

    private static SkillDefinition BuildSkillDefinition(
        StringName skillId,
        string displayName,
        StringName skillType
    )
    {
        return new SkillDefinition(
            skillId,
            displayName,
            "",
            "",
            skillType,
            1,
            1,
            "",
            0,
            0,
            System.Array.Empty<int>(),
            System.Array.Empty<StringName>(),
            "",
            System.Array.Empty<StringName>(),
            "",
            System.Array.Empty<StringName>(),
            new Dictionary<StringName, int>(),
            new Dictionary<StringName, int>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            false,
            "",
            System.Array.Empty<StringName>(),
            "",
            new Dictionary<StringName, int>(),
            "",
            System.Array.Empty<AttributeModifierDefinition>(),
            "",
            new Dictionary<int, IReadOnlyDictionary<string, object>>(),
            null
        );
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
            entries = new List<PendingCharacterRewardEntry> { entry },
        };
    }

    private static PendingCharacterReward BuildResearchReward()
    {
        return new PendingCharacterReward
        {
            reward_id = "hero_research_field_manual_reward",
            member_id = "hero",
            member_name = "Hero",
            source_type = "npc_teach",
            source_id = "research_field_manual",
            source_label = "大图书官·研究",
            summary_text = "大图书官 为 Hero 整理出新的研究成果：野外手册。",
            entries = new List<PendingCharacterRewardEntry>
            {
                new PendingCharacterRewardEntry
                {
                    entry_type = "knowledge_unlock",
                    target_id = "field_manual",
                    target_label = "野外手册",
                    amount = 1,
                    reason_text = "研究员整理出一份可长期翻阅的野外手册抄本。",
                },
            },
        };
    }
}
