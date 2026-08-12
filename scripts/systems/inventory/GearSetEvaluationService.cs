using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class GearSetDerivedTraitInstance
{
    internal GearSetDerivedTraitInstance(
        StringName gearSetId,
        StringName thresholdId,
        StringName traitId,
        StringName sourceItemId,
        StringName sourceEquipmentInstanceId,
        StringName effectiveInstanceKey
    )
    {
        GearSetId = gearSetId;
        ThresholdId = thresholdId;
        TraitId = traitId;
        SourceItemId = sourceItemId;
        SourceEquipmentInstanceId = sourceEquipmentInstanceId;
        EffectiveInstanceKey = effectiveInstanceKey;
    }

    public StringName GearSetId { get; }
    public StringName ThresholdId { get; }
    public StringName TraitId { get; }
    public StringName SourceItemId { get; }
    public StringName SourceEquipmentInstanceId { get; }
    public StringName EffectiveInstanceKey { get; }
}

public sealed class GearSetThresholdStatus
{
    internal GearSetThresholdStatus(
        StringName thresholdId,
        int requiredPieceCount,
        string displayName,
        string description,
        bool isActive
    )
    {
        ThresholdId = thresholdId;
        RequiredPieceCount = requiredPieceCount;
        DisplayName = displayName ?? "";
        Description = description ?? "";
        IsActive = isActive;
    }

    public StringName ThresholdId { get; }
    public int RequiredPieceCount { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public bool IsActive { get; }
}

public sealed class GearSetActivationSummary
{
    internal GearSetActivationSummary(
        StringName gearSetId,
        string displayName,
        int equippedPieceCount,
        int totalPieceCount,
        IReadOnlyList<StringName> contributingItemIds,
        IReadOnlyList<StringName> contributingItemInstanceIds,
        IReadOnlyList<GearSetThresholdStatus> thresholds
    )
    {
        GearSetId = gearSetId;
        DisplayName = displayName ?? "";
        EquippedPieceCount = equippedPieceCount;
        TotalPieceCount = totalPieceCount;
        ContributingItemIds = Freeze(contributingItemIds);
        ContributingItemInstanceIds = Freeze(contributingItemInstanceIds);
        Thresholds = Freeze(thresholds);
    }

    public StringName GearSetId { get; }
    public string DisplayName { get; }
    public int EquippedPieceCount { get; }
    public int TotalPieceCount { get; }
    public IReadOnlyList<StringName> ContributingItemIds { get; }
    public IReadOnlyList<StringName> ContributingItemInstanceIds { get; }
    public IReadOnlyList<GearSetThresholdStatus> Thresholds { get; }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        var copy = new List<T>();
        if (values != null)
        {
            foreach (T value in values)
                copy.Add(value);
        }
        return new ReadOnlyCollection<T>(copy);
    }
}

public sealed class GearSetEvaluationSnapshot
{
    public static GearSetEvaluationSnapshot Empty { get; } = new(
        Array.Empty<GearSetActivationSummary>(),
        Array.Empty<AttributeModifierDefinition>(),
        Array.Empty<GearSetDerivedTraitInstance>()
    );

    internal GearSetEvaluationSnapshot(
        IReadOnlyList<GearSetActivationSummary> activeSets,
        IReadOnlyList<AttributeModifierDefinition> attributeModifiers,
        IReadOnlyList<GearSetDerivedTraitInstance> derivedTraitInstances
    )
    {
        ActiveSets = Freeze(activeSets);
        AttributeModifiers = Freeze(attributeModifiers);
        DerivedTraitInstances = Freeze(derivedTraitInstances);
    }

    public IReadOnlyList<GearSetActivationSummary> ActiveSets { get; }
    public IReadOnlyList<AttributeModifierDefinition> AttributeModifiers { get; }
    public IReadOnlyList<GearSetDerivedTraitInstance> DerivedTraitInstances { get; }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values)
    {
        var copy = new List<T>();
        if (values != null)
        {
            foreach (T value in values)
                copy.Add(value);
        }
        return new ReadOnlyCollection<T>(copy);
    }
}

