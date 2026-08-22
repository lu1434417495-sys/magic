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
        TestBuildsDropFacesFromCells();
        TestTemporaryEdgeFeatureOverlayBlocksMovement();
        TestDirtyRuntimeEdgesRebuildAfterTemporaryFeature();

        RequestTestExit(_test.Finish("Battle edge face service regression"));
    }

    private void TestBuildsDropFacesFromCells()
    {
        BattleState state = BuildTwoCellState(2, 0);
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
        _test.False(edgeFace.HasFeatureFace(), "plain cells should not produce a feature face.");
        _test.False(
            edgeService.IsTraversableBetween(state, new Vector2I(0, 0), new Vector2I(1, 0)),
            "high drop should not be traversable."
        );
        _test.True(
            edgeService.BlocksOccupancyBetween(state, new Vector2I(0, 0), new Vector2I(1, 0)),
            "high drop should block occupancy."
        );
    }

    private void TestTemporaryEdgeFeatureOverlayBlocksMovement()
    {
        BattleState state = BuildTwoCellState(0, 0);
        var edgeService = new BattleEdgeService();

        _test.True(
            edgeService.IsTraversableBetween(state, new Vector2I(0, 0), new Vector2I(1, 0)),
            "flat edge should initially be traversable."
        );

        _test.True(
            state.PutTemporaryEdgeFeature(
                BuildTemporaryWall(new Vector2I(0, 0), Vector2I.Right),
                refreshExisting: false,
                maxActiveEdges: 0
            ),
            "temporary wall should be accepted by the state."
        );

        BattleEdgeFaceState edgeFace =
            edgeService.GetEdgeFace(state, new Vector2I(0, 0), new Vector2I(1, 0));

        _test.True(edgeFace != null, "edge face should still resolve after the temporary overlay.");
        if (edgeFace == null)
        {
            return;
        }

        _test.True(edgeFace.HasFeatureFace(), "temporary wall should produce a feature face.");
        _test.True(edgeFace.BlocksMove(), "temporary wall should block movement.");
        _test.True(edgeFace.BlocksOccupancy(), "temporary wall should block occupancy.");
        _test.False(
            edgeService.IsTraversableBetween(state, new Vector2I(0, 0), new Vector2I(1, 0)),
            "temporary wall edge should not be traversable."
        );
        _test.True(
            edgeService.BlocksOccupancyBetween(state, new Vector2I(0, 0), new Vector2I(1, 0)),
            "temporary wall edge should block occupancy."
        );
    }

    private void TestDirtyRuntimeEdgesRebuildAfterTemporaryFeature()
    {
        BattleState state = BuildTwoCellState(0, 0);
        var edgeService = new BattleEdgeService();

        _test.True(
            edgeService.IsTraversableBetween(state, new Vector2I(0, 0), new Vector2I(1, 0)),
            "flat edge should initially be traversable."
        );

        _test.True(
            state.PutTemporaryEdgeFeature(
                BuildTemporaryWall(new Vector2I(0, 0), Vector2I.Right),
                refreshExisting: false,
                maxActiveEdges: 0
            ),
            "temporary wall should be accepted by the state."
        );

        _test.False(
            edgeService.IsTraversableBetween(state, new Vector2I(0, 0), new Vector2I(1, 0)),
            "dirty edge cache should rebuild and apply the newly added temporary wall."
        );
    }

    private static BattleTemporaryEdgeFeatureState BuildTemporaryWall(
        Vector2I originCoord,
        Vector2I direction
    )
    {
        return new BattleTemporaryEdgeFeatureState
        {
            OriginCoord = originCoord,
            Direction = direction,
            BindingId = "edge_face_service_wall",
            ActionId = "edge_face_service_wall",
            CreatedAtTu = 0,
            ExpiresAtTu = 100,
            Feature = BattleEdgeFeatureState.MakeWall(),
        };
    }

    private static BattleState BuildTwoCellState(int westHeight, int eastHeight)
    {
        var state = new BattleState { map_size = new Vector2I(2, 1) };
        state.SetCell(BuildCell(new Vector2I(0, 0), westHeight));
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
