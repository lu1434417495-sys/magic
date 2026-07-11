using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;

public sealed class BarrierProfileDefinition
{
    public BarrierProfileDefinition(
        StringName profileId,
        string displayName,
        StringName anchorMode,
        StringName areaPattern,
        int radiusCells,
        int durationTu,
        bool catchAllProjectedEffects,
        IReadOnlyList<BarrierLayerDefinition> layers
    )
    {
        ProfileId = profileId;
        DisplayName = displayName
            ?? throw new InvalidDataException(
                "BarrierProfileDefinition.DisplayName must not be null."
            );
        AnchorMode = anchorMode;
        AreaPattern = areaPattern;
        RadiusCells = radiusCells;
        DurationTu = durationTu;
        CatchAllProjectedEffects = catchAllProjectedEffects;
        Layers = ProgressionDefinitionProjection.FreezeValues(
            layers,
            "BarrierProfileDefinition.Layers"
        );
    }

    public StringName ProfileId { get; }
    public string DisplayName { get; }
    public StringName AnchorMode { get; }
    public StringName AreaPattern { get; }
    public int RadiusCells { get; }
    public int DurationTu { get; }
    public bool CatchAllProjectedEffects { get; }
    public IReadOnlyList<BarrierLayerDefinition> Layers { get; }

    internal BarrierAnchorMode AnchorModeKind => BarrierProfileDef.ToAnchorMode(AnchorMode);
    internal BattleAreaPattern AreaPatternKind => BattleTypedNames.ToAreaPattern(AreaPattern);

    public IReadOnlyList<BarrierLayerDefinition> GetOrderedLayers()
    {
        var result = new List<BarrierLayerDefinition>(Layers);
        result.Sort(static (left, right) => left.Order.CompareTo(right.Order));
        return result.Count == 0
            ? Array.Empty<BarrierLayerDefinition>()
            : new ReadOnlyCollection<BarrierLayerDefinition>(result);
    }

    internal static BarrierProfileDefinition FromResource(BarrierProfileDef source, string path)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new BarrierProfileDefinition(
            source.profile_id,
            source.display_name,
            source.anchor_mode,
            source.area_pattern,
            source.radius_cells,
            source.duration_tu,
            source.catch_all_projected_effects,
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.LayersProjectionBorrowed,
                path + ".layers",
                BarrierLayerDefinition.FromResource
            )
        );
    }
}
