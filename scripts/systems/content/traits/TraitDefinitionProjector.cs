#nullable enable

using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// The single business projection from the plain trait import graph to immutable runtime
/// definitions. Strict JSON import is the only production source before this boundary.
/// </summary>
internal static class TraitDefinitionProjector
{
    internal static TraitDefinition Project(TraitImportModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new TraitDefinition(
            Name(source.TraitId),
            source.DisplayName,
            source.Description,
            Names(source.Categories),
            Names(source.AllowedSourceKinds),
            Name(source.EffectType),
            Name(source.TriggerType),
            Name(source.StackPolicy),
            Name(source.ChargeScope),
            Name(source.ChargeResetTiming),
            Name(source.HighestRollCompareKey),
            source.VisionRange,
            source.ProficiencyChoiceCount,
            ProjectModifiers(source.AttributeModifiers),
            Names(source.SaveAdvantageTags),
            Names(source.SaveDisadvantageTags),
            Names(source.SaveImmunityTags),
            ProjectDamageResistances(source.DamageResistanceEntries),
            ProjectSaveBonuses(source.SaveBonusEntries),
            ProjectPassiveStatuses(source.PassiveStatusEffects),
            ProjectRollSchema(source.RollValueSchema)
        );
    }

    internal static TraitImportModel ToImport(TraitDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var modifiers = new List<TraitAttributeModifierImportModel>();
        foreach (AttributeModifierDefinition value in source.AttributeModifiers)
        {
            modifiers.Add(new TraitAttributeModifierImportModel(Text(value.AttributeId), Text(value.Mode), value.Value, value.ValuePerRank, Text(value.SourceType), Text(value.SourceId)));
        }
        var resistances = new List<TraitDamageResistanceEntryImportModel>();
        foreach (TraitDamageResistanceEntryDefinition value in source.DamageResistanceEntries)
            resistances.Add(new TraitDamageResistanceEntryImportModel(Text(value.DamageTag), Text(value.MitigationTier)));
        var bonuses = new List<TraitSaveBonusEntryImportModel>();
        foreach (TraitSaveBonusEntryDefinition value in source.SaveBonusEntries)
            bonuses.Add(new TraitSaveBonusEntryImportModel(Text(value.SaveAbility), value.Bonus));
        var statuses = new List<TraitPassiveStatusEffectImportModel>();
        foreach (TraitPassiveStatusEffectDefinition value in source.PassiveStatusEffects)
            statuses.Add(new TraitPassiveStatusEffectImportModel(Text(value.StatusId), value.Power, value.Stacks, value.DisplayLabel, value.Undispellable, value.CountsAsDebuffOverride, value.CountsAsDebuff, Texts(value.SaveImmunityTags)));
        var schema = new List<TraitRollValueSchemaEntryImportModel>();
        foreach (TraitRollValueSchemaEntryDefinition value in source.RollValueSchema)
            schema.Add(new TraitRollValueSchemaEntryImportModel(Text(value.Key), Text(value.ValueType), value.MinValue, value.MaxValue, Texts(value.AllowedValues)));

        return new TraitImportModel(
            Text(source.TraitId), source.DisplayName, source.Description, Texts(source.Categories),
            Texts(source.AllowedSourceKinds), Text(source.EffectType), Text(source.TriggerType),
            Text(source.StackPolicy), Text(source.ChargeScope), Text(source.ChargeResetTiming),
            Text(source.ConfiguredHighestRollCompareKey), source.VisionRange, source.ProficiencyChoiceCount,
            modifiers, Texts(source.SaveAdvantageTags), Texts(source.SaveDisadvantageTags),
            Texts(source.SaveImmunityTags), resistances, bonuses, statuses, schema
        );
    }

    private static IReadOnlyList<AttributeModifierDefinition> ProjectModifiers(
        IReadOnlyList<TraitAttributeModifierImportModel> values
    )
    {
        var result = new List<AttributeModifierDefinition>(values.Count);
        foreach (TraitAttributeModifierImportModel value in values)
            result.Add(new AttributeModifierDefinition(Name(value.AttributeId), Name(value.Mode), value.Value, value.ValuePerRank, Name(value.SourceType), Name(value.SourceId)));
        return result;
    }

    private static IReadOnlyList<TraitDamageResistanceEntryDefinition> ProjectDamageResistances(
        IReadOnlyList<TraitDamageResistanceEntryImportModel> values
    )
    {
        var result = new List<TraitDamageResistanceEntryDefinition>(values.Count);
        foreach (TraitDamageResistanceEntryImportModel value in values)
            result.Add(new TraitDamageResistanceEntryDefinition(Name(value.DamageTag), Name(value.MitigationTier)));
        return result;
    }

    private static IReadOnlyList<TraitSaveBonusEntryDefinition> ProjectSaveBonuses(
        IReadOnlyList<TraitSaveBonusEntryImportModel> values
    )
    {
        var result = new List<TraitSaveBonusEntryDefinition>(values.Count);
        foreach (TraitSaveBonusEntryImportModel value in values)
            result.Add(new TraitSaveBonusEntryDefinition(Name(value.SaveAbility), value.Bonus));
        return result;
    }

    private static IReadOnlyList<TraitPassiveStatusEffectDefinition> ProjectPassiveStatuses(
        IReadOnlyList<TraitPassiveStatusEffectImportModel> values
    )
    {
        var result = new List<TraitPassiveStatusEffectDefinition>(values.Count);
        foreach (TraitPassiveStatusEffectImportModel value in values)
            result.Add(new TraitPassiveStatusEffectDefinition(Name(value.StatusId), value.Power, value.Stacks, value.DisplayLabel, value.Undispellable, value.CountsAsDebuffOverride, value.CountsAsDebuff, Names(value.SaveImmunityTags)));
        return result;
    }

    private static IReadOnlyList<TraitRollValueSchemaEntryDefinition> ProjectRollSchema(
        IReadOnlyList<TraitRollValueSchemaEntryImportModel> values
    )
    {
        var result = new List<TraitRollValueSchemaEntryDefinition>(values.Count);
        foreach (TraitRollValueSchemaEntryImportModel value in values)
            result.Add(new TraitRollValueSchemaEntryDefinition(Name(value.Key), Name(value.ValueType), value.MinValue, value.MaxValue, Names(value.AllowedValues)));
        return result;
    }

    private static IReadOnlyList<StringName> Names(IEnumerable<string> values)
    {
        var result = new List<StringName>();
        foreach (string value in values)
            result.Add(Name(value));
        return result;
    }

    private static IReadOnlyList<string> Texts(IEnumerable<StringName> values)
    {
        var result = new List<string>();
        foreach (StringName value in values)
            result.Add(Text(value));
        return result;
    }

    private static StringName Name(string? value) => new(value ?? "");
    private static string Text(StringName value) => value.ToString();
}
