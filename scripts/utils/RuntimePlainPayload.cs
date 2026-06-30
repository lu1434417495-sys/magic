using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class RuntimePlainPayload
{
    internal static Dictionary<string, object> NormalizeDictionary(
        GDictionary source,
        string ownerPath
    )
    {
        var result = new Dictionary<string, object>(System.StringComparer.Ordinal);
        if (source == null)
            return result;

        foreach (Variant rawKey in source.Keys)
        {
            string key = rawKey.VariantType switch
            {
                Variant.Type.String => rawKey.AsString(),
                Variant.Type.StringName => rawKey.AsStringName().ToString(),
                _ => "",
            };
            if (string.IsNullOrEmpty(key))
                continue;

            string childPath = string.IsNullOrEmpty(ownerPath) ? key : $"{ownerPath}.{key}";
            result[key] = NormalizeValue(source[rawKey], childPath);
        }
        return result;
    }

    internal static Dictionary<string, object> CloneDictionary(
        IReadOnlyDictionary<string, object> source
    )
    {
        var result = new Dictionary<string, object>(System.StringComparer.Ordinal);
        if (source == null)
            return result;

        foreach (KeyValuePair<string, object> entry in source)
        {
            if (!string.IsNullOrEmpty(entry.Key))
                result[entry.Key] = CloneValue(entry.Value);
        }
        return result;
    }

    internal static object CloneValue(object value)
    {
        return value switch
        {
            null => null,
            Dictionary<string, object> dictionaryValue => CloneDictionary(dictionaryValue),
            IReadOnlyDictionary<string, object> dictionaryValue => CloneDictionary(dictionaryValue),
            List<object> listValue => CloneList(listValue),
            IReadOnlyList<object> listValue => CloneList(listValue),
            _ => value,
        };
    }

    private static List<object> CloneList(IReadOnlyList<object> source)
    {
        var result = new List<object>();
        if (source == null)
            return result;

        for (int index = 0; index < source.Count; index++)
            result.Add(CloneValue(source[index]));
        return result;
    }

    internal static GDictionary ProjectDictionary(
        IReadOnlyDictionary<string, object> source,
        string reason
    )
    {
        var result = new GDictionary();
        if (source == null)
            return result;

        foreach (KeyValuePair<string, object> entry in source)
        {
            if (!string.IsNullOrEmpty(entry.Key))
                result[entry.Key] = ProjectValue(entry.Value, $"{reason}.{entry.Key}");
        }
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, reason);
        return result;
    }

    internal static List<Dictionary<string, object>> NormalizeDictionaryArray(
        GArray source,
        string ownerPath
    )
    {
        var result = new List<Dictionary<string, object>>();
        if (source == null)
            return result;

        int index = 0;
        foreach (object rawValue in source)
        {
            if (TryAsDictionary(rawValue, out GDictionary dictionaryValue))
            {
                result.Add(NormalizeDictionary(dictionaryValue, $"{ownerPath}[{index}]"));
            }
            index++;
        }
        return result;
    }

    internal static GArray ProjectDictionaryArray(
        IEnumerable<IReadOnlyDictionary<string, object>> source,
        string reason
    )
    {
        var result = new GArray();
        if (source != null)
        {
            int index = 0;
            foreach (IReadOnlyDictionary<string, object> entry in source)
            {
                result.Add(ProjectDictionary(entry, $"{reason}[{index}]"));
                index++;
            }
        }
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, reason);
        return result;
    }

    internal static List<object> NormalizeArray(GArray source, string ownerPath)
    {
        var result = new List<object>();
        if (source == null)
            return result;

        int index = 0;
        foreach (object rawValue in source)
        {
            result.Add(NormalizeValue(rawValue, $"{ownerPath}[{index}]"));
            index++;
        }
        return result;
    }

    internal static object NormalizeValue(object rawValue, string path)
    {
        if (rawValue is Variant variantValue)
            return NormalizeVariant(variantValue, path);
        if (rawValue is GDictionary dictionaryValue)
            return NormalizeDictionary(dictionaryValue, path);
        if (rawValue is GArray arrayValue)
            return NormalizeArray(arrayValue, path);
        if (rawValue is GodotObject godotObject)
        {
            throw new System.InvalidOperationException(
                $"Plain runtime payload does not accept Godot Object at {path}. type={godotObject.GetType().Name}"
            );
        }
        return rawValue;
    }

    private static bool TryAsDictionary(object value, out GDictionary dictionary)
    {
        if (value is GDictionary dictionaryValue)
        {
            dictionary = dictionaryValue;
            return true;
        }
        if (value is Variant variantValue && variantValue.VariantType == Variant.Type.Dictionary)
        {
            dictionary = variantValue.AsGodotDictionary();
            return true;
        }
        dictionary = null;
        return false;
    }

    internal static object NormalizeVariant(Variant value, string path)
    {
        return value.VariantType switch
        {
            Variant.Type.Nil => null,
            Variant.Type.Bool => value.AsBool(),
            Variant.Type.Int => value.AsInt64(),
            Variant.Type.Float => value.AsDouble(),
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.Vector2I => value.AsVector2I(),
            Variant.Type.Vector2 => value.AsVector2(),
            Variant.Type.Vector3I => value.AsVector3I(),
            Variant.Type.Vector3 => value.AsVector3(),
            Variant.Type.Dictionary => NormalizeDictionary(value.AsGodotDictionary(), path),
            Variant.Type.Array => NormalizeArray(value.AsGodotArray(), path),
            Variant.Type.Object => throw new System.InvalidOperationException(
                $"Plain runtime payload does not accept Godot Object at {path}."
            ),
            _ => value.ToString() ?? "",
        };
    }

    internal static GArray ProjectArray(IReadOnlyList<object> source, string reason)
    {
        var result = new GArray();
        if (source == null)
            return result;

        for (int index = 0; index < source.Count; index++)
            result.Add(ProjectValue(source[index], $"{reason}[{index}]"));
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, reason);
        return result;
    }

    internal static Variant ProjectValue(object value, string reason)
    {
        return value switch
        {
            null => default,
            Variant variant => variant,
            bool boolValue => boolValue,
            int intValue => intValue,
            long longValue => longValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            string stringValue => stringValue,
            StringName stringNameValue => stringNameValue,
            Vector2I vector2IValue => vector2IValue,
            Vector2 vector2Value => vector2Value,
            Vector3I vector3IValue => vector3IValue,
            Vector3 vector3Value => vector3Value,
            IReadOnlyDictionary<string, object> dictionaryValue =>
                ProjectDictionary(dictionaryValue, reason),
            IReadOnlyList<object> listValue => ProjectArray(listValue, reason),
            _ => value.ToString() ?? "",
        };
    }
}
