#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class ContentJsonOfflineValidationCatalog
{
    private static readonly IReadOnlyList<IContentJsonOfflineValidationDomain> Domains =
        BuildDomains();

    private static IReadOnlyList<IContentJsonOfflineValidationDomain> BuildDomains()
    {
        var domains = new List<IContentJsonOfflineValidationDomain>
        {
            ContentJsonOfflineValidationSampleDomain.Create(),
            SkillContentJsonAuthoringDomain.CreateOfflineValidationDomain(),
            ItemContentJsonAuthoringDomain.CreateOfflineValidationDomain(),
            TraitContentJsonAuthoringDomain.CreateOfflineValidationDomain(),
            EquipmentAbilityContentJsonAuthoringDomain.CreateOfflineValidationDomain(),
            GearSetContentJsonAuthoringDomain.CreateOfflineValidationDomain(),
            RecipeContentJsonAuthoringDomain.CreateOfflineValidationDomain(),
        };
        domains.AddRange(EnemyContentJsonAuthoringDomains.CreateOfflineDomains());
        domains.AddRange(BattleEncounterJsonAuthoringDomains.CreateOfflineDomains());
        domains.AddRange(BarrierJsonAuthoringDomains.CreateOfflineDomains());
        domains.AddRange(BattleSpecialProfileJsonAuthoringDomains.CreateOfflineDomains());
        domains.AddRange(ProfessionIdentityJsonImport.CreateOfflineDomains());
        domains.AddRange(QuestJsonContentDomain.CreateOfflineDomains());
        domains.AddRange(ContingencyJsonContentDomain.CreateOfflineDomains());
        domains.AddRange(BattleSimJsonContentDomains.CreateOfflineDomains());
        domains.AddRange(WorldJsonImport.CreateOfflineDomains());
        return Array.AsReadOnly(domains.ToArray());
    }

    internal static IReadOnlyList<IContentJsonOfflineValidationDomain> All => Domains;

    internal static IContentJsonOfflineValidationDomain Require(string domainId)
    {
        IContentJsonOfflineValidationDomain? domain = Domains.SingleOrDefault(candidate =>
            string.Equals(candidate.DomainId, domainId, StringComparison.Ordinal)
        );
        return domain
            ?? throw new ArgumentException(
                $"Unknown offline content validation domain '{domainId}'.",
                nameof(domainId)
            );
    }
}

internal static class ContentJsonOfflineValidationSampleRules
{
    internal const string InvalidEntryDto = "schema_fixture.dto.invalid_entry";
    internal const string InvalidActionPayload = "schema_fixture.dto.invalid_action_payload";
    internal const string UnknownActionKind = "schema_fixture.action.unknown_kind";
    internal const string DisplayNameRequired = "schema_fixture.display_name.required";
    internal const string UnknownQuality = "schema_fixture.quality.unknown";
    internal const string InvalidTags = "schema_fixture.tags.invalid";
}

internal static class ContentJsonOfflineValidationSampleDomain
{
    private static readonly ContentJsonNullabilityPolicy NullabilityPolicy = new(
        new[] { "/optional_note", "/action/payload/audience/*" }
    );

    internal static IContentJsonOfflineValidationDomain Create()
    {
        ContentJsonSchemaDomainRegistration schemaRegistration =
            ContentJsonSchemaCatalog.Require("schema_fixture");
        return new ContentJsonOfflineValidationDomain<
            ContentJsonSchemaSampleEntryDto,
            ContentJsonSchemaSampleImport
        >(
            domainId: schemaRegistration.DomainId,
            schemaVersion: schemaRegistration.SchemaVersion,
            entryIdPropertyName: "fixture_id",
            NullabilityPolicy,
            ParseEntry,
            NormalizeImport,
            ValidateDomainLocal
        );
    }

    private static ContentImportStageResult<ContentJsonSchemaSampleEntryDto> ParseEntry(
        JsonContentEntryContext context,
        string json
    ) =>
        ContentJsonStrictDtoParser.Parse(
            context,
            json,
            ContentJsonOfflineValidationSampleJsonContext.Default.ContentJsonSchemaSampleEntryDto,
            ContentJsonOfflineValidationSampleRules.InvalidEntryDto
        );

