using System.Collections.Generic;
using Godot;

public sealed class Vector2IList : List<Vector2I>
{
    public Vector2IList() { }

    public Vector2IList(IEnumerable<Vector2I> values)
    {
        AddValues(values);
    }

    public Vector2IList(Godot.Collections.Array<Vector2I> values)
    {
        if (values == null)
            return;
        foreach (Vector2I value in values)
            Add(value);
    }

    public Vector2IList(Godot.Collections.Array values)
    {
        if (values == null)
            return;
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Vector2I)
                Add(value.AsVector2I());
        }
    }

    public Vector2IList Duplicate() => new(this);

    internal GodotProjectionLease<Godot.Collections.Array> ToGodotArrayLease(
        LifetimeDomain domain,
        string reason
    )
    {
        Godot.Collections.Array result = new();
        GodotProjectionLease<Godot.Collections.Array> lease =
            GodotProjectionLease<Godot.Collections.Array>.CreateOwnedRoot(
                result,
                "vector2i-list",
                domain,
                reason
            );
        foreach (Vector2I value in this)
            result.Add(value);
        return lease;
    }

    private void AddValues(IEnumerable<Vector2I> values)
    {
        if (values == null)
            return;
        foreach (Vector2I value in values)
            Add(value);
    }

    public static implicit operator Vector2IList(Godot.Collections.Array<Vector2I> values) =>
        new(values);

}
