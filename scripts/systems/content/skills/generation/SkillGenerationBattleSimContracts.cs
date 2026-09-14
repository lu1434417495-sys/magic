#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal static class SkillGenerationBattleSimRules
{
    internal const string UnsupportedSurface =
        "skill.generation.battle_simulation.unsupported_surface";
    internal const string IncompleteSamples =
        "skill.generation.battle_simulation.incomplete_samples";
    internal const string CandidateNotExercised =
        "skill.generation.battle_simulation.candidate_not_exercised";
    internal const string HighStrengthOutlier =
        "skill.generation.battle_simulation.high_strength_outlier";
    internal const string LowStrengthOutlier =
        "skill.generation.battle_simulation.low_strength_outlier";
}

internal sealed class SkillGenerationBattleSimOptions
{
    private static readonly IReadOnlyList<int> FormalSeeds = Array.AsReadOnly(
        new[]
        {
            73001, 73002, 73003, 73004, 73005,
            73006, 73007, 73008, 73009, 73010,
            73011, 73012, 73013, 73014, 73015,
            73016, 73017, 73018, 73019, 73020,
        }
    );

    internal SkillGenerationBattleSimOptions(
        IReadOnlyList<int>? seeds = null,
        int minimumCompletedSamples = 20,
        int minimumCandidateAttempts = 3,
        int maximumWinRateDeltaBasisPoints = 3500,
        int minimumWinRateDeltaBasisPoints = -3500,
        int maximumDamageRatioBasisPoints = 22500,
        int minimumDamageRatioBasisPoints = 4500
    )
    {
        Seeds = FreezeSeeds(seeds ?? FormalSeeds);
        if (minimumCompletedSamples <= 0 || minimumCompletedSamples > Seeds.Count)
            throw new ArgumentOutOfRangeException(nameof(minimumCompletedSamples));
        if (minimumCandidateAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumCandidateAttempts));
        if (maximumWinRateDeltaBasisPoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumWinRateDeltaBasisPoints));
        if (minimumWinRateDeltaBasisPoints >= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumWinRateDeltaBasisPoints));
        if (maximumDamageRatioBasisPoints <= 10000)
            throw new ArgumentOutOfRangeException(nameof(maximumDamageRatioBasisPoints));
        if (minimumDamageRatioBasisPoints <= 0 || minimumDamageRatioBasisPoints >= 10000)
            throw new ArgumentOutOfRangeException(nameof(minimumDamageRatioBasisPoints));

        MinimumCompletedSamples = minimumCompletedSamples;
        MinimumCandidateAttempts = minimumCandidateAttempts;
        MaximumWinRateDeltaBasisPoints = maximumWinRateDeltaBasisPoints;
        MinimumWinRateDeltaBasisPoints = minimumWinRateDeltaBasisPoints;
        MaximumDamageRatioBasisPoints = maximumDamageRatioBasisPoints;
        MinimumDamageRatioBasisPoints = minimumDamageRatioBasisPoints;
    }

    internal IReadOnlyList<int> Seeds { get; }
    internal int MinimumCompletedSamples { get; }
    internal int MinimumCandidateAttempts { get; }
    internal int MaximumWinRateDeltaBasisPoints { get; }
    internal int MinimumWinRateDeltaBasisPoints { get; }
    internal int MaximumDamageRatioBasisPoints { get; }
    internal int MinimumDamageRatioBasisPoints { get; }

    private static IReadOnlyList<int> FreezeSeeds(IReadOnlyList<int> source)
    {
        if (source == null || source.Count == 0)
            throw new ArgumentException("BattleSim seeds cannot be empty.", nameof(source));
        var seen = new HashSet<int>();
        var copy = new List<int>(source.Count);
        foreach (int seed in source)
        {
            if (!seen.Add(seed))
                throw new ArgumentException($"BattleSim seed {seed} is duplicated.", nameof(source));
            copy.Add(seed);
        }
        return new ReadOnlyCollection<int>(copy);
    }
}

