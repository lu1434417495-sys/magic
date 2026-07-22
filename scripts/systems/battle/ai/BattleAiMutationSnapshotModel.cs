using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

internal enum StableDiffKind
{
    Removed,
    Added,
    Changed,
    SizeChanged,
}

internal readonly struct StableDiff
{
    private StableDiff(
        StableDiffKind kind,
        string path,
        StableValue expected = null,
        StableValue actual = null,
        int expectedCount = 0,
        int actualCount = 0
    )
    {
        Kind = kind;
        Path = path ?? "";
        Expected = expected;
        Actual = actual;
        ExpectedCount = expectedCount;
        ActualCount = actualCount;
    }

    public StableDiffKind Kind { get; }

    public string Path { get; }

    public StableValue Expected { get; }

    public StableValue Actual { get; }

    public int ExpectedCount { get; }

    public int ActualCount { get; }

    public static StableDiff Removed(string path) =>
        new(StableDiffKind.Removed, path);

    public static StableDiff Added(string path, StableValue actual) =>
        new(StableDiffKind.Added, path, actual: actual ?? StableValue.Nil());

    public static StableDiff Changed(string path, StableValue expected, StableValue actual) =>
        new(
            StableDiffKind.Changed,
            path,
            expected ?? StableValue.Nil(),
            actual ?? StableValue.Nil()
        );

    public static StableDiff SizeChanged(string path, int expectedCount, int actualCount) =>
        new(
            StableDiffKind.SizeChanged,
            path,
            expectedCount: expectedCount,
            actualCount: actualCount
        );

    public string Format()
    {
        return Kind switch
        {
            StableDiffKind.Removed => $"{Path} was removed",
            StableDiffKind.Added => $"{Path} was added with {Actual.Format()}",
            StableDiffKind.Changed
                => $"{Path} changed from {Expected.Format()} to {Actual.Format()}",
            StableDiffKind.SizeChanged
                => $"{Path} size changed from {ExpectedCount} to {ActualCount}",
            _ => Path,
        };
    }
}

internal enum StableValueKind
{
    Nil,
    Bool,
    Integer,
    Float,
    Text,
    Vector2I,
    Vector2,
    Vector3I,
    Vector3,
    ObjectId,
    Reference,
    Fallback,
    Map,
    Array,
}

internal sealed class StableMap
{
    private readonly Dictionary<string, StableValue> _entries = new(StringComparer.Ordinal);

    public IEnumerable<KeyValuePair<string, StableValue>> Entries => _entries;

    public static StableMap FromTypedDictionary(IReadOnlyDictionary<string, object> source)
    {
        StableMap result = new();
        if (source == null)
        {
            return result;
        }
        foreach ((string key, StableValue value) in BattleAiMutationSnapshotModel.ReadStableTypedEntries(source))
        {
            result.Set(key, value);
        }
        return result;
    }

    public StableMap Clone()
    {
        StableMap clone = new();
        foreach (KeyValuePair<string, StableValue> entry in _entries)
        {
            clone.Set(entry.Key, entry.Value.Clone());
        }
        return clone;
    }

    public bool ContainsKey(string key) => _entries.ContainsKey(key);

    public void CopyFrom(StableMap source)
    {
        _entries.Clear();
        if (source == null)
        {
            return;
        }
        foreach (KeyValuePair<string, StableValue> entry in source._entries)
        {
            Set(entry.Key, entry.Value.Clone());
        }
    }

    public bool Remove(string key) => _entries.Remove(key);

    public void Set(string key, StableValue value)
    {
        if (key == null)
        {
            return;
        }
        _entries[key] = value ?? StableValue.Nil();
    }

    public bool TryGet(string key, out StableValue value) => _entries.TryGetValue(key, out value);

    public bool TryGetMap(string key, out StableMap map)
    {
        if (_entries.TryGetValue(key, out StableValue value) && value.TryGetMap(out map))
        {
            return true;
        }
        map = null;
        return false;
    }

