using Godot;
using System.Collections.Generic;

internal static class TraitInstanceCollection
{
    internal static Godot.Collections.Array ToPayloadArray(
        IEnumerable<TraitInstanceState> instances)
    {
        var payload = new Godot.Collections.Array();
        if (instances == null)
            return payload;
        foreach (TraitInstanceState instance in instances)
            if (instance != null)
                payload.Add(instance.ToDictionary());
        return payload;
    }

    internal static List<TraitInstanceState> FromPayloadArray(
        Variant payload,
        TraitSourceKind expectedKind)
    {
        if (payload.VariantType != Variant.Type.Array)
            return null;
        List<TraitInstanceState> result = new();
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

    internal static List<TraitInstanceState> Duplicate(
        IEnumerable<TraitInstanceState> instances)
    {
        List<TraitInstanceState> result = new();
        if (instances == null)
            return result;
        foreach (TraitInstanceState instance in instances)
            if (instance != null)
                result.Add(instance.DuplicateState());
        return result;
    }
}
