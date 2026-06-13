using System;
using System.Collections.Generic;
using Godot;

// 翻译自 world_map_grid_system.gd（2026-05-25，世界系统 C# 迁移）。
// 世界地图网格：维护世界尺寸（格子）、区块尺寸、占用格子与 footprint 状态。
public sealed class WorldMapGridSystem
{
    private Vector2I _world_size_cells = Vector2I.Zero;
    private Vector2I _chunk_size = Vector2I.One;
    private readonly Dictionary<Vector2I, WorldMapOccupantState> _occupiedCells = new();
    private readonly Dictionary<string, WorldMapFootprintState> _footprintsByEntityId =
        new(StringComparer.Ordinal);

    public void Setup(Vector2I world_size_in_chunks, Vector2I chunk_size)
    {
        _chunk_size = new Vector2I(Math.Max(chunk_size.X, 1), Math.Max(chunk_size.Y, 1));
        Vector2I normalizedWorldSize = new(
            Math.Max(world_size_in_chunks.X, 0),
            Math.Max(world_size_in_chunks.Y, 0)
        );
        _world_size_cells = new Vector2I(
            normalizedWorldSize.X * _chunk_size.X,
            normalizedWorldSize.Y * _chunk_size.Y
        );
        _occupiedCells.Clear();
        _footprintsByEntityId.Clear();
    }

    public Vector2I GetWorldSizeCells()
    {
        return _world_size_cells;
    }

    public Vector2I GetChunkSize()
    {
        return _chunk_size;
    }

    public WorldMapCellData GetCell(Vector2I coord)
    {
        if (!IsCellInsideWorld(coord))
        {
            return null;
        }

        var cell = new WorldMapCellData(coord, GetChunkCoord(coord));
        WorldMapOccupantState occupantState = GetOccupantState(coord);
        if (occupantState != null)
        {
            cell.occupant_id = occupantState.OccupantId;
            cell.footprint_root_id = occupantState.FootprintRootId;
        }
        return cell;
    }

    public bool IsCellInsideWorld(Vector2I coord)
    {
        return coord.X >= 0
            && coord.Y >= 0
            && coord.X < _world_size_cells.X
            && coord.Y < _world_size_cells.Y;
    }

    public bool IsCellWalkable(Vector2I coord)
    {
        return IsCellInsideWorld(coord);
    }

    public string GetOccupantRoot(Vector2I coord)
    {
        if (!IsCellInsideWorld(coord))
        {
            return "";
        }
        WorldMapOccupantState occupantState = GetOccupantState(coord);
        return occupantState != null ? occupantState.FootprintRootId : "";
    }

    public bool CanPlaceFootprint(Vector2I origin, Vector2I size)
    {
        if (size.X <= 0 || size.Y <= 0)
        {
            return false;
        }

        for (int y = 0; y < size.Y; y++)
        {
            for (int x = 0; x < size.X; x++)
            {
                Vector2I coord = origin + new Vector2I(x, y);
                if (!IsCellInsideWorld(coord))
                {
                    return false;
                }
                if (_occupiedCells.ContainsKey(coord))
                {
                    return false;
                }
            }
        }
        return true;
    }

    public bool RegisterFootprint(string entity_id, Vector2I origin, Vector2I size)
    {
        if (string.IsNullOrEmpty(entity_id))
        {
            return false;
        }
        WorldMapFootprintState previousFootprint = GetFootprintState(entity_id);
        if (previousFootprint != null)
        {
            ClearFootprint(entity_id);
        }
        if (!CanPlaceFootprint(origin, size))
        {
            if (previousFootprint != null)
            {
                WriteFootprintCells(entity_id, previousFootprint.Origin, previousFootprint.Size);
            }
            return false;
        }
        WriteFootprintCells(entity_id, origin, size);
        return true;
    }

    public void ClearFootprint(string entity_id)
    {
        if (string.IsNullOrEmpty(entity_id))
        {
            return;
        }

        WorldMapFootprintState footprint = GetFootprintState(entity_id);
        if (footprint == null)
        {
            return;
        }

        for (int y = 0; y < footprint.Size.Y; y++)
        {
            for (int x = 0; x < footprint.Size.X; x++)
            {
                Vector2I coord = footprint.Origin + new Vector2I(x, y);
                WorldMapOccupantState occupantState = GetOccupantState(coord);
                if (occupantState != null && occupantState.FootprintRootId == entity_id)
                {
                    _occupiedCells.Remove(coord);
                }
            }
        }

        _footprintsByEntityId.Remove(entity_id);
    }

    public List<Vector2I> GetNeighbors4(Vector2I coord)
    {
        var neighbors = new List<Vector2I>();
        Vector2I[] directions = { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };

        foreach (Vector2I direction in directions)
        {
            Vector2I candidate = coord + direction;
            if (IsCellInsideWorld(candidate))
            {
                neighbors.Add(candidate);
            }
        }

        return neighbors;
    }

    public Vector2I GetChunkCoord(Vector2I coord)
    {
        if (_chunk_size.X <= 0 || _chunk_size.Y <= 0)
        {
            return Vector2I.Zero;
        }
        return new Vector2I(coord.X / _chunk_size.X, coord.Y / _chunk_size.Y);
    }

    private WorldMapOccupantState GetOccupantState(Vector2I coord)
    {
        return _occupiedCells.TryGetValue(coord, out WorldMapOccupantState occupantState)
            ? occupantState
            : null;
    }

    private WorldMapFootprintState GetFootprintState(string entityId)
    {
        if (string.IsNullOrEmpty(entityId))
        {
            return null;
        }

        if (!_footprintsByEntityId.TryGetValue(entityId, out WorldMapFootprintState footprint))
        {
            return null;
        }
        return footprint;
    }

    private void WriteFootprintCells(string entityId, Vector2I origin, Vector2I size)
    {
        _footprintsByEntityId[entityId] = new WorldMapFootprintState(origin, size);

        for (int y = 0; y < size.Y; y++)
        {
            for (int x = 0; x < size.X; x++)
            {
                Vector2I coord = origin + new Vector2I(x, y);
                _occupiedCells[coord] = new WorldMapOccupantState(entityId, entityId);
            }
        }
    }
}
