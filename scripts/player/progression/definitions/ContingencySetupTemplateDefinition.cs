#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Godot;

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
    public ContingencyTriggerKind TriggerKind => ContingencyContractRules.ToTriggerKind(Type);
    public ContingencyTimingKind TimingKind => ContingencyContractRules.ToTimingKind(Timing);
}

public sealed record ContingencyTargetResolverDefinition(
    StringName Type,
    StringName Preference,
    int MaxDistance
)
{
    public ContingencyTargetResolverKind ResolverKind =>
        ContingencyContractRules.ToTargetResolverKind(Type);
}

public sealed record ContingencyMaterialCostDefinition(StringName ItemId, int Quantity);

public sealed class ContingencyStoredSpellTemplateDefinition
{
    public ContingencyStoredSpellTemplateDefinition(
        StringName storedSkillId,
        int maxCastLevel,
        int order,
        ContingencyTargetResolverDefinition targetResolver,
        IReadOnlyDictionary<string, object> parameterBindings,
        StringName fallbackPolicy
    )
    {
        // 不变量放在构造器里，下游（ContingencyContentRules）才不用为"可能是空 id"留兜底分支，
        // 那种兜底会把损坏条目和"模板没有 stored spell"混成同一个结果。
        StoredSkillId = storedSkillId ?? "";
        if (StoredSkillId == "")
        {
            throw new InvalidDataException(
                "ContingencyStoredSpellTemplateDefinition.StoredSkillId must not be empty."
            );
        }
        MaxCastLevel = maxCastLevel;
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
}

public sealed class ContingencySetupTemplateDefinition
{
    public ContingencySetupTemplateDefinition(
        StringName templateId,
        string displayName,
        StringName sourceSkillId,
        int matrixLoad,
        int reservedMpPerMatrixLoad,
        IReadOnlyList<ContingencyMaterialCostDefinition> chargeMaterialCosts,
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
        ReservedMpPerMatrixLoad = reservedMpPerMatrixLoad;
        ChargeMaterialCosts = ProgressionDefinitionProjection.FreezeValues(
            chargeMaterialCosts,
            "ContingencySetupTemplateDefinition.ChargeMaterialCosts"
        );
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
    public int ReservedMpPerMatrixLoad { get; }
    public IReadOnlyList<ContingencyMaterialCostDefinition> ChargeMaterialCosts { get; }
    public StringName ReleaseMode { get; }
    public ContingencyTriggerDefinition Trigger { get; }
    public IReadOnlyList<ContingencyStoredSpellTemplateDefinition> StoredSpells { get; }

    internal static ContingencySetupTemplateDefinition FromImport(
        ContingencyTemplateImportModel source,
        string sourceLabel
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        string path = string.IsNullOrWhiteSpace(sourceLabel)
            ? "contingency_template"
            : sourceLabel;
        if (string.IsNullOrWhiteSpace(source.TemplateId))
            throw ContingencyDefinitionProjection.Invalid(path + ".template_id", "must not be empty");
        if (string.IsNullOrWhiteSpace(source.DisplayName))
            throw ContingencyDefinitionProjection.Invalid(path + ".display_name", "must not be blank");
        if (string.IsNullOrWhiteSpace(source.SourceSkillId))
            throw ContingencyDefinitionProjection.Invalid(path + ".source_skill_id", "must not be empty");
        if (source.MatrixLoad <= 0)
            throw ContingencyDefinitionProjection.Invalid(path + ".matrix_load", "must be positive");
        if (source.ReservedMpPerMatrixLoad <= 0)
            throw ContingencyDefinitionProjection.Invalid(path + ".reserved_mp_per_matrix_load", "must be positive");
        if (source.ChargeMaterialCosts.Count == 0)
            throw ContingencyDefinitionProjection.Invalid(path + ".charge_material_costs", "must not be empty");
        if (string.IsNullOrWhiteSpace(source.ReleaseMode))
            throw ContingencyDefinitionProjection.Invalid(path + ".release_mode", "must not be empty");

        ContingencyTriggerDefinition trigger = ProjectTrigger(source.Trigger, path + ".trigger");
        var materialCosts = new List<ContingencyMaterialCostDefinition>(
            source.ChargeMaterialCosts.Count
        );
        for (int index = 0; index < source.ChargeMaterialCosts.Count; index++)
        {
            ContingencyMaterialCostImportModel cost = source.ChargeMaterialCosts[index];
            if (string.IsNullOrWhiteSpace(cost.ItemId) || cost.Quantity <= 0)
                throw ContingencyDefinitionProjection.Invalid(
                    $"{path}.charge_material_costs[{index}]",
                    "item_id and positive quantity are required"
                );
            materialCosts.Add(new ContingencyMaterialCostDefinition(
                new StringName(cost.ItemId),
                cost.Quantity
            ));
        }
        var spells = new List<ContingencyStoredSpellTemplateDefinition>(
            source.StoredSpells.Count
        );
        for (int index = 0; index < source.StoredSpells.Count; index++)
        {
            spells.Add(ProjectStoredSpell(source.StoredSpells[index], $"{path}.stored_spells[{index}]"));
        }
        return new ContingencySetupTemplateDefinition(
            new StringName(source.TemplateId),
            source.DisplayName,
            new StringName(source.SourceSkillId),
            source.MatrixLoad,
            source.ReservedMpPerMatrixLoad,
            new ReadOnlyCollection<ContingencyMaterialCostDefinition>(materialCosts),
            new StringName(source.ReleaseMode),
            trigger,
            new ReadOnlyCollection<ContingencyStoredSpellTemplateDefinition>(spells)
        );
    }

    private static ContingencyTriggerDefinition ProjectTrigger(
        ContingencyTriggerImportModel source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        StringName type = new(source.Type);
        if (ContingencyContractRules.ToTriggerKind(type) == ContingencyTriggerKind.Unknown)
            throw ContingencyDefinitionProjection.Invalid(path + ".kind", $"unsupported trigger type '{type}'");
        StringName timing = new(source.Timing);
        if (ContingencyContractRules.ToTimingKind(timing) == ContingencyTimingKind.Unknown)
            throw ContingencyDefinitionProjection.Invalid(path + ".payload.timing", $"unsupported timing '{timing}'");
        return new ContingencyTriggerDefinition(
            type,
            new StringName(source.Subject),
            timing,
            source.Percent,
            source.CrossingOnly,
            source.DamagePercent,
            new StringName(source.DamageBasis),
            new StringName(source.DamageAmountMode),
            new StringName(source.Center),
            source.Radius,
            new StringName(source.RadiusMetric),
            new StringName(source.SourceTeam),
            source.StatusTags.Select(value => new StringName(value)).ToArray(),
            new StringName(source.ApplicationMatch),
            new StringName(source.SpellMatch)
        );
    }

    private static ContingencyStoredSpellTemplateDefinition ProjectStoredSpell(
        ContingencyStoredSpellImportModel source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(source.StoredSkillId))
            throw ContingencyDefinitionProjection.Invalid(path + ".stored_skill_id", "must not be empty");
        // 原先构造器用 Math.Max(value, 1) 静默钳位：内容里写 0 或负数会被当成 1 跑完整局，
        // 作者永远看不到自己写错了。改为内容期拒绝。
        if (source.MaxCastLevel < 1)
        {
            throw ContingencyDefinitionProjection.Invalid(
                path + ".max_cast_level",
                $"must be >= 1, got {source.MaxCastLevel}"
            );
        }
        ContingencyTargetResolverDefinition resolver = ProjectResolver(
            source.TargetResolver,
            path + ".target_resolver"
        );
        var result = new ContingencyStoredSpellTemplateDefinition(
            new StringName(source.StoredSkillId),
            source.MaxCastLevel,
            source.Order,
            resolver,
            source.ParameterBindings,
            new StringName(source.FallbackPolicy)
        );
        if (result.FallbackPolicyKind == ContingencyFallbackPolicyKind.Unknown)
            throw ContingencyDefinitionProjection.Invalid(path + ".fallback_policy", $"unsupported fallback policy '{source.FallbackPolicy}'");
        return result;
    }

