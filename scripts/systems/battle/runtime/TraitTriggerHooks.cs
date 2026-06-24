using System;
using System.Collections.Generic;
using Godot;
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
        TraitContentRules.ToStringName(TraitEffectKind.HalflingLuck);
    private static StringName TraitSavageAttacks =>
        TraitContentRules.ToStringName(TraitEffectKind.SavageAttacks);
    private static StringName TraitRelentlessEndurance =>
        TraitContentRules.ToStringName(TraitEffectKind.RelentlessEndurance);

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

    private readonly record struct UnitEffectiveTrait(
        StringName TraitId,
        StringName EffectiveInstanceKey,
        TraitEffectKind EffectKind,
        TraitTriggerKind TriggerKind,
        TraitChargeScopeKind ChargeScope,
        TraitChargeResetTimingKind ChargeResetTiming,
        List<TraitRollValueState> RollValues
    );

    private readonly record struct TraitChargeScopeEntry(
        StringName EffectiveInstanceKey,
        TraitChargeScopeKind ChargeScope
    );

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
        foreach (
            UnitEffectiveTrait trait in GetEffectiveInstances(
                unit_state,
                TraitTriggerKind.OnNaturalOne
            )
        )
        {
            if (trait.EffectKind != TraitEffectKind.HalflingLuck)
                continue;
            AttackTraitTriggerResult result = _handle_halfling_luck_typed(
                unit_state,
                roll,
                die_size,
                trait.EffectiveInstanceKey
            );
            if (!result.Triggered)
                continue;
            return new AttackTraitTriggerResult(
                triggered: result.Triggered,
                @event: TriggerOnNaturalOne,
                traitId: trait.TraitId,
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
        foreach (
            UnitEffectiveTrait trait in GetEffectiveInstances(
                sourceUnit,
                TraitTriggerKind.OnCrit
            )
        )
        {
            if (trait.EffectKind != TraitEffectKind.SavageAttacks)
                continue;
            AttackTraitTriggerResult result = HandleSavageAttacksTyped(
                sourceUnit,
                attackContext
            );
            if (!result.Triggered)
                continue;
            return new AttackTraitTriggerResult(
                triggered: true,
                @event: TriggerOnCrit,
                traitId: trait.TraitId,
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
        foreach (
            UnitEffectiveTrait trait in GetEffectiveInstances(
                targetUnit,
                TraitTriggerKind.OnFatalDamage
            )
        )
        {
            if (trait.EffectKind != TraitEffectKind.RelentlessEndurance)
                continue;
            AttackTraitTriggerResult result = HandleRelentlessEnduranceTyped(
                targetUnit,
                hpDamage,
                projectedHp,
                trait.EffectiveInstanceKey
            );
            if (!result.Triggered)
                continue;
            return new AttackTraitTriggerResult(
                triggered: true,
                @event: TriggerOnFatalDamage,
                traitId: trait.TraitId,
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
        bool changed = SeedChargesForTiming(
            unit_state,
            TraitChargeResetTimingKind.BattleStart
        );
        return new TraitDispatchResult(changed, changed, TriggerOnBattleStart);
    }

    internal TraitDispatchResult OnTurnStartResult(BattleUnitState unit_state)
    {
        bool changed = SeedChargesForTiming(
            unit_state,
            TraitChargeResetTimingKind.TurnStart
        );
        return new TraitDispatchResult(changed, changed, TriggerOnTurnStart);
    }

    internal static bool ReconcileChargesAfterEffectiveTraitProjection(
        BattleUnitState unitState,
        IEnumerable<BattleEffectiveTraitInstanceState> previousEffectiveTraitInstances
    )
    {
        if (unitState == null)
            return false;

        List<TraitChargeScopeEntry> previousScopes =
            GetChargeScopesByEffectiveKey(previousEffectiveTraitInstances);
        List<TraitChargeScopeEntry> currentScopes =
            GetChargeScopesByEffectiveKey(unitState.effective_trait_instances);
        bool changed = false;

        foreach (TraitChargeScopeEntry previous in previousScopes)
        {
            if (
                !TryFindScope(currentScopes, previous.EffectiveInstanceKey, out TraitChargeScopeKind currentScope)
                || currentScope != previous.ChargeScope
            )
            {
                changed |= RemoveCharge(
                    unitState,
                    previous.EffectiveInstanceKey,
                    previous.ChargeScope
                );
            }
        }

        foreach (TraitChargeScopeEntry current in currentScopes)
        {
            if (current.ChargeScope == TraitChargeScopeKind.None)
                continue;
            if (
                TryFindScope(previousScopes, current.EffectiveInstanceKey, out TraitChargeScopeKind previousScope)
                && previousScope == current.ChargeScope
            )
            {
                continue;
            }
            changed |= SeedChargeIfMissing(
                unitState,
                current.EffectiveInstanceKey,
                current.ChargeScope
            );
        }

        return changed;
    }

    internal AttackTraitTriggerResult _handle_halfling_luck_typed(
        BattleUnitState unitState,
        int roll,
        int dieSize,
        StringName effectiveInstanceKey = default
    )
    {
        if (roll != 1)
        {
            return new AttackTraitTriggerResult(@event: TriggerOnNaturalOne);
        }
        StringName chargeKey =
            !IsEmpty(effectiveInstanceKey)
                ? effectiveInstanceKey
                : GetTraitChargeKey(TraitHalflingLuck);
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
        int projectedHp,
        StringName effectiveInstanceKey = default
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
        StringName chargeKey =
            !IsEmpty(effectiveInstanceKey)
                ? effectiveInstanceKey
                : GetTraitChargeKey(TraitRelentlessEndurance);
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

    private static List<UnitEffectiveTrait> GetEffectiveInstances(
        BattleUnitState unitState,
        TraitTriggerKind triggerKind
    )
    {
        List<UnitEffectiveTrait> result = new();
        foreach (UnitEffectiveTrait trait in GetAllEffectiveInstances(unitState))
        {
            if (trait.TriggerKind == triggerKind)
                result.Add(trait);
        }
        return result;
    }

    private static List<UnitEffectiveTrait> GetChargeBearingInstances(
        BattleUnitState unitState,
        TraitChargeResetTimingKind timing
    )
    {
        List<UnitEffectiveTrait> result = new();
        foreach (UnitEffectiveTrait trait in GetAllEffectiveInstances(unitState))
        {
            if (trait.ChargeScope == TraitChargeScopeKind.None)
                continue;
            if (trait.ChargeResetTiming != timing)
                continue;
            result.Add(trait);
        }
        return result;
    }

    private static List<UnitEffectiveTrait> GetAllEffectiveInstances(BattleUnitState unitState)
    {
        return GetAllEffectiveInstances(unitState?.effective_trait_instances);
    }

    private static List<UnitEffectiveTrait> GetAllEffectiveInstances(
        IEnumerable<BattleEffectiveTraitInstanceState> effectiveTraitInstances)
    {
        List<UnitEffectiveTrait> result = new();
        if (effectiveTraitInstances == null)
            return result;

        foreach (BattleEffectiveTraitInstanceState entry in effectiveTraitInstances)
        {
            if (entry == null)
                continue;
            result.Add(
                new UnitEffectiveTrait(
                    entry.trait_id,
                    entry.effective_instance_key,
                    entry.EffectKind,
                    entry.TriggerKind,
                    entry.ChargeScopeKind,
                    entry.ChargeResetTimingKind,
                    TraitInstanceState.NormalizeRollValues(entry.roll_values)
                )
            );
        }
        return result;
    }

    private static List<TraitChargeScopeEntry> GetChargeScopesByEffectiveKey(
        IEnumerable<BattleEffectiveTraitInstanceState> effectiveTraitInstances
    )
    {
        List<TraitChargeScopeEntry> result = new();
        foreach (UnitEffectiveTrait trait in GetAllEffectiveInstances(effectiveTraitInstances))
        {
            if (IsEmpty(trait.EffectiveInstanceKey))
                continue;
            if (trait.ChargeScope == TraitChargeScopeKind.None)
                continue;
            UpsertScope(result, trait.EffectiveInstanceKey, trait.ChargeScope);
        }
        return result;
    }

    private static void UpsertScope(
        List<TraitChargeScopeEntry> values,
        StringName effectiveInstanceKey,
        TraitChargeScopeKind scope
    )
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (values[index].EffectiveInstanceKey != effectiveInstanceKey)
                continue;
            values[index] = new TraitChargeScopeEntry(effectiveInstanceKey, scope);
            return;
        }
        values.Add(new TraitChargeScopeEntry(effectiveInstanceKey, scope));
    }

    private static bool TryFindScope(
        List<TraitChargeScopeEntry> values,
        StringName effectiveInstanceKey,
        out TraitChargeScopeKind scope
    )
    {
        foreach (TraitChargeScopeEntry entry in values)
        {
            if (entry.EffectiveInstanceKey != effectiveInstanceKey)
                continue;
            scope = entry.ChargeScope;
            return true;
        }
        scope = TraitChargeScopeKind.None;
        return false;
    }

    private static bool SeedChargesForTiming(
        BattleUnitState unitState,
        TraitChargeResetTimingKind timing
    )
    {
        bool changed = false;
        foreach (UnitEffectiveTrait trait in GetChargeBearingInstances(unitState, timing))
        {
            if (trait.EffectiveInstanceKey == "")
                continue;
            if (trait.ChargeScope == TraitChargeScopeKind.PerTurn)
            {
                SetCharge(unitState, trait.EffectiveInstanceKey, 1, true, true);
                changed = true;
            }
            else if (trait.ChargeScope == TraitChargeScopeKind.PerBattle)
            {
                SetCharge(unitState, trait.EffectiveInstanceKey, 1, false, true);
                changed = true;
            }
        }
        return changed;
    }

    private static bool SeedChargeIfMissing(
        BattleUnitState unitState,
        StringName chargeKey,
        TraitChargeScopeKind scope
    )
    {
        if (unitState == null || IsEmpty(chargeKey))
            return false;
        if (scope == TraitChargeScopeKind.PerTurn)
        {
            if (unitState.per_turn_charges.ContainsKey(chargeKey))
                return false;
            SetCharge(unitState, chargeKey, 1, true);
            return true;
        }
        if (scope == TraitChargeScopeKind.PerBattle)
        {
            if (unitState.per_battle_charges.ContainsKey(chargeKey))
                return false;
            SetCharge(unitState, chargeKey, 1, false);
            return true;
        }
        return false;
    }

    private static bool RemoveCharge(
        BattleUnitState unitState,
        StringName chargeKey,
        TraitChargeScopeKind scope
    )
    {
        if (unitState == null || IsEmpty(chargeKey))
            return false;

        bool changed = false;
        if (scope == TraitChargeScopeKind.PerTurn)
        {
            changed |= RemoveKey(unitState.per_turn_charges, chargeKey);
            changed |= RemoveKey(unitState.per_turn_charge_limits, chargeKey);
        }
        else if (scope == TraitChargeScopeKind.PerBattle)
        {
            changed |= RemoveKey(unitState.per_battle_charges, chargeKey);
        }
        return changed;
    }

    private static bool RemoveKey(BattleStringNameIntMap source, StringName key)
    {
        if (source == null || IsEmpty(key) || !source.ContainsKey(key))
            return false;
        source.Remove(key);
        return true;
    }

    private static StringName GetTraitChargeKey(StringName traitId)
    {
        return ProgressionDataUtils.to_string_name(traitId);
    }

    private static bool IsEmpty(StringName value) =>
        value == default || value == (StringName)"";

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
        BattleStringNameIntMap charges =
            perTurn ? unitState.per_turn_charges : unitState.per_battle_charges;
        if (force || !charges.ContainsKey(chargeKey))
        {
            charges.Put(chargeKey, Math.Max(value, 0));
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
        BattleStringNameIntMap charges =
            perTurn ? unitState.per_turn_charges : unitState.per_battle_charges;
        if (!charges.ContainsKey(chargeKey))
        {
            charges.Put(chargeKey, Math.Max(defaultValue, 0));
        }
        int remaining = Math.Max(charges.Get(chargeKey), 0);
        if (remaining <= 0)
        {
            return false;
        }
        charges.Put(chargeKey, remaining - 1);
        return true;
    }

    private static int GetCharge(BattleUnitState unitState, StringName chargeKey, bool perTurn)
    {
        if (unitState == null || chargeKey == "")
        {
            return 0;
        }
        BattleStringNameIntMap charges =
            perTurn ? unitState.per_turn_charges : unitState.per_battle_charges;
        return Math.Max(charges.Get(chargeKey), 0);
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

    private static StringName GetStringName(GDictionary source, string key)
    {
        if (!TryResolveStringKey(source, key, out Variant value))
            return "";
        return ProgressionDataUtils.to_string_name(value);
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
