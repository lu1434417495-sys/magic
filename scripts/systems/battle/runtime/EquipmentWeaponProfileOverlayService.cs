using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Composes equipment-ability weapon profile overlays onto a base
/// <see cref="WeaponProjection"/>. This is a pure projection-time composition
/// service: it never mutates <see cref="BattleUnitState"/>, never consumes RNG,
/// and always starts from a fresh base projection so repeated refreshes stay
/// idempotent. Callers install the returned projection exactly once via
/// <c>BattleUnitState.ApplyWeaponProjectionTyped(...)</c>, whose normalization
/// reconciles grip/two-handed/dice combinations and clamps range.
/// </summary>
internal static class EquipmentWeaponProfileOverlayService
{
    private sealed class OverlayEntry
    {
        internal EquipmentWeaponProfileOverlayDefinition Overlay;
        internal StringName BindingId = "";
        internal StringName EffectiveInstanceKey = "";
        internal int SlotOrder = int.MaxValue;
    }

    internal static WeaponProjection ApplyOverlays(
        BattleUnitState unit,
        WeaponProjection baseProjection,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingIndex,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        if (unit == null || baseProjection == null || baseProjection.IsEmpty())
            return baseProjection;
        List<OverlayEntry> entries = CollectEntries(unit, bindingIndex);
        if (entries.Count == 0)
            return baseProjection;
        entries.Sort(CompareEntries);

        WeaponProjection working = baseProjection.DuplicateState();
        int rangeDelta = 0;
        int minRange = 0;
        int maxRange = 0;
        bool anyApplied = false;
        foreach (OverlayEntry entry in entries)
        {
            EquipmentWeaponProfileOverlayDefinition overlay = entry.Overlay;
            if (!OverlayApplies(overlay, baseProjection, unit, itemDefinitions))
                continue;
            anyApplied = true;
            rangeDelta += overlay.AttackRangeDelta;
            if (overlay.MinAttackRange > 0)
                minRange = Math.Max(minRange, overlay.MinAttackRange);
            if (overlay.MaxAttackRange > 0)
                maxRange =
                    maxRange <= 0
                        ? overlay.MaxAttackRange
                        : Math.Min(maxRange, overlay.MaxAttackRange);
            working.weapon_one_handed_dice = ApplyDiceOverlay(
                working.weapon_one_handed_dice,
                overlay.OneHandedDiceOverlay
            );
            working.weapon_two_handed_dice = ApplyDiceOverlay(
                working.weapon_two_handed_dice,
                overlay.TwoHandedDiceOverlay
            );
            if (overlay.PhysicalDamageTagOverride != "")
                working.weapon_physical_damage_tag = overlay.PhysicalDamageTagOverride;
            if (overlay.GripOverride != "")
                working.weapon_current_grip = overlay.GripOverride;
            if (overlay.UsesTwoHandsOverride)
                working.weapon_uses_two_hands = true;
            if (overlay.IsVersatileOverride)
                working.weapon_is_versatile = true;
        }
        if (!anyApplied)
            return baseProjection;

        int range = Math.Max(baseProjection.weapon_attack_range + rangeDelta, 0);
        if (minRange > 0)
            range = Math.Max(range, minRange);
        if (maxRange > 0)
            range = Math.Min(range, maxRange);
        working.weapon_attack_range = range;
        return working;
    }

