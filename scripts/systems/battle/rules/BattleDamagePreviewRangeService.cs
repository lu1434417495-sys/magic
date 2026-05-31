using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleDamagePreviewRangeService : RefCounted
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

    private readonly record struct WeaponDice(int DiceCount, int DiceSides, int FlatBonus)
    {
        public static WeaponDice Empty => new(0, 0, 0);
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
        public GDictionary ToDictionary()
        {
            return new GDictionary
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
        public GDictionary ToDictionary()
        {
            return new GDictionary
            {
                ["has_damage"] = HasDamage,
                ["min_damage"] = MinDamage,
                ["max_damage"] = MaxDamage,
                ["summary_text"] = FormatDamageRangeText(HasDamage, MinDamage, MaxDamage),
                ["damage_ranges"] = DamageRangesToArray(DamageRanges),
            };
        }

        private static GArray DamageRangesToArray(IReadOnlyList<DamageEffectRange> damageRanges)
        {
            var result = new GArray();
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

    public static GDictionary build_skill_damage_preview(
        GodotObject source_unit,
        GArray effect_defs
    )
    {
        return build_skill_damage_preview_typed(source_unit as BattleUnitState, effect_defs)
            .ToDictionary();
    }

    public static SkillDamagePreview build_skill_damage_preview_typed(
        BattleUnitState source_unit,
        GArray effect_defs
    )
    {
        var damageRanges = new List<DamageEffectRange>();
        int minDamage = 0;
        int maxDamage = 0;

        if (effect_defs != null)
        {
            for (int effectIndex = 0; effectIndex < effect_defs.Count; effectIndex++)
            {
                CombatEffectDef effectDef = effect_defs[effectIndex].AsGodotObject() as CombatEffectDef;
                if (
                    effectDef == null
                    || effectDef.effect_type != DamageEffectType
                )
                {
                    continue;
                }
                DamageEffectRange effectRange = BuildDamageEffectRange(source_unit, effectDef, effectIndex);
                damageRanges.Add(effectRange);
                minDamage += effectRange.MinDamage;
                maxDamage += effectRange.MaxDamage;
            }
        }

        return new SkillDamagePreview(
            damageRanges.Count > 0,
            minDamage,
            maxDamage,
            damageRanges
        );
    }

    public static string format_damage_range_text(GDictionary preview)
    {
        if (preview == null || preview.Count == 0)
        {
            return "";
        }
        int minDamage = ReadInt(preview, "min_damage");
        int maxDamage = ReadInt(preview, "max_damage", minDamage);
        return FormatDamageRangeText(ReadPreviewHasDamage(preview), minDamage, maxDamage);
    }

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
        GDictionary parameters = effectDef?.@params ?? new GDictionary();
        if (parameters.Count == 0)
        {
            return DiceRange.Empty;
        }
        int diceCount = Mathf.Max(ReadInt(parameters, "dice_count"), 0);
        int diceSides = Mathf.Max(ReadInt(parameters, "dice_sides"), 0);
        int diceBonus = ReadInt(parameters, "dice_bonus");
        return BuildDiceRange(diceCount, diceSides, diceBonus);
    }

    private static DiceRange BuildWeaponDiceRange(BattleUnitState sourceUnit)
    {
        WeaponDice dice = GetCurrentWeaponDamageDice(sourceUnit);
        if (dice == WeaponDice.Empty)
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

    private static WeaponDice GetCurrentWeaponDamageDice(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return WeaponDice.Empty;
        }
        GDictionary dice = unitState.weapon_uses_two_hands
            ? unitState.weapon_two_handed_dice
            : unitState.weapon_one_handed_dice;
        if (dice.Count == 0)
        {
            return WeaponDice.Empty;
        }
        return new WeaponDice(
            Mathf.Max(ReadInt(dice, "dice_count"), 0),
            Mathf.Max(ReadInt(dice, "dice_sides"), 0),
            ReadInt(dice, "flat_bonus")
        );
    }

    private static int ReadInt(GDictionary data, string key, int fallback = 0)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static bool ReadPreviewHasDamage(GDictionary preview)
    {
        return ReadFlag(preview, "has_damage");
    }

    private static bool ReadFlag(GDictionary data, string key)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return false;
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Bool && value.AsBool();
    }
}
