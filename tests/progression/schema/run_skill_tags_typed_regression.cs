using System.Collections.Generic;
using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_skill_tags_typed_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestOfficialSkillResourcesExposeTypedTags();

        Quit(_test.Finish("Skill tags typed regression"));
    }

    private void TestOfficialSkillResourcesExposeTypedTags()
    {
        ProgressionContentRegistry registry = new();
        IReadOnlyDictionary<StringName, SkillDef> skillDefs = registry.GetSkillDefsTyped();

        _test.True(
            skillDefs.TryGetValue("basic_attack", out SkillDef basicAttack) && basicAttack != null,
            "ProgressionContentRegistry 应暴露正式基础攻击资源。"
        );
        _test.True(
            skillDefs.TryGetValue("charge", out SkillDef charge) && charge != null,
            "ProgressionContentRegistry 应暴露正式冲锋资源。"
        );
        _test.True(
            skillDefs.TryGetValue("warrior_toughness", out SkillDef warriorToughness)
                && warriorToughness != null,
            "ProgressionContentRegistry 应暴露正式强健资源。"
        );
        if (basicAttack == null || charge == null || warriorToughness == null)
            return;

        _test.True(basicAttack.HasTag("basic"), "基础攻击应通过 typed tags 暴露 basic 标签。");
        _test.True(charge.HasTag("melee"), "冲锋应通过 typed tags 暴露 melee 标签。");
        _test.True(
            warriorToughness.HasTag("warrior"),
            "强健应通过 typed tags 暴露 warrior 标签。"
        );
    }
}
