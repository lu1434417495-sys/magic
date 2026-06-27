using System;
using System.Collections.Generic;
using Godot;

internal enum GradedSaveExecutionGrade
{
    Immune,
    CriticalSuccess,
    Success,
    Failure,
    CriticalFailure,
}

internal readonly record struct BattleGradedSaveExecutionProfile(
    StringName ProfileId,
    int FailureExecuteThresholdFixed,
    int FailureExecuteThresholdMaxHpPercent,
    int FailureDamageDiceCount,
    int FailureDamageDiceSides,
    int FailureFrightenedDurationTu,
    int FailureReactionLockDurationTu,
    int CriticalFailureExecuteThresholdMaxHpPercent,
    int CriticalFailureDamageDiceCount,
    int CriticalFailureDamageDiceSides,
    int CriticalFailureFrightenedDurationTu,
    int CriticalFailureStunnedDurationTu,
    int SuccessAftershockDurationTu
);

internal readonly record struct BattleGradedSaveGradeDistribution(
    int ImmuneBasisPoints,
    int CriticalSuccessBasisPoints,
    int SuccessBasisPoints,
    int FailureBasisPoints,
    int CriticalFailureBasisPoints
);

internal static class BattleGradedSaveExecutionRules
{
    internal static readonly StringName PhantasmalKillProfileId = "phantasmal_kill";

    private const int BasisPointsDenominator = 10000;
    private const int D20Sides = 20;

    private static readonly StringName AdvantageStateAdvantage =
        BattleSaveContentRules.ToStringName(BattleSaveAdvantageStateKind.Advantage);
    private static readonly StringName AdvantageStateDisadvantage =
        BattleSaveContentRules.ToStringName(BattleSaveAdvantageStateKind.Disadvantage);

