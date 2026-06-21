using System;
using System.Collections.Generic;
using Godot;

public partial class run_jump_arc_regression_typed : SceneTree
{
    private readonly TestHarness _test = new();
    private BattleGridService _gridService = null!;

    public override void _Initialize()
    {
        int exitCode = Run();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(exitCode);
    }

    public int RunForWrapper() => Run();

    private int Run()
    {
        _gridService = new BattleGridService();

        TestFlatJumpSucceedsWithinRange();
        TestJumpUpAStepSucceeds();
        TestJumpDownCliffSucceeds();
        TestJumpClearsLowObstacle();
        TestJumpBlockedByTallObstacle();
        TestJumpLandingOnOccupiedCellFails();
        TestJumpBeyondMaxRangeFails();
        TestJumpBlockedByFriendlyInPath();
        TestSmallUnitGetsAgilityBonus();
        TestHugeUnitTakesSizePenalty();
        TestShortJumpClearsTallerObstacleViaRedistribution();
        TestZeroDistanceTargetRejected();

        return _test.Finish("Jump arc regression");
    }

    private void TestFlatJumpSucceedsWithinRange()
    {
        BattleState state = BuildFlatState(new Vector2I(8, 4));
        BattleUnitState unit = BuildJumper(new Vector2I(1, 1), 12, BattleUnitState.BodySizeMedium);
        state.SetUnit(unit);
        RegisterUnitOnCells(state, unit);
        CombatEffectDef effect = BuildJumpEffect();
        bool ok = _gridService.CanJumpArc(state, unit, new Vector2I(3, 1), effect);
        _test.True(ok, "平地跳 (1,1)->(3,1) 距离 2 弧高足够，应该成功。");
    }

    private void TestJumpUpAStepSucceeds()
    {
        BattleState state = BuildFlatState(new Vector2I(8, 4));
        SetCellHeight(state, new Vector2I(3, 1), 2);
        BattleUnitState unit = BuildJumper(new Vector2I(1, 1), 12, BattleUnitState.BodySizeMedium);
        state.SetUnit(unit);
        RegisterUnitOnCells(state, unit);
        CombatEffectDef effect = BuildJumpEffect();
        bool ok = _gridService.CanJumpArc(state, unit, new Vector2I(3, 1), effect);
        _test.True(ok, "跳上高 2 的台阶，弧高足够，应该成功。");
    }

    private void TestJumpDownCliffSucceeds()
    {
        BattleState state = BuildFlatState(new Vector2I(8, 4));
        SetCellHeight(state, new Vector2I(1, 1), 4);
        BattleUnitState unit = BuildJumper(new Vector2I(1, 1), 12, BattleUnitState.BodySizeMedium);
        state.SetUnit(unit);
        RegisterUnitOnCells(state, unit);
        CombatEffectDef effect = BuildJumpEffect();
        bool ok = _gridService.CanJumpArc(state, unit, new Vector2I(3, 1), effect);
        _test.True(ok, "从高 4 跳下到地面，应该成功。");
    }

    private void TestJumpClearsLowObstacle()
    {
        BattleState state = BuildFlatState(new Vector2I(8, 4));
        SetCellHeight(state, new Vector2I(2, 1), 1);
        BattleUnitState unit = BuildJumper(new Vector2I(1, 1), 12, BattleUnitState.BodySizeMedium);
        state.SetUnit(unit);
        RegisterUnitOnCells(state, unit);
        CombatEffectDef effect = BuildJumpEffect();
        bool ok = _gridService.CanJumpArc(state, unit, new Vector2I(3, 1), effect);
        _test.True(ok, "跳过中间高 1 的低障碍，弧顶应足够。");
    }

    private void TestJumpBlockedByTallObstacle()
    {
        BattleState state = BuildFlatState(new Vector2I(8, 4));
        SetCellHeight(state, new Vector2I(2, 1), 8);
        BattleUnitState unit = BuildJumper(new Vector2I(1, 1), 12, BattleUnitState.BodySizeMedium);
        state.SetUnit(unit);
        RegisterUnitOnCells(state, unit);
        CombatEffectDef effect = BuildJumpEffect();
        bool ok = _gridService.CanJumpArc(state, unit, new Vector2I(3, 1), effect);
        _test.True(!ok, "中间格高 8 远超弧顶，应该撞墙失败。");
    }

    private void TestJumpLandingOnOccupiedCellFails()
    {
        BattleState state = BuildFlatState(new Vector2I(8, 4));
        BattleUnitState unit = BuildJumper(new Vector2I(1, 1), 12, BattleUnitState.BodySizeMedium);
        state.SetUnit(unit);
        RegisterUnitOnCells(state, unit);
        BattleUnitState blocker = BuildJumper(new Vector2I(3, 1), 8, BattleUnitState.BodySizeMedium);
        blocker.unit_id = "blocker";
        state.SetUnit(blocker);
        RegisterUnitOnCells(state, blocker);
        CombatEffectDef effect = BuildJumpEffect();
        bool ok = _gridService.CanJumpArc(state, unit, new Vector2I(3, 1), effect);
        _test.True(!ok, "落点已被其他单位占用，应该失败。");
    }

