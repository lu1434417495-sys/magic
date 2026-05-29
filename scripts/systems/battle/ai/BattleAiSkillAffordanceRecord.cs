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
            normalizedSkillId = GdInterop.GetStringName(record, "skill_id");
        }
        return new BattleAiSkillAffordanceRecord
        {
            skill_id = normalizedSkillId,
            is_generatable = GdInterop.GetBool(record, "is_generatable"),
            skip_reason = GdInterop.GetString(record, "skip_reason"),
            team_intent = GdInterop.GetStringName(record, "team_intent"),
            target_mode = GdInterop.GetStringName(record, "target_mode"),
            target_filter = GdInterop.GetStringName(record, "target_filter"),
            selection_mode = GdInterop.GetStringName(record, "selection_mode"),
            effect_roles = DecodeStringNameArray(GdInterop.GetArray(record, "effect_roles")),
            affordances = DecodeStringNameArray(GdInterop.GetArray(record, "affordances")),
            action_families = DecodeStringNameArray(GdInterop.GetArray(record, "action_families")),
            requires_positioning_action = GdInterop.GetBool(
                record,
                "requires_positioning_action"
            ),
            variant_ids = DecodeStringNameArray(GdInterop.GetArray(record, "variant_ids")),
            blocked_reason = GdInterop.GetString(record, "blocked_reason"),
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
        foreach (string value in GdInterop.ReadStringItems(values))
        {
            StringName normalizedValue = new(value.StripEdges());
            if (normalizedValue != "")
            {
                result.Add(normalizedValue);
            }
        }
        return result;
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