    internal static bool TryReadPhantasmalKillProfile(
        CombatEffectDefinition effectDefinition,
        out BattleGradedSaveExecutionProfile profile,
        out string error
    )
    {
        profile = default;
        error = "";

        if (effectDefinition == null)
        {
            error = "graded save execute effect is required.";
            return false;
        }
        if (effectDefinition.EffectKind != BattleEffectKind.GradedSaveExecute)
        {
            error = "effect_type must be graded_save_execute.";
            return false;
        }

        if (!TryReadStringNameParam(effectDefinition, "profile_id", out StringName profileId))
        {
            error = "params.profile_id is required.";
            return false;
        }
        if (profileId != PhantasmalKillProfileId)
        {
            error = "params.profile_id must be phantasmal_kill.";
            return false;
        }

        if (
            !TryReadIntParam(
                effectDefinition,
                "failure_execute_threshold_fixed",
                out int failureExecuteThresholdFixed
            )
            || failureExecuteThresholdFixed < 0
        )
        {
            error = "params.failure_execute_threshold_fixed must be a non-negative integer.";
            return false;
        }
        if (
            !TryReadPercentParam(
                effectDefinition,
                "failure_execute_threshold_max_hp_percent",
                out int failureExecuteThresholdMaxHpPercent
            )
        )
        {
            error = "params.failure_execute_threshold_max_hp_percent must be 1..100.";
            return false;
        }
        if (
            !TryReadPositiveIntParam(
                effectDefinition,
                "failure_damage_dice_count",
                out int failureDamageDiceCount
            )
        )
        {
            error = "params.failure_damage_dice_count must be positive.";
            return false;
        }
        if (
            !TryReadPositiveIntParam(
                effectDefinition,
                "failure_damage_dice_sides",
                out int failureDamageDiceSides
            )
        )
        {
            error = "params.failure_damage_dice_sides must be positive.";
            return false;
        }
        if (
            !TryReadPositiveIntParam(
                effectDefinition,
                "failure_frightened_duration_tu",
                out int failureFrightenedDurationTu
            )
        )
        {
            error = "params.failure_frightened_duration_tu must be positive.";
            return false;
        }
        if (
            !TryReadPositiveIntParam(
                effectDefinition,
                "failure_reaction_lock_duration_tu",
                out int failureReactionLockDurationTu
            )
        )
        {
            error = "params.failure_reaction_lock_duration_tu must be positive.";
            return false;
        }
        if (
            !TryReadPercentParam(
                effectDefinition,
                "critical_failure_execute_threshold_max_hp_percent",
                out int criticalFailureExecuteThresholdMaxHpPercent
            )
        )
        {
            error = "params.critical_failure_execute_threshold_max_hp_percent must be 1..100.";
            return false;
        }
        if (
            !TryReadPositiveIntParam(
                effectDefinition,
                "critical_failure_damage_dice_count",
                out int criticalFailureDamageDiceCount
            )
        )
        {
            error = "params.critical_failure_damage_dice_count must be positive.";
            return false;
        }
        if (
            !TryReadPositiveIntParam(
                effectDefinition,
                "critical_failure_damage_dice_sides",
                out int criticalFailureDamageDiceSides
            )
        )
        {
            error = "params.critical_failure_damage_dice_sides must be positive.";
            return false;
        }
        if (
            !TryReadPositiveIntParam(
                effectDefinition,
                "critical_failure_frightened_duration_tu",
                out int criticalFailureFrightenedDurationTu
            )
        )
        {
            error = "params.critical_failure_frightened_duration_tu must be positive.";
            return false;
        }
        if (
            !TryReadPositiveIntParam(
                effectDefinition,
                "critical_failure_stunned_duration_tu",
                out int criticalFailureStunnedDurationTu
            )
        )
        {
            error = "params.critical_failure_stunned_duration_tu must be positive.";
            return false;
        }
        if (
            !TryReadPositiveIntParam(
                effectDefinition,
                "success_aftershock_duration_tu",
                out int successAftershockDurationTu
            )
        )
        {
            error = "params.success_aftershock_duration_tu must be positive.";
            return false;
        }

        profile = new BattleGradedSaveExecutionProfile(
            profileId,
            failureExecuteThresholdFixed,
            failureExecuteThresholdMaxHpPercent,
            failureDamageDiceCount,
            failureDamageDiceSides,
            failureFrightenedDurationTu,
            failureReactionLockDurationTu,
            criticalFailureExecuteThresholdMaxHpPercent,
            criticalFailureDamageDiceCount,
            criticalFailureDamageDiceSides,
            criticalFailureFrightenedDurationTu,
            criticalFailureStunnedDurationTu,
            successAftershockDurationTu
        );
        return true;
    }

    internal static GradedSaveExecutionGrade ResolveGrade(BattleSaveResult saveResult)
    {
        if (saveResult.Immune)
        {
            return GradedSaveExecutionGrade.Immune;
        }
        return saveResult.Degree switch
        {
            BattleSaveDegreeKind.CriticalSuccess => GradedSaveExecutionGrade.CriticalSuccess,
            BattleSaveDegreeKind.Success => GradedSaveExecutionGrade.Success,
            BattleSaveDegreeKind.Failure => GradedSaveExecutionGrade.Failure,
            BattleSaveDegreeKind.CriticalFailure => GradedSaveExecutionGrade.CriticalFailure,
            _ => GradedSaveExecutionGrade.Failure,
        };
    }

    internal static int ResolveFailureExecuteThreshold(
        BattleGradedSaveExecutionProfile profile,
        int targetMaxHp
    )
    {
        int fixedThreshold = Math.Max(profile.FailureExecuteThresholdFixed, 0);
        int percentThreshold = ResolvePercentThreshold(
            targetMaxHp,
            profile.FailureExecuteThresholdMaxHpPercent
        );
        return Math.Max(fixedThreshold, percentThreshold);
    }

    internal static int ResolveCriticalFailureExecuteThreshold(
        BattleGradedSaveExecutionProfile profile,
        int targetMaxHp
    )
    {
        return ResolvePercentThreshold(
            targetMaxHp,
            profile.CriticalFailureExecuteThresholdMaxHpPercent
        );
    }

