using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

internal sealed class BattleMovementQueryService : IDisposable
{
    private static readonly StringName QueryReachable = "reachable_anchors";
    private static readonly StringName QueryDistanceBand = "distance_band_destinations";
    private static readonly StringName QueryDistanceBandPathCost = "distance_band_path_cost";
    private static readonly StringName QueryPathTarget = "current_turn_path_target";
    private static readonly StringName EmptyStringName = "";
    private static readonly Vector2I InvalidCoord = new(-1, -1);
    private const int InfiniteCost = int.MaxValue / 4;

    private BattleState _state;
    private BattleGridService _gridService;
    private Func<StringName, Vector2I, Vector2I, int> _moveCostProvider;
    private Vector2I _mapSize = Vector2I.Zero;
    private CellInfo[] _cells = System.Array.Empty<CellInfo>();
    private readonly System.Collections.Generic.Dictionary<StringName, UnitInfo> _units = new();
    private readonly System.Collections.Generic.Dictionary<Vector3I, EdgeInfo> _edges = new();
    private readonly System.Collections.Generic.Dictionary<DistanceFromAnchorToTargetKey, int> _distanceFromAnchorToTargetCache =
        new();
    private readonly System.Collections.Generic.Dictionary<PathTargetQueryCacheKey, MovementPathTargetResult> _pathTargetQueryCache =
        new();
    private readonly System.Collections.Generic.Dictionary<StringName, long> _moveCostSignatureCache =
        new();
    private long _snapshotRevision = long.MinValue;

    private readonly record struct PathTargetQueryCacheKey(
        StringName UnitId,
        StringName FocusTargetId,
        long MoveCostSignature,
        int MinDistance,
        int MaxDistance,
        int MaxCost,
        int MaxNodes,
        int MaxDestinations,
        int PathTreeMinDestinationCount,
        bool IncludeOrigin,
        bool PreferProgress
    );

    private readonly record struct DistanceFromAnchorToTargetKey(
        Vector2I AnchorCoord,
        Vector2I AnchorFootprintSize,
        StringName TargetUnitId
    );

    private readonly struct CellInfo
    {
        public readonly bool Exists;
        public readonly StringName Terrain;
        public readonly StringName Occupant;

        public CellInfo(bool exists, StringName terrain, StringName occupant)
        {
            Exists = exists;
            Terrain = terrain;
            Occupant = occupant;
        }
    }

    private sealed class UnitInfo
    {
        public StringName UnitId = EmptyStringName;
        public Vector2I Coord = Vector2I.Zero;
        public Vector2I FootprintSize = Vector2I.One;
        public readonly HashSet<StringName> MovementTags = new();
        public readonly List<Vector2I> OccupiedCoords = new();
    }

    private readonly struct EdgeInfo
    {
        public readonly bool BlocksMove;
        public readonly bool BlocksOccupancy;
        public readonly int HeightDifference;

        public EdgeInfo(bool blocksMove, bool blocksOccupancy, int heightDifference)
        {
            BlocksMove = blocksMove;
            BlocksOccupancy = blocksOccupancy;
            HeightDifference = heightDifference;
        }
    }

    internal readonly record struct PathSearchBudget(
        int MaxCost,
        int MaxNodes,
        int MaxDestinations,
        int PathTreeMinDestinationCount,
        bool IncludeOrigin,
        bool PreferProgress
    )
    {
        public PathSearchBudgetSnapshot ToSnapshot()
        {
            return new PathSearchBudgetSnapshot(
                MaxCost,
                MaxNodes,
                MaxDestinations,
                PathTreeMinDestinationCount,
                IncludeOrigin,
                PreferProgress
            );
        }
    }

    internal readonly record struct PathSearchBudgetSnapshot(
        int MaxCost,
        int MaxNodes,
        int MaxDestinations,
        int PathTreeMinDestinationCount,
        bool IncludeOrigin,
        bool PreferProgress
    );

    internal sealed class MovementQueryOptions
    {
        public int MaxCandidateCount;
        public bool IncludeOrigin;
        public bool PreferProgress;
        public bool HasIncludeOrigin;
        public bool HasPreferProgress;

        public static MovementQueryOptions ForPathSearchBudget(
            int maxCandidateCount,
            bool includeOrigin,
            bool preferProgress
        )
        {
            return new MovementQueryOptions
            {
                MaxCandidateCount = Math.Max(maxCandidateCount, 0),
                IncludeOrigin = includeOrigin,
                PreferProgress = preferProgress,
                HasIncludeOrigin = true,
                HasPreferProgress = true,
            };
        }
    }

    private readonly record struct PathSearchResult(
        bool Ok,
        StringName RejectReason,
        List<Vector2I> Path,
        int Cost,
        int VisitedCount
    )
    {
        public static PathSearchResult Failure(StringName reason, int visitedCount = 0)
        {
            return new PathSearchResult(false, reason, new List<Vector2I>(), 0, visitedCount);
        }

        public static PathSearchResult Success(List<Vector2I> path, int cost, int visitedCount)
        {
            return new PathSearchResult(true, EmptyStringName, path, cost, visitedCount);
        }
    }

    private readonly record struct PathTargetCandidate(
        Vector2I DestinationCoord,
        Vector2I TargetCoord,
        int PathCost,
        int PathLength,
        int SpentCost,
        int DistanceToTarget,
        int DistanceGap
    );

    private readonly record struct PathSearchNode(Vector2I Coord, int Cost);

    private readonly record struct PathTargetNodeMeta(
        bool HasValue,
        int Cost,
        Vector2I LandingCoord,
        int SpentCost,
        int PathLength,
        Vector2I ParentCoord
    );

    private sealed class PathSearchTree
    {
        public readonly System.Collections.Generic.Dictionary<Vector2I, int> Costs = new();
        public readonly System.Collections.Generic.Dictionary<Vector2I, Vector2I> Previous = new();
        public int VisitedCount;
        public bool BudgetExhausted;
        public bool InvalidMoveCost;
    }

    private sealed class PathTargetSearchResult
    {
        public readonly List<PathTargetCandidate> Candidates = new();
        public int VisitedCount;
        public int ReachedDestinationCount;
        public int SkippedOriginCount;
        public int RelaxCount;
        public bool BudgetExhausted;
        public bool InvalidMoveCost;
    }

    internal void Setup(
        BattleState state,
        BattleGridService grid_service,
        Func<StringName, Vector2I, Vector2I, int> move_cost_provider
        )
    {
        bool referencesChanged =
            !ReferenceEquals(_state, state)
            || !ReferenceEquals(_gridService, grid_service)
            || !ReferenceEquals(_moveCostProvider, move_cost_provider);
        _state = state;
        _gridService = grid_service;
        _moveCostProvider = move_cost_provider;
        _moveCostSignatureCache.Clear();
        if (referencesChanged)
        {
            _snapshotRevision = long.MinValue;
        }
        using (new BattleAiTraceSpan("movement_query_setup:ensure_snapshot"))
        {
            EnsureSnapshotFresh();
        }
    }

    internal void ClearRuntimeBindings()
    {
        _state = null;
        _gridService = null;
        _moveCostProvider = null;
        _mapSize = Vector2I.Zero;
        _cells = System.Array.Empty<CellInfo>();
        _units.Clear();
        _edges.Clear();
        _distanceFromAnchorToTargetCache.Clear();
        _pathTargetQueryCache.Clear();
        _moveCostSignatureCache.Clear();
        _snapshotRevision = long.MinValue;
    }

    internal void DisposeRuntime() => ClearRuntimeBindings();

    public void Dispose() => ClearRuntimeBindings();

