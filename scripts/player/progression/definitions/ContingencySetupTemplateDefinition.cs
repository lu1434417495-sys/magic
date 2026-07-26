using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public sealed class ContingencyTriggerDefinition
{
    public ContingencyTriggerDefinition(
        StringName type,
        StringName subject,
        StringName timing,
        int percent,
        bool crossingOnly,
        int damagePercent,
        StringName damageBasis,
        StringName damageAmountMode,
        StringName center,
        int radius,
        StringName radiusMetric,
        StringName sourceTeam,
        IReadOnlyList<StringName> statusTags,
        StringName applicationMatch,
        StringName spellMatch
    )
    {
        Type = type;
        Subject = subject;
        Timing = timing;
        Percent = percent;
        CrossingOnly = crossingOnly;
        DamagePercent = damagePercent;
        DamageBasis = damageBasis;
        DamageAmountMode = damageAmountMode;
        Center = center;
        Radius = radius;
        RadiusMetric = radiusMetric;
        SourceTeam = sourceTeam;
        StatusTags = ProgressionDefinitionProjection.FreezeValues(
            statusTags,
            "ContingencyTriggerDefinition.StatusTags"
        );
        ApplicationMatch = applicationMatch;
        SpellMatch = spellMatch;
    }

    public StringName Type { get; }
    public StringName Subject { get; }
    public StringName Timing { get; }
    public int Percent { get; }
    public bool CrossingOnly { get; }
    public int DamagePercent { get; }
    public StringName DamageBasis { get; }
    public StringName DamageAmountMode { get; }
    public StringName Center { get; }
    public int Radius { get; }
    public StringName RadiusMetric { get; }
    public StringName SourceTeam { get; }
    public IReadOnlyList<StringName> StatusTags { get; }
    public StringName ApplicationMatch { get; }
    public StringName SpellMatch { get; }

    public ContingencyTriggerKind TriggerKind =>
        ContingencyContractRules.ToTriggerKind(Type);

    public ContingencyTimingKind TimingKind =>
        ContingencyContractRules.ToTimingKind(Timing);

    internal static ContingencyTriggerDefinition FromAuthoring(
        GDictionary source,
        string path
    )
    {
        Dictionary<string, Variant> values = ContingencyDefinitionProjection.NormalizeKeys(
            source,
            path
        );
        StringName type = ContingencyDefinitionProjection.ReadRequiredStringName(
            values,
            "type",
            path
        );

        string[] expectedKeys = ContingencyContractRules.GetTriggerFields(type);
        if (expectedKeys == null)
        {
            throw ContingencyDefinitionProjection.Invalid(
                path + ".type",
                $"unsupported trigger type '{type}'"
            );
        }
        ContingencyDefinitionProjection.RequireExactKeys(values, expectedKeys, path);

        return new ContingencyTriggerDefinition(
            type,
            ContingencyDefinitionProjection.ReadOptionalStringName(values, "subject", path),
            ContingencyDefinitionProjection.ReadRequiredStringName(values, "timing", path),
            ContingencyDefinitionProjection.ReadOptionalInt(values, "percent", path),
            ContingencyDefinitionProjection.ReadOptionalBool(values, "crossing_only", path),
            ContingencyDefinitionProjection.ReadOptionalInt(values, "damage_percent", path),
            ContingencyDefinitionProjection.ReadOptionalStringName(values, "damage_basis", path),
            ContingencyDefinitionProjection.ReadOptionalStringName(
                values,
                "damage_amount_mode",
                path
            ),
            ContingencyDefinitionProjection.ReadOptionalStringName(values, "center", path),
            ContingencyDefinitionProjection.ReadOptionalInt(values, "radius", path),
            ContingencyDefinitionProjection.ReadOptionalStringName(values, "radius_metric", path),
            ContingencyDefinitionProjection.ReadOptionalStringName(values, "source_team", path),
            ContingencyDefinitionProjection.ReadOptionalStringNameList(values, "status_tags", path),
            ContingencyDefinitionProjection.ReadOptionalStringName(
                values,
                "application_match",
                path
            ),
            ContingencyDefinitionProjection.ReadOptionalStringName(values, "spell_match", path)
        );
    }
}

