#nullable enable

using System;
using System.Collections.Generic;

internal static class SkillRootCombatImportValueRules
{
    internal static bool TryUnlockMode(string? value, out SkillImportUnlockMode result) =>
        Try(value, "standard", SkillImportUnlockMode.Standard, "composite_upgrade", SkillImportUnlockMode.CompositeUpgrade, out result);

    internal static bool TryCoreTransition(string? value, out SkillImportCoreSkillTransitionMode result) =>
        Try(value, "inherit", SkillImportCoreSkillTransitionMode.Inherit, "replace_sources_with_result", SkillImportCoreSkillTransitionMode.ReplaceSourcesWithResult, out result);

    internal static bool TryTier(string? value, out SkillImportProgressionTier result)
    {
        switch (value)
        {
            case "": result = SkillImportProgressionTier.None; return true;
            case "basic": result = SkillImportProgressionTier.Basic; return true;
            case "intermediate": result = SkillImportProgressionTier.Intermediate; return true;
            case "advanced": result = SkillImportProgressionTier.Advanced; return true;
            case "ultimate": result = SkillImportProgressionTier.Ultimate; return true;
            default: result = default; return false;
        }
    }

    internal static bool TryAttributeModifierMode(string? value, out AttributeModifierImportMode result) =>
        Try(value, "flat", AttributeModifierImportMode.Flat, "percent", AttributeModifierImportMode.Percent, out result);

    internal static bool TryWeaponRangePolicy(string? value, out CombatWeaponRangePolicyImportKind result)
    {
        switch (value)
        {
            case "":
            case "current_weapon": result = CombatWeaponRangePolicyImportKind.CurrentWeapon; return true;
            case "configured": result = CombatWeaponRangePolicyImportKind.Configured; return true;
            case "current_weapon_plus_configured": result = CombatWeaponRangePolicyImportKind.CurrentWeaponPlusConfigured; return true;
            default: result = default; return false;
        }
    }

    internal static bool TryAttackResolution(
        string? value,
        out CombatSkillLevelOverrideAttackResolutionMode result
    )
    {
        if (value == "" || value == "auto")
        {
            result = CombatSkillLevelOverrideAttackResolutionMode.Auto;
            return true;
        }
        return SkillJsonImportValueRules.TryParseLevelOverrideAttackResolutionMode(
            value,
            out result
        );
    }

    internal static bool TryMasteryTrigger(string? value, out CombatMasteryTriggerImportKind result)
    {
        switch (value)
        {
            case "skill_damage_dice_max": result = CombatMasteryTriggerImportKind.SkillDamageDiceMax; return true;
            case "weapon_attack_quality": result = CombatMasteryTriggerImportKind.WeaponAttackQuality; return true;
            case "damage_dealt": result = CombatMasteryTriggerImportKind.DamageDealt; return true;
            case "status_applied": result = CombatMasteryTriggerImportKind.StatusApplied; return true;
            case "effect_applied": result = CombatMasteryTriggerImportKind.EffectApplied; return true;
            case "incoming_physical_hit": result = CombatMasteryTriggerImportKind.IncomingPhysicalHit; return true;
            case "secondary_hit": result = CombatMasteryTriggerImportKind.SecondaryHit; return true;
            case "source_bound_weapon_bonus_damage": result = CombatMasteryTriggerImportKind.SourceBoundWeaponBonusDamage; return true;
            case "terrain_effective_trigger": result = CombatMasteryTriggerImportKind.TerrainEffectiveTrigger; return true;
            default: result = default; return false;
        }
    }

    internal static bool TryMasteryAmount(string? value, out CombatMasteryAmountImportKind result) =>
        Try(value, "per_target_rank", CombatMasteryAmountImportKind.PerTargetRank, "per_cast_hp_ratio", CombatMasteryAmountImportKind.PerCastHpRatio, out result);

    internal static bool TrySpellFate(string? value, out CombatSpellFateImportKind result) =>
        Try(value, "", CombatSpellFateImportKind.None, "control_roll", CombatSpellFateImportKind.ControlRoll, out result);

    internal static bool TrySpellCritical(string? value, out CombatSpellCriticalImportKind result) =>
        Try(value, "", CombatSpellCriticalImportKind.None, "mp_refund", CombatSpellCriticalImportKind.MpRefund, out result);

    internal static bool TryBacklash(string? value, out CombatBacklashImportKind result) =>
        Try(value, "", CombatBacklashImportKind.None, "ground_anchor_drift", CombatBacklashImportKind.GroundAnchorDrift, out result);

    internal static bool TryAreaOrigin(string? value, out CombatAreaOriginImportKind result)
    {
        switch (value)
        {
            case "target": result = CombatAreaOriginImportKind.Target; return true;
            case "caster": result = CombatAreaOriginImportKind.Caster; return true;
            case "anchor_coord": result = CombatAreaOriginImportKind.AnchorCoord; return true;
            default: result = default; return false;
        }
    }

