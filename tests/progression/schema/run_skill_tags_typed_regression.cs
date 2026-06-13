using System.Collections.Generic;
using System.Reflection;
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
        TestSkillDefTagsUseTypedBackingProjection();
        TestOfficialSkillResourcesExposeTypedTags();

        Quit(_test.Finish("Skill tags typed regression"));
    }

    private void TestSkillDefTagsUseTypedBackingProjection()
    {
        _test.Eq(
            typeof(SkillDef).GetProperty("TagsTyped", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.PropertyType,
            typeof(IReadOnlyList<StringName>),
            "SkillDef.tags 业务态应保持 internal typed list。"
        );

        SkillDef skill = new() { skill_id = "typed_tags_skill" };
        skill.tags = new GStringNameArray { "mage", "magic", "mage" };

        GStringNameArray projected = skill.tags;
        projected.Add("spell");

        _test.Eq(skill.TagsTyped.Count, 3, "SkillDef.tags typed backing 应保留正式 tag 序列。");
        _test.Eq(skill.TagsTyped[0], new StringName("mage"), "SkillDef.tags typed backing 应保留原始顺序。");
        _test.Eq(skill.TagsTyped[2], new StringName("mage"), "SkillDef.tags typed backing 不应意外去重。");
        _test.True(skill.HasTag("magic"), "SkillDef.HasTag() 应命中 typed tag。");
        _test.False(skill.HasTag("spell"), "SkillDef.HasTag() 不应被投影副本污染。");
        _test.Eq(skill.tags.Count, 3, "SkillDef.tags public property 应返回 fresh projection。");

        skill.SetTags(new[] { new StringName("heavy"), new StringName("melee") });
        _test.Eq(skill.TagsTyped.Count, 2, "SkillDef.SetTags() 应重建 typed tag list。");
        _test.True(skill.HasTag("heavy"), "SkillDef.SetTags() 应同步 HasTag 行为。");
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
