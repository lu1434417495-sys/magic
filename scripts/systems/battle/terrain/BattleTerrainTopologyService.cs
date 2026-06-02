using System;
using System.Collections.Generic;
using Godot;

public readonly struct BattleTerrainTopologyChange
{
    public BattleTerrainTopologyChange(
        Vector2I coord,
        StringName beforeTerrain,
        StringName afterTerrain,
        Vector2I beforeFlowDirection,
        Vector2I afterFlowDirection
    )
    {
        Coord = coord;
        BeforeTerrain = beforeTerrain;
        AfterTerrain = afterTerrain;
        BeforeFlowDirection = beforeFlowDirection;
        AfterFlowDirection = afterFlowDirection;
    }

    public Vector2I Coord { get; }
    public StringName BeforeTerrain { get; }
    public StringName AfterTerrain { get; }
    public Vector2I BeforeFlowDirection { get; }
    public Vector2I AfterFlowDirection { get; }
}

public sealed class BattleTerrainTopologyService
{
    private static readonly Vector2I[] CardinalDirections =
    {
        Vector2I.Left,
        Vector2I.Right,
        Vector2I.Up,
        Vector2I.Down,
    };

    public IReadOnlyList<BattleTerrainTopologyChange> ReclassifyAllWaterTerrain(
        BattleState state
    )
    {
        return ReclassifyComponents(state, CollectAllWaterCoords(state));
    }

    public IReadOnlyList<BattleTerrainTopologyChange> ReclassifyWaterTerrainNearCoords(
        BattleState state,
        IEnumerable<Vector2I> seedCoords
    )
    {
        return ReclassifyComponents(state, CollectSeedWaterCoords(state, seedCoords));
    }

    private static List<BattleTerrainTopologyChange> ReclassifyComponents(
        BattleState state,
        IReadOnlyList<Vector2I> startCoords
    )
    {
        var changes = new List<BattleTerrainTopologyChange>();
        if (state == null || state.map_size == Vector2I.Zero || startCoords.Count == 0)
        {
            return changes;
        }

        var visited = new HashSet<Vector2I>();
        foreach (Vector2I start in startCoords)
        {
            if (visited.Contains(start))
            {
                continue;
            }
            List<Vector2I> component = CollectComponent(state, start, visited);
            if (component.Count == 0)
            {
                continue;
            }
            var componentLookup = new HashSet<Vector2I>(component);
            bool componentHasOutlet = ComponentHasOutlet(state, component);
            foreach (Vector2I coord in component)
            {
                BattleCellState cell = GetCell(state, coord);
                if (cell == null)
                {
                    continue;
                }

                Vector2I nextFlowDirection = Vector2I.Zero;
                StringName nextTerrain = BattleTerrainRules.TERRAIN_DEEP_WATER();
                if (componentHasOutlet)
                {
                    nextFlowDirection = ResolveFlowDirection(state, coord, componentLookup);
                }
                if (nextFlowDirection != Vector2I.Zero)
                {
                    nextTerrain = BattleTerrainRules.TERRAIN_FLOWING_WATER();
                }
                else if (IsShallowCell(state, coord))
                {
                    nextTerrain = BattleTerrainRules.TERRAIN_SHALLOW_WATER();
                }
                if (cell.base_terrain != nextTerrain || cell.flow_direction != nextFlowDirection)
                {
                    changes.Add(
                        new BattleTerrainTopologyChange(
                            coord,
                            cell.base_terrain,
                            nextTerrain,
                            cell.flow_direction,
                            nextFlowDirection
                        )
                    );
                }
            }
        }
        return changes;
    }

    private static List<Vector2I> CollectAllWaterCoords(BattleState state)
    {
        var results = new List<Vector2I>();
        if (state == null)
        {
            return results;
        }
        foreach (BattleState.BattleCellEntry entry in state.GetCellEntriesTyped())
        {
            if (IsWaterLike(entry.Cell))
            {
                results.Add(entry.Coord);
            }
        }
        return results;
    }

    private static List<Vector2I> CollectSeedWaterCoords(
        BattleState state,
        IEnumerable<Vector2I> seedCoords
    )
    {
        var results = new List<Vector2I>();
        if (state == null)
        {
            return results;
        }

        var seen = new HashSet<Vector2I>();
        foreach (Vector2I seed in seedCoords ?? Array.Empty<Vector2I>())
        {
            foreach (Vector2I coord in GetCoordAndNeighbors(state.map_size, seed))
            {
                if (!seen.Add(coord))
                {
                    continue;
                }
                if (IsWaterLike(GetCell(state, coord)))
                {
                    results.Add(coord);
                }
            }
        }
        return results;
    }

