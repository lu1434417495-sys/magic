using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class GodotTypedProjection
{
    internal static GDictionary ProjectDictionary(IReadOnlyDictionary<string, object> source)
    {
        return ProjectDictionary(source, null);
    }

    internal static GDictionary ProjectDictionary(
        IReadOnlyDictionary<string, object> source,
        GodotProjectionPayloadOwner payloads
    )
    {
        GDictionary projection = payloads?.Dictionary() ?? new GDictionary();
        if (source == null)
            return projection;
        foreach ((string key, object value) in source)
        {
            Variant projectedValue = ProjectValue(value, payloads);
            try
            {
                projection[key] = projectedValue;
            }
            finally
            {
                if (value is not Variant)
                    projectedValue.Dispose();
            }
        }
        return projection;
    }

    internal static GArray ProjectArray(IReadOnlyList<object> source)
    {
        return ProjectArray(source, null);
    }

    internal static GArray ProjectArray(
        IReadOnlyList<object> source,
        GodotProjectionPayloadOwner payloads
    )
    {
        GArray projection = payloads?.Array() ?? new GArray();
        if (source == null)
            return projection;
        foreach (object value in source)
        {
            Variant projectedValue = ProjectValue(value, payloads);
            try
            {
                projection.Add(projectedValue);
            }
            finally
            {
                if (value is not Variant)
                    projectedValue.Dispose();
            }
        }
        return projection;
    }

    internal static Variant ProjectValue(object value, GodotProjectionPayloadOwner payloads = null)
    {
        if (value == null)
            return default;
        if (value is IReadOnlyDictionary<string, object> dictionaryValue)
            return ProjectDictionary(dictionaryValue, payloads);
        if (value is IReadOnlyList<object> listValue)
            return ProjectArray(listValue, payloads);
        return value switch
        {
            Variant variantValue => variantValue,
            bool boolValue => boolValue,
            int intValue => intValue,
            long longValue => longValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            string stringValue => stringValue,
            StringName stringNameValue => stringNameValue,
            Vector2I vectorValue => vectorValue,
            GodotObject godotObjectValue => godotObjectValue,
            _ => value.ToString() ?? "",
        };
    }
}
