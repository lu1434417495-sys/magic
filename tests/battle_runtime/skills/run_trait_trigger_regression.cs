using Godot;
using System.Collections.Generic;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_trait_trigger_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private static StringName HalflingLuck =>
        RaceTraitDef.ToStringName(RaceTraitEffectKind.HalflingLuck);
    private static StringName SavageAttacks =>
        RaceTraitDef.ToStringName(RaceTraitEffectKind.SavageAttacks);
    private static StringName RelentlessEndurance =>
        RaceTraitDef.ToStringName(RaceTraitEffectKind.RelentlessEndurance);

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

        return _test.Finish("Trait trigger regression");
    }

    private void TestHalflingLuckRerollsNaturalOneAttack()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = BuildUnit("halfling_attacker", "player", 20);
        source.race_trait_ids = new Godot.Collections.Array<StringName>
        {
            HalflingLuck,
        };
        BattleUnitState target = BuildUnit("halfling_target", "enemy", 20);
        CombatEffectDef effect = MakeDamageEffect(5, false);
        var attackContext = new AttackContext(new[] { 1, 20 });

        GDictionary result = resolver.ResolveAttackEffects(
            source,
            target,
            new GArray { effect },
            new AttackCheckInput(requiredRoll: 99, displayRequiredRoll: 20, hitRatePercent: 5),
            attackContext
        );

        _test.True(
            DictBool(result, "attack_success", false),
            "halfling_luck reroll should turn a natural 1 into the overridden natural 20 success."
        );
        _test.Eq(
            DictInt(result, "hit_roll", 0),
            20,
            "halfling_luck should expose the rerolled hit_roll."
        );
        _test.Eq(
            DictInt(source.per_turn_charges, HalflingLuck, -1),
            0,
            "halfling_luck should consume its per-turn charge."
        );
        AssertHasTraitResult(
            result,
            HalflingLuck,
            "attack result should record halfling_luck."
        );
    }

    private void TestSavageAttacksAddsOneWeaponDieOnMeleeCrit()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = BuildUnit("savage_attacker", "player", 20);
        source.race_trait_ids = new Godot.Collections.Array<StringName>
        {
            SavageAttacks,
        };
        source.SetUnarmedWeaponProjection(
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

        GDictionary result = resolver.ResolveEffects(
            source,
            target,
            new GArray { effect },
            new GDictionary { ["critical_hit"] = true }
        );
        GDictionary damageEvent = FirstDamageEvent(result);

        _test.Eq(
            DictInt(damageEvent, "trait_extra_weapon_damage_dice_count", 0),
            1,
            "savage_attacks should add exactly one extra weapon die on melee crit."
        );
        _test.Eq(
            DictInt(damageEvent, "trait_extra_weapon_damage_dice_sides", 0),
            6,
            "savage_attacks should reuse the current melee weapon die size."
        );
        AssertHasTraitResult(
            damageEvent,
            SavageAttacks,
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
            RelentlessEndurance,
        };
        SetStatus(target, "death_ward");
        CombatEffectDef effect = MakeDamageEffect(99, false);

        GDictionary result = resolver.ResolveEffects(source, target, new GArray { effect });
        _test.Eq(target.current_hp, 1, "relentless_endurance should clamp fatal damage to 1 HP.");
        _test.True(target.is_alive, "relentless_endurance should keep the target alive.");
        _test.True(
            target.HasStatusEffect("death_ward"),
            "relentless_endurance should trigger before death_ward consumption."
        );
        AssertHasTraitResult(
            FirstDamageEvent(result),
            RelentlessEndurance,
            "fatal damage event should record relentless_endurance."
        );

        GDictionary secondResult = resolver.ResolveEffects(source, target, new GArray { effect });
        _test.Eq(
            target.current_hp,
            0,
            "relentless_endurance should not trigger a second time in the same battle."
        );
        _test.True(
            !target.is_alive,
            "relentless_endurance spent charge should allow the next fatal damage to kill."
        );
        _test.True(
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
            HalflingLuck,
        };
        TraitDispatchResult battleStartResult = hooks.OnBattleStartResult(unit);
        AttackTraitTriggerResult firstResult = hooks.OnNaturalOne(unit, 1, 20);

        _test.True(
            battleStartResult.Changed,
            "battle start should initialize trait charges for dispatch traits."
        );
        _test.True(
            firstResult.Triggered,
            "halfling_luck should trigger after battle start initialization."
        );
        _test.Eq(
            DictInt(unit.per_turn_charges, HalflingLuck, -1),
            0,
            "halfling_luck charge should be spent after use."
        );
        unit.ResetPerTurnCharges();
        TraitDispatchResult turnStartResult = hooks.OnTurnStartResult(unit);
        _test.Eq(
            DictInt(unit.per_turn_charges, HalflingLuck, -1),
            1,
            "turn start should refresh halfling_luck."
        );
        _test.True(turnStartResult.Changed, "turn start should report trait charge refresh.");
    }

    private void TestTraitDispatchContentRulesMatchRuntimeMethods()
    {
        var hooks = new TraitTriggerHooks();
        IReadOnlyDictionary<RaceTraitEffectKind, IReadOnlyDictionary<TraitTriggerKind, string>> dispatchTriggerTypes =
            TraitTriggerContentRules.GetDispatchTriggerTypes();
        foreach (StringName traitId in TraitTriggerContentRules.GetDispatchTraitIds())
        {
            RaceTraitEffectKind traitKind = RaceTraitDef.ToEffectKind(traitId);
            IReadOnlyDictionary<TraitTriggerKind, string> triggerMap =
                dispatchTriggerTypes.GetValueOrDefault(
                    traitKind,
                    new Dictionary<TraitTriggerKind, string>()
                );
            foreach (TraitTriggerKind triggerKind in triggerMap.Keys)
            {
                StringName triggerType = TraitTriggerContentRules.ToStringName(triggerKind);
                string dispatchKey = TraitTriggerContentRules.GetDispatchKey(
                    traitId,
                    triggerType
                );
                _test.True(
                    !string.IsNullOrEmpty(dispatchKey),
                    $"content dispatch should expose a dispatch key for {traitId}/{triggerType}."
                );
                _test.True(
                    TraitTriggerHooks.HasDispatchForTraitTrigger(traitId, triggerType),
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
        unit.RefreshFootprint();
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
        unit.SetStatusEffect(status);
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
        _test.Fail(message);
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
