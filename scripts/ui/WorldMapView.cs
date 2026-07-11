using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class WorldMapView : Control
{
    [Signal]
    public delegate void cell_clickedEventHandler(Vector2I coord);

    [Signal]
    public delegate void cell_right_clickedEventHandler(Vector2I coord);

    [Export(PropertyHint.Range, "24,128,1")]
    public int cell_size = 96;

    [Export(PropertyHint.Range, "0,8,1")]
    public int viewport_padding_cells = 2;

    [Export]
    public Texture2D cell_background_texture;

    [Export]
    public Texture2D player_texture;

    [Export]
    public Texture2D village_settlement_texture;

    [Export]
    public Texture2D farm_resource_texture;

    [Export]
    public Texture2D herb_resource_texture;

    [Export]
    public Texture2D mine_resource_texture;

    [Export(PropertyHint.Range, "16,256,1")]
    public int player_texture_draw_size = 128;

    [Export(PropertyHint.Range, "0,256,1")]
    public int cell_background_trim = 96;

    [Export(PropertyHint.Range, "0.0,1.0,0.05")]
    public float explored_fog_darkness = 0.45f;

    [Export]
    public Color selection_outline_color = new(0.98f, 0.9f, 0.42f, 0.95f);

    [Export]
    public Color world_event_marker_fill_color = new(0.95f, 0.78f, 0.28f, 0.96f);

    [Export]
    public Color world_event_marker_outline_color = new(0.25f, 0.11f, 0.02f, 1.0f);

    [Export]
    public Color world_event_marker_center_color = new(0.32f, 0.06f, 0.02f, 1.0f);

    [Export]
    public Color farm_resource_marker_color = new(0.48f, 0.73f, 0.35f, 0.95f);

    [Export]
    public Color herb_resource_marker_color = new(0.26f, 0.82f, 0.58f, 0.95f);

    [Export]
    public Color mine_resource_marker_color = new(0.62f, 0.65f, 0.68f, 0.95f);

    [Export]
    public Color resource_marker_outline_color = new(0.05f, 0.08f, 0.1f, 0.95f);

    [Export]
    public Color encounter_marker_outer_color = new(0.87f, 0.28f, 0.23f, 0.95f);

    [Export]
    public Color encounter_marker_inner_color = new(0.15f, 0.02f, 0.02f, 0.95f);

    [Export]
    public Color npc_marker_body_color = new(0.42f, 0.77f, 0.87f, 0.95f);

    [Export]
    public Color npc_marker_head_color = new(0.88f, 0.94f, 0.98f, 1.0f);

    [Export]
    public Color village_tier_color = new(0.57f, 0.75f, 0.43f, 1.0f);

    [Export]
    public Color town_tier_color = new(0.51f, 0.7f, 0.84f, 1.0f);

    [Export]
    public Color city_tier_color = new(0.78f, 0.63f, 0.42f, 1.0f);

    [Export]
    public Color capital_tier_color = new(0.74f, 0.48f, 0.76f, 1.0f);

    [Export]
    public Color world_stronghold_tier_color = new(0.9f, 0.43f, 0.31f, 1.0f);

    [Export]
    public Color metropolis_tier_color = new(0.95f, 0.82f, 0.45f, 1.0f);

    [Export]
    public Color fallback_tier_color = new(0.5f, 0.5f, 0.5f, 1.0f);

    private WorldMapGridSystem _gridSystem;
    private WorldMapFogSystem _fogSystem;
    private WorldRuntimeData _worldData = WorldRuntimeData.Empty();
    private Vector2I _playerCoord = Vector2I.Zero;
    private Vector2I _selectedCoord = Vector2I.Zero;
    private bool _playerVisibleOnMap = true;
    private string _playerFactionId = "player";

    public void Configure(
        WorldMapGridSystem grid_system,
        WorldMapFogSystem fog_system,
        GDictionary world_data,
        Vector2I player_coord,
        Vector2I selected_coord,
        bool player_visible_on_map,
        string player_faction_id
    )
    {
        _gridSystem = grid_system;
        _fogSystem = fog_system;
        _set_world_data(WorldRuntimeData.FromDictionary(world_data));
        _playerCoord = player_coord;
        _selectedCoord = selected_coord;
        _playerVisibleOnMap = player_visible_on_map;
        _playerFactionId = player_faction_id;
        QueueRedraw();
    }

    public void SetRuntimeState(
        Vector2I player_coord,
        Vector2I selected_coord,
        bool player_visible_on_map = true
    )
    {
        _playerCoord = player_coord;
        _selectedCoord = selected_coord;
        _playerVisibleOnMap = player_visible_on_map;
        QueueRedraw();
    }

    internal void RefreshWorld(WorldRuntimeData world_data)
    {
        _set_world_data(world_data);
        QueueRedraw();
    }

    private void _set_world_data(WorldRuntimeData world_data)
    {
        _worldData = world_data ?? WorldRuntimeData.Empty();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } mouseEvent)
            return;

        if (_gridSystem == null)
            return;

        Vector2I coord = _local_to_cell(mouseEvent.Position);
        if (!_gridSystem.IsCellInsideWorld(coord))
            return;

        if (mouseEvent.ButtonIndex == MouseButton.Left)
        {
            EmitSignal(SignalName.cell_clicked, coord);
            AcceptEvent();
        }
        else if (mouseEvent.ButtonIndex == MouseButton.Right)
        {
            EmitSignal(SignalName.cell_right_clicked, coord);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        if (_gridSystem == null || _fogSystem == null)
            return;
        Vector2 cameraOrigin = _get_camera_origin_cells();
        Rect2I visibleRect = _get_visible_world_rect(cameraOrigin);
        _draw_cells(cameraOrigin, visibleRect);
        _draw_settlements(cameraOrigin, visibleRect);
        _draw_mobile_entities(cameraOrigin, visibleRect);
        _draw_player(cameraOrigin);
        _draw_selection(cameraOrigin);
    }

    private void _draw_cells(Vector2 cameraOrigin, Rect2I visibleRect)
    {
        for (int y = visibleRect.Position.Y; y < visibleRect.End.Y; y++)
        {
            for (int x = visibleRect.Position.X; x < visibleRect.End.X; x++)
            {
                var coord = new Vector2I(x, y);
                Rect2 rect = _get_cell_rect_for_origin(coord, cameraOrigin);
                WorldMapFogStateKind fogState = WorldMapFogSystem.ToFogStateKind(
                    _fogSystem.GetFogState(coord, _playerFactionId)
                );
                if (fogState == WorldMapFogStateKind.Unexplored)
                {
                    DrawRect(rect, Colors.Black);
                }
                else
                {
                    _draw_cell_background(rect);
                    if (fogState == WorldMapFogStateKind.Explored)
                        DrawRect(rect, new Color(0.0f, 0.0f, 0.0f, explored_fog_darkness));
                }
                DrawRect(rect, new Color(0.12f, 0.16f, 0.26f, 0.7f), false, 1.0f);
            }
        }
    }

    private void _draw_cell_background(Rect2 rect)
    {
        if (cell_background_texture == null)
        {
            DrawRect(rect, new Color(0.11f, 0.14f, 0.18f, 1.0f));
            return;
        }

        Vector2 textureSize = cell_background_texture.GetSize();
        float trim = Mathf.Min(
            cell_background_trim,
            Mathf.Min(textureSize.X, textureSize.Y) * 0.45f
        );
        var sourceRect = new Rect2(
            new Vector2(trim, trim),
            textureSize - new Vector2(trim * 2.0f, trim * 2.0f)
        );
        if (sourceRect.Size.X <= 0.0f || sourceRect.Size.Y <= 0.0f)
        {
            DrawTextureRect(cell_background_texture, rect, false);
            return;
        }
        DrawTextureRectRegion(cell_background_texture, rect, sourceRect, Colors.White, false);
    }

    private void _draw_settlements(Vector2 cameraOrigin, Rect2I visibleRect)
    {
        Font font = GetThemeDefaultFont();
        const int fontSize = 16;
        bool canDrawLabels = font != null;

        foreach (WorldMapSettlementRecordData settlement in _worldData.Settlements)
        {
            if (settlement == null)
                continue;
            Vector2I origin = settlement.Origin;
            Vector2I footprintSize = settlement.FootprintSize;
            if (!new Rect2I(origin, footprintSize).Intersects(visibleRect))
                continue;
            int tier = settlement.Tier;
            Color color = _get_settlement_color(tier);
            int visibleCells = _draw_settlement_footprint_cells(
                origin,
                footprintSize,
                cameraOrigin,
                visibleRect,
                tier,
                color
            );
            if (visibleCells == 0 || !canDrawLabels)
                continue;

            WorldMapFogStateKind originFogState = WorldMapFogSystem.ToFogStateKind(
                _fogSystem.GetFogState(origin, _playerFactionId)
            );
            if (originFogState == WorldMapFogStateKind.Unexplored)
                continue;

            Rect2 rect = _get_cell_rect_for_origin(origin, cameraOrigin).Grow(-3.0f);
            string label = string.IsNullOrEmpty(settlement.DisplayName) ? "据点" : settlement.DisplayName;
            Vector2 labelPos = rect.Position + new Vector2(8, Mathf.Min(24, rect.Size.Y - 6));
            DrawString(
                font,
                labelPos,
                label,
                HorizontalAlignment.Left,
                rect.Size.X - 12.0f,
                fontSize,
                Colors.White
            );
        }
    }

    private int _draw_settlement_footprint_cells(
        Vector2I origin,
        Vector2I footprintSize,
        Vector2 cameraOrigin,
        Rect2I visibleRect,
        int tier,
        Color baseColor
    )
    {
        int drawnCells = 0;
        int width = Mathf.Max(footprintSize.X, 1);
        int height = Mathf.Max(footprintSize.Y, 1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2I cellCoord = origin + new Vector2I(x, y);
                if (!visibleRect.HasPoint(cellCoord))
                    continue;
                WorldMapFogStateKind fogState = WorldMapFogSystem.ToFogStateKind(
                    _fogSystem.GetFogState(cellCoord, _playerFactionId)
                );
                if (fogState == WorldMapFogStateKind.Unexplored)
                    continue;
                Rect2 cellRect = _get_cell_rect_for_origin(cellCoord, cameraOrigin).Grow(-3.0f);
                Color color = baseColor;
                bool isExplored = fogState == WorldMapFogStateKind.Explored;
                if (isExplored)
                {
                    color = color.Darkened(0.45f);
                    color.A = 0.85f;
                }
                _draw_settlement_body(cellRect, tier, color, isExplored);
                DrawRect(cellRect, new Color(0.05f, 0.08f, 0.14f, 0.95f), false, 2.0f);
                drawnCells++;
            }
        }
        return drawnCells;
    }

    private void _draw_settlement_body(Rect2 rect, int tier, Color color, bool isExplored)
    {
        if (tier == (int)SettlementTierKind.Village && village_settlement_texture != null)
        {
            DrawTextureRect(
                village_settlement_texture,
                rect,
                false,
                new Color(1.0f, 1.0f, 1.0f, color.A)
            );
            if (isExplored)
                DrawRect(rect, new Color(0.0f, 0.0f, 0.0f, 0.35f));
            return;
        }
        DrawRect(rect, color);
    }

    private void _draw_mobile_entities(Vector2 cameraOrigin, Rect2I visibleRect)
    {
        foreach (WorldMapEventData worldEvent in _worldData.WorldEvents)
        {
            if (worldEvent == null || !worldEvent.IsDiscovered)
                continue;
            Vector2I eventCoord = worldEvent.WorldCoord;
            if (
                !visibleRect.HasPoint(eventCoord)
                || !_fogSystem.IsVisible(eventCoord, _playerFactionId)
            )
                continue;
            _draw_world_event_marker(
                _get_cell_rect_for_origin(eventCoord, cameraOrigin).GetCenter()
            );
        }

        foreach (WorldMapResourceNodeData resourceNode in _worldData.ResourceNodes)
        {
            if (resourceNode == null || !resourceNode.Exists)
                continue;
            Vector2I coord = resourceNode.WorldCoord;
            if (!visibleRect.HasPoint(coord) || !_fogSystem.IsVisible(coord, _playerFactionId))
                continue;
            _draw_resource_marker(
                resourceNode,
                _get_cell_rect_for_origin(coord, cameraOrigin)
            );
        }

        foreach (EncounterAnchorData encounterAnchor in _worldData.EncounterAnchors)
        {
            if (encounterAnchor == null)
                continue;
            Vector2I coord = encounterAnchor.world_coord;
            if (!visibleRect.HasPoint(coord) || !_fogSystem.IsVisible(coord, _playerFactionId))
                continue;
            Vector2 center = _get_cell_rect_for_origin(coord, cameraOrigin).GetCenter();
            DrawCircle(center, cell_size * 0.22f, encounter_marker_outer_color);
            DrawCircle(center, cell_size * 0.12f, encounter_marker_inner_color);
        }

        foreach (WorldMapNpcData npc in _worldData.WorldNpcs)
        {
            if (npc == null)
                continue;
            Vector2I coord = npc.Coord;
            if (!visibleRect.HasPoint(coord) || !_fogSystem.IsVisible(coord, _playerFactionId))
                continue;
            Vector2 center = _get_cell_rect_for_origin(coord, cameraOrigin).GetCenter();
            DrawCircle(center, cell_size * 0.18f, npc_marker_body_color);
            DrawCircle(center + new Vector2(0, -4), cell_size * 0.06f, npc_marker_head_color);
        }
    }

    private void _draw_world_event_marker(Vector2 center)
    {
        float radius = cell_size * 0.2f;
        Vector2[] diamond =
        {
            center + new Vector2(0, -radius),
            center + new Vector2(radius, 0),
            center + new Vector2(0, radius),
            center + new Vector2(-radius, 0),
        };
        DrawColoredPolygon(diamond, world_event_marker_fill_color);
        Vector2[] outline = { diamond[0], diamond[1], diamond[2], diamond[3], diamond[0] };
        DrawPolyline(outline, world_event_marker_outline_color, 2.0f);
        DrawCircle(center, cell_size * 0.05f, world_event_marker_center_color);
    }

    private void _draw_resource_marker(WorldMapResourceNodeData resourceNode, Rect2 cellRect)
    {
        Texture2D texture = _get_resource_texture(resourceNode.NodeKind);
        if (texture != null)
        {
            Rect2 iconRect = cellRect.Grow(-cell_size * 0.1f);
            DrawTextureRect(texture, iconRect, false);
            DrawRect(iconRect, resource_marker_outline_color, false, 2.0f);
        }
        else
        {
            _draw_resource_marker_fallback(resourceNode, cellRect.GetCenter());
        }
        _draw_resource_label(resourceNode, cellRect);
    }

    private Texture2D _get_resource_texture(string nodeKind) =>
        nodeKind switch
        {
            WorldMapResourceNodeData.KindFarm => farm_resource_texture,
            WorldMapResourceNodeData.KindHerbGarden => herb_resource_texture,
            WorldMapResourceNodeData.KindMine => mine_resource_texture,
            _ => null,
        };

    private void _draw_resource_label(WorldMapResourceNodeData resourceNode, Rect2 cellRect)
    {
        Font font = GetThemeDefaultFont();
        if (font == null)
            return;
        const int fontSize = 14;
        string label = string.IsNullOrEmpty(resourceNode.DisplayName)
            ? "采集点"
            : resourceNode.DisplayName;
        Vector2 textSize = font.GetStringSize(
            label,
            HorizontalAlignment.Left,
            -1.0f,
            fontSize
        );
        var padding = new Vector2(5.0f, 2.0f);
        Vector2 bgSize = textSize + padding * 2.0f;
        var bgPos = new Vector2(
            cellRect.GetCenter().X - bgSize.X * 0.5f,
            cellRect.End.Y - bgSize.Y - 4.0f
        );
        DrawRect(new Rect2(bgPos, bgSize), new Color(0.05f, 0.07f, 0.11f, 0.82f));
        DrawRect(new Rect2(bgPos, bgSize), resource_marker_outline_color, false, 1.0f);
        var textPos = new Vector2(bgPos.X + padding.X, bgPos.Y + padding.Y + font.GetAscent(fontSize));
        DrawString(font, textPos, label, HorizontalAlignment.Left, -1.0f, fontSize, Colors.White);
    }

    private void _draw_resource_marker_fallback(WorldMapResourceNodeData resourceNode, Vector2 center)
    {
        float radius = cell_size * 0.16f;
        if (resourceNode.NodeKind == WorldMapResourceNodeData.KindFarm)
        {
            Rect2 body = new(center - Vector2.One * radius, Vector2.One * radius * 2.0f);
            DrawRect(body, farm_resource_marker_color);
            DrawRect(body, resource_marker_outline_color, false, 2.0f);
            DrawLine(
                new Vector2(body.Position.X, center.Y),
                new Vector2(body.End.X, center.Y),
                resource_marker_outline_color,
                1.0f
            );
            DrawLine(
                new Vector2(center.X, body.Position.Y),
                new Vector2(center.X, body.End.Y),
                resource_marker_outline_color,
                1.0f
            );
            return;
        }

        if (resourceNode.NodeKind == WorldMapResourceNodeData.KindHerbGarden)
        {
            DrawCircle(center, radius, herb_resource_marker_color);
            DrawCircle(center, radius, resource_marker_outline_color, false, 2.0f);
            DrawCircle(center + new Vector2(-radius * 0.3f, -radius * 0.15f), radius * 0.35f, Colors.White);
            DrawCircle(center + new Vector2(radius * 0.28f, radius * 0.1f), radius * 0.28f, new Color(0.1f, 0.42f, 0.24f, 0.9f));
            return;
        }

        Vector2[] diamond =
        {
            center + new Vector2(0, -radius),
            center + new Vector2(radius, 0),
            center + new Vector2(0, radius),
            center + new Vector2(-radius, 0),
        };
        DrawColoredPolygon(diamond, mine_resource_marker_color);
        DrawPolyline(
            new[] { diamond[0], diamond[1], diamond[2], diamond[3], diamond[0] },
            resource_marker_outline_color,
            2.0f
        );
    }

    private void _draw_player(Vector2 cameraOrigin)
    {
        if (!_playerVisibleOnMap || !_is_coord_visible_in_viewport(_playerCoord, cameraOrigin))
            return;
        Vector2 center = _get_cell_rect_for_origin(_playerCoord, cameraOrigin).GetCenter();
        if (player_texture != null)
        {
            Rect2 drawRect = _get_player_draw_rect_for_origin(_playerCoord, cameraOrigin);
            if (drawRect.Size.X > 0.0f && drawRect.Size.Y > 0.0f)
            {
                DrawTextureRect(player_texture, drawRect, false);
                return;
            }
        }

        Vector2[] points =
        {
            center + new Vector2(0, -cell_size * 0.26f),
            center + new Vector2(cell_size * 0.18f, 0),
            center + new Vector2(0, cell_size * 0.26f),
            center + new Vector2(-cell_size * 0.18f, 0),
        };
        DrawColoredPolygon(points, new Color(0.97f, 0.89f, 0.39f, 1.0f));
        Vector2[] outline = { points[0], points[1], points[2], points[3], points[0] };
        DrawPolyline(outline, new Color(0.18f, 0.12f, 0.02f, 1.0f), 2.0f);
    }

    private void _draw_selection(Vector2 cameraOrigin)
    {
        if (!_is_coord_visible_in_viewport(_selectedCoord, cameraOrigin))
            return;
        Rect2 rect = _get_cell_rect_for_origin(_selectedCoord, cameraOrigin).Grow(-2.0f);
        DrawRect(rect, selection_outline_color, false, 3.0f);
    }

    private Rect2 _get_cell_rect_for_origin(Vector2I coord, Vector2 cameraOrigin)
    {
        return new Rect2((ToVector2(coord) - cameraOrigin) * cell_size, Vector2.One * cell_size);
    }

    private Rect2 _get_player_draw_rect_for_origin(Vector2I coord, Vector2 cameraOrigin)
    {
        Vector2 center = _get_cell_rect_for_origin(coord, cameraOrigin).GetCenter();
        Vector2 drawSize = Vector2.One * player_texture_draw_size;
        return new Rect2(center - drawSize * 0.5f, drawSize);
    }

    private Vector2I _local_to_cell(Vector2 position)
    {
        Vector2 cameraOrigin = _get_camera_origin_cells();
        return new Vector2I(
            Mathf.FloorToInt(cameraOrigin.X + position.X / cell_size),
            Mathf.FloorToInt(cameraOrigin.Y + position.Y / cell_size)
        );
    }

    private Vector2 _get_camera_origin_cells()
    {
        Vector2 worldSizeCells = ToVector2(_gridSystem.GetWorldSizeCells());
        Vector2 cellSpan = _get_viewport_cell_span();
        Vector2 origin = ToVector2(_playerCoord) + Vector2.One * 0.5f - cellSpan * 0.5f;
        origin.X = Mathf.Clamp(origin.X, 0.0f, Mathf.Max(worldSizeCells.X - cellSpan.X, 0.0f));
        origin.Y = Mathf.Clamp(origin.Y, 0.0f, Mathf.Max(worldSizeCells.Y - cellSpan.Y, 0.0f));
        return origin;
    }

    private Vector2 _get_viewport_cell_span()
    {
        Vector2 worldSizeCells = ToVector2(_gridSystem.GetWorldSizeCells());
        var span = new Vector2(
            Mathf.Max(Size.X / cell_size, 1.0f),
            Mathf.Max(Size.Y / cell_size, 1.0f)
        );
        span.X = Mathf.Min(span.X, worldSizeCells.X);
        span.Y = Mathf.Min(span.Y, worldSizeCells.Y);
        return span;
    }

    private Rect2I _get_visible_world_rect(Vector2 cameraOrigin)
    {
        Vector2 cellSpan = _get_viewport_cell_span();
        Vector2I padding = Vector2I.One * viewport_padding_cells;
        var start = new Vector2I(
            Mathf.FloorToInt(cameraOrigin.X) - padding.X,
            Mathf.FloorToInt(cameraOrigin.Y) - padding.Y
        );
        var end = new Vector2I(
            Mathf.CeilToInt(cameraOrigin.X + cellSpan.X) + padding.X,
            Mathf.CeilToInt(cameraOrigin.Y + cellSpan.Y) + padding.Y
        );
        Vector2I worldSizeCells = _gridSystem.GetWorldSizeCells();
        start.X = Mathf.Clamp(start.X, 0, worldSizeCells.X);
        start.Y = Mathf.Clamp(start.Y, 0, worldSizeCells.Y);
        end.X = Mathf.Clamp(end.X, start.X, worldSizeCells.X);
        end.Y = Mathf.Clamp(end.Y, start.Y, worldSizeCells.Y);
        return new Rect2I(start, end - start);
    }

    private bool _is_coord_visible_in_viewport(Vector2I coord, Vector2 cameraOrigin)
    {
        var viewportBounds = new Rect2(cameraOrigin, _get_viewport_cell_span());
        Vector2 cellCenter = ToVector2(coord) + Vector2.One * 0.5f;
        return viewportBounds.HasPoint(cellCenter);
    }

    public Color _get_settlement_color(int tier)
    {
        if (tier == (int)SettlementTierKind.Village)
            return village_tier_color;
        if (tier == (int)SettlementTierKind.Town)
            return town_tier_color;
        if (tier == (int)SettlementTierKind.City)
            return city_tier_color;
        if (tier == (int)SettlementTierKind.Capital)
            return capital_tier_color;
        if (tier == (int)SettlementTierKind.WorldStronghold)
            return world_stronghold_tier_color;
        if (tier == (int)SettlementTierKind.Metropolis)
            return metropolis_tier_color;
        return fallback_tier_color;
    }

    private static Vector2 ToVector2(Vector2I value)
    {
        return new Vector2(value.X, value.Y);
    }

    private static GArray DictArray(GDictionary dict, string key)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new GArray();
    }

    private static Vector2I DictVector2I(GDictionary dict, string key, Vector2I defaultValue)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Vector2I
            ? value.AsVector2I()
            : defaultValue;
    }

    private static string DictString(GDictionary dict, string key, string defaultValue)
    {
        if (!TryRead(dict, key, out Variant value))
            return defaultValue;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => defaultValue,
        };
    }

    private static int DictInt(GDictionary dict, string key, int defaultValue)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Int
            ? value.AsInt32()
            : defaultValue;
    }

    private static bool DictBool(GDictionary dict, string key, bool defaultValue)
    {
        return TryRead(dict, key, out Variant value) && value.VariantType == Variant.Type.Bool
            ? value.AsBool()
            : defaultValue;
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        if (values == null)
            yield break;
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Dictionary)
                yield return value.AsGodotDictionary();
        }
    }

    private static bool TryRead(GDictionary dict, string key, out Variant value)
    {
        if (dict == null || string.IsNullOrEmpty(key))
        {
            value = default;
            return false;
        }
        if (dict.ContainsKey(key))
        {
            value = dict[key];
            return true;
        }
        value = default;
        return false;
    }
}
