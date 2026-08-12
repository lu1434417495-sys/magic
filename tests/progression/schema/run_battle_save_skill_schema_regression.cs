using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_save_skill_schema_regression : LifecycleTestSceneTree
{
    private const string TempSkillDirectory = "user://skill_level_override_schema_regression";
    private const string TempSkillPath =
        "user://skill_level_override_schema_regression/invalid_override_skill.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestSkillSchemaAcceptsValidSaveFields();
        TestDamageSaveCanApplyFailureStatus();
        TestSkillSchemaAcceptsDynamicCasterSpellSaveDc();
        TestSkillSchemaRejectsInvalidSaveFields();
        TestSkillSchemaRejectsInvalidSaveTagLists();
        TestLevelOverridesRejectNonIntFields();

        RequestTestExit(_test.Finish("Battle save skill schema regression"));
    }

    private void TestSkillSchemaAcceptsValidSaveFields()
    {
        using SkillContentRegistry registry = new(new TestContentResourceLoader(), loadDefaultContent: false);
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
            save_advantage_tags = new GStringNameArray { "poison" },
            save_disadvantage_tags = new GStringNameArray { "magic" },
            save_immunity_tags = new GStringNameArray { "sleep" },
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

    private void TestDamageSaveCanApplyFailureStatus()
    {
        using SkillContentRegistry registry = new(new TestContentResourceLoader(), loadDefaultContent: false);
        using CombatEffectDef damageEffect = new()
        {
            effect_type = "damage",
            damage_tag = "thunder",
            dice_count = 2,
            dice_sides = 6,
            save_dc = 14,
            save_ability = "constitution",
            save_tag = BattleSaveContentRules.ToStringName(BattleSaveTagKind.Magic),
            save_partial_on_success = true,
            save_failure_status_id = "prone",
            duration_tu = 50,
        };
        GStringArray errors = new();
        registry.AppendEffectValidationErrors(
            errors,
            "damage_save_failure_status",
            damageEffect,
            "test_effect"
        );
        _test.True(
            errors.Count == 0,
            $"damage effect should support save_failure_status_id using the same save result. errors={string.Join(" | ", errors)}"
        );
    }

    private void TestSkillSchemaAcceptsDynamicCasterSpellSaveDc()
    {
        using SkillContentRegistry registry = new(new TestContentResourceLoader(), loadDefaultContent: false);
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
        using SkillContentRegistry registry = new(new TestContentResourceLoader(), loadDefaultContent: false);

        using CombatEffectDef validStatusBaseline = BuildValidStatusSaveEffect();
        AssertExactErrors(
            ValidateEffect(registry, "valid_status_baseline", validStatusBaseline),
            "valid status-save baseline"
        );

        using CombatEffectDef invalidAbilityEffect = BuildValidStatusSaveEffect();
        invalidAbilityEffect.save_ability = "fortune";
        AssertExactErrors(
            ValidateEffect(registry, "invalid_save_ability", invalidAbilityEffect),
            "unsupported save ability",
            "Skill invalid_save_ability effect test_effect uses unsupported save_ability fortune."
        );

        using CombatEffectDef invalidTagEffect = BuildValidStatusSaveEffect();
        invalidTagEffect.save_tag = "cold";
        AssertExactErrors(
            ValidateEffect(registry, "invalid_save_tag", invalidTagEffect),
            "unsupported save tag",
            "Skill invalid_save_tag effect test_effect uses unsupported save_tag cold."
        );

        using CombatEffectDef invalidPartialEffect = BuildValidStatusSaveEffect();
        invalidPartialEffect.save_partial_on_success = true;
        AssertExactErrors(
            ValidateEffect(registry, "invalid_save_partial", invalidPartialEffect),
            "status save partial-on-success",
            "Skill invalid_save_partial effect test_effect save_partial_on_success is only supported on damage effects."
        );

        using CombatEffectDef noSaveBaseline = BuildPlainDamageEffect();
        AssertExactErrors(
            ValidateEffect(registry, "no_save_baseline", noSaveBaseline),
            "damage effect without save fields baseline"
        );

        using CombatEffectDef tagWithoutDcEffect = BuildPlainDamageEffect();
        tagWithoutDcEffect.save_tag = BattleSaveContentRules.ToStringName(
            BattleSaveTagKind.Poison
        );
        AssertExactErrors(
            ValidateEffect(registry, "save_tag_without_dc", tagWithoutDcEffect),
            "save tag without save DC",
            "Skill save_tag_without_dc effect test_effect save_tag requires save_dc >= 1 or caster_spell save_dc_mode."
        );

        using CombatEffectDef validDynamicBaseline = BuildValidDynamicSaveEffect();
        AssertExactErrors(
            ValidateEffect(registry, "valid_dynamic_baseline", validDynamicBaseline),
            "caster-spell save baseline"
        );

        using CombatEffectDef dynamicStaticDcEffect = BuildValidDynamicSaveEffect();
        dynamicStaticDcEffect.save_dc = 12;
        AssertExactErrors(
            ValidateEffect(registry, "dynamic_static_dc", dynamicStaticDcEffect),
            "caster-spell save with static DC",
            "Skill dynamic_static_dc effect test_effect caster_spell save_dc_mode must leave static save_dc at 0."
        );

        using CombatEffectDef dynamicInvalidSourceEffect = BuildValidDynamicSaveEffect();
        dynamicInvalidSourceEffect.save_dc_source_ability = "fortune";
        AssertExactErrors(
            ValidateEffect(registry, "dynamic_invalid_source", dynamicInvalidSourceEffect),
            "caster-spell save with invalid source ability",
            "Skill dynamic_invalid_source effect test_effect uses unsupported save_dc_source_ability fortune."
        );
    }

    private static CombatEffectDef BuildValidStatusSaveEffect()
    {
        return new CombatEffectDef
        {
            effect_type = "status",
            status_id = "test_status",
            save_dc = 10,
            save_ability = "constitution",
            save_tag = BattleSaveContentRules.ToStringName(BattleSaveTagKind.Poison),
        };
    }

    private static CombatEffectDef BuildPlainDamageEffect()
    {
        return new CombatEffectDef
        {
            effect_type = "damage",
            power = 4,
            damage_tag = "fire",
        };
    }

    private static CombatEffectDef BuildValidDynamicSaveEffect()
    {
        return new CombatEffectDef
        {
            effect_type = "damage",
            power = 4,
            damage_tag = "fire",
            save_dc_mode = BattleSaveContentRules.ToStringName(BattleSaveDcMode.CasterSpell),
            save_dc_source_ability = "intelligence",
            save_ability = "agility",
            save_tag = BattleSaveContentRules.ToStringName(BattleSaveTagKind.Fireball),
        };
    }

    private static GStringArray ValidateEffect(
        SkillContentRegistry registry,
        StringName skillId,
        CombatEffectDef effect
    )
    {
        GStringArray errors = new();
        registry.AppendEffectValidationErrors(
            errors,
            skillId,
            effect,
            "test_effect"
        );
        return errors;
    }

    private void AssertExactErrors(
        GStringArray actualErrors,
        string label,
        params string[] expectedErrors
    )
    {
        _test.Eq(
            actualErrors.Count,
            expectedErrors.Length,
            $"{label} should produce only its target diagnostics. errors={string.Join(" | ", actualErrors)}"
        );
        int comparableCount = System.Math.Min(actualErrors.Count, expectedErrors.Length);
        for (int index = 0; index < comparableCount; index++)
        {
            _test.Eq(
                actualErrors[index],
                expectedErrors[index],
                $"{label} diagnostic {index} should match exactly."
            );
        }
    }

    private void TestSkillSchemaRejectsInvalidSaveTagLists()
    {
        using SkillContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        using CombatEffectDef invalidEffect = new()
        {
            effect_type = "status",
            status_id = "invalid_save_tag_lists",
            save_advantage_tags = new GStringNameArray { "poison", "poison" },
            save_disadvantage_tags = new GStringNameArray { "sleep_advantage" },
            save_immunity_tags = new GStringNameArray { "not_a_save_tag" },
        };
        GStringArray errors = new();

        registry.AppendEffectValidationErrors(
            errors,
            "invalid_save_tag_lists",
            invalidEffect,
            "test_effect"
        );
        string formattedErrors = string.Join(" | ", errors);

        _test.True(
            formattedErrors.Contains("duplicates save tag poison"),
            $"技能 effect save tag 列表应拒绝重复值。 errors={formattedErrors}"
        );
        _test.True(
            formattedErrors.Contains("removed suffix syntax"),
            $"技能 effect save tag 列表应拒绝旧后缀写法。 errors={formattedErrors}"
        );
        _test.True(
            formattedErrors.Contains("not_a_save_tag")
                && formattedErrors.Contains("not a supported save tag"),
            $"技能 effect save tag 列表应拒绝未知值。 errors={formattedErrors}"
        );
    }

    private void TestLevelOverridesRejectNonIntFields()
    {
        CleanupTempSkillDirectory();
        _test.Eq(
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(TempSkillDirectory)),
            Error.Ok,
            "应能创建 skill override schema 临时目录。"
        );

        SkillDef skillDef = BuildSkillWithInvalidLevelOverrides();
        _test.Eq(
            ResourceSaver.Save(skillDef, TempSkillPath),
            Error.Ok,
            "应能写入 skill override schema 测试资源。"
        );

        using SkillContentRegistry registry = new(new TestContentResourceLoader(), loadDefaultContent: false);
        registry.LoadFromDirectory(TempSkillDirectory);
        GStringArray errors = registry.Validate();
        string formattedErrors = string.Join(" | ", errors);

        _test.True(
            formattedErrors.Contains("level override 1.range_value must be an int."),
            $"range_value 非 int override 应被拒绝。 errors={formattedErrors}"
        );
        _test.True(
            formattedErrors.Contains("level override 1.attack_roll_bonus must be an int."),
            $"attack_roll_bonus 非 int override 应被拒绝。 errors={formattedErrors}"
        );
        _test.True(
            formattedErrors.Contains("level override 1.area_value must be an int."),
            $"area_value 非 int override 应被拒绝。 errors={formattedErrors}"
        );
        _test.True(
            formattedErrors.Contains("level override 1.max_target_count must be an int."),
            $"max_target_count 非 int override 应被拒绝。 errors={formattedErrors}"
        );

        CleanupTempSkillDirectory();
    }

    private static SkillDef BuildSkillWithInvalidLevelOverrides()
    {
        const string skillId = "invalid_level_override_types_skill";
        return TestResourceOwnership.Own(
            new SkillDef
            {
                skill_id = skillId,
                display_name = "Invalid Level Override Types",
                icon_id = skillId,
                skill_type = "active",
                max_level = 1,
                mastery_curve = new[] { 1 },
                combat_profile = new CombatSkillDef
                {
                    skill_id = skillId,
                    target_mode = "unit",
                    target_team_filter = "enemy",
                    target_selection_mode = "single_unit",
                    selection_order_mode = "stable",
                    range_value = 1,
                    max_target_count = 1,
                    level_overrides = new GDictionary
                    {
                        [1] = new GDictionary
                        {
                            ["range_value"] = "3",
                            ["attack_roll_bonus"] = true,
                            ["area_value"] = 1.5,
                            ["max_target_count"] = "two",
                        },
                    },
                },
            },
            "BattleSaveSkillSchema.BuildSkillWithInvalidLevelOverrides"
        );
    }

    private static void CleanupTempSkillDirectory()
    {
        string absoluteFilePath = ProjectSettings.GlobalizePath(TempSkillPath);
        if (FileAccess.FileExists(absoluteFilePath))
            DirAccess.RemoveAbsolute(absoluteFilePath);
        string absoluteDirectoryPath = ProjectSettings.GlobalizePath(TempSkillDirectory);
        if (DirAccess.DirExistsAbsolute(absoluteDirectoryPath))
            DirAccess.RemoveAbsolute(absoluteDirectoryPath);
    }
}
