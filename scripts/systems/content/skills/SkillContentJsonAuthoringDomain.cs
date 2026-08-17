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

    internal static ContentJsonSchemaDomainRegistration SchemaRegistration { get; } =
        new(
            domainId: DomainId,
            schemaVersion: SchemaVersion,
            documentDtoType: typeof(SkillJsonDocumentDto),
            title: "Magic skill JSON authoring schema (stage 1a pilot)",
            description:
                "Current stage-1a skill import contract. It covers the committed pilot fields and the layered_barrier effect payload only; it is not the final full-skill-domain schema.",
            trackedSchemaPath: "res://data/schemas/content/skills.schema.json",
            contentFileMatch: "/data/configs/json/skills/**/*.json"
        );

    private static readonly ContentJsonNullabilityPolicy NullabilityPolicy = new(
        new[] { "/combat_profile" }
    );

    internal static IContentJsonOfflineValidationDomain CreateOfflineValidationDomain() =>
        new ContentJsonOfflineValidationDomain<SkillImportModel, SkillImportModel>(
            domainId: DomainId,
            schemaVersion: SchemaVersion,
            entryIdPropertyName: EntryIdPropertyName,
            NullabilityPolicy,
            SkillJsonImportParser.Parse,
            NormalizeIdentity,
            ValidateCurrentPilotContract
        );

    private static ContentImportStageResult<SkillImportModel> NormalizeIdentity(
        JsonContentEntryContext context,
        SkillImportModel import
    ) => ContentImportStageResult<SkillImportModel>.Success(import);

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateCurrentPilotContract(
        JsonContentEntryContext context,
        SkillImportModel import
    ) => Array.Empty<ContentJsonDiagnostic>();
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