    internal static bool TryAreaDirection(string? value, out CombatAreaDirectionImportKind result)
    {
        switch (value)
        {
            case "target_vector": result = CombatAreaDirectionImportKind.TargetVector; return true;
            case "target_vector_perpendicular": result = CombatAreaDirectionImportKind.TargetVectorPerpendicular; return true;
            case "caster_facing": result = CombatAreaDirectionImportKind.CasterFacing; return true;
            default: result = default; return false;
        }
    }

    internal static bool TryProjectile(string? value, out CombatProjectileImportKind result)
    {
        switch (value)
        {
            case "": result = CombatProjectileImportKind.Inherit; return true;
            case "none": result = CombatProjectileImportKind.None; return true;
            case "nonmagical": result = CombatProjectileImportKind.Nonmagical; return true;
            case "magical": result = CombatProjectileImportKind.Magical; return true;
            case "current_weapon": result = CombatProjectileImportKind.CurrentWeapon; return true;
            default: result = default; return false;
        }
    }

    internal static bool TryBaseProjectile(
        string? value,
        out CombatBaseProjectileImportKind result
    )
    {
        switch (value)
        {
            case "none": result = CombatBaseProjectileImportKind.None; return true;
            case "nonmagical": result = CombatBaseProjectileImportKind.Nonmagical; return true;
            case "magical": result = CombatBaseProjectileImportKind.Magical; return true;
            case "current_weapon": result = CombatBaseProjectileImportKind.CurrentWeapon; return true;
            default: result = default; return false;
        }
    }

    internal static bool TryTargetSelection(string? value, out CombatTargetSelectionImportKind result)
    {
        switch (value)
        {
            case "single_unit": result = CombatTargetSelectionImportKind.SingleUnit; return true;
            case "multi_unit": result = CombatTargetSelectionImportKind.MultiUnit; return true;
            case "random_chain": result = CombatTargetSelectionImportKind.RandomChain; return true;
            case "self": result = CombatTargetSelectionImportKind.Self; return true;
            case "single_coord": result = CombatTargetSelectionImportKind.SingleCoord; return true;
            case "coord_pair": result = CombatTargetSelectionImportKind.CoordPair; return true;
            default: result = default; return false;
        }
    }

    internal static bool TryUnitTargetResolution(string? value, out CombatUnitTargetResolutionImportKind result) =>
        Try(value, "aggregate", CombatUnitTargetResolutionImportKind.Aggregate, "ordered_slots", CombatUnitTargetResolutionImportKind.OrderedSlots, out result);

    internal static bool TrySelectionOrder(string? value, out CombatSelectionOrderImportKind result) =>
        Try(value, "stable", CombatSelectionOrderImportKind.Stable, "manual", CombatSelectionOrderImportKind.Manual, out result);

    internal static bool TryFootprint(string? value, out CombatCastFootprintImportKind result)
    {
        switch (value)
        {
            case "single": result = CombatCastFootprintImportKind.Single; return true;
            case "line2": result = CombatCastFootprintImportKind.Line2; return true;
            case "square2": result = CombatCastFootprintImportKind.Square2; return true;
            case "unordered": result = CombatCastFootprintImportKind.Unordered; return true;
            default: result = default; return false;
        }
    }

    internal static bool TrySaveAbility(string? value, out CombatSaveAbilityImportKind result)
    {
        switch (value)
        {
            case "strength": result = CombatSaveAbilityImportKind.Strength; return true;
            case "agility": result = CombatSaveAbilityImportKind.Agility; return true;
            case "constitution": result = CombatSaveAbilityImportKind.Constitution; return true;
            case "perception": result = CombatSaveAbilityImportKind.Perception; return true;
            case "intelligence": result = CombatSaveAbilityImportKind.Intelligence; return true;
            case "willpower": result = CombatSaveAbilityImportKind.Willpower; return true;
            default: result = default; return false;
        }
    }

    internal static bool TryDamageTag(string? value, out DamageTagImportKind result)
    {
        switch (value)
        {
            case "physical_slash": result = DamageTagImportKind.PhysicalSlash; return true;
            case "physical_pierce": result = DamageTagImportKind.PhysicalPierce; return true;
            case "physical_blunt": result = DamageTagImportKind.PhysicalBlunt; return true;
            case "fire": result = DamageTagImportKind.Fire; return true;
            case "freeze": result = DamageTagImportKind.Freeze; return true;
            case "lightning": result = DamageTagImportKind.Lightning; return true;
            case "negative_energy": result = DamageTagImportKind.NegativeEnergy; return true;
            case "force": result = DamageTagImportKind.Force; return true;
            case "psychic": result = DamageTagImportKind.Psychic; return true;
            case "radiant": result = DamageTagImportKind.Radiant; return true;
            case "thunder": result = DamageTagImportKind.Thunder; return true;
            case "magic": result = DamageTagImportKind.Magic; return true;
            case "acid": result = DamageTagImportKind.Acid; return true;
            case "poison": result = DamageTagImportKind.Poison; return true;
            default: result = default; return false;
        }
    }

