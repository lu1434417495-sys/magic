using System;
using System.Collections.Generic;
using Godot;

public partial class run_enemy_ai_transition_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestAcceptsDeclaredTransitionRulesForCustomStateNames();
        TestRejectsAmbiguousRuleOrderAndIds();
        TestRejectsEmptyConditionsAndUnknownPredicates();
        TestConditionTraceShapeIsTypedAndStable();

        RequestTestExit(_test.Finish("Enemy AI transition schema regression"));
    }

    private void TestAcceptsDeclaredTransitionRulesForCustomStateNames()
    {
        EnemyAiBrainDef brain = BuildBrain();
        EnemyAiTransitionRuleDef lowHpRule = Rule(
            "recover_when_low",
            10,
            "recover",
            Condition("self_hp_at_or_below_basis_points", basisPoints: 3000)
        );
        EnemyAiTransitionRuleDef closeRangeRule = Rule(
            "close_range_when_near",
            20,
            "close_range",
            Condition("nearest_enemy_distance_at_or_below", maxDistance: 2)
        );
        EnemyAiTransitionRuleDef holdRule = Rule(
            "hold_default",
            30,
            "hold",
            Condition("always")
        );
        brain.transition_rules = new Godot.Collections.Array<EnemyAiTransitionRuleDef>
        {
            lowHpRule,
            closeRangeRule,
            holdRule,
        };
        TestResourceOwnership.Own(
            brain,
            "EnemyAiTransitionSchema.AcceptsDeclared.brain"
        );

        Godot.Collections.Array<string> errors = brain.ValidateSchema();
        _test.True(errors.Count == 0, $"custom state transition schema 应合法: {FormatErrors(errors)}");
    }

    private void TestRejectsAmbiguousRuleOrderAndIds()
    {
        EnemyAiBrainDef brain = BuildBrain();
        brain.transition_rules = new Godot.Collections.Array<EnemyAiTransitionRuleDef>
        {
            Rule("duplicate", 10, "recover", Condition("always")),
            Rule("duplicate", 10, "hold", Condition("always")),
        };
        TestResourceOwnership.Own(
            brain,
            "EnemyAiTransitionSchema.RejectsAmbiguous.brain"
        );

        Godot.Collections.Array<string> errors = brain.ValidateSchema();
        _test.True(errors.Count >= 2, $"应拒绝重复 rule_id/order: {FormatErrors(errors)}");
    }

    private void TestRejectsEmptyConditionsAndUnknownPredicates()
    {
        EnemyAiBrainDef brain = BuildBrain();
        brain.transition_rules = new Godot.Collections.Array<EnemyAiTransitionRuleDef>
        {
            Rule("empty_conditions", 10, "recover"),
            Rule("unknown_condition", 20, "hold", Condition("scripted_expression")),
            Rule("bad_target", 30, "missing_state", Condition("always")),
            Rule(
                "bad_from",
                40,
                "hold",
                new[] { new StringName("missing_from_state") },
                Condition("always")
            ),
        };
        TestResourceOwnership.Own(
            brain,
            "EnemyAiTransitionSchema.RejectsInvalidConditions.brain"
        );

        Godot.Collections.Array<string> errors = brain.ValidateSchema();
        _test.True(errors.Count >= 4, $"应拒绝空 conditions、未知 predicate 和不存在的 state 引用: {FormatErrors(errors)}");
    }

    private void TestConditionTraceShapeIsTypedAndStable()
    {
        EnemyAiTransitionConditionDef condition = Condition(
            "has_skill_affordance",
            affordances: new[] { new StringName("ally_heal"), new StringName("self_or_ally_buff") }
        );
        BattleAiStateResolver.TransitionConditionTrace trace =
            BattleAiStateResolver.TransitionConditionTrace.FromCondition(
                condition.ToDefinition()
            );

        _test.Eq(trace.Predicate, new StringName("has_skill_affordance"), "trace 应输出 predicate。");
        _test.Eq(trace.BasisPoints, -1, "未使用的 basis_points 应固定为 -1。");
        _test.Eq(trace.MaxDistance, -1, "未使用的 max_distance 应固定为 -1。");
        _test.Eq(trace.StateIds.Count, 0, "未使用的 state_ids 应固定为空数组。");
        AssertListHas(trace.Affordances, "ally_heal", "affordance trace 应包含 ally_heal。");
        AssertListHas(trace.Affordances, "self_or_ally_buff", "affordance trace 应包含 self_or_ally_buff。");
    }

    private static EnemyAiBrainDef BuildBrain()
    {
        return new EnemyAiBrainDef
        {
            brain_id = "custom_transition_brain",
            default_state_id = "hold",
            states = new Godot.Collections.Array<EnemyAiStateDef>
            {
                State("hold"),
                State("recover"),
                State("close_range"),
            },
        };
    }

    private static EnemyAiStateDef State(StringName stateId)
    {
        WaitAction waitAction = TestResourceOwnership.Own(
            new WaitAction { action_id = $"{stateId}_wait" },
            $"EnemyAiTransitionSchema.State.{stateId}.wait_action"
        );
        var state = new EnemyAiStateDef
        {
            state_id = stateId,
            actions = new Godot.Collections.Array<EnemyAiAction>
            {
                waitAction,
            },
        };
        return TestResourceOwnership.Own(
            state,
            $"EnemyAiTransitionSchema.State.{stateId}"
        );
    }

    private static EnemyAiTransitionRuleDef Rule(
        StringName ruleId,
        int order,
        StringName targetStateId,
        params EnemyAiTransitionConditionDef[] conditions
    )
    {
        return Rule(ruleId, order, targetStateId, Array.Empty<StringName>(), conditions);
    }

    private static EnemyAiTransitionRuleDef Rule(
        StringName ruleId,
        int order,
        StringName targetStateId,
        IEnumerable<StringName> fromStateIds,
        params EnemyAiTransitionConditionDef[] conditions
    )
    {
        var rule = new EnemyAiTransitionRuleDef
        {
            rule_id = ruleId,
            order = order,
            target_state_id = targetStateId,
        };
        foreach (StringName fromStateId in fromStateIds ?? Array.Empty<StringName>())
        {
            rule.from_state_ids.Add(fromStateId);
        }
        foreach (EnemyAiTransitionConditionDef condition in conditions ?? Array.Empty<EnemyAiTransitionConditionDef>())
        {
            rule.conditions.Add(condition);
        }
        return TestResourceOwnership.Own(
            rule,
            $"EnemyAiTransitionSchema.Rule.{ruleId}"
        );
    }

    private static EnemyAiTransitionConditionDef Condition(
        StringName predicate,
        int basisPoints = -1,
        int maxDistance = -1,
        IEnumerable<StringName> stateIds = null,
        IEnumerable<StringName> affordances = null
    )
    {
        var condition = new EnemyAiTransitionConditionDef
        {
            predicate = predicate,
            basis_points = basisPoints,
            max_distance = maxDistance,
        };
        foreach (StringName stateId in stateIds ?? Array.Empty<StringName>())
        {
            condition.state_ids.Add(stateId);
        }
        foreach (StringName affordance in affordances ?? Array.Empty<StringName>())
        {
            condition.affordances.Add(affordance);
        }
        return TestResourceOwnership.Own(
            condition,
            $"EnemyAiTransitionSchema.Condition.{predicate}"
        );
    }

    private static string FormatErrors(IEnumerable<string> errors) => string.Join("; ", errors);

    private void AssertListHas(
        IEnumerable<StringName> values,
        StringName expected,
        string message
    )
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value == expected)
            {
                return;
            }
        }
        _test.Fail(message);
    }

}
