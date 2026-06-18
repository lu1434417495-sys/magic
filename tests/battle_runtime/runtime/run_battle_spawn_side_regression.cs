using System.Collections.Generic;
using Godot;

public partial class run_battle_spawn_side_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestWideMapUsesTopAndBottomLongEdges();
        TestTallMapUsesLeftAndRightLongEdges();
        TestSpawnPlacementDoesNotClearExistingOccupantsFromStaleCoords();
        TestFailedSpawnPlacementRollsBackPartialUnits();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Battle spawn side regression"));
    }

    private void TestWideMapUsesTopAndBottomLongEdges()
    {
        BattleTestFixture fixture = CreateFlatFixture(new Vector2I(8, 4));
        BattleRuntimeModule runtime = fixture.Runtime;

        var allyUnits = new Godot.Collections.Array
        {
            BuildUnit("ally_0"),
            BuildUnit("ally_1"),
        };
        var enemyUnits = new Godot.Collections.Array
        {
            BuildUnit("enemy_0"),
            BuildUnit("enemy_1"),
        };
        var allyCoords = new Godot.Collections.Array { new Vector2I(6, 3), new Vector2I(7, 3) };
        var enemyCoords = new Godot.Collections.Array { new Vector2I(0, 0), new Vector2I(1, 0) };

        bool alliesPlaced = runtime._place_units(
            allyUnits,
            allyCoords,
            true,
            BattleRuntimeModule.SPAWN_SIDE_NEAR_LONG_EDGE_VALUE
        );
        bool enemiesPlaced = runtime._place_units(
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
                unit.coord.Y < 2,
                $"宽图近长边应是上半边，不是固定左侧：{unit.unit_id} coord={unit.coord}"
            );
        }

        foreach (BattleUnitState unit in enemyUnits)
        {
            _test.True(
                unit.coord.Y >= 2,
                $"宽图远长边应是下半边，不是固定右侧：{unit.unit_id} coord={unit.coord}"
            );
        }
        fixture.Dispose();
    }

    private void TestTallMapUsesLeftAndRightLongEdges()
    {
        BattleTestFixture fixture = CreateFlatFixture(new Vector2I(4, 8));
        BattleRuntimeModule runtime = fixture.Runtime;

        var allyUnits = new Godot.Collections.Array { BuildUnit("tall_ally") };
        var enemyUnits = new Godot.Collections.Array { BuildUnit("tall_enemy") };
        var allyCoords = new Godot.Collections.Array { new Vector2I(3, 6) };
        var enemyCoords = new Godot.Collections.Array { new Vector2I(0, 1) };

        bool alliesPlaced = runtime._place_units(
            allyUnits,
            allyCoords,
            true,
            BattleRuntimeModule.SPAWN_SIDE_NEAR_LONG_EDGE_VALUE
        );
        bool enemiesPlaced = runtime._place_units(
            enemyUnits,
            enemyCoords,
            false,
            BattleRuntimeModule.SPAWN_SIDE_FAR_LONG_EDGE_VALUE
        );

        _test.True(alliesPlaced, "竖图近长边约束应能放下友军。");
        _test.True(enemiesPlaced, "竖图远长边约束应能放下敌军。");
        _test.True(
            ((BattleUnitState)allyUnits[0]).coord.X < 2,
            $"竖图近长边应是左半边：coord={((BattleUnitState)allyUnits[0]).coord}"
        );
        _test.True(
            ((BattleUnitState)enemyUnits[0]).coord.X >= 2,
            $"竖图远长边应是右半边：coord={((BattleUnitState)enemyUnits[0]).coord}"
        );
        fixture.Dispose();
    }

    private void TestSpawnPlacementDoesNotClearExistingOccupantsFromStaleCoords()
    {
        BattleTestFixture fixture = CreateFlatFixture(new Vector2I(4, 4));
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;

        BattleUnitState firstUnit = BuildUnit("first_unit");
        BattleUnitState secondUnit = BuildUnit("second_unit");
        var units = new Godot.Collections.Array { firstUnit, secondUnit };
        var coords = new Godot.Collections.Array { new Vector2I(0, 0), new Vector2I(1, 0) };

        bool placed = runtime._place_units(units, coords, true, new StringName(""));

        _test.True(placed, "开战 placement 应能连续放下初始坐标相同的单位。");
        _test.Eq(state.ally_unit_ids.Count, 2, "成功 placement 后两个单位都应进入 ally ids。");
        _test.True(
            firstUnit.coord != secondUnit.coord,
            $"两个单位不应重叠：first={firstUnit.coord} second={secondUnit.coord}"
        );
        state.TryGetCellTyped(firstUnit.coord, out BattleCellState firstCell);
        state.TryGetCellTyped(secondUnit.coord, out BattleCellState secondCell);
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
        fixture.Dispose();
    }

    private void TestFailedSpawnPlacementRollsBackPartialUnits()
    {
        BattleTestFixture fixture = CreateFlatFixture(new Vector2I(1, 1));
        BattleRuntimeModule runtime = fixture.Runtime;
        BattleState state = fixture.State;

        BattleUnitState firstUnit = BuildUnit("rollback_first");
        BattleUnitState secondUnit = BuildUnit("rollback_second");
        var units = new Godot.Collections.Array { firstUnit, secondUnit };
        var coords = new Godot.Collections.Array { new Vector2I(0, 0) };

        bool placed = runtime._place_units(units, coords, true, new StringName(""));

        _test.True(!placed, "地图没有足够出生空间时 placement 应失败。");
        _test.Eq(state.ally_unit_ids.Count, 0, "失败 placement 不应留下部分 ally ids。");
        _test.Eq(state.UnitCount, 0, "失败 placement 不应留下部分 units。");
        state.TryGetCellTyped(new Vector2I(0, 0), out BattleCellState onlyCell);
        _test.True(onlyCell != null, "回滚测试 cell 应存在。");
        if (onlyCell != null)
        {
            _test.Eq(onlyCell.occupant_unit_id, new StringName(""), "失败 placement 不应留下部分占用。");
        }
        fixture.Dispose();
    }

    private static BattleTestFixture CreateFlatFixture(Vector2I mapSize)
    {
        return BattleTestFixture.CreateFlatBattle("battle_spawn_side_regression", mapSize);
    }

    private static BattleUnitState BuildUnit(StringName unitId)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
            control_mode = "ai",
            current_hp = 30,
            current_stamina = 10,
            current_ap = 2,
            current_move_points = BattleUnitState.DefaultMovePointsPerTurn,
            is_alive = true,
        };
        unit.SetAnchorCoord(Vector2I.Zero);
        unit.attribute_snapshot.SetValue("hp_max", 30);
        unit.attribute_snapshot.SetValue("action_points", 2);
        return unit;
    }

}
