using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class QuestObjectiveProgressState
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
        StringName objectiveId = ProgressionDataUtils.to_string_name(id);
        return objectiveId != "" && _values.TryGetValue(objectiveId, out int value)
            ? Mathf.Max(value, 0)
            : 0;
    }

    public bool ContainsKey(StringName id)
    {
        StringName objectiveId = ProgressionDataUtils.to_string_name(id);
        return objectiveId != "" && _values.ContainsKey(objectiveId);
    }

    public void Set(StringName id, int value)
    {
        StringName objectiveId = ProgressionDataUtils.to_string_name(id);
        if (objectiveId == "")
            throw new ArgumentException("objective id is required", nameof(id));
        if (value < 0)
            throw new ArgumentException("objective progress must be non-negative", nameof(value));
        _values[objectiveId] = value;
    }

    public QuestObjectiveProgressState DuplicateState()
    {
        var result = new QuestObjectiveProgressState();
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

    public static QuestObjectiveProgressState FromDictionary(GDictionary payload)
    {
        if (payload == null)
            throw new ArgumentException("objective_progress payload is required", nameof(payload));
        var result = new QuestObjectiveProgressState();
        foreach (Variant rawKey in payload.Keys)
        {
            StringName id = ProgressionDataUtils.to_string_name(rawKey);
            if (id == "")
                throw new ArgumentException("objective_progress contains an empty objective id");
            if (result.ContainsKey(id))
                throw new ArgumentException($"objective_progress contains duplicate objective id {id}");
            Variant rawValue = payload[rawKey];
            if (rawValue.VariantType != Variant.Type.Int)
                throw new ArgumentException($"objective_progress[{id}] is not an int");
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
