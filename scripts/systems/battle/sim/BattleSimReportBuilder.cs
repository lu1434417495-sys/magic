using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleSimReportBuilder : RefCounted
{
    public Dictionary BuildProfileSummary(GodotObject profile, Array runs)
    {
        var winsByFaction = new Dictionary();
        var skillAttemptTotals = new Dictionary();
        var skillUsageTotals = new Dictionary();
        var actionChoiceCounts = new Dictionary();
        var factionMetricTotals = new Dictionary();
        int totalFinalTu = 0;
        int totalIterations = 0;
        int totalTimelineSteps = 0;

        foreach (var runEntryVariant in runs)
        {
            if (runEntryVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var runEntry = runEntryVariant.AsGodotDictionary();
            var winnerFaction = runEntry.GetValueOrDefault("winner_faction_id", "").AsString();
            if (!string.IsNullOrEmpty(winnerFaction))
            {
                winsByFaction[winnerFaction] = winsByFaction.GetValueOrDefault(winnerFaction, 0).AsInt32() + 1;
            }
            totalFinalTu += runEntry.GetValueOrDefault("final_tu", 0).AsInt32();
            totalIterations += runEntry.GetValueOrDefault("iterations", 0).AsInt32();
            totalTimelineSteps += runEntry.GetValueOrDefault("timeline_steps", 0).AsInt32();
            MergeSkillCounter(skillAttemptTotals, runEntry.GetValueOrDefault("metrics", new Dictionary()).AsGodotDictionary(), "skill_attempt_counts");
            MergeSkillUsage(skillUsageTotals, runEntry.GetValueOrDefault("metrics", new Dictionary()).AsGodotDictionary());
            MergeActionChoices(actionChoiceCounts, runEntry.GetValueOrDefault("ai_turn_traces", new Array()).AsGodotArray());
            MergeFactionMetricTotals(factionMetricTotals, runEntry.GetValueOrDefault("metrics", new Dictionary()).AsGodotDictionary());
        }

        int runCount = Mathf.Max(runs.Count, 1);
        return new Dictionary
        {
            ["profile_id"] = profile != null ? profile.Get("profile_id").AsStringName().ToString() : "",
            ["display_name"] = profile != null ? profile.Get("display_name").AsString() : "",
            ["run_count"] = runs.Count,
            ["wins_by_faction"] = winsByFaction,
            ["win_rate_by_faction"] = BuildRateDictionary(winsByFaction, runs.Count),
            ["average_final_tu"] = (float)totalFinalTu / (float)runCount,
            ["average_iterations"] = (float)totalIterations / (float)runCount,
            ["average_timeline_steps"] = (float)totalTimelineSteps / (float)runCount,
            ["skill_attempt_totals"] = skillAttemptTotals,
            ["skill_usage_totals"] = skillUsageTotals,
            ["skill_failure_totals"] = BuildSkillFailureTotals(skillAttemptTotals, skillUsageTotals),
            ["action_choice_counts"] = actionChoiceCounts,
            ["faction_metric_totals"] = factionMetricTotals,
        };
    }

    public Array<Dictionary> BuildProfileComparisons(Array profileEntries)
    {
        var comparisons = new Array<Dictionary>();
        if (profileEntries.Count <= 1)
            return comparisons;

        var baselineEntry = profileEntries[0].AsGodotDictionary();
        if (baselineEntry == null)
            return comparisons;

        var baselineSummary = baselineEntry.GetValueOrDefault("summary", new Dictionary()).AsGodotDictionary();
        for (int entryIndex = 1; entryIndex < profileEntries.Count; entryIndex++)
        {
            var candidateEntry = profileEntries[entryIndex].AsGodotDictionary();
            if (candidateEntry == null)
                continue;
            var candidateSummary = candidateEntry.GetValueOrDefault("summary", new Dictionary()).AsGodotDictionary();
            comparisons.Add(new Dictionary
            {
                ["baseline_profile_id"] = baselineSummary.GetValueOrDefault("profile_id", "").AsString(),
                ["candidate_profile_id"] = candidateSummary.GetValueOrDefault("profile_id", "").AsString(),
                ["average_final_tu_delta"] = candidateSummary.GetValueOrDefault("average_final_tu", 0.0f).AsSingle() - baselineSummary.GetValueOrDefault("average_final_tu", 0.0f).AsSingle(),
                ["average_iterations_delta"] = candidateSummary.GetValueOrDefault("average_iterations", 0.0f).AsSingle() - baselineSummary.GetValueOrDefault("average_iterations", 0.0f).AsSingle(),
                ["average_timeline_steps_delta"] = candidateSummary.GetValueOrDefault("average_timeline_steps", 0.0f).AsSingle() - baselineSummary.GetValueOrDefault("average_timeline_steps", 0.0f).AsSingle(),
                ["win_rate_delta"] = DiffNumberDictionary(
                    baselineSummary.GetValueOrDefault("win_rate_by_faction", new Dictionary()),
                    candidateSummary.GetValueOrDefault("win_rate_by_faction", new Dictionary())),
                ["skill_usage_delta"] = DiffIntDictionary(
                    baselineSummary.GetValueOrDefault("skill_usage_totals", new Dictionary()),
                    candidateSummary.GetValueOrDefault("skill_usage_totals", new Dictionary())),
                ["skill_attempt_delta"] = DiffIntDictionary(
                    baselineSummary.GetValueOrDefault("skill_attempt_totals", new Dictionary()),
                    candidateSummary.GetValueOrDefault("skill_attempt_totals", new Dictionary())),
                ["skill_failure_delta"] = DiffIntDictionary(
                    baselineSummary.GetValueOrDefault("skill_failure_totals", new Dictionary()),
                    candidateSummary.GetValueOrDefault("skill_failure_totals", new Dictionary())),
                ["action_choice_delta"] = DiffIntDictionary(
                    baselineSummary.GetValueOrDefault("action_choice_counts", new Dictionary()),
                    candidateSummary.GetValueOrDefault("action_choice_counts", new Dictionary())),
            });
        }
        return comparisons;
    }

    private void MergeSkillUsage(Dictionary skillUsageTotals, Dictionary metrics)
    {
        MergeSkillCounter(skillUsageTotals, metrics, "skill_success_counts");
    }

    private void MergeSkillCounter(Dictionary skillTotals, Dictionary metrics, string counterKey)
    {
        var units = metrics.GetValueOrDefault("units", new Dictionary());
        if (units.VariantType != Variant.Type.Dictionary)
            return;
        var unitsDict = units.AsGodotDictionary();
        foreach (var unitEntryVariant in unitsDict.Values)
        {
            if (unitEntryVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var unitEntry = unitEntryVariant.AsGodotDictionary();
            var skillCounts = unitEntry.GetValueOrDefault(counterKey, new Dictionary());
            if (skillCounts.VariantType != Variant.Type.Dictionary)
                continue;
            var skillCountsDict = skillCounts.AsGodotDictionary();
            foreach (var skillKey in skillCountsDict.Keys)
            {
                var normalizedKey = skillKey.AsString();
                skillTotals[normalizedKey] = skillTotals.GetValueOrDefault(normalizedKey, 0).AsInt32() + skillCountsDict[skillKey].AsInt32();
            }
        }
    }

    private Dictionary BuildSkillFailureTotals(Dictionary skillAttemptTotals, Dictionary skillUsageTotals)
    {
        var failures = new Dictionary();
        var keys = new Dictionary();
        foreach (var skillKey in skillAttemptTotals.Keys)
        {
            keys[skillKey.AsString()] = true;
        }
        foreach (var skillKey in skillUsageTotals.Keys)
        {
            keys[skillKey.AsString()] = true;
        }
        foreach (var skillKey in keys.Keys)
        {
            var attempts = skillAttemptTotals.GetValueOrDefault(skillKey, 0).AsInt32();
            var successes = skillUsageTotals.GetValueOrDefault(skillKey, 0).AsInt32();
            var failuresForSkill = Mathf.Max(attempts - successes, 0);
            if (failuresForSkill > 0)
            {
                failures[skillKey] = failuresForSkill;
            }
        }
        return failures;
    }

    private void MergeActionChoices(Dictionary actionChoiceCounts, Array aiTurnTraces)
    {
        foreach (var traceEntryVariant in aiTurnTraces)
        {
            if (traceEntryVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var traceEntry = traceEntryVariant.AsGodotDictionary();
            var actionId = traceEntry.GetValueOrDefault("action_id", "").AsString();
            if (string.IsNullOrEmpty(actionId))
                continue;
            actionChoiceCounts[actionId] = actionChoiceCounts.GetValueOrDefault(actionId, 0).AsInt32() + 1;
        }
    }

    private void MergeFactionMetricTotals(Dictionary factionMetricTotals, Dictionary metrics)
    {
        var factions = metrics.GetValueOrDefault("factions", new Dictionary());
        if (factions.VariantType != Variant.Type.Dictionary)
            return;
        var factionsDict = factions.AsGodotDictionary();
        foreach (var factionKey in factionsDict.Keys)
        {
            var sourceEntry = factionsDict.GetValueOrDefault(factionKey, new Dictionary());
            if (sourceEntry.VariantType != Variant.Type.Dictionary)
                continue;
            var sourceDict = sourceEntry.AsGodotDictionary();
            var normalizedKey = factionKey.AsString();
            var targetEntry = factionMetricTotals.GetValueOrDefault(normalizedKey, new Dictionary()).AsGodotDictionary().Duplicate(true);
            foreach (var metricKey in new[] { "unit_count", "turn_count", "successful_skill_count", "total_damage_done", "total_healing_done", "total_damage_taken", "total_healing_received", "kill_count", "death_count" })
            {
                targetEntry[metricKey] = targetEntry.GetValueOrDefault(metricKey, 0).AsInt32() + sourceDict.GetValueOrDefault(metricKey, 0).AsInt32();
            }
            factionMetricTotals[normalizedKey] = targetEntry;
        }
    }

    private Dictionary BuildRateDictionary(Dictionary counts, int totalCount)
    {
        var rates = new Dictionary();
        if (totalCount <= 0)
            return rates;
        foreach (var countKey in counts.Keys)
        {
            rates[countKey.AsString()] = (float)counts[countKey].AsInt32() / (float)totalCount;
        }
        return rates;
    }

    private Dictionary DiffIntDictionary(Variant baseline, Variant candidate)
    {
        var diff = new Dictionary();
        var keys = new Dictionary();
        if (baseline.VariantType == Variant.Type.Dictionary)
        {
            foreach (var key in baseline.AsGodotDictionary().Keys)
            {
                keys[key.AsString()] = true;
            }
        }
        if (candidate.VariantType == Variant.Type.Dictionary)
        {
            foreach (var key in candidate.AsGodotDictionary().Keys)
            {
                keys[key.AsString()] = true;
            }
        }
        var baselineDict = baseline.VariantType == Variant.Type.Dictionary ? baseline.AsGodotDictionary() : new Dictionary();
        var candidateDict = candidate.VariantType == Variant.Type.Dictionary ? candidate.AsGodotDictionary() : new Dictionary();
        foreach (var key in keys.Keys)
        {
            diff[key] = candidateDict.GetValueOrDefault(key, 0).AsInt32() - baselineDict.GetValueOrDefault(key, 0).AsInt32();
        }
        return diff;
    }

    private Dictionary DiffNumberDictionary(Variant baseline, Variant candidate)
    {
        var diff = new Dictionary();
        var keys = new Dictionary();
        if (baseline.VariantType == Variant.Type.Dictionary)
        {
            foreach (var key in baseline.AsGodotDictionary().Keys)
            {
                keys[key.AsString()] = true;
            }
        }
        if (candidate.VariantType == Variant.Type.Dictionary)
        {
            foreach (var key in candidate.AsGodotDictionary().Keys)
            {
                keys[key.AsString()] = true;
            }
        }
        var baselineDict = baseline.VariantType == Variant.Type.Dictionary ? baseline.AsGodotDictionary() : new Dictionary();
        var candidateDict = candidate.VariantType == Variant.Type.Dictionary ? candidate.AsGodotDictionary() : new Dictionary();
        foreach (var key in keys.Keys)
        {
            diff[key] = candidateDict.GetValueOrDefault(key, 0.0f).AsSingle() - baselineDict.GetValueOrDefault(key, 0.0f).AsSingle();
        }
        return diff;
    }
}
