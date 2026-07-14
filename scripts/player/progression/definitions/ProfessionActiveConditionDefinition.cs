using System;
using Godot;

public sealed class ProfessionActiveConditionDefinition
{
    private static readonly StringName ConditionAttributeRange = "attribute_range";
    private static readonly StringName ConditionReputationRange = "reputation_range";

    public ProfessionActiveConditionDefinition(
        StringName conditionType,
        StringName attributeId,
        StringName stateId,
        int minValue,
        int maxValue
    )
    {
        ConditionType = conditionType;
        AttributeId = attributeId;
        StateId = stateId;
        MinValue = minValue;
        MaxValue = maxValue;
    }

    public StringName ConditionType { get; }
    public StringName AttributeId { get; }
    public StringName StateId { get; }
    public int MinValue { get; }
    public int MaxValue { get; }
    internal ProfessionActiveConditionKind ConditionKind => ToConditionKind(ConditionType);

    public bool MatchesValue(int value) =>
        ProgressionDataUtils.MatchesValueRange(value, MinValue, MaxValue);

    internal static ProfessionActiveConditionDefinition FromResource(
        ProfessionActiveCondition source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ProgressionDefinitionProjection.RequireKnown(
            source.ConditionKind != ProfessionActiveConditionKind.Unknown,
            $"{path}.condition_type",
            source.condition_type
        );
        return new ProfessionActiveConditionDefinition(
            source.condition_type,
            source.attribute_id,
            source.state_id,
            source.min_value,
            source.max_value
        );
    }

    private static ProfessionActiveConditionKind ToConditionKind(StringName value)
    {
        if (value == ConditionAttributeRange)
            return ProfessionActiveConditionKind.AttributeRange;
        if (value == ConditionReputationRange)
            return ProfessionActiveConditionKind.ReputationRange;
        return ProfessionActiveConditionKind.Unknown;
    }
}
