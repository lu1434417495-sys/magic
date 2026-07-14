using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// Partial slice of GameSession — static Godot dictionary/variant read + conversion helpers.
// Pure physical split: same class, no behavior change. See GameSession.cs.
public partial class GameSession
{

    private static GDictionary GetDictionary(GDictionary dictionary, object key)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return new GDictionary();
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static GArray GetArray(GDictionary dictionary, object key)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return new GArray();
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static string GetString(GDictionary dictionary, object key, string fallback = "")
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            _ => fallback,
        };
    }

    private static StringName GetStringName(
        GDictionary dictionary,
        object key,
        StringName fallback = default
    )
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback ?? new StringName("");
        return value.VariantType switch
        {
            Variant.Type.String => new StringName(value.AsString()),
            _ => fallback ?? new StringName(""),
        };
    }

    private static int GetInt(GDictionary dictionary, object key, int fallback = 0)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static bool ReadExactBool(GDictionary dictionary, object key, bool fallback = false)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static Vector2I GetVector2I(GDictionary dictionary, object key, Vector2I fallback)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    private static bool TryRead(GDictionary dictionary, object key, out Variant value)
    {
        if (dictionary == null || key == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        if (dictionary.ContainsKey(variantKey))
        {
            value = dictionary[variantKey];
            return true;
        }
        value = default;
        return false;
    }

    private static bool HasArray(GDictionary dictionary, object key) =>
        TryRead(dictionary, key, out Variant value) && value.VariantType == Variant.Type.Array;

    private static bool HasInt(GDictionary dictionary, object key) =>
        TryRead(dictionary, key, out Variant value) && value.VariantType == Variant.Type.Int;

    private static bool HasString(GDictionary dictionary, object key) =>
        TryRead(dictionary, key, out Variant value)
        && value.VariantType == Variant.Type.String;

    private static StringNameList ReadStringNameList(GArray values)
    {
        var result = new StringNameList();
        if (values == null)
            return result;
        foreach (Variant value in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized != "")
                result.Add(normalized);
        }
        return result;
    }

    private static void AppendErrors(ICollection<string> target, IEnumerable<string> source)
    {
        if (target == null || source == null)
            return;
        foreach (string value in source)
            target.Add(value ?? "");
    }

}
