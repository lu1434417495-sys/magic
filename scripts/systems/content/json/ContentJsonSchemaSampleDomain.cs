#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;

internal static class ContentJsonSchemaCatalog
{
    private static readonly IReadOnlyList<ContentJsonSchemaDomainRegistration> Domains =
        BuildDomains();

    private static IReadOnlyList<ContentJsonSchemaDomainRegistration> BuildDomains()
    {
        var domains = new List<ContentJsonSchemaDomainRegistration>
        {
            new(
                domainId: "schema_fixture",
                schemaVersion: 1,
                documentDtoType: typeof(ContentJsonSchemaSampleDocumentDto),
                title: "Magic content JSON schema exporter fixture",
                description:
                    "Minimal non-gameplay domain proving the shared DTO-reflected content JSON schema contract. Future migration domains register their real document DTO here.",
                trackedSchemaPath: "res://data/schemas/content/schema_fixture.schema.json",
                contentFileMatch: "/data/configs/json/schema_fixture/**/*.json"
            ),
            SkillContentJsonAuthoringDomain.SchemaRegistration,
            EnemyContentJsonDomains.BrainSchemaRegistration,
            EnemyContentJsonDomains.TemplateSchemaRegistration,
            EnemyContentJsonDomains.RosterSchemaRegistration,
            BattleEncounterJsonDomain.SchemaRegistration,
            BarrierJsonDomains.ProfileSchemaRegistration,
            BarrierJsonDomains.LayerSchemaRegistration,
            BattleSpecialProfileJsonDomains.ManifestSchemaRegistration,
            BattleSpecialProfileJsonDomains.ProfileSchemaRegistration,
            QuestJsonContentDomain.SchemaRegistration,
            ContingencyJsonContentDomain.SchemaRegistration,
        };
        domains.AddRange(ProfessionIdentityJsonDomains.SchemaRegistrations);
        domains.AddRange(BattleSimJsonContentDomains.SchemaRegistrations);
        domains.AddRange(WorldJsonDomains.SchemaRegistrations);
        return Array.AsReadOnly(domains.ToArray());
    }

    internal static IReadOnlyList<ContentJsonSchemaDomainRegistration> All => Domains;

    internal static ContentJsonSchemaDomainRegistration Require(string domainId)
    {
        ContentJsonSchemaDomainRegistration? registration = Domains.SingleOrDefault(domain =>
            string.Equals(domain.DomainId, domainId, StringComparison.Ordinal)
        );
        return registration
            ?? throw new ArgumentException(
                $"Unknown content JSON schema domain '{domainId}'.",
                nameof(domainId)
            );
    }
}