    public StableMap GetMapOrEmpty(string key)
    {
        return TryGetMap(key, out StableMap map) ? map : new StableMap();
    }
}

internal sealed class StableValue
{
    private readonly StableValueKind _kind;
    private readonly bool _boolValue;
    private readonly long _integerValue;
    private readonly double _floatValue;
    private readonly string _textValue;
    private readonly Vector2I _vector2IValue;
    private readonly Vector2 _vector2Value;
    private readonly Vector3I _vector3IValue;
    private readonly Vector3 _vector3Value;
    private readonly StableMap _mapValue;
    private readonly List<StableValue> _arrayValue;
    private readonly object _referenceValue;

    private StableValue(
        StableValueKind kind,
        bool boolValue = false,
        long integerValue = 0,
        double floatValue = 0.0,
        string textValue = "",
        Vector2I vector2IValue = default,
        Vector2 vector2Value = default,
        Vector3I vector3IValue = default,
        Vector3 vector3Value = default,
        StableMap mapValue = null,
        List<StableValue> arrayValue = null,
        object referenceValue = null
    )
    {
        _kind = kind;
        _boolValue = boolValue;
        _integerValue = integerValue;
        _floatValue = floatValue;
        _textValue = textValue ?? "";
        _vector2IValue = vector2IValue;
        _vector2Value = vector2Value;
        _vector3IValue = vector3IValue;
        _vector3Value = vector3Value;
        _mapValue = mapValue;
        _arrayValue = arrayValue;
        _referenceValue = referenceValue;
    }

    public static StableValue Nil() => new(StableValueKind.Nil);

    public static StableValue FromMap(StableMap value) =>
        new(StableValueKind.Map, mapValue: value ?? new StableMap());

    public StableValue Clone()
    {
        if (_kind == StableValueKind.Map)
        {
            return FromMap(_mapValue.Clone());
        }
        if (_kind == StableValueKind.Array)
        {
            List<StableValue> items = new();
            foreach (StableValue item in _arrayValue)
            {
                items.Add(item.Clone());
            }
            return FromArray(items);
        }
        return this;
    }

    public bool TryGetMap(out StableMap value)
    {
        if (_kind == StableValueKind.Map)
        {
            value = _mapValue;
            return true;
        }
        value = null;
        return false;
    }

    public bool TryGetArray(out IReadOnlyList<StableValue> value)
    {
        if (_kind == StableValueKind.Array)
        {
            value = _arrayValue;
            return true;
        }
        value = null;
        return false;
    }

    public bool ScalarEquals(StableValue other)
    {
        if (other == null || _kind != other._kind)
        {
            return false;
        }
        return _kind switch
        {
            StableValueKind.Nil => true,
            StableValueKind.Bool => _boolValue == other._boolValue,
            StableValueKind.Integer => _integerValue == other._integerValue,
            StableValueKind.Float
                => BitConverter.DoubleToInt64Bits(_floatValue)
                    == BitConverter.DoubleToInt64Bits(other._floatValue),
            StableValueKind.Text => _textValue == other._textValue,
            StableValueKind.Vector2I => _vector2IValue == other._vector2IValue,
            StableValueKind.Vector2 => _vector2Value == other._vector2Value,
            StableValueKind.Vector3I => _vector3IValue == other._vector3IValue,
            StableValueKind.Vector3 => _vector3Value == other._vector3Value,
            StableValueKind.ObjectId => _integerValue == other._integerValue,
            StableValueKind.Reference => ReferenceEquals(_referenceValue, other._referenceValue),
            StableValueKind.Fallback => _textValue == other._textValue,
            _ => false,
        };
    }

