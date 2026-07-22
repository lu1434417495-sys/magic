using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleStatusEffectCollection
{
    private readonly Dictionary<StringName, BattleStatusEffectState> _effects = new();

    public IReadOnlyDictionary<StringName, BattleStatusEffectState> Effects => _effects;
    public int Count => _effects.Count;

    public BattleStatusEffectState Get(StringName id)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(id);
        return normalized != "" && _effects.TryGetValue(normalized, out var effect)
            ? effect
            : null;
    }

    public void Set(BattleStatusEffectState effect)
    {
        if (effect == null || effect.status_id == "")
        {
            throw new ArgumentException("status_id is required", nameof(effect));
        }
        _effects[effect.status_id] = effect;
    }

    internal void SetForMutationSnapshotExact(
        StringName statusKey,
        BattleStatusEffectState effect
    )
    {
        _effects[statusKey] = effect;
    }

    internal IReadOnlyList<KeyValuePair<StringName, BattleStatusEffectState>>
        SnapshotEntriesForMutationSnapshotExact()
    {
        var result =
            new List<KeyValuePair<StringName, BattleStatusEffectState>>();
        foreach (KeyValuePair<StringName, BattleStatusEffectState> entry in _effects)
        {
            result.Add(
                new KeyValuePair<StringName, BattleStatusEffectState>(
                    entry.Key,
                    entry.Value?.DuplicateForMutationSnapshotExact()
                )
            );
        }
        return result;
    }

    public bool Remove(StringName id)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(id);
        return normalized != "" && _effects.Remove(normalized);
    }

    public void Clear()
    {
        _effects.Clear();
    }

    public List<StringName> GetSortedStatusEffectIds()
    {
        var results = new List<StringName>(_effects.Keys);
        results.Sort((left, right) => StringComparer.Ordinal.Compare(left.ToString(), right.ToString()));
        return results;
    }

    public List<BattleStatusEffectState> GetStatusEffects()
    {
        var results = new List<BattleStatusEffectState>();
        foreach (StringName statusId in GetSortedStatusEffectIds())
        {
            BattleStatusEffectState effect = Get(statusId);
            if (effect != null && !effect.IsEmpty())
            {
                results.Add(effect);
            }
        }
        return results;
    }

    public BattleStatusEffectCollection DuplicateState()
    {
        var result = new BattleStatusEffectCollection();
        foreach (StringName statusId in GetSortedStatusEffectIds())
        {
            BattleStatusEffectState effect = Get(statusId);
            if (effect != null && !effect.IsEmpty())
            {
                result.Set(effect.DuplicateState());
            }
        }
        return result;
    }

    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain()
    {
        var payload = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (StringName statusId in GetSortedStatusEffectIds())
        {
            BattleStatusEffectState effect = Get(statusId);
            if (effect != null && !effect.IsEmpty())
            {
                payload[statusId.ToString()] = effect.BuildSnapshotPlain();
            }
        }
        return payload;
    }

    internal GodotProjectionLease<GDictionary> ToDictionaryLease(
        LifetimeDomain domain,
        string reason
    ) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            BuildSnapshotPlain(),
            "battle-status-effect-collection",
            domain,
            reason
        );

    public static BattleStatusEffectCollection FromDictionary(GDictionary payload)
    {
        if (payload == null)
        {
            throw new ArgumentException("status_effects payload is required", nameof(payload));
        }

        var result = new BattleStatusEffectCollection();
        foreach (Variant rawKey in payload.Keys)
        {
            StringName statusId = ProgressionDataUtils.to_string_name(rawKey);
            if (statusId == "")
            {
                throw new ArgumentException("status_effects contains an empty status id");
            }

            Variant rawValue = payload[rawKey];
            if (rawValue.VariantType != Variant.Type.Dictionary)
            {
                throw new ArgumentException($"status_effects[{statusId}] is not a dictionary payload");
            }

            BattleStatusEffectState effect =
                BattleStatusEffectState.FromDictionary(rawValue.AsGodotDictionary());
            if (effect == null || effect.status_id != statusId)
            {
                throw new ArgumentException($"status_effects[{statusId}] has mismatched status state");
            }

            result.Set(effect);
        }
        return result;
    }
}
