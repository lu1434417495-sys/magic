using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

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

    private static T GetObject<T>(GDictionary dictionary, object key)
        where T : RefCounted
    {
        return ReadGodotObject(dictionary, key) as T;
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

    private static bool TryUnboxToDictionary(Variant value, out GDictionary dictionary)
    {
        if (value.VariantType == Variant.Type.Dictionary)
        {
            dictionary = value.AsGodotDictionary();
            return true;
        }
        dictionary = default;
        return false;
    }

    private static GodotObject ReadGodotObject(GDictionary dictionary, object key)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return null;
        return value.VariantType == Variant.Type.Object ? value.AsGodotObject() : null;
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        foreach (Variant value in values ?? new GArray())
        {
            if (value.VariantType == Variant.Type.Dictionary)
                yield return value.AsGodotDictionary();
        }
    }

    private static GArray ToUntypedArray(GDictionaryArray entries)
    {
        GArray raw = new();
        if (entries == null)
            return raw;
        foreach (GDictionary entry in entries)
            raw.Add(entry);
        return raw;
    }

    private static GArray BuildDomainOrderArray()
    {
        GArray order = new();
        foreach (string domainId in ContentValidationDomainOrder)
            order.Add(domainId);
        return order;
    }

    private static GStringArray ToGodotStringArray(IEnumerable<string> values)
    {
        GStringArray result = new();
        AppendErrors(result, values);
        return result;
    }

    private static void AppendErrors(ICollection<string> target, IEnumerable<string> source)
    {
        if (target == null || source == null)
            return;
        foreach (string value in source)
            target.Add(value ?? "");
    }

    private static Variant ToVariant(object value)
    {
        return value switch
        {
            null => default,
            Variant variantValue => variantValue,
            bool boolValue => boolValue,
            int intValue => intValue,
            long longValue => longValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            string stringValue => stringValue,
            StringName stringNameValue => stringNameValue,
            Vector2I vectorValue => vectorValue,
            GDictionary dictionaryValue => dictionaryValue,
            GArray arrayValue => arrayValue,
            GodotObject objectValue => objectValue,
            _ => value.ToString(),
        };
    }

    private static bool VariantEquals(object leftValue, object rightValue)
    {
        var left = ToVariant(leftValue);
        var right = ToVariant(rightValue);
        if (left.VariantType != right.VariantType)
            return false;
        return left.Obj == right.Obj;
    }
}
