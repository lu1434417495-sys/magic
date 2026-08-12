using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class GearSetThresholdDefinition
{
    internal GearSetThresholdDefinition(
        StringName thresholdId,
        int requiredPieceCount,
        string displayName,
        string description,
        IReadOnlyList<StringName> mandatoryMemberItemIds,
        IReadOnlyList<AttributeModifierDefinition> attributeModifiers,
        IReadOnlyList<StringName> grantedTraitIds
    )
    {
        ThresholdId = ProgressionDataUtils.to_string_name(thresholdId);
        RequiredPieceCount = requiredPieceCount;
        DisplayName = displayName ?? "";
        Description = description ?? "";
        MandatoryMemberItemIds = Freeze(mandatoryMemberItemIds);
        AttributeModifiers = Freeze(attributeModifiers);
        GrantedTraitIds = Freeze(grantedTraitIds);
    }

    public StringName ThresholdId { get; }
    public int RequiredPieceCount { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public IReadOnlyList<StringName> MandatoryMemberItemIds { get; }
    public IReadOnlyList<AttributeModifierDefinition> AttributeModifiers { get; }
    public IReadOnlyList<StringName> GrantedTraitIds { get; }

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

public sealed class GearSetDefinition
{
    internal GearSetDefinition(
        StringName gearSetId,
        string displayName,
        string description,
        IReadOnlyList<StringName> memberItemIds,
        StringName usageAnchorItemId,
        IReadOnlyList<GearSetThresholdDefinition> thresholds,
        string resourcePath
    )
    {
        GearSetId = ProgressionDataUtils.to_string_name(gearSetId);
        DisplayName = displayName ?? "";
        Description = description ?? "";
        MemberItemIds = Freeze(memberItemIds);
        UsageAnchorItemId = ProgressionDataUtils.to_string_name(usageAnchorItemId);
        Thresholds = Freeze(thresholds);
        ResourcePath = resourcePath ?? "";
    }

    public StringName GearSetId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public IReadOnlyList<StringName> MemberItemIds { get; }
    public StringName UsageAnchorItemId { get; }
    public IReadOnlyList<GearSetThresholdDefinition> Thresholds { get; }
    public string ResourcePath { get; }

    public GearSetThresholdDefinition GetThresholdById(StringName thresholdId)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(thresholdId);
        foreach (GearSetThresholdDefinition threshold in Thresholds)
        {
            if (threshold?.ThresholdId == normalized)
                return threshold;
        }
        return null;
    }

    public GearSetThresholdDefinition GetThresholdByTraitId(StringName traitId)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(traitId);
        foreach (GearSetThresholdDefinition threshold in Thresholds)
        {
            if (threshold == null)
                continue;
            foreach (StringName grantedTraitId in threshold.GrantedTraitIds)
            {
                if (grantedTraitId == normalized)
                    return threshold;
            }
        }
        return null;
    }

    internal static GearSetDefinition FromResource(GearSetDef source, string resourcePath)
    {
        ArgumentNullException.ThrowIfNull(source);
        StringName gearSetId = ProgressionDataUtils.to_string_name(source.gear_set_id);
        var members = CopyStringNames(source.member_item_ids);
        var thresholds = new List<GearSetThresholdDefinition>();
        foreach (GearSetThresholdDef threshold in source.thresholds ?? new())
        {
            if (threshold == null)
            {
                thresholds.Add(null);
                continue;
            }

            StringName thresholdId = ProgressionDataUtils.to_string_name(threshold.threshold_id);
            var modifiers = new List<AttributeModifierDefinition>();
            foreach (AttributeModifier modifier in threshold.attribute_modifiers ?? new())
            {
                if (modifier == null)
                {
                    modifiers.Add(null);
                    continue;
                }
                modifiers.Add(
                    new AttributeModifierDefinition(
                        modifier.attribute_id,
                        modifier.mode,
                        modifier.value,
                        modifier.value_per_rank,
                        "gear_set",
                        new StringName($"gear_set::{gearSetId}::{thresholdId}")
                    )
                );
            }

            thresholds.Add(
                new GearSetThresholdDefinition(
                    thresholdId,
                    threshold.required_piece_count,
                    threshold.display_name,
                    threshold.description,
                    CopyStringNames(threshold.mandatory_member_item_ids),
                    modifiers,
                    CopyStringNames(threshold.granted_trait_ids)
                )
            );
        }

        return new GearSetDefinition(
            gearSetId,
            source.display_name,
            source.description,
            members,
            source.usage_anchor_item_id,
            thresholds,
            resourcePath
        );
    }

    private static List<StringName> CopyStringNames(IEnumerable<StringName> values)
    {
        var result = new List<StringName>();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(ProgressionDataUtils.to_string_name(value));
        return result;
    }

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
