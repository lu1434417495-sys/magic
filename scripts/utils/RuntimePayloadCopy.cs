using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class RuntimePayloadCopy
{
    internal static GDictionary Dictionary(GDictionary source, string reason)
    {
        GDictionary result = source != null ? (GDictionary)source.Duplicate(true) : new GDictionary();
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, reason);
        return result;
    }

    internal static GArray Array(GArray source, string reason)
    {
        GArray result = source != null ? source.Duplicate(true) : new GArray();
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, reason);
        return result;
    }

    internal static Variant CopyVariant(Variant value, string reason)
    {
        Variant result = value.VariantType switch
        {
            Godot.Variant.Type.Dictionary => Godot.Variant.From(
                Dictionary(value.AsGodotDictionary(), reason)
            ),
            Godot.Variant.Type.Array => Godot.Variant.From(Array(value.AsGodotArray(), reason)),
            _ => value,
        };
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, reason);
        return result;
    }
}