    private static List<Vector2I> CollectComponent(
        BattleState state,
        Vector2I start,
        HashSet<Vector2I> visited
    )
    {
        if (!IsWaterLike(GetCell(state, start)))
        {
            return new List<Vector2I>();
        }

        var component = new List<Vector2I>();
        var frontier = new Queue<Vector2I>();
        frontier.Enqueue(start);
        while (frontier.Count > 0)
        {
            Vector2I current = frontier.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }
            if (!IsWaterLike(GetCell(state, current)))
            {
                continue;
            }
            component.Add(current);
            foreach (Vector2I neighbor in GetNeighbors4(state.map_size, current))
            {
                if (!visited.Contains(neighbor))
                {
                    frontier.Enqueue(neighbor);
                }
            }
        }
        return component;
    }

    private static bool ComponentHasOutlet(
        BattleState state,
        IReadOnlyList<Vector2I> component
    )
    {
        foreach (Vector2I coord in component)
        {
            if (IsEdgeCoord(state.map_size, coord))
            {
                return true;
            }
            BattleCellState cell = GetCell(state, coord);
            if (cell == null)
            {
                continue;
            }
            foreach (Vector2I neighbor in GetNeighbors4(state.map_size, coord))
            {
                BattleCellState neighborCell = GetCell(state, neighbor);
                if (IsWaterLike(neighborCell))
                {
                    continue;
                }
                if (neighborCell != null && neighborCell.current_height <= cell.current_height)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static Vector2I ResolveFlowDirection(
        BattleState state,
        Vector2I coord,
        HashSet<Vector2I> componentLookup
    )
    {
        BattleCellState cell = GetCell(state, coord);
        if (cell == null)
        {
            return Vector2I.Zero;
        }

        Vector2I bestDirection = Vector2I.Zero;
        int bestNeighborHeight = int.MaxValue;
        foreach (Vector2I direction in CardinalDirections)
        {
            Vector2I neighborCoord = coord + direction;
            if (!IsInside(state.map_size, neighborCoord))
            {
                return direction;
            }
            BattleCellState neighborCell = GetCell(state, neighborCoord);
            if (IsWaterLike(neighborCell) || neighborCell == null)
            {
                continue;
            }
            int neighborHeight = neighborCell.current_height;
            if (neighborHeight > cell.current_height)
            {
                continue;
            }
            if (neighborHeight < bestNeighborHeight)
            {
                bestNeighborHeight = neighborHeight;
                bestDirection = direction;
            }
        }
        if (bestDirection != Vector2I.Zero)
        {
            return bestDirection;
        }

        foreach (Vector2I direction in CardinalDirections)
        {
            Vector2I neighborCoord = coord + direction;
            if (!componentLookup.Contains(neighborCoord))
            {
                continue;
            }
            BattleCellState neighborCell = GetCell(state, neighborCoord);
            if (
                neighborCell != null
                && neighborCell.base_terrain == BattleTerrainRules.TERRAIN_FLOWING_WATER()
            )
            {
                return direction;
            }
        }
        return Vector2I.Zero;
    }

    private static bool IsShallowCell(BattleState state, Vector2I coord)
    {
        BattleCellState cell = GetCell(state, coord);
        if (cell == null)
        {
            return false;
        }

        int minBankDelta = int.MaxValue;
        foreach (Vector2I neighbor in GetNeighbors4(state.map_size, coord))
        {
            BattleCellState neighborCell = GetCell(state, neighbor);
            if (IsWaterLike(neighborCell))
            {
                continue;
            }
            if (neighborCell == null)
            {
                minBankDelta = 0;
                continue;
            }
            minBankDelta = Math.Min(minBankDelta, neighborCell.current_height - cell.current_height);
        }
        return minBankDelta != int.MaxValue && minBankDelta <= 1;
    }

    private static bool IsWaterLike(BattleCellState cell)
    {
        return cell != null && BattleTerrainRules.is_water_terrain(cell.base_terrain);
    }

    private static List<Vector2I> GetCoordAndNeighbors(Vector2I mapSize, Vector2I coord)
    {
        var coords = new List<Vector2I>();
        if (IsInside(mapSize, coord))
        {
            coords.Add(coord);
        }
        coords.AddRange(GetNeighbors4(mapSize, coord));
        return coords;
    }

    private static List<Vector2I> GetNeighbors4(Vector2I mapSize, Vector2I coord)
    {
        var neighbors = new List<Vector2I>();
        foreach (Vector2I direction in CardinalDirections)
        {
            Vector2I candidate = coord + direction;
            if (IsInside(mapSize, candidate))
            {
                neighbors.Add(candidate);
            }
        }
        return neighbors;
    }

    private static bool IsInside(Vector2I mapSize, Vector2I coord)
    {
        return coord.X >= 0 && coord.Y >= 0 && coord.X < mapSize.X && coord.Y < mapSize.Y;
    }

    private static BattleCellState GetCell(BattleState state, Vector2I coord)
    {
        return state != null && state.TryGetCellTyped(coord, out BattleCellState cell)
            ? cell
            : null;
    }

    private static bool IsEdgeCoord(Vector2I mapSize, Vector2I coord)
    {
        return coord.X <= 0 || coord.Y <= 0 || coord.X >= mapSize.X - 1 || coord.Y >= mapSize.Y - 1;
    }
}
