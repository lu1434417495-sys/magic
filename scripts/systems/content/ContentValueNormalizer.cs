using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class ContentValueNormalizer
{
    private static readonly IReadOnlyDictionary<string, object> EmptyDictionary =
        new ReadOnlyDictionary<string, object>(
            new Dictionary<string, object>(StringComparer.Ordinal)
        );
    private static readonly IReadOnlyList<object> EmptyList = Array.Empty<object>();

    internal static object NormalizeVariant(Variant value, string path) =>
        NormalizeVariant(value, NormalizePath(path), new HashSet<object>(ReferenceEqualityComparer.Instance));

    internal static IReadOnlyDictionary<string, object> NormalizeDictionary(
        GDictionary source,
        string path
    ) => NormalizeGodotDictionary(source, NormalizePath(path), new HashSet<object>(ReferenceEqualityComparer.Instance));

    internal static IReadOnlyList<object> NormalizeArray(GArray source, string path) =>
        NormalizeGodotArray(source, NormalizePath(path), new HashSet<object>(ReferenceEqualityComparer.Instance));

    internal static object NormalizeValue(object value, string path) =>
        NormalizeManagedValue(value, NormalizePath(path), new HashSet<object>(ReferenceEqualityComparer.Instance));

    internal static IReadOnlyDictionary<string, object> NormalizeDictionary(
        IReadOnlyDictionary<string, object> source,
        string path
    ) => NormalizeManagedDictionary(source, NormalizePath(path), new HashSet<object>(ReferenceEqualityComparer.Instance));

    internal static IReadOnlyList<object> NormalizeArray(
        IReadOnlyList<object> source,
        string path
    ) => NormalizeManagedList(source, NormalizePath(path), new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static object NormalizeVariant(
        Variant value,
        string path,
        HashSet<object> activeContainers
    )
    {
        EnsureDepth(path);
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
            case Variant.Type.Vector2:
                return value.AsVector2();
            case Variant.Type.Vector2I:
                return value.AsVector2I();
            case Variant.Type.Rect2:
                return value.AsRect2();
            case Variant.Type.Rect2I:
                return value.AsRect2I();
            case Variant.Type.Vector3:
                return value.AsVector3();
            case Variant.Type.Vector3I:
                return value.AsVector3I();
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
                return NormalizeGodotDictionary(dictionary, path, activeContainers);
            }
            case Variant.Type.Array:
            {
                using GArray array = value.AsGodotArray();
                return NormalizeGodotArray(array, path, activeContainers);
            }
            case Variant.Type.Object:
                throw UnsupportedVariant(path, value.VariantType);
            default:
                throw UnsupportedVariant(path, value.VariantType);
        }
    }

    private static IReadOnlyDictionary<string, object> NormalizeGodotDictionary(
        GDictionary source,
        string path,
        HashSet<object> activeContainers
    )
    {
        if (source == null || source.Count == 0)
            return EmptyDictionary;
        EnterContainer(source, path, activeContainers);
        try
        {
            var result = new Dictionary<string, object>(source.Count, StringComparer.Ordinal);
            int keyIndex = 0;
            foreach (Variant rawKey in source.Keys)
            {
                string key = rawKey.VariantType switch
                {
                    Variant.Type.String => rawKey.AsString(),
                    Variant.Type.StringName => rawKey.AsStringName().ToString(),
                    _ => throw new InvalidDataException(
                        $"Content dictionary at '{path}' requires String or StringName keys; key[{keyIndex}] has {rawKey.VariantType}."
                    ),
                };
                ValidateDictionaryKey(result, key, path, keyIndex);
                result.Add(
                    key,
                    NormalizeVariant(source[rawKey], AppendKey(path, key), activeContainers)
                );
                keyIndex++;
            }
            return FreezeDictionary(result);
        }
        finally
        {
            activeContainers.Remove(source);
        }
    }

    private static IReadOnlyList<object> NormalizeGodotArray(
        GArray source,
        string path,
        HashSet<object> activeContainers
    )
    {
        if (source == null || source.Count == 0)
            return EmptyList;
        EnterContainer(source, path, activeContainers);
        try
        {
            var result = new List<object>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                result.Add(
                    NormalizeVariant(source[index], AppendIndex(path, index), activeContainers)
                );
            }
            return new ReadOnlyCollection<object>(result);
        }
        finally
        {
            activeContainers.Remove(source);
        }
    }

    private static object NormalizeManagedValue(
        object value,
        string path,
        HashSet<object> activeContainers
    )
    {
        EnsureDepth(path);
        switch (value)
        {
            case null:
                return null;
            case Variant variant:
                return NormalizeVariant(variant, path, activeContainers);
            case bool:
            case string:
            case StringName:
            case Vector2:
            case Vector2I:
            case Rect2:
            case Rect2I:
            case Vector3:
            case Vector3I:
            case Transform2D:
            case Vector4:
            case Vector4I:
            case Plane:
            case Quaternion:
            case Aabb:
            case Basis:
            case Transform3D:
            case Projection:
            case Color:
                return value;
            case sbyte signedByte:
                return (long)signedByte;
            case byte unsignedByte:
                return (long)unsignedByte;
            case short signedShort:
                return (long)signedShort;
            case ushort unsignedShort:
                return (long)unsignedShort;
            case int signedInt:
                return (long)signedInt;
            case uint unsignedInt:
                return (long)unsignedInt;
            case long:
                return value;
            case ulong unsignedLong when unsignedLong <= long.MaxValue:
                return (long)unsignedLong;
            case ulong:
                throw new InvalidDataException(
                    $"Content value at '{path}' exceeds the supported signed integral range."
                );
            case float floatValue:
                return (double)floatValue;
            case double:
                return value;
            case Enum enumValue:
                return Convert.ToInt64(enumValue);
            case GDictionary dictionary:
                return NormalizeGodotDictionary(dictionary, path, activeContainers);
            case GArray array:
                return NormalizeGodotArray(array, path, activeContainers);
            case IReadOnlyDictionary<string, object> dictionary:
                return NormalizeManagedDictionary(dictionary, path, activeContainers);
            case IDictionary dictionary:
                return NormalizeManagedDictionary(dictionary, path, activeContainers);
            case IReadOnlyList<object> list:
                return NormalizeManagedList(list, path, activeContainers);
            case IList list:
                return NormalizeManagedList(list, path, activeContainers);
            default:
                throw new InvalidDataException(
                    $"Content value at '{path}' has unsupported CLR type '{value.GetType().FullName}'."
                );
        }
    }

    private static IReadOnlyDictionary<string, object> NormalizeManagedDictionary(
        IReadOnlyDictionary<string, object> source,
        string path,
        HashSet<object> activeContainers
    )
    {
        if (source == null || source.Count == 0)
            return EmptyDictionary;
        EnterContainer(source, path, activeContainers);
        try
        {
            var result = new Dictionary<string, object>(source.Count, StringComparer.Ordinal);
            int keyIndex = 0;
            foreach (KeyValuePair<string, object> entry in source)
            {
                ValidateDictionaryKey(result, entry.Key, path, keyIndex);
                result.Add(
                    entry.Key,
                    NormalizeManagedValue(
                        entry.Value,
                        AppendKey(path, entry.Key),
                        activeContainers
                    )
                );
                keyIndex++;
            }
            return FreezeDictionary(result);
        }
        finally
        {
            activeContainers.Remove(source);
        }
    }

    private static IReadOnlyDictionary<string, object> NormalizeManagedDictionary(
        IDictionary source,
        string path,
        HashSet<object> activeContainers
    )
    {
        if (source == null || source.Count == 0)
            return EmptyDictionary;
        EnterContainer(source, path, activeContainers);
        try
        {
            var result = new Dictionary<string, object>(source.Count, StringComparer.Ordinal);
            int keyIndex = 0;
            foreach (DictionaryEntry entry in source)
            {
                if (entry.Key is not string key)
                {
                    throw new InvalidDataException(
                        $"Content dictionary at '{path}' requires string keys; key[{keyIndex}] has CLR type '{entry.Key?.GetType().FullName ?? "<null>"}'."
                    );
                }
                ValidateDictionaryKey(result, key, path, keyIndex);
                result.Add(
                    key,
                    NormalizeManagedValue(entry.Value, AppendKey(path, key), activeContainers)
                );
                keyIndex++;
            }
            return FreezeDictionary(result);
        }
        finally
        {
            activeContainers.Remove(source);
        }
    }

    private static IReadOnlyList<object> NormalizeManagedList(
        IReadOnlyList<object> source,
        string path,
        HashSet<object> activeContainers
    )
    {
        if (source == null || source.Count == 0)
            return EmptyList;
        EnterContainer(source, path, activeContainers);
        try
        {
            var result = new List<object>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                result.Add(
                    NormalizeManagedValue(source[index], AppendIndex(path, index), activeContainers)
                );
            }
            return new ReadOnlyCollection<object>(result);
        }
        finally
        {
            activeContainers.Remove(source);
        }
    }

    private static IReadOnlyList<object> NormalizeManagedList(
        IList source,
        string path,
        HashSet<object> activeContainers
    )
    {
        if (source == null || source.Count == 0)
            return EmptyList;
        EnterContainer(source, path, activeContainers);
        try
        {
            var result = new List<object>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                result.Add(
                    NormalizeManagedValue(source[index], AppendIndex(path, index), activeContainers)
                );
            }
            return new ReadOnlyCollection<object>(result);
        }
        finally
        {
            activeContainers.Remove(source);
        }
    }

    private static IReadOnlyDictionary<string, object> FreezeDictionary(
        Dictionary<string, object> values
    ) => values.Count == 0 ? EmptyDictionary : new ReadOnlyDictionary<string, object>(values);

    private static void ValidateDictionaryKey(
        Dictionary<string, object> result,
        string key,
        string path,
        int keyIndex
    )
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidDataException(
                $"Content dictionary at '{path}' contains an empty key at key[{keyIndex}]."
            );
        }
        if (result.ContainsKey(key))
        {
            throw new InvalidDataException(
                $"Content dictionary at '{path}' contains duplicate normalized key '{key}' at key[{keyIndex}]."
            );
        }
    }

    private static void EnterContainer(
        object container,
        string path,
        HashSet<object> activeContainers
    )
    {
        if (!activeContainers.Add(container))
        {
            throw new InvalidDataException(
                $"Content value graph contains a cycle at '{path}'."
            );
        }
    }

    private static InvalidDataException UnsupportedVariant(string path, Variant.Type type) =>
        new($"Content value at '{path}' has unsupported Variant type {type}.");

    private static void EnsureDepth(string path)
    {
        int depth = 0;
        foreach (char character in path)
        {
            if (character == '.' || character == '[')
                depth++;
        }
        if (depth > 128)
        {
            throw new InvalidDataException(
                $"Content value graph exceeds the maximum nesting depth at '{path}'."
            );
        }
    }

    private static string NormalizePath(string path) =>
        string.IsNullOrWhiteSpace(path) ? "$" : path;

    private static string AppendKey(string path, string key) => $"{path}.{key}";

    private static string AppendIndex(string path, int index) => $"{path}[{index}]";
}
