using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal static class ProgressionContentBundleAdapter
{
    internal static Dictionary<StringName, T> ReadDefMap<T>(
        GDictionary contentBundle,
        string primaryBucket,
        string aliasBucket
    )
        where T : class
    {
        var entries = new Dictionary<StringName, T>();
        GDictionary bucket = ReadBucket(contentBundle, primaryBucket, aliasBucket);
        if (bucket == null)
            return entries;

        foreach (Variant rawKey in bucket.Keys)
        {
            StringName id = ToStringName(rawKey);
            if (id == "" || entries.ContainsKey(id))
                continue;

            T value = ReadObject<T>(bucket[rawKey]);
            if (value != null)
                entries[id] = value;
        }
        return entries;
    }

    private static GDictionary ReadBucket(
        GDictionary contentBundle,
        string primaryBucket,
        string aliasBucket
    )
    {
        if (TryReadBucket(contentBundle, primaryBucket, out GDictionary bucket))
            return bucket;
        if (TryReadBucket(contentBundle, aliasBucket, out bucket))
            return bucket;
        return null;
    }

    private static bool TryReadBucket(GDictionary contentBundle, string key, out GDictionary bucket)
    {
        bucket = null;
        if (contentBundle == null || !contentBundle.ContainsKey(key))
            return false;

        Variant rawBucket = contentBundle[key];
        if (rawBucket.VariantType != Variant.Type.Dictionary)
            return false;

        bucket = rawBucket.AsGodotDictionary();
        return true;
    }

    private static T ReadObject<T>(Variant rawValue)
        where T : class
    {
        return rawValue.VariantType == Variant.Type.Object ? rawValue.AsGodotObject() as T : null;
    }

    private static StringName ToStringName(Variant rawKey)
    {
        return rawKey.VariantType switch
        {
            Variant.Type.StringName => rawKey.AsStringName(),
            Variant.Type.String => new StringName(rawKey.AsString()),
            _ => new StringName(rawKey.AsString()),
        };
    }
}
