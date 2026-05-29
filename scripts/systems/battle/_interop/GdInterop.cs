using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// 战斗模块 C# ↔ GDScript Variant 边界 helper。
// 设计契约见 docs/design/battle_csharp_migration.md §4。
// 不要在 .cs 文件里重复定义这些 helper；统一调 GdInterop。

internal static class GdInterop
{
    // ============================================================

    // GodotObject 属性读取

    // ============================================================

    internal static GodotObject GetObject(GodotObject src, StringName property)
    {
        if (src == null)
            return null;

        var value = src.Get(property);

        return value.VariantType == Variant.Type.Nil ? null : value.AsGodotObject();
    }

    internal static GDictionary GetDictionary(GodotObject src, StringName property)
    {
        if (src == null)
            return new GDictionary();

        var value = src.Get(property);

        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    internal static GArray GetArray(GodotObject src, StringName property)
    {
        if (src == null)
            return new GArray();

        var value = src.Get(property);

        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    internal static StringName GetStringName(
        GodotObject src,
        StringName property,
        StringName fallback = default
    )
    {
        if (src == null)
            return fallback ?? new StringName("");

        return ToStringName(src.Get(property), fallback);
    }

    internal static string GetString(GodotObject src, StringName property, string fallback = "")
    {
        if (src == null)
            return fallback;

        var value = src.Get(property);

        return value.VariantType == Variant.Type.Nil ? fallback : value.ToString();
    }

    internal static int GetInt(GodotObject src, StringName property, int fallback = 0)
    {
        if (src == null)
            return fallback;

        var value = src.Get(property);

        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }

    internal static bool GetBool(GodotObject src, StringName property, bool fallback = false)
    {
        if (src == null)
            return fallback;

        var value = src.Get(property);

        return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
    }

    internal static double GetFloat(GodotObject src, StringName property, double fallback = 0.0)
    {
        if (src == null)
            return fallback;

        var value = src.Get(property);

        return value.VariantType == Variant.Type.Nil ? fallback : value.AsDouble();
    }

    internal static Vector2I GetVector2I(
        GodotObject src,
        StringName property,
        Vector2I fallback = default
    )
    {
        if (src == null)
            return fallback;

        var value = src.Get(property);

        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    // ============================================================

    // GDictionary 取值（key 用 Variant，兼容 string / StringName / int）

    // ============================================================

    internal static GodotObject GetObject(GDictionary src, object key)
    {
        if (!TryGet(src, key, out Variant value))
            return null;

        return value.VariantType == Variant.Type.Nil ? null : value.AsGodotObject();
    }

    internal static GDictionary GetDictionary(GDictionary src, object key)
    {
        if (!TryGet(src, key, out Variant value))
            return new GDictionary();

        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    internal static GArray GetArray(GDictionary src, object key)
    {
        if (!TryGet(src, key, out Variant value))
            return new GArray();

        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    internal static StringName GetStringName(
        GDictionary src,
        object key,
        StringName fallback = default
    )
    {
        if (!TryGet(src, key, out Variant value))
            return fallback ?? new StringName("");

        return ToStringName(value, fallback);
    }

    internal static string GetString(GDictionary src, object key, string fallback = "")
    {
        if (!TryGet(src, key, out Variant value))
            return fallback;

        return value.VariantType == Variant.Type.Nil ? fallback : value.ToString();
    }

    internal static int GetInt(GDictionary src, object key, int fallback = 0)
    {
        if (!TryGet(src, key, out Variant value))
            return fallback;

        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }

    internal static bool GetBool(GDictionary src, object key, bool fallback = false)
    {
        if (!TryGet(src, key, out Variant value))
            return fallback;

        return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
    }

    internal static double GetFloat(GDictionary src, object key, double fallback = 0.0)
    {
        if (!TryGet(src, key, out Variant value))
            return fallback;

        return value.VariantType == Variant.Type.Nil ? fallback : value.AsDouble();
    }

    internal static Vector2I GetVector2I(GDictionary src, object key, Vector2I fallback = default)
    {
        if (!TryGet(src, key, out Variant value))
            return fallback;

        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    internal static bool TryGet(GDictionary src, object key, out Variant value)
    {
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        if (src != null)
        {
            if (src.ContainsKey(variantKey))
            {
                value = src[variantKey];

                return true;
            }

            if (variantKey.VariantType == Variant.Type.String)
            {
                StringName stringNameKey = new(variantKey.AsString());

                if (src.ContainsKey(stringNameKey))
                {
                    value = src[stringNameKey];

                    return true;
                }
            }
            else if (variantKey.VariantType == Variant.Type.StringName)
            {
                string stringKey = variantKey.AsStringName().ToString();

                if (src.ContainsKey(stringKey))
                {
                    value = src[stringKey];

                    return true;
                }
            }
        }

        value = default;

        return false;
    }

    internal static Variant GetValueOrDefault(
        this GDictionary src,
        object key,
        object fallback = null
    )
    {
        if (TryGet(src, key, out Variant value))
            return value;
        return ToVariant(fallback);
    }

    internal static Variant ToVariant(object value) =>
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

    // 泛型对象读取
    internal static T GetObject<T>(GDictionary src, object key) where T : GodotObject
        => GetObject(src, key) as T;

    internal static T GetObject<T>(GodotObject src, StringName property) where T : GodotObject
        => GetObject(src, property) as T;

    // 类型存在性检查（无需 Variant 出现在调用方）
    internal static bool HasInt(GDictionary src, object key)
        => TryGet(src, key, out var v) && v.VariantType == Variant.Type.Int;

    internal static bool HasFloat(GDictionary src, object key)
        => TryGet(src, key, out var v) && v.VariantType == Variant.Type.Float;

    internal static bool HasBool(GDictionary src, object key)
        => TryGet(src, key, out var v) && v.VariantType == Variant.Type.Bool;

    internal static bool HasString(GDictionary src, object key)
    {
        if (!TryGet(src, key, out var v))
            return false;
        return v.VariantType == Variant.Type.String || v.VariantType == Variant.Type.StringName;
    }

    internal static bool HasDictionary(GDictionary src, object key)
        => TryGet(src, key, out var v) && v.VariantType == Variant.Type.Dictionary;

    internal static bool HasArray(GDictionary src, object key)
        => TryGet(src, key, out var v) && v.VariantType == Variant.Type.Array;

    internal static bool HasObject(GDictionary src, object key)
        => TryGet(src, key, out var v) && v.VariantType == Variant.Type.Object;

    internal static bool HasVector2I(GDictionary src, object key)
        => TryGet(src, key, out var v) && v.VariantType == Variant.Type.Vector2I;

    // 类型化字典迭代（调用方零 Variant）
    internal static IEnumerable<(string key, int value)> ReadStringIntEntries(GDictionary src)
    {
        if (src == null)
            yield break;
        foreach (var rawKey in src.Keys)
        {
            if (rawKey.VariantType != Variant.Type.String && rawKey.VariantType != Variant.Type.StringName)
                continue;
            var rawVal = src[rawKey];
            if (rawVal.VariantType != Variant.Type.Int)
                continue;
            yield return (rawKey.AsString(), rawVal.AsInt32());
        }
    }

    internal static IEnumerable<(StringName key, int value)> ReadStringNameIntEntries(GDictionary src)
    {
        if (src == null)
            yield break;
        foreach (var rawKey in src.Keys)
        {
            if (rawKey.VariantType == Variant.Type.StringName)
            {
                var rawVal = src[rawKey];
                if (rawVal.VariantType == Variant.Type.Int)
                    yield return (rawKey.AsStringName(), rawVal.AsInt32());
            }
            else if (rawKey.VariantType == Variant.Type.String)
            {
                var rawVal = src[rawKey];
                if (rawVal.VariantType == Variant.Type.Int)
                    yield return (new StringName(rawKey.AsString()), rawVal.AsInt32());
            }
        }
    }

    internal static IEnumerable<(StringName key, StringName value)> ReadStringNameStringNameEntries(
        GDictionary src
    )
    {
        if (src == null)
            yield break;
        foreach (var rawKey in src.Keys)
        {
            StringName key;
            if (rawKey.VariantType == Variant.Type.StringName)
                key = rawKey.AsStringName();
            else if (rawKey.VariantType == Variant.Type.String)
                key = new StringName(rawKey.AsString());
            else
                continue;
            var rawVal = src[rawKey];
            if (rawVal.VariantType == Variant.Type.StringName)
                yield return (key, rawVal.AsStringName());
            else if (rawVal.VariantType == Variant.Type.String)
                yield return (key, new StringName(rawVal.AsString()));
        }
    }

    internal static IEnumerable<(StringName key, T value)> ReadStringNameObjectEntries<T>(
        GDictionary src
    )
        where T : GodotObject
    {
        if (src == null)
            yield break;
        foreach (var rawKey in src.Keys)
        {
            StringName key;
            if (rawKey.VariantType == Variant.Type.StringName)
                key = rawKey.AsStringName();
            else if (rawKey.VariantType == Variant.Type.String)
                key = new StringName(rawKey.AsString());
            else
                continue;
            var rawVal = src[rawKey];
            if (rawVal.VariantType == Variant.Type.Object && rawVal.AsGodotObject() is T typed)
                yield return (key, typed);
        }
    }

    internal static IEnumerable<(StringName key, GDictionary value)> ReadStringNameDictEntries(
        GDictionary src
    )
    {
        if (src == null)
            yield break;
        foreach (var rawKey in src.Keys)
        {
            StringName key;
            if (rawKey.VariantType == Variant.Type.StringName)
                key = rawKey.AsStringName();
            else if (rawKey.VariantType == Variant.Type.String)
                key = new StringName(rawKey.AsString());
            else
                continue;
            var rawVal = src[rawKey];
            if (rawVal.VariantType == Variant.Type.Dictionary)
                yield return (key, rawVal.AsGodotDictionary());
        }
    }

    // object → typed 解包（用于接受来自 GDScript/GArray 的 object 参数）
    internal static T UnboxToObject<T>(object raw) where T : GodotObject
    {
        if (raw is T typed) return typed;
        if (raw is GodotObject obj && obj is T typedObj) return typedObj;
        if (raw is Variant v && v.TryAsObject<T>(out var typedVar)) return typedVar;
        return null;
    }

    internal static bool TryUnboxToDictionary(object raw, out GDictionary result)
    {
        if (raw is GDictionary dict) { result = dict; return true; }
        if (raw is Variant v && v.TryAsDictionary(out result)) return true;
        result = null;
        return false;
    }

    internal static bool TryUnboxToArray(object raw, out GArray result)
    {
        if (raw is GArray arr) { result = arr; return true; }
        if (raw is Variant v && v.TryAsGodotArray(out result)) return true;
        result = null;
        return false;
    }

    // GArray 类型化迭代（调用方零 Variant）
    internal static IEnumerable<GDictionary> ReadDictionaryItems(GArray src)
    {
        if (src == null)
            yield break;
        foreach (var item in src)
            if (item.VariantType == Variant.Type.Dictionary)
                yield return item.AsGodotDictionary();
    }

    internal static IEnumerable<T> ReadObjectItems<T>(GArray src) where T : GodotObject
    {
        if (src == null)
            yield break;
        foreach (var item in src)
            if (item.VariantType == Variant.Type.Object && item.AsGodotObject() is T typed)
                yield return typed;
    }

    internal static IEnumerable<string> ReadStringItems(GArray src)
    {
        if (src == null)
            yield break;
        foreach (var item in src)
        {
            if (item.VariantType == Variant.Type.String)
                yield return item.AsString();
            else if (item.VariantType == Variant.Type.StringName)
                yield return item.AsStringName().ToString();
        }
    }

    // Variant 值类型萃取扩展（调用方用 var 声明，写 .TryAsX 不写 Variant 关键字）
    internal static bool TryAsObject<T>(this Variant v, out T result) where T : GodotObject
    {
        if (v.VariantType == Variant.Type.Object && v.AsGodotObject() is T typed)
        {
            result = typed;
            return true;
        }
        result = null;
        return false;
    }

    internal static bool TryAsDictionary(this Variant v, out GDictionary result)
    {
        if (v.VariantType == Variant.Type.Dictionary)
        {
            result = v.AsGodotDictionary();
            return true;
        }
        result = null;
        return false;
    }

    internal static bool TryAsGodotArray(this Variant v, out GArray result)
    {
        if (v.VariantType == Variant.Type.Array)
        {
            result = v.AsGodotArray();
            return true;
        }
        result = null;
        return false;
    }

    internal static bool TryAsInt(this Variant v, out int result)
    {
        if (v.VariantType == Variant.Type.Int)
        {
            result = v.AsInt32();
            return true;
        }
        result = 0;
        return false;
    }

    internal static bool TryAsString(this Variant v, out string result)
    {
        if (v.VariantType == Variant.Type.String)
        {
            result = v.AsString();
            return true;
        }
        if (v.VariantType == Variant.Type.StringName)
        {
            result = v.AsStringName().ToString();
            return true;
        }
        result = "";
        return false;
    }

    internal static bool TryAsStringName(this Variant v, out StringName result)
    {
        if (v.VariantType == Variant.Type.StringName)
        {
            result = v.AsStringName();
            return true;
        }
        if (v.VariantType == Variant.Type.String)
        {
            result = new StringName(v.AsString());
            return true;
        }
        result = new StringName("");
        return false;
    }

    internal static bool TryAsVector2I(this Variant v, out Vector2I result)
    {
        if (v.VariantType == Variant.Type.Vector2I)
        {
            result = v.AsVector2I();
            return true;
        }
        result = default;
        return false;
    }

    internal static bool TryAsBool(this Variant v, out bool result)
    {
        if (v.VariantType == Variant.Type.Bool)
        {
            result = v.AsBool();
            return true;
        }
        result = false;
        return false;
    }

    internal static bool IsEmpty(this string value)
    {
        return string.IsNullOrEmpty(value);
    }

    // ============================================================

    // StringName 工具

    // ============================================================

    internal static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    internal static StringName ToStringName(object rawValue, StringName fallback = default)
    {
        if (rawValue is string text)
        {
            return string.IsNullOrEmpty(text) ? fallback ?? new StringName("") : new StringName(text);
        }
        if (rawValue is StringName stringName)
        {
            return stringName;
        }
        if (rawValue is not Variant value)
        {
            return fallback ?? new StringName("");
        }
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),

            Variant.Type.String => new StringName(value.AsString()),

            _ => fallback ?? new StringName(""),
        };
    }
}
