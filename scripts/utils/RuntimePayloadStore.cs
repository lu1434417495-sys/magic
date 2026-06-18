using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class RuntimePayloadStore
{
    private readonly List<GameRuntimePayloadEntry> _entries = new();

    internal int Count => _entries.Count;

    internal void Clear() => _entries.Clear();

    internal void ReplaceWithPayload(GDictionary payload)
    {
        _entries.Clear();
        if (payload == null)
            return;
        foreach (Variant key in payload.Keys)
            _entries.Add(new GameRuntimePayloadEntry(key, payload[key]));
    }

    internal void SetValue(string key, Variant value)
    {
        if (string.IsNullOrEmpty(key))
            return;
        PutPayloadValue(Variant.From(key), value);
    }

    internal void PutPayloadValue(Variant key, Variant value)
    {
        _entries.RemoveAll(entry => SameKey(entry.Key, key));
        _entries.Add(new GameRuntimePayloadEntry(key, value));
    }

    internal bool RemovePayloadValue(Variant key)
    {
        int removed = _entries.RemoveAll(entry => SameKey(entry.Key, key));
        return removed > 0;
    }

    internal bool TryGetPayloadValue(Variant key, out Variant value)
    {
        foreach (GameRuntimePayloadEntry entry in _entries)
        {
            if (!SameKey(entry.Key, key))
                continue;
            value = entry.Value;
            return true;
        }
        value = default;
        return false;
    }

    internal GDictionary ProjectPayload()
    {
        var result = new GDictionary();
        foreach (GameRuntimePayloadEntry entry in _entries)
            result[entry.Key] = entry.Value;
        return result;
    }

    private static bool SameKey(Variant left, Variant right)
    {
        if (left.VariantType != right.VariantType)
            return false;
        return left.ToString() == right.ToString();
    }
}
