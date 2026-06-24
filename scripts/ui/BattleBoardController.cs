using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GIntArray = Godot.Collections.Array<int>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GTileLayerArray = Godot.Collections.Array<Godot.TileMapLayer>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public class BattleBoardController
{
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
    private static readonly StringName SOURCE_PREVIEW = "preview";
    private static readonly Vector2I INVALID_OPTION_COORD = new(-999999, -999999);
    private static readonly StringName PROP_SPIKE_BARRICADE = "spike_barricade";
    private static readonly PackedScene BattleBoardPropScene = GD.Load<PackedScene>(
        "res://scenes/common/battle_board_prop.tscn"
    );

    public TileMapLayer _input_layer;
    public GTileLayerArray _top_layers = new();
    public GTileLayerArray _edge_drop_east_layers = new();
    public GTileLayerArray _edge_drop_south_layers = new();
    public GTileLayerArray _wall_east_layers = new();
    public GTileLayerArray _wall_south_layers = new();
    public GTileLayerArray _overlay_layers = new();
    public GTileLayerArray _marker_layers = new();
    public Node2D _prop_layer;
    public Node2D _unit_layer;
    public Node2D _target_highlight_layer;
    public TileSet _tile_set;
    public GDictionary _source_ids = new();
    public StringName _tile_profile_id = "";
    public BattleBoardRenderProfile _render_profile;
    public GDictionary _texture_cache = new();
    public GDictionary _tileset_cache = new();
    public BattleEdgeService _edge_service = new();
    public BattleState _battle_state;
    public Vector2I _selected_coord = new(-1, -1);
    public GVector2IArray _preview_target_coords = new();
    public GVector2IArray _valid_target_coords = new();
    public StringName _target_selection_mode = "single_unit";
    public int _target_min_count = 1;
    public int _target_max_count = 1;
    public GDictionary _target_hit_badges = new();

    public void BindLayers(
        TileMapLayer input_layer,
        GTileLayerArray top_layers,
        GTileLayerArray edge_drop_east_layers,
        GTileLayerArray edge_drop_south_layers,
        GTileLayerArray wall_east_layers,
        GTileLayerArray wall_south_layers,
        GTileLayerArray overlay_layers,
        GTileLayerArray marker_layers,
        Node2D prop_layer,
        Node2D unit_layer,
        Node2D target_highlight_layer
    )
    {
        _input_layer = input_layer;
        _top_layers = CloneLayerArray(top_layers);
        _edge_drop_east_layers = CloneLayerArray(edge_drop_east_layers);
        _edge_drop_south_layers = CloneLayerArray(edge_drop_south_layers);
        _wall_east_layers = CloneLayerArray(wall_east_layers);
        _wall_south_layers = CloneLayerArray(wall_south_layers);
        _overlay_layers = CloneLayerArray(overlay_layers);
        _marker_layers = CloneLayerArray(marker_layers);
        _prop_layer = prop_layer;
        _unit_layer = unit_layer;
        _target_highlight_layer = target_highlight_layer;
        _ensure_tileset(BattleBoardRenderProfile.TERRAIN_PROFILE_DEFAULT());
        _apply_tileset_to_layers();
        _apply_layer_offsets();
        _apply_layer_draw_order();
    }

    public void Configure(
        BattleState battle_state,
        Vector2I selected_coord,
        GVector2IArray preview_target_coords,
        StringName target_selection_mode,
        int min_target_count,
        int max_target_count,
        GDictionary target_hit_badges
    )
    {
        _battle_state = battle_state;
        _selected_coord = selected_coord;
        _preview_target_coords = CloneVector2IArray(preview_target_coords);
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
        GVector2IArray preview_target_coords,
        GVector2IArray valid_target_coords,
        StringName target_selection_mode,
        int min_target_count,
        int max_target_count,
        GDictionary target_hit_badges
    )
    {
        _selected_coord = selected_coord;
        _preview_target_coords = CloneVector2IArray(preview_target_coords);
        _valid_target_coords = CloneVector2IArray(valid_target_coords);
        _target_selection_mode =
            target_selection_mode == "" ? new StringName("single_unit") : target_selection_mode;
        _target_min_count = Mathf.Max(min_target_count, 1);
        _target_max_count = Mathf.Max(max_target_count, _target_min_count);
        _set_target_hit_badges(target_hit_badges);
        _draw_marker_layer();
        _draw_target_highlights();
    }

    public void Clear()
    {
        _battle_state = null;
        _selected_coord = new Vector2I(-1, -1);
        _preview_target_coords.Clear();
        _valid_target_coords.Clear();
        _target_hit_badges.Clear();
        _clear_tile_layers();
        _clear_dynamic_nodes();
    }

    public void _refresh_tileset_profile()
    {
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
        GTileLayerArray layers =
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
        GTileLayerArray layers =
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

    private void _draw_props(List<BattleCellState> cells)
    {
        if (_prop_layer == null || _battle_state == null)
            return;
        foreach (BattleCellState cellState in cells)
        {
            if (cellState == null || !_is_cell_inside_battle(cellState.coord))
                continue;
            GStringNameArray propIds = _collect_prop_ids_for_cell(cellState);
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
            unitState.RefreshFootprint();
            Node2D unitNode = _create_unit_token(unitState);
            if (unitNode != null)
                _unit_layer.AddChild(unitNode);
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
        if (unit_state.battle_sprite_texture != null)
        {
            _attach_unit_sprite_visuals(token, unit_state);
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
            unit_state.battle_sprite_texture == null
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
        };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", new Color(0.98f, 0.96f, 0.9f, 0.98f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0.16f, 0.1f, 0.06f, 0.92f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        token.AddChild(label);
        Control healthBar = _create_unit_health_bar(unit_state);
        if (healthBar != null)
            token.AddChild(healthBar);
        return token;
    }

    private void _attach_unit_sprite_visuals(Node2D token, BattleUnitState unit_state)
    {
        if (token == null || unit_state == null || unit_state.battle_sprite_texture == null)
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
        Vector2 textureSize = unit_state.battle_sprite_texture.GetSize();
        if (textureSize.X <= 0.0f || textureSize.Y <= 0.0f)
            return;
        Vector2I tileSize = _get_board_tile_size();
        float targetWidth = Mathf.Max((float)tileSize.X * UNIT_SPRITE_TILE_WIDTH_RATIO, 1.0f);
        float spriteScale = targetWidth / textureSize.X;
        var sprite = new Sprite2D
        {
            Name = "UnitSprite",
            Texture = unit_state.battle_sprite_texture,
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
        var panelStyle = new StyleBoxFlat
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
        };
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
        unit_state.RefreshFootprint();
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
        unit_state.RefreshFootprint();
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

    private void _clear_tile_layers()
    {
        ClearLayers(_top_layers);
        ClearLayers(_edge_drop_east_layers);
        ClearLayers(_edge_drop_south_layers);
        ClearLayers(_wall_east_layers);
        ClearLayers(_wall_south_layers);
        ClearLayers(_overlay_layers);
        _clear_marker_layers();
    }

    private void _clear_marker_layers() => ClearLayers(_marker_layers);

    private void _clear_dynamic_nodes()
    {
        _clear_child_nodes(_prop_layer);
        _clear_child_nodes(_unit_layer);
        _clear_child_nodes(_target_highlight_layer);
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

    private void _set_target_hit_badges(GDictionary target_hit_badges)
    {
        _target_hit_badges.Clear();
        if (target_hit_badges == null)
            return;
        foreach (var coordValue in target_hit_badges.Keys)
        {
            if (coordValue.VariantType != Variant.Type.Vector2I)
                continue;
            Vector2I coord = coordValue.AsVector2I();
            string badgeText = DictString(target_hit_badges, coordValue);
            if (!string.IsNullOrEmpty(badgeText))
                _target_hit_badges[coord] = badgeText;
        }
    }

    private void _draw_target_hit_badges()
    {
        if (_target_highlight_layer == null || _target_hit_badges.Count == 0)
            return;
        foreach (var coordValue in _target_hit_badges.Keys)
        {
            if (coordValue.VariantType != Variant.Type.Vector2I)
                continue;
            Vector2I coord = coordValue.AsVector2I();
            if (!_is_cell_inside_battle(coord))
                continue;
            Control badge = _create_target_hit_badge(
                coord,
                DictString(_target_hit_badges, coord)
            );
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
        badge.AddThemeStyleboxOverride("panel", _build_hit_badge_style());
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

    private StyleBoxFlat _build_hit_badge_style() =>
        new()
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
        };

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

    private void _clear_child_nodes(Node container)
    {
        if (container == null)
            return;
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
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
        Node propInstance = BattleBoardPropScene.Instantiate();
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

    private GStringNameArray _collect_prop_ids_for_cell(BattleCellState cell_state)
    {
        var propIds = new GStringNameArray();
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

    private int _get_cell_render_depth(Vector2I coord, int height_value)
    {
        Vector2 planePosition = _get_cell_plane_position(coord);
        int clampedHeight = Mathf.Clamp(height_value, 0, MAX_HEIGHT_LAYERS - 1);
        float heightStep = Mathf.Max(_get_visual_height_step(), 1.0f);
        float planeDepth = planePosition.Y / heightStep * (float)LAYER_Z_STRIDE;
        return (int)
            Mathf.Round(
                planeDepth
                    + (float)clampedHeight * (float)LAYER_Z_STRIDE
                    + (float)DYNAMIC_LAYER_Z_OFFSET
            );
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
        if (_tileset_cache.ContainsKey(cacheKey))
        {
            GDictionary cachedProfile = DictDictionary(_tileset_cache, cacheKey);
            if (cachedProfile.Count > 0)
            {
                _tile_profile_id = renderProfile.terrain_profile_id;
                _render_profile = renderProfile;
                _tile_set = DictTileSet(cachedProfile, "tile_set");
                _source_ids = (GDictionary)DictDictionary(cachedProfile, "source_ids").Duplicate(true);
                return;
            }
        }
        _tile_profile_id = renderProfile.terrain_profile_id;
        _render_profile = renderProfile;
        _tile_set = new TileSet
        {
            TileSize = renderProfile.board_tile_size,
            TileShape = TileSet.TileShapeEnum.Isometric,
            TileLayout = TileSet.TileLayoutEnum.DiamondDown,
            TileOffsetAxis = TileSet.TileOffsetAxisEnum.Horizontal,
        };
        _source_ids.Clear();
        _register_profile_textures(renderProfile);
        _tileset_cache[cacheKey] = new GDictionary
        {
            ["tile_set"] = _tile_set,
            ["source_ids"] = _source_ids.Duplicate(true),
        };
    }

    private void _register_profile_textures(BattleBoardRenderProfile render_profile)
    {
        render_profile ??= BattleBoardRenderProfile.ForTerrainProfileId(
            BattleBoardRenderProfile.TERRAIN_PROFILE_DEFAULT()
        );
        string tileDir = render_profile.asset_dir;
        foreach (GDictionary sourceSpec in render_profile.GetSourceSpecs())
        {
            GArray fileNames = DictArray(sourceSpec, "files");
            var textures = new GArray();
            foreach (var fileNameValue in fileNames)
            {
                string fileName = fileNameValue.AsString();
                Texture2D texture = _load_texture_from_png($"{tileDir}/{fileName}");
                if (texture == null)
                {
                    GameLog.Error($"BattleBoardController 缺少地形贴图：{tileDir}/{fileName}.", "ui.battle.missing_tile_texture", "ui");
                    continue;
                }
                textures.Add(texture);
            }
            bool allowGeneratedFallback = true;
            if (TryRead(sourceSpec, "allow_generated_fallback", out Variant allowFallbackValue))
            {
                allowGeneratedFallback =
                    allowFallbackValue.VariantType == Variant.Type.Bool
                        ? allowFallbackValue.AsBool()
                        : allowGeneratedFallback;
            }
            if (textures.Count == 0 && allowGeneratedFallback)
            {
                Texture2D fallbackTexture = _build_missing_source_texture(
                    DictStringName(sourceSpec, "key"),
                    sourceSpec
                );
                if (fallbackTexture != null)
                    textures.Add(fallbackTexture);
            }
            _register_source_options(
                DictStringName(sourceSpec, "key"),
                textures,
                sourceSpec
            );
        }
        GDictionary generatedMarkerSpec = _build_generated_marker_source_spec(render_profile);
        _register_source_options(
            SOURCE_ACTIVE_SELECTED,
            new GArray { _build_active_selected_marker_texture(render_profile) },
            generatedMarkerSpec
        );
        _register_source_options(
            SOURCE_MOVE_REACHABLE,
            new GArray { _build_move_reachable_marker_texture(render_profile) },
            generatedMarkerSpec
        );
    }

    private int _add_atlas_source(Texture2D texture, GDictionary source_spec)
    {
        var source = new TileSetAtlasSource
        {
            Texture = texture,
            TextureRegionSize = DictVector2I(source_spec, "atlas_region_size", _get_board_tile_size()),
            UseTexturePadding = false,
        };
        source.CreateTile(Vector2I.Zero, Vector2I.One);
        TileData tileData = source.GetTileData(Vector2I.Zero, 0);
        if (tileData != null)
            tileData.TextureOrigin = DictVector2I(
                source_spec,
                "visual_origin",
                DictVector2I(source_spec, "texture_origin")
            );
        return _tile_set.AddSource(source);
    }

    private void _register_source_options(
        StringName source_key,
        GArray textures,
        GDictionary source_spec
    )
    {
        var sourceIds = new GIntArray();
        foreach (var textureValue in textures)
        {
            Texture2D texture = textureValue.AsGodotObject() as Texture2D;
            if (texture != null)
                sourceIds.Add(_add_atlas_source(texture, source_spec));
        }
        _source_ids[source_key] = sourceIds;
    }

    private Texture2D _build_active_selected_marker_texture(BattleBoardRenderProfile render_profile)
    {
        string tileDir = render_profile.asset_dir;
        string cacheKey = $"__generated_active_selected__{render_profile.GetCacheKey()}";
        if (_texture_cache.ContainsKey(cacheKey))
            return _texture_cache[cacheKey].AsGodotObject() as Texture2D;
        Texture2D baseTexture =
            _load_texture_from_png($"{tileDir}/{render_profile.GetPrimaryLandFile()}")
            ?? _load_texture_from_png($"{tileDir}/{render_profile.GetSelectedMarkerFile()}");
        if (baseTexture == null)
            return _build_diamond_texture(
                ACTIVE_SELECTED_MARKER_COLOR,
                1.0f,
                render_profile.board_tile_size
            );
        Image image = baseTexture.GetImage();
        if (image == null || image.IsEmpty())
            return null;
        image = (Image)image.Duplicate();
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
        Texture2D generatedTexture = ImageTexture.CreateFromImage(image);
        _texture_cache[cacheKey] = generatedTexture;
        return generatedTexture;
    }

    private Texture2D _build_move_reachable_marker_texture(BattleBoardRenderProfile render_profile)
    {
        string tileDir = render_profile.asset_dir;
        string cacheKey = $"__generated_move_reachable__{render_profile.GetCacheKey()}";
        if (_texture_cache.ContainsKey(cacheKey))
            return _texture_cache[cacheKey].AsGodotObject() as Texture2D;
        Texture2D baseTexture =
            _load_texture_from_png($"{tileDir}/{render_profile.GetPrimaryLandFile()}")
            ?? _load_texture_from_png($"{tileDir}/{render_profile.GetSelectedMarkerFile()}");
        if (baseTexture == null)
            return _build_diamond_texture(
                MOVE_REACHABLE_MARKER_COLOR_LIGHT,
                0.42f,
                render_profile.board_tile_size
            );
        Image image = baseTexture.GetImage();
        if (image == null || image.IsEmpty())
            return null;
        image = (Image)image.Duplicate();
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
        Texture2D generatedTexture = ImageTexture.CreateFromImage(image);
        _texture_cache[cacheKey] = generatedTexture;
        return generatedTexture;
    }

    private Texture2D _load_texture_from_png(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        if (_texture_cache.ContainsKey(path))
            return _texture_cache[path].AsGodotObject() as Texture2D;
        Texture2D texture = null;
        if (FileAccess.FileExists($"{path}.import"))
            texture = ResourceLoader.Load<Texture2D>(
                path,
                "Texture2D",
                ResourceLoader.CacheMode.Reuse
            );
        if (texture == null && FileAccess.FileExists(path))
        {
            var image = new Image();
            Error error = image.LoadPngFromBuffer(FileAccess.GetFileAsBytes(path));
            if (error == Error.Ok)
                texture = ImageTexture.CreateFromImage(image);
        }
        texture ??= ResourceLoader.Load<Texture2D>(
            path,
            "Texture2D",
            ResourceLoader.CacheMode.Reuse
        );
        _texture_cache[path] = texture;
        return texture;
    }

    private Texture2D _build_missing_source_texture(StringName source_key, GDictionary source_spec)
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
        Vector2I tileSize = DictVector2I(source_spec, "board_tile_size", _get_board_tile_size());
        return _build_diamond_texture(color, color.A, tileSize);
    }

    private GDictionary _build_generated_marker_source_spec(
        BattleBoardRenderProfile render_profile
    ) =>
        new()
        {
            ["key"] = SOURCE_ACTIVE_SELECTED,
            ["files"] = new GArray(),
            ["atlas_region_size"] = render_profile.board_tile_size,
            ["board_tile_size"] = render_profile.board_tile_size,
            ["texture_origin"] = Vector2I.Zero,
            ["visual_origin"] = Vector2I.Zero,
            ["layer_role"] = BattleBoardRenderProfile.LAYER_ROLE_MARKER(),
        };

    private Texture2D _build_diamond_texture(Color color, float alpha, Vector2I tile_size)
    {
        Vector2I safeTileSize = new(Mathf.Max(tile_size.X, 2), Mathf.Max(tile_size.Y, 2));
        Image image = Image.CreateEmpty(safeTileSize.X, safeTileSize.Y, false, Image.Format.Rgba8);
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
        return ImageTexture.CreateFromImage(image);
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
        if (_source_ids.ContainsKey(source_key))
        {
            var sourceOptionsValue = _source_ids[source_key];
            if (sourceOptionsValue.VariantType != Variant.Type.Array)
                return -1;
            GArray sourceOptions = sourceOptionsValue.AsGodotArray();
            if (sourceOptions.Count == 0)
                return -1;
            if (coord == INVALID_OPTION_COORD || sourceOptions.Count == 1)
                return sourceOptions[0].AsInt32();
            return sourceOptions[_get_variant_index(coord, sourceOptions.Count, salt)].AsInt32();
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
        activeUnit.RefreshFootprint();
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

    private static GTileLayerArray CloneLayerArray(GTileLayerArray values)
    {
        var result = new GTileLayerArray();
        if (values == null)
            return result;
        foreach (TileMapLayer value in values)
            result.Add(value);
        return result;
    }

    private static GVector2IArray CloneVector2IArray(GVector2IArray values)
    {
        var result = new GVector2IArray();
        if (values == null)
            return result;
        foreach (Vector2I value in values)
            result.Add(value);
        return result;
    }

    private static void ClearLayers(GTileLayerArray layers)
    {
        foreach (TileMapLayer layer in layers)
            layer?.Clear();
    }

    private void ApplyTileSet(GTileLayerArray layers)
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

    private static string DictString(GDictionary dict, object key, string fallback = "")
    {
        if (!TryRead(dict, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    private static StringName DictStringName(
        GDictionary dict,
        object key,
        StringName fallback = default
    )
    {
        if (!TryRead(dict, key, out Variant value))
            return fallback ?? new StringName("");
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => fallback ?? new StringName(""),
        };
    }

    private static Vector2I DictVector2I(
        GDictionary dict,
        object key,
        Vector2I fallback = default
    )
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Vector2I
            ? value.AsVector2I()
            : fallback;
    }

    private static GArray DictArray(GDictionary dict, object key)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new GArray();
    }

    private static GDictionary DictDictionary(GDictionary dict, object key)
    {
        return
            TryRead(dict, key, out Variant value)
            && value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static TileSet DictTileSet(GDictionary dict, object key) =>
        TryRead(dict, key, out Variant value) ? value.AsGodotObject() as TileSet : null;

    private static bool TryRead(GDictionary dict, object key, out Variant value)
    {
        if (dict == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = KeyToVariant(key);
        if (dict.ContainsKey(variantKey))
        {
            value = dict[variantKey];
            return true;
        }
        value = default;
        return false;
    }

    private static Variant KeyToVariant(object key)
    {
        return key switch
        {
            Variant variantKey => variantKey,
            StringName stringNameKey => stringNameKey,
            string stringKey => stringKey,
            Vector2I vectorKey => vectorKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
    }
}