    public string Format()
    {
        return _kind switch
        {
            StableValueKind.Nil => "null",
            StableValueKind.Bool => _boolValue ? "true" : "false",
            StableValueKind.Integer => _integerValue.ToString(CultureInfo.InvariantCulture),
            StableValueKind.Float => _floatValue.ToString("R", CultureInfo.InvariantCulture),
            StableValueKind.Text => _textValue ?? "",
            StableValueKind.Vector2I
                => $"Vector2i({_vector2IValue.X},{_vector2IValue.Y})",
            StableValueKind.Vector2
                => $"Vector2({_vector2Value.X.ToString("R", CultureInfo.InvariantCulture)},{_vector2Value.Y.ToString("R", CultureInfo.InvariantCulture)})",
            StableValueKind.Vector3I
                => $"Vector3i({_vector3IValue.X},{_vector3IValue.Y},{_vector3IValue.Z})",
            StableValueKind.Vector3
                => $"Vector3({_vector3Value.X.ToString("R", CultureInfo.InvariantCulture)},{_vector3Value.Y.ToString("R", CultureInfo.InvariantCulture)},{_vector3Value.Z.ToString("R", CultureInfo.InvariantCulture)})",
            StableValueKind.ObjectId
                => $"Object#{_integerValue.ToString(CultureInfo.InvariantCulture)}",
            StableValueKind.Reference
                => _referenceValue == null
                    ? "reference:null"
                    : $"reference:{_referenceValue.GetType().Name}",
            StableValueKind.Fallback => _textValue ?? "",
            StableValueKind.Map => "{...}",
            StableValueKind.Array => "[...]",
            _ => "",
        };
    }

    public static StableValue FromArray(List<StableValue> value) =>
        new(StableValueKind.Array, arrayValue: value ?? new List<StableValue>());

    public static StableValue FromBool(bool value) =>
        new(StableValueKind.Bool, boolValue: value);

    public static StableValue FromInteger(long value) =>
        new(StableValueKind.Integer, integerValue: value);

    public static StableValue FromFloat(double value) =>
        new(StableValueKind.Float, floatValue: value);

    public static StableValue FromText(string value) =>
        new(StableValueKind.Text, textValue: value ?? "");

    public static StableValue FromVector2I(Vector2I value) =>
        new(StableValueKind.Vector2I, vector2IValue: value);

    public static StableValue FromVector2(Vector2 value) =>
        new(StableValueKind.Vector2, vector2Value: value);

    public static StableValue FromVector3I(Vector3I value) =>
        new(StableValueKind.Vector3I, vector3IValue: value);

    public static StableValue FromVector3(Vector3 value) =>
        new(StableValueKind.Vector3, vector3Value: value);

    public static StableValue FromObjectId(long value) =>
        new(StableValueKind.ObjectId, integerValue: value);

    public static StableValue FromReference(object value) =>
        value == null
            ? Nil()
            : new StableValue(StableValueKind.Reference, referenceValue: value);
}

internal static class BattleAiMutationSnapshotModel
{
    internal const long StableHashOffset = unchecked((long)1469598103934665603UL);
    internal const long StableHashPrime = 1099511628211L;
    internal static long MixHash(long hash, long value)
    {
        unchecked
        {
            hash ^= value;
            hash *= BattleAiMutationSnapshotModel.StableHashPrime;
            return hash;
        }
    }

    internal static long MixTextHash(long hash, string value)
    {
        string text = value ?? "";
        hash = BattleAiMutationSnapshotModel.MixHash(hash, text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            hash = BattleAiMutationSnapshotModel.MixHash(hash, text[index]);
        }
        return hash;
    }

    internal static long MixStringNameArrayHash(
        long hash,
        IEnumerable<StringName> values
    )
    {
        if (values == null)
        {
            return BattleAiMutationSnapshotModel.MixHash(hash, 0);
        }

        hash = BattleAiMutationSnapshotModel.MixHash(hash, 1);
        int count = 0;
        foreach (StringName value in values)
        {
            count++;
            hash = MixNullableStringNameHash(hash, value);
        }
        return BattleAiMutationSnapshotModel.MixHash(hash, count);
    }

    internal static long MixNullableStringNameHash(long hash, StringName value)
    {
        if (value == null)
        {
            return BattleAiMutationSnapshotModel.MixHash(hash, 0);
        }
        hash = BattleAiMutationSnapshotModel.MixHash(hash, 1);
        return BattleAiMutationSnapshotModel.MixTextHash(hash, value.ToString());
    }

