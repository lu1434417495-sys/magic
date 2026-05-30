using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class TraitTriggerHooks : RefCounted
{
    private static readonly StringName TriggerPassive = "passive";
    private static readonly StringName TriggerOnNaturalOne = "on_natural_one";
    private static readonly StringName TriggerOnCrit = "on_crit";
    private static readonly StringName TriggerOnFatalDamage = "on_fatal_damage";
    private static readonly StringName TriggerOnBattleStart = "on_battle_start";
    private static readonly StringName TriggerOnTurnStart = "on_turn_start";

    private static readonly StringName TraitHalflingLuck = "halfling_luck";
    private static readonly StringName TraitSavageAttacks = "savage_attacks";
    private static readonly StringName TraitRelentlessEndurance = "relentless_endurance";

    public static StringName TRIGGER_PASSIVE() => TriggerPassive;

    public static StringName TRIGGER_ON_NATURAL_ONE() => TriggerOnNaturalOne;

    public static StringName TRIGGER_ON_CRIT() => TriggerOnCrit;

    public static StringName TRIGGER_ON_FATAL_DAMAGE() => TriggerOnFatalDamage;

    public static StringName TRIGGER_ON_BATTLE_START() => TriggerOnBattleStart;

    public static StringName TRIGGER_ON_TURN_START() => TriggerOnTurnStart;

    public static StringName TRAIT_HALFLING_LUCK() => TraitHalflingLuck;

    public static StringName TRAIT_SAVAGE_ATTACKS() => TraitSavageAttacks;

    public static StringName TRAIT_RELENTLESS_ENDURANCE() => TraitRelentlessEndurance;

    public static bool has_dispatch_for_trait_trigger(StringName trait_id, StringName trigger_type)
    {
        return TraitTriggerContentRules.has_dispatch_for_trait_trigger(
            ProgressionDataUtils.to_string_name(trait_id),
            ProgressionDataUtils.to_string_name(trigger_type)
        );
    }

    public static GStringNameArray get_dispatch_trait_ids()
    {
        return TraitTriggerContentRules.get_dispatch_trait_ids();
    }

    public GDictionary on_natural_one(BattleUnitState unit_state, GDictionary context = null)
    {
        return DispatchFirst(unit_state, TriggerOnNaturalOne, context ?? new GDictionary());
    }

    public AttackTraitTriggerResult on_natural_one_typed(
        BattleUnitState unit_state,
        int roll,
        int die_size
    )
    {
        foreach (StringName traitId in GetUnitTraitIds(unit_state))
        {
            string methodName = TraitTriggerContentRules.get_dispatch_method_name(
                traitId,
                TriggerOnNaturalOne
            );
            if (methodName != "_handle_halfling_luck")
            {
                continue;
            }
            AttackTraitTriggerResult result = _handle_halfling_luck_typed(
                unit_state,
                roll,
                die_size
            );
            if (!result.Triggered)
            {
                continue;
            }
            return new AttackTraitTriggerResult(
                triggered: result.Triggered,
                @event: TriggerOnNaturalOne,
                traitId: traitId,
                effectType: result.EffectType,
                originalRoll: result.OriginalRoll,
                rerollDie: result.RerollDie,
                rerolledRoll: result.RerolledRoll,
                dieSize: result.DieSize,
                chargeKey: result.ChargeKey,
                chargesRemaining: result.ChargesRemaining
            );
        }
        return new AttackTraitTriggerResult(@event: TriggerOnNaturalOne);
    }

    public GDictionary on_crit(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GDictionary context = null
    )
    {
        GDictionary eventContext = DuplicateDictionary(context);
        eventContext["target_unit"] = target_unit;
        return DispatchFirst(source_unit, TriggerOnCrit, eventContext);
    }

    public GDictionary on_fatal_damage(
        BattleUnitState target_unit,
        BattleUnitState source_unit,
        GDictionary context = null
    )
    {
        GDictionary eventContext = DuplicateDictionary(context);
        eventContext["source_unit"] = source_unit;
        return DispatchFirst(target_unit, TriggerOnFatalDamage, eventContext);
    }

    public GDictionary on_battle_start(BattleUnitState unit_state, GDictionary context = null)
    {
        bool changed = false;
        if (UnitHasTrait(unit_state, TraitHalflingLuck))
        {
            SetCharge(unit_state, GetTraitChargeKey(TraitHalflingLuck), 1, true, true);
            changed = true;
        }
        if (UnitHasTrait(unit_state, TraitRelentlessEndurance))
        {
            SetCharge(unit_state, GetTraitChargeKey(TraitRelentlessEndurance), 1, false, true);
            changed = true;
        }
        GDictionary dispatchResult = DispatchAll(
            unit_state,
            TriggerOnBattleStart,
            context ?? new GDictionary()
        );
        return new GDictionary
        {
            ["triggered"] = GdInterop.GetBool(dispatchResult, "triggered"),
            ["changed"] = changed || GdInterop.GetBool(dispatchResult, "changed"),
            ["event"] = TriggerOnBattleStart,
            ["results"] = GdInterop.GetArray(dispatchResult, "results"),
        };
    }

    public GDictionary on_turn_start(BattleUnitState unit_state, GDictionary context = null)
    {
        bool changed = false;
        if (UnitHasTrait(unit_state, TraitHalflingLuck))
        {
            SetCharge(unit_state, GetTraitChargeKey(TraitHalflingLuck), 1, true, true);
            changed = true;
        }
        GDictionary dispatchResult = DispatchAll(
            unit_state,
            TriggerOnTurnStart,
            context ?? new GDictionary()
        );
        return new GDictionary
        {
            ["triggered"] = GdInterop.GetBool(dispatchResult, "triggered"),
            ["changed"] = changed || GdInterop.GetBool(dispatchResult, "changed"),
            ["event"] = TriggerOnTurnStart,
            ["results"] = GdInterop.GetArray(dispatchResult, "results"),
        };
    }

    private GDictionary DispatchFirst(
        BattleUnitState unitState,
        StringName triggerType,
        GDictionary context
    )
    {
        foreach (StringName traitId in GetUnitTraitIds(unitState))
        {
            string methodName = TraitTriggerContentRules.get_dispatch_method_name(
                traitId,
                triggerType
            );
            if (string.IsNullOrEmpty(methodName))
            {
                continue;
            }
            GDictionary result = DispatchHandler(methodName, unitState, context);
            if (!GdInterop.GetBool(result, "triggered"))
            {
                continue;
            }
            result["trait_id"] = traitId;
            result["event"] = triggerType;
            return result;
        }
        return BuildEmptyResult(triggerType);
    }

    private GDictionary DispatchAll(
        BattleUnitState unitState,
        StringName triggerType,
        GDictionary context
    )
    {
        var results = new GArray();
        foreach (StringName traitId in GetUnitTraitIds(unitState))
        {
            string methodName = TraitTriggerContentRules.get_dispatch_method_name(
                traitId,
                triggerType
            );
            if (string.IsNullOrEmpty(methodName))
            {
                continue;
            }
            GDictionary result = DispatchHandler(methodName, unitState, context);
            if (!GdInterop.GetBool(result, "triggered"))
            {
                continue;
            }
            result["trait_id"] = traitId;
            result["event"] = triggerType;
            results.Add(result);
        }
        return new GDictionary
        {
            ["triggered"] = results.Count > 0,
            ["changed"] = results.Count > 0,
            ["event"] = triggerType,
            ["results"] = results,
        };
    }

    private GDictionary DispatchHandler(
        string methodName,
        BattleUnitState unitState,
        GDictionary context
    )
    {
        return methodName switch
        {
            "_handle_halfling_luck" => _handle_halfling_luck(unitState, context),
            "_handle_savage_attacks" => _handle_savage_attacks(unitState, context),
            "_handle_relentless_endurance" => _handle_relentless_endurance(unitState, context),
            _ => BuildEmptyResult(new StringName("")),
        };
    }

    public GDictionary _handle_halfling_luck(BattleUnitState unitState, GDictionary context)
    {
        int roll = GdInterop.GetInt(context, "roll");
        if (roll != 1)
        {
            return BuildEmptyResult(TriggerOnNaturalOne);
        }
        StringName chargeKey = GetTraitChargeKey(TraitHalflingLuck);
        if (!ConsumeCharge(unitState, chargeKey, true, 1))
        {
            return BuildEmptyResult(TriggerOnNaturalOne);
        }
        return new GDictionary
        {
            ["triggered"] = true,
            ["effect_type"] = TraitHalflingLuck,
            ["original_roll"] = roll,
            ["reroll_die"] = true,
            ["die_size"] = Math.Max(GdInterop.GetInt(context, "die_size", 20), 1),
            ["charge_key"] = chargeKey,
            ["charges_remaining"] = GetCharge(unitState, chargeKey, true),
        };
    }

    public AttackTraitTriggerResult _handle_halfling_luck_typed(
        BattleUnitState unitState,
        int roll,
        int dieSize
    )
    {
        if (roll != 1)
        {
            return new AttackTraitTriggerResult(@event: TriggerOnNaturalOne);
        }
        StringName chargeKey = GetTraitChargeKey(TraitHalflingLuck);
        if (!ConsumeCharge(unitState, chargeKey, true, 1))
        {
            return new AttackTraitTriggerResult(@event: TriggerOnNaturalOne);
        }
        return new AttackTraitTriggerResult(
            triggered: true,
            @event: TriggerOnNaturalOne,
            effectType: TraitHalflingLuck,
            originalRoll: roll,
            rerollDie: true,
            dieSize: Math.Max(dieSize, 1),
            chargeKey: chargeKey,
            chargesRemaining: GetCharge(unitState, chargeKey, true)
        );
    }

    public GDictionary _handle_savage_attacks(BattleUnitState unitState, GDictionary context)
    {
        if (
            !GdInterop.GetBool(context, "critical_hit")
            || !GdInterop.GetBool(context, "add_weapon_dice")
        )
        {
            return BuildEmptyResult(TriggerOnCrit);
        }
        if (GdInterop.GetInt(context, "weapon_attack_range") > 1)
        {
            return BuildEmptyResult(TriggerOnCrit);
        }
        GDictionary weaponDice = GdInterop.GetDictionary(context, "weapon_dice");
        int diceSides = Math.Max(GdInterop.GetInt(weaponDice, "dice_sides"), 0);
        if (diceSides <= 0)
        {
            return BuildEmptyResult(TriggerOnCrit);
        }
        return new GDictionary
        {
            ["triggered"] = true,
            ["effect_type"] = TraitSavageAttacks,
            ["extra_weapon_dice_count"] = 1,
            ["extra_weapon_dice_sides"] = diceSides,
        };
    }

    public GDictionary _handle_relentless_endurance(BattleUnitState unitState, GDictionary context)
    {
        if (unitState == null)
        {
            return BuildEmptyResult(TriggerOnFatalDamage);
        }
        int projectedHp = GdInterop.GetInt(context, "projected_hp", unitState.current_hp);
        if (projectedHp > 0)
        {
            return BuildEmptyResult(TriggerOnFatalDamage);
        }
        StringName chargeKey = GetTraitChargeKey(TraitRelentlessEndurance);
        if (!ConsumeCharge(unitState, chargeKey, false, 1))
        {
            return BuildEmptyResult(TriggerOnFatalDamage);
        }
        return new GDictionary
        {
            ["triggered"] = true,
            ["effect_type"] = TraitRelentlessEndurance,
            ["clamp_to_hp"] = 1,
            ["projected_hp"] = projectedHp,
            ["hp_damage"] = GdInterop.GetInt(context, "hp_damage"),
            ["charge_key"] = chargeKey,
            ["charges_remaining"] = GetCharge(unitState, chargeKey, false),
        };
    }

    private static GStringNameArray GetUnitTraitIds(BattleUnitState unitState)
    {
        var traitIds = new GStringNameArray();
        if (unitState == null)
        {
            return traitIds;
        }
        AppendUniqueTraits(traitIds, unitState.race_trait_ids);
        AppendUniqueTraits(traitIds, unitState.subrace_trait_ids);
        AppendUniqueTraits(traitIds, unitState.bloodline_trait_ids);
        AppendUniqueTraits(traitIds, unitState.ascension_trait_ids);
        return traitIds;
    }

    private static void AppendUniqueTraits(GStringNameArray target, GStringNameArray values)
    {
        foreach (StringName rawValue in values)
        {
            StringName value = ProgressionDataUtils.to_string_name(rawValue);
            if (value == "" || target.Contains(value))
            {
                continue;
            }
            target.Add(value);
        }
    }

    private static bool UnitHasTrait(BattleUnitState unitState, StringName traitId)
    {
        return GetUnitTraitIds(unitState).Contains(traitId);
    }

    private static StringName GetTraitChargeKey(StringName traitId)
    {
        return new StringName($"trait_{traitId}");
    }

    private static void SetCharge(
        BattleUnitState unitState,
        StringName chargeKey,
        int value,
        bool perTurn,
        bool force = false
    )
    {
        if (unitState == null || chargeKey == "")
        {
            return;
        }
        GDictionary charges = perTurn ? unitState.per_turn_charges : unitState.per_battle_charges;
        if (force || !charges.ContainsKey(chargeKey))
        {
            charges[chargeKey] = Math.Max(value, 0);
        }
    }

    private static bool ConsumeCharge(
        BattleUnitState unitState,
        StringName chargeKey,
        bool perTurn,
        int defaultValue
    )
    {
        if (unitState == null || chargeKey == "")
        {
            return false;
        }
        GDictionary charges = perTurn ? unitState.per_turn_charges : unitState.per_battle_charges;
        if (!charges.ContainsKey(chargeKey))
        {
            charges[chargeKey] = Math.Max(defaultValue, 0);
        }
        int remaining = Math.Max(GdInterop.GetInt(charges, chargeKey), 0);
        if (remaining <= 0)
        {
            return false;
        }
        charges[chargeKey] = remaining - 1;
        return true;
    }

    private static int GetCharge(BattleUnitState unitState, StringName chargeKey, bool perTurn)
    {
        if (unitState == null || chargeKey == "")
        {
            return 0;
        }
        GDictionary charges = perTurn ? unitState.per_turn_charges : unitState.per_battle_charges;
        return Math.Max(GdInterop.GetInt(charges, chargeKey), 0);
    }

    private static GDictionary BuildEmptyResult(StringName triggerType)
    {
        return new GDictionary { ["triggered"] = false, ["event"] = triggerType };
    }

    private static GDictionary DuplicateDictionary(GDictionary value)
    {
        return value?.Duplicate(true) ?? new GDictionary();
    }
}