[Description("Strict document envelope for the schema-exporter fixture domain.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContentJsonSchemaSampleDocumentDto
{
    [JsonPropertyName("schema")]
    [JsonRequired]
    [ContentJsonSchemaConst(1)]
    [Description("Exact document schema version.")]
    public int Schema { get; init; }

    [JsonPropertyName("domain")]
    [JsonRequired]
    [ContentJsonSchemaConst("schema_fixture")]
    [Description("Exact registered content domain identifier.")]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family")]
    [JsonRequired]
    [ContentJsonSchemaConst("fixture")]
    [Description("Fixture family label used only by this sample domain.")]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates")]
    [JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    [Description("File-local partial templates keyed by template name.")]
    public IReadOnlyDictionary<string, ContentJsonSchemaSampleEntryDto> Templates
    {
        get;
        init;
    } = new ReadOnlyDictionary<string, ContentJsonSchemaSampleEntryDto>(
        new Dictionary<string, ContentJsonSchemaSampleEntryDto>()
    );

    [JsonPropertyName("entries")]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        "fixture_id",
        "template"
    )]
    [Description("Concrete entries in source order; C# required metadata makes this key required.")]
    public required IReadOnlyList<ContentJsonSchemaSampleEntryDto> Entries { get; init; }
}

[Description("Concrete fixture entry demonstrating names, required fields, arrays, nullability, enums, stable business strings, and closed-kind payloads.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContentJsonSchemaSampleEntryDto
{
    [JsonPropertyName("fixture_id")]
    [JsonRequired]
    [Description("Stable entry identifier.")]
    public string FixtureId { get; init; } = "";

    [JsonPropertyName("display_name")]
    [Description("C# required metadata makes this property required without JsonRequired.")]
    public required string DisplayName { get; init; }

    [JsonPropertyName("mode")]
    [JsonRequired]
    public ContentJsonSchemaSampleMode Mode { get; init; }

    [JsonPropertyName("quality")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(ContentJsonSchemaSampleQualityValues))]
    [Description("Stable business string supplied by a code-side closed value provider.")]
    public string Quality { get; init; } = "";

    [JsonPropertyName("tags")]
    [JsonRequired]
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("optional_note")]
    [Description("Nullable but optional; when present, null is legal.")]
    public string? OptionalNote { get; init; }

    [JsonPropertyName("action")]
    [JsonRequired]
    public ContentJsonSchemaSampleActionDto Action { get; init; } = null!;

    [JsonPropertyName("settings")]
    [ContentJsonSchemaDisallowExplicitNull]
    [Description("Optional nested object used to prove recursive partial template schemas.")]
    public ContentJsonSchemaSampleSettingsDto? Settings { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContentJsonSchemaSampleSettingsDto
{
    [JsonPropertyName("label")]
    [JsonRequired]
    public string Label { get; init; } = "";

    [JsonPropertyName("options")]
    [JsonRequired]
    public IReadOnlyDictionary<string, ContentJsonSchemaSampleOptionDto> Options { get; init; } =
        new ReadOnlyDictionary<string, ContentJsonSchemaSampleOptionDto>(
            new Dictionary<string, ContentJsonSchemaSampleOptionDto>()
        );

    [JsonPropertyName("steps")]
    [JsonRequired]
    public IReadOnlyList<ContentJsonSchemaSampleStepDto> Steps { get; init; } =
        Array.Empty<ContentJsonSchemaSampleStepDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContentJsonSchemaSampleOptionDto
{
    [JsonPropertyName("value")]
    [JsonRequired]
    public string Value { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContentJsonSchemaSampleStepDto
{
    [JsonPropertyName("name")]
    [JsonRequired]
    public string Name { get; init; } = "";

    [JsonPropertyName("action")]
    [JsonRequired]
    public ContentJsonSchemaSampleActionDto Action { get; init; } = null!;
}

[JsonConverter(typeof(JsonStringEnumConverter<ContentJsonSchemaSampleMode>))]
internal enum ContentJsonSchemaSampleMode
{
    Manual,
    Automatic,
}

internal sealed class ContentJsonSchemaSampleQualityValues
    : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } =
        Array.AsReadOnly(new[] { "common", "rare", "legendary" });
}

[Description("Closed action union. The kind spec supplies stable discriminator values and payload DTO types; branch fields remain reflected from this DTO.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(ContentJsonSchemaSampleActionKinds))]
internal sealed class ContentJsonSchemaSampleActionDto
{
    [JsonPropertyName("kind")]
    [JsonRequired]
    public string Kind { get; init; } = "";

    [JsonPropertyName("payload")]
    [JsonRequired]
    public object Payload { get; init; } = new();
}

internal sealed class ContentJsonSchemaSampleActionKinds : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "kind";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } =
        Array.AsReadOnly(
            new[]
            {
                new ContentJsonSchemaClosedKindBranch(
                    "counter",
                    typeof(ContentJsonSchemaSampleCounterPayloadDto)
                ),
                new ContentJsonSchemaClosedKindBranch(
                    "message",
                    typeof(ContentJsonSchemaSampleMessagePayloadDto)
                ),
            }
        );
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContentJsonSchemaSampleCounterPayloadDto
{
    [JsonPropertyName("amount")]
    [JsonRequired]
    public int Amount { get; init; }

    [JsonPropertyName("limit")]
    public int? Limit { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContentJsonSchemaSampleMessagePayloadDto
{
    [JsonPropertyName("text")]
    [JsonRequired]
    public string Text { get; init; } = "";

    [JsonPropertyName("audience")]
    public IReadOnlyList<string?> Audience { get; init; } = Array.Empty<string?>();
}
