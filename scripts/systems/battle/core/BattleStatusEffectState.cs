using Godot;
using GDictionary = Godot.Collections.Dictionary;

// 战斗状态效果数据 bag。
// 翻译自 battle_status_effect_state.gd（2026-05-24，数据层 C# 迁移）。
// 契约：docs/design/battle_csharp_migration.md
[GlobalClass]
public partial class BattleStatusEffectState : RefCounted
{
    private static readonly string[] RequiredSchemaFields =
    {
        "status_id",
        "source_unit_id",
        "power",
        "params",
        "stacks",
    };

    private static readonly string[] OptionalSchemaFields =
    {
        "duration",
        "tick_interval_tu",
        "next_tick_at_tu",
        "skip_next_turn_end_decay",
    };

    public StringName status_id { get; set; } = "";
    public StringName source_unit_id { get; set; } = "";
    public int power { get; set; }
    public GDictionary @params { get; set; } = new();
    public int stacks { get; set; }
    public int duration { get; set; } = -1;
    public int tick_interval_tu { get; set; }
    public int next_tick_at_tu { get; set; }
    public bool skip_next_turn_end_decay { get; set; }

    public bool is_empty()
    {
        return status_id == "";
    }

    public bool has_duration()
    {
        return duration >= 0;
    }

    public BattleStatusEffectState duplicate_state()
    {
        return from_dict(to_dict());
    }

    public GDictionary to_dict()
    {
        GDictionary payload = new()
        {
            ["status_id"] = status_id.ToString(),
            ["source_unit_id"] = source_unit_id.ToString(),
            ["power"] = power,
            ["params"] = @params.Duplicate(true),
            ["stacks"] = stacks,
        };
        if (has_duration())
        {
            payload["duration"] = duration;
        }
        if (tick_interval_tu > 0)
        {
            payload["tick_interval_tu"] = tick_interval_tu;
        }
        if (next_tick_at_tu > 0)
        {
            payload["next_tick_at_tu"] = next_tick_at_tu;
        }
        if (skip_next_turn_end_decay)
        {
            payload["skip_next_turn_end_decay"] = true;
        }
        return payload;
    }

    public static BattleStatusEffectState from_dict(GDictionary effectDict)
    {
        if (effectDict == null)
            return null;
        if (!HasCurrentSchemaFields(effectDict))
        {
            return null;
        }

        if (!TryGetStringLike(effectDict, "status_id", out string statusId)
            || string.IsNullOrEmpty(statusId))
        {
            return null;
        }
        if (!TryGetStringLike(effectDict, "source_unit_id", out string sourceUnitId))
        {
            return null;
        }
        if (!TryGetStrictInt(effectDict, "power", out int power))
        {
            return null;
        }
        if (!TryGetDictionary(effectDict, "params", out GDictionary parameters))
        {
            return null;
        }
        if (!TryGetStrictInt(effectDict, "stacks", out int stacks) || stacks < 0)
        {
            return null;
        }

        int durationValue = -1;
        if (effectDict.ContainsKey("duration"))
        {
            if (!TryGetStrictInt(effectDict, "duration", out durationValue) || durationValue < 0)
            {
                return null;
            }
        }

        int tickIntervalValue = 0;
        if (effectDict.ContainsKey("tick_interval_tu"))
        {
            if (
                !TryGetStrictInt(effectDict, "tick_interval_tu", out tickIntervalValue)
                || tickIntervalValue <= 0
            )
            {
                return null;
            }
        }

        int nextTickAtValue = 0;
        if (effectDict.ContainsKey("next_tick_at_tu"))
        {
            if (
                !TryGetStrictInt(effectDict, "next_tick_at_tu", out nextTickAtValue)
                || nextTickAtValue <= 0
            )
            {
                return null;
            }
        }

        bool skipDecayValue = false;
        if (effectDict.ContainsKey("skip_next_turn_end_decay"))
        {
            if (!TryGetBool(effectDict, "skip_next_turn_end_decay", out skipDecayValue)
                || !skipDecayValue)
            {
                return null;
            }
        }

        return new BattleStatusEffectState
        {
            status_id = new StringName(statusId),
            source_unit_id = new StringName(sourceUnitId),
            power = power,
            @params = parameters.Duplicate(true),
            stacks = stacks,
            duration = durationValue,
            tick_interval_tu = tickIntervalValue,
            next_tick_at_tu = nextTickAtValue,
            skip_next_turn_end_decay = skipDecayValue,
        };
    }

    private static bool HasCurrentSchemaFields(GDictionary effectDict)
    {
        foreach (string field in RequiredSchemaFields)
        {
            if (!effectDict.ContainsKey(field))
            {
                return false;
            }
        }
        foreach (object keyValue in effectDict.Keys)
        {
            if (!TryAsStrictStringKey(keyValue, out string key))
            {
                return false;
            }
            if (!HasString(RequiredSchemaFields, key) && !HasString(OptionalSchemaFields, key))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryGetStringLike(GDictionary data, string key, out string value)
    {
        if (TryGetExactValue(data, key, out object rawValue)
            && TryAsStringLike(rawValue, out value))
        {
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetStrictInt(GDictionary data, string key, out int value)
    {
        if (TryGetExactValue(data, key, out object rawValue)
            && TryAsStrictInt(rawValue, out value))
        {
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryGetDictionary(GDictionary data, string key, out GDictionary value)
    {
        if (TryGetExactValue(data, key, out object rawValue)
            && TryAsDictionary(rawValue, out value))
        {
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static bool TryGetBool(GDictionary data, string key, out bool value)
    {
        if (TryGetExactValue(data, key, out object rawValue) && TryAsBool(rawValue, out value))
        {
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryAsStrictStringKey(object rawValue, out string value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.String)
        {
            value = variant.AsString();
            return true;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsStringLike(object rawValue, out string value)
    {
        if (rawValue is Variant variant)
        {
            if (variant.VariantType == Variant.Type.String)
            {
                value = variant.AsString();
                return true;
            }
            if (variant.VariantType == Variant.Type.StringName)
            {
                value = variant.AsStringName().ToString();
                return true;
            }
            value = "";
            return false;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        if (rawValue is StringName stringNameValue)
        {
            value = stringNameValue.ToString();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsStrictInt(object rawValue, out int value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Int)
        {
            value = variant.AsInt32();
            return true;
        }
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryAsDictionary(object rawValue, out GDictionary value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            value = variant.AsGodotDictionary();
            return true;
        }
        if (rawValue is GDictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static bool TryAsBool(object rawValue, out bool value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Bool)
        {
            value = variant.AsBool();
            return true;
        }
        if (rawValue is bool boolValue)
        {
            value = boolValue;
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryGetExactValue(GDictionary data, string key, out object value)
    {
        if (data != null && data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        value = null;
        return false;
    }

    private static bool HasString(string[] values, string value)
    {
        foreach (string entry in values)
        {
            if (entry == value)
            {
                return true;
            }
        }
        return false;
    }

}
