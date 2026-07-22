using System.Collections.Generic;
using Godot;

public sealed class BattleSimReportBuilder
{
    internal BattleSimProfileSummary BuildProfileSummary(
        BattleSimProfileDefinition profile,
        IReadOnlyList<BattleSimRunReport> runs
    )
    {
        var summary = new BattleSimProfileSummary
        {
            ProfileId = profile?.ProfileId.ToString() ?? "",
            DisplayName = profile?.DisplayName ?? "",
            RunCount = runs?.Count ?? 0,
        };

        int totalFinalTu = 0;
        int totalIterations = 0;
        int totalTimelineSteps = 0;

        if (runs != null)
        {
            foreach (BattleSimRunReport run in runs)
            {
                if (run == null)
                {
                    summary.InvalidRuntimeRunCount++;
                    continue;
                }

                switch (run.TerminationKind)
                {
                    case BattleSimTerminationKind.BattleEnded:
                        if (!run.HasFinalDecision)
                        {
                            summary.InvalidRuntimeRunCount++;
                            continue;
                        }
                        summary.CompletedRunCount++;
                        break;
                    case BattleSimTerminationKind.IdleStall:
                        summary.StalledRunCount++;
                        continue;
                    case BattleSimTerminationKind.IterationBudgetExhausted:
                        summary.IterationBudgetExhaustedRunCount++;
                        continue;
                    case BattleSimTerminationKind.InvalidRuntime:
                    default:
                        summary.InvalidRuntimeRunCount++;
                        continue;
                }

                switch (run.Outcome)
                {
                    case BattleOutcomeKind.PlayerSuccess:
                    case BattleOutcomeKind.PlayerFailure:
                    case BattleOutcomeKind.Draw:
                        IncrementCounter(summary.WinsByFaction, run.WinnerFactionId);
                        break;
                }

                totalFinalTu += run.FinalTu;
                totalIterations += run.Iterations;
                totalTimelineSteps += run.TimelineSteps;

                MergeSkillCounter(
                    summary.SkillAttemptTotals,
                    run.MetricsSnapshot,
                    useSuccessCounts: false
                );
                MergeSkillCounter(
                    summary.SkillUsageTotals,
                    run.MetricsSnapshot,
                    useSuccessCounts: true
                );
                MergeActionChoices(summary.ActionChoiceCounts, run.AiTurnTraces);
                MergeFactionMetricTotals(summary.FactionMetricTotals, run.MetricsSnapshot);
            }
        }

        summary.UnfinishedRunCount = summary.RunCount - summary.CompletedRunCount;

        int completedRunCount = Mathf.Max(summary.CompletedRunCount, 1);
        summary.AverageFinalTu = (float)totalFinalTu / completedRunCount;
        summary.AverageIterations = (float)totalIterations / completedRunCount;
        summary.AverageTimelineSteps = (float)totalTimelineSteps / completedRunCount;

        foreach (KeyValuePair<string, float> entry in BuildRateDictionary(summary.WinsByFaction, summary.CompletedRunCount))
            summary.WinRateByFaction[entry.Key] = entry.Value;
        foreach (
            KeyValuePair<string, int> entry in BuildSkillFailureTotals(
                summary.SkillAttemptTotals,
                summary.SkillUsageTotals
            )
        )
        {
            summary.SkillFailureTotals[entry.Key] = entry.Value;
        }

        return summary;
    }