    private static ContentImportStageResult<ContentJsonSchemaSampleImport> NormalizeImport(
        JsonContentEntryContext context,
        ContentJsonSchemaSampleEntryDto dto
    )
    {
        if (dto.Action == null)
        {
            return ContentImportStageResult<ContentJsonSchemaSampleImport>.Failure(
                Diagnostic(
                    ContentJsonOfflineValidationSampleRules.InvalidActionPayload,
                    "Schema fixture action is required.",
                    context,
                    "/action"
                )
            );
        }

        if (dto.Action.Payload is not JsonElement payload)
        {
            return ContentImportStageResult<ContentJsonSchemaSampleImport>.Failure(
                Diagnostic(
                    ContentJsonOfflineValidationSampleRules.InvalidActionPayload,
                    "Schema fixture action payload must be a JSON object.",
                    context,
                    "/action/payload"
                )
            );
        }

        var payloadContext = new JsonContentEntryContext(
            context.DomainId,
            context.EntryId,
            context.SourceLabel,
            $"{context.JsonPointer}/action/payload"
        );
        switch (dto.Action.Kind)
        {
            case "counter":
            {
                ContentImportStageResult<ContentJsonSchemaSampleCounterPayloadDto> parsed =
                    ContentJsonStrictDtoParser.Parse(
                        payloadContext,
                        payload.GetRawText(),
                        ContentJsonOfflineValidationSampleJsonContext
                            .Default.ContentJsonSchemaSampleCounterPayloadDto,
                        ContentJsonOfflineValidationSampleRules.InvalidActionPayload
                    );
                return parsed.HasValue
                    ? ContentImportStageResult<ContentJsonSchemaSampleImport>.Success(
                        new ContentJsonSchemaSampleImport(
                            dto.FixtureId,
                            dto.DisplayName,
                            dto.Quality,
                            dto.Tags,
                            dto.Action.Kind,
                            parsed.Value.Amount,
                            ""
                        )
                    )
                    : ContentImportStageResult<ContentJsonSchemaSampleImport>.Failure(
                        parsed.Diagnostics
                    );
            }
            case "message":
            {
                ContentImportStageResult<ContentJsonSchemaSampleMessagePayloadDto> parsed =
                    ContentJsonStrictDtoParser.Parse(
                        payloadContext,
                        payload.GetRawText(),
                        ContentJsonOfflineValidationSampleJsonContext
                            .Default.ContentJsonSchemaSampleMessagePayloadDto,
                        ContentJsonOfflineValidationSampleRules.InvalidActionPayload
                    );
                return parsed.HasValue
                    ? ContentImportStageResult<ContentJsonSchemaSampleImport>.Success(
                        new ContentJsonSchemaSampleImport(
                            dto.FixtureId,
                            dto.DisplayName,
                            dto.Quality,
                            dto.Tags,
                            dto.Action.Kind,
                            0,
                            parsed.Value.Text
                        )
                    )
                    : ContentImportStageResult<ContentJsonSchemaSampleImport>.Failure(
                        parsed.Diagnostics
                    );
            }
            default:
                return ContentImportStageResult<ContentJsonSchemaSampleImport>.Failure(
                    Diagnostic(
                        ContentJsonOfflineValidationSampleRules.UnknownActionKind,
                        "Schema fixture action kind is not registered.",
                        context,
                        "/action/kind"
                    )
                );
        }
    }

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateDomainLocal(
        JsonContentEntryContext context,
        ContentJsonSchemaSampleImport import
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        if (string.IsNullOrWhiteSpace(import.DisplayName))
        {
            diagnostics.Add(
                Diagnostic(
                    ContentJsonOfflineValidationSampleRules.DisplayNameRequired,
                    "Schema fixture display name must be non-empty.",
                    context,
                    "/display_name"
                )
            );
        }

        if (
            !new ContentJsonSchemaSampleQualityValues().Values.Contains(
                import.Quality,
                StringComparer.Ordinal
            )
        )
        {
            diagnostics.Add(
                Diagnostic(
                    ContentJsonOfflineValidationSampleRules.UnknownQuality,
                    "Schema fixture quality must use a registered stable value.",
                    context,
                    "/quality"
                )
            );
        }

        if (
            import.Tags == null
            || import.Tags.Any(tag => string.IsNullOrWhiteSpace(tag))
        )
        {
            diagnostics.Add(
                Diagnostic(
                    ContentJsonOfflineValidationSampleRules.InvalidTags,
                    "Schema fixture tags must be a non-null list of non-empty strings.",
                    context,
                    "/tags"
                )
            );
        }
        return diagnostics;
    }

    private static ContentJsonDiagnostic Diagnostic(
        string ruleId,
        string message,
        JsonContentEntryContext context,
        string relativePointer
    ) =>
        new(
            ruleId,
            message,
            context.SourceLabel,
            $"{context.JsonPointer}{relativePointer}"
        );
}

internal sealed record ContentJsonSchemaSampleImport(
    string FixtureId,
    string DisplayName,
    string Quality,
    IReadOnlyList<string> Tags,
    string ActionKind,
    int CounterAmount,
    string MessageText
);

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified
)]
[JsonSerializable(typeof(ContentJsonSchemaSampleEntryDto))]
[JsonSerializable(typeof(ContentJsonSchemaSampleCounterPayloadDto))]
[JsonSerializable(typeof(ContentJsonSchemaSampleMessagePayloadDto))]
internal partial class ContentJsonOfflineValidationSampleJsonContext : JsonSerializerContext { }
