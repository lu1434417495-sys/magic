#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
#if !CONTENT_JSON_OFFLINE
using Godot;
#endif

internal sealed record GearSetAttributeModifierImportModel(
    string AttributeId,
    string Mode,
    int Value,
    int ValuePerRank
);

internal sealed class GearSetThresholdImportModel
{
    internal GearSetThresholdImportModel(
        string thresholdId,
        int requiredPieceCount,
        string displayName,
        string description,
        IReadOnlyList<string> mandatoryMemberItemIds,
        IReadOnlyList<GearSetAttributeModifierImportModel> attributeModifiers,
        IReadOnlyList<string> grantedTraitIds
    )
    {
        ThresholdId = thresholdId ?? throw new ArgumentNullException(nameof(thresholdId));
        RequiredPieceCount = requiredPieceCount;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        MandatoryMemberItemIds = Freeze(mandatoryMemberItemIds, nameof(mandatoryMemberItemIds));
        AttributeModifiers = Freeze(attributeModifiers, nameof(attributeModifiers));
        GrantedTraitIds = Freeze(grantedTraitIds, nameof(grantedTraitIds));
    }

    internal string ThresholdId { get; }
    internal int RequiredPieceCount { get; }
    internal string DisplayName { get; }
    internal string Description { get; }
    internal IReadOnlyList<string> MandatoryMemberItemIds { get; }
    internal IReadOnlyList<GearSetAttributeModifierImportModel> AttributeModifiers { get; }
    internal IReadOnlyList<string> GrantedTraitIds { get; }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return new ReadOnlyCollection<T>(new List<T>(values));
    }
}

internal sealed class GearSetImportModel
{
    internal GearSetImportModel(
        string gearSetId,
        string displayName,
        string description,
        IReadOnlyList<string> memberItemIds,
        string usageAnchorItemId,
        IReadOnlyList<GearSetThresholdImportModel> thresholds
    )
    {
        GearSetId = gearSetId ?? throw new ArgumentNullException(nameof(gearSetId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        MemberItemIds = Freeze(memberItemIds, nameof(memberItemIds));
        UsageAnchorItemId = usageAnchorItemId ?? throw new ArgumentNullException(nameof(usageAnchorItemId));
        Thresholds = Freeze(thresholds, nameof(thresholds));
    }

    internal string GearSetId { get; }
    internal string DisplayName { get; }
    internal string Description { get; }
    internal IReadOnlyList<string> MemberItemIds { get; }
    internal string UsageAnchorItemId { get; }
    internal IReadOnlyList<GearSetThresholdImportModel> Thresholds { get; }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return new ReadOnlyCollection<T>(new List<T>(values));
    }
}

internal sealed class GearSetModifierModeSchemaValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(new[] { "flat", "percent" });
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class GearSetAttributeModifierJsonDto
{
    [JsonPropertyName("attribute_id")]
    [JsonRequired]
    public string AttributeId { get; init; } = "";

    [JsonPropertyName("mode")]
    [JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(GearSetModifierModeSchemaValues))]
    public string Mode { get; init; } = "";

    [JsonPropertyName("value")]
    [JsonRequired]
    public int Value { get; init; }

