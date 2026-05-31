using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_terrain_lifetime_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup(null, new GDictionary(), new GDictionary(), new GDictionary());
        BattleState state = BuildState(new Vector2I(4, 2));
        BattleUnitState unit = BuildUnit("mover", new Vector2I(0, 0));
        state.units[unit.unit_id] = unit;
        state.ally_unit_ids = new GStringNameArray { unit.unit_id };
        state.active_unit_id = unit.unit_id;
        AssertTrue(
            runtime._grid_service.place_unit(state, unit, unit.coord, true),
            "terrain lifetime 测试单位应能放入战场。"
        );
        runtime._state = state;
        var batch = new BattleEventBatch();

        CombatEffectDef coreCrater = BuildTerrainEffect(
            "meteor_swarm_crater_core",
            3,
            "battle",
            0,
            0
        );
        CombatEffectDef rubble = BuildTerrainEffect("meteor_swarm_rubble", 2, "battle", 0, 0);
        CombatEffectDef dust = BuildTerrainEffect("meteor_swarm_dust", 1, "timed", 50, 5);
        AssertTrue(
            runtime._terrain_effect_system.upsert_timed_terrain_effect(
                new Vector2I(1, 0),
                unit,
                null,
                coreCrater,
                "core_crater_1"
            ),
            "battle lifetime crater 应能写入 timed_terrain_effects。"
        );
        AssertTrue(
            runtime._terrain_effect_system.upsert_timed_terrain_effect(
                new Vector2I(1, 0),
                unit,
                null,
                rubble,
                "rubble_1"
            ),
            "battle lifetime rubble 应能写入 timed_terrain_effects。"
        );
        AssertTrue(
            runtime._terrain_effect_system.upsert_timed_terrain_effect(
                new Vector2I(2, 0),
                unit,
                null,
                dust,
                "dust_1"
            ),
            "timed dust 应能写入 timed_terrain_effects。"
        );

        AssertEq(
            runtime._terrain_effect_system.get_move_cost_delta_for_unit_target(
                unit,
                new Vector2I(1, 0)
            ),
            3,
            "crater + rubble 移动成本应按 max stacking，不能叠成 5。"
        );
        state.timeline.current_tu = 55;
        runtime._terrain_effect_system.process_timed_terrain_effects(batch);

        BattleCellState craterCell = GetCell(state, new Vector2I(1, 0));
        BattleCellState dustCell = GetCell(state, new Vector2I(2, 0));
        AssertEq(
            craterCell != null ? craterCell.timed_terrain_effects.Count : -1,
            2,
            "battle lifetime crater/rubble 推进 55 TU 后仍应存在。"
        );
        AssertEq(
            dustCell != null ? dustCell.timed_terrain_effects.Count : -1,
            0,
            "timed dust 到期后应消失。"
        );
        AssertEq(
            runtime._terrain_effect_system.get_move_cost_delta_for_unit_target(
                unit,
                new Vector2I(1, 0)
            ),
            3,
            "battle lifetime terrain 推进后仍应影响移动成本。"
        );

        if (_failures.Count == 0)
        {
            GD.Print("Battle terrain lifetime regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle terrain lifetime regression: FAIL ({_failures.Count})");
        return 1;
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState { map_size = mapSize };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                var coord = new Vector2I(x, y);
                var cell = new BattleCellState
                {
                    coord = coord,
                    passable = true,
                };
                state.cells[coord] = cell;
            }
        }
        return state;
    }

    private static BattleUnitState BuildUnit(StringName unitId, Vector2I coord)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
            coord = coord,
            is_alive = true,
        };
        unit.refresh_footprint();
        return unit;
    }

    private static CombatEffectDef BuildTerrainEffect(
        StringName effectId,
        int moveCostDelta,
        StringName lifetimePolicy,
        int durationTu,
        int tickIntervalTu
    )
    {
        return new CombatEffectDef
        {
            effect_type = "terrain_effect",
            tick_effect_type = "none",
            terrain_effect_id = effectId,
            duration_tu = durationTu,
            tick_interval_tu = tickIntervalTu,
            effect_target_team_filter = "any",
            @params = new GDictionary
            {
                ["lifetime_policy"] = lifetimePolicy,
                ["move_cost_delta"] = moveCostDelta,
                ["display_name"] = effectId.ToString(),
                ["render_overlay_id"] = effectId.ToString(),
            },
        };
    }

    private static BattleCellState GetCell(BattleState state, Vector2I coord)
    {
        if (state == null || state.cells == null || !state.cells.ContainsKey(coord))
            return null;
        return state.cells[coord].AsGodotObject() as BattleCellState;
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} actual={actual} expected={expected}");
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }
}
