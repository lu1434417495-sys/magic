using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal readonly struct TraitDispatchResult
{
    internal readonly bool Triggered;
    internal readonly bool Changed;
    internal readonly StringName Event;
    internal readonly GDictionary Payload;
    internal readonly GArray Results;

    internal TraitDispatchResult(
        bool triggered = false,
        bool changed = false,
        StringName @event = null,
        GDictionary payload = null,
        GArray results = null
    )
    {
        Triggered = triggered;
        Changed = changed;
        Event = @event ?? new StringName("");
        Payload = payload ?? new GDictionary();
        Results = results;
    }

    internal GDictionary ToDictionary()
    {
        GDictionary result = Payload.Duplicate(true);
        result["triggered"] = Triggered;
        result["changed"] = Changed;
        result["event"] = Event;
        if (Results != null)
        {
            result["results"] = Results;
        }
        return result;
    }
}

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

    private readonly record struct SavageAttacksContext(
        bool CriticalHit,
        bool AddWeaponDice,
        int WeaponAttackRange,
        int WeaponDiceSides
    )
    {
        public static SavageAttacksContext FromDictionary(GDictionary context)
        {
            GDictionary weaponDice = GetDict(context, "weapon_dice");
            return new SavageAttacksContext(
                ReadBool(context, "critical_hit"),
                ReadBool(context, "add_weapon_dice"),
                GetInt(context, "weapon_attack_range"),
                Math.Max(GetInt(weaponDice, "dice_sides"), 0)
            );
        }
    }

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
        var result = new GStringNameArray();
        foreach (StringName traitId in TraitTriggerContentRules.get_dispatch_trait_ids())
        {
            result.Add(traitId);
        }
        return result;
    }

    public GDictionary on_natural_one(BattleUnitState unit_state, GDictionary context = null)
    {
        return DispatchFirstResult(unit_state, TriggerOnNaturalOne, context ?? new GDictionary())
            .ToDictionary();
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
        return DispatchFirstResult(source_unit, TriggerOnCrit, eventContext).ToDictionary();
    }

    public AttackTraitTriggerResult on_crit_typed(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        bool criticalHit,
        bool addWeaponDice,
        int weaponAttackRange,
        int weaponDiceSides
    )
    {
        var attackContext = new SavageAttacksContext(
            criticalHit,
            addWeaponDice,
            Math.Max(weaponAttackRange, 0),
            Math.Max(weaponDiceSides, 0)
        );
        foreach (StringName traitId in GetUnitTraitIds(sourceUnit))
        {
            string methodName = TraitTriggerContentRules.get_dispatch_method_name(
                traitId,
                TriggerOnCrit
            );
            if (methodName != "_handle_savage_attacks")
            {
                continue;
            }
            AttackTraitTriggerResult result = HandleSavageAttacksTyped(
                sourceUnit,
                attackContext
            );
            if (!result.Triggered)
            {
                continue;
            }
            return new AttackTraitTriggerResult(
                triggered: true,
                @event: TriggerOnCrit,
                traitId: traitId,
                effectType: result.EffectType,
                extraWeaponDiceCount: result.ExtraWeaponDiceCount,
                extraWeaponDiceSides: result.ExtraWeaponDiceSides
            );
        }
        return new AttackTraitTriggerResult(@event: TriggerOnCrit);
    }

    public GDictionary on_fatal_damage(
        BattleUnitState target_unit,
        BattleUnitState source_unit,
        GDictionary context = null
    )
    {
        GDictionary eventContext = DuplicateDictionary(context);
        eventContext["source_unit"] = source_unit;
        return DispatchFirstResult(target_unit, TriggerOnFatalDamage, eventContext).ToDictionary();
    }

    public AttackTraitTriggerResult on_fatal_damage_typed(
        BattleUnitState targetUnit,
        BattleUnitState sourceUnit,
        int hpDamage,
        int projectedHp
    )
    {
        foreach (StringName traitId in GetUnitTraitIds(targetUnit))
        {
            string methodName = TraitTriggerContentRules.get_dispatch_method_name(
                traitId,
                TriggerOnFatalDamage
            );
            if (methodName != "_handle_relentless_endurance")
            {
                continue;
            }
            AttackTraitTriggerResult result = HandleRelentlessEnduranceTyped(
                targetUnit,
                hpDamage,
                projectedHp
            );
            if (!result.Triggered)
            {
                continue;
            }
            return new AttackTraitTriggerResult(
                triggered: true,
                @event: TriggerOnFatalDamage,
                traitId: traitId,
                effectType: result.EffectType,
                chargeKey: result.ChargeKey,
                chargesRemaining: result.ChargesRemaining,
                clampToHp: result.ClampToHp,
                projectedHp: result.ProjectedHp,
                hpDamage: result.HpDamage
            );
        }
        return new AttackTraitTriggerResult(@event: TriggerOnFatalDamage);
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
        TraitDispatchResult dispatchResult = DispatchAllResult(
            unit_state,
            TriggerOnBattleStart,
            context ?? new GDictionary()
        );
        return new TraitDispatchResult(
            dispatchResult.Triggered,
            changed || dispatchResult.Changed,
            TriggerOnBattleStart,
            results: dispatchResult.Results
        ).ToDictionary();
    }

    public GDictionary on_turn_start(BattleUnitState unit_state, GDictionary context = null)
    {
        return OnTurnStartResult(unit_state, context).ToDictionary();
    }

    internal TraitDispatchResult OnTurnStartResult(
        BattleUnitState unit_state,
        GDictionary context = null
    )
    {
        bool changed = false;
        if (UnitHasTrait(unit_state, TraitHalflingLuck))
        {
            SetCharge(unit_state, GetTraitChargeKey(TraitHalflingLuck), 1, true, true);
            changed = true;
        }
        TraitDispatchResult dispatchResult = DispatchAllResult(
            unit_state,
            TriggerOnTurnStart,
            context ?? new GDictionary()
        );
        return new TraitDispatchResult(
            dispatchResult.Triggered,
            changed || dispatchResult.Changed,
            TriggerOnTurnStart,
            results: dispatchResult.Results
        );
    }

    private GDictionary DispatchFirst(
        BattleUnitState unitState,
        StringName triggerType,
        GDictionary context
    )
    {
        return DispatchFirstResult(unitState, triggerType, context).ToDictionary();
    }

    private TraitDispatchResult DispatchFirstResult(
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
            TraitDispatchResult result = DispatchHandlerResult(methodName, unitState, context);
            if (!result.Triggered)
            {
                continue;
            }
            GDictionary payload = result.Payload.Duplicate(true);
            payload["trait_id"] = traitId;
            return new TraitDispatchResult(
                true,
                result.Changed,
                triggerType,
                payload,
                result.Results
            );
        }
        return BuildEmptyDispatchResult(triggerType);
    }

    private GDictionary DispatchAll(
        BattleUnitState unitState,
        StringName triggerType,
        GDictionary context
    )
    {
        return DispatchAllResult(unitState, triggerType, context).ToDictionary();
    }

    private TraitDispatchResult DispatchAllResult(
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
            TraitDispatchResult result = DispatchHandlerResult(methodName, unitState, context);
            if (!result.Triggered)
            {
                continue;
            }
            GDictionary payload = result.Payload.Duplicate(true);
            payload["trait_id"] = traitId;
            payload["event"] = triggerType;
            payload["triggered"] = true;
            results.Add(payload);
        }
        return new TraitDispatchResult(
            results.Count > 0,
            results.Count > 0,
            triggerType,
            results: results
        );
    }

    private GDictionary DispatchHandler(
        string methodName,
        BattleUnitState unitState,
        GDictionary context
    )
    {
        return DispatchHandlerResult(methodName, unitState, context).ToDictionary();
    }

    private TraitDispatchResult DispatchHandlerResult(
        string methodName,
        BattleUnitState unitState,
        GDictionary context
    )
    {
        return methodName switch
        {
            "_handle_halfling_luck" => HandleHalflingLuckResult(unitState, context),
            "_handle_savage_attacks" => HandleSavageAttacksResult(unitState, context),
            "_handle_relentless_endurance" => HandleRelentlessEnduranceResult(unitState, context),
            _ => BuildEmptyDispatchResult(new StringName("")),
        };
    }

    public GDictionary _handle_halfling_luck(BattleUnitState unitState, GDictionary context)
    {
        return HandleHalflingLuckResult(unitState, context).ToDictionary();
    }

    private TraitDispatchResult HandleHalflingLuckResult(
        BattleUnitState unitState,
        GDictionary context
    )
    {
        int roll = GetInt(context, "roll");
        if (roll != 1)
        {
            return BuildEmptyDispatchResult(TriggerOnNaturalOne);
        }
        StringName chargeKey = GetTraitChargeKey(TraitHalflingLuck);
        if (!ConsumeCharge(unitState, chargeKey, true, 1))
        {
            return BuildEmptyDispatchResult(TriggerOnNaturalOne);
        }
        return new TraitDispatchResult(
            true,
            true,
            TriggerOnNaturalOne,
            new GDictionary
            {
                ["effect_type"] = TraitHalflingLuck,
                ["original_roll"] = roll,
                ["reroll_die"] = true,
                ["die_size"] = Math.Max(GetInt(context, "die_size", 20), 1),
                ["charge_key"] = chargeKey,
                ["charges_remaining"] = GetCharge(unitState, chargeKey, true),
            }
        );
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
        return HandleSavageAttacksResult(unitState, context).ToDictionary();
    }

    private TraitDispatchResult HandleSavageAttacksResult(
        BattleUnitState unitState,
        GDictionary context
    )
    {
        SavageAttacksContext attackContext = SavageAttacksContext.FromDictionary(context);
        AttackTraitTriggerResult typedResult = HandleSavageAttacksTyped(unitState, attackContext);
        if (!typedResult.Triggered)
        {
            return BuildEmptyDispatchResult(TriggerOnCrit);
        }
        return new TraitDispatchResult(
            true,
            true,
            TriggerOnCrit,
            new GDictionary
            {
                ["effect_type"] = typedResult.EffectType,
                ["extra_weapon_dice_count"] = typedResult.ExtraWeaponDiceCount,
                ["extra_weapon_dice_sides"] = typedResult.ExtraWeaponDiceSides,
            }
        );
    }

    private AttackTraitTriggerResult HandleSavageAttacksTyped(
        BattleUnitState unitState,
        SavageAttacksContext attackContext
    )
    {
        if (!attackContext.CriticalHit || !attackContext.AddWeaponDice)
        {
            return new AttackTraitTriggerResult(@event: TriggerOnCrit);
        }
        if (attackContext.WeaponAttackRange > 1)
        {
            return new AttackTraitTriggerResult(@event: TriggerOnCrit);
        }
        if (attackContext.WeaponDiceSides <= 0)
        {
            return new AttackTraitTriggerResult(@event: TriggerOnCrit);
        }
        return new AttackTraitTriggerResult(
            triggered: true,
            @event: TriggerOnCrit,
            effectType: TraitSavageAttacks,
            extraWeaponDiceCount: 1,
            extraWeaponDiceSides: attackContext.WeaponDiceSides
        );
    }

    public GDictionary _handle_relentless_endurance(BattleUnitState unitState, GDictionary context)
    {
        return HandleRelentlessEnduranceResult(unitState, context).ToDictionary();
    }

    private TraitDispatchResult HandleRelentlessEnduranceResult(
        BattleUnitState unitState,
        GDictionary context
    )
    {
        AttackTraitTriggerResult typedResult = HandleRelentlessEnduranceTyped(
            unitState,
            GetInt(context, "hp_damage"),
            unitState != null ? GetInt(context, "projected_hp", unitState.current_hp) : 0
        );
        if (!typedResult.Triggered)
        {
            return BuildEmptyDispatchResult(TriggerOnFatalDamage);
        }
        return new TraitDispatchResult(
            true,
            true,
            TriggerOnFatalDamage,
            new GDictionary
            {
                ["effect_type"] = typedResult.EffectType,
                ["clamp_to_hp"] = typedResult.ClampToHp,
                ["projected_hp"] = typedResult.ProjectedHp,
                ["hp_damage"] = typedResult.HpDamage,
                ["charge_key"] = typedResult.ChargeKey,
                ["charges_remaining"] = typedResult.ChargesRemaining,
            }
        );
    }

    private AttackTraitTriggerResult HandleRelentlessEnduranceTyped(
        BattleUnitState unitState,
        int hpDamage,
        int projectedHp
    )
    {
        if (unitState == null)
        {
            return new AttackTraitTriggerResult(@event: TriggerOnFatalDamage);
        }
        if (projectedHp > 0)
        {
            return new AttackTraitTriggerResult(@event: TriggerOnFatalDamage);
        }
        StringName chargeKey = GetTraitChargeKey(TraitRelentlessEndurance);
        if (!ConsumeCharge(unitState, chargeKey, false, 1))
        {
            return new AttackTraitTriggerResult(@event: TriggerOnFatalDamage);
        }
        return new AttackTraitTriggerResult(
            triggered: true,
            @event: TriggerOnFatalDamage,
            effectType: TraitRelentlessEndurance,
            chargeKey: chargeKey,
            chargesRemaining: GetCharge(unitState, chargeKey, false),
            clampToHp: 1,
            projectedHp: projectedHp,
            hpDamage: Math.Max(hpDamage, 0)
        );
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
        return ProgressionDataUtils.to_string_name(traitId);
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
        int remaining = Math.Max(GetInt(charges, chargeKey), 0);
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
        return Math.Max(GetInt(charges, chargeKey), 0);
    }

    private static GDictionary BuildEmptyResult(StringName triggerType)
    {
        return BuildEmptyDispatchResult(triggerType).ToDictionary();
    }

    private static TraitDispatchResult BuildEmptyDispatchResult(StringName triggerType)
    {
        return new TraitDispatchResult(false, false, triggerType);
    }

    private static GDictionary DuplicateDictionary(GDictionary value)
    {
        return value?.Duplicate(true) ?? new GDictionary();
    }

    private static GDictionary GetDict(GDictionary source, object key)
    {
        if (!TryResolveKey(source, key, out StringName stringNameKey, out string stringKey, out bool useStringName))
            return new GDictionary();
        return useStringName
            ? source[stringNameKey].AsGodotDictionary()
            : source[stringKey].AsGodotDictionary();
    }

    private static GArray GetArray(GDictionary source, object key)
    {
        if (!TryResolveKey(source, key, out StringName stringNameKey, out string stringKey, out bool useStringName))
            return new GArray();
        return useStringName ? source[stringNameKey].AsGodotArray() : source[stringKey].AsGodotArray();
    }

    private static int GetInt(GDictionary source, object key, int fallback = 0)
    {
        if (!TryResolveKey(source, key, out StringName stringNameKey, out string stringKey, out bool useStringName))
            return fallback;
        return useStringName ? source[stringNameKey].AsInt32() : source[stringKey].AsInt32();
    }

    private static bool ReadBool(GDictionary source, object key, bool fallback = false)
    {
        if (!TryResolveKey(source, key, out StringName stringNameKey, out string stringKey, out bool useStringName))
        {
            return fallback;
        }
        return useStringName ? source[stringNameKey].AsBool() : source[stringKey].AsBool();
    }

    private static bool TryResolveKey(
        GDictionary source,
        object key,
        out StringName stringNameKey,
        out string stringKey,
        out bool useStringName
    )
    {
        stringNameKey = "";
        stringKey = "";
        useStringName = false;
        if (source == null)
        {
            return false;
        }
        if (key is StringName namedKey)
        {
            if (source.ContainsKey(namedKey))
            {
                stringNameKey = namedKey;
                useStringName = true;
                return true;
            }
            string namedKeyText = namedKey.ToString();
            if (source.ContainsKey(namedKeyText))
            {
                stringKey = namedKeyText;
                return true;
            }
            return false;
        }

        string textKey = key?.ToString() ?? "";
        if (string.IsNullOrEmpty(textKey))
        {
            return false;
        }
        if (source.ContainsKey(textKey))
        {
            stringKey = textKey;
            return true;
        }
        StringName normalizedKey = new(textKey);
        if (source.ContainsKey(normalizedKey))
        {
            stringNameKey = normalizedKey;
            useStringName = true;
            return true;
        }
        return false;
    }
}
