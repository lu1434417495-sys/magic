using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public readonly record struct BattleHeightDeltaResult(
    bool Changed,
    int BeforeHeight,
    int AfterHeight,
    int AppliedDelta
)
{
    public GDictionary ToDictionary() =>
        new()
        {
            ["changed"] = Changed,
            ["before_height"] = BeforeHeight,
            ["after_height"] = AfterHeight,
            ["applied_delta"] = AppliedDelta,
        };
}

[GlobalClass]
public partial class BattleGridService : RefCounted
{
    private static readonly StringName TerrainFlowingWater = "flowing_water";
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

    public BattleCellState get_cell(GodotObject state, Vector2I coord)
    {
        return GetCell(state, coord);
    }

    public bool has_cell(GodotObject state, Vector2I coord)
    {
        return GetCell(state, coord) != null;
    }

    public StringName get_cell_base_terrain_id(GodotObject state, Vector2I coord)
    {
        return GetCell(state, coord)?.base_terrain ?? "";
    }

    public Godot.Collections.Array<BattleCellState> get_column_cells(
        GodotObject state,
        Vector2I coord
    )
    {
        var results = new Godot.Collections.Array<BattleCellState>();
        if (state == null)
        {
            return results;
        }
        _ensure_cell_columns(state);
        var columnValue = GetDictionary(state, "cell_columns").ContainsKey(coord)
            ? GetDictionary(state, "cell_columns")[coord]
            : default;
        if (columnValue.VariantType == Variant.Type.Array)
        {
            foreach (var cellValue in columnValue.AsGodotArray())
            {
                var layerCell = cellValue.AsGodotObject() as BattleCellState;
                if (layerCell != null)
                {
                    results.Add(layerCell);
                }
            }
        }
        return results;
    }

    public BattleUnitState get_unit_at_coord(GodotObject state, Vector2I coord)
    {
        BattleCellState cell = GetCell(state, coord);
        if (cell == null || IsEmpty(cell.occupant_unit_id))
        {
            return null;
        }
        return GetUnit(state, cell.occupant_unit_id);
    }

    public bool is_inside(GodotObject state, Vector2I coord)
    {
        if (state == null)
        {
            return false;
        }
        Vector2I mapSize = GetMapSize(state);
        return coord.X >= 0 && coord.Y >= 0 && coord.X < mapSize.X && coord.Y < mapSize.Y;
    }

    public GVector2IArray get_neighbors_4(GodotObject state, Vector2I coord)
    {
        var neighbors = new GVector2IArray();
        AddNeighborIfInside(state, neighbors, coord + Vector2I.Left);
        AddNeighborIfInside(state, neighbors, coord + Vector2I.Right);
        AddNeighborIfInside(state, neighbors, coord + Vector2I.Up);
        AddNeighborIfInside(state, neighbors, coord + Vector2I.Down);
        return neighbors;
    }

