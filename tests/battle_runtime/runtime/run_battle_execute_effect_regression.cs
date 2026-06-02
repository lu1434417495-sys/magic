using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_execute_effect_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestExecuteFinishesLowHpTarget();
        TestExecuteNonLethalOnHighHpTarget();
        TestExecuteNonLethalOnBossTarget();
        TestExecuteShieldEfficiency();
        TestExecuteMinHpNeverHeals();

        if (_failures.Count == 0)
        {
            GD.Print("Battle execute effect regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle execute effect regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestExecuteFinishesLowHpTarget()
    {
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("weak_target", "hostile");
        target.current_hp = 1;
        CombatEffectDef effect = MakeExecuteEffect();
        var resolver = new BattleDamageResolver();
        GDictionary result = resolver.resolve_effects(
            source,
            target,
            new GArray { effect },
            new GDictionary { ["save_roll_override"] = 1 }
        );

        AssertFalse(target.is_alive, "execute on HP=1 target with failed save should kill.");
        AssertTrue(DictInt(result, "damage") > 0, "execute should register damage.");
        GDictionary deathEvent = FirstDamageEventWithDeathSource(result);
        AssertStringNameEq(
            DictStringName(deathEvent, BattleDeathResolutionRules.DeathSourcePayloadKey),
            BattleDeathResolutionRules.PowerWordKillExecuteDeathSource,
            "failed-save fatal execute damage event 应投影 Power Word Kill 死亡来源。"
        );
        AssertEq(
            DictInt(deathEvent, BattleDeathResolutionRules.DeathSourcePriorityPayloadKey),
            BattleDeathResolutionRules.DeathPriorityExecuteFatal,
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
        GDictionary result = resolver.resolve_effects(source, target, new GArray { effect });

        AssertTrue(target.is_alive, "execute on high-HP target should leave target alive.");
        AssertEq(target.current_hp, 29, "non-lethal should deal 1 damage leaving 29 HP.");
        AssertEq(DictInt(result, "damage"), 1, "non-lethal should register 1 damage.");
    }

    private void TestExecuteNonLethalOnBossTarget()
    {
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("boss_target", "hostile");
        target.attribute_snapshot.set_value("boss_target", 1);
        target.current_hp = 5;
        CombatEffectDef effect = MakeExecuteEffect();
        var resolver = new BattleDamageResolver();
        resolver.resolve_effects(source, target, new GArray { effect });

        AssertTrue(target.is_alive, "execute on boss target should never be lethal.");
        AssertEq(target.current_hp, 1, "boss should be clamped to 1 HP.");
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
        effect.@params["shield_absorption_percent"] = 50.0;
        var resolver = new BattleDamageResolver();
        GDictionary result = resolver.resolve_effects(
            source,
            target,
            new GArray { effect },
            new GDictionary { ["save_roll_override"] = 20 }
        );
        GDictionary firstEvent = FirstDamageEvent(result);

        AssertEq(
            DictInt(firstEvent, "shield_absorbed"),
            10,
            "50% shield efficiency should absorb at most ceil(20*0.5)=10."
        );
        AssertEq(target.current_shield_hp, 0, "50% efficiency should drain all 20 shield HP.");
        AssertEq(target.current_hp, 1, "burst should clamp target to 1 HP after shield.");
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
        GDictionary result = resolver.apply_direct_damage_to_target(target, outcome, source);

        AssertEq(target.current_hp, 1, "min_hp_after_damage=1 with 0 damage should not heal.");
        AssertEq(DictInt(result, "damage", -1), 0, "0 resolved damage should yield 0 hp_damage.");
    }

    private static CombatEffectDef MakeExecuteEffect()
    {
        return new CombatEffectDef
        {
            effect_type = "execute",
            save_dc_mode = "fixed",
            save_dc = 10,
            save_ability = "willpower",
            save_tag = "magic",
            staged_execution = true,
            @params = new GDictionary
            {
                ["skill_id"] = "mage_power_word_kill",
                ["burst_damage"] = 9999,
                ["finisher_damage"] = 1,
                ["shield_absorption_percent"] = 50.0,
                ["min_hp_after_damage"] = 1,
                ["boss_non_lethal_damage_max_hp_ratio_percent"] = 12,
                ["boss_non_lethal_damage_floor"] = 25,
            },
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
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 30);
        unit.attribute_snapshot.set_value(AttributeService.MP_MAX_ID(), 0);
        unit.attribute_snapshot.set_value(AttributeService.ACTION_POINTS_ID(), 2);
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 10);
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 10);
        unit.attribute_snapshot.set_value("agility", 10);
        unit.attribute_snapshot.set_value("constitution", 10);
        unit.attribute_snapshot.set_value("intelligence", 10);
        unit.attribute_snapshot.set_value("willpower", 10);
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

    private void AssertEq(int actual, int expected, string message)
    {
        if (actual != expected)
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
