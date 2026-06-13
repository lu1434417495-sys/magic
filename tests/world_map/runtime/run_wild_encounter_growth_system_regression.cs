using System.Collections.Generic;
using Godot;
public partial class run_wild_encounter_growth_system_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestStepAdvanceUsesTypedRosterFields();
        TestBattleVictoryUsesTypedRosterFields();
        TestMissingRosterIsRejected();

        Quit(_test.Finish("Wild encounter growth system regression"));
    }

    private void TestStepAdvanceUsesTypedRosterFields()
    {
        WildEncounterGrowthSystem growthSystem = new();
        using EncounterAnchorData encounterAnchor = BuildSettlementAnchor(growthStage: 0);
        using WildEncounterRosterDef roster = BuildRoster();
        var encounterAnchors = new List<EncounterAnchorData> { encounterAnchor };
        var rosters = new Dictionary<StringName, WildEncounterRosterDef>
        {
            ["wolf_den"] = roster,
        };

        bool changed = growthSystem.ApplyStepAdvance(encounterAnchors, 0, 2, rosters);

        _test.True(changed, "到达成长间隔时应报告变更。");
        _test.Eq(encounterAnchor.growth_stage, 1, "聚落类野怪应按 roster.growth_step_interval 提升阶段。");

        bool cappedChange = growthSystem.ApplyStepAdvance(encounterAnchors, 2, 20, rosters);
        _test.True(cappedChange, "继续推进到上限前应报告变更。");
        _test.Eq(encounterAnchor.growth_stage, 2, "成长阶段不应超过 roster.GetMaxStage()。");
    }

    private void TestBattleVictoryUsesTypedRosterFields()
    {
        WildEncounterGrowthSystem growthSystem = new();
        using EncounterAnchorData encounterAnchor = BuildSettlementAnchor(growthStage: 2);
        using WildEncounterRosterDef roster = BuildRoster();
        var rosters = new Dictionary<StringName, WildEncounterRosterDef>
        {
            ["wolf_den"] = roster,
        };

        bool changed = growthSystem.ApplyBattleVictory(encounterAnchor, 5, rosters);

        _test.True(changed, "聚落类野怪战斗胜利应应用成长回退。");
        _test.Eq(encounterAnchor.growth_stage, 1, "战斗胜利后应下降 1 个成长阶段，但不低于 initial_stage。");
        _test.Eq(
            encounterAnchor.suppressed_until_step,
            8,
            "战斗胜利后应按 roster.suppression_steps_on_victory 写入压制截止 step。"
        );
    }

    private void TestMissingRosterIsRejected()
    {
        WildEncounterGrowthSystem growthSystem = new();
        using EncounterAnchorData encounterAnchor = BuildSettlementAnchor(growthStage: 0);
        var encounterAnchors = new List<EncounterAnchorData> { encounterAnchor };
        var rosters = new Dictionary<StringName, WildEncounterRosterDef>();

        bool changed = growthSystem.ApplyStepAdvance(encounterAnchors, 0, 10, rosters);

        _test.False(changed, "缺少 typed WildEncounterRosterDef 时不应推进成长阶段。");
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

    private static WildEncounterRosterDef BuildRoster()
    {
        WildEncounterRosterDef roster = new()
        {
            profile_id = "wolf_den",
            display_name = "Wolf Den",
            initial_stage = 0,
            growth_step_interval = 2,
            suppression_steps_on_victory = 3,
        };
        roster.stages.Add(BuildStage(0));
        roster.stages.Add(BuildStage(1));
        roster.stages.Add(BuildStage(2));
        return roster;
    }

    private static WildEncounterRosterStageDef BuildStage(int stage)
    {
        WildEncounterRosterStageDef stageDef = new()
        {
            stage = stage,
        };
        stageDef.unit_entries.Add(
            new WildEncounterRosterUnitEntryDef
            {
                template_id = "wolf",
                count = 1,
            }
        );
        return stageDef;
    }
}
