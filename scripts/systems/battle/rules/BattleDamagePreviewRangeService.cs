using System;
using System.Collections.Generic;
using Godot;

public static class BattleDamagePreviewRangeService
{
    private static readonly StringName DamageEffectType = "damage";

    public readonly record struct DiceRange(
        int DiceCount,
        int DiceSides,
        int DiceBonus,
        int MinDamage,
        int MaxDamage
    )
    {
        public static DiceRange Empty => new(0, 0, 0, 0, 0);
    }

    private readonly record struct PreviewWeaponDice(int DiceCount, int DiceSides, int FlatBonus)
    {
        public static PreviewWeaponDice Empty => new(0, 0, 0);
    }

    public readonly record struct DamageEffectRange(
        int EffectIndex,
        int Power,
        bool AddWeaponDice,
        int MinDamage,
        int MaxDamage,
        DiceRange SkillDiceRange,
        DiceRange WeaponDiceRange
    );

    public readonly record struct SkillDamagePreview(
        bool HasDamage,
        int MinDamage,
        int MaxDamage,
        IReadOnlyList<DamageEffectRange> DamageRanges
    )
    {
        public string SummaryText => FormatDamageRangeText(this);
    }

    public static SkillDamagePreview BuildSkillDamagePreview(
        BattleUnitState sourceUnit,
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        return BuildSkillDamagePreview(effectDefinitions, effectDefinition =>
            BuildDamageEffectRange(sourceUnit, effectDefinition, 0)
        );
    }

    internal static SkillDamagePreview BuildSkillDamagePreview(
        BattleUnitReadView sourceUnit,
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        return BuildSkillDamagePreview(effectDefinitions, effectDefinition =>
            BuildDamageEffectRange(sourceUnit, effectDefinition, 0)
        );
    }

    private static SkillDamagePreview BuildSkillDamagePreview(
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        Func<CombatEffectDefinition, DamageEffectRange> rangeBuilder
    )
    {
        var damageRanges = new List<DamageEffectRange>();
        int minDamage = 0;
        int maxDamage = 0;

        if (effectDefinitions != null)
        {
            int effectIndex = 0;
            foreach (CombatEffectDefinition effectDefinition in effectDefinitions)
            {
                if (
                    effectDefinition == null
                    || effectDefinition.EffectKind != BattleEffectKind.Damage
                )
                {
                    effectIndex++;
                    continue;
                }
                DamageEffectRange effectRange = rangeBuilder(effectDefinition) with
                {
                    EffectIndex = effectIndex,
                };
                damageRanges.Add(effectRange);
                minDamage += effectRange.MinDamage;
                maxDamage += effectRange.MaxDamage;
                effectIndex++;
            }
        }

        return new SkillDamagePreview(
            damageRanges.Count > 0,
            minDamage,
            maxDamage,
            damageRanges
        );
    }

    public static string FormatDamageRangeText(SkillDamagePreview preview) =>
        FormatDamageRangeText(preview.HasDamage, preview.MinDamage, preview.MaxDamage);

    private static string FormatDamageRangeText(bool hasDamage, int minDamage, int maxDamage)
    {
        if (!hasDamage)
        {
            return "";
        }
        if (minDamage == maxDamage)
        {
            return $"伤害 {minDamage}";
        }
        return $"伤害 {minDamage}-{maxDamage}";
    }

    private static DamageEffectRange BuildDamageEffectRange(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition,
        int effectIndex
    )
    {
        int power = Mathf.Max(effectDefinition?.Power ?? 0, 0);
        DiceRange skillDiceRange = BuildSkillDiceRange(effectDefinition);
        bool addWeaponDice = ShouldAddWeaponDice(effectDefinition);
        DiceRange weaponDiceRange = addWeaponDice
            ? BuildWeaponDiceRange(sourceUnit, effectDefinition.WeaponDiceMultiplier)
            : DiceRange.Empty;
        int effectMinDamage = power + skillDiceRange.MinDamage + weaponDiceRange.MinDamage;
        int effectMaxDamage = power + skillDiceRange.MaxDamage + weaponDiceRange.MaxDamage;

        return new DamageEffectRange(
            effectIndex,
            power,
            addWeaponDice,
            effectMinDamage,
            effectMaxDamage,
            skillDiceRange,
            weaponDiceRange
        );
    }

