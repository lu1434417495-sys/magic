using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_ai_score_context_adapter_regression : SceneTree
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
            TestScoreServiceIsPlainInternalScoreAssemblyHelper();
            TestAdapterIsPlainInternalTypedHelper();
            TestSkillScoreInputUsesTypedIndexAndStripsSkillResource();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        if (_failures.Count == 0)
        {
            GD.Print("Battle AI score context adapter regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle AI score context adapter regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestAdapterIsPlainInternalTypedHelper()
    {
        Type adapterType = typeof(BattleAiScoreContextAdapter);
        AssertTrue(adapterType.IsNotPublic, "BattleAiScoreContextAdapter 应保持 internal。");
        AssertTrue(adapterType.IsSealed, "BattleAiScoreContextAdapter 应是 sealed helper。");
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(adapterType),
            "BattleAiScoreContextAdapter 不应继承 GodotObject/RefCounted。"
        );
        AssertTrue(
            adapterType.GetCustomAttribute<GlobalClassAttribute>() == null,
            "BattleAiScoreContextAdapter 不应注册 GlobalClass。"
        );
        AssertTrue(
            adapterType.GetMethod("setup") == null
                && adapterType.GetMethod("build_action_score_input") == null
                && adapterType.GetMethod("build_skill_score_input") == null,
            "BattleAiScoreContextAdapter 不应保留 GDScript-style snake_case public API。"
        );
        AssertTrue(
            adapterType.GetProperty("skill_defs") == null,
            "BattleAiScoreContextAdapter 不应暴露 public skill_defs Godot Dictionary。"
        );
        AssertPublicApiDoesNotExposeGodotCollections(adapterType, "BattleAiScoreContextAdapter");
    }

    private void TestScoreServiceIsPlainInternalScoreAssemblyHelper()
    {
        Type serviceType = typeof(BattleAiScoreService);
        AssertTrue(serviceType.IsSealed, "BattleAiScoreService 应是 sealed C# helper。");
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(serviceType),
            "BattleAiScoreService 不应继承 GodotObject/RefCounted。"
        );
        AssertTrue(
            serviceType.GetCustomAttribute<GlobalClassAttribute>() == null,
            "BattleAiScoreService 不应注册 GlobalClass。"
        );
        AssertTrue(
            serviceType.GetMethod("setup") == null
                && serviceType.GetMethod("set_profile") == null
                && serviceType.GetMethod("get_profile") == null
                && serviceType.GetMethod("get_bucket_priority") == null
                && serviceType.GetMethod("build_action_score_input") == null
                && serviceType.GetMethod("build_skill_score_input") == null
                && serviceType.GetMethod("_resolve_estimated_hit_rate_percent") == null,
            "BattleAiScoreService 不应保留 GDScript-style public API。"
        );
        AssertPublicApiDoesNotExposeGodotCollections(serviceType, "BattleAiScoreService");
    }

    private void TestSkillScoreInputUsesTypedIndexAndStripsSkillResource()
    {
        Fixture fixture = BuildFixture();
        var adapter = new BattleAiScoreContextAdapter();
        adapter.Setup(
            new BattleAiScoreService(),
            fixture.State,
            fixture.Actor,
            fixture.GridService,
            fixture.SkillDefs
        );

        IBattleAiScoreContext scoreContext = adapter;
        AssertTrue(
            scoreContext.skill_defs.Count == 1,
            "IBattleAiScoreContext 投影应保留 score service 当前所需 skill_defs 边界。"
        );

        BattleCommand command = new()
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = fixture.Actor.unit_id,
            skill_id = fixture.Skill.skill_id,
            target_coord = new Vector2I(2, 1),
        };
        BattlePreview preview = new()
        {
            allowed = true,
            resolved_anchor_coord = fixture.Actor.coord,
        };
        preview.target_coords.Add(command.target_coord);

        BattleAiScoreInput scoreInput = adapter.BuildSkillScoreInput(
            null,
            fixture.Skill.skill_id,
            command,
            preview,
            new Godot.Collections.Array(),
            new Godot.Collections.Dictionary
            {
                ["runtime_action_metadata"] = new Godot.Collections.Dictionary
                {
                    ["generated"] = true,
                    ["action_id"] = "adapter_regression",
                },
            }
        );

        AssertTrue(scoreInput != null, "有效 skill command 应生成 BattleAiScoreInput。");
        if (scoreInput == null)
        {
            return;
        }

        AssertTrue(scoreInput.skill_def == null, "score input 离开适配器前必须移除 SkillDef live resource。");
        AssertEq(scoreInput.command, command, "score input 应保留 command value object。");
        AssertEq(scoreInput.preview, preview, "score input 应保留 preview value object。");
        AssertEq(scoreInput.action_kind, new StringName("skill"), "默认 action_kind 应为 skill。");
        AssertEq(scoreInput.ap_cost, 2, "适配器应通过 typed skill index 解析 SkillDef 并计算 AP cost。");
        AssertEq(scoreInput.mp_cost, 3, "适配器应通过 typed skill index 解析 SkillDef 并计算 MP cost。");
        AssertEq(
            DictStringName(scoreInput.runtime_action_metadata, "skill_id"),
            fixture.Skill.skill_id,
            "runtime_action_metadata 应带上 skill_id，替代 live skill_def。"
        );
        AssertTrue(
            BattleAiPayloadGuard.ScoreInputHasNoLiveState(scoreInput),
            "score input 应满足 AI payload guard 的 no-live-state 合约。"
        );
    }

    private Fixture BuildFixture()
    {
        BattleState state = BuildFlatState(new Vector2I(4, 3));
        var gridService = new BattleGridService();
        BattleUnitState actor = BuildUnit("adapter_actor", "AI", "enemy", new Vector2I(1, 1));
        state.units[actor.unit_id] = actor;
        state.enemy_unit_ids.Add(actor.unit_id);
        bool placed = gridService.place_unit(state, actor, actor.coord, true);
        AssertTrue(placed, "测试单位应能放入测试战场。");

        SkillDef skill = BuildSkill();
        Godot.Collections.Dictionary skillDefs = new() { [skill.skill_id] = skill };

        return new Fixture
        {
            State = state,
            GridService = gridService,
            Actor = actor,
            Skill = skill,
            SkillDefs = skillDefs,
        };
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "ai_score_context_adapter_regression",
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
            control_mode = "ai",
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
        unit.known_active_skill_ids.Add("adapter_skill");
        unit.known_skill_level_map["adapter_skill"] = 1;
        return unit;
    }

    private static SkillDef BuildSkill()
    {
        return new SkillDef
        {
            skill_id = "adapter_skill",
            display_name = "Adapter Skill",
            combat_profile = new CombatSkillDef
            {
                skill_id = "adapter_skill",
                ap_cost = 2,
                mp_cost = 3,
                stamina_cost = 0,
                cooldown_tu = 0,
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
        || type == typeof(Godot.Collections.Array)
        || type == typeof(Variant)
        || type.FullName == "Godot.Collections.Dictionary"
        || type.FullName == "Godot.Collections.Array";

    private static StringName DictStringName(Godot.Collections.Dictionary source, string key)
    {
        if (source == null)
        {
            return "";
        }
        StringName exactKey = new(key);
        return source.ContainsKey(exactKey)
            ? ProgressionDataUtils.to_string_name(source[exactKey])
            : new StringName("");
    }

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
        public SkillDef Skill;
        internal Godot.Collections.Dictionary SkillDefs;
    }
}
