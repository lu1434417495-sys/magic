using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleUnitChargeState
{
    private BattleStringNameIntMap _perBattleCharges = new();
    private BattleStringNameIntMap _perTurnCharges = new();
    private BattleStringNameIntMap _perTurnChargeLimits = new();
    private BattleStringNameIntMap _fumbleProtectionUsed = new();

    internal int GetPerBattle(StringName chargeKey, int fallback = 0) =>
        Get(_perBattleCharges, chargeKey, fallback);

    internal bool HasPerBattle(StringName chargeKey) =>
        Contains(_perBattleCharges, chargeKey);

    internal void SetPerBattle(StringName chargeKey, int value) =>
        Put(ref _perBattleCharges, chargeKey, value);

    internal bool RemovePerBattle(StringName chargeKey) =>
        Remove(_perBattleCharges, chargeKey);

    internal int GetPerTurn(StringName chargeKey, int fallback = 0) =>
        Get(_perTurnCharges, chargeKey, fallback);

    internal bool HasPerTurn(StringName chargeKey) =>
        Contains(_perTurnCharges, chargeKey);

    internal void SetPerTurn(StringName chargeKey, int value) =>
        Put(ref _perTurnCharges, chargeKey, value);

    internal int GetPerTurnLimit(StringName chargeKey, int fallback = 0) =>
        Get(_perTurnChargeLimits, chargeKey, fallback);

    internal bool HasPerTurnLimit(StringName chargeKey) =>
        Contains(_perTurnChargeLimits, chargeKey);

    internal void SetPerTurnLimit(StringName chargeKey, int value) =>
        Put(ref _perTurnChargeLimits, chargeKey, value);

    internal bool RemovePerTurnAndLimit(StringName chargeKey)
    {
        bool changed = Remove(_perTurnCharges, chargeKey);
        changed |= Remove(_perTurnChargeLimits, chargeKey);
        return changed;
    }

    internal int GetFumbleProtectionUsed(StringName skillId, int fallback = 0) =>
        Get(_fumbleProtectionUsed, skillId, fallback);

    internal void SetFumbleProtectionUsed(StringName skillId, int value) =>
        Put(ref _fumbleProtectionUsed, skillId, value);

    internal Dictionary<StringName, int> SnapshotPerBattle() =>
        Snapshot(_perBattleCharges);

    internal Dictionary<StringName, int> SnapshotPerTurn() =>
        Snapshot(_perTurnCharges);

    internal Dictionary<StringName, int> SnapshotPerTurnLimits() =>
        Snapshot(_perTurnChargeLimits);

    internal Dictionary<StringName, int> SnapshotFumbleProtectionUsed() =>
        Snapshot(_fumbleProtectionUsed);

    internal void ResetPerTurn()
    {
        _perTurnCharges ??= new BattleStringNameIntMap();
        _perTurnCharges.Clear();
        foreach (KeyValuePair<StringName, int> entry in SnapshotPerTurnLimits())
        {
            if (IsEmpty(entry.Key) || entry.Value <= 0)
                continue;
            _perTurnCharges.Put(entry.Key, entry.Value);
        }
    }

    internal BattleUnitChargeState DuplicateState() =>
        new()
        {
            _perBattleCharges = _perBattleCharges?.Clone() ?? new BattleStringNameIntMap(),
            _perTurnCharges = _perTurnCharges?.Clone() ?? new BattleStringNameIntMap(),
            _perTurnChargeLimits =
                _perTurnChargeLimits?.Clone() ?? new BattleStringNameIntMap(),
            _fumbleProtectionUsed =
                _fumbleProtectionUsed?.Clone() ?? new BattleStringNameIntMap(),
        };

    internal BattleStringNameIntMap CapturePerBattleForMutationSnapshotExact() =>
        _perBattleCharges?.Clone();

    internal BattleStringNameIntMap CapturePerTurnForMutationSnapshotExact() =>
        _perTurnCharges?.Clone();

    internal BattleStringNameIntMap CapturePerTurnLimitsForMutationSnapshotExact() =>
        _perTurnChargeLimits?.Clone();

    internal BattleStringNameIntMap CaptureFumbleProtectionForMutationSnapshotExact() =>
        _fumbleProtectionUsed?.Clone();

    internal void RestorePerBattleForMutationSnapshotExact(BattleStringNameIntMap values) =>
        _perBattleCharges = values?.Clone();

    internal void RestorePerTurnForMutationSnapshotExact(BattleStringNameIntMap values) =>
        _perTurnCharges = values?.Clone();

    internal void RestorePerTurnLimitsForMutationSnapshotExact(
        BattleStringNameIntMap values
    ) => _perTurnChargeLimits = values?.Clone();

    internal void RestoreFumbleProtectionForMutationSnapshotExact(
        BattleStringNameIntMap values
    ) => _fumbleProtectionUsed = values?.Clone();

    private static int Get(
        BattleStringNameIntMap source,
        StringName key,
        int fallback
    ) =>
        !IsEmpty(key) && source != null && source.ContainsKey(key)
            ? source.Get(key, fallback)
            : fallback;

    private static bool Contains(BattleStringNameIntMap source, StringName key) =>
        !IsEmpty(key) && source != null && source.ContainsKey(key);

    private static void Put(
        ref BattleStringNameIntMap source,
        StringName key,
        int value
    )
    {
        if (IsEmpty(key))
            return;
        source ??= new BattleStringNameIntMap();
        source.Put(key, Math.Max(value, 0));
    }

    private static bool Remove(BattleStringNameIntMap source, StringName key) =>
        !IsEmpty(key) && source != null && source.Remove(key);

    private static Dictionary<StringName, int> Snapshot(BattleStringNameIntMap source) =>
        source?.ToTypedDictionary() ?? new Dictionary<StringName, int>();

    private static bool IsEmpty(StringName value) =>
        value == default || value == (StringName)"";
}
