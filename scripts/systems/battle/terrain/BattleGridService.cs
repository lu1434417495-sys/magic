using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal readonly record struct BattleHeightDeltaResult(
    bool Changed,
    int BeforeHeight,
    int AfterHeight,
    int AppliedDelta
);

public sealed class BattleGridService : IDisposable
{
    private static readonly StringName JumpStrengthAttribute = "strength";
    private const int MinRuntimeHeight = -5;
    private const int MaxRuntimeHeight = 8;
    private const double JumpRedistributionFactor = 0.7;
    private const int JumpSizeStrCost = 2;
    private const int JumpSmallAgilityBonus = 1;
    private const double MinJumpArcRatio = 0.15;
    private const int InfiniteCost = int.MaxValue;

    private readonly BattleEdgeService _edgeService = new();

    private readonly struct MovePathNode
    {
        public readonly int Priority;
        public readonly int Cost;
        public readonly Vector2I Coord;

        public MovePathNode(int priority, int cost, Vector2I coord)
        {
            Priority = priority;
            Cost = cost;
            Coord = coord;
        }
    }

    public void Dispose()
    {
    }

    internal BattleCellState GetCellState(BattleState state, Vector2I coord)
    {
        return GetCell(state, coord);
    }

    internal bool HasCell(BattleState state, Vector2I coord)
    {
        return GetCell(state, coord) != null;
    }

    internal StringName GetCellBaseTerrainId(BattleState state, Vector2I coord)
    {
        return GetCell(state, coord)?.base_terrain ?? "";
    }

    internal BattleUnitState GetUnitAtCoord(BattleState state, Vector2I coord)
    {
        BattleCellState cell = GetCell(state, coord);
        if (cell == null || IsEmpty(cell.occupant_unit_id))
        {
            return null;
        }
        return GetUnit(state, cell.occupant_unit_id);
    }

    internal bool IsInside(BattleState state, Vector2I coord)
    {
        if (state == null)
        {
            return false;
        }
        Vector2I mapSize = GetMapSize(state);
        return coord.X >= 0 && coord.Y >= 0 && coord.X < mapSize.X && coord.Y < mapSize.Y;
    }

    internal List<Vector2I> GetNeighbors4(BattleState state, Vector2I coord)
    {
        var neighbors = new List<Vector2I>();
        AddNeighborIfInside(state, neighbors, coord + Vector2I.Left);
        AddNeighborIfInside(state, neighbors, coord + Vector2I.Right);
        AddNeighborIfInside(state, neighbors, coord + Vector2I.Up);
        AddNeighborIfInside(state, neighbors, coord + Vector2I.Down);
        return neighbors;
    }

    internal List<Vector2I> GetFootprintCoords(Vector2I anchor_coord, Vector2I footprint_size)
    {
        var coords = new List<Vector2I>();
        Vector2I normalizedSize = new(Math.Max(footprint_size.X, 1), Math.Max(footprint_size.Y, 1));
        for (int y = 0; y < normalizedSize.Y; y++)
        {
            for (int x = 0; x < normalizedSize.X; x++)
            {
                coords.Add(anchor_coord + new Vector2I(x, y));
            }
        }
        return coords;
    }

    internal List<Vector2I> GetUnitTargetCoords(BattleUnitState unit_state, Vector2I anchor_coord)
    {
        if (unit_state == null)
        {
            return new List<Vector2I>();
        }
        Vector2I footprintSize = unit_state.footprint_size;
        if (footprintSize == Vector2I.Zero)
        {
            footprintSize = BattleUnitState.GetFootprintSizeForBodySize(unit_state.body_size);
        }
        return GetFootprintCoords(anchor_coord, footprintSize);
    }

    internal List<Vector2I> GetUnitTargetCoords(
        BattleUnitReadView unitView,
        Vector2I anchor_coord
    )
    {
        if (!unitView.IsValid)
        {
            return new List<Vector2I>();
        }
        var result = new List<Vector2I>();
        foreach (Vector2I coord in unitView.GetTargetCoords(anchor_coord))
            result.Add(coord);
        return result;
    }

    internal int GetDistance(Vector2I from_coord, Vector2I to_coord)
    {
        return Math.Abs(from_coord.X - to_coord.X) + Math.Abs(from_coord.Y - to_coord.Y);
    }

    internal List<Vector2I> GetAreaCoords(
        BattleState state,
        Vector2I center_coord,
        StringName area_pattern,
        int area_value
    )
    {
        return GetAreaCoords(state, center_coord, area_pattern, area_value, Vector2I.Zero);
    }

    internal List<Vector2I> GetAreaCoords(
        BattleState state,
        Vector2I center_coord,
        StringName area_pattern,
        int area_value,
        Vector2I facing_direction
    )
    {
        var coords = new List<Vector2I>();
        if (state == null || !IsInside(state, center_coord))
        {
            return coords;
        }

        int radius = Math.Max(area_value, 0);
        BattleAreaPattern areaPattern = BattleTypedNames.ToAreaPattern(area_pattern);
        if (
            areaPattern == BattleAreaPattern.Unknown
            || areaPattern == BattleAreaPattern.Single
            || areaPattern == BattleAreaPattern.Self
            || radius <= 0
        )
        {
            coords.Add(center_coord);
            return coords;
        }

        if (areaPattern == BattleAreaPattern.Diamond)
        {
            for (int y = center_coord.Y - radius; y <= center_coord.Y + radius; y++)
            {
                for (int x = center_coord.X - radius; x <= center_coord.X + radius; x++)
                {
                    var coord = new Vector2I(x, y);
                    if (
                        IsInside(state, coord)
                        && Math.Abs(coord.X - center_coord.X) + Math.Abs(coord.Y - center_coord.Y)
                            <= radius
                    )
                    {
                        coords.Add(coord);
                    }
                }
            }
        }
        else if (
            areaPattern == BattleAreaPattern.Square
            || areaPattern == BattleAreaPattern.Radius
        )
        {
            for (int y = center_coord.Y - radius; y <= center_coord.Y + radius; y++)
            {
                for (int x = center_coord.X - radius; x <= center_coord.X + radius; x++)
                {
                    var coord = new Vector2I(x, y);
                    if (
                        IsInside(state, coord)
                        && Math.Max(
                            Math.Abs(coord.X - center_coord.X),
                            Math.Abs(coord.Y - center_coord.Y)
                        ) <= radius
                    )
                    {
                        coords.Add(coord);
                    }
                }
            }
        }
        else if (areaPattern == BattleAreaPattern.Cross)
        {
            for (int y = center_coord.Y - radius; y <= center_coord.Y + radius; y++)
            {
                for (int x = center_coord.X - radius; x <= center_coord.X + radius; x++)
                {
                    var coord = new Vector2I(x, y);
                    int dx = Math.Abs(coord.X - center_coord.X);
                    int dy = Math.Abs(coord.Y - center_coord.Y);
                    if (
                        IsInside(state, coord)
                        && ((dx == 0 && dy <= radius) || (dy == 0 && dx <= radius))
                    )
                    {
                        coords.Add(coord);
                    }
                }
            }
        }
        else if (areaPattern == BattleAreaPattern.Line)
        {
            return BuildLineCoords(state, center_coord, radius, facing_direction);
        }
        else if (areaPattern == BattleAreaPattern.Cone)
        {
            return BuildConeCoords(state, center_coord, radius, facing_direction);
        }
        else if (areaPattern == BattleAreaPattern.NarrowCone)
        {
            return BuildNarrowConeCoords(state, center_coord, radius, facing_direction);
        }
        else if (areaPattern == BattleAreaPattern.FrontArc)
        {
            return BuildFrontArcCoords(state, center_coord, radius, facing_direction);
        }
        else
        {
            coords.Add(center_coord);
        }
        return SortUniqueCoords(coords);
    }

    private List<Vector2I> BuildLineCoords(
        BattleState state,
        Vector2I center_coord,
        int radius,
        Vector2I facing_direction
    )
    {
        var coords = new List<Vector2I>();
        if (radius <= 0)
        {
            coords.Add(center_coord);
            return coords;
        }
        if (GetDirectionalLineAxis(state, center_coord, facing_direction) == 0)
        {
            for (int x = center_coord.X - radius; x <= center_coord.X + radius; x++)
            {
                var coord = new Vector2I(x, center_coord.Y);
                if (IsInside(state, coord))
                {
                    coords.Add(coord);
                }
            }
        }
        else
        {
            for (int y = center_coord.Y - radius; y <= center_coord.Y + radius; y++)
            {
                var coord = new Vector2I(center_coord.X, y);
                if (IsInside(state, coord))
                {
                    coords.Add(coord);
                }
            }
        }
        return SortUniqueCoords(coords);
    }

    private List<Vector2I> BuildConeCoords(
        BattleState state,
        Vector2I center_coord,
        int radius,
        Vector2I facing_direction
    )
    {
        var coords = new List<Vector2I> { center_coord };
        if (radius <= 0)
        {
            return coords;
        }
        AddConeCoords(
            state,
            coords,
            center_coord,
            radius,
            ResolveAreaDirection(state, center_coord, facing_direction),
            wide: true
        );
        return SortUniqueCoords(coords);
    }

    private List<Vector2I> BuildNarrowConeCoords(
        BattleState state,
        Vector2I center_coord,
        int radius,
        Vector2I facing_direction
    )
    {
        var coords = new List<Vector2I>();
        if (radius <= 0)
        {
            coords.Add(center_coord);
            return coords;
        }
        AddConeCoords(
            state,
            coords,
            center_coord,
            radius,
            ResolveAreaDirection(state, center_coord, facing_direction),
            wide: false
        );
        return SortUniqueCoords(coords);
    }

