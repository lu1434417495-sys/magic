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

    public static GodotObject GetObject(GodotObject src, StringName property)
    {
        if (src == null) return null;
        Variant value = src.Get(property);
        return value.VariantType == Variant.Type.Nil ? null : value.AsGodotObject();
    }

    public static GDictionary GetDictionary(GodotObject src, StringName property)
    {
        if (src == null) return new GDictionary();
        Variant value = src.Get(property);
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new GDictionary();
    }

    public static GArray GetArray(GodotObject src, StringName property)
    {
        if (src == null) return new GArray();
        Variant value = src.Get(property);
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    public static StringName GetStringName(GodotObject src, StringName property, StringName fallback = default)
    {
        if (src == null) return fallback ?? new StringName("");
        return ToStringName(src.Get(property), fallback);
    }

    public static string GetString(GodotObject src, StringName property, string fallback = "")
    {
        if (src == null) return fallback;
        Variant value = src.Get(property);
        return value.VariantType == Variant.Type.Nil ? fallback : value.ToString();
    }

    public static int GetInt(GodotObject src, StringName property, int fallback = 0)
    {
        if (src == null) return fallback;
        Variant value = src.Get(property);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }

    public static bool GetBool(GodotObject src, StringName property, bool fallback = false)
    {
        if (src == null) return fallback;
        Variant value = src.Get(property);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
    }

    public static double GetFloat(GodotObject src, StringName property, double fallback = 0.0)
    {
        if (src == null) return fallback;
        Variant value = src.Get(property);
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsDouble();
    }

    public static Vector2I GetVector2I(GodotObject src, StringName property, Vector2I fallback = default)
    {
        if (src == null) return fallback;
        Variant value = src.Get(property);
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    // ============================================================
    // GDictionary 取值（key 用 Variant，兼容 string / StringName / int）
    // ============================================================

    public static GodotObject GetObject(GDictionary src, Variant key)
    {
        if (!TryGet(src, key, out Variant value)) return null;
        return value.VariantType == Variant.Type.Nil ? null : value.AsGodotObject();
    }

    public static GDictionary GetDictionary(GDictionary src, Variant key)
    {
        if (!TryGet(src, key, out Variant value)) return new GDictionary();
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new GDictionary();
    }

    public static GArray GetArray(GDictionary src, Variant key)
    {
        if (!TryGet(src, key, out Variant value)) return new GArray();
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    public static StringName GetStringName(GDictionary src, Variant key, StringName fallback = default)
    {
        if (!TryGet(src, key, out Variant value)) return fallback ?? new StringName("");
        return ToStringName(value, fallback);
    }

    public static string GetString(GDictionary src, Variant key, string fallback = "")
    {
        if (!TryGet(src, key, out Variant value)) return fallback;
        return value.VariantType == Variant.Type.Nil ? fallback : value.ToString();
    }

    public static int GetInt(GDictionary src, Variant key, int fallback = 0)
    {
        if (!TryGet(src, key, out Variant value)) return fallback;
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }

    public static bool GetBool(GDictionary src, Variant key, bool fallback = false)
    {
        if (!TryGet(src, key, out Variant value)) return fallback;
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
    }

    public static double GetFloat(GDictionary src, Variant key, double fallback = 0.0)
    {
        if (!TryGet(src, key, out Variant value)) return fallback;
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsDouble();
    }

    public static Vector2I GetVector2I(GDictionary src, Variant key, Vector2I fallback = default)
    {
        if (!TryGet(src, key, out Variant value)) return fallback;
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    public static bool TryGet(GDictionary src, Variant key, out Variant value)
    {
        if (src != null)
        {
            if (src.ContainsKey(key))
            {
                value = src[key];
                return true;
            }
            if (key.VariantType == Variant.Type.String)
            {
                StringName stringNameKey = new(key.AsString());
                if (src.ContainsKey(stringNameKey))
                {
                    value = src[stringNameKey];
                    return true;
                }
            }
            else if (key.VariantType == Variant.Type.StringName)
            {
                string stringKey = key.AsStringName().ToString();
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
    public static Variant GetValueOrDefault(this GDictionary src, Variant key, Variant fallback = default)
    {
        return TryGet(src, key, out Variant value) ? value : fallback;
    }

    public static Variant _get(this GDictionary src, Variant key, Variant fallback = default)
    {
        return GetValueOrDefault(src, key, fallback);
    }

    public static GDictionary AsGodotDictionary(this GDictionary src)
    {
        return src ?? new GDictionary();
    }

    public static Variant New(this Script script)
    {
        return script == null ? default : script.Call("new");
    }

    public static bool IsEmpty(this string value)
    {
        return string.IsNullOrEmpty(value);
    }


    // ============================================================
    // StringName 工具
    // ============================================================

    public static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    public static StringName ToStringName(Variant value, StringName fallback = default)
    {
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => fallback ?? new StringName(""),
        };
    }
}
