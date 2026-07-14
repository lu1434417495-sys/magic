using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattleDamagePreviewRangeProjection
{
    internal static GodotProjectionLease<GDictionary> BuildLease(
        BattleDamagePreviewRangeService.SkillDamagePreview? preview
    )
    {
        GDictionary root = new();
        GodotProjectionLease<GDictionary> lease =
            GodotProjectionLease<GDictionary>.CreateOwnedRoot(
                root,
                "battle-damage-preview-range",
                LifetimeDomain.Request,
                "BattleDamagePreviewRangeProjection.root"
            );
        try
        {
            if (preview.HasValue)
                WriteInto(lease, root, preview.Value);
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal static GDictionary WriteOwned<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleDamagePreviewRangeService.SkillDamagePreview? preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (preview.HasValue)
            WriteInto(lease, result, preview.Value);
        return result;
    }

    private static void WriteInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary target,
        BattleDamagePreviewRangeService.SkillDamagePreview preview
    )
        where TLeaseRoot : class, IDisposable
    {
        target["has_damage"] = preview.HasDamage;
        target["min_damage"] = preview.MinDamage;
        target["max_damage"] = preview.MaxDamage;
        target["summary_text"] = preview.SummaryText;
        target["damage_ranges"] = WriteDamageRanges(
            lease,
            preview.DamageRanges,
            "BattleDamagePreviewRangeProjection.damage_ranges"
        );
    }

    private static GArray WriteDamageRanges<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyList<BattleDamagePreviewRangeService.DamageEffectRange> ranges,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (ranges == null)
            return result;
        for (int index = 0; index < ranges.Count; index++)
            result.Add(WriteRange(lease, ranges[index], $"{reason}[{index}]"));
        return result;
    }

    private static GDictionary WriteRange<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleDamagePreviewRangeService.DamageEffectRange range,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        result["effect_index"] = range.EffectIndex;
        result["power"] = range.Power;
        result["add_weapon_dice"] = range.AddWeaponDice;
        result["min_damage"] = range.MinDamage;
        result["max_damage"] = range.MaxDamage;
        result["damage_dice_count"] = range.SkillDiceRange.DiceCount;
        result["damage_dice_sides"] = range.SkillDiceRange.DiceSides;
        result["damage_dice_bonus"] = range.SkillDiceRange.DiceBonus;
        result["damage_dice_min"] = range.SkillDiceRange.MinDamage;
        result["damage_dice_max"] = range.SkillDiceRange.MaxDamage;
        result["weapon_damage_dice_count"] = range.WeaponDiceRange.DiceCount;
        result["weapon_damage_dice_sides"] = range.WeaponDiceRange.DiceSides;
        result["weapon_damage_dice_bonus"] = range.WeaponDiceRange.DiceBonus;
        result["weapon_damage_dice_min"] = range.WeaponDiceRange.MinDamage;
        result["weapon_damage_dice_max"] = range.WeaponDiceRange.MaxDamage;
        return result;
    }
}
