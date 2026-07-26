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
        ContingencyTargetResolverKind kind =
            ContingencyContractRules.ToTargetResolverKind(type);
        if (kind == ContingencyTargetResolverKind.Unknown)
            return null;

        string[] expectedKeys = ContingencyContractRules.GetTargetResolverFields(kind);
        if (kind != ContingencyTargetResolverKind.EmptyCellNearOwner)
        {
            if (!ContingencySchemaUtils.HasExactKeys(payload, expectedKeys))
                return null;
            return new ContingencyTargetResolverState
            {
                ResolverKind = kind,
                Type = type,
            };
        }

        if (!ContingencySchemaUtils.HasExactKeys(payload, expectedKeys))
            return null;
        if (
            !ContingencySchemaUtils.TryReadStringName(
                payload,
                "preference",
                false,
                out StringName preference
            )
            || ContingencyContractRules.ToEmptyCellPreferenceKind(preference)
                == ContingencyEmptyCellPreferenceKind.Unknown
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

    internal static StringName ToStringName(ContingencyTargetResolverKind kind) =>
        ContingencyContractRules.ToTargetResolverType(kind);
}