    private void TestJumpBeyondMaxRangeFails()
    {
        BattleState state = BuildFlatState(new Vector2I(12, 4));
        BattleUnitState unit = BuildJumper(new Vector2I(1, 1), 4, BattleUnitState.BodySizeMedium);
        state.SetUnit(unit);
        RegisterUnitOnCells(state, unit);
        CombatEffectDef effect = BuildJumpEffect();
        bool ok = _gridService.CanJumpArc(state, unit, new Vector2I(8, 1), effect);
        _test.True(!ok, "目标距离 7 远超 max_range，应该失败。");
    }

    private void TestJumpBlockedByFriendlyInPath()
    {
        BattleState state = BuildFlatState(new Vector2I(8, 4));
        BattleUnitState unit = BuildJumper(new Vector2I(1, 1), 12, BattleUnitState.BodySizeMedium);
        state.SetUnit(unit);
        RegisterUnitOnCells(state, unit);
        SetCellHeight(state, new Vector2I(2, 1), 3);
        BattleUnitState ally = BuildJumper(new Vector2I(2, 1), 8, BattleUnitState.BodySizeMedium);
        ally.unit_id = "ally";
        ally.faction_id = unit.faction_id;
        state.SetUnit(ally);
        RegisterUnitOnCells(state, ally);
        CombatEffectDef effect = BuildJumpEffect();
        bool ok = _gridService.CanJumpArc(state, unit, new Vector2I(3, 1), effect);
        _test.True(!ok, "友军站在 3 格高地上 (含 presence_height=4)，应被阻挡。");
    }

    private void TestSmallUnitGetsAgilityBonus()
    {
        BattleState state = BuildFlatState(new Vector2I(12, 4));
        BattleUnitState unit = BuildJumper(new Vector2I(1, 1), 4, BattleUnitState.BodySizeSmall);
        state.SetUnit(unit);
        RegisterUnitOnCells(state, unit);
        CombatEffectDef effect = BuildJumpEffect();
        bool ok = _gridService.CanJumpArc(state, unit, new Vector2I(3, 1), effect);
        _test.True(ok, "小体型 +1 STR 加成应让 STR=4 角色跳出 2 格。");
    }

    private void TestHugeUnitTakesSizePenalty()
    {
        BattleState state = BuildFlatState(new Vector2I(12, 4));
        BattleUnitState unit = BuildJumper(new Vector2I(1, 1), 12, BattleUnitState.BodySizeHuge);
        state.SetUnit(unit);
        RegisterUnitOnCells(state, unit);
        CombatEffectDef effect = BuildJumpEffect();
        bool ok = _gridService.CanJumpArc(state, unit, new Vector2I(8, 1), effect);
        _test.True(!ok, "Huge 体型扣 10 STR 后远距离应失败。");
    }

    private void TestShortJumpClearsTallerObstacleViaRedistribution()
    {
        BattleState state = BuildFlatState(new Vector2I(8, 4));
        SetCellHeight(state, new Vector2I(2, 1), 3);
        BattleUnitState unit = BuildJumper(new Vector2I(1, 1), 22, BattleUnitState.BodySizeLarge);
        state.SetUnit(unit);
        RegisterUnitOnCells(state, unit);
        CombatEffectDef effect = BuildJumpEffect();
        bool ok = _gridService.CanJumpArc(state, unit, new Vector2I(3, 1), effect);
        _test.True(ok, "短距离 (距离 2) 跳跃配合短跳红利应能跨过高 3 障碍。");
    }

    private void TestZeroDistanceTargetRejected()
    {
        BattleState state = BuildFlatState(new Vector2I(8, 4));
        BattleUnitState unit = BuildJumper(new Vector2I(1, 1), 12, BattleUnitState.BodySizeMedium);
        state.SetUnit(unit);
        RegisterUnitOnCells(state, unit);
        CombatEffectDef effect = BuildJumpEffect();
        bool ok = _gridService.CanJumpArc(state, unit, unit.coord, effect);
        _test.True(!ok, "目标即为起点（距离 0）应该被拒绝。");
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        BattleState state = new() { map_size = mapSize };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                BattleCellState cell = new()
                {
                    coord = new Vector2I(x, y),
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 0,
                    height_offset = 0,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(cell.coord, cell);
            }
        }
        return state;
    }

    private static void SetCellHeight(BattleState state, Vector2I coord, int height)
    {
        BattleCellState cell = state.GetCell(coord);
        if (cell == null)
        {
            return;
        }

        cell.base_height = height;
        cell.RecalculateRuntimeValues();
    }

    private static BattleUnitState BuildJumper(Vector2I coord, int strength, int bodySize)
    {
        BattleUnitState unit = new()
        {
            unit_id = new StringName($"jumper_{coord.X}_{coord.Y}"),
            display_name = $"jumper_{coord.X}_{coord.Y}",
            faction_id = "player",
            body_size = bodySize,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue("strength", strength);
        return unit;
    }

    private static void RegisterUnitOnCells(BattleState state, BattleUnitState unit)
    {
        foreach (Vector2I coord in unit.occupied_coords)
        {
            BattleCellState cell = state.GetCell(coord);
            if (cell != null)
            {
                cell.occupant_unit_id = unit.unit_id;
            }
        }
    }

    private static CombatEffectDef BuildJumpEffect()
    {
        return new CombatEffectDef
        {
            effect_type = "forced_move",
            forced_move_mode = "jump",
            forced_move_distance = 4,
            jump_base_budget = 2,
            jump_str_scale = 0.3,
            jump_arc_ratio = 0.4,
            jump_range_multiplier = 1,
        };
    }

}