public static class GearSetEvaluationService
{
    public static GearSetEvaluationSnapshot Evaluate(
        EquipmentState equipment,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        IReadOnlyDictionary<StringName, GearSetDefinition> gearSetDefinitions
    )
    {
        if (
            equipment == null
            || itemDefinitions == null
            || gearSetDefinitions == null
            || gearSetDefinitions.Count == 0
        )
        {
            return GearSetEvaluationSnapshot.Empty;
        }

        Dictionary<StringName, EquipmentEntryState> equippedByItemId =
            BuildValidEquippedItemIndex(equipment, itemDefinitions);
        if (equippedByItemId.Count == 0)
            return GearSetEvaluationSnapshot.Empty;

        var activeSets = new List<GearSetActivationSummary>();
        var attributeModifiers = new List<AttributeModifierDefinition>();
        var derivedTraits = new List<GearSetDerivedTraitInstance>();
        foreach (GearSetDefinition definition in SortedDefinitions(gearSetDefinitions))
        {
            if (definition == null)
                continue;

            var contributingItems = new List<StringName>();
            var contributingInstances = new List<StringName>();
            EquipmentEntryState firstContributingEntry = null;
            foreach (StringName memberItemId in definition.MemberItemIds)
            {
                if (
                    memberItemId == ""
                    || !equippedByItemId.TryGetValue(
                        memberItemId,
                        out EquipmentEntryState entry
                    )
                    || entry == null
                )
                {
                    continue;
                }
                firstContributingEntry ??= entry;
                contributingItems.Add(memberItemId);
                contributingInstances.Add(entry.instance_id);
            }
            if (contributingItems.Count == 0)
                continue;

            EquipmentEntryState sourceEntry = equippedByItemId.TryGetValue(
                definition.UsageAnchorItemId,
                out EquipmentEntryState configuredAnchor
            )
                ? configuredAnchor
                : firstContributingEntry;

            var thresholdStatuses = new List<GearSetThresholdStatus>();
            foreach (GearSetThresholdDefinition threshold in definition.Thresholds)
            {
                if (threshold == null)
                    continue;
                bool active = contributingItems.Count >= threshold.RequiredPieceCount
                    && ContainsAll(contributingItems, threshold.MandatoryMemberItemIds);
                thresholdStatuses.Add(
                    new GearSetThresholdStatus(
                        threshold.ThresholdId,
                        threshold.RequiredPieceCount,
                        threshold.DisplayName,
                        threshold.Description,
                        active
                    )
                );
                if (!active)
                    continue;

                attributeModifiers.AddRange(threshold.AttributeModifiers);
                foreach (StringName traitId in threshold.GrantedTraitIds)
                {
                    if (traitId == "" || sourceEntry == null || sourceEntry.instance_id == "")
                        continue;
                    derivedTraits.Add(
                        new GearSetDerivedTraitInstance(
                            definition.GearSetId,
                            threshold.ThresholdId,
                            traitId,
                            sourceEntry.item_id,
                            sourceEntry.instance_id,
                            new StringName(
                                $"gear_set::{definition.GearSetId}::{threshold.ThresholdId}::{traitId}"
                            )
                        )
                    );
                }
            }

            activeSets.Add(
                new GearSetActivationSummary(
                    definition.GearSetId,
                    definition.DisplayName,
                    contributingItems.Count,
                    definition.MemberItemIds.Count,
                    contributingItems,
                    contributingInstances,
                    thresholdStatuses
                )
            );
        }

        return new GearSetEvaluationSnapshot(activeSets, attributeModifiers, derivedTraits);
    }

    private static Dictionary<StringName, EquipmentEntryState> BuildValidEquippedItemIndex(
        EquipmentState equipment,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        var result = new Dictionary<StringName, EquipmentEntryState>();
        foreach (StringName entrySlotId in equipment.GetEntrySlotIdsTyped())
        {
            EquipmentEntryState entry = equipment.GetEntry(entrySlotId);
            EquipmentInstanceState instance = entry?.GetEquipmentInstance();
            if (
                entry == null
                || entry.IsEmpty()
                || instance == null
                || instance.current_durability <= 0
                || result.ContainsKey(entry.item_id)
                || !itemDefinitions.TryGetValue(entry.item_id, out ItemDefinition itemDefinition)
                || itemDefinition == null
                || !itemDefinition.IsEquipment()
                || !HasValidEquipmentPlacement(
                    entrySlotId,
                    equipment,
                    itemDefinition
                )
            )
            {
                continue;
            }
            result.Add(entry.item_id, entry);
        }
        return result;
    }

    private static bool HasValidEquipmentPlacement(
        StringName entrySlotId,
        EquipmentState equipment,
        ItemDefinition itemDefinition
    )
    {
        IReadOnlyList<StringName> allowedEntrySlots =
            itemDefinition.GetEquipmentSlotIdsTyped();
        bool entrySlotAllowed = false;
        foreach (StringName allowedSlotId in allowedEntrySlots)
        {
            if (ProgressionDataUtils.to_string_name(allowedSlotId) != entrySlotId)
                continue;
            entrySlotAllowed = true;
            break;
        }
        if (!entrySlotAllowed)
            return false;

        IReadOnlyList<StringName> expectedOccupiedSlots =
            itemDefinition.GetFinalOccupiedSlotIdsTyped(entrySlotId);
        IReadOnlyList<StringName> actualOccupiedSlots =
            equipment.GetOccupiedSlotIdsForEntryTyped(entrySlotId);
        if (
            expectedOccupiedSlots.Count == 0
            || actualOccupiedSlots.Count != expectedOccupiedSlots.Count
        )
        {
            return false;
        }

        var actual = new HashSet<StringName>();
        foreach (StringName actualSlotId in actualOccupiedSlots)
            actual.Add(ProgressionDataUtils.to_string_name(actualSlotId));
        if (actual.Count != expectedOccupiedSlots.Count)
            return false;
        foreach (StringName expectedSlotId in expectedOccupiedSlots)
        {
            if (!actual.Contains(ProgressionDataUtils.to_string_name(expectedSlotId)))
                return false;
        }
        return true;
    }

    private static bool ContainsAll(
        IReadOnlyList<StringName> equippedItemIds,
        IReadOnlyList<StringName> requiredItemIds
    )
    {
        if (requiredItemIds == null || requiredItemIds.Count == 0)
            return true;
        var equipped = new HashSet<StringName>(equippedItemIds);
        foreach (StringName requiredItemId in requiredItemIds)
        {
            if (!equipped.Contains(requiredItemId))
                return false;
        }
        return true;
    }

    private static List<GearSetDefinition> SortedDefinitions(
        IReadOnlyDictionary<StringName, GearSetDefinition> definitions
    )
    {
        var result = new List<GearSetDefinition>(definitions.Values);
        result.Sort(
            (left, right) =>
                string.CompareOrdinal(
                    left?.GearSetId.ToString() ?? "",
                    right?.GearSetId.ToString() ?? ""
                )
        );
        return result;
    }
}
