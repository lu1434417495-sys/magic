using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_battle_skill_resolution_rules_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        try
        {
            int exitCode = Run();
            Quit(exitCode);
        }
        catch (Exception ex)
        {
            GD.PushError($"Battle skill resolution rules regression crashed: {ex}");
            Quit(1);
        }
    }

    private int Run()
    {
        TestRulesTypeIsPlainCSharp();
        TestTypedPolicyRoutesUnitVariantAndProjectsOnlyAtBoundary();
        TestGroundSkillGetsImplicitGroundVariant();
        TestAmbiguousVariantBlocksWithoutCollectingEffects();

        if (_failures.Count == 0)
        {
            GD.Print("Battle skill resolution rules regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle skill resolution rules regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestRulesTypeIsPlainCSharp()
    {
        Type rulesType = typeof(BattleSkillResolutionRules);
        Type policyType = typeof(BattleSkillResolutionPolicy);
        AssertFalse(
            typeof(RefCounted).IsAssignableFrom(rulesType),
            "Skill resolution rules 不应继承 RefCounted。"
        );
        AssertFalse(
            HasAttributeNamed(rulesType, "GlobalClassAttribute"),
            "Skill resolution rules 不应注册 GlobalClass。"
        );
        AssertNull(
            rulesType.GetMethod("build_skill_resolution_policy"),
            "规则本体不应保留 build_skill_resolution_policy snake_case API。"
        );
        AssertTrue(policyType.IsSealed, "BattleSkillResolutionPolicy 应为 sealed typed DTO。");
        AssertFalse(
            typeof(RefCounted).IsAssignableFrom(policyType),
            "BattleSkillResolutionPolicy 不应继承 RefCounted。"
        );
        AssertFalse(
            HasAttributeNamed(policyType, "GlobalClassAttribute"),
            "BattleSkillResolutionPolicy 不应注册 GlobalClass。"
        );
        AssertPublicApiDoesNotExposeGodotCollections(policyType);
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

        AssertTrue(policy.OptionAllowed, "合法 unit variant policy 应允许执行。");
        AssertTrue(policy.RoutesToUnitTargeting, "传入单位目标时应路由到 unit targeting。");
        AssertStringNameEq(policy.CommandCastVariant?.variant_id ?? "", "unit_bolt", "应解析指定 unit variant。");
        AssertStringNameEq(policy.TargetUnitIds.Count == 1 ? policy.TargetUnitIds[0] : "", "enemy", "target unit ids 应去重并过滤空值。");
        AssertEq(policy.EffectDefs.Count, 2, "policy 应聚合基础 effect 与 variant effect。");
        AssertTrue(policy.UsesFateAttack, "敌方无豁免 damage unit skill 应走 fate attack 预览。");
        object targetUnitIds = policy.TargetUnitIds;
        object effectDefs = policy.EffectDefs;
        AssertFalse(targetUnitIds is Godot.Collections.Array, "typed policy 内部不应保存 Godot Array target ids。");
        AssertFalse(effectDefs is Godot.Collections.Array, "typed policy 内部不应保存 Godot Array effect defs。");

        Godot.Collections.Dictionary projection = policy.ToDictionary();
        AssertTrue(
            projection["target_unit_ids"].VariantType == Variant.Type.Array,
            "ToDictionary 投影边界才应输出 Godot Array target ids。"
        );
        AssertTrue(
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

        AssertTrue(policy.OptionAllowed, "无显式 variant 的 ground skill 应允许隐式地面形态。");
        AssertFalse(policy.RoutesToUnitTargeting, "ground skill 不应路由到 unit targeting。");
        AssertNotNull(policy.GroundCastVariant, "ground skill 应生成隐式 ground cast variant。");
        AssertEq(policy.EffectDefs.Count, 2, "隐式 ground variant 当前会按既有合同收集 profile effect 与复制后的 variant effect。");
        AssertTrue(
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

        AssertFalse(policy.OptionAllowed, "多个同目标形态且未指定 variant 时应阻止执行。");
        AssertEq(policy.OptionErrorMessage, "技能形态不明确。", "应返回明确的形态歧义错误。");
        AssertEq(policy.EffectDefs.Count, 0, "被 option error 阻止时不应继续收集 effect defs。");
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

    private static bool HasAttributeNamed(Type type, string attributeTypeName)
    {
        foreach (object attribute in type.GetCustomAttributes(false))
        {
            if (attribute.GetType().Name == attributeTypeName)
            {
                return true;
            }
        }
        return false;
    }

    private void AssertPublicApiDoesNotExposeGodotCollections(Type type)
    {
        foreach (MethodInfo method in type.GetMethods(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly
                 ))
        {
            AssertFalse(
                IsForbiddenPublicApiType(method.ReturnType),
                $"{type.Name}.{method.Name} 不应公开返回 Godot Dictionary/Array/Variant。"
            );
            foreach (ParameterInfo parameter in method.GetParameters())
            {
                AssertFalse(
                    IsForbiddenPublicApiType(parameter.ParameterType),
                    $"{type.Name}.{method.Name}({parameter.Name}) 不应公开接收 Godot Dictionary/Array/Variant。"
                );
            }
        }
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

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        AssertTrue(!condition, message);
    }

    private void AssertNull(object value, string message)
    {
        if (value != null)
        {
            _failures.Add(message);
        }
    }

    private void AssertNotNull(object value, string message)
    {
        if (value == null)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }

    private void AssertStringNameEq(StringName actual, StringName expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }
}
