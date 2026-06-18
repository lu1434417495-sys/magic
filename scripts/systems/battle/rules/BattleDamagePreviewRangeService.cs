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

    private readonly record struct DamagePreviewEffectParameters(bool AddWeaponDice)
    {
        public static DamagePreviewEffectParameters FromEffect(CombatEffectDef effectDef)
        {
            return new DamagePreviewEffectParameters(effectDef?.add_weapon_dice ?? false);
        }
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
        IEnumerable<CombatEffectDef> effectDefs
    )
    {
        return BuildSkillDamagePreview(effectDefs, effectDef =>
            BuildDamageEffectRange(sourceUnit, effectDef, 0)
        );
    }

    internal static SkillDamagePreview BuildSkillDamagePreview(
        BattleUnitReadView sourceUnit,
        IEnumerable<CombatEffectDef> effectDefs
    )
    {
        return BuildSkillDamagePreview(effectDefs, effectDef =>
            BuildDamageEffectRange(sourceUnit, effectDef, 0)
        );
    }

    private static SkillDamagePreview BuildSkillDamagePreview(
        IEnumerable<CombatEffectDef> effectDefs,
        Func<CombatEffectDef, DamageEffectRange> rangeBuilder
    )
    {
        var damageRanges = new List<DamageEffectRange>();
        int minDamage = 0;
        int maxDamage = 0;

        if (effectDefs != null)
        {
            int effectIndex = 0;
            foreach (CombatEffectDef effectDef in effectDefs)
            {
                if (
                    effectDef == null
                    || effectDef.EffectKind != BattleEffectKind.Damage
                )
                {
                    effectIndex++;
                    continue;
                }
                DamageEffectRange effectRange = rangeBuilder(effectDef) with
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
        CombatEffectDef effectDef,
        int effectIndex
    )
    {
        int power = Mathf.Max(effectDef?.power ?? 0, 0);
        DiceRange skillDiceRange = BuildSkillDiceRange(effectDef);
        bool addWeaponDice = ShouldAddWeaponDice(effectDef);
        DiceRange weaponDiceRange = addWeaponDice
            ? BuildWeaponDiceRange(sourceUnit)
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
        CombatEffectDef effectDef,
        int effectIndex
    )
    {
        int power = Mathf.Max(effectDef?.power ?? 0, 0);
        DiceRange skillDiceRange = BuildSkillDiceRange(effectDef);
        bool addWeaponDice = ShouldAddWeaponDice(effectDef);
        DiceRange weaponDiceRange = addWeaponDice
            ? BuildWeaponDiceRange(sourceUnit)
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

    private static DiceRange BuildSkillDiceRange(CombatEffectDef effectDef)
    {
        if (effectDef == null)
        {
            return DiceRange.Empty;
        }
        int diceCount = Mathf.Max(effectDef.dice_count, 0);
        int diceSides = Mathf.Max(effectDef.dice_sides, 0);
        int diceBonus = effectDef.dice_bonus;
        return BuildDiceRange(diceCount, diceSides, diceBonus);
    }

    private static DiceRange BuildWeaponDiceRange(BattleUnitState sourceUnit)
    {
        PreviewWeaponDice dice = GetCurrentWeaponDamageDice(sourceUnit);
        if (dice == PreviewWeaponDice.Empty)
        {
            return DiceRange.Empty;
        }
        return BuildDiceRange(dice.DiceCount, dice.DiceSides, dice.FlatBonus);
    }

    private static DiceRange BuildWeaponDiceRange(BattleUnitReadView sourceUnit)
    {
        PreviewWeaponDice dice = GetCurrentWeaponDamageDice(sourceUnit);
        if (dice == PreviewWeaponDice.Empty)
        {
            return DiceRange.Empty;
        }
        return BuildDiceRange(dice.DiceCount, dice.DiceSides, dice.FlatBonus);
    }

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

    private static bool ShouldAddWeaponDice(CombatEffectDef effectDef)
    {
        return DamagePreviewEffectParameters.FromEffect(effectDef).AddWeaponDice;
    }

    private static PreviewWeaponDice GetCurrentWeaponDamageDice(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return PreviewWeaponDice.Empty;
        }
        WeaponDice dice = unitState.GetActiveWeaponDiceTyped();
        if (dice == null || dice.IsEmpty())
        {
            return PreviewWeaponDice.Empty;
        }
        return new PreviewWeaponDice(
            Mathf.Max(dice.dice_count, 0),
            Mathf.Max(dice.dice_sides, 0),
            dice.flat_bonus
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