internal sealed record SkillGenerationBattleSimStrengthSample(
    StringName SkillId,
    int BaselineCompletedSamples,
    int CandidateCompletedSamples,
    int CandidateAttemptCount,
    int CandidateSuccessCount,
    int BaselineAllyWinRateBasisPoints,
    int CandidateAllyWinRateBasisPoints,
    int WinRateDeltaBasisPoints,
    int BaselineDamagePerCompletedRunBasisPoints,
    int CandidateDamagePerCompletedRunBasisPoints,
    int DamageRatioBasisPoints,
    string BaselineReportPath,
    string CandidateReportPath
);

internal static class SkillGenerationBattleSimStrengthAnalyzer
{
    internal static IReadOnlyList<ContentJsonDiagnostic> Evaluate(
        SkillGenerationBattleSimStrengthSample sample,
        SkillGenerationBattleSimOptions options,
        JsonContentEntryContext context
    )
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);
        var diagnostics = new List<ContentJsonDiagnostic>();
        if (
            sample.BaselineCompletedSamples < options.MinimumCompletedSamples
            || sample.CandidateCompletedSamples < options.MinimumCompletedSamples
        )
        {
            diagnostics.Add(Diagnostic(
                SkillGenerationBattleSimRules.IncompleteSamples,
                context,
                $"at least {options.MinimumCompletedSamples} completed baseline and candidate samples",
                FormatActual(sample),
                "BattleSim did not produce the minimum comparable completed sample set."
            ));
            return diagnostics.AsReadOnly();
        }
        if (sample.CandidateAttemptCount < options.MinimumCandidateAttempts)
        {
            diagnostics.Add(Diagnostic(
                SkillGenerationBattleSimRules.CandidateNotExercised,
                context,
                $"at least {options.MinimumCandidateAttempts} candidate skill attempts",
                FormatActual(sample),
                "BattleSim AI did not exercise the candidate often enough to measure strength."
            ));
            return diagnostics.AsReadOnly();
        }

        bool high = sample.WinRateDeltaBasisPoints
                > options.MaximumWinRateDeltaBasisPoints
            || sample.DamageRatioBasisPoints
                > options.MaximumDamageRatioBasisPoints;
        bool low = sample.WinRateDeltaBasisPoints
                < options.MinimumWinRateDeltaBasisPoints
            || sample.DamageRatioBasisPoints
                < options.MinimumDamageRatioBasisPoints;
        if (high)
        {
            diagnostics.Add(Diagnostic(
                SkillGenerationBattleSimRules.HighStrengthOutlier,
                context,
                $"win_rate_delta_bp <= {options.MaximumWinRateDeltaBasisPoints} and damage_ratio_bp <= {options.MaximumDamageRatioBasisPoints}",
                FormatActual(sample),
                "Candidate strength is above the configured BattleSim outlier envelope."
            ));
        }
        else if (low)
        {
            diagnostics.Add(Diagnostic(
                SkillGenerationBattleSimRules.LowStrengthOutlier,
                context,
                $"win_rate_delta_bp >= {options.MinimumWinRateDeltaBasisPoints} and damage_ratio_bp >= {options.MinimumDamageRatioBasisPoints}",
                FormatActual(sample),
                "Candidate strength is below the configured BattleSim outlier envelope."
            ));
        }
        return diagnostics.AsReadOnly();
    }

    private static string FormatActual(SkillGenerationBattleSimStrengthSample sample) =>
        $"completed={sample.BaselineCompletedSamples}/{sample.CandidateCompletedSamples};"
        + $"attempts={sample.CandidateAttemptCount};successes={sample.CandidateSuccessCount};"
        + $"win_rate_delta_bp={sample.WinRateDeltaBasisPoints};"
        + $"damage_ratio_bp={sample.DamageRatioBasisPoints}";

    private static ContentJsonDiagnostic Diagnostic(
        string ruleId,
        JsonContentEntryContext context,
        string expected,
        string actual,
        string message
    ) => new(
        ruleId,
        message,
        context.SourceLabel,
        context.JsonPointer + "/combat_profile",
        expected,
        actual
    );
}
