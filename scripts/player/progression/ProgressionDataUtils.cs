using Godot;
using System.Collections.Generic;
using GDictionary = Godot.Collections.Dictionary;

internal static class ProgressionDataUtils
{
    internal static StringName to_string_name(object rawValue)
    {
        if (rawValue == null)
        {
            return "";
        }
        if (rawValue is StringName stringName)
        {
            return string.IsNullOrEmpty(stringName.ToString()) ? "" : stringName;
        }
        if (rawValue is not Variant value)
        {
            string rawText = rawValue.ToString();
            return string.IsNullOrEmpty(rawText) || rawText == "<null>" ? "" : new StringName(rawText);
        }
        if (value.VariantType == Variant.Type.Nil)
        {
            return "";
        }

        string text =
            value.VariantType == Variant.Type.StringName
                ? value.AsStringName().ToString()
                : value.ToString();
        return string.IsNullOrEmpty(text) || text == "<null>" ? "" : new StringName(text);
    }

    internal static string string_name_to_string(StringName value)
    {
        return value.ToString();
    }

    internal static Godot.Collections.Array<StringName> to_string_name_array(object rawValues)
    {
        var result = new Godot.Collections.Array<StringName>();
        Godot.Collections.Array values = null;
        if (rawValues is Variant variantValues && variantValues.VariantType == Variant.Type.Array)
        {
            values = variantValues.AsGodotArray();
        }
        else if (rawValues is Godot.Collections.Array arrayValues)
        {
            values = arrayValues;
        }
        if (values == null)
        {
            return result;
        }

        foreach (var value in values)
        {
            StringName normalized = to_string_name(value);
            if (normalized != (StringName)"")
            {
                result.Add(normalized);
            }
        }
        return result;
    }

    internal static Godot.Collections.Array<string> string_name_array_to_string_array(
        IEnumerable<StringName> values
    )
    {
        var result = new Godot.Collections.Array<string>();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value.ToString());
        return result;
    }

    internal static GDictionary to_string_name_int_map(GDictionary values)
    {
        var result = new GDictionary();
        if (values == null)
        {
            return result;
        }

        foreach (var key in values.Keys)
        {
            result[to_string_name(key)] = values[key].AsInt32();
        }
        return result;
    }

    internal static Dictionary<StringName, int> to_string_name_int_dictionary(GDictionary values)
    {
        var result = new Dictionary<StringName, int>();
        if (values == null)
        {
            return result;
        }

        foreach (var key in values.Keys)
        {
            StringName normalizedKey = to_string_name(key);
            if (normalizedKey == "")
            {
                continue;
            }
            result[normalizedKey] = values[key].AsInt32();
        }
        return result;
    }

    internal static GDictionary string_name_int_map_to_string_dict(GDictionary values)
    {
        var result = new GDictionary();
        foreach (var key in values.Keys)
        {
            result[key.ToString()] = values[key].AsInt32();
        }
        return result;
    }

    internal static GDictionary string_name_int_map_to_string_dict(
        System.Collections.Generic.Dictionary<StringName, int> values
    )
    {
        var result = new GDictionary();
        foreach (var kvp in values)
            result[kvp.Key.ToString()] = kvp.Value;
        return result;
    }

    internal static GDictionary string_name_array_map_to_string_dict(GDictionary values)
    {
        var result = new GDictionary();
        foreach (var key in values.Keys)
        {
            if (values[key].VariantType == Variant.Type.Array)
            {
                result[key.ToString()] = StringNameArrayToStringArray(values[key].AsGodotArray());
            }
        }
        return result;
    }

    internal static List<string> sorted_string_keys(GDictionary values)
    {
        var sorted = new List<string>();
        if (values == null)
        {
            return sorted;
        }
        foreach (var key in values.Keys)
        {
            sorted.Add(key.ToString());
        }
        sorted.Sort(System.StringComparer.Ordinal);
        return sorted;
    }

    private static Godot.Collections.Array<string> StringNameArrayToStringArray(
        Godot.Collections.Array values
    )
    {
        var result = new Godot.Collections.Array<string>();
        foreach (var entry in values)
        {
            StringName normalized = to_string_name(entry);
            if (normalized != (StringName)"")
            {
                result.Add(normalized.ToString());
            }
        }
        return result;
    }

    /// <summary>
    /// 检查 value 是否落在 [minValue, maxValue] 区间内。
    /// maxValue == 0 表示无上限；maxValue &lt; 0 按无上限处理（配置错误场景）。
    /// </summary>
    internal static bool MatchesValueRange(int value, int minValue, int maxValue)
    {
        if (value < minValue)
            return false;
        if (maxValue < 0)
            return true;
        return maxValue == 0 || value <= maxValue;
    }
}
