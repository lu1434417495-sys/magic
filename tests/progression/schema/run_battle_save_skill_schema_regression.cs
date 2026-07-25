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