    internal MovementReachabilityResult CollectReachableAnchors(
        StringName unit_id,
        Vector2I from_coord,
        int max_cost,
        BattleVirtualBoardOverlay overlay = null,
        MovementQueryOptions options = null
    )
    {
        EnsureSnapshotFresh();
        if (!TryGetUnit(unit_id, out UnitInfo unit))
        {
            return MovementReachabilityResult.Failure(QueryReachable, "missing_unit");
        }
        MovementQueryOptions queryOptions = options ?? new MovementQueryOptions();
        if (!TryResolveBudget(unit, queryOptions, out PathSearchBudget budget))
        {
            return MovementReachabilityResult.Failure(QueryReachable, "invalid_options");
        }

        bool includeOrigin = budget.IncludeOrigin;
        int maxDestinations = budget.MaxDestinations;
        int costLimit = Math.Max(max_cost, 0);
        var frontier = new Queue<Vector2I>();
        var costs = new System.Collections.Generic.Dictionary<Vector2I, int>();
        var coords = new List<Vector2I>();
        frontier.Enqueue(from_coord);
        costs[from_coord] = 0;
        int visitedCount = 0;
        if (includeOrigin)
        {
            coords.Add(from_coord);
        }

        while (frontier.Count > 0)
        {
            Vector2I current = frontier.Dequeue();
            visitedCount++;
            int currentCost = costs[current];
            foreach (Vector2I neighbor in GetNeighbors4(current))
            {
                if (!CanStep(unit, current, neighbor, overlay))
                {
                    continue;
                }
                int stepCost = GetMoveCost(unit.UnitId, current, neighbor);
                if (stepCost <= 0)
                {
                    return MovementReachabilityResult.Failure(QueryReachable, "invalid_move_cost");
                }
                int nextCost = currentCost + stepCost;
                if (nextCost > costLimit)
                {
                    continue;
                }
                if (costs.TryGetValue(neighbor, out int existingCost) && nextCost >= existingCost)
                {
                    continue;
                }
                costs[neighbor] = nextCost;
                frontier.Enqueue(neighbor);
                if (!coords.Contains(neighbor))
                {
                    coords.Add(neighbor);
                    if (maxDestinations > 0 && coords.Count >= maxDestinations)
                    {
                        return ReachabilitySuccess(
                            QueryReachable,
                            coords,
                            InvalidCoord,
                            new List<Vector2I>(),
                            0,
                            visitedCount,
                            false,
                            overlay
                        );
                    }
                }
            }
        }

        coords.Sort(
            (left, right) =>
            {
                int distanceCompare = GetDistance(from_coord, right)
                    .CompareTo(GetDistance(from_coord, left));
                if (distanceCompare != 0)
                {
                    return distanceCompare;
                }
                int yCompare = left.Y.CompareTo(right.Y);
                return yCompare != 0 ? yCompare : left.X.CompareTo(right.X);
            }
        );
        return ReachabilitySuccess(
            QueryReachable,
            coords,
            InvalidCoord,
            new List<Vector2I>(),
            0,
            visitedCount,
            false,
            overlay
        );
    }

    internal MovementDistanceBandResult CollectDistanceBandDestinations(
        StringName unit_id,
        StringName focus_target_id,
        int min_distance,
        int max_distance,
        BattleVirtualBoardOverlay overlay = null,
        MovementQueryOptions options = null
    )
    {
        EnsureSnapshotFresh();
        if (
            !TryGetUnit(unit_id, out UnitInfo unit)
            || !TryGetUnit(focus_target_id, out UnitInfo target)
        )
        {
            return MovementDistanceBandResult.Failure(QueryDistanceBand, "missing_unit");
        }
        MovementQueryOptions queryOptions = options ?? new MovementQueryOptions();
        if (!TryResolveBudget(unit, queryOptions, out PathSearchBudget budget))
        {
            return MovementDistanceBandResult.Failure(QueryDistanceBand, "invalid_options");
        }

        int maxDestinations = budget.MaxDestinations;
        int resolvedMin = Math.Max(min_distance, 0);
        int resolvedMax = Math.Max(max_distance, resolvedMin);
        var seen = new HashSet<Vector2I>();
        var coords = new List<Vector2I>();
        foreach (Vector2I occupiedCoord in target.OccupiedCoords)
        {
            for (int y = occupiedCoord.Y - resolvedMax; y <= occupiedCoord.Y + resolvedMax; y++)
            {
                for (int x = occupiedCoord.X - resolvedMax; x <= occupiedCoord.X + resolvedMax; x++)
                {
                    var coord = new Vector2I(x, y);
                    if (!seen.Add(coord))
                    {
                        continue;
                    }
                    if (!CanPlaceAnchor(unit, coord, overlay))
                    {
                        continue;
                    }
                    int distance = DistanceFromAnchorToTarget(coord, unit.FootprintSize, target);
                    if (distance < resolvedMin || distance > resolvedMax)
                    {
                        continue;
                    }
                    coords.Add(coord);
                    if (maxDestinations > 0 && coords.Count >= maxDestinations)
                    {
                        return DistanceBandSuccess(
                            QueryDistanceBand,
                            coords,
                            InvalidCoord,
                            new List<Vector2I>(),
                            0,
                            seen.Count,
                            false,
                            overlay
                        );
                    }
                }
            }
        }

        coords.Sort(
            (left, right) =>
            {
                int distanceCompare = GetDistance(unit.Coord, left)
                    .CompareTo(GetDistance(unit.Coord, right));
                if (distanceCompare != 0)
                {
                    return distanceCompare;
                }
                int yCompare = left.Y.CompareTo(right.Y);
                return yCompare != 0 ? yCompare : left.X.CompareTo(right.X);
            }
        );
        return DistanceBandSuccess(
            QueryDistanceBand,
            coords,
            InvalidCoord,
            new List<Vector2I>(),
            0,
            seen.Count,
            false,
            overlay
        );
    }

    internal MovementPathTargetResult CollectDistanceBandPathTargets(
        StringName unit_id,
        StringName focus_target_id,
        int min_distance,
        int max_distance,
        int max_cost,
        BattleVirtualBoardOverlay overlay = null,
        MovementQueryOptions options = null
        )
    {
        EnsureSnapshotFresh();
        if (
            !TryGetUnit(unit_id, out UnitInfo unit)
            || !TryGetUnit(focus_target_id, out UnitInfo target)
        )
        {
            return MovementPathTargetResult.Failure("missing_unit");
        }
        MovementQueryOptions queryOptions = options ?? new MovementQueryOptions();
        if (!TryResolveBudget(unit, queryOptions, out PathSearchBudget budget))
        {
            return MovementPathTargetResult.Failure("invalid_options");
        }
        return CollectDistanceBandPathTargetsFromBudget(
            unit,
            target,
            min_distance,
            max_distance,
            max_cost,
            overlay,
            budget
        );
    }

    internal MovementPathTargetResult CollectDistanceBandPathTargetsTyped(
        StringName unitId,
        StringName focusTargetId,
        int minDistance,
        int maxDistance,
        int maxCost,
        int maxNodes,
        int maxDestinations,
        int pathTreeMinDestinationCount,
        bool includeOrigin,
        bool preferProgress,
        BattleVirtualBoardOverlay overlay = null
    )
    {
        AiTraceRecorder.Enter("movement:distance_band_path_targets");
        try
        {
            return CollectDistanceBandPathTargetsTypedImpl(
                unitId,
                focusTargetId,
                minDistance,
                maxDistance,
                maxCost,
                maxNodes,
                maxDestinations,
                pathTreeMinDestinationCount,
                includeOrigin,
                preferProgress,
                overlay
            );
        }
        finally
        {
            AiTraceRecorder.Exit("movement:distance_band_path_targets");
        }
    }

