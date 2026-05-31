using Godot;

[GlobalClass]
public partial class BattleTerrainTopologyService : RefCounted
{
    public Godot.Collections.Array<Godot.Collections.Dictionary> reclassify_all_water_terrain(
        Godot.Collections.Dictionary cells,
        Vector2I mapSize
    )
    {
        return _reclassify_components(cells, mapSize, _collect_all_water_coords(cells));
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> reclassify_water_terrain_near_coords(
        Godot.Collections.Dictionary cells,
        Vector2I mapSize,
        Godot.Collections.Array<Vector2I> seedCoords
    )
    {
        return _reclassify_components(
            cells,
            mapSize,
            _collect_seed_water_coords(cells, mapSize, seedCoords)
        );
    }

    private Godot.Collections.Array<Godot.Collections.Dictionary> _reclassify_components(
        Godot.Collections.Dictionary cells,
        Vector2I mapSize,
        Godot.Collections.Array<Vector2I> startCoords
    )
    {
        var changes = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (cells.Count == 0 || mapSize == Vector2I.Zero || startCoords.Count == 0)
            return changes;

        var visited = new Godot.Collections.Dictionary();
        foreach (var start in startCoords)
        {
            if (visited.ContainsKey(start))
                continue;
            var component = _collect_component(cells, mapSize, start, visited);
            if (component.Count == 0)
                continue;
            var componentLookup = new Godot.Collections.Dictionary();
            foreach (var coord in component)
                componentLookup[coord] = true;
            bool componentHasOutlet = _component_has_outlet(cells, mapSize, component);
            foreach (var coord in component)
            {
                var cell = _get_cell(cells, coord) as BattleCellState;
                if (cell == null)
                    continue;
                var nextFlowDirection = Vector2I.Zero;
                var nextTerrain = BattleTerrainRules.TERRAIN_DEEP_WATER();
                if (componentHasOutlet)
                    nextFlowDirection = _resolve_flow_direction(
                        cells,
                        mapSize,
                        coord,
                        componentLookup
                    );
                if (nextFlowDirection != Vector2I.Zero)
                    nextTerrain = BattleTerrainRules.TERRAIN_FLOWING_WATER();
                else if (_is_shallow_cell(cells, mapSize, coord))
                    nextTerrain = BattleTerrainRules.TERRAIN_SHALLOW_WATER();
                if (cell.base_terrain != nextTerrain || cell.flow_direction != nextFlowDirection)
                {
                    changes.Add(
                        new Godot.Collections.Dictionary
                        {
                            { "coord", coord },
                            { "before_terrain", cell.base_terrain },
                            { "after_terrain", nextTerrain },
                            { "before_flow_direction", cell.flow_direction },
                            { "after_flow_direction", nextFlowDirection },
                        }
                    );
                }
            }
        }
        return changes;
    }

    private Godot.Collections.Array<Vector2I> _collect_all_water_coords(
        Godot.Collections.Dictionary cells
    )
    {
        var results = new Godot.Collections.Array<Vector2I>();
        foreach (var coordValue in cells.Keys)
        {
            if (coordValue.VariantType != Variant.Type.Vector2I)
                continue;
            var coord = coordValue.AsVector2I();
            var cell = _get_cell(cells, coord);
            if (_is_water_like(cell))
                results.Add(coord);
        }
        return results;
    }

    private Godot.Collections.Array<Vector2I> _collect_seed_water_coords(
        Godot.Collections.Dictionary cells,
        Vector2I mapSize,
        Godot.Collections.Array<Vector2I> seedCoords
    )
    {
        var results = new Godot.Collections.Array<Vector2I>();
        var seen = new Godot.Collections.Dictionary();
        foreach (var seed in seedCoords)
        {
            foreach (var coord in _get_coord_and_neighbors(mapSize, seed))
            {
                if (seen.ContainsKey(coord))
                    continue;
                seen[coord] = true;
                var cell = _get_cell(cells, coord);
                if (_is_water_like(cell))
                    results.Add(coord);
            }
        }
        return results;
    }

    private Godot.Collections.Array<Vector2I> _collect_component(
        Godot.Collections.Dictionary cells,
        Vector2I mapSize,
        Vector2I start,
        Godot.Collections.Dictionary visited
    )
    {
        var startCell = _get_cell(cells, start);
        if (!_is_water_like(startCell))
            return new Godot.Collections.Array<Vector2I>();

        var component = new Godot.Collections.Array<Vector2I>();
        var frontier = new Godot.Collections.Array<Vector2I> { start };
        while (frontier.Count > 0)
        {
            var current = frontier[0];
            frontier.RemoveAt(0);
            if (visited.ContainsKey(current))
                continue;
            var currentCell = _get_cell(cells, current);
            if (!_is_water_like(currentCell))
                continue;
            visited[current] = true;
            component.Add(current);
            foreach (var neighbor in _get_neighbors_4(mapSize, current))
            {
                if (!visited.ContainsKey(neighbor))
                    frontier.Add(neighbor);
            }
        }
        return component;
    }

    private bool _component_has_outlet(
        Godot.Collections.Dictionary cells,
        Vector2I mapSize,
        Godot.Collections.Array<Vector2I> component
    )
    {
        foreach (var coord in component)
        {
            if (_is_edge_coord(mapSize, coord))
                return true;
            var cell = _get_cell(cells, coord);
            if (cell == null)
                continue;
            foreach (var neighbor in _get_neighbors_4(mapSize, coord))
            {
                var neighborCell = _get_cell(cells, neighbor);
                if (_is_water_like(neighborCell))
                    continue;
                if (neighborCell != null && neighborCell.current_height <= cell.current_height)
                    return true;
            }
        }
        return false;
    }

    private Vector2I _resolve_flow_direction(
        Godot.Collections.Dictionary cells,
        Vector2I mapSize,
        Vector2I coord,
        Godot.Collections.Dictionary componentLookup
    )
    {
        var cell = _get_cell(cells, coord);
        if (cell == null)
            return Vector2I.Zero;

        var directions = new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
        var bestDirection = Vector2I.Zero;
        int bestNeighborHeight = int.MaxValue;
        foreach (var direction in directions)
        {
            var neighborCoord = coord + direction;
            if (!_is_inside(mapSize, neighborCoord))
                return direction;
            var neighborCell = _get_cell(cells, neighborCoord);
            if (_is_water_like(neighborCell))
                continue;
            if (neighborCell == null)
                continue;
            int neighborHeight = neighborCell.current_height;
            if (neighborHeight > cell.current_height)
                continue;
            if (neighborHeight < bestNeighborHeight)
            {
                bestNeighborHeight = neighborHeight;
                bestDirection = direction;
            }
        }
        if (bestDirection != Vector2I.Zero)
            return bestDirection;

        foreach (var direction in directions)
        {
            var neighborCoord = coord + direction;
            if (componentLookup.ContainsKey(neighborCoord))
            {
                var neighborCell = _get_cell(cells, neighborCoord);
                if (
                    neighborCell != null
                    && neighborCell.base_terrain == BattleTerrainRules.TERRAIN_FLOWING_WATER()
                )
                    return direction;
            }
        }
        return Vector2I.Zero;
    }

    private bool _is_shallow_cell(
        Godot.Collections.Dictionary cells,
        Vector2I mapSize,
        Vector2I coord
    )
    {
        var cell = _get_cell(cells, coord);
        if (cell == null)
            return false;
        int minBankDelta = int.MaxValue;
        foreach (var neighbor in _get_neighbors_4(mapSize, coord))
        {
            var neighborCell = _get_cell(cells, neighbor);
            if (_is_water_like(neighborCell))
                continue;
            if (neighborCell == null)
            {
                minBankDelta = 0;
                continue;
            }
            minBankDelta = Mathf.Min(
                minBankDelta,
                neighborCell.current_height - cell.current_height
            );
        }
        if (minBankDelta == int.MaxValue)
            return false;
        return minBankDelta <= 1;
    }

    private static bool _is_water_like(BattleCellState cell)
    {
        return cell != null && BattleTerrainRules.is_water_terrain(cell.base_terrain);
    }

    private Godot.Collections.Array<Vector2I> _get_coord_and_neighbors(
        Vector2I mapSize,
        Vector2I coord
    )
    {
        var coords = new Godot.Collections.Array<Vector2I>();
        if (_is_inside(mapSize, coord))
            coords.Add(coord);
        foreach (var neighbor in _get_neighbors_4(mapSize, coord))
            coords.Add(neighbor);
        return coords;
    }

    private Godot.Collections.Array<Vector2I> _get_neighbors_4(Vector2I mapSize, Vector2I coord)
    {
        var neighbors = new Godot.Collections.Array<Vector2I>();
        var directions = new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
        foreach (var direction in directions)
        {
            var candidate = coord + direction;
            if (_is_inside(mapSize, candidate))
                neighbors.Add(candidate);
        }
        return neighbors;
    }

    private static bool _is_inside(Vector2I mapSize, Vector2I coord)
    {
        return coord.X >= 0 && coord.Y >= 0 && coord.X < mapSize.X && coord.Y < mapSize.Y;
    }

    private static BattleCellState _get_cell(Godot.Collections.Dictionary cells, Vector2I coord)
    {
        if (!cells.ContainsKey(coord))
            return null;
        return cells[coord].AsGodotObject() as BattleCellState;
    }

    private static bool _is_edge_coord(Vector2I mapSize, Vector2I coord)
    {
        return coord.X <= 0 || coord.Y <= 0 || coord.X >= mapSize.X - 1 || coord.Y >= mapSize.Y - 1;
    }
}
