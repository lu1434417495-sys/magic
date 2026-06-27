using System;
using Godot;

public partial class run_battle_execution_rules_contract_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestExecuteFieldsAreProjectedAtResourceBoundary();
        TestSkillSchemaRejectsPromotedExecuteParamsInLegacyPayload();
        TestThresholdUsesSkillLevelButNotAbility();
        TestZeroOrDeadTargetCannotExecute();

        Quit(_test.Finish("Battle execution rules contract regression"));
    }

    private void TestExecuteFieldsAreProjectedAtResourceBoundary()
    {
        var effect = TestResourceOwnership.Own(
            new CombatEffectDef
            {
                threshold_base_value = 12,
                threshold_level_anchor = 3,
                threshold_level_bonus_per_delta = 4,
                threshold_max_hp_ratio_percent = 25,
                threshold_cap_max_hp_ratio_percent = 60,
                soul_fracture_duration_tu = 90,
                heal_multiplier_percent = 50,
                shield_gain_multiplier_percent = 40,
            },
            "BattleExecutionRulesContract.execute-fields-effect"
        );

        CombatEffectDefinition effectDefinition = CombatEffectDefinition.FromResource(effect);
        BattleExecutionRuleParams parameters = BattleExecutionRuleParams.FromEffect(
            effectDefinition,
            "execute_skill"
        );

        _test.Eq(parameters.SkillId, "execute_skill", "skill_id 应由调用方显式传入。");
        _test.Eq(parameters.ThresholdBaseValue, 12, "threshold_base_value 应直接读取字段。");
        _test.Eq(parameters.ThresholdLevelAnchor, 3, "threshold_level_anchor 应直接读取字段。");
        _test.Eq(parameters.ThresholdLevelBonusPerDelta, 4, "threshold_level_bonus_per_delta 应直接读取字段。");
        _test.Eq(parameters.ThresholdMaxHpRatioPercent, 25, "max hp ratio 应直接读取字段。");
        _test.Eq(parameters.ThresholdCapMaxHpRatioPercent, 60, "cap ratio 应直接读取字段。");
        _test.Eq(parameters.SoulFractureDurationTu, 90, "soul fracture duration 应直接读取字段。");
        _test.Eq(parameters.HealMultiplierPercent, 50, "heal multiplier 应直接读取字段。");
        _test.Eq(parameters.ShieldGainMultiplierPercent, 40, "shield multiplier 应直接读取字段。");
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

        string formattedErrors = string.Join(" | ", errors);
        _test.True(
            formattedErrors.Contains("execute must not use params payload"),
            $"execute 旧 params payload 应被 SkillContentRegistry 静态拒绝。 errors={formattedErrors}"
        );
    }

    private void TestThresholdUsesSkillLevelButNotAbility()
    {
        BattleUnitState source = MakeUnit("execute_source", 100, 100);
        source.known_skill_level_map["mage_power_word_kill"] = 20;
        source.attribute_snapshot.SetValue("intelligence_modifier", 99);
        BattleUnitState target = MakeUnit("execute_target", 100, 36);
        BattleExecutionRuleParams parameters =
            BattleExecutionRuleParams.FromEffect(
                TestSkillDefinitionProjection.BuildEffect(
                    "execute",
                    thresholdMaxHpRatioPercent: 20,
                    thresholdLevelAnchor: 17,
                    thresholdLevelBonusPerDelta: 5,
                    thresholdCapMaxHpRatioPercent: 50,
                    soulFractureDurationTu: 60,
                    healMultiplierPercent: 50,
                    shieldGainMultiplierPercent: 50
                ),
                "mage_power_word_kill"
            );

        int threshold = BattleExecutionRules.ResolveThreshold(source, target, parameters);

        _test.Eq(threshold, 35, "PWK threshold = 20% max HP + skill-level bonus, ignoring ability mod.");
        target.current_hp = 35;
        BattleExecutePlan plan = BattleExecutionRules.BuildExecutePlan(source, target, parameters);

        _test.True(plan.CanExecute, "current_hp <= formal threshold should execute.");
        _test.Eq(plan.FatalDamage, 35, "fatal damage should be current HP snapshot.");
    }

    private void TestZeroOrDeadTargetCannotExecute()
    {
        BattleUnitState source = MakeUnit("execute_source", 100, 100);
        source.known_skill_level_map["mage_power_word_kill"] = 20;
        BattleUnitState target = MakeUnit("execute_target", 100, 0);
        BattleExecutionRuleParams parameters = BattleExecutionRuleParams.Defaults("mage_power_word_kill");

        BattleExecutePlan plan = BattleExecutionRules.BuildExecutePlan(source, target, parameters);

        _test.False(plan.CanExecute, "dead/zero-HP targets should not enter low_hp_execute.");
        _test.Eq(plan.Branch, BattleExecutionRules.BranchInvalidTarget, "dead/zero-HP target is invalid.");
        _test.Eq(plan.FatalDamage, 0, "invalid target should not carry fatal damage.");
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
