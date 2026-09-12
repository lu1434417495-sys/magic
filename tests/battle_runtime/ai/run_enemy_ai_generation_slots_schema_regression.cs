using System;
using System.Collections.Generic;
using System.Linq;

public partial class run_enemy_ai_generation_slots_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestTargetSelectorRulesBehavior();
        TestValidGenerationSlotsPassSchema();
        TestDuplicateSlotIdsAndOrdersAreRejected();
        TestInvalidFamilyAndTemplateAreRejected();
        TestSelectorDistanceContractsAreRejected();
        RequestTestExit(_test.Finish("Enemy AI generation slots schema regression"));
    }

    private void TestTargetSelectorRulesBehavior()
    {
        _test.True(
            EnemyAiTargetSelectorRules.IsSupportedSelector("nearest_enemy")
                && EnemyAiTargetSelectorRules.IsSupportedSelector("lowest_hp_enemy")
                && EnemyAiTargetSelectorRules.IsSupportedSelector("nearest_role_threat_enemy")
                && EnemyAiTargetSelectorRules.IsSupportedSelector("nearest_ally")
                && EnemyAiTargetSelectorRules.IsSupportedSelector("lowest_hp_ally")
                && EnemyAiTargetSelectorRules.IsSupportedSelector("self"),
            "正式 target selector 集合应覆盖全部现有配置值。"
        );
        _test.True(
            !EnemyAiTargetSelectorRules.IsSupportedSelector("legacy_selector"),
            "未知 target selector 不应被兼容。"
        );
        _test.True(
            !EnemyAiTargetSelectorRules.IsSupportedSelector("")
                && EnemyAiTargetSelectorRules.IsSupportedSelector("", allowEmpty: true),
            "空 selector 只应在显式允许时通过。"
        );
        _test.True(
            EnemyAiTargetSelectorRules.IsEnemyFocusSelector("nearest_enemy")
                && !EnemyAiTargetSelectorRules.IsEnemyFocusSelector("nearest_ally")
                && !EnemyAiTargetSelectorRules.IsEnemyFocusSelector("self"),
            "enemy-focus action 应能复用 typed selector 分类。"
        );
    }

    private void TestValidGenerationSlotsPassSchema()
    {
        IReadOnlyList<ContentJsonDiagnostic> diagnostics = Validate(
            Slot("offense", 10, new[] { "unit_hostile.damage" }, new[] { "use_unit_skill" }, "template_attack"),
            Slot("close", 20, new[] { "random_chain" }, new[] { "move_to_range" }, "template_move")
        );
        _test.Eq(
            diagnostics.Count,
            0,
            $"合法 generation slots 不应产生 schema diagnostic: {FormatDiagnostics(diagnostics)}"
        );
    }

    private void TestDuplicateSlotIdsAndOrdersAreRejected()
    {
        IReadOnlyList<ContentJsonDiagnostic> diagnostics = Validate(
            Slot("dup", 10, new[] { "unit_hostile.damage" }, new[] { "use_unit_skill" }, "template_attack"),
            Slot("dup", 10, new[] { "ground_control" }, new[] { "use_ground_skill" }, "template_attack")
        );
        _test.Eq(
            diagnostics.Count,
            2,
            $"重复 fixture 应只触发 slot id/order 两条规则: {FormatDiagnostics(diagnostics)}"
        );
        AssertDiagnostic(
            diagnostics,
            EnemyContentImportRules.DuplicateId,
            "/generation_slots",
            "Duplicate slot_id 'dup'",
            "重复 slot_id 应命中 typed JSON validator。"
        );
        AssertDiagnostic(
            diagnostics,
            EnemyContentImportRules.DuplicateId,
            "/generation_slots",
            "Duplicate order '10'",
            "重复 slot order 应命中 typed JSON validator。"
        );
    }

    private void TestInvalidFamilyAndTemplateAreRejected()
    {
        IReadOnlyList<ContentJsonDiagnostic> diagnostics = Validate(
            Slot("bad_family", 10, new[] { "unit_hostile.damage" }, new[] { "old_use_skill" }, "template_attack"),
            Slot("missing_template", 20, new[] { "unit_hostile.damage" }, new[] { "use_unit_skill" }, "does_not_exist")
        );
        _test.Eq(
            diagnostics.Count,
            2,
            $"invalid family/template fixture 应只触发目标规则: {FormatDiagnostics(diagnostics)}"
        );
        AssertDiagnostic(
            diagnostics,
            EnemyContentImportRules.ValueUnsupported,
            "/action_families/0",
            "Unsupported action_family 'old_use_skill'",
            "旧 action family alias 应被拒绝。"
        );
        AssertDiagnostic(
            diagnostics,
            EnemyContentImportRules.ReferenceMissing,
            "/style_template_action_id",
            "style_template_action_id 'does_not_exist' is not declared",
            "缺失 style template action 应被拒绝。"
        );
    }

    private void TestSelectorDistanceContractsAreRejected()
    {
        IReadOnlyList<ContentJsonDiagnostic> diagnostics = Validate(
            Slot(
                "bad_selector",
                10,
                new[] { "unit_hostile.damage" },
                new[] { "use_unit_skill" },
                "template_attack",
                targetSelector: "legacy_selector"
            ),
            Slot(
                "bad_distance",
                20,
                new[] { "random_chain" },
                new[] { "move_to_range" },
                "template_move",
                desiredMinDistance: 6,
                desiredMaxDistance: 2
            )
        );
        _test.Eq(
            diagnostics.Count,
            2,
            $"selector/distance fixture 应只触发目标规则: {FormatDiagnostics(diagnostics)}"
        );
        AssertDiagnostic(
            diagnostics,
            EnemyContentImportRules.ValueUnsupported,
            "/target_selector",
            "Unsupported target_selector 'legacy_selector'",
            "未知 selector 应被拒绝。"
        );
        AssertDiagnostic(
            diagnostics,
            EnemyContentImportRules.ValueOutOfRange,
            "/desired_max_distance",
            "desired_min_distance cannot exceed desired_max_distance",
            "min > max 应被拒绝。"
        );
    }

    private static IReadOnlyList<ContentJsonDiagnostic> Validate(
        params EnemyAiGenerationSlotJsonDto[] slots
    ) =>
        EnemyContentImportValidator.ValidateBrain(
            new JsonContentEntryContext(
                EnemyContentJsonDomains.BrainDomainId,
                "schema_brain",
                "schema_brain.json",
                "/entries/0"
            ),
            BuildBrain(slots)
        );

    private static EnemyAiBrainImportModel BuildBrain(
        IReadOnlyList<EnemyAiGenerationSlotJsonDto> slots
    ) =>
        new(
            "schema_brain",
            "engage",
            null,
            new[]
            {
                new EnemyAiStateImportModel(
                    "engage",
                    new EnemyAiActionImportModel[]
                    {
                        new(
                            "use_unit_skill",
                            new UseUnitSkillActionPayloadJsonDto
                            {
                                ActionId = "template_attack",
                                ScoreBucketId = "default_offense",
                                ActionIntent = "offense",
                                SkillIds = new[] { "dummy_skill" },
                                TargetSelector = "nearest_enemy",
                                MinimumEffectiveTargetCount = 1,
                                MaximumFriendlyFireTargetCount = 0,
                                AllowFriendlyLethal = false,
                                DesiredMinDistance = 1,
                                DesiredMaxDistance = 4,
                                DistanceReference = "target_unit",
                            }
                        ),
                        new(
                            "move_to_range",
                            new MoveToRangeActionPayloadJsonDto
                            {
                                ActionId = "template_move",
                                ScoreBucketId = "default_offense",
                                ActionIntent = "positioning",
                                AiEvaluationMode = "inline_decide",
                                TargetSelector = "nearest_enemy",
                                DesiredMinDistance = 1,
                                DesiredMaxDistance = 4,
                                RangeSkillIds = Array.Empty<string>(),
                                ScreeningMode = "none",
                                EnableAoeSetupPositioning = true,
                                AoeSetupMinTargetCount = 2,
                                AoeSetupTargetCountWeight = 140,
                                AoeSetupImprovementWeight = 220,
                                AoeSetupFriendlyFirePenalty = 1000,
                                ScreeningMinHpBasisPoints = 4000,
                                ScreeningAllyMinAttackRange = 4,
                                ScreeningEnemyMaxContactRange = 2,
                                ScreeningThreatDistanceBuffer = 2,
                                ScreeningPathBonus = 45,
                            }
                        ),
                    },
                    slots
                ),
            },
            Array.Empty<EnemyAiTransitionRuleJsonDto>()
        );

    private static EnemyAiGenerationSlotJsonDto Slot(
        string slotId,
        int order,
        IReadOnlyList<string> affordances,
        IReadOnlyList<string> families,
        string templateActionId,
        string targetSelector = "nearest_enemy",
        int desiredMinDistance = -1,
        int desiredMaxDistance = -1
    ) =>
        new()
        {
            SlotId = slotId,
            SlotRole = "offense",
            Order = order,
            AllowedAffordances = affordances,
            ActionFamilies = families,
            StyleTemplateActionId = templateActionId,
            ScoreBucketId = "default_offense",
            TargetSelector = targetSelector,
            DesiredMinDistance = desiredMinDistance,
            DesiredMaxDistance = desiredMaxDistance,
            DistanceReference = "target_unit",
            SuppressionPolicy = "suppress_matching_family",
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
}
