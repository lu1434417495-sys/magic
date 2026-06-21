using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_status_modifier_rules_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private readonly List<GodotObject> _ownedGodotObjects = new();
    private readonly List<System.IDisposable> _ownedDisposables = new();

    public override void _Initialize()
    {
        RunCase(TestLegacyStatusParamsNoLongerDriveTypedMultipliers);
        RunCase(TestHealAndShieldMultipliersUseTypedStatusFields);
        RunCase(TestMissingTypedUnitKeepsDefaultMultiplier);
        RunCase(TestPositiveMultiplierKeepsPositiveAmount);
        RunCase(TestHealAndShieldApplicationConsumeStatusModifiers);

        Quit(_test.Finish("Battle status modifier rules regression"));
    }

    private void RunCase(System.Action testCase)
    {
        try
        {
            testCase();
        }
        finally
        {
            DisposeOwned();
            GodotSharpCleanup.CollectPendingFinalizers();
        }
    }

    private void TestLegacyStatusParamsNoLongerDriveTypedMultipliers()
    {
        var unit = TrackOwned(new BattleUnitState());
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
        var unit = TrackOwned(new BattleUnitState());
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
        var unit = TrackOwned(new BattleUnitState());
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

        var resolver = TrackDisposable(new BattleDamageResolver());
        var healEffect = TrackOwned(new CombatEffectDef
        {
            effect_type = "heal",
            power = 10,
        });
        GDictionary healResult = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(
            source,
            healTarget,
            new GArray { healEffect }
        ));

        _test.Eq(
            ReadInt(healResult, "healing"),
            5,
            "治疗应用路径应消费状态治疗倍率。"
        );
        _test.Eq(healTarget.current_hp, 10, "治疗写回 HP 应使用倍率后的数值。");

        BattleUnitState shieldTarget = MakeUnit("shield_target", 20, 20);
        shieldTarget.SetStatusEffect(MakeModifierStatus("soul_fracture", 50, 50));
        var shieldService = new BattleShieldService();
        var shieldEffect = TrackOwned(new CombatEffectDef
        {
            effect_type = "shield",
            power = 10,
            duration_tu = 60,
        });

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

    private BattleStatusEffectState MakeModifierStatus(
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

    private BattleUnitState MakeUnit(StringName unitId, int currentHp, int hpMax)
    {
        var unit = TrackOwned(new BattleUnitState
        {
            unit_id = unitId,
            current_hp = currentHp,
            is_alive = currentHp > 0,
        });
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), hpMax);
        return unit;
    }

    private T TrackOwned<T>(T value)
        where T : GodotObject
    {
        if (value != null)
            _ownedGodotObjects.Add(value);
        return value;
    }

    private T TrackDisposable<T>(T value)
        where T : System.IDisposable
    {
        if (value != null)
            _ownedDisposables.Add(value);
        return value;
    }

    private void DisposeOwned()
    {
        for (int index = _ownedDisposables.Count - 1; index >= 0; index--)
            _ownedDisposables[index]?.Dispose();
        _ownedDisposables.Clear();

        for (int index = _ownedGodotObjects.Count - 1; index >= 0; index--)
            BattleTestFixture.DisposeFixtureObject(_ownedGodotObjects[index]);
        _ownedGodotObjects.Clear();
    }

    private static int ReadInt(GDictionary source, string key)
    {
        return source != null && source.ContainsKey(key) ? source[key].AsInt32() : 0;
    }

}
