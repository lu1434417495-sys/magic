using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_combat_effect_shield_schema_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        TestTypedShieldFieldsProject();
        TestShieldFieldValidation();
        RequestTestExit(_test.Finish("Combat effect shield schema regression"));
    }

    private void TestTypedShieldFieldsProject()
    {
        using CombatEffectDef resource = BuildValidShield();
        CombatEffectDefinition definition = CombatEffectDefinition.FromDiagnosticFixture(
            resource,
            "test.shield.typed_projection"
        );
        _test.Eq(definition.ShieldFamily, new StringName("holy_barrier"), "typed shield_family 应投影到 immutable definition。");
        _test.Eq(definition.ShieldAttributeModifierId, new StringName("willpower_modifier"), "typed shield_attribute_modifier_id 应投影到 immutable definition。");
        _test.True(definition.ShieldRollPerTarget, "typed shield_roll_per_target 应投影到 immutable definition。");
    }

    private void TestShieldFieldValidation()
    {
        using SkillContentRegistry registry = new(loadDefaultContent: false);
        using CombatEffectDef valid = BuildValidShield();
        AssertErrors(registry, valid);

        using CombatEffectDef legacy = BuildValidShield();
        legacy.shield_family = "";
        legacy.@params = new GDictionary { ["shield_family"] = "holy_barrier" };
        AssertErrors(
            registry,
            legacy,
            "params.shield_family is unsupported; use CombatEffectDef.shield_family."
        );

        using CombatEffectDef invalidModifier = BuildValidShield();
        invalidModifier.shield_attribute_modifier_id = "spell_proficiency_bonus";
        AssertErrors(
            registry,
            invalidModifier,
            "shield_attribute_modifier_id must name a base ability modifier."
        );

        using CombatEffectDef whitespaceFamily = BuildValidShield();
        whitespaceFamily.shield_family = "   ";
        AssertErrors(
            registry,
            whitespaceFamily,
            "shield_family must not be whitespace."
        );

        using CombatEffectDef missingDice = new()
        {
            effect_type = "shield",
            effect_target_team_filter = "ally",
            power = 5,
            duration_tu = 40,
            shield_roll_per_target = true,
        };
        AssertErrors(
            registry,
            missingDice,
            "shield_roll_per_target requires a valid dice config."
        );

        using CombatEffectDef wrongEffect = new()
        {
            effect_type = "heal",
            effect_target_team_filter = "ally",
            power = 5,
            shield_family = "holy_barrier",
        };
        AssertErrors(
            registry,
            wrongEffect,
            "shield fields are only supported on shield effects."
        );
    }

    private void AssertErrors(
        SkillContentRegistry registry,
        CombatEffectDef effect,
        params string[] expectedFragments
    )
    {
        var errors = new GStringArray();
        registry.AppendEffectValidationErrors(errors, "shield_schema_probe", effect, "test_effect");
        _test.Eq(
            errors.Count,
            expectedFragments?.Length ?? 0,
            $"shield validator 应只报告目标诊断。errors={string.Join(" | ", errors)}"
        );
        string formatted = string.Join(" | ", errors);
        foreach (string fragment in expectedFragments ?? Array.Empty<string>())
        {
            _test.True(formatted.Contains(fragment, StringComparison.Ordinal), $"shield validator 缺少诊断：{fragment} errors={formatted}");
        }
    }

    private static CombatEffectDef BuildValidShield() =>
        new()
        {
            effect_type = "shield",
            effect_target_team_filter = "ally",
            duration_tu = 40,
            dice_count = 1,
            dice_sides = 8,
            dice_bonus = 3,
            shield_family = "holy_barrier",
            shield_attribute_modifier_id = "willpower_modifier",
            shield_roll_per_target = true,
        };
}
