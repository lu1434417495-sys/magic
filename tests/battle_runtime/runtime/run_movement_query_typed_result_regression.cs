using System;
using System.Reflection;
using Godot;

public partial class run_movement_query_typed_result_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestMovementQueryEntryPointsReturnTypedResults();
        TestMovementQueryResultsExposeTypedCollections();
        Quit(_test.Finish("Movement query typed result regression"));
    }

    private void TestMovementQueryEntryPointsReturnTypedResults()
    {
        AssertReturnType(
            "CollectReachableAnchors",
            typeof(MovementReachabilityResult),
            typeof(StringName),
            typeof(Vector2I),
            typeof(int),
            typeof(BattleVirtualBoardOverlay),
            typeof(BattleMovementQueryService.MovementQueryOptions)
        );
        AssertReturnType(
            "CollectDistanceBandDestinations",
            typeof(MovementDistanceBandResult),
            typeof(StringName),
            typeof(StringName),
            typeof(int),
            typeof(int),
            typeof(BattleVirtualBoardOverlay),
            typeof(BattleMovementQueryService.MovementQueryOptions)
        );
        AssertReturnType(
            "CollectDistanceBandPathTargetsTyped",
            typeof(MovementPathTargetResult),
            typeof(StringName),
            typeof(StringName),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(bool),
            typeof(bool),
            typeof(BattleVirtualBoardOverlay)
        );
    }

    private void TestMovementQueryResultsExposeTypedCollections()
    {
        var service = new BattleMovementQueryService();
        var gridService = new BattleGridService();
        BattleState state = BuildState(new Vector2I(3, 1));
        try
        {
            BattleUnitState actor = BuildUnit("actor", "player", new Vector2I(0, 0));
            BattleUnitState target = BuildUnit("target", "enemy", new Vector2I(2, 0));
            InstallUnit(gridService, state, actor);
            InstallUnit(gridService, state, target);

            service.Setup(state, gridService, FixedMoveCost);

            MovementReachabilityResult reachable = service.CollectReachableAnchors(
                actor.unit_id,
                actor.coord,
                1
            );
            _test.True(reachable.Ok, "reachable query 应成功。");
            _test.True(
                reachable.Coords.GetType() != typeof(Godot.Collections.Array<Vector2I>),
                "reachable Coords 真相源不应是 Godot Array。"
            );
            _test.Eq(reachable.Coords[0], new Vector2I(1, 0), "reachable query 应返回 typed 坐标。");

            MovementDistanceBandResult distanceBand = service.CollectDistanceBandDestinations(
                actor.unit_id,
                target.unit_id,
                1,
                1
            );
            _test.True(distanceBand.Ok, "distance band query 应成功。");
            _test.True(
                distanceBand.Coords.GetType() != typeof(Godot.Collections.Array<Vector2I>),
                "distance band Coords 真相源不应是 Godot Array。"
            );
            _test.Eq(distanceBand.Coords[0], new Vector2I(1, 0), "distance band query 应返回 typed 坐标。");

            MovementPathTargetResult pathTargets = service.CollectDistanceBandPathTargetsTyped(
                actor.unit_id,
                target.unit_id,
                1,
                1,
                1,
                0,
                4,
                0,
                false,
                true
            );
            _test.True(pathTargets.Ok, "path target query 应成功。");
            _test.True(pathTargets.Candidates.Count > 0, "path target query 应返回候选。");
            MovementPathTargetCandidate candidate = pathTargets.Candidates[0];
            _test.Eq(candidate.Coord, new Vector2I(1, 0), "path target query 应返回 typed 目标坐标。");
            _test.Eq(candidate.PathCost, 1, "path target query 应返回 typed path cost。");
        }
        finally
        {
            service.Dispose();
            BattleTestFixture.DisposeBattleState(state);
        }
    }

    private void AssertReturnType(string methodName, Type expectedReturnType, params Type[] parameterTypes)
    {
        MethodInfo method = typeof(BattleMovementQueryService).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            parameterTypes,
            null
        );
        if (method == null)
        {
            _test.Fail($"missing method {methodName}");
            return;
        }
        _test.Eq(method.ReturnType, expectedReturnType, $"{methodName} 应返回 typed result。");
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState { map_size = mapSize };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                state.SetCell(
                    coord,
                    new BattleCellState
                    {
                        coord = coord,
                        passable = true,
                        base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    }
                );
            }
        }
        return state;
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName factionId, Vector2I coord)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            faction_id = factionId,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.SetCurrentMovePoints(3);
        return unit;
    }

    private static void InstallUnit(
        BattleGridService gridService,
        BattleState state,
        BattleUnitState unit
    )
    {
        state.SetUnit(unit);
        gridService.PlaceUnit(state, unit, unit.coord, true);
    }

    private static int FixedMoveCost(StringName unitId, Vector2I fromCoord, Vector2I toCoord) => 1;
}
