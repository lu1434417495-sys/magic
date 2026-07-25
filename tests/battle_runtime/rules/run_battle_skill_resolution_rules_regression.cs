using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_skill_resolution_rules_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestTypedPolicyRoutesUnitVariantAndProjectsOnlyAtBoundary();
            TestGroundSkillGetsImplicitGroundVariant();
            TestAmbiguousVariantBlocksWithoutCollectingEffects();

            RequestTestExit(_test.Finish("Battle skill resolution rules regression"));
        }
        catch (Exception ex)
        {
            ConsoleProcessOutput.WriteFailure($"Battle skill resolution rules regression crashed: {ex}");
            RequestTestExit(_test.Finish("Battle skill resolution rules regression", 1));
        }
    }



    private void TestTypedPolicyRoutesUnitVariantAndProjectsOnlyAtBoundary()
    {
        var rules = new BattleSkillResolutionRules();
        SkillDefinition skill = BuildSkill(
            "typed_unit_variant",
            "unit",
            "single_unit",
            new[] { BuildDamageEffect("base_damage") },
            new[]
            {
                BuildVariant("unit_bolt", "", BuildDamageEffect("variant_damage")),
                BuildVariant("ground_burst", "ground"),
            }
        );
        BattleUnitState caster = BuildUnit("caster", "player", skill.SkillId, 1);
        BattleUnitState enemy = BuildUnit("enemy", "enemy", default, 0);

        BattleSkillResolutionPolicy policy = rules.BuildSkillResolutionPolicy(
            skill,
            caster,
            "unit_bolt",
            new[] { new StringName("enemy"), new StringName("enemy"), new StringName("") },
            enemy
        );

        _test.True(policy.OptionAllowed, "合法 unit variant policy 应允许执行。");
        _test.True(policy.RoutesToUnitTargeting, "传入单位目标时应路由到 unit targeting。");
        _test.Eq(policy.CommandCastVariantDefinition?.VariantId ?? "", "unit_bolt", "应解析指定 unit variant。");
        _test.Eq(policy.TargetUnitIds.Count == 1 ? policy.TargetUnitIds[0] : "", "enemy", "target unit ids 应去重并过滤空值。");
        _test.Eq(policy.EffectDefinitions.Count, 2, "policy 应聚合基础 effect 与 variant effect。");
        _test.True(policy.UsesFateAttack, "敌方无豁免 damage unit skill 应走 fate attack 预览。");
        object targetUnitIds = policy.TargetUnitIds;
        object effectDefs = policy.EffectDefinitions;
        _test.False(targetUnitIds is Godot.Collections.Array, "typed policy 内部不应保存 Godot Array target ids。");
        _test.False(effectDefs is Godot.Collections.Array, "typed policy 内部不应保存 Godot Array effect defs。");
        _test.False(
            typeof(GodotObject).IsAssignableFrom(policy.EffectDefinitions[0].GetType()),
            "typed policy 内部 effect 不应是 Godot Resource wrapper。"
        );

        Godot.Collections.Dictionary projection = BattleSkillResolutionPolicyProjection.Project(policy);
        _test.True(
            projection["target_unit_ids"].VariantType == Variant.Type.Array,
            "projection 边界才应输出 Godot Array target ids。"
        );
        _test.Eq((int)projection["effect_count"], 2, "projection 边界应只暴露 typed effect 数量。");
        _test.Eq(
            (StringName)projection["command_cast_variant_id"],
            "unit_bolt",
            "projection 边界应投影 typed cast variant id。"
        );
    }

    private void TestGroundSkillGetsImplicitGroundVariant()
    {
        var rules = new BattleSkillResolutionRules();
        SkillDefinition skill = BuildSkill(
            "implicit_ground_skill",
            "ground",
            "single_coord",
            new[] { BuildDamageEffect("ground_damage") }
        );
        BattleUnitState caster = BuildUnit("caster", "player", skill.SkillId, 1);

        BattleSkillResolutionPolicy policy = rules.BuildSkillResolutionPolicy(
            skill,
            caster
        );

        _test.True(policy.OptionAllowed, "无显式 variant 的 ground skill 应允许隐式地面形态。");
        _test.False(policy.RoutesToUnitTargeting, "ground skill 不应路由到 unit targeting。");
        _test.True(policy.GroundCastVariantDefinition != null, "ground skill 应生成隐式 ground cast variant。");
        _test.Eq(policy.GroundCastVariantDefinition.EffectDefinitions.Count, 0, "隐式 ground variant 只描述形态，不应复制 profile effects。");
        _test.Eq(policy.EffectDefinitions.Count, 1, "隐式 ground policy 应只收集一次 profile effect。");
        _test.True(
            rules.IsUnitEffect(policy.EffectDefinitions[0]),
            "隐式 ground policy 应保留会作用于单位的 payload effect。"
        );
    }

    private void TestAmbiguousVariantBlocksWithoutCollectingEffects()
    {
        var rules = new BattleSkillResolutionRules();
        SkillDefinition skill = BuildSkill(
            "ambiguous_unit_skill",
            "unit",
            "single_unit",
            castVariants: new[]
            {
                BuildVariant("left", "unit", BuildDamageEffect("left")),
                BuildVariant("right", "unit", BuildDamageEffect("right")),
            }
        );
        BattleUnitState caster = BuildUnit("caster", "player", skill.SkillId, 1);
        BattleUnitState enemy = BuildUnit("enemy", "enemy", default, 0);

        BattleSkillResolutionPolicy policy = rules.BuildSkillResolutionPolicy(
            skill,
            caster,
            default,
            new[] { new StringName("enemy") },
            enemy
        );

        _test.False(policy.OptionAllowed, "多个同目标形态且未指定 variant 时应阻止执行。");
        _test.Eq(policy.OptionErrorMessage, "技能形态不明确。", "应返回明确的形态歧义错误。");
        _test.Eq(policy.EffectDefinitions.Count, 0, "被 option error 阻止时不应继续收集 effect defs。");
    }

    private static SkillDefinition BuildSkill(
        StringName skillId,
        StringName targetMode,
        StringName targetSelectionMode,
        IReadOnlyList<CombatEffectDefinition> effects = null,
        IReadOnlyList<CombatCastVariantDefinition> castVariants = null
    )
    {
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: effects,
                targetMode: targetMode,
                targetTeamFilter: "enemy",
                targetSelectionMode: targetSelectionMode,
                rangePattern: "single",
                rangeValue: 5,
                areaPattern: "single",
                castVariants: castVariants
            )
        );
    }

    private static CombatCastVariantDefinition BuildVariant(
        StringName variantId,
        StringName targetMode,
        CombatEffectDefinition effect = null
    )
    {
        return TestSkillDefinitionProjection.BuildCastVariant(
            variantId,
            minSkillLevel: 0,
            effects: effect != null ? new[] { effect } : Array.Empty<CombatEffectDefinition>(),
            targetMode: targetMode,
            footprintPattern: "single",
            requiredCoordCount: 1
        );
    }

    private static CombatEffectDefinition BuildDamageEffect(StringName tag)
    {
        return TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            damageTag: tag,
            power: 3
        );
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        StringName skillId,
        int skillLevel
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            faction_id = factionId,
        }.WithCombatResourcesForTest(
            isAlive: true
        );
        if (skillId != default && skillId != "")
        {
            unit.AddKnownActiveSkill(skillId);
            unit.SetKnownSkillLevelTyped(
                skillId,
                skillLevel,
                preserveZero: skillLevel == 0
            );
        }
        return unit;
    }

    private static bool IsForbiddenPublicApiType(Type type)
    {
        if (type == typeof(Variant))
        {
            return true;
        }
        string typeName = type.FullName ?? "";
        return typeName.StartsWith("Godot.Collections.Dictionary", StringComparison.Ordinal)
            || typeName.StartsWith("Godot.Collections.Array", StringComparison.Ordinal);
    }


}
