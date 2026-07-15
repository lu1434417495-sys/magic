using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_skill_attribute_growth_typed_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestAttributeGrowthSchemaValidation();
        TestOfficialSkillResourcesExposeTypedAttributeGrowth();

        RequestTestExit(_test.Finish("Skill attribute growth typed regression"));
    }

    private void TestAttributeGrowthSchemaValidation()
    {
        using SkillContentRegistry registry = new(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );

        SkillDef validSkill = BuildGrowthSchemaSkill(
            "valid_growth_schema_skill",
            "intermediate",
            new GDictionary { ["agility"] = 90, ["perception"] = 30 }
        );
        GStringArray validErrors = new();
        registry.AppendAttributeGrowthValidationErrors(
            validErrors,
            validSkill.skill_id,
            validSkill
        );
        _test.Eq(validErrors.Count, 0, "合法属性进度配置应通过 SkillContentRegistry 校验。");

        SkillDef invalidTotalSkill = BuildGrowthSchemaSkill(
            "invalid_total_growth_schema_skill",
            "advanced",
            new GDictionary { ["agility"] = 120 }
        );
        AssertHasValidationError(
            registry,
            invalidTotalSkill,
            "advanced 技能属性进度总和必须等于 180。"
        );

        SkillDef invalidAttributeSkill = BuildGrowthSchemaSkill(
            "invalid_attribute_growth_schema_skill",
            "basic",
            new GDictionary { ["hp_max"] = 60 }
        );
        AssertHasValidationError(
            registry,
            invalidAttributeSkill,
            "属性进度配置只能引用六项基础属性。"
        );

        SkillDef stringNameKeySkill = BuildGrowthSchemaSkill(
            "string_name_key_growth_schema_skill",
            "basic",
            new GDictionary { [new StringName("agility")] = 60 }
        );
        _test.False(
            stringNameKeySkill.AttributeGrowthProgressTyped.ContainsKey("agility"),
            "StringName key 不应进入 SkillDef typed attribute-growth 业务态。"
        );
        AssertHasValidationError(
            registry,
            stringNameKeySkill,
            "attribute_growth_progress 旧 StringName key 应被 SkillContentRegistry 静态拒绝。"
        );

        SkillDef nonStringKeySkill = BuildGrowthSchemaSkill(
            "non_string_key_growth_schema_skill",
            "basic",
            new GDictionary { [123] = 60 }
        );
        AssertHasValidationError(
            registry,
            nonStringKeySkill,
            "attribute_growth_progress 非 String key 应被 SkillContentRegistry 静态拒绝。"
        );

        SkillDef emptyStringKeySkill = BuildGrowthSchemaSkill(
            "empty_string_key_growth_schema_skill",
            "basic",
            new GDictionary { [""] = 60 }
        );
        AssertHasValidationError(
            registry,
            emptyStringKeySkill,
            "attribute_growth_progress 空字符串 key 应被 SkillContentRegistry 静态拒绝。"
        );

        SkillDef nonIntAmountSkill = BuildGrowthSchemaSkill(
            "non_int_growth_schema_skill",
            "basic",
            new GDictionary { ["agility"] = "60" }
        );
        AssertHasValidationError(
            registry,
            nonIntAmountSkill,
            "attribute_growth_progress value 应拒绝字符串数字。"
        );
    }

    private void TestOfficialSkillResourcesExposeTypedAttributeGrowth()
    {
        ProgressionContentRegistry registry = new(new TestContentResourceLoader());
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            registry.GetSkillDefinitionsTyped();

        _test.True(
            skillDefinitions.TryGetValue("basic_attack", out SkillDefinition basicAttack)
                && basicAttack != null,
            "ProgressionContentRegistry 应暴露正式基础攻击 DTO。"
        );
        _test.True(
            skillDefinitions.TryGetValue("charge", out SkillDefinition charge) && charge != null,
            "ProgressionContentRegistry 应暴露正式冲锋 DTO。"
        );
        _test.True(
            skillDefinitions.TryGetValue("archer_multishot", out SkillDefinition archerMultishot)
                && archerMultishot != null,
            "ProgressionContentRegistry 应暴露正式连珠箭 DTO。"
        );
        if (basicAttack == null || charge == null || archerMultishot == null)
            return;

        _test.Eq(
            basicAttack.AttributeGrowthProgress.Count,
            0,
            "基础攻击不应直接承接属性成长。"
        );
        _test.Eq(
            charge.AttributeGrowthProgress["agility"],
            100,
            "冲锋敏捷成长进度应来自正式资源。"
        );
        _test.Eq(
            charge.AttributeGrowthProgress["strength"],
            20,
            "冲锋力量成长进度应来自正式资源。"
        );
        _test.Eq(
            archerMultishot.AttributeGrowthProgress["agility"],
            80,
            "连珠箭满级应提供 80 点敏捷成长进度。"
        );
        _test.Eq(
            archerMultishot.AttributeGrowthProgress["strength"],
            40,
            "连珠箭满级应提供 40 点力量成长进度。"
        );
        _test.False(
            archerMultishot.AttributeGrowthProgress.ContainsKey("perception"),
            "连珠箭不应再提供感知成长进度。"
        );
    }

    private static SkillDef BuildGrowthSchemaSkill(
        StringName skillId,
        StringName growthTier,
        GDictionary attributeGrowthProgress
    )
    {
        SkillDef skill = new()
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            max_level = 3,
            growth_tier = growthTier,
        };
        skill.attribute_growth_progress = attributeGrowthProgress?.Duplicate(true) ?? new GDictionary();
        return skill;
    }

    private void AssertHasValidationError(
        SkillContentRegistry registry,
        SkillDef skill,
        string message
    )
    {
        GStringArray errors = new();
        registry.AppendAttributeGrowthValidationErrors(errors, skill.skill_id, skill);
        _test.True(errors.Count > 0, message);
    }
}
