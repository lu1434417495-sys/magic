using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_game_runtime_reward_flow_regression : SceneTree
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

        Quit(_test.Finish("Game runtime reward flow regression"));
    }

    private void TestRewardQueueConfirmationShowsNextReward()
    {
        RuntimeFixture fixture = BuildRuntime(BuildPartyStateWithRewards("reward_a", "reward_b"));
        GameRuntimeFacade runtime = fixture.Runtime;
        PendingCharacterReward enqueuedReward = null;
        PendingCharacterReward confirmedReward = null;
        try
        {
            GameRuntimeRewardFlowHandler handler = runtime._reward_flow_handler;
            enqueuedReward = BuildReward("reward_c");
            handler.EnqueuePendingCharacterRewardsTyped(new[] { enqueuedReward });
            GodotRefCountedDisposer.DisposeIfValid(enqueuedReward);
            enqueuedReward = null;
            _test.Eq(
                runtime.GetPendingRewardCount(),
                3,
                "奖励入队应同步到 PartyState。"
            );

            _test.True(handler.PresentPendingRewardIfReady(), "第一条奖励应可进入 reward modal。");
            _test.Eq(runtime.GetActiveReward()?.reward_id.ToString() ?? "", "reward_a", "应先展示队首奖励。");

            confirmedReward = runtime.GetActiveReward();
            GameRuntimeFacade.RuntimeCommandResult result = handler.CommandConfirmPendingRewardTyped();
            GodotRefCountedDisposer.DisposeIfValid(confirmedReward);
            confirmedReward = null;
            _test.True(result.Ok, "确认奖励命令应成功。");
            _test.Eq(runtime.GetActiveReward()?.reward_id.ToString() ?? "", "reward_b", "确认第一条奖励后应自动展示下一条。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.Reward, "确认奖励后应继续停留在 reward modal。");
            _test.Eq(runtime.GetPendingRewardCount(), 2, "已确认奖励应从队列移除。");
        }
        finally
        {
            GodotRefCountedDisposer.DisposeIfValid(confirmedReward);
            GodotRefCountedDisposer.DisposeIfValid(enqueuedReward);
            fixture.Dispose();
        }
    }

    private void TestTypedResearchRewardQueueAndPresentation()
    {
        RuntimeFixture fixture = BuildRuntime(BuildPartyState());
        GameRuntimeFacade runtime = fixture.Runtime;
        PendingCharacterReward researchReward = null;
        PendingCharacterReward activeReward = null;
        try
        {
            GameRuntimeRewardFlowHandler handler = runtime._reward_flow_handler;
            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.Settlement);

            researchReward = BuildResearchReward();
            handler.EnqueuePendingCharacterRewardsTyped(new[] { researchReward });
            GodotRefCountedDisposer.DisposeIfValid(researchReward);
            researchReward = null;
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
            activeReward = runtime.GetActiveReward();
            _test.True(activeReward != null, "research 奖励呈现时应设置 active reward。");
            if (activeReward != null)
            {
                _test.Eq(activeReward.source_id.ToString(), "research_field_manual", "active reward 应沿用 research source_id。");
                _test.Eq(activeReward.entries[0].entry_type.ToString(), "knowledge_unlock", "active reward 应沿用 research 奖励条目类型。");
            }

            GameRuntimeFacade.RuntimeCommandResult confirmResult =
                handler.CommandConfirmPendingRewardTyped();
            GodotRefCountedDisposer.DisposeIfValid(activeReward);
            activeReward = null;
            _test.True(confirmResult.Ok, "research active reward 应能通过正式确认命令结算。");
            _test.True(runtime.GetActiveReward() == null, "research 奖励确认后 active reward 应清空。");
            _test.Eq(runtime.GetPendingRewardCount(), 0, "research 奖励确认后待处理队列应清空。");
            _test.Eq(runtime.GetActiveModalKind(), RuntimeModalKind.None, "research 奖励确认完成后不应残留 reward modal。");
        }
        finally
        {
            GodotRefCountedDisposer.DisposeIfValid(activeReward);
            GodotRefCountedDisposer.DisposeIfValid(researchReward);
            fixture.Dispose();
        }
    }

    private void TestCloseActiveModalStillPresentsPendingReward()
    {
        RuntimeFixture fixture = BuildRuntime(BuildPartyStateWithRewards("reward_a"));
        GameRuntimeFacade runtime = fixture.Runtime;
        try
        {
            GameRuntimeRewardFlowHandler handler = runtime._reward_flow_handler;
            runtime.SetActiveCharacterInfoContext(new GDictionary { ["display_name"] = "侦察兵" });
            runtime.SetRuntimeActiveModalKind(RuntimeModalKind.CharacterInfo);

            GameRuntimeFacade.RuntimeCommandResult closeResult =
                handler.CommandCloseActiveModalTyped();
            _test.True(closeResult.Ok, "关闭人物信息窗应成功。");
            _test.Eq(runtime.GetCharacterInfoContext().Count, 0, "关闭人物信息窗后上下文应清空。");
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
            fixture.Dispose();
        }
    }

    private static RuntimeFixture BuildRuntime(PartyState partyState)
    {
        GDictionary skillDefs = BuildSkillDefs();
        GameRuntimeFacade runtime = new()
        {
            _party_state = partyState,
        };
        runtime._character_management.setup(
            partyState,
            skillDefs,
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary()
        );
        runtime._settlement_command_handler.SetupRuntime(runtime);
        runtime._warehouse_handler.Setup(runtime);
        runtime._party_command_handler.Setup(runtime);
        runtime._reward_flow_handler.Setup(runtime);
        runtime._quest_command_handler.Setup(runtime);
        return new RuntimeFixture(runtime, partyState, skillDefs);
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
            entries = new Godot.Collections.Array<PendingCharacterRewardEntry>
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

    private sealed class RuntimeFixture : IDisposable
    {
        private readonly PartyState _partyState;
        private readonly GDictionary _skillDefs;

        internal RuntimeFixture(GameRuntimeFacade runtime, PartyState partyState, GDictionary skillDefs)
        {
            Runtime = runtime;
            _partyState = partyState;
            _skillDefs = skillDefs;
        }

        internal GameRuntimeFacade Runtime { get; }

        public void Dispose()
        {
            Runtime?.Dispose();
            if (_skillDefs != null)
            {
                foreach (Variant key in _skillDefs.Keys)
                {
                    if (_skillDefs[key].AsGodotObject() is SkillDef skillDef)
                        BattleTestFixture.DisposeSkill(skillDef);
                }
                _skillDefs.Clear();
            }
            GodotRefCountedDisposer.DisposeIfValid(_partyState);
        }
    }
}
