#nullable enable

using System;
using System.Collections.Generic;

internal static class TraitJsonImportRules
{
    internal const string InvalidDto = "trait.dto.invalid_entry";
}

internal static class TraitJsonImportParser
{
    internal static ContentImportStageResult<TraitImportModel> Parse(
        JsonContentEntryContext context,
        string json
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ContentImportStageResult<TraitJsonDto> dtoResult = ContentJsonStrictDtoParser.Parse(
            context,
            json ?? "",
            TraitJsonImportSerializerContext.Default.TraitJsonDto,
            TraitJsonImportRules.InvalidDto
        );
        if (!dtoResult.HasValue)
            return ContentImportStageResult<TraitImportModel>.Failure(dtoResult.Diagnostics);

        TraitJsonDto dto = dtoResult.Value;
        return ContentImportStageResult<TraitImportModel>.Success(
            new TraitImportModel(
                dto.TraitId ?? "",
                dto.DisplayName ?? "",
                dto.Description ?? "",
                CopyStrings(dto.Categories),
                CopyStrings(dto.AllowedSourceKinds),
                dto.EffectType ?? "",
                dto.TriggerType ?? "",
                dto.StackPolicy ?? "",
                dto.ChargeScope ?? "",
                dto.ChargeResetTiming ?? "",
                dto.HighestRollCompareKey ?? "",
                dto.VisionRange,
                dto.ProficiencyChoiceCount,
                CopyAttributeModifiers(dto.AttributeModifiers),
                CopyStrings(dto.SaveAdvantageTags),
                CopyStrings(dto.SaveDisadvantageTags),
                CopyStrings(dto.SaveImmunityTags),
                CopyDamageResistances(dto.DamageResistanceEntries),
                CopySaveBonuses(dto.SaveBonusEntries),
                CopyPassiveStatuses(dto.PassiveStatusEffects),
                CopyRollSchema(dto.RollValueSchema)
            )
        );
    }

    private static IReadOnlyList<string> CopyStrings(IReadOnlyList<string>? source)
    {
        if (source == null)
            return Array.Empty<string>();
        var result = new List<string>(source.Count);
        foreach (string? value in source)
            result.Add(value ?? "");
        return result;
    }

    private static IReadOnlyList<TraitAttributeModifierImportModel> CopyAttributeModifiers(
        IReadOnlyList<TraitAttributeModifierJsonDto>? source
    )
    {
        if (source == null)
            return Array.Empty<TraitAttributeModifierImportModel>();
        var result = new List<TraitAttributeModifierImportModel>(source.Count);
        foreach (TraitAttributeModifierJsonDto? value in source)
        {
            TraitAttributeModifierJsonDto current = value ?? new TraitAttributeModifierJsonDto();
            result.Add(
                new TraitAttributeModifierImportModel(
                    current.AttributeId ?? "",
                    current.Mode ?? "",
                    current.Value,
                    current.ValuePerRank,
                    current.SourceType ?? "",
                    current.SourceId ?? ""
                )
            );
        }
        return result;
    }

    private static IReadOnlyList<TraitDamageResistanceEntryImportModel> CopyDamageResistances(
        IReadOnlyList<TraitDamageResistanceEntryJsonDto>? source
    )
    {
        if (source == null)
            return Array.Empty<TraitDamageResistanceEntryImportModel>();
        var result = new List<TraitDamageResistanceEntryImportModel>(source.Count);
        foreach (TraitDamageResistanceEntryJsonDto? value in source)
        {
            TraitDamageResistanceEntryJsonDto current = value ?? new TraitDamageResistanceEntryJsonDto();
            result.Add(
                new TraitDamageResistanceEntryImportModel(
                    current.DamageTag ?? "",
                    current.MitigationTier ?? ""
                )
            );
        }
        return result;
    }

    private static IReadOnlyList<TraitSaveBonusEntryImportModel> CopySaveBonuses(
        IReadOnlyList<TraitSaveBonusEntryJsonDto>? source
    )
    {
        if (source == null)
            return Array.Empty<TraitSaveBonusEntryImportModel>();
        var result = new List<TraitSaveBonusEntryImportModel>(source.Count);
        foreach (TraitSaveBonusEntryJsonDto? value in source)
        {
            TraitSaveBonusEntryJsonDto current = value ?? new TraitSaveBonusEntryJsonDto();
            result.Add(new TraitSaveBonusEntryImportModel(current.SaveAbility ?? "", current.Bonus));
        }
        return result;
    }

    private static IReadOnlyList<TraitPassiveStatusEffectImportModel> CopyPassiveStatuses(
        IReadOnlyList<TraitPassiveStatusEffectJsonDto>? source
    )
    {
        if (source == null)
            return Array.Empty<TraitPassiveStatusEffectImportModel>();
        var result = new List<TraitPassiveStatusEffectImportModel>(source.Count);
        foreach (TraitPassiveStatusEffectJsonDto? value in source)
        {
            TraitPassiveStatusEffectJsonDto current = value ?? new TraitPassiveStatusEffectJsonDto();
            result.Add(
                new TraitPassiveStatusEffectImportModel(
                    current.StatusId ?? "",
                    current.Power,
                    current.Stacks,
                    current.DisplayLabel ?? "",
                    current.Undispellable,
                    current.CountsAsDebuffOverride,
                    current.CountsAsDebuff,
                    CopyStrings(current.SaveImmunityTags)
                )
            );
        }
        return result;
    }

    private static IReadOnlyList<TraitRollValueSchemaEntryImportModel> CopyRollSchema(
        IReadOnlyList<TraitRollValueSchemaEntryJsonDto>? source
    )
    {
        if (source == null)
            return Array.Empty<TraitRollValueSchemaEntryImportModel>();
        var result = new List<TraitRollValueSchemaEntryImportModel>(source.Count);
        foreach (TraitRollValueSchemaEntryJsonDto? value in source)
        {
            TraitRollValueSchemaEntryJsonDto current = value ?? new TraitRollValueSchemaEntryJsonDto();
            result.Add(
                new TraitRollValueSchemaEntryImportModel(
                    current.Key ?? "",
                    current.ValueType ?? "",
                    current.MinValue,
                    current.MaxValue,
                    CopyStrings(current.AllowedValues)
                )
            );
        }
        return result;
    }
}
