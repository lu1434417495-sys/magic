using System.Collections.Generic;
using Godot;
public partial class run_wild_encounter_growth_system_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestStepAdvanceUsesTypedRosterFields();
        TestBattleVictoryUsesTypedRosterFields();
        TestMissingRosterIsRejected();

        RequestTestExit(_test.Finish("Wild encounter growth system regression"));
    }

    private void TestStepAdvanceUsesTypedRosterFields()
    {
        WildEncounterGrowthSystem growthSystem = new();
        EncounterAnchorData encounterAnchor = BuildSettlementAnchor(growthStage: 0);
        WildEncounterRosterDefinition roster = BuildRoster();
        var encounterAnchors = new List<EncounterAnchorData> { encounterAnchor };
        var rosters = new Dictionary<StringName, WildEncounterRosterDefinition>
        {
            ["wolf_den"] = roster,
        };
        IReadOnlyDictionary<StringName, BattleEncounterDefinition> battleEncounters =
            BuildBattleEncounters();

        bool changed = growthSystem.ApplyStepAdvance(
            encounterAnchors,
            0,
            2,
            battleEncounters,
            rosters
        );

        _test.True(changed, "到达成长间隔时应报告变更。");
        _test.Eq(encounterAnchor.growth_stage, 1, "聚落类野怪应按 roster.growth_step_interval 提升阶段。");

        bool cappedChange = growthSystem.ApplyStepAdvance(
            encounterAnchors,
            2,
            20,
            battleEncounters,
            rosters
        );
        _test.True(cappedChange, "继续推进到上限前应报告变更。");
        _test.Eq(encounterAnchor.growth_stage, 2, "成长阶段不应超过 roster.GetMaxStage()。");
    }

    private void TestBattleVictoryUsesTypedRosterFields()
    {
        WildEncounterGrowthSystem growthSystem = new();
        EncounterAnchorData encounterAnchor = BuildSettlementAnchor(growthStage: 2);
        WildEncounterRosterDefinition roster = BuildRoster();
        var rosters = new Dictionary<StringName, WildEncounterRosterDefinition>
        {
            ["wolf_den"] = roster,
        };
        IReadOnlyDictionary<StringName, BattleEncounterDefinition> battleEncounters =
            BuildBattleEncounters();

        bool changed = growthSystem.ApplyBattleSuppression(
            encounterAnchor,
            5,
            battleEncounters,
            rosters
        );

        _test.True(changed, "聚落类野怪战斗胜利应应用成长回退。");
        _test.Eq(encounterAnchor.growth_stage, 1, "战斗胜利后应下降 1 个成长阶段，但不低于 initial_stage。");
        _test.Eq(
            encounterAnchor.suppressed_until_step,
            8,
            "战斗胜利后应按 BattleEncounter world resolution 写入压制截止 step。"
        );
    }

    private void TestMissingRosterIsRejected()
    {
        WildEncounterGrowthSystem growthSystem = new();
        EncounterAnchorData encounterAnchor = BuildSettlementAnchor(growthStage: 0);
        var encounterAnchors = new List<EncounterAnchorData> { encounterAnchor };
        var rosters = new Dictionary<StringName, WildEncounterRosterDefinition>();

        bool changed = growthSystem.ApplyStepAdvance(
            encounterAnchors,
            0,
            10,
            BuildBattleEncounters(),
            rosters
        );

        _test.False(changed, "缺少 typed WildEncounterRosterDefinition 时不应推进成长阶段。");
        _test.Eq(encounterAnchor.growth_stage, 0, "无有效 typed roster 时不应推进成长阶段。");
    }

    private static EncounterAnchorData BuildSettlementAnchor(int growthStage)
    {
        return new EncounterAnchorData
        {
            entity_id = "wolf_den_anchor",
            encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Settlement),
            encounter_profile_id = "wolf_den",
            growth_stage = growthStage,
            suppressed_until_step = 0,
        };
    }

    private static WildEncounterRosterDefinition BuildRoster()
    {
        return TestEnemyDefinitionFactory.Roster(
            "wolf_den",
            new[] { BuildStage(0), BuildStage(1), BuildStage(2) },
            displayName: "Wolf Den",
            initialStage: 0,
            growthStepInterval: 2
        );
    }

    private static WildEncounterRosterStageDefinition BuildStage(int stage) =>
        TestEnemyDefinitionFactory.RosterStage(
            stage,
            TestEnemyDefinitionFactory.RosterUnit("wolf")
        );

    private static IReadOnlyDictionary<StringName, BattleEncounterDefinition>
        BuildBattleEncounters() =>
            new Dictionary<StringName, BattleEncounterDefinition>
            {
                ["wolf_den"] = new BattleEncounterDefinition(
                    "wolf_den",
                    "Wolf Den",
                    "wolf_den",
                    BattleEliminationObjectiveDefinition.Instance,
                    new BattleEncounterWorldResolutionDefinition(
                        BattleWorldResolutionMode.Suppress,
                        BattleWorldResolutionMode.Preserve,
                        BattleWorldResolutionMode.Preserve,
                        3
                    )
                ),
            };
}
