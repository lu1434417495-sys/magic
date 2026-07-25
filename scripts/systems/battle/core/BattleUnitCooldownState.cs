using System;
using System.Collections.Generic;
using Godot;

internal readonly record struct BattleUnitCooldownSnapshot(
    BattleStringNameIntMap Cooldowns,
    int LastTurnTu
)
{
    internal static BattleUnitCooldownSnapshot MissingOwner => new(null, 0);
}

internal readonly record struct BattleUnitCooldownAdvanceResult(
    int ElapsedTu,
    bool AnchorChanged,
    bool CooldownMapChanged,
    bool InvalidGranularity
);

internal sealed class BattleUnitCooldownState
{
    private BattleStringNameIntMap _cooldowns = new();
    private int _lastTurnTu = -1;

    internal int Get(StringName skillId, int fallback = 0) =>
        !IsEmpty(skillId) && _cooldowns != null && _cooldowns.ContainsKey(skillId)
            ? _cooldowns.Get(skillId, fallback)
            : fallback;

    internal void Set(StringName skillId, int value)
    {
        if (IsEmpty(skillId))
            return;

        int normalizedValue = Math.Max(value, 0);
        if (normalizedValue <= 0)
        {
            _cooldowns?.Remove(skillId);
            return;
        }

        _cooldowns ??= new BattleStringNameIntMap();
        _cooldowns.Put(skillId, normalizedValue);
    }

    internal void ReplaceNormalized(IReadOnlyDictionary<StringName, int> values)
    {
        _cooldowns = new BattleStringNameIntMap();
        if (values == null)
            return;

        foreach (KeyValuePair<StringName, int> entry in values)
        {
            if (IsEmpty(entry.Key))
                continue;

            int normalizedValue = Math.Max(entry.Value, 0);
            if (normalizedValue > 0)
                _cooldowns.Put(entry.Key, normalizedValue);
        }
    }

    internal Dictionary<StringName, int> Snapshot() =>
        _cooldowns?.ToTypedDictionary() ?? new Dictionary<StringName, int>();

    internal int GetLastTurnTu() => _lastTurnTu;

    internal void SetLastTurnTu(int value) => _lastTurnTu = value;

    internal void EnsureAnchor(int currentTu)
    {
        if (_lastTurnTu < 0)
            _lastTurnTu = currentTu;
    }

    internal BattleUnitCooldownAdvanceResult AdvanceTo(
        int currentTu,
        int granularity
    )
    {
        int previousAnchor = _lastTurnTu;
        if (_lastTurnTu < 0)
        {
            _lastTurnTu = currentTu;
            return new BattleUnitCooldownAdvanceResult(
                0,
                previousAnchor != currentTu,
                false,
                false
            );
        }

        int elapsedTu = Math.Max(currentTu - _lastTurnTu, 0);
        _lastTurnTu = currentTu;
        bool anchorChanged = previousAnchor != currentTu;
        if (elapsedTu <= 0)
        {
            return new BattleUnitCooldownAdvanceResult(
                elapsedTu,
                anchorChanged,
                false,
                false
            );
        }
        if (granularity <= 0 || elapsedTu % granularity != 0)
        {
            return new BattleUnitCooldownAdvanceResult(
                elapsedTu,
                anchorChanged,
                false,
                true
            );
        }
        return new BattleUnitCooldownAdvanceResult(
            elapsedTu,
            anchorChanged,
            AdvanceCooldowns(elapsedTu),
            false
        );
    }

    internal void AdvanceFrozenAnchor(int elapsedTu, int currentTu)
    {
        if (elapsedTu <= 0 || _lastTurnTu < 0)
            return;

        _lastTurnTu = Math.Min(_lastTurnTu + elapsedTu, currentTu);
    }

    private bool AdvanceCooldowns(int cooldownDelta)
    {
        if (cooldownDelta <= 0)
            return false;

        Dictionary<StringName, int> previousCooldowns = Snapshot();
        var retainedCooldowns = new Dictionary<StringName, int>();
        foreach (KeyValuePair<StringName, int> entry in previousCooldowns)
        {
            int remaining = Math.Max(entry.Value - cooldownDelta, 0);
            if (remaining > 0)
                retainedCooldowns[entry.Key] = remaining;
        }

        ReplaceNormalized(retainedCooldowns);
        return !MapsEqual(previousCooldowns, retainedCooldowns);
    }

    internal BattleUnitCooldownSnapshot CaptureRaw() =>
        new(_cooldowns?.Clone(), _lastTurnTu);

    internal void RestoreRaw(BattleUnitCooldownSnapshot snapshot)
    {
        _cooldowns = snapshot.Cooldowns?.Clone();
        _lastTurnTu = snapshot.LastTurnTu;
    }

    internal BattleUnitCooldownState DuplicateState() =>
        new()
        {
            _cooldowns = _cooldowns?.Clone() ?? new BattleStringNameIntMap(),
            _lastTurnTu = _lastTurnTu,
        };

    internal static BattleUnitCooldownState FromRaw(BattleUnitCooldownSnapshot snapshot)
    {
        var result = new BattleUnitCooldownState();
        result.RestoreRaw(snapshot);
        return result;
    }

    private static bool MapsEqual(
        IReadOnlyDictionary<StringName, int> left,
        IReadOnlyDictionary<StringName, int> right
    )
    {
        if (left.Count != right.Count)
            return false;

        foreach (KeyValuePair<StringName, int> entry in left)
        {
            if (!right.TryGetValue(entry.Key, out int rightValue) || rightValue != entry.Value)
                return false;
        }
        return true;
    }

    private static bool IsEmpty(StringName value) =>
        value == default || value == (StringName)"";
}
