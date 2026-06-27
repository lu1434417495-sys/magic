using System.Collections.Generic;
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

    private readonly Dictionary<string, object> _parameterBindings =
        new(System.StringComparer.Ordinal);

    public StringName StoredSkillId { get; private set; } = "";
    public int CastLevel { get; private set; }
    public int Order { get; private set; }
    public ContingencyTargetResolverState TargetResolver { get; private set; }
    public GDictionary ParameterBindings =>
        RuntimePlainPayload.ProjectDictionary(
            _parameterBindings,
            "ContingencyStoredSpellEntryState.ParameterBindings"
        );
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
        Dictionary<string, object> bindings = ParseParameterBindings(bindingsPayload);
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

        var state = new ContingencyStoredSpellEntryState
        {
            StoredSkillId = storedSkillId,
            CastLevel = castLevel,
            Order = order,
            TargetResolver = resolver,
            FallbackPolicyKind = fallbackKind,
            FallbackPolicy = fallbackPolicy,
        };
        foreach (KeyValuePair<string, object> entry in bindings)
        {
            if (!string.IsNullOrEmpty(entry.Key))
                state._parameterBindings[entry.Key] = entry.Value;
        }
        return state;
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

    private static Dictionary<string, object> ParseParameterBindings(GDictionary payload)
    {
        if (payload == null)
            return null;
        var result = new Dictionary<string, object>(System.StringComparer.Ordinal);
        foreach (Variant rawKey in payload.Keys)
        {
            if (!ContingencySchemaUtils.TryAsStringLike(rawKey, out string keyText))
                return null;
            StringName key = new(keyText);
            string keyString = key.ToString();
            if (key == "" || result.ContainsKey(keyString))
                return null;
            Variant value = payload[rawKey];
            if (!TryParseParameterBindingValue(value, out object parsedValue))
                return null;
            result[keyString] = parsedValue;
        }
        return result;
    }

    private static bool TryParseParameterBindingValue(Variant value, out object parsedValue)
    {
        parsedValue = null;
        switch (value.VariantType)
        {
            case Variant.Type.Bool:
                parsedValue = value.AsBool();
                return true;
            case Variant.Type.Int:
                parsedValue = value.AsInt64();
                return true;
            case Variant.Type.Float:
                parsedValue = value.AsDouble();
                return true;
            case Variant.Type.String:
                parsedValue = value.AsString();
                return true;
            case Variant.Type.StringName:
                parsedValue = value.AsStringName();
                return true;
            case Variant.Type.Array:
                GArray rawArray = value.AsGodotArray();
                var normalizedArray = new List<object>();
                foreach (Variant rawItem in rawArray)
                {
                    if (!ContingencySchemaUtils.TryAsStringLike(rawItem, out string itemText))
                        return false;
                    StringName item = new(itemText);
                    if (item == "")
                        return false;
                    normalizedArray.Add(item);
                }
                parsedValue = normalizedArray;
                return true;
            default:
                return false;
        }
    }

    private static GDictionary DuplicateParameterBindings(GDictionary payload)
    {
        Dictionary<string, object> parsed = ParseParameterBindings(payload ?? new GDictionary());
        return RuntimePlainPayload.ProjectDictionary(
            parsed,
            "ContingencyStoredSpellEntryState.DuplicateParameterBindings"
        );
    }
}
