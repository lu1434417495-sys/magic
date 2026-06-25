using System.Collections;
using System.Collections.Generic;
using Godot;

public sealed class RuntimePayloadList : IEnumerable<Godot.Collections.Dictionary>
{
    private readonly List<RuntimePayloadStore> _items = new();

    public RuntimePayloadList() { }

    public RuntimePayloadList(IEnumerable<Godot.Collections.Dictionary> payloads)
    {
        AddRange(payloads);
    }

    public RuntimePayloadList(Godot.Collections.Array payloads)
    {
        SetFrom(payloads);
    }

    public int Count => _items.Count;

    public Godot.Collections.Dictionary this[int index] => _items[index].ProjectPayload();

    public void Add(Godot.Collections.Dictionary payload)
    {
        if (payload == null)
            return;
        RuntimePayloadStore item = new();
        item.ReplaceWithPayload(payload);
        _items.Add(item);
    }

    public void AddRange(IEnumerable<Godot.Collections.Dictionary> payloads)
    {
        if (payloads == null)
            return;
        foreach (Godot.Collections.Dictionary payload in payloads)
            Add(payload);
    }

    public void SetFrom(IEnumerable values)
    {
        Clear();
        if (values == null)
            return;
        foreach (object value in values)
        {
            if (TryAsDictionary(value, out Godot.Collections.Dictionary payload))
                Add(payload);
        }
    }

    public void Clear()
    {
        foreach (RuntimePayloadStore item in _items)
            item.Clear();
        _items.Clear();
    }

    public Godot.Collections.Array ToUntypedGodotArray()
    {
        Godot.Collections.Array result = new();
        foreach (RuntimePayloadStore item in _items)
            result.Add(item.ProjectPayload());
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(
            result,
            "RuntimePayloadList.ToUntypedGodotArray"
        );
        return result;
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> ToGodotArray()
    {
        Godot.Collections.Array<Godot.Collections.Dictionary> result = new();
        foreach (RuntimePayloadStore item in _items)
            result.Add(item.ProjectPayload());
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(
            result,
            "RuntimePayloadList.ToGodotArray"
        );
        return result;
    }

    public List<Godot.Collections.Dictionary> ToList()
    {
        List<Godot.Collections.Dictionary> result = new();
        foreach (RuntimePayloadStore item in _items)
            result.Add(item.ProjectPayload());
        return result;
    }

    public RuntimePayloadList DuplicateList()
    {
        RuntimePayloadList result = new();
        foreach (RuntimePayloadStore item in _items)
            result.Add(item.ProjectPayload());
        return result;
    }

    public Godot.Collections.Array Duplicate(bool deep = true) => ToUntypedGodotArray();

    public IEnumerator<Godot.Collections.Dictionary> GetEnumerator()
    {
        foreach (RuntimePayloadStore item in _items)
            yield return item.ProjectPayload();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static bool TryAsDictionary(object value, out Godot.Collections.Dictionary payload)
    {
        if (value is Godot.Collections.Dictionary dictionary)
        {
            payload = dictionary;
            return true;
        }
        if (value is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            payload = variant.AsGodotDictionary();
            return true;
        }
        payload = null;
        return false;
    }

    public static implicit operator RuntimePayloadList(Godot.Collections.Array values) =>
        new(values);

    public static implicit operator Godot.Collections.Array(RuntimePayloadList values) =>
        values?.ToUntypedGodotArray() ?? EmptyGodotArray("RuntimePayloadList.implicit");

    private static Godot.Collections.Array EmptyGodotArray(string reason)
    {
        Godot.Collections.Array result = new();
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, reason);
        return result;
    }
}
