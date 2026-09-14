#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class EquipmentAbilityContentJsonAuthoringDomain
{
    internal const string DomainId = "equipment_abilities";
    internal const int SchemaVersion = 1;
    internal const string EntryIdPropertyName = "pack_id";
    internal const string ProductionDirectory = "res://data/configs/json/equipment_abilities";

    internal static ContentJsonSchemaDomainRegistration SchemaRegistration { get; } =
        new(
            domainId: DomainId,
            schemaVersion: SchemaVersion,
            documentDtoType: typeof(EquipmentAbilityJsonDocumentDto),
            title: "Magic equipment ability JSON authoring schema",
            description: "Strict equipment ability pack schema with closed condition/action kind and payload shapes; stable runtime vocabularies are validated by the domain layer.",
            trackedSchemaPath: "res://data/schemas/content/equipment_abilities.schema.json",
            contentFileMatch: "/data/configs/json/equipment_abilities/**/*.json"
        );

    private static readonly ContentJsonNullabilityPolicy NullabilityPolicy =
        new(new[]
        {
            "/bindings/*/fatal_intercepts/*/recovery_dice",
            "/bindings/*/fatal_intercepts/*/recovery_dice/terms/*/count_bonus_fact",
            "/bindings/*/fatal_intercepts/*/roll_gate",
            "/bindings/*/fatal_intercepts/*/roll_gate/roll/terms/*/count_bonus_fact",
            "/bindings/*/fatal_intercepts/*/success_actions/*/condition_group",
            "/bindings/*/fatal_intercepts/*/success_actions/*/roll_gate",
            "/bindings/*/granted_actions/*/availability_conditions",
            "/bindings/*/movement_trails/*/damage_dice/terms/*/count_bonus_fact",
            "/bindings/*/reactions/*/actions/*/condition_group",
            "/bindings/*/reactions/*/actions/*/payload/count_dice/terms/*/count_bonus_fact",
            "/bindings/*/reactions/*/actions/*/payload/dice/terms/*/count_bonus_fact",
            "/bindings/*/reactions/*/actions/*/payload/natural_weapon_damage_dice",
            "/bindings/*/reactions/*/actions/*/payload/natural_weapon_damage_dice/terms/*/count_bonus_fact",
            "/bindings/*/reactions/*/actions/*/roll_gate",
            "/bindings/*/reactions/*/condition_group",
            "/bindings/*/reactions/*/outcome_table",
            "/bindings/*/reactions/*/outcome_table/entries/*/actions/*/condition_group",
            "/bindings/*/reactions/*/outcome_table/entries/*/actions/*/payload/dice/terms/*/count_bonus_fact",
            "/bindings/*/reactions/*/outcome_table/entries/*/actions/*/roll_gate",
            "/bindings/*/reactions/*/outcome_table/roll/terms/*/count_bonus_fact",
            "/bindings/*/reactions/*/roll_gate",
            "/bindings/*/reactions/*/roll_gate/roll/terms/*/count_bonus_fact",
        });

    internal static JsonContentDomainDescriptor<EquipmentAbilityContentPackImportModel, EquipmentAbilityContentPackImportModel>
        CreateImportDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        CreateDescriptor(sourceDirectory, sourceReader, ValidateDomainLocal);

    internal static JsonContentDomainDescriptor<EquipmentAbilityContentPackImportModel, EquipmentAbilityContentPackImportModel>
        CreateSchemaImportDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        CreateDescriptor(
            sourceDirectory,
            sourceReader,
            static (_, _) => Array.Empty<ContentJsonDiagnostic>()
        );

    internal static IContentJsonOfflineValidationDomain CreateOfflineValidationDomain() =>
        new OfflineDomain();

    private static JsonContentDomainDescriptor<EquipmentAbilityContentPackImportModel, EquipmentAbilityContentPackImportModel>
        CreateDescriptor(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader,
            Func<
                JsonContentEntryContext,
                EquipmentAbilityContentPackImportModel,
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
            parseDto: EquipmentAbilityJsonImportParser.Parse,
            normalizeImportModel: static (_, value) => ContentImportStageResult<EquipmentAbilityContentPackImportModel>.Success(value),
            validateDomainLocal: validateDomainLocal
        );

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateDomainLocal(
        JsonContentEntryContext context,
        EquipmentAbilityContentPackImportModel import
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        EquipmentAbilityImportVocabularyValidator.Validate(context, import, diagnostics);
        return new ReadOnlyCollection<ContentJsonDiagnostic>(diagnostics);
    }

    private sealed class OfflineDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => EquipmentAbilityContentJsonAuthoringDomain.DomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<EquipmentAbilityContentPackImportModel> batch =
                CreateImportDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(
                DomainId,
                batch.Entries.Count,
                batch.Diagnostics
            );
        }
    }
}

