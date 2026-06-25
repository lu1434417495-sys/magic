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

    public Godot.Collections.Array<Vector2I> ToGodotArray()
    {
        Godot.Collections.Array<Vector2I> result = new();
        foreach (Vector2I value in this)
            result.Add(value);
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, "Vector2IList.ToGodotArray");
        return result;
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

    public static implicit operator Godot.Collections.Array<Vector2I>(Vector2IList values) =>
        values?.ToGodotArray() ?? EmptyGodotArray("Vector2IList.implicit");

    private static Godot.Collections.Array<Vector2I> EmptyGodotArray(string reason)
    {
        Godot.Collections.Array<Vector2I> result = new();
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, reason);
        return result;
    }
}
