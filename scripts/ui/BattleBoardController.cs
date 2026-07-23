using System;
using System.Collections.Generic;
using Godot;

public sealed class BattleBoardController : IDisposable
{
    public sealed class TileSetCacheEntry
    {
        public TileSet TileSet { get; init; }
        public Dictionary<StringName, List<int>> SourceIds { get; init; } = new();
    }

    private const int MAX_HEIGHT_LAYERS = 9;
    private const int TOP_LAYER_Z_BASE = 0;
    private const int LAYER_Z_STRIDE = 10;
    private const int EDGE_DROP_EAST_LAYER_Z_OFFSET = -4;
    private const int EDGE_DROP_SOUTH_LAYER_Z_OFFSET = -3;
    private const int WALL_EAST_LAYER_Z_OFFSET = -2;
    private const int WALL_SOUTH_LAYER_Z_OFFSET = -1;
    private const int OVERLAY_LAYER_Z_OFFSET = 6;
    private const int MARKER_LAYER_Z_OFFSET = OVERLAY_LAYER_Z_OFFSET + 1;
    private const int DYNAMIC_LAYER_Z_OFFSET = MARKER_LAYER_Z_OFFSET + 1;

    // 单位身上的信息层(血条、名字)用"绝对 z"(ZAsRelative=false)钉到固定高层。
    // token 在 y_sort 的 UnitLayer 里,相对 z 会被 y-sort 扁平化吃掉(Control 子节点尤甚),
    // 导致血条被自己/邻近单位的贴图盖住;绝对 z 直接跳到所有贴图之上、仅低于目标高亮(1300)。
    private const int UNIT_OVERLAY_ABSOLUTE_Z = 1200;
    private const int PROP_LAYER_Z = 0;
    private const int UNIT_LAYER_Z = 0;
    private const int TARGET_HIGHLIGHT_LAYER_Z = 1300;
    private static readonly Vector2 UNIT_GLYPH_LABEL_SIZE = new(28.0f, 28.0f);
    private const float UNIT_SPRITE_TILE_WIDTH_RATIO = 0.95f;
    private const float UNIT_SPRITE_GROUND_ANCHOR_RATIO = 0.85f;
    private static readonly Vector2 UNIT_SPRITE_SHADOW_HALF_SIZE = new(20.0f, 6.0f);
    private static readonly Color UNIT_SPRITE_SHADOW_COLOR = new(0.0f, 0.0f, 0.0f, 0.45f);
    private static readonly Vector2 UNIT_SPRITE_HIGHLIGHT_HALF_SIZE = new(22.0f, 8.0f);
    private static readonly Color UNIT_SPRITE_HIGHLIGHT_COLOR = new(1.0f, 0.94f, 0.76f, 0.92f);
    private const int UNIT_SPRITE_ELLIPSE_SEGMENT_COUNT = 28;
    private static readonly Vector2 UNIT_HEALTH_BAR_SIZE = new(56.0f, 14.0f);
    private const float UNIT_HEALTH_BAR_Y_OFFSET = -50.0f;

    // 贴图单位的血条要落在贴图实际顶部之上(贴图很高,固定 -50 会压在身体中部)。
    private const float UNIT_SPRITE_OVERLAY_GAP = 4.0f;
    private static readonly Color UNIT_HEALTH_BAR_BG_COLOR = new(0.14f, 0.09f, 0.06f, 0.92f);
    private static readonly Color UNIT_HEALTH_BAR_BORDER_COLOR = new(0.95f, 0.91f, 0.8f, 0.9f);
    private static readonly Color UNIT_HEALTH_BAR_HIGH_COLOR = new(0.3f, 0.86f, 0.42f, 0.96f);
    private static readonly Color UNIT_HEALTH_BAR_MID_COLOR = new(0.9f, 0.76f, 0.24f, 0.96f);
    private static readonly Color UNIT_HEALTH_BAR_LOW_COLOR = new(0.9f, 0.28f, 0.22f, 0.96f);
    private static readonly StringName HP_MAX_ATTRIBUTE_ID = "hp_max";
    private static readonly Color ACTIVE_SELECTED_MARKER_COLOR = new(0.0f, 0.0f, 1.0f, 1.0f);
    private static readonly Color MOVE_REACHABLE_MARKER_COLOR_DARK = new(0.14f, 0.37f, 0.5f, 1.0f);
    private static readonly Color MOVE_REACHABLE_MARKER_COLOR_LIGHT = new(
        0.46f,
        0.72f,
        0.84f,
        1.0f
    );
    private static readonly Color OBJECTIVE_EXIT_MARKER_COLOR = new(
        0.18f,
        0.88f,
        0.64f,
        1.0f
    );
    private static readonly Color VALID_TARGET_HIGHLIGHT_COLOR = new(0.92f, 0.12f, 0.08f, 0.42f);
    private static readonly Color LOCKED_TARGET_HIGHLIGHT_COLOR = new(0.96f, 0.82f, 0.28f, 0.54f);
    private static readonly Color CONFIRM_READY_TARGET_HIGHLIGHT_COLOR = new(
        0.28f,
        0.8f,
        0.5f,
        0.5f
    );
    private static readonly Color CONFIRM_READY_FOCUS_HALO_COLOR = new(0.98f, 0.9f, 0.34f, 0.35f);
    private static readonly Vector2 HIT_BADGE_SIZE = new(82.0f, 26.0f);
    private static readonly Vector2 HIT_BADGE_OFFSET = new(-41.0f, -76.0f);
    private static readonly Color HIT_BADGE_BG_COLOR = new(0.08f, 0.04f, 0.02f, 0.9f);
    private static readonly Color HIT_BADGE_EDGE_COLOR = new(1.0f, 0.84f, 0.42f, 0.95f);
    private static readonly Color HIT_BADGE_TEXT_COLOR = new(1.0f, 0.95f, 0.82f, 1.0f);

    private static readonly StringName TERRAIN_LAND = "land";
    private static readonly StringName TERRAIN_FOREST = "forest";
    private static readonly StringName TERRAIN_WATER = "water";
    private static readonly StringName TERRAIN_SHALLOW_WATER = "shallow_water";
    private static readonly StringName TERRAIN_FLOWING_WATER = "flowing_water";
    private static readonly StringName TERRAIN_DEEP_WATER = "deep_water";
    private static readonly StringName TERRAIN_MUD = "mud";
    private static readonly StringName TERRAIN_SPIKE = "spike";

    private static readonly StringName SOURCE_LAND = "land";
    private static readonly StringName SOURCE_FOREST = "forest_ground";
    private static readonly StringName SOURCE_FOREST_TREE = "forest_tree";
    private static readonly StringName SOURCE_WATER = "water";
    private static readonly StringName SOURCE_MUD = "mud";
    private static readonly StringName SOURCE_EDGE_DROP_EAST = "edge_drop_east";
    private static readonly StringName SOURCE_EDGE_DROP_SOUTH = "edge_drop_south";
    private static readonly StringName SOURCE_WALL_EAST = "wall_east";
    private static readonly StringName SOURCE_WALL_SOUTH = "wall_south";
    private static readonly StringName SOURCE_SCRUB = "scrub";
    private static readonly StringName SOURCE_RUBBLE = "rubble";
    private static readonly StringName SOURCE_SELECTED = "selected";
    private static readonly StringName SOURCE_ACTIVE_SELECTED = "active_selected";
    private static readonly StringName SOURCE_MOVE_REACHABLE = "move_reachable";
    private static readonly StringName SOURCE_OBJECTIVE_EXIT = "objective_exit";
    private static readonly StringName SOURCE_PREVIEW = "preview";
    private static readonly Vector2I INVALID_OPTION_COORD = new(-999999, -999999);
    private static readonly StringName PROP_SPIKE_BARRICADE = "spike_barricade";
    private const string BattleBoardPropScenePath =
        "res://scenes/common/battle_board_prop.tscn";

    public TileMapLayer _input_layer;
    public readonly List<TileMapLayer> _top_layers = new();
    public readonly List<TileMapLayer> _edge_drop_east_layers = new();
    public readonly List<TileMapLayer> _edge_drop_south_layers = new();
    public readonly List<TileMapLayer> _wall_east_layers = new();
    public readonly List<TileMapLayer> _wall_south_layers = new();
    public readonly List<TileMapLayer> _overlay_layers = new();
    public readonly List<TileMapLayer> _marker_layers = new();
    public Node2D _prop_layer;
    public Node2D _unit_layer;
    public Node2D _target_highlight_layer;
    public TileSet _tile_set;
    public readonly Dictionary<StringName, List<int>> _source_ids = new();
    public StringName _tile_profile_id = "";
    public BattleBoardRenderProfile _render_profile;
    public readonly Dictionary<string, Texture2D> _texture_cache = new();
    public readonly Dictionary<StringName, TileSetCacheEntry> _tileset_cache = new();
    private StyleBoxFlat _unitHealthBarStyle;
    private StyleBoxFlat _hitBadgeStyle;
    private NativeLeaseScope _renderLease;
    private bool _disposed;
    private readonly Dictionary<StringName, Node2D> _unitNodesById = new();
    public BattleEdgeService _edge_service = new();
    public BattleState _battle_state;
    public Vector2I _selected_coord = new(-1, -1);
    public readonly List<Vector2I> _preview_target_coords = new();
    public readonly List<Vector2I> _valid_target_coords = new();
    public StringName _target_selection_mode = "single_unit";
    public int _target_min_count = 1;
    public int _target_max_count = 1;
    public readonly Dictionary<Vector2I, string> _target_hit_badges = new();

