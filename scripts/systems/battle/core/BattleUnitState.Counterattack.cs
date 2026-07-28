using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class BattleUnitState
{
    private static readonly string[] ReactionStateFields =
    [
        "owner_present",
        "charges_remaining",
        "charge_capacity",
        "recharge_interval_tu",
        "next_recharge_at_tu",
    ];

    private static readonly string[]
        CounterattackCapabilityStateFields =
        [
            "owner_present",
            "values",
        ];

    private static readonly string[] CounterattackCapabilityFields =
    [
        "instance_id",
        "trigger_kind",
        "selection_priority",
        "chance_percent",
        "attack_roll_bonus",
        "weapon_action_definition_id",
    ];

    private BattleUnitCounterattackCapabilityState
        _counterattackCapabilityState;
    private BattleUnitReactionState _reactionState;

    internal IReadOnlyList<BattleCounterattackCapability>
        GetCounterattackCandidatesTyped(
            BattleCounterattackTriggerKind triggerKind
        ) =>
            _counterattackCapabilityState?.GetCandidates(triggerKind)
            ?? Array.Empty<BattleCounterattackCapability>();

    internal bool TryGetCounterattackCapabilityTyped(
        StringName instanceId,
        out BattleCounterattackCapability capability
    )
    {
        if (_counterattackCapabilityState == null)
        {
            capability = default;
            return false;
        }
        return _counterattackCapabilityState.TryGetCapability(
            instanceId,
            out capability
        );
    }

    internal void ReplaceCounterattackCapabilitiesTyped(
        IReadOnlyList<BattleCounterattackCapability> values
    )
    {
        _counterattackCapabilityState ??=
            new BattleUnitCounterattackCapabilityState();
        _counterattackCapabilityState.ReplaceAll(values);
    }

    internal BattleUnitCounterattackCapabilitySnapshot
        CaptureCounterattackCapabilitiesRawTyped() =>
            _counterattackCapabilityState?.CaptureRaw()
            ?? BattleUnitCounterattackCapabilitySnapshot.MissingOwner;

    internal void RestoreCounterattackCapabilitiesRawTyped(
        BattleUnitCounterattackCapabilitySnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _counterattackCapabilityState = null;
            return;
        }
        _counterattackCapabilityState ??=
            new BattleUnitCounterattackCapabilityState();
        _counterattackCapabilityState.RestoreRaw(snapshot);
    }

    internal bool HasReactionChargeTyped() =>
        _reactionState?.HasCharge() == true;

    internal void InitializeReactionBudgetTyped(
        int currentTu,
        BattleReactionBudgetConfig config,
        bool startFull
    )
    {
        _reactionState ??= new BattleUnitReactionState();
        _reactionState.Initialize(currentTu, config, startFull);
    }

    internal BattleUnitReactionSnapshot CaptureReactionRawTyped() =>
        _reactionState?.CaptureRaw()
        ?? BattleUnitReactionSnapshot.MissingOwner;

    internal void RestoreReactionRawTyped(
        BattleUnitReactionSnapshot snapshot
    )
    {
        if (!snapshot.OwnerPresent)
        {
            _reactionState = null;
            return;
        }
        _reactionState ??= new BattleUnitReactionState();
        _reactionState.RestoreRaw(snapshot);
    }

    internal bool AdvanceReactionBudgetTyped(int currentTu) =>
        _reactionState?.AdvanceAndRefill(currentTu) == true;

    internal bool AdvanceFrozenReactionAnchorTyped(int elapsedTu) =>
        _reactionState?.AdvanceFrozenAnchor(elapsedTu) == true;

    internal void ValidateConsumeReactionChargeKnownAvailableTyped()
    {
        BattleUnitReactionState state = _reactionState
            ?? throw new InvalidOperationException(
                "reaction state is not initialized"
            );
        state.ValidateConsumeChargeKnownAvailable();
    }

    internal void CommitConsumeReactionChargeKnownAvailableTyped()
    {
        BattleUnitReactionState state = _reactionState
            ?? throw new InvalidOperationException(
                "reaction state is not initialized"
            );
        state.CommitConsumeChargeKnownAvailable();
    }

    internal void ValidateSpendStaminaKnownAvailableTyped(
        int staminaCost
    ) =>
        CombatResourceState.ValidateSpendStaminaKnownAvailable(
            staminaCost
        );

    internal void CommitSpendStaminaKnownAvailableTyped(
        int staminaCost
    ) =>
        CombatResourceState.CommitSpendStaminaKnownAvailable(
            staminaCost
        );

    internal bool TryCommitCounterattackAttemptCostTyped(
        int staminaCost,
        BattleEventBatch batch
    )
    {
        if (staminaCost < 0)
            throw new ArgumentOutOfRangeException(nameof(staminaCost));
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));
        BattleUnitReactionState reactionState = _reactionState;
        BattleUnitCombatResourceState combatState =
            _combatResourceState;
        if (
            reactionState == null
            || combatState == null
            || !reactionState.HasCharge()
            || combatState.GetCurrentStamina() < staminaCost
        )
        {
            return false;
        }

        reactionState.ValidateConsumeChargeKnownAvailable();
        combatState.ValidateSpendStaminaKnownAvailable(
            staminaCost
        );
        reactionState.CommitConsumeChargeKnownAvailable();
        combatState.CommitSpendStaminaKnownAvailable(staminaCost);
        batch.AddChangedUnitId(unit_id);
        return true;
    }

    private Dictionary<string, object> BuildReactionStatePlain()
    {
        BattleUnitReactionSnapshot snapshot =
            CaptureReactionRawTyped();
        return new Dictionary<string, object>(
            StringComparer.Ordinal
        )
        {
            ["owner_present"] = snapshot.OwnerPresent,
            ["charges_remaining"] = snapshot.ChargesRemaining,
            ["charge_capacity"] = snapshot.ChargeCapacity,
            ["recharge_interval_tu"] =
                snapshot.RechargeIntervalTu,
            ["next_recharge_at_tu"] =
                snapshot.NextRechargeAtTu,
        };
    }

    private Dictionary<string, object>
        BuildCounterattackCapabilityStatePlain()
    {
        BattleUnitCounterattackCapabilitySnapshot snapshot =
            CaptureCounterattackCapabilitiesRawTyped();
        var values = new List<object>(snapshot.Values.Count);
        foreach (
            BattleCounterattackCapability capability
                in snapshot.Values
        )
        {
            values.Add(
                new Dictionary<string, object>(
                    StringComparer.Ordinal
                )
                {
                    ["instance_id"] =
                        capability.InstanceId.ToString(),
                    ["trigger_kind"] =
                        BattleCounterattackTriggerNames
                            .ToStringName(
                                capability.TriggerKind
                            )
                            .ToString(),
                    ["selection_priority"] =
                        capability.SelectionPriority,
                    ["chance_percent"] =
                        capability.ChancePercent,
                    ["attack_roll_bonus"] =
                        capability.AttackRollBonus,
                    ["weapon_action_definition_id"] =
                        capability
                            .WeaponActionDefinitionId
                            .ToString(),
                }
            );
        }
        return new Dictionary<string, object>(
            StringComparer.Ordinal
        )
        {
            ["owner_present"] = snapshot.OwnerPresent,
            ["values"] = values,
        };
    }

    private static bool TryReadCounterattackComponentSnapshots(
        GDictionary payload,
        out BattleUnitReactionSnapshot reactionSnapshot,
        out BattleUnitCounterattackCapabilitySnapshot
            capabilitySnapshot
    )
    {
        reactionSnapshot =
            BattleUnitReactionSnapshot.MissingOwner;
        capabilitySnapshot =
            BattleUnitCounterattackCapabilitySnapshot
                .MissingOwner;
        if (
            payload == null
            || payload["reaction_state"].VariantType
                != Variant.Type.Dictionary
            || payload["counterattack_capability_state"]
                    .VariantType
                != Variant.Type.Dictionary
        )
        {
            return false;
        }
        return TryReadReactionSnapshotStrict(
                payload["reaction_state"].AsGodotDictionary(),
                out reactionSnapshot
            )
            && TryReadCounterattackCapabilitySnapshotStrict(
                payload["counterattack_capability_state"]
                    .AsGodotDictionary(),
                out capabilitySnapshot
            );
    }

    private static bool TryReadReactionSnapshotStrict(
        GDictionary payload,
        out BattleUnitReactionSnapshot snapshot
    )
    {
        snapshot = BattleUnitReactionSnapshot.MissingOwner;
        if (
            payload == null
            || !HasExactFields(payload, ReactionStateFields)
            || !TryReadStrictBool(
                payload,
                "owner_present",
                out bool ownerPresent
            )
            || !TryReadStrictInt32(
                payload,
                "charges_remaining",
                out int chargesRemaining
            )
            || !TryReadStrictInt32(
                payload,
                "charge_capacity",
                out int chargeCapacity
            )
            || !TryReadStrictInt32(
                payload,
                "recharge_interval_tu",
                out int rechargeIntervalTu
            )
            || !TryReadStrictInt32(
                payload,
                "next_recharge_at_tu",
                out int nextRechargeAtTu
            )
        )
        {
            return false;
        }
        if (!ownerPresent)
        {
            return chargesRemaining == 0
                && chargeCapacity == 0
                && rechargeIntervalTu == 0
                && nextRechargeAtTu == 0;
        }

        var parsed = new BattleUnitReactionSnapshot(
            true,
            chargesRemaining,
            chargeCapacity,
            rechargeIntervalTu,
            nextRechargeAtTu
        );
        try
        {
            BattleReactionBudgetRules.Validate(
                new BattleReactionBudgetConfig(
                    chargeCapacity,
                    rechargeIntervalTu
                )
            );
            var validator = new BattleUnitReactionState();
            validator.RestoreRaw(parsed);
        }
        catch (ArgumentException)
        {
            return false;
        }
        snapshot = parsed;
        return true;
    }

    private static bool
        TryReadCounterattackCapabilitySnapshotStrict(
            GDictionary payload,
            out BattleUnitCounterattackCapabilitySnapshot
                snapshot
        )
    {
        snapshot =
            BattleUnitCounterattackCapabilitySnapshot
                .MissingOwner;
        if (
            payload == null
            || !HasExactFields(
                payload,
                CounterattackCapabilityStateFields
            )
            || !TryReadStrictBool(
                payload,
                "owner_present",
                out bool ownerPresent
            )
            || payload["values"].VariantType
                != Variant.Type.Array
        )
        {
            return false;
        }

        GArray rawValues =
            payload["values"].AsGodotArray();
        if (!ownerPresent)
            return rawValues.Count == 0;

        var parsed =
            new List<BattleCounterattackCapability>(
                rawValues.Count
            );
        foreach (Variant rawValue in rawValues)
        {
            if (
                rawValue.VariantType
                    != Variant.Type.Dictionary
                || !TryReadCounterattackCapabilityStrict(
                    rawValue.AsGodotDictionary(),
                    out BattleCounterattackCapability
                        capability
                )
            )
            {
                return false;
            }
            parsed.Add(capability);
        }

        BattleUnitCounterattackCapabilitySnapshot canonical;
        try
        {
            var validator =
                new BattleUnitCounterattackCapabilityState();
            validator.ReplaceAll(parsed);
            canonical = validator.CaptureRaw();
        }
        catch (ArgumentException)
        {
            return false;
        }
        if (canonical.Values.Count != parsed.Count)
            return false;
        for (int index = 0; index < parsed.Count; index++)
        {
            if (!canonical.Values[index].Equals(parsed[index]))
                return false;
        }
        snapshot = new(
            true,
            parsed.AsReadOnly()
        );
        return true;
    }

    private static bool
        TryReadCounterattackCapabilityStrict(
            GDictionary payload,
            out BattleCounterattackCapability capability
        )
    {
        capability = default;
        if (
            payload == null
            || !HasExactFields(
                payload,
                CounterattackCapabilityFields
            )
            || !TryReadNonEmptyStringNameStrict(
                payload,
                "instance_id",
                out StringName instanceId
            )
            || !TryReadNonEmptyStringNameStrict(
                payload,
                "trigger_kind",
                out StringName triggerName
            )
            || !BattleCounterattackTriggerNames.TryParse(
                triggerName,
                out BattleCounterattackTriggerKind triggerKind
            )
            || !TryReadStrictInt32(
                payload,
                "selection_priority",
                out int selectionPriority
            )
            || !TryReadStrictInt32(
                payload,
                "chance_percent",
                out int chancePercent
            )
            || chancePercent < 0
            || chancePercent > 100
            || !TryReadStrictInt32(
                payload,
                "attack_roll_bonus",
                out int attackRollBonus
            )
            || !TryReadNonEmptyStringNameStrict(
                payload,
                "weapon_action_definition_id",
                out StringName weaponActionDefinitionId
            )
        )
        {
            return false;
        }
        capability = new BattleCounterattackCapability(
            instanceId,
            triggerKind,
            selectionPriority,
            chancePercent,
            attackRollBonus,
            weaponActionDefinitionId
        );
        return true;
    }

    private static bool TryReadStrictBool(
        GDictionary payload,
        string key,
        out bool value
    )
    {
        value = false;
        Variant raw = payload[key];
        if (raw.VariantType != Variant.Type.Bool)
            return false;
        value = raw.AsBool();
        return true;
    }

    private static bool TryReadStrictInt32(
        GDictionary payload,
        string key,
        out int value
    )
    {
        value = 0;
        Variant raw = payload[key];
        if (raw.VariantType != Variant.Type.Int)
            return false;
        long parsed = raw.AsInt64();
        if (parsed < int.MinValue || parsed > int.MaxValue)
            return false;
        value = (int)parsed;
        return true;
    }

    private static bool TryReadNonEmptyStringNameStrict(
        GDictionary payload,
        string key,
        out StringName value
    )
    {
        value = "";
        Variant raw = payload[key];
        if (
            !IsStringNamePayloadType(
                raw.VariantType.ToString()
            )
        )
        {
            return false;
        }
        value = ToStringName(raw);
        return value != new StringName("");
    }
}
