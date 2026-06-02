using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_ai_query_service_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        try
        {
            TestQueryServiceIsPlainCSharpBoundary();
            TestQueryServiceConsumesTypedSkillIndexAndBuildsActionScore();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        if (_failures.Count == 0)
        {
            GD.Print("Battle AI query service regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle AI query service regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestQueryServiceIsPlainCSharpBoundary()
    {
        Type queryType = typeof(BattleAiQueryService);
        AssertTrue(queryType.IsSealed, "BattleAiQueryService 应是 sealed C# query helper。");
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(queryType),
            "BattleAiQueryService 不应继承 GodotObject/RefCounted。"
        );
        AssertTrue(
            queryType.GetCustomAttribute<GlobalClassAttribute>() == null,
            "BattleAiQueryService 不应注册 GlobalClass。"
        );
        AssertTrue(
            queryType.GetMethod("setup") == null
                && queryType.GetMethod("setup_readonly") == null
                && queryType.GetMethod("get_actor_id") == null
                && queryType.GetMethod("get_actor_snapshot") == null
                && queryType.GetMethod("get_unit_snapshot") == null
                && queryType.GetMethod("get_skill_record") == null
                && queryType.GetMethod("build_action_score_input") == null
                && queryType.GetMethod("build_skill_score_input") == null
                && queryType.GetMethod("get_movement_query_service") == null,
            "BattleAiQueryService 不应保留 GDScript-style snake_case public API。"
        );
        AssertPublicApiDoesNotExposeGodotCollections(queryType, "BattleAiQueryService");

        Type skillRecordType = typeof(BattleAiQueryService.SkillRecord);
        AssertTrue(
            skillRecordType.GetMethod("ToDictionary") == null,
            "BattleAiQueryService.SkillRecord 不应保留 Dictionary 投影 API。"
        );
    }

    private void TestQueryServiceConsumesTypedSkillIndexAndBuildsActionScore()
    {
        Fixture fixture = BuildFixture();
        bool callbackCalled = false;
        BattleAiScoreInput callbackScoreInput = null;

        var query = new BattleAiQueryService();
        query.Setup(
            fixture.State,
            fixture.GridService,
            fixture.Actor.unit_id,
            fixture.SkillDefs,
            (service, actionKind, actionLabel, scoreBucketId, command, preview, metadata) =>
            {
                callbackCalled = service == query;
                callbackScoreInput = new BattleAiScoreInput
                {
                    action_kind = actionKind,
                    action_label = actionLabel,
                    score_bucket_id = scoreBucketId,
                    command = command,
                    preview = preview,
                    runtime_action_metadata =
                        metadata?.Duplicate(true) ?? new Godot.Collections.Dictionary(),
                };
                return callbackScoreInput;
            },
            null,
            unitId => unitId == fixture.Actor.unit_id
        );

        AssertEq(query.GetActorId(), fixture.Actor.unit_id, "actor id 应从 Setup 规范化保存。");
        AssertEq(
            query.GetActorSnapshot()?.unit_id ?? new StringName(""),
            fixture.Actor.unit_id,
            "actor snapshot 应从 battle state 构建。"
        );
        AssertEq(
            query.GetLivingUnitSnapshotsTyped("enemy").Count,
            1,
            "enemy living snapshots 应基于 actor faction 解析。"
        );
        AssertTrue(
            query.IsUnitMovementBlocked(fixture.Actor.unit_id),
            "movement blocked callback 应通过 typed StringName 调用。"
        );
        AssertEq(
            query.DistanceFromAnchorToTarget(
                fixture.Actor.coord,
                fixture.Actor.footprint_size,
                fixture.Target.unit_id
            ),
            2,
            "distance query 应通过 typed snapshot/grid 服务计算。"
        );

        AssertTrue(
            query.TryGetSkillRecordTyped(fixture.Skill.skill_id, out BattleAiQueryService.SkillRecord record),
            "QueryService 应从 typed skill-def index 生成 SkillRecord。"
        );
        AssertEq(record.skill_id, fixture.Skill.skill_id, "SkillRecord.skill_id 应来自 typed SkillDef。");
        AssertEq(record.range_value, 5, "SkillRecord.range_value 应读取有效技能范围。");
        AssertEq(record.ai_tags.Count, 1, "SkillRecord.ai_tags 应使用 typed List<StringName>。");
        AssertEq(record.ai_tags[0], new StringName("setup"), "SkillRecord.ai_tags 应保留技能 tag。");

        BattleCommand command = new()
        {
            command_type = BattleCommand.TYPE_MOVE(),
            unit_id = fixture.Actor.unit_id,
            target_coord = new Vector2I(2, 1),
        };
        BattleAiScoreInput scoreInput = query.BuildActionScoreInput(
            "move",
            "query move",
            "positioning",
            command,
            new BattlePreview { move_cost = 1 },
            new Godot.Collections.Dictionary
            {
                ["runtime_action_metadata"] = new Godot.Collections.Dictionary
                {
                    ["generated"] = true,
                },
            }
        );

        AssertTrue(callbackCalled, "BuildActionScoreInput 应调用 typed C# callback。");
        AssertEq(scoreInput, callbackScoreInput, "BuildActionScoreInput 应返回 callback score input。");
        AssertEq(scoreInput?.action_kind ?? new StringName(""), new StringName("move"), "score action_kind 应透传。");
        AssertEq(scoreInput?.action_label ?? "", "query move", "score action_label 应透传。");
    }

    private Fixture BuildFixture()
    {
        BattleState state = BuildFlatState(new Vector2I(5, 3));
        var gridService = new BattleGridService();
        BattleUnitState actor = BuildUnit("query_actor", "AI", "enemy", new Vector2I(1, 1));
        BattleUnitState target = BuildUnit("query_target", "Target", "player", new Vector2I(3, 1));
        AddUnitToState(gridService, state, actor, isEnemy: true);
        AddUnitToState(gridService, state, target, isEnemy: false);

        SkillDef skill = BuildSkill();
        var skillDefs = new Dictionary<StringName, SkillDef> { [skill.skill_id] = skill };

        return new Fixture
        {
            State = state,
            GridService = gridService,
            Actor = actor,
            Target = target,
            Skill = skill,
            SkillDefs = skillDefs,
        };
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "ai_query_service_regression",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                var cell = new BattleCellState
                {
                    coord = new Vector2I(x, y),
                    base_terrain = BattleCellState.TERRAIN_LAND(),
                    base_height = 4,
                    height_offset = 0,
                };
                cell.recalculate_runtime_values();
                state.cells[cell.coord] = cell;
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            current_hp = 20,
            current_mp = 20,
            current_stamina = 10,
            current_ap = 2,
            current_move_points = 2,
            is_alive = true,
        };
        unit.set_anchor_coord(coord);
        unit.attribute_snapshot.set_value("hp_max", 20);
        unit.attribute_snapshot.set_value("mp_max", 20);
        unit.attribute_snapshot.set_value("stamina_max", 10);
        unit.attribute_snapshot.set_value("action_points", 2);
        unit.known_active_skill_ids.Add("query_skill");
        unit.known_skill_level_map["query_skill"] = 1;
        return unit;
    }

    private void AddUnitToState(
        BattleGridService gridService,
        BattleState state,
        BattleUnitState unit,
        bool isEnemy
    )
    {
        state.units[unit.unit_id] = unit;
        if (isEnemy)
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        else
        {
            state.ally_unit_ids.Add(unit.unit_id);
        }
        AssertTrue(gridService.place_unit(state, unit, unit.coord, true), $"测试单位 {unit.unit_id} 应能放入测试战场。");
    }

    private static SkillDef BuildSkill()
    {
        return new SkillDef
        {
            skill_id = "query_skill",
            display_name = "Query Skill",
            combat_profile = new CombatSkillDef
            {
                skill_id = "query_skill",
                range_value = 5,
                target_mode = "ground",
                target_team_filter = "enemy",
                area_pattern = "diamond",
                area_value = 1,
                target_selection_mode = "single_unit",
                ai_tags = new Godot.Collections.Array<StringName> { "setup" },
            },
        };
    }

    private void AssertPublicApiDoesNotExposeGodotCollections(Type type, string label)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (FieldInfo field in type.GetFields(flags))
        {
            AssertTrue(
                !IsGodotDynamicBoundaryType(field.FieldType),
                $"{label}.{field.Name} 不应暴露 Godot Dictionary/Array/Variant。"
            );
        }

        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            AssertTrue(
                !IsGodotDynamicBoundaryType(property.PropertyType),
                $"{label}.{property.Name} 不应暴露 Godot Dictionary/Array/Variant。"
            );
        }

        foreach (MethodInfo method in type.GetMethods(flags))
        {
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertTrue(
                    !IsGodotDynamicBoundaryType(parameter.ParameterType),
                    $"{label}.{method.Name}({parameter.Name}) 不应接收 Godot Dictionary/Array/Variant。"
                );
            }
        }
    }

    private static bool IsGodotDynamicBoundaryType(Type type) =>
        type == typeof(Godot.Collections.Dictionary)
        || type == typeof(Variant)
        || type.FullName == "Godot.Collections.Dictionary"
        || type.FullName == "Godot.Collections.Array";

    private void AssertEq<TValue>(TValue actual, TValue expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private sealed class Fixture
    {
        public BattleState State;
        public BattleGridService GridService;
        public BattleUnitState Actor;
        public BattleUnitState Target;
        public SkillDef Skill;
        public Dictionary<StringName, SkillDef> SkillDefs;
    }
}
