using System.Collections;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleStringNameIntMap : IEnumerable<KeyValuePair<StringName, int>>
{
    private readonly Dictionary<StringName, int> _values = new();

    public int Count => _values.Count;

    public IEnumerable<StringName> Keys => _values.Keys;

    public IEnumerable<int> Values => _values.Values;

    public int this[StringName key]
    {
        get => Get(key);
        set => Put(key, value);
    }

    public int this[string key]
    {
        get => Get(new StringName(key ?? ""));
        set => Put(new StringName(key ?? ""), value);
    }

    public int this[Variant key]
    {
        get => Get(ToStringName(key));
        set => Put(ToStringName(key), value);
    }

    public void Clear() => _values.Clear();

    public bool ContainsKey(StringName key) => !IsEmpty(key) && _values.ContainsKey(key);

    public bool ContainsKey(string key) => ContainsKey(new StringName(key ?? ""));

    public bool ContainsKey(Variant key) => ContainsKey(ToStringName(key));

    public bool Remove(StringName key) => !IsEmpty(key) && _values.Remove(key);

    public bool Remove(string key) => Remove(new StringName(key ?? ""));

    public bool Remove(Variant key) => Remove(ToStringName(key));

    public int Get(StringName key, int fallback = 0) =>
        !IsEmpty(key) && _values.TryGetValue(key, out int value) ? value : fallback;

    public bool TryGetValue(StringName key, out int value)
    {
        value = default;
        return !IsEmpty(key) && _values.TryGetValue(key, out value);
    }

    public void Put(StringName key, int value)
    {
        if (IsEmpty(key))
            return;
        _values[key] = value;
    }

    public void ReplaceWithTyped(IReadOnlyDictionary<StringName, int> values)
    {
        _values.Clear();
        if (values == null)
            return;
        foreach (KeyValuePair<StringName, int> entry in values)
            Put(entry.Key, entry.Value);
    }

    public Dictionary<StringName, int> ToTypedDictionary() => new(_values);

    public BattleStringNameIntMap Clone()
    {
        var clone = new BattleStringNameIntMap();
        clone.ReplaceWithTyped(_values);
        return clone;
    }

    public GDictionary ProjectPayload()
    {
        GDictionary result = new();
        foreach (KeyValuePair<StringName, int> entry in _values)
            result[entry.Key] = entry.Value;
        return result;
    }

    public GDictionary ProjectStringKeyPayload()
    {
        GDictionary result = new();
        foreach (KeyValuePair<StringName, int> entry in _values)
            result[entry.Key.ToString()] = entry.Value;
        return result;
    }

    public IEnumerator<KeyValuePair<StringName, int>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal Dictionary<StringName, int>.Enumerator GetStructEnumerator() =>
        _values.GetEnumerator();

    internal static BattleStringNameIntMap FromPayloadOrNull(
        GDictionary values,
        bool requireNonEmptyKey
    )
    {
        if (values == null)
            return null;
        var result = new BattleStringNameIntMap();
        foreach (Variant key in values.Keys)
        {
            if (!IsStringNamePayloadType(key.VariantType))
                return null;
            StringName keyName = ToStringName(key);
            if (requireNonEmptyKey && IsEmpty(keyName))
                return null;
            Variant value = values[key];
            if (value.VariantType != Variant.Type.Int)
                return null;
            result.Put(keyName, value.AsInt32());
        }
        return result;
    }

    private static bool IsEmpty(StringName value) =>
        string.IsNullOrEmpty(value.ToString());

    private static bool IsStringNamePayloadType(Variant.Type type) =>
        type == Variant.Type.String || type == Variant.Type.StringName;

    private static StringName ToStringName(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName(""),
        };
    }
}

internal sealed class BattleStringNameMap : IEnumerable<KeyValuePair<StringName, StringName>>
{
    private readonly Dictionary<StringName, StringName> _values = new();

    public int Count => _values.Count;

    public IEnumerable<StringName> Keys => _values.Keys;

    public IEnumerable<StringName> Values => _values.Values;

    public StringName this[StringName key]
    {
        get => Get(key);
        set => Put(key, value);
    }

    public StringName this[string key]
    {
        get => Get(new StringName(key ?? ""));
        set => Put(new StringName(key ?? ""), value);
    }

    public StringName this[Variant key]
    {
        get => Get(ToStringName(key));
        set => Put(ToStringName(key), value);
    }

    public void Clear() => _values.Clear();

    public bool ContainsKey(StringName key) => !IsEmpty(key) && _values.ContainsKey(key);

    public bool ContainsKey(string key) => ContainsKey(new StringName(key ?? ""));

    public bool ContainsKey(Variant key) => ContainsKey(ToStringName(key));

    public bool Remove(StringName key) => !IsEmpty(key) && _values.Remove(key);

    public bool Remove(string key) => Remove(new StringName(key ?? ""));

    public bool Remove(Variant key) => Remove(ToStringName(key));

    public StringName Get(StringName key, StringName fallback = default) =>
        !IsEmpty(key) && _values.TryGetValue(key, out StringName value) ? value : fallback;

    public bool TryGetValue(StringName key, out StringName value)
    {
        value = default;
        return !IsEmpty(key) && _values.TryGetValue(key, out value);
    }

    public void Put(StringName key, StringName value)
    {
        if (IsEmpty(key) || IsEmpty(value))
            return;
        _values[key] = value;
    }

    public void ReplaceWithTyped(IReadOnlyDictionary<StringName, StringName> values)
    {
        _values.Clear();
        if (values == null)
            return;
        foreach (KeyValuePair<StringName, StringName> entry in values)
            Put(entry.Key, entry.Value);
    }

    public Dictionary<StringName, StringName> ToTypedDictionary() => new(_values);

    public BattleStringNameMap Clone()
    {
        var clone = new BattleStringNameMap();
        clone.ReplaceWithTyped(_values);
        return clone;
    }

    public GDictionary ProjectPayload()
    {
        GDictionary result = new();
        foreach (KeyValuePair<StringName, StringName> entry in _values)
            result[entry.Key] = entry.Value;
        return result;
    }

    public GDictionary ProjectStringKeyPayload()
    {
        GDictionary result = new();
        foreach (KeyValuePair<StringName, StringName> entry in _values)
            result[entry.Key.ToString()] = entry.Value.ToString();
        return result;
    }

    public IEnumerator<KeyValuePair<StringName, StringName>> GetEnumerator() =>
        _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal Dictionary<StringName, StringName>.Enumerator GetStructEnumerator() =>
        _values.GetEnumerator();

    internal static BattleStringNameMap FromPayloadOrNull(GDictionary values)
    {
        if (values == null)
            return null;
        var result = new BattleStringNameMap();
        foreach (Variant key in values.Keys)
        {
            if (!IsStringNamePayloadType(key.VariantType))
                return null;
            StringName keyName = ToStringName(key);
            if (IsEmpty(keyName) || result.ContainsKey(keyName))
                return null;
            Variant value = values[key];
            if (!IsStringNamePayloadType(value.VariantType))
                return null;
            StringName valueName = ToStringName(value);
            if (IsEmpty(valueName))
                return null;
            result.Put(keyName, valueName);
        }
        return result;
    }

    private static bool IsEmpty(StringName value) =>
        string.IsNullOrEmpty(value.ToString());

    private static bool IsStringNamePayloadType(Variant.Type type) =>
        type == Variant.Type.String || type == Variant.Type.StringName;

    private static StringName ToStringName(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName(""),
        };
    }
}
