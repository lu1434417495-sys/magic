using System;
using System.Collections;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_runtime_action_plan_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestPlanFingerprintIgnoresResourcesButTracksSkillsAndBrainShape();
            TestClearReopensNativeGenerationAndDisposeReturnsAuditBaseline();
            TestAssemblerExceptionDisposesPartialPlanAndBalancesTrace();
            TestServiceRequiresRuntimePlanByDefault();
            TestServiceUsesExplicitTestFallbackOnlyWhenEnabled();
            TestServiceReportsEmptyRuntimeState();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle AI runtime action plan regression"));
    }

    private void TestAssemblerExceptionDisposesPartialPlanAndBalancesTrace()
    {
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        EnemyAiBrainDef brain = BuildBrain();
        BattleUnitState unit = BuildUnit("throwing_actor", brain.brain_id, "engage");
        unit.known_active_skill_ids.Add("bolt");
        var recorder = new AiTraceRecorder();
        AiTraceRecorder.SetInstance(recorder);
        BattleAiRuntimeActionPlan unexpectedPlan = null;
        bool threw = false;
        try
        {
            unexpectedPlan = new BattleAiActionAssembler().BuildUnitActionPlan(
                unit,
                brain,
                new ThrowingSkillDictionary()
            );
        }
        catch (InvalidOperationException exception)
        {
            threw = exception.Message.Contains(
                "action plan classification probe",
                StringComparison.Ordinal
            );
        }
        finally
        {
            AiTraceRecorder.SetInstance(null);
            unexpectedPlan?.Dispose();
        }

        _test.True(threw, "assembler should surface the classification failure.");
        _test.True(recorder.AssertBalanced(), "assembler failure should close its trace span.");
        LifecycleAuditSnapshot actual = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(actual.ActiveOwnerCount, baseline.ActiveOwnerCount, "assembler owner baseline");
        _test.Eq(actual.ActiveLeaseCount, baseline.ActiveLeaseCount, "assembler lease baseline");
        _test.Eq(actual.ActiveScopeCount, baseline.ActiveScopeCount, "assembler scope baseline");
    }

    private void TestClearReopensNativeGenerationAndDisposeReturnsAuditBaseline()
    {
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        var plan = new BattleAiRuntimeActionPlan();
        WaitAction first = (WaitAction)plan.OwnRuntimeAction(
            new WaitAction { action_id = "first_generation" },
            "first_generation"
        );

        plan.Clear();
        _test.True(
            !GodotObject.IsInstanceValid(first),
            "Clear should dispose the previous runtime-action generation."
        );

        WaitAction second = (WaitAction)plan.OwnRuntimeAction(
            new WaitAction { action_id = "second_generation" },
            "second_generation"
        );
        plan.Dispose();
        _test.True(
            !GodotObject.IsInstanceValid(second),
            "Dispose should release the current runtime-action generation."
        );

        LifecycleAuditSnapshot actual = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(actual.ActiveOwnerCount, baseline.ActiveOwnerCount, "action-plan owner baseline");
        _test.Eq(actual.ActiveLeaseCount, baseline.ActiveLeaseCount, "action-plan lease baseline");
        _test.Eq(actual.ActiveScopeCount, baseline.ActiveScopeCount, "action-plan scope baseline");
    }

    private void TestPlanFingerprintIgnoresResourcesButTracksSkillsAndBrainShape()
    {
        EnemyAiBrainDef brain = BuildBrain();
        BattleUnitState unit = BuildUnit("actor", "plan_brain", "engage");
        unit.known_active_skill_ids.Add("bolt");
        unit.SetKnownSkillLevelsTyped(new Dictionary<StringName, int> { ["bolt"] = 1 });
        unit.current_ap = 1;

        using var plan = new BattleAiRuntimeActionPlan();
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

        using var transitionPlan = new BattleAiRuntimeActionPlan();
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
        using Fixture fixture = BuildServiceFixture(false, null);
        BattleAiDecision decision = fixture.Service
            .ChooseCommand(fixture.Context, captureTrace: false)
            ?.Decision;
        _test.True(decision != null, "Missing runtime plan should still return a wait decision.");
        _test.Eq(
            decision.action_id,
            new StringName("wait_missing_runtime_plan"),
            "Default path should not fall back to authored actions."
        );
    }

    private void TestServiceUsesExplicitTestFallbackOnlyWhenEnabled()
    {
        using Fixture fixture = BuildServiceFixture(true, null);
        BattleAiDecision decision = fixture.Service
            .ChooseCommand(fixture.Context, captureTrace: false)
            ?.Decision;
        _test.True(decision != null, "Explicit test fallback should return an authored decision.");
        _test.Eq(
            decision.action_id,
            new StringName("authored_wait"),
            "Authored fallback should require allow_authored_action_fallback_for_tests=true."
        );
    }

    private void TestServiceReportsEmptyRuntimeState()
    {
        using var plan = new BattleAiRuntimeActionPlan();
        using Fixture fixture = BuildServiceFixture(false, plan);
        plan.SetSource(fixture.Actor, fixture.Brain);
        plan.AddStateActions("engage", Array.Empty<EnemyAiAction>());

        BattleAiDecision decision = fixture.Service
            .ChooseCommand(fixture.Context, captureTrace: false)
            ?.Decision;
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
        context.SetSkillDefinitions(new Dictionary<StringName, SkillDefinition>());

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
        return TestResourceOwnership.Own(
            new EnemyAiBrainDef
            {
                brain_id = "plan_brain",
                default_state_id = "engage",
                states = new Godot.Collections.Array<EnemyAiStateDef> { state },
            },
            "BattleAiRuntimeActionPlan.BuildBrain"
        );
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
                state.SetCell(cell.coord, cell);
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
        state.SetUnit(unit);
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

    private sealed class Fixture : IDisposable
    {
        public BattleState State;
        public BattleGridService GridService;
        public BattleUnitState Actor;
        public EnemyAiBrainDef Brain;
        public BattleAiService Service;
        public BattleAiContext Context;

        public void Dispose()
        {
            Context?.ClearRuntimeBindings();
            Service?.Dispose();
        }
    }

    private sealed class ThrowingSkillDictionary
        : IReadOnlyDictionary<StringName, SkillDefinition>
    {
        public int Count => throw BuildException();

        public IEnumerable<StringName> Keys => throw BuildException();

        public IEnumerable<SkillDefinition> Values => throw BuildException();

        public SkillDefinition this[StringName key] => throw BuildException();

        public bool ContainsKey(StringName key) => throw BuildException();

        public bool TryGetValue(StringName key, out SkillDefinition value)
        {
            value = null;
            throw BuildException();
        }

        public IEnumerator<KeyValuePair<StringName, SkillDefinition>> GetEnumerator() =>
            throw BuildException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private static InvalidOperationException BuildException() =>
            new("action plan classification probe");
    }
}