    internal static int EstimateAverageDiceDamage(int diceCount, int diceSides)
    {
        if (diceCount <= 0 || diceSides <= 0)
        {
            return 0;
        }
        return (diceCount * (diceSides + 1) + 1) / 2;
    }

    internal static BattleGradedSaveGradeDistribution EstimateGradeDistribution(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition,
        BattleSaveContext context = default
    )
    {
        BattleSaveProbabilityResult probability =
            BattleSaveResolver.EstimateSaveSuccessProbabilityResult(
                sourceUnit,
                targetUnit,
                effectDefinition,
                context
            );
        return EstimateGradeDistribution(probability, context);
    }

    internal static BattleGradedSaveGradeDistribution EstimateGradeDistribution(
        BattleSaveProbabilityResult probability,
        BattleSaveContext context = default
    )
    {
        if (!probability.HasSave)
        {
            return default;
        }
        if (probability.Immune)
        {
            return new BattleGradedSaveGradeDistribution(
                BasisPointsDenominator,
                0,
                0,
                0,
                0
            );
        }

        GradeCountAccumulator counts = new();
        IReadOnlyList<int> rollOverrides = context.SaveRollOverrides;
        if (rollOverrides.Count > 0)
        {
            AddSelectedRoll(
                counts,
                SelectSaveRoll(probability.AdvantageState, rollOverrides),
                probability
            );
            return BuildDistribution(counts, totalCount: 1);
        }

        if (probability.AdvantageState == AdvantageStateAdvantage)
        {
            for (int firstRoll = 1; firstRoll <= D20Sides; firstRoll++)
            {
                for (int secondRoll = 1; secondRoll <= D20Sides; secondRoll++)
                {
                    AddSelectedRoll(counts, Math.Max(firstRoll, secondRoll), probability);
                }
            }
            return BuildDistribution(counts, D20Sides * D20Sides);
        }

        if (probability.AdvantageState == AdvantageStateDisadvantage)
        {
            for (int firstRoll = 1; firstRoll <= D20Sides; firstRoll++)
            {
                for (int secondRoll = 1; secondRoll <= D20Sides; secondRoll++)
                {
                    AddSelectedRoll(counts, Math.Min(firstRoll, secondRoll), probability);
                }
            }
            return BuildDistribution(counts, D20Sides * D20Sides);
        }

        for (int naturalRoll = 1; naturalRoll <= D20Sides; naturalRoll++)
        {
            AddSelectedRoll(counts, naturalRoll, probability);
        }
        return BuildDistribution(counts, D20Sides);
    }

    private static int ResolvePercentThreshold(int targetMaxHp, int percent)
    {
        return Math.Max(targetMaxHp, 0) * Math.Clamp(percent, 0, 100) / 100;
    }

    private static void AddSelectedRoll(
        GradeCountAccumulator counts,
        int naturalRoll,
        BattleSaveProbabilityResult probability
    )
    {
        GradedSaveExecutionGrade grade = ResolveNaturalRollGrade(naturalRoll, probability);
        counts.Add(grade);
    }

    private static GradedSaveExecutionGrade ResolveNaturalRollGrade(
        int naturalRoll,
        BattleSaveProbabilityResult probability
    )
    {
        int clampedNaturalRoll = Math.Clamp(naturalRoll, 1, D20Sides);
        int rollTotal = clampedNaturalRoll + probability.AbilityModifier + probability.Bonus;
        BattleSaveDegreeKind degree = BattleSaveResolver.ResolveSaveDegree(
            clampedNaturalRoll,
            rollTotal,
            probability.Dc
        );
        return ResolveGrade(
            new BattleSaveResult(
                HasSave: true,
                Immune: false,
                Success: degree
                    is BattleSaveDegreeKind.Success or BattleSaveDegreeKind.CriticalSuccess,
                NaturalRoll: clampedNaturalRoll,
                RollTotal: rollTotal,
                Dc: probability.Dc,
                Ability: probability.Ability,
                SaveTag: probability.SaveTag,
                AdvantageState: probability.AdvantageState,
                AbilityValue: probability.AbilityValue,
                AbilityModifier: probability.AbilityModifier,
                Bonus: probability.Bonus,
                Sources: probability.Sources ?? Array.Empty<BattleSaveSource>()
            )
            {
                Degree = degree,
            }
        );
    }

