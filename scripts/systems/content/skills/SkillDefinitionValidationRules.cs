#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

/// <summary>
/// Plain-value helpers shared by the migrated skill validator. These helpers never
/// construct or inspect Godot authoring Resources; they only read the immutable
/// definition/value graph projected from a skill import model.
/// </summary>
internal static class SkillDefinitionValidationRules
{
    internal const int TuGranularity = 5;

    internal static bool IsValidTuValue(int value) =>
        value >= 0 && (value == 0 || value % TuGranularity == 0);

    internal static void RequireStringName(
        Array<string> errors,
        StringName skillId,
        string fieldLabel,
        StringName actual,
        StringName expected
    )
    {
        if (actual != expected)
            errors.Add($"Skill {skillId} {fieldLabel} must be {expected}.");
    }

    internal static void RequireInt(
        Array<string> errors,
        StringName skillId,
        string fieldLabel,
        int actual,
        int expected
    )
    {
        if (actual != expected)
            errors.Add($"Skill {skillId} {fieldLabel} must be {expected}.");
    }

    internal static void RequireBool(
        Array<string> errors,
        StringName skillId,
        string fieldLabel,
        bool actual,
        bool expected
    )
    {
        if (actual != expected)
        {
            errors.Add(
                $"Skill {skillId} {fieldLabel} must be {expected.ToString().ToLowerInvariant()}."
            );
        }
    }

    internal static void RequireRange(
        Array<string> errors,
        StringName skillId,
        string fieldLabel,
        int actual,
        int minimum,
        int maximum
    )
    {
        if (actual < minimum || actual > maximum)
        {
            errors.Add(
                $"Skill {skillId} {fieldLabel} must be between {minimum} and {maximum}."
            );
        }
    }

    internal static void RequireStringNameParam(
        Array<string> errors,
        StringName skillId,
        IReadOnlyDictionary<string, object> parameters,
        string contextLabel,
        string paramName,
        StringName expected
    )
    {
        if (!TryGetParameter(parameters, paramName, out object? rawValue))
            return;
        StringName actual = ProgressionDataUtils.to_string_name(rawValue);
        if (actual != expected)
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.{paramName} must be {expected}."
            );
        }
    }

    internal static void RequirePositiveIntParam(
        Array<string> errors,
        StringName skillId,
        IReadOnlyDictionary<string, object> parameters,
        string contextLabel,
        string paramName
    )
    {
        if (
            TryReadStrictIntParam(
                errors,
                skillId,
                parameters,
                contextLabel,
                paramName,
                out int value
            )
            && value <= 0
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.{paramName} must be a positive int."
            );
        }
    }

    internal static void RequireNonNegativeIntParam(
        Array<string> errors,
        StringName skillId,
        IReadOnlyDictionary<string, object> parameters,
        string contextLabel,
        string paramName
    )
    {
        if (
            TryReadStrictIntParam(
                errors,
                skillId,
                parameters,
                contextLabel,
                paramName,
                out int value
            )
            && value < 0
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.{paramName} must be >= 0."
            );
        }
    }

    internal static void RequireIntRangeParam(
        Array<string> errors,
        StringName skillId,
        IReadOnlyDictionary<string, object> parameters,
        string contextLabel,
        string paramName,
        int minimum,
        int maximum
    )
    {
        if (
            TryReadStrictIntParam(
                errors,
                skillId,
                parameters,
                contextLabel,
                paramName,
                out int value
            )
            && (value < minimum || value > maximum)
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.{paramName} must be between {minimum} and {maximum}."
            );
        }
    }

    internal static void RequirePositiveTuParam(
        Array<string> errors,
        StringName skillId,
        IReadOnlyDictionary<string, object> parameters,
        string contextLabel,
        string paramName
    )
    {
        if (
            TryReadStrictIntParam(
                errors,
                skillId,
                parameters,
                contextLabel,
                paramName,
                out int value
            )
            && (value <= 0 || !IsValidTuValue(value))
        )
        {
            errors.Add(
                $"Skill {skillId} effect {contextLabel} params.{paramName} must be a positive multiple of {TuGranularity}."
            );
        }
    }

    private static bool TryReadStrictIntParam(
        Array<string> errors,
        StringName skillId,
        IReadOnlyDictionary<string, object> parameters,
        string contextLabel,
        string paramName,
        out int value
    )
    {
        if (!TryGetParameter(parameters, paramName, out object? rawValue))
        {
            value = 0;
            return false;
        }
        if (TryStrictInt(rawValue, out value))
            return true;
        errors.Add(
            $"Skill {skillId} effect {contextLabel} params.{paramName} must be an int."
        );
        return false;
    }

    internal static bool TryReadLevelOverrideInt(
        Array<string> errors,
        StringName skillId,
        object overrideLevelKey,
        IReadOnlyDictionary<string, object> overrideValues,
        string fieldName,
        out int value
    )
    {
        if (!TryGetParameter(overrideValues, fieldName, out object? rawValue))
        {
            value = 0;
            return false;
        }
        if (TryStrictInt(rawValue, out value))
            return true;
        errors.Add(
            $"Skill {skillId} combat_profile level override {overrideLevelKey}.{fieldName} must be an int."
        );
        return false;
    }

    internal static string ParameterKeyLabel(object? rawKey) => rawKey switch
    {
        null => "",
        StringName value => value.ToString(),
        _ => rawKey.ToString() ?? "",
    };

    internal static int DictInt(
        IReadOnlyDictionary<string, object> dictionary,
        string key,
        int fallback = 0
    )
    {
        if (!TryGetParameter(dictionary, key, out object? value))
            return fallback;
        return value switch
        {
            int intValue => intValue,
            long longValue when longValue is >= int.MinValue and <= int.MaxValue =>
                (int)longValue,
            _ => fallback,
        };
    }

    internal static bool TryStrictInt(object? rawValue, out int value)
    {
        switch (rawValue)
        {
            case int intValue:
                value = intValue;
                return true;
            case long longValue when longValue is >= int.MinValue and <= int.MaxValue:
                value = (int)longValue;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    internal static bool TryGetParameter(
        IReadOnlyDictionary<string, object>? dictionary,
        string key,
        out object? value
    )
    {
        if (dictionary != null && dictionary.TryGetValue(key, out object? found))
        {
            value = found;
            return true;
        }
        value = null;
        return false;
    }

    internal static bool TryGetDictionaryValue(
        object? dictionary,
        object? key,
        out object? value
    )
    {
        if (
            key != null
            && dictionary is IDictionary nonGeneric
            && nonGeneric.Contains(key)
        )
        {
            value = nonGeneric[key];
            return true;
        }
        value = null;
        return false;
    }

    internal static bool TryAsArray(
        object? rawValue,
        out IReadOnlyList<object> values
    )
    {
        if (rawValue is string || rawValue is not IEnumerable enumerable)
        {
            values = System.Array.Empty<object>();
            return false;
        }
        values = enumerable.Cast<object>().ToArray();
        return true;
    }

    internal static bool TryAsDictionary(
        object? rawValue,
        out IReadOnlyDictionary<string, object> values
    )
    {
        if (rawValue is IReadOnlyDictionary<string, object> typed)
        {
            values = typed;
            return true;
        }
        values = new System.Collections.Generic.Dictionary<string, object>(
            StringComparer.Ordinal
        );
        return false;
    }
}
