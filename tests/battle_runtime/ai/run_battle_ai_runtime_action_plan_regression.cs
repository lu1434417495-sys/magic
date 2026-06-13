using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_ai_runtime_action_plan_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestPlanIsPlainTypedBoundary();
            TestServiceAndDecisionEngineArePlainTypedBoundary();
            TestPlanFingerprintIgnoresResourcesButTracksSkillsAndBrainShape();
            TestServiceRequiresRuntimePlanByDefault();
            TestServiceUsesExplicitTestFallbackOnlyWhenEnabled();
            TestServiceReportsEmptyRuntimeState();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        Quit(_test.Finish("Battle AI runtime action plan regression"));
    }

    private void TestPlanIsPlainTypedBoundary()
    {
        Type planType = typeof(BattleAiRuntimeActionPlan);
        _test.True(planType.IsSealed, "BattleAiRuntimeActionPlan should be a sealed C# boundary.");
        _test.True(
            planType.GetMethod("set_source") == null
                && planType.GetMethod("add_state_actions") == null
                && planType.GetMethod("get_actions") == null
                && planType.GetMethod("has_state") == null
                && planType.GetMethod("is_stale_for") == null,
            "BattleAiRuntimeActionPlan should not keep GDScript-style public API."
        );
        AssertNoGodotDynamicBoundaryTypes(planType, "BattleAiRuntimeActionPlan");
        AssertNoGodotDynamicBoundaryTypes(
            typeof(BattleAiRuntimeActionPlan.RuntimeActionMetadata),
            "BattleAiRuntimeActionPlan.RuntimeActionMetadata"
        );
        AssertNoGodotDynamicBoundaryTypes(
            typeof(BattleAiRuntimeActionPlan.RuntimeActionExportMetadata),
            "BattleAiRuntimeActionPlan.RuntimeActionExportMetadata"
        );
    }

    private void TestServiceAndDecisionEngineArePlainTypedBoundary()
    {
        Type serviceType = typeof(BattleAiService);
        _test.True(serviceType.IsSealed, "BattleAiService should be a sealed C# service.");
        _test.True(
            serviceType.GetMethod("setup") == null
                && serviceType.GetMethod("set_score_profile") == null
                && serviceType.GetMethod("get_score_profile") == null
                && serviceType.GetMethod("get_score_service") == null
                && serviceType.GetMethod("choose_command") == null
                && serviceType.GetMethod("_choose_command_impl") == null
                && serviceType.GetMethod("_build_wait_decision") == null,
            "BattleAiService should not keep GDScript-style public API."
        );
        AssertPublicApiDoesNotExposeGodotDynamicBoundaryTypes(serviceType, "BattleAiService");

        Type engineType = typeof(BattleAiDecisionEngine);
        _test.True(engineType.IsSealed, "BattleAiDecisionEngine should be a sealed C# helper.");
        _test.True(
            engineType.GetMethod("choose_command_impl") == null
                && engineType.GetMethod("is_better_score_input") == null,
            "BattleAiDecisionEngine should not keep GDScript-style public API."
        );
        AssertPublicApiDoesNotExposeGodotDynamicBoundaryTypes(
            engineType,
            "BattleAiDecisionEngine"
        );

        Type contextType = typeof(BattleAiContext);
        Type contextRuntimeMetadataType = contextType.GetNestedType(
            "RuntimeActionMetadata",
            BindingFlags.NonPublic
        );
        _test.True(
            contextType.GetMethod("push_action_metadata") == null
                && contextType.GetMethod("pop_action_metadata") == null
                && contextType.GetMethod("get_current_action_metadata") == null
                && contextType.GetMethod("merge_current_action_metadata") == null
                && contextType.GetMethod("get_runtime_actions") == null
                && contextType.GetMethod("get_runtime_action_metadata") == null
                && contextType.GetMethod("has_skill_affordance") == null
                && contextType.GetMethod("get_skill_affordance_record") == null
                && contextType.GetMethod("build_skill_score_input") == null
                && contextType.GetMethod("build_action_score_input") == null
                && contextType.GetMethod("_build_command_dict") == null
                && contextType.GetMethod("_normalize_runtime_action_metadata") == null
                && contextType.GetMethod(
                    "ToObjectArray",
                    BindingFlags.Static | BindingFlags.NonPublic
                ) == null
                && contextType.GetMethod(
                    "ToStringArray",
                    BindingFlags.Static | BindingFlags.NonPublic
                ) == null,
            "BattleAiContext should not keep public Godot dictionary metadata/affordance helpers."
        );
        _test.True(
            contextType.GetMethod(
                "PushActionMetadata",
                BindingFlags.Instance | BindingFlags.NonPublic
            )?.GetParameters()[0].ParameterType
                == typeof(BattleAiRuntimeActionPlan.RuntimeActionMetadata)
                && contextType.GetMethod(
                    "ClearMutationGuardViolations",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(void)
                && contextType.GetMethod(
                    "SetMutationGuardViolations",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.GetParameters()[0].ParameterType == typeof(IEnumerable<string>)
                && contextType.GetMethod(
                    "PopActionMetadata",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(void)
                && contextType.GetMethod(
                    "MergeCurrentActionMetadataTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(Dictionary<string, object>)
                && contextType.GetMethod(
                    "GetRuntimeActionMetadataTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(BattleAiRuntimeActionPlan.RuntimeActionMetadata)
                && contextType.GetMethod(
                    "GetSkillAffordanceRecordTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(BattleAiSkillAffordanceRecord)
                && contextType.GetProperty("action_traces", BindingFlags.Instance | BindingFlags.Public) == null
                && contextType.GetProperty("mutation_guard_violations", BindingFlags.Instance | BindingFlags.Public) == null
                && contextType.GetMethod(
                    "GetActionTracesTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(IReadOnlyList<AiActionTrace>)
                && contextType.GetMethod(
                    "GetMutationGuardViolationsTyped",
                    BindingFlags.Instance | BindingFlags.NonPublic
                )?.ReturnType == typeof(IReadOnlyList<string>)
                && contextRuntimeMetadataType?.GetMethod("FromDictionary") == null
                && contextRuntimeMetadataType?.GetMethod(
                    "FromTraceDictionary",
                    BindingFlags.Public | BindingFlags.Static
                ) != null,
            "BattleAiContext metadata/affordance state should stay on internal typed helpers."
        );
    }

    private void TestPlanFingerprintIgnoresResourcesButTracksSkillsAndBrainShape()
    {
        EnemyAiBrainDef brain = BuildBrain();
        BattleUnitState unit = BuildUnit("actor", "plan_brain", "engage");
        unit.known_active_skill_ids.Add("bolt");
        unit.known_skill_level_map = new Godot.Collections.Dictionary { ["bolt"] = 1 };
        unit.current_ap = 1;

        var plan = new BattleAiRuntimeActionPlan();
        plan.SetSource(unit, brain);
        _test.True(!plan.IsStaleFor(unit, brain), "Same unit/brain/skill signature should not be stale.");

        unit.current_ap = 0;
        _test.True(!plan.IsStaleFor(unit, brain), "Turn resources should not affect plan staleness.");

        unit.known_skill_level_map["bolt"] = 2;
        _test.True(plan.IsStaleFor(unit, brain), "Skill level changes should make the plan stale.");

        unit.known_skill_level_map["bolt"] = 1;
        var extraState = new EnemyAiStateDef
        {
            state_id = "support",
            actions = new Godot.Collections.Array<EnemyAiAction> { Wait("support_wait") },
        };
        brain.states.Add(extraState);
        _test.True(plan.IsStaleFor(unit, brain), "Brain state/action shape changes should make the plan stale.");

        var transitionPlan = new BattleAiRuntimeActionPlan();
        transitionPlan.SetSource(unit, brain);
        brain.transition_rules = new Godot.Collections.Array<EnemyAiTransitionRuleDef>
        {
            Rule(
                "support_when_low",
                10,
                "support",
                new[] { Condition("self_hp_at_or_below_basis_points", basisPoints: 5000) }
            ),
        };
        _test.True(
            transitionPlan.IsStaleFor(unit, brain),
            "Brain transition rule shape changes should make the plan stale."
        );
    }

    private void TestServiceRequiresRuntimePlanByDefault()
    {
        Fixture fixture = BuildServiceFixture(false, null);
        BattleAiDecision decision = fixture.Service.ChooseCommand(fixture.Context);
        _test.True(decision != null, "Missing runtime plan should still return a wait decision.");
        _test.Eq(
            decision.action_id,
            new StringName("wait_missing_runtime_plan"),
            "Default path should not fall back to authored actions."
        );
    }

    private void TestServiceUsesExplicitTestFallbackOnlyWhenEnabled()
    {
        Fixture fixture = BuildServiceFixture(true, null);
        BattleAiDecision decision = fixture.Service.ChooseCommand(fixture.Context);
        _test.True(decision != null, "Explicit test fallback should return an authored decision.");
        _test.Eq(
            decision.action_id,
            new StringName("authored_wait"),
            "Authored fallback should require allow_authored_action_fallback_for_tests=true."
        );
    }

    private void TestServiceReportsEmptyRuntimeState()
    {
        var plan = new BattleAiRuntimeActionPlan();
        Fixture fixture = BuildServiceFixture(false, plan);
        plan.SetSource(fixture.Actor, fixture.Brain);
        plan.AddStateActions("engage", Array.Empty<EnemyAiAction>());

        BattleAiDecision decision = fixture.Service.ChooseCommand(fixture.Context);
        _test.True(decision != null, "Empty runtime state should return a wait decision.");
        _test.Eq(
            decision.action_id,
            new StringName("wait_empty_runtime_state"),
            "Empty runtime state should use the dedicated wait reason."
        );
    }

    private static Fixture BuildServiceFixture(
        bool enableTestFallback,
        BattleAiRuntimeActionPlan plan
    )
    {
        BattleState state = BuildState();
        var gridService = new BattleGridService();
        BattleUnitState actor = BuildUnit("actor", "plan_brain", "engage");
        BattleUnitState hero = BuildUnit("hero", "", "");
        hero.control_mode = "manual";
        actor.faction_id = "hostile";
        hero.faction_id = "player";
        actor.SetAnchorCoord(new Vector2I(1, 1));
        hero.SetAnchorCoord(new Vector2I(3, 1));
        AddUnit(gridService, state, actor, true);
        AddUnit(gridService, state, hero, false);
        state.phase = "unit_acting";
        state.active_unit_id = actor.unit_id;

        EnemyAiBrainDef brain = BuildBrain();
        var service = new BattleAiService
        {
            EnableMutationGuard = false,
        };
        service.Setup(new Dictionary<StringName, EnemyAiBrainDef> { [brain.brain_id] = brain }, null);

        var context = new BattleAiContext
        {
            state = state,
            unit_state = actor,
            grid_service = gridService,
            runtime_action_plan = plan,
            allow_authored_action_fallback_for_tests = enableTestFallback,
        };
        context.SetSkillDefs(new Dictionary<StringName, SkillDef>());

        return new Fixture
        {
            State = state,
            GridService = gridService,
            Actor = actor,
            Brain = brain,
            Service = service,
            Context = context,
        };
    }

    private static EnemyAiBrainDef BuildBrain()
    {
        var state = new EnemyAiStateDef
        {
            state_id = "engage",
            actions = new Godot.Collections.Array<EnemyAiAction> { Wait("authored_wait") },
        };
        return new EnemyAiBrainDef
        {
            brain_id = "plan_brain",
            default_state_id = "engage",
            states = new Godot.Collections.Array<EnemyAiStateDef> { state },
        };
    }

    private static BattleState BuildState()
    {
        var state = new BattleState
        {
            map_size = new Vector2I(6, 4),
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < state.map_size.Y; y++)
        {
            for (int x = 0; x < state.map_size.X; x++)
            {
                var cell = new BattleCellState { coord = new Vector2I(x, y) };
                state.cells[cell.coord] = cell;
            }
        }
        return state;
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName brainId, StringName stateId)
    {
        return new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            ai_brain_id = brainId,
            ai_state_id = stateId,
            control_mode = "ai",
            current_hp = 20,
            current_ap = 2,
            current_mp = 2,
            current_stamina = 2,
        };
    }

    private static void AddUnit(
        BattleGridService gridService,
        BattleState state,
        BattleUnitState unit,
        bool isEnemy
    )
    {
        gridService.PlaceUnit(state, unit, unit.coord, true);
        state.units[unit.unit_id] = unit;
        if (isEnemy)
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        else
        {
            state.ally_unit_ids.Add(unit.unit_id);
        }
    }

    private static WaitAction Wait(StringName actionId)
    {
        return new WaitAction { action_id = actionId };
    }

    private static EnemyAiTransitionRuleDef Rule(
        StringName ruleId,
        int order,
        StringName targetStateId,
        IEnumerable<EnemyAiTransitionConditionDef> conditions
    )
    {
        var rule = new EnemyAiTransitionRuleDef
        {
            rule_id = ruleId,
            order = order,
            target_state_id = targetStateId,
        };
        foreach (EnemyAiTransitionConditionDef condition in conditions)
        {
            rule.conditions.Add(condition);
        }
        return rule;
    }

    private static EnemyAiTransitionConditionDef Condition(
        StringName predicate,
        int basisPoints = -1,
        int maxDistance = -1
    )
    {
        return new EnemyAiTransitionConditionDef
        {
            predicate = predicate,
            basis_points = basisPoints,
            max_distance = maxDistance,
        };
    }

    private void AssertNoGodotDynamicBoundaryTypes(Type type, string label)
    {
        const BindingFlags Flags =
            BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly;
        foreach (MethodInfo method in type.GetMethods(Flags))
        {
            if (method.IsSpecialName)
            {
                continue;
            }
            _test.True(
                !IsGodotDynamicBoundaryType(method.ReturnType),
                $"{label}.{method.Name} should not return Godot dynamic boundary type {method.ReturnType}."
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                _test.True(
                    !IsGodotDynamicBoundaryType(parameter.ParameterType),
                    $"{label}.{method.Name} should not accept Godot dynamic boundary type {parameter.ParameterType}."
                );
            }
        }
        foreach (FieldInfo field in type.GetFields(Flags))
        {
            _test.True(
                !IsGodotDynamicBoundaryType(field.FieldType),
                $"{label}.{field.Name} should not store Godot dynamic boundary type {field.FieldType}."
            );
        }
        foreach (PropertyInfo property in type.GetProperties(Flags))
        {
            _test.True(
                !IsGodotDynamicBoundaryType(property.PropertyType),
                $"{label}.{property.Name} should not expose Godot dynamic boundary type {property.PropertyType}."
            );
        }
    }

    private void AssertPublicApiDoesNotExposeGodotDynamicBoundaryTypes(Type type, string label)
    {
        const BindingFlags Flags =
            BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.DeclaredOnly;
        foreach (MethodInfo method in type.GetMethods(Flags))
        {
            if (method.IsSpecialName)
            {
                continue;
            }
            _test.True(
                !IsGodotDynamicBoundaryType(method.ReturnType),
                $"{label}.{method.Name} should not return Godot dynamic boundary type {method.ReturnType}."
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                _test.True(
                    !IsGodotDynamicBoundaryType(parameter.ParameterType),
                    $"{label}.{method.Name} should not accept Godot dynamic boundary type {parameter.ParameterType}."
                );
            }
        }
        foreach (FieldInfo field in type.GetFields(Flags))
        {
            _test.True(
                !IsGodotDynamicBoundaryType(field.FieldType),
                $"{label}.{field.Name} should not expose Godot dynamic boundary type {field.FieldType}."
            );
        }
        foreach (PropertyInfo property in type.GetProperties(Flags))
        {
            _test.True(
                !IsGodotDynamicBoundaryType(property.PropertyType),
                $"{label}.{property.Name} should not expose Godot dynamic boundary type {property.PropertyType}."
            );
        }
    }

    private static bool IsGodotDynamicBoundaryType(Type type)
    {
        if (type == typeof(Variant) || type == typeof(GodotObject))
        {
            return true;
        }
        if (typeof(Godot.Collections.Dictionary).IsAssignableFrom(type))
        {
            return true;
        }
        if (typeof(Godot.Collections.Array).IsAssignableFrom(type))
        {
            return true;
        }
        return false;
    }

    private sealed class Fixture
    {
        public BattleState State;
        public BattleGridService GridService;
        public BattleUnitState Actor;
        public EnemyAiBrainDef Brain;
        public BattleAiService Service;
        public BattleAiContext Context;
    }
}
