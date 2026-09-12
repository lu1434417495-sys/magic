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

internal readonly record struct PhantasmalKillExecutionProfile(
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

internal static class PhantasmalKillExecutionRules
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
        out PhantasmalKillExecutionProfile profile,
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
        if (
            effectDefinition.Payload
            is not GradedSaveExecuteEffectPayloadDefinition payload
        )
        {
            error = "graded_save_execute payload is required.";
            return false;
        }
        if (payload.ProfileId != PhantasmalKillProfileId)
        {
            error = "payload.profile_id must be phantasmal_kill.";
            return false;
        }
        if (payload.FailureExecuteThresholdFixed < 0)
        {
            error = "payload.failure_execute_threshold_fixed must be a non-negative integer.";
            return false;
        }
        if (!IsPercent(payload.FailureExecuteThresholdMaxHpPercent))
        {
            error = "payload.failure_execute_threshold_max_hp_percent must be 1..100.";
            return false;
        }
        if (payload.FailureDamageDiceCount <= 0)
        {
            error = "payload.failure_damage_dice_count must be positive.";
            return false;
        }
        if (payload.FailureDamageDiceSides <= 0)
        {
            error = "payload.failure_damage_dice_sides must be positive.";
            return false;
        }
        if (payload.FailureFrightenedDurationTu <= 0)
        {
            error = "payload.failure_frightened_duration_tu must be positive.";
            return false;
        }
        if (payload.FailureReactionLockDurationTu <= 0)
        {
            error = "payload.failure_reaction_lock_duration_tu must be positive.";
            return false;
        }
        if (!IsPercent(payload.CriticalFailureExecuteThresholdMaxHpPercent))
        {
            error = "payload.critical_failure_execute_threshold_max_hp_percent must be 1..100.";
            return false;
        }
        if (payload.CriticalFailureDamageDiceCount <= 0)
        {
            error = "payload.critical_failure_damage_dice_count must be positive.";
            return false;
        }
        if (payload.CriticalFailureDamageDiceSides <= 0)
        {
            error = "payload.critical_failure_damage_dice_sides must be positive.";
            return false;
        }
        if (payload.CriticalFailureFrightenedDurationTu <= 0)
        {
            error = "payload.critical_failure_frightened_duration_tu must be positive.";
            return false;
        }
        if (payload.CriticalFailureStunnedDurationTu <= 0)
        {
            error = "payload.critical_failure_stunned_duration_tu must be positive.";
            return false;
        }
        if (payload.SuccessAftershockDurationTu <= 0)
        {
            error = "payload.success_aftershock_duration_tu must be positive.";
            return false;
        }

        profile = new PhantasmalKillExecutionProfile(
            payload.ProfileId,
            payload.FailureExecuteThresholdFixed,
            payload.FailureExecuteThresholdMaxHpPercent,
            payload.FailureDamageDiceCount,
            payload.FailureDamageDiceSides,
            payload.FailureFrightenedDurationTu,
            payload.FailureReactionLockDurationTu,
            payload.CriticalFailureExecuteThresholdMaxHpPercent,
            payload.CriticalFailureDamageDiceCount,
            payload.CriticalFailureDamageDiceSides,
            payload.CriticalFailureFrightenedDurationTu,
            payload.CriticalFailureStunnedDurationTu,
            payload.SuccessAftershockDurationTu
        );
        return true;
    }

    private static bool IsPercent(int value) => value >= 1 && value <= 100;

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
        PhantasmalKillExecutionProfile profile,
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
        PhantasmalKillExecutionProfile profile,
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