    private MovementPathTargetResult CollectDistanceBandPathTargetsTypedImpl(
        StringName unitId,
        StringName focusTargetId,
        int minDistance,
        int maxDistance,
        int maxCost,
        int maxNodes,
        int maxDestinations,
        int pathTreeMinDestinationCount,
        bool includeOrigin,
        bool preferProgress,
        BattleVirtualBoardOverlay overlay = null
    )
    {
        EnsureSnapshotFresh();
        if (!TryGetUnit(unitId, out UnitInfo unit) || !TryGetUnit(focusTargetId, out UnitInfo target))
        {
            return MovementPathTargetResult.Failure("missing_unit");
        }

        var budget = new PathSearchBudget(
            Math.Max(maxCost, 0),
            Math.Max(maxNodes, 0),
            Math.Max(maxDestinations, 0),
            Math.Max(pathTreeMinDestinationCount, 0),
            includeOrigin,
            preferProgress
        );
        PathTargetQueryCacheKey cacheKey = default;
        bool canUseCache = overlay == null;
        if (canUseCache)
        {
            cacheKey = new PathTargetQueryCacheKey(
                unitId,
                focusTargetId,
                BuildMoveCostCacheSignature(unitId),
                minDistance,
                maxDistance,
                maxCost,
                maxNodes,
                maxDestinations,
                pathTreeMinDestinationCount,
                includeOrigin,
                preferProgress
            );
            if (_pathTargetQueryCache.TryGetValue(cacheKey, out MovementPathTargetResult cachedResult))
            {
                return ClonePathTargetResult(cachedResult);
            }
        }
        MovementPathTargetResult result = CollectDistanceBandPathTargetsFromBudget(
            unit,
            target,
            minDistance,
            maxDistance,
            maxCost,
            overlay,
            budget
        );
        CachePathTargetResult(canUseCache, cacheKey, result);
        return result;
    }

    private MovementPathTargetResult CollectDistanceBandPathTargetsFromBudget(
        UnitInfo unit,
        UnitInfo target,
        int minDistance,
        int maxDistance,
        int maxCost,
        BattleVirtualBoardOverlay overlay,
        PathSearchBudget budget
    )
    {
        List<Vector2I> destinations = CollectDistanceBandDestinationList(
            unit,
            target,
            minDistance,
            maxDistance,
            0,
            overlay,
            out int visitedDestinationCount
        );
        int resolvedMin = Math.Max(minDistance, 0);
        int resolvedMax = Math.Max(maxDistance, resolvedMin);
        int currentDistance = DistanceFromAnchorToTarget(unit.Coord, unit.FootprintSize, target);
        if (destinations.Count == 0)
        {
            return new MovementPathTargetResult
            {
                Ok = true,
                RejectReason = EmptyStringName,
                DestinationCount = 0,
                ReachedDestinationCount = 0,
                UnreachableDestinationCount = 0,
                PathRejectCount = 0,
                SkippedOriginCount = 0,
                VisitedCount = visitedDestinationCount,
                DestinationScanCount = visitedDestinationCount,
            };
        }

        var destinationSet = new HashSet<Vector2I>(destinations);
        PathTargetSearchResult searchResult = BuildPathTargetCandidates(
            unit,
            target,
            unit.Coord,
            destinationSet,
            maxCost,
            overlay,
            budget,
            currentDistance,
            resolvedMin,
            resolvedMax
        );
        if (searchResult.InvalidMoveCost)
        {
            return MovementPathTargetResult.Failure("invalid_move_cost");
        }
        if (searchResult.BudgetExhausted)
        {
            return MovementPathTargetResult.Failure("budget_exhausted");
        }

        var result = new MovementPathTargetResult
        {
            Ok = true,
            RejectReason = EmptyStringName,
            DestinationCount = destinations.Count,
            ReachedDestinationCount = searchResult.ReachedDestinationCount,
            UnreachableDestinationCount = Math.Max(
                destinations.Count - searchResult.ReachedDestinationCount,
                0
            ),
            PathRejectCount = Math.Max(destinations.Count - searchResult.ReachedDestinationCount, 0),
            SkippedOriginCount = searchResult.SkippedOriginCount,
            VisitedCount = visitedDestinationCount + searchResult.VisitedCount,
            SearchVisitedCount = searchResult.VisitedCount,
            DestinationScanCount = visitedDestinationCount,
            RelaxCount = searchResult.RelaxCount,
            BudgetExhausted = searchResult.BudgetExhausted,
        };

        searchResult.Candidates.Sort(
            (left, right) =>
                ComparePathTargetCandidates(
                    left,
                    right,
                    currentDistance,
                    resolvedMin,
                    resolvedMax,
                    budget.PreferProgress
                )
        );
        int maxCandidates = Math.Max(budget.MaxDestinations, 0);
        foreach (PathTargetCandidate candidate in searchResult.Candidates)
        {
            if (maxCandidates > 0 && result.Candidates.Count >= maxCandidates)
            {
                break;
            }
            result.Candidates.Add(
                new MovementPathTargetCandidate
                {
                    DestinationCoord = candidate.DestinationCoord,
                    Coord = candidate.TargetCoord,
                    PathCost = candidate.PathCost,
                    PathLength = candidate.PathLength,
                    SpentCost = candidate.SpentCost,
                }
            );
        }
        return result;
    }

    private void CachePathTargetResult(
        bool canUseCache,
        PathTargetQueryCacheKey cacheKey,
        MovementPathTargetResult result
    )
    {
        if (!canUseCache || result == null)
        {
            return;
        }
        if (_pathTargetQueryCache.Count > 256)
        {
            _pathTargetQueryCache.Clear();
        }
        _pathTargetQueryCache[cacheKey] = ClonePathTargetResult(result);
    }

    private static MovementPathTargetResult ClonePathTargetResult(
        MovementPathTargetResult source
    )
    {
        if (source == null)
        {
            return null;
        }
        var clone = new MovementPathTargetResult
        {
            Ok = source.Ok,
            RejectReason = source.RejectReason,
            DestinationCount = source.DestinationCount,
            ReachedDestinationCount = source.ReachedDestinationCount,
            UnreachableDestinationCount = source.UnreachableDestinationCount,
            PathRejectCount = source.PathRejectCount,
            SkippedOriginCount = source.SkippedOriginCount,
            VisitedCount = source.VisitedCount,
            SearchVisitedCount = source.SearchVisitedCount,
            DestinationScanCount = source.DestinationScanCount,
            RelaxCount = source.RelaxCount,
            BudgetExhausted = source.BudgetExhausted,
        };
        foreach (MovementPathTargetCandidate candidate in source.Candidates)
        {
            if (candidate == null)
            {
                continue;
            }
            clone.Candidates.Add(
                new MovementPathTargetCandidate
                {
                    DestinationCoord = candidate.DestinationCoord,
                    Coord = candidate.Coord,
                    PathCost = candidate.PathCost,
                    PathLength = candidate.PathLength,
                    SpentCost = candidate.SpentCost,
                }
            );
        }
        return clone;
    }

    internal MovementDistanceBandResult resolve_distance_band_path_cost(
        StringName unit_id,
        StringName focus_target_id,
        int min_distance,
        int max_distance,
        BattleVirtualBoardOverlay overlay = null,
        MovementQueryOptions options = null
    )
    {
        EnsureSnapshotFresh();
        if (
            !TryGetUnit(unit_id, out UnitInfo unit)
            || !TryGetUnit(focus_target_id, out UnitInfo target)
        )
        {
            return MovementDistanceBandResult.Failure(QueryDistanceBandPathCost, "missing_unit");
        }
        MovementQueryOptions queryOptions = options ?? new MovementQueryOptions();
        if (!TryResolveBudget(unit, queryOptions, out PathSearchBudget budget))
        {
            return MovementDistanceBandResult.Failure(QueryDistanceBandPathCost, "invalid_options");
        }

        int resolvedMin = Math.Max(min_distance, 0);
        int resolvedMax = Math.Max(max_distance, resolvedMin);
        PathSearchResult pathResult = FindNearestDistanceBandPathCost(
            unit,
            target,
            resolvedMin,
            resolvedMax,
            overlay,
            budget.MaxNodes
        );
        int visitedCount = pathResult.VisitedCount;
        if (!pathResult.Ok)
        {
            return MovementDistanceBandResult.Failure(
                QueryDistanceBandPathCost,
                pathResult.RejectReason
            );
        }

        return DistanceBandSuccess(
            QueryDistanceBandPathCost,
            new List<Vector2I>(),
            pathResult.Path.Count > 0 ? pathResult.Path[^1] : InvalidCoord,
            pathResult.Path,
            pathResult.Cost,
            visitedCount,
            true,
            overlay
        );
    }

