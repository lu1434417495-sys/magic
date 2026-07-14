using System;
using Godot;

public sealed class AttributeModifierDefinition
{
    public AttributeModifierDefinition(
        StringName attributeId,
        StringName mode,
        int value,
        int valuePerRank,
        StringName sourceType,
        StringName sourceId
    )
    {
        AttributeId = attributeId;
        Mode = mode;
        Value = value;
        ValuePerRank = valuePerRank;
        SourceType = sourceType;
        SourceId = sourceId;
    }

    public StringName AttributeId { get; }
    public StringName Mode { get; }
    public int Value { get; }
    public int ValuePerRank { get; }
    public StringName SourceType { get; }
    public StringName SourceId { get; }

    internal static AttributeModifierDefinition FromResource(AttributeModifier source) =>
        source == null
            ? null
            : new AttributeModifierDefinition(
                source.attribute_id,
                source.mode,
                source.value,
                source.value_per_rank,
                source.source_type,
                source.source_id
            );

    public int GetValueForRank(int rank) =>
        Value + ValuePerRank * (Math.Max(rank, 1) - 1);

    public bool IsPercent() =>
        AttributeModifier.ToMode(Mode) == AttributeModifierMode.Percent;

    public bool IsFlat() =>
        AttributeModifier.ToMode(Mode) == AttributeModifierMode.Flat;
}
