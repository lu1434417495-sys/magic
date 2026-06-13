using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GEnemyAiActionArray = Godot.Collections.Array<EnemyAiAction>;
using GGenerationSlotArray = Godot.Collections.Array<EnemyAiGenerationSlotDef>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_enemy_ai_generation_slots_schema_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestTargetSelectorRulesBehavior();
        TestValidGenerationSlotsPassSchema();
        TestDuplicateSlotIdsAndOrdersAreRejected();
        TestInvalidFamilyAndTemplateAreRejected();
        TestSelectorDistanceContractsAreRejected();

        Quit(_test.Finish("Enemy AI generation slots schema regression"));
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
        state.generation_slots = new GGenerationSlotArray
        {
            Slot("offense", 10, new[] { "unit_hostile.damage" }, new[] { "use_unit_skill" }, "template_attack"),
            Slot("close", 20, new[] { "random_chain" }, new[] { "move_to_range" }, "template_move"),
        };

        GStringArray errors = state.ValidateSchema("schema_brain", SkillDefs());
        _test.True(errors.Count == 0, $"合法 generation slots 不应产生 schema error: {FormatErrors(errors)}");
    }

    private void TestDuplicateSlotIdsAndOrdersAreRejected()
    {
        EnemyAiStateDef state = BuildState();
        state.generation_slots = new GGenerationSlotArray
        {
            Slot("dup", 10, new[] { "unit_hostile.damage" }, new[] { "use_unit_skill" }, "template_attack"),
            Slot("dup", 10, new[] { "ground_control" }, new[] { "use_ground_skill" }, "template_attack"),
        };

        GStringArray errors = state.ValidateSchema("schema_brain", SkillDefs());
        _test.True(errors.Count >= 2, $"重复 slot id/order 应被拒绝: {FormatErrors(errors)}");
    }

    private void TestInvalidFamilyAndTemplateAreRejected()
    {
        EnemyAiStateDef state = BuildState();
        state.generation_slots = new GGenerationSlotArray
        {
            Slot("bad_family", 10, new[] { "unit_hostile.damage" }, new[] { "old_use_skill" }, "template_attack"),
            Slot("missing_template", 20, new[] { "unit_hostile.damage" }, new[] { "use_unit_skill" }, "does_not_exist"),
        };

        GStringArray errors = state.ValidateSchema("schema_brain", SkillDefs());
        _test.True(errors.Count >= 2, $"旧 alias/未知 family 与缺失 template action 应被拒绝: {FormatErrors(errors)}");
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
        state.generation_slots = new GGenerationSlotArray { badSelector, badDistance };

        GStringArray errors = state.ValidateSchema("schema_brain", SkillDefs());
        _test.True(errors.Count >= 2, $"未知 selector 与距离契约 min > max 应被拒绝: {FormatErrors(errors)}");
    }

    private static EnemyAiStateDef BuildState()
    {
        var state = new EnemyAiStateDef
        {
            state_id = "engage",
        };
        var attack = new UseUnitSkillAction
        {
            action_id = "template_attack",
            desired_min_distance = 1,
            desired_max_distance = 4,
            DistanceReferenceKind = EnemyAiDistanceReference.TargetUnit,
        };
        attack.skill_ids.Add("dummy_skill");
        var move = new MoveToRangeAction
        {
            action_id = "template_move",
        };
        state.actions = new GEnemyAiActionArray { attack, move };
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
        };
        foreach (string affordance in affordances)
        {
            slot.allowed_affordances.Add(affordance);
        }
        foreach (string family in families)
        {
            slot.action_families.Add(family);
        }
        return slot;
    }

    private static GDictionary SkillDefs() =>
        new()
        {
            ["dummy_skill"] = true,
        };

    private static string FormatErrors(GStringArray errors) => string.Join("; ", errors);

}
