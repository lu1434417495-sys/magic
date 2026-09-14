using System;
using System.Collections.Generic;
using Godot;

public sealed partial class BattleBoardController
{
    private readonly List<Node2D> _terrainArtNodes = new();
    private ImageTexture _terrainArtData;
    private ShaderMaterial _terrainArtMaterial;
    private Texture2D _paintedGround;
    private Texture2D _paintedRock;
    private Texture2D _paintedTree;
    private Texture2D _paintedScrub;
    private ShaderMaterial _paintedCliffMaterial;
    private ShaderMaterial _paintedSurroundMaterial;
    private ShaderMaterial _paintedGridMaterial;
    internal int PaintedSurfaceCount { get; private set; }
    internal ulong TerrainArtGeneration { get; private set; }

    private void InitializeTerrainArt()
    {
        if (_terrainArtMaterial != null)
            return;
        string paintedArtDir = _render_profile.PaintedAssetDirectory;
        _paintedGround = EngineAssetAccess.ResolveCodeAssetBorrowed<Texture2D>($"{paintedArtDir}/sandstone_ground.png");
        _paintedRock = EngineAssetAccess.ResolveCodeAssetBorrowed<Texture2D>($"{paintedArtDir}/sandstone_cliff.png");
        _paintedTree = EngineAssetAccess.ResolveCodeAssetBorrowed<Texture2D>($"{paintedArtDir}/canyon_oak.png");
        _paintedScrub = EngineAssetAccess.ResolveCodeAssetBorrowed<Texture2D>($"{paintedArtDir}/canyon_scrub.png");
        _paintedCliffMaterial = EngineAssetAccess.ResolveCodeAssetBorrowed<ShaderMaterial>(
            "res://scenes/ui/styles/battle_painted_cliff_material.tres");
        _paintedSurroundMaterial = EngineAssetAccess.ResolveCodeAssetBorrowed<ShaderMaterial>(
            "res://scenes/ui/styles/battle_painted_surround_material.tres");
        _paintedGridMaterial = EngineAssetAccess.ResolveCodeAssetBorrowed<ShaderMaterial>(
            "res://scenes/ui/styles/battle_tactical_grid_material.tres");
        _terrainArtData = OwnRenderResource(new ImageTexture(), "painted-terrain-data");
        _terrainArtMaterial = OwnRenderResource(new ShaderMaterial
        {
            Shader = EngineAssetAccess.ResolveCodeAssetBorrowed<Shader>(
                "res://assets/shaders/battle_painted_ground.gdshader"),
        }, "painted-terrain-material");
        _terrainArtMaterial.SetShaderParameter("terrain_data", _terrainArtData);
    }

