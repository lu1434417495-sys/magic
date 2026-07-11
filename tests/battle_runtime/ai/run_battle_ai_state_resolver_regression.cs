using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_state_resolver_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestResolvesCustomStateNamesWithoutMutatingUnitState();
        TestAllyLowHpExcludesSelf();
        TestNearestEnemyDistanceAndStickyRuleAreDataDriven();
        TestSkillAffordanceUsesPlanCacheAndContextLazyCache();
        TestContextLazySkillAffordanceRequiresTypedSkillDefs();

        RequestTestExit(_test.Finish("Battle AI state resolver regression"));
    }

    private void TestResolvesCustomStateNamesWithoutMutatingUnitState()
    {
        Fixture fixture = BuildFixture();
        EnemyAiBrainDef brain = BuildBrain(
            "hold",
            Rule("recover_low_hp", 10, "recover", Condition("self_hp_at_or_below_basis_points", basisPoints: 3000)),
            Rule("hold_default", 20, "hold", Condition("always"))
        );
        fixture.Actor.ai_state_id = "hold";
        fixture.Actor.current_hp = 5;
        fixture.Actor.attribute_snapshot.SetValue("hp_max", 20);

        BattleAiStateResolver.TransitionResult result = fixture.Resolver.ResolveTyped(
            fixture.Context,
            brain
        );

        _test.Eq(result.PreviousStateId, new StringName("hold"), "resolver 应记录原 state。");
        _test.Eq(result.StateId, new StringName("recover"), "低血量应按 rule 切到自定义 recover。");
        _test.Eq(result.RuleId, new StringName("recover_low_hp"), "trace 应记录命中的 rule。");
        _test.Eq(fixture.Actor.ai_state_id, new StringName("hold"), "resolver 本身不得写 unit_state.ai_state_id。");
    }

    private void TestAllyLowHpExcludesSelf()
    {
        Fixture fixture = BuildFixture();
        EnemyAiBrainDef brain = BuildBrain(
            "hold",
            Rule("aid_low_ally", 10, "aid_ally", Condition("ally_hp_at_or_below_basis_points", basisPoints: 5000)),
            Rule("hold_default", 20, "hold", Condition("always"))
        );
        fixture.Actor.current_hp = 1;
        fixture.Actor.attribute_snapshot.SetValue("hp_max", 20);

        BattleAiStateResolver.TransitionResult selfOnlyResult = fixture.Resolver.ResolveTyped(
            fixture.Context,
            brain
        );
        _test.Eq(selfOnlyResult.StateId, new StringName("hold"), "ally_hp_at_or_below 不应把自己算成低血友军。");

        fixture.Ally.current_hp = 5;
        fixture.Ally.attribute_snapshot.SetValue("hp_max", 20);
        BattleAiStateResolver.TransitionResult allyResult = fixture.Resolver.ResolveTyped(
            fixture.Context,
            brain
        );
        _test.Eq(allyResult.StateId, new StringName("aid_ally"), "其他同阵营单位低血时才应进入 aid_ally。");
    }

    private void TestNearestEnemyDistanceAndStickyRuleAreDataDriven()
    {
        Fixture fixture = BuildFixture();
        EnemyAiBrainDef brain = BuildBrain(
            "hold",
            Rule(
                "stay_close_range",
                5,
                "close_range",
                Condition("current_state_is", stateIds: new[] { new StringName("close_range") }),
                Condition("nearest_enemy_distance_at_or_below", maxDistance: 3),
                fromStateIds: new[] { new StringName("close_range") }
            ),
            Rule("enter_close_range", 10, "close_range", Condition("nearest_enemy_distance_at_or_below", maxDistance: 2)),
            Rule("hold_default", 20, "hold", Condition("always"))
        );
        fixture.Actor.ai_state_id = "hold";
        fixture.Enemy.SetAnchorCoord(new Vector2I(3, 1));
        fixture.GridService.PlaceUnit(fixture.State, fixture.Enemy, fixture.Enemy.coord, true);

        BattleAiStateResolver.TransitionResult enterResult = fixture.Resolver.ResolveTyped(
            fixture.Context,
            brain
        );
        _test.Eq(enterResult.StateId, new StringName("close_range"), "距离 2 时应进入 close_range。");

        fixture.Actor.ai_state_id = "close_range";
        fixture.Enemy.SetAnchorCoord(new Vector2I(4, 1));
        fixture.GridService.PlaceUnit(fixture.State, fixture.Enemy, fixture.Enemy.coord, true);
        BattleAiStateResolver.TransitionResult stickyResult = fixture.Resolver.ResolveTyped(
            fixture.Context,
            brain
        );
        _test.Eq(stickyResult.RuleId, new StringName("stay_close_range"), "sticky 行为应由 current_state_is + 距离 rule 表达。");
        _test.Eq(stickyResult.StateId, new StringName("close_range"), "距离 3 时应保持 close_range。");
    }

    private void TestSkillAffordanceUsesPlanCacheAndContextLazyCache()
    {
        Fixture fixture = BuildFixture();
        SkillDefinition supportSkill = BuildSupportSkill("aid_spell");
        fixture.Context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [supportSkill.SkillId] = supportSkill,
            }
        );
        fixture.Actor.known_active_skill_ids = new Godot.Collections.Array<Godot.StringName>
        {
            supportSkill.SkillId,
        };
        fixture.Actor.known_skill_level_map[supportSkill.SkillId] = 1;
        EnemyAiBrainDef brain = BuildBrain(
            "hold",
            Rule("aid_skill_available", 10, "aid_ally", Condition("has_skill_affordance", affordances: new[] { new StringName("ally_heal") })),
            Rule("hold_default", 20, "hold", Condition("always"))
        );

        using var plan = new BattleAiRuntimeActionPlan();
        plan.SetSource(fixture.Actor, brain);
        plan.SetSkillAffordanceRecordTyped(
            new BattleAiSkillAffordanceRecord
            {
                skill_id = supportSkill.SkillId,
                affordances = new() { "ally_heal" },
                action_families = new() { "use_unit_skill" },
            }
        );
        fixture.Context.runtime_action_plan = plan;

        BattleAiStateResolver.TransitionResult planResult = fixture.Resolver.ResolveTyped(
            fixture.Context,
            brain
        );
        _test.Eq(planResult.StateId, new StringName("aid_ally"), "production path 应优先读取 runtime plan 的 affordance cache。");

        fixture.Context.runtime_action_plan = null;
        fixture.Actor.ai_state_id = "hold";
        BattleAiStateResolver.TransitionResult lazyResult = fixture.Resolver.ResolveTyped(
            fixture.Context,
            brain
        );
        _test.Eq(lazyResult.StateId, new StringName("aid_ally"), "无 plan 的测试路径应按 skill definitions lazy classify。");
    }

    private void TestContextLazySkillAffordanceRequiresTypedSkillDefs()
    {
        Fixture fixture = BuildFixture();
        SkillDefinition supportSkill = BuildSupportSkill("aid_spell");
        fixture.Actor.known_active_skill_ids = new Godot.Collections.Array<Godot.StringName>
        {
            supportSkill.SkillId,
        };
        fixture.Actor.known_skill_level_map[supportSkill.SkillId] = 1;
        EnemyAiBrainDef brain = BuildBrain(
            "hold",
            Rule("aid_skill_available", 10, "aid_ally", Condition("has_skill_affordance", affordances: new[] { new StringName("ally_heal") })),
            Rule("hold_default", 20, "hold", Condition("always"))
        );

        fixture.Context.SetSkillDefinitions(new Dictionary<StringName, SkillDefinition>());
        BattleAiStateResolver.TransitionResult missingTypedDefsResult = fixture.Resolver.ResolveTyped(
            fixture.Context,
            brain
        );
        _test.Eq(missingTypedDefsResult.StateId, new StringName("hold"), "skill definitions lazy cache 不应从空 typed index 恢复。");

        fixture.Context.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [supportSkill.SkillId] = supportSkill,
            }
        );
        BattleAiStateResolver.TransitionResult typedIndexResult = fixture.Resolver.ResolveTyped(
            fixture.Context,
            brain
        );
        _test.Eq(typedIndexResult.StateId, new StringName("aid_ally"), "skill definitions lazy cache 应消费 typed skill index。");
    }

    private static Fixture BuildFixture()
    {
        var state = new BattleState
        {
            battle_id = "resolver_regression",
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

        var gridService = new BattleGridService();
        BattleUnitState actor = Unit("actor", "hostile", new Vector2I(1, 1));
        BattleUnitState ally = Unit("ally", "hostile", new Vector2I(1, 2));
        BattleUnitState enemy = Unit("enemy", "player", new Vector2I(5, 1));
        AddUnit(gridService, state, actor, true);
        AddUnit(gridService, state, ally, true);
        AddUnit(gridService, state, enemy, false);

        var context = new BattleAiContext
        {
            state = state,
            unit_state = actor,
            grid_service = gridService,
        };
        context.SetSkillDefinitions(new Dictionary<StringName, SkillDefinition>());

        return new Fixture
        {
            State = state,
            GridService = gridService,
            Actor = actor,
            Ally = ally,
            Enemy = enemy,
            Context = context,
            Resolver = new BattleAiStateResolver(),
        };
    }

    private static EnemyAiBrainDef BuildBrain(StringName defaultStateId, params EnemyAiTransitionRuleDef[] rules)
    {
        var brain = TestResourceOwnership.Own(
            new EnemyAiBrainDef
            {
                brain_id = "resolver_brain",
                default_state_id = defaultStateId,
                states = new Godot.Collections.Array<EnemyAiStateDef>
                {
                    State("hold"),
                    State("recover"),
                    State("aid_ally"),
                    State("close_range"),
                },
                transition_rules = new Godot.Collections.Array<EnemyAiTransitionRuleDef>(),
            },
            "BattleAiStateResolver.BuildBrain"
        );
        foreach (EnemyAiTransitionRuleDef rule in rules)
        {
            brain.transition_rules.Add(rule);
        }
        return TestResourceOwnership.Own(brain, "BattleAiStateResolver.BuildBrain");
    }

    private static EnemyAiStateDef State(StringName stateId)
    {
        return new EnemyAiStateDef
        {
            state_id = stateId,
            actions = new Godot.Collections.Array<EnemyAiAction>
            {
                Wait(new StringName($"{stateId}_wait")),
            },
        };
    }

    private static WaitAction Wait(StringName actionId)
    {
        return new WaitAction { action_id = actionId };
    }

    private static EnemyAiTransitionRuleDef Rule(
        StringName ruleId,
        int order,
        StringName targetStateId,
        params EnemyAiTransitionConditionDef[] conditions
    ) =>
        Rule(ruleId, order, targetStateId, conditions, Array.Empty<StringName>());

    private static EnemyAiTransitionRuleDef Rule(
        StringName ruleId,
        int order,
        StringName targetStateId,
        EnemyAiTransitionConditionDef firstCondition,
        EnemyAiTransitionConditionDef secondCondition,
        StringName[] fromStateIds
    ) =>
        Rule(ruleId, order, targetStateId, new[] { firstCondition, secondCondition }, fromStateIds);

    private static EnemyAiTransitionRuleDef Rule(
        StringName ruleId,
        int order,
        StringName targetStateId,
        EnemyAiTransitionConditionDef[] conditions,
        StringName[] fromStateIds
    )
    {
        var rule = new EnemyAiTransitionRuleDef
        {
            rule_id = ruleId,
            order = order,
            target_state_id = targetStateId,
            conditions = new Godot.Collections.Array<EnemyAiTransitionConditionDef>(),
            from_state_ids = new Godot.Collections.Array<Godot.StringName>(),
        };
        foreach (EnemyAiTransitionConditionDef condition in conditions ?? Array.Empty<EnemyAiTransitionConditionDef>())
        {
            rule.conditions.Add(condition);
        }
        foreach (StringName stateId in fromStateIds ?? Array.Empty<StringName>())
        {
            rule.from_state_ids.Add(stateId);
        }
        return rule;
    }

    private static EnemyAiTransitionConditionDef Condition(
        StringName predicate,
        int basisPoints = -1,
        int maxDistance = -1,
        StringName[] stateIds = null,
        StringName[] affordances = null
    )
    {
        var condition = new EnemyAiTransitionConditionDef
        {
            predicate = predicate,
            basis_points = basisPoints,
            max_distance = maxDistance,
            state_ids = new Godot.Collections.Array<Godot.StringName>(),
            affordances = new Godot.Collections.Array<Godot.StringName>(),
        };
        foreach (StringName stateId in stateIds ?? Array.Empty<StringName>())
        {
            condition.state_ids.Add(stateId);
        }
        foreach (StringName affordance in affordances ?? Array.Empty<StringName>())
        {
            condition.affordances.Add(affordance);
        }
        return condition;
    }

    private static BattleUnitState Unit(StringName unitId, StringName factionId, Vector2I coord)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = "ai",
            current_hp = 20,
            current_ap = 2,
            current_mp = 2,
            current_stamina = 2,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue("hp_max", 20);
        return unit;
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

    private static SkillDefinition BuildSupportSkill(StringName skillId) =>
        TestSkillDefinitionProjection.BuildSkill(
            skillId,
            "测试支援",
            TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[]
                {
                    TestSkillDefinitionProjection.BuildEffect(
                        "heal",
                        effectTargetTeamFilter: "ally",
                        power: 8
                    ),
                },
                targetMode: "unit",
                targetTeamFilter: "ally",
                targetSelectionMode: BattleTypedNames.ToStringName(
                    BattleTargetSelectionMode.SingleUnit
                )
            )
        );

    private static bool IsGodotDynamicBoundaryType(Type type)
    {
        if (type == typeof(Variant))
        {
            return true;
        }
        string typeName = type.FullName ?? "";
        return typeName.StartsWith("Godot.Collections.Dictionary", StringComparison.Ordinal)
            || typeName.StartsWith("Godot.Collections.Array", StringComparison.Ordinal);
    }

    private sealed class Fixture
    {
        public BattleState State;
        public BattleGridService GridService;
        public BattleUnitState Actor;
        public BattleUnitState Ally;
        public BattleUnitState Enemy;
        public BattleAiContext Context;
        public BattleAiStateResolver Resolver;
    }
}
