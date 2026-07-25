using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_escape_objective_regression
    : LifecycleTestSceneTree
{
    private static readonly StringName ExitZoneId = "east_exit";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestEnemiesMaySurviveSuccessfulEscape();
            TestEnemyDefeatDoesNotCompleteEscape();
            TestLargeUnitRequiresItsFullFootprintInsideExit();
            TestRequiredUnitDefeatFailsEscape();
            TestNonPersistentAlliesAreExcluded();
            TestExitRejectsFootprintBlockedByInternalEdge();
            TestExitMustFitRequiredPartySimultaneously();
            TestExitCapacityBacktracksToAlternativePlacement();
            TestInvalidEmptyAndTooDeepExitZonesAreRejected();
            TestAtomicArrivalAndDeathFailsEscape();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unhandled battle escape objective regression exception: {exception}"
            );
        }

        RequestTestExit(_test.Finish("Battle escape objective regression"));
    }

    private void TestEnemiesMaySurviveSuccessfulEscape()
    {
        BattleUnitState ally = BuildPersistentAlly(
            "escape_success_ally",
            new Vector2I(4, 0)
        );
        BattleUnitState enemy = BuildEnemy(
            "escape_success_enemy",
            new Vector2I(1, 0)
        );
        using BattleTestFixture fixture = CreateEscapeBattle(
            "escape_enemies_survive",
            new Vector2I(5, 2),
            new[] { ally },
            new[] { enemy },
            BattleMapEdge.Right,
            1
        );
        using BattleEventBatch batch = new();

        BattleOutcomeFlushResult flushResult =
            fixture.Runtime.FlushBattleOutcomeEvaluation(batch);

        _test.Eq(
            flushResult,
            BattleOutcomeFlushResult.Completed,
            "所有初始持久队员进入出口后应完成逃离。"
        );
        _test.True(
            enemy.IsAlive(),
            "逃离成功不应要求消灭仍存活的敌人。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerSuccess,
            BattleEndReasonKind.EscapeRequiredUnitsReachedExit,
            "全员抵达出口"
        );
    }

    private void TestEnemyDefeatDoesNotCompleteEscape()
    {
        BattleUnitState ally = BuildPersistentAlly(
            "escape_enemy_wipe_ally",
            new Vector2I(0, 0)
        );
        BattleUnitState enemy = BuildEnemy(
            "escape_enemy_wipe_enemy",
            new Vector2I(3, 0)
        );
        using BattleTestFixture fixture = CreateEscapeBattle(
            "escape_enemy_wipe",
            new Vector2I(5, 1),
            new[] { ally },
            new[] { enemy },
            BattleMapEdge.Right,
            1
        );
        using BattleEventBatch batch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult flushResult;
        try
        {
            DefeatUnit(fixture.Runtime, enemy, batch);
        }
        finally
        {
            flushResult = fixture.Runtime.EndObjectiveMutation(batch);
        }

        _test.Eq(
            flushResult,
            BattleOutcomeFlushResult.NoChange,
            "敌方全灭但队员未到出口时，逃离目标应继续进行。"
        );
        _test.True(
            fixture.State.FinalDecision == null,
            "逃离目标不得借用歼灭条件锁存胜利。"
        );
        _test.False(
            batch.battle_ended,
            "敌方全灭本身不应结束逃离战斗。"
        );
    }

    private void TestLargeUnitRequiresItsFullFootprintInsideExit()
    {
        BattleUnitState largeAlly = BuildPersistentAlly(
            "escape_large_ally",
            new Vector2I(3, 0)
        );
        _test.True(
            largeAlly.SetBodySizeCategory("large"),
            "测试单位应可配置为 2x2 large 体型。"
        );
        BattleUnitState enemy = BuildEnemy(
            "escape_large_enemy",
            new Vector2I(0, 2)
        );
        using BattleTestFixture fixture = CreateEscapeBattle(
            "escape_full_footprint",
            new Vector2I(6, 3),
            new[] { largeAlly },
            new[] { enemy },
            BattleMapEdge.Right,
            2
        );
        using BattleEventBatch partialBatch = new();

        _test.Eq(
            fixture.Runtime.FlushBattleOutcomeEvaluation(partialBatch),
            BattleOutcomeFlushResult.NoChange,
            "大型单位只有部分占地进入出口带时不得完成逃离。"
        );
        _test.True(
            fixture.State.FinalDecision == null,
            "部分占地进入出口带时不应锁存终局。"
        );

        using BattleEventBatch completionBatch = new();
        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult completionResult;
        try
        {
            _test.True(
                fixture.Runtime._grid_service.PlaceUnit(
                    fixture.State,
                    largeAlly,
                    new Vector2I(4, 0),
                    ignore_height: true
                ),
                "大型单位应能完整移入 2 格深的右侧出口带。"
            );
        }
        finally
        {
            completionResult = fixture.Runtime.EndObjectiveMutation(
                completionBatch
            );
        }

        _test.Eq(
            completionResult,
            BattleOutcomeFlushResult.Completed,
            "大型单位完整占地进入出口后应完成逃离。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerSuccess,
            BattleEndReasonKind.EscapeRequiredUnitsReachedExit,
            "大型单位完整进入出口"
        );
    }

    private void TestRequiredUnitDefeatFailsEscape()
    {
        BattleUnitState ally = BuildPersistentAlly(
            "escape_death_ally",
            new Vector2I(0, 0)
        );
        BattleUnitState enemy = BuildEnemy(
            "escape_death_enemy",
            new Vector2I(2, 0)
        );
        using BattleTestFixture fixture = CreateEscapeBattle(
            "escape_required_death",
            new Vector2I(5, 1),
            new[] { ally },
            new[] { enemy },
            BattleMapEdge.Right,
            1
        );
        using BattleEventBatch batch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult flushResult;
        try
        {
            DefeatUnit(fixture.Runtime, ally, batch);
        }
        finally
        {
            flushResult = fixture.Runtime.EndObjectiveMutation(batch);
        }

        _test.Eq(
            flushResult,
            BattleOutcomeFlushResult.Completed,
            "任一初始持久队员阵亡后应立即判定逃离失败。"
        );
        _test.True(enemy.IsAlive(), "逃离失败不应依赖敌方是否存活。");
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerFailure,
            BattleEndReasonKind.EscapeRequiredUnitDefeated,
            "必需队员阵亡"
        );
    }

    private void TestNonPersistentAlliesAreExcluded()
    {
        BattleUnitState persistentAlly = BuildPersistentAlly(
            "escape_persistent_ally",
            new Vector2I(4, 0)
        );
        BattleUnitState summon = BattleTestFixture.BuildUnit(
            "escape_summon",
            "player",
            new Vector2I(0, 1),
            currentHp: 20
        );
        BattleUnitState enemy = BuildEnemy(
            "escape_summon_enemy",
            new Vector2I(2, 0)
        );
        using (
            BattleTestFixture fixture = CreateEscapeBattle(
                "escape_ignores_summon",
                new Vector2I(5, 2),
                new[] { persistentAlly, summon },
                new[] { enemy },
                BattleMapEdge.Right,
                1
            )
        )
        using (BattleEventBatch batch = new())
        {
            _test.Eq(
                fixture.Runtime.FlushBattleOutcomeEvaluation(batch),
                BattleOutcomeFlushResult.Completed,
                "非持久友方停留在出口外不应阻止持久队伍逃离。"
            );
            _test.True(
                summon.IsAlive() && summon.GetAnchorCoord() == new Vector2I(0, 1),
                "测试召唤物应保持存活并停留在出口外。"
            );
        }

        BattleUnitState summonOnly = BattleTestFixture.BuildUnit(
            "escape_summon_only",
            "player",
            new Vector2I(0, 0),
            currentHp: 20
        );
        BattleUnitState summonOnlyEnemy = BuildEnemy(
            "escape_summon_only_enemy",
            new Vector2I(2, 0)
        );
        using BattleTestFixture summonOnlyFixture =
            BattleTestFixture.CreateFlatBattle(
                "escape_summon_only_party",
                new Vector2I(4, 1),
                new[] { summonOnly },
                new[] { summonOnlyEnemy }
            );
        _test.False(
            summonOnlyFixture.Runtime.InitializeBattleObjective(
                new BattleEscapeObjectiveDefinition(
                    ExitZoneId,
                    BattleMapEdge.Right,
                    1
                )
            ),
            "只有非持久友方的阵容不得初始化逃离目标。"
        );
    }

    private void TestInvalidEmptyAndTooDeepExitZonesAreRejected()
    {
        _test.True(
            Throws<ArgumentException>(
                () =>
                    _ = new BattleEscapeObjectiveDefinition(
                        "",
                        BattleMapEdge.Right,
                        1
                    )
            ),
            "空 exit_zone_id 应在定义边界被拒绝。"
        );
        _test.True(
            Throws<ArgumentOutOfRangeException>(
                () =>
                    _ = new BattleEscapeObjectiveDefinition(
                        ExitZoneId,
                        BattleMapEdge.Unknown,
                        1
                    )
            ),
            "Unknown 出口边应在定义边界被拒绝。"
        );
        _test.True(
            Throws<ArgumentOutOfRangeException>(
                () =>
                    _ = new BattleEscapeObjectiveDefinition(
                        ExitZoneId,
                        BattleMapEdge.Right,
                        0
                    )
            ),
            "非正数出口深度应在定义边界被拒绝。"
        );

        BattleUnitState tooDeepAlly = BuildPersistentAlly(
            "escape_too_deep_ally",
            new Vector2I(0, 0)
        );
        BattleUnitState tooDeepEnemy = BuildEnemy(
            "escape_too_deep_enemy",
            new Vector2I(2, 0)
        );
        using (
            BattleTestFixture tooDeepFixture =
                BattleTestFixture.CreateFlatBattle(
                    "escape_too_deep_zone",
                    new Vector2I(5, 1),
                    new[] { tooDeepAlly },
                    new[] { tooDeepEnemy }
                )
        )
        {
            _test.False(
                tooDeepFixture.Runtime.InitializeBattleObjective(
                    new BattleEscapeObjectiveDefinition(
                        ExitZoneId,
                        BattleMapEdge.Right,
                        5
                    )
                ),
                "覆盖整个地图宽度的出口带应被视为过深并拒绝初始化。"
            );
        }

        BattleUnitState emptyZoneAlly = BuildPersistentAlly(
            "escape_empty_zone_ally",
            new Vector2I(0, 0)
        );
        BattleUnitState emptyZoneEnemy = BuildEnemy(
            "escape_empty_zone_enemy",
            new Vector2I(2, 0)
        );
        using BattleTestFixture emptyZoneFixture =
            BattleTestFixture.CreateFlatBattle(
                "escape_empty_zone",
                new Vector2I(5, 2),
                new[] { emptyZoneAlly },
                new[] { emptyZoneEnemy }
            );
        emptyZoneFixture.State.GetCell(new Vector2I(4, 0))?.SetPassable(false);
        emptyZoneFixture.State.GetCell(new Vector2I(4, 1))?.SetPassable(false);
        _test.False(
            emptyZoneFixture.Runtime.InitializeBattleObjective(
                new BattleEscapeObjectiveDefinition(
                    ExitZoneId,
                    BattleMapEdge.Right,
                    1
                )
            ),
            "出口带没有任何可通行格时应拒绝初始化逃离目标。"
        );
    }

    private void TestExitRejectsFootprintBlockedByInternalEdge()
    {
        BattleUnitState largeAlly = BuildPersistentAlly(
            "escape_blocked_footprint_ally",
            new Vector2I(0, 0)
        );
        _test.True(
            largeAlly.SetBodySizeCategory("large"),
            "内部边阻断测试单位应为 2x2。"
        );
        BattleUnitState enemy = BuildEnemy(
            "escape_blocked_footprint_enemy",
            new Vector2I(2, 0)
        );
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "escape_internal_edge_blocks_capacity",
            new Vector2I(5, 2),
            new[] { largeAlly },
            new[] { enemy }
        );
        fixture.State
            .GetCell(new Vector2I(3, 0))
            ?.SetEdgeFeature(Vector2I.Right, BattleEdgeFeatureState.MakeWall());
        fixture.State.MarkRuntimeEdgesDirty();

        _test.False(
            fixture.Runtime.InitializeBattleObjective(
                new BattleEscapeObjectiveDefinition(
                    ExitZoneId,
                    BattleMapEdge.Right,
                    2
                )
            ),
            "出口被内部阻断边切开时，不得把不可落位的 2x2 footprint 判成可撤离。"
        );
    }

    private void TestExitMustFitRequiredPartySimultaneously()
    {
        BattleUnitState firstAlly = BuildPersistentAlly(
            "escape_capacity_first",
            new Vector2I(0, 0)
        );
        _test.True(
            firstAlly.SetBodySizeCategory("large"),
            "第一个容量测试单位应为 2x2。"
        );
        BattleUnitState secondAlly = BuildPersistentAlly(
            "escape_capacity_second",
            new Vector2I(0, 3)
        );
        _test.True(
            secondAlly.SetBodySizeCategory("large"),
            "第二个容量测试单位应为 2x2。"
        );
        BattleUnitState enemy = BuildEnemy(
            "escape_capacity_enemy",
            new Vector2I(2, 0)
        );
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "escape_insufficient_simultaneous_capacity",
            new Vector2I(5, 7),
            new[] { firstAlly, secondAlly },
            new[] { enemy }
        );
        var passableExitCoords = new HashSet<Vector2I>
        {
            new(3, 0),
            new(4, 0),
            new(3, 1),
            new(4, 1),
            new(3, 3),
            new(4, 4),
            new(3, 5),
            new(4, 6),
        };
        for (int y = 0; y < fixture.State.map_size.Y; y++)
        {
            for (int x = 3; x < fixture.State.map_size.X; x++)
            {
                fixture.State
                    .GetCell(new Vector2I(x, y))
                    ?.SetPassable(passableExitCoords.Contains(new Vector2I(x, y)));
            }
        }

        _test.False(
            fixture.Runtime.InitializeBattleObjective(
                new BattleEscapeObjectiveDefinition(
                    ExitZoneId,
                    BattleMapEdge.Right,
                    2
                )
            ),
            "出口总面积足够但只有一个 2x2 落点时，不得接受两个必须同时撤离的大型队员。"
        );
    }

    private void TestAtomicArrivalAndDeathFailsEscape()
    {
        BattleUnitState survivor = BuildPersistentAlly(
            "escape_atomic_survivor",
            new Vector2I(0, 0)
        );
        BattleUnitState doomed = BuildPersistentAlly(
            "escape_atomic_doomed",
            new Vector2I(0, 1)
        );
        BattleUnitState enemy = BuildEnemy(
            "escape_atomic_enemy",
            new Vector2I(2, 0)
        );
        using BattleTestFixture fixture = CreateEscapeBattle(
            "escape_atomic_arrival_and_death",
            new Vector2I(5, 2),
            new[] { survivor, doomed },
            new[] { enemy },
            BattleMapEdge.Right,
            1
        );
        using BattleEventBatch batch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult flushResult;
        try
        {
            _test.True(
                fixture.Runtime._grid_service.PlaceUnit(
                    fixture.State,
                    survivor,
                    new Vector2I(4, 0),
                    ignore_height: true
                ),
                "存活队员应能在原子变更内抵达出口。"
            );
            _test.True(
                fixture.Runtime._grid_service.PlaceUnit(
                    fixture.State,
                    doomed,
                    new Vector2I(4, 1),
                    ignore_height: true
                ),
                "将阵亡的队员应能在原子变更内先抵达出口。"
            );
            DefeatUnit(fixture.Runtime, doomed, batch);
        }
        finally
        {
            flushResult = fixture.Runtime.EndObjectiveMutation(batch);
        }

        _test.Eq(
            flushResult,
            BattleOutcomeFlushResult.Completed,
            "同一原子变更内发生抵达与必需队员阵亡时应完成失败结算。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerFailure,
            BattleEndReasonKind.EscapeRequiredUnitDefeated,
            "抵达与死亡竞态"
        );
    }

    private void TestExitCapacityBacktracksToAlternativePlacement()
    {
        BattleUnitState firstAlly = BuildPersistentAlly(
            "escape_backtrack_first",
            new Vector2I(0, 0)
        );
        BattleUnitState secondAlly = BuildPersistentAlly(
            "escape_backtrack_second",
            new Vector2I(3, 1)
        );
        _test.True(
            firstAlly.SetBodySizeCategory("large")
            && secondAlly.SetBodySizeCategory("large"),
            "回溯容量测试的两个单位都应为 2x2。"
        );
        BattleUnitState enemy = BuildEnemy(
            "escape_backtrack_enemy",
            new Vector2I(2, 2)
        );
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "escape_capacity_requires_backtracking",
            new Vector2I(5, 3),
            new[] { firstAlly, secondAlly },
            new[] { enemy }
        );
        var passableExitCoords = new HashSet<Vector2I>
        {
            new(2, 0),
            new(3, 0),
            new(1, 1),
            new(2, 1),
            new(3, 1),
            new(4, 1),
            new(1, 2),
            new(2, 2),
            new(3, 2),
            new(4, 2),
        };
        for (int y = 0; y < fixture.State.map_size.Y; y++)
        {
            for (int x = 1; x < fixture.State.map_size.X; x++)
            {
                Vector2I coord = new(x, y);
                fixture.State
                    .GetCell(coord)
                    ?.SetPassable(passableExitCoords.Contains(coord));
            }
        }

        _test.True(
            fixture.Runtime.InitializeBattleObjective(
                new BattleEscapeObjectiveDefinition(
                    ExitZoneId,
                    BattleMapEdge.Right,
                    4
                )
            ),
            "首个 2x2 落点阻断后续单位时，容量验证应回退并改选两侧不重叠落点。"
        );
    }

    private BattleTestFixture CreateEscapeBattle(
        StringName battleId,
        Vector2I mapSize,
        BattleUnitState[] allies,
        BattleUnitState[] enemies,
        BattleMapEdge edge,
        int depth
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            mapSize,
            allies,
            enemies
        );
        _test.True(
            fixture.Runtime.InitializeBattleObjective(
                new BattleEscapeObjectiveDefinition(ExitZoneId, edge, depth)
            ),
            $"{battleId} 应成功初始化逃离目标。"
        );
        return fixture;
    }

    private static BattleUnitState BuildPersistentAlly(
        StringName unitId,
        Vector2I coord
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            unitId,
            "player",
            coord,
            currentHp: 20
        );
        unit.source_member_id = $"{unitId}_member";
        return unit;
    }

    private static BattleUnitState BuildEnemy(StringName unitId, Vector2I coord) =>
        BattleTestFixture.BuildUnit(
            unitId,
            "enemy",
            coord,
            currentHp: 20
        );

    private static void DefeatUnit(
        BattleRuntimeModule runtime,
        BattleUnitState unit,
        BattleEventBatch batch
    )
    {
        unit.MarkDead();
        runtime.HandleUnitDefeatedByRuntimeEffect(
            unit,
            null,
            batch,
            "",
            new BattleDefeatHandlingOptions(collectLoot: false)
        );
    }

    private void AssertDecision(
        BattleFinalDecision decision,
        BattleOutcomeKind expectedOutcome,
        BattleEndReasonKind expectedReason,
        string context
    )
    {
        _test.True(decision != null, $"{context}应锁存终局决定。");
        if (decision == null)
            return;
        _test.Eq(
            decision.ObjectiveMode,
            BattleObjectiveMode.Escape,
            $"{context}终局应属于逃离目标。"
        );
        _test.Eq(
            decision.Outcome,
            expectedOutcome,
            $"{context}终局 outcome 不正确。"
        );
        _test.Eq(
            decision.EndReason,
            expectedReason,
            $"{context}终局原因不正确。"
        );
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }
}
