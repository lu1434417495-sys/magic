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

    public static BattleStatusEffectState from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        GDictionary effectDict = data.AsGodotDictionary();
        if (!HasCurrentSchemaFields(effectDict))
        {
            return null;
        }

        Variant rawStatusId = Get(effectDict, "status_id");
        if (!IsNonEmptyStringLike(rawStatusId))
        {
            return null;
        }
        Variant rawSourceUnitId = Get(effectDict, "source_unit_id");
        if (!IsStringLike(rawSourceUnitId))
        {
            return null;
        }
        Variant rawPower = Get(effectDict, "power");
        if (rawPower.VariantType != Variant.Type.Int)
        {
            return null;
        }
        Variant rawParams = Get(effectDict, "params");
        if (rawParams.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        Variant rawStacks = Get(effectDict, "stacks");
        if (rawStacks.VariantType != Variant.Type.Int || rawStacks.AsInt32() < 0)
        {
            return null;
        }

        int durationValue = -1;
        if (effectDict.ContainsKey("duration"))
        {
            Variant rawDuration = Get(effectDict, "duration");
            if (rawDuration.VariantType != Variant.Type.Int || rawDuration.AsInt32() < 0)
            {
                return null;
            }
            durationValue = rawDuration.AsInt32();
        }

        int tickIntervalValue = 0;
        if (effectDict.ContainsKey("tick_interval_tu"))
        {
            Variant rawTickInterval = Get(effectDict, "tick_interval_tu");
            if (rawTickInterval.VariantType != Variant.Type.Int || rawTickInterval.AsInt32() <= 0)
            {
                return null;
            }
            tickIntervalValue = rawTickInterval.AsInt32();
        }

        int nextTickAtValue = 0;
        if (effectDict.ContainsKey("next_tick_at_tu"))
        {
            Variant rawNextTickAt = Get(effectDict, "next_tick_at_tu");
            if (rawNextTickAt.VariantType != Variant.Type.Int || rawNextTickAt.AsInt32() <= 0)
            {
                return null;
            }
            nextTickAtValue = rawNextTickAt.AsInt32();
        }

        bool skipDecayValue = false;
        if (effectDict.ContainsKey("skip_next_turn_end_decay"))
        {
            Variant rawSkipDecay = Get(effectDict, "skip_next_turn_end_decay");
            if (rawSkipDecay.VariantType != Variant.Type.Bool || !rawSkipDecay.AsBool())
            {
                return null;
            }
            skipDecayValue = true;
        }

        return new BattleStatusEffectState
        {
            status_id = ToStringName(rawStatusId),
            source_unit_id = ToStringName(rawSourceUnitId),
            power = rawPower.AsInt32(),
            @params = rawParams.AsGodotDictionary().Duplicate(true),
            stacks = rawStacks.AsInt32(),
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
        foreach (Variant keyVariant in effectDict.Keys)
        {
            if (keyVariant.VariantType != Variant.Type.String)
            {
                return false;
            }
            string key = keyVariant.AsString();
            if (!HasString(RequiredSchemaFields, key) && !HasString(OptionalSchemaFields, key))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsStringLike(Variant value)
    {
        return value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName;
    }

    private static bool IsNonEmptyStringLike(Variant value)
    {
        return IsStringLike(value) && !value.AsString().IsEmpty();
    }

    private static StringName ToStringName(Variant value)
    {
        return IsStringLike(value) ? new StringName(value.AsString()) : "";
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

    private static Variant Get(GDictionary payload, string key)
    {
        return payload.ContainsKey(key) ? payload[key] : default;
    }
}
