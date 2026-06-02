using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_ai_skill_affordance_classifier_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestClassifierIsPlainTypedService();
        TestAffordanceRecordIsPlainTypedDto();
        TestGenerationSlotUsesTypedAffordanceRecord();
        TestUnitDamageSkillMapsToHostileUnitAffordance();
        TestAllyHealSkillMapsToSupportAffordance();
        TestGroundControlSkillMapsToGroundFamily();
        TestRandomChainSkillEmitsChainAndPositioningFamilies();
        TestMultiUnitSkillEmitsSkillAndPositioningFamilies();
        TestChargePathVariantEmitsChargePathFamily();
        TestPassiveSkillIsNotGeneratable();

        if (_failures.Count == 0)
        {
            GD.Print("Battle AI skill affordance classifier regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle AI skill affordance classifier regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestClassifierIsPlainTypedService()
    {
        Type classifierType = typeof(BattleAiSkillAffordanceClassifier);
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(classifierType),
            "BattleAiSkillAffordanceClassifier 不应继承 GodotObject/RefCounted。"
        );
        AssertTrue(
            classifierType.GetCustomAttribute<GlobalClassAttribute>() == null,
            "BattleAiSkillAffordanceClassifier 不应注册 GlobalClass。"
        );
        AssertTrue(
            classifierType.GetMethod("classify_skill") == null,
            "BattleAiSkillAffordanceClassifier 不应保留 GDScript-style classify_skill Dictionary API。"
        );
    }

    private void TestAffordanceRecordIsPlainTypedDto()
    {
        Type recordType = typeof(BattleAiSkillAffordanceRecord);
        AssertTrue(recordType.IsSealed, "BattleAiSkillAffordanceRecord 应保持 sealed typed DTO。");
        AssertTrue(
            !typeof(GodotObject).IsAssignableFrom(recordType),
            "BattleAiSkillAffordanceRecord 不应继承 GodotObject/RefCounted。"
        );
        AssertTrue(
            recordType.GetCustomAttribute<GlobalClassAttribute>() == null,
            "BattleAiSkillAffordanceRecord 不应注册 GlobalClass。"
        );
        AssertTrue(
            recordType.GetMethod("FromDictionary") == null
                && recordType.GetMethod("ToDictionary") == null
                && recordType.GetMethod("IsTypedKey") == null,
            "BattleAiSkillAffordanceRecord 不应保留 Godot Dictionary schema/conversion API。"
        );
        AssertNoGodotDynamicBoundaryTypes(
            recordType,
            "BattleAiSkillAffordanceRecord"
        );
    }

    private void TestGenerationSlotUsesTypedAffordanceRecord()
    {
        AssertTrue(
            typeof(EnemyAiGenerationSlotDef).GetMethod("matches_affordance") == null,
            "EnemyAiGenerationSlotDef 不应保留 matches_affordance(Dictionary) wrapper。"
        );
    }

    private void TestUnitDamageSkillMapsToHostileUnitAffordance()
    {
        BattleAiSkillAffordanceRecord record = Classify(
            BuildSkill("bolt", "unit", "enemy", Effect("damage"))
        );
        AssertTrue(record.is_generatable, "敌方单体伤害技能应可生成。");
        AssertContains(record.affordances, "unit_hostile.damage", "敌方单体伤害技能应标为 unit_hostile.damage。");
        AssertContains(record.action_families, "use_unit_skill", "敌方单体伤害技能应生成 use_unit_skill family。");
    }

    private void TestAllyHealSkillMapsToSupportAffordance()
    {
        BattleAiSkillAffordanceRecord record = Classify(
            BuildSkill("mend", "unit", "ally", Effect("heal", "ally"))
        );
        AssertTrue(record.is_generatable, "友方治疗技能应可生成。");
        AssertContains(record.affordances, "ally_heal", "友方治疗技能应标为 ally_heal。");
        AssertContains(record.action_families, "use_unit_skill", "友方治疗技能仍应使用 unit skill action family。");
    }

    private void TestGroundControlSkillMapsToGroundFamily()
    {
        BattleAiSkillAffordanceRecord record = Classify(
            BuildSkill("mud_patch", "ground", "enemy", Effect("terrain", "enemy"))
        );
        AssertTrue(record.is_generatable, "地面控制技能应可生成。");
        AssertContains(record.affordances, "ground_control", "地面控制技能应标为 ground_control。");
        AssertContains(record.action_families, "use_ground_skill", "地面控制技能应生成 use_ground_skill family。");
    }

    private void TestRandomChainSkillEmitsChainAndPositioningFamilies()
    {
        SkillDef skill = BuildSkill("chain_arc", "unit", "enemy", Effect("chain_damage"));
        CombatSkillDef combatProfile = (CombatSkillDef)skill.combat_profile;
        combatProfile.target_selection_mode = "random_chain";
        combatProfile.max_hits_per_target = 2;

        BattleAiSkillAffordanceRecord record = Classify(skill);
        AssertContains(record.affordances, "random_chain", "随机链技能应标为 random_chain。");
        AssertContains(record.action_families, "use_random_chain_skill", "随机链技能应生成 chain action family。");
        AssertContains(record.action_families, "move_to_range", "随机链技能应可生成 companion range move。");
    }

    private void TestMultiUnitSkillEmitsSkillAndPositioningFamilies()
    {
        SkillDef skill = BuildSkill("wide_shot", "unit", "enemy", Effect("damage"));
        CombatSkillDef combatProfile = (CombatSkillDef)skill.combat_profile;
        combatProfile.target_selection_mode = "multi_unit";
        combatProfile.min_target_count = 2;

        BattleAiSkillAffordanceRecord record = Classify(skill);
        AssertContains(record.affordances, "multi_unit", "多目标技能应标为 multi_unit。");
        AssertContains(record.action_families, "use_multi_unit_skill", "多目标技能应生成 multi-unit action family。");
        AssertContains(record.action_families, "move_to_multi_unit_skill_position", "多目标技能应可生成 companion multi-unit move。");
    }

    private void TestChargePathVariantEmitsChargePathFamily()
    {
        SkillDef skill = BuildSkill("trample", "unit", "enemy", Effect("damage"));
        var option = new CombatCastVariantDef
        {
            variant_id = "charge_line",
            min_skill_level = 1,
        };
        option.effect_defs.Add(Effect("charge"));
        option.effect_defs.Add(Effect("path_step_aoe"));
        ((CombatSkillDef)skill.combat_profile).cast_variants.Add(option);

        BattleAiSkillAffordanceRecord record = Classify(skill);
        AssertContains(record.affordances, "charge_path_aoe", "带 path_step_aoe 的冲锋变体应标为 charge_path_aoe。");
        AssertContains(record.action_families, "use_charge_path_aoe", "带 path_step_aoe 的冲锋变体应生成 charge path action family。");
    }

    private void TestPassiveSkillIsNotGeneratable()
    {
        SkillDef skill = BuildSkill("passive_aura", "unit", "ally", Effect("status", "ally"));
        skill.skill_type = "passive";

        BattleAiSkillAffordanceRecord record = Classify(skill);
        AssertTrue(!record.is_generatable, "被动技能不应进入 AI action 生成。");
        AssertEq(record.skip_reason, "passive_or_no_combat", "被动技能应给出稳定 skip reason。");
    }

    private static BattleAiSkillAffordanceRecord Classify(SkillDef skillDef)
    {
        var classifier = new BattleAiSkillAffordanceClassifier();
        return classifier.ClassifySkill(skillDef, 1);
    }

    private static SkillDef BuildSkill(
        StringName skillId,
        StringName targetMode,
        StringName targetFilter,
        params CombatEffectDef[] effectDefs
    )
    {
        var skill = new SkillDef
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            skill_type = "active",
        };
        var combat = new CombatSkillDef
        {
            target_mode = targetMode,
            target_team_filter = targetFilter,
            range_pattern = "fixed",
            range_value = 5,
        };
        foreach (CombatEffectDef effectDef in effectDefs ?? Array.Empty<CombatEffectDef>())
        {
            combat.effect_defs.Add(effectDef);
        }
        skill.combat_profile = combat;
        return skill;
    }

    private static CombatEffectDef Effect(StringName effectType, StringName effectFilter = default)
    {
        var effect = new CombatEffectDef
        {
            effect_type = effectType,
            effect_target_team_filter = effectFilter,
        };
        if (effectType == new StringName("status"))
        {
            effect.status_id = "rooted";
        }
        if (effectType == new StringName("terrain"))
        {
            effect.terrain_effect_id = "mud";
        }
        return effect;
    }

    private void AssertContains(
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
        _failures.Add(message);
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq(string actual, string expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertNoGodotDynamicBoundaryTypes(Type type, string label)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (FieldInfo field in type.GetFields(flags))
        {
            AssertTrue(
                !IsGodotDynamicBoundaryType(field.FieldType),
                $"{label}.{field.Name} 不应暴露 GodotObject/Dictionary/Array/Variant 字段。"
            );
        }
        foreach (PropertyInfo property in type.GetProperties(flags))
        {
            AssertTrue(
                !IsGodotDynamicBoundaryType(property.PropertyType),
                $"{label}.{property.Name} 不应暴露 GodotObject/Dictionary/Array/Variant 属性。"
            );
        }
        foreach (MethodInfo method in type.GetMethods(flags))
        {
            if (method.IsSpecialName)
            {
                continue;
            }
            AssertTrue(
                !IsGodotDynamicBoundaryType(method.ReturnType),
                $"{label}.{method.Name} 不应返回 GodotObject/Dictionary/Array/Variant。"
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertTrue(
                    !IsGodotDynamicBoundaryType(parameter.ParameterType),
                    $"{label}.{method.Name}({parameter.Name}) 不应接收 GodotObject/Dictionary/Array/Variant。"
                );
            }
        }
    }

    private static bool IsGodotDynamicBoundaryType(Type type)
    {
        if (type == typeof(Variant) || typeof(GodotObject).IsAssignableFrom(type))
        {
            return true;
        }
        string typeName = type.FullName ?? "";
        return typeName.StartsWith("Godot.Collections.Dictionary", StringComparison.Ordinal)
            || typeName.StartsWith("Godot.Collections.Array", StringComparison.Ordinal);
    }
}