    private static DamageEffectRange BuildDamageEffectRange(
        BattleUnitReadView sourceUnit,
        CombatEffectDefinition effectDefinition,
        int effectIndex
    )
    {
        int power = Mathf.Max(effectDefinition?.Power ?? 0, 0);
        DiceRange skillDiceRange = BuildSkillDiceRange(effectDefinition);
        bool addWeaponDice = ShouldAddWeaponDice(effectDefinition);
        DiceRange weaponDiceRange = addWeaponDice
            ? BuildWeaponDiceRange(sourceUnit, effectDefinition.WeaponDiceMultiplier)
            : DiceRange.Empty;
        int effectMinDamage = power + skillDiceRange.MinDamage + weaponDiceRange.MinDamage;
        int effectMaxDamage = power + skillDiceRange.MaxDamage + weaponDiceRange.MaxDamage;

        return new DamageEffectRange(
            effectIndex,
            power,
            addWeaponDice,
            effectMinDamage,
            effectMaxDamage,
            skillDiceRange,
            weaponDiceRange
        );
    }

    private static DiceRange BuildSkillDiceRange(CombatEffectDefinition effectDefinition)
    {
        if (effectDefinition == null)
        {
            return DiceRange.Empty;
        }
        int diceCount = Mathf.Max(effectDefinition.DiceCount, 0);
        int diceSides = Mathf.Max(effectDefinition.DiceSides, 0);
        int diceBonus = effectDefinition.DiceBonus;
        return BuildDiceRange(diceCount, diceSides, diceBonus);
    }

    private static DiceRange BuildWeaponDiceRange(
        BattleUnitState sourceUnit,
        int weaponDiceMultiplier
    )
    {
        PreviewWeaponDice dice = GetCurrentWeaponDamageDice(sourceUnit);
        if (dice == PreviewWeaponDice.Empty)
        {
            return DiceRange.Empty;
        }
        return BuildDiceRange(
            SaturatingMultiply(dice.DiceCount, weaponDiceMultiplier),
            dice.DiceSides,
            dice.FlatBonus
        );
    }

    private static DiceRange BuildWeaponDiceRange(
        BattleUnitReadView sourceUnit,
        int weaponDiceMultiplier
    )
    {
        PreviewWeaponDice dice = GetCurrentWeaponDamageDice(sourceUnit);
        if (dice == PreviewWeaponDice.Empty)
        {
            return DiceRange.Empty;
        }
        return BuildDiceRange(
            SaturatingMultiply(dice.DiceCount, weaponDiceMultiplier),
            dice.DiceSides,
            dice.FlatBonus
        );
    }

    private static int SaturatingMultiply(int value, int multiplier) =>
        (int)Math.Min(
            (long)Math.Max(value, 0) * Math.Max(multiplier, 1),
            int.MaxValue
        );

    private static DiceRange BuildDiceRange(int diceCount, int diceSides, int diceBonus)
    {
        if (diceCount <= 0 || diceSides <= 0)
        {
            return DiceRange.Empty;
        }
        return new DiceRange(
            diceCount,
            diceSides,
            diceBonus,
            diceCount + diceBonus,
            diceCount * diceSides + diceBonus
        );
    }

    private static bool ShouldAddWeaponDice(CombatEffectDefinition effectDefinition)
    {
        return effectDefinition?.AddWeaponDice ?? false;
    }

    private static PreviewWeaponDice GetCurrentWeaponDamageDice(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return PreviewWeaponDice.Empty;
        }
        BattleWeaponProjectionValues weaponProjection =
            unitState.GetWeaponProjectionReadViewTyped().Values;
        BattleWeaponDiceValues dice = weaponProjection.ActiveDice;
        if (!dice.HasUsableDice)
        {
            return PreviewWeaponDice.Empty;
        }
        return new PreviewWeaponDice(
            Mathf.Max(dice.DiceCount, 0),
            Mathf.Max(dice.DiceSides, 0),
            dice.FlatBonus
        );
    }

    private static PreviewWeaponDice GetCurrentWeaponDamageDice(BattleUnitReadView unitView)
    {
        if (!unitView.IsValid)
        {
            return PreviewWeaponDice.Empty;
        }
        return new PreviewWeaponDice(
            Mathf.Max(unitView.CurrentWeaponDamageDiceCount, 0),
            Mathf.Max(unitView.CurrentWeaponDamageDiceSides, 0),
            unitView.CurrentWeaponDamageFlatBonus
        );
    }
}
