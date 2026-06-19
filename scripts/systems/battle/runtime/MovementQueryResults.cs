using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class MovementReachabilityResult
{
    private static readonly Vector2I InvalidCoord = new(-1, -1);
    private readonly List<Vector2I> _coords = new();
    private readonly List<Vector2I> _path = new();

    public bool Ok { get; private set; }
    public StringName QueryKind { get; private set; } = "";
    public StringName RejectReason { get; private set; } = "";
    public IReadOnlyList<Vector2I> Coords => _coords;
    public Vector2I TargetCoord { get; private set; } = InvalidCoord;
    public IReadOnlyList<Vector2I> Path => _path;
    public int Cost { get; private set; }
    public int VisitedCount { get; private set; }
    public bool TargetCoordValid { get; private set; }
    public int OverlayOverrideCount { get; private set; }

    internal static MovementReachabilityResult Success(
        StringName queryKind,
        IEnumerable<Vector2I> coords,
        Vector2I targetCoord,
        IEnumerable<Vector2I> path,
        int cost,
        int visitedCount,
        bool targetCoordValid,
        int overlayOverrideCount
    )
    {
        MovementReachabilityResult result = new()
        {
            Ok = true,
            QueryKind = queryKind,
            RejectReason = "",
            TargetCoord = targetCoord,
            Cost = cost,
            VisitedCount = visitedCount,
            TargetCoordValid = targetCoordValid,
            OverlayOverrideCount = overlayOverrideCount,
        };
        AddAll(result._coords, coords);
        AddAll(result._path, path);
        return result;
    }

    internal static MovementReachabilityResult Failure(StringName queryKind, StringName rejectReason)
    {
        return new MovementReachabilityResult
        {
            Ok = false,
            QueryKind = queryKind,
            RejectReason = rejectReason,
            TargetCoord = InvalidCoord,
        };
    }

    internal GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["ok"] = Ok,
            ["coords"] = VectorListToArray(_coords),
            ["target_coord"] = TargetCoord,
            ["path"] = VectorListToArray(_path),
            ["cost"] = Cost,
            ["visited_count"] = VisitedCount,
            ["reject_reason"] = RejectReason,
            ["telemetry"] = BuildTelemetry(_coords.Count),
        };
    }

    private GDictionary BuildTelemetry(int destinationCount)
    {
        return new GDictionary
        {
            ["query_kind"] = QueryKind,
            ["target_coord_valid"] = TargetCoordValid,
            ["visited_count"] = VisitedCount,
            ["rejected_count"] = Ok ? 0 : 1,
            ["destination_count"] = destinationCount,
            ["overlay_override_count"] = OverlayOverrideCount,
            ["reject_reason"] = RejectReason,
        };
    }

    private static void AddAll(List<Vector2I> target, IEnumerable<Vector2I> values)
    {
        if (values == null)
        {
            return;
        }
        foreach (Vector2I value in values)
        {
            target.Add(value);
        }
    }

    private static GArray VectorListToArray(IEnumerable<Vector2I> values)
    {
        GArray result = new();
        if (values == null)
        {
            return result;
        }
        foreach (Vector2I value in values)
        {
            result.Add(value);
        }
        return result;
    }
}

internal sealed class MovementDistanceBandResult
{
    private static readonly Vector2I InvalidCoord = new(-1, -1);
    private readonly List<Vector2I> _coords = new();
    private readonly List<Vector2I> _path = new();

    public bool Ok { get; private set; }
    public StringName QueryKind { get; private set; } = "";
    public StringName RejectReason { get; private set; } = "";
    public IReadOnlyList<Vector2I> Coords => _coords;
    public Vector2I TargetCoord { get; private set; } = InvalidCoord;
    public IReadOnlyList<Vector2I> Path => _path;
    public int Cost { get; private set; }
    public int VisitedCount { get; private set; }
    public bool TargetCoordValid { get; private set; }
    public int OverlayOverrideCount { get; private set; }

    internal static MovementDistanceBandResult Success(
        StringName queryKind,
        IEnumerable<Vector2I> coords,
        Vector2I targetCoord,
        IEnumerable<Vector2I> path,
        int cost,
        int visitedCount,
        bool targetCoordValid,
        int overlayOverrideCount
    )
    {
        MovementDistanceBandResult result = new()
        {
            Ok = true,
            QueryKind = queryKind,
            RejectReason = "",
            TargetCoord = targetCoord,
            Cost = cost,
            VisitedCount = visitedCount,
            TargetCoordValid = targetCoordValid,
            OverlayOverrideCount = overlayOverrideCount,
        };
        AddAll(result._coords, coords);
        AddAll(result._path, path);
        return result;
    }

