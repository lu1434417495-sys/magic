#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

internal static class EquipmentClosureGenerationBattleSimRules
{
    internal const string NoEquipmentCandidate =
        "equipment_closure.battle_simulation.no_equipment_candidate";
    internal const string ProjectionFailed =
        "equipment_closure.battle_simulation.projection_failed";
    internal const string IncompleteSamples =
        "equipment_closure.battle_simulation.incomplete_samples";
    internal const string HighStrengthOutlier =
        "equipment_closure.battle_simulation.high_strength_outlier";
}

internal sealed class EquipmentClosureGenerationBattleSimOptions
{
    private static readonly IReadOnlyList<int> FormalSeeds = Array.AsReadOnly(
        new[]
        {
            84001, 84002, 84003, 84004, 84005, 84006,
            84007, 84008, 84009, 84010, 84011, 84012,
        }
    );

    internal EquipmentClosureGenerationBattleSimOptions(
        IReadOnlyList<int>? seeds = null,
        int minimumCompletedSamples = 12,
        int maximumSampledItems = 8,
        int maximumWinRateDeltaBasisPoints = 5000,
        int maximumDamageRatioBasisPoints = 40000
    )
    {
        Seeds = FreezeSeeds(seeds ?? FormalSeeds);
        if (minimumCompletedSamples <= 0 || minimumCompletedSamples > Seeds.Count)
            throw new ArgumentOutOfRangeException(nameof(minimumCompletedSamples));
        if (maximumSampledItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumSampledItems));
        if (maximumWinRateDeltaBasisPoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumWinRateDeltaBasisPoints));
        if (maximumDamageRatioBasisPoints <= 10000)
            throw new ArgumentOutOfRangeException(nameof(maximumDamageRatioBasisPoints));
        MinimumCompletedSamples = minimumCompletedSamples;
        MaximumSampledItems = maximumSampledItems;
        MaximumWinRateDeltaBasisPoints = maximumWinRateDeltaBasisPoints;
        MaximumDamageRatioBasisPoints = maximumDamageRatioBasisPoints;
    }

    internal IReadOnlyList<int> Seeds { get; }
    internal int MinimumCompletedSamples { get; }
    internal int MaximumSampledItems { get; }
    internal int MaximumWinRateDeltaBasisPoints { get; }
    internal int MaximumDamageRatioBasisPoints { get; }

    private static IReadOnlyList<int> FreezeSeeds(IReadOnlyList<int> source)
    {
        if (source == null || source.Count == 0)
            throw new ArgumentException("BattleSim seeds cannot be empty.", nameof(source));
        var seen = new HashSet<int>();
        var result = new List<int>(source.Count);
        foreach (int seed in source)
        {
            if (!seen.Add(seed))
                throw new ArgumentException($"BattleSim seed {seed} is duplicated.", nameof(source));
            result.Add(seed);
        }
        return new ReadOnlyCollection<int>(result);
    }
}

internal sealed record EquipmentClosureGenerationBattleSimStrengthSample(
    string ItemId,
    int BaselineCompletedSamples,
    int CandidateCompletedSamples,
    int BaselineAllyWinRateBasisPoints,
    int CandidateAllyWinRateBasisPoints,
    int WinRateDeltaBasisPoints,
    int BaselineDamagePerCompletedRunBasisPoints,
    int CandidateDamagePerCompletedRunBasisPoints,
    int DamageRatioBasisPoints,
    int EquipmentAbilitySourceCount,
    string BaselineItemId,
    string BaselineReportPath,
    string CandidateReportPath
);

internal static class EquipmentClosureGenerationBattleSimStrengthAnalyzer
{
    internal static IReadOnlyList<ContentJsonDiagnostic> Evaluate(
        EquipmentClosureGenerationBattleSimStrengthSample sample,
        EquipmentClosureGenerationBattleSimOptions options,
        JsonContentEntryContext context
    )
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);
        if (
            sample.BaselineCompletedSamples < options.MinimumCompletedSamples
            || sample.CandidateCompletedSamples < options.MinimumCompletedSamples
        )
        {
            return Array.AsReadOnly(new[]
            {
                Diagnostic(
                    EquipmentClosureGenerationBattleSimRules.IncompleteSamples,
                    "BattleSim did not produce the minimum comparable completed sample set.",
                    context,
                    $"at least {options.MinimumCompletedSamples} completed samples per arm",
                    FormatActual(sample)
                ),
            });
        }

        if (
            sample.WinRateDeltaBasisPoints > options.MaximumWinRateDeltaBasisPoints
            || sample.DamageRatioBasisPoints > options.MaximumDamageRatioBasisPoints
        )
        {
            return Array.AsReadOnly(new[]
            {
                Diagnostic(
                    EquipmentClosureGenerationBattleSimRules.HighStrengthOutlier,
                    "Candidate equipment strength is above the configured BattleSim outlier envelope.",
                    context,
                    $"win_rate_delta_bp <= {options.MaximumWinRateDeltaBasisPoints} and damage_ratio_bp <= {options.MaximumDamageRatioBasisPoints}",
                    FormatActual(sample)
                ),
            });
        }
        return Array.Empty<ContentJsonDiagnostic>();
    }

    private static string FormatActual(
        EquipmentClosureGenerationBattleSimStrengthSample sample
    ) =>
        $"completed={sample.BaselineCompletedSamples}/{sample.CandidateCompletedSamples};"
        + $"win_rate_delta_bp={sample.WinRateDeltaBasisPoints};"
        + $"damage_ratio_bp={sample.DamageRatioBasisPoints};"
        + $"equipment_ability_sources={sample.EquipmentAbilitySourceCount};"
        + $"baseline_item_id={sample.BaselineItemId}";

    private static ContentJsonDiagnostic Diagnostic(
        string ruleId,
        string message,
        JsonContentEntryContext context,
        string expected,
        string actual
    ) => new(
        ruleId,
        message,
        context.SourceLabel,
        context.JsonPointer,
        expected,
        actual
    );
}