    private List<Vector2I> BuildFrontArcCoords(
        BattleState state,
        Vector2I center_coord,
        int radius,
        Vector2I facing_direction
    )
    {
        var coords = new List<Vector2I>();
        int arcRadius = Math.Max(radius, 0);
        Vector2I direction = ResolveAreaDirection(state, center_coord, facing_direction);
        if (direction == Vector2I.Zero)
        {
            direction = Vector2I.Right;
        }
        if (direction.X != 0)
        {
            for (int offset = -arcRadius; offset <= arcRadius; offset++)
            {
                var coord = new Vector2I(center_coord.X, center_coord.Y + offset);
                if (IsInside(state, coord))
                {
                    coords.Add(coord);
                }
            }
        }
        else
        {
            for (int offset = -arcRadius; offset <= arcRadius; offset++)
            {
                var coord = new Vector2I(center_coord.X + offset, center_coord.Y);
                if (IsInside(state, coord))
                {
                    coords.Add(coord);
                }
            }
        }
        return SortUniqueCoords(coords);
    }

    private int GetDirectionalLineAxis(
        BattleState state,
        Vector2I center_coord,
        Vector2I facing_direction
    )
    {
        Vector2I normalizedDirection = NormalizeAreaDirection(facing_direction);
        if (normalizedDirection != Vector2I.Zero)
        {
            return normalizedDirection.X != 0 ? 0 : 1;
        }
        return GetStableLineAxis(state, center_coord);
    }

    private int GetStableLineAxis(BattleState state, Vector2I center_coord)
    {
        Vector2I mapSize = GetMapSize(state);
        int horizontalSpan = Math.Min(center_coord.X, mapSize.X - 1 - center_coord.X);
        int verticalSpan = Math.Min(center_coord.Y, mapSize.Y - 1 - center_coord.Y);
        return horizontalSpan >= verticalSpan ? 0 : 1;
    }

    private Vector2I ResolveAreaDirection(
        BattleState state,
        Vector2I center_coord,
        Vector2I facing_direction
    )
    {
        Vector2I normalizedDirection = NormalizeAreaDirection(facing_direction);
        return normalizedDirection != Vector2I.Zero
            ? normalizedDirection
            : GetStableConeDirection(state, center_coord);
    }

    private Vector2I GetStableConeDirection(BattleState state, Vector2I center_coord)
    {
        Vector2I mapSize = GetMapSize(state);
        int rightSpan = Math.Max(mapSize.X - 1 - center_coord.X, 0);
        int leftSpan = Math.Max(center_coord.X, 0);
        int downSpan = Math.Max(mapSize.Y - 1 - center_coord.Y, 0);
        int upSpan = Math.Max(center_coord.Y, 0);
        Vector2I bestDirection = Vector2I.Right;
        int bestSpan = rightSpan;
        if (leftSpan > bestSpan)
        {
            bestDirection = Vector2I.Left;
            bestSpan = leftSpan;
        }
        if (downSpan > bestSpan)
        {
            bestDirection = Vector2I.Down;
            bestSpan = downSpan;
        }
        if (upSpan > bestSpan)
        {
            bestDirection = Vector2I.Up;
        }
        return bestDirection;
    }

    private Vector2I NormalizeAreaDirection(Vector2I direction)
    {
        if (direction == Vector2I.Zero)
        {
            return Vector2I.Zero;
        }
        int absX = Math.Abs(direction.X);
        int absY = Math.Abs(direction.Y);
        if (absX >= absY && absX > 0)
        {
            return new Vector2I(direction.X > 0 ? 1 : -1, 0);
        }
        if (absY > 0)
        {
            return new Vector2I(0, direction.Y > 0 ? 1 : -1);
        }
        return Vector2I.Zero;
    }

    private List<Vector2I> SortUniqueCoords(IEnumerable<Vector2I> coords)
    {
        if (coords == null)
        {
            return new List<Vector2I>();
        }
        var uniqueCoords = new List<Vector2I>();
        var seen = new HashSet<Vector2I>();
        foreach (Vector2I coord in coords)
        {
            if (seen.Add(coord))
            {
                uniqueCoords.Add(coord);
            }
        }
        return uniqueCoords;
    }

    internal int GetDistanceFromUnitToCoord(BattleUnitState unit_state, Vector2I target_coord)
    {
        if (unit_state == null)
        {
            return 999999;
        }
        int bestDistance = 999999;
        foreach (Vector2I occupiedCoord in unit_state.occupied_coords)
        {
            bestDistance = Math.Min(bestDistance, GetDistance(occupiedCoord, target_coord));
        }
        return bestDistance;
    }

    internal int GetDistanceBetweenUnits(BattleUnitState first_unit, BattleUnitState second_unit)
    {
        if (first_unit == null || second_unit == null)
        {
            return 999999;
        }
        int bestDistance = 999999;
        foreach (Vector2I firstCoord in first_unit.occupied_coords)
        {
            foreach (Vector2I secondCoord in second_unit.occupied_coords)
            {
                bestDistance = Math.Min(bestDistance, GetDistance(firstCoord, secondCoord));
            }
        }
        return bestDistance;
    }

    internal int GetDistanceBetweenUnits(BattleUnitReadView firstUnit, BattleUnitReadView secondUnit)
    {
        if (!firstUnit.IsValid || !secondUnit.IsValid)
        {
            return 999999;
        }
        int bestDistance = 999999;
        foreach (Vector2I firstCoord in firstUnit.GetOccupiedCoords())
        {
            foreach (Vector2I secondCoord in secondUnit.GetOccupiedCoords())
            {
                bestDistance = Math.Min(bestDistance, GetDistance(firstCoord, secondCoord));
            }
        }
        return bestDistance;
    }

    internal int GetDistanceFromUnitToCoord(BattleUnitReadView unitView, Vector2I target_coord)
    {
        if (!unitView.IsValid)
        {
            return 999999;
        }
        int bestDistance = 999999;
        foreach (Vector2I occupiedCoord in unitView.GetOccupiedCoords())
        {
            bestDistance = Math.Min(bestDistance, GetDistance(occupiedCoord, target_coord));
        }
        return bestDistance;
    }

    private bool IsWalkable(BattleState state, Vector2I coord)
    {
        return CanPlaceFootprint(state, coord, Vector2I.One, "", null);
    }

    private bool CanEnterCell(BattleState state, Vector2I coord)
    {
        return CanPlaceFootprint(state, coord, Vector2I.One, "", null);
    }

    internal bool CanPlaceFootprint(
        BattleState state,
        Vector2I anchor_coord,
        Vector2I footprint_size,
        StringName ignored_unit_id,
        BattleUnitState unit_state
    )
    {
        return CanPlaceFootprintCore(
            state,
            anchor_coord,
            footprint_size,
            ignored_unit_id,
            unit_state,
            ignore_occupants: false
        );
    }

    internal bool CanFitFootprintIgnoringOccupants(
        BattleState state,
        Vector2I anchor_coord,
        Vector2I footprint_size,
        BattleUnitState unit_state
    )
    {
        return CanPlaceFootprintCore(
            state,
            anchor_coord,
            footprint_size,
            "",
            unit_state,
            ignore_occupants: true
        );
    }

    private bool CanPlaceFootprintCore(
        BattleState state,
        Vector2I anchor_coord,
        Vector2I footprint_size,
        StringName ignored_unit_id,
        BattleUnitState unit_state,
        bool ignore_occupants
    )
    {
        var footprintCoords = GetFootprintCoords(anchor_coord, footprint_size);
        var footprintLookup = new HashSet<Vector2I>();
        foreach (Vector2I footprintCoord in footprintCoords)
        {
            footprintLookup.Add(footprintCoord);
            if (!IsInside(state, footprintCoord))
            {
                return false;
            }
            BattleCellState cell = GetCell(state, footprintCoord);
            if (cell == null)
            {
                return false;
            }
            if (unit_state != null)
            {
                if (!CanUnitEnterCell(cell, unit_state))
                {
                    return false;
                }
            }
            else if (!cell.passable)
            {
                return false;
            }
            if (
                !ignore_occupants
                && !IsEmpty(cell.occupant_unit_id)
                && cell.occupant_unit_id != (ignored_unit_id ?? "")
            )
            {
                return false;
            }
        }
        foreach (Vector2I footprintCoord in footprintCoords)
        {
            foreach (Vector2I direction in RightDownDirections)
            {
                Vector2I neighborCoord = footprintCoord + direction;
                if (!footprintLookup.Contains(neighborCoord))
                {
                    continue;
                }
                if (EdgeBlocksOccupancyBetween(state, footprintCoord, neighborCoord))
                {
                    return false;
                }
            }
        }
        return true;
    }

    internal bool CanPlaceFootprint(
        BattleState state,
        Vector2I anchor_coord,
        Vector2I footprint_size,
        StringName ignored_unit_id,
        BattleUnitReadView unitView
    )
    {
        var footprintCoords = GetFootprintCoords(anchor_coord, footprint_size);
        var footprintLookup = new HashSet<Vector2I>();
        foreach (Vector2I footprintCoord in footprintCoords)
        {
            footprintLookup.Add(footprintCoord);
            if (!IsInside(state, footprintCoord))
            {
                return false;
            }
            BattleCellState cell = GetCell(state, footprintCoord);
            if (cell == null)
            {
                return false;
            }
            if (unitView.IsValid)
            {
                if (!CanUnitEnterCell(cell, unitView))
                {
                    return false;
                }
            }
            else if (!cell.passable)
            {
                return false;
            }
            if (!IsEmpty(cell.occupant_unit_id) && cell.occupant_unit_id != (ignored_unit_id ?? ""))
            {
                return false;
            }
        }
        foreach (Vector2I footprintCoord in footprintCoords)
        {
            foreach (Vector2I direction in RightDownDirections)
            {
                Vector2I neighborCoord = footprintCoord + direction;
                if (!footprintLookup.Contains(neighborCoord))
                {
                    continue;
                }
                if (EdgeBlocksOccupancyBetween(state, footprintCoord, neighborCoord))
                {
                    return false;
                }
            }
        }
        return true;
    }