    internal static long MixTypedDictionaryHash(
        long hash,
        IReadOnlyDictionary<string, object> values
    )
    {
        if (values == null || values.Count == 0)
        {
            return BattleAiMutationSnapshotModel.MixHash(hash, 0);
        }

        var keys = new List<string>(values.Keys);
        keys.Sort(StringComparer.Ordinal);
        hash = BattleAiMutationSnapshotModel.MixHash(hash, keys.Count);
        foreach (string key in keys)
        {
            hash = BattleAiMutationSnapshotModel.MixTextHash(hash, key);
            values.TryGetValue(key, out object value);
            hash = BattleAiMutationSnapshotModel.MixTextHash(hash, value?.ToString() ?? "");
        }
        return hash;
    }

    internal static string StableKey(string text)
    {
        return text ?? "";
    }

    internal static string StableKey(StringName stringName)
    {
        if (stringName == null)
        {
            return "StringName:null";
        }
        string text = stringName.ToString();
        return $"StringName:{text.Length.ToString(CultureInfo.InvariantCulture)}:{text}";
    }

    internal static string StableKey(Vector2I coord)
    {
        return $"Vector2i({coord.X},{coord.Y})";
    }











    internal static List<KeyValuePair<string, StableValue>> ReadStableTypedEntries(
        IReadOnlyDictionary<string, object> source
    )
    {
        var result = new List<KeyValuePair<string, StableValue>>();
        if (source == null)
        {
            return result;
        }
        foreach (KeyValuePair<string, object> entry in source)
        {
            if (entry.Key == null)
            {
                continue;
            }
            result.Add(
                new KeyValuePair<string, StableValue>(
                    entry.Key,
                    BattleAiMutationSnapshotModel.ReadStableTypedValue(entry.Value)
                )
            );
        }
        return result;
    }

