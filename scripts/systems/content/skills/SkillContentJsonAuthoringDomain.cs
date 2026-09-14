#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

internal static class SkillContentJsonAuthoringDomain
{
    internal const string DomainId = "skills";
    internal const int SchemaVersion = 1;
    internal const string EntryIdPropertyName = "skill_id";
#if !CONTENT_JSON_OFFLINE
    private static readonly SkillImportModelValidator ImportValidator = new();
#endif

    internal static ContentJsonSchemaDomainRegistration SchemaRegistration { get; } =
        new(
            domainId: DomainId,
            schemaVersion: SchemaVersion,
            documentDtoType: typeof(SkillJsonDocumentDto),
            title: "Magic skill JSON authoring schema",
            description:
                "Current full skill import contract, including combat profiles, nested effects, and closed typed payload variants.",
            trackedSchemaPath: "res://data/schemas/content/skills.schema.json",
            contentFileMatch: "/data/configs/json/skills/**/*.json"
        );

    private static readonly ContentJsonNullabilityPolicy NullabilityPolicy = new(
        new[] { "/combat_profile" }
    );

    internal static JsonContentDomainDescriptor<SkillImportModel, SkillImportModel>
        CreateImportDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        CreateDescriptor(sourceDirectory, sourceReader, ValidateCurrentContract);

    internal static JsonContentDomainDescriptor<SkillImportModel, SkillImportModel>
        CreateSchemaImportDescriptor(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        ) => CreateDescriptor(sourceDirectory, sourceReader, ValidateSchemaOnly);

    private static JsonContentDomainDescriptor<SkillImportModel, SkillImportModel>
        CreateDescriptor(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader,
            Func<
                JsonContentEntryContext,
                SkillImportModel,
                IReadOnlyList<ContentJsonDiagnostic>
            > validateDomainLocal
        ) =>
        new(
            domainId: DomainId,
            schemaVersion: SchemaVersion,
            entryIdPropertyName: EntryIdPropertyName,
            sourceDirectory: sourceDirectory,
            sourceReader: sourceReader,
            nullabilityPolicy: NullabilityPolicy,
            parseDto: SkillJsonImportParser.Parse,
            normalizeImportModel: NormalizeIdentity,
            validateDomainLocal: validateDomainLocal
        );

    internal static IContentJsonOfflineValidationDomain CreateOfflineValidationDomain() =>
        new SkillOfflineValidationDomain();

    private static ContentImportStageResult<SkillImportModel> NormalizeIdentity(
        JsonContentEntryContext context,
        SkillImportModel import
    ) => ContentImportStageResult<SkillImportModel>.Success(import);

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateCurrentContract(
        JsonContentEntryContext context,
        SkillImportModel import
    )
    {
#if CONTENT_JSON_OFFLINE
        // The standalone host shares the complete strict DTO/parser contract but intentionally
        // carries no Godot-bound Definition validators. Production performs this second stage.
        return Array.Empty<ContentJsonDiagnostic>();
#else
        return ImportValidator.ValidateDomainLocal(context, import);
#endif
    }

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateSchemaOnly(
        JsonContentEntryContext context,
        SkillImportModel import
    ) => Array.Empty<ContentJsonDiagnostic>();

    private sealed class SkillOfflineValidationDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => SkillContentJsonAuthoringDomain.DomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<SkillImportModel> batch =
                CreateImportDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(
                DomainId,
                batch.Entries.Count,
                batch.Diagnostics
            );
        }
    }
}

[Description(
    "Skill authoring document envelope. Entries use the strict stage-1a DTO plus the schema-only file-local template selector; templates are recursive partial views of that same DTO."
)]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class SkillJsonDocumentDto
{
    [JsonPropertyName("schema")]
    [JsonRequired]
    [ContentJsonSchemaConst(SkillContentJsonAuthoringDomain.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain")]
    [JsonRequired]
    [ContentJsonSchemaConst(SkillContentJsonAuthoringDomain.DomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family")]
    [JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates")]
    [JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, SkillJsonDto> Templates { get; init; } =
        new ReadOnlyDictionary<string, SkillJsonDto>(
            new Dictionary<string, SkillJsonDto>()
        );

    [JsonPropertyName("entries")]
    [JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        SkillContentJsonAuthoringDomain.EntryIdPropertyName,
        "template"
    )]
    public IReadOnlyList<SkillJsonDto> Entries { get; init; } = Array.Empty<SkillJsonDto>();
}
