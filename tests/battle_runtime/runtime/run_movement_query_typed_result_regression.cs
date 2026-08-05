using System;
using System.Reflection;
using Godot;

public partial class run_movement_query_typed_result_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestMovementQueryEntryPointsReturnTypedResults();
        TestMovementQueryResultsExposeTypedCollections();
        TestActorPathWorkspaceReusesExactTreeAcrossFocusTargets();
        RequestTestExit(_test.Finish("Movement query typed result regression"));
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
        BattleUnitState actor = BuildUnit("actor", "player", new Vector2I(0, 0));
        BattleUnitState target = BuildUnit("target", "enemy", new Vector2I(2, 0));
        InstallUnit(gridService, state, actor);
        InstallUnit(gridService, state, target);

        service.BeginBattle(1);
        service.Setup(1, state, gridService, FixedMoveCost);

        MovementReachabilityResult reachable = service.CollectReachableAnchors(
            actor.unit_id,
            actor.GetAnchorCoord(),
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

        BattleMovementQueryService.CacheDiagnostics firstDiagnostics =
            service.CaptureCacheDiagnostics();
        _test.Eq(firstDiagnostics.PathTargetCacheMissCount, 1L, "首次 path query 应记一次 miss。");
        _test.Eq(firstDiagnostics.PathTargetCacheHitCount, 0L, "首次 path query 不应命中 cache。");

        service.ClearRuntimeBindings();
        _test.False(
            service.CaptureCacheDiagnostics().DecisionBound,
            "decision unbind 后不应保留 live state/grid/callback borrower。"
        );
        service.Setup(1, state, gridService, FixedMoveCost);
        MovementPathTargetResult cachedPathTargets = service.CollectDistanceBandPathTargetsTyped(
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
        BattleMovementQueryService.CacheDiagnostics reboundDiagnostics =
            service.CaptureCacheDiagnostics();
        _test.True(cachedPathTargets.Ok, "same-epoch decision rebind 后 path query 应成功。");
        _test.Eq(
            reboundDiagnostics.SnapshotRebuildCount,
            firstDiagnostics.SnapshotRebuildCount,
            "相同 geometry revision 的 decision rebind 不应重建 snapshot。"
        );
        _test.Eq(reboundDiagnostics.PathTargetCacheHitCount, 1L, "跨 decision 应复用纯 path cache。");

        service.Dispose();
    }

    private void TestActorPathWorkspaceReusesExactTreeAcrossFocusTargets()
    {
        var service = new BattleMovementQueryService();
        var gridService = new BattleGridService();
        BattleState state = BuildState(new Vector2I(8, 3));
        BattleUnitState actor = BuildUnit("workspace_actor", "player", new Vector2I(0, 1));
        BattleUnitState firstTarget = BuildUnit(
            "workspace_target_near",
            "enemy",
            new Vector2I(3, 0)
        );
        BattleUnitState secondTarget = BuildUnit(
            "workspace_target_far",
            "enemy",
            new Vector2I(7, 2)
        );
        InstallUnit(gridService, state, actor);
        InstallUnit(gridService, state, firstTarget);
        InstallUnit(gridService, state, secondTarget);
        service.Setup(2, state, gridService, FixedMoveCost);

        var emptyOverlay = new BattleVirtualBoardOverlay();
        MovementPathTargetResult firstReference =
            service.CollectDistanceBandPathTargetsTyped(
                actor.unit_id,
                firstTarget.unit_id,
                1,
                2,
                3,
                0,
                6,
                0,
                false,
                true,
                emptyOverlay
            );
        MovementPathTargetResult secondReference =
            service.CollectDistanceBandPathTargetsTyped(
                actor.unit_id,
                secondTarget.unit_id,
                1,
                2,
                3,
                0,
                6,
                0,
                false,
                true,
                emptyOverlay
            );
        service.CollectDistanceBandPathTargetsTyped(
            actor.unit_id,
            firstTarget.unit_id,
            1,
            2,
            3,
            64,
            6,
            0,
            false,
            true
        );
        BattleMovementQueryService.CacheDiagnostics fallbackDiagnostics =
            service.CaptureCacheDiagnostics();
        _test.Eq(
            fallbackDiagnostics.ActorPathWorkspaceBuildCount,
            0L,
            "overlay 与 maxNodes 有界查询不得创建共享 actor workspace。"
        );
        _test.Eq(
            fallbackDiagnostics.ActorPathWorkspaceReuseCount,
            0L,
            "overlay 与 maxNodes 有界查询不得复用共享 actor workspace。"
        );

        MovementPathTargetResult firstOptimized =
            service.CollectDistanceBandPathTargetsTyped(
                actor.unit_id,
                firstTarget.unit_id,
                1,
                2,
                3,
                0,
                6,
                0,
                false,
                true
            );
        BattleMovementQueryService.CacheDiagnostics firstDiagnostics =
            service.CaptureCacheDiagnostics();
        MovementPathTargetResult secondOptimized =
            service.CollectDistanceBandPathTargetsTyped(
                actor.unit_id,
                secondTarget.unit_id,
                1,
                2,
                3,
                0,
                6,
                0,
                false,
                true
            );
        BattleMovementQueryService.CacheDiagnostics secondDiagnostics =
            service.CaptureCacheDiagnostics();

        AssertPathTargetGameplayEqual(
            firstOptimized,
            firstReference,
            "首个 focus target"
        );
        AssertPathTargetGameplayEqual(
            secondOptimized,
            secondReference,
            "后续 focus target"
        );
        _test.Eq(
            firstDiagnostics.ActorPathWorkspaceBuildCount,
            1L,
            "首个默认 path query 应构建一次 actor workspace。"
        );
        _test.Eq(
            secondDiagnostics.ActorPathWorkspaceBuildCount,
            1L,
            "切换 focus target 不应重建 actor workspace。"
        );
        _test.True(
            secondDiagnostics.ActorPathWorkspaceReuseCount
                > firstDiagnostics.ActorPathWorkspaceReuseCount,
            "后续 focus target 应续跑同一 actor workspace。"
        );
        service.Dispose();
    }

    private void AssertPathTargetGameplayEqual(
        MovementPathTargetResult actual,
        MovementPathTargetResult expected,
        string label
    )
    {
        _test.Eq(actual.Ok, expected.Ok, $"{label} 应保留 query 成功状态。");
        _test.Eq(
            actual.RejectReason,
            expected.RejectReason,
            $"{label} 应保留 reject reason。"
        );
        _test.Eq(
            actual.DestinationCount,
            expected.DestinationCount,
            $"{label} 应保留 destination count。"
        );
        _test.Eq(
            actual.ReachedDestinationCount,
            expected.ReachedDestinationCount,
            $"{label} 应保留 reached destination count。"
        );
        _test.Eq(
            actual.UnreachableDestinationCount,
            expected.UnreachableDestinationCount,
            $"{label} 应保留 unreachable destination count。"
        );
        _test.Eq(
            actual.PathRejectCount,
            expected.PathRejectCount,
            $"{label} 应保留 path reject count。"
        );
        _test.Eq(
            actual.SkippedOriginCount,
            expected.SkippedOriginCount,
            $"{label} 应保留 skipped origin count。"
        );
        _test.Eq(
            actual.Candidates.Count,
            expected.Candidates.Count,
            $"{label} 应保留候选数量。"
        );
        int count = Math.Min(actual.Candidates.Count, expected.Candidates.Count);
        for (int index = 0; index < count; index++)
        {
            MovementPathTargetCandidate actualCandidate = actual.Candidates[index];
            MovementPathTargetCandidate expectedCandidate = expected.Candidates[index];
            _test.Eq(
                actualCandidate.DestinationCoord,
                expectedCandidate.DestinationCoord,
                $"{label} candidate[{index}] 应保留 destination。"
            );
            _test.Eq(
                actualCandidate.Coord,
                expectedCandidate.Coord,
                $"{label} candidate[{index}] 应保留落点。"
            );
            _test.Eq(
                actualCandidate.PathCost,
                expectedCandidate.PathCost,
                $"{label} candidate[{index}] 应保留 path cost。"
            );
            _test.Eq(
                actualCandidate.PathLength,
                expectedCandidate.PathLength,
                $"{label} candidate[{index}] 应保留 path length。"
            );
            _test.Eq(
                actualCandidate.SpentCost,
                expectedCandidate.SpentCost,
                $"{label} candidate[{index}] 应保留 spent cost。"
            );
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
        }.WithCombatResourcesForTest(
            isAlive: true
        );
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
        gridService.PlaceUnit(state, unit, unit.GetAnchorCoord(), true);
    }

    private static int FixedMoveCost(StringName unitId, Vector2I fromCoord, Vector2I toCoord) => 1;
}
