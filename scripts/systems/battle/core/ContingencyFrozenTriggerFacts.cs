using System;
using System.Collections.Generic;
using Godot;

internal sealed class ContingencyFrozenTriggerFacts
{
    private static readonly Vector2I MissingCell = new(-1, -1);
    private IReadOnlyList<Vector2I> _currentDamageEventAreaCells = Array.Empty<Vector2I>();

    internal static ContingencyFrozenTriggerFacts Empty { get; } = new();

    internal StringName TriggerSourceUnitId { get; init; } = "";
    internal Vector2I TriggerSourceCell { get; init; } = MissingCell;
    internal StringName TriggerTargetUnitId { get; init; } = "";
    internal Vector2I TriggerTargetCell { get; init; } = MissingCell;
    internal Vector2I TriggerCell { get; init; } = MissingCell;
    internal IReadOnlyList<Vector2I> CurrentDamageEventAreaCells
    {
        get => _currentDamageEventAreaCells;
        init => _currentDamageEventAreaCells = CopyCells(value);
    }
    internal bool FatalDamageIncoming { get; init; }

    internal bool HasTriggerSourceCell => TriggerSourceCell != MissingCell;
    internal bool HasTriggerTargetCell => TriggerTargetCell != MissingCell;
    internal bool HasTriggerCell => TriggerCell != MissingCell;

    private static IReadOnlyList<Vector2I> CopyCells(IReadOnlyList<Vector2I> cells)
    {
        if (cells == null || cells.Count == 0)
            return Array.Empty<Vector2I>();
        Vector2I[] copy = new Vector2I[cells.Count];
        for (int i = 0; i < cells.Count; i++)
            copy[i] = cells[i];
        return Array.AsReadOnly(copy);
    }
}
