using System;
using Godot;

public partial class run_battle_execution_rules_contract_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestExecuteFieldsAreParsedAtResourceBoundary();
        TestSkillSchemaRejectsPromotedExecuteParamsInLegacyPayload();
        TestThresholdAndExecutePlan();
        TestEliteBossAndNonLethalRules();

        Quit(_test.Finish("Battle execution rules contract regression"));
    }

    private void TestExecuteFieldsAreParsedAtResourceBoundary()
    {
        var effect = new CombatEffectDef
        {
            threshold_base_value = 12,
            threshold_level_anchor = 3,
            threshold_level_bonus_per_delta = 4,
            threshold_ability_mod = "willpower_modifier",
            threshold_ability_mod_multiplier = 6,
            threshold_max_hp_ratio_percent = 25,
            threshold_cap_max_hp_ratio_percent = 60,
            soul_fracture_duration_tu = 90,
            heal_multiplier_percent = 50,
            shield_gain_multiplier_percent = 40,
            boss_non_lethal_damage_max_hp_ratio_percent = 13,
            boss_non_lethal_damage_floor = 31,
            non_lethal_damage_ratio_percent = 35,
        };

        BattleExecutionRuleParams parameters = BattleExecutionRuleParams.FromEffect(
            effect,
            "execute_skill"
        );

        _test.Eq(parameters.SkillId, "execute_skill", "skill_id 应由调用方显式传入。");
        _test.Eq(parameters.ThresholdBaseValue, 12, "threshold_base_value 应直接读取字段。");
        _test.Eq(parameters.ThresholdLevelAnchor, 3, "threshold_level_anchor 应直接读取字段。");
        _test.Eq(parameters.ThresholdLevelBonusPerDelta, 4, "threshold_level_bonus_per_delta 应直接读取字段。");
        _test.Eq(
            parameters.ThresholdAbilityMod,
            "willpower_modifier",
            "threshold_ability_mod 应直接读取字段。"
        );
        _test.Eq(parameters.ThresholdAbilityModMultiplier, 6, "ability multiplier 应直接读取字段。");
        _test.Eq(parameters.ThresholdMaxHpRatioPercent, 25, "max hp ratio 应直接读取字段。");
        _test.Eq(parameters.ThresholdCapMaxHpRatioPercent, 60, "cap ratio 应直接读取字段。");
        _test.Eq(parameters.SoulFractureDurationTu, 90, "soul fracture duration 应直接读取字段。");
        _test.Eq(parameters.HealMultiplierPercent, 50, "heal multiplier 应直接读取字段。");
        _test.Eq(parameters.ShieldGainMultiplierPercent, 40, "shield multiplier 应直接读取字段。");
        _test.Eq(parameters.BossNonLethalDamageMaxHpRatioPercent, 13, "boss ratio 应直接读取字段。");
        _test.Eq(parameters.BossNonLethalDamageFloor, 31, "boss floor 应直接读取字段。");
        _test.Eq(parameters.NonLethalDamageRatioPercent, 35, "non-lethal ratio 应直接读取字段。");
    }

    private void TestSkillSchemaRejectsPromotedExecuteParamsInLegacyPayload()
    {
        using SkillContentRegistry registry = new();
        using CombatEffectDef effect = new()
        {
            effect_type = "execute",
            @params = new Godot.Collections.Dictionary
            {
                ["min_hp_after_damage"] = 1,
                ["threshold_base_value"] = 12,
                ["threshold_level_anchor"] = 17,
                ["threshold_level_bonus_per_delta"] = 5,
                ["threshold_ability_mod"] = "intelligence_modifier",
                ["threshold_ability_mod_multiplier"] = 5,
                ["threshold_max_hp_ratio_percent"] = 20,
                ["threshold_cap_max_hp_ratio_percent"] = 50,
                ["soul_fracture_status"] = new Godot.Collections.Dictionary
                {
                    ["status_id"] = "soul_fracture",
                    ["duration_tu"] = 60,
                },
                ["soul_fracture_duration_tu"] = 60,
                ["heal_multiplier_percent"] = 100,
                ["shield_gain_multiplier_percent"] = 100,
                ["boss_non_lethal_damage_max_hp_ratio_percent"] = 12,
                ["boss_non_lethal_damage_floor"] = 25,
                ["non_lethal_damage_ratio_percent"] = 30,
            },
        };
        var errors = new Godot.Collections.Array<string>();
        registry.AppendEffectValidationErrors(
            errors,
            "legacy_execute_payload",
            effect,
            "test_effect"
        );

        _test.True(errors.Count >= 15, "execute 旧 params 键应被 SkillContentRegistry 静态拒绝。");
    }

    private void TestThresholdAndExecutePlan()
    {
        BattleUnitState source = MakeUnit("execute_source", 100, 100);
        source.known_skill_level_map["execute_skill"] = 20;
        source.attribute_snapshot.SetValue("intelligence_modifier", 3);
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

        _test.Eq(threshold, 50, "阈值应合并 hp floor、等级加成、能力加成并受 cap 限制。");
        _test.True(plan.CanExecute, "current_hp <= threshold 时应生成可执行计划。");
        _test.Eq(plan.Branch, BattleExecutionRules.BranchLowHpExecute, "branch 应为 low hp execute。");
        _test.Eq(plan.CurrentHp, 50, "plan 应保留当前 HP。");
        _test.Eq(plan.MaxHp, 100, "plan 应保留最大 HP。");
        _test.Eq(plan.FatalDamage, 50, "fatal damage 应等于当前 HP。");
        _test.True(plan.BypassShield, "低血处决计划应绕过护盾。");
        _test.True(plan.SoulFractureParams.HasValue, "配置 duration 时应生成 typed soul fracture 参数。");
        _test.Eq(plan.SoulFractureParams.DurationTu, 80, "soul fracture duration 应保留。");
        _test.Eq(plan.SoulFractureParams.HealMultiplierPercent, 55, "heal multiplier 应保留。");
        _test.Eq(plan.SoulFractureParams.ShieldGainMultiplierPercent, 65, "shield multiplier 应保留。");

        target.current_hp = 51;
        BattleExecutePlan invalidPlan = BattleExecutionRules.BuildExecutePlan(
            source,
            target,
            parameters
        );
        _test.False(invalidPlan.CanExecute, "current_hp > threshold 时不应处决。");
        _test.Eq(invalidPlan.Branch, BattleExecutionRules.BranchInvalidTarget, "branch 应为 invalid_target。");
        _test.Eq(invalidPlan.FatalDamage, 0, "无效计划 fatal damage 应为 0。");
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

        _test.False(BattleExecutionRules.IsBossTarget(target), "普通目标不应是 boss。");
        _test.False(BattleExecutionRules.IsEliteOrBossTarget(target), "普通目标不应是 elite/boss。");
        _test.Eq(
            BattleExecutionRules.ResolveNonLethalDamage(source, target, parameters),
            10,
            "非 boss 非致命伤害应使用 threshold * ratio。"
        );

        target.attribute_snapshot.SetValue(BattleExecutionRules.FortuneMarkTargetStatId, 1);
        _test.False(BattleExecutionRules.IsBossTarget(target), "fortune_mark_target=1 应只算 elite。");
        _test.True(BattleExecutionRules.IsEliteOrBossTarget(target), "fortune_mark_target=1 应算 elite/boss。");

        target.attribute_snapshot.SetValue(BattleExecutionRules.BossTargetStatId, 1);
        _test.True(BattleExecutionRules.IsBossTarget(target), "boss_target>0 应算 boss。");
        _test.Eq(
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
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), maxHp);
        return unit;
    }

    private void AssertPlainType(Type type, string label)
    {
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
}
