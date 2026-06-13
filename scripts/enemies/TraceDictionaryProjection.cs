using System.Collections.Generic;
using Godot;

internal static class TraceDictionaryProjection
{
    internal static Dictionary<string, object> FromDictionary(Godot.Collections.Dictionary source)
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

    internal static Godot.Collections.Dictionary ToDictionary(IReadOnlyDictionary<string, object> source)
    {
        var result = new Godot.Collections.Dictionary();
        if (source == null)
        {
            return result;
        }
        foreach (KeyValuePair<string, object> entry in source)
        {
            if (!string.IsNullOrEmpty(entry.Key))
            {
                AddValue(result, entry.Key, entry.Value);
            }
        }
        return result;
    }

    internal static Godot.Collections.Dictionary ToDictionary(IReadOnlyDictionary<string, int> source)
    {
        var result = new Godot.Collections.Dictionary();
        if (source == null)
        {
            return result;
        }
        foreach (KeyValuePair<string, int> entry in source)
        {
            if (!string.IsNullOrEmpty(entry.Key))
            {
                result[entry.Key] = entry.Value;
            }
        }
        return result;
    }

    private static void AddValue(Godot.Collections.Dictionary result, string key, object value)
    {
        switch (value)
        {
            case null:
                result[key] = "";
                break;
            case string text:
                result[key] = text;
                break;
            case StringName name:
                result[key] = name;
                break;
            case bool flag:
                result[key] = flag;
                break;
            case int intValue:
                result[key] = intValue;
                break;
            case long longValue:
                result[key] = longValue;
                break;
            case float floatValue:
                result[key] = floatValue;
                break;
            case double doubleValue:
                result[key] = doubleValue;
                break;
            case Vector2I coord:
                result[key] = coord;
                break;
            case AiCommandSummary command:
                result[key] = ToDictionary(command.ToTraceDictionary());
                break;
            case AiCandidateSummary candidate:
                result[key] = ToDictionary(candidate.ToTraceDictionary());
                break;
            case AiActionTrace actionTrace:
                result[key] = ToDictionary(actionTrace.ToTraceDictionary());
                break;
            case BattleAiTurnTraceProjection turnTrace:
                result[key] = ToDictionary(turnTrace.ToTraceDictionary());
                break;
            case BattleAiTraceTransitionProjection transition:
                result[key] = ToDictionary(transition.ToTraceDictionary());
                break;
            case BattleAiTraceTransitionConditionProjection condition:
                result[key] = ToDictionary(condition.ToTraceDictionary());
                break;
            case BattleAiTraceUnitSnapshotProjection snapshot:
                result[key] = ToDictionary(snapshot.ToTraceDictionary());
                break;
            case BattleAiTraceUnitResultProjection unitResult:
                result[key] = ToDictionary(unitResult.ToTraceDictionary());
                break;
            case BattleAiTraceExecutionResultProjection executionResult:
                result[key] = ToDictionary(executionResult.ToTraceDictionary());
                break;
            case BattleAiScoreInput scoreInput:
                result[key] = ToDictionary(scoreInput.ToTraceDictionary());
                break;
            case IReadOnlyDictionary<string, object> dictionary:
                result[key] = ToDictionary(dictionary);
                break;
            case IReadOnlyDictionary<string, int> intDictionary:
                result[key] = ToDictionary(intDictionary);
                break;
            case IEnumerable<StringName> stringNames:
                result[key] = ToStringNameArray(stringNames);
                break;
            case IEnumerable<Vector2I> coords:
                result[key] = ToVector2IArray(coords);
                break;
            case IEnumerable<string> strings:
                result[key] = ToStringArray(strings);
                break;
            case IEnumerable<object> values:
                result[key] = ToArray(values);
                break;
            default:
                result[key] = value.ToString();
                break;
        }
    }

    private static object FromVariant(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Bool => value.AsBool(),
            Variant.Type.Int => value.AsInt64(),
            Variant.Type.Float => value.AsDouble(),
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.Vector2I => value.AsVector2I(),
            Variant.Type.Dictionary => FromDictionary(value.AsGodotDictionary()),
            Variant.Type.Array => FromArray(value.AsGodotArray()),
            Variant.Type.Nil => "",
            _ => value.ToString(),
        };
    }

    internal static List<object> FromArray(Godot.Collections.Array source)
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

    internal static Godot.Collections.Array ToArray(IEnumerable<object> values)
    {
        var result = new Godot.Collections.Array();
        if (values == null)
        {
            return result;
        }
        foreach (object value in values)
        {
            AddArrayValue(result, value);
        }
        return result;
    }

    private static void AddArrayValue(Godot.Collections.Array result, object value)
    {
        switch (value)
        {
            case null:
                result.Add("");
                break;
            case string text:
                result.Add(text);
                break;
            case StringName name:
                result.Add(name);
                break;
            case bool flag:
                result.Add(flag);
                break;
            case int intValue:
                result.Add(intValue);
                break;
            case long longValue:
                result.Add(longValue);
                break;
            case float floatValue:
                result.Add(floatValue);
                break;
            case double doubleValue:
                result.Add(doubleValue);
                break;
            case Vector2I coord:
                result.Add(coord);
                break;
            case AiCommandSummary command:
                result.Add(ToDictionary(command.ToTraceDictionary()));
                break;
            case AiCandidateSummary candidate:
                result.Add(ToDictionary(candidate.ToTraceDictionary()));
                break;
            case AiActionTrace actionTrace:
                result.Add(ToDictionary(actionTrace.ToTraceDictionary()));
                break;
            case BattleAiTurnTraceProjection turnTrace:
                result.Add(ToDictionary(turnTrace.ToTraceDictionary()));
                break;
            case BattleAiTraceTransitionProjection transition:
                result.Add(ToDictionary(transition.ToTraceDictionary()));
                break;
            case BattleAiTraceTransitionConditionProjection condition:
                result.Add(ToDictionary(condition.ToTraceDictionary()));
                break;
            case BattleAiTraceUnitSnapshotProjection snapshot:
                result.Add(ToDictionary(snapshot.ToTraceDictionary()));
                break;
            case BattleAiTraceUnitResultProjection unitResult:
                result.Add(ToDictionary(unitResult.ToTraceDictionary()));
                break;
            case BattleAiTraceExecutionResultProjection executionResult:
                result.Add(ToDictionary(executionResult.ToTraceDictionary()));
                break;
            case BattleAiScoreInput scoreInput:
                result.Add(ToDictionary(scoreInput.ToTraceDictionary()));
                break;
            case IReadOnlyDictionary<string, object> dictionary:
                result.Add(ToDictionary(dictionary));
                break;
            case IReadOnlyDictionary<string, int> intDictionary:
                result.Add(ToDictionary(intDictionary));
                break;
            case IEnumerable<object> values:
                result.Add(ToArray(values));
                break;
            default:
                result.Add(value.ToString());
                break;
        }
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

    private static Godot.Collections.Array<StringName> ToStringNameArray(IEnumerable<StringName> values)
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (StringName value in values)
        {
            result.Add(ProgressionDataUtils.to_string_name(value));
        }
        return result;
    }

    private static Godot.Collections.Array<Vector2I> ToVector2IArray(IEnumerable<Vector2I> values)
    {
        var result = new Godot.Collections.Array<Vector2I>();
        foreach (Vector2I value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static Godot.Collections.Array<string> ToStringArray(IEnumerable<string> values)
    {
        var result = new Godot.Collections.Array<string>();
        foreach (string value in values)
        {
            result.Add(value ?? "");
        }
        return result;
    }
}
