#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

internal static class BarrierJsonImportParser
{
    internal static ContentImportStageResult<BarrierProfileJsonDto> ParseProfile(
        JsonContentEntryContext context,
        string json
    ) =>
        ContentJsonStrictDtoParser.Parse(
            context,
            json,
            BarrierJsonSerializerContext.Default.BarrierProfileJsonDto,
            BarrierJsonRules.InvalidProfileDto
        );

    internal static ContentImportStageResult<BarrierLayerJsonDto> ParseLayer(
        JsonContentEntryContext context,
        string json
    ) =>
        ContentJsonStrictDtoParser.Parse(
            context,
            json,
            BarrierJsonSerializerContext.Default.BarrierLayerJsonDto,
            BarrierJsonRules.InvalidLayerDto
        );

    internal static ContentImportStageResult<BarrierProfileImportModel> NormalizeProfile(
        JsonContentEntryContext _,
        BarrierProfileJsonDto dto
    ) =>
        ContentImportStageResult<BarrierProfileImportModel>.Success(
            new BarrierProfileImportModel(
                dto.ProfileId,
                dto.DisplayName,
                ParseAnchorMode(dto.AnchorMode),
                ParseAreaPattern(dto.AreaPattern),
                dto.RadiusCells,
                dto.DurationTu,
                dto.CatchAllProjectedEffects,
                dto.LayerIds.ToArray()
            )
        );

    internal static ContentImportStageResult<BarrierLayerImportModel> NormalizeLayer(
        JsonContentEntryContext _,
        BarrierLayerJsonDto dto
    ) =>
        ContentImportStageResult<BarrierLayerImportModel>.Success(
            new BarrierLayerImportModel(
                dto.LayerId,
                dto.DisplayName,
                dto.Order,
                dto.BlockedCategories.ToArray(),
                dto.BreakerSkillIds.ToArray(),
                dto.PassageOutcomes.Select(outcome => new BarrierOutcomeImportModel(
                    ParseOutcomeKind(outcome.OutcomeType),
                    outcome.Amount,
                    outcome.DamageTag,
                    outcome.HalfOnSuccess,
                    outcome.SuccessAmount,
                    outcome.SuccessDamageTag,
                    outcome.FatalDamage,
                    outcome.StatusId,
                    outcome.SaveAbility,
                    outcome.SaveTag,
                    outcome.SaveDc
                )).ToArray()
            )
        );

    private static BarrierAnchorImportKind ParseAnchorMode(string value) =>
        value == "fixed" ? BarrierAnchorImportKind.Fixed : BarrierAnchorImportKind.Unknown;

    private static BarrierAreaPatternImportKind ParseAreaPattern(string value) =>
        value switch
        {
            "single" => BarrierAreaPatternImportKind.Single,
            "diamond" => BarrierAreaPatternImportKind.Diamond,
            "square" => BarrierAreaPatternImportKind.Square,
            "radius" => BarrierAreaPatternImportKind.Radius,
            "cross" => BarrierAreaPatternImportKind.Cross,
            _ => BarrierAreaPatternImportKind.Unknown,
        };

    private static BarrierOutcomeImportKind ParseOutcomeKind(string value) =>
        value switch
        {
            "" => BarrierOutcomeImportKind.None,
            "damage" => BarrierOutcomeImportKind.Damage,
            "poison_death" => BarrierOutcomeImportKind.PoisonDeath,
            "status" => BarrierOutcomeImportKind.Status,
            "banish" => BarrierOutcomeImportKind.Banish,
            _ => BarrierOutcomeImportKind.Unknown,
        };
}

internal static class BarrierJsonAuthoringDomains
{
    private static readonly ContentJsonNullabilityPolicy NullabilityPolicy =
        new(Array.Empty<string>());

    internal static IReadOnlyList<ContentJsonSchemaDomainRegistration> SchemaRegistrations { get; } =
        Array.AsReadOnly(
            new[]
            {
                BarrierJsonDomains.ProfileSchemaRegistration,
                BarrierJsonDomains.LayerSchemaRegistration,
            }
        );

    internal static JsonContentDomainDescriptor<BarrierProfileJsonDto, BarrierProfileImportModel>
        CreateProfileDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        new(
            BarrierJsonDomains.ProfileDomainId,
            BarrierJsonDomains.SchemaVersion,
            "profile_id",
            sourceDirectory,
            sourceReader,
            NullabilityPolicy,
            BarrierJsonImportParser.ParseProfile,
            BarrierJsonImportParser.NormalizeProfile,
            BarrierImportValidator.ValidateProfile
        );

    internal static JsonContentDomainDescriptor<BarrierLayerJsonDto, BarrierLayerImportModel>
        CreateLayerDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        new(
            BarrierJsonDomains.LayerDomainId,
            BarrierJsonDomains.SchemaVersion,
            "layer_id",
            sourceDirectory,
            sourceReader,
            NullabilityPolicy,
            BarrierJsonImportParser.ParseLayer,
            BarrierJsonImportParser.NormalizeLayer,
            BarrierImportValidator.ValidateLayer
        );

    internal static IEnumerable<IContentJsonOfflineValidationDomain> CreateOfflineDomains()
    {
        yield return new OfflineProfileDomain();
        yield return new OfflineLayerDomain();
    }

    private sealed class OfflineProfileDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => BarrierJsonDomains.ProfileDomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<BarrierProfileImportModel> batch =
                CreateProfileDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(
                DomainId,
                batch.Entries.Count,
                batch.Diagnostics
            );
        }
    }

    private sealed class OfflineLayerDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => BarrierJsonDomains.LayerDomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<BarrierLayerImportModel> batch =
                CreateLayerDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(
                DomainId,
                batch.Entries.Count,
                batch.Diagnostics
            );
        }
    }
}