    internal GStringNameArray CollectBlockingUnitIds(
        BattleState state,
        BattleUnitState unit_state,
        Vector2I target_coord
    )
    {
        var blockingIds = new GStringNameArray();
        if (unit_state == null)
        {
            return blockingIds;
        }
        var seenIds = new HashSet<StringName>();
        foreach (Vector2I footprintCoord in GetUnitTargetCoords(unit_state, target_coord))
        {
            BattleCellState cell = GetCell(state, footprintCoord);
            if (cell == null)
            {
                continue;
            }
            StringName occupantUnitId = cell.occupant_unit_id;
            if (
                IsEmpty(occupantUnitId)
                || occupantUnitId == unit_state.unit_id
                || !seenIds.Add(occupantUnitId)
            )
            {
                continue;
            }
            blockingIds.Add(occupantUnitId);
        }
        return blockingIds;
    }

    internal bool CanPlaceUnit(
        BattleState state,
        BattleUnitState unit_state,
        Vector2I target_coord,
        bool ignore_height = false
    )
    {
        if (state == null || unit_state == null)
        {
            return false;
        }
        if (
            !CanPlaceFootprint(
                state,
                target_coord,
                unit_state.footprint_size,
                unit_state.unit_id,
                unit_state
            )
        )
        {
            return false;
        }
        if (ignore_height)
        {
            return true;
        }

        Vector2I delta = target_coord - unit_state.coord;
        if (delta == Vector2I.Zero)
        {
            return true;
        }
        if (GetDistance(Vector2I.Zero, delta) == 1)
        {
            return CanUnitStepAcrossEdges(state, unit_state, delta);
        }
        var currentCoords = new HashSet<Vector2I>();
        foreach (Vector2I occupiedCoord in unit_state.occupied_coords)
        {
            currentCoords.Add(occupiedCoord);
        }

        foreach (Vector2I footprintCoord in GetUnitTargetCoords(unit_state, target_coord))
        {
            BattleCellState targetCell = GetCell(state, footprintCoord);
            if (targetCell == null)
            {
                return false;
            }
            Vector2I referenceCoord =
                delta != Vector2I.Zero ? footprintCoord - delta : unit_state.coord;
            if (!currentCoords.Contains(referenceCoord))
            {
                referenceCoord = unit_state.coord;
            }
            BattleCellState referenceCell = GetCell(state, referenceCoord);
            if (referenceCell == null)
            {
                return false;
            }
            if (Math.Abs(referenceCell.current_height - targetCell.current_height) > 1)
            {
                return false;
            }
        }
        return true;
    }

    internal bool CanPlaceUnit(
        BattleState state,
        BattleUnitReadView unitView,
        Vector2I target_coord,
        bool ignore_height = false
    )
    {
        if (state == null || !unitView.IsValid)
        {
            return false;
        }
        if (
            !CanPlaceFootprint(
                state,
                target_coord,
                unitView.FootprintSize,
                unitView.UnitId,
                unitView
            )
        )
        {
            return false;
        }
        if (ignore_height)
        {
            return true;
        }

        Vector2I delta = target_coord - unitView.Coord;
        if (delta == Vector2I.Zero)
        {
            return true;
        }
        if (GetDistance(Vector2I.Zero, delta) == 1)
        {
            return CanAnchorStepAcrossEdges(state, unitView.FootprintSize, unitView.Coord, delta);
        }
        var currentCoords = new HashSet<Vector2I>();
        foreach (Vector2I occupiedCoord in unitView.GetOccupiedCoords())
        {
            currentCoords.Add(occupiedCoord);
        }

        foreach (Vector2I footprintCoord in unitView.GetTargetCoords(target_coord))
        {
            BattleCellState targetCell = GetCell(state, footprintCoord);
            if (targetCell == null)
            {
                return false;
            }
            Vector2I referenceCoord =
                delta != Vector2I.Zero ? footprintCoord - delta : unitView.Coord;
            if (!currentCoords.Contains(referenceCoord))
            {
                referenceCoord = unitView.Coord;
            }
            BattleCellState referenceCell = GetCell(state, referenceCoord);
            if (referenceCell == null)
            {
                return false;
            }
            if (Math.Abs(referenceCell.current_height - targetCell.current_height) > 1)
            {
                return false;
            }
        }
        return true;
    }

    internal BattleEdgeFaceState GetEdgeFace(
        BattleState state,
        Vector2I from_coord,
        Vector2I to_coord
    )
    {
        return _edgeService?.GetEdgeFace(state, from_coord, to_coord);
    }

    private bool CanUnitStepAcrossEdges(
        BattleState state,
        BattleUnitState unit_state,
        Vector2I delta
    )
    {
        if (state == null || unit_state == null)
        {
            return false;
        }
        return CanAnchorStepAcrossEdges(
            state,
            unit_state.footprint_size,
            unit_state.coord,
            delta
        );
    }

    private bool CanAnchorStepAcrossEdges(
        BattleState state,
        Vector2I footprint_size,
        Vector2I anchor_coord,
        Vector2I delta
    )
    {
        if (delta == Vector2I.Right)
        {
            for (int y = 0; y < footprint_size.Y; y++)
            {
                Vector2I fromCoord = anchor_coord + new Vector2I(footprint_size.X - 1, y);
                if (!EdgeIsTraversableBetween(state, fromCoord, fromCoord + Vector2I.Right))
                {
                    return false;
                }
            }
        }
        else if (delta == Vector2I.Left)
        {
            for (int y = 0; y < footprint_size.Y; y++)
            {
                Vector2I fromCoord = anchor_coord + new Vector2I(0, y);
                if (!EdgeIsTraversableBetween(state, fromCoord, fromCoord + Vector2I.Left))
                {
                    return false;
                }
            }
        }
        else if (delta == Vector2I.Down)
        {
            for (int x = 0; x < footprint_size.X; x++)
            {
                Vector2I fromCoord = anchor_coord + new Vector2I(x, footprint_size.Y - 1);
                if (!EdgeIsTraversableBetween(state, fromCoord, fromCoord + Vector2I.Down))
                {
                    return false;
                }
            }
        }
        else if (delta == Vector2I.Up)
        {
            for (int x = 0; x < footprint_size.X; x++)
            {
                Vector2I fromCoord = anchor_coord + new Vector2I(x, 0);
                if (!EdgeIsTraversableBetween(state, fromCoord, fromCoord + Vector2I.Up))
                {
                    return false;
                }
            }
        }
        else
        {
            return false;
        }
        return true;
    }

    private bool IsWallBlocked(BattleState state, Vector2I from_coord, Vector2I to_coord)
    {
        return _edgeService != null
            && _edgeService.HasFeatureBetween(
                state,
                from_coord,
                to_coord,
                BattleEdgeFeatureKind.Wall
            );
    }

    internal bool CanTraverse(
        BattleState state,
        Vector2I from_coord,
        Vector2I to_coord,
        BattleUnitState unit_state = null
    )
    {
        if (GetDistance(from_coord, to_coord) != 1)
        {
            return false;
        }
        if (IsWallBlocked(state, from_coord, to_coord))
        {
            return false;
        }
        if (unit_state != null)
        {
            return CanPlaceUnit(state, unit_state, to_coord);
        }
        if (!IsInside(state, to_coord) || !CanEnterCell(state, to_coord))
        {
            return false;
        }
        return EdgeIsTraversableBetween(state, from_coord, to_coord);
    }

    internal bool CanUnitStepBetweenAnchors(
        BattleState state,
        BattleUnitState unit_state,
        Vector2I from_anchor,
        Vector2I to_anchor
    )
    {
        if (state == null || unit_state == null)
        {
            return false;
        }
        Vector2I delta = to_anchor - from_anchor;
        if (GetDistance(from_anchor, to_anchor) != 1)
        {
            return false;
        }
        if (
            !CanPlaceFootprint(
                state,
                to_anchor,
                unit_state.footprint_size,
                unit_state.unit_id,
                unit_state
            )
        )
        {
            return false;
        }
        if (!CanAnchorStepAcrossEdges(state, unit_state.footprint_size, from_anchor, delta))
        {
            return false;
        }

        foreach (Vector2I footprintCoord in GetUnitTargetCoords(unit_state, to_anchor))
        {
            BattleCellState targetCell = GetCell(state, footprintCoord);
            BattleCellState referenceCell = GetCell(state, footprintCoord - delta);
            if (targetCell == null || referenceCell == null)
            {
                return false;
            }
            if (Math.Abs(referenceCell.current_height - targetCell.current_height) > 1)
            {
                return false;
            }
        }
        return true;
    }

    internal bool CanUnitStepBetweenAnchors(
        BattleState state,
        BattleUnitReadView unitView,
        Vector2I from_anchor,
        Vector2I to_anchor
    )
    {
        if (state == null || !unitView.IsValid)
        {
            return false;
        }
        Vector2I delta = to_anchor - from_anchor;
        if (GetDistance(from_anchor, to_anchor) != 1)
        {
            return false;
        }
        if (
            !CanPlaceFootprint(
                state,
                to_anchor,
                unitView.FootprintSize,
                unitView.UnitId,
                unitView
            )
        )
        {
            return false;
        }
        if (!CanAnchorStepAcrossEdges(state, unitView.FootprintSize, from_anchor, delta))
        {
            return false;
        }

        foreach (Vector2I footprintCoord in unitView.GetTargetCoords(to_anchor))
        {
            BattleCellState targetCell = GetCell(state, footprintCoord);
            BattleCellState referenceCell = GetCell(state, footprintCoord - delta);
            if (targetCell == null || referenceCell == null)
            {
                return false;
            }
            if (Math.Abs(referenceCell.current_height - targetCell.current_height) > 1)
            {
                return false;
            }
        }
        return true;
    }

