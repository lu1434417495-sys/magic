using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public enum ContingencyFallbackPolicyKind
{
    Unknown = 0,
    SkipIfInvalid,
    AbortRemainingIfInvalid,
}

public class ContingencyStoredSpellEntryState
{
    private static readonly string[] PayloadKeys =
    {
        "stored_skill_id",
        "cast_level",
        "order",
        "target_resolver",
        "parameter_bindings",
        "fallback_policy",
    };

    public StringName StoredSkillId { get; private set; } = "";
    public int CastLevel { get; private set; }
    public int Order { get; private set; }
    public ContingencyTargetResolverState TargetResolver { get; private set; }
    public GDictionary ParameterBindings { get; private set; } = new();
    public ContingencyFallbackPolicyKind FallbackPolicyKind { get; private set; } =
        ContingencyFallbackPolicyKind.Unknown;
    public StringName FallbackPolicy { get; private set; } = "";

    public ContingencyStoredSpellEntryState DuplicateState() => FromDictionary(ToDictionary());

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["stored_skill_id"] = StoredSkillId.ToString(),
            ["cast_level"] = CastLevel,
            ["order"] = Order,
            ["target_resolver"] = TargetResolver?.ToDictionary() ?? new GDictionary(),
            ["parameter_bindings"] = DuplicateParameterBindings(ParameterBindings),
            ["fallback_policy"] = FallbackPolicy.ToString(),
        };
    }

    public static ContingencyStoredSpellEntryState FromDictionary(GDictionary payload)
    {
        if (!ContingencySchemaUtils.HasExactKeys(payload, PayloadKeys))
            return null;
        if (
            !ContingencySchemaUtils.TryReadStringName(
                payload,
                "stored_skill_id",
                false,
                out StringName storedSkillId
            )
        )
            return null;
        if (!ContingencySchemaUtils.TryReadInt(payload, "cast_level", out int castLevel) || castLevel <= 0)
            return null;
        if (!ContingencySchemaUtils.TryReadInt(payload, "order", out int order) || order <= 0)
            return null;
        if (!ContingencySchemaUtils.TryReadDictionary(payload, "target_resolver", out GDictionary resolverPayload))
            return null;
        ContingencyTargetResolverState resolver =
            ContingencyTargetResolverState.FromDictionary(resolverPayload);
        if (resolver == null)
            return null;
        if (!ContingencySchemaUtils.TryReadDictionary(payload, "parameter_bindings", out GDictionary bindingsPayload))
            return null;
        GDictionary bindings = ParseParameterBindings(bindingsPayload);
        if (bindings == null)
            return null;
        if (
            !ContingencySchemaUtils.TryReadStringName(
                payload,
                "fallback_policy",
                false,
                out StringName fallbackPolicy
            )
        )
            return null;
        ContingencyFallbackPolicyKind fallbackKind = ToFallbackPolicyKind(fallbackPolicy);
        if (fallbackKind == ContingencyFallbackPolicyKind.Unknown)
            return null;

        return new ContingencyStoredSpellEntryState
        {
            StoredSkillId = storedSkillId,
            CastLevel = castLevel,
            Order = order,
            TargetResolver = resolver,
            ParameterBindings = bindings,
            FallbackPolicyKind = fallbackKind,
            FallbackPolicy = fallbackPolicy,
        };
    }

    internal static StringName ToStringName(ContingencyFallbackPolicyKind kind)
    {
        return kind switch
        {
            ContingencyFallbackPolicyKind.SkipIfInvalid => "skip_if_invalid",
            ContingencyFallbackPolicyKind.AbortRemainingIfInvalid => "abort_remaining_if_invalid",
            _ => new StringName(""),
        };
    }

    private static ContingencyFallbackPolicyKind ToFallbackPolicyKind(StringName policy)
    {
        if (policy == "skip_if_invalid")
            return ContingencyFallbackPolicyKind.SkipIfInvalid;
        if (policy == "abort_remaining_if_invalid")
            return ContingencyFallbackPolicyKind.AbortRemainingIfInvalid;
        return ContingencyFallbackPolicyKind.Unknown;
    }

    private static GDictionary ParseParameterBindings(GDictionary payload)
    {
        if (payload == null)
            return null;
        GDictionary result = new();
        foreach (Variant rawKey in payload.Keys)
        {
            if (!ContingencySchemaUtils.TryAsStringLike(rawKey, out string keyText))
                return null;
            StringName key = new(keyText);
            if (key == "" || result.ContainsKey(key.ToString()))
                return null;
            Variant value = payload[rawKey];
            if (!TryParseParameterBindingValue(value, out Variant parsedValue))
                return null;
            result[key] = parsedValue;
        }
        return result;
    }

    private static bool TryParseParameterBindingValue(Variant value, out Variant parsedValue)
    {
        parsedValue = default;
        switch (value.VariantType)
        {
            case Variant.Type.Bool:
            case Variant.Type.Int:
            case Variant.Type.Float:
            case Variant.Type.String:
            case Variant.Type.StringName:
                parsedValue = value;
                return true;
            case Variant.Type.Array:
                GArray rawArray = value.AsGodotArray();
                GArray normalizedArray = new();
                foreach (Variant rawItem in rawArray)
                {
                    if (!ContingencySchemaUtils.TryAsStringLike(rawItem, out string itemText))
                        return false;
                    StringName item = new(itemText);
                    if (item == "")
                        return false;
                    normalizedArray.Add(item);
                }
                parsedValue = Variant.From(normalizedArray);
                return true;
            default:
                return false;
        }
    }

    private static GDictionary DuplicateParameterBindings(GDictionary payload)
    {
        GDictionary parsed = ParseParameterBindings(payload ?? new GDictionary());
        return parsed?.Duplicate(true) ?? new GDictionary();
    }
}
