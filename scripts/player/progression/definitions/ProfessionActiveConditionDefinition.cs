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

    private static ProfessionActiveConditionKind ToConditionKind(StringName value)
    {
        if (value == ConditionAttributeRange)
            return ProfessionActiveConditionKind.AttributeRange;
        if (value == ConditionReputationRange)
            return ProfessionActiveConditionKind.ReputationRange;
        return ProfessionActiveConditionKind.Unknown;
    }
}
