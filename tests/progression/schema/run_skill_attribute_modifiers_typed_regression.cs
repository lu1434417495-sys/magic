using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GResourceArray = Godot.Collections.Array<Godot.Resource>;

public partial class run_skill_attribute_modifiers_typed_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestOfficialSkillResourcesExposeTypedAttributeModifiers();
        TestAttributeServiceAppliesTypedSkillModifiers();

        Quit(_test.Finish("Skill attribute modifiers typed regression"));
    }

    private void TestOfficialSkillResourcesExposeTypedAttributeModifiers()
    {
        ProgressionContentRegistry registry = new();
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            registry.GetSkillDefinitionsTyped();

        _test.True(
            skillDefinitions.TryGetValue("warrior_toughness", out SkillDefinition warriorToughness)
                && warriorToughness != null,
            "ProgressionContentRegistry 应暴露正式强健 DTO。"
        );
        if (warriorToughness == null)
            return;

        _test.Eq(
            warriorToughness.AttributeModifiers.Count,
            2,
            "强健应通过 DTO modifier list 暴露两条属性修正。"
        );
        _test.Eq(
            warriorToughness.AttributeModifiers[0].AttributeId,
            AttributeService.ToStringName(AttributeIdKind.CharacterHpMaxPercentBonus),
            "强健第一条修正应写入人物生命百分比通道。"
        );
        _test.Eq(
            warriorToughness.AttributeModifiers[0].Value,
            20,
            "强健人物生命百分比加成应保留 20。"
        );
        _test.Eq(
            warriorToughness.AttributeModifiers[1].AttributeId,
            AttributeService.ToStringName(AttributeIdKind.StaminaRecoveryPercentBonus),
            "强健第二条修正应写入体力恢复百分比通道。"
        );
        _test.Eq(
            warriorToughness.AttributeModifiers[1].Value,
            50,
            "强健体力恢复百分比加成应保留 50。"
        );
    }

    private void TestAttributeServiceAppliesTypedSkillModifiers()
    {
        UnitProgress progress = MakeProgress("typed_modifier_hero");
        SkillDef skill = new()
        {
            skill_id = "typed_modifier_skill",
            skill_type = "passive",
        };
        skill.SetAttributeModifiers(new[] { Modifier("strength", 2) });

        progress.SetSkillProgress(
            new UnitSkillProgress
            {
                skill_id = skill.skill_id,
                is_learned = true,
                skill_level = 1,
            }
        );

        AttributeService service = new();
        service.SetupContext(
            new AttributeSourceContext
            {
                unit_progress = progress,
                skill_definitions = new Dictionary<StringName, SkillDefinition>
                {
                    [skill.skill_id] = SkillDefinition.FromResource(skill),
                },
            }
        );

        _test.Eq(
            service.GetTotalValue("strength"),
            12,
            "AttributeService 应继续通过 typed skill modifier list 叠加技能属性修正。"
        );
    }

    private static UnitProgress MakeProgress(StringName unitId)
    {
        UnitProgress progress = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
        };
        foreach (
            StringName attributeId in new[]
            {
                new StringName("strength"),
                new StringName("agility"),
                new StringName("constitution"),
                new StringName("perception"),
                new StringName("intelligence"),
                new StringName("willpower"),
            }
        )
            progress.unit_base_attributes.SetAttributeValue(attributeId, 10);
        return progress;
    }

    private static AttributeModifier Modifier(
        StringName attributeId,
        int value,
        StringName mode = default,
        int valuePerRank = 0
    )
    {
        return new AttributeModifier
        {
            attribute_id = attributeId,
            mode = mode != ""
                ? mode
                : AttributeModifier.ToStringName(AttributeModifierMode.Flat),
            value = value,
            value_per_rank = valuePerRank,
        };
    }
}
