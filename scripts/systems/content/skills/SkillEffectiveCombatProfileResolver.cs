using System.Collections.Generic;
using Godot;

internal static class SkillEffectiveCombatProfileResolver
{
    private static readonly StringName EmptyAreaPattern = "";
    private static readonly IReadOnlyList<CombatCastVariantDef> EmptyCastVariants =
        System.Array.Empty<CombatCastVariantDef>();

    internal static SkillEffectiveCombatProfile Resolve(
        ISkillCatalog skillCatalog,
        SkillDef skillDef,
        int skillLevel
    )
    {
        if (skillCatalog != null && skillDef != null && skillDef.skill_id != "")
        {
            return skillCatalog.GetEffectiveCombatProfile(skillDef.skill_id, skillLevel);
        }
        return BuildUncached(skillDef, skillLevel);
    }

    internal static SkillEffectiveCombatProfile BuildUncached(SkillDef skillDef, int skillLevel)
    {
        if (skillDef?.combat_profile == null)
        {
            return BuildMissing(skillLevel);
        }

        return BuildUncached(skillDef, skillDef.combat_profile, skillLevel);
    }

    internal static SkillEffectiveCombatProfile BuildUncached(
        CombatSkillDef profile,
        int skillLevel
    )
    {
        return BuildUncached(null, profile, skillLevel);
    }

    private static SkillEffectiveCombatProfile BuildUncached(
        SkillDef skillDef,
        CombatSkillDef profile,
        int skillLevel
    )
    {
        if (profile == null)
        {
            return BuildMissing(skillLevel);
        }

        Godot.Collections.Dictionary levelOverride = profile.GetLevelOverride(skillLevel);
        return new SkillEffectiveCombatProfile(
            skillDef,
            profile,
            skillLevel,
            ResolveResourceCosts(profile, levelOverride),
            ReadIntOverride(levelOverride, "attack_roll_bonus", profile.attack_roll_bonus),
            ReadAreaPatternOverride(levelOverride, profile.area_pattern),
            ReadIntOverride(levelOverride, "area_value", profile.area_value),
            ReadIntOverride(levelOverride, "range_value", profile.range_value),
            ReadIntOverride(levelOverride, "max_target_count", profile.max_target_count),
            BuildUnlockedCastVariantsSnapshot(profile, skillLevel)
        );
    }

    internal static SkillEffectiveCombatProfile BuildMissing(int skillLevel)
    {
        return new SkillEffectiveCombatProfile(
            null,
            null,
            skillLevel,
            CombatSkillResourceCosts.Zero,
            0,
            EmptyAreaPattern,
            0,
            0,
            0,
            EmptyCastVariants
        );
    }

    internal static Godot.Collections.Array<CombatCastVariantDef> ToGodotArray(
        IReadOnlyList<CombatCastVariantDef> variants
    )
    {
        var result = new Godot.Collections.Array<CombatCastVariantDef>();
        foreach (CombatCastVariantDef variant in variants ?? EmptyCastVariants)
        {
            if (variant != null)
            {
                result.Add(variant);
            }
        }
        return result;
    }

    private static CombatSkillResourceCosts ResolveResourceCosts(
        CombatSkillDef profile,
        Godot.Collections.Dictionary overrides
    )
    {
        return new CombatSkillResourceCosts(
            TryReadResourceCostOverride(overrides, "ap_cost", out int effectiveApCost)
                ? effectiveApCost
                : profile.ap_cost,
            TryReadResourceCostOverride(overrides, "mp_cost", out int effectiveMpCost)
                ? effectiveMpCost
                : profile.mp_cost,
            TryReadResourceCostOverride(overrides, "stamina_cost", out int effectiveStaminaCost)
                ? effectiveStaminaCost
                : profile.stamina_cost,
            TryReadResourceCostOverride(overrides, "aura_cost", out int effectiveAuraCost)
                ? effectiveAuraCost
                : profile.aura_cost,
            TryReadResourceCostOverride(overrides, "cooldown_tu", out int effectiveCooldownTu)
                ? effectiveCooldownTu
                : profile.cooldown_tu
        );
    }

    private static bool TryReadResourceCostOverride(
        Godot.Collections.Dictionary overrides,
        string key,
        out int value
    )
    {
        if (overrides != null && overrides.ContainsKey(key))
        {
            Variant rawValue = overrides[key];
            if (rawValue.VariantType == Variant.Type.Int)
            {
                value = rawValue.AsInt32();
                return true;
            }
            if (rawValue.VariantType == Variant.Type.Float)
            {
                value = (int)rawValue.AsDouble();
                return true;
            }
        }
        value = 0;
        return false;
    }

    private static int ReadIntOverride(
        Godot.Collections.Dictionary overrides,
        string key,
        int fallback
    )
    {
        return overrides != null && overrides.ContainsKey(key)
            ? overrides[key].AsInt32()
            : fallback;
    }

    private static StringName ReadAreaPatternOverride(
        Godot.Collections.Dictionary overrides,
        StringName fallback
    )
    {
        return overrides != null && overrides.ContainsKey("area_pattern")
            ? ProgressionDataUtils.to_string_name(overrides["area_pattern"])
            : fallback;
    }

    private static IReadOnlyList<CombatCastVariantDef> BuildUnlockedCastVariantsSnapshot(
        CombatSkillDef profile,
        int skillLevel
    )
    {
        Godot.Collections.Array<CombatCastVariantDef> variants =
            profile?.GetUnlockedCastVariants(skillLevel);
        if (variants == null || variants.Count == 0)
        {
            return EmptyCastVariants;
        }

        var snapshot = new List<CombatCastVariantDef>(variants.Count);
        foreach (CombatCastVariantDef variant in variants)
        {
            if (variant != null)
            {
                snapshot.Add(variant);
            }
        }
        return snapshot.Count > 0 ? snapshot.AsReadOnly() : EmptyCastVariants;
    }
}
