using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattleSimReportProjection
{
    internal static GodotProjectionLease<GDictionary> BuildLease(
        BattleSimScenarioReport report
    )
    {
        GDictionary root = new();
        GodotProjectionLease<GDictionary> lease =
            GodotProjectionLease<GDictionary>.CreateOwnedRoot(
                root,
                "battle_sim_report",
                LifetimeDomain.Request,
                "BattleSimReportProjection.BuildLease"
            );
        try
        {
            WriteScenarioReportInto(lease, root, report, "battle_sim_report");
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal static GodotProjectionLease<GDictionary> BuildScenarioLease(
        BattleSimScenarioDef scenario
    ) =>
        BuildRootLease(
            "battle_sim_scenario",
            "BattleSimReportProjection.BuildScenarioLease",
            (lease, root) => WriteScenarioInto(lease, root, scenario, "scenario")
        );

    internal static GodotProjectionLease<GDictionary> BuildProfileLease(
        BattleSimProfileDefinition profile
    ) =>
        BuildRootLease(
            "battle_sim_profile",
            "BattleSimReportProjection.BuildProfileLease",
            (lease, root) => WriteProfileInto(lease, root, profile, "profile")
        );

    internal static GodotProjectionLease<GDictionary> BuildMetricsLease(
        BattleSimMetricsSnapshot metrics
    ) =>
        BuildRootLease(
            "battle_sim_metrics",
            "BattleSimReportProjection.BuildMetricsLease",
            (lease, root) => WriteMetricsInto(lease, root, metrics, "metrics")
        );

    internal static GodotProjectionLease<GDictionary> BuildFlattenedTraceLease(
        BattleAiTurnTraceProjection trace,
        string scenarioId,
        string profileId,
        long seed
    )
    {
        GDictionary root = new();
        GodotProjectionLease<GDictionary> lease =
            GodotProjectionLease<GDictionary>.CreateOwnedRoot(
                root,
                "battle_sim_flattened_trace",
                LifetimeDomain.Request,
                "BattleSimReportProjection.BuildFlattenedTraceLease"
            );
        try
        {
            BattleAiTurnTracePayloadProjection.WriteInto(
                lease,
                root,
                trace,
                "flattened_trace"
            );
            root["scenario_id"] = scenarioId ?? "";
            root["profile_id"] = profileId ?? "";
            root["seed"] = seed;
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal static GDictionary WriteComparison<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleSimProfileComparison comparison,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = NewDictionary(lease, reason);
        if (comparison == null)
            return result;
        result["baseline_profile_id"] = comparison.BaselineProfileId;
        result["candidate_profile_id"] = comparison.CandidateProfileId;
        result["average_final_tu_delta"] = comparison.AverageFinalTuDelta;
        result["average_iterations_delta"] = comparison.AverageIterationsDelta;
        result["average_timeline_steps_delta"] = comparison.AverageTimelineStepsDelta;
        result["win_rate_delta"] = WriteFloatDictionary(
            lease,
            comparison.WinRateDelta,
            $"{reason}.win_rate_delta"
        );
        result["skill_usage_delta"] = WriteIntDictionary(
            lease,
            comparison.SkillUsageDelta,
            $"{reason}.skill_usage_delta"
        );
        result["skill_attempt_delta"] = WriteIntDictionary(
            lease,
            comparison.SkillAttemptDelta,
            $"{reason}.skill_attempt_delta"
        );
        result["skill_failure_delta"] = WriteIntDictionary(
            lease,
            comparison.SkillFailureDelta,
            $"{reason}.skill_failure_delta"
        );
        result["action_choice_delta"] = WriteIntDictionary(
            lease,
            comparison.ActionChoiceDelta,
            $"{reason}.action_choice_delta"
        );
        return result;
    }

    internal static GDictionary WriteSummary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleSimProfileSummary summary,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = NewDictionary(lease, reason);
        if (summary == null)
            return result;

        GDictionary factionMetricTotals = NewDictionary(
            lease,
            $"{reason}.faction_metric_totals"
        );
        foreach (
            KeyValuePair<string, BattleSimFactionMetricSummary> entry
            in summary.FactionMetricTotals
        )
        {
            factionMetricTotals[entry.Key] = WriteFactionSummary(
                lease,
                entry.Value,
                $"{reason}.faction_metric_totals.{entry.Key}"
            );
        }

        result["profile_id"] = summary.ProfileId;
        result["display_name"] = summary.DisplayName;
        result["run_count"] = summary.RunCount;
        result["wins_by_faction"] = WriteIntDictionary(
            lease,
            summary.WinsByFaction,
            $"{reason}.wins_by_faction"
        );
        result["win_rate_by_faction"] = WriteFloatDictionary(
            lease,
            summary.WinRateByFaction,
            $"{reason}.win_rate_by_faction"
        );
        result["average_final_tu"] = summary.AverageFinalTu;
        result["average_iterations"] = summary.AverageIterations;
        result["average_timeline_steps"] = summary.AverageTimelineSteps;
        result["skill_attempt_totals"] = WriteIntDictionary(
            lease,
            summary.SkillAttemptTotals,
            $"{reason}.skill_attempt_totals"
        );
        result["skill_usage_totals"] = WriteIntDictionary(
            lease,
            summary.SkillUsageTotals,
            $"{reason}.skill_usage_totals"
        );
        result["skill_failure_totals"] = WriteIntDictionary(
            lease,
            summary.SkillFailureTotals,
            $"{reason}.skill_failure_totals"
        );
        result["action_choice_counts"] = WriteIntDictionary(
            lease,
            summary.ActionChoiceCounts,
            $"{reason}.action_choice_counts"
        );
        result["faction_metric_totals"] = factionMetricTotals;
        return result;
    }

    internal static GDictionary WriteScenario<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleSimScenarioDef scenario,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = NewDictionary(lease, reason);
        WriteScenarioInto(lease, result, scenario, reason);
        return result;
    }

    internal static GDictionary WriteProfile<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleSimProfileDefinition profile,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = NewDictionary(lease, reason);
        WriteProfileInto(lease, result, profile, reason);
        return result;
    }

    private static void WriteScenarioReportInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary target,
        BattleSimScenarioReport report,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        if (report == null)
            return;

        GArray profileEntries = NewArray(lease, $"{reason}.profile_entries");
        int profileIndex = 0;
        foreach (BattleSimProfileReportEntry entry in report.ProfileEntries)
        {
            profileEntries.Add(
                WriteProfileEntry(lease, entry, $"{reason}.profile_entries[{profileIndex}]")
            );
            profileIndex++;
        }

        GArray comparisons = NewArray(lease, $"{reason}.comparisons");
        int comparisonIndex = 0;
        foreach (BattleSimProfileComparison entry in report.Comparisons)
        {
            comparisons.Add(
                WriteComparison(lease, entry, $"{reason}.comparisons[{comparisonIndex}]")
            );
            comparisonIndex++;
        }

        target["scenario"] = WriteScenario(lease, report.ScenarioDef, $"{reason}.scenario");
        target["generated_at_unix"] = report.GeneratedAtUnix;
        target["profile_entries"] = profileEntries;
        target["comparisons"] = comparisons;
        target["output_files"] = WriteOutputFiles(
            lease,
            report.OutputFiles,
            $"{reason}.output_files"
        );
    }

    private static GDictionary WriteProfileEntry<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleSimProfileReportEntry entry,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = NewDictionary(lease, reason);
        if (entry == null)
            return result;
        GArray runs = NewArray(lease, $"{reason}.runs");
        int runIndex = 0;
        foreach (BattleSimRunReport run in entry.Runs)
        {
            runs.Add(WriteRun(lease, run, $"{reason}.runs[{runIndex}]"));
            runIndex++;
        }
        result["profile"] = WriteProfile(lease, entry.Profile, $"{reason}.profile");
        result["runs"] = runs;
        result["summary"] = WriteSummary(lease, entry.Summary, $"{reason}.summary");
        return result;
    }

    private static GDictionary WriteRun<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleSimRunReport report,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = NewDictionary(lease, reason);
        if (report == null)
            return result;
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
        result["metrics"] = WriteMetrics(lease, report.MetricsSnapshot, $"{reason}.metrics");
        result["ai_turn_traces"] = BattleAiTurnTracePayloadProjection.WriteArray(
            lease,
            report.AiTurnTraces,
            $"{reason}.ai_turn_traces"
        );
        result["final_units"] = TraceDictionaryProjection.WriteArray(
            lease,
            report.FinalUnits,
            $"{reason}.final_units"
        );
        return result;
    }

    private static GDictionary WriteMetrics<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleSimMetricsSnapshot metrics,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = NewDictionary(lease, reason);
        WriteMetricsInto(lease, result, metrics, reason);
        return result;
    }

    private static void WriteMetricsInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary target,
        BattleSimMetricsSnapshot metrics,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        if (metrics == null)
            return;
        target["battle_id"] = metrics.BattleId;
        target["seed"] = metrics.Seed;
        target["units"] = WriteUnitMetricsMap(lease, metrics.Units, $"{reason}.units");
        target["factions"] = WriteFactionMetricsMap(
            lease,
            metrics.Factions,
            $"{reason}.factions"
        );
    }

    internal static GDictionary WriteUnitMetricsMap<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyDictionary<string, BattleSimUnitMetricsSnapshot> metrics,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = NewDictionary(lease, reason);
        if (metrics == null)
            return result;
        foreach (KeyValuePair<string, BattleSimUnitMetricsSnapshot> entry in metrics)
            result[entry.Key] = WriteUnitMetrics(lease, entry.Value, $"{reason}.{entry.Key}");
        return result;
    }

    private static GDictionary WriteFactionMetrics<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleSimUnitMetricsSnapshot metrics,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = NewDictionary(lease, reason);
        if (metrics == null)
            return result;
        result["unit_count"] = metrics.UnitCount;
        result["turn_count"] = metrics.TurnCount;
        result["successful_skill_count"] = metrics.SuccessfulSkillCount;
        result["total_damage_done"] = metrics.TotalDamageDone;
        result["total_healing_done"] = metrics.TotalHealingDone;
        result["total_damage_taken"] = metrics.TotalDamageTaken;
        result["total_healing_received"] = metrics.TotalHealingReceived;
        result["kill_count"] = metrics.KillCount;
        result["death_count"] = metrics.DeathCount;
        return result;
    }

    private static GDictionary WriteUnitMetrics<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleSimUnitMetricsSnapshot metrics,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = NewDictionary(lease, reason);
        if (metrics == null)
            return result;
        result["faction_id"] = metrics.FactionId;
        result["turn_count"] = metrics.TurnCount;
        result["action_counts"] = WriteIntDictionary(
            lease,
            metrics.ActionCounts,
            $"{reason}.action_counts"
        );
        result["skill_attempt_counts"] = WriteIntDictionary(
            lease,
            metrics.SkillAttemptCounts,
            $"{reason}.skill_attempt_counts"
        );
        result["skill_success_counts"] = WriteIntDictionary(
            lease,
            metrics.SkillSuccessCounts,
            $"{reason}.skill_success_counts"
        );
        result["successful_skill_count"] = metrics.SuccessfulSkillCount;
        result["total_damage_done"] = metrics.TotalDamageDone;
        result["total_healing_done"] = metrics.TotalHealingDone;
        result["total_damage_taken"] = metrics.TotalDamageTaken;
        result["total_healing_received"] = metrics.TotalHealingReceived;
        result["kill_count"] = metrics.KillCount;
        result["death_count"] = metrics.DeathCount;
        if (!string.IsNullOrEmpty(metrics.UnitId))
        {
            result["unit_id"] = metrics.UnitId;
            result["display_name"] = metrics.DisplayName;
            result["control_mode"] = metrics.ControlMode;
            result["source_member_id"] = metrics.SourceMemberId;
        }
        else
        {
            result["unit_count"] = metrics.UnitCount;
        }
        return result;
    }

    internal static GDictionary WriteFactionMetricsMap<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyDictionary<string, BattleSimUnitMetricsSnapshot> metrics,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = NewDictionary(lease, reason);
        if (metrics == null)
            return result;
        foreach (KeyValuePair<string, BattleSimUnitMetricsSnapshot> entry in metrics)
            result[entry.Key] = WriteFactionMetrics(
                lease,
                entry.Value,
                $"{reason}.{entry.Key}"
            );
        return result;
    }

    private static GDictionary WriteFactionSummary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleSimFactionMetricSummary summary,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = NewDictionary(lease, reason);
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

    private static GDictionary WriteOutputFiles<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleSimOutputFiles files,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = NewDictionary(lease, reason);
        if (files == null)
            return result;
        result["report_json"] = files.ReportJson;
        result["turn_trace_jsonl"] = files.TurnTraceJsonl;
        if (!string.IsNullOrEmpty(files.TraceSummaryJson))
            result["trace_summary_json"] = files.TraceSummaryJson;
        return result;
    }

    private static void WriteProfileInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary target,
        BattleSimProfileDefinition profile,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        if (profile == null)
            return;
        target["profile_id"] = profile.ProfileId.ToString();
        target["display_name"] = profile.DisplayName;
        target["description"] = profile.Description;
        target["ai_score_profile"] = BattleAiScoreProjection.WriteProfile(
            lease,
            profile.AiScoreProfile,
            $"{reason}.ai_score_profile"
        );
        target["override_patch_count"] = profile.OverridePatches.Count;
    }

    private static void WriteScenarioInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary target,
        BattleSimScenarioDef scenario,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        if (scenario == null)
            return;
        target["scenario_id"] = scenario.scenario_id.ToString();
        target["display_name"] = scenario.display_name;
        target["description"] = scenario.description;
        target["map_size"] = scenario.map_size;
        target["terrain_profile_id"] = scenario.terrain_profile_id.ToString();
        target["use_formal_terrain_generation"] = scenario.use_formal_terrain_generation;
        target["world_coord"] = scenario.world_coord;
        target["timeline_ticks_per_step"] = scenario.timeline_ticks_per_step;
        target["tu_per_tick"] = scenario.tu_per_tick;
        target["max_iterations"] = scenario.max_iterations;
        target["manual_policy"] = scenario.manual_policy.ToString();
        target["trace_enabled"] = scenario.trace_enabled;
        GArray seeds = NewArray(lease, $"{reason}.seeds");
        int[] sourceSeeds = scenario.seeds;
        if (sourceSeeds == null || sourceSeeds.Length == 0)
            seeds.Add(101);
        else
            foreach (int seed in sourceSeeds)
                seeds.Add(seed);
        target["seeds"] = seeds;
        target["ally_unit_count"] = scenario.ally_units.Count;
        target["enemy_unit_count"] = scenario.enemy_units.Count;
    }

    private static GDictionary WriteIntDictionary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyDictionary<string, int> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = NewDictionary(lease, reason);
        if (source == null)
            return result;
        foreach (KeyValuePair<string, int> entry in source)
            result[entry.Key] = entry.Value;
        return result;
    }

    private static GDictionary WriteFloatDictionary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyDictionary<string, float> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = NewDictionary(lease, reason);
        if (source == null)
            return result;
        foreach (KeyValuePair<string, float> entry in source)
            result[entry.Key] = entry.Value;
        return result;
    }

    private static GodotProjectionLease<GDictionary> BuildRootLease(
        string ownerId,
        string reason,
        Action<GodotProjectionLease<GDictionary>, GDictionary> writer
    )
    {
        GDictionary root = new();
        GodotProjectionLease<GDictionary> lease =
            GodotProjectionLease<GDictionary>.CreateOwnedRoot(
                root,
                ownerId,
                LifetimeDomain.Request,
                reason
            );
        try
        {
            writer(lease, root);
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static GDictionary NewDictionary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        string reason
    )
        where TLeaseRoot : class, IDisposable =>
        lease.Own(new GDictionary(), reason);

    private static GArray NewArray<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        string reason
    )
        where TLeaseRoot : class, IDisposable =>
        lease.Own(new GArray(), reason);
}