    internal int GetUnitMoveCost(
        BattleState state,
        BattleUnitState unit_state,
        Vector2I target_coord
    )
    {
        if (state == null || unit_state == null)
        {
            return 1;
        }
        int moveCost = 1;
        foreach (Vector2I occupiedCoord in GetUnitTargetCoords(unit_state, target_coord))
        {
            BattleCellState cell = GetCell(state, occupiedCoord);
            if (cell == null)
            {
                continue;
            }
            moveCost = Math.Max(
                moveCost,
                BattleTerrainRules.GetUnitMoveCost(cell.base_terrain, unit_state.movement_tags)
            );
        }
        return moveCost;
    }

    internal int GetUnitMoveCost(
        BattleState state,
        BattleUnitReadView unitView,
        Vector2I target_coord
    )
    {
        if (state == null || !unitView.IsValid)
        {
            return 1;
        }
        int moveCost = 1;
        foreach (Vector2I occupiedCoord in unitView.GetTargetCoords(target_coord))
        {
            BattleCellState cell = GetCell(state, occupiedCoord);
            if (cell == null)
            {
                continue;
            }
            moveCost = Math.Max(
                moveCost,
                BattleTerrainRules.GetUnitMoveCost(cell.base_terrain, unitView.GetMovementTags())
            );
        }
        return moveCost;
    }

    internal BattleMovePathResult ResolveUnitMovePathTyped(
        BattleState state,
        BattleUnitState unit_state,
        Vector2I from_coord,
        Vector2I to_coord,
        int max_move_points,
        Func<BattleUnitState, Vector2I, int> move_cost_provider
    )
    {
        return ResolveUnitMovePathTypedCore(
            state,
            unit_state,
            from_coord,
            to_coord,
            max_move_points,
            move_cost_provider
        );
    }

    internal BattleMovePathResult ResolveUnitMovePathTyped(
        BattleState state,
        BattleUnitReadView unitView,
        Vector2I from_coord,
        Vector2I to_coord,
        int max_move_points,
        Func<BattleUnitReadView, Vector2I, int> move_cost_provider
    )
    {
        return ResolveUnitMovePathTypedCore(
            state,
            unitView,
            from_coord,
            to_coord,
            max_move_points,
            move_cost_provider
        );
    }

    private BattleMovePathResult ResolveUnitMovePathTypedCore(
        BattleState state,
        BattleUnitReadView unitView,
        Vector2I from_coord,
        Vector2I to_coord,
        int max_move_points,
        Func<BattleUnitReadView, Vector2I, int> move_cost_provider
    )
    {
        if (state == null)
        {
            return MovePathResult(false, 0, System.Array.Empty<Vector2I>(), "战斗状态不可用。");
        }
        if (!unitView.IsValid)
        {
            return MovePathResult(false, 0, System.Array.Empty<Vector2I>(), "当前单位数据不可用。");
        }
        if (!IsInside(state, from_coord))
        {
            return MovePathResult(false, 0, System.Array.Empty<Vector2I>(), "当前单位不在有效战斗格上。");
        }
        if (!IsInside(state, to_coord))
        {
            return MovePathResult(false, 0, System.Array.Empty<Vector2I>(), "已到达战斗地图边界。");
        }
        if (from_coord == to_coord)
        {
            return MovePathResult(true, 0, new[] { from_coord }, "可移动。");
        }

        int sanitizedMaxMovePoints = Math.Max(max_move_points, 0);
        var bestCosts = new Dictionary<Vector2I, int> { [from_coord] = 0 };
        var previous = new Dictionary<Vector2I, Vector2I>();
        var visited = new HashSet<Vector2I>();
        var heap = new List<MovePathNode>();
        HeapPush(heap, new MovePathNode(MovePathHeuristic(from_coord, to_coord), 0, from_coord));
        bool foundTarget = false;
        bool stoppedByMovePointCap = false;

        while (heap.Count > 0)
        {
            if (heap[0].Priority > sanitizedMaxMovePoints)
            {
                stoppedByMovePointCap = true;
                break;
            }

            MovePathNode entry = HeapPop(heap);
            int currentCost = entry.Cost;
            Vector2I currentCoord = entry.Coord;
            if (!visited.Add(currentCoord))
            {
                continue;
            }
            if (currentCoord == to_coord)
            {
                foundTarget = true;
                break;
            }
            foreach (Vector2I neighborCoord in GetNeighbors4(state, currentCoord))
            {
                if (visited.Contains(neighborCoord))
                {
                    continue;
                }
                if (!CanUnitStepBetweenAnchors(state, unitView, currentCoord, neighborCoord))
                {
                    continue;
                }
                int stepCost = GetUnitMoveCost(state, unitView, neighborCoord);
                if (move_cost_provider != null)
                {
                    stepCost = move_cost_provider.Invoke(unitView, neighborCoord);
                }
                int nextCost = currentCost + stepCost;
                if (
                    bestCosts.TryGetValue(neighborCoord, out int existingCost)
                    && nextCost >= existingCost
                )
                {
                    continue;
                }
                int h = MovePathHeuristic(neighborCoord, to_coord);
                long estimatedTotalCost = (long)nextCost + h;
                if (estimatedTotalCost > sanitizedMaxMovePoints)
                {
                    stoppedByMovePointCap = true;
                    continue;
                }

                bestCosts[neighborCoord] = nextCost;
                previous[neighborCoord] = currentCoord;
                HeapPush(heap, new MovePathNode((int)estimatedTotalCost, nextCost, neighborCoord));
            }
        }

        if (!foundTarget)
        {
            if (stoppedByMovePointCap)
            {
                return MovePathBudgetExceededResult(
                    state,
                    unitView,
                    from_coord,
                    to_coord,
                    sanitizedMaxMovePoints
                );
            }
            if (
                !CanPlaceFootprint(
                    state,
                    to_coord,
                    unitView.FootprintSize,
                    unitView.UnitId,
                    unitView
                )
            )
            {
                return MovePathResult(false, 0, System.Array.Empty<Vector2I>(), "目标区域不可放置当前单位。");
            }
            if (GetDistance(from_coord, to_coord) == 1)
            {
                return MovePathResult(
                    false,
                    GetUnitMoveCost(state, unitView, to_coord),
                    System.Array.Empty<Vector2I>(),
                    CanUnitStepBetweenAnchors(state, unitView, from_coord, to_coord)
                        ? "移动力不足，无法移动。"
                        : "目标区域不可放置当前单位。"
                );
            }
            return MovePathResult(false, 0, System.Array.Empty<Vector2I>(), "目标地格当前不可到达。");
        }

        int finalCost = bestCosts.TryGetValue(to_coord, out int resolvedCost)
            ? resolvedCost
            : InfiniteCost;
        List<Vector2I> anchorPath = ReconstructMovePath(previous, from_coord, to_coord);
        if (finalCost > sanitizedMaxMovePoints)
        {
            return MovePathResult(false, finalCost, anchorPath, "移动力不足，无法移动。");
        }
        return MovePathResult(true, finalCost, anchorPath, "可移动。");
    }