    internal static StableValue ReadStableTypedValue(object rawValue)
    {
        switch (rawValue)
        {
            case null:
                return StableValue.Nil();
            case bool boolValue:
                return StableTypedText("Boolean", boolValue ? "1" : "0");
            case byte byteValue:
                return StableTypedText("Byte", byteValue.ToString(CultureInfo.InvariantCulture));
            case sbyte sbyteValue:
                return StableTypedText("SByte", sbyteValue.ToString(CultureInfo.InvariantCulture));
            case short shortValue:
                return StableTypedText("Int16", shortValue.ToString(CultureInfo.InvariantCulture));
            case ushort ushortValue:
                return StableTypedText("UInt16", ushortValue.ToString(CultureInfo.InvariantCulture));
            case int intValue:
                return StableTypedText("Int32", intValue.ToString(CultureInfo.InvariantCulture));
            case uint uintValue:
                return StableTypedText("UInt32", uintValue.ToString(CultureInfo.InvariantCulture));
            case long longValue:
                return StableTypedText("Int64", longValue.ToString(CultureInfo.InvariantCulture));
            case ulong ulongValue:
                return StableTypedText("UInt64", ulongValue.ToString(CultureInfo.InvariantCulture));
            case float floatValue:
                return StableTypedText("Single", FloatBits(floatValue));
            case double doubleValue:
                return StableTypedText("Double", DoubleBits(doubleValue));
            case string textValue:
                return StableTypedText("String", textValue);
            case StringName stringNameValue:
                return StableTypedText("StringName", stringNameValue.ToString());
            case Vector2I vector2IValue:
                return StableTypedText(
                    "Vector2I",
                    IntegerComponents(vector2IValue.X, vector2IValue.Y)
                );
            case Vector2 vector2Value:
                return StableTypedText(
                    "Vector2",
                    FloatComponents(vector2Value.X, vector2Value.Y)
                );
            case Rect2 rect2Value:
                return StableTypedText(
                    "Rect2",
                    FloatComponents(
                        rect2Value.Position.X,
                        rect2Value.Position.Y,
                        rect2Value.Size.X,
                        rect2Value.Size.Y
                    )
                );
            case Rect2I rect2IValue:
                return StableTypedText(
                    "Rect2I",
                    IntegerComponents(
                        rect2IValue.Position.X,
                        rect2IValue.Position.Y,
                        rect2IValue.Size.X,
                        rect2IValue.Size.Y
                    )
                );
            case Vector3I vector3IValue:
                return StableTypedText(
                    "Vector3I",
                    IntegerComponents(vector3IValue.X, vector3IValue.Y, vector3IValue.Z)
                );
            case Vector3 vector3Value:
                return StableTypedText(
                    "Vector3",
                    FloatComponents(vector3Value.X, vector3Value.Y, vector3Value.Z)
                );
            case Transform2D transform2DValue:
                return StableTypedText(
                    "Transform2D",
                    FloatComponents(
                        transform2DValue.X.X,
                        transform2DValue.X.Y,
                        transform2DValue.Y.X,
                        transform2DValue.Y.Y,
                        transform2DValue.Origin.X,
                        transform2DValue.Origin.Y
                    )
                );
            case Vector4 vector4Value:
                return StableTypedText(
                    "Vector4",
                    FloatComponents(
                        vector4Value.X,
                        vector4Value.Y,
                        vector4Value.Z,
                        vector4Value.W
                    )
                );
            case Vector4I vector4IValue:
                return StableTypedText(
                    "Vector4I",
                    IntegerComponents(
                        vector4IValue.X,
                        vector4IValue.Y,
                        vector4IValue.Z,
                        vector4IValue.W
                    )
                );
            case Plane planeValue:
                return StableTypedText(
                    "Plane",
                    FloatComponents(
                        planeValue.Normal.X,
                        planeValue.Normal.Y,
                        planeValue.Normal.Z,
                        planeValue.D
                    )
                );
            case Quaternion quaternionValue:
                return StableTypedText(
                    "Quaternion",
                    FloatComponents(
                        quaternionValue.X,
                        quaternionValue.Y,
                        quaternionValue.Z,
                        quaternionValue.W
                    )
                );
            case Aabb aabbValue:
                return StableTypedText(
                    "Aabb",
                    FloatComponents(
                        aabbValue.Position.X,
                        aabbValue.Position.Y,
                        aabbValue.Position.Z,
                        aabbValue.Size.X,
                        aabbValue.Size.Y,
                        aabbValue.Size.Z
                    )
                );
            case Basis basisValue:
                return StableTypedText(
                    "Basis",
                    FloatComponents(
                        basisValue.X.X,
                        basisValue.X.Y,
                        basisValue.X.Z,
                        basisValue.Y.X,
                        basisValue.Y.Y,
                        basisValue.Y.Z,
                        basisValue.Z.X,
                        basisValue.Z.Y,
                        basisValue.Z.Z
                    )
                );
            case Transform3D transform3DValue:
                return StableTypedText(
                    "Transform3D",
                    FloatComponents(
                        transform3DValue.Basis.X.X,
                        transform3DValue.Basis.X.Y,
                        transform3DValue.Basis.X.Z,
                        transform3DValue.Basis.Y.X,
                        transform3DValue.Basis.Y.Y,
                        transform3DValue.Basis.Y.Z,
                        transform3DValue.Basis.Z.X,
                        transform3DValue.Basis.Z.Y,
                        transform3DValue.Basis.Z.Z,
                        transform3DValue.Origin.X,
                        transform3DValue.Origin.Y,
                        transform3DValue.Origin.Z
                    )
                );
            case Projection projectionValue:
                return StableTypedText(
                    "Projection",
                    FloatComponents(
                        projectionValue.X.X,
                        projectionValue.X.Y,
                        projectionValue.X.Z,
                        projectionValue.X.W,
                        projectionValue.Y.X,
                        projectionValue.Y.Y,
                        projectionValue.Y.Z,
                        projectionValue.Y.W,
                        projectionValue.Z.X,
                        projectionValue.Z.Y,
                        projectionValue.Z.Z,
                        projectionValue.Z.W,
                        projectionValue.W.X,
                        projectionValue.W.Y,
                        projectionValue.W.Z,
                        projectionValue.W.W
                    )
                );
            case Color colorValue:
                return StableTypedText(
                    "Color",
                    FloatComponents(colorValue.R, colorValue.G, colorValue.B, colorValue.A)
                );
            case IReadOnlyDictionary<string, object> dictionaryValue:
                return StableValue.FromMap(StableMap.FromTypedDictionary(dictionaryValue));
            case IReadOnlyDictionary<StringName, int> stringNameIntDictionary:
                return ReadStableStringNameIntDictionary(stringNameIntDictionary);
            case IReadOnlyList<object> listValue:
                return StableValue.FromArray(BattleAiMutationSnapshotModel.ReadStableTypedArrayValues(listValue));
            case System.Collections.IEnumerable enumerableValue
                when rawValue is not string:
                return StableValue.FromArray(BattleAiMutationSnapshotModel.ReadStableEnumerableValues(enumerableValue));
            default:
                return StableTypedText(
                    rawValue.GetType().FullName ?? rawValue.GetType().Name,
                    rawValue.ToString() ?? ""
                );
        }
    }

