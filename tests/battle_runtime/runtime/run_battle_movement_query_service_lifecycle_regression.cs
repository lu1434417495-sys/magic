using System;
using Godot;

public partial class run_battle_movement_query_service_lifecycle_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestDecisionUnbindRetainsCacheAndBattleDisposeAllowsRebind();
        TestSameEpochOwnerMismatchInvalidatesRetainedCache();
        TestRuntimeStateReplacementRotatesBattleCacheEpoch();
        TestBattleEndCheckDoesNotInvalidateMovementGeometry();
        TestDisposedServiceRejectsRebind();
        RequestTestExit(_test.Finish("Battle movement query service lifecycle regression"));
    }

    private void TestSameEpochOwnerMismatchInvalidatesRetainedCache()
    {
        var service = new BattleMovementQueryService();
        var gridService = new BattleGridService();
        BattleState firstState = BuildState(new Vector2I(3, 1));
        BattleUnitState firstUnit = BuildUnit("same_epoch_first", Vector2I.Zero);
        InstallUnit(gridService, firstState, firstUnit);
        service.Setup(7, firstState, gridService, FixedMoveCost);
        _test.True(
            service.CollectReachableAnchors(firstUnit.unit_id, firstUnit.coord, 1).Ok,
            "same-epoch owner mismatch 回归前置：首个 state 查询应成功。"
        );

        service.ClearRuntimeBindings();
        BattleState secondState = BuildState(new Vector2I(3, 1));
        BattleUnitState secondUnit = BuildUnit("same_epoch_second", new Vector2I(1, 0));
        InstallUnit(gridService, secondState, secondUnit);
        service.Setup(7, secondState, gridService, FixedMoveCost);

        _test.True(
            service.CollectReachableAnchors(secondUnit.unit_id, secondUnit.coord, 1).Ok,
            "同 epoch 误绑新 state 时应清空 retained cache 并读取新单位。"
        );
        _test.Eq(
            service.CollectReachableAnchors(firstUnit.unit_id, firstUnit.coord, 1).RejectReason,
            new StringName("missing_unit"),
            "同 epoch owner 变化后不得继续暴露旧 state 单位。"
        );
        service.Dispose();
    }

    private void TestDecisionUnbindRetainsCacheAndBattleDisposeAllowsRebind()
    {
        var service = new BattleMovementQueryService();
        var gridService = new BattleGridService();
        BattleState firstState = BuildState(new Vector2I(3, 1));
        BattleUnitState firstUnit = BuildUnit("first_unit", Vector2I.Zero);
        InstallUnit(gridService, firstState, firstUnit);

        service.BeginBattle(1);
        service.Setup(1, firstState, gridService, FixedMoveCost);
        MovementReachabilityResult firstQuery = service.CollectReachableAnchors(
            firstUnit.unit_id,
            firstUnit.coord,
            1
        );
        _test.True(firstQuery.Ok, "初次 setup 后应能查询 first_unit 可达格。");

        BattleMovementQueryService.CacheDiagnostics firstDiagnostics =
            service.CaptureCacheDiagnostics();
        _test.Eq(firstDiagnostics.SnapshotRebuildCount, 1L, "首次 bind 应构建一次 snapshot。");

        service.ClearRuntimeBindings();
        MovementReachabilityResult disposedQuery = service.CollectReachableAnchors(
            firstUnit.unit_id,
            firstUnit.coord,
            1
        );
        _test.False(disposedQuery.Ok, "decision unbind 后不应继续读旧 BattleState。");
        _test.Eq(
            disposedQuery.RejectReason,
            new StringName("missing_unit"),
            "decision unbind 后旧 unit 查询应以 missing_unit 失败。"
        );
        BattleMovementQueryService.CacheDiagnostics unboundDiagnostics =
            service.CaptureCacheDiagnostics();
        _test.False(unboundDiagnostics.DecisionBound, "decision cleanup 应清空 live borrower。");
        _test.Eq(
            unboundDiagnostics.SnapshotRebuildCount,
            firstDiagnostics.SnapshotRebuildCount,
            "decision cleanup 应保留 battle-lifetime snapshot cache。"
        );

        service.Setup(1, firstState, gridService, FixedMoveCost);
        _test.Eq(
            service.CaptureCacheDiagnostics().SnapshotRebuildCount,
            firstDiagnostics.SnapshotRebuildCount,
            "same-epoch rebind 不应因 borrower identity 重建 snapshot。"
        );
        _test.True(
            gridService.MoveUnit(firstState, firstUnit, new Vector2I(1, 0)),
            "测试移动应更新 movement geometry revision。"
        );
        service.Setup(1, firstState, gridService, FixedMoveCost);
        _test.Eq(
            service.CaptureCacheDiagnostics().SnapshotRebuildCount,
            firstDiagnostics.SnapshotRebuildCount + 1,
            "geometry revision 变化后应恰好重建一次 snapshot。"
        );

        service.DisposeRuntime();

        BattleState secondState = BuildState(new Vector2I(3, 1));
        BattleUnitState secondUnit = BuildUnit("second_unit", new Vector2I(1, 0));
        InstallUnit(gridService, secondState, secondUnit);

        service.BeginBattle(2);
        service.Setup(2, secondState, gridService, FixedMoveCost);
        MovementReachabilityResult reboundQuery = service.CollectReachableAnchors(
            secondUnit.unit_id,
            secondUnit.coord,
            1
        );
        _test.True(reboundQuery.Ok, "DisposeRuntime 后应允许重新 setup 新 BattleState。");

        MovementReachabilityResult oldUnitAfterRebind = service.CollectReachableAnchors(
            firstUnit.unit_id,
            firstUnit.coord,
            1
        );
        _test.False(
            oldUnitAfterRebind.Ok,
            "重新 setup 后不应残留旧 BattleState 的 first_unit。"
        );
        _test.Eq(
            oldUnitAfterRebind.RejectReason,
            new StringName("missing_unit"),
            "重新 setup 后旧 unit 查询应以 missing_unit 失败。"
        );
        BattleMovementQueryService.CacheDiagnostics secondDiagnostics =
            service.CaptureCacheDiagnostics();
        _test.Eq(secondDiagnostics.BattleEpoch, 2L, "新 battle 应切换 cache epoch。");
        _test.Eq(secondDiagnostics.SnapshotRebuildCount, 1L, "新 battle 应从空 cache 构建一次。");

        service.DisposeRuntime();
        service.Dispose();
    }

    private void TestRuntimeStateReplacementRotatesBattleCacheEpoch()
    {
        var runtime = new BattleRuntimeModule();
        try
        {
            BattleState firstState = BuildState(new Vector2I(2, 1));
            BattleState secondState = BuildState(new Vector2I(2, 1));

            runtime.SetupStateForTests(firstState);
            long firstEpoch = runtime.GetAiMovementQueryCacheDiagnostics().BattleEpoch;
            _test.True(firstEpoch != long.MinValue, "绑定 battle state 后应建立 cache epoch。");

            runtime.SetupStateForTests(firstState);
            _test.Eq(
                runtime.GetAiMovementQueryCacheDiagnostics().BattleEpoch,
                firstEpoch,
                "重复绑定同一 state 不应轮换 cache epoch。"
            );

            runtime.SetupStateForTests(secondState);
            long secondEpoch = runtime.GetAiMovementQueryCacheDiagnostics().BattleEpoch;
            _test.True(secondEpoch != firstEpoch, "替换 battle state 应轮换 cache epoch。");

            runtime.SetupStateForTests(null);
            _test.Eq(
                runtime.GetAiMovementQueryCacheDiagnostics().BattleEpoch,
                long.MinValue,
                "清空 battle state 应同步结束 battle-lifetime cache。"
            );
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private void TestDisposedServiceRejectsRebind()
    {
        var service = new BattleMovementQueryService();
        service.Dispose();
        bool rejected = false;
        try
        {
            service.BeginBattle(1);
        }
        catch (ObjectDisposedException)
        {
            rejected = true;
        }
        _test.True(rejected, "Dispose 后 movement query service 不得重新进入 battle lifecycle。");
        service.Dispose();
    }

    private void TestBattleEndCheckDoesNotInvalidateMovementGeometry()
    {
        var runtime = new BattleRuntimeModule();
        try
        {
            BattleState state = BuildState(new Vector2I(3, 1));
            BattleUnitState ally = BuildUnit("revision_ally", Vector2I.Zero);
            BattleUnitState enemy = BuildUnit("revision_enemy", new Vector2I(2, 0));
            enemy.faction_id = "enemy";
            InstallUnit(runtime.GetGridService(), state, ally);
            InstallUnit(runtime.GetGridService(), state, enemy);
            state.ally_unit_ids.Add(ally.unit_id);
            state.enemy_unit_ids.Add(enemy.unit_id);
            runtime.SetupStateForTests(state);

            state.NormalizeUnitIdArrays();
            long revisionBefore = state.MovementGeometryRevision;
            using BattleEventBatch firstBatch = new();
            using BattleEventBatch secondBatch = new();
            _test.Eq(
                runtime.FlushBattleOutcomeEvaluation(firstBatch),
                BattleOutcomeFlushResult.NoChange,
                "双方存活时战斗目标应保持进行中。"
            );
            _test.Eq(
                runtime.FlushBattleOutcomeEvaluation(secondBatch),
                BattleOutcomeFlushResult.NoChange,
                "重复求值仍应保持进行中。"
            );
            _test.Eq(
                state.MovementGeometryRevision,
                revisionBefore,
                "内容未变的战斗结束检查不得推进 movement geometry revision。"
            );
        }
        finally
        {
            runtime.Dispose();
        }
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState { map_size = mapSize };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                var coord = new Vector2I(x, y);
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

    private static BattleUnitState BuildUnit(StringName unitId, Vector2I coord)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            faction_id = "player",
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
        state.active_unit_id = unit.unit_id;
    }

    private static int FixedMoveCost(StringName unitId, Vector2I fromCoord, Vector2I toCoord) => 1;
}