    internal MovementReachabilityResult resolve_current_turn_path_target(
        StringName unit_id,
        Vector2I from_coord,
        Vector2I destination_coord,
        int max_cost,
        BattleVirtualBoardOverlay overlay = null,
        MovementQueryOptions options = null
    )
    {
        EnsureSnapshotFresh();
        if (!TryGetUnit(unit_id, out UnitInfo unit))
        {
            return MovementReachabilityResult.Failure(QueryPathTarget, "missing_unit");
        }
        MovementQueryOptions queryOptions = options ?? new MovementQueryOptions();

        PathSearchResult pathResult = FindPath(
            unit,
            from_coord,
            destination_coord,
            overlay,
            queryOptions
        );
        if (!pathResult.Ok)
        {
            return MovementReachabilityResult.Failure(QueryPathTarget, pathResult.RejectReason);
        }
        List<Vector2I> path = pathResult.Path;
        int spent = 0;
        Vector2I targetCoord = from_coord;
        for (int index = 1; index < path.Count; index++)
        {
            Vector2I nextCoord = path[index];
            int stepCost = GetMoveCost(unit.UnitId, path[index - 1], nextCoord);
            if (stepCost <= 0)
            {
                return MovementReachabilityResult.Failure(QueryPathTarget, "invalid_move_cost");
            }
            if (spent + stepCost > max_cost)
            {
                break;
            }
            spent += stepCost;
            targetCoord = nextCoord;
        }
        return ReachabilitySuccess(
            QueryPathTarget,
            new List<Vector2I>(),
            targetCoord,
            path,
            pathResult.Cost,
            pathResult.VisitedCount,
            true,
            overlay
        );
    }

    private List<Vector2I> CollectDistanceBandDestinationList(
        UnitInfo unit,
        UnitInfo target,
        int minDistance,
        int maxDistance,
        int maxDestinations,
        BattleVirtualBoardOverlay overlay,
        out int visitedCount
    )
    {
        int resolvedMin = Math.Max(minDistance, 0);
        int resolvedMax = Math.Max(maxDistance, resolvedMin);
        var seen = new HashSet<Vector2I>();
        var coords = new List<Vector2I>();
        foreach (Vector2I occupiedCoord in target.OccupiedCoords)
        {
            for (int y = occupiedCoord.Y - resolvedMax; y <= occupiedCoord.Y + resolvedMax; y++)
            {
                for (int x = occupiedCoord.X - resolvedMax; x <= occupiedCoord.X + resolvedMax; x++)
                {
                    var coord = new Vector2I(x, y);
                    if (!seen.Add(coord))
                    {
                        continue;
                    }
                    if (!CanPlaceAnchor(unit, coord, overlay))
                    {
                        continue;
                    }
                    int distance = DistanceFromAnchorToTarget(coord, unit.FootprintSize, target);
                    if (distance < resolvedMin || distance > resolvedMax)
                    {
                        continue;
                    }
                    coords.Add(coord);
                    if (maxDestinations > 0 && coords.Count >= maxDestinations)
                    {
                        visitedCount = seen.Count;
                        return coords;
                    }
                }
            }
        }
        coords.Sort(
            (left, right) =>
            {
                int distanceCompare = GetDistance(unit.Coord, left)
                    .CompareTo(GetDistance(unit.Coord, right));
                if (distanceCompare != 0)
                {
                    return distanceCompare;
                }
                int yCompare = left.Y.CompareTo(right.Y);
                return yCompare != 0 ? yCompare : left.X.CompareTo(right.X);
            }
        );
        visitedCount = seen.Count;
        return coords;
    }

    internal bool TryBuildPathSearchBudgetTyped(
        StringName unitId,
        MovementQueryOptions queryOptions,
        out PathSearchBudgetSnapshot budget
    )
    {
        budget = default;
        EnsureSnapshotFresh();
        queryOptions ??= new MovementQueryOptions();
        if (!TryGetUnit(unitId, out UnitInfo unit))
        {
            unit = null;
        }
        if (!TryResolveBudget(unit, queryOptions, out PathSearchBudget resolvedBudget))
        {
            return false;
        }
        budget = resolvedBudget.ToSnapshot();
        return true;
    }

    private void RebuildSnapshot()
    {
        using BattleAiTraceSpan trace = new("movement_query_setup:rebuild_snapshot");
        _mapSize = _state != null ? _state.map_size : Vector2I.Zero;
        _cells =
            _mapSize.X > 0 && _mapSize.Y > 0
                ? new CellInfo[_mapSize.X * _mapSize.Y]
                : System.Array.Empty<CellInfo>();
        _units.Clear();
        _edges.Clear();
        _distanceFromAnchorToTargetCache.Clear();
        _pathTargetQueryCache.Clear();
        _moveCostSignatureCache.Clear();
        EnsureRuntimeEdges();

        if (_state == null)
        {
            _snapshotRevision = 0;
            return;
        }
        if (_state != null)
        {
            foreach (BattleState.BattleCellEntry entry in _state.CellEntries())
            {
                Vector2I coord = entry.Coord;
                if (!IsInside(coord))
                {
                    continue;
                }
                BattleCellState cell = entry.Cell;
                if (cell == null)
                {
                    continue;
                }
                _cells[ToIndex(coord)] = new CellInfo(
                    true,
                    NormalizeTerrain(cell.base_terrain),
                    cell.occupant_unit_id
                );
            }
        }

        if (_state != null)
        {
            foreach (BattleUnitState unitObject in _state.Units())
            {
                UnitInfo unit = BuildUnitInfo(unitObject);
                if (unit != null && unit.UnitId != EmptyStringName)
                {
                    _units[unit.UnitId] = unit;
                }
            }
        }

        IReadOnlyDictionary<Vector3I, BattleEdgeFaceState> edges = _state.ProjectRuntimeEdgeFaces();
        if (edges != null)
        {
            foreach ((Vector3I key, BattleEdgeFaceState edgeObject) in edges)
            {
                if (edgeObject == null)
                {
                    continue;
                }
                _edges[key] = new EdgeInfo(
                    edgeObject.BlocksMove(),
                    edgeObject.BlocksOccupancy(),
                    edgeObject.height_difference
                );
            }
        }
        _snapshotRevision = GetCurrentSnapshotRevision();
    }

    private void EnsureSnapshotFresh()
    {
        if (GetCurrentSnapshotRevision() != _snapshotRevision)
        {
            RebuildSnapshot();
        }
    }

    private long GetCurrentSnapshotRevision()
    {
        return _state?.MovementGeometryRevision ?? 0;
    }

    private long BuildMoveCostCacheSignature(StringName unitId)
    {
        if (_moveCostProvider == null || _state == null)
        {
            return 0;
        }
        StringName normalizedUnitId = ToStringName(unitId);
        if (_moveCostSignatureCache.TryGetValue(normalizedUnitId, out long cachedSignature))
        {
            return cachedSignature;
        }
        long signature = ComputeMoveCostCacheSignature(normalizedUnitId);
        _moveCostSignatureCache[normalizedUnitId] = signature;
        return signature;
    }

