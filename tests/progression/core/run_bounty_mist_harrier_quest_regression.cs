using System;
using System.Collections.Generic;
using Godot;

public partial class run_bounty_mist_harrier_quest_regression : LifecycleTestSceneTree
{
    private static readonly StringName QuestId = "bounty_mist_harrier";
    private static readonly StringName ObjectiveId = "defeat_harriers";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            using TestContentResourceLoader loader = new();
            QuestDefinition quest = LoadQuest(loader);
            TestAuthoredBountyContract(quest);
            TestCrossBattleProgressClaimAndRepeat(quest);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        RequestTestExit(_test.Finish("Bounty mist harrier quest regression"));
    }

    private void TestAuthoredBountyContract(QuestDefinition quest)
    {
        _test.True(quest != null, "《迷雾猎手》正式任务资源应可加载并投影。");
        if (quest == null)
            return;

        _test.Eq(quest.QuestId, QuestId, "《迷雾猎手》quest_id 应稳定。");
        _test.Eq(quest.DisplayName, "迷雾猎手", "任务显示名称应为《迷雾猎手》。");
        _test.True(
            quest.Description.Contains("雾沼猎压者"),
            "任务说明应使用正式敌人名称“雾沼猎压者”。"
        );
        _test.Eq(
            QuestProviderContentRules.ToProviderKind(quest),
            QuestProviderKind.ServiceBountyRegistry,
            "《迷雾猎手》应由悬赏署提供。"
        );
        _test.True(
            QuestProviderContentRules
                .ToListingChannels(quest)
                .Contains(QuestListingChannel.BountyRegistry),
            "《迷雾猎手》应进入 bounty_registry 渠道。"
        );
        _test.True(
            Contains(quest.ListingSettlementIds, "template_town"),
            "《迷雾猎手》应能在城镇悬赏署出现。"
        );
        _test.True(
            Contains(quest.ListingSettlementIds, "template_city"),
            "《迷雾猎手》应能在城市悬赏署出现。"
        );
        _test.False(
            Contains(quest.ListingSettlementIds, "template_village"),
            "《迷雾猎手》不得绑定初始村落。"
        );
        _test.True(quest.IsRepeatable, "《迷雾猎手》完成领奖后应允许再次接取。");
        _test.True(
            !string.IsNullOrWhiteSpace(quest.AcceptDialogueText),
            "悬赏详情应提供正式接取文本。"
        );
        _test.True(
            quest.AcceptFeedbackSuccess.Contains("三只雾沼猎压者"),
            "接取成功反馈应明确三只雾沼猎压者的目标。"
        );
        _test.True(
            !string.IsNullOrWhiteSpace(quest.AcceptFeedbackFailure),
            "接取失败时应提供任务专属反馈。"
        );
        _test.Eq(
            quest.AcceptConfirmationText,
            "",
            "悬赏署不做逐项确认，《迷雾猎手》应保持直接接取。"
        );

        _test.Eq(quest.Objectives.Count, 1, "《迷雾猎手》应只有一个清剿目标。");
        if (quest.Objectives.Count == 1)
        {
            QuestObjectiveDefinition objective = quest.Objectives[0];
            _test.Eq(objective.ObjectiveId, ObjectiveId, "清剿目标 ID 应稳定。");
            _test.Eq(
                objective.ObjectiveType,
                QuestDef.ToStringName(QuestObjectiveKind.DefeatEnemy),
                "《迷雾猎手》应跨战累计普通 defeat_enemy 事件。"
            );
            _test.Eq(
                objective.TargetId,
                new StringName("mist_harrier"),
                "清剿目标应匹配正式雾沼猎压者模板。"
            );
            _test.Eq(objective.TargetValue, 3, "《迷雾猎手》应要求累计击败三只。");
        }

        _test.Eq(quest.Rewards.Count, 1, "《迷雾猎手》应只有一项金币奖励。");
        if (quest.Rewards.Count == 1)
        {
            QuestRewardDefinition reward = quest.Rewards[0];
            _test.Eq(
                reward.RewardType,
                QuestDef.ToStringName(QuestRewardKind.Gold),
                "《迷雾猎手》应奖励金币。"
            );
            _test.Eq(reward.GoldAmount, 250, "《迷雾猎手》应奖励250金币。");
        }
    }

    private void TestCrossBattleProgressClaimAndRepeat(QuestDefinition quest)
    {
        if (quest == null)
            return;

        PartyState party = new();
        using CharacterManagementModule manager = BuildManager(party, quest);
        _test.True(manager.AcceptQuest(QuestId, 1), "《迷雾猎手》应能通过正式任务服务接取。");

        manager.ApplyQuestProgressEventsTyped(
            new[]
            {
                BuildDefeatEvent("mist_harrier", 1, 2, "mist_hollow_a"),
            }
        );
        _test.Eq(
            party.GetActiveQuestState(QuestId)?.GetObjectiveProgress(ObjectiveId) ?? -1,
            1,
            "第一场击败一只雾沼猎压者后应记录1/3。"
        );

        manager.ApplyQuestProgressEventsTyped(
            new[]
            {
                BuildDefeatEvent("mist_beast", 4, 3, "mist_hollow_a"),
            }
        );
        _test.Eq(
            party.GetActiveQuestState(QuestId)?.GetObjectiveProgress(ObjectiveId) ?? -1,
            1,
            "击败其它敌人不得推进《迷雾猎手》。"
        );

        manager.ApplyQuestProgressEventsTyped(
            new[]
            {
                BuildDefeatEvent("mist_harrier", 2, 4, "mist_hollow_b"),
            }
        );
        _test.True(
            party.HasClaimableQuest(QuestId),
            "第二场再击败两只后，跨战累计3/3应进入待领奖励。"
        );
        _test.Eq(
            party.GetClaimableQuestState(QuestId)?.GetObjectiveProgress(ObjectiveId) ?? -1,
            3,
            "待领奖励状态应保留3/3清剿进度。"
        );

        int goldBefore = party.GetGold();
        QuestClaimResultData claim = manager.ClaimQuestRewardTyped(QuestId, 5);
        _test.True(claim.Ok, "《迷雾猎手》应能通过正式奖励事务领取报酬。");
        _test.Eq(claim.GoldDelta, 250, "领奖结果应报告250金币。");
        _test.Eq(party.GetGold(), goldBefore + 250, "250金币应写入队伍正式状态。");
        _test.True(party.HasCompletedQuest(QuestId), "领奖后任务应进入已完成集合。");
        _test.False(party.HasClaimableQuest(QuestId), "领奖后任务不得继续停留在待领奖励。");

        _test.True(
            manager.AcceptQuest(QuestId, 6, allow_reaccept: true),
            "repeatable 悬赏领奖后应允许再次接取。"
        );
        _test.True(party.HasActiveQuest(QuestId), "再次接取后任务应重新进入进行中。");
        _test.Eq(
            party.GetActiveQuestState(QuestId)?.GetObjectiveProgress(ObjectiveId) ?? -1,
            0,
            "再次接取必须从0/3重新开始。"
        );
    }

    private static QuestDefinition LoadQuest(TestContentResourceLoader loader)
    {
        const string path = "res://data/configs/quests/bounty_mist_harrier.tres";
        QuestDef resource = loader.LoadCanonical<QuestDef>(path);
        return resource != null ? QuestDefinition.FromResource(resource, path) : null;
    }

    private static CharacterManagementModule BuildManager(
        PartyState party,
        QuestDefinition quest
    )
    {
        var manager = new CharacterManagementModule();
        manager.setup(
            party,
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, ProfessionDefinition>(),
            new Dictionary<StringName, AchievementDefinition>(),
            new Dictionary<StringName, ItemDefinition>(),
            new Dictionary<StringName, QuestDefinition> { [quest.QuestId] = quest }
        );
        return manager;
    }

    private static QuestProgressService.QuestProgressEventData BuildDefeatEvent(
        StringName enemyTemplateId,
        int count,
        int worldStep,
        StringName encounterId
    ) =>
        QuestProgressService.QuestProgressEventData.CreateProgressByObjectiveTarget(
            QuestDef.ToStringName(QuestObjectiveKind.DefeatEnemy),
            enemyTemplateId,
            count,
            worldStep,
            enemyTemplateId,
            encounterId,
            "single"
        );

    private static bool Contains(IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value == expected)
                return true;
        }
        return false;
    }
}
