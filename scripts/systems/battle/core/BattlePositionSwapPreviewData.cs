using Godot;

internal sealed class BattlePositionSwapPreviewData
{
    internal StringName SourceUnitId { get; init; } = "";
    internal StringName TargetUnitId { get; init; } = "";
    internal Vector2I SourceFrom { get; init; } = new(-1, -1);
    internal Vector2I SourceTo { get; init; } = new(-1, -1);
    internal Vector2I TargetFrom { get; init; } = new(-1, -1);
    internal Vector2I TargetTo { get; init; } = new(-1, -1);
    internal bool RequiresEnemySave { get; init; }
    internal int SaveDc { get; init; }
    internal StringName SaveAbility { get; init; } = "";
    internal StringName SaveTag { get; init; } = "";
    internal int SaveSuccessProbabilityBasisPoints { get; init; }
    internal int SwapProbabilityBasisPoints { get; init; }
    internal string SummaryText { get; init; } = "";

    internal BattlePositionSwapPreviewData Clone() =>
        new()
        {
            SourceUnitId = SourceUnitId,
            TargetUnitId = TargetUnitId,
            SourceFrom = SourceFrom,
            SourceTo = SourceTo,
            TargetFrom = TargetFrom,
            TargetTo = TargetTo,
            RequiresEnemySave = RequiresEnemySave,
            SaveDc = SaveDc,
            SaveAbility = SaveAbility,
            SaveTag = SaveTag,
            SaveSuccessProbabilityBasisPoints = SaveSuccessProbabilityBasisPoints,
            SwapProbabilityBasisPoints = SwapProbabilityBasisPoints,
            SummaryText = SummaryText ?? "",
        };
}
