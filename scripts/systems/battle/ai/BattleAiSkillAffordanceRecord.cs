using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal sealed class BattleAiSkillAffordanceRecord
{
    public StringName skill_id = "";
    public bool is_generatable;
    public string skip_reason = "";
    public StringName team_intent = "";
    public StringName target_mode = "";
    public StringName target_filter = "";
    public StringName selection_mode = "";
    public List<StringName> effect_roles = new();
    public List<StringName> affordances = new();
    public List<StringName> action_families = new();
    public bool requires_positioning_action;
    public List<StringName> variant_ids = new();
    public string blocked_reason = "";

    public static BattleAiSkillAffordanceRecord Empty(SkillDef skillDef)
    {
        return new BattleAiSkillAffordanceRecord
        {
            skill_id = skillDef != null ? ProgressionDataUtils.to_string_name(skillDef.skill_id) : "",
        };
    }

    public static BattleAiSkillAffordanceRecord FromDictionary(
        StringName skillId,
        GDictionary record
    )
    {
        record ??= new GDictionary();
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        if (normalizedSkillId == "")
        {
            normalizedSkillId = ReadStringName(record, "skill_id");
        }
        return new BattleAiSkillAffordanceRecord
        {
            skill_id = normalizedSkillId,
            is_generatable = ReadBool(record, "is_generatable"),
            skip_reason = ReadString(record, "skip_reason"),
            team_intent = ReadStringName(record, "team_intent"),
            target_mode = ReadStringName(record, "target_mode"),
            target_filter = ReadStringName(record, "target_filter"),
            selection_mode = ReadStringName(record, "selection_mode"),
            effect_roles = DecodeStringNameArray(ReadArray(record, "effect_roles")),
            affordances = DecodeStringNameArray(ReadArray(record, "affordances")),
            action_families = DecodeStringNameArray(ReadArray(record, "action_families")),
            requires_positioning_action = ReadBool(
                record,
                "requires_positioning_action"
            ),
            variant_ids = DecodeStringNameArray(ReadArray(record, "variant_ids")),
            blocked_reason = ReadString(record, "blocked_reason"),
        };
    }

    public GDictionary ToDictionary()
    {
        GDictionary result = new();
        result["skill_id"] = skill_id;
        result["is_generatable"] = is_generatable;
        result["skip_reason"] = skip_reason;
        result["team_intent"] = team_intent;
        result["target_mode"] = target_mode;
        result["target_filter"] = target_filter;
        result["selection_mode"] = selection_mode;
        result["effect_roles"] = ToStringNameArray(effect_roles);
        result["affordances"] = ToStringNameArray(affordances);
        result["action_families"] = ToStringNameArray(action_families);
        result["requires_positioning_action"] = requires_positioning_action;
        result["variant_ids"] = ToStringNameArray(variant_ids);
        result["blocked_reason"] = blocked_reason;
        return result;
    }

    public BattleAiSkillAffordanceRecord Clone()
    {
        return new BattleAiSkillAffordanceRecord
        {
            skill_id = skill_id,
            is_generatable = is_generatable,
            skip_reason = skip_reason,
            team_intent = team_intent,
            target_mode = target_mode,
            target_filter = target_filter,
            selection_mode = selection_mode,
            effect_roles = new List<StringName>(effect_roles),
            affordances = new List<StringName>(affordances),
            action_families = new List<StringName>(action_families),
            requires_positioning_action = requires_positioning_action,
            variant_ids = new List<StringName>(variant_ids),
            blocked_reason = blocked_reason,
        };
    }

    public void AddEffectRole(StringName value) => AddUnique(effect_roles, value);

    public void AddAffordance(StringName value) => AddUnique(affordances, value);

    public void AddActionFamily(StringName value) => AddUnique(action_families, value);

    public void AddVariantId(StringName value) => AddUnique(variant_ids, value);

    public bool HasActionFamily(StringName value)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(value);
        return normalized != "" && action_families.Contains(normalized);
    }

    public bool HasAnyActionFamily(IEnumerable<StringName> values)
    {
        if (values == null)
        {
            return false;
        }
        foreach (StringName value in values)
        {
            if (HasActionFamily(value))
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsTypedKey(string key)
    {
        return key
            is "skill_id"
                or "is_generatable"
                or "skip_reason"
                or "team_intent"
                or "target_mode"
                or "target_filter"
                or "selection_mode"
                or "effect_roles"
                or "affordances"
                or "action_families"
                or "requires_positioning_action"
                or "variant_ids"
                or "blocked_reason";
    }

    private static void AddUnique(List<StringName> target, StringName value)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(value);
        if (normalized == "" || target.Contains(normalized))
        {
            return;
        }
        target.Add(normalized);
    }

    private static List<StringName> DecodeStringNameArray(GArray values)
    {
        var result = new List<StringName>();
        if (values == null)
        {
            return result;
        }
        foreach (var rawValue in values)
        {
            StringName normalizedValue = ProgressionDataUtils.to_string_name(rawValue);
            if (normalizedValue != "")
            {
                result.Add(normalizedValue);
            }
        }
        return result;
    }

    private static string ReadString(GDictionary data, string key, string fallback = "")
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback;
        }
        return data[key].ToString();
    }

    private static StringName ReadStringName(GDictionary data, string key)
    {
        string value = ReadString(data, key);
        return !string.IsNullOrEmpty(value) ? new StringName(value) : "";
    }

    private static bool ReadBool(GDictionary data, string key, bool fallback = false)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback;
        }
        return data[key].AsBool();
    }

    private static GArray ReadArray(GDictionary data, string key)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return new GArray();
        }
        return data[key].AsGodotArray();
    }

    private static GStringNameArray ToStringNameArray(IEnumerable<StringName> values)
    {
        var result = new GStringNameArray();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            StringName normalized = ProgressionDataUtils.to_string_name(value);
            if (normalized != "")
            {
                result.Add(normalized);
            }
        }
        return result;
    }
}
