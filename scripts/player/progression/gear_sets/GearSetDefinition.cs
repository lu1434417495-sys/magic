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
        IReadOnlyList<GearSetThresholdDefinition> thresholds
    )
    {
        GearSetId = ProgressionDataUtils.to_string_name(gearSetId);
        DisplayName = displayName ?? "";
        Description = description ?? "";
        MemberItemIds = Freeze(memberItemIds);
        UsageAnchorItemId = ProgressionDataUtils.to_string_name(usageAnchorItemId);
        Thresholds = Freeze(thresholds);
    }

    public StringName GearSetId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public IReadOnlyList<StringName> MemberItemIds { get; }
    public StringName UsageAnchorItemId { get; }
    public IReadOnlyList<GearSetThresholdDefinition> Thresholds { get; }

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