    public GVector2IArray get_footprint_coords(Vector2I anchor_coord, Vector2I footprint_size)
    {
        var coords = new GVector2IArray();
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

    public GVector2IArray get_unit_target_coords(BattleUnitState unit_state, Vector2I anchor_coord)
    {
        if (unit_state == null)
        {
            return new GVector2IArray();
        }
        Vector2I footprintSize = unit_state.footprint_size;
        if (footprintSize == Vector2I.Zero)
        {
            footprintSize = BattleUnitState.get_footprint_size_for_body_size(unit_state.body_size);
        }
        return get_footprint_coords(anchor_coord, footprintSize);
    }

    public int get_height_difference(GodotObject state, Vector2I from_coord, Vector2I to_coord)
    {
        BattleCellState fromCell = GetCell(state, from_coord);
        BattleCellState toCell = GetCell(state, to_coord);
        return fromCell == null || toCell == null
            ? 999
            : Math.Abs(fromCell.current_height - toCell.current_height);
    }

    public bool is_height_passable(GodotObject state, Vector2I from_coord, Vector2I to_coord)
    {
        return get_height_difference(state, from_coord, to_coord) <= 1;
    }

    public int get_movement_cost(GodotObject state, Vector2I coord)
    {
        BattleCellState cell = GetCell(state, coord);
        return cell == null ? 1 : Math.Max(cell.move_cost, 1);
    }

    public int get_distance(Vector2I from_coord, Vector2I to_coord)
    {
        return Math.Abs(from_coord.X - to_coord.X) + Math.Abs(from_coord.Y - to_coord.Y);
    }

    public GVector2IArray get_area_coords(
        GodotObject state,
        Vector2I center_coord,
        StringName area_pattern,
        int area_value
    )
    {
        return get_area_coords(state, center_coord, area_pattern, area_value, Vector2I.Zero);
    }

    public GVector2IArray get_area_coords(
        GodotObject state,
        Vector2I center_coord,
        StringName area_pattern,
        int area_value,
        Vector2I facing_direction
    )
    {
        var coords = new GVector2IArray();
        if (state == null || !is_inside(state, center_coord))
        {
            return coords;
        }

        int radius = Math.Max(area_value, 0);
        if (
            IsEmpty(area_pattern)
            || area_pattern == "single"
            || area_pattern == "self"
            || radius <= 0
        )
        {
            coords.Add(center_coord);
            return coords;
        }

        string pattern = area_pattern.ToString();
        if (pattern == "diamond")
        {
            for (int y = center_coord.Y - radius; y <= center_coord.Y + radius; y++)
            {
                for (int x = center_coord.X - radius; x <= center_coord.X + radius; x++)
                {
                    var coord = new Vector2I(x, y);
                    if (
                        is_inside(state, coord)
                        && Math.Abs(coord.X - center_coord.X) + Math.Abs(coord.Y - center_coord.Y)
                            <= radius
                    )
                    {
                        coords.Add(coord);
                    }
                }
            }
        }
        else if (pattern == "square" || pattern == "radius")
        {
            for (int y = center_coord.Y - radius; y <= center_coord.Y + radius; y++)
            {
                for (int x = center_coord.X - radius; x <= center_coord.X + radius; x++)
                {
                    var coord = new Vector2I(x, y);
                    if (
                        is_inside(state, coord)
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
        else if (pattern == "cross")
        {
            for (int y = center_coord.Y - radius; y <= center_coord.Y + radius; y++)
            {
                for (int x = center_coord.X - radius; x <= center_coord.X + radius; x++)
                {
                    var coord = new Vector2I(x, y);
                    int dx = Math.Abs(coord.X - center_coord.X);
                    int dy = Math.Abs(coord.Y - center_coord.Y);
                    if (
                        is_inside(state, coord)
                        && ((dx == 0 && dy <= radius) || (dy == 0 && dx <= radius))
                    )
                    {
                        coords.Add(coord);
                    }
                }
            }
        }
        else if (pattern == "line")
        {
            return _build_line_coords(state, center_coord, radius, facing_direction);
        }
        else if (pattern == "cone")
        {
            return _build_cone_coords(state, center_coord, radius, facing_direction);
        }
        else if (pattern == "narrow_cone")
        {
            return _build_narrow_cone_coords(state, center_coord, radius, facing_direction);
        }
        else if (pattern == "front_arc")
        {
            return _build_front_arc_coords(state, center_coord, radius, facing_direction);
        }
        else
        {
            coords.Add(center_coord);
        }
        return _sort_unique_coords(coords);
    }

    public GVector2IArray _build_line_coords(
        GodotObject state,
        Vector2I center_coord,
        int radius,
        Vector2I facing_direction
    )
    {
        var coords = new GVector2IArray();
        if (radius <= 0)
        {
            coords.Add(center_coord);
            return coords;
        }
        if (_get_directional_line_axis(state, center_coord, facing_direction) == 0)
        {
            for (int x = center_coord.X - radius; x <= center_coord.X + radius; x++)
            {
                var coord = new Vector2I(x, center_coord.Y);
                if (is_inside(state, coord))
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
                if (is_inside(state, coord))
                {
                    coords.Add(coord);
                }
            }
        }
        return _sort_unique_coords(coords);
    }

    public GVector2IArray _build_cone_coords(
        GodotObject state,
        Vector2I center_coord,
        int radius,
        Vector2I facing_direction
    )
    {
        var coords = new GVector2IArray { center_coord };
        if (radius <= 0)
        {
            return coords;
        }
        AddConeCoords(
            state,
            coords,
            center_coord,
            radius,
            _resolve_area_direction(state, center_coord, facing_direction),
            wide: true
        );
        return _sort_unique_coords(coords);
    }

    public GVector2IArray _build_narrow_cone_coords(
        GodotObject state,
        Vector2I center_coord,
        int radius,
        Vector2I facing_direction
    )
    {
        var coords = new GVector2IArray();
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
            _resolve_area_direction(state, center_coord, facing_direction),
            wide: false
        );
        return _sort_unique_coords(coords);
    }

    public GVector2IArray _build_front_arc_coords(
        GodotObject state,
        Vector2I center_coord,
        int radius,
        Vector2I facing_direction
    )
    {
        var coords = new GVector2IArray();
        int arcRadius = Math.Max(radius, 0);
        Vector2I direction = _resolve_area_direction(state, center_coord, facing_direction);
        if (direction == Vector2I.Zero)
        {
            direction = Vector2I.Right;
        }
        if (direction.X != 0)
        {
            for (int offset = -arcRadius; offset <= arcRadius; offset++)
            {
                var coord = new Vector2I(center_coord.X, center_coord.Y + offset);
                if (is_inside(state, coord))
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
                if (is_inside(state, coord))
                {
                    coords.Add(coord);
                }
            }
        }
        return _sort_unique_coords(coords);
    }

    public int _get_directional_line_axis(
        GodotObject state,
        Vector2I center_coord,
        Vector2I facing_direction
    )
    {
        Vector2I normalizedDirection = _normalize_area_direction(facing_direction);
        if (normalizedDirection != Vector2I.Zero)
        {
            return normalizedDirection.X != 0 ? 0 : 1;
        }
        return _get_stable_line_axis(state, center_coord);
    }

    public int _get_stable_line_axis(GodotObject state, Vector2I center_coord)
    {
        Vector2I mapSize = GetMapSize(state);
        int horizontalSpan = Math.Min(center_coord.X, mapSize.X - 1 - center_coord.X);
        int verticalSpan = Math.Min(center_coord.Y, mapSize.Y - 1 - center_coord.Y);
        return horizontalSpan >= verticalSpan ? 0 : 1;
    }

    public Vector2I _resolve_area_direction(
        GodotObject state,
        Vector2I center_coord,
        Vector2I facing_direction
    )
    {
        Vector2I normalizedDirection = _normalize_area_direction(facing_direction);
        return normalizedDirection != Vector2I.Zero
            ? normalizedDirection
            : _get_stable_cone_direction(state, center_coord);
    }

    public Vector2I _get_stable_cone_direction(GodotObject state, Vector2I center_coord)
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

    public Vector2I _normalize_area_direction(Vector2I direction)
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

    public GVector2IArray _sort_unique_coords(GVector2IArray coords)
    {
        if (coords == null || coords.Count == 0)
        {
            return coords ?? new GVector2IArray();
        }
        var uniqueCoords = new GVector2IArray();
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

    public int get_distance_from_unit_to_coord(BattleUnitState unit_state, Vector2I target_coord)
    {
        if (unit_state == null)
        {
            return 999999;
        }
        unit_state.refresh_footprint();
        int bestDistance = 999999;
        foreach (Vector2I occupiedCoord in unit_state.occupied_coords)
        {
            bestDistance = Math.Min(bestDistance, get_distance(occupiedCoord, target_coord));
        }
        return bestDistance;
    }

    public int get_distance_between_units(BattleUnitState first_unit, BattleUnitState second_unit)
    {
        if (first_unit == null || second_unit == null)
        {
            return 999999;
        }
        first_unit.refresh_footprint();
        second_unit.refresh_footprint();
        int bestDistance = 999999;
        foreach (Vector2I firstCoord in first_unit.occupied_coords)
        {
            foreach (Vector2I secondCoord in second_unit.occupied_coords)
            {
                bestDistance = Math.Min(bestDistance, get_distance(firstCoord, secondCoord));
            }
        }
        return bestDistance;
    }

    public bool is_walkable(GodotObject state, Vector2I coord)
    {
        return can_place_footprint(state, coord, Vector2I.One, "", null);
    }

    public bool can_enter_cell(GodotObject state, Vector2I coord)
    {
        return can_place_footprint(state, coord, Vector2I.One, "", null);
    }

    public bool can_unit_enter_coord(GodotObject state, Vector2I coord, BattleUnitState unit_state)
    {
        return can_place_footprint(state, coord, Vector2I.One, "", unit_state);
    }

    public bool can_place_footprint(
        GodotObject state,
        Vector2I anchor_coord,
        Vector2I footprint_size,
        StringName ignored_unit_id,
        BattleUnitState unit_state
    )
    {
        var footprintCoords = get_footprint_coords(anchor_coord, footprint_size);
        var footprintLookup = new HashSet<Vector2I>();
        foreach (Vector2I footprintCoord in footprintCoords)
        {
            footprintLookup.Add(footprintCoord);
            if (!is_inside(state, footprintCoord))
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
                if (!_can_unit_enter_cell(cell, unit_state))
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

    public GStringNameArray collect_blocking_unit_ids(
        GodotObject state,
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
        foreach (Vector2I footprintCoord in get_unit_target_coords(unit_state, target_coord))
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

    public bool can_place_unit(
        GodotObject state,
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
            !can_place_footprint(
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

        unit_state.refresh_footprint();
        Vector2I delta = target_coord - unit_state.coord;
        if (delta == Vector2I.Zero)
        {
            return true;
        }
        if (get_distance(Vector2I.Zero, delta) == 1)
        {
            return _can_unit_step_across_edges(state, unit_state, delta);
        }
        var currentCoords = new HashSet<Vector2I>();
        foreach (Vector2I occupiedCoord in unit_state.occupied_coords)
        {
            currentCoords.Add(occupiedCoord);
        }

        foreach (Vector2I footprintCoord in get_unit_target_coords(unit_state, target_coord))
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

    public BattleEdgeFaceState get_edge_face(
        GodotObject state,
        Vector2I from_coord,
        Vector2I to_coord
    )
    {
        return _edgeService?.get_edge_face(state, from_coord, to_coord);
    }

    public bool _can_unit_step_across_edges(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I delta
    )
    {
        if (state == null || unit_state == null)
        {
            return false;
        }
        unit_state.refresh_footprint();
        return _can_anchor_step_across_edges(
            state,
            unit_state.footprint_size,
            unit_state.coord,
            delta
        );
    }

    public bool _can_anchor_step_across_edges(
        GodotObject state,
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

    public bool is_wall_blocked(GodotObject state, Vector2I from_coord, Vector2I to_coord)
    {
        return _edgeService != null
            && _edgeService.has_feature_between(
                state,
                from_coord,
                to_coord,
                BattleEdgeFaceState.FEATURE_WALL()
            );
    }

    public bool can_traverse(
        GodotObject state,
        Vector2I from_coord,
        Vector2I to_coord,
        BattleUnitState unit_state = null
    )
    {
        if (get_distance(from_coord, to_coord) != 1)
        {
            return false;
        }
        if (is_wall_blocked(state, from_coord, to_coord))
        {
            return false;
        }
        if (unit_state != null)
        {
            return can_place_unit(state, unit_state, to_coord);
        }
        if (!is_inside(state, to_coord) || !can_enter_cell(state, to_coord))
        {
            return false;
        }
        return EdgeIsTraversableBetween(state, from_coord, to_coord);
    }

    public bool can_unit_step_between_anchors(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I from_anchor,
        Vector2I to_anchor
    )
    {
        if (state == null || unit_state == null)
        {
            return false;
        }
        unit_state.refresh_footprint();
        Vector2I delta = to_anchor - from_anchor;
        if (get_distance(from_anchor, to_anchor) != 1)
        {
            return false;
        }
        if (
            !can_place_footprint(
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
        if (!_can_anchor_step_across_edges(state, unit_state.footprint_size, from_anchor, delta))
        {
            return false;
        }

        foreach (Vector2I footprintCoord in get_unit_target_coords(unit_state, to_anchor))
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

    public int get_unit_move_cost(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I target_coord
    )
    {
        if (state == null || unit_state == null)
        {
            return 1;
        }
        GArray movementTags = _get_unit_movement_tags(unit_state);
        int moveCost = 1;
        foreach (Vector2I occupiedCoord in get_unit_target_coords(unit_state, target_coord))
        {
            BattleCellState cell = GetCell(state, occupiedCoord);
            if (cell == null)
            {
                continue;
            }
            moveCost = Math.Max(
                moveCost,
                BattleTerrainRules.get_unit_move_cost(cell.base_terrain, movementTags)
            );
        }
        return moveCost;
    }

    public GDictionary resolve_unit_move_path(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I from_coord,
        Vector2I to_coord,
        int max_move_points
    )
    {
        return ResolveUnitMovePath(
            state,
            unit_state,
            from_coord,
            to_coord,
            max_move_points,
            null
        );
    }

    public GDictionary resolve_unit_move_path(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I from_coord,
        Vector2I to_coord,
        int max_move_points,
        Func<BattleUnitState, Vector2I, int> move_cost_provider
    )
    {
        return ResolveUnitMovePath(
            state,
            unit_state,
            from_coord,
            to_coord,
            max_move_points,
            move_cost_provider
        );
    }

    public BattleMovePathResult resolve_unit_move_path_typed(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I from_coord,
        Vector2I to_coord,
        int max_move_points,
        Func<BattleUnitState, Vector2I, int> move_cost_provider
    )
    {
        return ResolveUnitMovePathTyped(
            state,
            unit_state,
            from_coord,
            to_coord,
            max_move_points,
            move_cost_provider
        );
    }

    private GDictionary ResolveUnitMovePath(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I from_coord,
        Vector2I to_coord,
        int max_move_points,
        Func<BattleUnitState, Vector2I, int> move_cost_provider
    )
    {
        return ResolveUnitMovePathTyped(
            state,
            unit_state,
            from_coord,
            to_coord,
            max_move_points,
            move_cost_provider
        ).ToDictionary();
    }

    private BattleMovePathResult ResolveUnitMovePathTyped(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I from_coord,
        Vector2I to_coord,
        int max_move_points,
        Func<BattleUnitState, Vector2I, int> move_cost_provider
    )
    {
        if (state == null)
        {
            return MovePathResult(false, 0, new GVector2IArray(), "战斗状态不可用。");
        }
        if (unit_state == null)
        {
            return MovePathResult(false, 0, new GVector2IArray(), "当前单位数据不可用。");
        }
        if (!is_inside(state, from_coord))
        {
            return MovePathResult(false, 0, new GVector2IArray(), "当前单位不在有效战斗格上。");
        }
        if (!is_inside(state, to_coord))
        {
            return MovePathResult(false, 0, new GVector2IArray(), "已到达战斗地图边界。");
        }
        if (from_coord == to_coord)
        {
            return MovePathResult(true, 0, new GVector2IArray { from_coord }, "可移动。");
        }

        int sanitizedMaxMovePoints = Math.Max(max_move_points, 0);
        var bestCosts = new Dictionary<Vector2I, int> { [from_coord] = 0 };
        var previous = new Dictionary<Vector2I, Vector2I>();
        var visited = new HashSet<Vector2I>();
        var heap = new List<MovePathNode>();
        HeapPush(heap, new MovePathNode(MovePathHeuristic(from_coord, to_coord), 0, from_coord));
        bool foundTarget = false;

        while (heap.Count > 0)
        {
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
            foreach (Vector2I neighborCoord in get_neighbors_4(state, currentCoord))
            {
                if (visited.Contains(neighborCoord))
                {
                    continue;
                }
                if (!can_unit_step_between_anchors(state, unit_state, currentCoord, neighborCoord))
                {
                    continue;
                }
                int stepCost = get_unit_move_cost(state, unit_state, neighborCoord);
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
                bestCosts[neighborCoord] = nextCost;
                previous[neighborCoord] = currentCoord;
                int h = MovePathHeuristic(neighborCoord, to_coord);
                HeapPush(heap, new MovePathNode(nextCost + h, nextCost, neighborCoord));
            }
        }

        if (!foundTarget)
        {
            if (
                !can_place_footprint(
                    state,
                    to_coord,
                    unit_state.footprint_size,
                    unit_state.unit_id,
                    unit_state
                )
            )
            {
                return MovePathResult(false, 0, new GVector2IArray(), "目标区域不可放置当前单位。");
            }
            if (get_distance(from_coord, to_coord) == 1)
            {
                GDictionary directResult = evaluate_move(state, from_coord, to_coord, unit_state);
                return MovePathResult(
                    false,
                    ReadInt(directResult, "cost", 0),
                    new GVector2IArray(),
                    ReadString(directResult, "message", "该移动不可执行。")
                );
            }
            return MovePathResult(false, 0, new GVector2IArray(), "目标地格当前不可到达。");
        }

        int finalCost = bestCosts.TryGetValue(to_coord, out int resolvedCost)
            ? resolvedCost
            : InfiniteCost;
        GVector2IArray anchorPath = ReconstructMovePath(previous, from_coord, to_coord);
        if (finalCost > sanitizedMaxMovePoints)
        {
            return MovePathResult(false, finalCost, anchorPath, "移动力不足，无法移动。");
        }
        return MovePathResult(true, finalCost, anchorPath, "可移动。");
    }

    public GDictionary build_unit_move_path_tree(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I from_coord,
        int max_path_cost
    )
    {
        return BuildUnitMovePathTree(state, unit_state, from_coord, max_path_cost, null)
            .ToDictionary();
    }

    public GDictionary build_unit_move_path_tree(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I from_coord,
        int max_path_cost,
        Func<BattleUnitState, Vector2I, int> move_cost_provider
    )
    {
        return BuildUnitMovePathTree(
            state,
            unit_state,
            from_coord,
            max_path_cost,
            move_cost_provider
        ).ToDictionary();
    }

    public BattleMovePathTreeResult build_unit_move_path_tree_typed(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I from_coord,
        int max_path_cost,
        Func<BattleUnitState, Vector2I, int> move_cost_provider
    )
    {
        return BuildUnitMovePathTree(
            state,
            unit_state,
            from_coord,
            max_path_cost,
            move_cost_provider
        );
    }

    private BattleMovePathTreeResult BuildUnitMovePathTree(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I from_coord,
        int max_path_cost,
        Func<BattleUnitState, Vector2I, int> move_cost_provider
    )
    {
        if (state == null || unit_state == null || !is_inside(state, from_coord))
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

            foreach (Vector2I neighborCoord in get_neighbors_4(state, currentCoord))
            {
                if (visited.Contains(neighborCoord))
                {
                    continue;
                }
                if (!can_unit_step_between_anchors(state, unit_state, currentCoord, neighborCoord))
                {
                    continue;
                }
                int stepCost = get_unit_move_cost(state, unit_state, neighborCoord);
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

    public GDictionary evaluate_move(
        GodotObject state,
        Vector2I from_coord,
        Vector2I to_coord,
        BattleUnitState unit_state = null
    )
    {
        if (state == null)
        {
            return new GDictionary { ["allowed"] = false, ["message"] = "战斗状态不可用。" };
        }
        if (!is_inside(state, to_coord))
        {
            return new GDictionary { ["allowed"] = false, ["message"] = "已到达战斗地图边界。" };
        }
        if (get_distance(from_coord, to_coord) != 1)
        {
            return new GDictionary
            {
                ["allowed"] = false,
                ["message"] = "普通移动只能前往相邻地格。",
            };
        }
        if (is_wall_blocked(state, from_coord, to_coord))
        {
            return new GDictionary { ["allowed"] = false, ["message"] = "通道被墙壁阻挡。" };
        }

        BattleUnitState moveUnit = unit_state ?? get_unit_at_coord(state, from_coord);
        if (moveUnit == null)
        {
            return new GDictionary { ["allowed"] = false, ["message"] = "当前单位数据不可用。" };
        }
        if (
            !can_place_footprint(
                state,
                to_coord,
                moveUnit.footprint_size,
                moveUnit.unit_id,
                moveUnit
            )
        )
        {
            return new GDictionary
            {
                ["allowed"] = false,
                ["message"] = "目标区域不可放置当前单位。",
            };
        }
        if (!can_place_unit(state, moveUnit, to_coord))
        {
            return new GDictionary
            {
                ["allowed"] = false,
                ["message"] = "目标区域高度差超过 1，无法通行。",
            };
        }
        int moveCost = get_unit_move_cost(state, moveUnit, to_coord);
        return new GDictionary
        {
            ["allowed"] = true,
            ["cost"] = moveCost,
            ["message"] = "可移动。",
        };
    }

    public void recalculate_cell(BattleCellState cell_state)
    {
        if (cell_state == null)
        {
            return;
        }
        cell_state.base_terrain = BattleTerrainRules.normalize_terrain_id(cell_state.base_terrain);
        if (cell_state.base_terrain != TerrainFlowingWater)
        {
            cell_state.flow_direction = Vector2I.Zero;
        }
        cell_state.current_height = Math.Clamp(
            cell_state.base_height + cell_state.height_offset,
            MinRuntimeHeight,
            MaxRuntimeHeight
        );
        cell_state.stack_layer = cell_state.current_height;
        cell_state.passable = BattleTerrainRules.get_global_passable(cell_state.base_terrain);
        cell_state.move_cost = BattleTerrainRules.get_base_move_cost(cell_state.base_terrain);
    }

    public void recalculate_cells(GDictionary cells)
    {
        if (cells == null)
        {
            return;
        }
        foreach (var cellValue in cells.Values)
        {
            if (cellValue.AsGodotObject() is BattleCellState cellState)
            {
                recalculate_cell(cellState);
            }
        }
    }

    public void rebuild_all_cell_columns(GodotObject state)
    {
        if (state == null)
        {
            return;
        }
        GDictionary rebuiltColumns = BattleCellState.build_columns_from_surface_cells(
            GetDictionary(state, "cells")
        );
        if (state is BattleState battleState)
        {
            battleState.cell_columns = rebuiltColumns;
        }
        else
        {
            state.Set("cell_columns", rebuiltColumns);
        }
    }

    public void sync_column_from_surface_cell(GodotObject state, Vector2I coord)
    {
        if (state == null)
        {
            return;
        }
        GDictionary cellColumns = GetDictionary(state, "cell_columns");
        BattleCellState surfaceCell = GetCell(state, coord);
        if (surfaceCell == null)
        {
            cellColumns.Remove(coord);
        }
        else
        {
            cellColumns[coord] = BattleCellState.build_stacked_cells_from_surface_cell(surfaceCell);
        }
        if (state is BattleState battleState)
        {
            battleState.cell_columns = cellColumns;
        }
        else
        {
            state.Set("cell_columns", cellColumns);
        }
    }

    public void _ensure_cell_columns(GodotObject state)
    {
        if (state == null)
        {
            return;
        }
        GDictionary cellColumns = GetDictionary(state, "cell_columns");
        GDictionary cells = GetDictionary(state, "cells");
        if (cellColumns.Count == 0 && cells.Count > 0)
        {
            rebuild_all_cell_columns(state);
        }
    }

    public bool set_base_terrain(GodotObject state, Vector2I coord, StringName terrain)
    {
        BattleCellState cell = GetCell(state, coord);
        if (cell == null)
        {
            return false;
        }
        cell.base_terrain = BattleTerrainRules.normalize_terrain_id(terrain);
        if (cell.base_terrain != TerrainFlowingWater)
        {
            cell.flow_direction = Vector2I.Zero;
        }
        recalculate_cell(cell);
        sync_column_from_surface_cell(state, coord);
        MarkRuntimeEdgeFacesDirty(state);
        return true;
    }

    public bool set_height_offset(GodotObject state, Vector2I coord, int height_offset)
    {
        BattleCellState cell = GetCell(state, coord);
        if (cell == null)
        {
            return false;
        }
        cell.height_offset = Math.Clamp(
            height_offset,
            MinRuntimeHeight - cell.base_height,
            MaxRuntimeHeight - cell.base_height
        );
        recalculate_cell(cell);
        sync_column_from_surface_cell(state, coord);
        MarkRuntimeEdgeFacesDirty(state);
        return true;
    }

    public bool set_edge_feature(
        GodotObject state,
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
        cell.set_edge_feature(direction, feature_state);
        sync_column_from_surface_cell(state, coord);
        MarkRuntimeEdgeFacesDirty(state);
        return true;
    }

    public bool clear_edge_feature(GodotObject state, Vector2I coord, Vector2I direction)
    {
        return set_edge_feature(state, coord, direction, BattleEdgeFeatureState.make_none());
    }

    public BattleHeightDeltaResult ApplyHeightDeltaResult(
        GodotObject state,
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
        bool changed = set_height_offset(state, coord, cell.height_offset + height_delta);
        int afterHeight = cell.current_height;
        return new BattleHeightDeltaResult(
            changed && beforeHeight != afterHeight,
            beforeHeight,
            afterHeight,
            afterHeight - beforeHeight
        );
    }

    public GDictionary apply_height_delta_result(
        GodotObject state,
        Vector2I coord,
        int height_delta
    )
    {
        return ApplyHeightDeltaResult(state, coord, height_delta).ToDictionary();
    }

    public bool apply_height_delta(GodotObject state, Vector2I coord, int height_delta)
    {
        return ApplyHeightDeltaResult(state, coord, height_delta).Changed;
    }

    public void set_occupant(GodotObject state, Vector2I coord, StringName unit_id)
    {
        BattleCellState cell = GetCell(state, coord);
        if (cell != null)
        {
            cell.occupant_unit_id = unit_id;
        }
    }

    public void set_occupants(GodotObject state, GArray coords, StringName unit_id)
    {
        if (coords == null)
        {
            return;
        }
        foreach (var coordValue in coords)
        {
            if (coordValue.VariantType == Variant.Type.Vector2I)
            {
                set_occupant(state, coordValue.AsVector2I(), unit_id);
            }
        }
    }

    public void clear_unit_occupancy(GodotObject state, BattleUnitState unit_state)
    {
        if (state == null || unit_state == null)
        {
            return;
        }
        unit_state.refresh_footprint();
        set_occupants(state, ToUntypedArray(unit_state.occupied_coords), "");
    }

    public bool place_unit(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I target_coord,
        bool ignore_height = false
    )
    {
        if (state == null || unit_state == null)
        {
            return false;
        }
        if (!can_place_unit(state, unit_state, target_coord, ignore_height))
        {
            return false;
        }
        clear_unit_occupancy(state, unit_state);
        unit_state.set_anchor_coord(target_coord);
        set_occupants(state, ToUntypedArray(unit_state.occupied_coords), unit_state.unit_id);
        return true;
    }

    public bool move_unit(GodotObject state, BattleUnitState unit_state, Vector2I target_coord)
    {
        return place_unit(state, unit_state, target_coord);
    }

    public bool move_unit_force(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I target_coord
    )
    {
        return place_unit(state, unit_state, target_coord, true);
    }

    public string get_terrain_display_name(string terrain)
    {
        return BattleTerrainRules.get_display_name(new StringName(terrain ?? ""));
    }

    public bool _can_unit_enter_cell(BattleCellState cell, BattleUnitState unit_state)
    {
        return cell != null
            && unit_state != null
            && BattleTerrainRules.can_unit_enter_terrain(
                cell.base_terrain,
                _get_unit_movement_tags(unit_state)
            );
    }

    public GArray _get_unit_movement_tags(BattleUnitState unit_state)
    {
        return unit_state != null
            ? ToUntypedStringNameArray(unit_state.movement_tags)
            : new GArray();
    }

    public int _move_path_heuristic(Vector2I from_coord, Vector2I to_coord)
    {
        return MovePathHeuristic(from_coord, to_coord);
    }

    public void _move_heap_push(GArray heap, GArray entry)
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

    public GArray _move_heap_pop(GArray heap)
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

    public GVector2IArray _reconstruct_move_path(GDictionary previous, Vector2I start, Vector2I end)
    {
        return ReconstructMovePath(previous, start, end);
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

    public int get_chebyshev_distance(Vector2I from_coord, Vector2I to_coord)
    {
        return Math.Max(Math.Abs(to_coord.X - from_coord.X), Math.Abs(to_coord.Y - from_coord.Y));
    }

    public GDictionary compute_jump_params(BattleUnitState unit_state, GodotObject effect_def)
    {
        CombatEffectDef effectDef = effect_def as CombatEffectDef;
        if (unit_state == null || effectDef == null)
        {
            return new GDictionary();
        }
        int jumpStr = _get_jump_effective_str(unit_state);
        double budget =
            effectDef.jump_base_budget
            + effectDef.jump_str_scale * jumpStr;
        double arcRatioRaw = effectDef.jump_arc_ratio;
        double arcRatio = Math.Clamp(arcRatioRaw, MinJumpArcRatio, 1.0);
        int rangeMultiplier = Math.Max(effectDef.jump_range_multiplier, 1);
        int minArc = Math.Max(1, RoundToInt(budget * arcRatio));
        double rangeBudget = Math.Max(0.0, budget * (1.0 - arcRatio));
        int maxRange = Math.Max(1, RoundToInt(rangeBudget * rangeMultiplier));
        int forcedMoveDistance = effectDef.forced_move_distance;
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

    public int compute_jump_arc_height_for_range(GDictionary parameters, int actual_range)
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

    public bool can_jump_arc(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I target_coord,
        GodotObject effect_def
    )
    {
        if (state == null || unit_state == null || effect_def == null)
        {
            return false;
        }
        if (target_coord == unit_state.coord || !is_inside(state, target_coord))
        {
            return false;
        }
        GDictionary parameters = compute_jump_params(unit_state, effect_def);
        if (parameters.Count == 0)
        {
            return false;
        }
        int maxRange = ReadInt(parameters, "max_range", 0);
        int actualRange = get_chebyshev_distance(unit_state.coord, target_coord);
        if (actualRange < 1 || actualRange > maxRange)
        {
            return false;
        }
        if (!can_place_unit(state, unit_state, target_coord, true))
        {
            return false;
        }
        BattleCellState fromCell = GetCell(state, unit_state.coord);
        BattleCellState toCell = GetCell(state, target_coord);
        if (fromCell == null || toCell == null)
        {
            return false;
        }
        int arcHeight = compute_jump_arc_height_for_range(parameters, actualRange);
        int h0 = fromCell.current_height;
        int h1 = toCell.current_height;
        double apex = Math.Max(h0, h1) + arcHeight;
        GVector2IArray path = _supercover_jump_path(unit_state.coord, target_coord);
        int pathN = path.Count - 1;
        if (pathN <= 1)
        {
            return true;
        }
        GDictionary units = GetDictionary(state, "units");
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
                BattleUnitState occupant = GetUnit(units, cell.occupant_unit_id);
                if (occupant != null)
                {
                    blockerH += _get_unit_presence_height(occupant);
                }
            }
            if (arcHAtT <= blockerH)
            {
                return false;
            }
        }
        return true;
    }

    public bool can_blink_to_coord(
        GodotObject state,
        BattleUnitState unit_state,
        Vector2I target_coord,
        GodotObject effect_def
    )
    {
        if (state == null || unit_state == null || effect_def == null)
        {
            return false;
        }
        if (target_coord == unit_state.coord || !is_inside(state, target_coord))
        {
            return false;
        }
        CombatEffectDef effectDef = effect_def as CombatEffectDef;
        int maxRange = effectDef?.forced_move_distance ?? 0;
        int actualRange = get_chebyshev_distance(unit_state.coord, target_coord);
        if (maxRange > 0 && actualRange > maxRange)
        {
            return false;
        }
        return actualRange >= 1 && can_place_unit(state, unit_state, target_coord, true);
    }

    public GVector2IArray _supercover_jump_path(Vector2I from_coord, Vector2I to_coord)
    {
        var path = new GVector2IArray();
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

    public int _get_jump_effective_str(BattleUnitState unit_state)
    {
        int rawStr = 0;
        if (unit_state?.attribute_snapshot != null)
        {
            rawStr = unit_state
                .attribute_snapshot.get_value(JumpStrengthAttribute);
        }
        int modifier = _get_jump_size_str_modifier(unit_state);
        return Math.Max(0, rawStr + modifier);
    }

    public int _get_jump_size_str_modifier(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return 0;
        }
        int bodySize = unit_state.body_size;
        if (bodySize == BattleUnitState.BODY_SIZE_SMALL())
        {
            return JumpSmallAgilityBonus;
        }
        if (bodySize == BattleUnitState.BODY_SIZE_MEDIUM())
        {
            return 0;
        }
        if (bodySize == BattleUnitState.BODY_SIZE_LARGE())
        {
            return -JumpSizeStrCost * 2;
        }
        if (bodySize == BattleUnitState.BODY_SIZE_HUGE())
        {
            return -JumpSizeStrCost * 5;
        }
        if (
            bodySize == BattleUnitState.BODY_SIZE_GARGANTUAN()
            || bodySize == BattleUnitState.BODY_SIZE_BOSS()
        )
        {
            return -JumpSizeStrCost * 8;
        }
        return 0;
    }

    public int _get_unit_presence_height(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return 1;
        }
        Vector2I footprint = unit_state.footprint_size;
        if (footprint == Vector2I.Zero)
        {
            footprint = BattleUnitState.get_footprint_size_for_body_size(unit_state.body_size);
        }
        return Math.Min(Math.Max(footprint.X, 1), Math.Max(footprint.Y, 1));
    }

    private static readonly Vector2I[] RightDownDirections = { Vector2I.Right, Vector2I.Down };

    private static GDictionary GetDictionary(GodotObject src, string property)
    {
        if (src is BattleState battleState)
        {
            return property switch
            {
                "cells" => battleState.cells,
                "cell_columns" => battleState.cell_columns,
                "units" => battleState.units,
                "runtime_edge_faces" => battleState.runtime_edge_faces,
                _ => new GDictionary(),
            };
        }
        return new GDictionary();
    }

    private static Vector2I GetMapSize(GodotObject state)
    {
        return state is BattleState battleState
            ? battleState.map_size
            : Vector2I.Zero;
    }

    private static BattleCellState GetCell(GodotObject state, Vector2I coord)
    {
        GDictionary cells = GetDictionary(state, "cells");
        return cells.ContainsKey(coord) ? cells[coord].AsGodotObject() as BattleCellState : null;
    }

    private static BattleUnitState GetUnit(GodotObject state, StringName unitId)
    {
        return GetUnit(GetDictionary(state, "units"), unitId);
    }

    private static BattleUnitState GetUnit(GDictionary units, StringName unitId)
    {
        if (units == null || unitId == "" || !units.ContainsKey(unitId))
        {
            return null;
        }
        Variant unitValue = units[unitId];
        return unitValue.AsGodotObject() as BattleUnitState;
    }

    private void AddNeighborIfInside(GodotObject state, GVector2IArray neighbors, Vector2I coord)
    {
        if (is_inside(state, coord))
        {
            neighbors.Add(coord);
        }
    }

    private void AddConeCoords(
        GodotObject state,
        GVector2IArray coords,
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
        GodotObject state,
        GVector2IArray coords,
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
            if (is_inside(state, coord))
            {
                coords.Add(coord);
            }
        }
    }

    private bool EdgeBlocksOccupancyBetween(GodotObject state, Vector2I fromCoord, Vector2I toCoord)
    {
        return _edgeService != null
            && _edgeService.blocks_occupancy_between(state, fromCoord, toCoord);
    }

    private bool EdgeIsTraversableBetween(GodotObject state, Vector2I fromCoord, Vector2I toCoord)
    {
        return _edgeService != null
            && _edgeService.is_traversable_between(state, fromCoord, toCoord);
    }

    private void MarkRuntimeEdgeFacesDirty(GodotObject state)
    {
        _edgeService?.mark_runtime_edge_faces_dirty(state);
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

    private static GVector2IArray ReconstructMovePath(
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
                return new GVector2IArray();
            }
            current = previousCoord;
        }
        reversedPath.Add(start);
        reversedPath.Reverse();
        return ToTypedVector2IArray(reversedPath);
    }

    private static GVector2IArray ReconstructMovePath(
        GDictionary previous,
        Vector2I start,
        Vector2I end
    )
    {
        var reversedPath = new List<Vector2I>();
        Vector2I current = end;
        while (current != start)
        {
            reversedPath.Add(current);
            if (!previous.ContainsKey(current))
            {
                return new GVector2IArray();
            }
            var previousValue = previous[current];
            if (previousValue.VariantType != Variant.Type.Vector2I)
            {
                return new GVector2IArray();
            }
            current = previousValue.AsVector2I();
        }
        reversedPath.Add(start);
        reversedPath.Reverse();
        return ToTypedVector2IArray(reversedPath);
    }

    private static BattleMovePathResult MovePathResult(
        bool allowed,
        int cost,
        GVector2IArray path,
        string message
    )
    {
        return new BattleMovePathResult
        {
            Allowed = allowed,
            Cost = cost,
            Path = path ?? new GVector2IArray(),
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

    private static GVector2IArray ToTypedVector2IArray(List<Vector2I> coords)
    {
        var result = new GVector2IArray();
        foreach (Vector2I coord in coords)
        {
            result.Add(coord);
        }
        return result;
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

    private static GArray ToUntypedStringNameArray(GStringNameArray source)
    {
        var result = new GArray();
        if (source == null)
        {
            return result;
        }
        foreach (StringName value in source)
        {
            result.Add(value);
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
