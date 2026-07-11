using System;
using Godot;

public sealed class EquipmentAttributeRequirementDefinition
{
    public EquipmentAttributeRequirementDefinition(StringName attributeId, int minValue)
    {
        AttributeId = attributeId;
        MinValue = minValue;
    }

    public StringName AttributeId { get; }
    public int MinValue { get; }

    internal static EquipmentAttributeRequirementDefinition FromResource(
        EquipmentAttributeRequirementDef source
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return new EquipmentAttributeRequirementDefinition(source.attribute_id, source.min_value);
    }
}
