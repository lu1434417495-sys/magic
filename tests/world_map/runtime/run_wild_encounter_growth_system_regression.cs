using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_wild_encounter_growth_system_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestStepAdvanceUsesTypedRosterFields();
        TestBattleVictoryUsesTypedRosterFields();
        TestNonRosterPayloadIsRejected();

        if (_failures.Count == 0)
        {
            GD.Print("Wild encounter growth system regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Wild encounter growth system regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestStepAdvanceUsesTypedRosterFields()
    {
        using WildEncounterGrowthSystem growthSystem = new();
        using EncounterAnchorData encounterAnchor = BuildSettlementAnchor(growthStage: 0);
        using WildEncounterRosterDef roster = BuildRoster();
        GDictionary worldData = new() { ["encounter_anchors"] = new GArray { encounterAnchor } };
        GDictionary rosters = new() { ["wolf_den"] = roster };

        bool changed = growthSystem.apply_step_advance(worldData, 0, 2, rosters);

        AssertTrue(changed, "到达成长间隔时应报告变更。");
        AssertEq(encounterAnchor.growth_stage, 1, "聚落类野怪应按 roster.growth_step_interval 提升阶段。");

        bool cappedChange = growthSystem.apply_step_advance(worldData, 2, 20, rosters);
        AssertTrue(cappedChange, "继续推进到上限前应报告变更。");
        AssertEq(encounterAnchor.growth_stage, 2, "成长阶段不应超过 roster.get_max_stage()。");
    }

    private void TestBattleVictoryUsesTypedRosterFields()
    {
        using WildEncounterGrowthSystem growthSystem = new();
        using EncounterAnchorData encounterAnchor = BuildSettlementAnchor(growthStage: 2);
        using WildEncounterRosterDef roster = BuildRoster();
        GDictionary rosters = new() { ["wolf_den"] = roster };

        bool changed = growthSystem.apply_battle_victory(encounterAnchor, 5, rosters);

        AssertTrue(changed, "聚落类野怪战斗胜利应应用成长回退。");
        AssertEq(encounterAnchor.growth_stage, 1, "战斗胜利后应下降 1 个成长阶段，但不低于 initial_stage。");
        AssertEq(
            encounterAnchor.suppressed_until_step,
            8,
            "战斗胜利后应按 roster.suppression_steps_on_victory 写入压制截止 step。"
        );
    }

    private void TestNonRosterPayloadIsRejected()
    {
        using WildEncounterGrowthSystem growthSystem = new();
        using EncounterAnchorData encounterAnchor = BuildSettlementAnchor(growthStage: 0);
        GDictionary worldData = new() { ["encounter_anchors"] = new GArray { encounterAnchor } };
        GDictionary rosters = new() { ["wolf_den"] = new GDictionary() };

        bool changed = growthSystem.apply_step_advance(worldData, 0, 10, rosters);

        AssertFalse(changed, "非 WildEncounterRosterDef payload 不应被动态读取成有效 roster。");
        AssertEq(encounterAnchor.growth_stage, 0, "无有效 typed roster 时不应推进成长阶段。");
    }

    private static EncounterAnchorData BuildSettlementAnchor(int growthStage)
    {
        return new EncounterAnchorData
        {
            entity_id = "wolf_den_anchor",
            encounter_kind = EncounterAnchorData.ENCOUNTER_KIND_SETTLEMENT(),
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

    private static GDictionary BuildStage(int stage)
    {
        return new GDictionary
        {
            ["stage"] = stage,
            ["unit_entries"] = new GArray
            {
                new GDictionary { ["template_id"] = "wolf", ["count"] = 1 },
            },
        };
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
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
