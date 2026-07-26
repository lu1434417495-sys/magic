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
        return TryRead(source, key, out Variant value) ? value : ToVariant(fallback);
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

    private static bool TryRead(GDictionary source, object key, out Variant value)
    {
        value = default;
        if (source == null || key == null)
            return false;
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        if (variantKey.VariantType == Variant.Type.Nil)
            return false;
        if (source.ContainsKey(variantKey))
        {
            value = source[variantKey];
            return value.VariantType != Variant.Type.Nil;
        }
        return false;
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
