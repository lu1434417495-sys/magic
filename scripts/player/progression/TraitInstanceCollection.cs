using Godot;

internal static class TraitInstanceCollection
{
    internal static Godot.Collections.Array ToPayloadArray(
        Godot.Collections.Array<TraitInstanceState> instances)
    {
        var payload = new Godot.Collections.Array();
        if (instances == null)
            return payload;
        foreach (TraitInstanceState instance in instances)
            if (instance != null)
                payload.Add(instance.ToDictionary());
        return payload;
    }

    internal static Godot.Collections.Array<TraitInstanceState> FromPayloadArray(
        Variant payload,
        TraitSourceKind expectedKind)
    {
        if (payload.VariantType != Variant.Type.Array)
            return null;
        var result = new Godot.Collections.Array<TraitInstanceState>();
        foreach (Variant entry in payload.AsGodotArray())
        {
            if (entry.VariantType != Variant.Type.Dictionary)
                return null;
            TraitInstanceState instance = TraitInstanceState.FromDictionary(
                entry.AsGodotDictionary()
            );
            if (instance == null || instance.SourceKind != expectedKind)
                return null;
            result.Add(instance);
        }
        return result;
    }

    internal static Godot.Collections.Array<TraitInstanceState> Duplicate(
        Godot.Collections.Array<TraitInstanceState> instances)
    {
        var result = new Godot.Collections.Array<TraitInstanceState>();
        if (instances == null)
            return result;
        foreach (TraitInstanceState instance in instances)
            if (instance != null)
                result.Add(instance.DuplicateState());
        return result;
    }
}