    private static ContingencyTargetResolverDefinition ProjectResolver(
        ContingencyTargetResolverImportModel source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        StringName type = new(source.Type);
        ContingencyTargetResolverKind kind = ContingencyContractRules.ToTargetResolverKind(type);
        if (kind == ContingencyTargetResolverKind.Unknown)
            throw ContingencyDefinitionProjection.Invalid(path + ".kind", $"unsupported resolver '{type}'");
        return new ContingencyTargetResolverDefinition(
            type,
            new StringName(source.Preference),
            source.MaxDistance
        );
    }
}

internal static class ContingencyDefinitionProjection
{
    internal static IReadOnlyDictionary<string, object> FreezePlainDictionary(
        IReadOnlyDictionary<string, object> source,
        string path
    )
    {
        if (source == null)
            throw Invalid(path, "dictionary is null");
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach ((string key, object value) in source)
        {
            if (string.IsNullOrEmpty(key))
                throw Invalid(path, "key must not be empty");
            result.Add(key, FreezePlainValue(value, path + "." + key));
        }
        return new ReadOnlyDictionary<string, object>(result);
    }

    private static object FreezePlainValue(object value, string path)
    {
        if (value is bool or int or long or float or double or string or StringName)
            return value;
        if (value is IReadOnlyList<object> list)
        {
            var result = new List<object>(list.Count);
            for (int index = 0; index < list.Count; index++)
            {
                object item = list[index]
                    ?? throw Invalid($"{path}[{index}]", "value is null");
                result.Add(FreezePlainValue(item, $"{path}[{index}]"));
            }
            return new ReadOnlyCollection<object>(result);
        }
        throw Invalid(path, $"unsupported plain value type {value?.GetType().FullName}");
    }

    internal static InvalidDataException Invalid(string path, string message) =>
        new($"Invalid authored contingency content at '{path}': {message}.");
}
