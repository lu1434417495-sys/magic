using System.Collections.Generic;
using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_skill_tags_typed_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestOfficialSkillResourcesExposeTypedTags();

        RequestTestExit(_test.Finish("Skill tags typed regression"));
    }

    private void TestOfficialSkillResourcesExposeTypedTags()
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
            skillDefinitions.TryGetValue("warrior_toughness", out SkillDefinition warriorToughness)
                && warriorToughness != null,
            "ProgressionContentRegistry 应暴露正式强健 DTO。"
        );
        if (basicAttack == null || charge == null || warriorToughness == null)
            return;

        _test.True(HasTag(basicAttack, "basic"), "基础攻击应通过 DTO tags 暴露 basic 标签。");
        _test.True(HasTag(charge, "melee"), "冲锋应通过 DTO tags 暴露 melee 标签。");
        _test.True(
            HasTag(warriorToughness, "warrior"),
            "强健应通过 DTO tags 暴露 warrior 标签。"
        );
    }

    private static bool HasTag(SkillDefinition skillDefinition, StringName tag)
    {
        if (skillDefinition == null || tag == "")
            return false;
        foreach (StringName value in skillDefinition.Tags)
        {
            if (value == tag)
                return true;
        }
        return false;
    }
}
