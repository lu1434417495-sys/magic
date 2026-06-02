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
    )
    {
        internal Godot.Collections.Dictionary ToDictionary()
        {
            return new Godot.Collections.Dictionary
            {
                ["effect_index"] = EffectIndex,
                ["power"] = Power,
                ["add_weapon_dice"] = AddWeaponDice,
                ["min_damage"] = MinDamage,
                ["max_damage"] = MaxDamage,
                ["damage_dice_count"] = SkillDiceRange.DiceCount,
                ["damage_dice_sides"] = SkillDiceRange.DiceSides,
                ["damage_dice_bonus"] = SkillDiceRange.DiceBonus,
                ["damage_dice_min"] = SkillDiceRange.MinDamage,
                ["damage_dice_max"] = SkillDiceRange.MaxDamage,
                ["weapon_damage_dice_count"] = WeaponDiceRange.DiceCount,
                ["weapon_damage_dice_sides"] = WeaponDiceRange.DiceSides,
                ["weapon_damage_dice_bonus"] = WeaponDiceRange.DiceBonus,
                ["weapon_damage_dice_min"] = WeaponDiceRange.MinDamage,
                ["weapon_damage_dice_max"] = WeaponDiceRange.MaxDamage,
            };
        }
    }

    public readonly record struct SkillDamagePreview(
        bool HasDamage,
        int MinDamage,
        int MaxDamage,
        IReadOnlyList<DamageEffectRange> DamageRanges
    )
    {
        public string SummaryText => FormatDamageRangeText(this);

        internal Godot.Collections.Dictionary ToDictionary()
        {
            return new Godot.Collections.Dictionary
            {
                ["has_damage"] = HasDamage,
                ["min_damage"] = MinDamage,
                ["max_damage"] = MaxDamage,
                ["summary_text"] = SummaryText,
                ["damage_ranges"] = DamageRangesToArray(DamageRanges),
            };
        }

        private static Godot.Collections.Array DamageRangesToArray(
            IReadOnlyList<DamageEffectRange> damageRanges
        )
        {
            var result = new Godot.Collections.Array();
            if (damageRanges == null)
            {
                return result;
            }
            foreach (DamageEffectRange damageRange in damageRanges)
            {
                result.Add(damageRange.ToDictionary());
            }
            return result;
        }
    }

    public static SkillDamagePreview BuildSkillDamagePreview(
        BattleUnitState sourceUnit,
        IEnumerable<CombatEffectDef> effectDefs
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
                    || effectDef.effect_type != DamageEffectType
                )
                {
                    effectIndex++;
                    continue;
                }
                DamageEffectRange effectRange = BuildDamageEffectRange(
                    sourceUnit,
                    effectDef,
                    effectIndex
                );
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
        Godot.Collections.Dictionary dice = unitState.weapon_uses_two_hands
            ? unitState.weapon_two_handed_dice
            : unitState.weapon_one_handed_dice;
        if (dice.Count == 0)
        {
            return PreviewWeaponDice.Empty;
        }
        return new PreviewWeaponDice(
            Mathf.Max(ReadInt(dice, "dice_count"), 0),
            Mathf.Max(ReadInt(dice, "dice_sides"), 0),
            ReadInt(dice, "flat_bonus")
        );
    }

    private static int ReadInt(Godot.Collections.Dictionary data, string key, int fallback = 0)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback;
        }
        return data[key].AsInt32();
    }
}
