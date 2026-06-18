using System.Collections.Generic;
using Godot;

internal sealed class BattleCalamityStore
{
    private readonly Dictionary<StringName, int> _values = new();

    internal IEnumerable<int> Values => _values.Values;

    internal int this[StringName memberId]
    {
        get => Get(memberId);
        set => Put(memberId, value);
    }

    internal int this[string memberId]
    {
        get => Get(new StringName(memberId ?? ""));
        set => Put(new StringName(memberId ?? ""), value);
    }

    internal void Clear() => _values.Clear();

    internal int Get(StringName memberId)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(memberId);
        return normalized != "" && _values.TryGetValue(normalized, out int value)
            ? Mathf.Max(value, 0)
            : 0;
    }

    internal void Put(StringName memberId, int value)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(memberId);
        if (normalized == "")
            return;
        int clamped = Mathf.Max(value, 0);
        if (clamped <= 0)
        {
            _values.Remove(normalized);
            return;
        }
        _values[normalized] = clamped;
    }

    internal Dictionary<StringName, int> Snapshot()
    {
        var result = new Dictionary<StringName, int>();
        foreach (KeyValuePair<StringName, int> entry in _values)
        {
            int value = Mathf.Max(entry.Value, 0);
            if (entry.Key != "" && value > 0)
                result[entry.Key] = value;
        }
        return result;
    }
}