    private long ComputeMoveCostCacheSignature(StringName unitId)
    {
        unchecked
        {
            long hash = 1469598103934665603;
            BattleUnitState unit = GetStateUnit(unitId);
            hash = MixHash(hash, HashStringName(unitId));
            if (unit != null)
            {
                hash = MixHash(hash, HashStringName(unit.faction_id));
                foreach (StringName statusId in unit.GetSortedStatusEffectIdsTyped())
                {
                    BattleStatusEffectState statusEntry = unit.GetStatusEffect(statusId);
                    if (statusEntry == null)
                    {
                        continue;
                    }
                    hash = MixHash(hash, HashStringName(statusId));
                    hash = MixHash(hash, statusEntry.power);
                    hash = MixHash(hash, statusEntry.stacks);
                    hash = MixHash(hash, statusEntry.duration);
                    hash = MixHash(hash, statusEntry.tick_interval_tu);
                    hash = MixHash(hash, statusEntry.next_tick_at_tu);
                    hash = MixHash(hash, BattleStatusSemanticTable.GetMoveCostDelta(statusEntry));
                }
            }

            var cellCoords = new List<Vector2I>();
            if (_state != null)
            {
                foreach (BattleState.BattleCellEntry entry in _state.CellEntries())
                {
                    Vector2I coord = entry.Coord;
                    BattleCellState cell = entry.Cell;
                    if (cell?.timed_terrain_effects == null || cell.timed_terrain_effects.Count == 0)
                    {
                        continue;
                    }
                    cellCoords.Add(coord);
                }
            }
            cellCoords.Sort(CompareCoordsYThenX);
            foreach (Vector2I coord in cellCoords)
            {
                BattleCellState cell = _gridService?.GetCellState(_state, coord);
                if (cell?.timed_terrain_effects == null || cell.timed_terrain_effects.Count == 0)
                {
                    continue;
                }
                hash = MixHash(hash, coord.X);
                hash = MixHash(hash, coord.Y);
                hash = MixHash(hash, cell.timed_terrain_effects.Count);
                foreach (BattleTerrainEffectState effectState in cell.timed_terrain_effects)
                {
                    hash = MixTerrainMoveCostEffectHash(hash, effectState);
                }
            }
            return hash;
        }
    }

    private long MixTerrainMoveCostEffectHash(long hash, BattleTerrainEffectState effectState)
    {
        unchecked
        {
            if (effectState == null)
            {
                return MixHash(hash, 0);
            }
            BattleUnitState sourceUnit = GetStateUnit(effectState.source_unit_id);
            hash = MixHash(hash, HashStringName(effectState.field_instance_id));
            hash = MixHash(hash, HashStringName(effectState.effect_id));
            hash = MixHash(hash, HashStringName(effectState.effect_type));
            hash = MixHash(hash, HashStringName(effectState.lifetime_policy));
            hash = MixHash(hash, effectState.move_cost_delta);
            hash = MixHash(hash, HashStringName(effectState.source_unit_id));
            hash = MixHash(hash, HashStringName(sourceUnit?.faction_id ?? EmptyStringName));
            hash = MixHash(hash, HashStringName(effectState.target_team_filter));
            hash = MixHash(hash, effectState.remaining_tu);
            hash = MixHash(hash, HashStringName(effectState.does_not_stack_with_status_id));
            hash = MixHash(hash, effectState.does_not_stack_with_status_ids?.Count ?? 0);
            if (effectState.does_not_stack_with_status_ids != null)
            {
                foreach (StringName statusId in effectState.does_not_stack_with_status_ids)
                {
                    hash = MixHash(hash, HashStringName(statusId));
                }
            }
            return hash;
        }
    }

    private BattleUnitState GetStateUnit(StringName unitId)
    {
        return _state?.GetUnit(unitId);
    }

    private static long MixHash(long hash, int value)
    {
        unchecked
        {
            return (hash ^ value) * 1099511628211;
        }
    }

    private static int HashStringName(StringName value)
    {
        StringName normalized = ToStringName(value);
        return normalized == EmptyStringName ? 0 : normalized.GetHashCode();
    }

    private static int CompareCoordsYThenX(Vector2I left, Vector2I right)
    {
        int yCompare = left.Y.CompareTo(right.Y);
        return yCompare != 0 ? yCompare : left.X.CompareTo(right.X);
    }

    private void EnsureRuntimeEdges()
    {
        if (_state == null || _gridService == null)
        {
            return;
        }
        if (_mapSize.X > 1)
        {
            _gridService.GetEdgeFace(_state, Vector2I.Zero, Vector2I.Right);
        }
        else if (_mapSize.Y > 1)
        {
            _gridService.GetEdgeFace(_state, Vector2I.Zero, Vector2I.Down);
        }
    }

    private UnitInfo BuildUnitInfo(BattleUnitState unitObject)
    {
        if (unitObject == null)
        {
            return null;
        }
        var unit = new UnitInfo
        {
            UnitId = unitObject.unit_id,
            Coord = unitObject.coord,
            FootprintSize = NormalizeFootprint(unitObject.footprint_size),
        };
        foreach (StringName tag in unitObject.movement_tags)
        {
            StringName normalized = ToStringName(tag);
            if (normalized != EmptyStringName)
            {
                unit.MovementTags.Add(normalized);
            }
        }
        foreach (Vector2I coord in unitObject.occupied_coords)
        {
            unit.OccupiedCoords.Add(coord);
        }
        if (unit.OccupiedCoords.Count == 0)
        {
            unit.OccupiedCoords.AddRange(GetFootprintCoords(unit.Coord, unit.FootprintSize));
        }
        return unit;
    }

    private PathSearchResult FindPath(
        UnitInfo unit,
        Vector2I fromCoord,
        Vector2I destinationCoord,
        BattleVirtualBoardOverlay overlay,
        MovementQueryOptions options
    )
    {
        if (!CanPlaceAnchor(unit, destinationCoord, overlay))
        {
            return PathSearchResult.Failure("destination_blocked");
        }
        if (!TryResolveBudget(unit, options, out PathSearchBudget budget))
        {
            return PathSearchResult.Failure("invalid_options");
        }
        int maxNodes = budget.MaxNodes;
        var bestCosts = new System.Collections.Generic.Dictionary<Vector2I, int>
        {
            [fromCoord] = 0,
        };
        var previous = new System.Collections.Generic.Dictionary<Vector2I, Vector2I>();
        var frontier = new Queue<Vector2I>();
        frontier.Enqueue(fromCoord);
        int visitedCount = 0;
        while (frontier.Count > 0)
        {
            Vector2I current = frontier.Dequeue();
            visitedCount++;
            if (maxNodes > 0 && visitedCount > maxNodes)
            {
                return PathSearchResult.Failure("budget_exhausted", visitedCount);
            }
            if (current == destinationCoord)
            {
                List<Vector2I> path = ReconstructPath(previous, fromCoord, destinationCoord);
                return PathSearchResult.Success(path, bestCosts[destinationCoord], visitedCount);
            }
            int currentCost = bestCosts[current];
            foreach (Vector2I neighbor in GetNeighbors4(current))
            {
                if (!CanStep(unit, current, neighbor, overlay))
                {
                    continue;
                }
                int stepCost = GetMoveCost(unit.UnitId, current, neighbor);
                if (stepCost <= 0)
                {
                    return PathSearchResult.Failure("invalid_move_cost", visitedCount);
                }
                int nextCost = currentCost + stepCost;
                if (
                    bestCosts.TryGetValue(neighbor, out int existingCost)
                    && nextCost >= existingCost
                )
                {
                    continue;
                }
                bestCosts[neighbor] = nextCost;
                previous[neighbor] = current;
                frontier.Enqueue(neighbor);
            }
        }
        return PathSearchResult.Failure("unreachable", visitedCount);
    }

