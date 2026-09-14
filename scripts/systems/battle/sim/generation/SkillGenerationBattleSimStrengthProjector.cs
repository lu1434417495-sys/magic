#nullable enable

using System;
using System.Collections.Generic;
using Godot;

internal static class SkillGenerationBattleSimStrengthProjector
{
    private const string AllyFactionId = "player";

    internal static SkillGenerationBattleSimStrengthSample BuildSample(
        StringName skillId,
        BattleSimScenarioReport baseline,
        BattleSimScenarioReport candidate
    )
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(candidate);
        BattleSimProfileSummary baselineSummary = RequireSingleSummary(baseline);
        BattleSimProfileSummary candidateSummary = RequireSingleSummary(candidate);
        int baselineWinRate = RateBasisPoints(
            baselineSummary.WinRateByFaction,
            AllyFactionId
        );
        int candidateWinRate = RateBasisPoints(
            candidateSummary.WinRateByFaction,
            AllyFactionId
        );
        int baselineDamage = DamagePerCompletedRunBasisPoints(baselineSummary);
        int candidateDamage = DamagePerCompletedRunBasisPoints(candidateSummary);
        return new SkillGenerationBattleSimStrengthSample(
            skillId,
            baselineSummary.CompletedRunCount,
            candidateSummary.CompletedRunCount,
            Counter(candidateSummary.SkillAttemptTotals, skillId.ToString()),
            Counter(candidateSummary.SkillUsageTotals, skillId.ToString()),
            baselineWinRate,
            candidateWinRate,
            candidateWinRate - baselineWinRate,
            baselineDamage,
            candidateDamage,
            RatioBasisPoints(candidateDamage, baselineDamage),
            baseline.OutputFiles?.ReportJson ?? "",
            candidate.OutputFiles?.ReportJson ?? ""
        );
    }

    private static BattleSimProfileSummary RequireSingleSummary(
        BattleSimScenarioReport report
    )
    {
        if (report.ProfileEntries.Count != 1 || report.ProfileEntries[0]?.Summary == null)
        {
            throw new InvalidOperationException(
                "Skill generation BattleSim requires exactly one profile summary."
            );
        }
        return report.ProfileEntries[0].Summary;
    }

    private static int RateBasisPoints(
        IReadOnlyDictionary<string, float> rates,
        string key
    ) => rates.TryGetValue(key, out float value)
        ? (int)Math.Round(value * 10000.0f, MidpointRounding.AwayFromZero)
        : 0;

    private static int DamagePerCompletedRunBasisPoints(
        BattleSimProfileSummary summary
    )
    {
        if (
            summary.CompletedRunCount <= 0
            || !summary.FactionMetricTotals.TryGetValue(
                AllyFactionId,
                out BattleSimFactionMetricSummary? metrics
            )
        )
        {
            return 0;
        }
        return (int)Math.Round(
            metrics.TotalDamageDone * 10000.0 / summary.CompletedRunCount,
            MidpointRounding.AwayFromZero
        );
    }

    private static int RatioBasisPoints(int candidate, int baseline)
    {
        if (baseline <= 0)
            return candidate <= 0 ? 10000 : 100000;
        return (int)Math.Clamp(
            Math.Round(candidate * 10000.0 / baseline, MidpointRounding.AwayFromZero),
            0,
            100000
        );
    }

    private static int Counter(
        IReadOnlyDictionary<string, int> counters,
        string key
    ) => counters.TryGetValue(key, out int value) ? value : 0;
}
