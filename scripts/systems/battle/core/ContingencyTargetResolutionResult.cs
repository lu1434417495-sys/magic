using System;
using System.Collections.Generic;
using Godot;

internal sealed class ContingencyTargetResolutionResult
{
    private static readonly Vector2I MissingCell = new(-1, -1);
    private IReadOnlyList<Vector2I> _areaCells = Array.Empty<Vector2I>();

    internal static ContingencyTargetResolutionResult Failure(StringName reasonId) =>
        new()
        {
            Ok = false,
            ReasonId = reasonId,
            AreaCells = Array.Empty<Vector2I>(),
        };

    internal static ContingencyTargetResolutionResult UnitTarget(
        StringName targetUnitId,
        Vector2I targetCell
    ) =>
        new()
        {
            Ok = true,
            TargetUnitId = targetUnitId,
            TargetCell = targetCell,
            IsGroundTarget = false,
            AreaCells = Array.Empty<Vector2I>(),
        };

    internal static ContingencyTargetResolutionResult GroundTarget(
        Vector2I targetCell,
        IReadOnlyList<Vector2I> areaCells,
        bool movedOutsideCurrentDamageEvent = false
    ) =>
        new()
        {
            Ok = true,
            TargetCell = targetCell,
            IsGroundTarget = true,
            AreaCells = CopyCells(areaCells),
            MovedOutsideCurrentDamageEvent = movedOutsideCurrentDamageEvent,
        };

    internal bool Ok { get; init; }
    internal StringName ReasonId { get; init; } = "";
    internal StringName TargetUnitId { get; init; } = "";
    internal Vector2I TargetCell { get; init; } = MissingCell;
    internal bool IsGroundTarget { get; init; }
    internal IReadOnlyList<Vector2I> AreaCells
    {
        get => _areaCells;
        init => _areaCells = CopyCells(value);
    }
    internal bool MovedOutsideCurrentDamageEvent { get; init; }

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