    private PathSearchResult FindNearestDistanceBandPathCost(
        UnitInfo unit,
        UnitInfo target,
        int minDistance,
        int maxDistance,
        BattleVirtualBoardOverlay overlay,
        int maxNodes
    )
    {
        if (unit == null || target == null)
        {
            return PathSearchResult.Failure("missing_unit");
        }
        int startDistance = DistanceFromAnchorToTarget(unit.Coord, unit.FootprintSize, target);
        if (startDistance >= minDistance && startDistance <= maxDistance)
        {
            return PathSearchResult.Success(new List<Vector2I> { unit.Coord }, 0, 1);
        }

        var costs = new System.Collections.Generic.Dictionary<Vector2I, int> { [unit.Coord] = 0 };
        var previous = new System.Collections.Generic.Dictionary<Vector2I, Vector2I>();
        var frontier = new PriorityQueue<PathSearchNode, int>();
        frontier.Enqueue(
            new PathSearchNode(unit.Coord, 0),
            EstimateDistanceBandCost(unit, target, unit.Coord, minDistance, maxDistance)
        );
        int visitedCount = 0;

        while (frontier.TryDequeue(out PathSearchNode node, out _))
        {
            Vector2I current = node.Coord;
            if (!costs.TryGetValue(current, out int currentCost) || node.Cost != currentCost)
            {
                continue;
            }
            visitedCount++;
            if (maxNodes > 0 && visitedCount > maxNodes)
            {
                return PathSearchResult.Failure("budget_exhausted", visitedCount);
            }
            int currentDistance = DistanceFromAnchorToTarget(current, unit.FootprintSize, target);
            if (currentDistance >= minDistance && currentDistance <= maxDistance)
            {
                List<Vector2I> path = ReconstructPath(previous, unit.Coord, current);
                return PathSearchResult.Success(path, currentCost, visitedCount);
            }

            foreach (Vector2I neighbor in GetNeighbors4(current))
            {
                if (!CanStep(unit, current, neighbor, overlay))
                {
                    continue;
                }
                int stepCost = GetMoveCost(unit.UnitId, current, neighbor);
                if (stepCost <= 0)
                {
                    return PathSearchResult.Failure("invalid_move_cost", visitedCount);
                }
                int nextCost = currentCost + stepCost;
                if (costs.TryGetValue(neighbor, out int existingCost) && nextCost >= existingCost)
                {
                    continue;
                }
                costs[neighbor] = nextCost;
                previous[neighbor] = current;
                int priority =
                    nextCost
                    + EstimateDistanceBandCost(unit, target, neighbor, minDistance, maxDistance);
                frontier.Enqueue(new PathSearchNode(neighbor, nextCost), priority);
            }
        }

        return PathSearchResult.Failure("unreachable", visitedCount);
    }

    private int EstimateDistanceBandCost(
        UnitInfo unit,
        UnitInfo target,
        Vector2I anchorCoord,
        int minDistance,
        int maxDistance
    )
    {
        int distance = DistanceFromAnchorToTarget(anchorCoord, unit.FootprintSize, target);
        return GetDistanceGap(distance, minDistance, maxDistance);
    }

    private PathTargetSearchResult BuildPathTargetCandidates(
        UnitInfo unit,
        UnitInfo target,
        Vector2I fromCoord,
        HashSet<Vector2I> destinationSet,
        int maxCost,
        BattleVirtualBoardOverlay overlay,
        PathSearchBudget budget,
        int currentDistance,
        int resolvedMin,
        int resolvedMax
    )
    {
        var result = new PathTargetSearchResult();
        if (
            unit == null
            || target == null
            || destinationSet == null
            || destinationSet.Count == 0
            || !IsInside(fromCoord)
            || _cells.Length == 0
        )
        {
            return result;
        }

        int costLimit = Math.Max(maxCost, 0);
        int maxNodes = budget.MaxNodes;
        var metaByIndex = new PathTargetNodeMeta[_cells.Length];
        var frontier = new PriorityQueue<PathSearchNode, int>();
        var reachedDestinations = new HashSet<Vector2I>();
        var bestByTargetCoord = new System.Collections.Generic.Dictionary<
            Vector2I,
            PathTargetCandidate
        >();

        int startIndex = ToIndex(fromCoord);
        metaByIndex[startIndex] = new PathTargetNodeMeta(true, 0, fromCoord, 0, 1, InvalidCoord);
        frontier.Enqueue(new PathSearchNode(fromCoord, 0), 0);

        while (frontier.TryDequeue(out PathSearchNode currentNode, out int currentPriority))
        {
            Vector2I current = currentNode.Coord;
            if (!IsInside(current))
            {
                continue;
            }
            PathTargetNodeMeta currentMeta = metaByIndex[ToIndex(current)];
            if (
                !currentMeta.HasValue
                || currentNode.Cost != currentMeta.Cost
                || currentPriority != currentMeta.Cost
            )
            {
                continue;
            }

            result.VisitedCount++;
            if (maxNodes > 0 && result.VisitedCount > maxNodes)
            {
                result.BudgetExhausted = true;
                return result;
            }

            if (destinationSet.Contains(current) && reachedDestinations.Add(current))
            {
                result.ReachedDestinationCount++;
                if (currentMeta.LandingCoord == unit.Coord)
                {
                    result.SkippedOriginCount++;
                }
                else
                {
                    int targetDistance = DistanceFromAnchorToTarget(
                        currentMeta.LandingCoord,
                        unit.FootprintSize,
                        target
                    );
                    var candidate = new PathTargetCandidate(
                        current,
                        currentMeta.LandingCoord,
                        currentMeta.Cost,
                        currentMeta.PathLength,
                        currentMeta.SpentCost,
                        targetDistance,
                        GetDistanceGap(targetDistance, resolvedMin, resolvedMax)
                    );
                    if (
                        !bestByTargetCoord.TryGetValue(
                            candidate.TargetCoord,
                            out PathTargetCandidate existing
                        )
                        || IsBetterPathTargetCandidate(
                            candidate,
                            existing,
                            currentDistance,
                            resolvedMin,
                            resolvedMax,
                            budget.PreferProgress
                        )
                    )
                    {
                        bestByTargetCoord[candidate.TargetCoord] = candidate;
                    }
                }

                if (result.ReachedDestinationCount >= destinationSet.Count)
                {
                    break;
                }
            }

            foreach (Vector2I neighbor in GetNeighbors4(current))
            {
                if (!CanStep(unit, current, neighbor, overlay))
                {
                    continue;
                }
                int stepCost = GetMoveCost(unit.UnitId, current, neighbor);
                if (stepCost <= 0)
                {
                    result.InvalidMoveCost = true;
                    return result;
                }
                int nextCost = currentMeta.Cost + stepCost;
                int neighborIndex = ToIndex(neighbor);
                PathTargetNodeMeta existingMeta = metaByIndex[neighborIndex];
                if (existingMeta.HasValue && nextCost >= existingMeta.Cost)
                {
                    continue;
                }

                Vector2I landingCoord = nextCost <= costLimit ? neighbor : currentMeta.LandingCoord;
                int spentCost = nextCost <= costLimit ? nextCost : currentMeta.SpentCost;
                metaByIndex[neighborIndex] = new PathTargetNodeMeta(
                    true,
                    nextCost,
                    landingCoord,
                    spentCost,
                    currentMeta.PathLength + 1,
                    current
                );
                result.RelaxCount++;
                frontier.Enqueue(new PathSearchNode(neighbor, nextCost), nextCost);
            }
        }

        result.Candidates.AddRange(bestByTargetCoord.Values);
        return result;
    }

    private PathSearchTree BuildPathSearchTree(
        UnitInfo unit,
        Vector2I fromCoord,
        HashSet<Vector2I> destinationSet,
        BattleVirtualBoardOverlay overlay,
        int maxNodes
    )
    {
        var result = new PathSearchTree();
        if (unit == null)
        {
            return result;
        }

        var frontier = new PriorityQueue<Vector2I, int>();
        result.Costs[fromCoord] = 0;
        frontier.Enqueue(fromCoord, 0);
        int reachedDestinationCount = 0;

        while (frontier.TryDequeue(out Vector2I current, out int currentPriority))
        {
            if (
                !result.Costs.TryGetValue(current, out int currentCost)
                || currentPriority != currentCost
            )
            {
                continue;
            }
            result.VisitedCount++;
            if (maxNodes > 0 && result.VisitedCount > maxNodes)
            {
                result.BudgetExhausted = true;
                return result;
            }
            if (destinationSet.Contains(current))
            {
                reachedDestinationCount++;
                if (reachedDestinationCount >= destinationSet.Count)
                {
                    return result;
                }
            }

            foreach (Vector2I neighbor in GetNeighbors4(current))
            {
                if (!CanStep(unit, current, neighbor, overlay))
                {
                    continue;
                }
                int stepCost = GetMoveCost(unit.UnitId, current, neighbor);
                if (stepCost <= 0)
                {
                    result.InvalidMoveCost = true;
                    return result;
                }
                int nextCost = currentCost + stepCost;
                if (
                    result.Costs.TryGetValue(neighbor, out int existingCost)
                    && nextCost >= existingCost
                )
                {
                    continue;
                }
                result.Costs[neighbor] = nextCost;
                result.Previous[neighbor] = current;
                frontier.Enqueue(neighbor, nextCost);
            }
        }

        return result;
    }

