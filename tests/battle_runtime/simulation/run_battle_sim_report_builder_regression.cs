using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

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
        TestExecutionLoopTerminationClassification();
        TestUnfinishedRunsRemainDiagnosticOnly();
        TestEndedRunWithoutFinalDecisionIsInvalid();
        TestNullRunProjectsInvalidPlaceholder();
        TestProfileComparisonsExposeAttemptAndFailureDeltas();

        return _test.Finish("Battle sim report builder regression");
    }

    private void TestExecutionLoopTerminationClassification()
    {
        var runtime = new BattleRuntimeModule();
        try
        {
            var endedState = new BattleState
            {
                PhaseKind = BattlePhaseKind.BattleEnded,
            };
            var runningState = new BattleState
            {
                PhaseKind = BattlePhaseKind.TimelineRunning,
            };

            runtime.SetupStateForTests(endedState);
            _test.Eq(
                BattleSimExecutionLoop.ResolveTerminationKind(
                    runtime,
                    endedState,
                    iterations: 0,
                    maxIterations: 0,
                    idleStalled: false
                ),
                BattleSimTerminationKind.BattleEnded,
                "已结束状态应优先于迭代预算判定。"
            );
            runtime.SetupStateForTests(runningState);
            _test.Eq(
                BattleSimExecutionLoop.ResolveTerminationKind(
                    runtime,
                    runningState,
                    iterations: 1,
                    maxIterations: 10,
                    idleStalled: true
                ),
                BattleSimTerminationKind.IdleStall,
                "idle guard 终止应分类为 IdleStall。"
            );
            _test.Eq(
                BattleSimExecutionLoop.ResolveTerminationKind(
                    runtime,
                    runningState,
                    iterations: 10,
                    maxIterations: 10,
                    idleStalled: false
                ),
                BattleSimTerminationKind.IterationBudgetExhausted,
                "达到最大迭代且战斗未结束时应分类为预算耗尽。"
            );
            _test.Eq(
                BattleSimExecutionLoop.ResolveTerminationKind(
                    null,
                    runningState,
                    iterations: 0,
                    maxIterations: 10,
                    idleStalled: false
                ),
                BattleSimTerminationKind.InvalidRuntime,
                "缺失 runtime 时应分类为 InvalidRuntime。"
            );
            _test.Eq(
                BattleSimExecutionLoop.ResolveTerminationKind(
                    runtime,
                    endedState,
                    iterations: 0,
                    maxIterations: 10,
                    idleStalled: false
                ),
                BattleSimTerminationKind.InvalidRuntime,
                "非 runtime 当前持有的 state 应分类为 InvalidRuntime。"
            );
        }
        finally
        {
            runtime.dispose();
        }
    }

    private void TestUnfinishedRunsRemainDiagnosticOnly()
    {
        var completed = BuildRunA();
        completed.FinalTu = 20;
        completed.Iterations = 8;
        completed.TimelineSteps = 4;
        var stalled = new BattleSimRunReport
        {
            TerminationKind = BattleSimTerminationKind.IdleStall,
            FinalTu = 900,
            Iterations = 901,
            TimelineSteps = 902,
            MetricsSnapshot = BuildMetricsSnapshot(
                "stalled_unit",
                new Dictionary<string, int> { ["unfinished_only"] = 99 },
                new Dictionary<string, int> { ["unfinished_only"] = 98 }
            ),
            AiTurnTraces = System.Array.Empty<BattleAiTurnTraceProjection>(),
        };
        var budgetExhausted = new BattleSimRunReport
        {
            TerminationKind = BattleSimTerminationKind.IterationBudgetExhausted,
            FinalTu = 990,
            Iterations = 991,
            TimelineSteps = 992,
            MetricsSnapshot = BuildMetricsSnapshot(
                "budget_unit",
                new Dictionary<string, int> { ["unfinished_only"] = 199 },
                new Dictionary<string, int> { ["unfinished_only"] = 198 }
            ),
            AiTurnTraces = System.Array.Empty<BattleAiTurnTraceProjection>(),
        };
        var runs = new List<BattleSimRunReport> { completed, stalled, budgetExhausted };
        BattleSimProfileDefinition profile = BuildProfile("baseline", "Baseline");
        BattleSimProfileSummary summary = new BattleSimReportBuilder().BuildProfileSummary(
            profile,
            runs
        );

        _test.Eq(summary.RunCount, 3, "run_count 应保留全部尝试，供诊断未完成场次。");
        _test.Eq(summary.CompletedRunCount, 1, "只有正常结束的 run 才是 completed sample。");
        _test.Eq(summary.UnfinishedRunCount, 2, "idle stall 与迭代预算耗尽都应计入 unfinished。");
        _test.Eq(summary.StalledRunCount, 1, "idle stall 应有独立诊断计数。");
        _test.Eq(
            summary.IterationBudgetExhaustedRunCount,
            1,
            "迭代预算耗尽应有独立诊断计数。"
        );
        _test.Eq(summary.InvalidRuntimeRunCount, 0, "本 fixture 不应产生 invalid runtime。");
        _test.True(summary.HasUnfinishedRuns, "summary 应显式暴露存在未完成样本。");
        _test.False(summary.IsComplete, "包含未完成样本的 summary 不得声明完整。");
        _test.Eq(summary.AverageFinalTu, 20.0f, "平均 TU 只能使用 completed sample。");
        _test.Eq(summary.AverageIterations, 8.0f, "平均迭代数只能使用 completed sample。");
        _test.Eq(summary.AverageTimelineSteps, 4.0f, "平均 timeline step 只能使用 completed sample。");
        _test.Eq(GetInt(summary.WinsByFaction, "player", -1), 1, "completed winner 应正常计数。");
        _test.False(summary.WinsByFaction.ContainsKey("hostile"), "unfinished winner 不得污染胜场。");
        _test.Eq(
            summary.WinRateByFaction["player"],
            1.0f,
            "胜率分母必须是 completed_run_count。"
        );
        _test.False(
            summary.SkillAttemptTotals.ContainsKey("unfinished_only"),
            "unfinished metrics 不得污染技能汇总。"
        );
        BattleSimFactionMetricSummary factionSummary = summary.FactionMetricTotals["player"];
        _test.Eq(
            GetInt(factionSummary.ActionCounts, "skill", -1),
            4,
            "faction action 汇总只能使用 completed sample。"
        );
        _test.False(
            factionSummary.SkillAttemptCounts.ContainsKey("unfinished_only"),
            "unfinished faction attempts 不得污染汇总。"
        );
        _test.False(
            factionSummary.SkillSuccessCounts.ContainsKey("unfinished_only"),
            "unfinished faction successes 不得污染汇总。"
        );

        var scenarioReport = new BattleSimScenarioReport();
        var profileEntry = new BattleSimProfileReportEntry
        {
            Profile = profile,
            Summary = summary,
        };
        profileEntry.Runs.AddRange(runs);
        scenarioReport.ProfileEntries.Add(profileEntry);
        using GodotProjectionLease<GDictionary> reportLease =
            BattleSimFilePayloadProjection.BuildReportLease(scenarioReport);
        GDictionary payload = reportLease.Value;
        _test.Eq(payload["run_count"].AsInt32(), 3, "报告根节点应保留全部尝试数。");
        _test.Eq(payload["completed_run_count"].AsInt32(), 1, "报告根节点应暴露 completed 数。");
        _test.Eq(payload["unfinished_run_count"].AsInt32(), 2, "报告根节点应暴露 unfinished 数。");
        _test.False(payload["is_complete"].AsBool(), "包含未完成 run 的报告不得标记 complete。");
        using GArray projectedProfiles = payload["profile_entries"].AsGodotArray();
        using GDictionary projectedProfile = projectedProfiles[0].AsGodotDictionary();
        using GArray projectedRuns = projectedProfile["runs"].AsGodotArray();
        using GDictionary projectedStalled = projectedRuns[1].AsGodotDictionary();
        using GDictionary projectedBudget = projectedRuns[2].AsGodotDictionary();
        _test.Eq(
            projectedStalled["termination_kind"].AsString(),
            "idle_stall",
            "stalled run 应在写盘投影中保留明确终止原因。"
        );
        _test.True(projectedStalled["stalled"].AsBool(), "idle stall 应保留便捷诊断标记。");
        _test.Eq(
            projectedBudget["termination_kind"].AsString(),
            "iteration_budget_exhausted",
            "预算耗尽 run 应与 idle stall 区分。"
        );
    }

    private void TestEndedRunWithoutFinalDecisionIsInvalid()
    {
        var invalidEndedRun = new BattleSimRunReport
        {
            TerminationKind = BattleSimTerminationKind.BattleEnded,
            FinalTu = 12,
            Iterations = 6,
            TimelineSteps = 3,
        };
        BattleSimProfileDefinition profile = BuildProfile("baseline", "Baseline");
        BattleSimProfileSummary summary = new BattleSimReportBuilder().BuildProfileSummary(
            profile,
            new[] { invalidEndedRun }
        );
        var scenarioReport = new BattleSimScenarioReport();
        var profileEntry = new BattleSimProfileReportEntry
        {
            Profile = profile,
            Summary = summary,
        };
        profileEntry.Runs.Add(invalidEndedRun);
        scenarioReport.ProfileEntries.Add(profileEntry);

        _test.Eq(summary.CompletedRunCount, 0, "缺失 typed final decision 的 ended run 不得进入汇总样本。");
        _test.Eq(summary.InvalidRuntimeRunCount, 1, "profile 汇总应将缺失 typed decision 的 ended run 记为 invalid。");
        _test.Eq(scenarioReport.CompletedRunCount, 0, "scenario 汇总必须与 profile 的 completed 定义一致。");
        _test.Eq(scenarioReport.InvalidRuntimeRunCount, 1, "scenario 汇总必须识别缺失 typed decision 的 ended run。");
        _test.False(scenarioReport.IsComplete, "含无决策 ended run 的 scenario 不得声明完整。");
    }

    private void TestNullRunProjectsInvalidPlaceholder()
    {
        BattleSimProfileDefinition profile = BuildProfile("baseline", "Baseline");
        var scenarioReport = new BattleSimScenarioReport();
        var profileEntry = new BattleSimProfileReportEntry { Profile = profile };
        profileEntry.Runs.Add(null);
        scenarioReport.ProfileEntries.Add(profileEntry);

        using GodotProjectionLease<GDictionary> reportLease =
            BattleSimFilePayloadProjection.BuildReportLease(scenarioReport);
        using GArray profiles = reportLease.Value["profile_entries"].AsGodotArray();
        using GDictionary projectedProfile = profiles[0].AsGodotDictionary();
        using GArray runs = projectedProfile["runs"].AsGodotArray();
        using GDictionary projectedRun = runs[0].AsGodotDictionary();

        _test.Eq(projectedRun["termination_kind"].AsString(), "invalid_runtime", "null run 应投影为显式 invalid_runtime 占位，而不是空对象。");
        _test.Eq(projectedRun["outcome"].AsString(), "unknown", "null run 占位应显式保留 unknown typed outcome。");
        _test.Eq(scenarioReport.InvalidRuntimeRunCount, 1, "null run 应计入 scenario invalid runtime 统计。");
    }

    private void TestProfileSummaryExposesSkillAttemptAndFailureTotals()
    {
        var builder = new BattleSimReportBuilder();
        BattleSimProfileDefinition profile = BuildProfile("baseline", "Baseline");
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

    private static BattleSimProfileDefinition BuildProfile(
        StringName profileId,
        string displayName
    )
    {
        return new BattleSimProfileDefinition(
            profileId,
            displayName,
            "",
            BattleAiScoreProfileDefinition.Default,
            System.Array.Empty<BattleSimOverridePatchDefinition>()
        );
    }

    private static BattleSimRunReport BuildRunA()
    {
        BattleSimRunReport report = new()
        {
            TerminationKind = BattleSimTerminationKind.BattleEnded,
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
        report.SetFinalDecision(
            BattleObjectiveTestFactory.CreateEliminationDecision("player", report.FinalTu)
        );
        return report;
    }

    private static BattleSimRunReport BuildRunB()
    {
        BattleSimRunReport report = new()
        {
            TerminationKind = BattleSimTerminationKind.BattleEnded,
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
        report.SetFinalDecision(
            BattleObjectiveTestFactory.CreateEliminationDecision("hostile", report.FinalTu)
        );
        return report;
    }

    private static BattleSimRunReport BuildRunCandidate()
    {
        BattleSimRunReport report = new()
        {
            TerminationKind = BattleSimTerminationKind.BattleEnded,
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
        report.SetFinalDecision(
            BattleObjectiveTestFactory.CreateEliminationDecision("player", report.FinalTu)
        );
        return report;
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
        var faction = new BattleMetricEntry { FactionId = "player", UnitCount = 1 };
        int actionCount = 0;
        foreach (KeyValuePair<string, int> entry in attempts)
        {
            faction.SkillAttemptCounts[entry.Key] = entry.Value;
            actionCount += entry.Value;
        }
        foreach (KeyValuePair<string, int> entry in successes)
            faction.SkillSuccessCounts[entry.Key] = entry.Value;
        faction.ActionCounts["skill"] = actionCount;
        state.Factions["player"] = faction;
        return BattleSimMetricsSnapshot.Capture(state);
    }

    private static int GetInt(IReadOnlyDictionary<string, int> source, string key, int fallback = 0)
    {
        return source != null && source.TryGetValue(key, out int value) ? value : fallback;
    }

}
