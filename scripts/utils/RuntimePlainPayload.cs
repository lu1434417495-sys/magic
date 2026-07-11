using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class RuntimePlainPayload
{
    internal static GodotProjectionLease<GDictionary> ProjectDictionaryLease(
        IReadOnlyDictionary<string, object> source,
        string ownerId,
        LifetimeDomain domain,
        string reason,
        bool minimizeStrings = false
    )
    {
        GDictionary root = new();
        GodotProjectionLease<GDictionary> lease =
            GodotProjectionLease<GDictionary>.CreateOwnedRoot(
                root,
                ownerId,
                domain,
                reason
            );
        try
        {
            PopulateOwnedDictionary(lease, root, source, reason, minimizeStrings);
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal static GodotProjectionLease<GArray> ProjectArrayLease(
        IReadOnlyList<object> source,
        string ownerId,
        LifetimeDomain domain,
        string reason,
        bool minimizeStrings = false
    )
    {
        GArray root = new();
        GodotProjectionLease<GArray> lease =
            GodotProjectionLease<GArray>.CreateOwnedRoot(
                root,
                ownerId,
                domain,
                reason
            );
        try
        {
            if (source != null)
            {
                for (int index = 0; index < source.Count; index++)
                {
                    root.Add(
                        ProjectOwnedValue(
                            lease,
                            source[index],
                            $"{reason}[{index}]",
                            minimizeStrings
                        )
                    );
                }
            }
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal static Dictionary<string, object> RestoreSaveDictionary(
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
                _ => throw new System.InvalidOperationException(
                    $"Save payload dictionary key at {ownerPath} must be String or StringName, got {rawKey.VariantType}."
                ),
            };
            if (string.IsNullOrEmpty(key))
            {
                throw new System.InvalidOperationException(
                    $"Save payload dictionary key at {ownerPath} must not be empty."
                );
            }
            if (result.ContainsKey(key))
            {
                throw new System.InvalidOperationException(
                    $"Save payload dictionary at {ownerPath} contains duplicate normalized key '{key}'."
                );
            }

            string childPath = string.IsNullOrEmpty(ownerPath) ? key : $"{ownerPath}.{key}";
            result[key] = RestoreSaveValue(source[rawKey], childPath);
        }
        return result;
    }

    internal static bool TryRestoreSaveVariantDictionary(
        Variant value,
        string ownerPath,
        out Dictionary<string, object> result
    )
    {
        if (value.VariantType != Variant.Type.Dictionary)
        {
            result = new Dictionary<string, object>(System.StringComparer.Ordinal);
            return false;
        }

        using GDictionary dictionary = value.AsGodotDictionary();
        try
        {
            result = RestoreSaveDictionary(dictionary, ownerPath);
            return true;
        }
        catch (System.InvalidOperationException)
        {
            result = new Dictionary<string, object>(System.StringComparer.Ordinal);
            return false;
        }
    }

    internal static object RestoreSaveVariantToPlain(Variant value, string ownerPath) =>
        RestoreSaveVariant(value, ownerPath);

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
            if (string.IsNullOrEmpty(entry.Key))
            {
                throw new System.InvalidOperationException(
                    "Plain runtime payload clone does not accept an empty dictionary key."
                );
            }
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
            bool or byte or short or int or long or float or double or string or StringName
                or Vector2 or Vector2I or Rect2 or Rect2I or Vector3 or Vector3I
                or Transform2D or Vector4 or Vector4I or Plane or Quaternion or Aabb
                or Basis or Transform3D or Projection or Color => value,
            Variant => throw UnsupportedCloneValue(value),
            GodotObject => throw UnsupportedCloneValue(value),
            System.IDisposable => throw UnsupportedCloneValue(value),
            System.Collections.IDictionary dictionaryValue =>
                CloneStringKeyedDictionary(dictionaryValue),
            System.Collections.IEnumerable enumerableValue => CloneEnumerable(enumerableValue),
            _ => throw UnsupportedCloneValue(value),
        };
    }

    private static Dictionary<string, object> CloneStringKeyedDictionary(
        System.Collections.IDictionary source
    )
    {
        var result = new Dictionary<string, object>(System.StringComparer.Ordinal);
        if (source == null)
            return result;

        foreach (System.Collections.DictionaryEntry entry in source)
        {
            string key = entry.Key switch
            {
                string stringKey => stringKey,
                StringName stringNameKey => stringNameKey.ToString(),
                _ => throw UnsupportedCloneValue(source),
            };
            if (string.IsNullOrEmpty(key))
            {
                throw new System.InvalidOperationException(
                    "Plain runtime payload clone does not accept an empty dictionary key."
                );
            }
            if (!result.TryAdd(key, CloneValue(entry.Value)))
            {
                throw new System.InvalidOperationException(
                    $"Plain runtime payload clone contains duplicate normalized key '{key}'."
                );
            }
        }
        return result;
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

    private static List<object> CloneEnumerable(System.Collections.IEnumerable source)
    {
        var result = new List<object>();
        if (source == null)
            return result;

        foreach (object value in source)
            result.Add(CloneValue(value));
        return result;
    }

    private static System.InvalidOperationException UnsupportedCloneValue(object value) =>
        new(
            $"Plain runtime payload clone does not support value type {value?.GetType().FullName ?? "<null>"}."
        );

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

    internal static Dictionary<string, object> NormalizeDictionaryStrict(
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
                _ => throw new System.InvalidOperationException(
                    $"Plain runtime payload dictionary key at {ownerPath} must be String or StringName, got {rawKey.VariantType}."
                ),
            };
            if (string.IsNullOrEmpty(key))
            {
                throw new System.InvalidOperationException(
                    $"Plain runtime payload dictionary key at {ownerPath} must not be empty."
                );
            }
            if (result.ContainsKey(key))
            {
                throw new System.InvalidOperationException(
                    $"Plain runtime payload dictionary at {ownerPath} contains duplicate normalized key '{key}'."
                );
            }

            string childPath = string.IsNullOrEmpty(ownerPath) ? key : $"{ownerPath}.{key}";
            result[key] = NormalizeRuntimeValueStrict(source[rawKey], childPath);
        }
        return result;
    }

    internal static List<object> NormalizeArrayStrict(GArray source, string ownerPath)
    {
        var result = new List<object>();
        if (source == null)
            return result;

        int index = 0;
        foreach (Variant rawValue in source)
        {
            result.Add(NormalizeRuntimeVariantStrict(rawValue, $"{ownerPath}[{index}]"));
            index++;
        }
        return result;
    }

    internal static object NormalizeRuntimeValueStrict(object rawValue, string path)
    {
        if (rawValue is Variant variantValue)
            return NormalizeRuntimeVariantStrict(variantValue, path);
        if (rawValue is GDictionary dictionaryValue)
            return NormalizeDictionaryStrict(dictionaryValue, path);
        if (rawValue is GArray arrayValue)
            return NormalizeArrayStrict(arrayValue, path);
        if (rawValue is GodotObject godotObject)
        {
            throw new System.InvalidOperationException(
                $"Plain runtime payload does not accept Godot Object at {path}. type={godotObject.GetType().Name}"
            );
        }
        return CloneValue(rawValue);
    }

    private static object NormalizeRuntimeVariantStrict(Variant value, string path)
    {
        switch (value.VariantType)
        {
            case Variant.Type.Nil:
                return null;
            case Variant.Type.Bool:
                return value.AsBool();
            case Variant.Type.Int:
                return value.AsInt64();
            case Variant.Type.Float:
                return value.AsDouble();
            case Variant.Type.String:
                return value.AsString();
            case Variant.Type.StringName:
                return value.AsStringName();
            case Variant.Type.Vector2I:
                return value.AsVector2I();
            case Variant.Type.Vector2:
                return value.AsVector2();
            case Variant.Type.Vector3I:
                return value.AsVector3I();
            case Variant.Type.Vector3:
                return value.AsVector3();
            case Variant.Type.Rect2:
                return value.AsRect2();
            case Variant.Type.Rect2I:
                return value.AsRect2I();
            case Variant.Type.Transform2D:
                return value.AsTransform2D();
            case Variant.Type.Vector4:
                return value.AsVector4();
            case Variant.Type.Vector4I:
                return value.AsVector4I();
            case Variant.Type.Plane:
                return value.AsPlane();
            case Variant.Type.Quaternion:
                return value.AsQuaternion();
            case Variant.Type.Aabb:
                return value.AsAabb();
            case Variant.Type.Basis:
                return value.AsBasis();
            case Variant.Type.Transform3D:
                return value.AsTransform3D();
            case Variant.Type.Projection:
                return value.AsProjection();
            case Variant.Type.Color:
                return value.AsColor();
            case Variant.Type.Dictionary:
            {
                using GDictionary dictionary = value.AsGodotDictionary();
                return NormalizeDictionaryStrict(dictionary, path);
            }
            case Variant.Type.Array:
            {
                using GArray array = value.AsGodotArray();
                return NormalizeArrayStrict(array, path);
            }
            case Variant.Type.PackedStringArray:
            {
                var result = new List<object>();
                foreach (string entry in value.AsStringArray())
                    result.Add(entry ?? "");
                return result;
            }
            case Variant.Type.Object:
                throw new System.InvalidOperationException(
                    $"Plain runtime payload does not accept Godot Object at {path}."
                );
            default:
                throw new System.InvalidOperationException(
                    $"Plain runtime payload does not support Variant type {value.VariantType} at {path}."
                );
        }
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
            Variant.Type.Rect2 => value.AsRect2(),
            Variant.Type.Rect2I => value.AsRect2I(),
            Variant.Type.Vector3I => value.AsVector3I(),
            Variant.Type.Vector3 => value.AsVector3(),
            Variant.Type.Transform2D => value.AsTransform2D(),
            Variant.Type.Vector4 => value.AsVector4(),
            Variant.Type.Vector4I => value.AsVector4I(),
            Variant.Type.Plane => value.AsPlane(),
            Variant.Type.Quaternion => value.AsQuaternion(),
            Variant.Type.Aabb => value.AsAabb(),
            Variant.Type.Basis => value.AsBasis(),
            Variant.Type.Transform3D => value.AsTransform3D(),
            Variant.Type.Projection => value.AsProjection(),
            Variant.Type.Color => value.AsColor(),
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
            Rect2 rect2Value => rect2Value,
            Rect2I rect2IValue => rect2IValue,
            Vector3I vector3IValue => vector3IValue,
            Vector3 vector3Value => vector3Value,
            Transform2D transform2DValue => transform2DValue,
            Vector4 vector4Value => vector4Value,
            Vector4I vector4IValue => vector4IValue,
            Plane planeValue => planeValue,
            Quaternion quaternionValue => quaternionValue,
            Aabb aabbValue => aabbValue,
            Basis basisValue => basisValue,
            Transform3D transform3DValue => transform3DValue,
            Projection projectionValue => projectionValue,
            Color colorValue => colorValue,
            IReadOnlyDictionary<string, object> dictionaryValue =>
                ProjectDictionary(dictionaryValue, reason),
            IReadOnlyList<object> listValue => ProjectArray(listValue, reason),
            _ => value.ToString() ?? "",
        };
    }

    private static void PopulateOwnedDictionary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary target,
        IReadOnlyDictionary<string, object> source,
        string reason,
        bool minimizeStrings
    )
        where TLeaseRoot : class, System.IDisposable
    {
        if (source == null)
            return;

        foreach (KeyValuePair<string, object> entry in source)
        {
            if (string.IsNullOrEmpty(entry.Key))
            {
                throw new System.InvalidOperationException(
                    $"Plain projection does not accept an empty dictionary key at {reason}."
                );
            }
            Variant key = minimizeStrings
                ? Variant.From(new StringName(entry.Key))
                : Variant.From(entry.Key);
            target[key] = ProjectOwnedValue(
                lease,
                entry.Value,
                $"{reason}.{entry.Key}",
                minimizeStrings
            );
        }
    }

    private static Variant ProjectOwnedValue<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        object value,
        string reason,
        bool minimizeStrings
    )
        where TLeaseRoot : class, System.IDisposable
    {
        switch (value)
        {
            case null:
                return default;
            case Variant variant:
                return ProjectOwnedVariant(lease, variant, reason, minimizeStrings);
            case bool boolValue:
                return boolValue;
            case byte byteValue:
                return (long)byteValue;
            case short shortValue:
                return (long)shortValue;
            case int intValue:
                return intValue;
            case long longValue:
                return longValue;
            case float floatValue:
                return floatValue;
            case double doubleValue:
                return doubleValue;
            case string stringValue:
                return minimizeStrings
                    ? Variant.From(new StringName(stringValue))
                    : Variant.From(stringValue);
            case StringName stringNameValue:
                return minimizeStrings
                    ? Variant.From(stringNameValue)
                    : Variant.From(stringNameValue);
            case Vector2I vector2IValue:
                return vector2IValue;
            case Vector2 vector2Value:
                return vector2Value;
            case Rect2 rect2Value:
                return rect2Value;
            case Rect2I rect2IValue:
                return rect2IValue;
            case Vector3I vector3IValue:
                return vector3IValue;
            case Vector3 vector3Value:
                return vector3Value;
            case Transform2D transform2DValue:
                return transform2DValue;
            case Vector4 vector4Value:
                return vector4Value;
            case Vector4I vector4IValue:
                return vector4IValue;
            case Plane planeValue:
                return planeValue;
            case Quaternion quaternionValue:
                return quaternionValue;
            case Aabb aabbValue:
                return aabbValue;
            case Basis basisValue:
                return basisValue;
            case Transform3D transform3DValue:
                return transform3DValue;
            case Projection projectionValue:
                return projectionValue;
            case Color colorValue:
                return colorValue;
            case IReadOnlyDictionary<string, object> dictionaryValue:
            {
                GDictionary dictionary = lease.Own(new GDictionary(), reason);
                PopulateOwnedDictionary(
                    lease,
                    dictionary,
                    dictionaryValue,
                    reason,
                    minimizeStrings
                );
                return Variant.From(dictionary);
            }
            case IReadOnlyList<object> listValue:
            {
                GArray array = lease.Own(new GArray(), reason);
                for (int index = 0; index < listValue.Count; index++)
                {
                    array.Add(
                        ProjectOwnedValue(
                            lease,
                            listValue[index],
                            $"{reason}[{index}]",
                            minimizeStrings
                        )
                    );
                }
                return Variant.From(array);
            }
            default:
                throw new System.InvalidOperationException(
                    $"Plain projection does not support value type {value.GetType().FullName} at {reason}."
                );
        }
    }

    private static Variant ProjectOwnedVariant<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        Variant value,
        string reason,
        bool minimizeStrings
    )
        where TLeaseRoot : class, System.IDisposable
    {
        object plain = RestoreSaveVariant(value, reason);
        return ProjectOwnedValue(lease, plain, reason, minimizeStrings);
    }

    private static object RestoreSaveValue(object rawValue, string path)
    {
        if (rawValue is Variant variantValue)
            return RestoreSaveVariant(variantValue, path);
        if (rawValue is GDictionary dictionaryValue)
            return RestoreSaveDictionary(dictionaryValue, path);
        if (rawValue is GArray arrayValue)
            return RestoreSaveArray(arrayValue, path);
        if (rawValue is GodotObject godotObject)
        {
            throw new System.InvalidOperationException(
                $"Save payload does not accept Godot Object at {path}. type={godotObject.GetType().Name}"
            );
        }
        return rawValue is StringName stringName ? stringName.ToString() : rawValue;
    }

    private static object RestoreSaveVariant(Variant value, string path)
    {
        switch (value.VariantType)
        {
            case Variant.Type.Nil:
                return null;
            case Variant.Type.Bool:
                return value.AsBool();
            case Variant.Type.Int:
                return value.AsInt64();
            case Variant.Type.Float:
                return value.AsDouble();
            case Variant.Type.String:
                return value.AsString();
            case Variant.Type.StringName:
                return value.AsStringName().ToString();
            case Variant.Type.Vector2I:
                return value.AsVector2I();
            case Variant.Type.Vector2:
                return value.AsVector2();
            case Variant.Type.Vector3I:
                return value.AsVector3I();
            case Variant.Type.Vector3:
                return value.AsVector3();
            case Variant.Type.Rect2:
                return value.AsRect2();
            case Variant.Type.Rect2I:
                return value.AsRect2I();
            case Variant.Type.Transform2D:
                return value.AsTransform2D();
            case Variant.Type.Vector4:
                return value.AsVector4();
            case Variant.Type.Vector4I:
                return value.AsVector4I();
            case Variant.Type.Plane:
                return value.AsPlane();
            case Variant.Type.Quaternion:
                return value.AsQuaternion();
            case Variant.Type.Aabb:
                return value.AsAabb();
            case Variant.Type.Basis:
                return value.AsBasis();
            case Variant.Type.Transform3D:
                return value.AsTransform3D();
            case Variant.Type.Projection:
                return value.AsProjection();
            case Variant.Type.Color:
                return value.AsColor();
            case Variant.Type.Dictionary:
            {
                using GDictionary dictionary = value.AsGodotDictionary();
                return RestoreSaveDictionary(dictionary, path);
            }
            case Variant.Type.Array:
            {
                using GArray array = value.AsGodotArray();
                return RestoreSaveArray(array, path);
            }
            case Variant.Type.Object:
                throw new System.InvalidOperationException(
                    $"Save payload does not accept Godot Object at {path}."
                );
            default:
                throw new System.InvalidOperationException(
                    $"Save payload does not support Variant type {value.VariantType} at {path}."
                );
        }
    }

    private static List<object> RestoreSaveArray(GArray source, string ownerPath)
    {
        var result = new List<object>();
        if (source == null)
            return result;
        int index = 0;
        foreach (Variant rawValue in source)
        {
            result.Add(RestoreSaveVariant(rawValue, $"{ownerPath}[{index}]"));
            index++;
        }
        return result;
    }
}
