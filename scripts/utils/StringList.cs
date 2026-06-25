using System.Collections.Generic;
using Godot;

public sealed class StringList : List<string>
{
    public StringList() { }

    public StringList(IEnumerable<string> values)
    {
        AddValues(values);
    }

    public StringList(Godot.Collections.Array<string> values)
    {
        if (values == null)
            return;
        foreach (string value in values)
            Add(value ?? "");
    }

    public StringList(Godot.Collections.Array values)
    {
        if (values == null)
            return;
        foreach (Variant value in values)
            Add(value.ToString());
    }

    public StringList Duplicate() => new(this);

    public Godot.Collections.Array<string> ToGodotArray()
    {
        Godot.Collections.Array<string> result = new();
        foreach (string value in this)
            result.Add(value ?? "");
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, "StringList.ToGodotArray");
        return result;
    }

    private void AddValues(IEnumerable<string> values)
    {
        if (values == null)
            return;
        foreach (string value in values)
            Add(value ?? "");
    }

    public static implicit operator StringList(Godot.Collections.Array<string> values) =>
        new(values);

    public static implicit operator Godot.Collections.Array<string>(StringList values) =>
        values?.ToGodotArray() ?? EmptyGodotArray("StringList.implicit");

    private static Godot.Collections.Array<string> EmptyGodotArray(string reason)
    {
        Godot.Collections.Array<string> result = new();
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, reason);
        return result;
    }
}
