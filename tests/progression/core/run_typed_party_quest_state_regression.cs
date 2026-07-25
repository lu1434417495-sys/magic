using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_typed_party_quest_state_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestPartyMembersRejectNonMemberValues();
        TestQuestProgressRejectsNonIntValues();
        TestQuestContextRejectsNonIntValues();
        TestQuestStateSafelyRejectsInvalidProgressContext();
        TestQuestJournalStoresFailedStateWithoutCrossStageLeak();
        TestQuestJournalReturnsDetachedQuestStates();
        TestCustomStatsRejectNonIntValues();
        TestReputationsRejectNonIntValues();
        TestValidTypedPayloadsRoundTrip();
        TestPartyStateUsesCurrentSaveVersion();

        RequestTestExit(_test.Finish("Typed party quest state regression"));
    }

    private void TestPartyMembersRejectNonMemberValues()
    {
        ExpectArgumentException(
            () => PartyMemberStateCollection.FromDictionary(new GDictionary { ["hero"] = new GDictionary() }),
            "member_states 中的非 PartyMemberState value 应在解析阶段被拒绝。"
        );
    }

    private void TestQuestProgressRejectsNonIntValues()
    {
        ExpectArgumentException(
            () => QuestObjectiveProgressState.FromDictionary(new GDictionary { ["kill"] = "bad" }),
            "objective_progress 中的非 int value 应在解析阶段被拒绝。"
        );
    }

    private void TestQuestContextRejectsNonIntValues()
    {
        ExpectArgumentException(
            () =>
                QuestProgressContext.FromDictionary(
                    new GDictionary { ["submitted_quantity"] = "bad" }
                ),
            "last_progress_context.submitted_quantity 的非 int value 应在解析阶段被拒绝。"
        );
    }

    private void TestQuestStateSafelyRejectsInvalidProgressContext()
    {
        GDictionary payload = new QuestState { quest_id = "corrupt_context" }.ToDictionary();
        payload["last_progress_context"] = new GDictionary
        {
            ["submitted_quantity"] = "bad",
        };

        QuestState parsedState = null;
        Exception parseException = null;
        try
        {
            parsedState = QuestState.FromDictionary(payload);
        }
        catch (Exception exception)
        {
            parseException = exception;
        }

        _test.True(
            parseException == null,
            "QuestState 应安全拒绝损坏的 last_progress_context，而不是抛出异常。"
        );
        _test.True(
            parsedState == null,
            "QuestState 应将损坏的 last_progress_context 判为无效 payload。"
        );
    }

    private void TestQuestJournalStoresFailedStateWithoutCrossStageLeak()
    {
        PartyState partyState = new();
        QuestState failedQuest = new() { quest_id = "failed_quest" };
        failedQuest.MarkAccepted(2);
        _test.True(
            failedQuest.MarkFailed(
                4,
                "deadline_expired",
                QuestProgressContext.FromDictionary(
                    new GDictionary { ["source_type"] = "quest_test" }
                )
            ),
            "active QuestState 应能记录带原因的失败事实。"
        );

        _test.True(
            partyState.SetQuestState(failedQuest.quest_id, failedQuest),
            "QuestJournalState 应接收合法 failed QuestState。"
        );

        _test.True(
            !partyState.HasActiveQuest(failedQuest.quest_id),
            "failed 任务不得残留在 active 集合。"
        );
        _test.True(
            !partyState.HasClaimableQuest(failedQuest.quest_id),
            "failed 状态不能伪装成 claimable 任务。"
        );
        _test.True(
            !partyState.HasCompletedQuest(failedQuest.quest_id),
            "failed 状态不能伪装成已领奖任务。"
        );
        _test.True(
            partyState.HasFailedQuest(failedQuest.quest_id),
            "failed 状态应由正式失败任务集合承载。"
        );
        QuestState storedFailedQuest = partyState.GetFailedQuestState(failedQuest.quest_id);
        _test.Eq(storedFailedQuest?.failed_at_world_step ?? -1, 4, "失败任务应保留失败时间。");
        _test.Eq(
            storedFailedQuest?.failure_reason_id ?? new StringName(""),
            new StringName("deadline_expired"),
            "失败任务应保留失败原因。"
        );
    }

    private void TestQuestJournalReturnsDetachedQuestStates()
    {
        PartyState partyState = new();
        QuestState activeQuest = new() { quest_id = "detached_active_quest" };
        activeQuest.MarkAccepted(3);
        _test.True(
            partyState.SetActiveQuestState(activeQuest),
            "测试 active quest 应进入 QuestJournalState。"
        );

        QuestState detached = partyState.GetActiveQuestState(activeQuest.quest_id);
        _test.True(detached != null, "任务查询应返回 detached 状态。");
        if (detached == null)
            return;
        detached.MarkCompleted(6);

        QuestState canonical = partyState.GetActiveQuestState(activeQuest.quest_id);
        _test.True(
            canonical != null && canonical.IsActive(),
            "修改查询得到的 QuestState 不得绕过 QuestJournalState 改写 canonical 状态。"
        );
        _test.True(
            !partyState.HasClaimableQuest(activeQuest.quest_id),
            "detached QuestState 的本地迁移不得造成跨集合残留。"
        );
    }

    private void TestCustomStatsRejectNonIntValues()
    {
        ExpectArgumentException(
            () => UnitCustomStatMap.FromDictionary(new GDictionary { ["storage_space"] = "bad" }),
            "custom_stats 中的非 int value 应在解析阶段被拒绝。"
        );
    }

    private void TestReputationsRejectNonIntValues()
    {
        ExpectArgumentException(
            () => UnitReputationMap.FromDictionary(new GDictionary { ["renown"] = "bad" }),
            "custom reputation 中的非 int value 应在解析阶段被拒绝。"
        );
    }

    private void TestValidTypedPayloadsRoundTrip()
    {
        PartyMemberState member = new() { member_id = "hero", display_name = "Hero" };
        member.progression.unit_id = "hero";
        member.progression.display_name = "Hero";
        PartyMemberStateCollection members = PartyMemberStateCollection.FromDictionary(
            new GDictionary { ["hero"] = member.ToDictionary() }
        );
        _test.Eq(members.Get("hero")?.display_name, "Hero", "typed member collection 应恢复成员 payload。");
        GDictionary memberProjection = members.ToDictionary();
        _test.True(memberProjection["hero"].VariantType == Variant.Type.Dictionary, "typed member collection 投影应输出成员存档字典。");
        _test.True(
            PartyMemberState.TryReadMemberPayload(memberProjection["hero"], out PartyMemberState projectedMember),
            "typed member collection 投影应能按成员 ID 读回成员 payload。"
        );
        _test.True(
            projectedMember?.member_id == (StringName)"hero",
            "typed member collection 投影应保留成员 ID。"
        );

        QuestObjectiveProgressState questProgress = QuestObjectiveProgressState.FromDictionary(
            new GDictionary { ["kill"] = 2 }
        );
        _test.Eq(questProgress.Get("kill"), 2, "typed objective progress 应读取 int 值。");
        _test.Eq(
            questProgress.ToDictionary()["kill"].AsInt32(),
            2,
            "typed objective progress 应 roundtrip 为 save 字典。"
        );

        UnitCustomStatMap customStats = UnitCustomStatMap.FromDictionary(
            new GDictionary { ["storage_space"] = 4 }
        );
        _test.Eq(customStats.Get("storage_space"), 4, "typed custom stat 应读取 int 值。");
        _test.Eq(
            customStats.ToDictionary()["storage_space"].AsInt32(),
            4,
            "typed custom stat 应 roundtrip 为 save 字典。"
        );

        UnitReputationMap reputations = UnitReputationMap.FromDictionary(
            new GDictionary { ["renown"] = 5 }
        );
        _test.Eq(reputations.Get("renown"), 5, "typed reputation 应读取 int 值。");
        _test.Eq(
            reputations.ToDictionary()["renown"].AsInt32(),
            5,
            "typed reputation 应 roundtrip 为 save 字典。"
        );

        QuestProgressContext context = QuestProgressContext.FromDictionary(
            new GDictionary { ["item_id"] = "sealed_dispatch", ["submitted_quantity"] = 1 }
        );
        GDictionary contextPayload = context.ToDictionary();
        _test.Eq(
            contextPayload["item_id"].AsString(),
            "sealed_dispatch",
            "typed quest context 应 roundtrip item_id。"
        );
        _test.Eq(
            contextPayload["submitted_quantity"].AsInt32(),
            1,
            "typed quest context 应 roundtrip submitted_quantity。"
        );
    }

    private void TestPartyStateUsesCurrentSaveVersion()
    {
        PartyState state = new();
        _test.Eq(state.version, 8, "PartyState save schema 应升级到 8。");
        _test.Eq(
            Convert.ToInt32(state.BuildSaveSnapshotPlain()["version"]),
            8,
            "PartyState save snapshot 应输出 schema 8。"
        );
    }

    private void ExpectArgumentException(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            _test.True(true, message);
            return;
        }

        _test.True(false, message);
    }
}
