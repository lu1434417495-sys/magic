using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// Strict helpers for Godot Variant/Dictionary boundary callers.
// Readers intentionally require the exact dictionary key variant type supplied by the caller.
internal static class GodotVariantReadExtensions
{
    internal static Variant GetValueOrDefault(
        this GDictionary source,
        object key,
        object fallback = null
    )
    {
        return source.TryReadValue(key, out Variant value) ? value : ToVariant(fallback);
    }

    internal static GDictionary ReadDictionaryOrEmpty(this GDictionary source, object key)
    {
        if (!source.TryReadValue(key, out Variant value))
            return new GDictionary();
        try
        {
            return value.VariantType == Variant.Type.Dictionary
                ? value.AsGodotDictionary()
                : new GDictionary();
        }
        finally
        {
            value.Dispose();
        }
    }

    internal static GArray ReadArrayOrEmpty(this GDictionary source, object key)
    {
        if (!source.TryReadValue(key, out Variant value))
            return new GArray();
        try
        {
            return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
        }
        finally
        {
            value.Dispose();
        }
    }

    internal static string ReadString(this GDictionary source, object key, string fallback = "")
    {
        if (!source.TryReadValue(key, out Variant value))
            return fallback;
        try
        {
            return value.VariantType switch
            {
                Variant.Type.String => value.AsString(),
                Variant.Type.StringName => value.AsStringName().ToString(),
                _ => fallback,
            };
        }
        finally
        {
            value.Dispose();
        }
    }

    internal static StringName ReadStringName(
        this GDictionary source,
        object key,
        StringName fallback = default
    )
    {
        if (!source.TryReadValue(key, out Variant value))
            return fallback ?? "";
        try
        {
            return value.VariantType switch
            {
                Variant.Type.StringName => value.AsStringName(),
                Variant.Type.String => ProgressionDataUtils.to_string_name(value.AsString()),
                _ => fallback ?? "",
            };
        }
        finally
        {
            value.Dispose();
        }
    }

    internal static Vector2I ReadVector2I(
        this GDictionary source,
        object key,
        Vector2I fallback = default
    )
    {
        if (!source.TryReadValue(key, out Variant value))
            return fallback;
        try
        {
            return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
        }
        finally
        {
            value.Dispose();
        }
    }

    internal static int ReadInt(this GDictionary source, object key, int fallback = 0)
    {
        if (!source.TryReadValue(key, out Variant value))
            return fallback;
        try
        {
            return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
        }
        finally
        {
            value.Dispose();
        }
    }

    internal static long ReadInt64(this GDictionary source, object key, long fallback = 0L)
    {
        if (!source.TryReadValue(key, out Variant value))
            return fallback;
        try
        {
            return value.VariantType == Variant.Type.Int ? value.AsInt64() : fallback;
        }
        finally
        {
            value.Dispose();
        }
    }

    internal static bool ReadBool(this GDictionary source, object key, bool fallback = false)
    {
        if (!source.TryReadValue(key, out Variant value))
            return fallback;
        try
        {
            return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
        }
        finally
        {
            value.Dispose();
        }
    }

    internal static bool TryAsObject<T>(this Variant value, out T result)
        where T : class
    {
        if (value.VariantType == Variant.Type.Object && value.AsGodotObject() is T typed)
        {
            result = typed;
            return true;
        }
        result = null;
        return false;
    }

    internal static bool TryAsDictionary(this Variant value, out GDictionary result)
    {
        if (value.VariantType == Variant.Type.Dictionary)
        {
            result = value.AsGodotDictionary();
            return true;
        }
        result = null;
        return false;
    }

    internal static bool TryAsGodotArray(this Variant value, out GArray result)
    {
        if (value.VariantType == Variant.Type.Array)
        {
            result = value.AsGodotArray();
            return true;
        }
        result = null;
        return false;
    }

    internal static bool TryAsInt(this Variant value, out int result)
    {
        if (value.VariantType == Variant.Type.Int)
        {
            result = value.AsInt32();
            return true;
        }
        result = 0;
        return false;
    }

    internal static bool TryAsVector2I(this Variant value, out Vector2I result)
    {
        if (value.VariantType == Variant.Type.Vector2I)
        {
            result = value.AsVector2I();
            return true;
        }
        result = Vector2I.Zero;
        return false;
    }

    internal static bool TryAsBool(this Variant value, out bool result)
    {
        if (value.VariantType == Variant.Type.Bool)
        {
            result = value.AsBool();
            return true;
        }
        result = false;
        return false;
    }

    internal static bool TryReadValue(this GDictionary source, object key, out Variant value)
    {
        value = default;
        if (source == null || key == null)
            return false;
        bool ownsVariantKey = key is not Variant;
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            Vector2I vectorKey => vectorKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        try
        {
            if (variantKey.VariantType == Variant.Type.Nil)
                return false;
            if (source.ContainsKey(variantKey))
            {
                value = source[variantKey];
                return value.VariantType != Variant.Type.Nil;
            }
            return false;
        }
        finally
        {
            if (ownsVariantKey)
                variantKey.Dispose();
        }
    }

    private static Variant ToVariant(object value) =>
        value switch
        {
            Variant variant => variant,
            string text => text,
            StringName stringName => stringName,
            int intValue => intValue,
            long longValue => longValue,
            bool boolValue => boolValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            Vector2I coord => coord,
            GArray array => array,
            GDictionary dictionary => dictionary,
            GodotObject godotObject => godotObject,
            _ => default,
        };
}
