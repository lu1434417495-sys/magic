using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

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

    private void EnsureRuntimeEdgeFaces(BattleState state)
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
        state.runtime_edge_faces = BuildEdgeFacesForCells(
            state.ProjectCells(),
            state.map_size,
            state.cell_columns
        );
        state.runtime_edges_dirty = false;
    }

    internal void MarkRuntimeEdgeFacesDirty(BattleState state)
    {
        if (state != null)
        {
            state.MarkRuntimeEdgesDirty();
        }
    }

    internal GDictionary BuildEdgeFacesForCells(
        GDictionary cells,
        Vector2I map_size,
        GDictionary cell_columns
    )
    {
        var edgeFaces = new GDictionary();
        GDictionary resolvedColumns =
            cell_columns != null && cell_columns.Count > 0
                ? cell_columns
                : BattleCellState.BuildColumnsFromSurfaceCells(cells ?? new GDictionary());
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

    internal Godot.Collections.Array<BattleEdgeFaceState> GetAllEdgeFaces(BattleState state)
    {
        var results = new Godot.Collections.Array<BattleEdgeFaceState>();
        if (state == null)
        {
            return results;
        }
        EnsureRuntimeEdgeFaces(state);
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

    public BattleEdgeFaceState GetEdgeFace(
        BattleState state,
        Vector2I from_coord,
        Vector2I to_coord
    )
    {
        if (state == null)
        {
            return null;
        }
        EnsureRuntimeEdgeFaces(state);
        return GetEdgeFaceFromCache(state.runtime_edge_faces, from_coord, to_coord);
    }

    private BattleEdgeFaceState GetEdgeFaceFromCache(
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

    internal bool IsTraversableBetween(BattleState state, Vector2I from_coord, Vector2I to_coord)
    {
        return IsEdgeFaceTraversable(GetEdgeFace(state, from_coord, to_coord));
    }

    internal bool IsTraversableInCache(
        GDictionary edge_faces,
        Vector2I from_coord,
        Vector2I to_coord
    )
    {
        return IsEdgeFaceTraversable(GetEdgeFaceFromCache(edge_faces, from_coord, to_coord));
    }

    private bool IsEdgeFaceTraversable(BattleEdgeFaceState edge_face)
    {
        if (edge_face == null)
        {
            return false;
        }
        if (edge_face.BlocksMove())
        {
            return false;
        }
        return edge_face.height_difference <= 1;
    }

    internal bool BlocksOccupancyBetween(BattleState state, Vector2I from_coord, Vector2I to_coord)
    {
        return BlocksOccupancyForEdgeFace(GetEdgeFace(state, from_coord, to_coord));
    }

    internal bool BlocksOccupancyInCache(
        GDictionary edge_faces,
        Vector2I from_coord,
        Vector2I to_coord
    )
    {
        return BlocksOccupancyForEdgeFace(
            GetEdgeFaceFromCache(edge_faces, from_coord, to_coord)
        );
    }

    private bool BlocksOccupancyForEdgeFace(BattleEdgeFaceState edge_face)
    {
        if (edge_face == null)
        {
            return true;
        }
        if (edge_face.BlocksOccupancy())
        {
            return true;
        }
        return edge_face.height_difference > 1;
    }

    internal bool HasFeatureBetween(
        BattleState state,
        Vector2I from_coord,
        Vector2I to_coord,
        BattleEdgeFeatureKind featureKind
    )
    {
        BattleEdgeFaceState edgeFace = GetEdgeFace(state, from_coord, to_coord);
        return edgeFace != null && edgeFace.FeatureKind == featureKind;
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
            ReadValue(cellColumns, originCoord),
            originCell
        );
        int toHeight = GetColumnTopHeight(
            ReadValue(cellColumns, neighborCoord),
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
        if (edgeFace == null || featureState == null || featureState.IsEmpty())
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

    private static void EnsureCellColumns(BattleState state)
    {
        if (state == null)
        {
            return;
        }
        if (state.cell_columns.Count == 0 && state.CellCount > 0)
        {
            state.RebuildCellColumns();
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

    private static object ReadValue(GDictionary source, Vector2I key, object fallback = null)
    {
        if (source != null && source.ContainsKey(key))
        {
            return source[key];
        }
        return fallback;
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
