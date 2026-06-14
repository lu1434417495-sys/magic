using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_skill_resolution_rules_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestTypedPolicyRoutesUnitVariantAndProjectsOnlyAtBoundary();
            TestGroundSkillGetsImplicitGroundVariant();
            TestAmbiguousVariantBlocksWithoutCollectingEffects();

            Quit(_test.Finish("Battle skill resolution rules regression"));
        }
        catch (Exception ex)
        {
            GD.PushError($"Battle skill resolution rules regression crashed: {ex}");
            Quit(1);
        }
    }



    private void TestTypedPolicyRoutesUnitVariantAndProjectsOnlyAtBoundary()
    {
        var rules = new BattleSkillResolutionRules();
        SkillDef skill = BuildSkill("typed_unit_variant", "unit", "single_unit");
        CombatEffectDef baseEffect = BuildDamageEffect("base_damage");
        CombatEffectDef variantEffect = BuildDamageEffect("variant_damage");
        skill.combat_profile.effect_defs.Add(baseEffect);
        skill.combat_profile.cast_variants.Add(BuildVariant("unit_bolt", "", variantEffect));
        skill.combat_profile.cast_variants.Add(BuildVariant("ground_burst", "ground"));
        BattleUnitState caster = BuildUnit("caster", "player", skill.skill_id, 1);
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
        _test.Eq(policy.CommandCastVariant?.variant_id ?? "", "unit_bolt", "应解析指定 unit variant。");
        _test.Eq(policy.TargetUnitIds.Count == 1 ? policy.TargetUnitIds[0] : "", "enemy", "target unit ids 应去重并过滤空值。");
        _test.Eq(policy.EffectDefs.Count, 2, "policy 应聚合基础 effect 与 variant effect。");
        _test.True(policy.UsesFateAttack, "敌方无豁免 damage unit skill 应走 fate attack 预览。");
        object targetUnitIds = policy.TargetUnitIds;
        object effectDefs = policy.EffectDefs;
        _test.False(targetUnitIds is Godot.Collections.Array, "typed policy 内部不应保存 Godot Array target ids。");
        _test.False(effectDefs is Godot.Collections.Array, "typed policy 内部不应保存 Godot Array effect defs。");

        Godot.Collections.Dictionary projection = policy.ToDictionary();
        _test.True(
            projection["target_unit_ids"].VariantType == Variant.Type.Array,
            "ToDictionary 投影边界才应输出 Godot Array target ids。"
        );
        _test.True(
            projection["effect_defs"].VariantType == Variant.Type.Array,
            "ToDictionary 投影边界才应输出 Godot Array effect defs。"
        );
    }

    private void TestGroundSkillGetsImplicitGroundVariant()
    {
        var rules = new BattleSkillResolutionRules();
        SkillDef skill = BuildSkill("implicit_ground_skill", "ground", "single_coord");
        CombatEffectDef groundDamageEffect = BuildDamageEffect("ground_damage");
        skill.combat_profile.effect_defs.Add(groundDamageEffect);
        BattleUnitState caster = BuildUnit("caster", "player", skill.skill_id, 1);

        BattleSkillResolutionPolicy policy = rules.BuildSkillResolutionPolicy(skill, caster);

        _test.True(policy.OptionAllowed, "无显式 variant 的 ground skill 应允许隐式地面形态。");
        _test.False(policy.RoutesToUnitTargeting, "ground skill 不应路由到 unit targeting。");
        _test.True(policy.GroundCastVariant != null, "ground skill 应生成隐式 ground cast variant。");
        _test.Eq(policy.GroundCastVariant.effect_defs.Count, 0, "隐式 ground variant 只描述形态，不应复制 profile effects。");
        _test.Eq(policy.EffectDefs.Count, 1, "隐式 ground policy 应只收集一次 profile effect。");
        _test.True(
            rules.IsUnitEffect(policy.EffectDefs[0]),
            "隐式 ground policy 应保留会作用于单位的 payload effect。"
        );
    }

    private void TestAmbiguousVariantBlocksWithoutCollectingEffects()
    {
        var rules = new BattleSkillResolutionRules();
        SkillDef skill = BuildSkill("ambiguous_unit_skill", "unit", "single_unit");
        skill.combat_profile.cast_variants.Add(BuildVariant("left", "unit", BuildDamageEffect("left")));
        skill.combat_profile.cast_variants.Add(BuildVariant("right", "unit", BuildDamageEffect("right")));
        BattleUnitState caster = BuildUnit("caster", "player", skill.skill_id, 1);
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
        _test.Eq(policy.EffectDefs.Count, 0, "被 option error 阻止时不应继续收集 effect defs。");
    }

    private static SkillDef BuildSkill(
        StringName skillId,
        StringName targetMode,
        StringName targetSelectionMode
    )
    {
        return new SkillDef
        {
            skill_id = skillId,
            skill_type = "active",
            combat_profile = new CombatSkillDef
            {
                skill_id = skillId,
                target_mode = targetMode,
                target_team_filter = "enemy",
                target_selection_mode = targetSelectionMode,
                range_pattern = "single",
                range_value = 5,
                area_pattern = "single",
            },
        };
    }

    private static CombatCastVariantDef BuildVariant(
        StringName variantId,
        StringName targetMode,
        CombatEffectDef effect = null
    )
    {
        var variant = new CombatCastVariantDef
        {
            variant_id = variantId,
            target_mode = targetMode,
            min_skill_level = 0,
            footprint_pattern = "single",
            required_coord_count = 1,
        };
        if (effect != null)
        {
            variant.effect_defs.Add(effect);
        }
        return variant;
    }

    private static CombatEffectDef BuildDamageEffect(StringName tag)
    {
        return new CombatEffectDef
        {
            effect_type = "damage",
            damage_tag = tag,
            effect_target_team_filter = "enemy",
            power = 3,
        };
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
            is_alive = true,
        };
        if (skillId != default && skillId != "")
        {
            unit.known_active_skill_ids.Add(skillId);
            unit.known_skill_level_map[skillId] = skillLevel;
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