public sealed record ContingencyTargetResolverDefinition(
    StringName Type,
    StringName Preference,
    int MaxDistance
)
{
    public ContingencyTargetResolverKind ResolverKind =>
        ContingencyContractRules.ToTargetResolverKind(Type);

    internal static ContingencyTargetResolverDefinition FromAuthoring(
        GDictionary source,
        string path
    )
    {
        Dictionary<string, Variant> values = ContingencyDefinitionProjection.NormalizeKeys(
            source,
            path
        );
        StringName type = ContingencyDefinitionProjection.ReadRequiredStringName(
            values,
            "type",
            path
        );
        ContingencyTargetResolverKind resolverKind =
            ContingencyContractRules.ToTargetResolverKind(type);
        if (resolverKind == ContingencyTargetResolverKind.Unknown)
        {
            throw ContingencyDefinitionProjection.Invalid(
                path + ".type",
                $"unsupported target resolver '{type}'"
            );
        }
        ContingencyDefinitionProjection.RequireExactKeys(
            values,
            ContingencyContractRules.GetTargetResolverFields(resolverKind),
            path
        );

        return new ContingencyTargetResolverDefinition(
            type,
            ContingencyDefinitionProjection.ReadOptionalStringName(values, "preference", path),
            ContingencyDefinitionProjection.ReadOptionalInt(values, "max_distance", path)
        );
    }
}

public sealed class ContingencyStoredSpellTemplateDefinition
{
    public ContingencyStoredSpellTemplateDefinition(
        StringName storedSkillId,
        int configuredMaxCastLevel,
        int order,
        ContingencyTargetResolverDefinition targetResolver,
        IReadOnlyDictionary<string, object> parameterBindings,
        StringName fallbackPolicy
    )
    {
        StoredSkillId = storedSkillId;
        ConfiguredMaxCastLevel = configuredMaxCastLevel;
        MaxCastLevel = Math.Max(configuredMaxCastLevel, 1);
        Order = order;
        TargetResolver = targetResolver
            ?? throw new InvalidDataException(
                "ContingencyStoredSpellTemplateDefinition.TargetResolver must not be null."
            );
        ParameterBindings = ContingencyDefinitionProjection.FreezePlainDictionary(
            parameterBindings,
            "ContingencyStoredSpellTemplateDefinition.ParameterBindings"
        );
        FallbackPolicy = fallbackPolicy;
    }

    public StringName StoredSkillId { get; }
    public int ConfiguredMaxCastLevel { get; }
    public int MaxCastLevel { get; }
    public int Order { get; }
    public ContingencyTargetResolverDefinition TargetResolver { get; }
    public IReadOnlyDictionary<string, object> ParameterBindings { get; }
    public StringName FallbackPolicy { get; }

    public ContingencyFallbackPolicyKind FallbackPolicyKind => FallbackPolicy switch
    {
        var value when value == "skip_if_invalid" => ContingencyFallbackPolicyKind.SkipIfInvalid,
        var value when value == "abort_remaining_if_invalid" =>
            ContingencyFallbackPolicyKind.AbortRemainingIfInvalid,
        _ => ContingencyFallbackPolicyKind.Unknown,
    };

