using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_execute_effect_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestExecuteFinishesLowHpTarget();
        TestExecuteDoesNothingOnHighHpTarget();
        TestExecuteIgnoresBossTargetFlag();
        TestExecuteBypassesShieldWithoutMutation();
        TestExecuteAppliesSoulFractureOnSuccessfulSave();
        TestExecuteMinHpNeverHeals();
        RequestTestExit(_test.Finish("Battle execute effect regression"));
    }

    private void TestExecuteFinishesLowHpTarget()
    {
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("weak_target", "hostile");
        target.current_hp = 1;
        CombatEffectDefinition effect = MakeExecuteEffect();
        var resolver = new BattleDamageResolver();
        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(
            source,
            target,
            new[] { effect },
            DamageResolutionContext.FromDictionary(new GDictionary { ["save_roll_override"] = 1 })
        ));

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

    private void TestExecuteDoesNothingOnHighHpTarget()
    {
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("healthy_target", "hostile");
        target.current_hp = 30;
        CombatEffectDefinition effect = MakeExecuteEffect();
        var resolver = new BattleDamageResolver();
        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, target, new[] { effect }));

        _test.True(target.is_alive, "execute on high-HP target should leave target alive.");
        _test.Eq(target.current_hp, 30, "high-HP execute should not deal non-lethal damage.");
        _test.Eq(DictInt(result, "damage"), 0, "high-HP execute should not register damage.");
    }

    private void TestExecuteIgnoresBossTargetFlag()
    {
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("boss_target", "hostile");
        target.attribute_snapshot.SetValue("boss_target", 1);
        target.current_hp = 5;
        CombatEffectDefinition effect = MakeExecuteEffect();
        var resolver = new BattleDamageResolver();
        resolver.ResolveEffects(
            source,
            target,
            new[] { effect },
            DamageResolutionContext.FromDictionary(new GDictionary { ["save_roll_override"] = 1 })
        );

        _test.False(target.is_alive, "PWK should ignore boss_target for low-HP fatal execute.");
        _test.Eq(target.current_hp, 0, "boss_target should not clamp PWK fatal execute.");
    }

    private void TestExecuteBypassesShieldWithoutMutation()
    {
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("shielded_target", "hostile");
        target.current_hp = 5;
        target.current_shield_hp = 20;
        target.shield_max_hp = 20;
        target.shield_duration = 10;
        CombatEffectDefinition effect = MakeExecuteEffect();
        var resolver = new BattleDamageResolver();
        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(
            source,
            target,
            new[] { effect },
            DamageResolutionContext.FromDictionary(new GDictionary { ["save_roll_override"] = 1 })
        ));
        GDictionary firstEvent = FirstDamageEvent(result);

        _test.Eq(DictInt(firstEvent, "shield_absorbed"), 0, "PWK should bypass shield absorption.");
        _test.Eq(target.current_shield_hp, 20, "PWK should not drain shield HP.");
        _test.Eq(target.shield_max_hp, 20, "PWK should not mutate shield max HP.");
        _test.Eq(target.shield_duration, 10, "PWK should not mutate shield duration.");
        _test.False(target.is_alive, "failed-save PWK should kill through shield.");
    }

    private void TestExecuteAppliesSoulFractureOnSuccessfulSave()
    {
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("cursed_target", "hostile");
        target.current_hp = 5;
        CombatEffectDefinition effect = MakeExecuteEffect(
            soulFractureDurationTu: 60,
            healMultiplierPercent: 50,
            shieldGainMultiplierPercent: 40
        );
        var resolver = new BattleDamageResolver();

        resolver.ResolveEffects(
            source,
            target,
            new[] { effect },
            DamageResolutionContext.FromDictionary(new GDictionary { ["save_roll_override"] = 20 })
        );

        _test.True(target.is_alive, "successful save should leave the low-HP target alive.");
        _test.True(target.HasStatusEffect("soul_fracture"), "successful-save PWK survivor should receive soul fracture.");
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

    private static CombatEffectDefinition MakeExecuteEffect(
        int soulFractureDurationTu = 60,
        int healMultiplierPercent = 50,
        int shieldGainMultiplierPercent = 50
    )
    {
        return TestSkillDefinitionProjection.BuildEffect(
            "execute",
            saveDcMode: "static",
            saveDc: 10,
            saveAbility: "willpower",
            saveTag: "magic",
            thresholdMaxHpRatioPercent: 20,
            thresholdLevelAnchor: 17,
            thresholdLevelBonusPerDelta: 5,
            thresholdCapMaxHpRatioPercent: 50,
            soulFractureDurationTu: soulFractureDurationTu,
            healMultiplierPercent: healMultiplierPercent,
            shieldGainMultiplierPercent: shieldGainMultiplierPercent
        );
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
