using System.Collections.Generic;
using Godot;

internal enum BattleAttackRollModifierStackMode
{
    Unknown = 0,
    Add,
    Exclusive,
    Max,
    Min,
}

internal enum BattleAttackRollModifierEndpointMode
{
    Unknown = 0,
    Either,
    Attacker,
    Target,
    Both,
}

internal enum BattleAttackRollModifierFootprintMode
{
    Unknown = 0,
    AnyCell,
}

internal enum BattleAttackRollModifierApplyTarget
{
    Unknown = 0,
    AttackRoll,
}

public class BattleAttackRollModifierSpec
{
    private static readonly StringName StackModeAdd = "add";
    private static readonly StringName StackModeExclusive = "exclusive";
    private static readonly StringName StackModeMax = "max";
    private static readonly StringName StackModeMin = "min";
    private static readonly StringName EndpointModeEither = "either";
    private static readonly StringName EndpointModeAttacker = "attacker";
    private static readonly StringName EndpointModeTarget = "target";
    private static readonly StringName EndpointModeBoth = "both";
    private static readonly StringName FootprintModeAnyCell = "any_cell";
    private static readonly StringName ApplyTargetAttackRoll = "attack_roll";

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
    public StringName applies_to { get; set; } = ApplyTargetAttackRoll;

    internal BattleAttackRollModifierStackMode StackModeKind
    {
        get => ToStackMode(stack_mode);
        set => stack_mode = ToStringName(value);
    }

    internal BattleAttackRollModifierEndpointMode EndpointModeKind
    {
        get => ToEndpointMode(endpoint_mode);
        set => endpoint_mode = ToStringName(value);
    }

    internal BattleAttackRollModifierFootprintMode FootprintModeKind
    {
        get => ToFootprintMode(footprint_mode);
        set => footprint_mode = ToStringName(value);
    }

