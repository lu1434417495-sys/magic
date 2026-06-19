using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class UnitCustomStatMap
{
    private readonly Dictionary<StringName, int> _values = new();

    public IReadOnlyDictionary<StringName, int> ValuesTyped => _values;
    public int Count => _values.Count;

    public Godot.Collections.Array Keys
    {
        get
        {
            var result = new Godot.Collections.Array();
            foreach (StringName key in GetSortedIds())
                result.Add(key);
            return result;
        }
    }

    public Variant this[string id]
    {
        get => Variant.From(Get(new StringName(id)));
        set => Set(new StringName(id), ReadVariantInt(value));
    }

    public Variant this[StringName id]
    {
        get => Variant.From(Get(id));
        set => Set(id, ReadVariantInt(value));
    }

    public int Get(StringName id)
    {
        StringName statId = ProgressionDataUtils.to_string_name(id);
        return statId != "" && _values.TryGetValue(statId, out int value) ? value : 0;
    }

    public bool ContainsKey(StringName id)
    {
        StringName statId = ProgressionDataUtils.to_string_name(id);
        return statId != "" && _values.ContainsKey(statId);
    }

    public bool ContainsKey(string id) => ContainsKey(new StringName(id));

    public void Set(StringName id, int value)
    {
        StringName statId = ProgressionDataUtils.to_string_name(id);
        if (statId == "")
            throw new ArgumentException("custom stat id is required", nameof(id));
        _values[statId] = value;
    }

    public UnitCustomStatMap DuplicateState()
    {
        var result = new UnitCustomStatMap();
        foreach (StringName id in GetSortedIds())
            result.Set(id, _values[id]);
        return result;
    }

    public GDictionary ToDictionary()
    {
        var payload = new GDictionary();
        foreach (StringName id in GetSortedIds())
            payload[id.ToString()] = _values[id];
        return payload;
    }

    public static UnitCustomStatMap FromDictionary(GDictionary payload)
    {
        if (payload == null)
            throw new ArgumentException("custom_stats payload is required", nameof(payload));
        var result = new UnitCustomStatMap();
        foreach (Variant rawKey in payload.Keys)
        {
            StringName id = ProgressionDataUtils.to_string_name(rawKey);
            if (id == "")
                throw new ArgumentException("custom_stats contains an empty stat id");
            if (result.ContainsKey(id))
                throw new ArgumentException($"custom_stats contains duplicate stat id {id}");
            Variant rawValue = payload[rawKey];
            if (rawValue.VariantType != Variant.Type.Int)
                throw new ArgumentException($"custom_stats[{id}] is not an int");
            result.Set(id, rawValue.AsInt32());
        }
        return result;
    }

    private List<StringName> GetSortedIds()
    {
        var result = new List<StringName>(_values.Keys);
        result.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
        return result;
    }

    private static int ReadVariantInt(Variant value)
    {
        if (value.VariantType != Variant.Type.Int)
            throw new ArgumentException("value must be an int");
        return value.AsInt32();
    }
}
