using System;
using System.Collections.Generic;
using Godot;

internal enum EnemyAiActionSkillCompatibilityFailureKind
{
    None = 0,
    MissingActiveCombatProfile,
    TargetModeMismatch,
    TargetSelectionModeMismatch,
    SpecialResolutionRouteMismatch,
    MissingCompatibleCastOption,
    ActionCapacityMismatch,
}

internal readonly struct EnemyAiActionSkillCompatibilityResult
{
    internal EnemyAiActionSkillCompatibilityResult(
        EnemyAiActionSkillCompatibilityFailureKind failureKind,
        string reason
    )
    {
        FailureKind = failureKind;
        Reason = reason ?? "";
    }

    internal EnemyAiActionSkillCompatibilityFailureKind FailureKind { get; }
    internal string Reason { get; }
    internal bool IsCompatible =>
        FailureKind == EnemyAiActionSkillCompatibilityFailureKind.None;

    internal static EnemyAiActionSkillCompatibilityResult Compatible() => new(
        EnemyAiActionSkillCompatibilityFailureKind.None,
        ""
    );
}

internal static class EnemyAiActionSkillCompatibilityRules
{
    private static readonly StringName MeteorSwarmProfileId = "meteor_swarm";

    internal static EnemyAiActionSkillCompatibilityResult Evaluate(
        EnemyAiActionKind actionKind,
        SkillDefinition skillDefinition,
        int candidatePoolLimit = int.MaxValue,
        int? skillLevel = null
    )
    {
        if (!RequiresCastableSkill(actionKind))
            return EnemyAiActionSkillCompatibilityResult.Compatible();

        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (
            skillDefinition == null
            || skillDefinition.SkillTypeKind != SkillTypeKind.Active
            || combatProfile == null
        )
        {
            return Fail(
                EnemyAiActionSkillCompatibilityFailureKind.MissingActiveCombatProfile,
                "expected an active skill with a combat profile"
            );
        }
        if (
            combatProfile.SpecialResolutionProfileId == MeteorSwarmProfileId
            && actionKind != EnemyAiActionKind.UseGroundSkill
        )
        {
            return Fail(
                EnemyAiActionSkillCompatibilityFailureKind.SpecialResolutionRouteMismatch,
                "meteor_swarm requires UseGroundSkillAction because its formal preview "
                    + "and execution consume ground target coordinates"
            );
        }
        if (
            actionKind != EnemyAiActionKind.UseRandomChainSkill
            && combatProfile.TargetSelectionModeKind
                == BattleTargetSelectionMode.RandomChain
        )
        {
            return Fail(
                EnemyAiActionSkillCompatibilityFailureKind.TargetSelectionModeMismatch,
                "expected a non-random-chain target_selection_mode; "
                    + "random_chain requires UseRandomChainSkillAction"
            );
        }

        return actionKind switch
        {
            EnemyAiActionKind.UseRandomChainSkill =>
                EvaluateRandomChainSkill(skillDefinition, skillLevel),
            EnemyAiActionKind.UseMultiUnitSkill
                or EnemyAiActionKind.MoveToMultiUnitSkillPosition =>
                EvaluateMultiUnitSkill(skillDefinition, candidatePoolLimit, skillLevel),
            EnemyAiActionKind.UseChargePathAoe =>
                EvaluateChargePathAoeSkill(combatProfile, skillLevel),
            EnemyAiActionKind.UseCharge => EvaluateChargeSkill(combatProfile, skillLevel),
            EnemyAiActionKind.UseGroundRepositionSkill =>
                EvaluateGroundRepositionSkill(combatProfile, skillLevel),
            EnemyAiActionKind.UseUnitSkill =>
                EvaluateUnitSkill(skillDefinition, skillLevel),
            EnemyAiActionKind.UseGroundSkill =>
                EvaluateGroundSkill(combatProfile, skillLevel),
            _ => EnemyAiActionSkillCompatibilityResult.Compatible(),
        };
    }

    private static bool RequiresCastableSkill(EnemyAiActionKind actionKind) =>
        actionKind
            is EnemyAiActionKind.UseUnitSkill
                or EnemyAiActionKind.UseGroundSkill
                or EnemyAiActionKind.UseRandomChainSkill
                or EnemyAiActionKind.UseMultiUnitSkill
                or EnemyAiActionKind.MoveToMultiUnitSkillPosition
                or EnemyAiActionKind.UseCharge
                or EnemyAiActionKind.UseChargePathAoe
                or EnemyAiActionKind.UseGroundRepositionSkill;