[Description("Equipment ability authoring document. Production files contain expanded entries; templates remain file-local schema controls only.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EquipmentAbilityJsonDocumentDto
{
    [JsonPropertyName("schema")]
    [JsonRequired]
    [ContentJsonSchemaConst(EquipmentAbilityContentJsonAuthoringDomain.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain")]
    [JsonRequired]
    [ContentJsonSchemaConst(EquipmentAbilityContentJsonAuthoringDomain.DomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family")]
    [JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates")]
    [JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, EquipmentAbilityContentPackJsonDto> Templates { get; init; } =
        new ReadOnlyDictionary<string, EquipmentAbilityContentPackJsonDto>(
            new Dictionary<string, EquipmentAbilityContentPackJsonDto>()
        );

    [JsonPropertyName("entries")]
    [JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        EquipmentAbilityContentJsonAuthoringDomain.EntryIdPropertyName,
        "template"
    )]
    public IReadOnlyList<EquipmentAbilityContentPackJsonDto> Entries { get; init; } =
        Array.Empty<EquipmentAbilityContentPackJsonDto>();
}

internal static class EquipmentAbilityJsonImportRules
{
    internal const string InvalidDto = "equipment_ability.dto.invalid_entry";
    internal const string UnknownKind = "equipment_ability.dto.kind.unknown";
    internal const string InvalidPayload = "equipment_ability.dto.payload.invalid";
}

internal static class EquipmentAbilityJsonImportParser
{
    internal static ContentImportStageResult<EquipmentAbilityContentPackImportModel> Parse(
        JsonContentEntryContext context,
        string json
    )
    {
        ContentImportStageResult<EquipmentAbilityContentPackJsonDto> parsed =
            ContentJsonStrictDtoParser.Parse(
                context,
                json ?? "",
                EquipmentAbilityJsonSerializerContext.Default.EquipmentAbilityContentPackJsonDto,
                EquipmentAbilityJsonImportRules.InvalidDto
            );
        if (!parsed.HasValue)
            return ContentImportStageResult<EquipmentAbilityContentPackImportModel>.Failure(parsed.Diagnostics);

        var diagnostics = new List<ContentJsonDiagnostic>();
        EquipmentAbilityContentPackImportModel import =
            EquipmentAbilityImportGraphMapper.FromDto(
                parsed.Value,
                context,
                context.JsonPointer,
                diagnostics
            );
        return diagnostics.Count == 0
            ? ContentImportStageResult<EquipmentAbilityContentPackImportModel>.Success(import)
            : ContentImportStageResult<EquipmentAbilityContentPackImportModel>.Failure(diagnostics);
    }
}

internal static class EquipmentAbilityImportCanonicalJson
{
    internal static string WriteSingleEntry(EquipmentAbilityContentPackImportModel import)
    {
        ArgumentNullException.ThrowIfNull(import);
        return WriteDocument(import.pack_id, new[] { import });
    }

    internal static string WriteDocument(
        string family,
        IReadOnlyList<EquipmentAbilityContentPackImportModel> imports
    )
    {
        ArgumentNullException.ThrowIfNull(imports);
        var entries = new List<EquipmentAbilityContentPackJsonDto>(imports.Count);
        foreach (EquipmentAbilityContentPackImportModel import in imports)
            entries.Add(EquipmentAbilityImportGraphMapper.ToDto(import));
        var document = new EquipmentAbilityJsonDocumentDto
        {
            Schema = EquipmentAbilityContentJsonAuthoringDomain.SchemaVersion,
            Domain = EquipmentAbilityContentJsonAuthoringDomain.DomainId,
            Family = family ?? "",
            Entries = new ReadOnlyCollection<EquipmentAbilityContentPackJsonDto>(entries),
        };
        return JsonSerializer.Serialize(
            document,
            EquipmentAbilityJsonSerializerContext.Default.EquipmentAbilityJsonDocumentDto
        ) + "\n";
    }
}
