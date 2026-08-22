using System;
using Godot;

public sealed class ProfessionRankGateDefinition
{
    private static readonly StringName CheckHistorical = "historical";
    private static readonly StringName CheckActiveOnly = "active_only";

    public ProfessionRankGateDefinition(
        StringName professionId,
        int minRank,
        StringName checkMode
    )
    {
        ProfessionId = professionId;
        MinRank = minRank;
        CheckMode = checkMode;
    }

    public StringName ProfessionId { get; }
    public int MinRank { get; }
    public StringName CheckMode { get; }
    internal ProfessionGateCheckMode CheckModeKind => ToCheckMode(CheckMode);

    private static ProfessionGateCheckMode ToCheckMode(StringName value)
    {
        if (value == CheckHistorical)
            return ProfessionGateCheckMode.Historical;
        if (value == CheckActiveOnly)
            return ProfessionGateCheckMode.ActiveOnly;
        return ProfessionGateCheckMode.Unknown;
    }
}
