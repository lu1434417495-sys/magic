using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// 翻译自 world_map_grid_system.gd（2026-05-25，世界系统 C# 迁移）。
// 世界地图网格：维护世界尺寸（格子）、区块尺寸、占用格子与 footprint 状态。
[GlobalClass]
public partial class WorldMapGridSystem : RefCounted
{
    private Vector2I _world_size_cells = Vector2I.Zero;
    private Vector2I _chunk_size = Vector2I.One;
    private GDictionary _occupied_cells = new();
    private GDictionary _footprints = new();

    public void setup(Vector2I world_size_in_chunks, Vector2I chunk_size)
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
        _occupied_cells.Clear();
        _footprints.Clear();
    }

    public Vector2I get_world_size_cells()
    {
        return _world_size_cells;
    }

    public Vector2I get_chunk_size()
    {
        return _chunk_size;
    }

    public WorldMapCellData get_cell(Vector2I coord)
    {
        if (!is_cell_inside_world(coord))
        {
            return null;
        }

        var cell = new WorldMapCellData(coord, get_chunk_coord(coord));
        WorldMapOccupantState occupantState = GetOccupantState(coord);
        if (occupantState != null)
        {
            cell.occupant_id = occupantState.occupant_id;
            cell.footprint_root_id = occupantState.footprint_root_id;
        }
        return cell;
    }

    public bool is_cell_inside_world(Vector2I coord)
    {
        return coord.X >= 0
            && coord.Y >= 0
            && coord.X < _world_size_cells.X
            && coord.Y < _world_size_cells.Y;
    }

    public bool is_cell_walkable(Vector2I coord)
    {
        return is_cell_inside_world(coord);
    }

    public string get_occupant_root(Vector2I coord)
    {
        if (!is_cell_inside_world(coord))
        {
            return "";
        }
        WorldMapOccupantState occupantState = GetOccupantState(coord);
        return occupantState != null ? occupantState.footprint_root_id : "";
    }

    public bool can_place_footprint(Vector2I origin, Vector2I size)
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
                if (!is_cell_inside_world(coord))
                {
                    return false;
                }
                if (_occupied_cells.ContainsKey(coord))
                {
                    return false;
                }
            }
        }
        return true;
    }

    public bool register_footprint(string entity_id, Vector2I origin, Vector2I size)
    {
        if (string.IsNullOrEmpty(entity_id))
        {
            return false;
        }
        WorldMapFootprintState previousFootprint = GetFootprintState(entity_id);
        if (previousFootprint != null)
        {
            clear_footprint(entity_id);
        }
        if (!can_place_footprint(origin, size))
        {
            if (previousFootprint != null)
            {
                WriteFootprintCells(entity_id, previousFootprint.origin, previousFootprint.size);
            }
            return false;
        }
        WriteFootprintCells(entity_id, origin, size);
        return true;
    }

    public void clear_footprint(string entity_id)
    {
        WorldMapFootprintState footprint = GetFootprintState(entity_id);
        if (footprint == null)
        {
            return;
        }

        for (int y = 0; y < footprint.size.Y; y++)
        {
            for (int x = 0; x < footprint.size.X; x++)
            {
                Vector2I coord = footprint.origin + new Vector2I(x, y);
                WorldMapOccupantState occupantState = GetOccupantState(coord);
                if (occupantState != null && occupantState.footprint_root_id == entity_id)
                {
                    _occupied_cells.Remove(coord);
                }
            }
        }

        _footprints.Remove(entity_id);
    }

    public GVector2IArray get_neighbors_4(Vector2I coord)
    {
        var neighbors = new GVector2IArray();
        Vector2I[] directions = { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };

        foreach (Vector2I direction in directions)
        {
            Vector2I candidate = coord + direction;
            if (is_cell_inside_world(candidate))
            {
                neighbors.Add(candidate);
            }
        }

        return neighbors;
    }

    public Vector2I get_chunk_coord(Vector2I coord)
    {
        if (_chunk_size.X <= 0 || _chunk_size.Y <= 0)
        {
            return Vector2I.Zero;
        }
        return new Vector2I(coord.X / _chunk_size.X, coord.Y / _chunk_size.Y);
    }

    private WorldMapOccupantState GetOccupantState(Vector2I coord)
    {
        GdInterop.TryGet(_occupied_cells, coord, out var occupantValue);
        if (occupantValue.TryAsObject(out WorldMapOccupantState occupantObject))
        {
            if (occupantObject.is_empty())
            {
                _occupied_cells.Remove(coord);
                return null;
            }
            return occupantObject;
        }
        if (occupantValue.TryAsDictionary(out GDictionary occupantDict))
        {
            if (
                !occupantDict.ContainsKey("occupant_id")
                || !occupantDict.ContainsKey("footprint_root_id")
            )
            {
                _occupied_cells.Remove(coord);
                return null;
            }
            WorldMapOccupantState occupantState = WorldMapOccupantState.create(
                GdInterop.GetString(occupantDict, "occupant_id"),
                GdInterop.GetString(occupantDict, "footprint_root_id")
            );
            if (occupantState.is_empty())
            {
                _occupied_cells.Remove(coord);
                return null;
            }
            _occupied_cells[coord] = occupantState;
            return occupantState;
        }
        return null;
    }

    private WorldMapFootprintState GetFootprintState(string entityId)
    {
        GdInterop.TryGet(_footprints, entityId, out var footprintValue);
        if (footprintValue.TryAsObject(out WorldMapFootprintState footprintObject))
            return footprintObject;
        if (footprintValue.TryAsDictionary(out GDictionary footprintDict))
        {
            if (
                !GdInterop.HasVector2I(footprintDict, "origin")
                || !GdInterop.HasVector2I(footprintDict, "size")
            )
            {
                _footprints.Remove(entityId);
                return null;
            }
            WorldMapFootprintState footprintState = WorldMapFootprintState.create(
                GdInterop.GetVector2I(footprintDict, "origin"),
                GdInterop.GetVector2I(footprintDict, "size")
            );
            if (footprintState.is_empty())
            {
                _footprints.Remove(entityId);
                return null;
            }
            _footprints[entityId] = footprintState;
            return footprintState;
        }
        return null;
    }

    private void WriteFootprintCells(string entityId, Vector2I origin, Vector2I size)
    {
        _footprints[entityId] = WorldMapFootprintState.create(origin, size);

        for (int y = 0; y < size.Y; y++)
        {
            for (int x = 0; x < size.X; x++)
            {
                Vector2I coord = origin + new Vector2I(x, y);
                _occupied_cells[coord] = WorldMapOccupantState.create(entityId, entityId);
            }
        }
    }
}
