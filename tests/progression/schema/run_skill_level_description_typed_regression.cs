using System.Collections.Generic;
using System.Reflection;
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
        TestSkillDefLevelDescriptionConfigsUseTypedBackingProjection();
        TestLevelDescriptionSchemaValidationUsesTypedEntries();
        TestLevelDescriptionFormatterUsesTypedConfigs();

        Quit(_test.Finish("Skill level description typed regression"));
    }

    private void TestSkillDefLevelDescriptionConfigsUseTypedBackingProjection()
    {
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "LevelDescriptionConfigsTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyDictionary<int, Dictionary<string, Variant>>),
            "SkillDef.level_description_configs 业务态应保持 internal typed dictionary。"
        );
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "LevelDescriptionConfigEntriesTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<SkillDef.LevelDescriptionConfigEntryData>),
            "SkillDef.level_description_configs 校验态应保持 internal typed entry list。"
        );

        SkillDef skill = new()
        {
            skill_id = "typed_level_description_skill",
            level_description_template = "模板{value}",
            max_level = 2,
        };
        skill.level_description_configs = new GDictionary
        {
            ["0"] = new GDictionary { ["value"] = "零级" },
            ["1"] = new GDictionary { ["value"] = "一级" },
        };

        GDictionary projection = skill.level_description_configs;
        (projection["1"].AsGodotDictionary())["value"] = "被篡改";

        _test.True(
            skill.LevelDescriptionConfigsTyped.TryGetValue(1, out Dictionary<string, Variant> levelOne)
                && levelOne.TryGetValue("value", out Variant typedValue)
                && typedValue.AsString() == "一级",
            "SkillDef.level_description_configs runtime 业务态应保持 typed dictionary。"
        );
        _test.Eq(
            skill.LevelDescriptionConfigEntriesTyped.Count,
            2,
            "SkillDef.level_description_configs typed setter 应同步 schema entry list。"
        );
        _test.Eq(
            skill.level_description_configs["1"].AsGodotDictionary()["value"].AsString(),
            "一级",
            "SkillDef.level_description_configs public property 应返回 fresh projection。"
        );
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
