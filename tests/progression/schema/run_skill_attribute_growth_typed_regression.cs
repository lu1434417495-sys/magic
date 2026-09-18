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
        SkillDef validSkill = BuildGrowthSchemaSkill(
            "valid_growth_schema_skill",
            "intermediate",
            new GDictionary { ["agility"] = 90, ["perception"] = 30 }
        );
        GStringArray validErrors = ValidateAttributeGrowth(validSkill);
        _test.Eq(
            validErrors.Count,
            0,
            $"合法属性进度配置应通过正式技能校验。 errors={string.Join(" | ", validErrors)}"
        );

        SkillDef invalidTotalSkill = BuildGrowthSchemaSkill(
            "invalid_total_growth_schema_skill",
            "advanced",
            new GDictionary { ["agility"] = 120 }
        );
        AssertHasValidationError(
            invalidTotalSkill,
            "Skill invalid_total_growth_schema_skill attribute_growth_progress total must equal 180 for growth_tier advanced.",
            "advanced 技能属性进度总和必须等于 180。"
        );

        SkillDef invalidAttributeSkill = BuildGrowthSchemaSkill(
            "invalid_attribute_growth_schema_skill",
            "basic",
            new GDictionary { ["hp_max"] = 60 }
        );
        AssertHasValidationError(
            invalidAttributeSkill,
            "Skill invalid_attribute_growth_schema_skill attribute_growth_progress references invalid attribute hp_max.",
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
            stringNameKeySkill,
            "skill.fixture.invalid_input string_name_key_growth_schema_skill/attribute_growth_progress/agility: Map entries must use Variant.String keys.",
            "attribute_growth_progress 旧 StringName key 应在技能导入边界被拒绝。"
        );

        SkillDef nonStringKeySkill = BuildGrowthSchemaSkill(
            "non_string_key_growth_schema_skill",
            "basic",
            new GDictionary { [123] = 60 }
        );
        AssertHasValidationError(
            nonStringKeySkill,
            "skill.fixture.invalid_input non_string_key_growth_schema_skill/attribute_growth_progress: Map entries must use Variant.String keys.",
            "attribute_growth_progress 非 String key 应在技能导入边界被拒绝。"
        );

        SkillDef emptyStringKeySkill = BuildGrowthSchemaSkill(
            "empty_string_key_growth_schema_skill",
            "basic",
            new GDictionary { [""] = 60 }
        );
        AssertHasValidationError(
            emptyStringKeySkill,
            "skill.dto.id.invalid_snake_case empty_string_key_growth_schema_skill/attribute_growth_progress/: Content ID must be canonical lower snake_case ASCII.",
            "attribute_growth_progress 空字符串 key 应在技能导入边界被拒绝。"
        );

        SkillDef nonIntAmountSkill = BuildGrowthSchemaSkill(
            "non_int_growth_schema_skill",
            "basic",
            new GDictionary { ["agility"] = "60" }
        );
        AssertHasValidationError(
            nonIntAmountSkill,
            "skill.fixture.invalid_input non_int_growth_schema_skill/attribute_growth_progress/agility: Map entries must use Int32 integer values.",
            "attribute_growth_progress value 应拒绝字符串数字。"
        );
    }

    private void TestOfficialSkillResourcesExposeTypedAttributeGrowth()
    {
        using var registry = new ProgressionContentRegistry();
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

    /// <summary>
    /// 经生产 fixture 投影与 SkillImportModelValidator 校验，只保留成长配置引入的报错：
    /// 同一技能清空 growth_tier / attribute_growth_progress 后已有的报错会被扣除。
    /// </summary>
    private static GStringArray ValidateAttributeGrowth(SkillDef skill)
    {
        StringName growthTier = skill.growth_tier;
        // getter 返回的是 SkillDef 内部投影，setter 会原地清空它，必须先深拷贝再恢复。
        using GDictionary growthProgress = skill.attribute_growth_progress.Duplicate(true);
        GStringArray baseline;
        using (var emptyProgress = new GDictionary())
        {
            skill.growth_tier = "";
            skill.attribute_growth_progress = emptyProgress;
            try
            {
                baseline = TestSkillDefinitionProjection.ValidateSyntheticSkillFixture(
                    skill,
                    skill.skill_id.ToString()
                );
            }
            finally
            {
                skill.growth_tier = growthTier;
                skill.attribute_growth_progress = growthProgress;
            }
        }
        GStringArray errors = TestSkillDefinitionProjection.ValidateSyntheticSkillFixture(
            skill,
            skill.skill_id.ToString()
        );
        foreach (string message in baseline)
            errors.Remove(message);
        return errors;
    }

    private void AssertHasValidationError(
        SkillDef skill,
        string expectedFragment,
        string message
    )
    {
        GStringArray errors = ValidateAttributeGrowth(skill);
        foreach (string error in errors)
        {
            if ((error ?? "").Contains(expectedFragment))
                return;
        }
        _test.Fail($"{message} expected={expectedFragment} errors={string.Join(" | ", errors)}");
    }
}
