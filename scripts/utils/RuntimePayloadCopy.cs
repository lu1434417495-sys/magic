using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class RuntimePayloadCopy
{
    internal static GDictionary DictionaryInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary source,
        string reason
    )
        where TLeaseRoot : class, System.IDisposable =>
        RuntimePlainPayload.ProjectDictionaryInto(
            lease,
            RuntimePlainPayload.NormalizeDictionary(source, reason),
            reason
        );

    internal static GArray ArrayInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GArray source,
        string reason
    )
        where TLeaseRoot : class, System.IDisposable =>
        RuntimePlainPayload.ProjectArrayInto(
            lease,
            RuntimePlainPayload.NormalizeArray(source, reason),
            reason
        );

    internal static Variant CopyVariantInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        Variant value,
        string reason
    )
        where TLeaseRoot : class, System.IDisposable
    {
        if (value.VariantType == Godot.Variant.Type.Dictionary)
        {
            using GDictionary source = value.AsGodotDictionary();
            return Godot.Variant.From(DictionaryInto(lease, source, reason));
        }
        if (value.VariantType == Godot.Variant.Type.Array)
        {
            using GArray source = value.AsGodotArray();
            return Godot.Variant.From(ArrayInto(lease, source, reason));
        }
        return value;
    }
}
