using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class run_battle_tree_occlusion_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private readonly Vector2I _treeCoord = new(3, 3);
    private BattleBoard2D _board;
    private BattleTestFixture _fixture;

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        try
        {
            _fixture = BattleTestFixture.CreateFlatBattle("tree_occlusion", new(7, 7),
                Array.Empty<BattleUnitState>(), Array.Empty<BattleUnitState>());
            _board = EngineAssetAccess.ResolveCodeAssetBorrowed<PackedScene>(
                "res://scenes/ui/battle_board_2d.tscn").Instantiate<BattleBoard2D>();
            Root.AddChild(_board);
            await ToSignal(this, SignalName.ProcessFrame);

            foreach (int baseHeight in new[] { 0, -5, 5 })
            {
                foreach (BattleCellState cell in _fixture.State.Cells())
                {
                    cell.SetBaseHeight(baseHeight + (cell.coord.Y < 3 ? 3 : 0));
                    cell.SetTerrain(cell.coord == _treeCoord ? "forest" : "land");
                }
                _fixture.State.RebuildCellColumns();
                await Refresh();
                AssertDepthAndPicking(baseHeight);
                if (DisplayServer.GetName() == "headless") continue;

                foreach (Vector2I size in new[] { new Vector2I(1280, 720), new Vector2I(3840, 2160) })
                {
                    Root.ContentScaleSize = Vector2I.Zero;
                    Root.Size = size;
                    _board.SetViewportSize(size);
                    // Keep the complete test tree and the bordering terraces in frame.
                    _board.Scale = Vector2.One * (size.X / 1280f);
                    _board.Position = (Vector2)size * new Vector2(0.5f, 0.72f)
                        - _board._get_coord_anchor(_treeCoord) * _board.Scale;
                    await AssertCanopyPixels(size, baseHeight);
                }
            }
            if (DisplayServer.GetName() == "headless")
                ConsoleProcessOutput.WriteStandard("Native canopy pixel assertions skipped in headless mode.");
        }
        catch (Exception exception)
        {
            _test.Fail($"Tree occlusion: {exception}");
        }
        finally
        {
            _board?.Free();
            _fixture?.Dispose();
            RequestTestExit(_test.Finish("Battle tree occlusion regression"));
        }
    }

    private async Task Refresh()
    {
        _fixture.State.MarkRuntimeEdgesDirty();
        _board.Configure(new BattleBoardSnapshotBuilder().Build(_fixture.State), _treeCoord);
        await ToSignal(this, SignalName.ProcessFrame);
        _test.True(_board.IsRenderContentReady(), "森林和高台 fixture 必须完成正式棋盘绘制。");
    }

    private void AssertDepthAndPicking(int baseHeight)
    {
        Sprite2D tree = _board.GetNode<Sprite2D>("PaintedOak_3_3");
        BattleTerrainPaintLayer rear = _board.GetNode<BattleTerrainPaintLayer>(
            $"PaintTopH{baseHeight + 3}R5");
        BattleTerrainPaintLayer grid = _board.GetNode<BattleTerrainPaintLayer>(
            $"TacticalGridH{baseHeight}R6");
        Sprite2D marker = _board.GetNode<Sprite2D>("PaintMarker_3_3");
        _test.True(tree.ZIndex > rear.ZIndex, "后方高台不能盖住前方树冠。");
        _test.True(tree.ZIndex > grid.ZIndex && tree.ZIndex > marker.ZIndex,
            "地面格线和选中格不能浮到树冠上。");
        _test.True(marker.Material != null, "重排绘制不能丢失正式选中格材质。");
        Vector2I clicked = new(-1, -1);
        void OnClick(Vector2I coord) => clicked = coord;
        _board.battle_cell_clicked += OnClick;
        _board.HandleViewportMouseButton(_board.CoordToViewportPosition(_treeCoord), (int)MouseButton.Left);
        _board.battle_cell_clicked -= OnClick;
        _test.Eq(clicked, _treeCoord, "树木不能拦截所在森林格的点击。");
    }

    private async Task AssertCanopyPixels(Vector2I size, int baseHeight)
    {
        Sprite2D tree = _board.GetNode<Sprite2D>("PaintedOak_3_3");
        using Image composite = await Capture();
        SaveCapture(composite, $"trees_height_{baseHeight}_{size.X}x{size.Y}");
        var otherItems = _board.GetChildren().OfType<CanvasItem>()
            .Where(item => item != tree && item.Visible).ToArray();
        foreach (CanvasItem item in otherItems) item.Visible = false;
        using Image isolated = await Capture();
        foreach (CanvasItem item in otherItems) item.Visible = true;

        using Image texture = tree.Texture.GetImage();
        if (texture.IsCompressed()) _test.Eq(texture.Decompress(), Error.Ok, "读取树木 alpha。");
        Rect2 rect = tree.GetRect();
        Vector2 screenStart = tree.ToGlobal(rect.Position);
        Vector2 screenEnd = tree.ToGlobal(rect.End);
        int samples = 0;
        int covered = 0;
        var canopyPixels = new List<Vector2I>();
        int step = Math.Max(1, size.X / 640);
        for (int y = Math.Max(0, (int)screenStart.Y); y < Math.Min(size.Y, screenEnd.Y); y += step)
        for (int x = Math.Max(0, (int)screenStart.X); x < Math.Min(size.X, screenEnd.X); x += step)
        {
            Vector2 uv = (tree.ToLocal(new Vector2(x + 0.5f, y + 0.5f)) - rect.Position) / rect.Size;
            if (uv.X <= 0 || uv.X >= 1 || uv.Y <= 0 || uv.Y >= 0.68f) continue;
            int tx = (int)(uv.X * texture.GetWidth());
            int ty = (int)(uv.Y * texture.GetHeight());
            // The formal artwork's solid leaves use alpha 250..253, not 255.
            // Exclude soft edges and allow their small background contribution.
            if (texture.GetPixel(tx, ty).A < 0.985f) continue;
            samples++;
            canopyPixels.Add(new(x, y));
            Color expected = isolated.GetPixel(x, y);
            Color actual = composite.GetPixel(x, y);
            if (Math.Abs(actual.R - expected.R) + Math.Abs(actual.G - expected.G)
                + Math.Abs(actual.B - expected.B) > 0.10f) covered++;
        }
        _test.True(samples > 1000, $"{size}/{baseHeight}: 必须实际采样足够树冠像素。");
        _test.True(covered < samples * 0.01f,
            $"{size}/{baseHeight}: 后方高地和格线不能覆盖树冠像素 ({covered}/{samples})。");
        ConsoleProcessOutput.WriteStandard($"Canopy pixels {size}, height {baseHeight}: covered={covered}/{samples}");

        // The opposite case protects genuine occlusion: a raised foreground column
        // must still cover a rear tree. This rejects moving every tree to a fixed top Z.
        BattleCellState front = _fixture.State.GetCell(new(3, 4));
        front.SetBaseHeight(baseHeight + 3);
        _fixture.State.RebuildCellColumns();
        Transform2D captureTransform = _board.Transform;
        await Refresh();
        _board.Transform = captureTransform;
        Sprite2D refreshedTree = _board.GetNode<Sprite2D>("PaintedOak_3_3");
        var frontFace = _board.GetChildren().OfType<BattleTerrainPaintLayer>()
            .First(layer => layer.Name.ToString().StartsWith("PaintFaceR7", StringComparison.Ordinal));
        _test.True(frontFace.ZIndex > refreshedTree.ZIndex, "前方岩壁必须保持对后方树木的正常遮挡。");
        using Image foreground = await Capture();
        SaveCapture(foreground, $"foreground_cliff_height_{baseHeight}_{size.X}x{size.Y}");
        int changed = 0;
        foreach (Vector2I pixel in canopyPixels)
        {
            Color oldPixel = composite.GetPixelv(pixel);
            Color newPixel = foreground.GetPixelv(pixel);
            if (Math.Abs(oldPixel.R - newPixel.R) + Math.Abs(oldPixel.G - newPixel.G)
                + Math.Abs(oldPixel.B - newPixel.B) > 0.15f) changed++;
        }
        _test.True(changed > samples * 0.1f, "实际前方高台应遮挡树冠中与它重叠的部分。");
        front.SetBaseHeight(baseHeight);
        _fixture.State.RebuildCellColumns();
        await Refresh();
    }

    private async Task<Image> Capture()
    {
        await ToSignal(this, SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        return Root.GetTexture().GetImage();
    }

    private static void SaveCapture(Image image, string name)
    {
        string directory = System.Environment.GetEnvironmentVariable("MAGIC_BATTLE_TREE_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        System.IO.Directory.CreateDirectory(directory);
        Error result = image.SavePng(System.IO.Path.Combine(directory, name + ".png"));
        if (result != Error.Ok) throw new InvalidOperationException($"Tree capture failed: {result}");
    }
}
