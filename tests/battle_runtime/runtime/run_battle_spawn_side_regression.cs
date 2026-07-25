using System.Collections.Generic;
using Godot;

public partial class run_battle_spawn_side_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestWideMapUsesTopAndBottomLongEdges();
        TestTallMapUsesLeftAndRightLongEdges();
        TestSpawnPlacementDoesNotClearExistingOccupantsFromStaleCoords();
        TestFailedSpawnPlacementRollsBackPartialUnits();
        RequestTestExit(_test.Finish("Battle spawn side regression"));
    }

    private void TestWideMapUsesTopAndBottomLongEdges()
    {
        BattleTestFixture fixture = CreateFlatFixture(new Vector2I(8, 4));
        BattleRuntimeModule runtime = fixture.Runtime;

        var allyUnits = new List<BattleUnitState>
        {
            BuildUnit("ally_0"),
            BuildUnit("ally_1"),
        };
        var enemyUnits = new List<BattleUnitState>
        {
            BuildUnit("enemy_0"),
            BuildUnit("enemy_1"),
        };
        var allyCoords = new List<Vector2I> { new(6, 3), new(7, 3) };
        var enemyCoords = new List<Vector2I> { new(0, 0), new(1, 0) };

        try
        {
            bool alliesPlaced = runtime.PlaceUnitsForTestsTyped(
                allyUnits,
                allyCoords,
                true,
                BattleRuntimeModule.SPAWN_SIDE_NEAR_LONG_EDGE_VALUE
            );
            bool enemiesPlaced = runtime.PlaceUnitsForTestsTyped(
                enemyUnits,
                enemyCoords,
                false,
                BattleRuntimeModule.SPAWN_SIDE_FAR_LONG_EDGE_VALUE
            );

            _test.True(alliesPlaced, "宽图近长边约束应仍能放下所有友军。");
            _test.True(enemiesPlaced, "宽图远长边约束应仍能放下所有敌军。");
            foreach (BattleUnitState unit in allyUnits)
            {
                _test.True(
                    unit.GetAnchorCoord().Y < 2,
                    $"宽图近长边应是上半边，不是固定左侧：{unit.unit_id} coord={unit.GetAnchorCoord()}"
                );
            }

            foreach (BattleUnitState unit in enemyUnits)
            {
                _test.True(
                    unit.GetAnchorCoord().Y >= 2,
                    $"宽图远长边应是下半边，不是固定右侧：{unit.unit_id} coord={unit.GetAnchorCoord()}"
                );
            }
        }
        finally
        {
            DisposeFixtureAndUnits(fixture, allyUnits, enemyUnits);
        }
    }

    private void TestTallMapUsesLeftAndRightLongEdges()
    {
        BattleTestFixture fixture = CreateFlatFixture(new Vector2I(4, 8));
        BattleRuntimeModule runtime = fixture.Runtime;

        var allyUnits = new List<BattleUnitState> { BuildUnit("tall_ally") };
        var enemyUnits = new List<BattleUnitState> { BuildUnit("tall_enemy") };
        var allyCoords = new List<Vector2I> { new(3, 6) };
        var enemyCoords = new List<Vector2I> { new(0, 1) };

        try
        {
            bool alliesPlaced = runtime.PlaceUnitsForTestsTyped(
                allyUnits,
                allyCoords,
                true,
                BattleRuntimeModule.SPAWN_SIDE_NEAR_LONG_EDGE_VALUE
            );
            bool enemiesPlaced = runtime.PlaceUnitsForTestsTyped(
                enemyUnits,
                enemyCoords,
                false,
                BattleRuntimeModule.SPAWN_SIDE_FAR_LONG_EDGE_VALUE
            );

            _test.True(alliesPlaced, "竖图近长边约束应能放下友军。");
            _test.True(enemiesPlaced, "竖图远长边约束应能放下敌军。");
            _test.True(
                allyUnits[0].GetAnchorCoord().X < 2,
                $"竖图近长边应是左半边：coord={allyUnits[0].GetAnchorCoord()}"
            );
            _test.True(
                enemyUnits[0].GetAnchorCoord().X >= 2,
                $"竖图远长边应是右半边：coord={enemyUnits[0].GetAnchorCoord()}"
            );
        }
        finally
        {
            DisposeFixtureAndUnits(fixture, allyUnits, enemyUnits);
        }
    }

    private void TestSpawnPlacementDoesNotClearExistingOccupantsFromStaleCoords()
    {
        BattleTestFixture fixture = CreateFlatFixture(new Vector2I(4, 4));
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;

        BattleUnitState firstUnit = BuildUnit("first_unit");
        BattleUnitState secondUnit = BuildUnit("second_unit");
        var units = new List<BattleUnitState> { firstUnit, secondUnit };
        var coords = new List<Vector2I> { new(0, 0), new(1, 0) };

        try
        {
            bool placed = runtime.PlaceUnitsForTestsTyped(units, coords, true, new StringName(""));

            _test.True(placed, "开战 placement 应能连续放下初始坐标相同的单位。");
            _test.Eq(state.ally_unit_ids.Count, 2, "成功 placement 后两个单位都应进入 ally ids。");
            _test.True(
                firstUnit.GetAnchorCoord() != secondUnit.GetAnchorCoord(),
                $"两个单位不应重叠：first={firstUnit.GetAnchorCoord()} second={secondUnit.GetAnchorCoord()}"
            );
            state.TryGetCellTyped(firstUnit.GetAnchorCoord(), out BattleCellState firstCell);
            state.TryGetCellTyped(secondUnit.GetAnchorCoord(), out BattleCellState secondCell);
            _test.True(firstCell != null, "第一个单位坐标应有 cell。");
            _test.True(secondCell != null, "第二个单位坐标应有 cell。");
            if (firstCell != null)
            {
                _test.Eq(
                    firstCell.occupant_unit_id,
                    firstUnit.unit_id,
                    "第二个单位 placement 不应清掉第一个单位的占用。"
                );
            }

            if (secondCell != null)
            {
                _test.Eq(
                    secondCell.occupant_unit_id,
                    secondUnit.unit_id,
                    "第二个单位应写入自己的占用。"
                );
            }
        }
        finally
        {
            DisposeFixtureAndUnits(fixture, units);
        }
    }

    private void TestFailedSpawnPlacementRollsBackPartialUnits()
    {
        BattleTestFixture fixture = CreateFlatFixture(new Vector2I(1, 1));
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;

        BattleUnitState firstUnit = BuildUnit("rollback_first");
        BattleUnitState secondUnit = BuildUnit("rollback_second");
        var units = new List<BattleUnitState> { firstUnit, secondUnit };
        var coords = new List<Vector2I> { new(0, 0) };

        try
        {
            bool placed = runtime.PlaceUnitsForTestsTyped(units, coords, true, new StringName(""));

            _test.True(!placed, "地图没有足够出生空间时 placement 应失败。");
            _test.Eq(state.ally_unit_ids.Count, 0, "失败 placement 不应留下部分 ally ids。");
            _test.Eq(state.UnitCount, 0, "失败 placement 不应留下部分 units。");
            state.TryGetCellTyped(new Vector2I(0, 0), out BattleCellState onlyCell);
            _test.True(onlyCell != null, "回滚测试 cell 应存在。");
            if (onlyCell != null)
            {
                _test.Eq(onlyCell.occupant_unit_id, new StringName(""), "失败 placement 不应留下部分占用。");
            }
        }
        finally
        {
            DisposeFixtureAndUnits(fixture, units);
        }
    }

    private static BattleTestFixture CreateFlatFixture(Vector2I mapSize)
    {
        return BattleTestFixture.CreateFlatBattle("battle_spawn_side_regression", mapSize);
    }

    private static BattleUnitState BuildUnit(StringName unitId)
    {
        BattleUnitState unit = new BattleUnitState()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
            control_mode = "ai",
        }.WithCombatResourcesForTest(
            hp: 30,
            stamina: 10,
            ap: 2,
            movePoints: BattleUnitState.DefaultMovePointsPerTurn,
            isAlive: true
        );
        unit.SetAnchorCoord(Vector2I.Zero);
        unit.attribute_snapshot.SetValue("hp_max", 30);
        unit.attribute_snapshot.SetValue("action_points", 2);
        return unit;
    }

    private static void DisposeFixtureAndUnits(
        BattleTestFixture fixture,
        params List<BattleUnitState>[] unitLists
    )
    {
        var stateOwnedUnits = new List<BattleUnitState>();
        BattleState state = fixture?.State;
        if (state != null)
        {
            foreach (BattleUnitState unit in state.GetUnitsTyped())
            {
                if (unit != null)
                    stateOwnedUnits.Add(unit);
            }
        }

        fixture?.Dispose();

        foreach (List<BattleUnitState> unitList in unitLists ?? System.Array.Empty<List<BattleUnitState>>())
        {
            if (unitList == null)
                continue;
            foreach (BattleUnitState unit in unitList)
            {
                if (!ContainsReference(stateOwnedUnits, unit))
                    BattleTestFixture.DisposeBattleUnit(unit);
            }
            unitList.Clear();
        }

        stateOwnedUnits.Clear();
    }

    private static bool ContainsReference(List<BattleUnitState> units, BattleUnitState candidate)
    {
        foreach (BattleUnitState unit in units)
        {
            if (ReferenceEquals(unit, candidate))
                return true;
        }
        return false;
    }

}
