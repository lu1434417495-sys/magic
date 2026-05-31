using Godot;

[GlobalClass]
public partial class PendingProfessionChoice : RefCounted
{
    public Godot.Collections.Array<StringName> trigger_skill_ids = new();
    public Godot.Collections.Array<StringName> candidate_profession_ids = new();
    public Godot.Collections.Dictionary target_rank_map = new();
    public Godot.Collections.Array<StringName> qualifier_skill_pool_ids = new();
    public Godot.Collections.Array<StringName> assignable_skill_candidate_ids = new();
    public int required_qualifier_count;
    public int required_assigned_core_count;

    public void set_target_rank(StringName profession_id, int target_rank) =>
        target_rank_map[profession_id] = target_rank;

    public Godot.Collections.Dictionary to_dict() =>
        new()
        {
            {
                "trigger_skill_ids",
                ProgressionDataUtils.string_name_array_to_string_array(trigger_skill_ids)
            },
            {
                "candidate_profession_ids",
                ProgressionDataUtils.string_name_array_to_string_array(candidate_profession_ids)
            },
            {
                "target_rank_map",
                ProgressionDataUtils.string_name_int_map_to_string_dict(target_rank_map)
            },
            {
                "qualifier_skill_pool_ids",
                ProgressionDataUtils.string_name_array_to_string_array(qualifier_skill_pool_ids)
            },
            {
                "assignable_skill_candidate_ids",
                ProgressionDataUtils.string_name_array_to_string_array(
                    assignable_skill_candidate_ids
                )
            },
            { "required_qualifier_count", required_qualifier_count },
            { "required_assigned_core_count", required_assigned_core_count },
        };

    public static PendingProfessionChoice from_dict(Godot.Collections.Dictionary data)
    {
        if (
            !HasExactFields(
                data,
                new Godot.Collections.Array<string>
                {
                    "trigger_skill_ids",
                    "candidate_profession_ids",
                    "target_rank_map",
                    "qualifier_skill_pool_ids",
                    "assignable_skill_candidate_ids",
                    "required_qualifier_count",
                    "required_assigned_core_count",
                }
            )
        )
            return null;
        if (data["trigger_skill_ids"].VariantType != Variant.Type.Array)
            return null;
        if (data["candidate_profession_ids"].VariantType != Variant.Type.Array)
            return null;
        if (data["target_rank_map"].VariantType != Variant.Type.Dictionary)
            return null;
        if (data["qualifier_skill_pool_ids"].VariantType != Variant.Type.Array)
            return null;
        if (data["assignable_skill_candidate_ids"].VariantType != Variant.Type.Array)
            return null;
        var tsi = ParseUniqueStringNameArray(data["trigger_skill_ids"].AsGodotArray());
        if (tsi == null)
            return null;
        var cpi = ParseUniqueStringNameArray(data["candidate_profession_ids"].AsGodotArray());
        if (cpi == null)
            return null;
        var trm = ParseStringNameIntMap(data["target_rank_map"].AsGodotDictionary());
        if (trm == null)
            return null;
        var qsi = ParseUniqueStringNameArray(data["qualifier_skill_pool_ids"].AsGodotArray());
        if (qsi == null)
            return null;
        var asi = ParseUniqueStringNameArray(
            data["assignable_skill_candidate_ids"].AsGodotArray()
        );
        if (asi == null)
            return null;
        if (
            data["required_qualifier_count"].VariantType != Variant.Type.Int
            || data["required_qualifier_count"].AsInt32() < 0
        )
            return null;
        if (
            data["required_assigned_core_count"].VariantType != Variant.Type.Int
            || data["required_assigned_core_count"].AsInt32() < 0
        )
            return null;
        return new PendingProfessionChoice
        {
            trigger_skill_ids = tsi,
            candidate_profession_ids = cpi,
            target_rank_map = trm,
            qualifier_skill_pool_ids = qsi,
            assignable_skill_candidate_ids = asi,
            required_qualifier_count = data["required_qualifier_count"].AsInt32(),
            required_assigned_core_count = data["required_assigned_core_count"].AsInt32(),
        };
    }

    private static bool HasExactFields(
        Godot.Collections.Dictionary data,
        Godot.Collections.Array<string> fields
    )
    {
        if (data.Count != fields.Count)
            return false;
        foreach (string fieldName in fields)
            if (!data.ContainsKey(fieldName))
                return false;
        return true;
    }

    private static Godot.Collections.Array<StringName> ParseUniqueStringNameArray(
        Godot.Collections.Array values
    )
    {
        var results = new Godot.Collections.Array<StringName>();
        var seen = new Godot.Collections.Dictionary();
        foreach (var rawValue in values)
        {
            var parsed = ParseRequiredStringName(rawValue);
            if (parsed == null || seen.ContainsKey(parsed))
                return null;
            seen[parsed] = true;
            results.Add(parsed);
        }
        return results;
    }

    private static Godot.Collections.Dictionary ParseStringNameIntMap(
        Godot.Collections.Dictionary values
    )
    {
        var parsedValues = new Godot.Collections.Dictionary();
        var seen = new Godot.Collections.Dictionary();
        foreach (var rawKey in values.Keys)
        {
            var parsedKey = ParseRequiredStringName(rawKey);
            if (parsedKey == null || seen.ContainsKey(parsedKey))
                return null;
            var rawValue = values[rawKey];
            if (rawValue.VariantType != Variant.Type.Int || rawValue.AsInt32() < 0)
                return null;
            seen[parsedKey] = true;
            parsedValues[parsedKey] = rawValue.AsInt32();
        }
        return parsedValues;
    }

    private static StringName ParseRequiredStringName(object rawValue)
    {
        if (rawValue is Variant value)
        {
            var vt = value.VariantType;
            if (vt != Variant.Type.String && vt != Variant.Type.StringName)
                return null;
        }
        else if (rawValue is not string && rawValue is not StringName)
        {
            return null;
        }
        var p = ProgressionDataUtils.to_string_name(rawValue);
        return (string)p == "" ? null : p;
    }
}
