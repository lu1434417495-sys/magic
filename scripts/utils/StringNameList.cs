using System.Collections.Generic;
using Godot;

public sealed class StringNameList : List<StringName>
{
    public StringNameList() { }

    public StringNameList(IEnumerable<StringName> values)
    {
        AddValues(values);
    }

    public StringNameList(Godot.Collections.Array<StringName> values)
    {
        if (values == null)
            return;
        foreach (StringName value in values)
            Add(value);
    }

    public StringNameList(Godot.Collections.Array values)
    {
        if (values == null)
            return;
        foreach (Variant value in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized != "")
                Add(normalized);
        }
    }

    public StringNameList Duplicate() => new(this);

    public Godot.Collections.Array<StringName> ToGodotArray()
    {
        Godot.Collections.Array<StringName> result = new();
        foreach (StringName value in this)
            result.Add(value);
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, "StringNameList.ToGodotArray");
        return result;
    }

    public Godot.Collections.Array<string> ToStringArray()
    {
        Godot.Collections.Array<string> result = new();
        foreach (StringName value in this)
            result.Add(value.ToString());
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, "StringNameList.ToStringArray");
        return result;
    }

    private void AddValues(IEnumerable<StringName> values)
    {
        if (values == null)
            return;
        foreach (StringName value in values)
            Add(value);
    }

    public static implicit operator StringNameList(Godot.Collections.Array<StringName> values) =>
        new(values);

    public static implicit operator Godot.Collections.Array<StringName>(StringNameList values) =>
        values?.ToGodotArray() ?? EmptyGodotArray("StringNameList.implicit");

    private static Godot.Collections.Array<StringName> EmptyGodotArray(string reason)
    {
        Godot.Collections.Array<StringName> result = new();
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, reason);
        return result;
    }
}
