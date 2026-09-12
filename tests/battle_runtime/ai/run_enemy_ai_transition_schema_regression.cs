using System;
using System.Collections.Generic;
using System.Linq;
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
        IReadOnlyList<ContentJsonDiagnostic> diagnostics = Validate(
            Rule(
                "recover_when_low",
                10,
                "recover",
                Condition("self_hp_at_or_below_basis_points", basisPoints: 3000)
            ),
            Rule(
                "close_range_when_near",
                20,
                "close_range",
                Condition("nearest_enemy_distance_at_or_below", maxDistance: 2)
            ),
            Rule("hold_default", 30, "hold", Condition("always"))
        );
        _test.Eq(
            diagnostics.Count,
            0,
            $"custom state transition schema 应合法: {FormatDiagnostics(diagnostics)}"
        );
    }

    private void TestRejectsAmbiguousRuleOrderAndIds()
    {
        IReadOnlyList<ContentJsonDiagnostic> diagnostics = Validate(
            Rule("duplicate", 10, "recover", Condition("always")),
            Rule("duplicate", 10, "hold", Condition("always"))
        );
        _test.Eq(
            diagnostics.Count,
            2,
            $"重复 fixture 应只触发 rule_id/order 两条规则: {FormatDiagnostics(diagnostics)}"
        );
        AssertDiagnostic(
            diagnostics,
            EnemyContentImportRules.DuplicateId,
            "/transition_rules",
            "Duplicate rule_id 'duplicate'",
            "重复 rule_id 应命中 typed JSON validator。"
        );
        AssertDiagnostic(
            diagnostics,
            EnemyContentImportRules.DuplicateId,
            "/transition_rules",
            "Duplicate order '10'",
            "重复 transition order 应命中 typed JSON validator。"
        );
    }

    private void TestRejectsEmptyConditionsAndUnknownPredicates()
    {
        IReadOnlyList<ContentJsonDiagnostic> diagnostics = Validate(
            Rule("empty_conditions", 10, "recover"),
            Rule("unknown_condition", 20, "hold", Condition("scripted_expression")),
            Rule("bad_target", 30, "missing_state", Condition("always")),
            Rule(
                "bad_from",
                40,
                "hold",
                new[] { "missing_from_state" },
                Condition("always")
            )
        );
        _test.Eq(
            diagnostics.Count,
            4,
            $"非法 transition fixture 应逐条触发四项目标规则: {FormatDiagnostics(diagnostics)}"
        );
        AssertDiagnostic(
            diagnostics,
            EnemyContentImportRules.CollectionRequired,
            "/conditions",
            "must declare at least one condition",
            "空 conditions 应命中所属 rule。"
        );
        AssertDiagnostic(
            diagnostics,
            EnemyContentImportRules.ValueUnsupported,
            "/predicate",
            "Unsupported transition predicate 'scripted_expression'",
            "未知 predicate 应命中所属 rule。"
        );
        AssertDiagnostic(
            diagnostics,
            EnemyContentImportRules.ReferenceMissing,
            "/target_state_id",
            "target_state_id 'missing_state' is not declared",
            "缺失 target state 应命中所属 rule。"
        );
        AssertDiagnostic(
            diagnostics,
            EnemyContentImportRules.ReferenceMissing,
            "/from_state_ids/0",
            "from_state_id 'missing_from_state' is not declared",
            "缺失 from state 应命中所属 rule。"
        );
    }

    private void TestConditionTraceShapeIsTypedAndStable()
    {
        EnemyAiTransitionConditionDefinition condition =
            TestEnemyDefinitionFactory.TransitionCondition(
                "has_skill_affordance",
                affordances: new StringName[] { "ally_heal", "self_or_ally_buff" }
            );
        BattleAiStateResolver.TransitionConditionTrace trace =
            BattleAiStateResolver.TransitionConditionTrace.FromCondition(condition);

        _test.Eq(trace.Predicate, new StringName("has_skill_affordance"), "trace 应输出 predicate。");
        _test.Eq(trace.BasisPoints, -1, "未使用的 basis_points 应固定为 -1。");
        _test.Eq(trace.MaxDistance, -1, "未使用的 max_distance 应固定为 -1。");
        _test.Eq(trace.StateIds.Count, 0, "未使用的 state_ids 应固定为空数组。");
        AssertListHas(trace.Affordances, "ally_heal", "affordance trace 应包含 ally_heal。");
        AssertListHas(
            trace.Affordances,
            "self_or_ally_buff",
            "affordance trace 应包含 self_or_ally_buff。"
        );
    }

    private static IReadOnlyList<ContentJsonDiagnostic> Validate(
        params EnemyAiTransitionRuleJsonDto[] rules
    ) =>
        EnemyContentImportValidator.ValidateBrain(
            new JsonContentEntryContext(
                EnemyContentJsonDomains.BrainDomainId,
                "custom_transition_brain",
                "custom_transition_brain.json",
                "/entries/0"
            ),
            BuildBrain(rules)
        );

    private static EnemyAiBrainImportModel BuildBrain(
        IReadOnlyList<EnemyAiTransitionRuleJsonDto> rules
    ) =>
        new(
            "custom_transition_brain",
            "hold",
            null,
            new[] { State("hold"), State("recover"), State("close_range") },
            rules
        );

    private static EnemyAiStateImportModel State(string stateId) =>
        new(
            stateId,
            new EnemyAiActionImportModel[]
            {
                new(
                    "wait",
                    new WaitActionPayloadJsonDto
                    {
                        ActionId = $"{stateId}_wait",
                        ScoreBucketId = "default",
                        ActionIntent = "wait",
                        ActiveRestActionBaseScore = 10,
                        ActiveRestMinStaminaResidue = 1,
                    }
                ),
            },
            Array.Empty<EnemyAiGenerationSlotJsonDto>()
        );

    private static EnemyAiTransitionRuleJsonDto Rule(
        string ruleId,
        int order,
        string targetStateId,
        params EnemyAiTransitionConditionJsonDto[] conditions
    ) => Rule(ruleId, order, targetStateId, Array.Empty<string>(), conditions);

    private static EnemyAiTransitionRuleJsonDto Rule(
        string ruleId,
        int order,
        string targetStateId,
        IReadOnlyList<string> fromStateIds,
        params EnemyAiTransitionConditionJsonDto[] conditions
    ) =>
        new()
        {
            RuleId = ruleId,
            Order = order,
            TargetStateId = targetStateId,
            FromStateIds = fromStateIds,
            Conditions = conditions,
            DesignerNote = "fixture",
        };

    private static EnemyAiTransitionConditionJsonDto Condition(
        string predicate,
        int basisPoints = -1,
        int maxDistance = -1,
        IReadOnlyList<string> stateIds = null,
        IReadOnlyList<string> affordances = null
    ) =>
        new()
        {
            Predicate = predicate,
            BasisPoints = basisPoints,
            MaxDistance = maxDistance,
            StateIds = stateIds ?? Array.Empty<string>(),
            Affordances = affordances ?? Array.Empty<string>(),
        };

    private static string FormatDiagnostics(IEnumerable<ContentJsonDiagnostic> diagnostics) =>
        string.Join(
            "; ",
            diagnostics.Select(value => $"{value.RuleId}@{value.JsonPointer}: {value.Message}")
        );

    private void AssertDiagnostic(
        IEnumerable<ContentJsonDiagnostic> diagnostics,
        string ruleId,
        string pointerSuffix,
        string messageFragment,
        string message
    )
    {
        if (
            diagnostics.Any(value =>
                value.RuleId == ruleId
                && value.JsonPointer.EndsWith(pointerSuffix, StringComparison.Ordinal)
                && value.Message.Contains(messageFragment, StringComparison.Ordinal)
            )
        )
        {
            return;
        }
        _test.Fail($"{message} diagnostics={FormatDiagnostics(diagnostics)}");
    }

    private void AssertListHas(
        IEnumerable<StringName> values,
        StringName expected,
        string message
    )
    {
        if (values?.Contains(expected) == true)
            return;
        _test.Fail(message);
    }
}
