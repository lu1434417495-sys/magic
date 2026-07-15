using Godot;

public partial class run_battle_edge_face_service_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestBuildsDropAndFeatureFacesFromCells();
        TestDirtyRuntimeEdgesRebuildAfterCellMutation();

        RequestTestExit(_test.Finish("Battle edge face service regression"));
    }

    private void TestBuildsDropAndFeatureFacesFromCells()
    {
        BattleState state = BuildTwoCellState(2, 0, BattleEdgeFeatureState.MakeWall());
        var edgeService = new BattleEdgeService();

        BattleEdgeFaceState edgeFace =
            edgeService.GetEdgeFace(state, new Vector2I(0, 0), new Vector2I(1, 0));

        _test.True(edgeFace != null, "edge service should build an edge face between adjacent cells.");
        if (edgeFace == null)
        {
            return;
        }

        _test.Eq(edgeFace.height_difference, 2, "edge face should capture adjacent height delta.");
        _test.Eq(edgeFace.drop_layers, 2, "edge face should expose both descending layer faces.");
        _test.True(edgeFace.HasDropFace(), "height drop should produce a drop face.");
        _test.True(edgeFace.HasFeatureFace(), "authored wall should produce a feature face.");
        _test.True(edgeFace.BlocksMove(), "authored wall should block movement.");
        _test.True(edgeFace.BlocksOccupancy(), "authored wall should block occupancy.");
        _test.False(
            edgeService.IsTraversableBetween(state, new Vector2I(0, 0), new Vector2I(1, 0)),
            "blocking wall/high drop should not be traversable."
        );
        _test.True(
            edgeService.BlocksOccupancyBetween(state, new Vector2I(0, 0), new Vector2I(1, 0)),
            "blocking wall/high drop should block occupancy."
        );
        _test.True(
            edgeService.HasFeatureBetween(
                state,
                new Vector2I(0, 0),
                new Vector2I(1, 0),
                BattleEdgeFeatureKind.Wall
            ),
            "edge service should preserve authored wall feature kind."
        );
    }

    private void TestDirtyRuntimeEdgesRebuildAfterCellMutation()
    {
        BattleState state = BuildTwoCellState(0, 0, BattleEdgeFeatureState.MakeNone());
        var edgeService = new BattleEdgeService();

        _test.True(
            edgeService.IsTraversableBetween(state, new Vector2I(0, 0), new Vector2I(1, 0)),
            "flat edge should initially be traversable."
        );

        BattleCellState west = state.GetCell(new Vector2I(0, 0));
        west.SetEdgeFeature(Vector2I.Right, BattleEdgeFeatureState.MakeWall());
        state.MarkRuntimeEdgesDirty();

        _test.False(
            edgeService.IsTraversableBetween(state, new Vector2I(0, 0), new Vector2I(1, 0)),
            "dirty edge cache should rebuild and apply newly authored wall."
        );
    }

    private static BattleState BuildTwoCellState(
        int westHeight,
        int eastHeight,
        BattleEdgeFeatureState eastFeature
    )
    {
        var state = new BattleState { map_size = new Vector2I(2, 1) };
        BattleCellState west = BuildCell(new Vector2I(0, 0), westHeight);
        west.SetEdgeFeature(Vector2I.Right, eastFeature);
        state.SetCell(west);
        state.SetCell(BuildCell(new Vector2I(1, 0), eastHeight));
        return state;
    }

    private static BattleCellState BuildCell(Vector2I coord, int height)
    {
        var cell = new BattleCellState();
        cell.SetCoord(coord);
        cell.SetBaseHeight(height);
        cell.SetPassable(true);
        return cell;
    }
}
