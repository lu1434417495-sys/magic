using Godot;

internal enum ProfessionActiveConditionKind
{
    Unknown = 0,
    AttributeRange,
    ReputationRange,
}

[GlobalClass]
public partial class ProfessionActiveCondition : Resource
{
    private static readonly StringName ConditionAttributeRange = "attribute_range";
    private static readonly StringName ConditionReputationRange = "reputation_range";

    [Export]
    public StringName condition_type = "attribute_range";
    internal ProfessionActiveConditionKind ConditionKind
    {
        get => ToConditionKind(condition_type);
        set => condition_type = ToStringName(value);
    }

    [Export]
    public StringName attribute_id = "";

    [Export]
    public StringName state_id = "";

    [Export]
    public int min_value = 0;

    [Export]
    public int max_value = 0;

    public bool MatchesValue(int value) =>
        ProgressionDataUtils.MatchesValueRange(value, min_value, max_value);

    internal static ProfessionActiveConditionKind ToConditionKind(StringName value)
    {
        if (value == ConditionAttributeRange)
            return ProfessionActiveConditionKind.AttributeRange;
        if (value == ConditionReputationRange)
            return ProfessionActiveConditionKind.ReputationRange;
        return ProfessionActiveConditionKind.Unknown;
    }

    internal static StringName ToStringName(ProfessionActiveConditionKind kind)
    {
        return kind switch
        {
            ProfessionActiveConditionKind.AttributeRange => ConditionAttributeRange,
            ProfessionActiveConditionKind.ReputationRange => ConditionReputationRange,
            _ => "",
        };
    }
}