    private BattleMovePathResult ResolveUnitMovePathTypedCore(
        BattleState state,
        BattleUnitState unit_state,
        Vector2I from_coord,
        Vector2I to_coord,
        int max_move_points,
        Func<BattleUnitState, Vector2I, int> move_cost_provider
    )
    {
        if (state == null)
        {
            return MovePathResult(false, 0, System.Array.Empty<Vector2I>(), "战斗状态不可用。");
        }
        if (unit_state == null)
        {
            return MovePathResult(false, 0, System.Array.Empty<Vector2I>(), "当前单位数据不可用。");
        }
        if (!IsInside(state, from_coord))
        {
            return MovePathResult(false, 0, System.Array.Empty<Vector2I>(), "当前单位不在有效战斗格上。");
        }
        if (!IsInside(state, to_coord))
        {
            return MovePathResult(false, 0, System.Array.Empty<Vector2I>(), "已到达战斗地图边界。");
        }
        if (from_coord == to_coord)
        {
            return MovePathResult(true, 0, new[] { from_coord }, "可移动。");
        }

        int sanitizedMaxMovePoints = Math.Max(max_move_points, 0);
        var bestCosts = new Dictionary<Vector2I, int> { [from_coord] = 0 };
        var previous = new Dictionary<Vector2I, Vector2I>();
        var visited = new HashSet<Vector2I>();
        var heap = new List<MovePathNode>();
        HeapPush(heap, new MovePathNode(MovePathHeuristic(from_coord, to_coord), 0, from_coord));
        bool foundTarget = false;
        bool stoppedByMovePointCap = false;

        while (heap.Count > 0)
        {
            if (heap[0].Priority > sanitizedMaxMovePoints)
            {
                stoppedByMovePointCap = true;
                break;
            }

            MovePathNode entry = HeapPop(heap);
            int currentCost = entry.Cost;
            Vector2I currentCoord = entry.Coord;
            if (!visited.Add(currentCoord))
            {
                continue;
            }
            if (currentCoord == to_coord)
            {
                foundTarget = true;
                break;
            }
            foreach (Vector2I neighborCoord in GetNeighbors4(state, currentCoord))
            {
                if (visited.Contains(neighborCoord))
                {
                    continue;
                }
                if (!CanUnitStepBetweenAnchors(state, unit_state, currentCoord, neighborCoord))
                {
                    continue;
                }
                int stepCost = GetUnitMoveCost(state, unit_state, neighborCoord);
                if (move_cost_provider != null)
                {
                    stepCost = move_cost_provider.Invoke(unit_state, neighborCoord);
                }
                int nextCost = currentCost + stepCost;
                if (
                    bestCosts.TryGetValue(neighborCoord, out int existingCost)
                    && nextCost >= existingCost
                )
                {
                    continue;
                }
                int h = MovePathHeuristic(neighborCoord, to_coord);
                long estimatedTotalCost = (long)nextCost + h;
                if (estimatedTotalCost > sanitizedMaxMovePoints)
                {
                    stoppedByMovePointCap = true;
                    continue;
                }

                bestCosts[neighborCoord] = nextCost;
                previous[neighborCoord] = currentCoord;
                HeapPush(heap, new MovePathNode((int)estimatedTotalCost, nextCost, neighborCoord));
            }
        }

        if (!foundTarget)
        {
            if (stoppedByMovePointCap)
            {
                return MovePathBudgetExceededResult(
                    state,
                    unit_state,
                    from_coord,
                    to_coord,
                    sanitizedMaxMovePoints
                );
            }
            if (
                !CanPlaceFootprint(
                    state,
                    to_coord,
                    unit_state.footprint_size,
                    unit_state.unit_id,
                    unit_state
                )
            )
            {
                return MovePathResult(false, 0, System.Array.Empty<Vector2I>(), "目标区域不可放置当前单位。");
            }
            if (GetDistance(from_coord, to_coord) == 1)
            {
                MoveEvaluationResult directResult = EvaluateMoveTyped(
                    state,
                    from_coord,
                    to_coord,
                    unit_state
                );
                return MovePathResult(
                    false,
                    directResult.Cost,
                    System.Array.Empty<Vector2I>(),
                    directResult.Message
                );
            }
            return MovePathResult(false, 0, System.Array.Empty<Vector2I>(), "目标地格当前不可到达。");
        }

        int finalCost = bestCosts.TryGetValue(to_coord, out int resolvedCost)
            ? resolvedCost
            : InfiniteCost;
        List<Vector2I> anchorPath = ReconstructMovePath(previous, from_coord, to_coord);
        if (finalCost > sanitizedMaxMovePoints)
        {
            return MovePathResult(false, finalCost, anchorPath, "移动力不足，无法移动。");
        }
        return MovePathResult(true, finalCost, anchorPath, "可移动。");
    }

    private BattleMovePathResult MovePathBudgetExceededResult(
        BattleState state,
        BattleUnitState unit_state,
        Vector2I from_coord,
        Vector2I to_coord,
        int maxMovePoints
    )
    {
        if (
            !CanPlaceFootprint(
                state,
                to_coord,
                unit_state.footprint_size,
                unit_state.unit_id,
                unit_state
            )
        )
        {
            return MovePathResult(false, 0, System.Array.Empty<Vector2I>(), "目标区域不可放置当前单位。");
        }

        if (GetDistance(from_coord, to_coord) == 1)
        {
            MoveEvaluationResult directResult = EvaluateMoveTyped(
                state,
                from_coord,
                to_coord,
                unit_state
            );
            if (!directResult.Allowed)
            {
                return MovePathResult(
                    false,
                    directResult.Cost,
                    System.Array.Empty<Vector2I>(),
                    directResult.Message
                );
            }
            return MovePathResult(
                false,
                directResult.Cost,
                new[] { from_coord, to_coord },
                "移动力不足，无法移动。"
            );
        }

        int exceededCost = maxMovePoints < InfiniteCost ? maxMovePoints + 1 : InfiniteCost;
        return MovePathResult(false, exceededCost, System.Array.Empty<Vector2I>(), "移动力不足，无法移动。");
    }

    private BattleMovePathResult MovePathBudgetExceededResult(
        BattleState state,
        BattleUnitReadView unitView,
        Vector2I from_coord,
        Vector2I to_coord,
        int maxMovePoints
    )
    {
        if (
            !CanPlaceFootprint(
                state,
                to_coord,
                unitView.FootprintSize,
                unitView.UnitId,
                unitView
            )
        )
        {
            return MovePathResult(false, 0, System.Array.Empty<Vector2I>(), "目标区域不可放置当前单位。");
        }

        if (GetDistance(from_coord, to_coord) == 1)
        {
            int directCost = GetUnitMoveCost(state, unitView, to_coord);
            if (!CanUnitStepBetweenAnchors(state, unitView, from_coord, to_coord))
            {
                return MovePathResult(
                    false,
                    directCost,
                    System.Array.Empty<Vector2I>(),
                    "目标区域不可放置当前单位。"
                );
            }
            return MovePathResult(
                false,
                directCost,
                new[] { from_coord, to_coord },
                "移动力不足，无法移动。"
            );
        }

        int exceededCost = maxMovePoints < InfiniteCost ? maxMovePoints + 1 : InfiniteCost;
        return MovePathResult(false, exceededCost, System.Array.Empty<Vector2I>(), "移动力不足，无法移动。");
    }

    internal BattleMovePathTreeResult BuildUnitMovePathTreeTyped(
        BattleState state,
        BattleUnitState unit_state,
        Vector2I from_coord,
        int max_path_cost,
        Func<BattleUnitState, Vector2I, int> move_cost_provider
    )
    {
        return BuildUnitMovePathTreeCore(
            state,
            unit_state,
            from_coord,
            max_path_cost,
            move_cost_provider
        );
    }

    private BattleMovePathTreeResult BuildUnitMovePathTreeCore(
        BattleState state,
        BattleUnitState unit_state,
        Vector2I from_coord,
        int max_path_cost,
        Func<BattleUnitState, Vector2I, int> move_cost_provider
    )
    {
        if (state == null || unit_state == null || !IsInside(state, from_coord))
        {
            return new BattleMovePathTreeResult();
        }

        int sanitizedMaxPathCost = Math.Max(max_path_cost, 0);
        var bestCosts = new Dictionary<Vector2I, int> { [from_coord] = 0 };
        var previous = new Dictionary<Vector2I, Vector2I>();
        var steps = new Dictionary<Vector2I, int> { [from_coord] = 0 };
        var visited = new HashSet<Vector2I>();
        var heap = new List<MovePathNode>();
        HeapPush(heap, new MovePathNode(0, 0, from_coord));

        while (heap.Count > 0)
        {
            MovePathNode entry = HeapPop(heap);
            int currentCost = entry.Cost;
            Vector2I currentCoord = entry.Coord;
            if (!visited.Add(currentCoord))
            {
                continue;
            }

            foreach (Vector2I neighborCoord in GetNeighbors4(state, currentCoord))
            {
                if (visited.Contains(neighborCoord))
                {
                    continue;
                }
                if (!CanUnitStepBetweenAnchors(state, unit_state, currentCoord, neighborCoord))
                {
                    continue;
                }
                int stepCost = GetUnitMoveCost(state, unit_state, neighborCoord);
                if (move_cost_provider != null)
                {
                    stepCost = move_cost_provider.Invoke(unit_state, neighborCoord);
                }
                int nextCost = currentCost + stepCost;
                if (nextCost > sanitizedMaxPathCost)
                {
                    continue;
                }
                if (
                    bestCosts.TryGetValue(neighborCoord, out int existingCost)
                    && nextCost >= existingCost
                )
                {
                    continue;
                }
                bestCosts[neighborCoord] = nextCost;
                previous[neighborCoord] = currentCoord;
                steps[neighborCoord] = steps.TryGetValue(currentCoord, out int currentSteps)
                    ? currentSteps + 1
                    : 1;
                HeapPush(heap, new MovePathNode(nextCost, nextCost, neighborCoord));
            }
        }

        var result = new BattleMovePathTreeResult();
        foreach ((Vector2I coord, int cost) in bestCosts)
        {
            result.Costs[coord] = cost;
        }
        foreach ((Vector2I coord, Vector2I previousCoord) in previous)
        {
            result.Previous[coord] = previousCoord;
        }
        foreach ((Vector2I coord, int stepCount) in steps)
        {
            result.Steps[coord] = stepCount;
        }
        return result;
    }

    private MoveEvaluationResult EvaluateMoveTyped(
        BattleState state,
        Vector2I from_coord,
        Vector2I to_coord,
        BattleUnitState unit_state = null
    )
    {
        if (state == null)
        {
            return MoveEvaluationResult.Blocked("战斗状态不可用。");
        }
        if (!IsInside(state, to_coord))
        {
            return MoveEvaluationResult.Blocked("已到达战斗地图边界。");
        }
        if (GetDistance(from_coord, to_coord) != 1)
        {
            return MoveEvaluationResult.Blocked("普通移动只能前往相邻地格。");
        }
        if (IsWallBlocked(state, from_coord, to_coord))
        {
            return MoveEvaluationResult.Blocked("通道被墙壁阻挡。");
        }

        BattleUnitState moveUnit = unit_state ?? GetUnitAtCoord(state, from_coord);
        if (moveUnit == null)
        {
            return MoveEvaluationResult.Blocked("当前单位数据不可用。");
        }
        if (
            !CanPlaceFootprint(
                state,
                to_coord,
                moveUnit.footprint_size,
                moveUnit.unit_id,
                moveUnit
            )
        )
        {
            return MoveEvaluationResult.Blocked("目标区域不可放置当前单位。");
        }
        if (!CanPlaceUnit(state, moveUnit, to_coord))
        {
            return MoveEvaluationResult.Blocked("目标区域高度差超过 1，无法通行。");
        }
        int moveCost = GetUnitMoveCost(state, moveUnit, to_coord);
        return MoveEvaluationResult.Permitted(moveCost, "可移动。");
    }

