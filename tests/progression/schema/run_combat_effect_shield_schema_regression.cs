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
        using CombatEffectDef valid = BuildValidShield();
        AssertErrors(valid);

        using CombatEffectDef legacy = BuildValidShield();
        legacy.shield_family = "";
        legacy.@params = new GDictionary { ["shield_family"] = "holy_barrier" };
        AssertErrors(
            legacy,
            "payload/shield_family: Unknown effect payload member."
        );

        using CombatEffectDef invalidModifier = BuildValidShield();
        invalidModifier.shield_attribute_modifier_id = "spell_proficiency_bonus";
        AssertErrors(
            invalidModifier,
            "effect_defs/0/shield_attribute_modifier_id: Value is not registered by the closed combat effect contract."
        );

        using CombatEffectDef whitespaceFamily = BuildValidShield();
        whitespaceFamily.shield_family = "   ";
        AssertErrors(
            whitespaceFamily,
            "effect_defs/0/shield_family: StringName value must be empty or a canonical snake_case identifier."
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
            wrongEffect,
            "shield fields are only supported on shield effects."
        );
    }

    private void AssertErrors(
        CombatEffectDef effect,
        params string[] expectedFragments
    )
    {
        var errors = new GStringArray();
        errors.AddRange(TestSkillDefinitionProjection.ValidateSyntheticEffectFixture(effect, "shield_schema_probe"));
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
