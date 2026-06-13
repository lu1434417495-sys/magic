using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_execute_effect_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestExecuteFinishesLowHpTarget();
        TestExecuteNonLethalOnHighHpTarget();
        TestExecuteNonLethalOnBossTarget();
        TestExecuteShieldEfficiency();
        TestExecuteAppliesSoulFractureFromFormalFields();
        TestExecuteMinHpNeverHeals();
        Quit(_test.Finish("Battle execute effect regression"));
    }

    private void TestExecuteFinishesLowHpTarget()
    {
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("weak_target", "hostile");
        target.current_hp = 1;
        CombatEffectDef effect = MakeExecuteEffect();
        var resolver = new BattleDamageResolver();
        GDictionary result = resolver.ResolveEffects(
            source,
            target,
            new GArray { effect },
            new GDictionary { ["save_roll_override"] = 1 }
        );

        _test.False(target.is_alive, "execute on HP=1 target with failed save should kill.");
        _test.True(DictInt(result, "damage") > 0, "execute should register damage.");
        GDictionary deathEvent = FirstDamageEventWithDeathSource(result);
        AssertStringNameEq(
            DictStringName(deathEvent, BattleDeathResolutionRules.DeathSourcePayloadKey),
            BattleDeathResolutionRules.PowerWordKillExecuteDeathSource,
            "failed-save fatal execute damage event 应投影 Power Word Kill 死亡来源。"
        );
        _test.Eq(
            DictInt(deathEvent, BattleDeathResolutionRules.DeathSourcePriorityPayloadKey),
            900,
            "failed-save fatal execute damage event 应投影 execute fatal 优先级。"
        );
    }

    private void TestExecuteNonLethalOnHighHpTarget()
    {
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("healthy_target", "hostile");
        target.current_hp = 30;
        CombatEffectDef effect = MakeExecuteEffect();
        var resolver = new BattleDamageResolver();
        GDictionary result = resolver.ResolveEffects(source, target, new GArray { effect });

        _test.True(target.is_alive, "execute on high-HP target should leave target alive.");
        _test.Eq(target.current_hp, 29, "non-lethal should deal 1 damage leaving 29 HP.");
        _test.Eq(DictInt(result, "damage"), 1, "non-lethal should register 1 damage.");
    }

    private void TestExecuteNonLethalOnBossTarget()
    {
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("boss_target", "hostile");
        target.attribute_snapshot.SetValue("boss_target", 1);
        target.current_hp = 5;
        CombatEffectDef effect = MakeExecuteEffect();
        var resolver = new BattleDamageResolver();
        resolver.ResolveEffects(source, target, new GArray { effect });

        _test.True(target.is_alive, "execute on boss target should never be lethal.");
        _test.Eq(target.current_hp, 1, "boss should be clamped to 1 HP.");
    }

    private void TestExecuteShieldEfficiency()
    {
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("shielded_target", "hostile");
        target.current_hp = 5;
        target.current_shield_hp = 20;
        target.shield_max_hp = 20;
        target.shield_duration = 10;
        CombatEffectDef effect = MakeExecuteEffect();
        effect.shield_absorption_percent = 50.0;
        var resolver = new BattleDamageResolver();
        GDictionary result = resolver.ResolveEffects(
            source,
            target,
            new GArray { effect },
            new GDictionary { ["save_roll_override"] = 20 }
        );
        GDictionary firstEvent = FirstDamageEvent(result);

        _test.Eq(
            DictInt(firstEvent, "shield_absorbed"),
            10,
            "50% shield efficiency should absorb at most ceil(20*0.5)=10."
        );
        _test.Eq(target.current_shield_hp, 0, "50% efficiency should drain all 20 shield HP.");
        _test.Eq(target.current_hp, 1, "burst should clamp target to 1 HP after shield.");
    }

    private void TestExecuteAppliesSoulFractureFromFormalFields()
    {
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("cursed_target", "hostile");
        target.current_hp = 30;
        CombatEffectDef effect = MakeExecuteEffect();
        effect.soul_fracture_duration_tu = 60;
        effect.soul_fracture_status_id = "soul_fracture";
        effect.heal_multiplier_percent = 50;
        effect.shield_gain_multiplier_percent = 40;
        var resolver = new BattleDamageResolver();

        resolver.ResolveEffects(source, target, new GArray { effect });

        _test.True(target.HasStatusEffect("soul_fracture"), "execute 应按 formal soul fracture 字段附加状态。");
        _test.Eq(
            BattleStatusModifierRules.ResolveHealMultiplierPercent(target),
            50,
            "execute 附加的 soul fracture 应投影治疗倍率。"
        );
        _test.Eq(
            BattleStatusModifierRules.ResolveShieldGainMultiplierPercent(target),
            40,
            "execute 附加的 soul fracture 应投影护盾倍率。"
        );
    }

    private void TestExecuteMinHpNeverHeals()
    {
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("wounded_target", "hostile");
        target.current_hp = 1;
        var outcome = new GDictionary
        {
            ["resolved_damage"] = 0,
            ["min_hp_after_damage"] = 1,
        };
        var resolver = new BattleDamageResolver();
        int damage = resolver.ApplyDirectDamageToTargetTyped(target, outcome, source);

        _test.Eq(target.current_hp, 1, "min_hp_after_damage=1 with 0 damage should not heal.");
        _test.Eq(damage, 0, "0 resolved damage should yield 0 hp_damage.");
    }

    private static CombatEffectDef MakeExecuteEffect()
    {
        return new CombatEffectDef
        {
            effect_type = "execute",
            save_dc_mode = "static",
            save_dc = 10,
            save_ability = "willpower",
            save_tag = "magic",
            staged_execution = true,
            burst_damage = 9999,
            finisher_damage = 1,
            shield_absorption_percent = 50.0,
            min_hp_after_damage = 1,
            boss_non_lethal_damage_max_hp_ratio_percent = 12,
            boss_non_lethal_damage_floor = 25,
        };
    }

    private static BattleUnitState MakeUnit(StringName unitId, StringName factionId)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = "manual",
            current_hp = 30,
            current_mp = 0,
            current_ap = 2,
            current_stamina = 20,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 0);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.attribute_snapshot.SetValue("agility", 10);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("intelligence", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        return unit;
    }

    private static GDictionary FirstDamageEvent(GDictionary result)
    {
        if (result == null || !result.ContainsKey("damage_events"))
        {
            return new GDictionary();
        }
        GArray events = result["damage_events"].AsGodotArray();
        if (events.Count == 0)
        {
            return new GDictionary();
        }
        return events[0].AsGodotDictionary();
    }

    private static GDictionary FirstDamageEventWithDeathSource(GDictionary result)
    {
        if (result == null || !result.ContainsKey("damage_events"))
        {
            return new GDictionary();
        }
        foreach (Variant eventValue in result["damage_events"].AsGodotArray())
        {
            if (eventValue.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            GDictionary damageEvent = eventValue.AsGodotDictionary();
            if (DictStringName(damageEvent, BattleDeathResolutionRules.DeathSourcePayloadKey) != "")
            {
                return damageEvent;
            }
        }
        return new GDictionary();
    }

    private static int DictInt(GDictionary data, string key, int fallback = 0)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return fallback;
        }
        return data[key].AsInt32();
    }

    private static StringName DictStringName(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return "";
        }
        return ProgressionDataUtils.to_string_name(data[key]);
    }

    private void AssertStringNameEq(StringName actual, StringName expected, string message)
    {
        if (actual != expected)
        {
            _test.Fail($"{message} | actual={actual} expected={expected}");
        }
    }
}
