using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_sim_typed_report_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestStringNameSkillLevelKeysAreHonored();
        TestMalformedScenarioUnitEntryIsRejectedBeforeSpawnProjection();
        TestTypedMetricsSnapshotFeedsProfileSummary();
        Quit(_test.Finish("Battle sim typed report regression"));
    }

    private void TestStringNameSkillLevelKeysAreHonored()
    {
        StringName skillId = "typed_slash";
        var spec = new BattleSimUnitSpec
        {
            unit_id = "sim_unit",
            display_name = "Sim Unit",
            skill_ids = new GArray { skillId },
            skill_level_map = new GDictionary { [skillId] = 4 },
        };

        BattleUnitState unitState = spec.ToBattleUnitState("player", "manual");

        _test.Eq(
            unitState.GetKnownSkillLevelTyped(skillId),
            4,
            "BattleSimUnitSpec skill_level_map 应支持 StringName key。"
        );
    }

    private void TestMalformedScenarioUnitEntryIsRejectedBeforeSpawnProjection()
    {
        var scenario = new BattleSimScenarioDef
        {
            scenario_id = "malformed_units",
            ally_units = new GArray { new GDictionary { ["unit_id"] = "not_a_resource" } },
        };

        bool rejected = false;
        try
        {
            scenario.BuildStartContext();
        }
        catch (InvalidOperationException error)
        {
            rejected = error.Message.Contains("ally_units[0]");
        }

        _test.True(
            rejected,
            "BattleSimScenarioDef 应在 typed entry 构建阶段拒绝 malformed ally_units entry。"
        );
    }

    private void TestTypedMetricsSnapshotFeedsProfileSummary()
    {
        StringName skillId = "typed_fire";
        BattleSimMetricsSnapshot metrics = BattleSimMetricsSnapshot.FromDictionary(
            new GDictionary
            {
                ["units"] = new GDictionary
                {
                    ["caster"] = new GDictionary
                    {
                        ["skill_attempt_counts"] = new GDictionary { [skillId] = 3 },
                        ["skill_success_counts"] = new GDictionary { [skillId] = 2 },
                    },
                },
                ["factions"] = new GDictionary
                {
                    ["player"] = new GDictionary
                    {
                        ["unit_count"] = 1,
                        ["successful_skill_count"] = 2,
                    },
                },
            }
        );
        var report = new BattleSimRunReport
        {
            WinnerFactionId = "player",
            MetricsSnapshot = metrics,
            AiTurnTraces = Array.Empty<BattleAiTurnTraceProjection>(),
        };

        BattleSimProfileSummary summary = new BattleSimReportBuilder().BuildProfileSummary(
            new BattleSimProfileDef { profile_id = "baseline" },
            new List<BattleSimRunReport> { report }
        );

        _test.Eq(
            GetInt(summary.SkillAttemptTotals, skillId.ToString()),
            3,
            "typed metrics snapshot 应保留 StringName skill attempt key。"
        );
        _test.Eq(
            GetInt(summary.SkillFailureTotals, skillId.ToString()),
            1,
            "typed metrics snapshot 应继续派生 failure totals。"
        );
        _test.Eq(
            summary.FactionMetricTotals["player"].SuccessfulSkillCount,
            2,
            "typed metrics snapshot 应保留 faction metrics。"
        );
    }

    private static int GetInt(IReadOnlyDictionary<string, int> source, string key)
    {
        return source != null && source.TryGetValue(key, out int value) ? value : 0;
    }
}