    private static EnemyAiActionSkillCompatibilityResult EvaluateUnitSkill(
        SkillDefinition skillDefinition,
        int? skillLevel
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition.CombatProfile;
        EnemyAiActionSkillCompatibilityResult targetModeResult = RequireTargetMode(
            combatProfile,
            BattleTargetMode.Unit
        );
        if (!targetModeResult.IsCompatible)
            return targetModeResult;
        if (
            combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.MultiUnit
            && Math.Max(combatProfile.MinTargetCount, 1) > 1
        )
        {
            return Fail(
                EnemyAiActionSkillCompatibilityFailureKind.ActionCapacityMismatch,
                "expected a skill requiring at most one unit target; "
                    + $"multi_unit requires {Math.Max(combatProfile.MinTargetCount, 1)}"
            );
        }
        return HasUnitExecutionOption(
            skillDefinition,
            BattleTargetMode.Unit,
            skillLevel
        )
            ? EnemyAiActionSkillCompatibilityResult.Compatible()
            : MissingOption(
                FormatLevelSpecificReason(
                    "expected at least one unit-target cast option supported by the unit execution pipeline",
                    skillLevel
                )
            );
    }

    private static EnemyAiActionSkillCompatibilityResult EvaluateGroundSkill(
        CombatSkillDefinition combatProfile,
        int? skillLevel
    )
    {
        EnemyAiActionSkillCompatibilityResult targetModeResult = RequireTargetMode(
            combatProfile,
            BattleTargetMode.Ground
        );
        if (!targetModeResult.IsCompatible)
            return targetModeResult;
        return HasNonChargeGroundOption(combatProfile, skillLevel)
            ? EnemyAiActionSkillCompatibilityResult.Compatible()
            : MissingOption(
                FormatLevelSpecificReason(
                    "expected at least one non-charge ground-target cast option",
                    skillLevel
                )
            );
    }

    private static EnemyAiActionSkillCompatibilityResult EvaluateRandomChainSkill(
        SkillDefinition skillDefinition,
        int? skillLevel
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition.CombatProfile;
        EnemyAiActionSkillCompatibilityResult targetModeResult = RequireTargetMode(
            combatProfile,
            BattleTargetMode.Unit
        );
        if (!targetModeResult.IsCompatible)
            return targetModeResult;
        EnemyAiActionSkillCompatibilityResult selectionResult = RequireTargetSelectionMode(
            combatProfile,
            BattleTargetSelectionMode.RandomChain
        );
        if (!selectionResult.IsCompatible)
            return selectionResult;
        return HasUnitExecutionOption(
            skillDefinition,
            BattleTargetMode.Unit,
            skillLevel
        )
            ? EnemyAiActionSkillCompatibilityResult.Compatible()
            : MissingOption(
                FormatLevelSpecificReason(
                    "expected at least one unit-target random-chain cast option supported by the unit execution pipeline",
                    skillLevel
                )
            );
    }

    private static EnemyAiActionSkillCompatibilityResult EvaluateMultiUnitSkill(
        SkillDefinition skillDefinition,
        int candidatePoolLimit,
        int? skillLevel
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition.CombatProfile;
        EnemyAiActionSkillCompatibilityResult selectionResult = RequireTargetSelectionMode(
            combatProfile,
            BattleTargetSelectionMode.MultiUnit
        );
        if (!selectionResult.IsCompatible)
            return selectionResult;
        int minimumTargetCount = Math.Max(combatProfile.MinTargetCount, 1);
        if (candidatePoolLimit < minimumTargetCount)
        {
            return Fail(
                EnemyAiActionSkillCompatibilityFailureKind.ActionCapacityMismatch,
                $"expected candidate_pool_limit >= required target count {minimumTargetCount} "
                    + $"but found {candidatePoolLimit}"
            );
        }
        return HasNonChargeMultiUnitOption(skillDefinition, skillLevel)
            ? EnemyAiActionSkillCompatibilityResult.Compatible()
            : MissingOption(
                FormatLevelSpecificReason(
                    "expected at least one non-charge multi-unit cast option matching target_mode "
                        + TargetModeLabel(combatProfile.TargetModeKind)
                        + " and supported by the unit execution pipeline",
                    skillLevel
                )
            );
    }

