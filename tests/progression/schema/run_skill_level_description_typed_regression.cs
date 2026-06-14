using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_skill_level_description_typed_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestLevelDescriptionSchemaValidationUsesTypedEntries();
        TestLevelDescriptionFormatterUsesTypedConfigs();

        Quit(_test.Finish("Skill level description typed regression"));
    }

    private void TestLevelDescriptionSchemaValidationUsesTypedEntries()
    {
        SkillDef validSkill = new()
        {
            skill_id = "valid_level_description_skill",
            level_description_template = "模板{value}",
            max_level = 1,
            level_description_configs = new GDictionary
            {
                ["0"] = new GDictionary { ["value"] = "零级" },
                ["1"] = new GDictionary { ["value"] = "一级" },
            },
        };
        List<string> validErrors = SkillLevelDescriptionContentRules.CollectValidationErrors(
            validSkill.skill_id,
            validSkill
        );
        _test.Eq(validErrors.Count, 0, "合法 level_description_configs 应通过 typed schema 校验。");

        SkillDef invalidIntKeySkill = new()
        {
            skill_id = "invalid_level_description_int_key_skill",
            level_description_template = "模板{value}",
            max_level = 1,
            level_description_configs = new GDictionary
            {
                [0] = new GDictionary { ["value"] = "零级" },
            },
        };
        List<string> invalidIntKeyErrors =
            SkillLevelDescriptionContentRules.CollectValidationErrors(
                invalidIntKeySkill.skill_id,
                invalidIntKeySkill
            );
        _test.True(
            invalidIntKeyErrors.Count > 0,
            "level_description_configs int key 应被 typed schema entry 拒绝。"
        );

        SkillDef invalidShapeSkill = new()
        {
            skill_id = "invalid_level_description_shape_skill",
            level_description_template = "模板{value}",
            max_level = 1,
            level_description_configs = new GDictionary
            {
                ["0"] = new GDictionary { ["value"] = "零级" },
                ["2"] = "旧格式",
            },
        };
        List<string> invalidErrors = SkillLevelDescriptionContentRules.CollectValidationErrors(
            invalidShapeSkill.skill_id,
            invalidShapeSkill
        );
        _test.True(
            invalidErrors.Count >= 3,
            "非法 level_description_configs shape 应保持非法。"
        );
    }

    private void TestLevelDescriptionFormatterUsesTypedConfigs()
    {
        SkillDef skill = new()
        {
            skill_id = "typed_level_description_formatter_skill",
            level_description_template = "模板{value}{{?bonus}}+{bonus}{{/bonus}}",
            max_level = 1,
        };
        skill.SetLevelDescriptionConfigs(
            new Dictionary<int, Dictionary<string, Variant>>
            {
                [0] = new() { ["value"] = Variant.From("零级") },
                [1] = new()
                {
                    ["value"] = Variant.From("一级"),
                    ["bonus"] = Variant.From(2),
                },
            }
        );

        _test.Eq(
            SkillLevelDescriptionFormatter.BuildLevelDescription(skill, 0, new GDictionary()),
            "模板零级",
            "formatter 应从 typed level description config 读取 0 级描述。"
        );
        _test.Eq(
            SkillLevelDescriptionFormatter.BuildLevelDescription(skill, 1, new GDictionary()),
            "模板一级+2",
            "formatter 应从 typed level description config 读取 1 级描述。"
        );
    }
}
