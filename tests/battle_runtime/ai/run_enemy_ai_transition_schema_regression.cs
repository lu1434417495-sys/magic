using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_enemy_ai_transition_schema_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestTransitionConditionPredicatesUseTypedTables();
        TestAcceptsDeclaredTransitionRulesForCustomStateNames();
        TestRejectsAmbiguousRuleOrderAndIds();
        TestRejectsEmptyConditionsAndUnknownPredicates();
        TestConditionTraceShapeIsTypedAndStable();

        if (_failures.Count == 0)
        {
            GD.Print("Enemy AI transition schema regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Enemy AI transition schema regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestTransitionConditionPredicatesUseTypedTables()
    {
        Type conditionType = typeof(EnemyAiTransitionConditionDef);
        AssertTrue(
            conditionType.GetMethod("VALID_PREDICATES", BindingFlags.Public | BindingFlags.Static) == null,
            "EnemyAiTransitionConditionDef 不应公开 Godot Dictionary VALID_PREDICATES()。"
        );
        AssertTrue(
            conditionType.GetMethod("to_trace_dict", BindingFlags.Public | BindingFlags.Instance) == null,
            "EnemyAiTransitionConditionDef 不应公开 to_trace_dict() Dictionary projection。"
        );
        AssertTrue(
            conditionType.GetMethod("validate_schema", BindingFlags.Public | BindingFlags.Instance) == null,
            "EnemyAiTransitionConditionDef 不应公开 validate_schema(Dictionary) wrapper。"
        );
        AssertTrue(
            conditionType.GetMethod("to_signature", BindingFlags.Public | BindingFlags.Instance) == null,
            "EnemyAiTransitionConditionDef 不应公开 snake_case to_signature()。"
        );
        foreach (
            string removedMethod in new[]
            {
                "HP_BASIS_POINTS_DENOMINATOR",
                "PREDICATE_ALWAYS",
                "PREDICATE_CURRENT_STATE_IS",
                "PREDICATE_SELF_HP_AT_OR_BELOW",
                "PREDICATE_ALLY_HP_AT_OR_BELOW",
                "PREDICATE_NEAREST_ENEMY_DISTANCE_AT_OR_BELOW",
                "PREDICATE_HAS_SKILL_AFFORDANCE",
            }
        )
        {
            AssertTrue(
                conditionType.GetMethod(removedMethod, BindingFlags.Public | BindingFlags.Static) == null,
                $"EnemyAiTransitionConditionDef 不应公开 {removedMethod}() GDScript-style wrapper。"
            );
        }

        Type ruleType = typeof(EnemyAiTransitionRuleDef);
        AssertTrue(
            ruleType.GetMethod("applies_to_state", BindingFlags.Public | BindingFlags.Instance) == null,
            "EnemyAiTransitionRuleDef 不应公开 applies_to_state() snake_case API。"
        );
        AssertTrue(
            ruleType.GetMethod("validate_schema", BindingFlags.Public | BindingFlags.Instance) == null,
            "EnemyAiTransitionRuleDef 不应公开 validate_schema(Dictionary) wrapper。"
        );
        AssertTrue(
            ruleType.GetMethod("to_signature", BindingFlags.Public | BindingFlags.Instance) == null,
            "EnemyAiTransitionRuleDef 不应公开 to_signature() snake_case API。"
        );
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

        Godot.Collections.Array<string> errors = brain.validate_schema();
        AssertTrue(errors.Count == 0, $"custom state transition schema 应合法: {FormatErrors(errors)}");
    }

    private void TestRejectsAmbiguousRuleOrderAndIds()
    {
        EnemyAiBrainDef brain = BuildBrain();
        brain.transition_rules = new Godot.Collections.Array<EnemyAiTransitionRuleDef>
        {
            Rule("duplicate", 10, "recover", Condition("always")),
            Rule("duplicate", 10, "hold", Condition("always")),
        };

        Godot.Collections.Array<string> errors = brain.validate_schema();
        AssertTrue(ContainsError(errors, "duplicate transition rule_id duplicate"), $"应拒绝重复 rule_id: {FormatErrors(errors)}");
        AssertTrue(ContainsError(errors, "duplicate transition order 10"), $"应拒绝重复 order: {FormatErrors(errors)}");
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

        Godot.Collections.Array<string> errors = brain.validate_schema();
        AssertTrue(ContainsError(errors, "must declare at least one condition"), $"应拒绝空 conditions: {FormatErrors(errors)}");
        AssertTrue(ContainsError(errors, "uses unsupported predicate scripted_expression"), $"应拒绝未知 predicate: {FormatErrors(errors)}");
        AssertTrue(ContainsError(errors, "target_state_id missing_state is not declared"), $"应拒绝不存在的 target state: {FormatErrors(errors)}");
        AssertTrue(ContainsError(errors, "from_state_id missing_from_state is not declared"), $"应拒绝不存在的 from state: {FormatErrors(errors)}");
    }

    private void TestConditionTraceShapeIsTypedAndStable()
    {
        EnemyAiTransitionConditionDef condition = Condition(
            "has_skill_affordance",
            affordances: new[] { new StringName("ally_heal"), new StringName("self_or_ally_buff") }
        );
        BattleAiStateResolver.TransitionConditionTrace trace =
            BattleAiStateResolver.TransitionConditionTrace.FromCondition(condition);

        AssertEq(trace.Predicate, new StringName("has_skill_affordance"), "trace 应输出 predicate。");
        AssertEq(trace.BasisPoints, -1, "未使用的 basis_points 应固定为 -1。");
        AssertEq(trace.MaxDistance, -1, "未使用的 max_distance 应固定为 -1。");
        AssertEq(trace.StateIds.Count, 0, "未使用的 state_ids 应固定为空数组。");
        AssertContains(trace.Affordances, "ally_heal", "affordance trace 应包含 ally_heal。");
        AssertContains(trace.Affordances, "self_or_ally_buff", "affordance trace 应包含 self_or_ally_buff。");
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
        return new EnemyAiStateDef
        {
            state_id = stateId,
            actions = new Godot.Collections.Array<EnemyAiAction>
            {
                new WaitAction { action_id = $"{stateId}_wait" },
            },
        };
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
        return rule;
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
        return condition;
    }

    private static bool ContainsError(IEnumerable<string> errors, string expectedFragment)
    {
        foreach (string error in errors)
        {
            if ((error ?? "").Contains(expectedFragment, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string FormatErrors(IEnumerable<string> errors) => string.Join("; ", errors);

    private void AssertContains(
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
        _failures.Add(message);
    }

    private void AssertEq(StringName actual, StringName expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertEq(int actual, int expected, string message)
    {
        if (actual != expected)
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
}