    internal static bool TryTerrain(string? value, out BattleTerrainImportKind result)
    {
        switch (value)
        {
            case "land": result = BattleTerrainImportKind.Land; return true;
            case "forest": result = BattleTerrainImportKind.Forest; return true;
            case "water": result = BattleTerrainImportKind.Water; return true;
            case "shallow_water": result = BattleTerrainImportKind.ShallowWater; return true;
            case "flowing_water": result = BattleTerrainImportKind.FlowingWater; return true;
            case "deep_water": result = BattleTerrainImportKind.DeepWater; return true;
            case "ice": result = BattleTerrainImportKind.Ice; return true;
            case "mud": result = BattleTerrainImportKind.Mud; return true;
            case "spike": result = BattleTerrainImportKind.Spike; return true;
            default: result = default; return false;
        }
    }

    internal static bool TrySquare2Corner(
        string? value,
        out CombatCastSquare2Corner result
    )
    {
        switch (value)
        {
            case "top_left": result = CombatCastSquare2Corner.TopLeft; return true;
            case "top_right": result = CombatCastSquare2Corner.TopRight; return true;
            case "bottom_left": result = CombatCastSquare2Corner.BottomLeft; return true;
            case "bottom_right": result = CombatCastSquare2Corner.BottomRight; return true;
            default: result = default; return false;
        }
    }

    private static bool Try<T>(
        string? value,
        string firstText,
        T first,
        string secondText,
        T second,
        out T result
    ) where T : struct
    {
        if (value == firstText) { result = first; return true; }
        if (value == secondText) { result = second; return true; }
        result = default;
        return false;
    }
}

internal static class SkillRootCombatSchemaValues
{
    internal static IReadOnlyList<string> Of(params string[] values) => Array.AsReadOnly(values);
}

internal sealed class SkillUnlockModeSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("standard", "composite_upgrade");
}

internal sealed class SkillCoreTransitionSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("inherit", "replace_sources_with_result");
}

internal sealed class SkillProgressionTierSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("basic", "intermediate", "advanced", "ultimate");
}

internal sealed class SkillAttributeModifierModeSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("flat", "percent");
}

internal sealed class SkillWeaponRangePolicySchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("current_weapon", "configured", "current_weapon_plus_configured");
}

internal sealed class SkillMasteryTriggerSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of(
        "skill_damage_dice_max", "weapon_attack_quality", "damage_dealt", "status_applied",
        "effect_applied", "incoming_physical_hit", "secondary_hit",
        "source_bound_weapon_bonus_damage", "terrain_effective_trigger"
    );
}

internal sealed class SkillMasteryAmountSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("per_target_rank", "per_cast_hp_ratio");
}

internal sealed class SkillSpellFateSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("control_roll");
}

internal sealed class SkillSpellCriticalSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("mp_refund");
}

internal sealed class SkillBacklashSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("ground_anchor_drift");
}

internal sealed class SkillAreaOriginSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("target", "caster", "anchor_coord");
}

internal sealed class SkillAreaDirectionSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("target_vector", "target_vector_perpendicular", "caster_facing");
}

internal sealed class SkillBaseProjectileSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("none", "nonmagical", "magical", "current_weapon");
}

internal sealed class SkillTargetSelectionSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("single_unit", "multi_unit", "random_chain", "self", "single_coord", "coord_pair");
}

internal sealed class SkillUnitTargetResolutionSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("aggregate", "ordered_slots");
}

internal sealed class SkillSelectionOrderSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("stable", "manual");
}

internal sealed class SkillCastFootprintSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("single", "line2", "square2", "unordered");
}

internal sealed class SkillSaveAbilitySchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("strength", "agility", "constitution", "perception", "intelligence", "willpower");
}

internal sealed class SkillDamageTagSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of(
        "physical_slash", "physical_pierce", "physical_blunt", "fire", "freeze",
        "lightning", "negative_energy", "force", "psychic", "radiant", "thunder",
        "magic", "acid", "poison"
    );
}

internal sealed class SkillTerrainSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of(
        "land", "forest", "water", "shallow_water", "flowing_water", "deep_water",
        "ice", "mud", "spike"
    );
}

internal sealed class SkillSquare2CornerSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = SkillRootCombatSchemaValues.Of("top_left", "top_right", "bottom_left", "bottom_right");
}
