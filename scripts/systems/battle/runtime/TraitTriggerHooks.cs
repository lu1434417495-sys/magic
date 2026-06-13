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
    internal readonly AttackTraitTriggerResult Result;
    internal readonly IReadOnlyList<AttackTraitTriggerResult> Results;

    internal TraitDispatchResult(
        bool triggered = false,
        bool changed = false,
        StringName @event = null,
        AttackTraitTriggerResult result = default,
        IReadOnlyList<AttackTraitTriggerResult> results = null
    )
    {
        Triggered = triggered;
        Changed = changed;
        Event = @event ?? new StringName("");
        Result = result;
        Results = results ?? Array.Empty<AttackTraitTriggerResult>();
    }

    internal GDictionary ToDictionary()
    {
        GDictionary result = BuildPayload(Result);
        result["triggered"] = Triggered;
        result["changed"] = Changed;
        result["event"] = Event;
        if (Results.Count > 0)
        {
            GArray entries = new();
            foreach (AttackTraitTriggerResult entry in Results)
            {
                GDictionary payload = BuildPayload(entry);
                payload["triggered"] = entry.Triggered;
                if (!IsEmpty(entry.Event))
                {
                    payload["event"] = entry.Event;
                }
                entries.Add(payload);
            }
            result["results"] = entries;
        }
        return result;
    }

    private static GDictionary BuildPayload(AttackTraitTriggerResult result)
    {
        GDictionary payload = new();
        if (!IsEmpty(result.TraitId))
        {
            payload["trait_id"] = result.TraitId;
        }
        if (!IsEmpty(result.EffectType))
        {
            payload["effect_type"] = result.EffectType;
        }
        if (result.OriginalRoll != 0)
        {
            payload["original_roll"] = result.OriginalRoll;
        }
        if (result.RerollDie)
        {
            payload["reroll_die"] = true;
        }
        if (result.RerolledRoll != 0)
        {
            payload["rerolled_roll"] = result.RerolledRoll;
        }
        if (result.DieSize != 0)
        {
            payload["die_size"] = result.DieSize;
        }
        if (!IsEmpty(result.ChargeKey))
        {
            payload["charge_key"] = result.ChargeKey;
            payload["charges_remaining"] = result.ChargesRemaining;
        }
        if (result.ExtraWeaponDiceCount != 0)
        {
            payload["extra_weapon_dice_count"] = result.ExtraWeaponDiceCount;
        }
        if (result.ExtraWeaponDiceSides != 0)
        {
            payload["extra_weapon_dice_sides"] = result.ExtraWeaponDiceSides;
        }
        if (result.ClampToHp != 0)
        {
            payload["clamp_to_hp"] = result.ClampToHp;
        }
        if (result.ProjectedHp != 0)
        {
            payload["projected_hp"] = result.ProjectedHp;
        }
        if (result.HpDamage != 0)
        {
            payload["hp_damage"] = result.HpDamage;
        }
        return payload;
    }

    private static bool IsEmpty(StringName value) => value == default || value == (StringName)"";
}

internal class TraitTriggerHooks
{
    private static StringName TriggerOnNaturalOne =>
        TraitTriggerContentRules.ToStringName(TraitTriggerKind.OnNaturalOne);
    private static StringName TriggerOnCrit =>
        TraitTriggerContentRules.ToStringName(TraitTriggerKind.OnCrit);
    private static StringName TriggerOnFatalDamage =>
        TraitTriggerContentRules.ToStringName(TraitTriggerKind.OnFatalDamage);
    private static StringName TriggerOnBattleStart =>
        TraitTriggerContentRules.ToStringName(TraitTriggerKind.OnBattleStart);
    private static StringName TriggerOnTurnStart =>
        TraitTriggerContentRules.ToStringName(TraitTriggerKind.OnTurnStart);

    private static StringName TraitHalflingLuck =>
        RaceTraitDef.ToStringName(RaceTraitEffectKind.HalflingLuck);
    private static StringName TraitSavageAttacks =>
        RaceTraitDef.ToStringName(RaceTraitEffectKind.SavageAttacks);
    private static StringName TraitRelentlessEndurance =>
        RaceTraitDef.ToStringName(RaceTraitEffectKind.RelentlessEndurance);

