using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleShieldPreviewData
{
    internal bool HasShield { get; init; }
    internal int MinShieldHp { get; init; }
    internal int MaxShieldHp { get; init; }
    internal int ExpectedShieldHpBasisPoints { get; init; }
    internal int AttributeModifier { get; init; }
    internal int DurationTu { get; init; }
    internal int TargetCount { get; init; }
    internal int ExpectedBenefitingTargetCount { get; init; }
    internal int ExpectedTotalNetGainBasisPoints { get; init; }
    internal bool RollPerTarget { get; init; }
    internal StringName ShieldFamily { get; init; }

    internal string SummaryText
    {
        get
        {
            if (!HasShield)
            {
                return "";
            }
            string modifierText = AttributeModifier >= 0
                ? $"+{AttributeModifier}"
                : AttributeModifier.ToString();
            string rollText = RollPerTarget ? "；每个目标独立投骰" : "；所有目标共享投骰";
            return $"护盾 {MinShieldHp}-{MaxShieldHp}（属性调整值 {modifierText}），持续 {DurationTu}TU{rollText}；预计改善 {ExpectedBenefitingTargetCount}/{TargetCount} 个目标，预期有效护盾总量 {FormatBasisPoints(ExpectedTotalNetGainBasisPoints)}。";
        }
    }

    private static string FormatBasisPoints(int basisPoints)
    {
        double value = Math.Max(basisPoints, 0) / 10000.0;
        return value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
    }
}

internal readonly record struct BattleShieldValueRange(
    int MinHp,
    int MaxHp,
    int ExpectedHpBasisPoints,
    int AttributeModifier,
    int DurationTu,
    bool RollPerTarget,
    StringName ShieldFamily
);

internal static class BattleShieldPreviewRules
{
    private static readonly StringName Constitution = "constitution";
    private static readonly StringName Willpower = "willpower";
    private static readonly StringName FallbackFamily = "shield";

    internal static BattleShieldPreviewData BuildPreview(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        IEnumerable<BattleUnitState> targetUnits
    )
    {
        CombatEffectDefinition shieldEffect = FindFirstShieldEffect(effectDefinitions);
        if (shieldEffect == null)
        {
            return null;
        }

        BattleShieldValueRange range = BuildValueRange(
            sourceUnit,
            skillDefinition,
            shieldEffect
        );
        int targetCount = 0;
        int benefitingTargetCount = 0;
        long totalNetGainBasisPoints = 0;
        foreach (BattleUnitState targetUnit in targetUnits ?? Array.Empty<BattleUnitState>())
        {
            if (targetUnit == null)
            {
                continue;
            }
            targetCount++;
            int netGain = EstimateExpectedNetGainBasisPoints(targetUnit, range);
            totalNetGainBasisPoints += netGain;
            if (WouldApplyAtExpectedValue(targetUnit, range))
            {
                benefitingTargetCount++;
            }
        }

        return new BattleShieldPreviewData
        {
            HasShield = true,
            MinShieldHp = range.MinHp,
            MaxShieldHp = range.MaxHp,
            ExpectedShieldHpBasisPoints = range.ExpectedHpBasisPoints,
            AttributeModifier = range.AttributeModifier,
            DurationTu = range.DurationTu,
            TargetCount = targetCount,
            ExpectedBenefitingTargetCount = benefitingTargetCount,
            ExpectedTotalNetGainBasisPoints = (int)Math.Clamp(
                totalNetGainBasisPoints,
                0L,
                int.MaxValue
            ),
            RollPerTarget = range.RollPerTarget,
            ShieldFamily = range.ShieldFamily,
        };
    }

    internal static BattleShieldValueRange BuildValueRange(
        BattleUnitState sourceUnit,
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition
    )
    {
        if (effectDefinition == null || effectDefinition.EffectKind != BattleEffectKind.Shield)
        {
            return default;
        }
        int modifier = ResolveAttributeModifier(sourceUnit, effectDefinition);
        int diceCount = Math.Max(effectDefinition.DiceCount, 0);
        int diceSides = ResolveDiceSides(sourceUnit, effectDefinition);
        int baseHp = Math.Max(effectDefinition.Power, 0) + effectDefinition.DiceBonus + modifier;
        bool hasDice = diceCount > 0 && diceSides > 0;
        int minHp = hasDice ? Math.Max(baseHp + diceCount, 1) : Math.Max(baseHp, 0);
        int maxHp = hasDice
            ? Math.Max(baseHp + diceCount * diceSides, 1)
            : Math.Max(baseHp, 0);
        long expectedBasisPoints = hasDice
            ? (long)baseHp * 10000L + (long)diceCount * (diceSides + 1L) * 5000L
            : (long)Math.Max(baseHp, 0) * 10000L;
        if (hasDice)
        {
            expectedBasisPoints = Math.Max(expectedBasisPoints, 10000L);
        }
        return new BattleShieldValueRange(
            minHp,
            maxHp,
            (int)Math.Clamp(expectedBasisPoints, 0L, int.MaxValue),
            modifier,
            Math.Max(effectDefinition.DurationTu, 0),
            effectDefinition.ShieldRollPerTarget,
            ResolveFamily(skillDefinition, effectDefinition)
        );
    }

