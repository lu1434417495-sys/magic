using System;
using Godot;

public sealed class AttributeRequirementDefinition
{
    public AttributeRequirementDefinition(StringName attributeId, int minValue, int maxValue)
    {
        AttributeId = attributeId;
        MinValue = minValue;
        MaxValue = maxValue;
    }

    public StringName AttributeId { get; }
    public int MinValue { get; }
    public int MaxValue { get; }

    public bool MatchesValue(int value) =>
        ProgressionDataUtils.MatchesValueRange(value, MinValue, MaxValue);

    internal static AttributeRequirementDefinition FromResource(
        AttributeRequirement source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return new AttributeRequirementDefinition(
            source.attribute_id,
            source.min_value,
            source.max_value
        );
    }
}
