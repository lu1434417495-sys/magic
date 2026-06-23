using System;
using System.Collections.Generic;
using Godot;

internal sealed class ContingencyFrozenTriggerFacts
{
    private static readonly Vector2I MissingCell = new(-1, -1);

    internal static ContingencyFrozenTriggerFacts Empty { get; } = new();

    internal StringName TriggerSourceUnitId { get; init; } = "";
    internal Vector2I TriggerSourceCell { get; init; } = MissingCell;
    internal StringName TriggerTargetUnitId { get; init; } = "";
    internal Vector2I TriggerTargetCell { get; init; } = MissingCell;
    internal Vector2I TriggerCell { get; init; } = MissingCell;
    internal IReadOnlyList<Vector2I> CurrentDamageEventAreaCells { get; init; } =
        Array.Empty<Vector2I>();
    internal bool FatalDamageIncoming { get; init; }

    internal bool HasTriggerSourceCell => TriggerSourceCell != MissingCell;
    internal bool HasTriggerTargetCell => TriggerTargetCell != MissingCell;
    internal bool HasTriggerCell => TriggerCell != MissingCell;
}
