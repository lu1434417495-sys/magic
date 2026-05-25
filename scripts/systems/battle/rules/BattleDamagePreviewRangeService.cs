using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleDamagePreviewRangeService : RefCounted
{
    private static readonly StringName DamageEffectType = "damage";

    private readonly record struct DiceRange(
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

    private readonly record struct DamageEffectRange(
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

    public static GDictionary build_skill_damage_preview(GodotObject source_unit, GArray effect_defs)
    {
        var damageRanges = new GArray();
        int minDamage = 0;
        int maxDamage = 0;

        if (effect_defs != null)
        {
            for (int effectIndex = 0; effectIndex < effect_defs.Count; effectIndex++)
            {
                GodotObject effectDef = effect_defs[effectIndex].AsGodotObject();
                if (effectDef == null || GdInterop.GetStringName(effectDef, "effect_type") != DamageEffectType)
                {
                    continue;
                }
                DamageEffectRange effectRange = BuildDamageEffectRange(source_unit, effectDef, effectIndex);
                damageRanges.Add(effectRange.ToDictionary());
                minDamage += effectRange.MinDamage;
                maxDamage += effectRange.MaxDamage;
            }
        }

        var preview = new GDictionary
        {
            ["has_damage"] = damageRanges.Count > 0,
            ["min_damage"] = minDamage,
            ["max_damage"] = maxDamage,
            ["summary_text"] = "",
            ["damage_ranges"] = damageRanges,
        };
        preview["summary_text"] = format_damage_range_text(preview);
        return preview;
    }

    public static string format_damage_range_text(GDictionary preview)
    {
        if (preview == null || preview.Count == 0 || !GdInterop.GetBool(preview, "has_damage"))
        {
            return "";
        }
        int minDamage = GdInterop.GetInt(preview, "min_damage");
        int maxDamage = GdInterop.GetInt(preview, "max_damage", minDamage);
        if (minDamage == maxDamage)
        {
            return $"伤害 {minDamage}";
        }
        return $"伤害 {minDamage}-{maxDamage}";
    }

    private static DamageEffectRange BuildDamageEffectRange(GodotObject sourceUnit, GodotObject effectDef, int effectIndex)
    {
        int power = Mathf.Max(GdInterop.GetInt(effectDef, "power"), 0);
        DiceRange skillDiceRange = BuildSkillDiceRange(effectDef);
        bool addWeaponDice = ShouldAddWeaponDice(effectDef);
        DiceRange weaponDiceRange = addWeaponDice
            ? BuildWeaponDiceRange(sourceUnit)
            : DiceRange.Empty;
        int effectMinDamage = power
            + skillDiceRange.MinDamage
            + weaponDiceRange.MinDamage;
        int effectMaxDamage = power
            + skillDiceRange.MaxDamage
            + weaponDiceRange.MaxDamage;

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

    private static DiceRange BuildSkillDiceRange(GodotObject effectDef)
    {
        GDictionary parameters = GdInterop.GetDictionary(effectDef, "params");
        if (parameters.Count == 0)
        {
            return DiceRange.Empty;
        }
        int diceCount = Mathf.Max(GdInterop.GetInt(parameters, "dice_count"), 0);
        int diceSides = Mathf.Max(GdInterop.GetInt(parameters, "dice_sides"), 0);
        int diceBonus = GdInterop.GetInt(parameters, "dice_bonus");
        return BuildDiceRange(diceCount, diceSides, diceBonus);
    }

    private static DiceRange BuildWeaponDiceRange(GodotObject sourceUnit)
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

    private static bool ShouldAddWeaponDice(GodotObject effectDef)
    {
        GDictionary parameters = GdInterop.GetDictionary(effectDef, "params");
        return parameters.Count > 0 && GdInterop.GetBool(parameters, "add_weapon_dice");
    }

    private static WeaponDice GetCurrentWeaponDamageDice(GodotObject unitState)
    {
        if (unitState == null)
        {
            return WeaponDice.Empty;
        }
        GDictionary dice = GdInterop.GetBool(unitState, "weapon_uses_two_hands")
            ? GdInterop.GetDictionary(unitState, "weapon_two_handed_dice")
            : GdInterop.GetDictionary(unitState, "weapon_one_handed_dice");
        if (dice.Count == 0)
        {
            return WeaponDice.Empty;
        }
        return new WeaponDice(
            Mathf.Max(GdInterop.GetInt(dice, "dice_count"), 0),
            Mathf.Max(GdInterop.GetInt(dice, "dice_sides"), 0),
            GdInterop.GetInt(dice, "flat_bonus")
        );
    }
}