    private static EnemyAiActionSkillCompatibilityResult EvaluateChargeSkill(
        CombatSkillDefinition combatProfile,
        int? skillLevel
    )
    {
        EnemyAiActionSkillCompatibilityResult targetModeResult = RequireTargetMode(
            combatProfile,
            BattleTargetMode.Ground
        );
        if (!targetModeResult.IsCompatible)
            return targetModeResult;
        return HasGroundOptionWithEffects(
            combatProfile,
            skillLevel,
            BattleEffectKind.Charge
        )
            ? EnemyAiActionSkillCompatibilityResult.Compatible()
            : MissingOption(
                FormatLevelSpecificReason(
                    "expected a single-coordinate ground cast option containing a charge effect",
                    skillLevel
                )
            );
    }

    private static EnemyAiActionSkillCompatibilityResult EvaluateChargePathAoeSkill(
        CombatSkillDefinition combatProfile,
        int? skillLevel
    )
    {
        EnemyAiActionSkillCompatibilityResult targetModeResult = RequireTargetMode(
            combatProfile,
            BattleTargetMode.Ground
        );
        if (!targetModeResult.IsCompatible)
            return targetModeResult;
        return HasGroundOptionWithEffects(
            combatProfile,
            skillLevel,
            BattleEffectKind.Charge,
            BattleEffectKind.PathStepAoe
        )
            ? EnemyAiActionSkillCompatibilityResult.Compatible()
            : MissingOption(
                FormatLevelSpecificReason(
                    "expected one single-coordinate ground cast option containing both charge and path_step_aoe effects",
                    skillLevel
                )
            );
    }

    private static EnemyAiActionSkillCompatibilityResult EvaluateGroundRepositionSkill(
        CombatSkillDefinition combatProfile,
        int? skillLevel
    )
    {
        EnemyAiActionSkillCompatibilityResult targetModeResult = RequireTargetMode(
            combatProfile,
            BattleTargetMode.Ground
        );
        if (!targetModeResult.IsCompatible)
            return targetModeResult;
        return HasGroundRepositionOption(combatProfile, skillLevel)
            ? EnemyAiActionSkillCompatibilityResult.Compatible()
            : MissingOption(
                FormatLevelSpecificReason(
                    "expected a non-charge single-coordinate ground cast option containing a blink or jump forced_move effect",
                    skillLevel
                )
            );
    }

    private static EnemyAiActionSkillCompatibilityResult RequireTargetMode(
        CombatSkillDefinition combatProfile,
        BattleTargetMode expected
    )
    {
        BattleTargetMode actual = combatProfile?.TargetModeKind ?? BattleTargetMode.Unknown;
        return actual == expected
            ? EnemyAiActionSkillCompatibilityResult.Compatible()
            : Fail(
                EnemyAiActionSkillCompatibilityFailureKind.TargetModeMismatch,
                $"expected target_mode {TargetModeLabel(expected)} but found {TargetModeLabel(actual)}"
            );
    }

    private static EnemyAiActionSkillCompatibilityResult RequireTargetSelectionMode(
        CombatSkillDefinition combatProfile,
        BattleTargetSelectionMode expected
    )
    {
        BattleTargetSelectionMode actual =
            combatProfile?.TargetSelectionModeKind ?? BattleTargetSelectionMode.Unknown;
        return actual == expected
            ? EnemyAiActionSkillCompatibilityResult.Compatible()
            : Fail(
                EnemyAiActionSkillCompatibilityFailureKind.TargetSelectionModeMismatch,
                "expected target_selection_mode "
                    + $"{TargetSelectionModeLabel(expected)} but found "
                    + TargetSelectionModeLabel(actual)
            );
    }

    private static bool HasUnitExecutionOption(
        SkillDefinition skillDefinition,
        BattleTargetMode expectedTargetMode,
        int? skillLevel
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
            return false;
        if (combatProfile.CastVariants.Count == 0)
        {
            return combatProfile.TargetModeKind == expectedTargetMode
                && (
                    !skillLevel.HasValue
                    || CanUnitExecutionPipelineHandleEffectiveEffects(
                        combatProfile,
                        null,
                        skillLevel.Value
                    )
                );
        }
        foreach (CombatCastVariantDefinition castVariant in combatProfile.CastVariants)
        {
            if (
                castVariant != null
                && ResolveCastOptionTargetMode(combatProfile, castVariant)
                    == expectedTargetMode
                && (
                    !skillLevel.HasValue
                    || (
                        IsCastVariantUnlocked(castVariant, skillLevel.Value)
                        && CanUnitExecutionPipelineHandleEffectiveEffects(
                            combatProfile,
                            castVariant,
                            skillLevel.Value
                        )
                    )
                )
            )
                return true;
        }
        return false;
    }

