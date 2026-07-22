using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattleSimFilePayloadProjection
{
    internal static GodotProjectionLease<GDictionary> BuildReportLease(
        BattleSimScenarioReport report
    ) =>
        TraceDictionaryProjection.BuildJsonSafeLease(
            BuildReportFacts(report),
            "battle-sim-file-report",
            LifetimeDomain.Request,
            "BattleSimFilePayloadProjection.report"
        );

    internal static GodotProjectionLease<GDictionary> BuildFlattenedTraceLease(
        BattleAiTurnTraceProjection trace,
        string scenarioId,
        string profileId,
        BattleSimRunReport run
    ) =>
        TraceDictionaryProjection.BuildJsonSafeLease(
            BuildFlattenedTraceFacts(trace, scenarioId, profileId, run),
            "battle-sim-file-trace",
            LifetimeDomain.Request,
            "BattleSimFilePayloadProjection.trace"
        );

    internal static Dictionary<string, object> BuildScenarioFacts(
        BattleSimScenarioDefinition scenario
    )
    {
        var result = NewMap();
        if (scenario == null)
            return result;
        var seeds = new List<object>();
        foreach (int seed in scenario.Seeds)
            seeds.Add(seed);
        result["scenario_id"] = scenario.ScenarioId.ToString();
        result["display_name"] = scenario.DisplayName;
        result["description"] = scenario.Description;
        result["map_size"] = scenario.MapSize;
        result["terrain_profile_id"] = scenario.TerrainProfileId.ToString();
        result["use_formal_terrain_generation"] = scenario.UseFormalTerrainGeneration;
        result["world_coord"] = scenario.WorldCoord;
        result["timeline_ticks_per_step"] = scenario.TimelineTicksPerStep;
        result["tu_per_tick"] = scenario.TuPerTick;
        result["max_iterations"] = scenario.MaxIterations;
        result["manual_policy"] = scenario.ManualPolicy.ToString();
        result["trace_enabled"] = scenario.TraceEnabled;
        result["seeds"] = seeds;
        result["ally_unit_count"] = scenario.AuthoringAllyUnitCount;
        result["enemy_unit_count"] = scenario.AuthoringEnemyUnitCount;
        return result;
    }

    internal static Dictionary<string, object> BuildProfileFacts(
        BattleSimProfileDefinition profile
    )
    {
        var result = NewMap();
        if (profile == null)
            return result;
        result["profile_id"] = profile.ProfileId.ToString();
        result["display_name"] = profile.DisplayName ?? "";
        result["description"] = profile.Description ?? "";
        result["ai_score_profile"] = BattleAiScoreProjection.BuildProfilePlain(
            profile.AiScoreProfile
        );
        result["override_patch_count"] = profile.OverridePatches.Count;
        return result;
    }

    private static Dictionary<string, object> BuildReportFacts(BattleSimScenarioReport report)
    {
        var result = NewMap();
        if (report == null)
            return result;
        var profileEntries = new List<object>();
        foreach (BattleSimProfileReportEntry entry in report.ProfileEntries)
            profileEntries.Add(BuildProfileEntryFacts(entry));
        var comparisons = new List<object>();
        foreach (BattleSimProfileComparison comparison in report.Comparisons)
            comparisons.Add(BuildComparisonFacts(comparison));
        result["scenario"] = BuildScenarioFacts(report.Scenario);
        result["generated_at_unix"] = report.GeneratedAtUnix;
        result["run_count"] = report.RunCount;
        result["completed_run_count"] = report.CompletedRunCount;
        result["unfinished_run_count"] = report.UnfinishedRunCount;
        result["stalled_run_count"] = report.StalledRunCount;
        result["iteration_budget_exhausted_run_count"] =
            report.IterationBudgetExhaustedRunCount;
        result["invalid_runtime_run_count"] = report.InvalidRuntimeRunCount;
        result["has_unfinished_runs"] = report.HasUnfinishedRuns;
        result["is_complete"] = report.IsComplete;
        result["profile_entries"] = profileEntries;
        result["comparisons"] = comparisons;
        result["output_files"] = BuildOutputFilesFacts(report.OutputFiles);
        return result;
    }

    private static Dictionary<string, object> BuildProfileEntryFacts(
        BattleSimProfileReportEntry entry
    )
    {
        var result = NewMap();
        if (entry == null)
            return result;
        var runs = new List<object>();
        foreach (BattleSimRunReport run in entry.Runs)
            runs.Add(BuildRunFacts(run));
        result["profile"] = BuildProfileFacts(entry.Profile);
        result["runs"] = runs;
        result["summary"] = BuildSummaryFacts(entry.Summary);
        return result;
    }

    private static Dictionary<string, object> BuildRunFacts(BattleSimRunReport report)
    {
        var result = NewMap();
        report ??= new BattleSimRunReport();
        var traces = new List<object>();
        foreach (BattleAiTurnTraceProjection trace in report.AiTurnTraces)
            if (trace != null)
                traces.Add(trace.ToTraceDictionary());
        result["scenario_id"] = report.ScenarioId;
        result["profile_id"] = report.ProfileId;
        result["seed"] = report.Seed;
        result["battle_id"] = report.BattleId;
        result["battle_ended"] = report.BattleEnded;
        result["termination_kind"] = BattleSimTerminationKindCodec.ToWireValue(
            report.TerminationKind
        );
        result["stalled"] = report.Stalled;
        result["start_failure"] = BuildStartFailureFacts(report.StartFailure);
        result["objective_mode"] = BattleObjectiveRuntimeCodec.ToWireValue(
            report.ObjectiveMode
        );
        result["outcome"] = BattleObjectiveRuntimeCodec.ToWireValue(report.Outcome);
        result["end_reason"] = BattleObjectiveRuntimeCodec.ToWireValue(report.EndReason);
        result["decision_tu"] = report.DecisionTu;
        result["winner_faction_id"] = report.WinnerFactionId;
        result["final_tu"] = report.FinalTu;
        result["iterations"] = report.Iterations;
        result["idle_loops"] = report.IdleLoops;
        result["timeline_steps"] = report.TimelineSteps;
        result["ally_alive"] = report.AllyAlive;
        result["enemy_alive"] = report.EnemyAlive;
        result["metrics"] = report.MetricsSnapshot.BuildPlain();
        result["ai_turn_traces"] = traces;
        result["final_units"] = report.FinalUnits;
        return result;
    }

    internal static Dictionary<string, object> BuildStartFailureFacts(
        BattleStartFailureSnapshot snapshot
    )
    {
        var result = NewMap();
        if (snapshot == null || snapshot.IsEmpty)
            return result;
        if (!string.IsNullOrEmpty(snapshot.Reason))
            result["reason"] = snapshot.Reason;
        if (snapshot.AllyUnitCount >= 0)
            result["ally_unit_count"] = snapshot.AllyUnitCount;
        if (snapshot.EnemyUnitCount >= 0)
            result["enemy_unit_count"] = snapshot.EnemyUnitCount;
        if (snapshot.PlacementAttempt >= 0)
            result["placement_attempt"] = snapshot.PlacementAttempt;
        if (snapshot.TerrainSeed != 0)
            result["terrain_seed"] = snapshot.TerrainSeed;
        if (snapshot.AllySpawnCount >= 0)
            result["ally_spawn_count"] = snapshot.AllySpawnCount;
        if (snapshot.EnemySpawnCount >= 0)
            result["enemy_spawn_count"] = snapshot.EnemySpawnCount;
        if (snapshot.PlacementAttempts >= 0)
            result["placement_attempts"] = snapshot.PlacementAttempts;
        if (snapshot.ReachabilityResult != null)
            result["reachability"] = BuildReachabilityFacts(snapshot.ReachabilityResult);
        return result;
    }

    private static Dictionary<string, object> BuildReachabilityFacts(
        BattleSpawnReachabilityResult reachability
    )
    {
        var details = new List<object>();
        foreach (BattleSpawnReachabilityUnitResult detail in reachability.Details)
        {
            var item = NewMap();
            item["valid"] = detail.Valid;
            if (detail.UnitId != (StringName)"")
                item["unit_id"] = detail.UnitId;
            if (detail.FactionId != (StringName)"")
                item["faction_id"] = detail.FactionId;
            if (!string.IsNullOrEmpty(detail.Reason))
                item["reason"] = detail.Reason;
            if (detail.AttackAnchor != new Vector2I(-1, -1))
                item["attack_anchor"] = detail.AttackAnchor;
            if (detail.TargetUnitId != (StringName)"")
                item["target_unit_id"] = detail.TargetUnitId;
            if (detail.SkillId != (StringName)"")
                item["skill_id"] = detail.SkillId;
            if (detail.ReachableAnchorCount >= 0)
                item["reachable_anchor_count"] = detail.ReachableAnchorCount;
            if (detail.AttackSkillIds.Count > 0)
                item["attack_skill_ids"] = new List<StringName>(detail.AttackSkillIds);
            details.Add(item);
        }

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["valid"] = reachability.Valid,
            ["invalid_enemy_unit_ids"] = new List<StringName>(
                reachability.InvalidEnemyUnitIds
            ),
            ["invalid_player_unit_ids"] = new List<StringName>(
                reachability.InvalidPlayerUnitIds
            ),
            ["details"] = details,
        };
    }

    internal static Dictionary<string, object> BuildSummaryFacts(BattleSimProfileSummary summary)
    {
        var result = NewMap();
        if (summary == null)
            return result;
        var factionTotals = NewMap();
        foreach ((string key, BattleSimFactionMetricSummary value) in summary.FactionMetricTotals)
            factionTotals[key] = BuildFactionSummaryFacts(value);
        result["profile_id"] = summary.ProfileId;
        result["display_name"] = summary.DisplayName;
        result["run_count"] = summary.RunCount;
        result["completed_run_count"] = summary.CompletedRunCount;
        result["unfinished_run_count"] = summary.UnfinishedRunCount;
        result["stalled_run_count"] = summary.StalledRunCount;
        result["iteration_budget_exhausted_run_count"] =
            summary.IterationBudgetExhaustedRunCount;
        result["invalid_runtime_run_count"] = summary.InvalidRuntimeRunCount;
        result["has_unfinished_runs"] = summary.HasUnfinishedRuns;
        result["is_complete"] = summary.IsComplete;
        result["wins_by_faction"] = BoxMap(summary.WinsByFaction);
        result["win_rate_by_faction"] = BoxMap(summary.WinRateByFaction);
        result["average_final_tu"] = summary.AverageFinalTu;
        result["average_iterations"] = summary.AverageIterations;
        result["average_timeline_steps"] = summary.AverageTimelineSteps;
        result["skill_attempt_totals"] = BoxMap(summary.SkillAttemptTotals);
        result["skill_usage_totals"] = BoxMap(summary.SkillUsageTotals);
        result["skill_failure_totals"] = BoxMap(summary.SkillFailureTotals);
        result["action_choice_counts"] = BoxMap(summary.ActionChoiceCounts);
        result["faction_metric_totals"] = factionTotals;
        return result;
    }

    internal static Dictionary<string, object> BuildComparisonFacts(
        BattleSimProfileComparison comparison
    )
    {
        var result = NewMap();
        if (comparison == null)
            return result;
        result["baseline_profile_id"] = comparison.BaselineProfileId;
        result["candidate_profile_id"] = comparison.CandidateProfileId;
        result["baseline_run_count"] = comparison.BaselineRunCount;
        result["baseline_completed_run_count"] = comparison.BaselineCompletedRunCount;
        result["candidate_run_count"] = comparison.CandidateRunCount;
        result["candidate_completed_run_count"] = comparison.CandidateCompletedRunCount;
        result["has_unfinished_runs"] = comparison.HasUnfinishedRuns;
        result["is_complete"] = comparison.IsComplete;
        result["average_final_tu_delta"] = comparison.AverageFinalTuDelta;
        result["average_iterations_delta"] = comparison.AverageIterationsDelta;
        result["average_timeline_steps_delta"] = comparison.AverageTimelineStepsDelta;
        result["win_rate_delta"] = BoxMap(comparison.WinRateDelta);
        result["skill_usage_delta"] = BoxMap(comparison.SkillUsageDelta);
        result["skill_attempt_delta"] = BoxMap(comparison.SkillAttemptDelta);
        result["skill_failure_delta"] = BoxMap(comparison.SkillFailureDelta);
        result["action_choice_delta"] = BoxMap(comparison.ActionChoiceDelta);
        return result;
    }

    private static Dictionary<string, object> BuildFactionSummaryFacts(
        BattleSimFactionMetricSummary summary
    )
    {
        var result = NewMap();
        if (summary == null)
            return result;
        result["unit_count"] = summary.UnitCount;
        result["turn_count"] = summary.TurnCount;
        result["action_counts"] = BoxMap(summary.ActionCounts);
        result["skill_attempt_counts"] = BoxMap(summary.SkillAttemptCounts);
        result["skill_success_counts"] = BoxMap(summary.SkillSuccessCounts);
        result["successful_skill_count"] = summary.SuccessfulSkillCount;
        result["total_damage_done"] = summary.TotalDamageDone;
        result["total_healing_done"] = summary.TotalHealingDone;
        result["total_damage_taken"] = summary.TotalDamageTaken;
        result["total_healing_received"] = summary.TotalHealingReceived;
        result["kill_count"] = summary.KillCount;
        result["death_count"] = summary.DeathCount;
        return result;
    }

    private static Dictionary<string, object> BuildOutputFilesFacts(BattleSimOutputFiles files)
    {
        var result = NewMap();
        if (files == null)
            return result;
        result["report_json"] = files.ReportJson;
        result["turn_trace_jsonl"] = files.TurnTraceJsonl;
        if (!string.IsNullOrEmpty(files.TraceSummaryJson))
            result["trace_summary_json"] = files.TraceSummaryJson;
        return result;
    }

    private static Dictionary<string, object> BuildFlattenedTraceFacts(
        BattleAiTurnTraceProjection trace,
        string scenarioId,
        string profileId,
        BattleSimRunReport run
    )
    {
        Dictionary<string, object> result = trace?.ToTraceDictionary() ?? NewMap();
        result["scenario_id"] = scenarioId ?? "";
        result["profile_id"] = profileId ?? "";
        result["seed"] = run?.Seed ?? 0;
        result["objective_mode"] = BattleObjectiveRuntimeCodec.ToWireValue(
            run?.ObjectiveMode ?? BattleObjectiveMode.Unknown
        );
        result["outcome"] = BattleObjectiveRuntimeCodec.ToWireValue(
            run?.Outcome ?? BattleOutcomeKind.Unknown
        );
        result["end_reason"] = BattleObjectiveRuntimeCodec.ToWireValue(
            run?.EndReason ?? BattleEndReasonKind.None
        );
        result["decision_tu"] = run?.DecisionTu ?? -1;
        result["winner_faction_id"] = run?.WinnerFactionId ?? "";
        return result;
    }

    private static Dictionary<string, object> BoxMap<T>(
        IReadOnlyDictionary<string, T> source
    )
    {
        var result = NewMap();
        if (source != null)
            foreach ((string key, T value) in source)
                result[key] = value;
        return result;
    }

    private static Dictionary<string, object> NewMap() =>
        new(StringComparer.Ordinal);
}