    private static StableValue StableTypedText(string typeName, string payload) =>
        StableValue.FromText($"{typeName}:{payload ?? ""}");

    private static string FloatBits(float value) =>
        unchecked((uint)BitConverter.SingleToInt32Bits(value)).ToString(
            "X8",
            CultureInfo.InvariantCulture
        );

    private static string DoubleBits(double value) =>
        unchecked((ulong)BitConverter.DoubleToInt64Bits(value)).ToString(
            "X16",
            CultureInfo.InvariantCulture
        );

    private static string FloatComponents(params float[] values)
    {
        string[] components = new string[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            components[index] = FloatBits(values[index]);
        }
        return string.Join(",", components);
    }

    private static string IntegerComponents(params int[] values)
    {
        string[] components = new string[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            components[index] = values[index].ToString(CultureInfo.InvariantCulture);
        }
        return string.Join(",", components);
    }

    internal static StableValue ReadStableStringNameIntDictionary(
        IReadOnlyDictionary<StringName, int> source
    )
    {
        var entries = new List<KeyValuePair<StringName, int>>(source);
        entries.Sort(
            (left, right) => StringComparer.Ordinal.Compare(
                StableKey(left.Key),
                StableKey(right.Key)
            )
        );
        List<StableValue> result = new();
        foreach (KeyValuePair<StringName, int> entry in entries)
        {
            StableMap stableEntry = new();
            stableEntry.Set("key", ReadStableTypedValue(entry.Key));
            stableEntry.Set("value", ReadStableTypedValue(entry.Value));
            result.Add(StableValue.FromMap(stableEntry));
        }
        return StableValue.FromArray(result);
    }

    internal static List<StableValue> ReadStableTypedArrayValues(IReadOnlyList<object> source)
    {
        List<StableValue> result = new();
        if (source == null)
        {
            return result;
        }
        foreach (object rawValue in source)
        {
            result.Add(BattleAiMutationSnapshotModel.ReadStableTypedValue(rawValue));
        }
        return result;
    }

    internal static List<StableValue> ReadStableEnumerableValues(
        System.Collections.IEnumerable source
    )
    {
        List<StableValue> result = new();
        if (source == null)
        {
            return result;
        }
        foreach (object rawValue in source)
        {
            result.Add(BattleAiMutationSnapshotModel.ReadStableTypedValue(rawValue));
        }
        return result;
    }

    internal static StableValue StableScalarFromText(string text)
    {
        text ??= "";
        if (bool.TryParse(text, out bool boolValue))
        {
            return StableValue.FromBool(boolValue);
        }
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integerValue))
        {
            return StableValue.FromInteger(integerValue);
        }
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double floatValue))
        {
            return StableValue.FromFloat(floatValue);
        }
        return StableValue.FromText(text);
    }
}
