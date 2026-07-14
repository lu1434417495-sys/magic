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

    internal static ProfessionRankGateDefinition FromResource(
        ProfessionRankGate source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ProgressionDefinitionProjection.RequireKnown(
            source.check_mode == ""
                || source.CheckModeKind != ProfessionGateCheckMode.Unknown,
            $"{path}.check_mode",
            source.check_mode
        );
        return new ProfessionRankGateDefinition(
            source.profession_id,
            source.min_rank,
            source.check_mode
        );
    }

    private static ProfessionGateCheckMode ToCheckMode(StringName value)
    {
        if (value == CheckHistorical)
            return ProfessionGateCheckMode.Historical;
        if (value == CheckActiveOnly)
            return ProfessionGateCheckMode.ActiveOnly;
        return ProfessionGateCheckMode.Unknown;
    }
}
