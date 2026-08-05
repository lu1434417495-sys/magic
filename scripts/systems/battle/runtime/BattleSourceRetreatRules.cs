using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class BattleSourceRetreatPlan
{
    private static readonly Vector2I InvalidCoord = new(-1, -1);

    internal BattleSourceRetreatPlan(
        bool allowed,
        int requestedDistance,
        IReadOnlyList<Vector2I> path,
        string message,
        bool stoppedByObstacle
    )
    {
        Allowed = allowed;
        RequestedDistance = Math.Max(requestedDistance, 0);
        Path = new ReadOnlyCollection<Vector2I>(
            new List<Vector2I>(path ?? Array.Empty<Vector2I>())
        );
        Message = message ?? "";
        StoppedByObstacle = stoppedByObstacle;
    }

    internal bool Allowed { get; }
    internal int RequestedDistance { get; }
    internal IReadOnlyList<Vector2I> Path { get; }
    internal string Message { get; }
    internal bool StoppedByObstacle { get; }
    internal int ReachableDistance => Math.Max(Path.Count - 1, 0);
    internal Vector2I FinalCoord => Path.Count > 0 ? Path[^1] : InvalidCoord;

    internal static BattleSourceRetreatPlan Denied(string message) =>
        new(false, 0, Array.Empty<Vector2I>(), message, false);
}

internal static class BattleSourceRetreatRules
{
    private static readonly IReadOnlyList<Vector2I> Directions =
        Array.AsReadOnly(
            new[]
            {
                Vector2I.Up,
                Vector2I.Right,
                Vector2I.Down,
                Vector2I.Left,
            }
        );

    internal static IReadOnlyList<Vector2I> CardinalDirections => Directions;

    internal static CombatEffectDefinition FindEffect(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (
            CombatEffectDefinition effectDefinition
            in effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition?.EffectKind == BattleEffectKind.SourceRetreat)
                return effectDefinition;
        }
        return null;
    }

    internal static bool HasEffect(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    ) => FindEffect(effectDefinitions) != null;

    internal static bool IsExactCardinalDirection(Vector2I direction) =>
        direction == Vector2I.Up
        || direction == Vector2I.Right
        || direction == Vector2I.Down
        || direction == Vector2I.Left;

    internal static bool TryResolveSelectionDirection(
        Vector2I sourceCoord,
        Vector2I selectedCoord,
        out Vector2I direction
    )
    {
        direction = Vector2I.Zero;
        Vector2I delta = selectedCoord - sourceCoord;
        if (delta == Vector2I.Zero || (delta.X != 0 && delta.Y != 0))
            return false;
        direction =
            delta.X != 0
                ? new Vector2I(Math.Sign(delta.X), 0)
                : new Vector2I(0, Math.Sign(delta.Y));
        return IsExactCardinalDirection(direction);
    }

    internal static bool IncreasesDistanceFromTarget(
        Vector2I sourceCoord,
        Vector2I targetCoord,
        Vector2I direction
    )
    {
        if (!IsExactCardinalDirection(direction))
            return false;
        int currentDistance = ManhattanDistance(sourceCoord, targetCoord);
        int nextDistance = ManhattanDistance(sourceCoord + direction, targetCoord);
        return nextDistance > currentDistance;
    }

    internal static BattleSourceRetreatPlan BuildPlan(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitState sourceUnit,
        Vector2I targetCoord,
        Vector2I direction,
        int distance,
        bool movementBlocked
    )
    {
        if (
            state == null
            || gridService == null
            || sourceUnit == null
            || !sourceUnit.IsAlive()
        )
            return BattleSourceRetreatPlan.Denied("后撤单位无效。");

        return BuildPlanCore(
            sourceUnit.GetAnchorCoord(),
            targetCoord,
            direction,
            distance,
            movementBlocked,
            (fromCoord, toCoord) =>
                gridService.CanUnitStepBetweenAnchors(
                    state,
                    sourceUnit,
                    fromCoord,
                    toCoord
                ),
            (fromCoord, toCoord) =>
                barrierService?.HasUnitBoundaryBarrier(
                    sourceUnit,
                    fromCoord,
                    toCoord
                ) == true
        );
    }

    internal static BattleSourceRetreatPlan BuildPlan(
        BattleState state,
        BattleGridService gridService,
        BattleLayeredBarrierService barrierService,
        BattleUnitReadView sourceUnit,
        Vector2I targetCoord,
        Vector2I direction,
        int distance,
        bool movementBlocked
    )
    {
        if (
            state == null
            || gridService == null
            || !sourceUnit.IsValid
            || !sourceUnit.IsAlive
        )
            return BattleSourceRetreatPlan.Denied("后撤单位无效。");

        return BuildPlanCore(
            sourceUnit.Coord,
            targetCoord,
            direction,
            distance,
            movementBlocked,
            (fromCoord, toCoord) =>
                gridService.CanUnitStepBetweenAnchors(
                    state,
                    sourceUnit,
                    fromCoord,
                    toCoord
                ),
            (fromCoord, toCoord) =>
                barrierService?.HasUnitBoundaryBarrier(
                    sourceUnit.UnsafeUnitForReadOnlyRules,
                    fromCoord,
                    toCoord
                ) == true
        );
    }

    private static BattleSourceRetreatPlan BuildPlanCore(
        Vector2I sourceCoord,
        Vector2I targetCoord,
        Vector2I direction,
        int distance,
        bool movementBlocked,
        Func<Vector2I, Vector2I, bool> canStep,
        Func<Vector2I, Vector2I, bool> hasBoundaryBarrier
    )
    {
        if (distance <= 0)
            return BattleSourceRetreatPlan.Denied("后撤距离必须大于0。");
        if (!IsExactCardinalDirection(direction))
            return BattleSourceRetreatPlan.Denied("后撤方向必须是上下左右之一。");
        if (!IncreasesDistanceFromTarget(sourceCoord, targetCoord, direction))
            return BattleSourceRetreatPlan.Denied("所选方向没有远离攻击目标。");
        if (movementBlocked)
            return BattleSourceRetreatPlan.Denied("当前被限制移动，无法使用后撤技能。");

        var path = new List<Vector2I> { sourceCoord };
        Vector2I currentCoord = sourceCoord;
        bool stoppedByObstacle = false;
        for (int step = 0; step < distance; step++)
        {
            Vector2I nextCoord = currentCoord + direction;
            if (
                canStep?.Invoke(currentCoord, nextCoord) != true
                || hasBoundaryBarrier?.Invoke(currentCoord, nextCoord) == true
            )
            {
                stoppedByObstacle = true;
                break;
            }
            path.Add(nextCoord);
            currentCoord = nextCoord;
        }

        return new BattleSourceRetreatPlan(
            true,
            distance,
            path,
            "",
            stoppedByObstacle
        );
    }

    private static int ManhattanDistance(Vector2I left, Vector2I right) =>
        Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
}
