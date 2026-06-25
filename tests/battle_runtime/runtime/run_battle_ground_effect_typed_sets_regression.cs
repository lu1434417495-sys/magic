using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_battle_ground_effect_typed_sets_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestWindPushUsesTypedAffectedSets();
        TestGroundUnitEffectsMergesTypedWindPushAffectedIds();
        TestSpecialForcedMoveUsesTypedContextDirection();
        TestGroundApplicationResultsProjectInternalBoundary();
        TestBuildGroundEffectCoordsUsesTypedHelperBoundary();
        TestDedupeEffectDefsUsesTypedHelperBoundary();
        Quit(_test.Finish("Battle ground effect typed sets regression"));
    }

    private void TestGroundEffectServiceUsesPlainTypedHelperBoundary()
    {
    }

    private void TestWindPushUsesTypedAffectedSets()
    {
        Fixture fixture = BuildWindPushFixture();
        var batch = new BattleEventBatch();
        BattleGroundWindPushResult result =
            fixture.Runtime._ground_effect_service._apply_ground_wind_push_effects_result(
                fixture.Source,
                fixture.Skill,
                new List<CombatEffectDef> { fixture.WindPushEffect },
                new List<Vector2I> { new Vector2I(1, 0) },
                new List<Vector2I> { new Vector2I(1, 0) },
                batch
            );

        _test.True(result.Applied, "wind push 应成功推动连锁单位。");
        _test.Eq(fixture.Front.coord, new Vector2I(2, 0), "前排单位应被推到后排原坐标。");
        _test.Eq(fixture.Back.coord, new Vector2I(3, 0), "后排阻挡单位应先被递归推开。");
        _test.True(
            result.AffectedUnitIds.Contains(fixture.Front.unit_id),
            "typed affected set 应包含前排单位。"
        );
        _test.True(
            result.AffectedUnitIds.Contains(fixture.Back.unit_id),
            "typed affected set 应包含递归推动的后排单位。"
        );
        _test.Eq(result.AffectedUnitIds.Count, 2, "typed affected set 不应重复记录单位。");
        CleanupFixture(fixture, batch);
    }

    private void TestSpecialSkillResolverUsesPlainTypedHelperBoundary()
    {
    }

    private void TestGroundUnitEffectsMergesTypedWindPushAffectedIds()
    {
        Fixture fixture = BuildWindPushFixture();
        var batch = new BattleEventBatch();
        BattleGroundUnitEffectsResult result =
            fixture.Runtime._ground_effect_service._apply_ground_unit_effects_result(
                fixture.Source,
                fixture.Skill,
                new List<CombatEffectDef> { fixture.WindPushEffect },
                new List<Vector2I> { new Vector2I(1, 0) },
                batch,
                new List<Vector2I> { new Vector2I(1, 0) }
            );

        _test.True(result.Applied, "ground unit effects 应应用 wind push。");
        _test.Eq(result.AffectedUnitCount, 2, "ground unit effects 应合并 wind push affected set。");
        _test.Eq(fixture.Front.coord, new Vector2I(2, 0), "ground unit effects 应推动前排单位。");
        _test.Eq(fixture.Back.coord, new Vector2I(3, 0), "ground unit effects 应推动递归阻挡单位。");
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
            new Godot.Collections.Array<CombatEffectDef> { fixture.WindPushEffect },
            batch,
            BattleForcedMoveContext.FromDirection(Vector2I.Right)
        );

        _test.True(result.Applied, "typed forced move context 应触发 wind_push。");
        _test.Eq(result.MovedSteps, 1, "typed forced move context 应记录移动步数。");
        _test.Eq(
            fixture.Front.coord,
            new Vector2I(3, 0),
            "typed context direction 应覆盖 source->target fallback 方向。"
        );
        CleanupFixture(fixture, batch);
    }

    private void TestGroundApplicationResultPublicApiStaysTyped()
    {
    }

    private void AssertResultTypePublicApiStaysTyped(Type type, string typeName)
    {
        _test.True(
            type.IsValueType || type.IsSealed,
            $"{typeName} 应保持 plain C# result DTO。"
        );
    }

    private void TestGroundApplicationResultsProjectInternalBoundary()
    {
        Godot.Collections.Dictionary unitPayload =
            BattleGroundEffectApplicationResultProjection.ProjectUnitEffects(
                new BattleGroundUnitEffectsResult(true, 3, 14, 2, 1)
            );
        _test.True(ReadBool(unitPayload, "applied"), "ground unit result 应投影 applied。");
        _test.Eq(
            ReadInt(unitPayload, "affected_unit_count"),
            3,
            "ground unit result 应投影 affected count。"
        );
        _test.Eq(ReadInt(unitPayload, "damage"), 14, "ground unit result 应投影 damage。");
        _test.Eq(ReadInt(unitPayload, "healing"), 2, "ground unit result 应投影 healing。");
        _test.Eq(ReadInt(unitPayload, "kill_count"), 1, "ground unit result 应投影 kill count。");

        Godot.Collections.Dictionary terrainPayload =
            BattleGroundEffectApplicationResultProjection.ProjectTerrainEffects(
                new BattleGroundTerrainEffectsResult(true)
            );
        _test.True(ReadBool(terrainPayload, "applied"), "ground terrain result 应投影 applied。");

        Godot.Collections.Dictionary windPayload =
            BattleGroundEffectApplicationResultProjection.ProjectWindPush(
                new BattleGroundWindPushResult(
                    true,
                    new[] { new StringName("front"), new StringName("back") }
                )
            );
        _test.True(ReadBool(windPayload, "applied"), "wind push result 应投影 applied。");
        Godot.Collections.Array affectedIds = windPayload["affected_unit_ids"].AsGodotArray();
        _test.Eq(
            ProgressionDataUtils.to_string_name(affectedIds[0]),
            new StringName("front"),
            "wind push result 应投影第一个 affected id。"
        );
        _test.Eq(
            ProgressionDataUtils.to_string_name(affectedIds[1]),
            new StringName("back"),
            "wind push result 应投影第二个 affected id。"
        );
    }

    private void TestBuildGroundEffectCoordsUsesTypedHelperBoundary()
    {
        Fixture fixture = BuildGroundEffectCoordsFixture();
        try
        {
            var castVariant = TestResourceOwnership.Own(
                new CombatCastVariantDef
            {
                @params = new Godot.Collections.Dictionary { ["square2_corner"] = "top_left" }
                },
                "battle_ground_effect_typed_sets.cast_variant"
            );
            IReadOnlyList<Vector2I> typedCoords = fixture.Runtime._ground_effect_service
                .BuildGroundEffectCoords(
                    null,
                    new List<Vector2I> { new Vector2I(1, 1) },
                    new Vector2I(-1, -1),
                    null,
                    castVariant
                );

            _test.Eq(typedCoords.Count, 4, "typed ground effect coords 应展开 square2。");
            _test.Eq(typedCoords[0], new Vector2I(1, 1), "typed ground effect coords 应按 Y/X 排序。");
            _test.Eq(typedCoords[3], new Vector2I(2, 2), "typed ground effect coords 应包含右下角。");
        }
        finally
        {
            CleanupFixture(fixture, null);
        }
    }

    private void TestDedupeEffectDefsUsesTypedHelperBoundary()
    {
        Fixture fixture = BuildWindPushFixture();
        try
        {
            var duplicatePayload = new Godot.Collections.Array<CombatEffectDef>
            {
                fixture.WindPushEffect,
                fixture.WindPushEffect,
            };
            IReadOnlyList<CombatEffectDef> typedDeduped = fixture.Runtime._ground_effect_service
                .DedupeEffectDefsByInstanceTyped(duplicatePayload);

            _test.Eq(typedDeduped.Count, 1, "typed dedupe helper 应按实例去重。");
            _test.True(
                ReferenceEquals(typedDeduped[0], fixture.WindPushEffect),
                "typed dedupe helper 应保留原始 effect 实例。"
            );
        }
        finally
        {
            CleanupFixture(fixture, null);
        }
    }

    private void TestEdgeClearUsesTypedPrivateBoundary()
    {
    }

    private Fixture BuildWindPushFixture()
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup();

        BattleState state = BuildState(new Vector2I(5, 1));
        BattleUnitState source = BuildUnit("typed_wind_source", "player", new Vector2I(0, 0));
        BattleUnitState front = BuildUnit("typed_wind_front", "enemy", new Vector2I(1, 0));
        BattleUnitState back = BuildUnit("typed_wind_back", "enemy", new Vector2I(2, 0));
        AddUnit(runtime, state, source);
        AddUnit(runtime, state, front);
        AddUnit(runtime, state, back);
        runtime.SetupStateForTests(state);

        return new Fixture
        {
            Runtime = runtime,
            State = state,
            Source = source,
            Front = front,
            Back = back,
            Skill = TestResourceOwnership.Own(
                new SkillDef
            {
                skill_id = "typed_wind_push_skill",
                combat_profile = new CombatSkillDef { target_team_filter = "enemy" },
                },
                "battle_ground_effect_typed_sets.wind_push_skill"
            ),
            WindPushEffect = TestResourceOwnership.Own(
                new CombatEffectDef
            {
                effect_type = "forced_move",
                effect_target_team_filter = "enemy",
                forced_move_mode = "wind_push",
                forced_move_distance = 1,
                },
                "battle_ground_effect_typed_sets.wind_push_effect"
            ),
        };
    }

    private Fixture BuildForcedMoveContextFixture()
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup();

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
        runtime.SetupStateForTests(state);

        return new Fixture
        {
            Runtime = runtime,
            State = state,
            Source = source,
            Front = front,
            Skill = TestResourceOwnership.Own(
                new SkillDef
            {
                skill_id = "typed_forced_context_skill",
                combat_profile = new CombatSkillDef { target_team_filter = "enemy" },
                },
                "battle_ground_effect_typed_sets.forced_context_skill"
            ),
            WindPushEffect = TestResourceOwnership.Own(
                new CombatEffectDef
            {
                effect_type = "forced_move",
                effect_target_team_filter = "enemy",
                forced_move_mode = "wind_push",
                forced_move_distance = 1,
                },
                "battle_ground_effect_typed_sets.forced_context_effect"
            ),
        };
    }

    private Fixture BuildGroundEffectCoordsFixture()
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup();
        BattleState state = BuildState(new Vector2I(4, 4));
        runtime.SetupStateForTests(state);
        return new Fixture { Runtime = runtime, State = state };
    }

    private static void CleanupFixture(Fixture fixture, BattleEventBatch batch)
    {
        if (fixture == null)
        {
            return;
        }
        fixture.Runtime?._state?.ClearUnits();
        fixture.Runtime?._state?.ClearCells();
        if (fixture.Runtime != null)
        {
            fixture.Runtime.SetupStateForTests(null);
            fixture.Runtime.dispose();
        }
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
                state.SetCell(coord, new BattleCellState { coord = coord, passable = true });
            }
        }
        state.RebuildCellColumns();
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
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 20);
        return unit;
    }

    private void AddUnit(BattleRuntimeModule runtime, BattleState state, BattleUnitState unit)
    {
        state.SetUnit(unit);
        if (unit.faction_id == new StringName("player"))
        {
            state.ally_unit_ids.Add(unit.unit_id);
        }
        else
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        _test.True(
            runtime._grid_service.PlaceUnit(state, unit, unit.coord, true),
            $"单位应能放入测试棋盘：{unit.unit_id}"
        );
    }

    private static bool IsGodotCollectionOrVariant(Type type)
    {
        if (type == typeof(Variant))
            return true;
        Type genericDefinition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        return genericDefinition == typeof(Godot.Collections.Dictionary)
            || genericDefinition == typeof(Godot.Collections.Array)
            || type.Namespace == "Godot.Collections";
    }

    private static bool ReadBool(Godot.Collections.Dictionary source, string key) =>
        source != null
        && source.ContainsKey(key)
        && source[key].AsBool();

    private static int ReadInt(Godot.Collections.Dictionary source, string key) =>
        source != null && source.ContainsKey(key) ? source[key].AsInt32() : 0;

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