    private static int SelectSaveRoll(StringName advantageState, IReadOnlyList<int> rolls)
    {
        if (rolls == null || rolls.Count <= 0)
        {
            return 1;
        }
        if (advantageState == AdvantageStateAdvantage && rolls.Count >= 2)
        {
            return Math.Max(
                Math.Clamp(rolls[0], 1, D20Sides),
                Math.Clamp(rolls[1], 1, D20Sides)
            );
        }
        if (advantageState == AdvantageStateDisadvantage && rolls.Count >= 2)
        {
            return Math.Min(
                Math.Clamp(rolls[0], 1, D20Sides),
                Math.Clamp(rolls[1], 1, D20Sides)
            );
        }
        return Math.Clamp(rolls[0], 1, D20Sides);
    }

    private static BattleGradedSaveGradeDistribution BuildDistribution(
        GradeCountAccumulator counts,
        int totalCount
    )
    {
        if (totalCount <= 0)
        {
            return default;
        }
        return new BattleGradedSaveGradeDistribution(
            ToBasisPoints(counts.Immune, totalCount),
            ToBasisPoints(counts.CriticalSuccess, totalCount),
            ToBasisPoints(counts.Success, totalCount),
            ToBasisPoints(counts.Failure, totalCount),
            ToBasisPoints(counts.CriticalFailure, totalCount)
        );
    }

    private static int ToBasisPoints(int count, int totalCount)
    {
        return Math.Clamp(
            (int)Math.Round((double)count * BasisPointsDenominator / totalCount),
            0,
            BasisPointsDenominator
        );
    }

    private static bool TryReadStringNameParam(
        CombatEffectDefinition effectDefinition,
        string key,
        out StringName value
    )
    {
        value = "";
        if (!HasParam(effectDefinition, key))
        {
            return false;
        }
        value = ProgressionDataUtils.to_string_name(effectDefinition.Parameters[key]);
        return !IsEmpty(value);
    }

    private static bool TryReadIntParam(
        CombatEffectDefinition effectDefinition,
        string key,
        out int value
    )
    {
        value = 0;
        if (!HasParam(effectDefinition, key))
        {
            return false;
        }
        Variant rawValue = effectDefinition.Parameters[key];
        if (rawValue.VariantType != Variant.Type.Int)
        {
            return false;
        }
        value = rawValue.AsInt32();
        return true;
    }

    private static bool TryReadPositiveIntParam(
        CombatEffectDefinition effectDefinition,
        string key,
        out int value
    )
    {
        return TryReadIntParam(effectDefinition, key, out value) && value > 0;
    }

    private static bool TryReadPercentParam(
        CombatEffectDefinition effectDefinition,
        string key,
        out int value
    )
    {
        return TryReadIntParam(effectDefinition, key, out value) && value >= 1 && value <= 100;
    }

    private static bool HasParam(CombatEffectDefinition effectDefinition, string key)
    {
        return effectDefinition?.Parameters != null
            && !string.IsNullOrEmpty(key)
            && effectDefinition.Parameters.ContainsKey(key);
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private sealed class GradeCountAccumulator
    {
        public int Immune { get; private set; }
        public int CriticalSuccess { get; private set; }
        public int Success { get; private set; }
        public int Failure { get; private set; }
        public int CriticalFailure { get; private set; }

        public void Add(GradedSaveExecutionGrade grade)
        {
            switch (grade)
            {
                case GradedSaveExecutionGrade.Immune:
                    Immune++;
                    break;
                case GradedSaveExecutionGrade.CriticalSuccess:
                    CriticalSuccess++;
                    break;
                case GradedSaveExecutionGrade.Success:
                    Success++;
                    break;
                case GradedSaveExecutionGrade.CriticalFailure:
                    CriticalFailure++;
                    break;
                default:
                    Failure++;
                    break;
            }
        }
    }
}