    public List<BattleSimProfileComparison> BuildProfileComparisons(
        IReadOnlyList<BattleSimProfileReportEntry> profileEntries
    )
    {
        var comparisons = new List<BattleSimProfileComparison>();
        if (profileEntries == null || profileEntries.Count <= 1)
            return comparisons;

        BattleSimProfileSummary baselineSummary = profileEntries[0]?.Summary;
        if (baselineSummary == null)
            return comparisons;

        for (int entryIndex = 1; entryIndex < profileEntries.Count; entryIndex++)
        {
            BattleSimProfileSummary candidateSummary = profileEntries[entryIndex]?.Summary;
            if (
                candidateSummary == null
                || baselineSummary.CompletedRunCount <= 0
                || candidateSummary.CompletedRunCount <= 0
            )
                continue;

            var comparison = new BattleSimProfileComparison
            {
                BaselineProfileId = baselineSummary.ProfileId,
                CandidateProfileId = candidateSummary.ProfileId,
                BaselineRunCount = baselineSummary.RunCount,
                BaselineCompletedRunCount = baselineSummary.CompletedRunCount,
                CandidateRunCount = candidateSummary.RunCount,
                CandidateCompletedRunCount = candidateSummary.CompletedRunCount,
                AverageFinalTuDelta = candidateSummary.AverageFinalTu - baselineSummary.AverageFinalTu,
                AverageIterationsDelta =
                    candidateSummary.AverageIterations - baselineSummary.AverageIterations,
                AverageTimelineStepsDelta =
                    candidateSummary.AverageTimelineSteps - baselineSummary.AverageTimelineSteps,
            };

            CopyInto(
                comparison.WinRateDelta,
                DiffFloatDictionary(baselineSummary.WinRateByFaction, candidateSummary.WinRateByFaction)
            );
            CopyInto(
                comparison.SkillUsageDelta,
                DiffIntDictionary(baselineSummary.SkillUsageTotals, candidateSummary.SkillUsageTotals)
            );
            CopyInto(
                comparison.SkillAttemptDelta,
                DiffIntDictionary(
                    baselineSummary.SkillAttemptTotals,
                    candidateSummary.SkillAttemptTotals
                )
            );
            CopyInto(
                comparison.SkillFailureDelta,
                DiffIntDictionary(
                    baselineSummary.SkillFailureTotals,
                    candidateSummary.SkillFailureTotals
                )
            );
            CopyInto(
                comparison.ActionChoiceDelta,
                DiffIntDictionary(
                    baselineSummary.ActionChoiceCounts,
                    candidateSummary.ActionChoiceCounts
                )
            );

            comparisons.Add(comparison);
        }

        return comparisons;
    }

    private static void MergeSkillCounter(
        Dictionary<string, int> skillTotals,
        BattleSimMetricsSnapshot metrics,
        bool useSuccessCounts
    )
    {
        if (metrics == null)
            return;
        foreach (BattleSimUnitMetricsSnapshot unitMetrics in metrics.Units.Values)
        {
            IReadOnlyDictionary<string, int> skillCounts = useSuccessCounts
                ? unitMetrics.SkillSuccessCounts
                : unitMetrics.SkillAttemptCounts;
            foreach (KeyValuePair<string, int> entry in skillCounts)
                IncrementCounter(skillTotals, entry.Key, entry.Value);
        }
    }

    private static void MergeActionChoices(
        Dictionary<string, int> actionChoiceCounts,
        IReadOnlyList<BattleAiTurnTraceProjection> aiTurnTraces
    )
    {
        if (aiTurnTraces == null)
            return;

        foreach (BattleAiTurnTraceProjection traceEntry in aiTurnTraces)
        {
            string actionId = traceEntry?.ActionId ?? "";
            if (string.IsNullOrEmpty(actionId))
                continue;
            IncrementCounter(actionChoiceCounts, actionId);
        }
    }

    private static void MergeFactionMetricTotals(
        Dictionary<string, BattleSimFactionMetricSummary> factionMetricTotals,
        BattleSimMetricsSnapshot metrics
    )
    {
        if (metrics == null)
            return;
        foreach (KeyValuePair<string, BattleSimUnitMetricsSnapshot> entry in metrics.Factions)
        {
            if (!factionMetricTotals.TryGetValue(entry.Key, out BattleSimFactionMetricSummary targetEntry))
            {
                targetEntry = new BattleSimFactionMetricSummary();
                factionMetricTotals[entry.Key] = targetEntry;
            }
            AccumulateFactionMetrics(targetEntry, entry.Value);
        }
    }

    private static void AccumulateFactionMetrics(
        BattleSimFactionMetricSummary target,
        BattleSimUnitMetricsSnapshot source
    )
    {
        if (target == null || source == null)
            return;
        target.UnitCount += source.UnitCount;
        target.TurnCount += source.TurnCount;
        MergeCounters(target.ActionCounts, source.ActionCounts);
        MergeCounters(target.SkillAttemptCounts, source.SkillAttemptCounts);
        MergeCounters(target.SkillSuccessCounts, source.SkillSuccessCounts);
        target.SuccessfulSkillCount += source.SuccessfulSkillCount;
        target.TotalDamageDone += source.TotalDamageDone;
        target.TotalHealingDone += source.TotalHealingDone;
        target.TotalDamageTaken += source.TotalDamageTaken;
        target.TotalHealingReceived += source.TotalHealingReceived;
        target.KillCount += source.KillCount;
        target.DeathCount += source.DeathCount;
    }

