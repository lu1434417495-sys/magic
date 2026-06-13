using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_status_modifier_rules_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestLegacyStatusParamsNoLongerDriveTypedMultipliers();
        TestHealAndShieldMultipliersUseTypedStatusFields();
        TestMissingTypedUnitKeepsDefaultMultiplier();
        TestPositiveMultiplierKeepsPositiveAmount();
        TestHealAndShieldApplicationConsumeStatusModifiers();

        Quit(_test.Finish("Battle status modifier rules regression"));
    }

    private void TestLegacyStatusParamsNoLongerDriveTypedMultipliers()
    {
        var unit = new BattleUnitState();
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "healing_suppressed",
                @params = new GDictionary
                {
                    ["heal_multiplier_percent"] = 50,
                    ["shield_gain_multiplier_percent"] = 25,
                },
            }
        );

        _test.Eq(
            BattleStatusModifierRules.ResolveHealMultiplierPercent(unit),
            100,
            "legacy status params.heal_multiplier_percent 不应继续驱动 typed 治疗倍率。"
        );
        _test.Eq(
            BattleStatusModifierRules.ResolveShieldGainMultiplierPercent(unit),
            100,
            "legacy status params.shield_gain_multiplier_percent 不应继续驱动 typed 护盾倍率。"
        );
    }

    private void TestHealAndShieldMultipliersUseTypedStatusFields()
    {
        var unit = new BattleUnitState();
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "healing_suppressed",
                heal_multiplier_percent = 50,
                shield_gain_multiplier_percent = 25,
            }
        );

        _test.Eq(
            BattleStatusModifierRules.ResolveHealMultiplierPercent(unit),
            50,
            "typed status fields 应决定治疗倍率。"
        );
        _test.Eq(
            BattleStatusModifierRules.ApplyHealMultiplier(unit, 11),
            6,
            "治疗倍率应用应四舍五入。"
        );
        _test.Eq(
            BattleStatusModifierRules.ResolveShieldGainMultiplierPercent(unit),
            25,
            "typed status fields 应决定护盾获取倍率。"
        );
        _test.Eq(
            BattleStatusModifierRules.ApplyShieldGainMultiplier(unit, 8),
            2,
            "护盾倍率应用应使用同一套 typed status fields。"
        );
    }

    private void TestMissingTypedUnitKeepsDefaultMultiplier()
    {
        _test.Eq(
            BattleStatusModifierRules.ResolveHealMultiplierPercent(null),
            100,
            "空单位应使用默认倍率。"
        );
    }

    private void TestPositiveMultiplierKeepsPositiveAmount()
    {
        var unit = new BattleUnitState();
        unit.SetStatusEffect(MakeModifierStatus("partial_suppression", 25, 25));

        _test.Eq(
            BattleStatusModifierRules.ApplyHealMultiplier(unit, 1),
            1,
            "正数治疗在正倍率下至少保留 1。"
        );
        _test.Eq(
            BattleStatusModifierRules.ApplyShieldGainMultiplier(unit, 1),
            1,
            "正数护盾在正倍率下至少保留 1。"
        );
    }

    private void TestHealAndShieldApplicationConsumeStatusModifiers()
    {
        BattleUnitState source = MakeUnit("source", 20, 20);
        BattleUnitState healTarget = MakeUnit("heal_target", 5, 20);
        healTarget.SetStatusEffect(MakeModifierStatus("soul_fracture", 50, 50));

        var resolver = new BattleDamageResolver();
        var healEffect = new CombatEffectDef
        {
            effect_type = "heal",
            power = 10,
        };
        GDictionary healResult = resolver.ResolveEffects(
            source,
            healTarget,
            new GArray { healEffect }
        );

        _test.Eq(
            ReadInt(healResult, "healing"),
            5,
            "治疗应用路径应消费状态治疗倍率。"
        );
        _test.Eq(healTarget.current_hp, 10, "治疗写回 HP 应使用倍率后的数值。");

        BattleUnitState shieldTarget = MakeUnit("shield_target", 20, 20);
        shieldTarget.SetStatusEffect(MakeModifierStatus("soul_fracture", 50, 50));
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

        _test.Eq(shieldResult.CurrentShieldHp, 5, "护盾应用路径应消费状态护盾倍率。");
        _test.Eq(shieldTarget.current_shield_hp, 5, "护盾写回应使用倍率后的数值。");
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
            heal_multiplier_percent = healMultiplierPercent,
            shield_gain_multiplier_percent = shieldGainMultiplierPercent,
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
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), hpMax);
        return unit;
    }

    private static int ReadInt(GDictionary source, string key)
    {
        return source != null && source.ContainsKey(key) ? source[key].AsInt32() : 0;
    }

}
