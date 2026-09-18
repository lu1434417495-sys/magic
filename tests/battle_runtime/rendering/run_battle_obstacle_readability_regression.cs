using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class run_battle_obstacle_readability_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private readonly BattleGridService _grid = new();
    private readonly BattleBoardSnapshotBuilder _builder = new();
    private BattleTestFixture _fixture;
    private BattleBoard2D _board;
    private static readonly Vector2I Deep = new(1, 5);
    private static readonly Vector2I Shallow = new(3, 5);

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private async void Run()
    {
        try
        {
            var ally = BattleTestFixture.BuildUnit("ally", "ally", new(4, 3));
            ally.source_member_id = "obstacle_visual_fixture";
            _fixture = BattleTestFixture.CreateFlatBattle("obstacle_readability", new(9, 8),
                new[] { ally }, Array.Empty<BattleUnitState>());
            BuildTerrain();
            _board = EngineAssetAccess.ResolveCodeAssetBorrowed<PackedScene>(
                "res://scenes/ui/battle_board_2d.tscn").Instantiate<BattleBoard2D>();
            Root.AddChild(_board);
            await ToSignal(this, SignalName.ProcessFrame);
            BattleBoardRenderSnapshot snapshot = _builder.Build(_fixture.State);
            _board.Configure(snapshot, ally.GetAnchorCoord());
            await ToSignal(this, SignalName.ProcessFrame);
            AssertBoundariesMatchMovement(snapshot);
            AssertWallStandsAboveGround();

            if (DisplayServer.GetName() != "headless")
            {
                foreach (Vector2I size in new[] { new Vector2I(1280, 720), new(3840, 2160) })
                {
                    FitBoard(size);
                    using Image image = await Capture();
                    SaveCapture(image, $"obstacles_{size.X}x{size.Y}");
                    AssertWaterContrast(image);
                    await AssertWallVisible(image, size);
                }
            }
            else
                ConsoleProcessOutput.WriteStandard("Native obstacle pixel checks skipped in headless mode.");

            // A runtime terrain rebuild must remove obsolete movement cues and walls.
            _fixture.State.ReplaceTemporaryEdgeFeaturesTyped(Array.Empty<BattleTemporaryEdgeFeatureState>());
            foreach (BattleCellState cell in _fixture.State.Cells()) cell.SetBaseHeight(0);
            _fixture.State.RebuildCellColumns();
            _fixture.State.MarkRuntimeEdgesDirty();
            BattleBoardRenderSnapshot cleared = _builder.Build(_fixture.State);
            _test.True(snapshot.Edges.Any(edge => edge.BlocksMovement), "旧 detached 快照保留原阻挡事实。");
            _test.False(cleared.Edges.Any(edge => edge.BlocksMovement), "平地且移除墙后不能保留旧阻挡事实。");
            _board.Configure(cleared, ally.GetAnchorCoord());
            await ToSignal(this, SignalName.ProcessFrame);
            _test.False(_board.GetChildren().Any(node => node.Name.ToString().StartsWith("BlockedEdge_")),
                "刷新后旧陡坎提示必须释放。");
            _test.False(_board.GetChildren().Any(node => node.Name.ToString().StartsWith("RaisedWall_")),
                "临时墙结束后实体墙必须消失。");
        }
        catch (Exception exception) { _test.Fail($"Obstacle readability: {exception}"); }
        finally
        {
            _board?.Free();
            _fixture?.Dispose();
            RequestTestExit(_test.Finish("Battle obstacle readability regression"));
        }
    }

    private void BuildTerrain()
    {
        foreach (BattleCellState cell in _fixture.State.Cells())
        {
            Vector2I coord = cell.coord;
            cell.SetBaseHeight(coord.X <= 2 && coord.Y <= 2 ? 2
                : coord.X >= 6 && coord.X <= 7 && coord.Y >= 1 && coord.Y <= 3 ? 2 : 0);
            cell.SetTerrain(coord.X <= 2 && coord.X >= 1 && coord.Y >= 5 && coord.Y <= 6 ? "deep_water"
                : coord.X >= 3 && coord.X <= 4 && coord.Y >= 5 && coord.Y <= 6 ? "shallow_water" : "land");
        }
        _fixture.State.GetCell(new(2, 3)).SetBaseHeight(1);
        _fixture.State.GetCell(new(1, 3)).SetTerrain("forest");
        _fixture.State.RebuildCellColumns();
        _fixture.State.MarkRuntimeEdgesDirty();
        foreach ((Vector2I coord, Vector2I direction) in new[] {
            (new Vector2I(6, 5), Vector2I.Right), (new Vector2I(6, 6), Vector2I.Right),
            (new Vector2I(5, 5), Vector2I.Down) })
        {
            _fixture.State.PutTemporaryEdgeFeature(new()
            {
                OriginCoord = coord, Direction = direction,
                BindingId = "visual_wall", ActionId = "visual_wall",
                ExpiresAtTu = 100, Feature = BattleEdgeFeatureState.MakeWall(),
            }, refreshExisting: false, maxActiveEdges: 0);
        }
    }

    private void AssertBoundariesMatchMovement(BattleBoardRenderSnapshot snapshot)
    {
        foreach (BattleCellState cell in _fixture.State.Cells())
        foreach (Vector2I direction in new[] { Vector2I.Right, Vector2I.Down })
        {
            Vector2I target = cell.coord + direction;
            if (!snapshot.ContainsCell(target)) continue;
            bool blocked = !_grid.CanCrossEdgeBetween(_fixture.State, cell.coord, target);
            BattleBoardEdgeSnapshot edge = snapshot.Edges.FirstOrDefault(
                candidate => candidate.OriginCoord == cell.coord && candidate.NeighborCoord == target);
            _test.Eq(edge?.BlocksMovement ?? false, blocked,
                $"{cell.coord} → {target}: 可视边界必须与正式跨边判定相同，包括背向镜头的上坡。");
        }
        _test.True(_grid.CanCrossEdgeBetween(_fixture.State, new(2, 2), new(2, 3)), "一层高差保留通行。");
        _test.False(_grid.CanCrossEdgeBetween(_fixture.State, new(5, 2), new(6, 2)), "两层上坡确实阻挡。");
        BattleUnitState walker = _fixture.Allies[0];
        _test.True(_grid.CanUnitStepBetweenAnchors(_fixture.State, walker, new(1, 4), new(1, 3)),
            "森林并不是不可行走的障碍。");
        _test.True(_grid.CanUnitStepBetweenAnchors(_fixture.State, walker, Shallow + Vector2I.Up, Shallow),
            "浅水允许普通移动。");
        _test.False(_grid.CanUnitStepBetweenAnchors(_fixture.State, walker, Deep + Vector2I.Up, Deep),
            "深水需要特殊移动能力。");
        _test.False(_grid.CanCrossEdgeBetween(_fixture.State, new(6, 5), new(7, 5)), "实体墙边界确实阻挡。");
    }

    private void AssertWallStandsAboveGround()
    {
        var wall = _board.GetNode<BattleTerrainPaintLayer>("RaisedWall_6_5_True");
        float groundY = (_board._get_coord_anchor(new(6, 5)).Y + _board._get_coord_anchor(new(7, 5)).Y) / 2f;
        _test.True(wall.Patches.SelectMany(patch => patch.Points).Min(point => point.Y) < groundY - 60,
            "临时墙必须立在地面上，不能画成地面以下的岩壁。");
    }

    private void FitBoard(Vector2I size)
    {
        Root.ContentScaleSize = Vector2I.Zero;
        Root.Size = size;
        _board.SetViewportSize(size);
        Rect2 bounds = _board._content_bounds;
        float scale = Mathf.Min(size.X * 0.94f / bounds.Size.X, size.Y * 0.89f / bounds.Size.Y);
        _board.Scale = Vector2.One * scale;
        _board.Position = (Vector2)size * 0.5f - bounds.GetCenter() * scale;
    }

    private void AssertWaterContrast(Image image)
    {
        Color deep = SampleGround(image, Deep);
        Color shallow = SampleGround(image, Shallow);
        float deepBrightness = deep.R * 0.2126f + deep.G * 0.7152f + deep.B * 0.0722f;
        float shallowBrightness = shallow.R * 0.2126f + shallow.G * 0.7152f + shallow.B * 0.0722f;
        _test.True(shallowBrightness > deepBrightness + 0.16f,
            $"深浅水必须有足够原生像素明度差：deep={deepBrightness:F3}, shallow={shallowBrightness:F3}。");
        _test.True(deep.B > deep.G, "深水应为可辨认的深蓝色。");
        ConsoleProcessOutput.WriteStandard($"Water luminance: deep={deepBrightness:F3}, shallow={shallowBrightness:F3}");
    }

    private Color SampleGround(Image image, Vector2I coord)
    {
        Vector2 point = _board.ToGlobal(_board._get_coord_anchor(coord) + new Vector2(8, 3));
        return image.GetPixel((int)point.X, (int)point.Y);
    }

    private async Task AssertWallVisible(Image composite, Vector2I size)
    {
        CanvasItem[] walls = _board.GetChildren().OfType<CanvasItem>().Where(node =>
            node.Name.ToString().StartsWith("RaisedWall_") || node.Name.ToString().StartsWith("WallEnd_")
            || node.Name.ToString().StartsWith("WallCap_") || node.Name.ToString().StartsWith("WallMasonry_")).ToArray();
        foreach (CanvasItem wall in walls) wall.Hide();
        using Image withoutWalls = await Capture();
        foreach (CanvasItem wall in walls) wall.Show();
        Vector2[] corners = _board.GetNode<BattleTerrainPaintLayer>("RaisedWall_6_5_True")
            .Patches[0].Points.Select(_board.ToGlobal).ToArray();
        int left = (int)corners.Min(point => point.X), right = (int)corners.Max(point => point.X);
        int top = (int)corners.Min(point => point.Y), bottom = (int)corners.Max(point => point.Y);
        int changed = 0;
        int stride = Math.Max(1, size.X / 1280);
        for (int y = Math.Max(0, top); y < Math.Min(size.Y, bottom); y += stride)
        for (int x = Math.Max(0, left); x < Math.Min(size.X, right); x += stride)
        {
            Color a = composite.GetPixel(x, y), b = withoutWalls.GetPixel(x, y);
            if (Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) > 0.15f) changed++;
        }
        _test.True(changed > 200, $"{size}: 墙体必须实际可见，不能被地面/格线完全盖住 ({changed})。");
        ConsoleProcessOutput.WriteStandard($"Visible wall pixels {size}: {changed}");
    }

    private async Task<Image> Capture()
    {
        await ToSignal(this, SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        return Root.GetTexture().GetImage();
    }

    private static void SaveCapture(Image image, string name)
    {
        string directory = System.Environment.GetEnvironmentVariable("MAGIC_BATTLE_HUD_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        System.IO.Directory.CreateDirectory(directory);
        Error error = image.SavePng(System.IO.Path.Combine(directory, name + ".png"));
        if (error != Error.Ok) throw new InvalidOperationException($"Obstacle capture: {error}");
    }
}
