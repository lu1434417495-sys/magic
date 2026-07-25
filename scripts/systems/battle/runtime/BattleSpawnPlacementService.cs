using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GBattleUnitArray = System.Collections.Generic.List<BattleUnitState>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal sealed class BattleSpawnPlacementService : BattleRuntimeModuleBorrower
{

    internal bool _place_units(
        GArray units,
        GArray spawn_coords,
        bool is_ally,
        StringName spawn_side = default
    ) => PlaceUnitsTyped(
        BattleRuntimeModule.ToBattleUnitArray(units),
        BattleRuntimeModule.ToVector2IList(spawn_coords),
        is_ally,
        spawn_side
    );

    internal bool PlaceUnitsForTestsTyped(
        IReadOnlyList<BattleUnitState> units,
        IReadOnlyList<Vector2I> spawnCoords,
        bool isAlly,
        StringName spawnSide = default
    ) => PlaceUnitsTyped(units, spawnCoords, isAlly, spawnSide);

    internal bool PlaceUnitsTyped(
        IReadOnlyList<BattleUnitState> units,
        IReadOnlyList<Vector2I> spawnCoordValues,
        bool is_ally,
        StringName spawn_side = default
    )
    {
        units ??= Array.Empty<BattleUnitState>();
        spawnCoordValues ??= Array.Empty<Vector2I>();
        var placedUnits = new GBattleUnitArray();
        for (int index = 0; index < units.Count; index++)
        {
            BattleUnitState unitState = units[index];
            if (unitState == null)
                continue;
            var preferredCoords = new List<Vector2I>();
            if (index < spawnCoordValues.Count)
                preferredCoords.Add(spawnCoordValues[index]);
            foreach (Vector2I coord in spawnCoordValues)
            {
                if (!preferredCoords.Contains(coord))
                    preferredCoords.Add(coord);
            }
            Vector2I placementCoord = _find_spawn_anchor(unitState, preferredCoords, spawn_side);
            if (placementCoord == new Vector2I(-1, -1))
            {
                _clear_spawn_placed_units(placedUnits, is_ally);
                return false;
            }
            if (!_place_spawn_unit_at_anchor(unitState, placementCoord))
            {
                _clear_spawn_placed_units(placedUnits, is_ally);
                return false;
            }
            if (is_ally)
                _runtime._state.ally_unit_ids.Add(unitState.unit_id);
            else
                _runtime._state.enemy_unit_ids.Add(unitState.unit_id);
            placedUnits.Add(unitState);
        }
        return true;
    }

    internal bool _place_units(GArray units, GArray spawn_coords, bool is_ally) =>
        _place_units(units, spawn_coords, is_ally, "");

    internal void _clear_spawn_placed_units(GBattleUnitArray placed_units, bool is_ally)
    {
        if (_runtime._state == null)
            return;
        foreach (BattleUnitState unitState in placed_units)
        {
            if (unitState == null)
                continue;
            _runtime._grid_service.ClearUnitOccupancy(_runtime._state, unitState);
            _runtime._state.RemoveUnit(unitState.unit_id);
            if (is_ally)
                _runtime._state.ally_unit_ids.Remove(unitState.unit_id);
            else
                _runtime._state.enemy_unit_ids.Remove(unitState.unit_id);
        }
    }

    internal bool _place_spawn_unit_at_anchor(BattleUnitState unit_state, Vector2I coord)
    {
        if (_runtime._state == null || unit_state == null)
            return false;
        if (!_can_place_spawn_anchor(unit_state, coord))
            return false;
        unit_state.SetAnchorCoord(coord);
        _runtime._state.SetUnit(unit_state);
        _runtime._grid_service.SetOccupantsTyped(
            _runtime._state,
            unit_state.GetOccupiedCoordsReadViewTyped(),
            unit_state.unit_id
        );
        return true;
    }

    internal Vector2I _find_spawn_anchor(
        BattleUnitState unit_state,
        IReadOnlyList<Vector2I> preferred_coords,
        StringName spawn_side = default
    )
    {
        if (_runtime._state == null || unit_state == null)
            return new Vector2I(-1, -1);
        preferred_coords ??= Array.Empty<Vector2I>();
        Vector2I bestCoord = new(-1, -1);
        int bestScore = int.MinValue + 1;
        for (int preferredIndex = 0; preferredIndex < preferred_coords.Count; preferredIndex++)
        {
            Vector2I coord = preferred_coords[preferredIndex];
            if (!_can_place_spawn_anchor(unit_state, coord, spawn_side))
                continue;
            int score = _score_spawn_anchor(unit_state, coord, preferredIndex);
            if (score > bestScore)
            {
                bestScore = score;
                bestCoord = coord;
            }
        }
        if (bestCoord != new Vector2I(-1, -1))
            return bestCoord;
        foreach (Vector2I coord in preferred_coords)
        {
            if (_can_place_spawn_anchor(unit_state, coord, spawn_side))
                return coord;
        }
        for (int y = 0; y < _runtime._state.map_size.Y; y++)
        {
            for (int x = 0; x < _runtime._state.map_size.X; x++)
            {
                var coord = new Vector2I(x, y);
                if (_can_place_spawn_anchor(unit_state, coord, spawn_side))
                    return coord;
            }
        }
        return new Vector2I(-1, -1);
    }

    internal Vector2I _find_spawn_anchor(
        BattleUnitState unit_state,
        IReadOnlyList<Vector2I> preferred_coords
    ) => _find_spawn_anchor(unit_state, preferred_coords, "");

    internal bool _can_place_spawn_anchor(
        BattleUnitState unit_state,
        Vector2I coord,
        StringName spawn_side = default
    )
    {
        if (_runtime._state == null || unit_state == null)
            return false;
        if (
            !_runtime._grid_service.CanPlaceFootprint(
                _runtime._state,
                coord,
                unit_state.GetFootprintSize(),
                unit_state.unit_id,
                unit_state
            )
        )
            return false;
        if (!BattleRuntimeModule.IsEmpty(spawn_side) && !_footprint_matches_spawn_side(unit_state, coord, spawn_side))
            return false;
        foreach (Vector2I footprintCoord in _runtime._grid_service.GetUnitTargetCoords(unit_state, coord))
        {
            BattleCellState cell = _runtime._grid_service.GetCellState(_runtime._state, footprintCoord);
            if (cell == null || BattleTerrainRules.IsWaterTerrain(cell.base_terrain))
                return false;
        }
        return true;
    }

    internal StringName _resolve_spawn_side_from_coords(
        IReadOnlyList<Vector2I> spawn_coords
    )
    {
        if (_runtime._state == null || _get_long_edge_side_extent() <= 1)
            return "";
        int nearCount = 0;
        int farCount = 0;
        foreach (Vector2I coord in spawn_coords ?? Array.Empty<Vector2I>())
        {
            if (_coord_matches_spawn_side(coord, BattleRuntimeModule.SPAWN_SIDE_NEAR_LONG_EDGE_VALUE))
                nearCount++;
            else if (_coord_matches_spawn_side(coord, BattleRuntimeModule.SPAWN_SIDE_FAR_LONG_EDGE_VALUE))
                farCount++;
        }
        if (nearCount == 0 && farCount == 0)
            return "";
        return nearCount >= farCount
            ? BattleRuntimeModule.SPAWN_SIDE_NEAR_LONG_EDGE_VALUE
            : BattleRuntimeModule.SPAWN_SIDE_FAR_LONG_EDGE_VALUE;
    }

    internal StringName _get_opposite_spawn_side(StringName spawn_side)
    {
        if (spawn_side == BattleRuntimeModule.SPAWN_SIDE_NEAR_LONG_EDGE_VALUE)
            return BattleRuntimeModule.SPAWN_SIDE_FAR_LONG_EDGE_VALUE;
        if (spawn_side == BattleRuntimeModule.SPAWN_SIDE_FAR_LONG_EDGE_VALUE)
            return BattleRuntimeModule.SPAWN_SIDE_NEAR_LONG_EDGE_VALUE;
        return "";
    }

    internal bool _footprint_matches_spawn_side(
        BattleUnitState unit_state,
        Vector2I coord,
        StringName spawn_side
    )
    {
        if (_runtime._state == null || unit_state == null)
            return false;
        foreach (Vector2I footprintCoord in _runtime._grid_service.GetUnitTargetCoords(unit_state, coord))
        {
            if (!_coord_matches_spawn_side(footprintCoord, spawn_side))
                return false;
        }
        return true;
    }

    internal bool _coord_matches_spawn_side(Vector2I coord, StringName spawn_side)
    {
        if (_runtime._state == null || _get_long_edge_side_extent() <= 1)
            return true;
        int sideValue = _get_long_edge_side_axis_value(coord);
        int splitValue = Mathf.FloorToInt(_get_long_edge_side_extent() * 0.5f);
        if (spawn_side == BattleRuntimeModule.SPAWN_SIDE_NEAR_LONG_EDGE_VALUE)
            return sideValue < splitValue;
        if (spawn_side == BattleRuntimeModule.SPAWN_SIDE_FAR_LONG_EDGE_VALUE)
            return sideValue >= splitValue;
        return true;
    }

    internal int _get_long_edge_side_axis_value(Vector2I coord) =>
        _runtime._state == null ? 0 : (_runtime._state.map_size.X >= _runtime._state.map_size.Y ? coord.Y : coord.X);

    internal int _get_long_edge_side_extent() =>
        _runtime._state == null
            ? 0
            : (_runtime._state.map_size.X >= _runtime._state.map_size.Y ? _runtime._state.map_size.Y : _runtime._state.map_size.X);

    internal int _score_spawn_anchor(BattleUnitState unit_state, Vector2I coord, int preferred_index)
    {
        int mobilityScore = _count_spawn_anchor_reachable_coords(unit_state, coord);
        int edgeClearance = _get_spawn_anchor_edge_clearance(unit_state, coord);
        int centerBias = _get_spawn_anchor_center_bias(unit_state, coord);
        return mobilityScore * 100 + edgeClearance * 18 + centerBias * 4 - preferred_index;
    }

    internal int _count_spawn_anchor_reachable_coords(
        BattleUnitState unit_state,
        Vector2I start_coord
    )
    {
        if (_runtime._state == null || unit_state == null)
            return 0;
        int moveBudget = Math.Min(Math.Max(unit_state.GetCurrentMovePoints(), 0), 4);
        if (moveBudget <= 0)
            moveBudget = 1;
        var bestCosts = new Dictionary<Vector2I, int> { [start_coord] = 0 };
        var frontier = new List<Vector2I> { start_coord };
        int frontierIndex = 0;
        while (frontierIndex < frontier.Count)
        {
            Vector2I currentCoord = frontier[frontierIndex++];
            int spentCost = bestCosts[currentCoord];
            foreach (Vector2I neighborCoord in _runtime._grid_service.GetNeighbors4(_runtime._state, currentCoord))
            {
                if (
                    !_runtime._grid_service.CanUnitStepBetweenAnchors(
                        _runtime._state,
                        unit_state,
                        currentCoord,
                        neighborCoord
                    )
                )
                    continue;
                int nextCost =
                    spentCost + _runtime._grid_service.GetUnitMoveCost(_runtime._state, unit_state, neighborCoord);
                if (nextCost > moveBudget)
                    continue;
                if (
                    bestCosts.TryGetValue(neighborCoord, out int existingCost)
                    && nextCost >= existingCost
                )
                    continue;
                bestCosts[neighborCoord] = nextCost;
                frontier.Add(neighborCoord);
            }
        }
        return bestCosts.Count - 1;
    }

    internal int _get_spawn_anchor_edge_clearance(BattleUnitState unit_state, Vector2I coord)
    {
        if (_runtime._state == null || unit_state == null)
            return 0;
        Vector2I footprint = unit_state.GetFootprintSize();
        int left = coord.X;
        int top = coord.Y;
        int right = _runtime._state.map_size.X - (coord.X + footprint.X);
        int bottom = _runtime._state.map_size.Y - (coord.Y + footprint.Y);
        return Math.Min(Math.Min(left, right), Math.Min(top, bottom));
    }

    internal int _get_spawn_anchor_center_bias(BattleUnitState unit_state, Vector2I coord)
    {
        if (_runtime._state == null || unit_state == null)
            return 0;
        Vector2I footprint = unit_state.GetFootprintSize();
        float centerX = (_runtime._state.map_size.X - footprint.X) * 0.5f;
        float centerY = (_runtime._state.map_size.Y - footprint.Y) * 0.5f;
        float distance = Mathf.Abs(coord.X - centerX) + Mathf.Abs(coord.Y - centerY);
        return -Mathf.RoundToInt(distance * 10.0f);
    }
}
