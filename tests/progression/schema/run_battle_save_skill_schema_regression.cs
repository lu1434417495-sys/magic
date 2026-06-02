using System.Collections.Generic;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_save_skill_schema_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestSkillSchemaAcceptsValidSaveFields();
        TestSkillSchemaAcceptsDynamicCasterSpellSaveDc();
        TestSkillSchemaRejectsInvalidSaveFields();

        if (_failures.Count == 0)
        {
            GD.Print("Battle save skill schema regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle save skill schema regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestSkillSchemaAcceptsValidSaveFields()
    {
        using SkillContentRegistry registry = new();
        using CombatEffectDef damageEffect = new()
        {
            effect_type = "damage",
            power = 8,
            damage_tag = "fire",
            save_dc = 12,
            save_ability = "constitution",
            save_tag = BattleSaveContentRules.SAVE_TAG_DRAGON_BREATH,
            save_partial_on_success = true,
        };
        GStringArray damageErrors = new();
        registry._append_effect_validation_errors(
            damageErrors,
            "valid_save_damage",
            damageEffect,
            "test_effect"
        );
        AssertTrue(
            damageErrors.Count == 0,
            "valid damage save fields should pass SkillContentRegistry validation."
        );

        using CombatEffectDef statusEffect = new()
        {
            effect_type = "status",
            status_id = "poisoned",
            save_failure_status_id = "poisoned",
            save_dc = 11,
            save_ability = "constitution",
            save_tag = BattleSaveContentRules.SAVE_TAG_POISON,
        };
        GStringArray statusErrors = new();
        registry._append_effect_validation_errors(
            statusErrors,
            "valid_save_status",
            statusEffect,
            "test_effect"
        );
        AssertTrue(
            statusErrors.Count == 0,
            "valid status save fields should pass SkillContentRegistry validation."
        );
    }

    private void TestSkillSchemaAcceptsDynamicCasterSpellSaveDc()
    {
        using SkillContentRegistry registry = new();
        using CombatEffectDef damageEffect = new()
        {
            effect_type = "damage",
            power = 8,
            damage_tag = "fire",
            save_dc_mode = BattleSaveContentRules.SAVE_DC_MODE_CASTER_SPELL,
            save_dc_source_ability = "intelligence",
            save_ability = "agility",
            save_tag = BattleSaveContentRules.SAVE_TAG_FIREBALL,
            save_partial_on_success = true,
        };
        GStringArray errors = new();
        registry._append_effect_validation_errors(
            errors,
            "valid_dynamic_spell_save_damage",
            damageEffect,
            "test_effect"
        );
        AssertTrue(
            errors.Count == 0,
            "caster_spell save_dc_mode should allow save fields without static save_dc."
        );

        using CombatEffectDef genericMagicEffect = new()
        {
            effect_type = "damage",
            power = 8,
            damage_tag = "fire",
            save_dc_mode = BattleSaveContentRules.SAVE_DC_MODE_CASTER_SPELL,
            save_dc_source_ability = "intelligence",
            save_ability = "agility",
            save_tag = BattleSaveContentRules.SAVE_TAG_MAGIC,
            save_partial_on_success = true,
        };
        GStringArray genericErrors = new();
        registry._append_effect_validation_errors(
            genericErrors,
            "valid_dynamic_generic_magic_save_damage",
            genericMagicEffect,
            "test_effect"
        );
        AssertTrue(
            genericErrors.Count == 0,
            "caster_spell save_dc_mode should accept generic magic save tags."
        );
    }

    private void TestSkillSchemaRejectsInvalidSaveFields()
    {
        using SkillContentRegistry registry = new();
        using CombatEffectDef invalidEffect = new()
        {
            effect_type = "status",
            status_id = "bad_status",
            save_dc = 10,
            save_ability = "fortune",
            save_tag = "cold",
            save_partial_on_success = true,
        };
        GStringArray invalidErrors = new();
        registry._append_effect_validation_errors(
            invalidErrors,
            "invalid_save_status",
            invalidEffect,
            "test_effect"
        );
        AssertTrue(
            HasErrorContaining(invalidErrors, "unsupported save_ability"),
            "invalid save_ability should be rejected."
        );
        AssertTrue(
            HasErrorContaining(invalidErrors, "unsupported save_tag"),
            "invalid save_tag should be rejected."
        );
        AssertTrue(
            HasErrorContaining(
                invalidErrors,
                "save_partial_on_success is only supported on damage effects"
            ),
            "status save_partial_on_success should be rejected."
        );

        using CombatEffectDef noopEffect = new()
        {
            effect_type = "damage",
            power = 4,
            damage_tag = "fire",
            save_tag = BattleSaveContentRules.SAVE_TAG_POISON,
        };
        GStringArray noopErrors = new();
        registry._append_effect_validation_errors(noopErrors, "noop_save", noopEffect, "test_effect");
        AssertTrue(
            HasErrorContaining(noopErrors, "save_tag requires save_dc"),
            "save_tag without save_dc should be rejected."
        );

        using CombatEffectDef badDynamicEffect = new()
        {
            effect_type = "damage",
            power = 4,
            damage_tag = "fire",
            save_dc = 12,
            save_dc_mode = BattleSaveContentRules.SAVE_DC_MODE_CASTER_SPELL,
            save_dc_source_ability = "fortune",
            save_ability = "agility",
            save_tag = BattleSaveContentRules.SAVE_TAG_FIREBALL,
        };
        GStringArray badDynamicErrors = new();
        registry._append_effect_validation_errors(
            badDynamicErrors,
            "bad_dynamic_save",
            badDynamicEffect,
            "test_effect"
        );
        AssertTrue(
            HasErrorContaining(badDynamicErrors, "static save_dc at 0"),
            "caster_spell save_dc_mode should reject static save_dc."
        );
        AssertTrue(
            HasErrorContaining(badDynamicErrors, "unsupported save_dc_source_ability"),
            "caster_spell save_dc_mode should validate source ability."
        );
    }

    private static bool HasErrorContaining(GStringArray errors, string needle)
    {
        foreach (string error in errors)
        {
            if (error.Contains(needle))
            {
                return true;
            }
        }
        return false;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }
}