    [JsonPropertyName("value_per_rank")]
    [JsonRequired]
    public int ValuePerRank { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class GearSetThresholdJsonDto
{
    [JsonPropertyName("threshold_id")]
    [JsonRequired]
    public string ThresholdId { get; init; } = "";

    [JsonPropertyName("required_piece_count")]
    [JsonRequired]
    public int RequiredPieceCount { get; init; }

    [JsonPropertyName("display_name")]
    [JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("description")]
    [JsonRequired]
    public string Description { get; init; } = "";

    [JsonPropertyName("mandatory_member_item_ids")]
    [JsonRequired]
    public IReadOnlyList<string> MandatoryMemberItemIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("attribute_modifiers")]
    [JsonRequired]
    public IReadOnlyList<GearSetAttributeModifierJsonDto> AttributeModifiers { get; init; } =
        Array.Empty<GearSetAttributeModifierJsonDto>();

    [JsonPropertyName("granted_trait_ids")]
    [JsonRequired]
    public IReadOnlyList<string> GrantedTraitIds { get; init; } = Array.Empty<string>();
}

[Description("Strict expanded gear-set entry with stable item and trait references.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class GearSetJsonDto
{
    [JsonPropertyName("gear_set_id")]
    [JsonRequired]
    public string GearSetId { get; init; } = "";

    [JsonPropertyName("display_name")]
    [JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("description")]
    [JsonRequired]
    public string Description { get; init; } = "";

    [JsonPropertyName("member_item_ids")]
    [JsonRequired]
    public IReadOnlyList<string> MemberItemIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("usage_anchor_item_id")]
    [JsonRequired]
    public string UsageAnchorItemId { get; init; } = "";

    [JsonPropertyName("thresholds")]
    [JsonRequired]
    public IReadOnlyList<GearSetThresholdJsonDto> Thresholds { get; init; } =
        Array.Empty<GearSetThresholdJsonDto>();
}

[Description("Gear-set authoring document with file-local templates.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class GearSetJsonDocumentDto
{
    [JsonPropertyName("schema")]
    [JsonRequired]
    [ContentJsonSchemaConst(GearSetContentJsonAuthoringDomain.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain")]
    [JsonRequired]
    [ContentJsonSchemaConst(GearSetContentJsonAuthoringDomain.DomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family")]
    [JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates")]
    [JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, GearSetJsonDto> Templates { get; init; } =
        new ReadOnlyDictionary<string, GearSetJsonDto>(new Dictionary<string, GearSetJsonDto>());

    [JsonPropertyName("entries")]
    [JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        GearSetContentJsonAuthoringDomain.EntryIdPropertyName,
        "template"
    )]
    public IReadOnlyList<GearSetJsonDto> Entries { get; init; } = Array.Empty<GearSetJsonDto>();
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    GenerationMode = JsonSourceGenerationMode.Metadata
)]
[JsonSerializable(typeof(GearSetJsonDto))]
internal partial class GearSetJsonSerializerContext : JsonSerializerContext { }

internal static class GearSetJsonRules
{
    internal const string InvalidDto = "gear_set.dto.invalid_entry";
    internal const string InvalidId = "gear_set.validation.id";
    internal const string DisplayName = "gear_set.validation.display_name";
    internal const string MemberRequired = "gear_set.validation.member_required";
    internal const string DuplicateMember = "gear_set.validation.duplicate_member";
    internal const string UsageAnchor = "gear_set.validation.usage_anchor";
    internal const string ThresholdRequired = "gear_set.validation.threshold_required";
    internal const string ThresholdOrder = "gear_set.validation.threshold_order";
    internal const string DuplicateThreshold = "gear_set.validation.duplicate_threshold";
    internal const string MandatoryMember = "gear_set.validation.mandatory_member";
    internal const string AttributeModifier = "gear_set.validation.attribute_modifier";
    internal const string GrantedTrait = "gear_set.validation.granted_trait";

    internal static bool IsStableId(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 160 || value[0] is < 'a' or > 'z')
            return false;
        foreach (char character in value)
        {
            if (!(character is >= 'a' and <= 'z') && !char.IsAsciiDigit(character) && character != '_' && character != '.')
                return false;
        }
        return true;
    }
}

internal static class GearSetJsonImportParser
{
    internal static ContentImportStageResult<GearSetImportModel> Parse(
        JsonContentEntryContext context,
        string json
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ContentImportStageResult<GearSetJsonDto> dtoResult = ContentJsonStrictDtoParser.Parse(
            context,
            json ?? "",
            GearSetJsonSerializerContext.Default.GearSetJsonDto,
            GearSetJsonRules.InvalidDto
        );
        if (!dtoResult.HasValue)
            return ContentImportStageResult<GearSetImportModel>.Failure(dtoResult.Diagnostics);

        GearSetJsonDto dto = dtoResult.Value;
        var members = new List<string>(dto.MemberItemIds ?? Array.Empty<string>());
        var thresholds = new List<GearSetThresholdImportModel>();
        foreach (GearSetThresholdJsonDto? threshold in dto.Thresholds ?? Array.Empty<GearSetThresholdJsonDto>())
        {
            if (threshold == null)
            {
                thresholds.Add(new GearSetThresholdImportModel(
                    "",
                    0,
                    "",
                    "",
                    Array.Empty<string>(),
                    Array.Empty<GearSetAttributeModifierImportModel>(),
                    Array.Empty<string>()
                ));
                continue;
            }

            var modifiers = new List<GearSetAttributeModifierImportModel>();
            foreach (GearSetAttributeModifierJsonDto? modifier in threshold.AttributeModifiers ?? Array.Empty<GearSetAttributeModifierJsonDto>())
            {
                modifiers.Add(new GearSetAttributeModifierImportModel(
                    modifier?.AttributeId ?? "",
                    modifier?.Mode ?? "",
                    modifier?.Value ?? 0,
                    modifier?.ValuePerRank ?? 0
                ));
            }
            thresholds.Add(new GearSetThresholdImportModel(
                threshold.ThresholdId ?? "",
                threshold.RequiredPieceCount,
                threshold.DisplayName ?? "",
                threshold.Description ?? "",
                new List<string>(threshold.MandatoryMemberItemIds ?? Array.Empty<string>()),
                modifiers,
                new List<string>(threshold.GrantedTraitIds ?? Array.Empty<string>())
            ));
        }

        return ContentImportStageResult<GearSetImportModel>.Success(
            new GearSetImportModel(
                dto.GearSetId ?? "",
                dto.DisplayName ?? "",
                dto.Description ?? "",
                members,
                dto.UsageAnchorItemId ?? "",
                thresholds
            )
        );
    }
}

internal sealed class GearSetImportModelValidator
{
    internal IReadOnlyList<ContentJsonDiagnostic> ValidateDomainLocal(
        JsonContentEntryContext context,
        GearSetImportModel import
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(import);
        var diagnostics = new List<ContentJsonDiagnostic>();

        ValidateId(context, "/gear_set_id", import.GearSetId, diagnostics);
        if (string.IsNullOrWhiteSpace(import.DisplayName))
            Add(GearSetJsonRules.DisplayName, context, "/display_name", diagnostics);

        ValidateIdList(
            import.MemberItemIds,
            context,
            "/member_item_ids",
            GearSetJsonRules.MemberRequired,
            GearSetJsonRules.DuplicateMember,
            diagnostics
        );
        ValidateId(context, "/usage_anchor_item_id", import.UsageAnchorItemId, diagnostics);
        if (!import.MemberItemIds.Contains(import.UsageAnchorItemId, StringComparer.Ordinal))
            Add(GearSetJsonRules.UsageAnchor, context, "/usage_anchor_item_id", diagnostics);

        if (import.Thresholds.Count == 0)
            Add(GearSetJsonRules.ThresholdRequired, context, "/thresholds", diagnostics);
        var thresholdIds = new HashSet<string>(StringComparer.Ordinal);
        int previousCount = 0;
        for (int index = 0; index < import.Thresholds.Count; index += 1)
        {
            GearSetThresholdImportModel threshold = import.Thresholds[index];
            string pointer = $"/thresholds/{index}";
            ValidateId(context, pointer + "/threshold_id", threshold.ThresholdId, diagnostics);
            if (!thresholdIds.Add(threshold.ThresholdId))
                Add(GearSetJsonRules.DuplicateThreshold, context, pointer + "/threshold_id", diagnostics);
            if (
                threshold.RequiredPieceCount <= previousCount
                || threshold.RequiredPieceCount > import.MemberItemIds.Count
            )
            {
                Add(
                    GearSetJsonRules.ThresholdOrder,
                    context,
                    pointer + "/required_piece_count",
                    diagnostics
                );
            }
            previousCount = Math.Max(previousCount, threshold.RequiredPieceCount);

            ValidateIdList(
                threshold.MandatoryMemberItemIds,
                context,
                pointer + "/mandatory_member_item_ids",
                "",
                GearSetJsonRules.MandatoryMember,
                diagnostics
            );
            foreach (string member in threshold.MandatoryMemberItemIds)
            {
                if (!import.MemberItemIds.Contains(member, StringComparer.Ordinal))
                {
                    Add(
                        GearSetJsonRules.MandatoryMember,
                        context,
                        pointer + "/mandatory_member_item_ids",
                        diagnostics
                    );
                }
            }
            if (threshold.MandatoryMemberItemIds.Count > threshold.RequiredPieceCount)
            {
                Add(
                    GearSetJsonRules.MandatoryMember,
                    context,
                    pointer + "/mandatory_member_item_ids",
                    diagnostics
                );
            }

            var modifierIds = new HashSet<string>(StringComparer.Ordinal);
            for (int modifierIndex = 0; modifierIndex < threshold.AttributeModifiers.Count; modifierIndex += 1)
            {
                GearSetAttributeModifierImportModel modifier = threshold.AttributeModifiers[modifierIndex];
                if (
                    !GearSetJsonRules.IsStableId(modifier.AttributeId)
                    || !modifierIds.Add(modifier.AttributeId)
                    || (modifier.Mode != "flat" && modifier.Mode != "percent")
                )
                {
                    Add(
                        GearSetJsonRules.AttributeModifier,
                        context,
                        $"{pointer}/attribute_modifiers/{modifierIndex}",
                        diagnostics
                    );
                }
            }
            ValidateIdList(
                threshold.GrantedTraitIds,
                context,
                pointer + "/granted_trait_ids",
                "",
                GearSetJsonRules.GrantedTrait,
                diagnostics
            );
        }

        return new ReadOnlyCollection<ContentJsonDiagnostic>(diagnostics);
    }

    private static void ValidateIdList(
        IReadOnlyList<string>? values,
        JsonContentEntryContext context,
        string pointer,
        string emptyRule,
        string duplicateRule,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        if (values == null || values.Count == 0)
        {
            if (!string.IsNullOrEmpty(emptyRule))
                Add(emptyRule, context, pointer, diagnostics);
            return;
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < values.Count; index += 1)
        {
            string value = values[index] ?? "";
            ValidateId(context, $"{pointer}/{index}", value, diagnostics);
            if (!seen.Add(value))
                Add(duplicateRule, context, $"{pointer}/{index}", diagnostics);
        }
    }

    private static void ValidateId(JsonContentEntryContext context, string pointer, string? value, ICollection<ContentJsonDiagnostic> diagnostics)
    {
        if (!GearSetJsonRules.IsStableId(value))
            Add(GearSetJsonRules.InvalidId, context, pointer, diagnostics);
    }

    private static void Add(string rule, JsonContentEntryContext context, string pointer, ICollection<ContentJsonDiagnostic> diagnostics) =>
        diagnostics.Add(new ContentJsonDiagnostic(rule, $"Gear-set entry violates {rule}.", context.SourceLabel, context.JsonPointer + pointer));
}

internal static class GearSetContentJsonAuthoringDomain
{
    internal const string DomainId = "gear_sets";
    internal const int SchemaVersion = 1;
    internal const string EntryIdPropertyName = "gear_set_id";
    internal const string ProductionDirectory = "res://data/configs/json/gear_sets";
    private static readonly GearSetImportModelValidator ImportValidator = new();

    internal static ContentJsonSchemaDomainRegistration SchemaRegistration { get; } = new(
        DomainId,
        SchemaVersion,
        typeof(GearSetJsonDocumentDto),
        "Magic gear-set JSON authoring schema",
        "Expanded gear-set thresholds with explicit item and trait IDs.",
        "res://data/schemas/content/gear_sets.schema.json",
        "/data/configs/json/gear_sets/**/*.json"
    );

    internal static JsonContentDomainDescriptor<GearSetImportModel, GearSetImportModel>
        CreateImportDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        CreateDescriptor(sourceDirectory, sourceReader, ValidateCurrentContract);

    internal static JsonContentDomainDescriptor<GearSetImportModel, GearSetImportModel>
        CreateSchemaImportDescriptor(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        ) => CreateDescriptor(sourceDirectory, sourceReader, ValidateSchemaOnly);

    private static JsonContentDomainDescriptor<GearSetImportModel, GearSetImportModel>
        CreateDescriptor(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader,
            Func<
                JsonContentEntryContext,
                GearSetImportModel,
                IReadOnlyList<ContentJsonDiagnostic>
            > validateDomainLocal
        ) =>
        new(
            DomainId,
            SchemaVersion,
            EntryIdPropertyName,
            sourceDirectory,
            sourceReader,
            new ContentJsonNullabilityPolicy(Array.Empty<string>()),
            GearSetJsonImportParser.Parse,
            static (_, import) => ContentImportStageResult<GearSetImportModel>.Success(import),
            validateDomainLocal
        );

    internal static IReadOnlyList<ContentJsonDiagnostic> ValidateDomainLocal(
        JsonContentEntryContext context,
        GearSetImportModel import
    ) => ImportValidator.ValidateDomainLocal(context, import);

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateCurrentContract(
        JsonContentEntryContext context,
        GearSetImportModel import
    ) => ImportValidator.ValidateDomainLocal(context, import);

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateSchemaOnly(
        JsonContentEntryContext context,
        GearSetImportModel import
    ) => Array.Empty<ContentJsonDiagnostic>();

    internal static IContentJsonOfflineValidationDomain CreateOfflineValidationDomain() => new OfflineDomain();

    private sealed class OfflineDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => GearSetContentJsonAuthoringDomain.DomainId;
        public ContentJsonOfflineValidationReport Validate(string sourceDirectory, IContentJsonSourceReader sourceReader)
        {
            ContentImportBatch<GearSetImportModel> batch = CreateImportDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(DomainId, batch.Entries.Count, batch.Diagnostics);
        }
    }
}

#if !CONTENT_JSON_OFFLINE
internal static class GearSetDefinitionProjector
{
    internal static GearSetDefinition Project(GearSetImportModel import)
    {
        ArgumentNullException.ThrowIfNull(import);
        var members = ToStringNames(import.MemberItemIds);
        var thresholds = new List<GearSetThresholdDefinition>();
        foreach (GearSetThresholdImportModel threshold in import.Thresholds)
        {
            var modifiers = new List<AttributeModifierDefinition>();
            foreach (GearSetAttributeModifierImportModel modifier in threshold.AttributeModifiers)
            {
                modifiers.Add(new AttributeModifierDefinition(
                    new StringName(modifier.AttributeId),
                    new StringName(modifier.Mode),
                    modifier.Value,
                    modifier.ValuePerRank,
                    "gear_set",
                    new StringName($"gear_set::{import.GearSetId}::{threshold.ThresholdId}")
                ));
            }
            thresholds.Add(new GearSetThresholdDefinition(
                new StringName(threshold.ThresholdId),
                threshold.RequiredPieceCount,
                threshold.DisplayName,
                threshold.Description,
                ToStringNames(threshold.MandatoryMemberItemIds),
                modifiers,
                ToStringNames(threshold.GrantedTraitIds)
            ));
        }
        return new GearSetDefinition(
            new StringName(import.GearSetId),
            import.DisplayName,
            import.Description,
            members,
            new StringName(import.UsageAnchorItemId),
            thresholds
        );
    }

    private static List<StringName> ToStringNames(IReadOnlyList<string> values)
    {
        var result = new List<StringName>();
        foreach (string value in values)
            result.Add(new StringName(value));
        return result;
    }
}

internal static class GearSetImportCanonicalJson
{
    private sealed record EmptyTemplates;
    private sealed record Document(string Family, IReadOnlyList<GearSetImportModel> Entries)
    {
        internal EmptyTemplates Templates { get; } = new();
    }
    private static readonly ContentCanonicalJsonValueSchema<string> Text = ContentCanonicalJsonValue.Text;
    private static readonly ContentCanonicalJsonObjectSchema<GearSetAttributeModifierImportModel> Modifier = new(
        ContentCanonicalJsonProperty<GearSetAttributeModifierImportModel>.Required("attribute_id", value => value.AttributeId, Text),
        ContentCanonicalJsonProperty<GearSetAttributeModifierImportModel>.Required("mode", value => value.Mode, Text),
        ContentCanonicalJsonProperty<GearSetAttributeModifierImportModel>.Required("value", value => value.Value, ContentCanonicalJsonValue.Int32),
        ContentCanonicalJsonProperty<GearSetAttributeModifierImportModel>.Required("value_per_rank", value => value.ValuePerRank, ContentCanonicalJsonValue.Int32)
    );
    private static readonly ContentCanonicalJsonObjectSchema<GearSetThresholdImportModel> Threshold = new(
        ContentCanonicalJsonProperty<GearSetThresholdImportModel>.Required("threshold_id", value => value.ThresholdId, Text),
        ContentCanonicalJsonProperty<GearSetThresholdImportModel>.Required("required_piece_count", value => value.RequiredPieceCount, ContentCanonicalJsonValue.Int32),
        ContentCanonicalJsonProperty<GearSetThresholdImportModel>.Required("display_name", value => value.DisplayName, Text),
        ContentCanonicalJsonProperty<GearSetThresholdImportModel>.Required("description", value => value.Description, Text),
        ContentCanonicalJsonProperty<GearSetThresholdImportModel>.Required("mandatory_member_item_ids", value => value.MandatoryMemberItemIds, ContentCanonicalJsonValue.Array(Text)),
        ContentCanonicalJsonProperty<GearSetThresholdImportModel>.Required("attribute_modifiers", value => value.AttributeModifiers, ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Object(Modifier))),
        ContentCanonicalJsonProperty<GearSetThresholdImportModel>.Required("granted_trait_ids", value => value.GrantedTraitIds, ContentCanonicalJsonValue.Array(Text))
    );
    internal static readonly ContentCanonicalJsonObjectSchema<GearSetImportModel> Entry = new(
        ContentCanonicalJsonProperty<GearSetImportModel>.Required("gear_set_id", value => value.GearSetId, Text),
        ContentCanonicalJsonProperty<GearSetImportModel>.Required("display_name", value => value.DisplayName, Text),
        ContentCanonicalJsonProperty<GearSetImportModel>.Required("description", value => value.Description, Text),
        ContentCanonicalJsonProperty<GearSetImportModel>.Required("member_item_ids", value => value.MemberItemIds, ContentCanonicalJsonValue.Array(Text)),
        ContentCanonicalJsonProperty<GearSetImportModel>.Required("usage_anchor_item_id", value => value.UsageAnchorItemId, Text),
        ContentCanonicalJsonProperty<GearSetImportModel>.Required("thresholds", value => value.Thresholds, ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Object(Threshold)))
    );
    private static readonly ContentCanonicalJsonValueSchema<EmptyTemplates> EmptyTemplateSchema = ContentCanonicalJsonValue.Object(new ContentCanonicalJsonObjectSchema<EmptyTemplates>());
    private static readonly ContentCanonicalJsonObjectSchema<Document> DocumentSchema = new(
        ContentCanonicalJsonProperty<Document>.Required("schema", _ => GearSetContentJsonAuthoringDomain.SchemaVersion, ContentCanonicalJsonValue.Int32),
        ContentCanonicalJsonProperty<Document>.Required("domain", _ => GearSetContentJsonAuthoringDomain.DomainId, Text),
        ContentCanonicalJsonProperty<Document>.Required("family", value => value.Family, Text),
        ContentCanonicalJsonProperty<Document>.Required("templates", value => value.Templates, EmptyTemplateSchema),
        ContentCanonicalJsonProperty<Document>.Required("entries", value => value.Entries, ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Object(Entry)))
    );

    internal static string WriteDocument(ContentCanonicalJsonWriter writer, string family, IReadOnlyList<GearSetImportModel> entries) =>
        writer.Write(new Document(family, entries), DocumentSchema, indented: true) + "\n";

    internal static string WriteEntry(ContentCanonicalJsonWriter writer, GearSetImportModel entry) =>
        writer.Write(entry, Entry, indented: true);
}
#endif
