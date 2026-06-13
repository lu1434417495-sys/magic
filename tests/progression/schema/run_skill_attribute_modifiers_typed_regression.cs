using System.Collections.Generic;
using System.Reflection;
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
        TestSkillDefAttributeModifiersUseTypedBackingProjection();
        TestOfficialSkillResourcesExposeTypedAttributeModifiers();
        TestAttributeServiceAppliesTypedSkillModifiers();

        Quit(_test.Finish("Skill attribute modifiers typed regression"));
    }

    private void TestSkillDefAttributeModifiersUseTypedBackingProjection()
    {
        _test.Eq(
            typeof(SkillDef).GetProperty(
                "AttributeModifiersTyped",
                BindingFlags.NonPublic | BindingFlags.Instance
            )?.PropertyType,
            typeof(IReadOnlyList<AttributeModifier>),
            "SkillDef.attribute_modifiers 业务态应保持 internal typed list。"
        );

        AttributeModifier hpModifier = Modifier(AttributeService.ToStringName(AttributeIdKind.HpMax), 10);
        CombatSkillDef unrelatedResource = new();
        SkillDef skill = new() { skill_id = "typed_modifier_skill" };
        skill.attribute_modifiers = new GResourceArray { hpModifier, unrelatedResource };

        GResourceArray projected = skill.attribute_modifiers;
        projected.Clear();

        _test.Eq(
            skill.AttributeModifiersTyped.Count,
            1,
            "SkillDef.attribute_modifiers typed 业务态应只接收 AttributeModifier。"
        );
        _test.True(
            ReferenceEquals(skill.AttributeModifiersTyped[0], hpModifier),
            "SkillDef.attribute_modifiers typed 业务态应保留正式 modifier 引用。"
        );
        _test.Eq(
            skill.attribute_modifiers.Count,
            2,
            "SkillDef.attribute_modifiers public property 应返回 fresh projection。"
        );

        AttributeModifier staminaModifier = Modifier(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 5);
        skill.SetAttributeModifiers(new[] { staminaModifier });

        _test.Eq(
            skill.AttributeModifiersTyped.Count,
            1,
            "SkillDef typed setter 应重建 typed modifier list。"
        );
        _test.True(
            ReferenceEquals(skill.AttributeModifiersTyped[0], staminaModifier),
            "SkillDef typed setter 应保留 typed modifier。"
        );
        _test.Eq(
            skill.attribute_modifiers.Count,
            1,
            "SkillDef typed setter 应同步 public projection。"
        );
    }

    private void TestOfficialSkillResourcesExposeTypedAttributeModifiers()
    {
        ProgressionContentRegistry registry = new();
        IReadOnlyDictionary<StringName, SkillDef> skillDefs = registry.GetSkillDefsTyped();

        _test.True(
            skillDefs.TryGetValue("warrior_toughness", out SkillDef warriorToughness)
                && warriorToughness != null,
            "ProgressionContentRegistry 应暴露正式强健资源。"
        );
        if (warriorToughness == null)
            return;

        _test.Eq(
            warriorToughness.AttributeModifiersTyped.Count,
            2,
            "强健应通过 typed modifier list 暴露两条属性修正。"
        );
        _test.Eq(
            warriorToughness.AttributeModifiersTyped[0].attribute_id,
            AttributeService.ToStringName(AttributeIdKind.CharacterHpMaxPercentBonus),
            "强健第一条修正应写入人物生命百分比通道。"
        );
        _test.Eq(
            warriorToughness.AttributeModifiersTyped[0].value,
            20,
            "强健人物生命百分比加成应保留 20。"
        );
        _test.Eq(
            warriorToughness.AttributeModifiersTyped[1].attribute_id,
            AttributeService.ToStringName(AttributeIdKind.StaminaRecoveryPercentBonus),
            "强健第二条修正应写入体力恢复百分比通道。"
        );
        _test.Eq(
            warriorToughness.AttributeModifiersTyped[1].value,
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
                skill_defs = new Dictionary<StringName, SkillDef> { [skill.skill_id] = skill },
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