    private readonly record struct SavageAttacksContext(
        bool CriticalHit,
        bool AddWeaponDice,
        int WeaponAttackRange,
        int WeaponDiceSides
    )
    {
        internal static SavageAttacksContext FromDictionary(GDictionary context)
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

    public static bool HasDispatchForTraitTrigger(StringName trait_id, StringName trigger_type)
    {
        return TraitTriggerContentRules.HasDispatchForTraitTrigger(
            ProgressionDataUtils.to_string_name(trait_id),
            ProgressionDataUtils.to_string_name(trigger_type)
        );
    }

    internal static GStringNameArray get_dispatch_trait_ids()
    {
        var result = new GStringNameArray();
        foreach (StringName traitId in TraitTriggerContentRules.GetDispatchTraitIds())
        {
            result.Add(traitId);
        }
        return result;
    }

    public AttackTraitTriggerResult OnNaturalOne(
        BattleUnitState unit_state,
        int roll,
        int die_size
    )
    {
        foreach (StringName traitId in GetUnitTraitIds(unit_state))
        {
            string dispatchKey = TraitTriggerContentRules.GetDispatchKey(
                traitId,
                TriggerOnNaturalOne
            );
            if (dispatchKey != TraitHalflingLuck.ToString())
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

    public AttackTraitTriggerResult OnCrit(
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
            string dispatchKey = TraitTriggerContentRules.GetDispatchKey(
                traitId,
                TriggerOnCrit
            );
            if (dispatchKey != TraitSavageAttacks.ToString())
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

    public AttackTraitTriggerResult OnFatalDamage(
        BattleUnitState targetUnit,
        BattleUnitState sourceUnit,
        int hpDamage,
        int projectedHp
    )
    {
        foreach (StringName traitId in GetUnitTraitIds(targetUnit))
        {
            string dispatchKey = TraitTriggerContentRules.GetDispatchKey(
                traitId,
                TriggerOnFatalDamage
            );
            if (dispatchKey != TraitRelentlessEndurance.ToString())
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

    internal TraitDispatchResult OnBattleStartResult(BattleUnitState unit_state)
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
        return new TraitDispatchResult(changed, changed, TriggerOnBattleStart);
    }

    internal TraitDispatchResult OnTurnStartResult(BattleUnitState unit_state)
    {
        bool changed = false;
        if (UnitHasTrait(unit_state, TraitHalflingLuck))
        {
            SetCharge(unit_state, GetTraitChargeKey(TraitHalflingLuck), 1, true, true);
            changed = true;
        }
        return new TraitDispatchResult(changed, changed, TriggerOnTurnStart);
    }

    internal AttackTraitTriggerResult _handle_halfling_luck_typed(
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

    private static List<StringName> GetUnitTraitIds(BattleUnitState unitState)
    {
        var traitIds = new List<StringName>();
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

    private static void AppendUniqueTraits(List<StringName> target, GStringNameArray values)
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

    private static GDictionary GetDict(GDictionary source, string key)
    {
        if (!TryResolveStringKey(source, key, out Variant value))
            return new GDictionary();
        return value.AsGodotDictionary();
    }

    private static int GetInt(GDictionary source, string key, int fallback = 0)
    {
        if (!TryResolveStringKey(source, key, out Variant value))
            return fallback;
        return value.AsInt32();
    }

    private static int GetInt(GDictionary source, StringName key, int fallback = 0)
    {
        if (!TryResolveStringNameKey(source, key, out Variant value))
            return fallback;
        return value.AsInt32();
    }

    private static bool ReadBool(GDictionary source, string key, bool fallback = false)
    {
        if (!TryResolveStringKey(source, key, out Variant value))
            return fallback;
        return value.AsBool();
    }

    private static bool TryResolveStringKey(GDictionary source, string key, out Variant value)
    {
        value = default;
        if (source == null || string.IsNullOrEmpty(key))
        {
            return false;
        }
        if (source.ContainsKey(key))
        {
            value = source[key];
            return true;
        }
        return false;
    }

    private static bool TryResolveStringNameKey(
        GDictionary source,
        StringName key,
        out Variant value
    )
    {
        value = default;
        if (source == null || key == "")
        {
            return false;
        }
        if (source.ContainsKey(key))
        {
            value = source[key];
            return true;
        }
        return false;
    }
}