    private static void MergeCounters(
        Dictionary<string, int> target,
        IReadOnlyDictionary<string, int> source
    )
    {
        if (target == null || source == null)
            return;
        foreach (KeyValuePair<string, int> entry in source)
            IncrementCounter(target, entry.Key, entry.Value);
    }

    private static Dictionary<string, int> BuildSkillFailureTotals(
        IReadOnlyDictionary<string, int> skillAttemptTotals,
        IReadOnlyDictionary<string, int> skillUsageTotals
    )
    {
        var failures = NewIntMap();
        var keys = CollectStringKeys(skillAttemptTotals.Keys, skillUsageTotals.Keys);
        foreach (string skillKey in keys)
        {
            int attempts = skillAttemptTotals.TryGetValue(skillKey, out int attemptCount)
                ? attemptCount
                : 0;
            int successes = skillUsageTotals.TryGetValue(skillKey, out int successCount)
                ? successCount
                : 0;
            int failuresForSkill = Mathf.Max(attempts - successes, 0);
            if (failuresForSkill > 0)
                failures[skillKey] = failuresForSkill;
        }
        return failures;
    }

    private static Dictionary<string, float> BuildRateDictionary(
        IReadOnlyDictionary<string, int> counts,
        int runCount
    )
    {
        var rates = NewFloatMap();
        if (runCount <= 0)
            return rates;

        foreach (KeyValuePair<string, int> entry in counts)
            rates[entry.Key] = (float)entry.Value / runCount;
        return rates;
    }

    private static Dictionary<string, int> DiffIntDictionary(
        IReadOnlyDictionary<string, int> baseline,
        IReadOnlyDictionary<string, int> candidate
    )
    {
        var diff = NewIntMap();
        var keys = CollectStringKeys(baseline.Keys, candidate.Keys);
        foreach (string key in keys)
        {
            int baselineValue = baseline.TryGetValue(key, out int left) ? left : 0;
            int candidateValue = candidate.TryGetValue(key, out int right) ? right : 0;
            diff[key] = candidateValue - baselineValue;
        }
        return diff;
    }

    private static Dictionary<string, float> DiffFloatDictionary(
        IReadOnlyDictionary<string, float> baseline,
        IReadOnlyDictionary<string, float> candidate
    )
    {
        var diff = NewFloatMap();
        var keys = CollectStringKeys(baseline.Keys, candidate.Keys);
        foreach (string key in keys)
        {
            float baselineValue = baseline.TryGetValue(key, out float left) ? left : 0.0f;
            float candidateValue = candidate.TryGetValue(key, out float right) ? right : 0.0f;
            diff[key] = candidateValue - baselineValue;
        }
        return diff;
    }

    private static void IncrementCounter(Dictionary<string, int> counters, string key, int amount = 1)
    {
        if (string.IsNullOrEmpty(key))
            return;
        counters[key] = (counters.TryGetValue(key, out int existing) ? existing : 0) + amount;
    }

    private static HashSet<string> CollectStringKeys(
        IEnumerable<string> left,
        IEnumerable<string> right
    )
    {
        var keys = new HashSet<string>(System.StringComparer.Ordinal);
        if (left != null)
        {
            foreach (string key in left)
                keys.Add(key);
        }
        if (right != null)
        {
            foreach (string key in right)
                keys.Add(key);
        }
        return keys;
    }

    private static void CopyInto<TValue>(
        Dictionary<string, TValue> target,
        IReadOnlyDictionary<string, TValue> source
    )
    {
        foreach (KeyValuePair<string, TValue> entry in source)
            target[entry.Key] = entry.Value;
    }

    private static Dictionary<string, int> NewIntMap() => new(System.StringComparer.Ordinal);

    private static Dictionary<string, float> NewFloatMap() => new(System.StringComparer.Ordinal);

}
