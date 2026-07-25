using System;
using System.Collections.Generic;
using Godot;

internal class BattleMovementService
{
    private static readonly StringName TraceMovePathGridResolve = "move_path:grid_resolve";
    private static readonly StringName TraceMovePathExtractPath = "move_path:extract_path";
    private static readonly StringName TraceMovePathSemanticCost = "move_path:semantic_cost";

    private WeakReference<BattleRuntimeModule> _runtimeRef;

    private BattleRuntimeModule _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<BattleRuntimeModule>(value) : null;
    }

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    internal void Dispose()
    {
        _runtime = null;
    }

    private void RecordActionIssued(BattleUnitState unit_state, StringName command_type, int ap_cost = 0)
    {
        _runtime?._record_action_issued(unit_state, command_type, ap_cost);
    }

    private void AppendChangedCoords(BattleEventBatch batch, IEnumerable<Vector2I> coords)
    {
        if (batch == null || coords == null)
        {
            return;
        }
        foreach (Vector2I coord in coords)
        {
            AppendChangedCoord(batch, coord);
        }
    }

    private void AppendChangedUnitCoords(BattleEventBatch batch, BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return;
        }
        AppendChangedCoords(
            batch,
            unit_state.GetOccupiedCoordsReadViewTyped()
        );
    }

    internal IReadOnlyList<Vector2I> SortCoords(IEnumerable<Vector2I> target_coords)
    {
        var coords = CloneCoords(target_coords);
        coords.Sort(CompareCoordsYThenX);
        return coords;
    }

    private bool IsMovementBlocked(BattleUnitState unit_state)
    {
        return _runtime != null && _runtime._is_movement_blocked(unit_state);
    }

    internal bool IsMovementBlocked(BattleUnitReadView unitView)
    {
        return unitView.IsValid
            && (
                unitView.HasStatusEffect(BattleStatusSemanticTable.STATUS_PINNED)
                || unitView.HasStatusEffect(BattleStatusSemanticTable.STATUS_ROOTED)
                || unitView.HasStatusEffect(BattleStatusSemanticTable.STATUS_TENDON_CUT)
                || unitView.HasStatusEffect(BattleStatusSemanticTable.STATUS_PETRIFIED)
                || unitView.HasStatusEffect(BattleStatusSemanticTable.STATUS_PARALYZED)
                || unitView.HasStatusEffect(BattleStatusSemanticTable.STATUS_TIME_STASIS)
            );
    }

    private bool HasStatus(BattleUnitState unit_state, StringName status_id)
    {
        return _runtime != null && _runtime._has_status(unit_state, status_id);
    }

    internal IReadOnlyList<Vector2I> GetUnitReachableMoveCoords(BattleUnitState unit_state)
    {
        BattleState state = State;
        BattleGridService gridService = GridService;
        if (state == null || gridService == null || unit_state == null || !unit_state.IsAlive())
        {
            return Array.Empty<Vector2I>();
        }
        if (IsMovementBlocked(unit_state))
        {
            return Array.Empty<Vector2I>();
        }

        Vector2I origin = unit_state.GetAnchorCoord();
        int maxMovePoints = GetAvailableMovePoints(unit_state);
        var bestCoordCosts = new Dictionary<Vector2I, int>
        {
            [origin] = 0,
        };
        List<List<ReachableFrontierEntry>> buckets = BuildReachableBuckets(maxMovePoints);
        buckets[0].Add(new ReachableFrontierEntry(origin, 0));

        for (int currentCost = 0; currentCost <= maxMovePoints; currentCost++)
        {
            int bucketIndex = 0;
            while (bucketIndex < buckets[currentCost].Count)
            {
                ReachableFrontierEntry frontierEntry = buckets[currentCost][bucketIndex];
                bucketIndex++;
                Vector2I currentCoord = frontierEntry.Coord;
                int spentCost = frontierEntry.SpentCost;
                if (spentCost != currentCost)
                {
                    continue;
                }
                if (!bestCoordCosts.TryGetValue(currentCoord, out int bestCost) || spentCost != bestCost)
                {
                    continue;
                }
                foreach (Vector2I neighborCoord in GetNeighbors4(state, gridService, currentCoord))
                {
                    if (!CanUnitStepBetweenAnchors(state, gridService, unit_state, currentCoord, neighborCoord))
                    {
                        continue;
                    }
                    int moveCost = GetMoveCostForUnitTarget(unit_state, neighborCoord);
                    int nextCost = spentCost + moveCost;
                    if (nextCost > maxMovePoints)
                    {
                        continue;
                    }
                    if (bestCoordCosts.TryGetValue(neighborCoord, out int existingCost) && nextCost >= existingCost)
                    {
                        continue;
                    }
                    bestCoordCosts[neighborCoord] = nextCost;
                    buckets[nextCost].Add(new ReachableFrontierEntry(neighborCoord, nextCost));
                }
            }
        }

        bestCoordCosts.Remove(origin);
        var coords = new List<Vector2I>(bestCoordCosts.Keys);
        coords.Sort(CompareCoordsYThenX);
        return coords;
    }

    internal int GetMoveCostForUnitTarget(BattleUnitState unit_state, Vector2I target_coord)
    {
        BattleState state = State;
        BattleGridService gridService = GridService;
        if (state == null || gridService == null || unit_state == null)
        {
            return 1;
        }
        int moveCost = gridService.GetUnitMoveCost(state, unit_state, target_coord);
        BattleTerrainEffectSystem terrainEffectSystem = TerrainEffectSystem;
        if (terrainEffectSystem != null)
        {
            moveCost += terrainEffectSystem.GetMoveCostDeltaForUnitTarget(unit_state, target_coord);
        }
        moveCost += GetStatusMoveCostDelta(unit_state);
        return moveCost;
    }

    internal int GetMoveCostForUnitTarget(BattleUnitReadView unitView, Vector2I target_coord)
    {
        BattleState state = State;
        BattleGridService gridService = GridService;
        if (state == null || gridService == null || !unitView.IsValid)
        {
            return 1;
        }
        int moveCost = gridService.GetUnitMoveCost(state, unitView, target_coord);
        BattleTerrainEffectSystem terrainEffectSystem = TerrainEffectSystem;
        if (terrainEffectSystem != null)
        {
            moveCost += terrainEffectSystem.GetMoveCostDeltaForUnitTarget(unitView, target_coord);
        }
        moveCost += unitView.GetStatusMoveCostDelta();
        return moveCost;
    }

    private int GetMoveCostForUnitId(StringName unit_id, Vector2I _from_coord, Vector2I target_coord)
    {
        BattleState state = State;
        BattleUnitState unitState = state?.GetUnit(unit_id);
        return unitState == null ? 1 : GetMoveCostForUnitTarget(unitState, target_coord);
    }

    internal int GetMovePathCost(BattleUnitState unit_state, IReadOnlyList<Vector2I> anchor_path)
    {
        if (unit_state == null || anchor_path == null || anchor_path.Count <= 1)
        {
            return 0;
        }
        int totalCost = 0;
        for (int pathIndex = 1; pathIndex < anchor_path.Count; pathIndex++)
        {
            totalCost += GetMoveCostForUnitTarget(unit_state, anchor_path[pathIndex]);
        }
        return totalCost;
    }

    internal int GetMovePathCost(BattleUnitReadView unitView, IReadOnlyList<Vector2I> anchor_path)
    {
        if (!unitView.IsValid || anchor_path == null || anchor_path.Count <= 1)
        {
            return 0;
        }
        int totalCost = 0;
        for (int pathIndex = 1; pathIndex < anchor_path.Count; pathIndex++)
        {
            totalCost += GetMoveCostForUnitTarget(unitView, anchor_path[pathIndex]);
        }
        return totalCost;
    }

    internal int GetStatusMoveCostDelta(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return 0;
        }
        int totalDelta = 0;
        foreach (StringName statusId in unit_state.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState statusEntry = unit_state.GetStatusEffect(statusId);
            totalDelta += BattleStatusSemanticTable.GetMoveCostDelta(statusEntry);
        }
        return Math.Max(totalDelta, 0);
    }

    internal BattleMovePathResult ResolveMovePathResultTyped(BattleUnitState active_unit, Vector2I target_coord)
    {
        BattleState state = State;
        BattleGridService gridService = GridService;
        if (state == null || gridService == null || active_unit == null)
        {
            return new BattleMovePathResult
            {
                Allowed = false,
                Cost = 0,
                Path = System.Array.Empty<Vector2I>(),
                Message = "当前单位数据不可用。",
            };
        }

        int availableMovePoints = GetAvailableMovePoints(active_unit);
        if (availableMovePoints <= 0)
        {
            return new BattleMovePathResult
            {
                Allowed = false,
                Cost = 0,
                Path = System.Array.Empty<Vector2I>(),
                Message = IsNormalMovementLocked(active_unit) ? "已行动，移动力被锁定。" : "移动力不足，无法移动。",
            };
        }

        BattleMovePathResult moveResult;
        TraceEnter(TraceMovePathGridResolve);
        try
        {
            moveResult = gridService.ResolveUnitMovePathTyped(
                state,
                active_unit,
                active_unit.GetAnchorCoord(),
                target_coord,
                availableMovePoints,
                GetMoveCostForUnitTarget);
        }
        finally
        {
            TraceExit(TraceMovePathGridResolve);
        }

        if (moveResult.Path.Count > 1)
        {
            TraceEnter(TraceMovePathSemanticCost);
            try
            {
                int semanticCost = GetMovePathCost(active_unit, moveResult.Path);
                if (semanticCost > availableMovePoints)
                {
                    moveResult = new BattleMovePathResult
                    {
                        Allowed = false,
                        Cost = semanticCost,
                        Path = moveResult.Path,
                        Message = "移动力不足，无法移动。",
                    };
                }
                else if (semanticCost != moveResult.Cost)
                {
                    moveResult = new BattleMovePathResult
                    {
                        Allowed = moveResult.Allowed,
                        Cost = semanticCost,
                        Path = moveResult.Path,
                        Message = moveResult.Message,
                    };
                }
            }
            finally
            {
                TraceExit(TraceMovePathSemanticCost);
            }
        }
        return moveResult;
    }

    internal BattleMovePathResult ResolveMovePathResultTyped(
        BattleUnitReadView activeUnit,
        Vector2I target_coord
    )
    {
        BattleState state = State;
        BattleGridService gridService = GridService;
        if (state == null || gridService == null || !activeUnit.IsValid)
        {
            return new BattleMovePathResult
            {
                Allowed = false,
                Cost = 0,
                Path = System.Array.Empty<Vector2I>(),
                Message = "当前单位数据不可用。",
            };
        }

        int availableMovePoints = GetAvailableMovePoints(activeUnit);
        if (availableMovePoints <= 0)
        {
            return new BattleMovePathResult
            {
                Allowed = false,
                Cost = 0,
                Path = System.Array.Empty<Vector2I>(),
                Message = IsNormalMovementLocked(activeUnit) ? "已行动，移动力被锁定。" : "移动力不足，无法移动。",
            };
        }

        BattleMovePathResult moveResult;
        TraceEnter(TraceMovePathGridResolve);
        try
        {
            moveResult = gridService.ResolveUnitMovePathTyped(
                state,
                activeUnit,
                activeUnit.Coord,
                target_coord,
                availableMovePoints,
                GetMoveCostForUnitTarget);
        }
        finally
        {
            TraceExit(TraceMovePathGridResolve);
        }

        if (moveResult.Path.Count > 1)
        {
            TraceEnter(TraceMovePathSemanticCost);
            try
            {
                int semanticCost = GetMovePathCost(activeUnit, moveResult.Path);
                if (semanticCost > availableMovePoints)
                {
                    moveResult = new BattleMovePathResult
                    {
                        Allowed = false,
                        Cost = semanticCost,
                        Path = moveResult.Path,
                        Message = "移动力不足，无法移动。",
                    };
                }
                else if (semanticCost != moveResult.Cost)
                {
                    moveResult = new BattleMovePathResult
                    {
                        Allowed = moveResult.Allowed,
                        Cost = semanticCost,
                        Path = moveResult.Path,
                        Message = moveResult.Message,
                    };
                }
            }
            finally
            {
                TraceExit(TraceMovePathSemanticCost);
            }
        }
        return moveResult;
    }

    internal int GetAvailableMovePoints(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return 0;
        }
        int normalMovePoints = Math.Max(unit_state.GetCurrentMovePoints(), 0);
        if (normalMovePoints <= 0)
        {
            return 0;
        }
        if (!IsNormalMovementLocked(unit_state))
        {
            return normalMovePoints;
        }
        return unit_state.CanUseLockedMovePointsThisTurnTyped() ? normalMovePoints : 0;
    }

    internal int GetAvailableMovePoints(BattleUnitReadView unitView)
    {
        if (!unitView.IsValid)
        {
            return 0;
        }
        int normalMovePoints = Math.Max(unitView.CurrentMovePoints, 0);
        if (normalMovePoints <= 0)
        {
            return 0;
        }
        if (!IsNormalMovementLocked(unitView))
        {
            return normalMovePoints;
        }
        return unitView.CanUseLockedMovePointsThisTurn ? normalMovePoints : 0;
    }

    internal bool IsNormalMovementLocked(BattleUnitState unit_state)
    {
        return unit_state?.IsNormalMovementLockedThisTurnTyped() ?? false;
    }

    internal bool IsNormalMovementLocked(BattleUnitReadView unitView)
    {
        return unitView.IsValid && (unitView.HasTakenActionThisTurn || unitView.HasMovedThisTurn);
    }

    internal void HandleMoveCommand(BattleUnitState active_unit, BattleCommand command, BattleEventBatch batch)
    {
        if (active_unit == null || command == null)
        {
            return;
        }
        if (IsMovementBlocked(active_unit))
        {
            AppendLog(batch, $"{active_unit.display_name} 当前被限制移动。");
            return;
        }

        Vector2I targetCoord = command.target_coord;
        BattleMovePathResult moveResult = ResolveMovePathResultTyped(active_unit, targetCoord);
        if (!moveResult.Allowed)
        {
            AppendLog(batch, string.IsNullOrEmpty(moveResult.Message) ? "该移动不可执行。" : moveResult.Message);
            return;
        }

        BattleCellState targetCell = GetCell(targetCoord);
        if (targetCell == null)
        {
            return;
        }

        int moveCost = moveResult.Cost;
        IReadOnlyList<Vector2I> anchorPath = moveResult.Path;

        Vector2I previousAnchor = active_unit.GetAnchorCoord();
        List<Vector2I> previousCoords = CloneCoords(
            active_unit.GetOccupiedCoordsReadViewTyped()
        );
        BattleValidatedMoveExecutionResult executionResult =
            MoveUnitAlongValidatedPathTyped(active_unit, anchorPath, targetCoord, batch);
        if (executionResult.Executed)
        {
            moveCost = GetMovePathCost(active_unit, executionResult.ExecutedPath);
            active_unit.SetCurrentMovePoints(active_unit.GetCurrentMovePoints() - moveCost);
            RecordActionIssued(active_unit, BattleTypedNames.ToStringName(BattleCommandKind.Move));
            if (batch != null)
            {
                batch.AddChangedUnitId(active_unit.unit_id);
            }
            AppendChangedCoords(batch, previousCoords);
            AppendChangedUnitCoords(batch, active_unit);

            targetCell = GetCell(active_unit.GetAnchorCoord());
            string terrainName = targetCell != null
                ? GridService.GetTerrainDisplayName(targetCell.base_terrain.ToString())
                : "地格";
            AppendLog(
                batch,
                $"{active_unit.display_name} 从 ({previousAnchor.X}, {previousAnchor.Y}) 移动到 ({active_unit.GetAnchorCoord().X}, {active_unit.GetAnchorCoord().Y})，移动距离消耗 {moveCost} 点，剩余移动力 {active_unit.GetCurrentMovePoints()} 点并锁定。{terrainName}。");
            _runtime?.EmitContingencyPositionChanged(
                active_unit,
                previousAnchor,
                active_unit.GetAnchorCoord(),
                _runtime.AllocateContingencySourceEventId("position_changed")
            );
            if (executionResult.StoppedByBarrier)
            {
                AppendLog(batch, $"{active_unit.display_name} 的移动被屏障拦下，停在当前可达位置。");
            }
        }
        else
        {
            AppendLog(batch, $"{active_unit.display_name} 的移动落点已失效，无法执行。");
        }
    }

    internal BattleValidatedMoveExecutionResult MoveUnitAlongValidatedPathTyped(BattleUnitState active_unit, IReadOnlyList<Vector2I> anchor_path, Vector2I target_coord, BattleEventBatch batch)
    {
        var result = new BattleValidatedMoveExecutionResult();
        if (active_unit == null || anchor_path == null || anchor_path.Count == 0)
        {
            return result;
        }

        List<Vector2I> path = CloneCoords(anchor_path);
        if (
            path.Count == 0
            || path[0] != active_unit.GetAnchorCoord()
            || path[^1] != target_coord
        )
        {
            return result;
        }

        List<Vector2I> executedPath = result.ExecutedPath;
        executedPath.Add(active_unit.GetAnchorCoord());
        if (path.Count == 1)
        {
            bool reachedCurrentTarget =
                active_unit.GetAnchorCoord() == target_coord;
            result.Executed = reachedCurrentTarget;
            result.ReachedTarget = reachedCurrentTarget;
            return result;
        }

        BattleState state = State;
        BattleGridService gridService = GridService;
        if (state == null || gridService == null)
        {
            return result;
        }

        for (int pathIndex = 1; pathIndex < path.Count; pathIndex++)
        {
            Vector2I nextCoord = path[pathIndex];
            if (
                !CanUnitStepBetweenAnchors(
                    state,
                    gridService,
                    active_unit,
                    active_unit.GetAnchorCoord(),
                    nextCoord
                )
            )
            {
                AppendLog(batch, $"{active_unit.display_name} 的移动路径第 {pathIndex} 步已不可通行。");
                return result;
            }

            BattleBarrierInteractionResult barrierResult = new(false, false);
            BattleLayeredBarrierService layeredBarrierService = LayeredBarrierService;
            if (layeredBarrierService != null)
            {
                barrierResult = layeredBarrierService.ResolveUnitBoundaryCrossingResult(
                    active_unit,
                    active_unit.GetAnchorCoord(),
                    nextCoord,
                    batch
                );
            }
            if (
                barrierResult.Blocked
                || !active_unit.IsAlive()
                || active_unit.GetAnchorCoord() != path[pathIndex - 1]
            )
            {
                result.Executed = executedPath.Count > 1;
                result.StoppedByBarrier = barrierResult.Blocked;
                return result;
            }

            if (!gridService.MoveUnit(state, active_unit, nextCoord))
            {
                AppendLog(batch, $"{active_unit.display_name} 的移动路径第 {pathIndex} 步执行失败。");
                return result;
            }
            result.Executed = true;
            executedPath.Add(active_unit.GetAnchorCoord());
            TerrainEffectSystem?.ApplyContactEffectsForUnit(
                active_unit,
                BattleSaveContext.Empty,
                batch
            );
            if (!active_unit.IsAlive())
            {
                return result;
            }
        }

        result.ReachedTarget =
            active_unit.GetAnchorCoord() == target_coord;
        return result;
    }

    private readonly record struct ReachableFrontierEntry(Vector2I Coord, int SpentCost);

    private BattleState State => _runtime?._state;
    private BattleGridService GridService => _runtime?._grid_service;
    private BattleTerrainEffectSystem TerrainEffectSystem => _runtime?._terrain_effect_system;
    private BattleLayeredBarrierService LayeredBarrierService => _runtime?._layered_barrier_service;

    private BattleCellState GetCell(Vector2I coord)
    {
        BattleGridService gridService = GridService;
        BattleState state = State;
        return gridService == null || state == null ? null : gridService.GetCellState(state, coord);
    }

    private static bool CanUnitStepBetweenAnchors(
        BattleState state,
        BattleGridService gridService,
        BattleUnitState unitState,
        Vector2I fromCoord,
        Vector2I toCoord)
    {
        return gridService != null
            && state != null
            && unitState != null
            && gridService.CanUnitStepBetweenAnchors(state, unitState, fromCoord, toCoord);
    }

    private static IEnumerable<Vector2I> GetNeighbors4(BattleState state, BattleGridService gridService, Vector2I coord)
    {
        if (state == null || gridService == null)
        {
            yield break;
        }
        foreach (Vector2I rawCoord in gridService.GetNeighbors4(state, coord))
        {
            yield return rawCoord;
        }
    }

    private static List<List<ReachableFrontierEntry>> BuildReachableBuckets(int maxMovePoints)
    {
        int bucketCount = Math.Max(maxMovePoints, 0) + 1;
        var buckets = new List<List<ReachableFrontierEntry>>(bucketCount);
        for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
        {
            buckets.Add(new List<ReachableFrontierEntry>());
        }
        return buckets;
    }

    private static List<Vector2I> CloneCoords(IEnumerable<Vector2I> source)
    {
        var result = new List<Vector2I>();
        if (source == null)
        {
            return result;
        }
        foreach (Vector2I coord in source)
        {
            result.Add(coord);
        }
        return result;
    }

    private static int CompareCoordsYThenX(Vector2I left, Vector2I right)
    {
        int yCompare = left.Y.CompareTo(right.Y);
        return yCompare != 0 ? yCompare : left.X.CompareTo(right.X);
    }

    private static void AppendChangedCoord(BattleEventBatch batch, Vector2I coord)
    {
        if (!batch.ContainsChangedCoord(coord))
        {
            batch.AddChangedCoord(coord);
        }
    }

    private static void AppendLog(BattleEventBatch batch, string line)
    {
        if (batch == null || string.IsNullOrEmpty(line))
        {
            return;
        }
        batch.AddLogLine(line);
    }

    private static void TraceEnter(StringName name)
    {
        AiTraceRecorder.Enter(name);
    }

    private static void TraceExit(StringName name)
    {
        AiTraceRecorder.Exit(name);
    }

    private static BattleRuntimeModule ResolveWeakRef(WeakReference<BattleRuntimeModule> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out BattleRuntimeModule target))
        {
            return null;
        }
        return target;
    }
}
