#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Godot;

public partial class run_content_json_schema_export_regression : LifecycleTestSceneTree
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestGeneratedSchemaReflectsDtoMetadata();
            TestEntryControlMembersAreSchemaOnly();
            TestRecursivePartialTemplatesPreserveReplaceOnlyArrays();
            TestOptionalNullableCarrierCanDisallowExplicitNull();
            TestScalarOrStringArrayDictionaryMetadataFailsClosed();
            TestSkillPilotSchemaReflectsCurrentImportContract();
            TestSkillSchemaValueProvidersMatchParserEnums();
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

        JsonElement entryAuthoringSchema = ReferencedDefinition(
            root,
            documentSchema.GetProperty("properties")
                .GetProperty("entries")
                .GetProperty("items")
        );
        JsonElement entrySchema = EntryAuthoringBranch(
            root,
            entryAuthoringSchema,
            templated: false
        );
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
        _test.False(
            entryProperties.TryGetProperty("template", out _),
            "non-templated authoring entries should remain the strict full DTO"
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

    private void TestEntryControlMembersAreSchemaOnly()
    {
        ContentJsonSchemaDomainRegistration registration =
            ContentJsonSchemaCatalog.Require("schema_fixture");
        string generated = new ContentJsonSchemaExporter().Export(registration);
        using JsonDocument document = JsonDocument.Parse(generated);
        JsonElement root = document.RootElement;
        JsonElement documentSchema = Definition(root, nameof(ContentJsonSchemaSampleDocumentDto));
        JsonElement entryAuthoringSchema = ReferencedDefinition(
            root,
            documentSchema.GetProperty("properties")
                .GetProperty("entries")
                .GetProperty("items")
        );
        _test.Eq(
            entryAuthoringSchema.GetProperty("oneOf").GetArrayLength(),
            2,
            "authoring entries should separate full and templated raw shapes"
        );
        JsonElement fullEntry = EntryAuthoringBranch(
            root,
            entryAuthoringSchema,
            templated: false
        );
        JsonElement templatedEntry = EntryAuthoringBranch(
            root,
            entryAuthoringSchema,
            templated: true
        );

        _test.False(
            typeof(ContentJsonSchemaSampleEntryDto).GetProperties().Any(property =>
                string.Equals(property.Name, "Template", StringComparison.Ordinal)
            ),
            "schema-only template control metadata must not pollute the strict post-merge DTO"
        );
        _test.True(
            templatedEntry.GetProperty("properties")
                .TryGetProperty("template", out JsonElement template),
            "templated raw entry branch should expose template"
        );
        _test.Eq(
            template.GetProperty("type").GetString(),
            "string",
            "template control member should remain a non-null string when present"
        );
        string templatePattern = template.GetProperty("pattern").GetString() ?? "";
        _test.Eq(
            templatePattern,
            ContentJsonSchemaExporter.NonBlankStringPattern,
            "template control should export the shared non-blank pattern"
        );
        _test.False(
            Regex.IsMatch("", templatePattern) || Regex.IsMatch(" \t", templatePattern),
            "template control pattern should reject empty and whitespace-only identifiers"
        );
        _test.True(
            Regex.IsMatch("base", templatePattern),
            "template control pattern should accept a normal parent template identifier"
        );
        AssertRequired(
            templatedEntry,
            "fixture_id",
            "templated raw entries must retain the pre-merge source identity"
        );
        AssertRequired(
            templatedEntry,
            "template",
            "the partial authoring branch must be selected only by an explicit template"
        );
        _test.False(
            templatedEntry.GetProperty("required").EnumerateArray().Any(item =>
                item.GetString() == "display_name"
            ),
            "templated raw entries may inherit ordinary required DTO members"
        );
        _test.False(
            fullEntry.GetProperty("properties").TryGetProperty("template", out _),
            "the full branch should reject template through strict additionalProperties"
        );
        AssertRequired(
            fullEntry,
            "display_name",
            "a partial raw entry without template must fall back to the full DTO branch"
        );

        JsonElement partialAction = ReferencedDefinition(
            root,
            templatedEntry.GetProperty("properties").GetProperty("action")
        );
        JsonElement partialCounterAction = partialAction.GetProperty("anyOf")
            .EnumerateArray()
            .Select(branch => ReferencedDefinition(root, branch))
            .Single(branch =>
                branch.GetProperty("properties")
                    .GetProperty("kind")
                    .GetProperty("const")
                    .GetString() == "counter"
            );
        _test.False(
            partialCounterAction.TryGetProperty("required", out _),
            "templated raw entries should accept payload-only direct closed-kind fragments"
        );

        ExpectExportFailure(
            typeof(ContentJsonSchemaMissingEntryIdMetadataDocumentDto),
            "has no entry ID property",
            "entry-control metadata should fail closed when the named entry ID is absent"
        );
        ExpectExportFailure(
            typeof(ContentJsonSchemaWrongEntryIdCarrierDocumentDto),
            "entry ID property",
            "entry-control metadata should require a non-null string entry ID carrier"
        );
        ExpectExportFailure(
            typeof(ContentJsonSchemaWrongTemplateCarrierDocumentDto),
            "template control property",
            "entry-control metadata should require a non-null string template carrier"
        );
        ExpectExportFailure(
            typeof(ContentJsonSchemaMissingTemplateNonBlankDocumentDto),
            nameof(ContentJsonSchemaNonBlankStringAttribute),
            "entry-control metadata should require an explicit non-blank template selector"
        );
    }

    private void TestOptionalNullableCarrierCanDisallowExplicitNull()
    {
        var registration = new ContentJsonSchemaDomainRegistration(
            "explicit_null_probe",
            1,
            typeof(ContentJsonSchemaExplicitNullProbeDto),
            "Explicit null probe",
            "Regression-only nullable-carrier schema metadata.",
            "res://data/schemas/content/explicit_null_probe.schema.json",
            "/data/configs/json/explicit_null_probe/**/*.json"
        );
        string generated = new ContentJsonSchemaExporter().Export(registration);
        using JsonDocument document = JsonDocument.Parse(generated);
        JsonElement properties = Definition(
            document.RootElement,
            nameof(ContentJsonSchemaExplicitNullProbeDto)
        ).GetProperty("properties");

        JsonElement absentOnly = properties.GetProperty("absent_only");
        _test.Eq(
            absentOnly.GetProperty("type").GetString(),
            "integer",
            "schema-only explicit-null metadata should keep a nullable carrier optional but non-null"
        );
        _test.False(
            absentOnly.TryGetProperty("anyOf", out _),
            "explicit-null-forbidden carrier should not export a null branch"
        );
        JsonElement nullable = properties.GetProperty("nullable");
        _test.True(
            nullable.GetProperty("anyOf").EnumerateArray().Any(item =>
                item.TryGetProperty("type", out JsonElement type)
                && type.GetString() == "null"
            ),
            "ordinary nullable schema behavior must remain unchanged"
        );

        ExpectExportFailure(
            typeof(ContentJsonSchemaInvalidExplicitNullProbeDto),
            "requires a nullable CLR carrier",
            "explicit-null metadata on a non-nullable carrier must fail closed"
        );
    }

    private void TestRecursivePartialTemplatesPreserveReplaceOnlyArrays()
    {
        ContentJsonSchemaDomainRegistration registration =
            ContentJsonSchemaCatalog.Require("schema_fixture");
        string generated = new ContentJsonSchemaExporter().Export(registration);
        using JsonDocument document = JsonDocument.Parse(generated);
        JsonElement root = document.RootElement;
        JsonElement documentSchema = Definition(root, nameof(ContentJsonSchemaSampleDocumentDto));
        JsonElement partialEntry = ReferencedDefinition(
            root,
            documentSchema.GetProperty("properties")
                .GetProperty("templates")
                .GetProperty("additionalProperties")
        );
        _test.False(
            partialEntry.TryGetProperty("required", out _),
            "template root should be a partial view of the same entry DTO"
        );
        _test.True(
            partialEntry.GetProperty("properties").TryGetProperty("template", out _),
            "template roots should compose the schema-only parent-template selector"
        );

        JsonElement partialAction = ReferencedDefinition(
            root,
            partialEntry.GetProperty("properties").GetProperty("action")
        );
        _test.False(
            partialAction.TryGetProperty("oneOf", out _),
            "direct closed-kind template objects must not use exclusive full branches"
        );
        _test.Eq(
            partialAction.GetProperty("anyOf").GetArrayLength(),
            2,
            "direct closed-kind template objects should expose all partial kind branches"
        );
        JsonElement partialCounterAction = partialAction.GetProperty("anyOf")
            .EnumerateArray()
            .Select(branch => ReferencedDefinition(root, branch))
            .Single(branch =>
                branch.GetProperty("properties")
                    .GetProperty("kind")
                    .GetProperty("const")
                    .GetString() == "counter"
            );
        _test.False(
            partialCounterAction.TryGetProperty("required", out _),
            "a direct closed-kind template branch should allow kind-only or payload-only layers"
        );
        JsonElement partialCounterPayload = ReferencedDefinition(
            root,
            partialCounterAction.GetProperty("properties").GetProperty("payload")
        );
        _test.False(
            partialCounterPayload.TryGetProperty("required", out _),
            "direct closed-kind payload objects should recursively support deep-merge fragments"
        );

        JsonElement partialSettings = ReferencedDefinition(
            root,
            partialEntry.GetProperty("properties").GetProperty("settings")
        );
        _test.False(
            partialSettings.TryGetProperty("required", out _),
            "nested template objects should recursively remove required members"
        );

        JsonElement partialOption = ReferencedDefinition(
            root,
            partialSettings.GetProperty("properties")
                .GetProperty("options")
                .GetProperty("additionalProperties")
        );
        _test.False(
            partialOption.TryGetProperty("required", out _),
            "dictionary object values should remain recursive partial template views"
        );

        JsonElement fullStep = ReferencedDefinition(
            root,
            partialSettings.GetProperty("properties")
                .GetProperty("steps")
                .GetProperty("items")
        );
        AssertRequired(
            fullStep,
            "name",
            "replace-only array elements should keep their full DTO requirements"
        );
        AssertRequired(
            fullStep,
            "action",
            "replace-only array elements should keep nested closed-kind values complete"
        );
        JsonElement fullAction = ReferencedDefinition(
            root,
            fullStep.GetProperty("properties").GetProperty("action")
        );
        _test.True(
            fullAction.GetProperty("oneOf").GetArrayLength() > 0,
            "closed-kind payloads reached through replace-only arrays should remain full unions"
        );
        _test.False(
            fullAction.TryGetProperty("anyOf", out _),
            "replace-only array elements must not receive partial closed-kind branches"
        );
    }

    private void TestScalarOrStringArrayDictionaryMetadataFailsClosed()
    {
        ExpectExportFailure(
            typeof(ContentJsonSchemaWrongScalarDictionaryValueDto),
            "value carrier is JsonElement",
            "scalar-or-string-array dictionary metadata should reject a copied string DTO shape"
        );
        ExpectExportFailure(
            typeof(ContentJsonSchemaNonStringScalarDictionaryKeyDto),
            "must use string keys",
            "scalar-or-string-array dictionary metadata should reject non-string keys"
        );
        ExpectExportFailure(
            typeof(ContentJsonSchemaCombinedScalarDictionaryShapeDto),
            "cannot combine multiple schema-only shape metadata attributes",
            "scalar-or-string-array dictionary metadata should remain mutually exclusive"
        );
        ExpectExportFailure(
            typeof(ContentJsonSchemaPartialCombinedScalarDictionaryShapeDto),
            "cannot combine multiple schema-only shape metadata attributes",
            "recursive partial DTOs should independently reject combined shape metadata"
        );
    }

    private void TestSkillPilotSchemaReflectsCurrentImportContract()
    {
        ContentJsonSchemaDomainRegistration registration =
            ContentJsonSchemaCatalog.Require(SkillContentJsonAuthoringDomain.DomainId);
        string generated = new ContentJsonSchemaExporter().Export(registration);
        using JsonDocument document = JsonDocument.Parse(generated);
        JsonElement root = document.RootElement;
        JsonElement documentSchema = Definition(root, nameof(SkillJsonDocumentDto));
        _test.Eq(
            documentSchema.GetProperty("properties").GetProperty("domain").GetProperty("const").GetString(),
            "skills",
            "skill schema document should lock the registered domain"
        );

        JsonElement entryAuthoring = ReferencedDefinition(
            root,
            documentSchema.GetProperty("properties")
                .GetProperty("entries")
                .GetProperty("items")
        );
        JsonElement entry = EntryAuthoringBranch(root, entryAuthoring, templated: false);
        JsonElement templatedEntry = EntryAuthoringBranch(
            root,
            entryAuthoring,
            templated: true
        );
        AssertRequired(entry, "skill_id", "skill entry should require its stable ID");
        AssertRequired(entry, "display_name", "skill entry should require its display name");
        _test.True(
            templatedEntry.GetProperty("properties").TryGetProperty("template", out _),
            "templated skill entries should expose only the schema-side template selector"
        );
        AssertRequired(
            templatedEntry,
            "skill_id",
            "templated skill entries must preserve their raw pre-merge identity"
        );
        AssertRequired(
            templatedEntry,
            "template",
            "templated skill entries should require the authoring control member"
        );
        _test.False(
            templatedEntry.GetProperty("required").EnumerateArray().Any(item =>
                item.GetString() == "display_name"
            ),
            "templated skill entries may inherit display_name from their template chain"
        );
        AssertEnumValues(
            entry.GetProperty("properties").GetProperty("skill_type"),
            "active",
            "passive"
        );
        _test.Eq(
            entry.GetProperty("properties").GetProperty("max_level").GetProperty("type").GetString(),
            "integer",
            "skill max_level should be optional but explicit-null-forbidden"
        );
        JsonElement contingency = ReferencedDefinition(
            root,
            NonNullBranch(
                entry.GetProperty("properties")
                    .GetProperty("contingency_automation_profile")
            )
        );
        JsonElement fullParameterBindings = contingency.GetProperty("properties")
            .GetProperty("allowed_parameter_bindings");
        AssertScalarOrStringArrayDictionarySchema(fullParameterBindings);

        JsonElement combat = ReferencedDefinition(
            root,
            NonNullBranch(entry.GetProperty("properties").GetProperty("combat_profile"))
        );
        AssertEnumValues(
            combat.GetProperty("properties").GetProperty("target_team_filter"),
            "self",
            "ally",
            "enemy",
            "any"
        );
        JsonElement effect = ReferencedDefinition(
            root,
            combat.GetProperty("properties")
                .GetProperty("effect_defs")
                .GetProperty("items")
        );
        _test.Eq(
            effect.GetProperty("oneOf").GetArrayLength(),
            1,
            "stage-1a skill schema should truthfully expose only the layered_barrier pilot kind"
        );
        JsonElement layeredBranch = ReferencedDefinition(
            root,
            effect.GetProperty("oneOf").EnumerateArray().Single()
        );
        _test.Eq(
            layeredBranch.GetProperty("properties").GetProperty("effect_type").GetProperty("const").GetString(),
            "layered_barrier",
            "pilot closed-kind branch should lock its wire discriminator"
        );
        JsonElement payload = ReferencedDefinition(
            root,
            layeredBranch.GetProperty("properties").GetProperty("payload")
        );
        AssertRequired(payload, "profile_id", "layered barrier payload should require profile_id");
        AssertEnumValues(
            payload.GetProperty("properties").GetProperty("area_pattern"),
            "single",
            "self",
            "diamond",
            "square",
            "radius",
            "cross",
            "line",
            "cone",
            "narrow_cone",
            "front_arc"
        );
        JsonElement castVariant = ReferencedDefinition(
            root,
            combat.GetProperty("properties")
                .GetProperty("cast_variants")
                .GetProperty("items")
        );
        JsonElement castPayloadReference = castVariant.GetProperty("properties")
            .GetProperty("payload");
        _test.False(
            castPayloadReference.TryGetProperty("anyOf", out _),
            "cast payload may be omitted but must reject explicit null"
        );
        JsonElement castPayload = ReferencedDefinition(root, castPayloadReference);
        _test.False(
            castPayload.GetProperty("additionalProperties").GetBoolean(),
            "cast payload should be closed by its strict typed DTO"
        );
        _test.Eq(
            castPayload.GetProperty("properties").EnumerateObject().Single().Name,
            "square2_corner",
            "cast payload should expose only the registered square2_corner member"
        );

        JsonElement partialEntry = ReferencedDefinition(
            root,
            documentSchema.GetProperty("properties")
                .GetProperty("templates")
                .GetProperty("additionalProperties")
        );
        _test.False(
            partialEntry.TryGetProperty("required", out _),
            "skill templates should reuse a recursive partial view of SkillJsonDto"
        );
        _test.True(
            partialEntry.GetProperty("properties").TryGetProperty("template", out _),
            "skill template roots should support parent-template chaining"
        );
        JsonElement partialCombat = ReferencedDefinition(
            root,
            NonNullBranch(
                partialEntry.GetProperty("properties").GetProperty("combat_profile")
            )
        );
        _test.False(
            partialCombat.TryGetProperty("required", out _),
            "nested skill combat template objects should remain partial"
        );
        JsonElement partialOverride = ReferencedDefinition(
            root,
            partialCombat.GetProperty("properties")
                .GetProperty("level_overrides")
                .GetProperty("additionalProperties")
        );
        JsonElement pendingMode = partialOverride.GetProperty("properties")
            .GetProperty("pending_cast_binding_mode");
        _test.False(
            pendingMode.TryGetProperty("anyOf", out _),
            "optional override carriers should remain explicit-null-forbidden in templates"
        );
        AssertEnumValues(
            pendingMode,
            "soft_anchor",
            "hard_anchor",
            "ground_bind"
        );
        JsonElement partialCastVariant = ReferencedDefinition(
            root,
            partialCombat.GetProperty("properties")
                .GetProperty("cast_variants")
                .GetProperty("items")
        );
        _test.Eq(
            partialCastVariant.GetProperty("properties")
                .GetProperty("payload")
                .GetRawText(),
            castPayloadReference.GetRawText(),
            "replace-only cast variants should retain the same closed non-null payload schema"
        );
        JsonElement spellReaction = ReferencedDefinition(
            root,
            NonNullBranch(combat.GetProperty("properties").GetProperty("spell_reaction_profile"))
        );
        JsonElement partialSpellReaction = ReferencedDefinition(
            root,
            NonNullBranch(partialCombat.GetProperty("properties").GetProperty("spell_reaction_profile"))
        );
        JsonElement rangedReaction = ReferencedDefinition(
            root,
            NonNullBranch(combat.GetProperty("properties").GetProperty("ranged_weapon_reaction_profile"))
        );
        JsonElement partialRangedReaction = ReferencedDefinition(
            root,
            NonNullBranch(partialCombat.GetProperty("properties").GetProperty("ranged_weapon_reaction_profile"))
        );
        AssertStableStringPropertySchemas(entry, partialEntry, typeof(SkillJsonDto));
        AssertStableStringPropertySchemas(combat, partialCombat, typeof(CombatSkillJsonDto));
        AssertStableStringPropertySchemas(
            Definition(root, nameof(AttributeModifierJsonDto)),
            null,
            typeof(AttributeModifierJsonDto)
        );
        AssertStableStringPropertySchemas(
            spellReaction,
            partialSpellReaction,
            typeof(CombatSpellReactionJsonDto)
        );
        AssertStableStringPropertySchemas(
            rangedReaction,
            partialRangedReaction,
            typeof(CombatRangedWeaponReactionJsonDto)
        );
        AssertStableStringPropertySchemas(
            castVariant,
            partialCastVariant,
            typeof(CombatCastVariantJsonDto)
        );
        AssertStableStringPropertySchemas(
            castPayload,
            null,
            typeof(CombatCastVariantPayloadJsonDto)
        );
        JsonElement partialContingency = ReferencedDefinition(
            root,
            NonNullBranch(
                partialEntry.GetProperty("properties")
                    .GetProperty("contingency_automation_profile")
            )
        );
        JsonElement partialParameterBindings = partialContingency.GetProperty("properties")
            .GetProperty("allowed_parameter_bindings");
        AssertScalarOrStringArrayDictionarySchema(partialParameterBindings);
        _test.Eq(
            partialParameterBindings.GetRawText(),
            fullParameterBindings.GetRawText(),
            "full entries and partial templates must expose the same raw binding value union"
        );

        Type templateValueType = typeof(SkillJsonDocumentDto).GetProperty("Templates")!
            .PropertyType.GetGenericArguments()[1];
        Type entryValueType = typeof(SkillJsonDocumentDto).GetProperty("Entries")!
            .PropertyType.GetGenericArguments()[0];
        _test.Eq(
            templateValueType,
            entryValueType,
            "skill templates and entries must derive from the same DTO instead of a duplicate field list"
        );
    }

    private void TestSkillSchemaValueProvidersMatchParserEnums()
    {
        AssertSkillSchemaProviderParity<SkillImportType>(
            new SkillTypeSchemaValues(),
            SkillJsonImportValueRules.TryParseSkillType,
            "skill_type"
        );
        AssertSkillSchemaProviderParity<SkillImportLearnSource>(
            new SkillLearnSourceSchemaValues(),
            SkillJsonImportValueRules.TryParseLearnSource,
            "learn_source"
        );
        AssertSkillSchemaProviderParity<CombatSkillImportTargetMode>(
            new SkillTargetModeSchemaValues(),
            SkillJsonImportValueRules.TryParseTargetMode,
            "target_mode"
        );
        AssertSkillSchemaProviderParity<CombatSkillImportTargetTeamFilter>(
            new SkillTargetTeamFilterSchemaValues(),
            SkillJsonImportValueRules.TryParseTargetTeamFilter,
            "target_team_filter"
        );
        AssertSkillSchemaProviderParity<CombatSkillImportRangePattern>(
            new SkillRangePatternSchemaValues(),
            SkillJsonImportValueRules.TryParseRangePattern,
            "range_pattern"
        );
        AssertSkillSchemaProviderParity<CombatSkillImportAreaPattern>(
            new SkillAreaPatternSchemaValues(),
            SkillJsonImportValueRules.TryParseAreaPattern,
            "area_pattern"
        );
        AssertSkillSchemaProviderParity<PendingCastBindingModeKind>(
            new SkillPendingCastBindingModeSchemaValues(),
            SkillJsonImportValueRules.TryParsePendingCastBindingMode,
            "pending_cast_binding_mode"
        );
        AssertSkillSchemaProviderParity<CombatSkillLevelOverrideAttackResolutionMode>(
            new SkillAttackResolutionModeSchemaValues(),
            SkillJsonImportValueRules.TryParseLevelOverrideAttackResolutionMode,
            "level_override.attack_resolution_mode"
        );
        AssertSkillSchemaProviderParity<CombatSkillLevelOverrideAttackDefenseMode>(
            new SkillAttackDefenseModeSchemaValues(),
            SkillJsonImportValueRules.TryParseLevelOverrideAttackDefenseMode,
            "level_override.attack_defense_mode"
        );
        AssertSkillSchemaProviderParity<CombatSkillLevelOverrideAreaPattern>(
            new SkillLevelOverrideAreaPatternSchemaValues(),
            SkillJsonImportValueRules.TryParseLevelOverrideAreaPattern,
            "level_override.area_pattern"
        );
        AssertSkillSchemaProviderParity<SkillImportUnlockMode>(
            new SkillUnlockModeSchemaValues(),
            SkillRootCombatImportValueRules.TryUnlockMode,
            "unlock_mode"
        );
        AssertSkillSchemaProviderParity<SkillImportCoreSkillTransitionMode>(
            new SkillCoreTransitionSchemaValues(),
            SkillRootCombatImportValueRules.TryCoreTransition,
            "core_skill_transition_mode"
        );
        AssertSkillSchemaProviderParity<SkillImportProgressionTier>(
            new SkillProgressionTierSchemaValues(),
            SkillRootCombatImportValueRules.TryTier,
            "progression_tier",
            SkillImportProgressionTier.None
        );
        AssertSkillSchemaProviderParity<AttributeModifierImportMode>(
            new SkillAttributeModifierModeSchemaValues(),
            SkillRootCombatImportValueRules.TryAttributeModifierMode,
            "attribute_modifier.mode"
        );
        AssertSkillSchemaProviderParity<CombatWeaponRangePolicyImportKind>(
            new SkillWeaponRangePolicySchemaValues(),
            SkillRootCombatImportValueRules.TryWeaponRangePolicy,
            "weapon_range_policy"
        );
        AssertSkillSchemaProviderParity<CombatMasteryTriggerImportKind>(
            new SkillMasteryTriggerSchemaValues(),
            SkillRootCombatImportValueRules.TryMasteryTrigger,
            "mastery_trigger_mode"
        );
        AssertSkillSchemaProviderParity<CombatMasteryAmountImportKind>(
            new SkillMasteryAmountSchemaValues(),
            SkillRootCombatImportValueRules.TryMasteryAmount,
            "mastery_amount_mode"
        );
        AssertSkillSchemaProviderParity<CombatSpellFateImportKind>(
            new SkillSpellFateSchemaValues(),
            SkillRootCombatImportValueRules.TrySpellFate,
            "spell_fate_mode",
            CombatSpellFateImportKind.None
        );
        AssertSkillSchemaProviderParity<CombatSpellCriticalImportKind>(
            new SkillSpellCriticalSchemaValues(),
            SkillRootCombatImportValueRules.TrySpellCritical,
            "spell_critical_mode",
            CombatSpellCriticalImportKind.None
        );
        AssertSkillSchemaProviderParity<CombatBacklashImportKind>(
            new SkillBacklashSchemaValues(),
            SkillRootCombatImportValueRules.TryBacklash,
            "backlash_mode",
            CombatBacklashImportKind.None
        );
        AssertSkillSchemaProviderParity<CombatAreaOriginImportKind>(
            new SkillAreaOriginSchemaValues(),
            SkillRootCombatImportValueRules.TryAreaOrigin,
            "area_origin_mode"
        );
        AssertSkillSchemaProviderParity<CombatAreaDirectionImportKind>(
            new SkillAreaDirectionSchemaValues(),
            SkillRootCombatImportValueRules.TryAreaDirection,
            "area_direction_mode"
        );
        AssertSkillSchemaProviderParity<CombatBaseProjectileImportKind>(
            new SkillBaseProjectileSchemaValues(),
            SkillRootCombatImportValueRules.TryBaseProjectile,
            "projectile_kind"
        );
        AssertSkillSchemaProviderParity<CombatProjectileImportKind>(
            new SkillBaseProjectileSchemaValues(),
            SkillRootCombatImportValueRules.TryProjectile,
            "cast_variant.projectile_kind_override",
            CombatProjectileImportKind.Inherit
        );
        AssertSkillSchemaProviderParity<CombatTargetSelectionImportKind>(
            new SkillTargetSelectionSchemaValues(),
            SkillRootCombatImportValueRules.TryTargetSelection,
            "target_selection_mode"
        );
        AssertSkillSchemaProviderParity<CombatUnitTargetResolutionImportKind>(
            new SkillUnitTargetResolutionSchemaValues(),
            SkillRootCombatImportValueRules.TryUnitTargetResolution,
            "unit_target_resolution_mode"
        );
        AssertSkillSchemaProviderParity<CombatSelectionOrderImportKind>(
            new SkillSelectionOrderSchemaValues(),
            SkillRootCombatImportValueRules.TrySelectionOrder,
            "selection_order_mode"
        );
        AssertSkillSchemaProviderParity<CombatCastFootprintImportKind>(
            new SkillCastFootprintSchemaValues(),
            SkillRootCombatImportValueRules.TryFootprint,
            "cast_variant.footprint_pattern"
        );
        AssertSkillSchemaProviderParity<CombatSaveAbilityImportKind>(
            new SkillSaveAbilitySchemaValues(),
            SkillRootCombatImportValueRules.TrySaveAbility,
            "spell_reaction.save_ability"
        );
        AssertSkillSchemaProviderParity<DamageTagImportKind>(
            new SkillDamageTagSchemaValues(),
            SkillRootCombatImportValueRules.TryDamageTag,
            "ranged_weapon_reaction.damage_tag"
        );
        AssertSkillSchemaProviderParity<BattleTerrainImportKind>(
            new SkillTerrainSchemaValues(),
            SkillRootCombatImportValueRules.TryTerrain,
            "cast_variant.allowed_base_terrains"
        );
        AssertSkillSchemaProviderParity<CombatCastSquare2Corner>(
            new SkillSquare2CornerSchemaValues(),
            SkillRootCombatImportValueRules.TrySquare2Corner,
            "cast_variant.payload.square2_corner"
        );
    }

    private void AssertSkillSchemaProviderParity<TEnum>(
        IContentJsonSchemaStableStringValues provider,
        TryParseSkillSchemaValue<TEnum> parser,
        string fieldLabel,
        TEnum? implicitOmittedValue = null
    )
        where TEnum : struct, Enum
    {
        IReadOnlyList<string> values = provider.Values;
        _test.Eq(
            values.Distinct(StringComparer.Ordinal).Count(),
            values.Count,
            $"{fieldLabel} schema values should not contain duplicate wire strings"
        );

        var parsedValues = new HashSet<TEnum>();
        foreach (string value in values)
        {
            bool parsed = parser(value, out TEnum result);
            _test.True(parsed, $"{fieldLabel} schema value '{value}' should parse successfully");
            if (!parsed)
                continue;
            _test.True(
                parsedValues.Add(result),
                $"{fieldLabel} schema values should map one-to-one onto enum members"
            );
        }

        _test.False(
            parser("__schema_unknown__", out _),
            $"{fieldLabel} parser must reject strings outside its schema provider"
        );

        TEnum[] expected = Enum.GetValues<TEnum>()
            .Where(value => !implicitOmittedValue.HasValue || !EqualityComparer<TEnum>.Default.Equals(value, implicitOmittedValue.Value))
            .ToArray();
        _test.Eq(
            parsedValues.Count,
            expected.Length,
            $"{fieldLabel} parsed schema values should cover the complete enum"
        );
        foreach (TEnum expectedValue in expected)
        {
            _test.True(
                parsedValues.Contains(expectedValue),
                $"{fieldLabel} schema values should cover enum member {expectedValue}"
            );
        }
    }

    private void AssertStableStringPropertySchemas(
        JsonElement fullSchema,
        JsonElement? partialSchema,
        Type dtoType
    )
    {
        JsonElement fullProperties = fullSchema.GetProperty("properties");
        foreach (PropertyInfo property in dtoType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            ContentJsonSchemaStableStringValuesAttribute? attribute =
                property.GetCustomAttribute<ContentJsonSchemaStableStringValuesAttribute>();
            if (attribute == null)
                continue;

            string jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? throw new InvalidOperationException($"{dtoType.Name}.{property.Name} lacks JsonPropertyName.");
            var provider = (IContentJsonSchemaStableStringValues)(
                Activator.CreateInstance(attribute.ProviderType)
                ?? throw new InvalidOperationException($"Cannot create {attribute.ProviderType.Name}.")
            );
            JsonElement fullProperty = StableStringScalarSchema(fullProperties.GetProperty(jsonName));
            AssertEnumValues(fullProperty, provider.Values.ToArray());
            _test.False(
                fullProperty.GetProperty("enum").EnumerateArray().Any(value => value.GetString() == ""),
                $"{dtoType.Name}.{jsonName} schema must not publish omission-only empty sentinels"
            );

            if (partialSchema.HasValue)
            {
                JsonElement partialProperty = partialSchema.Value.GetProperty("properties").GetProperty(jsonName);
                _test.Eq(
                    partialProperty.GetRawText(),
                    fullProperties.GetProperty(jsonName).GetRawText(),
                    $"{dtoType.Name}.{jsonName} full and partial schemas must match exactly"
                );
            }
        }
    }

    private static JsonElement StableStringScalarSchema(JsonElement propertySchema) =>
        propertySchema.TryGetProperty("items", out JsonElement items) ? items : propertySchema;

    private void TestTrackedSchemaIsByteExact()
    {
        var exporter = new ContentJsonSchemaExporter();
        foreach (
            ContentJsonSchemaDomainRegistration registration in ContentJsonSchemaCatalog.All
        )
        {
            string generated = exporter.Export(registration);
            using FileAccess file = FileAccess.Open(
                registration.TrackedSchemaPath,
                FileAccess.ModeFlags.Read
            );
            _test.True(
                file != null,
                $"tracked schema should exist for domain {registration.DomainId}"
            );
            if (file == null)
                continue;

            long trackedLength = (long)file.GetLength();
            byte[] trackedBytes = file.GetBuffer(trackedLength);
            _test.Eq(
                trackedBytes.LongLength,
                trackedLength,
                $"tracked schema bytes should be read completely for domain {registration.DomainId}"
            );
            string tracked = StrictUtf8.GetString(trackedBytes);
            byte[] generatedBytes = StrictUtf8.GetBytes(generated);
            _test.True(
                string.Equals(generated, tracked, StringComparison.Ordinal),
                $"regenerated schema text must exactly match domain {registration.DomainId}"
            );
            _test.True(
                generatedBytes.SequenceEqual(trackedBytes),
                $"regenerated schema raw UTF-8 bytes must exactly match domain {registration.DomainId}"
            );
            _test.True(
                trackedBytes.Length > 0 && trackedBytes[^1] == (byte)'\n',
                $"tracked schema should end in exactly one LF for domain {registration.DomainId}"
            );
            _test.False(
                trackedBytes.Length > 1 && trackedBytes[^2] == (byte)'\n',
                $"tracked schema should not end in duplicate LF bytes for domain {registration.DomainId}"
            );
            _test.False(
                trackedBytes.Contains((byte)'\r'),
                $"tracked schema should not contain CR bytes for domain {registration.DomainId}"
            );
        }
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
        using FileAccess file = FileAccess.Open("res://magic.code-workspace", FileAccess.ModeFlags.Read);
        _test.True(file != null, "tracked workspace schema association should exist");
        if (file == null)
            return;

        using JsonDocument workspace = JsonDocument.Parse(file.GetAsText(skipCr: false));
        JsonElement schemas = workspace.RootElement
            .GetProperty("settings")
            .GetProperty("json.schemas");
        foreach (
            ContentJsonSchemaDomainRegistration registration in ContentJsonSchemaCatalog.All
        )
        {
            JsonElement association = schemas.EnumerateArray().SingleOrDefault(candidate =>
                candidate.GetProperty("fileMatch").EnumerateArray().Any(match =>
                    match.GetString() == registration.ContentFileMatch
                )
            );
            _test.True(
                association.ValueKind == JsonValueKind.Object,
                $"workspace fileMatch should consume registered domain {registration.DomainId}"
            );
            if (association.ValueKind != JsonValueKind.Object)
                continue;

            string expectedRelativeSchemaPath =
                "./" + registration.TrackedSchemaPath["res://".Length..];
            _test.Eq(
                association.GetProperty("url").GetString(),
                expectedRelativeSchemaPath,
                $"workspace schema URL should match registered domain {registration.DomainId}"
            );
        }
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

    private void AssertScalarOrStringArrayDictionarySchema(JsonElement schema)
    {
        _test.Eq(
            schema.GetProperty("type").GetString(),
            "object",
            "parameter bindings should remain a JSON object keyed by binding ID"
        );
        JsonElement[] branches = schema.GetProperty("additionalProperties")
            .GetProperty("anyOf")
            .EnumerateArray()
            .ToArray();
        _test.Eq(branches.Length, 5, "binding values should expose exactly five closed shapes");
        string[] types = branches
            .Select(branch => branch.GetProperty("type").GetString() ?? "")
            .ToArray();
        _test.True(
            types.SequenceEqual(new[] { "boolean", "integer", "number", "string", "array" }),
            $"binding value shapes should be stable and closed | actual={string.Join(",", types)}"
        );
        _test.False(
            types.Contains("object") || types.Contains("null"),
            "binding value union must not expose object or null compatibility branches"
        );
        JsonElement integer = branches.Single(branch =>
            branch.GetProperty("type").GetString() == "integer"
        );
        _test.Eq(integer.GetProperty("minimum").GetInt64(), long.MinValue, "integer minimum should match Int64 parsing");
        _test.Eq(integer.GetProperty("maximum").GetInt64(), long.MaxValue, "integer maximum should match Int64 parsing");
        JsonElement number = branches.Single(branch =>
            branch.GetProperty("type").GetString() == "number"
        );
        _test.Eq(number.GetProperty("minimum").GetDouble(), -double.MaxValue, "number minimum should reject negative overflow");
        _test.Eq(number.GetProperty("maximum").GetDouble(), double.MaxValue, "number maximum should reject positive overflow");
        JsonElement array = branches.Single(branch =>
            branch.GetProperty("type").GetString() == "array"
        );
        _test.Eq(
            array.GetProperty("items").GetProperty("type").GetString(),
            "string",
            "binding arrays should contain strings only"
        );
    }

    private static JsonElement Definition(JsonElement root, string name) =>
        root.GetProperty("$defs").GetProperty(name);

    private static JsonElement ReferencedDefinition(JsonElement root, JsonElement reference)
    {
        const string prefix = "#/$defs/";
        string value = reference.GetProperty("$ref").GetString() ?? "";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected schema reference '{value}'.");
        return Definition(root, value[prefix.Length..].Replace("~1", "/").Replace("~0", "~"));
    }

    private static JsonElement EntryAuthoringBranch(
        JsonElement root,
        JsonElement authoringSchema,
        bool templated
    ) => authoringSchema.GetProperty("oneOf")
        .EnumerateArray()
        .Select(branch => ReferencedDefinition(root, branch))
        .Single(branch =>
            branch.GetProperty("properties").TryGetProperty("template", out _) == templated
        );

    private static JsonElement NonNullBranch(JsonElement nullableSchema) =>
        nullableSchema.GetProperty("anyOf").EnumerateArray().Single(item =>
            item.TryGetProperty("$ref", out _)
        );

    private delegate bool TryParseSkillSchemaValue<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum;

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaDriftProbeDto
    {
        [JsonPropertyName("changed_field")]
        [JsonRequired]
        public string ChangedField { get; init; } = "";
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaExplicitNullProbeDto
    {
        [JsonPropertyName("absent_only")]
        [ContentJsonSchemaDisallowExplicitNull]
        public int? AbsentOnly { get; init; }

        [JsonPropertyName("nullable")]
        public int? Nullable { get; init; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaInvalidExplicitNullProbeDto
    {
        [JsonPropertyName("value")]
        [ContentJsonSchemaDisallowExplicitNull]
        public int Value { get; init; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaWrongScalarDictionaryValueDto
    {
        [JsonPropertyName("values")]
        [ContentJsonSchemaScalarOrStringArrayDictionaryValues]
        public IReadOnlyDictionary<string, string> Values { get; init; } =
            new Dictionary<string, string>();
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaNonStringScalarDictionaryKeyDto
    {
        [JsonPropertyName("values")]
        [ContentJsonSchemaScalarOrStringArrayDictionaryValues]
        public IReadOnlyDictionary<int, JsonElement> Values { get; init; } =
            new Dictionary<int, JsonElement>();
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaCombinedScalarDictionaryShapeDto
    {
        [JsonPropertyName("values")]
        [ContentJsonSchemaScalarOrStringArrayDictionaryValues]
        [ContentJsonSchemaDisallowExplicitNull]
        public IReadOnlyDictionary<string, JsonElement>? Values { get; init; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaPartialCombinedScalarDictionaryShapeDto
    {
        [JsonPropertyName("templates")]
        [ContentJsonSchemaPartialObjectValues]
        public IReadOnlyDictionary<string, ContentJsonSchemaCombinedScalarDictionaryShapeDto> Templates { get; init; } =
            new Dictionary<string, ContentJsonSchemaCombinedScalarDictionaryShapeDto>();
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaMissingEntryIdMetadataDocumentDto
    {
        [JsonPropertyName("entries")]
        [ContentJsonSchemaEntryControlMembers(
            typeof(ContentJsonSchemaValidTemplateControlDto),
            "missing_id",
            "template"
        )]
        public IReadOnlyList<ContentJsonSchemaValidEntryControlDto> Entries { get; init; } =
            Array.Empty<ContentJsonSchemaValidEntryControlDto>();
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaWrongEntryIdCarrierDocumentDto
    {
        [JsonPropertyName("entries")]
        [ContentJsonSchemaEntryControlMembers(
            typeof(ContentJsonSchemaValidTemplateControlDto),
            "entry_id",
            "template"
        )]
        public IReadOnlyList<ContentJsonSchemaWrongEntryIdCarrierDto> Entries { get; init; } =
            Array.Empty<ContentJsonSchemaWrongEntryIdCarrierDto>();
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaWrongTemplateCarrierDocumentDto
    {
        [JsonPropertyName("entries")]
        [ContentJsonSchemaEntryControlMembers(
            typeof(ContentJsonSchemaWrongTemplateCarrierDto),
            "entry_id",
            "template"
        )]
        public IReadOnlyList<ContentJsonSchemaValidEntryControlDto> Entries { get; init; } =
            Array.Empty<ContentJsonSchemaValidEntryControlDto>();
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaMissingTemplateNonBlankDocumentDto
    {
        [JsonPropertyName("entries")]
        [ContentJsonSchemaEntryControlMembers(
            typeof(ContentJsonSchemaMissingNonBlankTemplateControlDto),
            "entry_id",
            "template"
        )]
        public IReadOnlyList<ContentJsonSchemaValidEntryControlDto> Entries { get; init; } =
            Array.Empty<ContentJsonSchemaValidEntryControlDto>();
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaValidEntryControlDto
    {
        [JsonPropertyName("entry_id")]
        public string EntryId { get; init; } = "";
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaWrongEntryIdCarrierDto
    {
        [JsonPropertyName("entry_id")]
        public int EntryId { get; init; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaValidTemplateControlDto
    {
        [JsonPropertyName("template")]
        [ContentJsonSchemaNonBlankString]
        public string Template { get; init; } = "";
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaMissingNonBlankTemplateControlDto
    {
        [JsonPropertyName("template")]
        public string Template { get; init; } = "";
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class ContentJsonSchemaWrongTemplateCarrierDto
    {
        [JsonPropertyName("template")]
        public int Template { get; init; }
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
