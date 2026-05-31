using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_trait_trigger_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestHalflingLuckRerollsNaturalOneAttack();
        TestSavageAttacksAddsOneWeaponDieOnMeleeCrit();
        TestRelentlessEndurancePrecedesDeathWard();
        TestTurnStartRefreshesHalflingLuck();
        TestTraitDispatchContentRulesMatchRuntimeMethods();

        if (_failures.Count == 0)
        {
            GD.Print("Trait trigger regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Trait trigger regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestHalflingLuckRerollsNaturalOneAttack()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = BuildUnit("halfling_attacker", "player", 20);
        source.race_trait_ids = new Godot.Collections.Array<StringName>
        {
            TraitTriggerHooks.TRAIT_HALFLING_LUCK(),
        };
        BattleUnitState target = BuildUnit("halfling_target", "enemy", 20);
        CombatEffectDef effect = MakeDamageEffect(5, false);
        var attackContext = new AttackContext(new[] { 1, 20 });

        GDictionary result = resolver.resolve_attack_effects(
            source,
            target,
            new GArray { effect },
            new AttackCheckInput(requiredRoll: 99, displayRequiredRoll: 20, hitRatePercent: 5),
            attackContext
        );

        AssertTrue(
            DictBool(result, "attack_success", false),
            "halfling_luck reroll should turn a natural 1 into the overridden natural 20 success."
        );
        AssertEq(
            DictInt(result, "hit_roll", 0),
            20,
            "halfling_luck should expose the rerolled hit_roll."
        );
        AssertEq(
            DictInt(source.per_turn_charges, TraitTriggerHooks.TRAIT_HALFLING_LUCK(), -1),
            0,
            "halfling_luck should consume its per-turn charge."
        );
        AssertHasTraitResult(
            result,
            TraitTriggerHooks.TRAIT_HALFLING_LUCK(),
            "attack result should record halfling_luck."
        );
    }

    private void TestSavageAttacksAddsOneWeaponDieOnMeleeCrit()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = BuildUnit("savage_attacker", "player", 20);
        source.race_trait_ids = new Godot.Collections.Array<StringName>
        {
            TraitTriggerHooks.TRAIT_SAVAGE_ATTACKS(),
        };
        source.set_unarmed_weapon_projection(
            "physical_slash",
            new GDictionary
            {
                ["dice_count"] = 1,
                ["dice_sides"] = 6,
                ["flat_bonus"] = 0,
            },
            1
        );
        BattleUnitState target = BuildUnit("savage_target", "enemy", 100);
        CombatEffectDef effect = MakeDamageEffect(0, true);

        GDictionary result = resolver.resolve_effects(
            source,
            target,
            new GArray { effect },
            new GDictionary { ["critical_hit"] = true }
        );
        GDictionary damageEvent = FirstDamageEvent(result);

        AssertEq(
            DictInt(damageEvent, "trait_extra_weapon_damage_dice_count", 0),
            1,
            "savage_attacks should add exactly one extra weapon die on melee crit."
        );
        AssertEq(
            DictInt(damageEvent, "trait_extra_weapon_damage_dice_sides", 0),
            6,
            "savage_attacks should reuse the current melee weapon die size."
        );
        AssertHasTraitResult(
            damageEvent,
            TraitTriggerHooks.TRAIT_SAVAGE_ATTACKS(),
            "damage event should record savage_attacks."
        );
    }

    private void TestRelentlessEndurancePrecedesDeathWard()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = BuildUnit("fatal_source", "enemy", 20);
        BattleUnitState target = BuildUnit("relentless_target", "player", 8);
        target.race_trait_ids = new Godot.Collections.Array<StringName>
        {
            TraitTriggerHooks.TRAIT_RELENTLESS_ENDURANCE(),
        };
        SetStatus(target, "death_ward");
        CombatEffectDef effect = MakeDamageEffect(99, false);

        GDictionary result = resolver.resolve_effects(source, target, new GArray { effect });
        AssertEq(target.current_hp, 1, "relentless_endurance should clamp fatal damage to 1 HP.");
        AssertTrue(target.is_alive, "relentless_endurance should keep the target alive.");
        AssertTrue(
            target.has_status_effect("death_ward"),
            "relentless_endurance should trigger before death_ward consumption."
        );
        AssertHasTraitResult(
            FirstDamageEvent(result),
            TraitTriggerHooks.TRAIT_RELENTLESS_ENDURANCE(),
            "fatal damage event should record relentless_endurance."
        );

        GDictionary secondResult = resolver.resolve_effects(source, target, new GArray { effect });
        AssertEq(
            target.current_hp,
            0,
            "relentless_endurance should not trigger a second time in the same battle."
        );
        AssertTrue(
            !target.is_alive,
            "relentless_endurance spent charge should allow the next fatal damage to kill."
        );
        AssertTrue(
            GetArray(FirstDamageEvent(secondResult), "trait_trigger_results").Count == 0,
            "second fatal damage should not record a spent relentless_endurance."
        );
    }

    private void TestTurnStartRefreshesHalflingLuck()
    {
        var hooks = new TraitTriggerHooks();
        BattleUnitState unit = BuildUnit("turn_halfling", "player", 20);
        unit.race_trait_ids = new Godot.Collections.Array<StringName>
        {
            TraitTriggerHooks.TRAIT_HALFLING_LUCK(),
        };
        hooks.on_battle_start(unit, new GDictionary());
        GDictionary firstResult = hooks.on_natural_one(
            unit,
            new GDictionary
            {
                ["roll"] = 1,
                ["die_size"] = 20,
            }
        );

        AssertTrue(
            DictBool(firstResult, "triggered", false),
            "halfling_luck should trigger after battle start initialization."
        );
        AssertEq(
            DictInt(unit.per_turn_charges, TraitTriggerHooks.TRAIT_HALFLING_LUCK(), -1),
            0,
            "halfling_luck charge should be spent after use."
        );
        unit.reset_per_turn_charges();
        hooks.on_turn_start(unit, new GDictionary());
        AssertEq(
            DictInt(unit.per_turn_charges, TraitTriggerHooks.TRAIT_HALFLING_LUCK(), -1),
            1,
            "turn start should refresh halfling_luck."
        );
    }

    private void TestTraitDispatchContentRulesMatchRuntimeMethods()
    {
        var hooks = new TraitTriggerHooks();
        GDictionary dispatchTriggerTypes = TraitTriggerContentRules.get_dispatch_trigger_types();
        foreach (Variant traitValue in TraitTriggerContentRules.get_dispatch_trait_ids())
        {
            StringName traitId = traitValue.AsStringName();
            GDictionary triggerMap = dispatchTriggerTypes.GetValueOrDefault(traitId, new GDictionary()).AsGodotDictionary();
            foreach (Variant triggerTypeValue in triggerMap.Keys)
            {
                StringName triggerType = triggerTypeValue.AsStringName();
                StringName methodName = TraitTriggerContentRules.get_dispatch_method_name(
                    traitId,
                    triggerType
                );
                AssertTrue(
                    !string.IsNullOrEmpty(methodName.ToString()),
                    $"content dispatch should expose a method for {traitId}/{triggerType}."
                );
                AssertTrue(
                    hooks.HasMethod(methodName),
                    $"runtime hooks should implement content dispatch method {methodName}."
                );
                AssertTrue(
                    TraitTriggerHooks.has_dispatch_for_trait_trigger(traitId, triggerType),
                    "runtime static dispatch query should agree with content dispatch table."
                );
            }
        }
    }

    private static BattleUnitState BuildUnit(StringName unitId, StringName factionId, int hp)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            coord = Vector2I.Zero,
            current_hp = hp,
            current_ap = 1,
            body_size = 1,
            is_alive = hp > 0,
        };
        unit.refresh_footprint();
        return unit;
    }

    private static CombatEffectDef MakeDamageEffect(int power, bool addWeaponDice)
    {
        var effect = new CombatEffectDef
        {
            effect_type = "damage",
            power = power,
            damage_tag = "physical_slash",
        };
        if (addWeaponDice)
        {
            effect.add_weapon_dice = true;
        }
        return effect;
    }

    private static void SetStatus(BattleUnitState unit, StringName statusId)
    {
        var status = new BattleStatusEffectState
        {
            status_id = statusId,
            source_unit_id = "",
            @params = new GDictionary(),
        };
        unit.set_status_effect(status);
    }

    private static GDictionary FirstDamageEvent(GDictionary result)
    {
        GArray events = GetArray(result, "damage_events");
        if (events.Count == 0)
        {
            return new GDictionary();
        }
        return events[0].AsGodotDictionary();
    }

    private void AssertHasTraitResult(GDictionary result, StringName traitId, string message)
    {
        foreach (Variant triggerResultValue in GetArray(result, "trait_trigger_results"))
        {
            GDictionary triggerResult = triggerResultValue.AsGodotDictionary();
            if (triggerResult.Count == 0)
            {
                continue;
            }
            if (DictStringName(triggerResult, "trait_id") == traitId)
            {
                return;
            }
        }
        _failures.Add(message);
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }

    private static GArray GetArray(GDictionary dictionary, Variant key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return new GArray();
        }
        return dictionary[key].AsGodotArray();
    }

    private static int DictInt(GDictionary dictionary, Variant key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        return dictionary[key].AsInt32();
    }

    private static bool DictBool(GDictionary dictionary, Variant key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        return dictionary[key].AsBool();
    }

    private static StringName DictStringName(GDictionary dictionary, Variant key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return new StringName("");
        }
        return dictionary[key].AsStringName();
    }
}
