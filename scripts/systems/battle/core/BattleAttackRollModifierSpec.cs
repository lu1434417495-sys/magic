using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAttackRollModifierSpec : RefCounted
{
    private static readonly string[] SchemaKeys =
    {
        "source_domain",
        "source_id",
        "source_instance_id",
        "label",
        "modifier_delta",
        "stack_key",
        "stack_mode",
        "roll_kind_filter",
        "endpoint_mode",
        "distance_min_exclusive",
        "distance_max_inclusive",
        "target_team_filter",
        "footprint_mode",
        "applies_to",
    };

    public StringName source_domain { get; set; } = "";
    public StringName source_id { get; set; } = "";
    public string source_instance_id { get; set; } = "";
    public string label { get; set; } = "";
    public int modifier_delta { get; set; }
    public StringName stack_key { get; set; } = "";
    public StringName stack_mode { get; set; } = "add";
    public StringName roll_kind_filter { get; set; } = "";
    public StringName endpoint_mode { get; set; } = "either";
    public int distance_min_exclusive { get; set; } = -1;
    public int distance_max_inclusive { get; set; } = -1;
    public StringName target_team_filter { get; set; } = "any";
    public StringName footprint_mode { get; set; } = "any_cell";
    public StringName applies_to { get; set; } = "attack_roll";

    public GDictionary to_dict()
    {
        return BuildDictionary(modifier_delta);
    }

    public GDictionary to_dict_with_effective_modifier_delta(int effective_modifier_delta)
    {
        return BuildDictionary(effective_modifier_delta);
    }

    private GDictionary BuildDictionary(int effectiveDelta)
    {
        return new GDictionary
        {
            ["source_domain"] = source_domain.ToString(),
            ["source_id"] = source_id.ToString(),
            ["source_instance_id"] = source_instance_id,
            ["label"] = label,
            ["modifier_delta"] = modifier_delta,
            ["effective_modifier_delta"] = effectiveDelta,
            ["stack_key"] = stack_key.ToString(),
            ["stack_mode"] = stack_mode.ToString(),
            ["roll_kind_filter"] = roll_kind_filter.ToString(),
            ["endpoint_mode"] = endpoint_mode.ToString(),
            ["distance_min_exclusive"] = distance_min_exclusive,
            ["distance_max_inclusive"] = distance_max_inclusive,
            ["target_team_filter"] = target_team_filter.ToString(),
            ["footprint_mode"] = footprint_mode.ToString(),
            ["applies_to"] = applies_to.ToString(),
        };
    }

    public static BattleAttackRollModifierSpec from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        GDictionary payload = data.AsGodotDictionary();
        if (!HasExactSchema(payload))
        {
            return null;
        }
        if (!IsStringLike(Get(payload, "source_domain")))
            return null;
        if (!IsStringLike(Get(payload, "source_id")))
            return null;
        if (!IsStringLike(Get(payload, "source_instance_id")))
            return null;
        if (Get(payload, "label").VariantType != Variant.Type.String)
            return null;
        if (Get(payload, "modifier_delta").VariantType != Variant.Type.Int)
            return null;
        if (!IsStringLike(Get(payload, "stack_key")))
            return null;
        if (!IsStringLike(Get(payload, "stack_mode")))
            return null;
        if (!IsStringLike(Get(payload, "roll_kind_filter")))
            return null;
        if (!IsStringLike(Get(payload, "endpoint_mode")))
            return null;
        if (Get(payload, "distance_min_exclusive").VariantType != Variant.Type.Int)
            return null;
        if (Get(payload, "distance_max_inclusive").VariantType != Variant.Type.Int)
            return null;
        if (!IsStringLike(Get(payload, "target_team_filter")))
            return null;
        StringName targetTeamFilter = ToStringName(Get(payload, "target_team_filter"));
        if (!IsValidSkillTargetTeamFilter(targetTeamFilter))
            return null;
        if (!IsStringLike(Get(payload, "footprint_mode")))
            return null;
        if (!IsStringLike(Get(payload, "applies_to")))
            return null;

        return new BattleAttackRollModifierSpec
        {
            source_domain = ToStringName(Get(payload, "source_domain")),
            source_id = ToStringName(Get(payload, "source_id")),
            source_instance_id = Get(payload, "source_instance_id").AsString(),
            label = Get(payload, "label").AsString(),
            modifier_delta = Get(payload, "modifier_delta").AsInt32(),
            stack_key = ToStringName(Get(payload, "stack_key")),
            stack_mode = ToStringName(Get(payload, "stack_mode")),
            roll_kind_filter = ToStringName(Get(payload, "roll_kind_filter")),
            endpoint_mode = ToStringName(Get(payload, "endpoint_mode")),
            distance_min_exclusive = Get(payload, "distance_min_exclusive").AsInt32(),
            distance_max_inclusive = Get(payload, "distance_max_inclusive").AsInt32(),
            target_team_filter = targetTeamFilter,
            footprint_mode = ToStringName(Get(payload, "footprint_mode")),
            applies_to = ToStringName(Get(payload, "applies_to")),
        };
    }

    public static BattleAttackRollModifierSpec from_partial_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        GDictionary payload = data.AsGodotDictionary();
        StringName targetTeamFilter = ToStringName(Get(payload, "target_team_filter", "any"));
        if (!IsValidSkillTargetTeamFilter(targetTeamFilter))
        {
            return null;
        }
        return new BattleAttackRollModifierSpec
        {
            source_domain = ToStringName(Get(payload, "source_domain", "")),
            source_id = ToStringName(Get(payload, "source_id", "")),
            source_instance_id = Get(payload, "source_instance_id", "").AsString(),
            label = Get(payload, "label", "").AsString(),
            modifier_delta = Get(payload, "modifier_delta", 0).AsInt32(),
            stack_key = ToStringName(Get(payload, "stack_key", "")),
            stack_mode = ToStringName(Get(payload, "stack_mode", "add")),
            roll_kind_filter = ToStringName(Get(payload, "roll_kind_filter", "")),
            endpoint_mode = ToStringName(Get(payload, "endpoint_mode", "either")),
            distance_min_exclusive = Get(payload, "distance_min_exclusive", -1).AsInt32(),
            distance_max_inclusive = Get(payload, "distance_max_inclusive", -1).AsInt32(),
            target_team_filter = targetTeamFilter,
            footprint_mode = ToStringName(Get(payload, "footprint_mode", "any_cell")),
            applies_to = ToStringName(Get(payload, "applies_to", "attack_roll")),
        };
    }

    private static bool HasExactSchema(GDictionary payload)
    {
        if (payload.Count != SchemaKeys.Length)
        {
            return false;
        }
        foreach (string key in SchemaKeys)
        {
            if (!payload.ContainsKey(key))
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

    private static StringName ToStringName(Variant value)
    {
        return IsStringLike(value) ? new StringName(value.AsString()) : "";
    }

    private static bool IsValidSkillTargetTeamFilter(StringName value)
    {
        return value == "enemy" || value == "ally" || value == "self" || value == "any";
    }

    private static Variant Get(GDictionary payload, string key, Variant fallback = default)
    {
        return payload.ContainsKey(key) ? payload[key] : fallback;
    }
}
