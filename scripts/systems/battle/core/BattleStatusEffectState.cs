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
        "counts_as_debuff_override",
        "counts_as_debuff",
        "lock_counterattack",
        "lock_crit",
        "main_skill_lock_other_debuff_count",
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
    public bool forced_move_immune { get; set; }
    public bool counts_as_debuff_override { get; set; }
    public bool counts_as_debuff { get; set; }
    public bool lock_counterattack { get; set; }
    public bool lock_crit { get; set; }
    public int main_skill_lock_other_debuff_count { get; set; }

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
        return new BattleStatusEffectState
        {
            status_id = status_id,
            source_unit_id = source_unit_id,
            power = power,
            @params = @params?.Duplicate(true) ?? new GDictionary(),
            stacks = stacks,
            duration = duration,
            tick_interval_tu = tick_interval_tu,
            next_tick_at_tu = next_tick_at_tu,
            skip_next_turn_end_decay = skip_next_turn_end_decay,
            forced_move_immune = forced_move_immune,
            counts_as_debuff_override = counts_as_debuff_override,
            counts_as_debuff = counts_as_debuff,
            lock_counterattack = lock_counterattack,
            lock_crit = lock_crit,
            main_skill_lock_other_debuff_count = main_skill_lock_other_debuff_count,
        };
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
        if (counts_as_debuff_override)
        {
            payload["counts_as_debuff_override"] = true;
            payload["counts_as_debuff"] = counts_as_debuff;
        }
        if (lock_counterattack)
        {
            payload["lock_counterattack"] = true;
        }
        if (lock_crit)
        {
            payload["lock_crit"] = true;
        }
        if (main_skill_lock_other_debuff_count > 0)
        {
            payload["main_skill_lock_other_debuff_count"] = main_skill_lock_other_debuff_count;
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
            if (!TryReadBoolField(effectDict, "skip_next_turn_end_decay", out skipDecayValue)
                || !skipDecayValue)
            {
                return null;
            }
        }

        bool countsAsDebuffOverrideValue = false;
        if (effectDict.ContainsKey("counts_as_debuff_override"))
        {
            if (
                !TryReadBoolField(
                    effectDict,
                    "counts_as_debuff_override",
                    out countsAsDebuffOverrideValue
                )
                || !countsAsDebuffOverrideValue
            )
            {
                return null;
            }
        }

        bool countsAsDebuffValue = false;
        if (effectDict.ContainsKey("counts_as_debuff"))
        {
            if (
                !countsAsDebuffOverrideValue
                || !TryReadBoolField(effectDict, "counts_as_debuff", out countsAsDebuffValue)
            )
            {
                return null;
            }
        }
        else if (countsAsDebuffOverrideValue)
        {
            return null;
        }

        bool lockCounterattackValue = false;
        if (effectDict.ContainsKey("lock_counterattack"))
        {
            if (
                !TryReadBoolField(effectDict, "lock_counterattack", out lockCounterattackValue)
                || !lockCounterattackValue
            )
            {
                return null;
            }
        }

        bool lockCritValue = false;
        if (effectDict.ContainsKey("lock_crit"))
        {
            if (
                !TryReadBoolField(effectDict, "lock_crit", out lockCritValue)
                || !lockCritValue
            )
            {
                return null;
            }
        }

        int mainSkillLockOtherDebuffCountValue = 0;
        if (effectDict.ContainsKey("main_skill_lock_other_debuff_count"))
        {
            if (
                !TryGetStrictInt(
                    effectDict,
                    "main_skill_lock_other_debuff_count",
                    out mainSkillLockOtherDebuffCountValue
                )
                || mainSkillLockOtherDebuffCountValue <= 0
            )
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
            counts_as_debuff_override = countsAsDebuffOverrideValue,
            counts_as_debuff = countsAsDebuffValue,
            lock_counterattack = lockCounterattackValue,
            lock_crit = lockCritValue,
            main_skill_lock_other_debuff_count = mainSkillLockOtherDebuffCountValue,
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
        foreach (var keyValue in effectDict.Keys)
        {
            string key = keyValue.ToString();
            if (!HasString(RequiredSchemaFields, key) && !HasString(OptionalSchemaFields, key))
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryGetStringLike(GDictionary data, string key, out string value)
    {
        if (data != null && data.ContainsKey(key) && IsStringLikeField(data, key))
        {
            value = data[key].ToString();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetStrictInt(GDictionary data, string key, out int value)
    {
        if (data != null && data.ContainsKey(key) && IsFieldType(data, key, "Int"))
        {
            value = data[key].AsInt32();
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryGetDictionary(GDictionary data, string key, out GDictionary value)
    {
        if (data != null && data.ContainsKey(key) && IsFieldType(data, key, "Dictionary"))
        {
            value = data[key].AsGodotDictionary();
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static bool TryReadBoolField(GDictionary data, string key, out bool value)
    {
        if (data != null && data.ContainsKey(key) && IsFieldType(data, key, "Bool"))
        {
            value = data[key].AsBool();
            return true;
        }
        value = false;
        return false;
    }

    private static bool IsStringLikeField(GDictionary data, string key)
    {
        string typeName = data[key].VariantType.ToString();
        return typeName == "String" || typeName == "StringName";
    }

    private static bool IsFieldType(GDictionary data, string key, string expectedTypeName)
    {
        return data[key].VariantType.ToString() == expectedTypeName;
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
