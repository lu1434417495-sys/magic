using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleBarrierStore
{
    private readonly Dictionary<StringName, BattleBarrierInstanceState> _barriers = new();

    internal int Count => _barriers.Count;

    internal void Clear() => _barriers.Clear();

    internal void Put(BattleBarrierInstanceState barrier)
    {
        if (barrier == null)
        {
            return;
        }
        Put(barrier.BarrierInstanceId, barrier);
    }

    internal void Put(StringName barrierKey, BattleBarrierInstanceState barrier)
    {
        if (barrierKey == "" || barrier == null || barrier.IsEmpty)
        {
            return;
        }
        BattleBarrierInstanceState copy = CloneBarrier(barrier);
        if (copy.BarrierInstanceId == "")
        {
            copy.BarrierInstanceId = barrierKey;
        }
        _barriers[barrierKey] = copy;
    }

    internal bool TryGet(StringName barrierKey, out BattleBarrierInstanceState barrier)
    {
        barrier = null;
        if (barrierKey == "")
        {
            return false;
        }
        if (!_barriers.TryGetValue(barrierKey, out BattleBarrierInstanceState stored))
        {
            return false;
        }
        barrier = CloneBarrier(stored);
        return barrier != null && !barrier.IsEmpty;
    }

    internal bool Remove(StringName barrierKey)
    {
        return barrierKey != "" && _barriers.Remove(barrierKey);
    }

    internal IReadOnlyList<StringName> SortedKeys()
    {
        var keys = new List<StringName>(_barriers.Keys);
        keys.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        return keys;
    }

    internal IReadOnlyList<BattleBarrierInstanceState> ValuesSorted()
    {
        var values = new List<BattleBarrierInstanceState>();
        foreach (StringName key in SortedKeys())
        {
            if (TryGet(key, out BattleBarrierInstanceState barrier))
            {
                values.Add(barrier);
            }
        }
        return values;
    }

    internal IReadOnlyList<KeyValuePair<StringName, BattleBarrierInstanceState>> SnapshotEntries()
    {
        var result = new List<KeyValuePair<StringName, BattleBarrierInstanceState>>();
        foreach (StringName key in SortedKeys())
        {
            if (TryGet(key, out BattleBarrierInstanceState barrier))
            {
                result.Add(new KeyValuePair<StringName, BattleBarrierInstanceState>(key, barrier));
            }
        }
        return result;
    }

    internal void ReplaceWith(
        IEnumerable<KeyValuePair<StringName, BattleBarrierInstanceState>> barriers
    )
    {
        var parsed = new Dictionary<StringName, BattleBarrierInstanceState>();
        if (barriers != null)
        {
            foreach (KeyValuePair<StringName, BattleBarrierInstanceState> entry in barriers)
            {
                if (entry.Key == "" || entry.Value == null || entry.Value.IsEmpty)
                {
                    continue;
                }
                parsed[entry.Key] = CloneBarrier(entry.Value);
            }
        }
        _barriers.Clear();
        foreach (KeyValuePair<StringName, BattleBarrierInstanceState> entry in parsed)
        {
            _barriers[entry.Key] = entry.Value;
        }
    }

    internal GDictionary ProjectPayload()
    {
        GDictionary result = new();
        foreach (StringName key in SortedKeys())
        {
            if (_barriers.TryGetValue(key, out BattleBarrierInstanceState barrier))
            {
                result[key] = barrier.ToRuntimeDict();
            }
        }
        return result;
    }

    internal bool ReplaceFromPayload(GDictionary payload)
    {
        if (!TryReadPayload(payload, out Dictionary<StringName, BattleBarrierInstanceState> parsed))
        {
            return false;
        }
        _barriers.Clear();
        foreach (KeyValuePair<StringName, BattleBarrierInstanceState> entry in parsed)
        {
            _barriers[entry.Key] = entry.Value;
        }
        return true;
    }

    internal bool PutFromPayload(StringName barrierKey, GDictionary payload)
    {
        if (!TryReadBarrierPayload(barrierKey, payload, out BattleBarrierInstanceState barrier))
        {
            return false;
        }
        _barriers[barrierKey] = barrier;
        return true;
    }

    private static bool TryReadPayload(
        GDictionary payload,
        out Dictionary<StringName, BattleBarrierInstanceState> barriers
    )
    {
        barriers = new Dictionary<StringName, BattleBarrierInstanceState>();
        if (payload == null)
        {
            return true;
        }
        foreach (Variant rawKey in payload.Keys)
        {
            StringName barrierKey = ProgressionDataUtils.to_string_name(rawKey);
            if (barrierKey == "")
            {
                continue;
            }
            Variant rawValue = payload[rawKey];
            if (rawValue.VariantType != Variant.Type.Dictionary)
            {
                return false;
            }
            GDictionary barrierPayload;
            try
            {
                barrierPayload = rawValue.AsGodotDictionary();
            }
            catch
            {
                return false;
            }
            if (!TryReadBarrierPayload(barrierKey, barrierPayload, out BattleBarrierInstanceState barrier))
            {
                return false;
            }
            barriers[barrierKey] = barrier;
        }
        return true;
    }

    private static bool TryReadBarrierPayload(
        StringName barrierKey,
        GDictionary payload,
        out BattleBarrierInstanceState barrier
    )
    {
        barrier = null;
        if (barrierKey == "" || payload == null || payload.Count == 0)
        {
            return false;
        }
        try
        {
            barrier = BattleBarrierInstanceState.FromRuntimeDict(payload);
        }
        catch
        {
            barrier = null;
            return false;
        }
        if (barrier == null || barrier.IsEmpty || barrier.BarrierInstanceId == "")
        {
            return false;
        }
        if (barrier.BarrierInstanceId != barrierKey)
        {
            return false;
        }
        barrier = CloneBarrier(barrier);
        return true;
    }

    internal static BattleBarrierInstanceState CloneBarrier(BattleBarrierInstanceState source)
    {
        if (source == null)
        {
            return null;
        }
        var copy = new BattleBarrierInstanceState
        {
            BarrierInstanceId = source.BarrierInstanceId,
            ProfileId = source.ProfileId,
            DisplayName = source.DisplayName ?? "",
            SourceUnitId = source.SourceUnitId,
            SourceSkillId = source.SourceSkillId,
            AnchorMode = source.AnchorMode,
            AnchorCoord = source.AnchorCoord,
            RadiusCells = source.RadiusCells,
            AreaPattern = source.AreaPattern,
            RemainingTu = source.RemainingTu,
            CreatedTu = source.CreatedTu,
            SaveDc = source.SaveDc,
            CatchAllProjectedEffects = source.CatchAllProjectedEffects,
        };
        var layers = new List<BattleBarrierLayerState>();
        foreach (BattleBarrierLayerState layer in source.GetLayersTyped())
        {
            BattleBarrierLayerState layerCopy = CloneLayer(layer);
            if (layerCopy != null && layerCopy.LayerId != "")
            {
                layers.Add(layerCopy);
            }
        }
        copy.SetLayers(layers);
        return copy;
    }

    private static BattleBarrierLayerState CloneLayer(BattleBarrierLayerState source)
    {
        if (source == null)
        {
            return null;
        }
        var copy = new BattleBarrierLayerState
        {
            LayerId = source.LayerId,
            DisplayName = source.DisplayName ?? "",
            Order = source.Order,
            Broken = source.Broken,
            HasSaveRollOverride = source.HasSaveRollOverride,
            SaveRollOverride = source.SaveRollOverride,
        };
        copy.SetBlockedCategories(source.BlockedCategories);
        copy.SetBreakerSkillIds(source.BreakerSkillIds);
        var outcomes = new List<BattleBarrierOutcomeState>();
        foreach (BattleBarrierOutcomeState outcome in source.GetPassageOutcomesTyped())
        {
            BattleBarrierOutcomeState outcomeCopy = CloneOutcome(outcome);
            if (outcomeCopy != null && !outcomeCopy.IsEmpty)
            {
                outcomes.Add(outcomeCopy);
            }
        }
        copy.SetPassageOutcomes(outcomes);
        return copy;
    }

    private static BattleBarrierOutcomeState CloneOutcome(BattleBarrierOutcomeState source)
    {
        if (source == null)
        {
            return null;
        }
        return new BattleBarrierOutcomeState
        {
            OutcomeType = source.OutcomeType,
            Amount = source.Amount,
            DamageTag = source.DamageTag,
            HalfOnSuccess = source.HalfOnSuccess,
            SuccessAmount = source.SuccessAmount,
            SuccessDamageTag = source.SuccessDamageTag,
            FatalDamage = source.FatalDamage,
            StatusId = source.StatusId,
            SaveAbility = source.SaveAbility,
            SaveTag = source.SaveTag,
            SaveDc = source.SaveDc,
        };
    }
}