    private bool TryResolveCurrentTurnPathTarget(
        UnitInfo unit,
        List<Vector2I> path,
        int maxCost,
        out Vector2I targetCoord,
        out int spentCost
    )
    {
        targetCoord = unit?.Coord ?? InvalidCoord;
        spentCost = 0;
        if (unit == null || path == null)
        {
            return true;
        }
        int costLimit = Math.Max(maxCost, 0);
        for (int index = 1; index < path.Count; index++)
        {
            Vector2I nextCoord = path[index];
            int stepCost = GetMoveCost(unit.UnitId, path[index - 1], nextCoord);
            if (stepCost <= 0)
            {
                return false;
            }
            if (spentCost + stepCost > costLimit)
            {
                break;
            }
            spentCost += stepCost;
            targetCoord = nextCoord;
        }
        return true;
    }

    private static int ComparePathTargetCandidates(
        PathTargetCandidate left,
        PathTargetCandidate right,
        int currentDistance,
        int minDistance,
        int maxDistance,
        bool preferProgress
    )
    {
        if (left.DistanceGap != right.DistanceGap)
        {
            return left.DistanceGap.CompareTo(right.DistanceGap);
        }
        if (preferProgress)
        {
            int progressCompare = CompareProgressDistance(
                left.DistanceToTarget,
                right.DistanceToTarget,
                currentDistance,
                minDistance,
                maxDistance
            );
            if (progressCompare != 0)
            {
                return progressCompare;
            }
        }
        if (left.PathCost != right.PathCost)
        {
            return left.PathCost.CompareTo(right.PathCost);
        }
        if (left.SpentCost != right.SpentCost)
        {
            return right.SpentCost.CompareTo(left.SpentCost);
        }
        if (left.PathLength != right.PathLength)
        {
            return left.PathLength.CompareTo(right.PathLength);
        }
        int yCompare = left.TargetCoord.Y.CompareTo(right.TargetCoord.Y);
        return yCompare != 0 ? yCompare : left.TargetCoord.X.CompareTo(right.TargetCoord.X);
    }

    private static bool IsBetterPathTargetCandidate(
        PathTargetCandidate candidate,
        PathTargetCandidate existing,
        int currentDistance,
        int minDistance,
        int maxDistance,
        bool preferProgress
    )
    {
        return ComparePathTargetCandidates(
                candidate,
                existing,
                currentDistance,
                minDistance,
                maxDistance,
                preferProgress
            ) < 0;
    }

    private static int CompareProgressDistance(
        int leftDistance,
        int rightDistance,
        int currentDistance,
        int minDistance,
        int maxDistance
    )
    {
        if (currentDistance > maxDistance && leftDistance != rightDistance)
        {
            return leftDistance.CompareTo(rightDistance);
        }
        if (currentDistance < minDistance && leftDistance != rightDistance)
        {
            return rightDistance.CompareTo(leftDistance);
        }
        return 0;
    }

    private static int GetDistanceGap(int distance, int minDistance, int maxDistance)
    {
        if (distance < 0 || minDistance < 0 || maxDistance < minDistance)
        {
            return InfiniteCost;
        }
        if (distance < minDistance)
        {
            return minDistance - distance;
        }
        if (distance > maxDistance)
        {
            return distance - maxDistance;
        }
        return 0;
    }

    private static List<Vector2I> ReconstructPath(
        System.Collections.Generic.Dictionary<Vector2I, Vector2I> previous,
        Vector2I fromCoord,
        Vector2I destinationCoord
    )
    {
        var path = new List<Vector2I> { destinationCoord };
        Vector2I current = destinationCoord;
        while (current != fromCoord && previous.TryGetValue(current, out Vector2I prior))
        {
            current = prior;
            path.Insert(0, current);
        }
        return path;
    }

    private bool CanStep(UnitInfo unit, Vector2I fromCoord, Vector2I toCoord, BattleVirtualBoardOverlay overlay)
    {
        if (unit == null || GetDistance(fromCoord, toCoord) != 1)
        {
            return false;
        }
        if (!CanStepAcrossEdges(unit.FootprintSize, fromCoord, toCoord - fromCoord))
        {
            return false;
        }
        return CanPlaceAnchor(unit, toCoord, overlay);
    }

    private bool CanPlaceAnchor(UnitInfo unit, Vector2I anchorCoord, BattleVirtualBoardOverlay overlay)
    {
        if (unit == null)
        {
            return false;
        }
        foreach (Vector2I coord in GetFootprintCoords(anchorCoord, unit.FootprintSize))
        {
            if (!IsInside(coord))
            {
                return false;
            }
            CellInfo cell = GetCell(coord);
            if (!cell.Exists || !CanUnitEnterTerrain(cell.Terrain, unit.MovementTags))
            {
                return false;
            }
            StringName occupant = GetOverlayOccupant(overlay, coord, cell.Occupant);
            if (occupant != EmptyStringName && occupant != unit.UnitId)
            {
                return false;
            }
        }
        return CanOccupyFootprintEdges(unit.FootprintSize, anchorCoord);
    }

    private bool CanStepAcrossEdges(Vector2I footprintSize, Vector2I anchorCoord, Vector2I delta)
    {
        if (delta == Vector2I.Right)
        {
            for (int y = 0; y < footprintSize.Y; y++)
            {
                if (
                    !IsEdgeTraversable(
                        anchorCoord + new Vector2I(footprintSize.X - 1, y),
                        Vector2I.Right
                    )
                )
                {
                    return false;
                }
            }
            return true;
        }
        if (delta == Vector2I.Left)
        {
            for (int y = 0; y < footprintSize.Y; y++)
            {
                if (!IsEdgeTraversable(anchorCoord + new Vector2I(0, y), Vector2I.Left))
                {
                    return false;
                }
            }
            return true;
        }
        if (delta == Vector2I.Down)
        {
            for (int x = 0; x < footprintSize.X; x++)
            {
                if (
                    !IsEdgeTraversable(
                        anchorCoord + new Vector2I(x, footprintSize.Y - 1),
                        Vector2I.Down
                    )
                )
                {
                    return false;
                }
            }
            return true;
        }
        if (delta == Vector2I.Up)
        {
            for (int x = 0; x < footprintSize.X; x++)
            {
                if (!IsEdgeTraversable(anchorCoord + new Vector2I(x, 0), Vector2I.Up))
                {
                    return false;
                }
            }
            return true;
        }
        return false;
    }

    private bool IsEdgeTraversable(Vector2I fromCoord, Vector2I delta)
    {
        if (!TryGetEdge(fromCoord, fromCoord + delta, out EdgeInfo edge))
        {
            return false;
        }
        if (edge.BlocksMove)
        {
            return false;
        }
        return edge.HeightDifference <= 1;
    }