    private static List<OverlayEntry> CollectEntries(
        BattleUnitState unit,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingIndex
    )
    {
        var entries = new List<OverlayEntry>();
        if (bindingIndex == null || bindingIndex.Count == 0)
            return entries;
        EquipmentState equipmentView = unit.GetEquipmentView();
        foreach (
            BattleEquipmentAbilitySourceReadView source in
                unit.GetEquipmentAbilitySourcesReadViewTyped()
        )
        {
            if (source?.AbilityIds == null)
                continue;
            int slotOrder = ResolveSourceSlotOrder(equipmentView, source);
            foreach (StringName abilityId in source.AbilityIds)
            {
                if (
                    !bindingIndex.TryGetValue(
                        abilityId,
                        out EquipmentAbilityBindingDefinition binding
                    ) || binding?.WeaponProfileOverlays == null
                )
                    continue;
                foreach (
                    EquipmentWeaponProfileOverlayDefinition overlay in
                        binding.WeaponProfileOverlays
                )
                {
                    if (overlay == null)
                        continue;
                    entries.Add(
                        new OverlayEntry
                        {
                            Overlay = overlay,
                            BindingId = abilityId,
                            EffectiveInstanceKey = source.EffectiveInstanceKey,
                            SlotOrder = slotOrder,
                        }
                    );
                }
            }
        }
        return entries;
    }

    private static int CompareEntries(OverlayEntry left, OverlayEntry right)
    {
        int result = left.Overlay.Priority.CompareTo(right.Overlay.Priority);
        if (result != 0)
            return result;
        result = left.SlotOrder.CompareTo(right.SlotOrder);
        if (result != 0)
            return result;
        result = string.CompareOrdinal(
            left.BindingId.ToString(),
            right.BindingId.ToString()
        );
        if (result != 0)
            return result;
        result = string.CompareOrdinal(
            left.Overlay.OverlayId.ToString(),
            right.Overlay.OverlayId.ToString()
        );
        if (result != 0)
            return result;
        return string.CompareOrdinal(
            left.EffectiveInstanceKey.ToString(),
            right.EffectiveInstanceKey.ToString()
        );
    }

    private static int ResolveSourceSlotOrder(
        EquipmentState equipmentView,
        BattleEquipmentAbilitySourceReadView source
    )
    {
        if (
            equipmentView == null
            || source == null
            || source.SourceEquipmentInstanceId == ""
        )
            return int.MaxValue;
        IReadOnlyList<StringName> slotIds = equipmentView.GetEntrySlotIdsTyped();
        for (int index = 0; index < slotIds.Count; index++)
        {
            EquipmentEntryState entry = equipmentView.GetEntry(slotIds[index]);
            if (entry != null && entry.instance_id == source.SourceEquipmentInstanceId)
                return index;
        }
        return int.MaxValue;
    }

