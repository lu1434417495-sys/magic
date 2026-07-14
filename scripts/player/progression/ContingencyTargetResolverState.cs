using Godot;
using GDictionary = Godot.Collections.Dictionary;

public enum ContingencyTargetResolverKind
{
    Unknown = 0,
    Self,
    TriggerSource,
    TriggerTarget,
    NearestEnemyToOwner,
    NearestEnemyToTriggerCell,
    OwnerCenteredArea,
    AttackerCell,
    EmptyCellNearOwner,
}

public enum ContingencyEmptyCellPreferenceKind
{
    Unknown = 0,
    AwayFromTriggerSource,
    SafeCell,
}

public class ContingencyTargetResolverState
{
    public ContingencyTargetResolverKind ResolverKind { get; private set; } =
        ContingencyTargetResolverKind.Unknown;
    public StringName Type { get; private set; } = "";
    public StringName Preference { get; private set; } = "";
    public int MaxDistance { get; private set; }

    public ContingencyTargetResolverState DuplicateState()
    {
        return new ContingencyTargetResolverState
        {
            ResolverKind = ResolverKind,
            Type = Type,
            Preference = Preference,
            MaxDistance = MaxDistance,
        };
    }

    public GDictionary ToDictionary()
    {
        GDictionary payload = new() { ["type"] = Type.ToString() };
        if (ResolverKind == ContingencyTargetResolverKind.EmptyCellNearOwner)
        {
            payload["preference"] = Preference.ToString();
            payload["max_distance"] = MaxDistance;
        }
        return payload;
    }

    public static ContingencyTargetResolverState FromDictionary(GDictionary payload)
    {
        if (!ContingencySchemaUtils.TryReadStringName(payload, "type", false, out StringName type))
            return null;
        ContingencyTargetResolverKind kind = ToResolverKind(type);
        if (kind == ContingencyTargetResolverKind.Unknown)
            return null;

        if (kind != ContingencyTargetResolverKind.EmptyCellNearOwner)
        {
            if (!ContingencySchemaUtils.HasExactKeys(payload, new[] { "type" }))
                return null;
            return new ContingencyTargetResolverState
            {
                ResolverKind = kind,
                Type = type,
            };
        }

        if (!ContingencySchemaUtils.HasExactKeys(payload, new[] { "type", "preference", "max_distance" }))
            return null;
        if (
            !ContingencySchemaUtils.TryReadStringName(
                payload,
                "preference",
                false,
                out StringName preference
            )
            || ToEmptyCellPreferenceKind(preference) == ContingencyEmptyCellPreferenceKind.Unknown
        )
            return null;
        if (
            !ContingencySchemaUtils.TryReadInt(payload, "max_distance", out int maxDistance)
            || maxDistance < 1
            || maxDistance > 8
        )
            return null;

        return new ContingencyTargetResolverState
        {
            ResolverKind = kind,
            Type = type,
            Preference = preference,
            MaxDistance = maxDistance,
        };
    }

    internal static StringName ToStringName(ContingencyTargetResolverKind kind)
    {
        return kind switch
        {
            ContingencyTargetResolverKind.Self => "self",
            ContingencyTargetResolverKind.TriggerSource => "trigger_source",
            ContingencyTargetResolverKind.TriggerTarget => "trigger_target",
            ContingencyTargetResolverKind.NearestEnemyToOwner => "nearest_enemy_to_owner",
            ContingencyTargetResolverKind.NearestEnemyToTriggerCell =>
                "nearest_enemy_to_trigger_cell",
            ContingencyTargetResolverKind.OwnerCenteredArea => "owner_centered_area",
            ContingencyTargetResolverKind.AttackerCell => "attacker_cell",
            ContingencyTargetResolverKind.EmptyCellNearOwner => "empty_cell_near_owner",
            _ => new StringName(""),
        };
    }

    private static ContingencyTargetResolverKind ToResolverKind(StringName type)
    {
        if (type == "self")
            return ContingencyTargetResolverKind.Self;
        if (type == "trigger_source")
            return ContingencyTargetResolverKind.TriggerSource;
        if (type == "trigger_target")
            return ContingencyTargetResolverKind.TriggerTarget;
        if (type == "nearest_enemy_to_owner")
            return ContingencyTargetResolverKind.NearestEnemyToOwner;
        if (type == "nearest_enemy_to_trigger_cell")
            return ContingencyTargetResolverKind.NearestEnemyToTriggerCell;
        if (type == "owner_centered_area")
            return ContingencyTargetResolverKind.OwnerCenteredArea;
        if (type == "attacker_cell")
            return ContingencyTargetResolverKind.AttackerCell;
        if (type == "empty_cell_near_owner")
            return ContingencyTargetResolverKind.EmptyCellNearOwner;
        return ContingencyTargetResolverKind.Unknown;
    }

    private static ContingencyEmptyCellPreferenceKind ToEmptyCellPreferenceKind(
        StringName preference
    )
    {
        if (preference == "away_from_trigger_source")
            return ContingencyEmptyCellPreferenceKind.AwayFromTriggerSource;
        if (preference == "safe_cell")
            return ContingencyEmptyCellPreferenceKind.SafeCell;
        return ContingencyEmptyCellPreferenceKind.Unknown;
    }
}
