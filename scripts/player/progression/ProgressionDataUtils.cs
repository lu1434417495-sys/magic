using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class ProgressionDataUtils : RefCounted
{
    public static StringName to_string_name(Variant value)
    {
        if (value.VariantType == Variant.Type.Nil)
        {
            return "";
        }

        string text = value.VariantType == Variant.Type.StringName
            ? value.AsStringName().ToString()
            : value.ToString();
        return string.IsNullOrEmpty(text) || text == "<null>" ? "" : new StringName(text);
    }

    public static string string_name_to_string(StringName value)
    {
        return value.ToString();
    }

    public static Godot.Collections.Array<StringName> to_string_name_array(Variant values)
    {
        var result = new Godot.Collections.Array<StringName>();
        if (values.VariantType != Variant.Type.Array)
        {
            return result;
        }

        foreach (Variant value in values.AsGodotArray())
        {
            StringName normalized = to_string_name(value);
            if (normalized != (StringName)"")
            {
                result.Add(normalized);
            }
        }
        return result;
    }

    public static Godot.Collections.Array<string> string_name_array_to_string_array(Godot.Collections.Array<StringName> values)
    {
        var result = new Godot.Collections.Array<string>();
        foreach (StringName value in values)
        {
            result.Add(value.ToString());
        }
        return result;
    }

    public static GDictionary to_string_name_int_map(Variant values)
    {
        var result = new GDictionary();
        if (values.VariantType != Variant.Type.Dictionary)
        {
            return result;
        }

        GDictionary source = values.AsGodotDictionary();
        foreach (Variant key in source.Keys)
        {
            result[to_string_name(key)] = source[key].AsInt32();
        }
        return result;
    }

    public static GDictionary string_name_int_map_to_string_dict(GDictionary values)
    {
        var result = new GDictionary();
        foreach (Variant key in values.Keys)
        {
            result[key.ToString()] = values[key].AsInt32();
        }
        return result;
    }

    public static GDictionary to_string_name_array_map(Variant values)
    {
        var result = new GDictionary();
        if (values.VariantType != Variant.Type.Dictionary)
        {
            return result;
        }

        GDictionary source = values.AsGodotDictionary();
        foreach (Variant key in source.Keys)
        {
            result[to_string_name(key)] = to_string_name_array(source[key]);
        }
        return result;
    }

    public static GDictionary string_name_array_map_to_string_dict(GDictionary values)
    {
        var result = new GDictionary();
        foreach (Variant key in values.Keys)
        {
            if (values[key].VariantType == Variant.Type.Array)
            {
                result[key.ToString()] = StringNameArrayVariantToStringArray(values[key]);
            }
        }
        return result;
    }

    public static Godot.Collections.Array<string> sorted_string_keys(GDictionary values)
    {
        var sorted = new System.Collections.Generic.List<string>();
        foreach (Variant key in values.Keys)
        {
            sorted.Add(key.ToString());
        }
        sorted.Sort(System.StringComparer.Ordinal);

        var result = new Godot.Collections.Array<string>();
        foreach (string key in sorted)
        {
            result.Add(key);
        }
        return result;
    }

    private static Godot.Collections.Array<string> StringNameArrayVariantToStringArray(Variant value)
    {
        var result = new Godot.Collections.Array<string>();
        foreach (Variant entry in value.AsGodotArray())
        {
            StringName normalized = to_string_name(entry);
            if (normalized != (StringName)"")
            {
                result.Add(normalized.ToString());
            }
        }
        return result;
    }
}
