using System.Linq;
using Godot;
using GArray = Godot.Collections.Array;
using GCombatEffectArray = Godot.Collections.Array<CombatEffectDef>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_ground_effect_typed_sets_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        GodotSharpCleanup.collect_pending_finalizers();
        Quit(exitCode);
    }

    private int Run()
    {
        TestWindPushUsesTypedAffectedSets();
        TestGroundUnitEffectsMergesTypedWindPushAffectedIds();
        TestSpecialForcedMoveUsesTypedContextDirection();
        TestSpecialForcedMoveWrapperUsesTypedContext();

        if (_failures.Count == 0)
        {
            GD.Print("Battle ground effect typed sets regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle ground effect typed sets regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestWindPushUsesTypedAffectedSets()
    {
        Fixture fixture = BuildWindPushFixture();
        var batch = new BattleEventBatch();
        BattleGroundWindPushResult result =
            fixture.Runtime._ground_effect_service._apply_ground_wind_push_effects_result(
                fixture.Source,
                fixture.Skill,
                new GArray { fixture.WindPushEffect },
                new GArray { new Vector2I(1, 0) },
                new GArray { new Vector2I(1, 0) },
                batch
            );

        AssertTrue(result.Applied, "wind push 应成功推动连锁单位。");
        AssertEq(fixture.Front.coord, new Vector2I(2, 0), "前排单位应被推到后排原坐标。");
        AssertEq(fixture.Back.coord, new Vector2I(3, 0), "后排阻挡单位应先被递归推开。");
        AssertTrue(
            result.AffectedUnitIds.Contains(fixture.Front.unit_id),
            "typed affected set 应包含前排单位。"
        );
        AssertTrue(
            result.AffectedUnitIds.Contains(fixture.Back.unit_id),
            "typed affected set 应包含递归推动的后排单位。"
        );
        AssertEq(result.AffectedUnitIds.Count, 2, "typed affected set 不应重复记录单位。");
        CleanupFixture(fixture, batch);
    }

    private void TestGroundUnitEffectsMergesTypedWindPushAffectedIds()
    {
        Fixture fixture = BuildWindPushFixture();
        var batch = new BattleEventBatch();
        BattleGroundUnitEffectsResult result =
            fixture.Runtime._ground_effect_service._apply_ground_unit_effects_result(
                fixture.Source,
                fixture.Skill,
                new GArray { fixture.WindPushEffect },
                new GArray { new Vector2I(1, 0) },
                batch,
                new GArray { new Vector2I(1, 0) }
            );

        AssertTrue(result.Applied, "ground unit effects 应应用 wind push。");
        AssertEq(result.AffectedUnitCount, 2, "ground unit effects 应合并 wind push affected set。");
        AssertEq(fixture.Front.coord, new Vector2I(2, 0), "ground unit effects 应推动前排单位。");
        AssertEq(fixture.Back.coord, new Vector2I(3, 0), "ground unit effects 应推动递归阻挡单位。");
        CleanupFixture(fixture, batch);
    }

    private void TestSpecialForcedMoveUsesTypedContextDirection()
    {
        Fixture fixture = BuildForcedMoveContextFixture();
        var batch = new BattleEventBatch();
        BattleSpecialSkillResult result = fixture.Runtime.ApplyUnitSkillSpecialEffectsResult(
            fixture.Source,
            fixture.Front,
            fixture.Skill,
            null,
            new GCombatEffectArray { fixture.WindPushEffect },
            batch,
            BattleForcedMoveContext.FromDirection(Vector2I.Right)
        );

        AssertTrue(result.Applied, "typed forced move context 应触发 wind_push。");
        AssertEq(result.MovedSteps, 1, "typed forced move context 应记录移动步数。");
        AssertEq(
            fixture.Front.coord,
            new Vector2I(3, 0),
            "typed context direction 应覆盖 source->target fallback 方向。"
        );
        CleanupFixture(fixture, batch);
    }

    private void TestSpecialForcedMoveWrapperUsesTypedContext()
    {
        Fixture fixture = BuildForcedMoveContextFixture();
        var batch = new BattleEventBatch();
        GDictionary result = fixture.Runtime._apply_unit_skill_special_effects(
            fixture.Source,
            fixture.Front,
            fixture.Skill,
            null,
            new GCombatEffectArray { fixture.WindPushEffect },
            batch,
            BattleForcedMoveContext.FromDirection(Vector2I.Right)
        );

        AssertTrue(ReadBool(result, "applied"), "runtime wrapper 应使用 typed forced move context。");
        AssertEq(
            fixture.Front.coord,
            new Vector2I(3, 0),
            "runtime wrapper typed 方向也应覆盖 source->target fallback。"
        );
        CleanupFixture(fixture, batch);
    }

    private Fixture BuildWindPushFixture()
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            null,
            null,
            new GDictionary(),
            null
        );

        BattleState state = BuildState(new Vector2I(5, 1));
        BattleUnitState source = BuildUnit("typed_wind_source", "player", new Vector2I(0, 0));
        BattleUnitState front = BuildUnit("typed_wind_front", "enemy", new Vector2I(1, 0));
        BattleUnitState back = BuildUnit("typed_wind_back", "enemy", new Vector2I(2, 0));
        AddUnit(runtime, state, source);
        AddUnit(runtime, state, front);
        AddUnit(runtime, state, back);
        runtime._state = state;

        return new Fixture
        {
            Runtime = runtime,
            State = state,
            Source = source,
            Front = front,
            Back = back,
            Skill = new SkillDef
            {
                skill_id = "typed_wind_push_skill",
                combat_profile = new CombatSkillDef { target_team_filter = "enemy" },
            },
            WindPushEffect = new CombatEffectDef
            {
                effect_type = "forced_move",
                effect_target_team_filter = "enemy",
                forced_move_mode = "wind_push",
                forced_move_distance = 1,
            },
        };
    }

    private Fixture BuildForcedMoveContextFixture()
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            null,
            null,
            new GDictionary(),
            null
        );

        BattleState state = BuildState(new Vector2I(5, 1));
        BattleUnitState source = BuildUnit(
            "typed_forced_context_source",
            "player",
            new Vector2I(4, 0)
        );
        BattleUnitState front = BuildUnit(
            "typed_forced_context_target",
            "enemy",
            new Vector2I(2, 0)
        );
        AddUnit(runtime, state, source);
        AddUnit(runtime, state, front);
        runtime._state = state;

        return new Fixture
        {
            Runtime = runtime,
            State = state,
            Source = source,
            Front = front,
            Skill = new SkillDef
            {
                skill_id = "typed_forced_context_skill",
                combat_profile = new CombatSkillDef { target_team_filter = "enemy" },
            },
            WindPushEffect = new CombatEffectDef
            {
                effect_type = "forced_move",
                effect_target_team_filter = "enemy",
                forced_move_mode = "wind_push",
                forced_move_distance = 1,
            },
        };
    }

    private static void CleanupFixture(Fixture fixture, BattleEventBatch batch)
    {
        if (fixture == null)
        {
            return;
        }
        fixture.Runtime?._state?.units?.Clear();
        fixture.Runtime?._state?.cells?.Clear();
        if (fixture.Runtime != null)
        {
            fixture.Runtime._state = null;
            fixture.Runtime.dispose();
            fixture.Runtime.Dispose();
        }
        batch?.Dispose();
        fixture.WindPushEffect?.Dispose();
        fixture.Skill?.combat_profile?.Dispose();
        fixture.Skill?.Dispose();
        fixture.Source?.Dispose();
        fixture.Front?.Dispose();
        fixture.Back?.Dispose();
        fixture.State?.Dispose();
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "battle_ground_effect_typed_sets_regression",
            phase = "unit_acting",
            active_unit_id = "typed_wind_source",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                state.cells[coord] = new BattleCellState { coord = coord, passable = true };
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName factionId, Vector2I coord)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            current_hp = 20,
            is_alive = true,
        };
        unit.set_anchor_coord(coord);
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 20);
        return unit;
    }

    private void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.units[unit.unit_id] = unit;
        if (unit.faction_id == new StringName("player"))
        {
            state.ally_unit_ids.Add(unit.unit_id);
        }
        else
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        AssertTrue(
            runtime._grid_service.place_unit(state, unit, unit.coord, true),
            $"单位应能放入测试棋盘：{unit.unit_id}"
        );
    }

    private void AssertTrue(bool value, string message)
    {
        if (!value)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private static bool ReadBool(GDictionary source, string key) =>
        source != null
        && source.ContainsKey(key)
        && source[key].AsBool();

    private sealed class Fixture
    {
        public BattleRuntimeModule Runtime;
        public BattleState State;
        public BattleUnitState Source;
        public BattleUnitState Front;
        public BattleUnitState Back;
        public SkillDef Skill;
        public CombatEffectDef WindPushEffect;
    }
}