    internal static ContingencyStoredSpellTemplateDefinition FromAuthoring(
        GDictionary source,
        string path
    )
    {
        Dictionary<string, Variant> values = ContingencyDefinitionProjection.NormalizeKeys(
            source,
            path
        );
        ContingencyDefinitionProjection.RequireExactKeys(
            values,
            new[]
            {
                "stored_skill_id",
                "max_cast_level",
                "order",
                "target_resolver",
                "parameter_bindings",
                "fallback_policy",
            },
            path
        );

        using GDictionary targetResolver = ContingencyDefinitionProjection.ReadRequiredDictionary(
            values,
            "target_resolver",
            path
        );
        using GDictionary parameterBindings = ContingencyDefinitionProjection.ReadRequiredDictionary(
            values,
            "parameter_bindings",
            path
        );
        StringName fallbackPolicy = ContingencyDefinitionProjection.ReadRequiredStringName(
            values,
            "fallback_policy",
            path
        );
        var result = new ContingencyStoredSpellTemplateDefinition(
            ContingencyDefinitionProjection.ReadRequiredStringName(
                values,
                "stored_skill_id",
                path
            ),
            ContingencyDefinitionProjection.ReadRequiredInt(values, "max_cast_level", path),
            ContingencyDefinitionProjection.ReadRequiredInt(values, "order", path),
            ContingencyTargetResolverDefinition.FromAuthoring(
                targetResolver,
                path + ".target_resolver"
            ),
            ContingencyDefinitionProjection.ProjectParameterBindings(
                parameterBindings,
                path + ".parameter_bindings"
            ),
            fallbackPolicy
        );
        if (result.FallbackPolicyKind == ContingencyFallbackPolicyKind.Unknown)
        {
            throw ContingencyDefinitionProjection.Invalid(
                path + ".fallback_policy",
                $"unsupported fallback policy '{fallbackPolicy}'"
            );
        }
        return result;
    }
}

public sealed class ContingencySetupTemplateDefinition
{
    public ContingencySetupTemplateDefinition(
        StringName templateId,
        string displayName,
        StringName sourceSkillId,
        int matrixLoad,
        StringName releaseMode,
        ContingencyTriggerDefinition trigger,
        IReadOnlyList<ContingencyStoredSpellTemplateDefinition> storedSpells
    )
    {
        TemplateId = templateId;
        DisplayName = displayName
            ?? throw new InvalidDataException(
                "ContingencySetupTemplateDefinition.DisplayName must not be null."
            );
        SourceSkillId = sourceSkillId;
        MatrixLoad = matrixLoad;
        ReleaseMode = releaseMode;
        Trigger = trigger
            ?? throw new InvalidDataException(
                "ContingencySetupTemplateDefinition.Trigger must not be null."
            );
        StoredSpells = ProgressionDefinitionProjection.FreezeValues(
            storedSpells,
            "ContingencySetupTemplateDefinition.StoredSpells"
        );
    }

    public StringName TemplateId { get; }
    public string DisplayName { get; }
    public StringName SourceSkillId { get; }
    public int MatrixLoad { get; }
    public StringName ReleaseMode { get; }
    public ContingencyTriggerDefinition Trigger { get; }
    public IReadOnlyList<ContingencyStoredSpellTemplateDefinition> StoredSpells { get; }

    internal static ContingencySetupTemplateDefinition FromResource(
        ContingencySetupTemplateDef source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.StoredSpellsProjectionBorrowed == null)
            throw ContingencyDefinitionProjection.Invalid(path + ".stored_spells", "collection is null");
        var storedSpells = new List<ContingencyStoredSpellTemplateDefinition>(
            source.StoredSpellsProjectionBorrowed.Count
        );
        for (int index = 0; index < source.StoredSpellsProjectionBorrowed.Count; index++)
        {
            storedSpells.Add(
                ContingencyStoredSpellTemplateDefinition.FromAuthoring(
                    source.StoredSpellsProjectionBorrowed[index],
                    $"{path}.stored_spells[{index}]"
                )
            );
        }

        return new ContingencySetupTemplateDefinition(
            source.template_id,
            source.display_name,
            source.source_skill_id,
            source.matrix_load,
            source.release_mode,
            ContingencyTriggerDefinition.FromAuthoring(
                source.TriggerProjectionBorrowed,
                path + ".trigger"
            ),
            new ReadOnlyCollection<ContingencyStoredSpellTemplateDefinition>(storedSpells)
        );
    }
}

