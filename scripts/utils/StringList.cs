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

    internal GodotProjectionLease<Godot.Collections.Array> ToGodotArrayLease(
        LifetimeDomain domain,
        string reason
    )
    {
        Godot.Collections.Array result = new();
        GodotProjectionLease<Godot.Collections.Array> lease =
            GodotProjectionLease<Godot.Collections.Array>.CreateOwnedRoot(
                result,
                "string-list",
                domain,
                reason
            );
        foreach (string value in this)
            result.Add(value ?? "");
        return lease;
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

}
