using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_trait_trigger_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private static StringName HalflingLuck =>
        TraitContentRules.ToStringName(TraitEffectKind.HalflingLuck);
    private static StringName SavageAttacks =>
        TraitContentRules.ToStringName(TraitEffectKind.SavageAttacks);
    private static StringName RelentlessEndurance =>
        TraitContentRules.ToStringName(TraitEffectKind.RelentlessEndurance);

    public override void _Initialize()
    {
        int exitCode = Run();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(exitCode);
    }

    private int Run()
    {
        TestHalflingLuckRerollsNaturalOneAttack();
        TestSavageAttacksAddsOneWeaponDieOnMeleeCrit();
        TestRelentlessEndurancePrecedesDeathWard();
        TestTurnStartRefreshesHalflingLuck();
        TestEffectiveStackByInstanceUsesIndependentChargeKeys();
        TestPassiveEffectiveTraitChargeSeedsByResetTiming();
        TestTraitDispatchContentRulesMatchRuntimeMethods();

        return _test.Finish("Trait trigger regression");
    }

    private void TestHalflingLuckRerollsNaturalOneAttack()
    {
        var resolver = new BattleDamageResolver();
        BattleUnitState source = BuildUnit("halfling_attacker", "player", 20);
        source.effective_trait_instances = TraitTestData.EffectiveTraits(
            EffectiveTraitPayload(HalflingLuck, HalflingLuck, "on_natural_one", "per_turn", "turn_start")
        );
        source.effective_trait_ids = new Godot.Collections.Array<StringName> { HalflingLuck };
        BattleUnitState target = BuildUnit("halfling_target", "enemy", 20);
        CombatEffectDef effect = MakeDamageEffect(5, false);
        var attackContext = new AttackContext(new[] { 1, 20 });

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveAttackEffects(
            source,
            target,
            new GArray { effect },
            new AttackCheckInput(requiredRoll: 99, displayRequiredRoll: 20, hitRatePercent: 5),
            attackContext
        ));

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
            source.per_turn_charges.Get(HalflingLuck, -1),
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
        source.effective_trait_instances = TraitTestData.EffectiveTraits(
            EffectiveTraitPayload(SavageAttacks, SavageAttacks, "on_crit", "none", "none")
        );
        source.effective_trait_ids = new Godot.Collections.Array<StringName> { SavageAttacks };
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

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(
            source,
            target,
            new GArray { effect },
            new GDictionary { ["critical_hit"] = true }
        ));
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
        target.effective_trait_instances = TraitTestData.EffectiveTraits(
            EffectiveTraitPayload(
                RelentlessEndurance,
                RelentlessEndurance,
                "on_fatal_damage",
                "per_battle",
                "battle_start"
            )
        );
        target.effective_trait_ids = new Godot.Collections.Array<StringName> { RelentlessEndurance };
        SetStatus(target, "death_ward");
        CombatEffectDef effect = MakeDamageEffect(99, false);

        GDictionary result = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, target, new GArray { effect }));
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

        GDictionary secondResult = AttackEffectResolutionResultReader.BuildGodotPayload(resolver.ResolveEffects(source, target, new GArray { effect }));
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
        unit.effective_trait_instances = TraitTestData.EffectiveTraits(
            EffectiveTraitPayload(HalflingLuck, HalflingLuck, "on_natural_one", "per_turn", "turn_start")
        );
        unit.effective_trait_ids = new Godot.Collections.Array<StringName> { HalflingLuck };
        TraitDispatchResult battleStartResult = hooks.OnBattleStartResult(unit);

        _test.True(
            !battleStartResult.Changed,
            "battle start should not initialize turn-start trait charges."
        );
        _test.Eq(
            DictInt(unit.per_turn_charges, HalflingLuck, -1),
            -1,
            "battle start should leave halfling_luck unseeded when reset timing is turn_start."
        );
        TraitDispatchResult turnStartResult = hooks.OnTurnStartResult(unit);
        _test.Eq(
            DictInt(unit.per_turn_charges, HalflingLuck, -1),
            1,
            "turn start should initialize halfling_luck."
        );
        _test.True(turnStartResult.Changed, "turn start should report trait charge initialization.");

        AttackTraitTriggerResult firstResult = hooks.OnNaturalOne(unit, 1, 20);
        _test.True(
            firstResult.Triggered,
            "halfling_luck should trigger after turn start initialization."
        );
        _test.Eq(
            unit.per_turn_charges.Get(HalflingLuck, -1),
            0,
            "halfling_luck charge should be spent after use."
        );
        unit.ResetPerTurnCharges();
        TraitDispatchResult refreshResult = hooks.OnTurnStartResult(unit);
        _test.Eq(
            unit.per_turn_charges.Get(HalflingLuck, -1),
            1,
            "turn start should refresh halfling_luck."
        );
        _test.True(refreshResult.Changed, "turn start should report trait charge refresh.");
    }

    private void TestEffectiveStackByInstanceUsesIndependentChargeKeys()
    {
        var hooks = new TraitTriggerHooks();
        BattleUnitState unit = BuildUnit("effective_halfling", "player", 20);
        unit.effective_trait_instances = TraitTestData.EffectiveTraits(
            EffectiveTraitPayload(HalflingLuck, "luck_a", "on_natural_one", "per_turn", "turn_start"),
            EffectiveTraitPayload(HalflingLuck, "luck_b", "on_natural_one", "per_turn", "turn_start")
        );
        unit.effective_trait_ids = new Godot.Collections.Array<StringName> { HalflingLuck };

        TraitDispatchResult turnStartResult = hooks.OnTurnStartResult(unit);
        _test.True(turnStartResult.Changed, "effective per-turn traits should seed on turn start.");
        _test.Eq(DictInt(unit.per_turn_charges, "luck_a", -1), 1, "first effective key should seed.");
        _test.Eq(DictInt(unit.per_turn_charges, "luck_b", -1), 1, "second effective key should seed.");

        AttackTraitTriggerResult first = hooks.OnNaturalOne(unit, 1, 20);
        AttackTraitTriggerResult second = hooks.OnNaturalOne(unit, 1, 20);
        AttackTraitTriggerResult third = hooks.OnNaturalOne(unit, 1, 20);

        _test.True(first.Triggered, "first effective trait instance should trigger.");
        _test.Eq(first.ChargeKey, new StringName("luck_a"), "first trigger should use first effective key.");
        _test.True(second.Triggered, "second effective trait instance should trigger after first key is spent.");
        _test.Eq(second.ChargeKey, new StringName("luck_b"), "second trigger should use second effective key.");
        _test.True(!third.Triggered, "no effective instance should trigger after both keys are spent.");
    }

    private void TestPassiveEffectiveTraitChargeSeedsByResetTiming()
    {
        var hooks = new TraitTriggerHooks();
        BattleUnitState unit = BuildUnit("passive_charge_trait", "player", 20);
        unit.effective_trait_instances = TraitTestData.EffectiveTraits(
            EffectiveTraitPayload(HalflingLuck, "passive_luck", "passive", "per_turn", "turn_start")
        );
        unit.effective_trait_ids = new Godot.Collections.Array<StringName> { HalflingLuck };

        TraitDispatchResult turnStartResult = hooks.OnTurnStartResult(unit);
        _test.True(
            turnStartResult.Changed,
            "turn start should seed charge-bearing effective traits even when trigger_type is passive."
        );
        _test.Eq(
            DictInt(unit.per_turn_charges, "passive_luck", -1),
            1,
            "passive trigger effective trait should seed by charge_reset_timing."
        );
    }

    private void TestTraitDispatchContentRulesMatchRuntimeMethods()
    {
        var hooks = new TraitTriggerHooks();
        foreach (TraitTriggerDispatchRule rule in TraitTriggerContentRules.GetDispatchTriggerRules())
        {
            StringName traitId = TraitContentRules.ToStringName(rule.TraitKind);
            StringName triggerType = TraitTriggerContentRules.ToStringName(rule.TriggerKind);
            string dispatchKey = TraitTriggerContentRules.GetDispatchKey(traitId, triggerType);
            _test.True(
                !string.IsNullOrEmpty(dispatchKey),
                $"content dispatch should expose a dispatch key for {traitId}/{triggerType}."
            );
            _test.Eq(
                rule.DispatchKey,
                dispatchKey,
                "content dispatch key should match the typed dispatch rule."
            );
            _test.True(
                TraitTriggerHooks.HasDispatchForTraitTrigger(traitId, triggerType),
                "runtime static dispatch query should agree with content dispatch table."
            );
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

    private static BattleEffectiveTraitInstanceState EffectiveTraitPayload(
        StringName traitId,
        StringName effectiveKey,
        StringName triggerType,
        StringName chargeScope,
        StringName chargeResetTiming
    ) => TraitTestData.EffectiveTrait(
        traitId,
        effectiveKey,
        triggerType,
        chargeScope,
        chargeResetTiming,
        effectType: traitId
    );

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

    private static int DictInt(BattleStringNameIntMap source, StringName key, int fallback)
    {
        return source?.Get(key, fallback) ?? fallback;
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