internal static class ContingencyDefinitionProjection
{
    internal static Dictionary<string, Variant> NormalizeKeys(GDictionary source, string path)
    {
        if (source == null)
            throw Invalid(path, "dictionary is null");
        var result = new Dictionary<string, Variant>(StringComparer.Ordinal);
        int index = 0;
        foreach (Variant rawKey in source.Keys)
        {
            string key = rawKey.VariantType switch
            {
                Variant.Type.String => rawKey.AsString(),
                Variant.Type.StringName => rawKey.AsStringName().ToString(),
                _ => throw Invalid(
                    $"{path}[key:{index}]",
                    $"key must be String or StringName, got {rawKey.VariantType}"
                ),
            };
            if (string.IsNullOrEmpty(key))
                throw Invalid($"{path}[key:{index}]", "key must not be empty");
            if (!result.TryAdd(key, source[rawKey]))
                throw Invalid(path + "." + key, "duplicate normalized key");
            index++;
        }
        return result;
    }

    internal static void RequireExactKeys(
        IReadOnlyDictionary<string, Variant> values,
        IReadOnlyList<string> expectedKeys,
        string path
    )
    {
        if (values.Count != expectedKeys.Count)
            throw Invalid(path, $"expected exactly {expectedKeys.Count} fields, got {values.Count}");
        foreach (string key in expectedKeys)
        {
            if (!values.ContainsKey(key))
                throw Invalid(path + "." + key, "required field is missing");
        }
    }

    internal static StringName ReadRequiredStringName(
        IReadOnlyDictionary<string, Variant> values,
        string key,
        string path
    )
    {
        if (!values.TryGetValue(key, out Variant value))
            throw Invalid(path + "." + key, "required field is missing");
        return ReadStringName(value, path + "." + key);
    }

    internal static StringName ReadOptionalStringName(
        IReadOnlyDictionary<string, Variant> values,
        string key,
        string path
    ) =>
        values.TryGetValue(key, out Variant value)
            ? ReadStringName(value, path + "." + key)
            : new StringName("");

    internal static int ReadRequiredInt(
        IReadOnlyDictionary<string, Variant> values,
        string key,
        string path
    )
    {
        if (!values.TryGetValue(key, out Variant value))
            throw Invalid(path + "." + key, "required field is missing");
        return ReadInt(value, path + "." + key);
    }

    internal static int ReadOptionalInt(
        IReadOnlyDictionary<string, Variant> values,
        string key,
        string path
    ) =>
        values.TryGetValue(key, out Variant value) ? ReadInt(value, path + "." + key) : 0;

    internal static bool ReadOptionalBool(
        IReadOnlyDictionary<string, Variant> values,
        string key,
        string path
    )
    {
        if (!values.TryGetValue(key, out Variant value))
            return false;
        if (value.VariantType != Variant.Type.Bool)
            throw Invalid(path + "." + key, $"must be Bool, got {value.VariantType}");
        return value.AsBool();
    }

    internal static GDictionary ReadRequiredDictionary(
        IReadOnlyDictionary<string, Variant> values,
        string key,
        string path
    )
    {
        if (!values.TryGetValue(key, out Variant value))
            throw Invalid(path + "." + key, "required field is missing");
        if (value.VariantType != Variant.Type.Dictionary)
            throw Invalid(path + "." + key, $"must be Dictionary, got {value.VariantType}");
        return value.AsGodotDictionary();
    }

