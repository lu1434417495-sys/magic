using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;

public sealed class TraitDefinition
{
    public TraitDefinition(
        StringName traitId,
        string displayName,
        string description,
        IReadOnlyList<StringName> categories,
        IReadOnlyList<StringName> allowedSourceKinds,
        StringName effectType,
        StringName triggerType,
        StringName stackPolicy,
        StringName chargeScope,
        StringName chargeResetTiming,
        StringName highestRollCompareKey,
        int visionRange,
        int proficiencyChoiceCount,
        IReadOnlyList<AttributeModifierDefinition> attributeModifiers,
        IReadOnlyList<StringName> saveAdvantageTags,
        IReadOnlyList<TraitDamageResistanceEntryDefinition> damageResistanceEntries,
        IReadOnlyList<TraitSaveBonusEntryDefinition> saveBonusEntries,
        IReadOnlyList<TraitPassiveStatusEffectDefinition> passiveStatusEffects,
        IReadOnlyList<TraitRollValueSchemaEntryDefinition> rollValueSchema
    )
    {
        TraitId = traitId;
        DisplayName = ProgressionDefinitionProjection.RequireString(
            displayName,
            "TraitDefinition.DisplayName"
        );
        Description = ProgressionDefinitionProjection.RequireString(
            description,
            "TraitDefinition.Description"
        );
        Categories = ProgressionDefinitionProjection.FreezeValues(
            categories,
            "TraitDefinition.Categories"
        );
        AllowedSourceKinds = ProgressionDefinitionProjection.FreezeValues(
            allowedSourceKinds,
            "TraitDefinition.AllowedSourceKinds"
        );
        EffectType = effectType;
        TriggerType = triggerType;
        StackPolicy = stackPolicy;
        ChargeScope = chargeScope;
        ChargeResetTiming = chargeResetTiming;
        ConfiguredHighestRollCompareKey = highestRollCompareKey;
        VisionRange = visionRange;
        ProficiencyChoiceCount = proficiencyChoiceCount;
        AttributeModifiers = ProgressionDefinitionProjection.FreezeValues(
            attributeModifiers,
            "TraitDefinition.AttributeModifiers"
        );
        SaveAdvantageTags = ProgressionDefinitionProjection.FreezeValues(
            saveAdvantageTags,
            "TraitDefinition.SaveAdvantageTags"
        );
        DamageResistanceEntries = ProgressionDefinitionProjection.FreezeValues(
            damageResistanceEntries,
            "TraitDefinition.DamageResistanceEntries"
        );
        SaveBonusEntries = ProgressionDefinitionProjection.FreezeValues(
            saveBonusEntries,
            "TraitDefinition.SaveBonusEntries"
        );
        PassiveStatusEffects = ProgressionDefinitionProjection.FreezeValues(
            passiveStatusEffects,
            "TraitDefinition.PassiveStatusEffects"
        );
        RollValueSchema = ProgressionDefinitionProjection.FreezeValues(
            rollValueSchema,
            "TraitDefinition.RollValueSchema"
        );
    }

    public StringName TraitId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public IReadOnlyList<StringName> Categories { get; }
    public IReadOnlyList<StringName> AllowedSourceKinds { get; }
    public StringName EffectType { get; }
    public StringName TriggerType { get; }
    public StringName StackPolicy { get; }
    public StringName ChargeScope { get; }
    public StringName ChargeResetTiming { get; }

    /// <summary>
    /// The value authored in <c>highest_roll_compare_key</c> before projection defaults apply.
    /// </summary>
    public StringName ConfiguredHighestRollCompareKey { get; }

    public StringName HighestRollCompareKey
    {
        get
        {
            if (ConfiguredHighestRollCompareKey != "")
                return ConfiguredHighestRollCompareKey;

            foreach (TraitRollValueSchemaEntryDefinition entry in RollValueSchema)
            {
                if (entry.ValueTypeKind == TraitRollValueType.Int && entry.Key != "")
                    return entry.Key;
            }

            return "";
        }
    }

    public int VisionRange { get; }
    public int ProficiencyChoiceCount { get; }
    public IReadOnlyList<AttributeModifierDefinition> AttributeModifiers { get; }
    public IReadOnlyList<StringName> SaveAdvantageTags { get; }
    public IReadOnlyList<TraitDamageResistanceEntryDefinition> DamageResistanceEntries { get; }
    public IReadOnlyList<TraitSaveBonusEntryDefinition> SaveBonusEntries { get; }
    public IReadOnlyList<TraitPassiveStatusEffectDefinition> PassiveStatusEffects { get; }
    public IReadOnlyList<TraitRollValueSchemaEntryDefinition> RollValueSchema { get; }

    internal TraitEffectKind EffectKind => TraitContentRules.ToEffectKind(EffectType);
    internal TraitTriggerKind TriggerKind => TraitTriggerContentRules.ToTriggerKind(TriggerType);
    internal TraitStackPolicyKind StackPolicyKind =>
        TraitContentRules.ToStackPolicyKind(StackPolicy);
    internal TraitChargeScopeKind ChargeScopeKind =>
        TraitContentRules.ToChargeScopeKind(ChargeScope);
    internal TraitChargeResetTimingKind ChargeResetTimingKind =>
        TraitContentRules.ToChargeResetTimingKind(ChargeResetTiming);

    internal StringName GetHighestRollCompareKey() => HighestRollCompareKey;

    internal bool IsSourceKindAllowed(TraitSourceKind sourceKind)
    {
        if (sourceKind == TraitSourceKind.Unknown)
            return false;

        StringName expectedSource = TraitContentRules.ToStringName(sourceKind);
        foreach (StringName allowedSource in AllowedSourceKinds)
        {
            if (allowedSource == expectedSource)
                return true;
        }

        return false;
    }

