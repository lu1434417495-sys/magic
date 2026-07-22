using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_sim_typed_report_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestStringNameSkillLevelKeysAreHonored();
        TestMalformedScenarioUnitEntryIsRejectedBeforeSpawnProjection();
        TestTypedMetricsSnapshotFeedsProfileSummary();
        TestStandaloneRawReportExcludesUnfinishedRuns();
        RequestTestExit(_test.Finish("Battle sim typed report regression"));
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

        BattleUnitState unitState = spec
            .ToDefinition("player", "manual")
            .CreateRuntimeState();

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
            BattleSimScenarioDefinition definition = scenario.ToDefinition();
            using GodotProjectionLease<GDictionary> contextLease =
                definition.BuildStartContextLease();
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
        var metricsState = new BattleMetricsState();
        var casterMetrics = new BattleMetricEntry();
        casterMetrics.SkillAttemptCounts[skillId.ToString()] = 3;
        casterMetrics.SkillSuccessCounts[skillId.ToString()] = 2;
        metricsState.Units["caster"] = casterMetrics;
        var factionMetrics = new BattleMetricEntry
        {
            UnitCount = 1,
            SuccessfulSkillCount = 2,
        };
        factionMetrics.ActionCounts["skill"] = 3;
        factionMetrics.SkillAttemptCounts[skillId.ToString()] = 3;
        factionMetrics.SkillSuccessCounts[skillId.ToString()] = 2;
        metricsState.Factions["player"] = factionMetrics;
        BattleSimMetricsSnapshot metrics = BattleSimMetricsSnapshot.Capture(metricsState);
        var report = new BattleSimRunReport
        {
            TerminationKind = BattleSimTerminationKind.BattleEnded,
            MetricsSnapshot = metrics,
            AiTurnTraces = Array.Empty<BattleAiTurnTraceProjection>(),
        };
        report.SetFinalDecision(
            BattleObjectiveTestFactory.CreateEliminationDecision("player")
        );

        BattleSimProfileSummary summary = new BattleSimReportBuilder().BuildProfileSummary(
            new BattleSimProfileDefinition(
                "baseline",
                "Baseline",
                "",
                BattleAiScoreProfileDefinition.Default,
                Array.Empty<BattleSimOverridePatchDefinition>()
            ),
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
        BattleSimFactionMetricSummary factionSummary = summary.FactionMetricTotals["player"];
        _test.Eq(
            factionSummary.SuccessfulSkillCount,
            2,
            "typed metrics snapshot 应保留 faction metrics。"
        );
        _test.Eq(
            GetInt(factionSummary.ActionCounts, "skill"),
            3,
            "profile summary 应汇总 faction action counts。"
        );
        _test.Eq(
            GetInt(factionSummary.SkillAttemptCounts, skillId.ToString()),
            3,
            "profile summary 应汇总 faction skill attempts。"
        );
        _test.Eq(
            GetInt(factionSummary.SkillSuccessCounts, skillId.ToString()),
            2,
            "profile summary 应汇总 faction skill successes。"
        );

        var profileEntry = new BattleSimProfileReportEntry { Summary = summary };
        profileEntry.Runs.Add(report);
        var scenarioReport = new BattleSimScenarioReport();
        scenarioReport.ProfileEntries.Add(profileEntry);

        using (GodotProjectionLease<GDictionary> projectionLease =
            BattleSimReportProjection.BuildLease(scenarioReport))
        {
            AssertFactionCounters(projectionLease.Value, "Godot report projection");
        }
        using (GodotProjectionLease<GDictionary> fileLease =
            BattleSimFilePayloadProjection.BuildReportLease(scenarioReport))
        {
            AssertFactionCounters(fileLease.Value, "file report projection");
        }

        Dictionary<string, object> standaloneFactions =
            RunMixed6v12MirrorAnalysis.BuildFactionRunDetails(metrics);
        var standalonePlayer = (Dictionary<string, object>)standaloneFactions["player"];
        _test.Eq(
            (string)standalonePlayer["faction_id"],
            "player",
            "6v12 standalone runner 应保留 faction id。"
        );
        AssertPlainCounterValue(
            standalonePlayer,
            "action_counts",
            "skill",
            3,
            "6v12 standalone runner faction actions"
        );
        AssertPlainCounterValue(
            standalonePlayer,
            "skill_attempt_counts",
            "typed_fire",
            3,
            "6v12 standalone runner faction attempts"
        );
        AssertPlainCounterValue(
            standalonePlayer,
            "skill_success_counts",
            "typed_fire",
            2,
            "6v12 standalone runner faction successes"
        );
    }

    private void TestStandaloneRawReportExcludesUnfinishedRuns()
    {
        var accumulator = new RunMixed6v12MirrorAnalysis.RawReportAccumulator();
        var completedFixture = new BattleSimFormalCombatFixture
        {
            charge_mastery = 7,
        };
        var unfinishedFixture = new BattleSimFormalCombatFixture
        {
            charge_mastery = 999,
        };
        try
        {
            accumulator.AbsorbRun(
                WithEliminationDecision(
                    new BattleSimRunReport
                    {
                        Seed = 201,
                        TerminationKind = BattleSimTerminationKind.BattleEnded,
                        FinalTu = 24,
                        Iterations = 12,
                        TimelineSteps = 5,
                        MetricsSnapshot = BuildStandaloneMetrics(
                            "completed_unit",
                            damageDone: 60,
                            chargeAttempts: 2,
                            chargeSuccesses: 1
                        ),
                    },
                    "player"
                ),
                completedFixture,
                traceAi: false
            );
            accumulator.AbsorbRun(
                new BattleSimRunReport
                {
                    Seed = 202,
                    TerminationKind = BattleSimTerminationKind.IterationBudgetExhausted,
                    FinalTu = 6000,
                    Iterations = 3000,
                    TimelineSteps = 2500,
                    MetricsSnapshot = BuildStandaloneMetrics(
                        "unfinished_unit",
                        damageDone: 9999,
                        chargeAttempts: 999,
                        chargeSuccesses: 999
                    ),
                },
                unfinishedFixture,
                traceAi: false
            );
        }
        finally
        {
            unfinishedFixture.Dispose();
            completedFixture.Dispose();
        }

        Dictionary<string, object> report = accumulator.BuildReport(
            startSeed: 201,
            startSeedSource: "test",
            requestedRunCount: 2,
            timeoutSeconds: 0,
            timedOut: false,
            elapsedSeconds: 0.0,
            aiMutationGuardEnabled: false,
            validateSpawnReachability: false,
            validateBidirectionalSpawnReachability: false,
            scenario: null
        );

        _test.Eq((int)report["run_count"], 2, "raw report 应保留全部尝试数。");
        _test.Eq(
            (int)report["completed_run_count"],
            1,
            "raw report completed_run_count 应只统计 battle-ended runs。"
        );
        _test.Eq(
            (int)report["unfinished_run_count"],
            1,
            "raw report 应公开 unfinished run 数。"
        );
        _test.Eq(
            (int)report["iteration_budget_exhausted_run_count"],
            1,
            "raw report 应保留 iteration-budget 终止分类。"
        );
        _test.False((bool)report["is_complete"], "存在 unfinished run 时报告不得标记 complete。");
        _test.Eq((double)report["avg_iterations"], 12.0, "平均迭代数只使用完成局。");
        _test.Eq((double)report["avg_timeline_steps"], 5.0, "平均 timeline steps 只使用完成局。");

        var wins = (Dictionary<string, object>)report["wins_by_faction"];
        var winRate = (Dictionary<string, object>)report["win_rate"];
        _test.Eq((int)wins["player"], 1, "完成局玩家胜场应保留。");
        _test.Eq((int)wins["hostile"], 0, "未完成局的 winner 不得污染胜场。");
        _test.Eq((double)winRate["player"], 1.0, "胜率分母应为 completed_run_count。");

        var player = (Dictionary<string, object>)report["player"];
        _test.Eq((int)player["total_damage_done"], 60, "未完成局伤害不得污染 faction aggregate。");
        var global = (Dictionary<string, object>)report["global"];
        var charge = (Dictionary<string, object>)global["charge"];
        _test.Eq((int)charge["attempts"], 2, "未完成局技能尝试不得污染 global aggregate。");
        _test.Eq((int)charge["mastery"], 7, "未完成局 mastery 不得污染 global aggregate。");

        var perUnit = (Dictionary<string, object>)report["per_unit_summary"];
        _test.True(perUnit.ContainsKey("completed_unit"), "完成局单位应进入 per-unit 汇总。");
        _test.False(perUnit.ContainsKey("unfinished_unit"), "未完成局单位不得进入 per-unit 汇总。");

        var runs = (List<object>)report["runs"];
        _test.Eq(runs.Count, 2, "未完成局仍应保留在 runs[] 供诊断。");
        var completedRun = (Dictionary<string, object>)runs[0];
        var unfinishedRun = (Dictionary<string, object>)runs[1];
        _test.True((bool)completedRun["battle_ended"], "完成局应显式输出 battle_ended=true。");
        _test.Eq(
            (string)completedRun["termination_kind"],
            "battle_ended",
            "完成局应输出明确 termination_kind。"
        );
        _test.False((bool)unfinishedRun["battle_ended"], "未完成局应显式输出 battle_ended=false。");
        _test.Eq(
            (string)unfinishedRun["termination_kind"],
            "iteration_budget_exhausted",
            "未完成局应保留具体 termination_kind。"
        );
    }

    private static BattleSimMetricsSnapshot BuildStandaloneMetrics(
        string unitId,
        int damageDone,
        int chargeAttempts,
        int chargeSuccesses
    )
    {
        var metricsState = new BattleMetricsState();
        var unit = new BattleMetricEntry
        {
            UnitId = unitId,
            DisplayName = unitId,
            FactionId = "player",
            ControlMode = "ai",
            TurnCount = 1,
            TotalDamageDone = damageDone,
        };
        unit.SkillAttemptCounts["charge"] = chargeAttempts;
        unit.SkillSuccessCounts["charge"] = chargeSuccesses;
        metricsState.Units[unitId] = unit;

        var faction = new BattleMetricEntry
        {
            FactionId = "player",
            UnitCount = 1,
            TurnCount = 1,
            TotalDamageDone = damageDone,
            SuccessfulSkillCount = chargeSuccesses,
        };
        faction.SkillAttemptCounts["charge"] = chargeAttempts;
        faction.SkillSuccessCounts["charge"] = chargeSuccesses;
        metricsState.Factions["player"] = faction;
        return BattleSimMetricsSnapshot.Capture(metricsState);
    }

    private void AssertFactionCounters(GDictionary report, string label)
    {
        using GArray profileEntries = report["profile_entries"].AsGodotArray();
        using GDictionary profileEntry = profileEntries[0].AsGodotDictionary();
        using GArray runs = profileEntry["runs"].AsGodotArray();
        using GDictionary run = runs[0].AsGodotDictionary();
        _test.Eq(
            run["objective_mode"].AsString(),
            "elimination",
            $"{label} objective mode。"
        );
        _test.Eq(
            run["outcome"].AsString(),
            "player_success",
            $"{label} typed outcome。"
        );
        _test.Eq(
            run["end_reason"].AsString(),
            "elimination_hostiles_defeated",
            $"{label} typed end reason。"
        );
        _test.Eq(run["decision_tu"].AsInt32(), 0, $"{label} decision TU。");
        _test.Eq(
            run["winner_faction_id"].AsString(),
            "player",
            $"{label} winner projection。"
        );
        using GDictionary metrics = run["metrics"].AsGodotDictionary();
        using GDictionary factions = metrics["factions"].AsGodotDictionary();
        using GDictionary player = factions["player"].AsGodotDictionary();
        _test.Eq(player["faction_id"].AsString(), "player", $"{label} faction id。");
        AssertCounterValue(player, "action_counts", "skill", 3, $"{label} run actions");
        AssertCounterValue(
            player,
            "skill_attempt_counts",
            "typed_fire",
            3,
            $"{label} run attempts"
        );
        AssertCounterValue(
            player,
            "skill_success_counts",
            "typed_fire",
            2,
            $"{label} run successes"
        );

        using GDictionary summary = profileEntry["summary"].AsGodotDictionary();
        using GDictionary factionTotals = summary["faction_metric_totals"].AsGodotDictionary();
        using GDictionary playerTotals = factionTotals["player"].AsGodotDictionary();
        AssertCounterValue(
            playerTotals,
            "action_counts",
            "skill",
            3,
            $"{label} summary actions"
        );
        AssertCounterValue(
            playerTotals,
            "skill_attempt_counts",
            "typed_fire",
            3,
            $"{label} summary attempts"
        );
        AssertCounterValue(
            playerTotals,
            "skill_success_counts",
            "typed_fire",
            2,
            $"{label} summary successes"
        );
    }

    private void AssertCounterValue(
        GDictionary owner,
        string mapKey,
        string counterKey,
        int expected,
        string label
    )
    {
        using GDictionary counters = owner[mapKey].AsGodotDictionary();
        _test.Eq(counters[counterKey].AsInt32(), expected, label);
    }

    private void AssertPlainCounterValue(
        Dictionary<string, object> owner,
        string mapKey,
        string counterKey,
        int expected,
        string label
    )
    {
        var counters = (IReadOnlyDictionary<string, int>)owner[mapKey];
        _test.Eq(GetInt(counters, counterKey), expected, label);
    }

    private static int GetInt(IReadOnlyDictionary<string, int> source, string key)
    {
        return source != null && source.TryGetValue(key, out int value) ? value : 0;
    }

    private static BattleSimRunReport WithEliminationDecision(
        BattleSimRunReport report,
        StringName winnerFactionId
    )
    {
        report.SetFinalDecision(
            BattleObjectiveTestFactory.CreateEliminationDecision(winnerFactionId)
        );
        return report;
    }
}
