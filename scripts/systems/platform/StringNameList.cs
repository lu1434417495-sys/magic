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

    internal GodotProjectionLease<Godot.Collections.Array> ToGodotArrayLease(
        LifetimeDomain domain,
        string reason
    )
    {
        Godot.Collections.Array result = new();
        GodotProjectionLease<Godot.Collections.Array> lease =
            GodotProjectionLease<Godot.Collections.Array>.CreateOwnedRoot(
                result,
                "string-name-list",
                domain,
                reason
            );
        foreach (StringName value in this)
            result.Add(value);
        return lease;
    }

    internal GodotProjectionLease<Godot.Collections.Array> ToStringArrayLease(
        LifetimeDomain domain,
        string reason
    )
    {
        Godot.Collections.Array result = new();
        GodotProjectionLease<Godot.Collections.Array> lease =
            GodotProjectionLease<Godot.Collections.Array>.CreateOwnedRoot(
                result,
                "string-name-list-text",
                domain,
                reason
            );
        foreach (StringName value in this)
            result.Add(value.ToString());
        return lease;
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

}
