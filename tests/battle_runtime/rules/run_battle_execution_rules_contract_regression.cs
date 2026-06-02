using System;
using System.Reflection;
using Godot;

public partial class run_battle_execution_rules_contract_regression : SceneTree
{
    private readonly Godot.Collections.Array<string> _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestPlainCSharpContract();
        TestParamsAreParsedAtResourceBoundary();
        TestThresholdAndExecutePlan();
        TestEliteBossAndNonLethalRules();

        if (_failures.Count == 0)
        {
            GD.Print("Battle execution rules contract regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle execution rules contract regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestPlainCSharpContract()
    {
        AssertPlainType(typeof(BattleExecutionRules), "BattleExecutionRules");
        AssertPlainType(typeof(BattleExecutionRuleParams), "BattleExecutionRuleParams");
        AssertPlainType(typeof(BattleExecutePlan), "BattleExecutePlan");
        AssertPlainType(typeof(BattleExecuteSoulFractureParams), "BattleExecuteSoulFractureParams");

        Type rulesType = typeof(BattleExecutionRules);
        AssertNull(rulesType.GetMethod("build_execute_plan"), "旧 build_execute_plan API 应移除。");
        AssertNull(rulesType.GetMethod("resolve_threshold"), "旧 resolve_threshold API 应移除。");
        AssertNull(
            rulesType.GetMethod("resolve_non_lethal_damage"),
            "旧 resolve_non_lethal_damage API 应移除。"
        );
        AssertNull(rulesType.GetMethod("is_boss_target"), "旧 is_boss_target API 应移除。");
        AssertNull(
            rulesType.GetMethod("is_elite_or_boss_target"),
            "旧 is_elite_or_boss_target API 应移除。"
        );
        AssertNull(rulesType.GetMethod("BRANCH_INVALID_TARGET"), "旧 BRANCH_* Godot API 应移除。");

        foreach (Type type in new[] { typeof(BattleExecutionRules), typeof(BattleExecutionRuleParams) })
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                AssertFalse(
                    IsGodotPayloadType(method.ReturnType),
                    $"{type.Name}.{method.Name} 不应返回 Godot Dictionary/Array/Variant。"
                );
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    AssertFalse(
                        IsGodotPayloadType(parameter.ParameterType),
                        $"{type.Name}.{method.Name}({parameter.Name}) 不应接收 Godot Dictionary/Array/Variant。"
                    );
                }
            }
        }
    }

    private void TestParamsAreParsedAtResourceBoundary()
    {
        var effect = new CombatEffectDef
        {
            @params = new Godot.Collections.Dictionary
            {
                ["skill_id"] = "execute_skill",
                ["threshold_base_value"] = 12,
                ["threshold_level_anchor"] = 3,
                ["threshold_level_bonus_per_delta"] = 4,
                ["threshold_ability_mod"] = "willpower_modifier",
                ["threshold_ability_mod_multiplier"] = 6,
                ["threshold_max_hp_ratio_percent"] = 25,
                ["threshold_cap_max_hp_ratio_percent"] = 60,
                ["soul_fracture_duration_tu"] = 90,
                ["heal_multiplier_percent"] = 50,
                ["shield_gain_multiplier_percent"] = 40,
                ["boss_non_lethal_damage_max_hp_ratio_percent"] = 13,
                ["boss_non_lethal_damage_floor"] = 31,
                ["non_lethal_damage_ratio_percent"] = 35,
            },
        };

        BattleExecutionRuleParams parameters = BattleExecutionRuleParams.FromEffect(effect);

        AssertStringNameEq(parameters.SkillId, "execute_skill", "skill_id 应解析为 StringName。");
        AssertEq(parameters.ThresholdBaseValue, 12, "threshold_base_value 应解析。");
        AssertEq(parameters.ThresholdLevelAnchor, 3, "threshold_level_anchor 应解析。");
        AssertEq(parameters.ThresholdLevelBonusPerDelta, 4, "threshold_level_bonus_per_delta 应解析。");
        AssertStringNameEq(
            parameters.ThresholdAbilityMod,
            "willpower_modifier",
            "threshold_ability_mod 应解析。"
        );
        AssertEq(parameters.ThresholdAbilityModMultiplier, 6, "ability multiplier 应解析。");
        AssertEq(parameters.ThresholdMaxHpRatioPercent, 25, "max hp ratio 应解析。");
        AssertEq(parameters.ThresholdCapMaxHpRatioPercent, 60, "cap ratio 应解析。");
        AssertEq(parameters.SoulFractureDurationTu, 90, "soul fracture duration 应解析。");
        AssertEq(parameters.HealMultiplierPercent, 50, "heal multiplier 应解析。");
        AssertEq(parameters.ShieldGainMultiplierPercent, 40, "shield multiplier 应解析。");
        AssertEq(parameters.BossNonLethalDamageMaxHpRatioPercent, 13, "boss ratio 应解析。");
        AssertEq(parameters.BossNonLethalDamageFloor, 31, "boss floor 应解析。");
        AssertEq(parameters.NonLethalDamageRatioPercent, 35, "non-lethal ratio 应解析。");
    }

    private void TestThresholdAndExecutePlan()
    {
        BattleUnitState source = MakeUnit("execute_source", 100, 100);
        source.known_skill_level_map["execute_skill"] = 20;
        source.attribute_snapshot.set_value("intelligence_modifier", 3);
        BattleUnitState target = MakeUnit("execute_target", 100, 50);
        BattleExecutionRuleParams parameters =
            BattleExecutionRuleParams.Defaults("execute_skill") with
            {
                SoulFractureDurationTu = 80,
                HealMultiplierPercent = 55,
                ShieldGainMultiplierPercent = 65,
            };

        int threshold = BattleExecutionRules.ResolveThreshold(source, target, parameters);
        BattleExecutePlan plan = BattleExecutionRules.BuildExecutePlan(source, target, parameters);

        AssertEq(threshold, 50, "阈值应合并 hp floor、等级加成、能力加成并受 cap 限制。");
        AssertTrue(plan.CanExecute, "current_hp <= threshold 时应生成可执行计划。");
        AssertStringNameEq(plan.Branch, BattleExecutionRules.BranchLowHpExecute, "branch 应为 low hp execute。");
        AssertEq(plan.CurrentHp, 50, "plan 应保留当前 HP。");
        AssertEq(plan.MaxHp, 100, "plan 应保留最大 HP。");
        AssertEq(plan.FatalDamage, 50, "fatal damage 应等于当前 HP。");
        AssertTrue(plan.BypassShield, "低血处决计划应绕过护盾。");
        AssertTrue(plan.SoulFractureParams.HasValue, "配置 duration 时应生成 typed soul fracture 参数。");
        AssertEq(plan.SoulFractureParams.DurationTu, 80, "soul fracture duration 应保留。");
        AssertEq(plan.SoulFractureParams.HealMultiplierPercent, 55, "heal multiplier 应保留。");
        AssertEq(plan.SoulFractureParams.ShieldGainMultiplierPercent, 65, "shield multiplier 应保留。");

        target.current_hp = 51;
        BattleExecutePlan invalidPlan = BattleExecutionRules.BuildExecutePlan(
            source,
            target,
            parameters
        );
        AssertFalse(invalidPlan.CanExecute, "current_hp > threshold 时不应处决。");
        AssertStringNameEq(invalidPlan.Branch, BattleExecutionRules.BranchInvalidTarget, "branch 应为 invalid_target。");
        AssertEq(invalidPlan.FatalDamage, 0, "无效计划 fatal damage 应为 0。");
    }

    private void TestEliteBossAndNonLethalRules()
    {
        BattleUnitState source = MakeUnit("execute_source", 100, 100);
        BattleUnitState target = MakeUnit("regular_target", 100, 80);
        BattleExecutionRuleParams parameters =
            BattleExecutionRuleParams.Defaults() with
            {
                ThresholdBaseValue = 40,
                NonLethalDamageRatioPercent = 25,
                BossNonLethalDamageMaxHpRatioPercent = 12,
                BossNonLethalDamageFloor = 25,
            };

        AssertFalse(BattleExecutionRules.IsBossTarget(target), "普通目标不应是 boss。");
        AssertFalse(BattleExecutionRules.IsEliteOrBossTarget(target), "普通目标不应是 elite/boss。");
        AssertEq(
            BattleExecutionRules.ResolveNonLethalDamage(source, target, parameters),
            10,
            "非 boss 非致命伤害应使用 threshold * ratio。"
        );

        target.attribute_snapshot.set_value(BattleExecutionRules.FortuneMarkTargetStatId, 1);
        AssertFalse(BattleExecutionRules.IsBossTarget(target), "fortune_mark_target=1 应只算 elite。");
        AssertTrue(BattleExecutionRules.IsEliteOrBossTarget(target), "fortune_mark_target=1 应算 elite/boss。");

        target.attribute_snapshot.set_value(BattleExecutionRules.BossTargetStatId, 1);
        AssertTrue(BattleExecutionRules.IsBossTarget(target), "boss_target>0 应算 boss。");
        AssertEq(
            BattleExecutionRules.ResolveNonLethalDamage(source, target, parameters, true),
            25,
            "boss 非致命伤害应使用 max(hp ratio, floor)。"
        );
    }

    private static BattleUnitState MakeUnit(StringName unitId, int maxHp, int currentHp)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            current_hp = currentHp,
            is_alive = currentHp > 0,
        };
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), maxHp);
        return unit;
    }

    private void AssertPlainType(Type type, string label)
    {
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(type),
            $"{label} 不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            type.GetCustomAttribute<GlobalClassAttribute>() != null,
            $"{label} 不应注册 GlobalClass。"
        );
    }

    private static bool IsGodotPayloadType(Type type)
    {
        if (type == typeof(Variant))
        {
            return true;
        }
        string typeName = type.FullName ?? "";
        return typeName.StartsWith("Godot.Collections.Dictionary", StringComparison.Ordinal)
            || typeName.StartsWith("Godot.Collections.Array", StringComparison.Ordinal);
    }

    private void AssertTrue(bool value, string message)
    {
        if (!value)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool value, string message)
    {
        AssertTrue(!value, message);
    }

    private void AssertNull(object value, string message)
    {
        if (value != null)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq(int actual, int expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private void AssertStringNameEq(StringName actual, StringName expected, string message)
    {
        if (actual != expected)
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }
}
