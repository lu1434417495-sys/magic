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
        long seed
    ) =>
        TraceDictionaryProjection.BuildJsonSafeLease(
            BuildFlattenedTraceFacts(trace, scenarioId, profileId, seed),
            "battle-sim-file-trace",
            LifetimeDomain.Request,
            "BattleSimFilePayloadProjection.trace"
        );

    internal static Dictionary<string, object> BuildScenarioFacts(
        BattleSimScenarioDef scenario
    )
    {
        var result = NewMap();
        if (scenario == null)
            return result;
        var seeds = new List<object>();
        if (scenario.seeds == null || scenario.seeds.Length == 0)
            seeds.Add(101);
        else
            foreach (int seed in scenario.seeds)
                seeds.Add(seed);
        result["scenario_id"] = scenario.scenario_id.ToString();
        result["display_name"] = scenario.display_name ?? "";
        result["description"] = scenario.description ?? "";
        result["map_size"] = scenario.map_size;
        result["terrain_profile_id"] = scenario.terrain_profile_id.ToString();
        result["use_formal_terrain_generation"] = scenario.use_formal_terrain_generation;
        result["world_coord"] = scenario.world_coord;
        result["timeline_ticks_per_step"] = scenario.timeline_ticks_per_step;
        result["tu_per_tick"] = scenario.tu_per_tick;
        result["max_iterations"] = scenario.max_iterations;
        result["manual_policy"] = scenario.manual_policy.ToString();
        result["trace_enabled"] = scenario.trace_enabled;
        result["seeds"] = seeds;
        result["ally_unit_count"] = scenario.ally_units.Count;
        result["enemy_unit_count"] = scenario.enemy_units.Count;
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
        result["scenario"] = BuildScenarioFacts(report.ScenarioDef);
        result["generated_at_unix"] = report.GeneratedAtUnix;
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
        if (report == null)
            return result;
        var traces = new List<object>();
        foreach (BattleAiTurnTraceProjection trace in report.AiTurnTraces)
            if (trace != null)
                traces.Add(trace.ToTraceDictionary());
        result["scenario_id"] = report.ScenarioId;
        result["profile_id"] = report.ProfileId;
        result["seed"] = report.Seed;
        result["battle_id"] = report.BattleId;
        result["battle_ended"] = report.BattleEnded;
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
        long seed
    )
    {
        Dictionary<string, object> result = trace?.ToTraceDictionary() ?? NewMap();
        result["scenario_id"] = scenarioId ?? "";
        result["profile_id"] = profileId ?? "";
        result["seed"] = seed;
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
