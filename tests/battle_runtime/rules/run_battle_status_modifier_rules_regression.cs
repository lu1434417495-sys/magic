using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_status_modifier_rules_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestRuleTypeIsPlainStaticCSharp();
        TestHealAndShieldMultipliersUseTypedStatusState();
        TestMissingTypedUnitKeepsDefaultMultiplier();
        TestPositiveMultiplierKeepsPositiveAmount();
        TestHealAndShieldApplicationConsumeStatusModifiers();

        if (_failures.Count == 0)
        {
            GD.Print("Battle status modifier rules regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle status modifier rules regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestRuleTypeIsPlainStaticCSharp()
    {
        Type ruleType = typeof(BattleStatusModifierRules);
        AssertTrue(ruleType.IsAbstract && ruleType.IsSealed, "状态倍率规则应是 plain static C# class。");
        AssertFalse(typeof(RefCounted).IsAssignableFrom(ruleType), "状态倍率规则不应继承 RefCounted。");
        AssertFalse(HasAttributeNamed(ruleType, "GlobalClassAttribute"), "状态倍率规则不应注册 GlobalClass。");
    }

    private void TestHealAndShieldMultipliersUseTypedStatusState()
    {
        var unit = new BattleUnitState();
        unit.set_status_effect(
            new BattleStatusEffectState
            {
                status_id = "healing_suppressed",
                @params = new GDictionary
                {
                    [BattleStatusModifierRules.HealMultiplierPercentParam] = 50,
                    [BattleStatusModifierRules.ShieldGainMultiplierPercentParam] = 25,
                },
            }
        );

        AssertEq(
            BattleStatusModifierRules.ResolveHealMultiplierPercent(unit),
            50,
            "typed status params 应决定治疗倍率。"
        );
        AssertEq(
            BattleStatusModifierRules.ApplyHealMultiplier(unit, 11),
            6,
            "治疗倍率应用应四舍五入。"
        );
        AssertEq(
            BattleStatusModifierRules.ResolveShieldGainMultiplierPercent(unit),
            25,
            "typed status params 应决定护盾获取倍率。"
        );
        AssertEq(
            BattleStatusModifierRules.ApplyShieldGainMultiplier(unit, 8),
            2,
            "护盾倍率应用应使用同一套 typed status params。"
        );
    }

    private void TestMissingTypedUnitKeepsDefaultMultiplier()
    {
        AssertEq(
            BattleStatusModifierRules.ResolveHealMultiplierPercent(null),
            BattleStatusModifierRules.DefaultMultiplierPercent,
            "空单位应使用默认倍率。"
        );
    }

    private void TestPositiveMultiplierKeepsPositiveAmount()
    {
        var unit = new BattleUnitState();
        unit.set_status_effect(MakeModifierStatus("partial_suppression", 25, 25));

        AssertEq(
            BattleStatusModifierRules.ApplyHealMultiplier(unit, 1),
            1,
            "正数治疗在正倍率下至少保留 1。"
        );
        AssertEq(
            BattleStatusModifierRules.ApplyShieldGainMultiplier(unit, 1),
            1,
            "正数护盾在正倍率下至少保留 1。"
        );
    }

    private void TestHealAndShieldApplicationConsumeStatusModifiers()
    {
        BattleUnitState source = MakeUnit("source", 20, 20);
        BattleUnitState healTarget = MakeUnit("heal_target", 5, 20);
        healTarget.set_status_effect(MakeModifierStatus("soul_fracture", 50, 50));

        var resolver = new BattleDamageResolver();
        var healEffect = new CombatEffectDef
        {
            effect_type = "heal",
            power = 10,
        };
        GDictionary healResult = resolver.resolve_effects(
            source,
            healTarget,
            new GArray { healEffect }
        );

        AssertEq(
            ReadInt(healResult, "healing"),
            5,
            "治疗应用路径应消费状态治疗倍率。"
        );
        AssertEq(healTarget.current_hp, 10, "治疗写回 HP 应使用倍率后的数值。");

        BattleUnitState shieldTarget = MakeUnit("shield_target", 20, 20);
        shieldTarget.set_status_effect(MakeModifierStatus("soul_fracture", 50, 50));
        var shieldService = new BattleShieldService();
        var shieldEffect = new CombatEffectDef
        {
            effect_type = "shield",
            power = 10,
            duration_tu = 60,
        };

        BattleShieldApplyResult shieldResult = shieldService.ApplyShieldEffectToTargetResult(
            source,
            shieldTarget,
            null,
            shieldEffect,
            new Dictionary<long, int>()
        );

        AssertEq(shieldResult.CurrentShieldHp, 5, "护盾应用路径应消费状态护盾倍率。");
        AssertEq(shieldTarget.current_shield_hp, 5, "护盾写回应使用倍率后的数值。");
    }

    private static BattleStatusEffectState MakeModifierStatus(
        StringName statusId,
        int healMultiplierPercent,
        int shieldGainMultiplierPercent
    )
    {
        return new BattleStatusEffectState
        {
            status_id = statusId,
            @params = new GDictionary
            {
                [BattleStatusModifierRules.HealMultiplierPercentParam] = healMultiplierPercent,
                [BattleStatusModifierRules.ShieldGainMultiplierPercentParam] =
                    shieldGainMultiplierPercent,
            },
        };
    }

    private static BattleUnitState MakeUnit(StringName unitId, int currentHp, int hpMax)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            current_hp = currentHp,
            is_alive = currentHp > 0,
        };
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), hpMax);
        return unit;
    }

    private static int ReadInt(GDictionary source, string key)
    {
        return source != null && source.ContainsKey(key) ? source[key].AsInt32() : 0;
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

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }
}
