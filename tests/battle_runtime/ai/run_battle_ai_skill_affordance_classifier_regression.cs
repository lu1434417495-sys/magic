using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_skill_affordance_classifier_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestUnitDamageSkillMapsToHostileUnitAffordance();
        TestAllyHealSkillMapsToSupportAffordance();
        TestGroundControlSkillMapsToGroundFamily();
        TestRandomChainSkillEmitsChainAndPositioningFamilies();
        TestMultiUnitSkillEmitsSkillAndPositioningFamilies();
        TestChargePathVariantEmitsChargePathFamily();
        TestPassiveSkillIsNotGeneratable();

        Quit(_test.Finish("Battle AI skill affordance classifier regression"));
    }

    private void TestUnitDamageSkillMapsToHostileUnitAffordance()
    {
        SkillDefinition skill = BuildSkill("bolt", "unit", "enemy", Effect("damage"));
        BattleAiSkillAffordanceRecord record = Classify(skill);
        _test.True(record.is_generatable, "敌方单体伤害技能应可生成。");
        AssertListHas(record.affordances, "unit_hostile.damage", "敌方单体伤害技能应标为 unit_hostile.damage。");
        AssertListHas(record.action_families, "use_unit_skill", "敌方单体伤害技能应生成 use_unit_skill family。");
    }

    private void TestAllyHealSkillMapsToSupportAffordance()
    {
        SkillDefinition skill = BuildSkill("mend", "unit", "ally", Effect("heal", "ally"));
        BattleAiSkillAffordanceRecord record = Classify(skill);
        _test.True(record.is_generatable, "友方治疗技能应可生成。");
        AssertListHas(record.affordances, "ally_heal", "友方治疗技能应标为 ally_heal。");
        AssertListHas(record.action_families, "use_unit_skill", "友方治疗技能仍应使用 unit skill action family。");
    }

    private void TestGroundControlSkillMapsToGroundFamily()
    {
        SkillDefinition skill = BuildSkill("mud_patch", "ground", "enemy", Effect("terrain", "enemy"));
        BattleAiSkillAffordanceRecord record = Classify(skill);
        _test.True(record.is_generatable, "地面控制技能应可生成。");
        AssertListHas(record.affordances, "ground_control", "地面控制技能应标为 ground_control。");
        AssertListHas(record.action_families, "use_ground_skill", "地面控制技能应生成 use_ground_skill family。");
    }

    private void TestRandomChainSkillEmitsChainAndPositioningFamilies()
    {
        SkillDefinition skill = BuildSkill(
            "chain_arc",
            "unit",
            "enemy",
            Effect("chain_damage"),
            targetSelectionMode: BattleTypedNames.ToStringName(BattleTargetSelectionMode.RandomChain),
            maxHitsPerTarget: 2
        );

        BattleAiSkillAffordanceRecord record = Classify(skill);
        AssertListHas(record.affordances, "random_chain", "随机链技能应标为 random_chain。");
        AssertListHas(record.action_families, "use_random_chain_skill", "随机链技能应生成 chain action family。");
        AssertListHas(record.action_families, "move_to_range", "随机链技能应可生成 companion range move。");
    }

    private void TestMultiUnitSkillEmitsSkillAndPositioningFamilies()
    {
        SkillDefinition skill = BuildSkill(
            "wide_shot",
            "unit",
            "enemy",
            Effect("damage"),
            targetSelectionMode: BattleTypedNames.ToStringName(BattleTargetSelectionMode.MultiUnit),
            minTargetCount: 2
        );

        BattleAiSkillAffordanceRecord record = Classify(skill);
        AssertListHas(record.affordances, "multi_unit", "多目标技能应标为 multi_unit。");
        AssertListHas(record.action_families, "use_multi_unit_skill", "多目标技能应生成 multi-unit action family。");
        AssertListHas(record.action_families, "move_to_multi_unit_skill_position", "多目标技能应可生成 companion multi-unit move。");
    }

    private void TestChargePathVariantEmitsChargePathFamily()
    {
        SkillDefinition skill = BuildSkill(
            "trample",
            "unit",
            "enemy",
            Effect("damage"),
            castVariants: new[]
            {
                TestSkillDefinitionProjection.BuildCastVariant(
                    "charge_line",
                    1,
                    new[] { Effect("charge"), Effect("path_step_aoe") }
                ),
            }
        );

        BattleAiSkillAffordanceRecord record = Classify(skill);
        AssertListHas(record.affordances, "charge_path_aoe", "带 path_step_aoe 的冲锋变体应标为 charge_path_aoe。");
        AssertListHas(record.action_families, "use_charge_path_aoe", "带 path_step_aoe 的冲锋变体应生成 charge path action family。");
    }

    private void TestPassiveSkillIsNotGeneratable()
    {
        SkillDefinition skill = BuildSkill(
            "passive_aura",
            "unit",
            "ally",
            Effect("status", "ally"),
            skillType: "passive"
        );

        BattleAiSkillAffordanceRecord record = Classify(skill);
        _test.True(!record.is_generatable, "被动技能不应进入 AI action 生成。");
        _test.Eq(record.skip_reason, "passive_or_no_combat", "被动技能应给出稳定 skip reason。");
    }

    private static BattleAiSkillAffordanceRecord Classify(SkillDefinition skillDefinition)
    {
        var classifier = new BattleAiSkillAffordanceClassifier();
        return classifier.ClassifySkill(skillDefinition, 1);
    }

    private static SkillDefinition BuildSkill(
        StringName skillId,
        StringName targetMode,
        StringName targetFilter,
        CombatEffectDefinition effectDef,
        StringName targetSelectionMode = default,
        int minTargetCount = 0,
        int maxHitsPerTarget = 0,
        IReadOnlyList<CombatCastVariantDefinition> castVariants = null,
        StringName skillType = default
    )
    {
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            skillId.ToString(),
            TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { effectDef },
                targetMode: targetMode,
                targetTeamFilter: targetFilter,
                rangePattern: "fixed",
                rangeValue: 5,
                targetSelectionMode: targetSelectionMode,
                minTargetCount: minTargetCount,
                maxHitsPerTarget: maxHitsPerTarget,
                castVariants: castVariants
            ),
            skillType: skillType == "" ? (StringName)"active" : skillType
        );
    }

    private static CombatEffectDefinition Effect(StringName effectType, StringName effectFilter = default)
    {
        StringName statusId = default;
        StringName terrainEffectId = default;
        if (effectType == new StringName("status"))
        {
            statusId = "rooted";
        }
        if (effectType == new StringName("terrain"))
        {
            terrainEffectId = "mud";
        }
        return TestSkillDefinitionProjection.BuildEffect(
            effectType,
            effectTargetTeamFilter: effectFilter,
            statusId: statusId,
            terrainEffectId: terrainEffectId
        );
    }

    private void AssertListHas(
        System.Collections.Generic.IEnumerable<StringName> values,
        StringName expected,
        string message
    )
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value == expected)
            {
                return;
            }
        }
        _test.Fail(message);
    }

}
