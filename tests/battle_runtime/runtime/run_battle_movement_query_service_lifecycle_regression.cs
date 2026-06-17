using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_movement_query_service_lifecycle_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestDisposeRuntimeClearsBindingsAndAllowsRebind();
        Quit(_test.Finish("Battle movement query service lifecycle regression"));
    }

    private void TestDisposeRuntimeClearsBindingsAndAllowsRebind()
    {
        var service = new BattleMovementQueryService();
        var gridService = new BattleGridService();
        BattleState firstState = BuildState(new Vector2I(3, 1));
        BattleUnitState firstUnit = BuildUnit("first_unit", Vector2I.Zero);
        InstallUnit(gridService, firstState, firstUnit);

        service.Setup(firstState, gridService, FixedMoveCost);
        GDictionary firstQuery = service.CollectReachableAnchors(
            firstUnit.unit_id,
            firstUnit.coord,
            1
        );
        _test.True(ReadBool(firstQuery, "ok"), "初次 setup 后应能查询 first_unit 可达格。");

        service.DisposeRuntime();
        GDictionary disposedQuery = service.CollectReachableAnchors(
            firstUnit.unit_id,
            firstUnit.coord,
            1
        );
        _test.False(ReadBool(disposedQuery, "ok"), "DisposeRuntime 后不应继续读旧 BattleState。");
        _test.Eq(
            ReadStringName(disposedQuery, "reject_reason"),
            new StringName("missing_unit"),
            "DisposeRuntime 后旧 unit 查询应以 missing_unit 失败。"
        );

        BattleState secondState = BuildState(new Vector2I(3, 1));
        BattleUnitState secondUnit = BuildUnit("second_unit", new Vector2I(1, 0));
        InstallUnit(gridService, secondState, secondUnit);

        service.Setup(secondState, gridService, FixedMoveCost);
        GDictionary reboundQuery = service.CollectReachableAnchors(
            secondUnit.unit_id,
            secondUnit.coord,
            1
        );
        _test.True(ReadBool(reboundQuery, "ok"), "DisposeRuntime 后应允许重新 setup 新 BattleState。");

        GDictionary oldUnitAfterRebind = service.CollectReachableAnchors(
            firstUnit.unit_id,
            firstUnit.coord,
            1
        );
        _test.False(
            ReadBool(oldUnitAfterRebind, "ok"),
            "重新 setup 后不应残留旧 BattleState 的 first_unit。"
        );
        _test.Eq(
            ReadStringName(oldUnitAfterRebind, "reject_reason"),
            new StringName("missing_unit"),
            "重新 setup 后旧 unit 查询应以 missing_unit 失败。"
        );

        service.DisposeRuntime();
        service.Dispose();
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

    private static bool ReadBool(GDictionary payload, string key) =>
        payload != null
        && payload.ContainsKey(key)
        && payload[key].VariantType == Variant.Type.Bool
        && payload[key].AsBool();

    private static StringName ReadStringName(GDictionary payload, string key)
    {
        if (payload == null || !payload.ContainsKey(key))
            return "";
        Variant value = payload[key];
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => "",
        };
    }
}