    internal static int EstimateExpectedNetGainBasisPoints(
        BattleUnitState targetUnit,
        BattleShieldValueRange range
    )
    {
        if (targetUnit == null || range.ExpectedHpBasisPoints <= 0)
        {
            return 0;
        }
        BattleUnitShieldSnapshot current = targetUnit.GetShieldStateTyped();
        int currentBasisPoints = Math.Max(current.CurrentHp, 0) * 10000;
        if (current.CurrentHp <= 0 || current.Duration <= 0)
        {
            return range.ExpectedHpBasisPoints;
        }
        if (current.Family == range.ShieldFamily)
        {
            return Math.Max(range.ExpectedHpBasisPoints - currentBasisPoints, 0);
        }
        return range.ExpectedHpBasisPoints > currentBasisPoints
            ? range.ExpectedHpBasisPoints - currentBasisPoints
            : 0;
    }

    internal static bool WouldApplyAtExpectedValue(
        BattleUnitState targetUnit,
        BattleShieldValueRange range
    )
    {
        if (targetUnit == null || range.ExpectedHpBasisPoints <= 0 || range.DurationTu <= 0)
        {
            return false;
        }
        BattleUnitShieldSnapshot current = targetUnit.GetShieldStateTyped();
        if (current.CurrentHp <= 0 || current.Duration <= 0)
        {
            return true;
        }
        int currentBasisPoints = Math.Max(current.CurrentHp, 0) * 10000;
        if (current.Family == range.ShieldFamily)
        {
            return range.ExpectedHpBasisPoints > currentBasisPoints
                || range.DurationTu > current.Duration;
        }
        return range.ExpectedHpBasisPoints > currentBasisPoints
            || (
                range.ExpectedHpBasisPoints == currentBasisPoints
                && range.DurationTu > current.Duration
            );
    }

    internal static int ResolveAttributeModifier(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        AttributeSnapshotIdKind modifierKind =
            effectDefinition?.ShieldAttributeModifierKind
            ?? AttributeSnapshotIdKind.Unknown;
        if (!AttributeSnapshot.IsAbilityModifierKind(modifierKind))
        {
            return 0;
        }
        return sourceUnit?.attribute_snapshot?.GetValue(
                AttributeSnapshot.ToStringName(modifierKind)
            )
            ?? 0;
    }

    internal static int ResolveDiceSides(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        if (effectDefinition == null || effectDefinition.DiceCount <= 0)
        {
            return 0;
        }
        if (effectDefinition.DiceSidesBase <= 0)
        {
            return Math.Max(effectDefinition.DiceSides, 0);
        }

        int baseSides = Math.Max(effectDefinition.DiceSidesBase, 0);
        if (sourceUnit?.attribute_snapshot == null)
        {
            return Math.Max(baseSides, 4);
        }
        int conModSides = Math.Max(effectDefinition.DiceSidesPerConstitutionMod, 0);
        int willModSides = Math.Max(effectDefinition.DiceSidesPerWillpowerMod, 0);
        int conScore = sourceUnit?.attribute_snapshot?.GetValue(Constitution) ?? 0;
        int willScore = sourceUnit?.attribute_snapshot?.GetValue(Willpower) ?? 0;
        int conMod = AttributeSnapshot.CalculateScoreModifier(conScore);
        int willMod = AttributeSnapshot.CalculateScoreModifier(willScore);
        long diceSides =
            (long)baseSides + (long)conMod * conModSides + (long)willMod * willModSides;
        return (int)Math.Clamp(diceSides, 4L, int.MaxValue);
    }

    internal static StringName ResolveFamily(
        SkillDefinition skillDefinition,
        CombatEffectDefinition effectDefinition
    )
    {
        if (effectDefinition != null && effectDefinition.ShieldFamily != "")
        {
            return effectDefinition.ShieldFamily;
        }
        if (skillDefinition != null && skillDefinition.SkillId != "")
        {
            return skillDefinition.SkillId;
        }
        return FallbackFamily;
    }

    private static CombatEffectDefinition FindFirstShieldEffect(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition
            in effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition?.EffectKind == BattleEffectKind.Shield)
            {
                return effectDefinition;
            }
        }
        return null;
    }
}