    private readonly record struct MoveEvaluationResult(bool Allowed, int Cost, string Message)
    {
        public static MoveEvaluationResult Blocked(string message) => new(false, 0, message);

        public static MoveEvaluationResult Permitted(int cost, string message) =>
            new(true, cost, message);
    }

    internal void RecalculateCell(BattleCellState cell_state)
    {
        if (cell_state == null)
        {
            return;
        }
        cell_state.RecalculateRuntimeValues();
    }

    private void RebuildAllCellColumns(BattleState state)
    {
        if (state == null)
        {
            return;
        }
        state.RebuildCellColumns();
    }

    internal void SyncColumnFromSurfaceCell(BattleState state, Vector2I coord)
    {
        if (state == null)
        {
            return;
        }
        BattleCellState surfaceCell = GetCell(state, coord);
        if (surfaceCell == null)
        {
            state.RemoveCellColumnPayload(coord);
        }
        else
        {
            state.PutCellColumn(
                coord,
                BattleCellState.BuildStackedCellsFromSurfaceCell(surfaceCell)
            );
        }
    }

    private void EnsureCellColumns(BattleState state)
    {
        if (state == null)
        {
            return;
        }
        if (state.CellColumnCount == 0 && state.CellCount > 0)
        {
            RebuildAllCellColumns(state);
        }
    }

    internal bool SetBaseTerrain(BattleState state, Vector2I coord, StringName terrain)
    {
        BattleCellState cell = GetCell(state, coord);
        if (cell == null)
        {
            return false;
        }
        StringName normalizedTerrain = BattleTerrainRules.NormalizeTerrainId(terrain);
        StringName previousTerrain = cell.base_terrain;
        Vector2I previousFlowDirection = cell.flow_direction;
        cell.SetTerrain(normalizedTerrain);
        bool changed =
            previousTerrain != cell.base_terrain
            || previousFlowDirection != cell.flow_direction;
        SyncColumnFromSurfaceCell(state, coord);
        if (changed)
        {
            state?.MarkMovementGeometryChanged();
            MarkRuntimeEdgeFacesDirty(state);
        }
        return true;
    }

    internal bool SetHeightOffset(BattleState state, Vector2I coord, int height_offset)
    {
        BattleCellState cell = GetCell(state, coord);
        if (cell == null)
        {
            return false;
        }
        int clampedHeightOffset = Math.Clamp(
            height_offset,
            MinRuntimeHeight - cell.base_height,
            MaxRuntimeHeight - cell.base_height
        );
        bool changed = cell.height_offset != clampedHeightOffset;
        cell.SetHeightOffset(clampedHeightOffset);
        SyncColumnFromSurfaceCell(state, coord);
        if (changed)
        {
            state?.MarkMovementGeometryChanged();
            MarkRuntimeEdgeFacesDirty(state);
        }
        return true;
    }

    internal bool SetEdgeFeature(
        BattleState state,
        Vector2I coord,
        Vector2I direction,
        BattleEdgeFeatureState feature_state
    )
    {
        BattleCellState cell = GetCell(state, coord);
        if (cell == null)
        {
            return false;
        }
        cell.SetEdgeFeature(direction, feature_state);
        SyncColumnFromSurfaceCell(state, coord);
        state?.MarkMovementGeometryChanged();
        MarkRuntimeEdgeFacesDirty(state);
        return true;
    }

    internal bool ClearEdgeFeature(BattleState state, Vector2I coord, Vector2I direction)
    {
        return SetEdgeFeature(state, coord, direction, null);
    }

    internal BattleHeightDeltaResult ApplyHeightDeltaResult(
        BattleState state,
        Vector2I coord,
        int height_delta
    )
    {
        BattleCellState cell = GetCell(state, coord);
        if (cell == null)
        {
            return new BattleHeightDeltaResult(false, 0, 0, 0);
        }
        int beforeHeight = cell.current_height;
        bool changed = SetHeightOffset(state, coord, cell.height_offset + height_delta);
        int afterHeight = cell.current_height;
        return new BattleHeightDeltaResult(
            changed && beforeHeight != afterHeight,
            beforeHeight,
            afterHeight,
            afterHeight - beforeHeight
        );
    }

    internal bool ApplyHeightDelta(BattleState state, Vector2I coord, int height_delta)
    {
        return ApplyHeightDeltaResult(state, coord, height_delta).Changed;
    }

    internal void SetOccupant(
        BattleState state,
        Vector2I coord,
        StringName unit_id,
        bool mark_revision = true
    )
    {
        BattleCellState cell = GetCell(state, coord);
        if (cell != null)
        {
            if (cell.occupant_unit_id == unit_id)
            {
                return;
            }
            cell.occupant_unit_id = unit_id;
            if (mark_revision)
            {
                state?.MarkMovementGeometryChanged();
            }
        }
    }

    internal void SetOccupantsTyped(
        BattleState state,
        IEnumerable<Vector2I> coords,
        StringName unit_id,
        bool mark_revision = true
    )
    {
        if (coords == null)
        {
            return;
        }
        bool changed = false;
        foreach (Vector2I coord in coords)
        {
            BattleCellState cell = GetCell(state, coord);
            if (cell == null || cell.occupant_unit_id == unit_id)
            {
                continue;
            }
            cell.occupant_unit_id = unit_id;
            changed = true;
        }
        if (changed && mark_revision)
        {
            state?.MarkMovementGeometryChanged();
        }
    }

    internal void ClearUnitOccupancy(BattleState state, BattleUnitState unit_state)
    {
        if (state == null || unit_state == null)
        {
            return;
        }
        SetOccupantsTyped(state, unit_state.occupied_coords, "");
    }

    internal bool PlaceUnit(
        BattleState state,
        BattleUnitState unit_state,
        Vector2I target_coord,
        bool ignore_height = false
    )
    {
        if (state == null || unit_state == null)
        {
            return false;
        }
        if (!CanPlaceUnit(state, unit_state, target_coord, ignore_height))
        {
            return false;
        }
        SetOccupantsTyped(state, unit_state.occupied_coords, "", mark_revision: false);
        unit_state.SetAnchorCoord(target_coord);
        SetOccupantsTyped(state, unit_state.occupied_coords, unit_state.unit_id, mark_revision: false);
        state.MarkMovementGeometryChanged();
        return true;
    }

    internal bool MoveUnit(BattleState state, BattleUnitState unit_state, Vector2I target_coord)
    {
        // 静滞单位的移动/推拉/交换/跳跃等位移一律 fail closed；占位仍由 live footprint 持有。
        if (BattleTemporalStatusService.HasTimeStasis(unit_state))
            return false;
        return PlaceUnit(state, unit_state, target_coord);
    }

    internal bool MoveUnitForce(
        BattleState state,
        BattleUnitState unit_state,
        Vector2I target_coord
    )
    {
        return PlaceUnit(state, unit_state, target_coord, true);
    }

    internal string GetTerrainDisplayName(string terrain)
    {
        return BattleTerrainRules.GetDisplayName(new StringName(terrain ?? ""));
    }

    private bool CanUnitEnterCell(BattleCellState cell, BattleUnitState unit_state)
    {
        return cell != null
            && unit_state != null
            && BattleTerrainRules.CanUnitEnterTerrain(
                cell.base_terrain,
                unit_state.movement_tags
            );
    }

    private bool CanUnitEnterCell(BattleCellState cell, BattleUnitReadView unitView)
    {
        return cell != null
            && unitView.IsValid
            && BattleTerrainRules.CanUnitEnterTerrain(
                cell.base_terrain,
                unitView.GetMovementTags()
            );
    }

    private int MovePathHeuristicLegacy(Vector2I from_coord, Vector2I to_coord)
    {
        return MovePathHeuristic(from_coord, to_coord);
    }

    private void MoveHeapPush(GArray heap, GArray entry)
    {
        heap.Add(entry);
        int index = heap.Count - 1;
        while (index > 0)
        {
            int parentIndex = (index - 1) >> 1;
            if (GetHeapPriority(heap, parentIndex) <= GetHeapPriority(heap, index))
            {
                break;
            }
            var tmp = heap[parentIndex];
            heap[parentIndex] = heap[index];
            heap[index] = tmp;
            index = parentIndex;
        }
    }

    private GArray MoveHeapPop(GArray heap)
    {
        GArray top = heap[0].AsGodotArray();
        var last = heap[heap.Count - 1];
        heap.RemoveAt(heap.Count - 1);
        if (heap.Count == 0)
        {
            return top;
        }
        heap[0] = last;
        int index = 0;
        int size = heap.Count;
        while (true)
        {
            int left = (index << 1) + 1;
            int right = left + 1;
            int smallest = index;
            if (left < size && GetHeapPriority(heap, left) < GetHeapPriority(heap, smallest))
            {
                smallest = left;
            }
            if (right < size && GetHeapPriority(heap, right) < GetHeapPriority(heap, smallest))
            {
                smallest = right;
            }
            if (smallest == index)
            {
                break;
            }
            var tmp = heap[index];
            heap[index] = heap[smallest];
            heap[smallest] = tmp;
            index = smallest;
        }
        return top;
    }

    private static int GetHeapPriority(GArray heap, int index)
    {
        if (index < 0 || index >= heap.Count)
        {
            return int.MaxValue;
        }
        GArray entry = heap[index].AsGodotArray();
        return GetArrayInt(entry, 0, int.MaxValue);
    }

    internal int GetChebyshevDistance(Vector2I from_coord, Vector2I to_coord)
    {
        return Math.Max(Math.Abs(to_coord.X - from_coord.X), Math.Abs(to_coord.Y - from_coord.Y));
    }

