using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_move_path_result_projection_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestMovePathResultProjectsTypedPath();
        TestMovePathTreeProjectsTypedMaps();
        TestValidatedMoveExecutionResultProjectsTypedPath();
        TestMovementServiceUsesTypedPathAndExecutesMove();
        Quit(_test.Finish("Battle move path result projection regression"));
    }

    private void TestMovePathResultProjectsTypedPath()
    {
        var result = new BattleMovePathResult
        {
            Allowed = true,
            Cost = 3,
            Path = new[] { new Vector2I(0, 0), new Vector2I(1, 0), new Vector2I(1, 1) },
            Message = "ok",
        };

        Godot.Collections.Dictionary payload = result.ToDictionary();
        Godot.Collections.Array<Vector2I> pathPayload = payload["path"].AsGodotArray<Vector2I>();

        _test.True(
            result.Path.GetType() != typeof(Godot.Collections.Array<Vector2I>),
            "Path 真相源不应是 Godot Array。"
        );
        _test.True(payload["allowed"].AsBool(), "move path result 应投影 allowed。");
        _test.Eq(payload["cost"].AsInt32(), 3, "move path result 应投影 cost。");
        _test.Eq(payload["message"].AsString(), "ok", "move path result 应投影 message。");
        _test.Eq(pathPayload[2], new Vector2I(1, 1), "move path result 应投影 path 坐标。");
    }

    private void TestMovePathTreeProjectsTypedMaps()
    {
        var result = new BattleMovePathTreeResult();
        result.Costs[new Vector2I(1, 2)] = 5;
        result.Previous[new Vector2I(1, 2)] = new Vector2I(1, 1);
        result.Steps[new Vector2I(1, 2)] = 2;

        Godot.Collections.Dictionary payload = result.ToDictionary();
        Godot.Collections.Dictionary costs = payload["costs"].AsGodotDictionary();
        Godot.Collections.Dictionary previous = payload["previous"].AsGodotDictionary();
        Godot.Collections.Dictionary steps = payload["steps"].AsGodotDictionary();

        _test.Eq(costs[new Vector2I(1, 2)].AsInt32(), 5, "path tree 应投影 cost map。");
        _test.Eq(
            previous[new Vector2I(1, 2)].AsVector2I(),
            new Vector2I(1, 1),
            "path tree 应投影 previous map。"
        );
        _test.Eq(steps[new Vector2I(1, 2)].AsInt32(), 2, "path tree 应投影 step map。");
    }

    private void TestValidatedMoveExecutionResultProjectsTypedPath()
    {
        var result = new BattleValidatedMoveExecutionResult
        {
            Executed = true,
            ReachedTarget = false,
            StoppedByBarrier = true,
        };
        result.ExecutedPath.Add(new Vector2I(0, 0));
        result.ExecutedPath.Add(new Vector2I(0, 1));

        Godot.Collections.Dictionary payload = result.ToDictionary();
        Godot.Collections.Array<Vector2I> executedPath = payload["executed_path"]
            .AsGodotArray<Vector2I>();

        _test.True(
            result.ExecutedPath.GetType() != typeof(Godot.Collections.Array<Vector2I>),
            "ExecutedPath 真相源不应是 Godot Array。"
        );
        _test.True(payload["executed"].AsBool(), "validated move result 应投影 executed。");
        _test.True(
            !payload["reached_target"].AsBool(),
            "validated move result 应投影 reached target。"
        );
        _test.True(
            payload["stopped_by_barrier"].AsBool(),
            "validated move result 应投影 stopped by barrier。"
        );
        _test.Eq(
            executedPath[1],
            new Vector2I(0, 1),
            "validated move result 应投影 executed path。"
        );
    }

    private void TestMovementServiceUsesTypedPathAndExecutesMove()
    {
        PartyState party = BuildParty("movement_hero");
        CharacterManagementModule gateway = new();
        gateway.setup(party, new Godot.Collections.Dictionary(), new Godot.Collections.Dictionary(), new Godot.Collections.Dictionary(), new Godot.Collections.Dictionary());
        BattleRuntimeModule runtime = new();
        runtime.setup(gateway);
        BattleState state = BuildState("movement_service_projection_regression");
        BattleUnitState ally = BuildUnit("movement_hero", Vector2I.Zero);
        BattleUnitState enemy = BuildUnit("movement_enemy", new Vector2I(2, 0));
        enemy.faction_id = "enemy";
        InstallUnits(runtime, state, ally, enemy);
        runtime.SetupStateForTests(state);

        IReadOnlyList<Vector2I> reachable = runtime._movement_service.GetUnitReachableMoveCoords(ally);
        _test.True(
            reachable.GetType() != typeof(Godot.Collections.Array<Vector2I>),
            "BattleMovementService reachable coords 真相源不应是 Godot Array。"
        );
        _test.True(reachable.Count == 1 && reachable[0] == new Vector2I(1, 0), "reachable coords 应只包含可达落点。");

        BattleMovePathResult moveResult = runtime._movement_service.ResolveMovePathResultTyped(
            ally,
            new Vector2I(1, 0)
        );
        _test.True(moveResult.Allowed, $"ResolveMovePathResultTyped 应允许一步移动。 message={moveResult.Message}");
        BattleValidatedMoveExecutionResult executionResult =
            runtime._movement_service.MoveUnitAlongValidatedPathTyped(
                ally,
                moveResult.Path,
                new Vector2I(1, 0),
                new BattleEventBatch()
            );
        _test.True(executionResult.Executed, "typed validated move 应实际执行。");
        _test.True(executionResult.ReachedTarget, "typed validated move 应到达目标。");
        _test.Eq(ally.coord, new Vector2I(1, 0), "typed validated move 应更新单位坐标。");

        runtime.dispose();
    }

    private static bool IsForbiddenGodotBoundaryType(Type type) =>
        type == typeof(Variant)
        || type.FullName == "Godot.Collections.Dictionary"
        || type.FullName == "Godot.Collections.Array";

    private static PartyState BuildParty(StringName memberId)
    {
        var party = new PartyState();
        party.SetMemberState(
            new PartyMemberState
            {
                member_id = memberId,
                display_name = memberId.ToString(),
                progression = new UnitProgress
                {
                    unit_id = memberId,
                    display_name = memberId.ToString(),
                    unit_base_attributes = new UnitBaseAttributes(),
                },
            }
        );
        party.active_member_ids = new Godot.Collections.Array<StringName> { memberId };
        party.leader_member_id = memberId;
        party.main_character_member_id = memberId;
        return party;
    }

    private static BattleState BuildState(StringName battleId)
    {
        var state = new BattleState
        {
            battle_id = battleId,
            phase = "unit_acting",
            map_size = new Vector2I(3, 1),
            timeline = new BattleTimelineState(),
        };
        for (int x = 0; x < state.map_size.X; x++)
        {
            Vector2I coord = new(x, 0);
            var cell = new BattleCellState
            {
                coord = coord,
                base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                base_height = 4,
            };
            cell.RecalculateRuntimeValues();
            state.SetCell(coord, cell);
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(StringName unitId, Vector2I coord)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
            current_ap = 2,
            current_move_points = 2,
            current_hp = 20,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 20);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private void InstallUnits(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState ally,
        BattleUnitState enemy
    )
    {
        state.SetUnit(ally);
        state.ally_unit_ids.Add(ally.unit_id);
        state.SetUnit(enemy);
        state.enemy_unit_ids.Add(enemy.unit_id);
        state.active_unit_id = ally.unit_id;
        _test.True(runtime._grid_service.PlaceUnit(state, ally, ally.coord, true), "测试友方应能放入战场。");
        _test.True(runtime._grid_service.PlaceUnit(state, enemy, enemy.coord, true), "测试敌方应能放入战场。");
    }
}
