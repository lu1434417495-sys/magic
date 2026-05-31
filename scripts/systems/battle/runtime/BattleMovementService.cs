using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

[GlobalClass]
public partial class BattleMovementService : RefCounted
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

    public void setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    public void dispose()
    {
        _runtime = null;
    }

    public void _record_action_issued(BattleUnitState unit_state, StringName command_type, int ap_cost = 0)
    {
        _runtime?._record_action_issued(unit_state, command_type, ap_cost);
    }

    public void _append_changed_coords(BattleEventBatch batch, GArray coords)
    {
        if (batch == null || coords == null)
        {
            return;
        }
        foreach (var rawCoord in coords)
        {
            AppendChangedCoord(batch, rawCoord.AsVector2I());
        }
    }

    public void _append_changed_coords(BattleEventBatch batch, GVector2IArray coords)
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

    public void _append_changed_unit_coords(BattleEventBatch batch, BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return;
        }
        unit_state.refresh_footprint();
        _append_changed_coords(batch, unit_state.occupied_coords);
    }

    public GVector2IArray _sort_coords(GArray target_coords)
    {
        var coords = ExtractVector2IList(target_coords);
        coords.Sort(CompareCoordsYThenX);
        return ToTypedVector2IArray(coords);
    }

    public bool _is_movement_blocked(BattleUnitState unit_state)
    {
        return _runtime != null && _runtime._is_movement_blocked(unit_state);
    }

    public bool _has_status(BattleUnitState unit_state, StringName status_id)
    {
        return _runtime != null && _runtime._has_status(unit_state, status_id);
    }

    public GVector2IArray get_unit_reachable_move_coords(BattleUnitState unit_state)
    {
        BattleState state = State;
        BattleGridService gridService = GridService;
        if (state == null || gridService == null || unit_state == null || !unit_state.is_alive)
        {
            return new GVector2IArray();
        }
        if (_is_movement_blocked(unit_state))
        {
            return new GVector2IArray();
        }

        Vector2I origin = unit_state.coord;
        int maxMovePoints = _get_available_move_points(unit_state);
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
                    int moveCost = _get_move_cost_for_unit_target(unit_state, neighborCoord);
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
        return ToTypedVector2IArray(coords);
    }

    public int _get_move_cost_for_unit_target(BattleUnitState unit_state, Vector2I target_coord)
    {
        BattleState state = State;
        BattleGridService gridService = GridService;
        if (state == null || gridService == null || unit_state == null)
        {
            return 1;
        }
        int moveCost = gridService.get_unit_move_cost(state, unit_state, target_coord);
        BattleTerrainEffectSystem terrainEffectSystem = TerrainEffectSystem;
        if (terrainEffectSystem != null)
        {
            moveCost += terrainEffectSystem.GetMoveCostDeltaForUnitTarget(unit_state, target_coord);
        }
        moveCost += _get_status_move_cost_delta(unit_state);
        return moveCost;
    }

    public int _get_move_cost_for_unit_id(StringName unit_id, Vector2I _from_coord, Vector2I target_coord)
    {
        BattleState state = State;
        if (state == null || !state.units.ContainsKey(unit_id))
        {
            return 1;
        }
        BattleUnitState unitState = state.units[unit_id].As<BattleUnitState>();
        return unitState == null ? 1 : _get_move_cost_for_unit_target(unitState, target_coord);
    }

    public int _get_move_path_cost(BattleUnitState unit_state, GArray anchor_path)
    {
        if (unit_state == null || anchor_path == null || anchor_path.Count <= 1)
        {
            return 0;
        }
        int totalCost = 0;
        for (int pathIndex = 1; pathIndex < anchor_path.Count; pathIndex++)
        {
            totalCost += _get_move_cost_for_unit_target(unit_state, anchor_path[pathIndex].AsVector2I());
        }
        return totalCost;
    }

    public int _get_status_move_cost_delta(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return 0;
        }
        var sortedStatusIds = new List<string>();
        foreach (var rawStatusId in unit_state.status_effects.Keys)
        {
            sortedStatusIds.Add(rawStatusId.ToString());
        }
        sortedStatusIds.Sort(StringComparer.Ordinal);

        int totalDelta = 0;
        foreach (string statusIdString in sortedStatusIds)
        {
            BattleStatusEffectState statusEntry = unit_state.get_status_effect(new StringName(statusIdString));
            totalDelta += BattleStatusSemanticTable.get_move_cost_delta(statusEntry);
        }
        return Math.Max(totalDelta, 0);
    }

    public BattleMovePathResult _resolve_move_path_result_typed(BattleUnitState active_unit, Vector2I target_coord)
    {
        BattleState state = State;
        BattleGridService gridService = GridService;
        if (state == null || gridService == null || active_unit == null)
        {
            return new BattleMovePathResult
            {
                Allowed = false,
                Cost = 0,
                Path = new GVector2IArray(),
                Message = "当前单位数据不可用。",
            };
        }

        int availableMovePoints = _get_available_move_points(active_unit);
        if (availableMovePoints <= 0)
        {
            return new BattleMovePathResult
            {
                Allowed = false,
                Cost = 0,
                Path = new GVector2IArray(),
                Message = _is_normal_movement_locked(active_unit) ? "已行动，移动力被锁定。" : "移动力不足，无法移动。",
            };
        }

        BattleMovePathResult moveResult;
        TraceEnter(TraceMovePathGridResolve);
        try
        {
            moveResult = gridService.resolve_unit_move_path_typed(
                state,
                active_unit,
                active_unit.coord,
                target_coord,
                availableMovePoints,
                _get_move_cost_for_unit_target);
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
                int semanticCost = _get_move_path_cost(active_unit, ToUntypedArray(moveResult.Path));
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

    public GDictionary _resolve_move_path_result(BattleUnitState active_unit, Vector2I target_coord)
    {
        return _resolve_move_path_result_typed(active_unit, target_coord).ToDictionary();
    }

    public int _get_available_move_points(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return 0;
        }
        int normalMovePoints = Math.Max(unit_state.current_move_points, 0);
        if (normalMovePoints <= 0)
        {
            return 0;
        }
        if (!_is_normal_movement_locked(unit_state))
        {
            return normalMovePoints;
        }
        return unit_state.can_use_locked_move_points_this_turn ? normalMovePoints : 0;
    }

    public bool _is_normal_movement_locked(BattleUnitState unit_state)
    {
        return unit_state != null && (unit_state.has_taken_action_this_turn || unit_state.has_moved_this_turn);
    }

    public void _handle_move_command(BattleUnitState active_unit, BattleCommand command, BattleEventBatch batch)
    {
        if (active_unit == null || command == null)
        {
            return;
        }
        if (_is_movement_blocked(active_unit))
        {
            AppendLog(batch, $"{active_unit.display_name} 当前被限制移动。");
            return;
        }

        Vector2I targetCoord = command.target_coord;
        BattleMovePathResult moveResult = _resolve_move_path_result_typed(active_unit, targetCoord);
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
        GArray anchorPath = ToUntypedArray(moveResult.Path);

        Vector2I previousAnchor = active_unit.coord;
        GVector2IArray previousCoords = CloneCoords(active_unit.occupied_coords);
        BattleValidatedMoveExecutionResult executionResult =
            MoveUnitAlongValidatedPathTyped(active_unit, anchorPath, targetCoord, batch);
        if (executionResult.Executed)
        {
            moveCost = _get_move_path_cost(active_unit, ToUntypedArray(executionResult.ExecutedPath));
            active_unit.current_move_points = Math.Max(active_unit.current_move_points - moveCost, 0);
            _record_action_issued(active_unit, BattleCommand.TYPE_MOVE());
            if (batch != null)
            {
                batch.changed_unit_ids.Add(active_unit.unit_id);
            }
            _append_changed_coords(batch, previousCoords);
            _append_changed_unit_coords(batch, active_unit);

            targetCell = GetCell(active_unit.coord);
            string terrainName = targetCell != null
                ? GridService.get_terrain_display_name(targetCell.base_terrain.ToString())
                : "地格";
            AppendLog(
                batch,
                $"{active_unit.display_name} 从 ({previousAnchor.X}, {previousAnchor.Y}) 移动到 ({active_unit.coord.X}, {active_unit.coord.Y})，移动距离消耗 {moveCost} 点，剩余移动力 {active_unit.current_move_points} 点并锁定。{terrainName}。");
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

    public bool _move_unit_along_validated_path(BattleUnitState active_unit, GArray anchor_path, Vector2I target_coord, BattleEventBatch batch)
    {
        return MoveUnitAlongValidatedPathTyped(
            active_unit,
            anchor_path,
            target_coord,
            batch
        ).ReachedTarget;
    }

    public GDictionary _move_unit_along_validated_path_result(BattleUnitState active_unit, GArray anchor_path, Vector2I target_coord, BattleEventBatch batch) =>
        MoveUnitAlongValidatedPathTyped(active_unit, anchor_path, target_coord, batch).ToDictionary();

    public BattleValidatedMoveExecutionResult MoveUnitAlongValidatedPathTyped(BattleUnitState active_unit, GArray anchor_path, Vector2I target_coord, BattleEventBatch batch)
    {
        var result = new BattleValidatedMoveExecutionResult();
        if (active_unit == null || anchor_path == null || anchor_path.Count == 0)
        {
            return result;
        }

        List<Vector2I> path = ExtractVector2IList(anchor_path);
        if (path.Count == 0 || path[0] != active_unit.coord || path[^1] != target_coord)
        {
            return result;
        }

        GVector2IArray executedPath = result.ExecutedPath;
        executedPath.Add(active_unit.coord);
        if (path.Count == 1)
        {
            bool reachedCurrentTarget = active_unit.coord == target_coord;
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
            if (!CanUnitStepBetweenAnchors(state, gridService, active_unit, active_unit.coord, nextCoord))
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
                    active_unit.coord,
                    nextCoord,
                    batch
                );
            }
            if (barrierResult.Blocked || !active_unit.is_alive || active_unit.coord != path[pathIndex - 1])
            {
                result.Executed = result.Executed
                    || barrierResult.Applied
                    || executedPath.Count > 1;
                result.StoppedByBarrier = barrierResult.Blocked;
                return result;
            }

            if (!gridService.move_unit(state, active_unit, nextCoord))
            {
                AppendLog(batch, $"{active_unit.display_name} 的移动路径第 {pathIndex} 步执行失败。");
                return result;
            }
            result.Executed = true;
            executedPath.Add(active_unit.coord);
        }

        result.ReachedTarget = active_unit.coord == target_coord;
        return result;
    }

    public GVector2IArray _collect_dict_vector2i_keys(GDictionary values)
    {
        var coords = new List<Vector2I>();
        if (values != null)
        {
            foreach (var rawCoord in values.Keys)
            {
                coords.Add(rawCoord.AsVector2I());
            }
        }
        return ToTypedVector2IArray(coords);
    }

    public GArray _build_reachable_move_buckets(int max_move_points)
    {
        int bucketCount = Math.Max(max_move_points, 0) + 1;
        var buckets = new GArray();
        for (int bucketIndex = 0; bucketIndex < bucketCount; bucketIndex++)
        {
            buckets.Add(new GArray());
        }
        return buckets;
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
        return gridService == null || state == null ? null : gridService.get_cell(state, coord);
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
            && gridService.can_unit_step_between_anchors(state, unitState, fromCoord, toCoord);
    }

    private static IEnumerable<Vector2I> GetNeighbors4(BattleState state, BattleGridService gridService, Vector2I coord)
    {
        if (state == null || gridService == null)
        {
            yield break;
        }
        foreach (Vector2I rawCoord in gridService.get_neighbors_4(state, coord))
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

    private static GArray ToUntypedArray(GVector2IArray source)
    {
        var result = new GArray();
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

    private static List<Vector2I> ExtractVector2IList(GArray values)
    {
        var coords = new List<Vector2I>();
        if (values == null)
        {
            return coords;
        }
        foreach (var rawCoord in values)
        {
            coords.Add(rawCoord.AsVector2I());
        }
        return coords;
    }

    private static GVector2IArray ToTypedVector2IArray(List<Vector2I> coords)
    {
        var result = new GVector2IArray();
        foreach (Vector2I coord in coords)
        {
            result.Add(coord);
        }
        return result;
    }

    private static GVector2IArray CloneCoords(GVector2IArray source)
    {
        var result = new GVector2IArray();
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
        if (!batch.changed_coords.Contains(coord))
        {
            batch.changed_coords.Add(coord);
        }
    }

    private static void AppendLog(BattleEventBatch batch, string line)
    {
        if (batch == null || string.IsNullOrEmpty(line))
        {
            return;
        }
        batch.log_lines.Add(line);
    }

    private static void TraceEnter(StringName name)
    {
        AiTraceRecorder.enter(name);
    }

    private static void TraceExit(StringName name)
    {
        AiTraceRecorder.exit(name);
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
