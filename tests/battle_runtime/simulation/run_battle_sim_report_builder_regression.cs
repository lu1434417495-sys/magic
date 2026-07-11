using System.Collections.Generic;
using Godot;

public partial class run_battle_sim_report_builder_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestResult exitCode = Run();
        RequestTestExit(exitCode);
    }

    private TestResult Run()
    {
        TestProfileSummaryExposesSkillAttemptAndFailureTotals();
        TestProfileComparisonsExposeAttemptAndFailureDeltas();

        return _test.Finish("Battle sim report builder regression");
    }

    private void TestProfileSummaryExposesSkillAttemptAndFailureTotals()
    {
        var builder = new BattleSimReportBuilder();
        BattleSimProfileDef profile = BuildProfile("baseline", "Baseline");
        BattleSimProfileSummary summary = builder.BuildProfileSummary(
            profile,
            new List<BattleSimRunReport> { BuildRunA(), BuildRunB() }
        );

        _test.Eq(GetInt(summary.SkillUsageTotals, "skill_alpha", -1), 3, "skill_alpha 成功次数应汇总两场 run。");
        _test.Eq(GetInt(summary.SkillUsageTotals, "skill_beta", -1), 1, "skill_beta 成功次数应被正确保留。");
        _test.Eq(GetInt(summary.SkillAttemptTotals, "skill_alpha", -1), 4, "skill_alpha 尝试次数应汇总两场 run。");
        _test.Eq(GetInt(summary.SkillAttemptTotals, "skill_beta", -1), 1, "skill_beta 尝试次数应被正确保留。");
        _test.Eq(GetInt(summary.SkillAttemptTotals, "skill_gamma", -1), 2, "skill_gamma 纯失败尝试也应被汇总。");
        _test.Eq(GetInt(summary.SkillFailureTotals, "skill_alpha", -1), 1, "skill_alpha 失败次数应等于 attempt-success。");
        _test.Eq(GetInt(summary.SkillFailureTotals, "skill_gamma", -1), 2, "skill_gamma 全失败时应保留全部失败次数。");
        _test.False(summary.SkillFailureTotals.ContainsKey("skill_beta"), "零失败技能不应写入 failure_totals。");
        _test.Eq(summary.AverageTimelineSteps, 3.0f, "timeline_steps 应按 run 平均汇总。");
    }

    private void TestProfileComparisonsExposeAttemptAndFailureDeltas()
    {
        var builder = new BattleSimReportBuilder();
        BattleSimProfileSummary baselineSummary = builder.BuildProfileSummary(
            BuildProfile("baseline", "Baseline"),
            new List<BattleSimRunReport> { BuildRunA(), BuildRunB() }
        );
        BattleSimProfileSummary candidateSummary = builder.BuildProfileSummary(
            BuildProfile("candidate", "Candidate"),
            new List<BattleSimRunReport> { BuildRunCandidate() }
        );
        var comparisons = builder.BuildProfileComparisons(
            new List<BattleSimProfileReportEntry>
            {
                new BattleSimProfileReportEntry
                {
                    Profile = BuildProfile("baseline", "Baseline"),
                    Summary = baselineSummary,
                },
                new BattleSimProfileReportEntry
                {
                    Profile = BuildProfile("candidate", "Candidate"),
                    Summary = candidateSummary,
                },
            }
        );
        _test.Eq(comparisons.Count, 1, "两组 summary 应产出一条 comparison。");
        if (comparisons.Count == 0)
            return;

        BattleSimProfileComparison comparison = comparisons[0];
        _test.Eq(GetInt(comparison.SkillAttemptDelta, "skill_alpha", 999), -1, "candidate 的 skill_alpha 尝试次数较 baseline 应少 1。");
        _test.Eq(GetInt(comparison.SkillFailureDelta, "skill_alpha", 999), -1, "candidate 的 skill_alpha 失败次数较 baseline 应少 1。");
        _test.Eq(GetInt(comparison.SkillAttemptDelta, "skill_gamma", 999), -2, "candidate 不再尝试 skill_gamma 时，attempt delta 应为 -2。");
        _test.Eq(GetInt(comparison.SkillFailureDelta, "skill_gamma", 999), -2, "candidate 不再失败 skill_gamma 时，failure delta 应为 -2。");
        _test.Eq(comparison.AverageTimelineStepsDelta, -1.0f, "candidate 的平均 timeline_steps 应比 baseline 少 1。");
    }

    private static BattleSimProfileDef BuildProfile(StringName profileId, string displayName)
    {
        return new BattleSimProfileDef { profile_id = profileId, display_name = displayName };
    }

    private static BattleSimRunReport BuildRunA()
    {
        return new BattleSimRunReport
        {
            WinnerFactionId = "player",
            FinalTu = 10,
            Iterations = 5,
            TimelineSteps = 2,
            MetricsSnapshot = BuildMetricsSnapshot(
                "unit_a",
                new Dictionary<string, int>
                {
                    ["skill_alpha"] = 3,
                    ["skill_beta"] = 1,
                },
                new Dictionary<string, int>
                {
                    ["skill_alpha"] = 2,
                    ["skill_beta"] = 1,
                }
            ),
            AiTurnTraces = System.Array.Empty<BattleAiTurnTraceProjection>(),
        };
    }

    private static BattleSimRunReport BuildRunB()
    {
        return new BattleSimRunReport
        {
            WinnerFactionId = "hostile",
            FinalTu = 20,
            Iterations = 8,
            TimelineSteps = 4,
            MetricsSnapshot = BuildMetricsSnapshot(
                "unit_b",
                new Dictionary<string, int>
                {
                    ["skill_alpha"] = 1,
                    ["skill_gamma"] = 2,
                },
                new Dictionary<string, int> { ["skill_alpha"] = 1 }
            ),
            AiTurnTraces = System.Array.Empty<BattleAiTurnTraceProjection>(),
        };
    }

    private static BattleSimRunReport BuildRunCandidate()
    {
        return new BattleSimRunReport
        {
            WinnerFactionId = "player",
            FinalTu = 9,
            Iterations = 4,
            TimelineSteps = 2,
            MetricsSnapshot = BuildMetricsSnapshot(
                "unit_candidate",
                new Dictionary<string, int>
                {
                    ["skill_alpha"] = 3,
                    ["skill_beta"] = 1,
                },
                new Dictionary<string, int>
                {
                    ["skill_alpha"] = 3,
                    ["skill_beta"] = 1,
                }
            ),
            AiTurnTraces = System.Array.Empty<BattleAiTurnTraceProjection>(),
        };
    }

    private static BattleSimMetricsSnapshot BuildMetricsSnapshot(
        string unitId,
        IReadOnlyDictionary<string, int> attempts,
        IReadOnlyDictionary<string, int> successes
    )
    {
        var state = new BattleMetricsState();
        var unit = new BattleMetricEntry { UnitId = unitId };
        foreach (KeyValuePair<string, int> entry in attempts)
            unit.SkillAttemptCounts[entry.Key] = entry.Value;
        foreach (KeyValuePair<string, int> entry in successes)
            unit.SkillSuccessCounts[entry.Key] = entry.Value;
        state.Units[unitId] = unit;
        return BattleSimMetricsSnapshot.Capture(state);
    }

    private static int GetInt(IReadOnlyDictionary<string, int> source, string key, int fallback = 0)
    {
        return source != null && source.TryGetValue(key, out int value) ? value : fallback;
    }

}