    private bool CanOccupyFootprintEdges(Vector2I footprintSize, Vector2I anchorCoord)
    {
        for (int y = 0; y < footprintSize.Y; y++)
        {
            for (int x = 0; x < footprintSize.X; x++)
            {
                Vector2I coord = anchorCoord + new Vector2I(x, y);
                if (x + 1 < footprintSize.X && EdgeBlocksOccupancy(coord, coord + Vector2I.Right))
                {
                    return false;
                }
                if (y + 1 < footprintSize.Y && EdgeBlocksOccupancy(coord, coord + Vector2I.Down))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private bool EdgeBlocksOccupancy(Vector2I fromCoord, Vector2I toCoord)
    {
        if (!TryGetEdge(fromCoord, toCoord, out EdgeInfo edge))
        {
            return true;
        }
        if (edge.BlocksOccupancy)
        {
            return true;
        }
        return edge.HeightDifference > 1;
    }

    private bool TryGetEdge(Vector2I fromCoord, Vector2I toCoord, out EdgeInfo edge)
    {
        Vector2I delta = toCoord - fromCoord;
        Vector3I key;
        if (delta == Vector2I.Right)
        {
            key = new Vector3I(fromCoord.X, fromCoord.Y, 0);
        }
        else if (delta == Vector2I.Left)
        {
            key = new Vector3I(toCoord.X, toCoord.Y, 0);
        }
        else if (delta == Vector2I.Down)
        {
            key = new Vector3I(fromCoord.X, fromCoord.Y, 1);
        }
        else if (delta == Vector2I.Up)
        {
            key = new Vector3I(toCoord.X, toCoord.Y, 1);
        }
        else
        {
            edge = default;
            return false;
        }
        return _edges.TryGetValue(key, out edge);
    }

    private int GetMoveCost(StringName unitId, Vector2I fromCoord, Vector2I toCoord)
    {
        if (_moveCostProvider == null)
        {
            return 1;
        }
        int cost = _moveCostProvider.Invoke(unitId, fromCoord, toCoord);
        if (cost <= 0)
        {
            Fail("move_cost_provider must return int >= 1.");
            return -1;
        }
        return cost;
    }

    private bool TryResolveBudget(
        UnitInfo unit,
        MovementQueryOptions options,
        out PathSearchBudget budget
    )
    {
        options ??= new MovementQueryOptions();
        budget = BuildDefaultBudget(unit, options);
        return true;
    }

    private PathSearchBudget BuildDefaultBudget(
        UnitInfo unit,
        MovementQueryOptions options
    )
    {
        options ??= new MovementQueryOptions();
        return new PathSearchBudget(
            ResolveCurrentMovePoints(unit?.UnitId ?? EmptyStringName),
            0,
            Math.Max(options.MaxCandidateCount, 0),
            0,
            options.HasIncludeOrigin && options.IncludeOrigin,
            options.HasPreferProgress && options.PreferProgress
        );
    }

    private int ResolveCurrentMovePoints(StringName unitId)
    {
        if (_state == null || unitId == EmptyStringName)
        {
            return 0;
        }
        return _state.TryGetUnitTyped(unitId, out BattleUnitState unitState)
            ? Math.Max(unitState.current_move_points, 0)
            : 0;
    }

    private MovementReachabilityResult ReachabilitySuccess(
        StringName queryKind,
        List<Vector2I> coords,
        Vector2I targetCoord,
        List<Vector2I> path,
        int cost,
        int visitedCount,
        bool targetCoordValid,
        BattleVirtualBoardOverlay overlay
    )
    {
        return MovementReachabilityResult.Success(
            queryKind,
            coords,
            targetCoord,
            path,
            cost,
            visitedCount,
            targetCoordValid,
            ResolveOverlayOverrideCount(overlay)
        );
    }

    private MovementDistanceBandResult DistanceBandSuccess(
        StringName queryKind,
        List<Vector2I> coords,
        Vector2I targetCoord,
        List<Vector2I> path,
        int cost,
        int visitedCount,
        bool targetCoordValid,
        BattleVirtualBoardOverlay overlay
    )
    {
        return MovementDistanceBandResult.Success(
            queryKind,
            coords,
            targetCoord,
            path,
            cost,
            visitedCount,
            targetCoordValid,
            ResolveOverlayOverrideCount(overlay)
        );
    }

    private static int ResolveOverlayOverrideCount(BattleVirtualBoardOverlay overlay)
    {
        int overlayOverrideCount = 0;
        Dictionary overlayDescription = overlay?.Describe();
        int parsedOverrideCount = 0;
        if (
            overlayDescription != null
            && overlayDescription.ContainsKey("override_count")
            && (parsedOverrideCount = overlayDescription["override_count"].AsInt32()) >= 0
        )
        {
            overlayOverrideCount = parsedOverrideCount;
        }
        return overlayOverrideCount;
    }

    private bool TryGetUnit(StringName unitId, out UnitInfo unit)
    {
        return _units.TryGetValue(unitId, out unit);
    }

    private CellInfo GetCell(Vector2I coord)
    {
        return IsInside(coord) ? _cells[ToIndex(coord)] : default;
    }

    private int ToIndex(Vector2I coord)
    {
        return coord.Y * _mapSize.X + coord.X;
    }

    private bool IsInside(Vector2I coord)
    {
        return coord.X >= 0 && coord.Y >= 0 && coord.X < _mapSize.X && coord.Y < _mapSize.Y;
    }

    private IEnumerable<Vector2I> GetNeighbors4(Vector2I coord)
    {
        Vector2I left = coord + Vector2I.Left;
        if (IsInside(left))
            yield return left;
        Vector2I right = coord + Vector2I.Right;
        if (IsInside(right))
            yield return right;
        Vector2I up = coord + Vector2I.Up;
        if (IsInside(up))
            yield return up;
        Vector2I down = coord + Vector2I.Down;
        if (IsInside(down))
            yield return down;
    }

    private static List<Vector2I> GetFootprintCoords(Vector2I anchorCoord, Vector2I footprintSize)
    {
        Vector2I normalized = NormalizeFootprint(footprintSize);
        var coords = new List<Vector2I>(normalized.X * normalized.Y);
        for (int y = 0; y < normalized.Y; y++)
        {
            for (int x = 0; x < normalized.X; x++)
            {
                coords.Add(anchorCoord + new Vector2I(x, y));
            }
        }
        return coords;
    }

    private static Vector2I NormalizeFootprint(Vector2I footprintSize)
    {
        return new Vector2I(Math.Max(footprintSize.X, 1), Math.Max(footprintSize.Y, 1));
    }

    private static int GetDistance(Vector2I fromCoord, Vector2I toCoord)
    {
        return Math.Abs(fromCoord.X - toCoord.X) + Math.Abs(fromCoord.Y - toCoord.Y);
    }

    private int DistanceFromAnchorToTarget(
        Vector2I anchorCoord,
        Vector2I anchorFootprintSize,
        UnitInfo target
    )
    {
        if (target == null || target.UnitId == EmptyStringName)
        {
            return -1;
        }
        Vector2I normalizedFootprint = NormalizeFootprint(anchorFootprintSize);
        var cacheKey = new DistanceFromAnchorToTargetKey(
            anchorCoord,
            normalizedFootprint,
            target.UnitId
        );
        if (_distanceFromAnchorToTargetCache.TryGetValue(cacheKey, out int cachedDistance))
        {
            return cachedDistance;
        }

        int bestDistance = InfiniteCost;
        for (int y = 0; y < normalizedFootprint.Y; y++)
        {
            for (int x = 0; x < normalizedFootprint.X; x++)
            {
                Vector2I sourceCoord = anchorCoord + new Vector2I(x, y);
                foreach (Vector2I targetCoord in target.OccupiedCoords)
                {
                    bestDistance = Math.Min(bestDistance, GetDistance(sourceCoord, targetCoord));
                }
            }
        }
        int distance = bestDistance < InfiniteCost ? bestDistance : -1;
        _distanceFromAnchorToTargetCache[cacheKey] = distance;
        return distance;
    }

    private static bool CanUnitEnterTerrain(StringName terrain, HashSet<StringName> movementTags)
    {
        if (movementTags.Contains("fly"))
        {
            return true;
        }
        return NormalizeTerrain(terrain) != (StringName)"deep_water"
            || movementTags.Contains("amphibious");
    }

    private static StringName NormalizeTerrain(StringName terrain)
    {
        return terrain == EmptyStringName ? (StringName)"land" : terrain;
    }

    private static StringName GetOverlayOccupant(
        BattleVirtualBoardOverlay overlay,
        Vector2I coord,
        StringName baseOccupant
    )
    {
        if (overlay == null)
        {
            return baseOccupant;
        }
        return overlay.GetOccupant(coord, baseOccupant);
    }

    private static StringName ToStringName(object rawValue) =>
        ProgressionDataUtils.to_string_name(rawValue);

    private bool Fail(string message)
    {
        return FailStatic(message);
    }

    private static bool FailStatic(string message)
    {
        GameLog.Error(message, "battle.movement.query_failed", "battle");
        return false;
    }
}