    internal static MovementDistanceBandResult Failure(StringName queryKind, StringName rejectReason)
    {
        return new MovementDistanceBandResult
        {
            Ok = false,
            QueryKind = queryKind,
            RejectReason = rejectReason,
            TargetCoord = InvalidCoord,
        };
    }

    internal GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["ok"] = Ok,
            ["coords"] = VectorListToArray(_coords),
            ["target_coord"] = TargetCoord,
            ["path"] = VectorListToArray(_path),
            ["cost"] = Cost,
            ["visited_count"] = VisitedCount,
            ["reject_reason"] = RejectReason,
            ["telemetry"] = BuildTelemetry(_coords.Count),
        };
    }

    private GDictionary BuildTelemetry(int destinationCount)
    {
        return new GDictionary
        {
            ["query_kind"] = QueryKind,
            ["target_coord_valid"] = TargetCoordValid,
            ["visited_count"] = VisitedCount,
            ["rejected_count"] = Ok ? 0 : 1,
            ["destination_count"] = destinationCount,
            ["overlay_override_count"] = OverlayOverrideCount,
            ["reject_reason"] = RejectReason,
        };
    }

    private static void AddAll(List<Vector2I> target, IEnumerable<Vector2I> values)
    {
        if (values == null)
        {
            return;
        }
        foreach (Vector2I value in values)
        {
            target.Add(value);
        }
    }

    private static GArray VectorListToArray(IEnumerable<Vector2I> values)
    {
        GArray result = new();
        if (values == null)
        {
            return result;
        }
        foreach (Vector2I value in values)
        {
            result.Add(value);
        }
        return result;
    }
}

internal sealed class MovementPathTargetCandidate
{
    public Vector2I DestinationCoord = new(-1, -1);
    public Vector2I Coord = new(-1, -1);
    public int PathCost;
    public int PathLength;
    public int SpentCost;
}

internal sealed class MovementPathTargetResult
{
    private static readonly StringName QueryDistanceBandPathTargets = "distance_band_path_targets";

    public bool Ok;
    public StringName RejectReason = "";
    public readonly List<MovementPathTargetCandidate> Candidates = new();
    public int DestinationCount;
    public int ReachedDestinationCount;
    public int UnreachableDestinationCount;
    public int PathRejectCount;
    public int SkippedOriginCount;
    public int VisitedCount;
    public int SearchVisitedCount;
    public int DestinationScanCount;
    public int RelaxCount;
    public bool BudgetExhausted;

    public static MovementPathTargetResult Failure(StringName rejectReason)
    {
        return new MovementPathTargetResult
        {
            Ok = false,
            RejectReason = rejectReason,
            PathRejectCount = 1,
        };
    }

    internal GDictionary ToDictionary()
    {
        GArray destinationCoords = new();
        GArray targetCoords = new();
        GArray costs = new();
        GArray pathLengths = new();
        GArray spentCosts = new();
        foreach (MovementPathTargetCandidate candidate in Candidates)
        {
            if (candidate == null)
            {
                continue;
            }
            destinationCoords.Add(candidate.DestinationCoord);
            targetCoords.Add(candidate.Coord);
            costs.Add(candidate.PathCost);
            pathLengths.Add(candidate.PathLength);
            spentCosts.Add(candidate.SpentCost);
        }
        return new GDictionary
        {
            ["ok"] = Ok,
            ["destination_coords"] = destinationCoords,
            ["target_coords"] = targetCoords,
            ["costs"] = costs,
            ["path_lengths"] = pathLengths,
            ["spent_costs"] = spentCosts,
            ["path_reject_count"] = PathRejectCount,
            ["skipped_origin_count"] = SkippedOriginCount,
            ["destination_count"] = DestinationCount,
            ["reached_destination_count"] = ReachedDestinationCount,
            ["unreachable_destination_count"] = UnreachableDestinationCount,
            ["candidate_count"] = targetCoords.Count,
            ["visited_count"] = VisitedCount,
            ["reject_reason"] = RejectReason,
            ["telemetry"] = new GDictionary
            {
                ["query_kind"] = QueryDistanceBandPathTargets,
                ["destination_count"] = DestinationCount,
                ["reached_destination_count"] = ReachedDestinationCount,
                ["unreachable_destination_count"] = UnreachableDestinationCount,
                ["candidate_count"] = targetCoords.Count,
                ["path_reject_count"] = PathRejectCount,
                ["skipped_origin_count"] = SkippedOriginCount,
                ["visited_count"] = VisitedCount,
                ["search_visited_count"] = SearchVisitedCount,
                ["destination_scan_count"] = DestinationScanCount,
                ["relax_count"] = RelaxCount,
                ["budget_exhausted"] = BudgetExhausted,
                ["reject_reason"] = RejectReason,
            },
        };
    }
}
