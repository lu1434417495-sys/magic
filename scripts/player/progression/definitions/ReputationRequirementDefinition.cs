using System;
using Godot;

public sealed class ReputationRequirementDefinition
{
    public ReputationRequirementDefinition(StringName stateId, int minValue, int maxValue)
    {
        StateId = stateId;
        MinValue = minValue;
        MaxValue = maxValue;
    }

    public StringName StateId { get; }
    public int MinValue { get; }
    public int MaxValue { get; }

    public bool MatchesValue(int value) =>
        ProgressionDataUtils.MatchesValueRange(value, MinValue, MaxValue);

    internal static ReputationRequirementDefinition FromResource(
        ReputationRequirement source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ReputationRequirementDefinition(
            source.state_id,
            source.min_value,
            source.max_value
        );
    }
}
