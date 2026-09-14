using System;
using Godot;

internal enum CombatUnitTargetResolutionMode
{
    Unknown = 0,
    Aggregate = 1,
    OrderedSlots = 2,
}

internal static class CombatUnitTargetResolutionContentRules
{
    private static readonly StringName Aggregate = "aggregate";
    private static readonly StringName OrderedSlots = "ordered_slots";

    internal static CombatUnitTargetResolutionMode ToMode(StringName value)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(value);
        if (normalized == Aggregate || normalized == "")
            return CombatUnitTargetResolutionMode.Aggregate;
        if (normalized == OrderedSlots)
            return CombatUnitTargetResolutionMode.OrderedSlots;
        return CombatUnitTargetResolutionMode.Unknown;
    }

    internal static StringName ToStringName(CombatUnitTargetResolutionMode value) =>
        value switch
        {
            CombatUnitTargetResolutionMode.Aggregate => Aggregate,
            CombatUnitTargetResolutionMode.OrderedSlots => OrderedSlots,
            _ => new StringName(""),
        };

    internal static bool IsValid(StringName value) =>
        ToMode(value) != CombatUnitTargetResolutionMode.Unknown;

    internal static string ValidValueLabel() => "aggregate, ordered_slots";
}

internal static class BattleTargetSlotCostRules
{
    internal static bool UsesOrderedTargetSlots(SkillDefinition skillDefinition) =>
        skillDefinition?.CombatProfile?.UnitTargetResolutionModeKind
        == CombatUnitTargetResolutionMode.OrderedSlots;

    internal static CombatSkillResourceCosts Resolve(
        CombatSkillDefinition combatProfile,
        int skillLevel,
        int targetSlotCount = 1
    )
    {
        if (combatProfile == null)
            return CombatSkillResourceCosts.Zero;
        CombatSkillResourceCosts baseCosts = combatProfile.GetEffectiveResourceCostValues(
            skillLevel
        );
        if (
            combatProfile.UnitTargetResolutionModeKind
            != CombatUnitTargetResolutionMode.OrderedSlots
        )
        {
            return baseCosts;
        }
        return ApplyLinearTargetSlotCosts(
            baseCosts,
            combatProfile.GetEffectiveMpCostPerTargetSlot(skillLevel),
            combatProfile.GetEffectiveStaminaCostPerTargetSlot(skillLevel),
            targetSlotCount
        );
    }

    internal static CombatSkillResourceCosts ApplyLinearTargetSlotCosts(
        CombatSkillResourceCosts baseCosts,
        int mpCostPerTargetSlot,
        int staminaCostPerTargetSlot,
        int targetSlotCount
    )
    {
        int slots = Math.Max(targetSlotCount, 1);
        return baseCosts with
        {
            MpCost = SaturatingLinearCost(baseCosts.MpCost, mpCostPerTargetSlot, slots),
            StaminaCost = SaturatingLinearCost(
                baseCosts.StaminaCost,
                staminaCostPerTargetSlot,
                slots
            ),
        };
    }

    private static int SaturatingLinearCost(int baseCost, int perSlotCost, int slots)
    {
        long value = Math.Max(baseCost, 0L)
            + Math.Max(perSlotCost, 0L) * Math.Max(slots, 1L);
        return value >= int.MaxValue ? int.MaxValue : (int)value;
    }
}