    internal static IReadOnlyList<StringName> ReadOptionalStringNameList(
        IReadOnlyDictionary<string, Variant> values,
        string key,
        string path
    )
    {
        if (!values.TryGetValue(key, out Variant value))
            return new ReadOnlyCollection<StringName>(new List<StringName>());
        if (value.VariantType != Variant.Type.Array)
            throw Invalid(path + "." + key, $"must be Array, got {value.VariantType}");
        using GArray array = value.AsGodotArray();
        var result = new List<StringName>(array.Count);
        for (int index = 0; index < array.Count; index++)
            result.Add(ReadStringName(array[index], $"{path}.{key}[{index}]"));
        return new ReadOnlyCollection<StringName>(result);
    }

    internal static IReadOnlyDictionary<string, object> ProjectParameterBindings(
        GDictionary source,
        string path
    )
    {
        Dictionary<string, Variant> normalized = NormalizeKeys(source, path);
        var result = new Dictionary<string, object>(normalized.Count, StringComparer.Ordinal);
        foreach ((string key, Variant value) in normalized)
            result.Add(key, ProjectParameterBindingValue(value, path + "." + key));
        return new ReadOnlyDictionary<string, object>(result);
    }

    internal static IReadOnlyDictionary<string, object> FreezePlainDictionary(
        IReadOnlyDictionary<string, object> source,
        string path
    )
    {
        if (source == null)
            throw Invalid(path, "dictionary is null");
        var result = new Dictionary<string, object>(source.Count, StringComparer.Ordinal);
        foreach ((string key, object value) in source)
        {
            if (string.IsNullOrEmpty(key))
                throw Invalid(path, "key must not be empty");
            if (value == null)
                throw Invalid(path + "." + key, "value is null");
            object frozenValue = FreezePlainValue(value, path + "." + key);
            if (!result.TryAdd(key, frozenValue))
                throw Invalid(path + "." + key, "duplicate key");
        }
        return new ReadOnlyDictionary<string, object>(result);
    }

    private static object FreezePlainValue(object value, string path)
    {
        if (
            value is bool
            or int
            or long
            or float
            or double
            or string
            or StringName
        )
        {
            return value;
        }

        if (value is IReadOnlyList<object> list)
        {
            var result = new List<object>(list.Count);
            for (int index = 0; index < list.Count; index++)
            {
                object item = list[index];
                if (item == null)
                    throw Invalid($"{path}[{index}]", "value is null");
                result.Add(FreezePlainValue(item, $"{path}[{index}]"));
            }
            return new ReadOnlyCollection<object>(result);
        }

        throw Invalid(path, $"unsupported plain value type {value.GetType().FullName}");
    }

    private static object ProjectParameterBindingValue(Variant value, string path)
    {
        switch (value.VariantType)
        {
            case Variant.Type.Bool:
                return value.AsBool();
            case Variant.Type.Int:
                return value.AsInt64();
            case Variant.Type.Float:
                return value.AsDouble();
            case Variant.Type.String:
                return value.AsString();
            case Variant.Type.StringName:
                return value.AsStringName();
            case Variant.Type.Array:
            {
                using GArray source = value.AsGodotArray();
                var result = new List<object>(source.Count);
                for (int index = 0; index < source.Count; index++)
                    result.Add(ReadStringName(source[index], $"{path}[{index}]"));
                return new ReadOnlyCollection<object>(result);
            }
            default:
                throw Invalid(
                    path,
                    $"unsupported parameter binding value type {value.VariantType}"
                );
        }
    }

    private static StringName ReadStringName(Variant value, string path)
    {
        return value.VariantType switch
        {
            Variant.Type.String => new StringName(value.AsString()),
            Variant.Type.StringName => value.AsStringName(),
            _ => throw Invalid(path, $"must be String or StringName, got {value.VariantType}"),
        };
    }

    private static int ReadInt(Variant value, string path)
    {
        if (value.VariantType != Variant.Type.Int)
            throw Invalid(path, $"must be Int, got {value.VariantType}");
        return value.AsInt32();
    }

    internal static InvalidDataException Invalid(string path, string message) =>
        new($"Invalid authored contingency content at '{path}': {message}.");
}