    private GDictionary ComputeJumpParams(
        BattleUnitState unit_state,
        CombatEffectDefinition effectDefinition
    )
    {
        if (unit_state == null || effectDefinition == null)
        {
            return new GDictionary();
        }
        int jumpStr = GetJumpEffectiveStr(unit_state);
        return ComputeJumpParams(jumpStr, effectDefinition);
    }

    private GDictionary ComputeJumpParams(
        BattleUnitReadView unitView,
        CombatEffectDefinition effectDefinition
    )
    {
        if (!unitView.IsValid || effectDefinition == null)
        {
            return new GDictionary();
        }
        int jumpStr = GetJumpEffectiveStr(unitView);
        return ComputeJumpParams(jumpStr, effectDefinition);
    }

    private GDictionary ComputeJumpParams(int jumpStr, CombatEffectDefinition effectDefinition)
    {
        if (effectDefinition == null)
        {
            return new GDictionary();
        }
        double budget =
            effectDefinition.JumpBaseBudget
            + effectDefinition.JumpStrScale * jumpStr;
        double arcRatioRaw = effectDefinition.JumpArcRatio;
        double arcRatio = Math.Clamp(arcRatioRaw, MinJumpArcRatio, 1.0);
        int rangeMultiplier = Math.Max(effectDefinition.JumpRangeMultiplier, 1);
        int minArc = Math.Max(1, RoundToInt(budget * arcRatio));
        double rangeBudget = Math.Max(0.0, budget * (1.0 - arcRatio));
        int maxRange = Math.Max(1, RoundToInt(rangeBudget * rangeMultiplier));
        int forcedMoveDistance = effectDefinition.ForcedMoveDistance;
        if (forcedMoveDistance > 0)
        {
            maxRange = Math.Min(maxRange, forcedMoveDistance);
        }
        return new GDictionary
        {
            ["budget"] = budget,
            ["min_arc"] = minArc,
            ["range_budget"] = rangeBudget,
            ["max_range"] = maxRange,
            ["arc_ratio"] = arcRatio,
            ["range_multiplier"] = rangeMultiplier,
        };
    }

    private int ComputeJumpArcHeightForRange(GDictionary parameters, int actual_range)
    {
        if (parameters == null || parameters.Count == 0 || actual_range < 1)
        {
            return 0;
        }
        int rangeMultiplier = Math.Max(ReadInt(parameters, "range_multiplier", 1), 1);
        double distanceCost = (double)actual_range / rangeMultiplier;
        double rangeBudget = ReadDouble(parameters, "range_budget", 0.0);
        double savedBudget = Math.Max(0.0, rangeBudget - distanceCost);
        int extraArc = RoundToInt(savedBudget * JumpRedistributionFactor);
        return ReadInt(parameters, "min_arc", 0) + extraArc;
    }

    internal bool CanJumpArc(
        BattleState state,
        BattleUnitState unit_state,
        Vector2I target_coord,
        CombatEffectDefinition effectDefinition
    )
    {
        if (state == null || unit_state == null || effectDefinition == null)
        {
            return false;
        }
        if (target_coord == unit_state.coord || !IsInside(state, target_coord))
        {
            return false;
        }
        GDictionary parameters = ComputeJumpParams(unit_state, effectDefinition);
        if (parameters.Count == 0)
        {
            return false;
        }
        int maxRange = ReadInt(parameters, "max_range", 0);
        int actualRange = GetChebyshevDistance(unit_state.coord, target_coord);
        if (actualRange < 1 || actualRange > maxRange)
        {
            return false;
        }
        if (!CanPlaceUnit(state, unit_state, target_coord, true))
        {
            return false;
        }
        BattleCellState fromCell = GetCell(state, unit_state.coord);
        BattleCellState toCell = GetCell(state, target_coord);
        if (fromCell == null || toCell == null)
        {
            return false;
        }
        int arcHeight = ComputeJumpArcHeightForRange(parameters, actualRange);
        int h0 = fromCell.current_height;
        int h1 = toCell.current_height;
        double apex = Math.Max(h0, h1) + arcHeight;
        List<Vector2I> path = SupercoverJumpPath(unit_state.coord, target_coord);
        int pathN = path.Count - 1;
        if (pathN <= 1)
        {
            return true;
        }
        for (int i = 1; i < pathN; i++)
        {
            double t = (double)i / pathN;
            double chordH = Lerp(h0, h1, t);
            double arcHAtT = chordH + 4.0 * (apex - chordH) * t * (1.0 - t);
            BattleCellState cell = GetCell(state, path[i]);
            if (cell == null)
            {
                return false;
            }
            int blockerH = cell.current_height;
            if (!IsEmpty(cell.occupant_unit_id) && cell.occupant_unit_id != unit_state.unit_id)
            {
                BattleUnitState occupant = GetUnit(state, cell.occupant_unit_id);
                if (occupant != null)
                {
                    blockerH += GetUnitPresenceHeight(occupant);
                }
            }
            if (arcHAtT <= blockerH)
            {
                return false;
            }
        }
        return true;
    }

    internal bool CanJumpArc(
        BattleState state,
        BattleUnitReadView unitView,
        Vector2I target_coord,
        CombatEffectDefinition effectDefinition
    )
    {
        if (state == null || !unitView.IsValid || effectDefinition == null)
        {
            return false;
        }
        if (target_coord == unitView.Coord || !IsInside(state, target_coord))
        {
            return false;
        }
        GDictionary parameters = ComputeJumpParams(unitView, effectDefinition);
        if (parameters.Count == 0)
        {
            return false;
        }
        int maxRange = ReadInt(parameters, "max_range", 0);
        int actualRange = GetChebyshevDistance(unitView.Coord, target_coord);
        if (actualRange < 1 || actualRange > maxRange)
        {
            return false;
        }
        if (!CanPlaceUnit(state, unitView, target_coord, true))
        {
            return false;
        }
        BattleCellState fromCell = GetCell(state, unitView.Coord);
        BattleCellState toCell = GetCell(state, target_coord);
        if (fromCell == null || toCell == null)
        {
            return false;
        }
        int arcHeight = ComputeJumpArcHeightForRange(parameters, actualRange);
        int h0 = fromCell.current_height;
        int h1 = toCell.current_height;
        double apex = Math.Max(h0, h1) + arcHeight;
        List<Vector2I> path = SupercoverJumpPath(unitView.Coord, target_coord);
        int pathN = path.Count - 1;
        if (pathN <= 1)
        {
            return true;
        }
        for (int i = 1; i < pathN; i++)
        {
            double t = (double)i / pathN;
            double chordH = Lerp(h0, h1, t);
            double arcHAtT = chordH + 4.0 * (apex - chordH) * t * (1.0 - t);
            BattleCellState cell = GetCell(state, path[i]);
            if (cell == null)
            {
                return false;
            }
            int blockerH = cell.current_height;
            if (!IsEmpty(cell.occupant_unit_id) && cell.occupant_unit_id != unitView.UnitId)
            {
                BattleUnitState occupant = GetUnit(state, cell.occupant_unit_id);
                if (occupant != null)
                {
                    blockerH += GetUnitPresenceHeight(occupant);
                }
            }
            if (arcHAtT <= blockerH)
            {
                return false;
            }
        }
        return true;
    }

    internal bool CanBlinkToCoord(
        BattleState state,
        BattleUnitState unit_state,
        Vector2I target_coord,
        CombatEffectDefinition effectDefinition
    )
    {
        if (state == null || unit_state == null || effectDefinition == null)
        {
            return false;
        }
        if (target_coord == unit_state.coord || !IsInside(state, target_coord))
        {
            return false;
        }
        int maxRange = effectDefinition.ForcedMoveDistance;
        int actualRange = GetChebyshevDistance(unit_state.coord, target_coord);
        if (maxRange > 0 && actualRange > maxRange)
        {
            return false;
        }
        return actualRange >= 1 && CanPlaceUnit(state, unit_state, target_coord, true);
    }

    internal bool CanBlinkToCoord(
        BattleState state,
        BattleUnitReadView unitView,
        Vector2I target_coord,
        CombatEffectDefinition effectDefinition
    )
    {
        if (state == null || !unitView.IsValid || effectDefinition == null)
        {
            return false;
        }
        if (target_coord == unitView.Coord || !IsInside(state, target_coord))
        {
            return false;
        }
        int maxRange = effectDefinition.ForcedMoveDistance;
        int actualRange = GetChebyshevDistance(unitView.Coord, target_coord);
        if (maxRange > 0 && actualRange > maxRange)
        {
            return false;
        }
        return actualRange >= 1 && CanPlaceUnit(state, unitView, target_coord, true);
    }