    public void BindLayers(
        TileMapLayer input_layer,
        IEnumerable<TileMapLayer> top_layers,
        IEnumerable<TileMapLayer> edge_drop_east_layers,
        IEnumerable<TileMapLayer> edge_drop_south_layers,
        IEnumerable<TileMapLayer> wall_east_layers,
        IEnumerable<TileMapLayer> wall_south_layers,
        IEnumerable<TileMapLayer> overlay_layers,
        IEnumerable<TileMapLayer> marker_layers,
        Node2D prop_layer,
        Node2D unit_layer,
        Node2D target_highlight_layer
    )
    {
        ThrowIfDisposed();
        if (_renderLease != null || _input_layer != null || _top_layers.Count > 0)
            ClearCore();
        _input_layer = input_layer;
        ReplaceLayers(_top_layers, top_layers);
        ReplaceLayers(_edge_drop_east_layers, edge_drop_east_layers);
        ReplaceLayers(_edge_drop_south_layers, edge_drop_south_layers);
        ReplaceLayers(_wall_east_layers, wall_east_layers);
        ReplaceLayers(_wall_south_layers, wall_south_layers);
        ReplaceLayers(_overlay_layers, overlay_layers);
        ReplaceLayers(_marker_layers, marker_layers);
        _prop_layer = prop_layer;
        _unit_layer = unit_layer;
        _target_highlight_layer = target_highlight_layer;
        try
        {
            _ensure_tileset(BattleBoardRenderProfile.TERRAIN_PROFILE_DEFAULT());
            _apply_tileset_to_layers();
            _apply_layer_offsets();
            _apply_layer_draw_order();
        }
        catch (Exception constructionFailure)
        {
            try
            {
                ClearCore();
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    "BattleBoardController bind failed and cleanup reported failures.",
                    constructionFailure,
                    cleanupFailure
                );
            }
            throw;
        }
    }

    public void Configure(
        BattleState battle_state,
        Vector2I selected_coord,
        IEnumerable<Vector2I> preview_target_coords,
        StringName target_selection_mode,
        int min_target_count,
        int max_target_count,
        IReadOnlyDictionary<Vector2I, string> target_hit_badges
    )
    {
        ThrowIfDisposed();
        _battle_state = battle_state;
        _selected_coord = selected_coord;
        ReplaceCoords(_preview_target_coords, preview_target_coords);
        _target_selection_mode =
            target_selection_mode == "" ? new StringName("single_unit") : target_selection_mode;
        _target_min_count = Mathf.Max(min_target_count, 1);
        _target_max_count = Mathf.Max(max_target_count, _target_min_count);
        _set_target_hit_badges(target_hit_badges);
        _refresh_tileset_profile();
        _redraw();
    }

    public void UpdateMarkers(
        Vector2I selected_coord,
        IEnumerable<Vector2I> preview_target_coords,
        IEnumerable<Vector2I> valid_target_coords,
        StringName target_selection_mode,
        int min_target_count,
        int max_target_count,
        IReadOnlyDictionary<Vector2I, string> target_hit_badges
    )
    {
        ThrowIfDisposed();
        _selected_coord = selected_coord;
        ReplaceCoords(_preview_target_coords, preview_target_coords);
        ReplaceCoords(_valid_target_coords, valid_target_coords);
        _target_selection_mode =
            target_selection_mode == "" ? new StringName("single_unit") : target_selection_mode;
        _target_min_count = Mathf.Max(min_target_count, 1);
        _target_max_count = Mathf.Max(max_target_count, _target_min_count);
        _set_target_hit_badges(target_hit_badges);
        _draw_marker_layer();
        _draw_target_highlights();
    }

    public void RefreshUnits(
        BattleState battle_state,
        IEnumerable<StringName> changed_unit_ids
    )
    {
        ThrowIfDisposed();
        if (_unit_layer == null || battle_state == null || changed_unit_ids == null)
            return;

        _battle_state = battle_state;
        var requestedUnitIds = new HashSet<StringName>();
        foreach (StringName unitId in changed_unit_ids)
        {
            if (unitId != "")
                requestedUnitIds.Add(unitId);
        }
        if (requestedUnitIds.Count == 0)
        {
            foreach (StringName unitId in _unitNodesById.Keys)
                requestedUnitIds.Add(unitId);
            foreach (BattleUnitState unit in battle_state.Units())
            {
                if (unit?.unit_id != "")
                    requestedUnitIds.Add(unit.unit_id);
            }
        }

        foreach (StringName unitId in requestedUnitIds)
        {
            if (_unitNodesById.Remove(unitId, out Node2D existingNode))
            {
                if (GodotObject.IsInstanceValid(existingNode))
                {
                    if (existingNode.GetParent() == _unit_layer)
                        _unit_layer.RemoveChild(existingNode);
                    existingNode.Free();
                }
            }

            BattleUnitState unitState = GetUnit(_battle_state, unitId);
            if (unitState == null || !unitState.is_alive)
                continue;
            Node2D unitNode = _create_unit_token(unitState);
            if (unitNode == null)
                continue;
            _unit_layer.AddChild(unitNode);
            _unitNodesById[unitId] = unitNode;
        }
    }

    public void Clear()
    {
        ThrowIfDisposed();
        ClearCore();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ClearCore();
    }

    private void ClearCore()
    {
        NativeLeaseScope renderLease = _renderLease;
        _renderLease = null;
        var failures = new List<Exception>();
        TryCleanup(() => _clear_tile_layers(failures), failures);
        TryCleanup(() => _detach_tilesets_from_layers(failures), failures);
        TryCleanup(() => _clear_dynamic_nodes(failures), failures);
        TryCleanup(() => _detach_sources_from_cached_tilesets(failures), failures);
        TryCleanup(ClearBorrowedFields, failures);
        if (renderLease != null)
            TryCleanup(renderLease.Dispose, failures);
        if (failures.Count > 0)
        {
            throw new AggregateException(
                "BattleBoardController render generation cleanup failed.",
                failures
            );
        }
    }

    private static void TryCleanup(Action cleanup, List<Exception> failures)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private void ClearBorrowedFields()
    {
        _battle_state = null;
        _tile_set = null;
        _render_profile = null;
        _tile_profile_id = "";
        _selected_coord = new Vector2I(-1, -1);
        _preview_target_coords.Clear();
        _valid_target_coords.Clear();
        _target_hit_badges.Clear();
        _source_ids.Clear();
        _unitHealthBarStyle = null;
        _hitBadgeStyle = null;
        _texture_cache.Clear();
        _tileset_cache.Clear();
        _unitNodesById.Clear();
        _input_layer = null;
        _top_layers.Clear();
        _edge_drop_east_layers.Clear();
        _edge_drop_south_layers.Clear();
        _wall_east_layers.Clear();
        _wall_south_layers.Clear();
        _overlay_layers.Clear();
        _marker_layers.Clear();
        _prop_layer = null;
        _unit_layer = null;
        _target_highlight_layer = null;
    }

    private void _detach_sources_from_cached_tilesets(List<Exception> failures)
    {
        var visited = new HashSet<TileSet>(GodotWrapperReferenceComparer.Instance);
        foreach (TileSetCacheEntry entry in _tileset_cache.Values)
        {
            TileSet tileSet = entry?.TileSet;
            if (tileSet == null || !visited.Add(tileSet))
                continue;
            DetachTileSetSources(tileSet, failures);
        }
        if (_tile_set != null && visited.Add(_tile_set))
            DetachTileSetSources(_tile_set, failures);
    }

    private static void DetachTileSetSources(TileSet tileSet, List<Exception> failures)
    {
        var sourceIds = new List<int>();
        int sourceCount = 0;
        bool sourceCountRead = false;
        ExecuteCleanup(
            () =>
            {
                sourceCount = tileSet.GetSourceCount();
                sourceCountRead = true;
            },
            failures
        );
        if (!sourceCountRead)
            return;

        for (int sourceIndex = 0; sourceIndex < sourceCount; sourceIndex++)
        {
            int sourceId = 0;
            bool sourceIdRead = false;
            int capturedIndex = sourceIndex;
            ExecuteCleanup(
                () =>
                {
                    sourceId = tileSet.GetSourceId(capturedIndex);
                    sourceIdRead = true;
                },
                failures
            );
            if (sourceIdRead)
                sourceIds.Add(sourceId);
        }
        for (int sourceIndex = sourceIds.Count - 1; sourceIndex >= 0; sourceIndex--)
        {
            int sourceId = sourceIds[sourceIndex];
            ExecuteCleanup(() => tileSet.RemoveSource(sourceId), failures);
        }
    }

    public void _refresh_tileset_profile()
    {
        ThrowIfDisposed();
        StringName desiredProfile = _resolve_tile_profile_id();
        if (desiredProfile == _tile_profile_id && _tile_set != null)
            return;
        _ensure_tileset(desiredProfile);
        _apply_tileset_to_layers();
        _apply_layer_offsets();
    }

    public bool HasLayersBound() =>
        _input_layer != null && _marker_layers.Count > 0 && _tile_set != null;

    public bool IsRenderContentReady()
    {
        if (!HasLayersBound())
            return false;
        if (
            _battle_state == null
            || _battle_state.IsEmpty()
            || _battle_state.map_size == Vector2I.Zero
        )
            return false;
        if (_count_rendered_top_cells() < _count_expected_drawable_cells())
            return false;
        if (_count_rendered_units() != _count_expected_rendered_units())
            return false;
        if (_count_rendered_props() != _count_expected_rendered_props())
            return false;
        return true;
    }

    private void _redraw()
    {
        _clear_tile_layers();
        _clear_dynamic_nodes();
        if (
            _battle_state == null
            || _battle_state.IsEmpty()
            || _battle_state.map_size == Vector2I.Zero
        )
            return;
        List<BattleCellState> cells = _collect_cells();
        _draw_terrain_layers(cells);
        _draw_marker_layer();
        _draw_props(cells);
        _draw_units();
        _draw_target_highlights();
    }

    private void _draw_terrain_layers(List<BattleCellState> cells)
    {
        foreach (BattleCellState cellState in cells)
        {
            if (cellState == null)
                continue;
            Vector2I coord = cellState.coord;
            if (!_is_cell_inside_battle(coord))
                continue;
            int heightIndex = Mathf.Clamp((int)cellState.current_height, 0, MAX_HEIGHT_LAYERS - 1);
            int topSourceId = _get_top_source_id(cellState.base_terrain.ToString(), coord);
            if (topSourceId >= 0 && heightIndex < _top_layers.Count)
                _top_layers[heightIndex].SetCell(coord, topSourceId, Vector2I.Zero, 0);
            int overlaySourceId = _get_overlay_source_id(cellState.base_terrain.ToString(), coord);
            if (overlaySourceId >= 0 && heightIndex < _overlay_layers.Count)
                _overlay_layers[heightIndex].SetCell(coord, overlaySourceId, Vector2I.Zero, 0);
        }
        _draw_edge_faces();
    }

    private void _draw_edge_faces()
    {
        if (_battle_state == null)
            return;
        foreach (BattleEdgeFaceState edgeFace in _edge_service.GetAllEdgeFaces(_battle_state))
        {
            if (edgeFace == null || !edgeFace.HasAnyFace())
                continue;
            _draw_drop_face(edgeFace);
            _draw_feature_face(edgeFace);
        }
    }

    private void _draw_drop_face(BattleEdgeFaceState edge_face)
    {
        if (edge_face == null || !edge_face.HasDropFace())
            return;
        IReadOnlyList<TileMapLayer> layers =
            edge_face.direction == Vector2I.Right
                ? _edge_drop_east_layers
                : _edge_drop_south_layers;
        StringName sourceKey =
            edge_face.direction == Vector2I.Right ? SOURCE_EDGE_DROP_EAST : SOURCE_EDGE_DROP_SOUTH;
        Vector2I renderCoord = _get_edge_render_coord(edge_face);
        foreach (int renderHeight in edge_face.drop_face_layer_heights)
        {
            int layerIndex = renderHeight - 1;
            if (layerIndex < 0 || layerIndex >= layers.Count)
                continue;
            layers[layerIndex]
                .SetCell(
                    renderCoord,
                    _get_source_id(sourceKey, edge_face.origin_coord, layerIndex),
                    Vector2I.Zero,
                    0
                );
        }
    }

    private void _draw_feature_face(BattleEdgeFaceState edge_face)
    {
        if (edge_face == null || !edge_face.HasFeatureFace())
            return;
        if (edge_face.FeatureRenderKind != BattleEdgeRenderKind.Wall)
            return;
        IReadOnlyList<TileMapLayer> layers =
            edge_face.direction == Vector2I.Right ? _wall_east_layers : _wall_south_layers;
        StringName sourceKey =
            edge_face.direction == Vector2I.Right ? SOURCE_WALL_EAST : SOURCE_WALL_SOUTH;
        Vector2I renderCoord = _get_edge_render_coord(edge_face);
        for (int layerOffset = 0; layerOffset < edge_face.feature_layers; layerOffset++)
        {
            int layerIndex = Mathf.Clamp(
                (int)edge_face.from_height - layerOffset,
                0,
                MAX_HEIGHT_LAYERS - 1
            );
            if (layerIndex < 0 || layerIndex >= layers.Count)
                continue;
            layers[layerIndex]
                .SetCell(
                    renderCoord,
                    _get_source_id(sourceKey, edge_face.origin_coord, layerIndex),
                    Vector2I.Zero,
                    0
                );
        }
    }

    private Vector2I _get_edge_render_coord(BattleEdgeFaceState edge_face)
    {
        if (edge_face == null)
            return Vector2I.Zero;
        if (edge_face.direction == Vector2I.Right || edge_face.direction == Vector2I.Down)
            return edge_face.neighbor_coord;
        return edge_face.origin_coord;
    }

    private void _draw_marker_layer()
    {
        if (_marker_layers.Count == 0)
            return;
        _clear_marker_layers();
        _draw_objective_exit_markers();
        if (_selected_coord != new Vector2I(-1, -1) && _is_cell_inside_battle(_selected_coord))
            _set_marker_cell(_selected_coord, _get_selected_marker_source_id(_selected_coord));
        if (_target_selection_mode == "movement")
        {
            foreach (Vector2I reachableCoord in _valid_target_coords)
            {
                if (reachableCoord == _selected_coord || !_is_cell_inside_battle(reachableCoord))
                    continue;
                _set_marker_cell(reachableCoord, _get_move_reachable_marker_source_id());
            }
            return;
        }
        foreach (Vector2I previewCoord in _preview_target_coords)
        {
            if (previewCoord == _selected_coord || !_is_cell_inside_battle(previewCoord))
                continue;
            _set_marker_cell(previewCoord, _get_source_id(SOURCE_PREVIEW));
        }
    }

    private void _draw_objective_exit_markers()
    {
        IReadOnlyList<Vector2I> exitCoords =
            _battle_state?.ObjectiveRuntimeState switch
            {
                BattleEscapeObjectiveRuntimeState escapeObjective =>
                    escapeObjective.ExitCoords,
                BattleEscortObjectiveRuntimeState escortObjective =>
                    escortObjective.ExitCoords,
                BattleInterceptObjectiveRuntimeState interceptObjective =>
                    interceptObjective.ExitCoords,
                BattleNodeOperationObjectiveRuntimeState nodeOperationObjective =>
                    ResolveIncompleteOperationNodeCoords(nodeOperationObjective),
                BattleControlObjectiveRuntimeState controlObjective =>
                    ResolveControlZoneCoords(controlObjective),
                _ => null,
            };
        if (exitCoords == null)
            return;
        int sourceId = _get_source_id(SOURCE_OBJECTIVE_EXIT);
        if (sourceId < 0)
            return;
        foreach (Vector2I exitCoord in exitCoords)
        {
            if (_is_cell_inside_battle(exitCoord))
                _set_marker_cell(exitCoord, sourceId);
        }
    }

    private static IReadOnlyList<Vector2I> ResolveIncompleteOperationNodeCoords(
        BattleNodeOperationObjectiveRuntimeState objective
    )
    {
        var result = new List<Vector2I>();
        foreach (
            BattleOperationNodeRuntimeState node in
            objective?.OperationNodes
            ?? System.Array.Empty<BattleOperationNodeRuntimeState>()
        )
        {
            if (!node.IsCompleted)
                result.Add(node.Coord);
        }
        return result;
    }

    private static IReadOnlyList<Vector2I> ResolveControlZoneCoords(
        BattleControlObjectiveRuntimeState objective
    )
    {
        var result = new List<Vector2I>();
        foreach (
            BattleControlZoneRuntimeState zone in
            objective?.ControlZones
            ?? System.Array.Empty<BattleControlZoneRuntimeState>()
        )
        {
            foreach (Vector2I coord in zone.Coords)
            {
                if (!result.Contains(coord))
                    result.Add(coord);
            }
        }
        return result;
    }

    private void _draw_props(List<BattleCellState> cells)
    {
        if (_prop_layer == null || _battle_state == null)
            return;
        foreach (BattleCellState cellState in cells)
        {
            if (cellState == null || !_is_cell_inside_battle(cellState.coord))
                continue;
            List<StringName> propIds = _collect_prop_ids_for_cell(cellState);
            for (int index = 0; index < propIds.Count; index++)
            {
                BattleBoardProp propNode = _create_prop_node(cellState, propIds[index], index);
                if (propNode != null)
                    _prop_layer.AddChild(propNode);
            }
        }
    }

    private void _draw_units()
    {
        if (_unit_layer == null || _battle_state == null)
            return;
        var unitIds = new List<StringName>();
        foreach ((StringName unitId, BattleUnitState _) in _battle_state.UnitEntries())
        {
            if (unitId != "")
                unitIds.Add(unitId);
        }
        unitIds.Sort(
            (a, b) =>
            {
                BattleUnitState leftUnit = GetUnit(_battle_state, a);
                BattleUnitState rightUnit = GetUnit(_battle_state, b);
                if (leftUnit == null)
                    return 1;
                if (rightUnit == null)
                    return -1;
                return _get_unit_sort_key(leftUnit).CompareTo(_get_unit_sort_key(rightUnit));
            }
        );
        foreach (StringName unitIdValue in unitIds)
        {
            BattleUnitState unitState = GetUnit(_battle_state, unitIdValue);
            if (unitState == null || !unitState.is_alive)
                continue;
            Node2D unitNode = _create_unit_token(unitState);
            if (unitNode != null)
            {
                _unit_layer.AddChild(unitNode);
                _unitNodesById[unitIdValue] = unitNode;
            }
        }
    }

    private Node2D _create_unit_token(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return null;
        Vector2 anchor = _get_unit_anchor_position(unit_state);
        int renderDepth = _get_unit_render_depth(unit_state);
        var token = new Node2D
        {
            Name = unit_state.unit_id.ToString(),
            Position = anchor + _get_unit_anchor_bias(),
            ZIndex = renderDepth,
        };
        token.SetMeta("sort_anchor_y", anchor.Y);
        token.SetMeta("sort_depth", renderDepth);
        token.SetMeta("board_coord", unit_state.coord);
        Texture2D spriteTexture = _resolve_unit_sprite_texture(unit_state);
        if (spriteTexture != null)
        {
            _attach_unit_sprite_visuals(token, unit_state, spriteTexture);
        }
        else
        {
            var body = new Polygon2D
            {
                Polygon = new[]
                {
                    new Vector2(0.0f, -14.0f),
                    new Vector2(12.0f, 0.0f),
                    new Vector2(0.0f, 14.0f),
                    new Vector2(-12.0f, 0.0f),
                },
                Color = _get_unit_color(unit_state),
                Antialiased = true,
            };
            token.AddChild(body);
            var outline = new Line2D
            {
                Points = new[]
                {
                    new Vector2(0.0f, -14.0f),
                    new Vector2(12.0f, 0.0f),
                    new Vector2(0.0f, 14.0f),
                    new Vector2(-12.0f, 0.0f),
                    new Vector2(0.0f, -14.0f),
                },
                Width = 2.0f,
                DefaultColor = new Color(0.18f, 0.11f, 0.06f, 0.96f),
                Antialiased = true,
            };
            token.AddChild(outline);
        }
        if (
            spriteTexture == null
            && _battle_state != null
            && unit_state.unit_id == _battle_state.active_unit_id
        )
        {
            var activeOutline = new Line2D
            {
                Points = new[]
                {
                    new Vector2(0.0f, -18.0f),
                    new Vector2(16.0f, 0.0f),
                    new Vector2(0.0f, 18.0f),
                    new Vector2(-16.0f, 0.0f),
                    new Vector2(0.0f, -18.0f),
                },
                Width = 2.0f,
                DefaultColor = new Color(1.0f, 0.94f, 0.76f, 0.96f),
                Antialiased = true,
            };
            token.AddChild(activeOutline);
        }
        var label = new Label
        {
            Name = "UnitGlyphLabel",
            Text = _build_unit_short_name(unit_state),
            Position = new Vector2(
                -UNIT_GLYPH_LABEL_SIZE.X * 0.5f,
                -UNIT_GLYPH_LABEL_SIZE.Y * 0.5f
            ),
            Size = UNIT_GLYPH_LABEL_SIZE,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = UNIT_OVERLAY_ABSOLUTE_Z,
            ZAsRelative = false,
        };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", new Color(0.98f, 0.96f, 0.9f, 0.98f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0.16f, 0.1f, 0.06f, 0.92f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        token.AddChild(label);
        Control healthBar = _create_unit_health_bar(unit_state);
        if (healthBar != null)
        {
            healthBar.ZIndex = UNIT_OVERLAY_ABSOLUTE_Z;
            healthBar.ZAsRelative = false;
            if (spriteTexture != null)
            {
                // 高贴图:把血条抬到贴图顶部之上,而不是落在身体中部
                float barTop = _get_unit_sprite_top_y(spriteTexture)
                    - UNIT_SPRITE_OVERLAY_GAP
                    - UNIT_HEALTH_BAR_SIZE.Y;
                healthBar.Position = new Vector2(healthBar.Position.X, barTop);
            }
            token.AddChild(healthBar);
        }
        return token;
    }

    private void _attach_unit_sprite_visuals(
        Node2D token,
        BattleUnitState unit_state,
        Texture2D spriteTexture
    )
    {
        if (token == null || unit_state == null || spriteTexture == null)
            return;
        float groundY = -_get_unit_anchor_bias().Y;
        var shadow = new Polygon2D
        {
            Name = "UnitSpriteShadow",
            Polygon = _build_unit_ellipse_polygon(UNIT_SPRITE_SHADOW_HALF_SIZE),
            Color = UNIT_SPRITE_SHADOW_COLOR,
            Position = new Vector2(0.0f, groundY),
            Antialiased = true,
            ZIndex = -2,
        };
        token.AddChild(shadow);
        if (_battle_state != null && unit_state.unit_id == _battle_state.active_unit_id)
        {
            var highlight = new Polygon2D
            {
                Name = "UnitSpriteActiveHighlight",
                Polygon = _build_unit_ellipse_polygon(UNIT_SPRITE_HIGHLIGHT_HALF_SIZE),
                Color = UNIT_SPRITE_HIGHLIGHT_COLOR,
                Position = new Vector2(0.0f, groundY),
                Antialiased = true,
                ZIndex = -1,
            };
            token.AddChild(highlight);
        }
        Vector2 textureSize = spriteTexture.GetSize();
        if (textureSize.X <= 0.0f || textureSize.Y <= 0.0f)
            return;
        Vector2I tileSize = _get_board_tile_size();
        float targetWidth = Mathf.Max((float)tileSize.X * UNIT_SPRITE_TILE_WIDTH_RATIO, 1.0f);
        float spriteScale = targetWidth / textureSize.X;
        var sprite = new Sprite2D
        {
            Name = "UnitSprite",
            Texture = spriteTexture,
            Centered = true,
            Scale = Vector2.One * spriteScale,
            Position = new Vector2(
                0.0f,
                groundY + (0.5f - UNIT_SPRITE_GROUND_ANCHOR_RATIO) * textureSize.Y * spriteScale
            ),
            ZIndex = 0,
        };
        token.AddChild(sprite);
    }

    private Texture2D _resolve_unit_sprite_texture(BattleUnitState unitState)
    {
        string path = unitState?.battle_sprite_texture_path ?? "";
        return string.IsNullOrEmpty(path) ? null : _load_texture_from_png(path);
    }

    // 贴图缩放后的可见高度(像素,token 本地坐标)。与 _attach_unit_sprite_visuals 的
    // 缩放算法一致:宽度按格宽比缩放,等比得到高度。
    private float _get_unit_sprite_scaled_height(Vector2 textureSize)
    {
        if (textureSize.X <= 0.0f || textureSize.Y <= 0.0f)
            return 0.0f;
        Vector2I tileSize = _get_board_tile_size();
        float targetWidth = Mathf.Max((float)tileSize.X * UNIT_SPRITE_TILE_WIDTH_RATIO, 1.0f);
        float spriteScale = targetWidth / textureSize.X;
        return textureSize.Y * spriteScale;
    }

    // 贴图顶部在 token 本地坐标的 Y(脚底锚点为基准,向上为负)。
    private float _get_unit_sprite_top_y(Texture2D spriteTexture)
    {
        float groundY = -_get_unit_anchor_bias().Y;
        float scaledHeight = _get_unit_sprite_scaled_height(spriteTexture.GetSize());
        return groundY - UNIT_SPRITE_GROUND_ANCHOR_RATIO * scaledHeight;
    }

    private Vector2[] _build_unit_ellipse_polygon(Vector2 half_size)
    {
        Vector2 safeHalfSize = new(Mathf.Max(half_size.X, 1.0f), Mathf.Max(half_size.Y, 1.0f));
        var points = new Vector2[UNIT_SPRITE_ELLIPSE_SEGMENT_COUNT];
        for (int index = 0; index < UNIT_SPRITE_ELLIPSE_SEGMENT_COUNT; index++)
        {
            float angle = Mathf.Tau * (float)index / (float)UNIT_SPRITE_ELLIPSE_SEGMENT_COUNT;
            points[index] = new Vector2(
                Mathf.Cos(angle) * safeHalfSize.X,
                Mathf.Sin(angle) * safeHalfSize.Y
            );
        }
        return points;
    }

    private Control _create_unit_health_bar(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return null;
        int hpMax = _get_unit_hp_max(unit_state);
        int clampedHp = Mathf.Clamp((int)unit_state.current_hp, 0, hpMax);
        float hpRatio = Mathf.Clamp((float)clampedHp / (float)hpMax, 0.0f, 1.0f);
        float maxFillWidth = Mathf.Max(UNIT_HEALTH_BAR_SIZE.X - 2.0f, 0.0f);
        float fillWidth = maxFillWidth * hpRatio;
        if (clampedHp > 0 && fillWidth > 0.0f && fillWidth < 1.0f)
            fillWidth = 1.0f;
        var healthBar = new Panel
        {
            Name = "HealthBarRoot",
            Position = new Vector2(-UNIT_HEALTH_BAR_SIZE.X * 0.5f, UNIT_HEALTH_BAR_Y_OFFSET),
            Size = UNIT_HEALTH_BAR_SIZE,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true,
        };
        StyleBoxFlat panelStyle = _get_unit_health_bar_style();
        healthBar.AddThemeStyleboxOverride("panel", panelStyle);
        var fill = new ColorRect
        {
            Name = "HealthBarFill",
            Position = Vector2.One,
            Size = new Vector2(fillWidth, Mathf.Max(UNIT_HEALTH_BAR_SIZE.Y - 2.0f, 0.0f)),
            Color = _get_unit_health_bar_fill_color(hpRatio),
        };
        healthBar.AddChild(fill);
        var valueLabel = new Label
        {
            Name = "HealthBarTextLabel",
            Text = $"{clampedHp}/{hpMax}",
            Position = Vector2.Zero,
            Size = UNIT_HEALTH_BAR_SIZE,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        valueLabel.AddThemeFontSizeOverride("font_size", 9);
        valueLabel.AddThemeColorOverride("font_color", new Color(0.98f, 0.97f, 0.94f, 1.0f));
        valueLabel.AddThemeColorOverride(
            "font_shadow_color",
            new Color(0.08f, 0.05f, 0.04f, 0.94f)
        );
        valueLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        valueLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        healthBar.AddChild(valueLabel);
        return healthBar;
    }

    private Vector2 _get_unit_anchor_position(BattleUnitState unit_state)
    {
        if (unit_state == null || _input_layer == null || _battle_state == null)
            return Vector2.Zero;
        Vector2 total = Vector2.Zero;
        int count = 0;
        foreach (Vector2I occupiedCoord in unit_state.occupied_coords)
        {
            BattleCellState cell = GetCell(_battle_state, occupiedCoord);
            if (cell == null)
                continue;
            total += _get_cell_anchor_position(occupiedCoord, (int)cell.current_height);
            count += 1;
        }
        return count <= 0 ? _get_cell_anchor_position(unit_state.coord, 0) : total / (float)count;
    }

    private int _get_unit_render_depth(BattleUnitState unit_state)
    {
        if (unit_state == null || _input_layer == null || _battle_state == null)
            return 0;
        int bestDepth = int.MinValue;
        foreach (Vector2I occupiedCoord in unit_state.occupied_coords)
        {
            BattleCellState cell = GetCell(_battle_state, occupiedCoord);
            int heightValue =
                cell != null ? Mathf.Clamp((int)cell.current_height, 0, MAX_HEIGHT_LAYERS - 1) : 0;
            bestDepth = Mathf.Max(bestDepth, _get_cell_render_depth(occupiedCoord, heightValue));
        }
        return bestDepth == int.MinValue ? _get_cell_render_depth(unit_state.coord, 0) : bestDepth;
    }

    private float _get_unit_sort_key(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return 0.0f;
        float bestKey = (float)unit_state.coord.Y * 1000.0f + (float)unit_state.coord.X;
        foreach (Vector2I occupiedCoord in unit_state.occupied_coords)
        {
            BattleCellState cell = GetCell(_battle_state, occupiedCoord);
            float heightValue =
                cell != null
                    ? (float)Mathf.Clamp((int)cell.current_height, 0, MAX_HEIGHT_LAYERS - 1)
                    : 0.0f;
            bestKey = Mathf.Max(
                bestKey,
                (float)occupiedCoord.Y * 1000.0f + (float)occupiedCoord.X + heightValue * 0.01f
            );
        }
        return bestKey;
    }

    private void _clear_tile_layers() => _clear_tile_layers(null);

    private void _clear_tile_layers(List<Exception> failures)
    {
        ClearLayers(_top_layers, failures);
        ClearLayers(_edge_drop_east_layers, failures);
        ClearLayers(_edge_drop_south_layers, failures);
        ClearLayers(_wall_east_layers, failures);
        ClearLayers(_wall_south_layers, failures);
        ClearLayers(_overlay_layers, failures);
        ClearLayers(_marker_layers, failures);
    }

    private void _clear_marker_layers() => ClearLayers(_marker_layers, null);

    private void _detach_tilesets_from_layers(List<Exception> failures)
    {
        ClearTileSets(_top_layers, failures);
        ClearTileSets(_edge_drop_east_layers, failures);
        ClearTileSets(_edge_drop_south_layers, failures);
        ClearTileSets(_wall_east_layers, failures);
        ClearTileSets(_wall_south_layers, failures);
        ClearTileSets(_overlay_layers, failures);
        ClearTileSets(_marker_layers, failures);
        ExecuteCleanup(
            () =>
            {
                if (_input_layer != null)
                    _input_layer.TileSet = null;
            },
            failures
        );
    }

    private void _clear_dynamic_nodes() => _clear_dynamic_nodes(null);

    private void _clear_dynamic_nodes(List<Exception> failures)
    {
        _clear_child_nodes(_prop_layer, failures);
        _unitNodesById.Clear();
        _clear_child_nodes(_unit_layer, failures);
        _clear_child_nodes(_target_highlight_layer, failures);
    }

    private void _draw_target_highlights()
    {
        if (_target_highlight_layer == null)
            return;
        _clear_child_nodes(_target_highlight_layer);
        if (_target_selection_mode == "movement")
            return;
        var previewCoordSet = new HashSet<Vector2I>();
        bool isMultiUnitSelection = _target_selection_mode == "multi_unit";
        if (isMultiUnitSelection)
        {
            foreach (Vector2I previewCoord in _preview_target_coords)
            {
                previewCoordSet.Add(previewCoord);
                Polygon2D lockedHighlight = _create_target_highlight(
                    previewCoord,
                    LOCKED_TARGET_HIGHLIGHT_COLOR,
                    0.88f,
                    0.68f
                );
                if (lockedHighlight != null)
                {
                    lockedHighlight.Name = $"LockedTarget_{previewCoord.X}_{previewCoord.Y}";
                    _target_highlight_layer.AddChild(lockedHighlight);
                }
            }
        }
        else
        {
            foreach (Vector2I previewCoord in _preview_target_coords)
                previewCoordSet.Add(previewCoord);
        }
        bool isMultiUnitConfirmReady =
            isMultiUnitSelection
            && _preview_target_coords.Count >= _target_min_count
            && _preview_target_coords.Count < _target_max_count;
        foreach (Vector2I targetCoord in _valid_target_coords)
        {
            if (previewCoordSet.Contains(targetCoord) || !_is_cell_inside_battle(targetCoord))
                continue;
            Color targetColor = isMultiUnitConfirmReady
                ? CONFIRM_READY_TARGET_HIGHLIGHT_COLOR
                : VALID_TARGET_HIGHLIGHT_COLOR;
            float targetScale = isMultiUnitConfirmReady ? 0.92f : 0.88f;
            Polygon2D highlight = _create_target_highlight(
                targetCoord,
                targetColor,
                targetScale,
                0.0f
            );
            if (highlight == null)
                continue;
            highlight.Name = $"ValidTarget_{targetCoord.X}_{targetCoord.Y}";
            _target_highlight_layer.AddChild(highlight);
        }
        Vector2I confirmFocusCoord = isMultiUnitConfirmReady
            ? _resolve_multi_unit_confirm_focus_coord()
            : new Vector2I(-1, -1);
        if (isMultiUnitConfirmReady && _is_cell_inside_battle(confirmFocusCoord))
        {
            Polygon2D confirmHalo = _create_target_highlight(
                confirmFocusCoord,
                CONFIRM_READY_FOCUS_HALO_COLOR,
                1.14f,
                0.0f
            );
            if (confirmHalo != null)
            {
                confirmHalo.Name = $"ConfirmReady_{confirmFocusCoord.X}_{confirmFocusCoord.Y}";
                _target_highlight_layer.AddChild(confirmHalo);
            }
        }
        _draw_target_hit_badges();
    }

    private void _set_target_hit_badges(IReadOnlyDictionary<Vector2I, string> target_hit_badges)
    {
        _target_hit_badges.Clear();
        if (target_hit_badges == null)
            return;
        foreach ((Vector2I coord, string badgeText) in target_hit_badges)
        {
            if (!string.IsNullOrEmpty(badgeText))
                _target_hit_badges[coord] = badgeText;
        }
    }

    private void _draw_target_hit_badges()
    {
        if (_target_highlight_layer == null || _target_hit_badges.Count == 0)
            return;
        foreach ((Vector2I coord, string badgeText) in _target_hit_badges)
        {
            if (!_is_cell_inside_battle(coord))
                continue;
            Control badge = _create_target_hit_badge(coord, badgeText);
            if (badge == null)
                continue;
            badge.Name = $"HitBadge_{coord.X}_{coord.Y}";
            _target_highlight_layer.AddChild(badge);
        }
    }

    private Control _create_target_hit_badge(Vector2I target_coord, string badge_text)
    {
        if (string.IsNullOrEmpty(badge_text) || !_is_cell_inside_battle(target_coord))
            return null;
        var badge = new PanelContainer
        {
            Position =
                _get_cell_anchor_position(target_coord, _get_cell_height_index(target_coord))
                + HIT_BADGE_OFFSET,
            CustomMinimumSize = HIT_BADGE_SIZE,
            Size = HIT_BADGE_SIZE,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        badge.AddThemeStyleboxOverride("panel", _get_hit_badge_style());
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 3);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 3);
        badge.AddChild(margin);
        var label = new Label
        {
            Text = badge_text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", HIT_BADGE_TEXT_COLOR);
        margin.AddChild(label);
        return badge;
    }

    private StyleBoxFlat _get_unit_health_bar_style()
    {
        return _unitHealthBarStyle ??= OwnRenderResource(
            new StyleBoxFlat
            {
                BgColor = UNIT_HEALTH_BAR_BG_COLOR,
                BorderColor = UNIT_HEALTH_BAR_BORDER_COLOR,
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusBottomLeft = 2,
            },
            "unit_health_bar_style"
        );
    }

    private StyleBoxFlat _get_hit_badge_style()
    {
        return _hitBadgeStyle ??= OwnRenderResource(
            new StyleBoxFlat
            {
                BgColor = HIT_BADGE_BG_COLOR,
                BorderColor = HIT_BADGE_EDGE_COLOR,
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 5,
                CornerRadiusTopRight = 5,
                CornerRadiusBottomRight = 5,
                CornerRadiusBottomLeft = 5,
            },
            "target_hit_badge_style"
        );
    }

    private Vector2I _resolve_multi_unit_confirm_focus_coord()
    {
        if (_battle_state != null)
        {
            BattleUnitState activeUnit = GetUnit(_battle_state, _battle_state.active_unit_id);
            if (
                activeUnit != null
                && activeUnit.is_alive
                && _is_cell_inside_battle(activeUnit.coord)
            )
                return activeUnit.coord;
        }
        return _selected_coord;
    }

    private Polygon2D _create_target_highlight(
        Vector2I target_coord,
        Color color,
        float scale,
        float alpha_scale
    )
    {
        if (!_is_cell_inside_battle(target_coord))
            return null;
        Color finalColor =
            alpha_scale > 0.0f
                ? new Color(color.R, color.G, color.B, color.A * alpha_scale)
                : color;
        var highlight = new Polygon2D
        {
            Position = _get_cell_anchor_position(
                target_coord,
                _get_cell_height_index(target_coord)
            ),
            Polygon = _build_target_highlight_polygon(scale),
            Color = finalColor,
            Antialiased = true,
        };
        highlight.SetMeta("board_coord", target_coord);
        return highlight;
    }

    private void _set_marker_cell(Vector2I coord, int source_id)
    {
        if (source_id < 0)
            return;
        int heightIndex = _get_cell_height_index(coord);
        if (heightIndex < 0 || heightIndex >= _marker_layers.Count)
            return;
        _marker_layers[heightIndex]?.SetCell(coord, source_id, Vector2I.Zero, 0);
    }

    private Vector2[] _build_target_highlight_polygon(float scale)
    {
        float safeScale = Mathf.Max(scale, 0.2f);
        return new[]
        {
            new Vector2(0.0f, -13.0f) * safeScale,
            new Vector2(28.0f, 0.0f) * safeScale,
            new Vector2(0.0f, 13.0f) * safeScale,
            new Vector2(-28.0f, 0.0f) * safeScale,
        };
    }

    private void _clear_child_nodes(Node container) => _clear_child_nodes(container, null);

    private void _clear_child_nodes(Node container, List<Exception> failures)
    {
        if (container == null)
            return;

        var children = new List<Node>();
        ExecuteCleanup(
            () =>
            {
                foreach (Node child in container.GetChildren())
                    children.Add(child);
            },
            failures
        );
        foreach (Node child in children)
        {
            ExecuteCleanup(() => container.RemoveChild(child), failures);
            ExecuteCleanup(() => child.Free(), failures);
        }
    }

    public int _count_expected_drawable_cells()
    {
        if (_battle_state == null)
            return 0;
        int count = 0;
        foreach ((Vector2I coord, BattleCellState _) in _battle_state.CellEntries())
            if (_is_cell_inside_battle(coord))
                count += 1;
        return count;
    }

    public int _count_rendered_top_cells()
    {
        int count = 0;
        foreach (TileMapLayer layer in _top_layers)
            if (layer != null)
                count += layer.GetUsedCells().Count;
        return count;
    }

    public int _count_expected_rendered_units()
    {
        if (_battle_state == null)
            return 0;
        int count = 0;
        foreach (BattleUnitState unitState in _battle_state.Units())
        {
            if (unitState != null && unitState.is_alive)
                count += 1;
        }
        return count;
    }

    public int _count_rendered_units() => _unit_layer != null ? _unit_layer.GetChildCount() : 0;

    public int _count_expected_rendered_props()
    {
        if (_battle_state == null)
            return 0;
        int count = 0;
        foreach (BattleCellState cellState in _battle_state.Cells())
        {
            if (cellState == null || !_is_cell_inside_battle(cellState.coord))
                continue;
            count += _collect_prop_ids_for_cell(cellState).Count;
        }
        return count;
    }

    public int _count_rendered_props() => _prop_layer != null ? _prop_layer.GetChildCount() : 0;

    private List<BattleCellState> _collect_cells()
    {
        var cells = new List<BattleCellState>();
        if (_battle_state == null)
            return cells;
        foreach (BattleCellState cellState in _battle_state.Cells())
        {
            if (cellState != null)
                cells.Add(cellState);
        }
        cells.Sort(
            (a, b) =>
                a.coord.Y == b.coord.Y
                    ? a.coord.X.CompareTo(b.coord.X)
                    : a.coord.Y.CompareTo(b.coord.Y)
        );
        return cells;
    }

    private void _apply_tileset_to_layers()
    {
        if (_tile_set == null)
            return;
        if (_input_layer != null)
            _input_layer.TileSet = _tile_set;
        ApplyTileSet(_top_layers);
        ApplyTileSet(_edge_drop_east_layers);
        ApplyTileSet(_edge_drop_south_layers);
        ApplyTileSet(_wall_east_layers);
        ApplyTileSet(_wall_south_layers);
        ApplyTileSet(_overlay_layers);
        ApplyTileSet(_marker_layers);
    }

    private void _apply_layer_offsets()
    {
        float heightStep = _get_visual_height_step();
        for (int index = 0; index < _top_layers.Count; index++)
            if (_top_layers[index] != null)
                _top_layers[index].Position = new Vector2(0.0f, -(float)index * heightStep);
        for (int index = 0; index < _edge_drop_east_layers.Count; index++)
            if (_edge_drop_east_layers[index] != null)
                _edge_drop_east_layers[index].Position = new Vector2(
                    0.0f,
                    -(float)(index + 1) * heightStep
                );
        for (int index = 0; index < _edge_drop_south_layers.Count; index++)
            if (_edge_drop_south_layers[index] != null)
                _edge_drop_south_layers[index].Position = new Vector2(
                    0.0f,
                    -(float)(index + 1) * heightStep
                );
        for (int index = 0; index < _wall_east_layers.Count; index++)
            if (_wall_east_layers[index] != null)
                _wall_east_layers[index].Position = new Vector2(0.0f, -(float)index * heightStep);
        for (int index = 0; index < _wall_south_layers.Count; index++)
            if (_wall_south_layers[index] != null)
                _wall_south_layers[index].Position = new Vector2(0.0f, -(float)index * heightStep);
        for (int index = 0; index < _overlay_layers.Count; index++)
            if (_overlay_layers[index] != null)
                _overlay_layers[index].Position = new Vector2(0.0f, -(float)index * heightStep);
        for (int index = 0; index < _marker_layers.Count; index++)
            if (_marker_layers[index] != null)
                _marker_layers[index].Position = new Vector2(0.0f, -(float)index * heightStep);
    }

    private void _apply_layer_draw_order()
    {
        if (_input_layer != null)
            _input_layer.ZIndex = TOP_LAYER_Z_BASE - LAYER_Z_STRIDE;
        for (int index = 0; index < _top_layers.Count; index++)
            if (_top_layers[index] != null)
                _top_layers[index].ZIndex = TOP_LAYER_Z_BASE + index * LAYER_Z_STRIDE;
        for (int index = 0; index < _edge_drop_east_layers.Count; index++)
            if (_edge_drop_east_layers[index] != null)
                _edge_drop_east_layers[index].ZIndex =
                    TOP_LAYER_Z_BASE + (index + 1) * LAYER_Z_STRIDE + EDGE_DROP_EAST_LAYER_Z_OFFSET;
        for (int index = 0; index < _edge_drop_south_layers.Count; index++)
            if (_edge_drop_south_layers[index] != null)
                _edge_drop_south_layers[index].ZIndex =
                    TOP_LAYER_Z_BASE
                    + (index + 1) * LAYER_Z_STRIDE
                    + EDGE_DROP_SOUTH_LAYER_Z_OFFSET;
        for (int index = 0; index < _wall_east_layers.Count; index++)
            if (_wall_east_layers[index] != null)
                _wall_east_layers[index].ZIndex =
                    TOP_LAYER_Z_BASE + index * LAYER_Z_STRIDE + WALL_EAST_LAYER_Z_OFFSET;
        for (int index = 0; index < _wall_south_layers.Count; index++)
            if (_wall_south_layers[index] != null)
                _wall_south_layers[index].ZIndex =
                    TOP_LAYER_Z_BASE + index * LAYER_Z_STRIDE + WALL_SOUTH_LAYER_Z_OFFSET;
        for (int index = 0; index < _overlay_layers.Count; index++)
            if (_overlay_layers[index] != null)
                _overlay_layers[index].ZIndex =
                    TOP_LAYER_Z_BASE + index * LAYER_Z_STRIDE + OVERLAY_LAYER_Z_OFFSET;
        for (int index = 0; index < _marker_layers.Count; index++)
            if (_marker_layers[index] != null)
                _marker_layers[index].ZIndex =
                    TOP_LAYER_Z_BASE + index * LAYER_Z_STRIDE + MARKER_LAYER_Z_OFFSET;
        if (_prop_layer != null)
            _prop_layer.ZIndex = PROP_LAYER_Z;
        if (_unit_layer != null)
            _unit_layer.ZIndex = UNIT_LAYER_Z;
        if (_target_highlight_layer != null)
            _target_highlight_layer.ZIndex = TARGET_HIGHLIGHT_LAYER_Z;
    }

    private BattleBoardProp _create_prop_node(
        BattleCellState cell_state,
        StringName prop_id,
        int stack_index
    )
    {
        Node propInstance = EngineAssetAccess
            .ResolveBorrowed<PackedScene>(BattleBoardPropScenePath)
            .Instantiate();
        BattleBoardProp propNode = propInstance as BattleBoardProp;
        if (propNode == null)
            return null;
        int heightValue = Mathf.Clamp((int)cell_state.current_height, 0, MAX_HEIGHT_LAYERS - 1);
        Vector2 anchor = _get_cell_anchor_position(cell_state.coord, heightValue);
        int renderDepth = _get_cell_render_depth(cell_state.coord, heightValue);
        propNode.Name = $"{prop_id}_{cell_state.coord.X}_{cell_state.coord.Y}_{stack_index}";
        propNode.Position = anchor + _get_prop_offset(prop_id, cell_state.coord, stack_index);
        propNode.ZIndex = renderDepth;
        propNode.SetMeta("sort_anchor_y", anchor.Y);
        propNode.SetMeta("sort_depth", renderDepth);
        propNode.SetMeta("board_coord", cell_state.coord);
        propNode.SetMeta("prop_id", prop_id);
        propNode.Configure(
            prop_id,
            _build_coord_hash(
                cell_state.coord,
                stack_index + BattleBoardPropCatalog.GetSortPriority(prop_id)
            ),
            BattleBoardPropCatalog.RequiresInteractionShape(prop_id)
        );
        return propNode;
    }

    private List<StringName> _collect_prop_ids_for_cell(BattleCellState cell_state)
    {
        var propIds = new List<StringName>();
        if (cell_state == null)
            return propIds;
        if (cell_state.base_terrain == TERRAIN_SPIKE)
            propIds.Add(PROP_SPIKE_BARRICADE);
        foreach (StringName propId in cell_state.prop_ids)
        {
            if (!BattleBoardPropCatalog.IsSupported(propId) || propIds.Contains(propId))
                continue;
            propIds.Add(propId);
        }
        var sorted = new List<StringName>();
        foreach (StringName propId in propIds)
            sorted.Add(propId);
        sorted.Sort(
            (a, b) =>
                BattleBoardPropCatalog
                    .GetSortPriority(a)
                    .CompareTo(BattleBoardPropCatalog.GetSortPriority(b))
        );
        propIds.Clear();
        foreach (StringName propId in sorted)
            propIds.Add(propId);
        return propIds;
    }

    private Vector2 _get_prop_offset(StringName prop_id, Vector2I coord, int stack_index)
    {
        float sideSign = _get_variant_index(coord, 2, stack_index + 1) == 0 ? 1.0f : -1.0f;
        _render_profile ??= BattleBoardRenderProfile.ForTerrainProfileId(_tile_profile_id);
        return _render_profile.GetPropAnchorBias(prop_id, sideSign);
    }

    private Vector2 _get_cell_anchor_position(Vector2I coord, int height_value)
    {
        if (_input_layer == null)
            return Vector2.Zero;
        Vector2 anchor = _get_cell_plane_position(coord);
        anchor.Y -=
            (float)Mathf.Clamp(height_value, 0, MAX_HEIGHT_LAYERS - 1) * _get_visual_height_step();
        return anchor;
    }

    private Vector2 _get_cell_plane_position(Vector2I coord) =>
        _input_layer == null ? Vector2.Zero : _input_layer.MapToLocal(coord);

    // 单位/道具的层级深度只按"高度分档"(height×stride + 偏移),不再叠加随屏幕 Y
    // 增长的 planeY 项。这样动态对象与地形高度层正确交织:更高一级的前方地形/南墙
    // 会盖住单位;而同高度的多个单位/道具靠 Unit/PropLayer 自身的 y_sort 按真实 Y
    // 排前后(逐格 planeY 一旦写进 ZIndex 会压过 y_sort,反而让单位恒压地形)。
    private int _get_cell_render_depth(Vector2I coord, int height_value)
    {
        int clampedHeight = Mathf.Clamp(height_value, 0, MAX_HEIGHT_LAYERS - 1);
        return clampedHeight * LAYER_Z_STRIDE + DYNAMIC_LAYER_Z_OFFSET;
    }

    private int _get_cell_height_index(Vector2I coord)
    {
        if (_battle_state == null)
            return 0;
        BattleCellState cell = GetCell(_battle_state, coord);
        return cell == null ? 0 : Mathf.Clamp((int)cell.current_height, 0, MAX_HEIGHT_LAYERS - 1);
    }

    private void _ensure_tileset(StringName profile_id)
    {
        BattleBoardRenderProfile renderProfile = BattleBoardRenderProfile.ForTerrainProfileId(
            profile_id
        );
        StringName cacheKey = renderProfile.GetCacheKey();
        if (_tile_set != null && _tile_profile_id == renderProfile.terrain_profile_id)
        {
            _render_profile = renderProfile;
            return;
        }
        if (_tileset_cache.TryGetValue(cacheKey, out TileSetCacheEntry cachedProfile))
        {
            if (cachedProfile?.TileSet != null)
            {
                _tile_profile_id = renderProfile.terrain_profile_id;
                _render_profile = renderProfile;
                _tile_set = cachedProfile.TileSet;
                ReplaceSourceIds(cachedProfile.SourceIds);
                return;
            }
        }
        _tile_profile_id = renderProfile.terrain_profile_id;
        _render_profile = renderProfile;
        _tile_set = OwnRenderResource(
            new TileSet
            {
                TileSize = renderProfile.board_tile_size,
                TileShape = TileSet.TileShapeEnum.Isometric,
                TileLayout = TileSet.TileLayoutEnum.DiamondDown,
                TileOffsetAxis = TileSet.TileOffsetAxisEnum.Horizontal,
            },
            $"tileset:{cacheKey}"
        );
        _source_ids.Clear();
        _register_profile_textures(renderProfile);
        _tileset_cache[cacheKey] = new TileSetCacheEntry
        {
            TileSet = _tile_set,
            SourceIds = CloneSourceIds(_source_ids),
        };
    }

    private void _register_profile_textures(BattleBoardRenderProfile render_profile)
    {
        render_profile ??= BattleBoardRenderProfile.ForTerrainProfileId(
            BattleBoardRenderProfile.TERRAIN_PROFILE_DEFAULT()
        );
        string tileDir = render_profile.asset_dir;
        foreach (BattleBoardTileSourceSpec sourceSpec in render_profile.GetSourceSpecs())
        {
            var textures = new List<Texture2D>();
            foreach (string fileName in sourceSpec.Files)
            {
                Texture2D texture = _load_texture_from_png($"{tileDir}/{fileName}");
                if (texture == null)
                {
                    GameLog.Error($"BattleBoardController 缺少地形贴图：{tileDir}/{fileName}.", "ui.battle.missing_tile_texture", "ui");
                    continue;
                }
                textures.Add(texture);
            }
            if (textures.Count == 0 && sourceSpec.AllowGeneratedFallback)
            {
                Texture2D fallbackTexture = _build_missing_source_texture(
                    sourceSpec.Key,
                    sourceSpec
                );
                if (fallbackTexture != null)
                    textures.Add(fallbackTexture);
            }
            _register_source_options(
                sourceSpec.Key,
                textures,
                sourceSpec
            );
        }
        BattleBoardTileSourceSpec generatedMarkerSpec =
            _build_generated_marker_source_spec(render_profile);
        _register_source_options(
            SOURCE_ACTIVE_SELECTED,
            new[] { _build_active_selected_marker_texture(render_profile) },
            generatedMarkerSpec
        );
        _register_source_options(
            SOURCE_MOVE_REACHABLE,
            new[] { _build_move_reachable_marker_texture(render_profile) },
            generatedMarkerSpec
        );
        _register_source_options(
            SOURCE_OBJECTIVE_EXIT,
            new[]
            {
                _build_diamond_texture(
                    OBJECTIVE_EXIT_MARKER_COLOR,
                    0.32f,
                    render_profile.board_tile_size
                ),
            },
            generatedMarkerSpec
        );
    }

    private int _add_atlas_source(
        Texture2D texture,
        BattleBoardTileSourceSpec source_spec
    )
    {
        var source = OwnRenderResource(
            new TileSetAtlasSource
            {
                Texture = texture,
                TextureRegionSize = source_spec?.AtlasRegionSize ?? _get_board_tile_size(),
                UseTexturePadding = false,
            },
            "atlas_source"
        );
        source.CreateTile(Vector2I.Zero, Vector2I.One);
        TileData tileData = source.GetTileData(Vector2I.Zero, 0);
        if (tileData != null)
            tileData.TextureOrigin = source_spec?.VisualOrigin ?? source_spec?.TextureOrigin
                ?? Vector2I.Zero;
        return _tile_set.AddSource(source);
    }

    private void _register_source_options(
        StringName source_key,
        IEnumerable<Texture2D> textures,
        BattleBoardTileSourceSpec source_spec
    )
    {
        var sourceIds = new List<int>();
        foreach (Texture2D texture in textures)
        {
            if (texture != null)
                sourceIds.Add(_add_atlas_source(texture, source_spec));
        }
        _source_ids[source_key] = sourceIds;
    }

    private Texture2D _build_active_selected_marker_texture(BattleBoardRenderProfile render_profile)
    {
        string tileDir = render_profile.asset_dir;
        string cacheKey = $"__generated_active_selected__{render_profile.GetCacheKey()}";
        if (_texture_cache.TryGetValue(cacheKey, out Texture2D cachedTexture))
            return cachedTexture;
        Texture2D baseTexture =
            _load_texture_from_png($"{tileDir}/{render_profile.GetPrimaryLandFile()}")
            ?? _load_texture_from_png($"{tileDir}/{render_profile.GetSelectedMarkerFile()}");
        if (baseTexture == null)
            return _build_diamond_texture(
                ACTIVE_SELECTED_MARKER_COLOR,
                1.0f,
                render_profile.board_tile_size
            );
        Image image = OwnRenderResource(
            baseTexture.GetImage(),
            $"active_selected_image:{cacheKey}"
        );
        if (image == null || image.IsEmpty())
            return null;
        image.Convert(Image.Format.Rgba8);
        for (int y = 0; y < image.GetHeight(); y++)
        for (int x = 0; x < image.GetWidth(); x++)
        {
            Color pixel = image.GetPixel(x, y);
            if (pixel.A > 0.0f)
                image.SetPixel(
                    x,
                    y,
                    new Color(
                        ACTIVE_SELECTED_MARKER_COLOR.R,
                        ACTIVE_SELECTED_MARKER_COLOR.G,
                        ACTIVE_SELECTED_MARKER_COLOR.B,
                        1.0f
                    )
                );
        }
        Texture2D generatedTexture = OwnRenderResource(
            ImageTexture.CreateFromImage(image),
            $"active_selected_marker:{cacheKey}"
        );
        _texture_cache[cacheKey] = generatedTexture;
        return generatedTexture;
    }

    private Texture2D _build_move_reachable_marker_texture(BattleBoardRenderProfile render_profile)
    {
        string tileDir = render_profile.asset_dir;
        string cacheKey = $"__generated_move_reachable__{render_profile.GetCacheKey()}";
        if (_texture_cache.TryGetValue(cacheKey, out Texture2D cachedTexture))
            return cachedTexture;
        Texture2D baseTexture =
            _load_texture_from_png($"{tileDir}/{render_profile.GetPrimaryLandFile()}")
            ?? _load_texture_from_png($"{tileDir}/{render_profile.GetSelectedMarkerFile()}");
        if (baseTexture == null)
            return _build_diamond_texture(
                MOVE_REACHABLE_MARKER_COLOR_LIGHT,
                0.42f,
                render_profile.board_tile_size
            );
        Image image = OwnRenderResource(
            baseTexture.GetImage(),
            $"move_reachable_image:{cacheKey}"
        );
        if (image == null || image.IsEmpty())
            return null;
        image.Convert(Image.Format.Rgba8);
        for (int y = 0; y < image.GetHeight(); y++)
        for (int x = 0; x < image.GetWidth(); x++)
        {
            Color pixel = image.GetPixel(x, y);
            if (pixel.A <= 0.0f)
                continue;
            float shade = Mathf.Clamp(CalcLuminance(pixel), 0.0f, 1.0f);
            float mixRatio = Mathf.Clamp(0.25f + shade * 0.5f, 0.0f, 1.0f);
            Color tintedColor = MOVE_REACHABLE_MARKER_COLOR_DARK.Lerp(
                MOVE_REACHABLE_MARKER_COLOR_LIGHT,
                mixRatio
            );
            float alpha = Mathf.Lerp(0.3f, 0.5f, shade);
            image.SetPixel(x, y, new Color(tintedColor.R, tintedColor.G, tintedColor.B, alpha));
        }
        Texture2D generatedTexture = OwnRenderResource(
            ImageTexture.CreateFromImage(image),
            $"move_reachable_marker:{cacheKey}"
        );
        _texture_cache[cacheKey] = generatedTexture;
        return generatedTexture;
    }

    internal Texture2D _load_texture_from_png(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        if (_texture_cache.TryGetValue(path, out Texture2D cachedTexture))
            return cachedTexture;
        Texture2D texture = null;
        if (ResourceLoader.Exists(path, "Texture2D"))
            texture = EngineAssetAccess.ResolveBorrowed<Texture2D>(path);
        if (texture == null && FileAccess.FileExists(path))
        {
            var image = OwnRenderResource(new Image(), $"image_loader:{path}");
            Error error = image.LoadPngFromBuffer(FileAccess.GetFileAsBytes(path));
            if (error == Error.Ok)
                texture = OwnRenderResource(
                    ImageTexture.CreateFromImage(image),
                    $"image_texture:{path}"
                );
        }
        _texture_cache[path] = texture;
        return texture;
    }

    internal Texture2D _build_missing_source_texture(
        StringName source_key,
        BattleBoardTileSourceSpec source_spec
    )
    {
        Color color = source_key switch
        {
            var key when key == SOURCE_WATER => new Color(0.24f, 0.47f, 0.66f, 0.86f),
            var key when key == SOURCE_MUD => new Color(0.43f, 0.31f, 0.18f, 0.88f),
            var key
                when key == SOURCE_EDGE_DROP_EAST
                    || key == SOURCE_EDGE_DROP_SOUTH
                    || key == SOURCE_WALL_EAST
                    || key == SOURCE_WALL_SOUTH => new Color(0.31f, 0.25f, 0.2f, 0.92f),
            var key when key == SOURCE_SCRUB => new Color(0.22f, 0.45f, 0.24f, 0.68f),
            var key when key == SOURCE_RUBBLE => new Color(0.42f, 0.39f, 0.34f, 0.72f),
            var key when key == SOURCE_SELECTED => new Color(0.98f, 0.92f, 0.42f, 0.42f),
            var key when key == SOURCE_PREVIEW => new Color(0.88f, 0.82f, 0.36f, 0.34f),
            _ => new Color(0.5f, 0.42f, 0.32f, 0.9f),
        };
        Vector2I tileSize = source_spec?.BoardTileSize ?? _get_board_tile_size();
        return _build_diamond_texture(color, color.A, tileSize);
    }

    private BattleBoardTileSourceSpec _build_generated_marker_source_spec(
        BattleBoardRenderProfile render_profile
    ) => new(
        SOURCE_ACTIVE_SELECTED,
        Array.Empty<string>(),
        BattleBoardRenderProfile.LAYER_ROLE_MARKER(),
        render_profile.board_tile_size,
        render_profile.board_tile_size,
        Vector2I.Zero,
        Vector2I.Zero,
        allowGeneratedFallback: false
    );

    private Texture2D _build_diamond_texture(Color color, float alpha, Vector2I tile_size)
    {
        Vector2I safeTileSize = new(Mathf.Max(tile_size.X, 2), Mathf.Max(tile_size.Y, 2));
        Image image = OwnRenderResource(
            Image.CreateEmpty(safeTileSize.X, safeTileSize.Y, false, Image.Format.Rgba8),
            $"diamond_image:{tile_size.X}x{tile_size.Y}:{color}"
        );
        image.Fill(new Color(0.0f, 0.0f, 0.0f, 0.0f));
        Vector2 center = new(
            (float)(safeTileSize.X - 1) * 0.5f,
            (float)(safeTileSize.Y - 1) * 0.5f
        );
        Vector2 halfSize = new(
            Mathf.Max((float)safeTileSize.X * 0.5f, 1.0f),
            Mathf.Max((float)safeTileSize.Y * 0.5f, 1.0f)
        );
        for (int y = 0; y < safeTileSize.Y; y++)
        for (int x = 0; x < safeTileSize.X; x++)
        {
            Vector2 delta = new Vector2((float)x, (float)y) - center;
            if (Mathf.Abs(delta.X) / halfSize.X + Mathf.Abs(delta.Y) / halfSize.Y <= 1.0f)
                image.SetPixel(x, y, new Color(color.R, color.G, color.B, alpha));
        }
        return OwnRenderResource(
            ImageTexture.CreateFromImage(image),
            $"diamond_texture:{tile_size.X}x{tile_size.Y}:{color}"
        );
    }

    private void ReplaceSourceIds(IReadOnlyDictionary<StringName, List<int>> sourceIds)
    {
        _source_ids.Clear();
        if (sourceIds == null)
            return;
        foreach ((StringName key, List<int> values) in sourceIds)
            _source_ids[key] = values != null ? new List<int>(values) : new List<int>();
    }

    private static Dictionary<StringName, List<int>> CloneSourceIds(
        IReadOnlyDictionary<StringName, List<int>> sourceIds
    )
    {
        var result = new Dictionary<StringName, List<int>>();
        if (sourceIds == null)
            return result;
        foreach ((StringName key, List<int> values) in sourceIds)
            result[key] = values != null ? new List<int>(values) : new List<int>();
        return result;
    }

    private T OwnRenderResource<T>(T resource, string reason)
        where T : Resource
    {
        if (resource == null)
            return null;
        if (!string.IsNullOrEmpty(resource.ResourcePath))
        {
            GodotContentOwnership.RegisterBorrowedContent(
                resource,
                $"BattleBoardController:{reason}:{resource.ResourcePath}"
            );
            return resource;
        }
        if (GodotWrapperOwnershipRegistry.IsBorrowedOrDerivedStaticContent(resource))
            return resource;
        if (_renderLease?.Owns(resource) == true)
            return resource;
        return EnsureRenderLease().Own(resource, reason);
    }

    private NativeLeaseScope EnsureRenderLease()
    {
        ThrowIfDisposed();
        return _renderLease ??= new NativeLeaseScope(
            "BattleBoardController.render-generation",
            LifetimeDomain.SceneTree
        );
    }

    internal int RenderOwnerCount => _renderLease?.OwnedCount ?? 0;

    internal bool OwnsRenderResource(Resource resource) =>
        resource != null && _renderLease?.Owns(resource) == true;

    internal IReadOnlyList<IDisposable> SnapshotOwnedRenderResources() =>
        _renderLease?.SnapshotOwnedWrappers() ?? Array.Empty<IDisposable>();

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BattleBoardController));
    }

    private float _get_visual_height_step()
    {
        _render_profile ??= BattleBoardRenderProfile.ForTerrainProfileId(_tile_profile_id);
        return _render_profile.visual_height_step;
    }

    private Vector2 _get_unit_anchor_bias()
    {
        _render_profile ??= BattleBoardRenderProfile.ForTerrainProfileId(_tile_profile_id);
        return _render_profile.unit_anchor_bias;
    }

    private Vector2I _get_board_tile_size()
    {
        _render_profile ??= BattleBoardRenderProfile.ForTerrainProfileId(_tile_profile_id);
        return _render_profile.board_tile_size;
    }

    private StringName _resolve_tile_profile_id() =>
        _battle_state == null
            ? BattleBoardRenderProfile.TERRAIN_PROFILE_DEFAULT()
            : BattleBoardRenderProfile.NormalizeTerrainProfileId(
                _battle_state.terrain_profile_id
            );

    public int _get_source_id(StringName source_key) =>
        _get_source_id(source_key, INVALID_OPTION_COORD, 0);

    public int _get_source_id(StringName source_key, Vector2I coord) =>
        _get_source_id(source_key, coord, 0);

    public int _get_source_id(StringName source_key, Vector2I coord, int salt)
    {
        if (_source_ids.TryGetValue(source_key, out List<int> sourceOptions))
        {
            if (sourceOptions.Count == 0)
                return -1;
            if (coord == INVALID_OPTION_COORD || sourceOptions.Count == 1)
                return sourceOptions[0];
            return sourceOptions[_get_variant_index(coord, sourceOptions.Count, salt)];
        }
        return -1;
    }

    private int _get_selected_marker_source_id(Vector2I coord)
    {
        if (_is_active_unit_coord(coord))
        {
            int activeSourceId = _get_source_id(SOURCE_ACTIVE_SELECTED);
            if (activeSourceId >= 0)
                return activeSourceId;
        }
        return _get_source_id(SOURCE_SELECTED);
    }

    private int _get_move_reachable_marker_source_id()
    {
        int moveSourceId = _get_source_id(SOURCE_MOVE_REACHABLE);
        return moveSourceId >= 0 ? moveSourceId : _get_source_id(SOURCE_SELECTED);
    }

    private bool _is_active_unit_coord(Vector2I coord)
    {
        if (_battle_state == null)
            return false;
        BattleUnitState activeUnit = GetUnit(_battle_state, _battle_state.active_unit_id);
        if (activeUnit == null || !activeUnit.is_alive)
            return false;
        return activeUnit.occupied_coords.Contains(coord);
    }

    private int _get_top_source_id(string terrain, Vector2I coord)
    {
        StringName terrainName = terrain;
        if (terrainName == TERRAIN_LAND)
            return _get_source_id(SOURCE_LAND, coord);
        if (terrainName == TERRAIN_FOREST)
            return _get_source_id(SOURCE_FOREST, coord);
        if (
            terrainName == TERRAIN_WATER
            || terrainName == TERRAIN_SHALLOW_WATER
            || terrainName == TERRAIN_FLOWING_WATER
            || terrainName == TERRAIN_DEEP_WATER
        )
            return _get_source_id(SOURCE_WATER, coord);
        if (terrainName == TERRAIN_MUD)
            return _get_source_id(SOURCE_MUD, coord);
        if (terrainName == TERRAIN_SPIKE)
            return _get_source_id(SOURCE_LAND, coord, 2);
        return _get_source_id(SOURCE_LAND, coord);
    }

    private int _get_overlay_source_id(string terrain, Vector2I coord)
    {
        int timedOverlaySourceId = _get_timed_terrain_overlay_source_id(coord);
        if (timedOverlaySourceId >= 0)
            return timedOverlaySourceId;
        StringName terrainName = terrain;
        if (terrainName == TERRAIN_FOREST)
            return _get_source_id(SOURCE_FOREST_TREE, coord);
        if (terrainName == TERRAIN_SPIKE)
            return _get_source_id(SOURCE_RUBBLE, coord);
        return -1;
    }

    private int _get_timed_terrain_overlay_source_id(Vector2I coord)
    {
        if (_battle_state == null)
            return -1;
        BattleCellState cell = GetCell(_battle_state, coord);
        if (cell == null || cell.timed_terrain_effects.Count == 0)
            return -1;
        int bestSourceId = -1;
        int bestPriority = int.MinValue;
        string bestSourceKey = "";
        foreach (BattleTerrainEffectState effectState in cell.timed_terrain_effects)
        {
            if (
                effectState == null
                || !BattleTerrainEffectSystem.IsTerrainEffectActive(effectState)
            )
                continue;
            StringName overlayId = effectState.render_overlay_id;
            if (overlayId == "")
                continue;
            int sourceId = _get_source_id(overlayId, coord);
            if (sourceId < 0)
                continue;
            int priority = effectState.overlay_priority;
            string sourceKey = overlayId.ToString();
            if (
                priority > bestPriority
                || (priority == bestPriority && string.CompareOrdinal(sourceKey, bestSourceKey) < 0)
            )
            {
                bestPriority = priority;
                bestSourceKey = sourceKey;
                bestSourceId = sourceId;
            }
        }
        return bestSourceId;
    }

    internal static int GetVariantIndexForTest(int hashValue, int optionCount) =>
        GetNonNegativeVariantIndex(hashValue, optionCount);

    private int _get_variant_index(Vector2I coord, int option_count, int salt = 0) =>
        GetNonNegativeVariantIndex(_build_coord_hash(coord, salt), option_count);

    private static int GetNonNegativeVariantIndex(int hashValue, int optionCount)
    {
        if (optionCount <= 1)
            return 0;
        long remainder = (long)hashValue % optionCount;
        return (int)(remainder < 0 ? remainder + optionCount : remainder);
    }

    private int _build_coord_hash(Vector2I coord, int salt = 0)
    {
        int hashValue = coord.X * 73856093;
        hashValue += coord.Y * 19349663;
        hashValue += (int)StringExtensions.Hash(_tile_profile_id.ToString()) * 83492791;
        hashValue += salt * 1640531513;
        return Mathf.Abs(hashValue);
    }

    private string _build_unit_short_name(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return "?";
        if (!string.IsNullOrEmpty(unit_state.display_name))
            return unit_state.display_name.Substring(0, 1);
        string unitId = unit_state.unit_id.ToString();
        return unitId.Length > 0 ? unitId.Substring(0, 1) : "?";
    }

    private int _get_unit_hp_max(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return 1;
        int snapshotHpMax =
            unit_state.attribute_snapshot != null
                ? unit_state.attribute_snapshot.GetValue(HP_MAX_ATTRIBUTE_ID)
                : 0;
        return Mathf.Max(Mathf.Max(snapshotHpMax, (int)unit_state.current_hp), 1);
    }

    private Color _get_unit_health_bar_fill_color(float hp_ratio)
    {
        float clampedRatio = Mathf.Clamp(hp_ratio, 0.0f, 1.0f);
        if (clampedRatio <= 0.35f)
            return UNIT_HEALTH_BAR_LOW_COLOR.Lerp(
                UNIT_HEALTH_BAR_MID_COLOR,
                Mathf.InverseLerp(0.0f, 0.35f, clampedRatio)
            );
        if (clampedRatio <= 0.7f)
            return UNIT_HEALTH_BAR_MID_COLOR;
        return UNIT_HEALTH_BAR_MID_COLOR.Lerp(
            UNIT_HEALTH_BAR_HIGH_COLOR,
            Mathf.InverseLerp(0.7f, 1.0f, clampedRatio)
        );
    }

    private Color _get_unit_color(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return new Color(0.78f, 0.8f, 0.84f, 0.94f);
        if (unit_state.faction_id.ToString() == "player")
            return new Color(0.96f, 0.86f, 0.38f, 0.96f);
        if (unit_state.faction_id.ToString() == "hostile")
            return new Color(0.9f, 0.32f, 0.22f, 0.96f);
        return new Color(0.7f, 0.74f, 0.78f, 0.92f);
    }

    private bool _is_cell_inside_battle(Vector2I coord) =>
        _battle_state != null
        && coord.X >= 0
        && coord.Y >= 0
        && coord.X < _battle_state.map_size.X
        && coord.Y < _battle_state.map_size.Y;

    private static void ReplaceLayers(
        List<TileMapLayer> destination,
        IEnumerable<TileMapLayer> values
    )
    {
        destination.Clear();
        if (values == null)
            return;
        foreach (TileMapLayer value in values)
            destination.Add(value);
    }

    private static void ReplaceCoords(List<Vector2I> destination, IEnumerable<Vector2I> values)
    {
        destination.Clear();
        if (values == null)
            return;
        destination.AddRange(values);
    }

    private static void ClearLayers(
        IEnumerable<TileMapLayer> layers,
        List<Exception> failures
    )
    {
        foreach (TileMapLayer layer in layers)
            ExecuteCleanup(() => layer?.Clear(), failures);
    }

    private static void ClearTileSets(
        IEnumerable<TileMapLayer> layers,
        List<Exception> failures
    )
    {
        foreach (TileMapLayer layer in layers)
        {
            ExecuteCleanup(
                () =>
                {
                    if (layer != null)
                        layer.TileSet = null;
                },
                failures
            );
        }
    }

    private static void ExecuteCleanup(Action cleanup, List<Exception> failures)
    {
        if (failures == null)
        {
            cleanup();
            return;
        }
        TryCleanup(cleanup, failures);
    }

    private void ApplyTileSet(IEnumerable<TileMapLayer> layers)
    {
        foreach (TileMapLayer layer in layers)
            if (layer != null)
                layer.TileSet = _tile_set;
    }

    private static BattleUnitState GetUnit(BattleState state, StringName key)
    {
        return state?.GetUnit(key);
    }

    private static BattleCellState GetCell(BattleState state, Vector2I coord) =>
        state?.GetCell(coord);

    private static float CalcLuminance(Color color)
    {
        return color.R * 0.2126f + color.G * 0.7152f + color.B * 0.0722f;
    }

}
