using System;
using System.Collections.Generic;
using Godot;

public partial class run_quest_danger_rating_resolver_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private ContentSnapshot _snapshot;

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        _snapshot = GameSessionTestFactory.GetProcessSnapshot();
        TestStarThresholdBoundaries();
        TestOfficialBountyQuestStarTable();
        TestOverrideWinsOverDerivation();
        TestSingleBattleEnemyObjectiveIsRated();
        TestUnratedBoundaries();
        TestStarsLabelRendering();

        RequestTestExit(_test.Finish("Quest danger rating resolver regression"));
    }

    private void TestStarThresholdBoundaries()
    {
        foreach ((double threat, int expectedStars) in new[]
        {
            (0.0, 1), (3.99, 1), (4.0, 2), (7.99, 2), (8.0, 3),
            (11.99, 3), (12.0, 4), (19.99, 4), (20.0, 5), (100.0, 5),
        })
        {
            _test.Eq(
                QuestDangerRatingPolicy.ToStars(threat),
                expectedStars,
                $"threat={threat} 应映射到 {expectedStars} 星。"
            );
        }
    }

    private void TestOfficialBountyQuestStarTable()
    {
        foreach ((string questId, int expectedStars) in new[]
        {
            ("bounty_wolf_raider", 1),
            ("bounty_wolf_pack", 1),
            ("bounty_mist_harrier", 3),
            ("bounty_wolf_alpha", 2),
            ("bounty_mist_beast", 3),
            ("bounty_wolf_vanguard", 3),
            ("bounty_wolf_shaman", 3),
            ("bounty_mist_weaver", 3),
            ("ashen_abyss_survival", 4),
            ("ashen_dread_alpha", 4),
            ("ashen_rift_warden", 5),
        })
        {
            _test.True(
                _snapshot.Quests.TryGetValue(questId, out QuestDefinition questDefinition),
                $"正式内容应包含悬赏任务 {questId}。"
            );
            QuestDangerRatingResult rating = QuestDangerRatingResolver.Resolve(
                questDefinition,
                _snapshot.EnemyTemplates
            );
            _test.True(rating.IsRated, $"{questId} 应可推导危险度。");
            _test.Eq(
                rating.Source,
                QuestDangerRatingResult.SourceDerived,
                $"{questId} 危险度应来自公式推导。"
            );
            _test.Eq(rating.Stars, expectedStars, $"{questId} 应为 {expectedStars} 星。");
        }
    }

    private void TestOverrideWinsOverDerivation()
    {
        _test.True(
            _snapshot.Quests.TryGetValue(
                "contract_regional_bounty",
                out QuestDefinition overriddenQuest
            ),
            "正式内容应包含 contract_regional_bounty。"
        );
        QuestDangerRatingResult overriddenRating = QuestDangerRatingResolver.Resolve(
            overriddenQuest,
            _snapshot.EnemyTemplates
        );
        _test.True(overriddenRating.IsRated, "override 悬赏应视为已评级。");
        _test.Eq(
            overriddenRating.Source,
            QuestDangerRatingResult.SourceOverride,
            "contract_regional_bounty 危险度应来自作者 override。"
        );
        _test.Eq(overriddenRating.Stars, 2, "contract_regional_bounty override 应为 2 星。");

        QuestDefinition syntheticOverride = BuildQuestDefinition(
            "synthetic_override_quest",
            [BuildObjective("report_once", "settlement_action", "service:training", 1)],
            dangerTierOverride: 4
        );
        QuestDangerRatingResult syntheticRating = QuestDangerRatingResolver.Resolve(
            syntheticOverride,
            _snapshot.EnemyTemplates
        );
        _test.Eq(
            syntheticRating.Source,
            QuestDangerRatingResult.SourceOverride,
            "非战斗目标 + override 时 override 应优先生效。"
        );
        _test.Eq(syntheticRating.Stars, 4, "override=4 应直接映射为 4 星。");
    }

    private void TestSingleBattleEnemyObjectiveIsRated()
    {
        QuestDefinition quest = BuildQuestDefinition(
            "synthetic_single_battle_hunt",
            [
                BuildObjective(
                    "defeat_pack_together",
                    "defeat_enemy_in_single_battle",
                    "wolf_pack",
                    5
                ),
            ]
        );

        QuestDangerRatingResult rating = QuestDangerRatingResolver.Resolve(
            quest,
            _snapshot.EnemyTemplates
        );

        _test.True(rating.IsRated, "单场击败敌人的目标应沿用敌人威胁度推导。");
        _test.Eq(
            rating.Source,
            QuestDangerRatingResult.SourceDerived,
            "单场击败敌人的目标应得到公式推导评级。"
        );
    }

    private void TestUnratedBoundaries()
    {
        QuestDangerRatingResult nullRating = QuestDangerRatingResolver.Resolve(
            null,
            _snapshot.EnemyTemplates
        );
        _test.False(nullRating.IsRated, "null 任务应返回未评级。");
        _test.Eq(
            nullRating.Source,
            QuestDangerRatingResult.SourceUnrated,
            "null 任务的评级来源应为 unrated。"
        );

        QuestDefinition emptyTargetQuest = BuildQuestDefinition(
            "synthetic_empty_target_quest",
            [BuildObjective("defeat_any", "defeat_enemy", "", 1)]
        );
        QuestDangerRatingResult emptyTargetRating = QuestDangerRatingResolver.Resolve(
            emptyTargetQuest,
            _snapshot.EnemyTemplates
        );
        _test.False(emptyTargetRating.IsRated, "空 target_id 的 defeat_enemy 应返回未评级。");

        QuestDefinition missingTemplateQuest = BuildQuestDefinition(
            "synthetic_missing_template_quest",
            [BuildObjective("defeat_missing", "defeat_enemy", "missing_enemy_template", 1)]
        );
        QuestDangerRatingResult missingTemplateRating = QuestDangerRatingResolver.Resolve(
            missingTemplateQuest,
            _snapshot.EnemyTemplates
        );
        _test.False(missingTemplateRating.IsRated, "缺失敌人模板时应返回未评级。");
        _test.Eq(
            missingTemplateRating.MissingTargetIds.Count,
            1,
            "缺失敌人模板时应记录缺失的 target_id。"
        );
        _test.Eq(
            missingTemplateRating.MissingTargetIds[0],
            new StringName("missing_enemy_template"),
            "缺失记录应指向具体 target_id。"
        );

        QuestDefinition nonCombatQuest = BuildQuestDefinition(
            "synthetic_non_combat_quest",
            [BuildObjective("report_once", "settlement_action", "service:training", 1)]
        );
        QuestDangerRatingResult nonCombatRating = QuestDangerRatingResolver.Resolve(
            nonCombatQuest,
            _snapshot.EnemyTemplates
        );
        _test.False(nonCombatRating.IsRated, "非战斗目标且无 override 时应返回未评级。");
    }

    private void TestStarsLabelRendering()
    {
        QuestDangerRatingResult unrated = QuestDangerRatingResolver.Resolve(
            null,
            _snapshot.EnemyTemplates
        );
        _test.Eq(
            QuestDangerRatingResolver.BuildStarsLabel(unrated),
            "危险度：未评级",
            "未评级应渲染未评级文案。"
        );
        _test.Eq(
            QuestDangerRatingResolver.BuildStarsLabel(null),
            "危险度：未评级",
            "null 结果应渲染未评级文案。"
        );

        QuestDefinition overriddenQuest = BuildQuestDefinition(
            "synthetic_label_quest",
            [BuildObjective("report_once", "settlement_action", "service:training", 1)],
            dangerTierOverride: 3
        );
        QuestDangerRatingResult overriddenRating = QuestDangerRatingResolver.Resolve(
            overriddenQuest,
            _snapshot.EnemyTemplates
        );
        _test.Eq(
            QuestDangerRatingResolver.BuildStarsLabel(overriddenRating),
            "危险度：★★★☆☆",
            "3 星应渲染为三实两空。"
        );
    }

    private static QuestDefinition BuildQuestDefinition(
        StringName questId,
        IReadOnlyList<QuestObjectiveDefinition> objectives,
        int dangerTierOverride = 0
    )
    {
        return new QuestDefinition(
            questId,
            "Synthetic Bounty",
            "危险度推导测试用任务。",
            "service_bounty_registry",
            Array.Empty<StringName>(),
            Array.Empty<QuestAcceptRequirementDefinition>(),
            objectives,
            [
                new QuestRewardDefinition(
                    "gold",
                    10,
                    "",
                    0,
                    "",
                    Array.Empty<QuestPendingRewardEntryDefinition>()
                ),
            ],
            false,
            "service_bounty_registry",
            [new StringName("bounty_registry")],
            "",
            "",
            "",
            "",
            dangerTierOverride
        );
    }

    private static QuestObjectiveDefinition BuildObjective(
        string objectiveId,
        string objectiveType,
        string targetId,
        int targetValue
    ) => new(objectiveId, objectiveType, targetId, targetValue);
}
