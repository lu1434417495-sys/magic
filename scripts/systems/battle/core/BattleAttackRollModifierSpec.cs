using Godot;

public class BattleAttackRollModifierSpec
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

    internal Godot.Collections.Dictionary ToDictionary()
    {
        return BuildDictionary(modifier_delta);
    }

    internal Godot.Collections.Dictionary ToDictionaryWithEffectiveModifierDelta(int effective_modifier_delta)
    {
        return BuildDictionary(effective_modifier_delta);
    }

    public BattleAttackRollModifierSpec Clone()
    {
        return new BattleAttackRollModifierSpec
        {
            source_domain = source_domain,
            source_id = source_id,
            source_instance_id = source_instance_id,
            label = label,
            modifier_delta = modifier_delta,
            stack_key = stack_key,
            stack_mode = stack_mode,
            roll_kind_filter = roll_kind_filter,
            endpoint_mode = endpoint_mode,
            distance_min_exclusive = distance_min_exclusive,
            distance_max_inclusive = distance_max_inclusive,
            target_team_filter = target_team_filter,
            footprint_mode = footprint_mode,
            applies_to = applies_to,
        };
    }

    private Godot.Collections.Dictionary BuildDictionary(int effectiveDelta)
    {
        return new Godot.Collections.Dictionary
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

    internal static BattleAttackRollModifierSpec FromDictionary(Godot.Collections.Dictionary payload)
    {
        if (payload == null)
            return null;
        if (!HasExactSchema(payload))
        {
            return null;
        }
        if (!TryGetStringLike(payload, "source_domain", out string sourceDomain))
            return null;
        if (!TryGetStringLike(payload, "source_id", out string sourceId))
            return null;
        if (!TryGetStringLike(payload, "source_instance_id", out string sourceInstanceId))
            return null;
        if (!TryGetStrictString(payload, "label", out string label))
            return null;
        if (!TryGetStrictInt(payload, "modifier_delta", out int modifierDelta))
            return null;
        if (!TryGetStringLike(payload, "stack_key", out string stackKey))
            return null;
        if (!TryGetStringLike(payload, "stack_mode", out string stackMode))
            return null;
        if (!TryGetStringLike(payload, "roll_kind_filter", out string rollKindFilter))
            return null;
        if (!TryGetStringLike(payload, "endpoint_mode", out string endpointMode))
            return null;
        if (!TryGetStrictInt(payload, "distance_min_exclusive", out int distanceMinExclusive))
            return null;
        if (!TryGetStrictInt(payload, "distance_max_inclusive", out int distanceMaxInclusive))
            return null;
        if (!TryGetStringLike(payload, "target_team_filter", out string targetTeamFilterText))
            return null;
        StringName targetTeamFilter = new StringName(targetTeamFilterText);
        if (!CombatTargetTeamContentRules.is_valid_skill_target_team_filter(targetTeamFilter))
            return null;
        if (!TryGetStringLike(payload, "footprint_mode", out string footprintMode))
            return null;
        if (!TryGetStringLike(payload, "applies_to", out string appliesTo))
            return null;

        return new BattleAttackRollModifierSpec
        {
            source_domain = new StringName(sourceDomain),
            source_id = new StringName(sourceId),
            source_instance_id = sourceInstanceId,
            label = label,
            modifier_delta = modifierDelta,
            stack_key = new StringName(stackKey),
            stack_mode = new StringName(stackMode),
            roll_kind_filter = new StringName(rollKindFilter),
            endpoint_mode = new StringName(endpointMode),
            distance_min_exclusive = distanceMinExclusive,
            distance_max_inclusive = distanceMaxInclusive,
            target_team_filter = targetTeamFilter,
            footprint_mode = new StringName(footprintMode),
            applies_to = new StringName(appliesTo),
        };
    }

    internal static BattleAttackRollModifierSpec FromPartialDictionary(Godot.Collections.Dictionary payload)
    {
        if (payload == null)
            return null;
        StringName targetTeamFilter = ProgressionDataUtils.to_string_name(
            payload.GetValueOrDefault("target_team_filter", "any")
        );
        if (!CombatTargetTeamContentRules.is_valid_skill_target_team_filter(targetTeamFilter))
        {
            return null;
        }
        return new BattleAttackRollModifierSpec
        {
            source_domain = ProgressionDataUtils.to_string_name(payload.GetValueOrDefault("source_domain", "")),
            source_id = ProgressionDataUtils.to_string_name(payload.GetValueOrDefault("source_id", "")),
            source_instance_id = payload.GetValueOrDefault("source_instance_id", "").AsString(),
            label = payload.GetValueOrDefault("label", "").AsString(),
            modifier_delta = payload.GetValueOrDefault("modifier_delta", 0).AsInt32(),
            stack_key = ProgressionDataUtils.to_string_name(payload.GetValueOrDefault("stack_key", "")),
            stack_mode = ProgressionDataUtils.to_string_name(payload.GetValueOrDefault("stack_mode", "add")),
            roll_kind_filter = ProgressionDataUtils.to_string_name(
                payload.GetValueOrDefault("roll_kind_filter", "")
            ),
            endpoint_mode = ProgressionDataUtils.to_string_name(
                payload.GetValueOrDefault("endpoint_mode", "either")
            ),
            distance_min_exclusive = payload.GetValueOrDefault("distance_min_exclusive", -1).AsInt32(),
            distance_max_inclusive = payload.GetValueOrDefault("distance_max_inclusive", -1).AsInt32(),
            target_team_filter = targetTeamFilter,
            footprint_mode = ProgressionDataUtils.to_string_name(
                payload.GetValueOrDefault("footprint_mode", "any_cell")
            ),
            applies_to = ProgressionDataUtils.to_string_name(
                payload.GetValueOrDefault("applies_to", "attack_roll")
            ),
        };
    }

    private static bool HasExactSchema(Godot.Collections.Dictionary payload)
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

    private static bool TryGetStringLike(Godot.Collections.Dictionary data, string key, out string value)
    {
        if (data != null && data.ContainsKey(key))
        {
            value = ProgressionDataUtils.to_string_name(data[key]).ToString();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetStrictString(Godot.Collections.Dictionary data, string key, out string value)
    {
        if (data != null && data.ContainsKey(key))
        {
            value = data[key].AsString();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetStrictInt(Godot.Collections.Dictionary data, string key, out int value)
    {
        if (data != null && data.ContainsKey(key))
        {
            value = data[key].AsInt32();
            return true;
        }
        value = 0;
        return false;
    }

}
