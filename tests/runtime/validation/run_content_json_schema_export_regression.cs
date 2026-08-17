#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

public partial class run_content_json_schema_export_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestGeneratedSchemaReflectsDtoMetadata();
            TestTrackedSchemaIsByteExact();
            TestGenerationIsDeterministicAndDtoChangesProduceDrift();
            TestUnknownReferenceNullabilityFailsClosed();
            TestEnumConverterMustTargetTheReflectedEnum();
            TestClosedKindCarrierShapeFailsClosed();
            TestDefinitionKeyCollisionsFailClosedInBothOrders();
            TestWorkspaceAssociationMatchesRegistration();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected content JSON schema exporter exception: {exception}");
        }

        RequestTestExit(_test.Finish("Content JSON schema exporter regression"));
    }

    private void TestGeneratedSchemaReflectsDtoMetadata()
    {
        ContentJsonSchemaDomainRegistration registration =
            ContentJsonSchemaCatalog.Require("schema_fixture");
        string generated = new ContentJsonSchemaExporter().Export(registration);
        using JsonDocument document = JsonDocument.Parse(generated);
        JsonElement root = document.RootElement;

        _test.Eq(
            root.GetProperty("$schema").GetString(),
            "https://json-schema.org/draft/2020-12/schema",
            "export should declare the canonical JSON Schema draft"
        );
        _test.Eq(
            root.GetProperty("$id").GetString(),
            "urn:magic:content-schema:schema_fixture:v1",
            "schema identity should be path-free and domain/version stable"
        );

        JsonElement documentSchema = Definition(root, nameof(ContentJsonSchemaSampleDocumentDto));
        _test.False(
            documentSchema.GetProperty("additionalProperties").GetBoolean(),
            "JsonUnmappedMemberHandling.Disallow should close the document object"
        );
        AssertRequired(documentSchema, "domain", "JsonRequired should export a required field");
        AssertRequired(
            documentSchema,
            "entries",
            "C# required metadata should export a required field"
        );
        _test.Eq(
            documentSchema.GetProperty("properties").GetProperty("domain").GetProperty("const").GetString(),
            "schema_fixture",
            "code-side const metadata should constrain the domain"
        );

        JsonElement entrySchema = Definition(root, nameof(ContentJsonSchemaSampleEntryDto));
        AssertRequired(entrySchema, "fixture_id", "JsonRequired entry field should be required");
        AssertRequired(
            entrySchema,
            "display_name",
            "C# required entry field should be required"
        );
        JsonElement entryProperties = entrySchema.GetProperty("properties");
        _test.True(
            entryProperties.TryGetProperty("fixture_id", out _),
            "explicit JsonPropertyName should own the wire field name"
        );
        _test.False(
            entryProperties.TryGetProperty("FixtureId", out _),
            "CLR property names must not leak into the schema"
        );

        JsonElement tags = entryProperties.GetProperty("tags");
        _test.Eq(tags.GetProperty("type").GetString(), "array", "list should export as array");
        _test.Eq(
            tags.GetProperty("items").GetProperty("type").GetString(),
            "string",
            "array element nullability/type should be reflected"
        );
        JsonElement optionalNote = entryProperties.GetProperty("optional_note");
        _test.True(
            optionalNote.GetProperty("anyOf").EnumerateArray().Any(item =>
                item.TryGetProperty("type", out JsonElement type)
                && type.GetString() == "null"
            ),
            "nullable reference metadata should add a null branch"
        );

        AssertEnumValues(entryProperties.GetProperty("mode"), "Manual", "Automatic");
        AssertEnumValues(
            entryProperties.GetProperty("quality"),
            "common",
            "rare",
            "legendary"
        );

        JsonElement action = Definition(root, nameof(ContentJsonSchemaSampleActionDto));
        _test.Eq(
            action.GetProperty("oneOf").GetArrayLength(),
            2,
            "closed kind should export one reflected branch per spec entry"
        );
        _test.Eq(
            action.GetProperty("discriminator").GetProperty("propertyName").GetString(),
            "kind",
            "closed kind should export the configured discriminator"
        );
        JsonElement counterBranch = Definition(
            root,
            nameof(ContentJsonSchemaSampleActionDto) + "__counter"
        );
        _test.Eq(
            counterBranch.GetProperty("properties").GetProperty("kind").GetProperty("const").GetString(),
            "counter",
            "each oneOf branch should constrain kind with const"
        );
        _test.Eq(
            counterBranch.GetProperty("properties").GetProperty("payload").GetProperty("$ref").GetString(),
            "#/$defs/ContentJsonSchemaSampleCounterPayloadDto",
            "branch payload shape should point at its reflected payload DTO"
        );
        JsonElement counterPayload = Definition(
            root,
            nameof(ContentJsonSchemaSampleCounterPayloadDto)
        );
        _test.False(
            counterPayload.GetProperty("additionalProperties").GetBoolean(),
            "closed-kind payload DTO should retain strict unmapped-member handling"
        );
    }

    private void TestTrackedSchemaIsByteExact()
    {
        ContentJsonSchemaDomainRegistration registration =
            ContentJsonSchemaCatalog.Require("schema_fixture");
        string generated = new ContentJsonSchemaExporter().Export(registration);
        using FileAccess file = FileAccess.Open(
            registration.TrackedSchemaPath,
            FileAccess.ModeFlags.Read
        );
        _test.True(file != null, "tracked schema should exist at the registered Godot path");
        if (file == null)
            return;

        string tracked = file.GetAsText(skipCr: false);
        _test.True(
            string.Equals(generated, tracked, StringComparison.Ordinal),
            "regenerated schema must match the tracked artifact byte-for-byte"
        );
    }

    private void TestGenerationIsDeterministicAndDtoChangesProduceDrift()
    {
        ContentJsonSchemaDomainRegistration registration =
            ContentJsonSchemaCatalog.Require("schema_fixture");
        var exporter = new ContentJsonSchemaExporter();
        string first = exporter.Export(registration);
        string second = exporter.Export(registration);
        _test.Eq(second, first, "the same DTO metadata should produce deterministic bytes");

        var changedRegistration = new ContentJsonSchemaDomainRegistration(
            registration.DomainId,
            registration.SchemaVersion,
            typeof(ContentJsonSchemaDriftProbeDto),
            registration.Title,
            registration.Description,
            registration.TrackedSchemaPath,
            registration.ContentFileMatch
        );
        string changed = exporter.Export(changedRegistration);
        _test.Ne(
            changed,
            first,
            "a DTO shape change should produce non-zero schema drift for check mode/CI"
        );
    }

    private void TestUnknownReferenceNullabilityFailsClosed()
    {
        var registration = new ContentJsonSchemaDomainRegistration(
            "unknown_nullability",
            1,
            typeof(ContentJsonSchemaUnknownNullabilityDto),
            "Unknown nullability probe",
            "Regression-only DTO with nullable metadata intentionally disabled.",
            "res://data/schemas/content/unknown_nullability.schema.json",
            "/data/configs/json/unknown_nullability/**/*.json"
        );
        try
        {
            new ContentJsonSchemaExporter().Export(registration);
            _test.Fail("unknown reference nullability should fail closed");
        }
        catch (InvalidOperationException exception)
        {
            _test.True(
                exception.Message.Contains("unknown nullable metadata", StringComparison.Ordinal),
                "unknown nullability failure should explain how to make DTO metadata explicit"
            );
        }
    }

    private void TestEnumConverterMustTargetTheReflectedEnum()
    {
        ExpectExportFailure(
            typeof(ContentJsonSchemaWrongEnumConverterDto),
            "JsonStringEnumConverter<TEnum>",
            "a converter closed over a different enum must not define this property's wire values"
        );
    }

    private void TestClosedKindCarrierShapeFailsClosed()
    {
        ExpectExportFailure(
            typeof(ContentJsonSchemaBadDiscriminatorCarrierDto),
            "non-nullable string carrier",
            "closed-kind discriminator must use the exact non-null string wire carrier"
        );
        ExpectExportFailure(
            typeof(ContentJsonSchemaBadPayloadCarrierDto),
            "non-nullable object carrier",
            "closed-kind payload must remain an explicitly allowed delayed-parse carrier"
        );
    }

    private void TestDefinitionKeyCollisionsFailClosedInBothOrders()
    {
        ExpectExportFailure(
            typeof(ContentJsonSchemaBranchFirstCollisionRootDto),
            "collides between",
            "a later DTO definition must not overwrite an already reserved branch definition"
        );
        ExpectExportFailure(
            typeof(ContentJsonSchemaDtoFirstCollisionRootDto),
            "collides between",
            "a later branch definition must not overwrite an already reserved DTO definition"
        );
    }

    private void TestWorkspaceAssociationMatchesRegistration()
    {
        ContentJsonSchemaDomainRegistration registration =
            ContentJsonSchemaCatalog.Require("schema_fixture");
        using FileAccess file = FileAccess.Open("res://magic.code-workspace", FileAccess.ModeFlags.Read);
        _test.True(file != null, "tracked workspace schema association should exist");
        if (file == null)
            return;

        using JsonDocument workspace = JsonDocument.Parse(file.GetAsText(skipCr: false));
        JsonElement schemas = workspace.RootElement
            .GetProperty("settings")
            .GetProperty("json.schemas");
        JsonElement association = schemas.EnumerateArray().SingleOrDefault(candidate =>
            candidate.GetProperty("fileMatch").EnumerateArray().Any(match =>
                match.GetString() == registration.ContentFileMatch
            )
        );
        _test.True(
            association.ValueKind == JsonValueKind.Object,
            "workspace fileMatch should consume the registered sample-domain glob"
        );
        if (association.ValueKind != JsonValueKind.Object)
            return;

        string expectedRelativeSchemaPath =
            "./" + registration.TrackedSchemaPath["res://".Length..];
        _test.Eq(
            association.GetProperty("url").GetString(),
            expectedRelativeSchemaPath,
            "workspace schema URL should match the registered tracked schema path"
        );
    }

    private void ExpectExportFailure(Type documentDtoType, string fragment, string message)
    {
        var registration = new ContentJsonSchemaDomainRegistration(
            "negative_probe",
            1,
            documentDtoType,
            "Negative schema probe",
            "Regression-only invalid DTO metadata.",
            "res://data/schemas/content/negative_probe.schema.json",
            "/data/configs/json/negative_probe/**/*.json"
        );
        try
        {
            new ContentJsonSchemaExporter().Export(registration);
            _test.Fail(message);
        }
        catch (InvalidOperationException exception)
        {
            _test.True(
                exception.Message.Contains(fragment, StringComparison.Ordinal),
                $"{message}; diagnostic should contain '{fragment}' | actual={exception.Message}"
            );
        }
    }

    private void AssertRequired(JsonElement schema, string propertyName, string message)
    {
        _test.True(
            schema.GetProperty("required").EnumerateArray().Any(item =>
                item.GetString() == propertyName
            ),
            message
        );
    }

    private void AssertEnumValues(JsonElement schema, params string[] expected)
    {
        string[] actual = schema.GetProperty("enum")
            .EnumerateArray()
            .Select(item => item.GetString() ?? "")
            .ToArray();
        _test.True(
            actual.SequenceEqual(expected),
            $"schema enum values should match code-side stable order | actual={string.Join(",", actual)}"
        );
    }

    private static JsonElement Definition(JsonElement root, string name) =>
        root.GetProperty("$defs").GetProperty(name);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaDriftProbeDto
    {
        [JsonPropertyName("changed_field")]
        [JsonRequired]
        public string ChangedField { get; init; } = "";
    }

    private enum ContentJsonSchemaExpectedWireEnum
    {
        Expected,
    }

    private enum ContentJsonSchemaDifferentWireEnum
    {
        Different,
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaWrongEnumConverterDto
    {
        [JsonPropertyName("value")]
        [JsonRequired]
        [JsonConverter(typeof(JsonStringEnumConverter<ContentJsonSchemaDifferentWireEnum>))]
        public ContentJsonSchemaExpectedWireEnum Value { get; init; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    [ContentJsonSchemaClosedKind(typeof(ContentJsonSchemaInvalidCarrierKinds))]
    private sealed class ContentJsonSchemaBadDiscriminatorCarrierDto
    {
        [JsonPropertyName("kind")]
        [JsonRequired]
        public int Kind { get; init; }

        [JsonPropertyName("payload")]
        [JsonRequired]
        public object Payload { get; init; } = new();
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    [ContentJsonSchemaClosedKind(typeof(ContentJsonSchemaInvalidCarrierKinds))]
    private sealed class ContentJsonSchemaBadPayloadCarrierDto
    {
        [JsonPropertyName("kind")]
        [JsonRequired]
        public string Kind { get; init; } = "";

        [JsonPropertyName("payload")]
        [JsonRequired]
        public string Payload { get; init; } = "";
    }

    private sealed class ContentJsonSchemaInvalidCarrierKinds
        : IContentJsonSchemaClosedKindSpec
    {
        public string DiscriminatorPropertyName => "kind";
        public string PayloadPropertyName => "payload";
        public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } =
            Array.AsReadOnly(
                new[]
                {
                    new ContentJsonSchemaClosedKindBranch(
                        "probe",
                        typeof(ContentJsonSchemaCollisionPayloadDto)
                    ),
                }
            );
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    [ContentJsonSchemaClosedKind(typeof(ContentJsonSchemaCollisionActionKinds))]
    private sealed class ContentJsonSchemaCollisionActionDto
    {
        [JsonPropertyName("kind")]
        [JsonRequired]
        public string Kind { get; init; } = "";

        [JsonPropertyName("payload")]
        [JsonRequired]
        public object Payload { get; init; } = new();
    }

    private sealed class ContentJsonSchemaCollisionActionKinds
        : IContentJsonSchemaClosedKindSpec
    {
        public string DiscriminatorPropertyName => "kind";
        public string PayloadPropertyName => "payload";
        public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } =
            Array.AsReadOnly(
                new[]
                {
                    new ContentJsonSchemaClosedKindBranch(
                        "branch",
                        typeof(ContentJsonSchemaCollisionPayloadDto)
                    ),
                }
            );
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaCollisionPayloadDto
    {
        [JsonPropertyName("value")]
        [JsonRequired]
        public int Value { get; init; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaCollisionActionDto__branch
    {
        [JsonPropertyName("collision")]
        [JsonRequired]
        public bool Collision { get; init; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaBranchFirstCollisionRootDto
    {
        [JsonPropertyName("a_action")]
        [JsonRequired]
        public ContentJsonSchemaCollisionActionDto Action { get; init; } = null!;

        [JsonPropertyName("z_collision")]
        [JsonRequired]
        public ContentJsonSchemaCollisionActionDto__branch Collision { get; init; } = null!;
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaDtoFirstCollisionRootDto
    {
        [JsonPropertyName("a_collision")]
        [JsonRequired]
        public ContentJsonSchemaCollisionActionDto__branch Collision { get; init; } = null!;

        [JsonPropertyName("z_action")]
        [JsonRequired]
        public ContentJsonSchemaCollisionActionDto Action { get; init; } = null!;
    }

#nullable disable annotations
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaUnknownNullabilityDto
    {
        [JsonPropertyName("unknown")]
        [JsonRequired]
        public string Unknown { get; init; }
    }
#nullable restore annotations
}
