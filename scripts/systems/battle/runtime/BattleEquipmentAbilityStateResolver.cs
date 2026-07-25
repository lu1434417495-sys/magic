using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleEquipmentAbilityStateResolver
{
    private BattleRuntimeModule _runtime;

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    internal void DisposeRuntime()
    {
        _runtime = null;
    }

    private static readonly StringName OnceScopeTurn = "turn";
    private static readonly StringName ResetTimingPerBattle = "per_battle";
    private static readonly StringName ResetTimingBattle = "battle";
    private static readonly StringName ResetTimingPersistentCounter = "persistent_counter";

    internal void ResolveModifyAbilityStateAction(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition binding,
        ModifyAbilityStateActionPayloadDefinition payload,
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit
    )
    {
        BattleUnitState owner = BattleEquipmentAbilityRuntimeService.ResolveSubject(
            payload?.TargetSelector ?? "",
            sourceUnit,
            targetUnit
        );
        EquipmentAbilityBindingDefinition stateBinding = ResolveStateBinding(
            activeBinding,
            binding,
            payload?.BindingId ?? new StringName("")
        );
        StringName stateKey = ProgressionDataUtils.to_string_name(
            payload?.StateKey ?? new StringName("")
        );
        if (owner == null || stateKey == "")
            return;

        StringName operation = ProgressionDataUtils.to_string_name(payload.Operation);
        if (IsPersistentCounterState(stateBinding, stateKey))
        {
            EquipmentInstanceState instance = EquipmentAbilityUsageRuntime.FindEquipmentInstance(
                owner,
                activeBinding.Source?.SourceEquipmentInstanceId ?? new StringName("")
            );
            if (instance == null)
                return;
            long currentCounter = GetPersistentCounterValue(instance, stateBinding, stateKey, 0);
            long nextCounter = ResolveNextAbilityStateValue(
                currentCounter,
                operation,
                payload.IntDelta
            );
            SetPersistentCounterValue(instance, stateBinding, stateKey, nextCounter);
            SyncDerivedAbilityStates(
                activeBinding,
                stateBinding,
                owner,
                stateKey,
                GetPersistentCounterValue(instance, stateBinding, stateKey, 0)
            );
            return;
        }

        StringName chargeKey = BuildBindingStateChargeKey(
            activeBinding.Source,
            stateBinding,
            stateKey
        );
        if (chargeKey == "")
            return;

        int current = GetAbilityStateValue(owner, stateBinding, chargeKey, stateKey, 0);
        SetAbilityStateValue(
            owner,
            stateBinding,
            chargeKey,
            stateKey,
            ClampFactInt(ResolveNextAbilityStateValue(current, operation, payload.IntDelta))
        );
        SyncDerivedAbilityStates(
            activeBinding,
            stateBinding,
            owner,
            stateKey,
            GetAbilityStateValue(owner, stateBinding, chargeKey, stateKey, 0)
        );
    }

    private void SyncDerivedAbilityStates(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition stateBinding,
        BattleUnitState owner,
        StringName sourceStateKey,
        long sourceValue
    )
    {
        sourceStateKey = ProgressionDataUtils.to_string_name(sourceStateKey);
        if (owner == null || stateBinding == null || sourceStateKey == "")
            return;
        foreach (
            EquipmentAbilityStateSchemaDefinition schema in stateBinding.StateSchemas
                ?? Array.Empty<EquipmentAbilityStateSchemaDefinition>()
        )
        {
            SyncDerivedAbilityState(
                activeBinding,
                stateBinding,
                owner,
                sourceStateKey,
                sourceValue,
                schema
            );
        }
    }

    private void SyncDerivedAbilityState(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition stateBinding,
        BattleUnitState owner,
        StringName sourceStateKey,
        long sourceValue,
        EquipmentAbilityStateSchemaDefinition schema
    )
    {
        if (
            schema == null
            || ProgressionDataUtils.to_string_name(schema.SyncSourceStateKey) != sourceStateKey
        )
            return;

        StringName syncStateKey = ProgressionDataUtils.to_string_name(schema.StateKey);
        if (syncStateKey == "" || syncStateKey == sourceStateKey)
            return;

        long syncValue = ApplyStateSyncAggregation(
            sourceValue,
            schema.SyncAggregation,
            schema.SyncIntLiteral
        );
        if (IsPersistentCounterState(stateBinding, syncStateKey))
        {
            EquipmentInstanceState instance = EquipmentAbilityUsageRuntime.FindEquipmentInstance(
                owner,
                activeBinding.Source?.SourceEquipmentInstanceId ?? new StringName("")
            );
            if (instance == null)
                return;
            SetPersistentCounterValue(instance, stateBinding, syncStateKey, syncValue);
            return;
        }

        StringName syncChargeKey = BuildBindingStateChargeKey(
            activeBinding.Source,
            stateBinding,
            syncStateKey
        );
        if (syncChargeKey == "")
            return;
        SetAbilityStateValue(
            owner,
            stateBinding,
            syncChargeKey,
            syncStateKey,
            ClampFactInt(syncValue)
        );
    }

    private static long ResolveNextAbilityStateValue(
        long current,
        StringName operation,
        int intDelta
    )
    {
        return operation == "clear"
            ? 0
            : operation == "add"
                ? current + intDelta
                : intDelta;
    }

    private static long ApplyStateSyncAggregation(
        long rawValue,
        StringName aggregation,
        int intLiteral
    )
    {
        long normalizedValue = Math.Max(rawValue, 0L);
        StringName normalizedAggregation = ProgressionDataUtils.to_string_name(aggregation);
        if (normalizedAggregation == "" || normalizedAggregation == "value")
            return normalizedValue;
        if (normalizedAggregation == "floor_div")
            return normalizedValue / Math.Max(intLiteral, 1);
        return normalizedValue;
    }

    internal static bool TryConsumeOnceScope(
        BattleEquipmentAbilitySourceReadView source,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityReactionDefinition reaction,
        EquipmentAbilityActionDefinition action,
        BattleUnitState owner
    )
    {
        if (reaction?.OnceScope != OnceScopeTurn)
            return true;
        if (owner == null || binding == null || action == null)
            return false;

        StringName chargeKey = BuildOnceScopeTurnChargeKey(source, binding, reaction, action);
        if (chargeKey == "")
            return false;
        if (!owner.HasPerTurnChargeLimitTyped(chargeKey))
        {
            owner.SetPerTurnChargeLimitTyped(chargeKey, 1);
        }
        if (!owner.HasPerTurnChargeTyped(chargeKey))
        {
            owner.SetPerTurnChargeTyped(chargeKey, owner.GetPerTurnChargeLimitTyped(chargeKey, 1));
        }

        int charge = owner.GetPerTurnChargeTyped(chargeKey, 0);
        if (charge <= 0)
            return false;
        owner.SetPerTurnChargeTyped(chargeKey, charge - 1);
        return true;
    }

    private static StringName BuildOnceScopeTurnChargeKey(
        BattleEquipmentAbilitySourceReadView source,
        EquipmentAbilityBindingDefinition binding,
        EquipmentAbilityReactionDefinition reaction,
        EquipmentAbilityActionDefinition action
    )
    {
        StringName ownerSourceKey = source?.EffectiveInstanceKey ?? new StringName("");
        StringName sourceInstanceId = source?.SourceEquipmentInstanceId ?? new StringName("");
        if (ownerSourceKey == "")
        {
            ownerSourceKey = source?.EquipmentDefId ?? new StringName("");
        }
        if (ownerSourceKey == "" && sourceInstanceId == "")
        {
            return "";
        }
        StringName bindingId = ProgressionDataUtils.to_string_name(binding?.BindingId ?? new StringName(""));
        StringName reactionId = ProgressionDataUtils.to_string_name(reaction?.ReactionId ?? new StringName(""));
        StringName actionId = ProgressionDataUtils.to_string_name(action?.ActionId ?? new StringName(""));
        if (bindingId == "" || actionId == "")
        {
            return "";
        }
        return new StringName(
            string.Join(
                "|",
                "equipment_ability",
                "turn",
                ownerSourceKey.ToString(),
                sourceInstanceId.ToString(),
                bindingId.ToString(),
                reactionId.ToString(),
                actionId.ToString()
            )
        );
    }

    internal static StringName BuildBindingStateChargeKey(
        BattleEquipmentAbilitySourceReadView source,
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        StringName normalizedStateKey = ProgressionDataUtils.to_string_name(stateKey);
        StringName ownerSourceKey = source?.SourceEquipmentInstanceId ?? new StringName("");
        if (ownerSourceKey == "")
            ownerSourceKey = source?.EquipmentDefId ?? new StringName("");
        if (ownerSourceKey == "")
            ownerSourceKey = source?.EffectiveInstanceKey ?? new StringName("");
        if (ownerSourceKey == "" || normalizedStateKey == "")
            return "";
        return new StringName(
            string.Join(
                "|",
                "equipment_ability",
                "state",
                ownerSourceKey.ToString(),
                normalizedStateKey.ToString()
            )
        );
    }

    internal EquipmentAbilityBindingDefinition ResolveStateBinding(
        BattleEquipmentAbilityRuntimeService.ActiveEquipmentAbilityBinding activeBinding,
        EquipmentAbilityBindingDefinition fallbackBinding,
        StringName bindingId
    )
    {
        StringName normalizedBindingId = ProgressionDataUtils.to_string_name(bindingId);
        if (normalizedBindingId == "")
            return fallbackBinding;
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> bindingIndex =
            _runtime?.GetEquipmentAbilityBindingIndexTyped();
        if (
            bindingIndex != null
            && bindingIndex.TryGetValue(normalizedBindingId, out EquipmentAbilityBindingDefinition binding)
            && binding != null
        )
        {
            return binding;
        }
        return fallbackBinding;
    }

    private static EquipmentAbilityStateSchemaDefinition FindStateSchema(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        StringName normalizedStateKey = ProgressionDataUtils.to_string_name(stateKey);
        if (binding == null || normalizedStateKey == "")
            return null;
        foreach (EquipmentAbilityStateSchemaDefinition schema in binding.StateSchemas ?? Array.Empty<EquipmentAbilityStateSchemaDefinition>())
        {
            if (schema?.StateKey == normalizedStateKey)
                return schema;
        }
        return null;
    }

    private static bool IsPerBattleState(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        EquipmentAbilityStateSchemaDefinition schema = FindStateSchema(binding, stateKey);
        StringName resetTiming = ProgressionDataUtils.to_string_name(schema?.ResetTiming ?? new StringName(""));
        return resetTiming == ResetTimingPerBattle || resetTiming == ResetTimingBattle;
    }

    internal static bool IsPersistentCounterState(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        EquipmentAbilityStateSchemaDefinition schema = FindStateSchema(binding, stateKey);
        StringName resetTiming = ProgressionDataUtils.to_string_name(
            schema?.ResetTiming ?? new StringName("")
        );
        return resetTiming == ResetTimingPersistentCounter;
    }

    private static string BuildPersistentCounterId(
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey
    )
    {
        StringName bindingId = ProgressionDataUtils.to_string_name(
            binding?.BindingId ?? new StringName("")
        );
        StringName normalizedStateKey = ProgressionDataUtils.to_string_name(stateKey);
        if (bindingId == "" || normalizedStateKey == "")
            return "";
        return $"{bindingId}:{normalizedStateKey}";
    }

    internal static long GetPersistentCounterValue(
        EquipmentInstanceState instance,
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey,
        long fallback
    )
    {
        string counterId = BuildPersistentCounterId(binding, stateKey);
        if (instance == null || string.IsNullOrEmpty(counterId))
            return Math.Max(fallback, 0L);
        foreach (
            EquipmentAbilityPersistentCounterState counter in instance.ability_persistent_counters
                ?? new List<EquipmentAbilityPersistentCounterState>()
        )
        {
            if (counter != null && counter.CounterId == counterId)
                return Math.Max(counter.Value, 0L);
        }
        EquipmentAbilityStateSchemaDefinition schema = FindStateSchema(binding, stateKey);
        return Math.Max(schema?.InitialIntValue ?? fallback, 0L);
    }

    private static void SetPersistentCounterValue(
        EquipmentInstanceState instance,
        EquipmentAbilityBindingDefinition binding,
        StringName stateKey,
        long value
    )
    {
        string counterId = BuildPersistentCounterId(binding, stateKey);
        if (instance == null || string.IsNullOrEmpty(counterId))
            return;
        EquipmentAbilityStateSchemaDefinition schema = FindStateSchema(binding, stateKey);
        long normalizedValue = Math.Max(value, 0L);
        if (schema != null && schema.MaxIntValue > 0)
            normalizedValue = Math.Min(normalizedValue, schema.MaxIntValue);
        instance.ability_persistent_counters ??= new List<EquipmentAbilityPersistentCounterState>();
        foreach (EquipmentAbilityPersistentCounterState counter in instance.ability_persistent_counters)
        {
            if (counter != null && counter.CounterId == counterId)
            {
                counter.Value = normalizedValue;
                return;
            }
        }
        instance.ability_persistent_counters.Add(
            new EquipmentAbilityPersistentCounterState
            {
                CounterId = counterId,
                Value = normalizedValue,
            }
        );
    }

    internal static int ClampFactInt(long value) =>
        value > int.MaxValue ? int.MaxValue : (int)Math.Max(value, 0L);

    internal static int GetAbilityStateValue(
        BattleUnitState owner,
        EquipmentAbilityBindingDefinition binding,
        StringName chargeKey,
        StringName stateKey,
        int fallback
    )
    {
        if (owner == null || chargeKey == "")
            return fallback;
        EquipmentAbilityStateSchemaDefinition schema = FindStateSchema(binding, stateKey);
        int initial = schema != null ? Math.Max(schema.InitialIntValue, 0) : Math.Max(fallback, 0);
        return IsPerBattleState(binding, stateKey)
            ? owner.GetPerBattleChargeTyped(chargeKey, initial)
            : owner.GetPerTurnChargeTyped(chargeKey, initial);
    }

    private static void SetAbilityStateValue(
        BattleUnitState owner,
        EquipmentAbilityBindingDefinition binding,
        StringName chargeKey,
        StringName stateKey,
        int value
    )
    {
        if (owner == null || chargeKey == "")
            return;
        EquipmentAbilityStateSchemaDefinition schema = FindStateSchema(binding, stateKey);
        int normalizedValue = Math.Max(value, 0);
        if (schema != null && schema.MaxIntValue > 0)
            normalizedValue = Math.Min(normalizedValue, schema.MaxIntValue);

        if (IsPerBattleState(binding, stateKey))
            owner.SetPerBattleChargeTyped(chargeKey, normalizedValue);
        else
            owner.SetPerTurnChargeTyped(chargeKey, normalizedValue);
    }
}
