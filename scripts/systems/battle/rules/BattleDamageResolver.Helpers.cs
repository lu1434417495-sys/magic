using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// BattleDamageResolver 的 partial：纯静态/无副作用的字典与集合读取 helper。
// 从主文件按职责拆出，不改逻辑。
public partial class BattleDamageResolver
{
    private static GArray CoerceEffectDefs(GArray effectDefs)
    {
        return effectDefs ?? new GArray();
    }

    internal static GArray ToValueArray(Godot.Collections.Array<CombatEffectDef> values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (CombatEffectDef value in values)
        {
            if (value != null)
            {
                result.Add(value);
            }
        }
        return result;
    }

    internal static GArray ToValueArray(IEnumerable<CombatEffectDef> values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (CombatEffectDef value in values)
        {
            if (value != null)
            {
                result.Add(value);
            }
        }
        return result;
    }

    private static GDictionary DuplicateDictionary(GDictionary source, bool deep = true)
    {
        return source != null ? source.Duplicate(deep) : new GDictionary();
    }

    private SkillDef GetSkillDefTyped(StringName skillId)
    {
        if (skillId == null || skillId == "")
        {
            return null;
        }
        return _skillDefIndex.TryGetValue(skillId, out SkillDef skillDef) ? skillDef : null;
    }

    private static bool TryGet(GDictionary source, object key, out Variant value)
    {
        value = default;
        if (source == null || key == null)
            return false;
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        if (variantKey.VariantType == Variant.Type.Nil)
            return false;
        if (source.ContainsKey(variantKey))
        {
            value = source[variantKey];
            return value.VariantType != Variant.Type.Nil;
        }
        return false;
    }

    private static GDictionary GetDictionary(GDictionary source, object key)
    {
        if (!TryGet(source, key, out Variant value))
            return new GDictionary();
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static GArray GetArray(GDictionary source, object key)
    {
        if (!TryGet(source, key, out Variant value))
            return new GArray();
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static T GetObject<T>(GDictionary source, object key)
        where T : RefCounted
    {
        if (!TryGet(source, key, out Variant value))
            return null;
        return value.VariantType == Variant.Type.Object ? value.AsGodotObject() as T : null;
    }

    private static int GetInt(GDictionary source, object key, int fallback = 0)
    {
        if (!TryGet(source, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static double GetFloat(GDictionary source, object key, double fallback = 0.0)
    {
        if (!TryGet(source, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt64(),
            Variant.Type.Float => value.AsDouble(),
            _ => fallback,
        };
    }

    private static string GetString(GDictionary source, object key, string fallback = "")
    {
        if (!TryGet(source, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    private static StringName GetStringName(
        GDictionary source,
        object key,
        StringName fallback = default
    )
    {
        if (!TryGet(source, key, out Variant value))
            return fallback ?? "";
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => fallback ?? "",
        };
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        if (values == null)
            yield break;
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Dictionary)
                yield return value.AsGodotDictionary();
        }
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static bool HasStringName(IReadOnlyList<StringName> values, StringName target)
    {
        if (values == null || values.Count == 0 || IsEmpty(target))
        {
            return false;
        }
        foreach (StringName value in values)
        {
            if (value == target)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasKey(GDictionary source, object key)
    {
        return TryGet(source, key, out _);
    }

    private static int DictInt(GDictionary source, object key, int fallback = 0)
    {
        return GetInt(source, key, fallback);
    }

    private static double DictFloat(GDictionary source, object key, double fallback = 0.0)
    {
        return GetFloat(source, key, fallback);
    }

    private static string DictString(GDictionary source, object key, string fallback = "")
    {
        return GetString(source, key, fallback);
    }

    private static StringName DictStringName(
        GDictionary source,
        object key,
        StringName fallback = default
    )
    {
        return GetStringName(source, key, fallback);
    }

    private static bool TryGetStatusParam(
        GDictionary @params,
        StringName param_key,
        out object value
    )
    {
        if (@params == null || param_key == "")
        {
            value = default;
            return false;
        }
        if (@params.ContainsKey(param_key))
        {
            value = @params[param_key];
            return true;
        }
        string paramName = param_key.ToString();
        if (@params.ContainsKey(paramName))
        {
            value = @params[paramName];
            return true;
        }
        foreach (Variant keyValue in @params.Keys)
        {
            if (ProgressionDataUtils.to_string_name(keyValue) == param_key)
            {
                value = @params[keyValue];
                return true;
            }
        }
        value = default;
        return false;
    }

    private static bool TryGetStatusParamTyped(
        IReadOnlyDictionary<string, object> @params,
        StringName param_key,
        out object value
    )
    {
        value = default;
        if (@params == null || param_key == "")
        {
            return false;
        }
        return @params.TryGetValue(param_key.ToString(), out value);
    }

    private static T DictObject<T>(GDictionary source, StringName key)
        where T : RefCounted
    {
        return TryGetStatusParam(source, key, out object rawValue)
            ? ToGodotObject<T>(rawValue)
            : null;
    }

    private static T ToGodotObject<T>(object rawValue)
        where T : RefCounted
    {
        if (rawValue is T typedValue)
        {
            return typedValue;
        }
        if (rawValue is Variant variantValue && variantValue.VariantType == Variant.Type.Object)
        {
            return variantValue.AsGodotObject() as T;
        }
        return null;
    }

    private static StringName DictStringNameLocal(
        GDictionary source,
        StringName key,
        StringName fallback = default
    )
    {
        if (!TryGetStatusParam(source, key, out object rawValue))
        {
            return fallback ?? "";
        }
        StringName normalized = ProgressionDataUtils.to_string_name(rawValue);
        return normalized == "" ? fallback ?? "" : normalized;
    }

    private static bool ToBool(object rawValue, bool fallback)
    {
        if (rawValue is bool boolValue)
        {
            return boolValue;
        }
        if (rawValue is not Variant variantValue)
        {
            return rawValue != null ? rawValue.ToString()?.ToLowerInvariant() == "true" : fallback;
        }
        return variantValue.VariantType switch
        {
            Variant.Type.Bool => variantValue.AsBool(),
            Variant.Type.Int => variantValue.AsInt32() != 0,
            Variant.Type.Float => !Mathf.IsZeroApprox((float)variantValue.AsDouble()),
            Variant.Type.String => variantValue.AsString().ToLowerInvariant() == "true",
            Variant.Type.StringName
                => variantValue.AsStringName().ToString().ToLowerInvariant() == "true",
            _ => fallback,
        };
    }

    private static void AddUnique(GStringNameArray target, StringName value)
    {
        if (value != "" && !target.Contains(value))
        {
            target.Add(value);
        }
    }

    private static GStringNameArray ToStringNameArray(GArray values)
    {
        var result = new GStringNameArray();
        foreach (var value in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized != "")
            {
                result.Add(normalized);
            }
        }
        return result;
    }

    private static int RoundToInt(double value)
    {
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static void AppendTraitTriggerResult(
        ref DamageEventResult target,
        TraitTriggerResultSnapshot triggerResult
    )
    {
        if (!triggerResult.Triggered)
        {
            return;
        }
        var results = new List<TraitTriggerEventResult>();
        if (target.TraitTriggerResults != null)
        {
            results.AddRange(target.TraitTriggerResults);
        }
        results.Add(triggerResult.ToEventResult());
        target.TraitTriggerResults = results.ToArray();
    }
}
