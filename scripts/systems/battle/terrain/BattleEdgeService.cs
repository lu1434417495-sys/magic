using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleEdgeService : RefCounted
{
    private static readonly Vector2I DirectionEast = Vector2I.Right;
    private static readonly Vector2I DirectionSouth = Vector2I.Down;
    private const int DirectionIndexEast = 0;
    private const int DirectionIndexSouth = 1;
    private const int BoundaryRenderHeight = 0;

    private readonly struct EdgeLookup
    {
        public readonly bool Valid;
        public readonly Vector3I Key;

        public EdgeLookup(bool valid, Vector3I key)
        {
            Valid = valid;
            Key = key;
        }
    }

    public void ensure_runtime_edge_faces(GodotObject state)
    {
        if (state is BattleState battleState)
        {
            ensure_runtime_edge_faces(battleState);
            return;
        }
        if (state == null)
        {
            return;
        }
        EnsureCellColumns(state);
        GDictionary runtimeEdgeFaces = GdInterop.GetDictionary(state, "runtime_edge_faces");
        if (!GdInterop.GetBool(state, "runtime_edges_dirty", true) && runtimeEdgeFaces.Count > 0)
        {
            return;
        }
        state.Set(
            "runtime_edge_faces",
            build_edge_faces_for_cells(
                GdInterop.GetDictionary(state, "cells"),
                GdInterop.GetVector2I(state, "map_size"),
                GdInterop.GetDictionary(state, "cell_columns")
            )
        );
        state.Set("runtime_edges_dirty", false);
    }

    public void ensure_runtime_edge_faces(BattleState state)
    {
        if (state == null)
        {
            return;
        }
        EnsureCellColumns(state);
        if (!state.runtime_edges_dirty && state.runtime_edge_faces.Count > 0)
        {
            return;
        }
        state.runtime_edge_faces = build_edge_faces_for_cells(
            state.cells,
            state.map_size,
            state.cell_columns
        );
        state.runtime_edges_dirty = false;
    }

    public void rebuild_runtime_edge_faces(GodotObject state)
    {
        if (state is BattleState battleState)
        {
            rebuild_runtime_edge_faces(battleState);
            return;
        }
        if (state == null)
        {
            return;
        }
        EnsureCellColumns(state);
        state.Set(
            "runtime_edge_faces",
            build_edge_faces_for_cells(
                GdInterop.GetDictionary(state, "cells"),
                GdInterop.GetVector2I(state, "map_size"),
                GdInterop.GetDictionary(state, "cell_columns")
            )
        );
        state.Set("runtime_edges_dirty", false);
    }

    public void rebuild_runtime_edge_faces(BattleState state)
    {
        if (state == null)
        {
            return;
        }
        EnsureCellColumns(state);
        state.runtime_edge_faces = build_edge_faces_for_cells(
            state.cells,
            state.map_size,
            state.cell_columns
        );
        state.runtime_edges_dirty = false;
    }

    public void mark_runtime_edge_faces_dirty(GodotObject state)
    {
        if (state is BattleState battleState)
        {
            mark_runtime_edge_faces_dirty(battleState);
            return;
        }
        state?.Set("runtime_edges_dirty", true);
    }

    public void mark_runtime_edge_faces_dirty(BattleState state)
    {
        if (state != null)
        {
            state.runtime_edges_dirty = true;
        }
    }

    public void clear_runtime_edge_faces(GodotObject state)
    {
        if (state is BattleState battleState)
        {
            clear_runtime_edge_faces(battleState);
            return;
        }
        if (state == null)
        {
            return;
        }
        GdInterop.GetDictionary(state, "runtime_edge_faces").Clear();
        state.Set("runtime_edges_dirty", true);
    }

    public void clear_runtime_edge_faces(BattleState state)
    {
        if (state == null)
        {
            return;
        }
        state.runtime_edge_faces.Clear();
        state.runtime_edges_dirty = true;
    }

    public GDictionary build_edge_faces_for_cells(
        GDictionary cells,
        Vector2I map_size,
        GDictionary cell_columns
    )
    {
        var edgeFaces = new GDictionary();
        GDictionary resolvedColumns =
            cell_columns != null && cell_columns.Count > 0
                ? cell_columns
                : BattleCellState.build_columns_from_surface_cells(cells ?? new GDictionary());
        int maxY = Math.Max(map_size.Y, 0);
        int maxX = Math.Max(map_size.X, 0);
        for (int y = 0; y < maxY; y++)
        {
            for (int x = 0; x < maxX; x++)
            {
                var originCoord = new Vector2I(x, y);
                BattleCellState originCell = GetCell(cells, originCoord);
                if (originCell == null)
                {
                    continue;
                }
                edgeFaces[BuildEdgeKey(originCoord, DirectionIndexEast)] = BuildEdgeFace(
                    cells,
                    resolvedColumns,
                    originCoord,
                    originCell,
                    DirectionEast
                );
                edgeFaces[BuildEdgeKey(originCoord, DirectionIndexSouth)] = BuildEdgeFace(
                    cells,
                    resolvedColumns,
                    originCoord,
                    originCell,
                    DirectionSouth
                );
            }
        }
        return edgeFaces;
    }

    public Godot.Collections.Array<BattleEdgeFaceState> get_all_edge_faces(GodotObject state)
    {
        if (state is BattleState battleState)
        {
            return get_all_edge_faces(battleState);
        }
        var results = new Godot.Collections.Array<BattleEdgeFaceState>();
        if (state == null)
        {
            return results;
        }
        ensure_runtime_edge_faces(state);
        foreach (
            Variant edgeFaceValue in GdInterop.GetDictionary(state, "runtime_edge_faces").Values
        )
        {
            if (
                edgeFaceValue.VariantType == Variant.Type.Object
                && edgeFaceValue.AsGodotObject() is BattleEdgeFaceState edgeFace
            )
            {
                results.Add(edgeFace);
            }
        }
        return results;
    }

    public Godot.Collections.Array<BattleEdgeFaceState> get_all_edge_faces(BattleState state)
    {
        var results = new Godot.Collections.Array<BattleEdgeFaceState>();
        if (state == null)
        {
            return results;
        }
        ensure_runtime_edge_faces(state);
        foreach (var edgeFaceValue in state.runtime_edge_faces.Values)
        {
            if (
                edgeFaceValue.VariantType == Variant.Type.Object
                && edgeFaceValue.AsGodotObject() is BattleEdgeFaceState edgeFace
            )
            {
                results.Add(edgeFace);
            }
        }
        return results;
    }

    public BattleEdgeFaceState get_edge_face(
        GodotObject state,
        Vector2I from_coord,
        Vector2I to_coord
    )
    {
        if (state is BattleState battleState)
        {
            return get_edge_face(battleState, from_coord, to_coord);
        }
        if (state == null)
        {
            return null;
        }
        ensure_runtime_edge_faces(state);
        return get_edge_face_from_cache(
            GdInterop.GetDictionary(state, "runtime_edge_faces"),
            from_coord,
            to_coord
        );
    }

    public BattleEdgeFaceState get_edge_face(
        BattleState state,
        Vector2I from_coord,
        Vector2I to_coord
    )
    {
        if (state == null)
        {
            return null;
        }
        ensure_runtime_edge_faces(state);
        return get_edge_face_from_cache(state.runtime_edge_faces, from_coord, to_coord);
    }

    public BattleEdgeFaceState get_edge_face_by_origin(
        GodotObject state,
        Vector2I origin_coord,
        Vector2I direction
    )
    {
        if (state is BattleState battleState)
        {
            return get_edge_face_by_origin(battleState, origin_coord, direction);
        }
        if (state == null)
        {
            return null;
        }
        ensure_runtime_edge_faces(state);
        return GetEdgeFaceFromDictionary(
            GdInterop.GetDictionary(state, "runtime_edge_faces"),
            BuildEdgeKey(origin_coord, GetDirectionIndex(direction))
        );
    }

    public BattleEdgeFaceState get_edge_face_by_origin(
        BattleState state,
        Vector2I origin_coord,
        Vector2I direction
    )
    {
        if (state == null)
        {
            return null;
        }
        ensure_runtime_edge_faces(state);
        return GetEdgeFaceFromDictionary(
            state.runtime_edge_faces,
            BuildEdgeKey(origin_coord, GetDirectionIndex(direction))
        );
    }

    public BattleEdgeFaceState get_edge_face_from_cache(
        GDictionary edge_faces,
        Vector2I from_coord,
        Vector2I to_coord
    )
    {
        EdgeLookup lookup = ResolveLookupKey(from_coord, to_coord);
        if (!lookup.Valid)
        {
            return null;
        }
        return GetEdgeFaceFromDictionary(edge_faces, lookup.Key);
    }

    public bool is_traversable_between(GodotObject state, Vector2I from_coord, Vector2I to_coord)
    {
        return is_edge_face_traversable(get_edge_face(state, from_coord, to_coord));
    }

    public bool is_traversable_between(BattleState state, Vector2I from_coord, Vector2I to_coord)
    {
        return is_edge_face_traversable(get_edge_face(state, from_coord, to_coord));
    }

    public bool is_traversable_in_cache(
        GDictionary edge_faces,
        Vector2I from_coord,
        Vector2I to_coord
    )
    {
        return is_edge_face_traversable(get_edge_face_from_cache(edge_faces, from_coord, to_coord));
    }

    public bool is_edge_face_traversable(BattleEdgeFaceState edge_face)
    {
        if (edge_face == null)
        {
            return false;
        }
        if (edge_face.blocks_move())
        {
            return false;
        }
        return edge_face.height_difference <= 1;
    }

    public bool blocks_occupancy_between(GodotObject state, Vector2I from_coord, Vector2I to_coord)
    {
        return blocks_occupancy_for_edge_face(get_edge_face(state, from_coord, to_coord));
    }

    public bool blocks_occupancy_between(BattleState state, Vector2I from_coord, Vector2I to_coord)
    {
        return blocks_occupancy_for_edge_face(get_edge_face(state, from_coord, to_coord));
    }

    public bool blocks_occupancy_in_cache(
        GDictionary edge_faces,
        Vector2I from_coord,
        Vector2I to_coord
    )
    {
        return blocks_occupancy_for_edge_face(
            get_edge_face_from_cache(edge_faces, from_coord, to_coord)
        );
    }

    public bool blocks_occupancy_for_edge_face(BattleEdgeFaceState edge_face)
    {
        if (edge_face == null)
        {
            return true;
        }
        if (edge_face.blocks_occupancy())
        {
            return true;
        }
        return edge_face.height_difference > 1;
    }

    public bool has_feature_between(
        GodotObject state,
        Vector2I from_coord,
        Vector2I to_coord,
        StringName feature_kind
    )
    {
        BattleEdgeFaceState edgeFace = get_edge_face(state, from_coord, to_coord);
        return edgeFace != null && edgeFace.feature_kind == feature_kind;
    }

    public bool has_feature_between(
        BattleState state,
        Vector2I from_coord,
        Vector2I to_coord,
        StringName feature_kind
    )
    {
        BattleEdgeFaceState edgeFace = get_edge_face(state, from_coord, to_coord);
        return edgeFace != null && edgeFace.feature_kind == feature_kind;
    }

    private static BattleEdgeFaceState BuildEdgeFace(
        GDictionary cells,
        GDictionary cellColumns,
        Vector2I originCoord,
        BattleCellState originCell,
        Vector2I direction
    )
    {
        var edgeFace = new BattleEdgeFaceState();
        Vector2I neighborCoord = originCoord + direction;
        BattleCellState neighborCell = GetCell(cells, neighborCoord);
        int fromHeight = GetColumnTopHeight(
            cellColumns.GetValueOrDefault(originCoord),
            originCell
        );
        int toHeight = GetColumnTopHeight(
            cellColumns.GetValueOrDefault(neighborCoord),
            neighborCell
        );
        edgeFace.origin_coord = originCoord;
        edgeFace.neighbor_coord = neighborCoord;
        edgeFace.direction = direction;
        edgeFace.from_height = fromHeight;
        edgeFace.to_height = toHeight;
        edgeFace.height_difference = Math.Abs(fromHeight - toHeight);
        edgeFace.drop_face_layer_heights = BuildExposedLayerHeights(fromHeight, toHeight);
        edgeFace.drop_layers = edgeFace.drop_face_layer_heights.Count;
        if (direction == DirectionEast)
        {
            ApplyAuthoredFeature(edgeFace, originCell.edge_feature_east);
        }
        else if (direction == DirectionSouth)
        {
            ApplyAuthoredFeature(edgeFace, originCell.edge_feature_south);
        }
        return edgeFace;
    }

    private static void ApplyAuthoredFeature(
        BattleEdgeFaceState edgeFace,
        BattleEdgeFeatureState featureState
    )
    {
        if (edgeFace == null || featureState == null || featureState.is_empty())
        {
            return;
        }
        edgeFace.feature_kind = featureState.feature_kind;
        edgeFace.feature_render_kind = featureState.render_kind;
        edgeFace.feature_layers = Math.Max(featureState.render_layers, 0);
        edgeFace.feature_blocks_move = featureState.blocks_move;
        edgeFace.feature_blocks_occupancy = featureState.blocks_occupancy;
        edgeFace.feature_blocks_los = featureState.blocks_los;
        edgeFace.feature_interaction_kind = featureState.interaction_kind;
        edgeFace.feature_state_tag = featureState.state_tag;
    }

    private static EdgeLookup ResolveLookupKey(Vector2I fromCoord, Vector2I toCoord)
    {
        Vector2I delta = toCoord - fromCoord;
        if (delta == Vector2I.Right)
        {
            return new EdgeLookup(true, BuildEdgeKey(fromCoord, DirectionIndexEast));
        }
        if (delta == Vector2I.Left)
        {
            return new EdgeLookup(true, BuildEdgeKey(toCoord, DirectionIndexEast));
        }
        if (delta == Vector2I.Down)
        {
            return new EdgeLookup(true, BuildEdgeKey(fromCoord, DirectionIndexSouth));
        }
        if (delta == Vector2I.Up)
        {
            return new EdgeLookup(true, BuildEdgeKey(toCoord, DirectionIndexSouth));
        }
        return new EdgeLookup(false, Vector3I.Zero);
    }

    private static Vector3I BuildEdgeKey(Vector2I originCoord, int directionIndex)
    {
        return new Vector3I(originCoord.X, originCoord.Y, directionIndex);
    }

    private static int GetDirectionIndex(Vector2I direction)
    {
        if (direction == DirectionSouth)
        {
            return DirectionIndexSouth;
        }
        return DirectionIndexEast;
    }

    private static void EnsureCellColumns(GodotObject state)
    {
        if (state == null)
        {
            return;
        }
        GDictionary cellColumns = GdInterop.GetDictionary(state, "cell_columns");
        GDictionary cells = GdInterop.GetDictionary(state, "cells");
        if (cellColumns.Count == 0 && cells.Count > 0)
        {
            state.Set("cell_columns", BattleCellState.build_columns_from_surface_cells(cells));
        }
    }

    private static void EnsureCellColumns(BattleState state)
    {
        if (state == null)
        {
            return;
        }
        if (state.cell_columns.Count == 0 && state.cells.Count > 0)
        {
            state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        }
    }

    private static int GetColumnTopHeight(
        object rawColumnValue,
        BattleCellState fallbackSurfaceCell = null
    )
    {
        if (
            rawColumnValue is Variant columnValue
            && columnValue.VariantType == Variant.Type.Array
        )
        {
            GArray column = columnValue.AsGodotArray();
            return GetColumnTopHeightFromArray(column, fallbackSurfaceCell);
        }
        if (rawColumnValue is GArray rawColumn)
        {
            return GetColumnTopHeightFromArray(rawColumn, fallbackSurfaceCell);
        }
        return fallbackSurfaceCell != null
            ? fallbackSurfaceCell.current_height
            : BoundaryRenderHeight;
    }

    private static int GetColumnTopHeightFromArray(
        GArray column,
        BattleCellState fallbackSurfaceCell
    )
    {
        if (column != null)
        {
            for (int index = column.Count - 1; index >= 0; index--)
            {
                var cellValue = column[index];
                if (
                    cellValue.VariantType == Variant.Type.Object
                    && cellValue.AsGodotObject() is BattleCellState layerCell
                )
                {
                    return layerCell.stack_layer;
                }
            }
        }
        return fallbackSurfaceCell != null
            ? fallbackSurfaceCell.current_height
            : BoundaryRenderHeight;
    }

    private static Godot.Collections.Array<int> BuildExposedLayerHeights(
        int fromHeight,
        int toHeight
    )
    {
        var exposedLayers = new Godot.Collections.Array<int>();
        if (fromHeight <= toHeight)
        {
            return exposedLayers;
        }
        int lowestExposedHeight = Math.Max(toHeight + 1, 1);
        for (int layerHeight = fromHeight; layerHeight >= lowestExposedHeight; layerHeight--)
        {
            exposedLayers.Add(layerHeight);
        }
        return exposedLayers;
    }

    private static BattleCellState GetCell(GDictionary cells, Vector2I coord)
    {
        if (cells == null || !cells.ContainsKey(coord))
        {
            return null;
        }
        var value = cells[coord];
        return value.VariantType == Variant.Type.Object
            ? value.AsGodotObject() as BattleCellState
            : null;
    }

    private static BattleEdgeFaceState GetEdgeFaceFromDictionary(
        GDictionary edgeFaces,
        Vector3I key
    )
    {
        if (edgeFaces == null || !edgeFaces.ContainsKey(key))
        {
            return null;
        }
        var value = edgeFaces[key];
        return value.VariantType == Variant.Type.Object
            ? value.AsGodotObject() as BattleEdgeFaceState
            : null;
    }

}