    internal static TraitDefinition FromResource(TraitDef source)
    {
        ArgumentNullException.ThrowIfNull(source);
        string path = $"trait.{ProgressionDefinitionProjection.PathId(source.trait_id)}";

        ProgressionDefinitionProjection.RequireKnown(
            source.EffectKind != TraitEffectKind.Unknown,
            $"{path}.effect_type",
            source.effect_type
        );
        ProgressionDefinitionProjection.RequireKnown(
            source.TriggerKind != TraitTriggerKind.Unknown,
            $"{path}.trigger_type",
            source.trigger_type
        );
        ProgressionDefinitionProjection.RequireKnown(
            source.StackPolicyKind != TraitStackPolicyKind.Unknown,
            $"{path}.stack_policy",
            source.stack_policy
        );
        ProgressionDefinitionProjection.RequireKnown(
            source.ChargeScopeKind != TraitChargeScopeKind.Unknown,
            $"{path}.charge_scope",
            source.charge_scope
        );
        ProgressionDefinitionProjection.RequireKnown(
            source.ChargeResetTimingKind != TraitChargeResetTimingKind.Unknown,
            $"{path}.charge_reset_timing",
            source.charge_reset_timing
        );

        IReadOnlyList<StringName> allowedSources =
            ProgressionDefinitionProjection.CopyBorrowedValues(
                source.AllowedSourceKindsProjectionBorrowed,
                $"{path}.allowed_source_kinds"
            );
        for (int index = 0; index < allowedSources.Count; index++)
        {
            ProgressionDefinitionProjection.RequireKnown(
                TraitContentRules.ToSourceKind(allowedSources[index]) != TraitSourceKind.Unknown,
                $"{path}.allowed_source_kinds[{index}]",
                allowedSources[index]
            );
        }

        return new TraitDefinition(
            source.trait_id,
            ProgressionDefinitionProjection.RequireString(
                source.display_name,
                $"{path}.display_name"
            ),
            ProgressionDefinitionProjection.RequireString(
                source.description,
                $"{path}.description"
            ),
            ProgressionDefinitionProjection.CopyBorrowedValues(
                source.CategoriesProjectionBorrowed,
                $"{path}.categories"
            ),
            allowedSources,
            source.effect_type,
            source.trigger_type,
            source.stack_policy,
            source.charge_scope,
            source.charge_reset_timing,
            source.highest_roll_compare_key,
            source.vision_range,
            source.proficiency_choice_count,
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.AttributeModifiersProjectionBorrowed,
                $"{path}.attribute_modifiers",
                static (value, _) => AttributeModifierDefinition.FromResource(value)
            ),
            ProgressionDefinitionProjection.CopyBorrowedValues(
                source.SaveAdvantageTagsProjectionBorrowed,
                $"{path}.save_advantage_tags"
            ),
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.DamageResistanceEntriesProjectionBorrowed,
                $"{path}.damage_resistance_entries",
                TraitDamageResistanceEntryDefinition.FromResource
            ),
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.SaveBonusEntriesProjectionBorrowed,
                $"{path}.save_bonus_entries",
                TraitSaveBonusEntryDefinition.FromResource
            ),
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.PassiveStatusEffectsProjectionBorrowed,
                $"{path}.passive_status_effects",
                TraitPassiveStatusEffectDefinition.FromResource
            ),
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.RollValueSchemaProjectionBorrowed,
                $"{path}.roll_value_schema",
                TraitRollValueSchemaEntryDefinition.FromResource
            )
        );
    }
}

internal static class ProgressionDefinitionProjection
{
    internal static IReadOnlyList<T> FreezeValues<T>(
        IEnumerable<T> values,
        string path
    )
    {
        if (values == null)
            throw Invalid(path, "collection is null");

        var result = new List<T>();
        int index = 0;
        foreach (T value in values)
        {
            if (value is null)
                throw Invalid(path + $"[{index}]", "value is null");
            result.Add(value);
            index++;
        }

        return new ReadOnlyCollection<T>(result);
    }

    internal static IReadOnlyList<T> CopyBorrowedValues<T>(
        IEnumerable<T> values,
        string path
    )
    {
        if (values == null)
            throw Invalid(path, "collection is null");
        return FreezeValues(values, path);
    }

    internal static IReadOnlyList<TDefinition> ProjectBorrowedValues<TSource, TDefinition>(
        IEnumerable<TSource> values,
        string path,
        Func<TSource, string, TDefinition> projector
    )
        where TSource : class
        where TDefinition : class
    {
        if (values == null)
            throw Invalid(path, "collection is null");
        ArgumentNullException.ThrowIfNull(projector);

        var result = new List<TDefinition>();
        int index = 0;
        foreach (TSource value in values)
        {
            string childPath = $"{path}[{index}]";
            if (value == null)
                throw Invalid(childPath, "resource is null");

            TDefinition definition = projector(value, childPath);
            if (definition == null)
                throw Invalid(childPath, "projector returned null");
            result.Add(definition);
            index++;
        }

        return new ReadOnlyCollection<TDefinition>(result);
    }

    internal static void RequireKnown(bool isKnown, string path, StringName value)
    {
        if (!isKnown)
            throw Invalid(path, $"unsupported value '{value}'");
    }

    internal static string RequireString(string value, string path) =>
        value ?? throw Invalid(path, "string is null");

    internal static string PathId(StringName value) => value == "" ? "<missing>" : value.ToString();

    internal static InvalidDataException Invalid(string path, string message) =>
        new($"Invalid authored content at '{path}': {message}.");
}
