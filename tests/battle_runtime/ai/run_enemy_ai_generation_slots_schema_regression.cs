using Godot;
using System.Collections.Generic;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

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
        EnemyAiStateDef state = BuildState();
        state.generation_slots.Add(
            Slot("offense", 10, new[] { "unit_hostile.damage" }, new[] { "use_unit_skill" }, "template_attack")
        );
        state.generation_slots.Add(
            Slot("close", 20, new[] { "random_chain" }, new[] { "move_to_range" }, "template_move")
        );
        TestResourceOwnership.Own(
            state,
            "enemy_ai_generation_slots_schema.valid_state"
        );

        GStringArray errors = TestResourceOwnership.OwnWrapper(
            state.ValidateSchema("schema_brain", SkillDefinitions()),
            "enemy_ai_generation_slots_schema.valid_errors"
        );
        _test.True(errors.Count == 0, $"合法 generation slots 不应产生 schema error: {FormatErrors(errors)}");
    }

    private void TestDuplicateSlotIdsAndOrdersAreRejected()
    {
        EnemyAiStateDef state = BuildState();
        state.generation_slots.Add(
            Slot("dup", 10, new[] { "unit_hostile.damage" }, new[] { "use_unit_skill" }, "template_attack")
        );
        state.generation_slots.Add(
            Slot("dup", 10, new[] { "ground_control" }, new[] { "use_ground_skill" }, "template_attack")
        );
        TestResourceOwnership.Own(
            state,
            "enemy_ai_generation_slots_schema.duplicate_state"
        );

        GStringArray errors = TestResourceOwnership.OwnWrapper(
            state.ValidateSchema("schema_brain", SkillDefinitions()),
            "enemy_ai_generation_slots_schema.duplicate_errors"
        );
        _test.Eq(errors.Count, 2, $"重复 fixture 应只触发 slot id/order 两条规则: {FormatErrors(errors)}");
        AssertHasError(
            errors,
            "Enemy brain schema_brain state engage declares duplicate generation slot_id dup.",
            "重复 slot_id 应命中 engage state 的精确诊断。"
        );
        AssertHasError(
            errors,
            "Enemy brain schema_brain state engage declares duplicate generation slot order 10.",
            "重复 slot order 应命中 engage state 的精确诊断。"
        );
    }

    private void TestInvalidFamilyAndTemplateAreRejected()
    {
        EnemyAiStateDef state = BuildState();
        state.generation_slots.Add(
            Slot("bad_family", 10, new[] { "unit_hostile.damage" }, new[] { "old_use_skill" }, "template_attack")
        );
        state.generation_slots.Add(
            Slot("missing_template", 20, new[] { "unit_hostile.damage" }, new[] { "use_unit_skill" }, "does_not_exist")
        );
        TestResourceOwnership.Own(
            state,
            "enemy_ai_generation_slots_schema.invalid_family_state"
        );

        GStringArray errors = TestResourceOwnership.OwnWrapper(
            state.ValidateSchema("schema_brain", SkillDefinitions()),
            "enemy_ai_generation_slots_schema.invalid_family_errors"
        );
        _test.Eq(errors.Count, 2, $"invalid family/template fixture 应只触发目标规则: {FormatErrors(errors)}");
        AssertHasError(
            errors,
            "engage generation slot bad_family declares unsupported action_family old_use_skill.",
            "旧 action family alias 应命中 bad_family slot 的精确诊断。"
        );
        AssertHasError(
            errors,
            "engage generation slot missing_template style_template_action_id does_not_exist does not exist in the same state.",
            "缺失 style template action 应命中 missing_template slot 的精确诊断。"
        );
    }

    private void TestSelectorDistanceContractsAreRejected()
    {
        EnemyAiStateDef state = BuildState();
        EnemyAiGenerationSlotDef badSelector = Slot(
            "bad_selector",
            10,
            new[] { "unit_hostile.damage" },
            new[] { "use_unit_skill" },
            "template_attack"
        );
        badSelector.target_selector = "legacy_selector";
        EnemyAiGenerationSlotDef badDistance = Slot(
            "bad_distance",
            20,
            new[] { "random_chain" },
            new[] { "move_to_range" },
            "template_move"
        );
        badDistance.desired_min_distance = 6;
        badDistance.desired_max_distance = 2;
        state.generation_slots.Add(badSelector);
        state.generation_slots.Add(badDistance);
        TestResourceOwnership.Own(
            state,
            "enemy_ai_generation_slots_schema.selector_distance_state"
        );

        GStringArray errors = TestResourceOwnership.OwnWrapper(
            state.ValidateSchema("schema_brain", SkillDefinitions()),
            "enemy_ai_generation_slots_schema.selector_distance_errors"
        );
        _test.Eq(errors.Count, 2, $"selector/distance fixture 应只触发目标规则: {FormatErrors(errors)}");
        AssertHasError(
            errors,
            "engage generation slot bad_selector declares unsupported target_selector legacy_selector.",
            "未知 selector 应命中 bad_selector slot 的精确诊断。"
        );
        AssertHasError(
            errors,
            "engage generation slot bad_distance desired_min_distance cannot exceed desired_max_distance.",
            "min > max 应命中 bad_distance slot 的精确诊断。"
        );
    }

    private static EnemyAiStateDef BuildState()
    {
        var state = new EnemyAiStateDef
        {
            state_id = "engage",
        };
        var attack = TestResourceOwnership.Own(
            new UseUnitSkillAction
            {
                action_id = "template_attack",
                desired_min_distance = 1,
                desired_max_distance = 4,
                DistanceReferenceKind = EnemyAiDistanceReference.TargetUnit,
            },
            "enemy_ai_generation_slots_schema.attack_action"
        );
        attack.skill_ids.Add("dummy_skill");
        var move = TestResourceOwnership.Own(
            new MoveToRangeAction
            {
                action_id = "template_move",
            },
            "enemy_ai_generation_slots_schema.move_action"
        );
        state.actions.Add(attack);
        state.actions.Add(move);
        return state;
    }

    private static EnemyAiGenerationSlotDef Slot(
        StringName slotId,
        int order,
        string[] affordances,
        string[] families,
        StringName templateActionId
    )
    {
        var slot = new EnemyAiGenerationSlotDef
        {
            slot_id = slotId,
            order = order,
            style_template_action_id = templateActionId,
            target_selector = "nearest_enemy",
            score_bucket_id = "default_offense",
            distance_reference = "target_unit",
        };
        foreach (string affordance in affordances)
        {
            slot.allowed_affordances.Add(affordance);
        }
        foreach (string family in families)
        {
            slot.action_families.Add(family);
        }
        return TestResourceOwnership.Own(
            slot,
            $"enemy_ai_generation_slots_schema.slot.{slotId}"
        );
    }

    private static IReadOnlyDictionary<StringName, SkillDefinition> SkillDefinitions() =>
        new Dictionary<StringName, SkillDefinition>
        {
            ["dummy_skill"] = TestSkillDefinitionProjection.BuildSkill(
                "dummy_skill",
                combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                    "dummy_skill",
                    targetMode: "unit"
                )
            ),
        };

    private static string FormatErrors(GStringArray errors) => string.Join("; ", errors);

    private void AssertHasError(GStringArray errors, string fragment, string message)
    {
        foreach (string error in errors)
        {
            if ((error ?? "").Contains(fragment))
                return;
        }
        _test.Fail($"{message} expected={fragment} errors={FormatErrors(errors)}");
    }

}