    private static bool HasNonChargeGroundOption(
        CombatSkillDefinition combatProfile,
        int? skillLevel
    )
    {
        if (combatProfile.CastVariants.Count == 0)
        {
            return combatProfile.TargetModeKind == BattleTargetMode.Ground
                && !HasEffect(combatProfile.EffectDefinitions, BattleEffectKind.Charge);
        }
        foreach (CombatCastVariantDefinition castVariant in combatProfile.CastVariants)
        {
            if (
                castVariant != null
                && (
                    !skillLevel.HasValue
                    || IsCastVariantUnlocked(castVariant, skillLevel.Value)
                )
                && ResolveCastOptionTargetMode(combatProfile, castVariant)
                    == BattleTargetMode.Ground
                && !HasEffect(castVariant.EffectDefinitions, BattleEffectKind.Charge)
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasNonChargeMultiUnitOption(
        SkillDefinition skillDefinition,
        int? skillLevel
    )
    {
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
            return false;
        if (combatProfile.CastVariants.Count == 0)
        {
            return !HasEffectiveEffect(
                    combatProfile,
                    null,
                    BattleEffectKind.Charge
                )
                && (
                    !skillLevel.HasValue
                    || CanUnitExecutionPipelineHandleEffectiveEffects(
                        combatProfile,
                        null,
                        skillLevel.Value
                    )
                );
        }
        foreach (CombatCastVariantDefinition castVariant in combatProfile.CastVariants)
        {
            if (
                castVariant != null
                && ResolveCastOptionTargetMode(combatProfile, castVariant)
                    == combatProfile.TargetModeKind
                && !HasEffectiveEffect(
                    combatProfile,
                    castVariant,
                    BattleEffectKind.Charge
                )
                && (
                    !skillLevel.HasValue
                    || (
                        IsCastVariantUnlocked(castVariant, skillLevel.Value)
                        && CanUnitExecutionPipelineHandleEffectiveEffects(
                            combatProfile,
                            castVariant,
                            skillLevel.Value
                        )
                    )
                )
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool CanUnitExecutionPipelineHandleEffectiveEffects(
        CombatSkillDefinition combatProfile,
        CombatCastVariantDefinition castVariant,
        int skillLevel
    ) =>
        BattleUnitSkillDefinitionExecutionRules.CanApplyUnitSkillOrRepeatResult(
            EnumerateEffectiveEffects(
                combatProfile,
                castVariant,
                skillLevel
            )
        );

    private static bool HasEffectiveEffect(
        CombatSkillDefinition combatProfile,
        CombatCastVariantDefinition castVariant,
        BattleEffectKind expected
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in EnumerateEffectiveEffects(
                combatProfile,
                castVariant
            )
        )
        {
            if (effectDefinition?.EffectKind == expected)
                return true;
        }
        return false;
    }

    private static IEnumerable<CombatEffectDefinition> EnumerateEffectiveEffects(
        CombatSkillDefinition combatProfile,
        CombatCastVariantDefinition castVariant,
        int? skillLevel = null
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in combatProfile?.EffectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                !skillLevel.HasValue
                || IsEffectUnlocked(effectDefinition, skillLevel.Value)
            )
            {
                yield return effectDefinition;
            }
        }
        foreach (
            CombatEffectDefinition effectDefinition in castVariant?.EffectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                !skillLevel.HasValue
                || IsEffectUnlocked(effectDefinition, skillLevel.Value)
            )
            {
                yield return effectDefinition;
            }
        }
    }

    private static bool IsCastVariantUnlocked(
        CombatCastVariantDefinition castVariant,
        int skillLevel
    ) => castVariant != null && skillLevel >= Math.Max(castVariant.MinSkillLevel, 0);

    private static bool IsEffectUnlocked(
        CombatEffectDefinition effectDefinition,
        int skillLevel
    )
    {
        if (effectDefinition == null)
            return false;
        int minLevel = Math.Max(effectDefinition.MinSkillLevel, 0);
        int maxLevel = effectDefinition.MaxSkillLevel;
        return skillLevel >= minLevel && (maxLevel < 0 || skillLevel <= maxLevel);
    }

    private static BattleTargetMode ResolveCastOptionTargetMode(
        CombatSkillDefinition combatProfile,
        CombatCastVariantDefinition castVariant
    )
    {
        BattleTargetMode targetMode =
            castVariant?.TargetModeKind ?? BattleTargetMode.Unknown;
        return targetMode != BattleTargetMode.Unknown
            ? targetMode
            : combatProfile?.TargetModeKind ?? BattleTargetMode.Unknown;
    }

    private static bool HasGroundOptionWithEffects(
        CombatSkillDefinition combatProfile,
        int? skillLevel,
        params BattleEffectKind[] requiredEffects
    )
    {
        if (combatProfile.CastVariants.Count == 0)
        {
            return combatProfile.TargetModeKind == BattleTargetMode.Ground
                && HasAllEffects(combatProfile.EffectDefinitions, requiredEffects);
        }
        foreach (CombatCastVariantDefinition castVariant in combatProfile.CastVariants)
        {
            if (
                castVariant != null
                && (
                    !skillLevel.HasValue
                    || IsCastVariantUnlocked(castVariant, skillLevel.Value)
                )
                && ResolveCastOptionTargetMode(combatProfile, castVariant)
                    == BattleTargetMode.Ground
                && SupportsSingleCoordinateCommand(castVariant)
                && HasAllEffects(castVariant.EffectDefinitions, requiredEffects)
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasGroundRepositionOption(
        CombatSkillDefinition combatProfile,
        int? skillLevel
    )
    {
        if (combatProfile.CastVariants.Count == 0)
        {
            return combatProfile.TargetModeKind == BattleTargetMode.Ground
                && !HasEffect(combatProfile.EffectDefinitions, BattleEffectKind.Charge)
                && HasBlinkOrJumpEffect(combatProfile.EffectDefinitions);
        }
        foreach (CombatCastVariantDefinition castVariant in combatProfile.CastVariants)
        {
            if (
                castVariant != null
                && (
                    !skillLevel.HasValue
                    || IsCastVariantUnlocked(castVariant, skillLevel.Value)
                )
                && ResolveCastOptionTargetMode(combatProfile, castVariant)
                    == BattleTargetMode.Ground
                && SupportsSingleCoordinateCommand(castVariant)
                && !HasEffect(castVariant.EffectDefinitions, BattleEffectKind.Charge)
                && HasBlinkOrJumpEffect(castVariant.EffectDefinitions)
            )
            {
                return true;
            }
        }
        return false;
    }

    private static bool SupportsSingleCoordinateCommand(
        CombatCastVariantDefinition castVariant
    ) =>
        castVariant != null
        && castVariant.RequiredCoordCount == 1
        && castVariant.FootprintPatternKind
            is CombatCastFootprintPattern.Single
                or CombatCastFootprintPattern.Unordered;

    private static bool HasAllEffects(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyList<BattleEffectKind> requiredEffects
    )
    {
        foreach (BattleEffectKind requiredEffect in requiredEffects)
        {
            if (!HasEffect(effectDefinitions, requiredEffect))
                return false;
        }
        return true;
    }

    private static bool HasEffect(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        BattleEffectKind expected
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition?.EffectKind == expected)
                return true;
        }
        return false;
    }

    private static bool HasBlinkOrJumpEffect(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effectDefinition?.EffectKind == BattleEffectKind.ForcedMove
                && effectDefinition.ForcedMoveModeKind
                    is BattleForcedMoveMode.Blink or BattleForcedMoveMode.Jump
            )
            {
                return true;
            }
        }
        return false;
    }

    private static EnemyAiActionSkillCompatibilityResult MissingOption(string reason) =>
        Fail(EnemyAiActionSkillCompatibilityFailureKind.MissingCompatibleCastOption, reason);

    private static string FormatLevelSpecificReason(string reason, int? skillLevel) =>
        skillLevel.HasValue ? $"{reason} at skill level {skillLevel.Value}" : reason;

    private static EnemyAiActionSkillCompatibilityResult Fail(
        EnemyAiActionSkillCompatibilityFailureKind failureKind,
        string reason
    ) => new(failureKind, reason);

    private static string TargetModeLabel(BattleTargetMode targetMode)
    {
        string value = BattleTypedNames.ToStringName(targetMode).ToString();
        return string.IsNullOrEmpty(value) ? "unknown" : value;
    }

    private static string TargetSelectionModeLabel(BattleTargetSelectionMode selectionMode)
    {
        string value = BattleTypedNames.ToStringName(selectionMode).ToString();
        return string.IsNullOrEmpty(value) ? "unknown" : value;
    }
}