    private void DrawTerrainArt(List<BattleBoardCellSnapshot> cells)
    {
        if (_input_layer == null || _snapshot == null || cells.Count == 0)
            return;
        InitializeTerrainArt();
        UpdateTerrainArtData(cells);
        TerrainArtGeneration++;
        // TileMap occupancy remains the common input/debug representation. The painted
        // surface uses the exact same MapToLocal anchors and profile height step.
        foreach (TileMapLayer layer in _top_layers) layer.Visible = false;
        foreach (TileMapLayer layer in _edge_drop_east_layers) layer.Visible = false;
        foreach (TileMapLayer layer in _edge_drop_south_layers) layer.Visible = false;
        foreach (TileMapLayer layer in _wall_east_layers) layer.Visible = false;
        foreach (TileMapLayer layer in _wall_south_layers) layer.Visible = false;

        Vector2 origin = _input_layer.MapToLocal(Vector2I.Zero);
        Vector2 axisX = _input_layer.MapToLocal(Vector2I.Right) - origin;
        Vector2 axisY = _input_layer.MapToLocal(Vector2I.Down) - origin;
        Vector2[] corners = { new(-0.5f,-0.5f), new(0.5f,-0.5f), new(0.5f,0.5f), new(-0.5f,0.5f) };
        var tops = new Dictionary<int, BattleTerrainPaintLayer>();
        var grids = new Dictionary<int, BattleTerrainPaintLayer>();
        var edges = new Dictionary<int, BattleTerrainPaintLayer>();
        int floor = 8;
        foreach (BattleBoardCellSnapshot cell in cells) floor = Math.Min(floor, cell.Height);
        floor = floor < 0 ? floor - 1 : Math.Max(0, floor - 1);
        DrawTerrainSurround(origin, axisX, axisY, floor);
        foreach (BattleBoardCellSnapshot cell in cells)
        {
            if (!_is_cell_inside_battle(cell.Coord)) continue;
            int height = Mathf.Clamp(cell.Height, MIN_RENDER_HEIGHT, MAX_RENDER_HEIGHT);
            if (!tops.TryGetValue(height, out BattleTerrainPaintLayer top))
            {
                top = AddTerrainPaintLayer($"PaintTopH{height}", height * LAYER_Z_STRIDE,
                    _paintedGround, _terrainArtMaterial);
                tops.Add(height, top);
                // Keep the cell footprint visible over trees and surface effects, below
                // state markers and units. Higher terraces still occlude lower cells.
                grids.Add(height, AddTerrainPaintLayer($"TacticalGridH{height}",
                    height * LAYER_Z_STRIDE + OVERLAY_LAYER_Z_OFFSET, null, _paintedGridMaterial));
            }
            Vector2 anchor = _input_layer.MapToLocal(cell.Coord) - new Vector2(0, height * _get_visual_height_step());
            var points = new Vector2[4];
            var uvs = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                points[i] = anchor + axisX * corners[i].X + axisY * corners[i].Y;
                uvs[i] = (Vector2)cell.Coord + corners[i];
            }
            top.Patches.Add(new(points, uvs, Colors.White));
            grids[height].Patches.Add(new(points, uvs, Colors.White));
            PaintedSurfaceCount++;
            if (cell.BaseTerrain == TERRAIN_FOREST)
                DrawPaintedTree(cell, anchor);
        }
        var decoratedCoords = new HashSet<Vector2I>();
        foreach (BattleBoardEdgeSnapshot edge in _snapshot.Edges)
        {
            foreach (int height in edge.DropFaceLayerHeights)
            {
                // The outer cutaway is drawn separately down to the visual floor.
                if (!_snapshot.ContainsCell(edge.NeighborCoord)) continue;
                DrawPaintedFace(edge, height, false, edges, axisX, axisY);
            }
            if (edge.HasFeatureFace)
                for (int offset = 0; offset < edge.FeatureLayers; offset++)
                    DrawPaintedFace(edge, Math.Max(MIN_RENDER_HEIGHT, edge.FromHeight - offset), true, edges, axisX, axisY);
            BattleBoardCellSnapshot cell = _snapshot.GetCell(edge.OriginCoord);
            if (cell == null || !edge.HasDropFace) continue;
            int variant = _get_variant_index(cell.Coord, 13, 77);
            if (cell.BaseTerrain != TERRAIN_FOREST && !IsPaintedWater(cell.BaseTerrain)
                && variant >= 8 && decoratedCoords.Add(cell.Coord))
            {
                int height = Mathf.Clamp(cell.Height, MIN_RENDER_HEIGHT, MAX_RENDER_HEIGHT);
                Vector2 anchor = _input_layer.MapToLocal(cell.Coord)
                    - new Vector2(0, height * _get_visual_height_step());
                Vector2 direction = edge.Direction == Vector2I.Right ? axisX : axisY;
                AddPaintedScrub(anchor + direction * 0.43f, 120 + variant * 8,
                    height * LAYER_Z_STRIDE + 2, 1f, $"RimScrub_{cell.Coord.X}_{cell.Coord.Y}");
            }
        }
        // Runtime boundary faces use height zero as their outside datum. Extend the
        // decorative map cutaway to the lowest visible terrace, including depressions.
        // This is outside the playable cells and contributes no movement geometry.
        foreach (BattleBoardCellSnapshot cell in cells)
        {
            foreach (Vector2I direction in new[] { Vector2I.Right, Vector2I.Down })
            {
                if (_snapshot.ContainsCell(cell.Coord + direction)) continue;
                var boundary = new BattleBoardEdgeSnapshot(cell.Coord, cell.Coord + direction,
                    direction, Array.Empty<int>(), BattleEdgeRenderKind.None, 0, cell.Height);
                for (int height = cell.Height; height > floor; height--)
                    DrawPaintedFace(boundary, height, false, edges, axisX, axisY);
            }
        }
        // Unselectable surroundings provide a quiet continuation below the cutaway.
        // Their placement is deterministic and never enters the battle snapshot.
        Vector2I[] directions = { Vector2I.Right, Vector2I.Down, Vector2I.Left, Vector2I.Up };
        foreach (BattleBoardCellSnapshot cell in cells)
        {
            int variant = _get_variant_index(cell.Coord, 11, 89);
            if (variant < 6) continue;
            foreach (Vector2I direction in directions)
            {
                if (_snapshot.ContainsCell(cell.Coord + direction)) continue;
                Vector2 anchor = _input_layer.MapToLocal(cell.Coord) +
                    (axisX * direction.X + axisY * direction.Y) * (0.9f + variant * 0.03f)
                    - new Vector2(0, floor * _get_visual_height_step());
                AddPaintedScrub(anchor, 240 + variant * 16, -70, 0.74f,
                    $"SurroundScrub_{cell.Coord.X}_{cell.Coord.Y}_{direction.X}_{direction.Y}");
            }
        }
    }

    private void UpdateTerrainArtData(List<BattleBoardCellSnapshot> cells)
    {
        using var request = new NativeLeaseScope("BattleBoardController.terrain-paint-upload", LifetimeDomain.Request);
        Image data = request.Own(Image.CreateEmpty(_snapshot.MapSize.X, _snapshot.MapSize.Y,
            false, Image.Format.Rgba8), "terrain-map-image");
        data.Fill(Colors.Transparent);
        foreach (BattleBoardCellSnapshot cell in cells)
        {
            if (!_is_cell_inside_battle(cell.Coord)) continue;
            float water = cell.BaseTerrain == TERRAIN_SHALLOW_WATER ? 0.8f
                : cell.BaseTerrain == TERRAIN_FLOWING_WATER ? 0.9f
                : cell.BaseTerrain == TERRAIN_DEEP_WATER || cell.BaseTerrain == TERRAIN_WATER ? 1f : 0f;
            data.SetPixelv(cell.Coord, new Color(water, cell.BaseTerrain == TERRAIN_FOREST ? 1f : 0f,
                cell.BaseTerrain == TERRAIN_MUD ? 1f : 0f, (Mathf.Clamp(cell.Height, MIN_RENDER_HEIGHT, MAX_RENDER_HEIGHT) - MIN_RENDER_HEIGHT + 1f) / 16f));
        }
        _terrainArtData.SetImage(data);
        _terrainArtMaterial.SetShaderParameter("map_size", (Vector2)_snapshot.MapSize);
    }

    private void DrawPaintedFace(BattleBoardEdgeSnapshot edge, int height, bool wall,
        Dictionary<int, BattleTerrainPaintLayer> layers, Vector2 axisX, Vector2 axisY)
    {
        if (height < MIN_RENDER_HEIGHT || height > MAX_RENDER_HEIGHT) return;
        bool right = edge.Direction == Vector2I.Right;
        int z = height * LAYER_Z_STRIDE + (wall ? (right ? -2 : -1) : (right ? -4 : -3));
        if (!layers.TryGetValue(z, out BattleTerrainPaintLayer layer))
        {
            layer = AddTerrainPaintLayer($"PaintFace{z}", z, _paintedRock, _paintedCliffMaterial);
            layers.Add(z, layer);
        }
        Vector2 coord = edge.OriginCoord;
        Vector2 anchor = _input_layer.MapToLocal(edge.OriginCoord) - new Vector2(0, height * _get_visual_height_step());
        Vector2 a = anchor + (right ? (axisX - axisY) * 0.5f : (axisX + axisY) * 0.5f);
        Vector2 b = anchor + (right ? (axisX + axisY) * 0.5f : (axisY - axisX) * 0.5f);
        // Shared world UVs run through the entire face, across both tile and height seams.
        float along = right ? coord.Y : -coord.X;
        float span = 5.5f;
        Vector2[] uvs = { new(along / span, -height * 0.14f), new((along + 1) / span, -height * 0.14f),
            new((along + 1) / span, -(height - 1) * 0.14f), new(along / span, -(height - 1) * 0.14f) };
        Vector2 down = new(0, _get_visual_height_step() + 0.5f);
        float light = 0.82f + height * 0.018f;
        Color tint = right ? new Color(0.73f,0.78f,0.80f) : new Color(1.03f,0.99f,0.91f);
        layer.Patches.Add(new(new[] { a, b, b + down, a + down }, uvs, tint * new Color(light,light,light)));
        // The sun catches the lip once, not at every stacked height band.
        bool lip = wall || height == edge.FromHeight;
        if (lip)
        {
            var rim = AddTerrainPaintLayer($"RockLip_{edge.OriginCoord.X}_{edge.OriginCoord.Y}_{z}", z + 5, null, null);
            rim.Strokes.Add(new(new[] { a, b }, new Color(0.29f,0.23f,0.16f,0.34f), 5f));
            rim.Strokes.Add(new(new[] { a - new Vector2(0,1.5f), b - new Vector2(0,1.5f) },
                new Color(1f,0.85f,0.57f,0.5f), 1.5f));
        }
    }

    private void DrawPaintedTree(BattleBoardCellSnapshot cell, Vector2 anchor)
    {
        int height = Mathf.Clamp(cell.Height, MIN_RENDER_HEIGHT, MAX_RENDER_HEIGHT);
        int variant = _get_variant_index(cell.Coord, 7, 53);
        float width = 188f + variant * 8f;
        float scale = width / _paintedTree.GetWidth();
        // Contact shadows sit on the same terrace, below trees and tactical markers.
        var shadow = AddTerrainPaintLayer($"TreeShadow_{cell.Coord.X}_{cell.Coord.Y}",
            height * LAYER_Z_STRIDE + 1, null, null);
        for (int ring = 0; ring < 8; ring++)
        {
            var points = new Vector2[24];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.Tau / points.Length;
                points[i] = anchor + new Vector2(27,9) + new Vector2(Mathf.Cos(angle)*(74-ring*4), Mathf.Sin(angle)*(29-ring*1.5f));
            }
            shadow.Patches.Add(new(points, null, new Color(0.17f,0.22f,0.17f,0.025f)));
        }
        var tree = new Sprite2D
        {
            Name = $"PaintedOak_{cell.Coord.X}_{cell.Coord.Y}",
            Texture = _paintedTree,
            Position = anchor + new Vector2((variant - 3) * 3f, -_paintedTree.GetHeight() * scale * 0.47f),
            Scale = new Vector2(scale * (0.96f + variant * 0.012f), scale),
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            ZIndex = height * LAYER_Z_STRIDE + 5,
            Modulate = new Color(1f - variant * 0.012f, 1f - variant * 0.009f, 1f),
        };
        AddTerrainArtNode(tree);
    }

    private void DrawTerrainSurround(Vector2 origin, Vector2 axisX, Vector2 axisY, int floor)
    {
        var surround = AddTerrainPaintLayer("CanyonSurround", -100, _paintedGround, _paintedSurroundMaterial);
        Vector2[] coords = { new(-50,-50), new(80,-50), new(80,80), new(-50,80) };
        var points = new Vector2[4];
        var uvs = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            points[i] = origin + axisX * coords[i].X + axisY * coords[i].Y - new Vector2(0, floor * _get_visual_height_step());
            uvs[i] = coords[i] / 10f;
        }
        surround.Patches.Add(new(points, uvs, Colors.White));
    }

    private static bool IsPaintedWater(StringName terrain) => terrain == TERRAIN_WATER
        || terrain == TERRAIN_SHALLOW_WATER || terrain == TERRAIN_FLOWING_WATER || terrain == TERRAIN_DEEP_WATER;

    private void AddPaintedScrub(Vector2 anchor, float width, int z, float light, string name)
    {
        float scale = width / _paintedScrub.GetWidth();
        AddTerrainArtNode(new Sprite2D
        {
            Name = name, Texture = _paintedScrub, Scale = Vector2.One * scale,
            Position = anchor - new Vector2(0, _paintedScrub.GetHeight() * scale * 0.36f),
            ZIndex = z, Modulate = new Color(light, light, light),
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
        });
    }

    private BattleTerrainPaintLayer AddTerrainPaintLayer(string name, int z, Texture2D texture, Material material)
    {
        var layer = new BattleTerrainPaintLayer { Name = name, ZIndex = z, Texture = texture,
            Material = material, TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
        AddTerrainArtNode(layer);
        return layer;
    }

    private void AddTerrainArtNode(Node2D node)
    {
        _terrainArtNodes.Add(node);
        _input_layer.GetParent().AddChild(node);
    }

    private void ClearTerrainArtNodes()
    {
        foreach (Node2D node in _terrainArtNodes)
        {
            if (!GodotObject.IsInstanceValid(node)) continue;
            node.GetParent()?.RemoveChild(node);
            node.Free();
        }
        _terrainArtNodes.Clear();
        PaintedSurfaceCount = 0;
    }

    private void ClearTerrainArtResources()
    {
        _terrainArtData = null;
        _terrainArtMaterial = null;
        _paintedGround = null;
        _paintedRock = null;
        _paintedTree = null;
        _paintedScrub = null;
        _paintedCliffMaterial = null;
        _paintedSurroundMaterial = null;
        _paintedGridMaterial = null;
    }
}
