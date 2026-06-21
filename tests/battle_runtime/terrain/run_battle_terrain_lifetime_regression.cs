using Godot;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_terrain_lifetime_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(exitCode);
    }

    private int Run()
    {
        TestTerrainStatusTicksUseFormalFields();

        var runtime = new BattleRuntimeModule();
        BattleState state = null;
        BattleEventBatch batch = null;
        CombatEffectDef coreCrater = null;
        CombatEffectDef rubble = null;
        CombatEffectDef dust = null;
        try
        {
            runtime.setup();
            state = BuildState(new Vector2I(4, 2));
            BattleUnitState unit = BuildUnit("mover", new Vector2I(0, 0));
            state.SetUnit(unit);
            state.ally_unit_ids = new GStringNameArray { unit.unit_id };
            state.active_unit_id = unit.unit_id;
            _test.True(
                runtime._grid_service.PlaceUnit(state, unit, unit.coord, true),
                "terrain lifetime 测试单位应能放入战场。"
            );
            runtime.SetupStateForTests(state);
            batch = new BattleEventBatch();

            coreCrater = BuildTerrainEffect(
                "meteor_swarm_crater_core",
                3,
                "battle",
                0,
                0
            );
            rubble = BuildTerrainEffect("meteor_swarm_rubble", 2, "battle", 0, 0);
            dust = BuildTerrainEffect("meteor_swarm_dust", 1, "timed", 50, 5);
            _test.True(
                runtime._terrain_effect_system.UpsertTimedTerrainEffect(
                    new Vector2I(1, 0),
                    unit,
                    null,
                    coreCrater,
                    "core_crater_1"
                ),
                "battle lifetime crater 应能写入 timed_terrain_effects。"
            );
            _test.True(
                runtime._terrain_effect_system.UpsertTimedTerrainEffect(
                    new Vector2I(1, 0),
                    unit,
                    null,
                    rubble,
                    "rubble_1"
                ),
                "battle lifetime rubble 应能写入 timed_terrain_effects。"
            );
            _test.True(
                runtime._terrain_effect_system.UpsertTimedTerrainEffect(
                    new Vector2I(2, 0),
                    unit,
                    null,
                    dust,
                    "dust_1"
                ),
                "timed dust 应能写入 timed_terrain_effects。"
            );

            _test.Eq(
                runtime._terrain_effect_system.GetMoveCostDeltaForUnitTarget(
                    unit,
                    new Vector2I(1, 0)
                ),
                3,
                "crater + rubble 移动成本应按 max stacking，不能叠成 5。"
            );
            state.timeline.current_tu = 55;
            runtime._terrain_effect_system.ProcessTimedTerrainEffects(batch);

            BattleCellState craterCell = GetCell(state, new Vector2I(1, 0));
            BattleCellState dustCell = GetCell(state, new Vector2I(2, 0));
            _test.Eq(
                craterCell != null ? craterCell.timed_terrain_effects.Count : -1,
                2,
                "battle lifetime crater/rubble 推进 55 TU 后仍应存在。"
            );
            _test.Eq(
                dustCell != null ? dustCell.timed_terrain_effects.Count : -1,
                0,
                "timed dust 到期后应消失。"
            );
            _test.Eq(
                runtime._terrain_effect_system.GetMoveCostDeltaForUnitTarget(
                    unit,
                    new Vector2I(1, 0)
                ),
                3,
                "battle lifetime terrain 推进后仍应影响移动成本。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleFixture(runtime, state, batch);
            GodotRefCountedDisposer.DisposeIfValid(coreCrater);
            GodotRefCountedDisposer.DisposeIfValid(rubble);
            GodotRefCountedDisposer.DisposeIfValid(dust);
        }

        return _test.Finish("Battle terrain lifetime regression");
    }

    private void TestTerrainStatusTicksUseFormalFields()
    {
        var runtime = new BattleRuntimeModule();
        BattleState state = null;
        BattleEventBatch preTickBatch = null;
        BattleEventBatch tickBatch = null;
        CombatEffectDef burningTerrain = null;
        try
        {
            runtime.setup();
            state = BuildState(new Vector2I(2, 1));
            BattleUnitState unit = BuildUnit("burn_target", new Vector2I(0, 0));
            state.SetUnit(unit);
            state.ally_unit_ids = new GStringNameArray { unit.unit_id };
            state.active_unit_id = unit.unit_id;
            _test.True(
                runtime._grid_service.PlaceUnit(state, unit, unit.coord, true),
                "terrain status tick 测试单位应能放入战场。"
            );
            runtime.SetupStateForTests(state);
            state.timeline.current_tu = 0;

            burningTerrain = BuildTerrainEffect(
                "typed_burning_ground",
                0,
                "timed",
                40,
                20,
                "status",
                "burning",
                40
            );
            _test.True(
                runtime._terrain_effect_system.UpsertTimedTerrainEffect(
                    unit.coord,
                    unit,
                    null,
                    burningTerrain,
                    "typed_burning_ground_1"
                ),
                "typed burning terrain 应能写入 timed_terrain_effects。"
            );

            preTickBatch = new BattleEventBatch();
            runtime._terrain_effect_system.ProcessTimedTerrainEffects(preTickBatch);
            _test.True(!unit.HasStatusEffect("burning"), "tick 前不应提前附加 burning。");

            state.timeline.current_tu = 20;
            tickBatch = new BattleEventBatch();
            runtime._terrain_effect_system.ProcessTimedTerrainEffects(tickBatch);
            _test.True(unit.HasStatusEffect("burning"), "terrain tick 到点后应附加 burning。");
            BattleStatusEffectState burningEntry = unit.GetStatusEffect("burning");
            _test.Eq(
                burningEntry != null ? burningEntry.duration : -1,
                40,
                "terrain tick status duration 应直接来自 formal applied_status_duration_tu。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleFixture(runtime, state, preTickBatch, tickBatch);
            GodotRefCountedDisposer.DisposeIfValid(burningTerrain);
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
                var cell = new BattleCellState
                {
                    coord = coord,
                    passable = true,
                };
                state.SetCell(coord, cell);
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
        unit.RefreshFootprint();
        return unit;
    }

    private static CombatEffectDef BuildTerrainEffect(
        StringName effectId,
        int moveCostDelta,
        StringName lifetimePolicy,
        int durationTu,
        int tickIntervalTu,
        StringName tickEffectType = default,
        StringName statusId = default,
        int appliedStatusDurationTu = 0
    )
    {
        return new CombatEffectDef
        {
            effect_type = "terrain_effect",
            tick_effect_type = tickEffectType == default ? "none" : tickEffectType,
            lifetime_policy = lifetimePolicy == default ? new StringName("timed") : lifetimePolicy,
            move_cost_delta = moveCostDelta,
            status_id = statusId == default ? new StringName("") : statusId,
            applied_status_duration_tu = appliedStatusDurationTu,
            terrain_effect_id = effectId,
            display_name = effectId.ToString(),
            render_overlay_id = effectId,
            duration_tu = durationTu,
            tick_interval_tu = tickIntervalTu,
            effect_target_team_filter = "any",
        };
    }

    private static BattleCellState GetCell(BattleState state, Vector2I coord)
    {
        if (state == null || !state.ContainsCell(coord))
            return null;
        return state.GetCell(coord);
    }
}
