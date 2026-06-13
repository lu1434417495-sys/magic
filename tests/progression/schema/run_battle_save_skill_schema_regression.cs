using System.Collections.Generic;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_save_skill_schema_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestSkillSchemaAcceptsValidSaveFields();
        TestSkillSchemaAcceptsDynamicCasterSpellSaveDc();
        TestSkillSchemaRejectsInvalidSaveFields();

        Quit(_test.Finish("Battle save skill schema regression"));
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
            save_tag = BattleSaveContentRules.ToStringName(BattleSaveTagKind.DragonBreath),
            save_partial_on_success = true,
        };
        GStringArray damageErrors = new();
        registry.AppendEffectValidationErrors(
            damageErrors,
            "valid_save_damage",
            damageEffect,
            "test_effect"
        );
        _test.True(
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
            save_tag = BattleSaveContentRules.ToStringName(BattleSaveTagKind.Poison),
        };
        GStringArray statusErrors = new();
        registry.AppendEffectValidationErrors(
            statusErrors,
            "valid_save_status",
            statusEffect,
            "test_effect"
        );
        _test.True(
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
            save_dc_mode = BattleSaveContentRules.ToStringName(BattleSaveDcMode.CasterSpell),
            save_dc_source_ability = "intelligence",
            save_ability = "agility",
            save_tag = BattleSaveContentRules.ToStringName(BattleSaveTagKind.Fireball),
            save_partial_on_success = true,
        };
        GStringArray errors = new();
        registry.AppendEffectValidationErrors(
            errors,
            "valid_dynamic_spell_save_damage",
            damageEffect,
            "test_effect"
        );
        _test.True(
            errors.Count == 0,
            "caster_spell save_dc_mode should allow save fields without static save_dc."
        );

        using CombatEffectDef genericMagicEffect = new()
        {
            effect_type = "damage",
            power = 8,
            damage_tag = "fire",
            save_dc_mode = BattleSaveContentRules.ToStringName(BattleSaveDcMode.CasterSpell),
            save_dc_source_ability = "intelligence",
            save_ability = "agility",
            save_tag = BattleSaveContentRules.ToStringName(BattleSaveTagKind.Magic),
            save_partial_on_success = true,
        };
        GStringArray genericErrors = new();
        registry.AppendEffectValidationErrors(
            genericErrors,
            "valid_dynamic_generic_magic_save_damage",
            genericMagicEffect,
            "test_effect"
        );
        _test.True(
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
        registry.AppendEffectValidationErrors(
            invalidErrors,
            "invalid_save_status",
            invalidEffect,
            "test_effect"
        );
        _test.True(
            invalidErrors.Count >= 3,
            "invalid save fields should be rejected."
        );

        using CombatEffectDef noopEffect = new()
        {
            effect_type = "damage",
            power = 4,
            damage_tag = "fire",
            save_tag = BattleSaveContentRules.ToStringName(BattleSaveTagKind.Poison),
        };
        GStringArray noopErrors = new();
        registry.AppendEffectValidationErrors(noopErrors, "noop_save", noopEffect, "test_effect");
        _test.True(noopErrors.Count > 0, "save_tag without save_dc should be rejected.");

        using CombatEffectDef badDynamicEffect = new()
        {
            effect_type = "damage",
            power = 4,
            damage_tag = "fire",
            save_dc = 12,
            save_dc_mode = BattleSaveContentRules.ToStringName(BattleSaveDcMode.CasterSpell),
            save_dc_source_ability = "fortune",
            save_ability = "agility",
            save_tag = BattleSaveContentRules.ToStringName(BattleSaveTagKind.Fireball),
        };
        GStringArray badDynamicErrors = new();
        registry.AppendEffectValidationErrors(
            badDynamicErrors,
            "bad_dynamic_save",
            badDynamicEffect,
            "test_effect"
        );
        _test.True(
            badDynamicErrors.Count >= 2,
            "caster_spell save_dc_mode should reject static save_dc and invalid source ability."
        );
    }
}
