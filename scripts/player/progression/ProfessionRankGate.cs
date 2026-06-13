using Godot;

internal enum ProfessionGateCheckMode
{
    Unknown = 0,
    Historical,
    ActiveOnly,
}

[GlobalClass]
public partial class ProfessionRankGate : Resource
{
    private static readonly StringName CheckHistorical = "historical";
    private static readonly StringName CheckActiveOnly = "active_only";

    [Export]
    public StringName profession_id = "";

    [Export]
    public int min_rank = 1;

    [Export]
    public StringName check_mode = "historical";

    internal ProfessionGateCheckMode CheckModeKind => ToCheckMode(check_mode);

    internal static ProfessionGateCheckMode ToCheckMode(StringName value)
    {
        if (value == CheckHistorical)
            return ProfessionGateCheckMode.Historical;
        if (value == CheckActiveOnly)
            return ProfessionGateCheckMode.ActiveOnly;
        return ProfessionGateCheckMode.Unknown;
    }

    internal static StringName ToStringName(ProfessionGateCheckMode mode)
    {
        return mode switch
        {
            ProfessionGateCheckMode.Historical => CheckHistorical,
            ProfessionGateCheckMode.ActiveOnly => CheckActiveOnly,
            _ => "",
        };
    }
}
