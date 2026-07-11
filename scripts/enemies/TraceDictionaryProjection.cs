using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class TraceDictionaryProjection
{
    internal static Dictionary<string, object> FromDictionary(GDictionary source)
    {
        var result = new Dictionary<string, object>(System.StringComparer.Ordinal);
        if (source == null)
        {
            return result;
        }
        foreach (Variant rawKey in source.Keys)
        {
            string key = ReadKey(rawKey);
            if (string.IsNullOrEmpty(key) || !source.ContainsKey(rawKey))
            {
                continue;
            }
            result[key] = FromVariant(source[rawKey]);
        }
        return result;
    }

    internal static GodotProjectionLease<GDictionary> BuildLease(
        IReadOnlyDictionary<string, object> source,
        string ownerId,
        LifetimeDomain domain,
        string reason
    )
    {
        var root = new GDictionary();
        GodotProjectionLease<GDictionary> lease =
            GodotProjectionLease<GDictionary>.CreateOwnedRoot(
                root,
                ownerId,
                domain,
                reason
            );
        try
        {
            WriteInto(lease, root, source, reason);
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal static GodotProjectionLease<GDictionary> BuildJsonSafeLease(
        IReadOnlyDictionary<string, object> source,
        string ownerId,
        LifetimeDomain domain,
        string reason
    )
    {
        var root = new GDictionary();
        GodotProjectionLease<GDictionary> lease =
            GodotProjectionLease<GDictionary>.CreateOwnedRoot(
                root,
                ownerId,
                domain,
                reason
            );
        try
        {
            WriteJsonSafeInto(lease, root, source, reason);
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal static GodotProjectionLease<GArray> BuildArrayLease(
        IEnumerable<object> source,
        string ownerId,
        LifetimeDomain domain,
        string reason
    )
    {
        var root = new GArray();
        GodotProjectionLease<GArray> lease = GodotProjectionLease<GArray>.CreateOwnedRoot(
            root,
            ownerId,
            domain,
            reason
        );
        try
        {
            WriteArrayInto(lease, root, source, reason);
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal static void WriteInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary target,
        IReadOnlyDictionary<string, object> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(target);
        if (source == null)
            return;

        foreach (KeyValuePair<string, object> entry in source)
        {
            if (!string.IsNullOrEmpty(entry.Key))
                target[entry.Key] = WriteValue(lease, entry.Value, $"{reason}.{entry.Key}");
        }
    }

    private static void WriteJsonSafeInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary target,
        IReadOnlyDictionary<string, object> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(target);
        if (source == null)
            return;
        foreach (KeyValuePair<string, object> entry in source)
        {
            if (!string.IsNullOrEmpty(entry.Key))
            {
                target[entry.Key] = WriteJsonSafeValue(
                    lease,
                    entry.Value,
                    $"{reason}.{entry.Key}"
                );
            }
        }
    }

    internal static GDictionary WriteDictionary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyDictionary<string, object> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        WriteInto(lease, result, source, reason);
        return result;
    }

    internal static GDictionary WriteDictionary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyDictionary<string, int> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (source == null)
            return result;
        foreach (KeyValuePair<string, int> entry in source)
        {
            if (!string.IsNullOrEmpty(entry.Key))
                result[entry.Key] = entry.Value;
        }
        return result;
    }

    internal static GArray WriteArray<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IEnumerable<object> values,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        WriteArrayInto(lease, result, values, reason);
        return result;
    }

    internal static void WriteArrayInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GArray target,
        IEnumerable<object> values,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(target);
        if (values == null)
            return;
        int index = 0;
        foreach (object value in values)
        {
            target.Add(WriteValue(lease, value, $"{reason}[{index}]"));
            index++;
        }
    }

    private static Variant WriteValue<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        object value,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        return value switch
        {
            null => Variant.From(""),
            string text => Variant.From(text),
            StringName name => Variant.From(name),
            bool flag => Variant.From(flag),
            int intValue => Variant.From(intValue),
            long longValue => Variant.From(longValue),
            float floatValue => Variant.From(floatValue),
            double doubleValue => Variant.From(doubleValue),
            Vector2I coord => Variant.From(coord),
            AiCommandSummary command => Variant.From(
                WriteDictionary(lease, command.ToTraceDictionary(), reason)
            ),
            AiCandidateSummary candidate => Variant.From(
                WriteDictionary(lease, candidate.ToTraceDictionary(), reason)
            ),
            AiActionTrace actionTrace => Variant.From(
                WriteDictionary(lease, actionTrace.ToTraceDictionary(), reason)
            ),
            BattleAiTurnTraceProjection turnTrace => Variant.From(
                WriteDictionary(lease, turnTrace.ToTraceDictionary(), reason)
            ),
            BattleAiTraceTransitionProjection transition => Variant.From(
                WriteDictionary(lease, transition.ToTraceDictionary(), reason)
            ),
            BattleAiTraceTransitionConditionProjection condition => Variant.From(
                WriteDictionary(lease, condition.ToTraceDictionary(), reason)
            ),
            BattleAiTraceUnitSnapshotProjection snapshot => Variant.From(
                WriteDictionary(lease, snapshot.ToTraceDictionary(), reason)
            ),
            BattleAiTraceUnitResultProjection unitResult => Variant.From(
                WriteDictionary(lease, unitResult.ToTraceDictionary(), reason)
            ),
            BattleAiTraceExecutionResultProjection executionResult => Variant.From(
                WriteDictionary(lease, executionResult.ToTraceDictionary(), reason)
            ),
            BattleAiScoreInput scoreInput => Variant.From(
                WriteDictionary(lease, scoreInput.ToTraceDictionary(), reason)
            ),
            IReadOnlyDictionary<string, object> dictionary => Variant.From(
                WriteDictionary(lease, dictionary, reason)
            ),
            IReadOnlyDictionary<string, int> intDictionary => Variant.From(
                WriteDictionary(lease, intDictionary, reason)
            ),
            IReadOnlyDictionary<StringName, object> stringNameObjectDictionary => Variant.From(
                WriteStringNameObjectDictionary(
                    lease,
                    stringNameObjectDictionary,
                    reason
                )
            ),
            IReadOnlyDictionary<StringName, int> stringNameIntDictionary => Variant.From(
                WriteStringNameIntDictionary(lease, stringNameIntDictionary, reason)
            ),
            IEnumerable<StringName> stringNames => Variant.From(
                WriteStringNameArray(lease, stringNames, reason)
            ),
            IEnumerable<Vector2I> coords => Variant.From(
                WriteVector2IArray(lease, coords, reason)
            ),
            IEnumerable<string> strings => Variant.From(
                WriteStringArray(lease, strings, reason)
            ),
            IEnumerable<object> values => Variant.From(WriteArray(lease, values, reason)),
            _ => throw new InvalidOperationException(
                $"Unsupported trace projection value type: {value.GetType().FullName} ({reason})."
            ),
        };
    }

    private static Variant WriteJsonSafeValue<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        object value,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        return value switch
        {
            null => Variant.From(""),
            string text => Variant.From(text),
            StringName name => Variant.From(name.ToString()),
            bool flag => Variant.From(flag),
            int intValue => Variant.From(intValue),
            long longValue => Variant.From(longValue),
            float floatValue => Variant.From(floatValue),
            double doubleValue => Variant.From(doubleValue),
            Vector2I coord => Variant.From(WriteJsonSafeCoord(lease, coord, reason)),
            AiCommandSummary command => Variant.From(
                WriteJsonSafeDictionary(lease, command.ToTraceDictionary(), reason)
            ),
            AiCandidateSummary candidate => Variant.From(
                WriteJsonSafeDictionary(lease, candidate.ToTraceDictionary(), reason)
            ),
            AiActionTrace actionTrace => Variant.From(
                WriteJsonSafeDictionary(lease, actionTrace.ToTraceDictionary(), reason)
            ),
            BattleAiTurnTraceProjection turnTrace => Variant.From(
                WriteJsonSafeDictionary(lease, turnTrace.ToTraceDictionary(), reason)
            ),
            BattleAiTraceTransitionProjection transition => Variant.From(
                WriteJsonSafeDictionary(lease, transition.ToTraceDictionary(), reason)
            ),
            BattleAiTraceTransitionConditionProjection condition => Variant.From(
                WriteJsonSafeDictionary(lease, condition.ToTraceDictionary(), reason)
            ),
            BattleAiTraceUnitSnapshotProjection snapshot => Variant.From(
                WriteJsonSafeDictionary(lease, snapshot.ToTraceDictionary(), reason)
            ),
            BattleAiTraceUnitResultProjection unitResult => Variant.From(
                WriteJsonSafeDictionary(lease, unitResult.ToTraceDictionary(), reason)
            ),
            BattleAiTraceExecutionResultProjection executionResult => Variant.From(
                WriteJsonSafeDictionary(lease, executionResult.ToTraceDictionary(), reason)
            ),
            BattleAiScoreInput scoreInput => Variant.From(
                WriteJsonSafeDictionary(lease, scoreInput.ToTraceDictionary(), reason)
            ),
            IReadOnlyDictionary<string, object> dictionary => Variant.From(
                WriteJsonSafeDictionary(lease, dictionary, reason)
            ),
            IReadOnlyDictionary<string, int> intDictionary => Variant.From(
                WriteJsonSafeStringIntDictionary(lease, intDictionary, reason)
            ),
            IReadOnlyDictionary<string, float> floatDictionary => Variant.From(
                WriteJsonSafeStringFloatDictionary(lease, floatDictionary, reason)
            ),
            IReadOnlyDictionary<StringName, object> stringNameObjectDictionary => Variant.From(
                WriteJsonSafeStringNameObjectDictionary(
                    lease,
                    stringNameObjectDictionary,
                    reason
                )
            ),
            IReadOnlyDictionary<StringName, int> stringNameIntDictionary => Variant.From(
                WriteJsonSafeStringNameIntDictionary(
                    lease,
                    stringNameIntDictionary,
                    reason
                )
            ),
            IEnumerable<StringName> stringNames => Variant.From(
                WriteJsonSafeStringNameArray(lease, stringNames, reason)
            ),
            IEnumerable<Vector2I> coords => Variant.From(
                WriteJsonSafeVectorArray(lease, coords, reason)
            ),
            IEnumerable<string> strings => Variant.From(
                WriteJsonSafeStringArray(lease, strings, reason)
            ),
            IEnumerable<object> values => Variant.From(
                WriteJsonSafeArray(lease, values, reason)
            ),
            _ => throw new InvalidOperationException(
                $"Unsupported JSON-safe projection value type: {value.GetType().FullName} ({reason})."
            ),
        };
    }

    private static GDictionary WriteJsonSafeDictionary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyDictionary<string, object> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        WriteJsonSafeInto(lease, result, source, reason);
        return result;
    }

    private static GArray WriteJsonSafeArray<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IEnumerable<object> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (source == null)
            return result;
        int index = 0;
        foreach (object value in source)
        {
            result.Add(WriteJsonSafeValue(lease, value, $"{reason}[{index}]"));
            index++;
        }
        return result;
    }

    private static GDictionary WriteJsonSafeCoord<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        Vector2I coord,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        result["x"] = coord.X;
        result["y"] = coord.Y;
        return result;
    }

    private static GDictionary WriteJsonSafeStringIntDictionary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyDictionary<string, int> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (source != null)
            foreach ((string key, int value) in source)
                if (!string.IsNullOrEmpty(key))
                    result[key] = value;
        return result;
    }

    private static GDictionary WriteJsonSafeStringFloatDictionary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyDictionary<string, float> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (source != null)
            foreach ((string key, float value) in source)
                if (!string.IsNullOrEmpty(key))
                    result[key] = value;
        return result;
    }

    private static GDictionary WriteJsonSafeStringNameObjectDictionary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyDictionary<StringName, object> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (source == null)
            return result;
        foreach ((StringName key, object value) in source)
        {
            string textKey = key.ToString();
            if (!string.IsNullOrEmpty(textKey))
                result[textKey] = WriteJsonSafeValue(lease, value, $"{reason}.{textKey}");
        }
        return result;
    }

    private static GDictionary WriteJsonSafeStringNameIntDictionary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyDictionary<StringName, int> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (source != null)
            foreach ((StringName key, int value) in source)
                if (key != (StringName)"")
                    result[key.ToString()] = value;
        return result;
    }

    private static GArray WriteJsonSafeStringNameArray<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IEnumerable<StringName> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (source != null)
            foreach (StringName value in source)
                result.Add(value.ToString());
        return result;
    }

    private static GArray WriteJsonSafeVectorArray<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IEnumerable<Vector2I> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (source == null)
            return result;
        int index = 0;
        foreach (Vector2I value in source)
        {
            result.Add(WriteJsonSafeCoord(lease, value, $"{reason}[{index}]"));
            index++;
        }
        return result;
    }

    private static GArray WriteJsonSafeStringArray<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IEnumerable<string> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (source != null)
            foreach (string value in source)
                result.Add(value ?? "");
        return result;
    }

    private static object FromVariant(Variant value)
    {
        switch (value.VariantType)
        {
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
            case Variant.Type.Dictionary:
                using (GDictionary dictionary = value.AsGodotDictionary())
                    return FromDictionary(dictionary);
            case Variant.Type.Array:
                using (GArray array = value.AsGodotArray())
                    return FromArray(array);
            case Variant.Type.Nil:
                return "";
            default:
                return value.ToString();
        }
    }

    internal static List<object> FromArray(GArray source)
    {
        var result = new List<object>();
        if (source == null)
        {
            return result;
        }
        foreach (Variant value in source)
        {
            result.Add(FromVariant(value));
        }
        return result;
    }

    private static string ReadKey(Variant key)
    {
        return key.VariantType switch
        {
            Variant.Type.String => key.AsString(),
            Variant.Type.StringName => key.AsStringName().ToString(),
            Variant.Type.Nil => "",
            _ => key.ToString(),
        };
    }

    private static GArray WriteStringNameArray<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IEnumerable<StringName> values,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        foreach (StringName value in values)
            result.Add(ProgressionDataUtils.to_string_name(value));
        return result;
    }

    private static GDictionary WriteStringNameIntDictionary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyDictionary<StringName, int> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (source == null)
            return result;
        foreach (KeyValuePair<StringName, int> entry in source)
        {
            if (entry.Key != (StringName)"")
                result[entry.Key] = entry.Value;
        }
        return result;
    }

    private static GDictionary WriteStringNameObjectDictionary<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyDictionary<StringName, object> source,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (source == null)
            return result;
        foreach (KeyValuePair<StringName, object> entry in source)
        {
            if (entry.Key != (StringName)"")
                result[entry.Key] = WriteValue(
                    lease,
                    entry.Value,
                    $"{reason}.{entry.Key}"
                );
        }
        return result;
    }

    private static GArray WriteVector2IArray<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IEnumerable<Vector2I> values,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        foreach (Vector2I value in values)
            result.Add(value);
        return result;
    }

    private static GArray WriteStringArray<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IEnumerable<string> values,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        foreach (string value in values)
            result.Add(value ?? "");
        return result;
    }
}