    private static bool OverlayApplies(
        EquipmentWeaponProfileOverlayDefinition overlay,
        WeaponProjection baseProjection,
        BattleUnitState unit,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        if (
            overlay.RequireEquippedWeapon
            && baseProjection.weapon_profile_kind
                != BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped)
        )
            return false;
        if (
            overlay.RequiredWeaponFamilies.Count > 0
            && !overlay.RequiredWeaponFamilies.Contains(baseProjection.weapon_family)
        )
            return false;
        if (
            overlay.RequiredWeaponTypeIds.Count > 0
            && !overlay.RequiredWeaponTypeIds.Contains(baseProjection.weapon_profile_type_id)
        )
            return false;
        return OverlayConditionsPass(overlay.ConditionGroup, unit, itemDefinitions);
    }

    /// <summary>
    /// Projection-safe condition evaluation: only has_equipment_tag conditions
    /// against the source unit's own equipment view are allowed. The content
    /// validator rejects every other condition kind in overlay condition
    /// groups, so anything else fails closed here.
    /// </summary>
    private static bool OverlayConditionsPass(
        EquipmentConditionGroupDefinition group,
        BattleUnitState unit,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        if (group == null)
            return true;
        bool anyMode = group.Mode == "any";
        bool sawAny = false;
        bool passed = !anyMode;
        foreach (
            EquipmentAbilityConditionDefinition condition in group.Conditions
                ?? Array.Empty<EquipmentAbilityConditionDefinition>()
        )
        {
            sawAny = true;
            bool conditionPassed = OverlayConditionPasses(
                condition,
                unit,
                itemDefinitions
            );
            passed = anyMode ? passed || conditionPassed : passed && conditionPassed;
        }
        foreach (
            EquipmentConditionGroupDefinition child in group.Groups
                ?? Array.Empty<EquipmentConditionGroupDefinition>()
        )
        {
            sawAny = true;
            bool childPassed = OverlayConditionsPass(child, unit, itemDefinitions);
            passed = anyMode ? passed || childPassed : passed && childPassed;
        }
        if (!sawAny)
            passed = true;
        return group.Negate ? !passed : passed;
    }

    private static bool OverlayConditionPasses(
        EquipmentAbilityConditionDefinition condition,
        BattleUnitState unit,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        if (
            condition?.Kind == "has_equipment_tag"
            && condition.PayloadDefinition
                is HasEquipmentTagConditionPayloadDefinition payload
        )
            return HasEquipmentTagPasses(payload, unit, itemDefinitions);
        return false;
    }

    private static bool HasEquipmentTagPasses(
        HasEquipmentTagConditionPayloadDefinition payload,
        BattleUnitState unit,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        if (
            payload == null
            || unit == null
            || itemDefinitions == null
            || itemDefinitions.Count == 0
        )
            return false;
        StringName subject = ProgressionDataUtils.to_string_name(payload.Subject);
        if (subject != "" && subject != "source" && subject != "self" && subject != "holder")
            return false;
        StringName selector = ProgressionDataUtils.to_string_name(
            payload.EquipmentSelector
        );
        if (selector == "")
            return false;
        StringName itemId = ProgressionDataUtils.to_string_name(
            unit.GetEquipmentView()?.GetEquippedItemId(selector) ?? new StringName("")
        );
        if (
            itemId == ""
            || !itemDefinitions.TryGetValue(itemId, out ItemDefinition itemDefinition)
            || itemDefinition == null
        )
            return false;
        return TagsPresent(itemDefinition, payload.AllTags, true)
            && TagsPresent(itemDefinition, payload.AnyTags, false);
    }

    private static bool TagsPresent(
        ItemDefinition itemDefinition,
        IReadOnlyList<StringName> requiredTags,
        bool requireAll
    )
    {
        if (requiredTags == null || requiredTags.Count == 0)
            return true;
        foreach (StringName requiredTag in requiredTags)
        {
            bool present = BattleEquipmentRequirementRules.ItemHasTag(
                itemDefinition,
                requiredTag
            );
            if (requireAll && !present)
                return false;
            if (!requireAll && present)
                return true;
        }
        return requireAll;
    }

    private static WeaponDice ApplyDiceOverlay(
        WeaponDice current,
        EquipmentWeaponDiceOverlayDefinition overlay
    )
    {
        if (overlay == null || overlay.Mode == EquipmentWeaponDiceOverlayModeKind.None)
            return current;
        if (overlay.Mode == EquipmentWeaponDiceOverlayModeKind.Override)
        {
            WeaponDice replacement = BuildDiceFromExpression(overlay.DiceOverride);
            return replacement ?? current;
        }
        if (current == null || current.IsEmpty())
            return current;
        WeaponDice result = current.DuplicateState();
        result.dice_count = Math.Max(result.dice_count + overlay.DiceCountDelta, 0);
        if (overlay.DiceSidesOverride > 0)
            result.dice_sides = overlay.DiceSidesOverride;
        result.flat_bonus += overlay.FlatBonusDelta;
        return result;
    }

    private static WeaponDice BuildDiceFromExpression(DiceExpressionDefinition expression)
    {
        if (expression?.Terms == null || expression.Terms.Count != 1)
            return null;
        DiceExpressionTermDefinition term = expression.Terms[0];
        if (
            term == null
            || term.CountBonusFact != null
            || term.DiceCount <= 0
            || term.DiceSides <= 1
        )
            return null;
        return new WeaponDice
        {
            dice_count = term.DiceCount,
            dice_sides = term.DiceSides,
            flat_bonus = expression.FlatBonus,
        };
    }
}