    internal BattleAttackRollModifierApplyTarget AppliesToKind
    {
        get => ToApplyTarget(applies_to);
        set => applies_to = ToStringName(value);
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

    internal Dictionary<string, object> ToTraceDictionary()
    {
        return ToTraceDictionary(modifier_delta);
    }

    internal Dictionary<string, object> ToTraceDictionary(int effectiveModifierDelta)
    {
        return new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["source_domain"] = source_domain.ToString(),
            ["source_id"] = source_id.ToString(),
            ["source_instance_id"] = source_instance_id ?? "",
            ["label"] = label ?? "",
            ["modifier_delta"] = modifier_delta,
            ["effective_modifier_delta"] = effectiveModifierDelta,
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
        StringName stackModeName = new StringName(stackMode);
        if (ToStackMode(stackModeName) == BattleAttackRollModifierStackMode.Unknown)
            return null;
        if (!TryGetStringLike(payload, "roll_kind_filter", out string rollKindFilter))
            return null;
        if (!TryGetStringLike(payload, "endpoint_mode", out string endpointMode))
            return null;
        StringName endpointModeName = new StringName(endpointMode);
        if (ToEndpointMode(endpointModeName) == BattleAttackRollModifierEndpointMode.Unknown)
            return null;
        if (!TryGetStrictInt(payload, "distance_min_exclusive", out int distanceMinExclusive))
            return null;
        if (!TryGetStrictInt(payload, "distance_max_inclusive", out int distanceMaxInclusive))
            return null;
        if (!TryGetStringLike(payload, "target_team_filter", out string targetTeamFilterText))
            return null;
        StringName targetTeamFilter = new StringName(targetTeamFilterText);
        if (!CombatTargetTeamContentRules.IsValidSkillTargetTeamFilter(targetTeamFilter))
            return null;
        if (!TryGetStringLike(payload, "footprint_mode", out string footprintMode))
            return null;
        StringName footprintModeName = new StringName(footprintMode);
        if (ToFootprintMode(footprintModeName) == BattleAttackRollModifierFootprintMode.Unknown)
            return null;
        if (!TryGetStringLike(payload, "applies_to", out string appliesTo))
            return null;
        StringName appliesToName = new StringName(appliesTo);
        if (ToApplyTarget(appliesToName) == BattleAttackRollModifierApplyTarget.Unknown)
            return null;

        return new BattleAttackRollModifierSpec
        {
            source_domain = new StringName(sourceDomain),
            source_id = new StringName(sourceId),
            source_instance_id = sourceInstanceId,
            label = label,
            modifier_delta = modifierDelta,
            stack_key = new StringName(stackKey),
            stack_mode = stackModeName,
            roll_kind_filter = new StringName(rollKindFilter),
            endpoint_mode = endpointModeName,
            distance_min_exclusive = distanceMinExclusive,
            distance_max_inclusive = distanceMaxInclusive,
            target_team_filter = targetTeamFilter,
            footprint_mode = footprintModeName,
            applies_to = appliesToName,
        };
    }

    internal static BattleAttackRollModifierSpec FromPartialDictionary(Godot.Collections.Dictionary payload)
    {
        if (payload == null)
            return null;
        StringName targetTeamFilter = ReadStringNameWithDefault(payload, "target_team_filter", "any");
        if (!CombatTargetTeamContentRules.IsValidSkillTargetTeamFilter(targetTeamFilter))
        {
            return null;
        }
        StringName stackMode = ReadStringNameWithDefault(payload, "stack_mode", StackModeAdd);
        if (ToStackMode(stackMode) == BattleAttackRollModifierStackMode.Unknown)
        {
            return null;
        }
        StringName endpointMode = ReadStringNameWithDefault(
            payload,
            "endpoint_mode",
            EndpointModeEither
        );
        if (ToEndpointMode(endpointMode) == BattleAttackRollModifierEndpointMode.Unknown)
        {
            return null;
        }
        StringName footprintMode = ReadStringNameWithDefault(
            payload,
            "footprint_mode",
            FootprintModeAnyCell
        );
        if (ToFootprintMode(footprintMode) == BattleAttackRollModifierFootprintMode.Unknown)
        {
            return null;
        }
        StringName appliesTo = ReadStringNameWithDefault(
            payload,
            "applies_to",
            ApplyTargetAttackRoll
        );
        if (ToApplyTarget(appliesTo) == BattleAttackRollModifierApplyTarget.Unknown)
        {
            return null;
        }
        return new BattleAttackRollModifierSpec
        {
            source_domain = ReadStringNameWithDefault(payload, "source_domain", ""),
            source_id = ReadStringNameWithDefault(payload, "source_id", ""),
            source_instance_id = ReadStringWithDefault(payload, "source_instance_id", ""),
            label = ReadStringWithDefault(payload, "label", ""),
            modifier_delta = ReadIntWithDefault(payload, "modifier_delta", 0),
            stack_key = ReadStringNameWithDefault(payload, "stack_key", ""),
            stack_mode = stackMode,
            roll_kind_filter = ReadStringNameWithDefault(payload, "roll_kind_filter", ""),
            endpoint_mode = endpointMode,
            distance_min_exclusive = ReadIntWithDefault(payload, "distance_min_exclusive", -1),
            distance_max_inclusive = ReadIntWithDefault(payload, "distance_max_inclusive", -1),
            target_team_filter = targetTeamFilter,
            footprint_mode = footprintMode,
            applies_to = appliesTo,
        };
    }

    private static BattleAttackRollModifierStackMode ToStackMode(StringName value)
    {
        if (value == StackModeAdd)
            return BattleAttackRollModifierStackMode.Add;
        if (value == StackModeExclusive)
            return BattleAttackRollModifierStackMode.Exclusive;
        if (value == StackModeMax)
            return BattleAttackRollModifierStackMode.Max;
        if (value == StackModeMin)
            return BattleAttackRollModifierStackMode.Min;
        return BattleAttackRollModifierStackMode.Unknown;
    }

    private static StringName ToStringName(BattleAttackRollModifierStackMode mode)
    {
        return mode switch
        {
            BattleAttackRollModifierStackMode.Add => StackModeAdd,
            BattleAttackRollModifierStackMode.Exclusive => StackModeExclusive,
            BattleAttackRollModifierStackMode.Max => StackModeMax,
            BattleAttackRollModifierStackMode.Min => StackModeMin,
            _ => "",
        };
    }

    private static BattleAttackRollModifierEndpointMode ToEndpointMode(StringName value)
    {
        if (value == EndpointModeEither)
            return BattleAttackRollModifierEndpointMode.Either;
        if (value == EndpointModeAttacker)
            return BattleAttackRollModifierEndpointMode.Attacker;
        if (value == EndpointModeTarget)
            return BattleAttackRollModifierEndpointMode.Target;
        if (value == EndpointModeBoth)
            return BattleAttackRollModifierEndpointMode.Both;
        return BattleAttackRollModifierEndpointMode.Unknown;
    }

    private static StringName ToStringName(BattleAttackRollModifierEndpointMode mode)
    {
        return mode switch
        {
            BattleAttackRollModifierEndpointMode.Either => EndpointModeEither,
            BattleAttackRollModifierEndpointMode.Attacker => EndpointModeAttacker,
            BattleAttackRollModifierEndpointMode.Target => EndpointModeTarget,
            BattleAttackRollModifierEndpointMode.Both => EndpointModeBoth,
            _ => "",
        };
    }

    private static BattleAttackRollModifierFootprintMode ToFootprintMode(StringName value)
    {
        if (value == FootprintModeAnyCell)
            return BattleAttackRollModifierFootprintMode.AnyCell;
        return BattleAttackRollModifierFootprintMode.Unknown;
    }

    private static StringName ToStringName(BattleAttackRollModifierFootprintMode mode)
    {
        return mode switch
        {
            BattleAttackRollModifierFootprintMode.AnyCell => FootprintModeAnyCell,
            _ => "",
        };
    }

    private static BattleAttackRollModifierApplyTarget ToApplyTarget(StringName value)
    {
        if (value == ApplyTargetAttackRoll)
            return BattleAttackRollModifierApplyTarget.AttackRoll;
        return BattleAttackRollModifierApplyTarget.Unknown;
    }

    private static StringName ToStringName(BattleAttackRollModifierApplyTarget target)
    {
        return target switch
        {
            BattleAttackRollModifierApplyTarget.AttackRoll => ApplyTargetAttackRoll,
            _ => "",
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

    private static string ReadStringWithDefault(
        Godot.Collections.Dictionary data,
        string key,
        string fallback
    )
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return fallback;
        return data[key].ToString();
    }

    private static StringName ReadStringNameWithDefault(
        Godot.Collections.Dictionary data,
        string key,
        StringName fallback
    )
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return fallback;
        return ProgressionDataUtils.to_string_name(data[key]);
    }

    private static int ReadIntWithDefault(
        Godot.Collections.Dictionary data,
        string key,
        int fallback
    )
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

}
