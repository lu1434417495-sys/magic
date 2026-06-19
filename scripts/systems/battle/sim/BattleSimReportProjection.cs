using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattleSimReportProjection
{
    internal static GDictionary Project(BattleSimScenarioReport report)
    {
        if (report == null)
            return new GDictionary();

        var profileEntries = new Godot.Collections.Array();
        foreach (BattleSimProfileReportEntry entry in report.ProfileEntries)
        {
            profileEntries.Add(Project(entry));
        }

        var comparisons = new Godot.Collections.Array();
        foreach (BattleSimProfileComparison entry in report.Comparisons)
        {
            comparisons.Add(Project(entry));
        }

        return new GDictionary
        {
            ["scenario"] = Project(report.ScenarioDef),
            ["generated_at_unix"] = report.GeneratedAtUnix,
            ["profile_entries"] = profileEntries,
            ["comparisons"] = comparisons,
            ["output_files"] = Project(report.OutputFiles),
        };
    }

    internal static GDictionary Project(BattleSimProfileReportEntry entry)
    {
        if (entry == null)
            return new GDictionary();

        var runsPayload = new Godot.Collections.Array();
        foreach (BattleSimRunReport run in entry.Runs)
        {
            runsPayload.Add(Project(run));
        }

        return new GDictionary
        {
            ["profile"] = Project(entry.Profile),
            ["runs"] = runsPayload,
            ["summary"] = Project(entry.Summary),
        };
    }

    internal static GDictionary Project(BattleSimRunReport report)
    {
        if (report == null)
            return new GDictionary();
        return new GDictionary
        {
            ["scenario_id"] = report.ScenarioId,
            ["profile_id"] = report.ProfileId,
            ["seed"] = report.Seed,
            ["battle_id"] = report.BattleId,
            ["battle_ended"] = report.BattleEnded,
            ["winner_faction_id"] = report.WinnerFactionId,
            ["final_tu"] = report.FinalTu,
            ["iterations"] = report.Iterations,
            ["idle_loops"] = report.IdleLoops,
            ["timeline_steps"] = report.TimelineSteps,
            ["ally_alive"] = report.AllyAlive,
            ["enemy_alive"] = report.EnemyAlive,
            ["metrics"] = Project(report.MetricsSnapshot),
            ["ai_turn_traces"] = BattleAiTurnTracePayloadProjection.ProjectArray(
                report.AiTurnTraces
            ),
            ["final_units"] = report.FinalUnits,
        };
    }

    internal static GDictionary Project(BattleSimProfileComparison comparison)
    {
        if (comparison == null)
            return new GDictionary();
        return new GDictionary
        {
            ["baseline_profile_id"] = comparison.BaselineProfileId,
            ["candidate_profile_id"] = comparison.CandidateProfileId,
            ["average_final_tu_delta"] = comparison.AverageFinalTuDelta,
            ["average_iterations_delta"] = comparison.AverageIterationsDelta,
            ["average_timeline_steps_delta"] = comparison.AverageTimelineStepsDelta,
            ["win_rate_delta"] = ProjectFloatDictionary(comparison.WinRateDelta),
            ["skill_usage_delta"] = ProjectIntDictionary(comparison.SkillUsageDelta),
            ["skill_attempt_delta"] = ProjectIntDictionary(comparison.SkillAttemptDelta),
            ["skill_failure_delta"] = ProjectIntDictionary(comparison.SkillFailureDelta),
            ["action_choice_delta"] = ProjectIntDictionary(comparison.ActionChoiceDelta),
        };
    }

    internal static GDictionary Project(BattleSimProfileSummary summary)
    {
        if (summary == null)
            return new GDictionary();

        var factionMetricTotals = new GDictionary();
        foreach (
            KeyValuePair<string, BattleSimFactionMetricSummary> entry in summary.FactionMetricTotals
        )
        {
            factionMetricTotals[entry.Key] = Project(entry.Value);
        }

        return new GDictionary
        {
            ["profile_id"] = summary.ProfileId,
            ["display_name"] = summary.DisplayName,
            ["run_count"] = summary.RunCount,
            ["wins_by_faction"] = ProjectIntDictionary(summary.WinsByFaction),
            ["win_rate_by_faction"] = ProjectFloatDictionary(summary.WinRateByFaction),
            ["average_final_tu"] = summary.AverageFinalTu,
            ["average_iterations"] = summary.AverageIterations,
            ["average_timeline_steps"] = summary.AverageTimelineSteps,
            ["skill_attempt_totals"] = ProjectIntDictionary(summary.SkillAttemptTotals),
            ["skill_usage_totals"] = ProjectIntDictionary(summary.SkillUsageTotals),
            ["skill_failure_totals"] = ProjectIntDictionary(summary.SkillFailureTotals),
            ["action_choice_counts"] = ProjectIntDictionary(summary.ActionChoiceCounts),
            ["faction_metric_totals"] = factionMetricTotals,
        };
    }

    internal static GDictionary Project(BattleSimFactionMetricSummary summary)
    {
        if (summary == null)
            return new GDictionary();
        return new GDictionary
        {
            ["unit_count"] = summary.UnitCount,
            ["turn_count"] = summary.TurnCount,
            ["successful_skill_count"] = summary.SuccessfulSkillCount,
            ["total_damage_done"] = summary.TotalDamageDone,
            ["total_healing_done"] = summary.TotalHealingDone,
            ["total_damage_taken"] = summary.TotalDamageTaken,
            ["total_healing_received"] = summary.TotalHealingReceived,
            ["kill_count"] = summary.KillCount,
            ["death_count"] = summary.DeathCount,
        };
    }

    internal static GDictionary Project(BattleSimMetricsSnapshot metrics)
    {
        if (metrics == null)
            return new GDictionary();
        return new GDictionary
        {
            ["battle_id"] = metrics.BattleId,
            ["seed"] = metrics.Seed,
            ["units"] = ProjectUnitMetrics(metrics.Units),
            ["factions"] = ProjectFactionMetrics(metrics.Factions),
        };
    }

    internal static GDictionary Project(BattleSimUnitMetricsSnapshot metrics)
    {
        if (metrics == null)
            return new GDictionary();
        var payload = new GDictionary
        {
            ["faction_id"] = metrics.FactionId,
            ["turn_count"] = metrics.TurnCount,
            ["action_counts"] = ProjectIntDictionary(metrics.ActionCounts),
            ["skill_attempt_counts"] = ProjectIntDictionary(metrics.SkillAttemptCounts),
            ["skill_success_counts"] = ProjectIntDictionary(metrics.SkillSuccessCounts),
            ["successful_skill_count"] = metrics.SuccessfulSkillCount,
            ["total_damage_done"] = metrics.TotalDamageDone,
            ["total_healing_done"] = metrics.TotalHealingDone,
            ["total_damage_taken"] = metrics.TotalDamageTaken,
            ["total_healing_received"] = metrics.TotalHealingReceived,
            ["kill_count"] = metrics.KillCount,
            ["death_count"] = metrics.DeathCount,
        };
        if (!string.IsNullOrEmpty(metrics.UnitId))
        {
            payload["unit_id"] = metrics.UnitId;
            payload["display_name"] = metrics.DisplayName;
            payload["control_mode"] = metrics.ControlMode;
            payload["source_member_id"] = metrics.SourceMemberId;
        }
        return payload;
    }

    internal static GDictionary ProjectUnitMetrics(
        IReadOnlyDictionary<string, BattleSimUnitMetricsSnapshot> metrics
    )
    {
        var payload = new GDictionary();
        foreach (
            KeyValuePair<string, BattleSimUnitMetricsSnapshot> entry
            in metrics ?? new Dictionary<string, BattleSimUnitMetricsSnapshot>()
        )
        {
            payload[entry.Key] = Project(entry.Value);
        }
        return payload;
    }

    internal static GDictionary ProjectFactionMetrics(
        IReadOnlyDictionary<string, BattleSimFactionMetricSummary> metrics
    )
    {
        var payload = new GDictionary();
        foreach (
            KeyValuePair<string, BattleSimFactionMetricSummary> entry
            in metrics ?? new Dictionary<string, BattleSimFactionMetricSummary>()
        )
        {
            payload[entry.Key] = Project(entry.Value);
        }
        return payload;
    }

    internal static GDictionary Project(BattleSimOutputFiles files)
    {
        if (files == null)
            return new GDictionary();

        var payload = new GDictionary
        {
            ["report_json"] = files.ReportJson,
            ["turn_trace_jsonl"] = files.TurnTraceJsonl,
        };
        if (!string.IsNullOrEmpty(files.TraceSummaryJson))
            payload["trace_summary_json"] = files.TraceSummaryJson;
        return payload;
    }

    internal static GDictionary Project(BattleSimProfileDef profile)
    {
        if (profile == null)
            return new GDictionary();
        return new GDictionary
        {
            ["profile_id"] = profile.profile_id.ToString(),
            ["display_name"] = profile.display_name,
            ["description"] = profile.description,
            ["ai_score_profile"] = BattleAiScoreProjection.Project(profile.ai_score_profile),
            ["override_patch_count"] = profile.override_patches.Count,
        };
    }

    internal static GDictionary Project(BattleSimScenarioDef scenario)
    {
        if (scenario == null)
            return new GDictionary();
        return new GDictionary
        {
            ["scenario_id"] = scenario.scenario_id.ToString(),
            ["display_name"] = scenario.display_name,
            ["description"] = scenario.description,
            ["map_size"] = scenario.map_size,
            ["terrain_profile_id"] = scenario.terrain_profile_id.ToString(),
            ["use_formal_terrain_generation"] = scenario.use_formal_terrain_generation,
            ["world_coord"] = scenario.world_coord,
            ["timeline_ticks_per_step"] = scenario.timeline_ticks_per_step,
            ["tu_per_tick"] = scenario.tu_per_tick,
            ["max_iterations"] = scenario.max_iterations,
            ["manual_policy"] = scenario.manual_policy.ToString(),
            ["trace_enabled"] = scenario.trace_enabled,
            ["seeds"] = scenario.ResolveSeeds(),
            ["ally_unit_count"] = scenario.ally_units.Count,
            ["enemy_unit_count"] = scenario.enemy_units.Count,
        };
    }

    internal static Godot.Collections.Array ProjectFinalUnitSnapshots(BattleState state)
    {
        var snapshots = new Godot.Collections.Array();
        if (state == null)
            return snapshots;

        foreach ((StringName _, BattleUnitState unitState) in state.UnitEntries(sorted: true))
        {
            if (unitState != null)
                snapshots.Add(unitState.ToDictionary());
        }
        return snapshots;
    }

    internal static GDictionary Project(BattleUnitState unitState) =>
        unitState?.ToDictionary() ?? new GDictionary();

    internal static GDictionary ProjectFlattenedTrace(
        BattleAiTurnTraceProjection trace,
        string scenarioId,
        string profileId,
        long seed
    )
    {
        GDictionary flattenedTrace = BattleAiTurnTracePayloadProjection.Project(trace);
        flattenedTrace["scenario_id"] = scenarioId ?? "";
        flattenedTrace["profile_id"] = profileId ?? "";
        flattenedTrace["seed"] = seed;
        return flattenedTrace;
    }

    private static GDictionary ProjectIntDictionary(IReadOnlyDictionary<string, int> source)
    {
        var payload = new GDictionary();
        foreach (KeyValuePair<string, int> entry in source ?? new Dictionary<string, int>())
        {
            payload[entry.Key] = entry.Value;
        }
        return payload;
    }

    private static GDictionary ProjectFloatDictionary(IReadOnlyDictionary<string, float> source)
    {
        var payload = new GDictionary();
        foreach (KeyValuePair<string, float> entry in source ?? new Dictionary<string, float>())
        {
            payload[entry.Key] = entry.Value;
        }
        return payload;
    }
}
