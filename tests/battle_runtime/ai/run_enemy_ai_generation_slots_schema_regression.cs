using System;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GEnemyAiActionArray = Godot.Collections.Array<EnemyAiAction>;
using GGenerationSlotArray = Godot.Collections.Array<EnemyAiGenerationSlotDef>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_enemy_ai_generation_slots_schema_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestTargetSelectorRulesArePlainStaticTypedCSharp();
        TestGenerationSlotValidationTablesAreTypedCSharp();
        TestValidGenerationSlotsPassSchema();
        TestDuplicateSlotIdsAndOrdersAreRejected();
        TestInvalidFamilyAndTemplateAreRejected();
        TestSelectorDistanceContractsAreRejected();

        if (_failures.Count == 0)
        {
            GD.Print("Enemy AI generation slots schema regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Enemy AI generation slots schema regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestTargetSelectorRulesArePlainStaticTypedCSharp()
    {
        Type rulesType = typeof(EnemyAiTargetSelectorRules);
        AssertTrue(rulesType.IsAbstract && rulesType.IsSealed, "EnemyAiTargetSelectorRules 应是 plain static C# helper。");
        AssertTrue(!typeof(GodotObject).IsAssignableFrom(rulesType), "EnemyAiTargetSelectorRules 不应继承 GodotObject/RefCounted。");
        AssertTrue(
            rulesType.GetCustomAttribute<GlobalClassAttribute>() == null,
            "EnemyAiTargetSelectorRules 不应注册 GlobalClass。"
        );
        AssertTrue(
            rulesType.GetMethod("ANY_TARGET_SELECTORS") == null
                && rulesType.GetMethod("ENEMY_TARGET_SELECTORS") == null
                && rulesType.GetMethod("validate_target_selector") == null,
            "EnemyAiTargetSelectorRules 不应保留 Godot Dictionary/Array wrapper。"
        );
        AssertTrue(
            EnemyAiTargetSelectorRules.IsSupportedSelector("nearest_enemy")
                && EnemyAiTargetSelectorRules.IsSupportedSelector("lowest_hp_enemy")
                && EnemyAiTargetSelectorRules.IsSupportedSelector("nearest_role_threat_enemy")
                && EnemyAiTargetSelectorRules.IsSupportedSelector("nearest_ally")
                && EnemyAiTargetSelectorRules.IsSupportedSelector("lowest_hp_ally")
                && EnemyAiTargetSelectorRules.IsSupportedSelector("self"),
            "正式 target selector 集合应覆盖全部现有配置值。"
        );
        AssertTrue(
            !EnemyAiTargetSelectorRules.IsSupportedSelector("legacy_selector"),
            "未知 target selector 不应被兼容。"
        );
        AssertTrue(
            !EnemyAiTargetSelectorRules.IsSupportedSelector("")
                && EnemyAiTargetSelectorRules.IsSupportedSelector("", allowEmpty: true),
            "空 selector 只应在显式允许时通过。"
        );
        AssertTrue(
            EnemyAiTargetSelectorRules.IsEnemyFocusSelector("nearest_enemy")
                && !EnemyAiTargetSelectorRules.IsEnemyFocusSelector("nearest_ally")
                && !EnemyAiTargetSelectorRules.IsEnemyFocusSelector("self"),
            "enemy-focus action 应能复用 typed selector 分类。"
        );
    }

    private void TestGenerationSlotValidationTablesAreTypedCSharp()
    {
        Type slotType = typeof(EnemyAiGenerationSlotDef);
        string[] removedTableMethods =
        {
            "VALID_AFFORDANCES",
            "VALID_ACTION_FAMILIES",
            "VALID_SLOT_ROLES",
            "VALID_TARGET_SELECTORS",
            "VALID_DISTANCE_REFERENCES",
            "VALID_SUPPRESSION_POLICIES",
        };
        foreach (string methodName in removedTableMethods)
        {
            AssertTrue(
                slotType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static) == null,
                $"EnemyAiGenerationSlotDef 不应公开 {methodName} Godot Dictionary validation table。"
            );
        }
        AssertTrue(
            slotType.GetMethod("to_signature") == null,
            "EnemyAiGenerationSlotDef 不应用 Dictionary signature 作为 runtime plan 中间态。"
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

        GStringArray errors = state.validate_schema("schema_brain", SkillDefs());
        AssertTrue(errors.Count == 0, $"合法 generation slots 不应产生 schema error: {FormatErrors(errors)}");
    }

    private void TestDuplicateSlotIdsAndOrdersAreRejected()
    {
        EnemyAiStateDef state = BuildState();
        state.generation_slots = new GGenerationSlotArray
        {
            Slot("dup", 10, new[] { "unit_hostile.damage" }, new[] { "use_unit_skill" }, "template_attack"),
            Slot("dup", 10, new[] { "ground_control" }, new[] { "use_ground_skill" }, "template_attack"),
        };

        GStringArray errors = state.validate_schema("schema_brain", SkillDefs());
        AssertTrue(ContainsError(errors, "duplicate generation slot_id dup"), $"重复 slot_id 应被拒绝: {FormatErrors(errors)}");
        AssertTrue(ContainsError(errors, "duplicate generation slot order 10"), $"重复 slot order 应被拒绝: {FormatErrors(errors)}");
    }

    private void TestInvalidFamilyAndTemplateAreRejected()
    {
        EnemyAiStateDef state = BuildState();
        state.generation_slots = new GGenerationSlotArray
        {
            Slot("bad_family", 10, new[] { "unit_hostile.damage" }, new[] { "old_use_skill" }, "template_attack"),
            Slot("missing_template", 20, new[] { "unit_hostile.damage" }, new[] { "use_unit_skill" }, "does_not_exist"),
        };

        GStringArray errors = state.validate_schema("schema_brain", SkillDefs());
        AssertTrue(ContainsError(errors, "unsupported action_family old_use_skill"), $"旧 alias/未知 family 不应被兼容: {FormatErrors(errors)}");
        AssertTrue(ContainsError(errors, "style_template_action_id does_not_exist does not exist"), $"缺失 template action 应被拒绝: {FormatErrors(errors)}");
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

        GStringArray errors = state.validate_schema("schema_brain", SkillDefs());
        AssertTrue(ContainsError(errors, "unsupported target_selector legacy_selector"), $"未知 selector 应被拒绝: {FormatErrors(errors)}");
        AssertTrue(ContainsError(errors, "desired_min_distance cannot exceed desired_max_distance"), $"距离契约 min > max 应被拒绝: {FormatErrors(errors)}");
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
            distance_reference = UseUnitSkillAction.DISTANCE_REF_TARGET_UNIT(),
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

    private static bool ContainsError(GStringArray errors, string expectedFragment)
    {
        foreach (string error in errors)
        {
            if (error.Contains(expectedFragment, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string FormatErrors(GStringArray errors) => string.Join("; ", errors);

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }
}
