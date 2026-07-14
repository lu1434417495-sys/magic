using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class BattleBoard2D : Node2D
{
    [Signal]
    public delegate void battle_cell_clickedEventHandler(Vector2I coord);

    [Signal]
    public delegate void battle_cell_right_clickedEventHandler(Vector2I coord);

    [Signal]
    public delegate void battle_cell_hoveredEventHandler(Vector2I coord);

    private const int MAX_RENDER_HEIGHT = 8;
    private const float DEFAULT_CAMERA_ZOOM = 2.0f;
    private const float MIN_CAMERA_ZOOM = 1.25f;
    private const float MAX_CAMERA_ZOOM = 4.0f;
    private const float CAMERA_ZOOM_STEP = 0.2f;
    private const float KEYBOARD_CAMERA_PAN_STEP = 96.0f;
    private static readonly Vector2 FOCUS_VIEWPORT_RATIO = new(0.5f, 0.44f);

    private const int MOUSE_BUTTON_LEFT_VALUE = 1;
    private const int MOUSE_BUTTON_RIGHT_VALUE = 2;
    private const int MOUSE_BUTTON_MASK_MIDDLE_VALUE = 4;

    public TileMapLayer input_layer;
    public List<TileMapLayer> top_layers = new();
    public List<TileMapLayer> edge_drop_east_layers = new();
    public List<TileMapLayer> edge_drop_south_layers = new();
    public List<TileMapLayer> wall_east_layers = new();
    public List<TileMapLayer> wall_south_layers = new();
    public List<TileMapLayer> overlay_layers = new();
    public List<TileMapLayer> marker_layers = new();
    public Node2D prop_layer;
    public Node2D unit_layer;
    public Node2D target_highlight_layer;

    public BattleBoardController _controller = new();
    public BattleBoardRenderProfile _render_profile =
        BattleBoardRenderProfile.ForTerrainProfileId(
            BattleBoardRenderProfile.TERRAIN_PROFILE_DEFAULT()
        );
    public bool _is_bound;
    public BattleState _pending_battle_state;
    public Vector2I _pending_selected_coord = new(-1, -1);
    public List<Vector2I> _pending_preview_target_coords = new();
    public List<Vector2I> _pending_valid_target_coords = new();
    public StringName _pending_target_selection_mode = "single_unit";
    public int _pending_target_min_count = 1;
    public int _pending_target_max_count = 1;
    public Dictionary<Vector2I, string> _pending_target_hit_badges = new();
    public Vector2 _viewport_size = Vector2.Zero;
    public float _camera_zoom = DEFAULT_CAMERA_ZOOM;
    public Rect2 _content_bounds = new();
    public bool _has_content_bounds;
    public bool _camera_initialized;
    public Vector2I _last_focus_coord = new(-9999, -9999);
    public bool _has_manual_zoom_override;
    public bool _is_panning;
    public Vector2 _last_pan_viewport_position = Vector2.Zero;
    public Vector2I _hovered_coord = new(-1, -1);

    public override void _Ready()
    {
        YSortEnabled = false;
        Scale = Vector2.One * _camera_zoom;
        input_layer = GetNode<TileMapLayer>("%InputLayer");
        top_layers = _collect_tile_layers("TopH", 0, MAX_RENDER_HEIGHT);
        edge_drop_east_layers = _collect_tile_layers("EdgeDropEastH", 1, MAX_RENDER_HEIGHT);
        edge_drop_south_layers = _collect_tile_layers("EdgeDropSouthH", 1, MAX_RENDER_HEIGHT);
        wall_east_layers = _collect_tile_layers("WallEastH", 0, MAX_RENDER_HEIGHT);
        wall_south_layers = _collect_tile_layers("WallSouthH", 0, MAX_RENDER_HEIGHT);
        overlay_layers = _collect_tile_layers("OverlayH", 0, MAX_RENDER_HEIGHT);
        marker_layers = _collect_tile_layers("MarkerH", 0, MAX_RENDER_HEIGHT);
        prop_layer = GetNode<Node2D>("%PropLayer");
        unit_layer = GetNode<Node2D>("%UnitLayer");
        target_highlight_layer = GetNode<Node2D>("%TargetHighlightLayer");
        _bind_controller();
        _apply_pending_configuration();
    }

    public override void _ExitTree()
    {
        _controller?.Dispose();
        _controller = null;
        _pending_battle_state = null;
        _pending_preview_target_coords.Clear();
        _pending_valid_target_coords.Clear();
        _pending_target_hit_badges.Clear();
        input_layer = null;
        top_layers.Clear();
        edge_drop_east_layers.Clear();
        edge_drop_south_layers.Clear();
        wall_east_layers.Clear();
        wall_south_layers.Clear();
        overlay_layers.Clear();
        marker_layers.Clear();
        prop_layer = null;
        unit_layer = null;
        target_highlight_layer = null;
        _render_profile = null;
        _is_bound = false;
    }

    public void Configure(
        BattleState battle_state,
        Vector2I selected_coord,
        IEnumerable<Vector2I> preview_target_coords = null,
        IEnumerable<Vector2I> valid_target_coords = null,
        StringName target_selection_mode = default,
        int min_target_count = 1,
        int max_target_count = 1,
        IReadOnlyDictionary<Vector2I, string> target_hit_badges = null
    )
    {
        _pending_battle_state = battle_state;
        _set_render_profile_for_state(battle_state);
        _pending_selected_coord = selected_coord;
        _pending_preview_target_coords = CloneCoords(preview_target_coords);
        _pending_valid_target_coords = CloneCoords(valid_target_coords);
        _pending_target_selection_mode = NormalizeTargetSelectionMode(target_selection_mode);
        _pending_target_min_count = Mathf.Max(min_target_count, 1);
        _pending_target_max_count = Mathf.Max(max_target_count, _pending_target_min_count);
        _pending_target_hit_badges = CloneHitBadges(target_hit_badges);
        _apply_pending_configuration();
        _fit_to_viewport(true);
    }

    public void UpdateSelection(
        Vector2I selected_coord,
        IEnumerable<Vector2I> preview_target_coords = null,
        IEnumerable<Vector2I> valid_target_coords = null,
        StringName target_selection_mode = default,
        int min_target_count = 1,
        int max_target_count = 1,
        IReadOnlyDictionary<Vector2I, string> target_hit_badges = null
    )
    {
        _pending_selected_coord = selected_coord;
        _pending_preview_target_coords = CloneCoords(preview_target_coords);
        _pending_valid_target_coords = CloneCoords(valid_target_coords);
        _pending_target_selection_mode = NormalizeTargetSelectionMode(target_selection_mode);
        _pending_target_min_count = Mathf.Max(min_target_count, 1);
        _pending_target_max_count = Mathf.Max(max_target_count, _pending_target_min_count);
        _pending_target_hit_badges = CloneHitBadges(target_hit_badges);
        _apply_pending_marker_update();
    }

    public void RefreshUnits(
        BattleState battle_state,
        IEnumerable<StringName> changed_unit_ids
    )
    {
        _pending_battle_state = battle_state;
        if (!_is_bound)
            _bind_controller();
        if (!_is_bound)
            return;
        _controller.RefreshUnits(battle_state, changed_unit_ids);
    }

    public void SetViewportSize(Vector2 viewport_size)
    {
        _viewport_size = viewport_size;
        _fit_to_viewport();
    }

    public void BeginViewportPan(Vector2 viewport_position)
    {
        _is_panning = true;
        _last_pan_viewport_position = viewport_position;
    }

    public void EndViewportPan()
    {
        _is_panning = false;
    }

    public bool IsViewportPanning()
    {
        return _is_panning;
    }

    public bool HandleViewportMouseMotion(Vector2 viewport_position, int button_mask)
    {
        if (!_is_panning)
            return _update_hovered_coord(viewport_position);

        if ((button_mask & MOUSE_BUTTON_MASK_MIDDLE_VALUE) == 0)
        {
            _is_panning = false;
            return false;
        }

        Vector2 delta = viewport_position - _last_pan_viewport_position;
        _last_pan_viewport_position = viewport_position;
        Position += delta;
        _clamp_camera_position();
        return true;
    }

    public bool ZoomViewport(int step, Vector2 viewport_position)
    {
        float nextZoom = Mathf.Clamp(
            _camera_zoom + (float)step * CAMERA_ZOOM_STEP,
            MIN_CAMERA_ZOOM,
            MAX_CAMERA_ZOOM
        );
        if (Mathf.IsEqualApprox(nextZoom, _camera_zoom))
            return false;

        Vector2 localAnchor = (viewport_position - Position) / _camera_zoom;
        _camera_zoom = nextZoom;
        _has_manual_zoom_override = true;
        Scale = Vector2.One * _camera_zoom;
        Position = viewport_position - localAnchor * _camera_zoom;
        _camera_initialized = true;
        _clamp_camera_position();
        return true;
    }

    public bool PanViewportDirection(Vector2I direction)
    {
        if (direction == Vector2I.Zero)
            return false;

        Vector2 previousPosition = Position;
        Position -= new Vector2(direction.X, direction.Y) * KEYBOARD_CAMERA_PAN_STEP;
        _camera_initialized = true;
        _clamp_camera_position();
        return !Position.IsEqualApprox(previousPosition);
    }

    public bool HandleViewportMouseButton(Vector2 viewport_position, int button_index)
    {
        if (_controller == null || _pending_battle_state == null || _is_panning)
            return false;

        Vector2I clickedCoord = _viewport_position_to_board_coord(viewport_position);
        if (clickedCoord == new Vector2I(-1, -1))
            return false;

        if (button_index == MOUSE_BUTTON_LEFT_VALUE)
        {
            EmitSignal(SignalName.battle_cell_clicked, clickedCoord);
            return true;
        }

        if (button_index == MOUSE_BUTTON_RIGHT_VALUE)
        {
            EmitSignal(SignalName.battle_cell_right_clicked, clickedCoord);
            return true;
        }

        return false;
    }

    public void ClearBoard()
    {
        _pending_battle_state = null;
        _render_profile = BattleBoardRenderProfile.ForTerrainProfileId(
            BattleBoardRenderProfile.TERRAIN_PROFILE_DEFAULT()
        );
        _pending_selected_coord = new Vector2I(-1, -1);
        _pending_preview_target_coords.Clear();
        _pending_valid_target_coords.Clear();
        _pending_target_hit_badges.Clear();
        _pending_target_selection_mode = "single_unit";
        _pending_target_min_count = 1;
        _pending_target_max_count = 1;
        _is_panning = false;
        _has_content_bounds = false;
        _camera_initialized = false;
        _has_manual_zoom_override = false;
        _camera_zoom = DEFAULT_CAMERA_ZOOM;
        _last_focus_coord = new Vector2I(-9999, -9999);
        _set_hovered_coord(new Vector2I(-1, -1));
        try
        {
            _controller?.Clear();
        }
        finally
        {
            _is_bound = false;
        }
        _fit_to_viewport();
    }

    public bool IsRenderContentReady()
    {
        return _is_bound
            && _pending_battle_state != null
            && _controller != null
            && _controller.IsRenderContentReady();
    }

    public Vector2 CoordToViewportPosition(Vector2I coord)
    {
        if (_pending_battle_state == null || input_layer == null)
            return new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        if (!_pending_battle_state.ContainsCell(coord))
            return new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        Vector2 anchor = _get_coord_anchor(coord);
        return Position + anchor * _camera_zoom;
    }

    public bool IsCoordInViewport(Vector2I coord)
    {
        if (_viewport_size == Vector2.Zero)
            return false;

        Vector2 viewportPosition = CoordToViewportPosition(coord);
        if (
            float.IsNegativeInfinity(viewportPosition.X)
            || float.IsNegativeInfinity(viewportPosition.Y)
        )
            return false;

        return viewportPosition.X >= 0.0f
            && viewportPosition.Y >= 0.0f
            && viewportPosition.X <= _viewport_size.X
            && viewportPosition.Y <= _viewport_size.Y;
    }

    private void _bind_controller()
    {
        if (_is_bound || _controller == null)
            return;

        _controller.BindLayers(
            input_layer,
            top_layers,
            edge_drop_east_layers,
            edge_drop_south_layers,
            wall_east_layers,
            wall_south_layers,
            overlay_layers,
            marker_layers,
            prop_layer,
            unit_layer,
            target_highlight_layer
        );
        _is_bound = true;
    }

    private void _apply_pending_configuration()
    {
        if (!_is_bound)
            _bind_controller();
        if (!_is_bound)
            return;

        _controller.Configure(
            _pending_battle_state,
            _pending_selected_coord,
            _pending_preview_target_coords,
            _pending_target_selection_mode,
            _pending_target_min_count,
            _pending_target_max_count,
            _pending_target_hit_badges
        );
        _controller.UpdateMarkers(
            _pending_selected_coord,
            _pending_preview_target_coords,
            _pending_valid_target_coords,
            _pending_target_selection_mode,
            _pending_target_min_count,
            _pending_target_max_count,
            _pending_target_hit_badges
        );
        _refresh_content_bounds();
        _fit_to_viewport();
    }

    private void _apply_pending_marker_update()
    {
        if (!_is_bound || _pending_battle_state == null)
            return;

        _controller.UpdateMarkers(
            _pending_selected_coord,
            _pending_preview_target_coords,
            _pending_valid_target_coords,
            _pending_target_selection_mode,
            _pending_target_min_count,
            _pending_target_max_count,
            _pending_target_hit_badges
        );
    }

    private void _set_render_profile_for_state(BattleState battle_state)
    {
        StringName terrainProfileId = BattleBoardRenderProfile.TERRAIN_PROFILE_DEFAULT();
        if (battle_state != null)
            terrainProfileId = battle_state.terrain_profile_id;
        _render_profile = BattleBoardRenderProfile.ForTerrainProfileId(terrainProfileId);
    }

    private Vector2I _viewport_position_to_board_coord(Vector2 viewport_position)
    {
        if (input_layer == null || _pending_battle_state == null)
            return new Vector2I(-1, -1);

        Vector2 boardLocal = ToLocal(viewport_position);
        Vector2I visualCoord = _pick_visual_surface_coord(boardLocal);
        if (visualCoord != new Vector2I(-1, -1))
            return visualCoord;

        Vector2 inputLocal = input_layer.ToLocal(ToGlobal(boardLocal));
        Vector2I coord = input_layer.LocalToMap(inputLocal);
        if (!_pending_battle_state.ContainsCell(coord))
            return new Vector2I(-1, -1);
        return coord;
    }

    private bool _update_hovered_coord(Vector2 viewport_position)
    {
        if (_pending_battle_state == null)
            return _set_hovered_coord(new Vector2I(-1, -1));
        return _set_hovered_coord(_viewport_position_to_board_coord(viewport_position));
    }

    private bool _set_hovered_coord(Vector2I coord)
    {
        if (coord == _hovered_coord)
            return false;

        _hovered_coord = coord;
        EmitSignal(SignalName.battle_cell_hovered, _hovered_coord);
        return true;
    }

    private Vector2I _pick_visual_surface_coord(Vector2 board_local)
    {
        if (_pending_battle_state == null || _pending_battle_state.CellCount == 0)
            return new Vector2I(-1, -1);

        Vector2I bestCoord = new(-1, -1);
        double bestSortKey = -1e20;
        float bestDistanceSq = float.PositiveInfinity;
        foreach (BattleCellState cellState in _pending_battle_state.Cells())
        {
            if (cellState == null)
                continue;

            Vector2 anchor = _get_coord_anchor(cellState.coord);
            if (!_point_hits_cell_top_surface(board_local, anchor))
                continue;

            Vector2 planeAnchor = input_layer.MapToLocal(cellState.coord);
            float sortKey = _build_visual_pick_sort_key(
                (int)cellState.current_height,
                planeAnchor.Y
            );
            float distanceSq = board_local.DistanceSquaredTo(anchor);
            if (sortKey > bestSortKey)
            {
                bestCoord = cellState.coord;
                bestSortKey = sortKey;
                bestDistanceSq = distanceSq;
                continue;
            }

            if (Mathf.IsEqualApprox(sortKey, (float)bestSortKey) && distanceSq < bestDistanceSq)
            {
                bestCoord = cellState.coord;
                bestDistanceSq = distanceSq;
            }
        }
        return bestCoord;
    }

    private bool _point_hits_cell_top_surface(Vector2 point, Vector2 anchor)
    {
        return _render_profile.PointHitsTopSurface(point, anchor);
    }

    private float _build_visual_pick_sort_key(int height_value, float plane_anchor_y)
    {
        int clampedHeight = Mathf.Clamp(height_value, 0, MAX_RENDER_HEIGHT);
        return (float)clampedHeight * 1000000.0f + plane_anchor_y;
    }

    private void _fit_to_viewport(bool force_focus = false)
    {
        if (_viewport_size == Vector2.Zero)
            return;

        if (_pending_battle_state == null || _pending_battle_state.CellCount == 0)
        {
            Scale = Vector2.One * _camera_zoom;
            Position = _viewport_size * 0.5f;
            return;
        }

        Scale = Vector2.One * _camera_zoom;
        if (!_has_content_bounds)
            _refresh_content_bounds();
        if (!_has_content_bounds)
        {
            Position = _viewport_size * 0.5f;
            return;
        }

        if (!_has_manual_zoom_override)
            _camera_zoom = _compute_horizontal_fit_zoom();

        Vector2I focusCoord = _resolve_focus_coord();
        Scale = Vector2.One * _camera_zoom;
        if (force_focus || !_camera_initialized || focusCoord != _last_focus_coord)
        {
            _center_camera_on_coord(focusCoord);
            _last_focus_coord = focusCoord;
            _camera_initialized = true;
        }
        else
        {
            _clamp_camera_position();
        }
    }

    private void _refresh_content_bounds()
    {
        _has_content_bounds = false;
        _content_bounds = new Rect2();
        if (_pending_battle_state == null || _pending_battle_state.CellCount == 0)
            return;

        foreach (BattleCellState cellState in _pending_battle_state.Cells())
        {
            if (cellState == null)
                continue;

            Vector2 anchor = _get_coord_anchor(cellState.coord);
            Vector2 tileHalfSize = _render_profile.tile_half_size;
            Rect2 cellRect = new(anchor - tileHalfSize, tileHalfSize * 2.0f);
            if (!_has_content_bounds)
            {
                _content_bounds = cellRect;
                _has_content_bounds = true;
            }
            else
            {
                _content_bounds = _content_bounds.Merge(cellRect);
            }
        }

        if (_has_content_bounds)
        {
            Vector4 boundsMargin = _render_profile.content_bounds_margin;
            _content_bounds = _content_bounds.GrowIndividual(
                boundsMargin.X,
                boundsMargin.Y,
                boundsMargin.Z,
                boundsMargin.W
            );
        }
    }

    private Vector2I _resolve_focus_coord()
    {
        if (_pending_battle_state == null)
            return Vector2I.Zero;

        if (
            _pending_selected_coord != new Vector2I(-1, -1)
            && _pending_battle_state.ContainsCell(_pending_selected_coord)
        )
            return _pending_selected_coord;

        BattleUnitState activeUnit = GetUnit(
            _pending_battle_state,
            _pending_battle_state.active_unit_id
        );
        if (
            activeUnit != null
            && activeUnit.is_alive
            && _pending_battle_state.ContainsCell(activeUnit.coord)
        )
            return activeUnit.coord;

        foreach (StringName allyUnitId in _pending_battle_state.ally_unit_ids)
        {
            BattleUnitState allyUnit = GetUnit(_pending_battle_state, allyUnitId);
            if (
                allyUnit != null
                && allyUnit.is_alive
                && _pending_battle_state.ContainsCell(allyUnit.coord)
            )
                return allyUnit.coord;
        }

        foreach ((Vector2I coord, BattleCellState _) in _pending_battle_state.CellEntries())
            return coord;
        return Vector2I.Zero;
    }

    public Vector2 _get_coord_anchor(Vector2I coord)
    {
        Vector2 anchor = input_layer.MapToLocal(coord);
        BattleCellState cellState = _pending_battle_state?.GetCell(coord);

        if (cellState != null)
            anchor.Y -=
                (float)Mathf.Clamp((int)cellState.current_height, 0, MAX_RENDER_HEIGHT)
                * _render_profile.visual_height_step;
        return anchor;
    }

    private Vector2 _get_focus_viewport_position()
    {
        return new Vector2(
            _viewport_size.X * FOCUS_VIEWPORT_RATIO.X,
            _viewport_size.Y * FOCUS_VIEWPORT_RATIO.Y
        );
    }

    private void _center_camera_on_coord(Vector2I coord)
    {
        Position = _get_focus_viewport_position() - _get_coord_anchor(coord) * _camera_zoom;
        _clamp_camera_position();
    }

    private void _clamp_camera_position()
    {
        if (_viewport_size == Vector2.Zero || !_has_content_bounds)
            return;

        Vector2 boundsEnd = _content_bounds.Position + _content_bounds.Size;
        Position = new Vector2(
            _clamp_camera_axis(
                Position.X,
                _content_bounds.Position.X,
                boundsEnd.X,
                _viewport_size.X,
                _render_profile.camera_margin.X
            ),
            _clamp_camera_axis(
                Position.Y,
                _content_bounds.Position.Y,
                boundsEnd.Y,
                _viewport_size.Y,
                _render_profile.camera_margin.Y
            )
        );
    }

    private float _compute_horizontal_fit_zoom()
    {
        if (!_has_content_bounds)
            return DEFAULT_CAMERA_ZOOM;

        float availableWidth = Mathf.Max(
            _viewport_size.X - _render_profile.camera_margin.X * 2.0f,
            1.0f
        );
        float contentWidth = Mathf.Max(_content_bounds.Size.X, 1.0f);
        return Mathf.Clamp(availableWidth / contentWidth, MIN_CAMERA_ZOOM, MAX_CAMERA_ZOOM);
    }

    private float _clamp_camera_axis(
        float current_position,
        float bounds_start,
        float bounds_end,
        float viewport_extent,
        float margin
    )
    {
        float minPosition = viewport_extent - margin - bounds_end * _camera_zoom;
        float maxPosition = margin - bounds_start * _camera_zoom;
        if (minPosition > maxPosition)
            return (viewport_extent - (bounds_start + bounds_end) * _camera_zoom) * 0.5f;
        return Mathf.Clamp(current_position, minPosition, maxPosition);
    }

    private List<TileMapLayer> _collect_tile_layers(string prefix, int start_height, int end_height)
    {
        var layers = new List<TileMapLayer>();
        for (int height = start_height; height <= end_height; height++)
        {
            TileMapLayer layer = GetNodeOrNull<TileMapLayer>($"{prefix}{height}");
            if (layer != null)
                layers.Add(layer);
        }
        return layers;
    }

    private static List<Vector2I> CloneCoords(IEnumerable<Vector2I> values)
        => values != null ? new List<Vector2I>(values) : new List<Vector2I>();

    private static Dictionary<Vector2I, string> CloneHitBadges(
        IReadOnlyDictionary<Vector2I, string> values
    )
    {
        var result = new Dictionary<Vector2I, string>();
        if (values == null)
            return result;
        foreach ((Vector2I coord, string badgeText) in values)
        {
            if (!string.IsNullOrEmpty(badgeText))
                result[coord] = badgeText;
        }
        return result;
    }

    private static StringName NormalizeTargetSelectionMode(StringName value)
    {
        return value == "" ? new StringName("single_unit") : value;
    }

    private static BattleUnitState GetUnit(BattleState battleState, StringName unitId)
    {
        return battleState?.GetUnit(unitId);
    }
}
