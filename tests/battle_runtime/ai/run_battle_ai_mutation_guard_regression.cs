using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

public partial class run_battle_ai_mutation_guard_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        try
        {
            TestDecisionCommitterIsPlainTypedHelper();
            TestBattleAiDecisionIsPlainTypedDto();
            TestTurnTraceProjectsTypedDecisionTransition();
            TestBenignAiBookkeepingIsAllowed();
            TestActiveUnitHpMutationIsBlockedAndRestored();
            TestOtherUnitCoordMutationIsBlockedAndRestored();
            TestUnknownBlackboardKeyWriteIsIgnored();
            TestCellOccupantMutationIsBlockedAndRestored();
            TestCellHeightMutationIsBlockedAndRestored();
            TestMissingBrainWaitPathIsAllowed();
            TestMissingStateWaitPathIsAllowed();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        if (_failures.Count == 0)
        {
            GD.Print("Battle AI mutation guard regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle AI mutation guard regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestDecisionCommitterIsPlainTypedHelper()
    {
        Type committerType = typeof(BattleAiDecisionCommitter);
        AssertTrue(
            committerType.IsAbstract && committerType.IsSealed,
            "BattleAiDecisionCommitter 应是 plain static C# helper。"
        );
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(committerType),
            "BattleAiDecisionCommitter 不应继承 GodotObject/RefCounted。"
        );
        AssertTrue(
            committerType.GetCustomAttribute<GlobalClassAttribute>() == null,
            "BattleAiDecisionCommitter 不应注册 GlobalClass。"
        );
        AssertTrue(
            committerType.GetMethod("commit") == null
                && committerType.GetMethod("build_state_patch") == null
                && committerType.GetMethod("validate_state_patch") == null,
            "BattleAiDecisionCommitter 不应保留 GDScript-style snake_case API。"
        );
        AssertPublicApiDoesNotExposeGodotCollections(committerType, "BattleAiDecisionCommitter");
    }

    private void TestBattleAiDecisionIsPlainTypedDto()
    {
        Type decisionType = typeof(BattleAiDecision);
        AssertTrue(decisionType.IsSealed, "BattleAiDecision 应是 sealed plain C# DTO。");
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(decisionType),
            "BattleAiDecision 不应继承 GodotObject/RefCounted。"
        );
        AssertTrue(
            decisionType.GetCustomAttribute<GlobalClassAttribute>() == null,
            "BattleAiDecision 不应注册 GlobalClass。"
        );
        AssertTrue(
            decisionType.GetProperty("transition") == null
                && decisionType.GetProperty("trace_counters") == null
                && decisionType.GetProperty("state_patch") == null
                && decisionType.GetProperty("TypedTransition") == null
                && decisionType.GetProperty("TypedStatePatch") == null,
            "BattleAiDecision 不应保留 Godot Dictionary mirror 或旧 typed 别名字段。"
        );
        AssertPublicApiDoesNotExposeGodotCollections(decisionType, "BattleAiDecision");
    }

    private void TestTurnTraceProjectsTypedDecisionTransition()
    {
        var context = new BattleAiContext
        {
            state = new BattleState { battle_id = "decision_transition_projection" },
            unit_state = new BattleUnitState
            {
                unit_id = "actor",
                display_name = "Actor",
                faction_id = "hostile",
                ai_brain_id = "brain",
                ai_state_id = "idle",
            },
        };
        var condition = new EnemyAiTransitionConditionDef
        {
            predicate = "always",
            state_ids = new Godot.Collections.Array<StringName> { "idle" },
        };
        var transition = new BattleAiStateResolver.TransitionResult(
            "idle",
            "engage",
            "enter_engage",
            "matched",
            new List<BattleAiStateResolver.TransitionConditionTrace>
            {
                BattleAiStateResolver.TransitionConditionTrace.FromCondition(condition),
            }
        );
        var command = new BattleCommand
        {
            command_type = BattleCommand.TYPE_WAIT(),
            unit_id = "actor",
        };
        var decision = new BattleAiDecision
        {
            command = command,
            action_id = "wait",
            reason_text = "transition projected",
            Transition = transition,
        };

        GDictionary turnTrace = context.build_turn_trace(decision);
        GDictionary transitionPayload = turnTrace["transition"].AsGodotDictionary();
        AssertEq(
            ProgressionDataUtils.to_string_name(transitionPayload["previous_state_id"]),
            new StringName("idle"),
            "turn trace 应从 typed transition 投影 previous_state_id。"
        );
        AssertEq(
            ProgressionDataUtils.to_string_name(transitionPayload["state_id"]),
            new StringName("engage"),
            "turn trace 应从 typed transition 投影 state_id。"
        );
        AssertEq(
            ProgressionDataUtils.to_string_name(transitionPayload["rule_id"]),
            new StringName("enter_engage"),
            "turn trace 应从 typed transition 投影 rule_id。"
        );
        AssertEq(
            transitionPayload["matched_conditions"].AsGodotArray().Count,
            1,
            "turn trace 应投影 matched condition trace。"
        );
    }

    private void TestBenignAiBookkeepingIsAllowed()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("none"));
        BattleAiDecision decision = fixture.Service.ChooseCommand(fixture.Context);

        AssertNoGuardViolation(fixture.Context, "普通 wait 决策不应触发 mutation guard。");
        AssertTrue(
            decision != null && decision.action_id == new StringName("test_mutation_none"),
            "普通 action 应正常返回原 decision。"
        );
        BattleAiDecisionCommitter.Commit(fixture.Actor, decision);
        AssertEq(
            fixture.Actor.ai_blackboard.last_action_id,
            new StringName("test_mutation_none"),
            "合法 decision bookkeeping 应保留。"
        );
        AssertEq(
            fixture.Actor.ai_blackboard.turn_decision_count,
            1,
            "合法 decision commit 应递增 turn_decision_count。"
        );
        AssertTrue(
            fixture.Actor.ai_blackboard.has("turn_decision_count"),
            "合法 decision commit 应同步 turn_decision_count 的 typed presence 标记。"
        );
    }

    private void TestActiveUnitHpMutationIsBlockedAndRestored()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("active_hp"));
        int beforeHp = fixture.Actor.current_hp;

        BattleAiDecision decision = fixture.Service.ChooseCommand(fixture.Context);

        AssertGuardBlocked(fixture.Context, decision, "active unit HP mutation 应触发 guard。");
        AssertEq(fixture.Actor.current_hp, beforeHp, "active unit HP mutation 应被恢复。");
    }

    private void TestOtherUnitCoordMutationIsBlockedAndRestored()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("other_coord"));
        Vector2I beforeCoord = fixture.Hero.coord;
        GVector2IArray beforeOccupied = DuplicateVector2IArray(fixture.Hero.occupied_coords);

        BattleAiDecision decision = fixture.Service.ChooseCommand(fixture.Context);

        AssertGuardBlocked(fixture.Context, decision, "其他单位坐标 mutation 应触发 guard。");
        AssertEq(fixture.Hero.coord, beforeCoord, "其他单位坐标 mutation 应被恢复。");
        AssertVector2IArrayEq(
            fixture.Hero.occupied_coords,
            beforeOccupied,
            "其他单位 footprint cache mutation 应被恢复。"
        );
    }

    private void TestUnknownBlackboardKeyWriteIsIgnored()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("blackboard"));

        BattleAiDecision decision = fixture.Service.ChooseCommand(fixture.Context);

        AssertNoGuardViolation(fixture.Context, "未知 blackboard key 写入应被 typed blackboard 忽略。");
        AssertTrue(
            decision != null && decision.action_id == new StringName("test_mutation_blackboard"),
            "未知 blackboard key 不应阻断 action。"
        );
        AssertTrue(
            !fixture.Actor.ai_blackboard.has("rogue_key"),
            "未知 blackboard key 不应落入运行时状态。"
        );
    }

    private void TestCellOccupantMutationIsBlockedAndRestored()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("cell_occupant"));
        BattleCellState cell = fixture.GridService.get_cell(fixture.State, new Vector2I(3, 1));
        StringName beforeOccupant = cell?.occupant_unit_id ?? "";

        BattleAiDecision decision = fixture.Service.ChooseCommand(fixture.Context);

        AssertGuardBlocked(fixture.Context, decision, "cell occupant mutation 应触发 guard。");
        cell = fixture.GridService.get_cell(fixture.State, new Vector2I(3, 1));
        AssertEq(cell?.occupant_unit_id ?? "", beforeOccupant, "cell occupant mutation 应被恢复。");
    }

    private void TestCellHeightMutationIsBlockedAndRestored()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("cell_height"));
        BattleCellState cell = fixture.GridService.get_cell(fixture.State, new Vector2I(0, 0));
        int beforeHeight = cell?.current_height ?? int.MinValue;
        int beforeOffset = cell?.height_offset ?? int.MinValue;

        BattleAiDecision decision = fixture.Service.ChooseCommand(fixture.Context);

        AssertGuardBlocked(fixture.Context, decision, "cell height mutation 应触发 guard。");
        cell = fixture.GridService.get_cell(fixture.State, new Vector2I(0, 0));
        AssertEq(cell?.current_height ?? int.MinValue, beforeHeight, "cell current_height mutation 应被恢复。");
        AssertEq(cell?.height_offset ?? int.MinValue, beforeOffset, "cell height_offset mutation 应被恢复。");
    }

    private void TestMissingBrainWaitPathIsAllowed()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("none"), includeBrain: false);
        BattleAiDecision decision = fixture.Service.ChooseCommand(fixture.Context);

        AssertNoGuardViolation(fixture.Context, "missing brain fallback 不应触发 mutation guard。");
        AssertTrue(
            decision != null && decision.action_id == new StringName("wait_missing_brain"),
            "missing brain 应正常回落到 wait。"
        );
    }

    private void TestMissingStateWaitPathIsAllowed()
    {
        Fixture fixture = BuildFixture(MakeMutationAction("none"), includeBrain: true, includeState: false);
        BattleAiDecision decision = fixture.Service.ChooseCommand(fixture.Context);

        AssertNoGuardViolation(fixture.Context, "missing state fallback 不应触发 mutation guard。");
        AssertTrue(
            decision != null && decision.action_id == new StringName("wait_missing_state"),
            "missing state 应正常回落到 wait。"
        );
    }

    private static BattleAiMutationGuardTestAction MakeMutationAction(StringName kind)
    {
        var action = new BattleAiMutationGuardTestAction();
        action.setup(kind);
        return action;
    }

    private Fixture BuildFixture(
        EnemyAiAction action,
        bool includeBrain = true,
        bool includeState = true
    )
    {
        BattleState state = BuildFlatState(new Vector2I(6, 4));
        var gridService = new BattleGridService();
        BattleUnitState actor = BuildUnit(
            "guard_actor",
            "守卫",
            "hostile",
            new Vector2I(1, 1),
            "guard_brain",
            "engage",
            20,
            2
        );
        BattleUnitState hero = BuildUnit(
            "hero",
            "玩家",
            "player",
            new Vector2I(3, 1),
            "",
            "",
            30,
            2
        );
        AddUnitToState(gridService, state, actor, isEnemy: true);
        AddUnitToState(gridService, state, hero, isEnemy: false);
        state.phase = "unit_acting";
        state.active_unit_id = actor.unit_id;

        Dictionary<StringName, EnemyAiBrainDef> brainMap = new();
        if (includeBrain)
        {
            var brain = new EnemyAiBrainDef
            {
                brain_id = "guard_brain",
                default_state_id = "engage",
                states = new Godot.Collections.Array<EnemyAiStateDef>(),
            };
            if (includeState)
            {
                brain.states.Add(
                    new EnemyAiStateDef
                    {
                        state_id = "engage",
                        actions = new Godot.Collections.Array<EnemyAiAction> { action },
                    }
                );
            }
            brainMap[brain.brain_id] = brain;
        }

        var service = new BattleAiService();
        service.Setup(brainMap, null);
        var context = new BattleAiContext
        {
            state = state,
            unit_state = actor,
            grid_service = gridService,
            skill_defs = new GDictionary(),
            allow_authored_action_fallback_for_tests = true,
        };

        return new Fixture
        {
            State = state,
            GridService = gridService,
            Actor = actor,
            Hero = hero,
            Service = service,
            Context = context,
        };
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "ai_mutation_guard_regression",
            phase = "timeline_running",
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
        Vector2I coord,
        StringName brainId,
        StringName stateId,
        int currentHp,
        int currentAp
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = brainId != "" ? new StringName("ai") : new StringName("manual"),
            ai_brain_id = brainId,
            ai_state_id = stateId,
            current_hp = currentHp,
            current_mp = 20,
            current_stamina = 10,
            current_ap = currentAp,
            current_move_points = 2,
            is_alive = true,
        };
        unit.set_anchor_coord(coord);
        unit.attribute_snapshot.set_value("hp_max", Math.Max(currentHp, 1));
        unit.attribute_snapshot.set_value("mp_max", 20);
        unit.attribute_snapshot.set_value("stamina_max", 10);
        unit.attribute_snapshot.set_value("action_points", Math.Max(currentAp, 1));
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

        bool placed = gridService.place_unit(state, unit, unit.coord, true);
        AssertTrue(placed, $"测试单位 {unit.unit_id} 应能放入测试战场。");
    }

    private void AssertGuardBlocked(BattleAiContext context, BattleAiDecision decision, string message)
    {
        GStringArray violations = GetGuardViolations(context);
        AssertTrue(violations.Count > 0, $"{message} violations={FormatStringArray(violations)}");
        AssertTrue(decision == null, $"{message} guard 应 fail-loud 并阻断原 decision。");
    }

    private void AssertNoGuardViolation(BattleAiContext context, string message)
    {
        GStringArray violations = GetGuardViolations(context);
        AssertTrue(violations.Count == 0, $"{message} violations={FormatStringArray(violations)}");
    }

    private static GStringArray GetGuardViolations(BattleAiContext context)
    {
        var result = new GStringArray();
        if (context?.mutation_guard_violations == null)
        {
            return result;
        }
        foreach (Variant value in context.mutation_guard_violations)
        {
            result.Add(value.ToString());
        }
        return result;
    }

    private static GVector2IArray DuplicateVector2IArray(GVector2IArray source)
    {
        var result = new GVector2IArray();
        foreach (Vector2I value in source ?? new GVector2IArray())
        {
            result.Add(value);
        }
        return result;
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
        type == typeof(GDictionary)
        || type == typeof(Variant)
        || type.FullName == "Godot.Collections.Dictionary"
        || type.FullName == "Godot.Collections.Array";

    private void AssertVector2IArrayEq(
        GVector2IArray actual,
        GVector2IArray expected,
        string message
    )
    {
        if ((actual?.Count ?? 0) != (expected?.Count ?? 0))
        {
            _failures.Add(
                $"{message} expected={FormatVector2IArray(expected)} actual={FormatVector2IArray(actual)}"
            );
            return;
        }
        for (int index = 0; index < actual.Count; index++)
        {
            if (actual[index] != expected[index])
            {
                _failures.Add(
                    $"{message} expected={FormatVector2IArray(expected)} actual={FormatVector2IArray(actual)}"
                );
                return;
            }
        }
    }

    private static string FormatStringArray(GStringArray values) =>
        "[" + string.Join(", ", values ?? new GStringArray()) + "]";

    private static string FormatVector2IArray(GVector2IArray values) =>
        "[" + string.Join(", ", values ?? new GVector2IArray()) + "]";

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq(int actual, int expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertEq(StringName actual, StringName expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertEq(Vector2I actual, Vector2I expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private sealed class Fixture
    {
        public BattleState State;
        public BattleGridService GridService;
        public BattleUnitState Actor;
        public BattleUnitState Hero;
        public BattleAiService Service;
        public BattleAiContext Context;
    }
}
