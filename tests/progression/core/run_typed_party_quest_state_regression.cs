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
        TestSocialStandingStateContracts();
        TestPartyStateRejectsInvalidSocialStandingPayloads();
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
            () =>
                UnitReputationMap.FromDictionary(
                    new GDictionary { ["guild_esteem"] = "bad" }
                ),
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
            new GDictionary { ["guild_esteem"] = 5 }
        );
        _test.Eq(reputations.Get("guild_esteem"), 5, "typed reputation 应读取 int 值。");
        _test.Eq(
            reputations.ToDictionary()["guild_esteem"].AsInt32(),
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

    private void TestSocialStandingStateContracts()
    {
        PartyState state = BuildValidPartyState();
        _test.Eq(state.GetWorldRenown(), 0, "新队伍的世界名望应默认为 0。");

        _test.Eq(state.SetWorldRenown(140), 100, "世界名望写入应在上界饱和。");
        _test.Eq(state.AddWorldRenown(-170), 0, "世界名望增量应在下界饱和。");
        state.SetWorldRenown(63);

        _test.Eq(
            state.SetCountryReputation("frost_ash_empire", 35),
            35,
            "国家声望应按 country_id 写入。"
        );
        _test.Eq(
            state.SetCountryReputation("starfall_federation", -20),
            -20,
            "不同国家应拥有独立声望值。"
        );
        _test.Eq(
            state.AddCountryReputation("frost_ash_empire", 80),
            100,
            "单一国家声望增量应在上界饱和。"
        );
        _test.Eq(
            state.GetCountryReputation("starfall_federation"),
            -20,
            "修改帝国声望不得联动联邦声望。"
        );
        _test.Eq(
            state.AddCountryReputation("starfall_federation", -90),
            -100,
            "单一国家声望增量应在下界饱和。"
        );
        _test.Eq(
            state.GetCountryReputation("frost_ash_empire"),
            100,
            "修改联邦声望不得联动帝国声望。"
        );

        PartyState duplicate = state.DuplicateState();
        _test.Eq(duplicate.GetWorldRenown(), 63, "DuplicateState 应保留世界名望。");
        _test.Eq(
            duplicate.GetCountryReputation("frost_ash_empire"),
            100,
            "DuplicateState 应保留帝国声望。"
        );
        _test.Eq(
            duplicate.GetCountryReputation("starfall_federation"),
            -100,
            "DuplicateState 应保留联邦声望。"
        );
        duplicate.SetWorldRenown(7);
        duplicate.SetCountryReputation("frost_ash_empire", -45);
        _test.Eq(state.GetWorldRenown(), 63, "修改 duplicate 世界名望不得影响源队伍。");
        _test.Eq(
            state.GetCountryReputation("frost_ash_empire"),
            100,
            "修改 duplicate 国家声望不得影响源队伍。"
        );

        using GodotProjectionLease<GDictionary> payloadLease =
            state.ToDictionaryLease("TypedPartyQuestState.SocialStandingRoundTrip");
        GDictionary payload = payloadLease.Value;
        _test.Eq(
            payload["world_renown"].AsInt32(),
            63,
            "Party save payload 应输出世界名望。"
        );
        using GDictionary countryPayload =
            payload["country_reputations"].AsGodotDictionary();
        _test.Eq(
            countryPayload["frost_ash_empire"].AsInt32(),
            100,
            "Party save payload 应输出帝国声望。"
        );
        _test.Eq(
            countryPayload["starfall_federation"].AsInt32(),
            -100,
            "Party save payload 应输出联邦声望。"
        );

        PartyState restored = PartyState.FromDictionary(payload);
        _test.True(restored != null, "合法社会声望字段应通过 PartyState round-trip。");
        if (restored == null)
            return;
        _test.Eq(restored.GetWorldRenown(), 63, "round-trip 后应保留世界名望。");
        _test.Eq(
            restored.GetCountryReputation("frost_ash_empire"),
            100,
            "round-trip 后应保留帝国声望。"
        );
        _test.Eq(
            restored.GetCountryReputation("starfall_federation"),
            -100,
            "round-trip 后应保留联邦声望。"
        );
    }

    private void TestPartyStateRejectsInvalidSocialStandingPayloads()
    {
        AssertPartyPayloadRejected(
            payload => payload.Remove("world_renown"),
            "缺少 world_renown 的 PartyState payload 应被拒绝。"
        );
        AssertPartyPayloadRejected(
            payload => payload["world_renown"] = "bad",
            "非 int world_renown 应被拒绝。"
        );
        AssertPartyPayloadRejected(
            payload => payload["world_renown"] = -1,
            "低于下界的 world_renown 应被拒绝。"
        );
        AssertPartyPayloadRejected(
            payload => payload["world_renown"] = 101,
            "高于上界的 world_renown 应被拒绝。"
        );
        AssertPartyPayloadRejected(
            payload => payload["world_renown"] = long.MaxValue,
            "超出 int 范围的 world_renown 不得截断后通过校验。"
        );
        AssertPartyPayloadRejected(
            payload => payload.Remove("country_reputations"),
            "缺少 country_reputations 的 PartyState payload 应被拒绝。"
        );
        AssertPartyPayloadRejected(
            payload => payload["country_reputations"] = new Godot.Collections.Array(),
            "非 Dictionary country_reputations 应被拒绝。"
        );
        AssertPartyPayloadRejected(
            payload =>
            {
                using GDictionary countryPayload =
                    payload["country_reputations"].AsGodotDictionary();
                countryPayload["frost_ash_empire"] = "bad";
            },
            "国家声望的非 int value 应被拒绝。"
        );
        AssertPartyPayloadRejected(
            payload =>
            {
                using GDictionary countryPayload =
                    payload["country_reputations"].AsGodotDictionary();
                countryPayload["frost_ash_empire"] = -101;
            },
            "低于下界的国家声望应被拒绝。"
        );
        AssertPartyPayloadRejected(
            payload =>
            {
                using GDictionary countryPayload =
                    payload["country_reputations"].AsGodotDictionary();
                countryPayload["frost_ash_empire"] = 101;
            },
            "高于上界的国家声望应被拒绝。"
        );
        AssertPartyPayloadRejected(
            payload =>
            {
                using GDictionary countryPayload =
                    payload["country_reputations"].AsGodotDictionary();
                countryPayload["frost_ash_empire"] = long.MinValue;
            },
            "超出 int 范围的国家声望不得截断后通过校验。"
        );
        AssertPartyPayloadRejected(
            payload =>
            {
                using GDictionary countryPayload =
                    payload["country_reputations"].AsGodotDictionary();
                countryPayload.Clear();
                countryPayload[1] = 10;
            },
            "国家声望字典不得把非字符串键转换成 country_id。"
        );
    }

    private void TestPartyStateUsesCurrentSaveVersion()
    {
        PartyState state = new();
        _test.Eq(state.version, 9, "PartyState save schema 应升级到 9。");
        _test.Eq(
            Convert.ToInt32(state.BuildSaveSnapshotPlain()["version"]),
            9,
            "PartyState save snapshot 应输出 schema 9。"
        );
    }

    private void AssertPartyPayloadRejected(Action<GDictionary> mutate, string message)
    {
        PartyState state = BuildValidPartyState();
        state.SetWorldRenown(40);
        state.SetCountryReputation("frost_ash_empire", 25);
        using GodotProjectionLease<GDictionary> payloadLease =
            state.ToDictionaryLease("TypedPartyQuestState.InvalidSocialStanding");
        using GDictionary payload = (GDictionary)payloadLease.Value.Duplicate(true);
        mutate(payload);
        _test.True(PartyState.FromDictionary(payload) == null, message);
    }

    private static PartyState BuildValidPartyState()
    {
        PartyMemberState member = new()
        {
            member_id = "hero",
            display_name = "Hero",
        };
        member.progression.unit_id = "hero";
        member.progression.display_name = "Hero";

        PartyState state = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
        };
        state.SetMemberState(member);
        state.active_member_ids.Add("hero");
        return state;
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
