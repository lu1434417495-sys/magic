using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleForcedMoveTargetPreviewData
{
    internal StringName TargetUnitId { get; init; } = "";
    internal string TargetDisplayName { get; init; } = "";
    internal Vector2I SourceCoord { get; init; } = new(-1, -1);
    internal Vector2I DestinationCoord { get; init; } = new(-1, -1);
    internal int Distance { get; init; }
    internal int MaximumDistance { get; init; }
    internal int TargetBodySize { get; init; }
    internal int MaximumTargetBodySize { get; init; }
    internal int SaveDc { get; init; }
    internal StringName SaveAbility { get; init; } = "";
    internal StringName SaveTag { get; init; } = "";
    internal int SaveSuccessProbabilityBasisPoints { get; init; }
    internal int SaveFailureProbabilityBasisPoints { get; init; }
    internal bool CanMoveOnFailedSave { get; init; }
    internal string BlockReason { get; init; } = "";

    internal BattleForcedMoveTargetPreviewData Clone() => (BattleForcedMoveTargetPreviewData)MemberwiseClone();
}

internal sealed class BattleForcedMovePreviewData
{
    internal StringName Mode { get; init; } = "";
    internal StringName TargetUnitId { get; init; } = "";
    internal Vector2I SourceCoord { get; init; } = new(-1, -1);
    internal Vector2I DestinationCoord { get; init; } = new(-1, -1);
    internal int Distance { get; init; }
    internal int MaximumDistance { get; init; }
    internal int TargetBodySize { get; init; }
    internal int MaximumTargetBodySize { get; init; }
    internal bool IgnoresIntermediateUnits { get; init; }
    internal bool IgnoresHeightDifference { get; init; }
    internal bool AppliesLandingContact { get; init; }
    internal bool AppliesContactPerEnteredCell { get; init; }
    internal IReadOnlyList<BattleForcedMoveTargetPreviewData> Targets { get; init; } =
        Array.Empty<BattleForcedMoveTargetPreviewData>();
    internal string SummaryText { get; init; } = "";

    internal BattleForcedMovePreviewData Clone() =>
        new()
        {
            Mode = Mode,
            TargetUnitId = TargetUnitId,
            SourceCoord = SourceCoord,
            DestinationCoord = DestinationCoord,
            Distance = Distance,
            MaximumDistance = MaximumDistance,
            TargetBodySize = TargetBodySize,
            MaximumTargetBodySize = MaximumTargetBodySize,
            IgnoresIntermediateUnits = IgnoresIntermediateUnits,
            IgnoresHeightDifference = IgnoresHeightDifference,
            AppliesLandingContact = AppliesLandingContact,
            AppliesContactPerEnteredCell = AppliesContactPerEnteredCell,
            Targets = CloneTargets(Targets),
            SummaryText = SummaryText,
        };

    private static IReadOnlyList<BattleForcedMoveTargetPreviewData> CloneTargets(
        IReadOnlyList<BattleForcedMoveTargetPreviewData> targets
    )
    {
        if (targets == null || targets.Count == 0)
            return Array.Empty<BattleForcedMoveTargetPreviewData>();
        var result = new List<BattleForcedMoveTargetPreviewData>(targets.Count);
        foreach (BattleForcedMoveTargetPreviewData target in targets)
        {
            if (target != null)
                result.Add(target.Clone());
        }
        return result.AsReadOnly();
    }
}