    private List<Vector2I> SupercoverJumpPath(Vector2I from_coord, Vector2I to_coord)
    {
        var path = new List<Vector2I>();
        int dx = to_coord.X - from_coord.X;
        int dy = to_coord.Y - from_coord.Y;
        int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (steps <= 0)
        {
            path.Add(from_coord);
            return path;
        }
        var previous = new Vector2I(-99999, -99999);
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            int x = RoundToInt(from_coord.X + dx * t);
            int y = RoundToInt(from_coord.Y + dy * t);
            var current = new Vector2I(x, y);
            if (current != previous)
            {
                path.Add(current);
                previous = current;
            }
        }
        if (path.Count == 0 || path[^1] != to_coord)
        {
            path.Add(to_coord);
        }
        return path;
    }

    private int GetJumpEffectiveStr(BattleUnitState unit_state)
    {
        int rawStr = 0;
        if (unit_state?.attribute_snapshot != null)
        {
            rawStr = unit_state
                .attribute_snapshot.GetValue(JumpStrengthAttribute);
        }
        int modifier = GetJumpSizeStrModifier(unit_state);
        return Math.Max(0, rawStr + modifier);
    }

    private int GetJumpEffectiveStr(BattleUnitReadView unitView)
    {
        int rawStr = unitView.GetAttributeValue(JumpStrengthAttribute);
        int modifier = GetJumpSizeStrModifier(unitView);
        return Math.Max(0, rawStr + modifier);
    }

    private int GetJumpSizeStrModifier(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return 0;
        }
        int bodySize = unit_state.body_size;
        if (bodySize == BattleUnitState.BodySizeSmall)
        {
            return JumpSmallAgilityBonus;
        }
        if (bodySize == BattleUnitState.BodySizeMedium)
        {
            return 0;
        }
        if (bodySize == BattleUnitState.BodySizeLarge)
        {
            return -JumpSizeStrCost * 2;
        }
        if (bodySize == BattleUnitState.BodySizeHuge)
        {
            return -JumpSizeStrCost * 5;
        }
        if (
            bodySize == BattleUnitState.BodySizeGargantuan
            || bodySize == BattleUnitState.BodySizeBoss
        )
        {
            return -JumpSizeStrCost * 8;
        }
        return 0;
    }

    private int GetJumpSizeStrModifier(BattleUnitReadView unitView)
    {
        if (!unitView.IsValid)
        {
            return 0;
        }
        int bodySize = unitView.BodySize;
        if (bodySize == BattleUnitState.BodySizeSmall)
        {
            return JumpSmallAgilityBonus;
        }
        if (bodySize == BattleUnitState.BodySizeMedium)
        {
            return 0;
        }
        if (bodySize == BattleUnitState.BodySizeLarge)
        {
            return -JumpSizeStrCost * 2;
        }
        if (bodySize == BattleUnitState.BodySizeHuge)
        {
            return -JumpSizeStrCost * 5;
        }
        if (
            bodySize == BattleUnitState.BodySizeGargantuan
            || bodySize == BattleUnitState.BodySizeBoss
        )
        {
            return -JumpSizeStrCost * 8;
        }
        return 0;
    }

    private int GetUnitPresenceHeight(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return 1;
        }
        Vector2I footprint = unit_state.footprint_size;
        if (footprint == Vector2I.Zero)
        {
            footprint = BattleUnitState.GetFootprintSizeForBodySize(unit_state.body_size);
        }
        return Math.Min(Math.Max(footprint.X, 1), Math.Max(footprint.Y, 1));
    }

    private static readonly Vector2I[] RightDownDirections = { Vector2I.Right, Vector2I.Down };

    private static Vector2I GetMapSize(BattleState state)
    {
        return state?.map_size ?? Vector2I.Zero;
    }

    private static BattleCellState GetCell(BattleState state, Vector2I coord)
    {
        return state?.GetCell(coord);
    }

    private static BattleUnitState GetUnit(BattleState state, StringName unitId)
    {
        return state?.GetUnit(unitId);
    }

    private void AddNeighborIfInside(BattleState state, List<Vector2I> neighbors, Vector2I coord)
    {
        if (IsInside(state, coord))
        {
            neighbors.Add(coord);
        }
    }

    private void AddConeCoords(
        BattleState state,
        List<Vector2I> coords,
        Vector2I centerCoord,
        int radius,
        Vector2I direction,
        bool wide
    )
    {
        if (direction == Vector2I.Right)
        {
            for (int step = wide ? 1 : 0; step <= radius; step++)
            {
                int halfWidth = wide ? step : (step <= Math.Min(radius, 1) ? 1 : 0);
                AddAxisConeStep(
                    state,
                    coords,
                    centerCoord.X + step,
                    centerCoord.Y,
                    halfWidth,
                    horizontal: true
                );
            }
        }
        else if (direction == Vector2I.Left)
        {
            for (int step = wide ? 1 : 0; step <= radius; step++)
            {
                int halfWidth = wide ? step : (step <= Math.Min(radius, 1) ? 1 : 0);
                AddAxisConeStep(
                    state,
                    coords,
                    centerCoord.X - step,
                    centerCoord.Y,
                    halfWidth,
                    horizontal: true
                );
            }
        }
        else if (direction == Vector2I.Down)
        {
            for (int step = wide ? 1 : 0; step <= radius; step++)
            {
                int halfWidth = wide ? step : (step <= Math.Min(radius, 1) ? 1 : 0);
                AddAxisConeStep(
                    state,
                    coords,
                    centerCoord.X,
                    centerCoord.Y + step,
                    halfWidth,
                    horizontal: false
                );
            }
        }
        else
        {
            for (int step = wide ? 1 : 0; step <= radius; step++)
            {
                int halfWidth = wide ? step : (step <= Math.Min(radius, 1) ? 1 : 0);
                AddAxisConeStep(
                    state,
                    coords,
                    centerCoord.X,
                    centerCoord.Y - step,
                    halfWidth,
                    horizontal: false
                );
            }
        }
    }

    private void AddAxisConeStep(
        BattleState state,
        List<Vector2I> coords,
        int baseX,
        int baseY,
        int halfWidth,
        bool horizontal
    )
    {
        for (int offset = -halfWidth; offset <= halfWidth; offset++)
        {
            var coord = horizontal
                ? new Vector2I(baseX, baseY + offset)
                : new Vector2I(baseX + offset, baseY);
            if (IsInside(state, coord))
            {
                coords.Add(coord);
            }
        }
    }

    private bool EdgeBlocksOccupancyBetween(BattleState state, Vector2I fromCoord, Vector2I toCoord)
    {
        return _edgeService != null
            && _edgeService.BlocksOccupancyBetween(state, fromCoord, toCoord);
    }

    private bool EdgeIsTraversableBetween(BattleState state, Vector2I fromCoord, Vector2I toCoord)
    {
        return _edgeService != null
            && _edgeService.IsTraversableBetween(state, fromCoord, toCoord);
    }

    private void MarkRuntimeEdgeFacesDirty(BattleState state)
    {
        _edgeService?.MarkRuntimeEdgeFacesDirty(state);
    }

    private static int MovePathHeuristic(Vector2I fromCoord, Vector2I toCoord)
    {
        return Math.Abs(toCoord.X - fromCoord.X) + Math.Abs(toCoord.Y - fromCoord.Y);
    }

    private static void HeapPush(List<MovePathNode> heap, MovePathNode entry)
    {
        heap.Add(entry);
        int index = heap.Count - 1;
        while (index > 0)
        {
            int parentIndex = (index - 1) >> 1;
            if (heap[parentIndex].Priority <= heap[index].Priority)
            {
                break;
            }
            (heap[parentIndex], heap[index]) = (heap[index], heap[parentIndex]);
            index = parentIndex;
        }
    }

    private static MovePathNode HeapPop(List<MovePathNode> heap)
    {
        MovePathNode top = heap[0];
        MovePathNode last = heap[^1];
        heap.RemoveAt(heap.Count - 1);
        if (heap.Count == 0)
        {
            return top;
        }
        heap[0] = last;
        int index = 0;
        while (true)
        {
            int left = (index << 1) + 1;
            int right = left + 1;
            int smallest = index;
            if (left < heap.Count && heap[left].Priority < heap[smallest].Priority)
            {
                smallest = left;
            }
            if (right < heap.Count && heap[right].Priority < heap[smallest].Priority)
            {
                smallest = right;
            }
            if (smallest == index)
            {
                break;
            }
            (heap[index], heap[smallest]) = (heap[smallest], heap[index]);
            index = smallest;
        }
        return top;
    }

    private static List<Vector2I> ReconstructMovePath(
        Dictionary<Vector2I, Vector2I> previous,
        Vector2I start,
        Vector2I end
    )
    {
        var reversedPath = new List<Vector2I>();
        Vector2I current = end;
        while (current != start)
        {
            reversedPath.Add(current);
            if (!previous.TryGetValue(current, out Vector2I previousCoord))
            {
                return new List<Vector2I>();
            }
            current = previousCoord;
        }
        reversedPath.Add(start);
        reversedPath.Reverse();
        return reversedPath;
    }

    private static BattleMovePathResult MovePathResult(
        bool allowed,
        int cost,
        IEnumerable<Vector2I> path,
        string message
    )
    {
        return new BattleMovePathResult
        {
            Allowed = allowed,
            Cost = cost,
            Path = path != null ? new List<Vector2I>(path) : System.Array.Empty<Vector2I>(),
            Message = message,
        };
    }

    private static GDictionary ToVariantDictionary(Dictionary<Vector2I, int> source)
    {
        var result = new GDictionary();
        foreach ((Vector2I key, int value) in source)
        {
            result[key] = value;
        }
        return result;
    }

    private static GDictionary ToVariantDictionary(Dictionary<Vector2I, Vector2I> source)
    {
        var result = new GDictionary();
        foreach ((Vector2I key, Vector2I value) in source)
        {
            result[key] = value;
        }
        return result;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static string ReadString(GDictionary data, string key, string fallback = "")
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = data[key];
        if (value.VariantType == Variant.Type.String)
        {
            return value.AsString();
        }
        if (value.VariantType == Variant.Type.StringName)
        {
            return value.AsStringName().ToString();
        }
        return fallback;
    }

    private static int ReadInt(GDictionary data, string key, int fallback = 0)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static double ReadDouble(GDictionary data, string key, double fallback = 0.0)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.Float => value.AsDouble(),
            Variant.Type.Int => value.AsInt32(),
            _ => fallback,
        };
    }

    private static int GetArrayInt(GArray values, int index, int fallback = 0)
    {
        if (values == null || index < 0 || index >= values.Count)
        {
            return fallback;
        }
        var value = values[index];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static int RoundToInt(double value)
    {
        return (int)Math.Floor(value + 0.5);
    }

    private static double Lerp(double from, double to, double weight)
    {
        return from + (to - from) * weight;
    }
}
