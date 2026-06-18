using System.Collections.Generic;

internal static class BattleDamagePreviewRangeProjection
{
    internal static Godot.Collections.Dictionary Project(
        BattleDamagePreviewRangeService.SkillDamagePreview? preview
    )
    {
        return preview.HasValue
            ? Project(preview.Value)
            : new Godot.Collections.Dictionary();
    }

    internal static Godot.Collections.Dictionary Project(
        BattleDamagePreviewRangeService.SkillDamagePreview preview
    )
    {
        return new Godot.Collections.Dictionary
        {
            ["has_damage"] = preview.HasDamage,
            ["min_damage"] = preview.MinDamage,
            ["max_damage"] = preview.MaxDamage,
            ["summary_text"] = preview.SummaryText,
            ["damage_ranges"] = DamageRangesToArray(preview.DamageRanges),
        };
    }

    internal static Godot.Collections.Dictionary Project(
        BattleDamagePreviewRangeService.DamageEffectRange range
    )
    {
        return new Godot.Collections.Dictionary
        {
            ["effect_index"] = range.EffectIndex,
            ["power"] = range.Power,
            ["add_weapon_dice"] = range.AddWeaponDice,
            ["min_damage"] = range.MinDamage,
            ["max_damage"] = range.MaxDamage,
            ["damage_dice_count"] = range.SkillDiceRange.DiceCount,
            ["damage_dice_sides"] = range.SkillDiceRange.DiceSides,
            ["damage_dice_bonus"] = range.SkillDiceRange.DiceBonus,
            ["damage_dice_min"] = range.SkillDiceRange.MinDamage,
            ["damage_dice_max"] = range.SkillDiceRange.MaxDamage,
            ["weapon_damage_dice_count"] = range.WeaponDiceRange.DiceCount,
            ["weapon_damage_dice_sides"] = range.WeaponDiceRange.DiceSides,
            ["weapon_damage_dice_bonus"] = range.WeaponDiceRange.DiceBonus,
            ["weapon_damage_dice_min"] = range.WeaponDiceRange.MinDamage,
            ["weapon_damage_dice_max"] = range.WeaponDiceRange.MaxDamage,
        };
    }

    private static Godot.Collections.Array DamageRangesToArray(
        IReadOnlyList<BattleDamagePreviewRangeService.DamageEffectRange> damageRanges
    )
    {
        var result = new Godot.Collections.Array();
        if (damageRanges == null)
            return result;
        foreach (BattleDamagePreviewRangeService.DamageEffectRange damageRange in damageRanges)
        {
            result.Add(Project(damageRange));
        }
        return result;
    }
}
