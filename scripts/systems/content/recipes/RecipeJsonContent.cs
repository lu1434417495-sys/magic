#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
#if !CONTENT_JSON_OFFLINE
using Godot;
#endif

internal sealed class RecipeIngredientImportModel
{
    internal RecipeIngredientImportModel(string itemId, int quantity)
    {
        ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
        Quantity = quantity;
    }

    internal string ItemId { get; }
    internal int Quantity { get; }
}

internal sealed class RecipeImportModel
{
    internal RecipeImportModel(
        string recipeId,
        string displayName,
        string description,
        IReadOnlyList<RecipeIngredientImportModel> inputs,
        string outputItemId,
        int outputQuantity,
        IReadOnlyList<string> requiredFacilityTags,
        string failureReason
    )
    {
        RecipeId = recipeId ?? throw new ArgumentNullException(nameof(recipeId));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Inputs = Freeze(inputs, nameof(inputs));
        OutputItemId = outputItemId ?? throw new ArgumentNullException(nameof(outputItemId));
        OutputQuantity = outputQuantity;
        RequiredFacilityTags = Freeze(requiredFacilityTags, nameof(requiredFacilityTags));
        FailureReason = failureReason ?? throw new ArgumentNullException(nameof(failureReason));
    }

    internal string RecipeId { get; }
    internal string DisplayName { get; }
    internal string Description { get; }
    internal IReadOnlyList<RecipeIngredientImportModel> Inputs { get; }
    internal string OutputItemId { get; }
    internal int OutputQuantity { get; }
    internal IReadOnlyList<string> RequiredFacilityTags { get; }
    internal string FailureReason { get; }

    private static IReadOnlyList<T> Freeze<T>(IReadOnlyList<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values, name);
        return new ReadOnlyCollection<T>(new List<T>(values));
    }
}

[Description("One recipe input item and its positive quantity.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class RecipeIngredientJsonDto
{
    [JsonPropertyName("item_id")]
    [JsonRequired]
    public string ItemId { get; init; } = "";

    [JsonPropertyName("quantity")]
    [JsonRequired]
    public int Quantity { get; init; }
}

[Description("Strict expanded recipe entry. All item references are stable IDs.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class RecipeJsonDto
{
    [JsonPropertyName("recipe_id")]
    [JsonRequired]
    public string RecipeId { get; init; } = "";

    [JsonPropertyName("display_name")]
    [JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("description")]
    [JsonRequired]
    public string Description { get; init; } = "";

    [JsonPropertyName("inputs")]
    [JsonRequired]
    public IReadOnlyList<RecipeIngredientJsonDto> Inputs { get; init; } =
        Array.Empty<RecipeIngredientJsonDto>();

    [JsonPropertyName("output_item_id")]
    [JsonRequired]
    public string OutputItemId { get; init; } = "";

    [JsonPropertyName("output_quantity")]
    [JsonRequired]
    public int OutputQuantity { get; init; }

    [JsonPropertyName("required_facility_tags")]
    [JsonRequired]
    public IReadOnlyList<string> RequiredFacilityTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("failure_reason")]
    [JsonRequired]
    public string FailureReason { get; init; } = "";
}

[Description("Recipe authoring document with file-local templates.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class RecipeJsonDocumentDto
{
    [JsonPropertyName("schema")]
    [JsonRequired]
    [ContentJsonSchemaConst(RecipeContentJsonAuthoringDomain.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain")]
    [JsonRequired]
    [ContentJsonSchemaConst(RecipeContentJsonAuthoringDomain.DomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family")]
    [JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates")]
    [JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, RecipeJsonDto> Templates { get; init; } =
        new ReadOnlyDictionary<string, RecipeJsonDto>(new Dictionary<string, RecipeJsonDto>());

    [JsonPropertyName("entries")]
    [JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        RecipeContentJsonAuthoringDomain.EntryIdPropertyName,
        "template"
    )]
    public IReadOnlyList<RecipeJsonDto> Entries { get; init; } = Array.Empty<RecipeJsonDto>();
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    GenerationMode = JsonSourceGenerationMode.Metadata
)]
[JsonSerializable(typeof(RecipeJsonDto))]
internal partial class RecipeJsonSerializerContext : JsonSerializerContext { }

internal static class RecipeJsonRules
{
    internal const string InvalidDto = "recipe.dto.invalid_entry";
    internal const string InvalidId = "recipe.validation.id";
    internal const string DisplayName = "recipe.validation.display_name";
    internal const string InputRequired = "recipe.validation.input_required";
    internal const string DuplicateInput = "recipe.validation.duplicate_input";
    internal const string Quantity = "recipe.validation.quantity";
    internal const string FacilityRequired = "recipe.validation.facility_required";
    internal const string DuplicateFacility = "recipe.validation.duplicate_facility";
    internal const int QuantityMaximum = 9999;

    internal static bool IsStableId(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 128)
            return false;
        for (int index = 0; index < value.Length; index += 1)
        {
            char character = value[index];
            if (
                !(character is >= 'a' and <= 'z')
                && !char.IsAsciiDigit(character)
                && character != '_'
                && character != '.'
            )
            {
                return false;
            }
        }
        return value[0] is >= 'a' and <= 'z';
    }
}

internal static class RecipeJsonImportParser
{
    internal static ContentImportStageResult<RecipeImportModel> Parse(
        JsonContentEntryContext context,
        string json
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ContentImportStageResult<RecipeJsonDto> dtoResult = ContentJsonStrictDtoParser.Parse(
            context,
            json ?? "",
            RecipeJsonSerializerContext.Default.RecipeJsonDto,
            RecipeJsonRules.InvalidDto
        );
        if (!dtoResult.HasValue)
            return ContentImportStageResult<RecipeImportModel>.Failure(dtoResult.Diagnostics);

        RecipeJsonDto dto = dtoResult.Value;
        var inputs = new List<RecipeIngredientImportModel>();
        foreach (RecipeIngredientJsonDto? input in dto.Inputs ?? Array.Empty<RecipeIngredientJsonDto>())
        {
            inputs.Add(new RecipeIngredientImportModel(
                input?.ItemId ?? "",
                input?.Quantity ?? 0
            ));
        }
        return ContentImportStageResult<RecipeImportModel>.Success(
            new RecipeImportModel(
                dto.RecipeId ?? "",
                dto.DisplayName ?? "",
                dto.Description ?? "",
                inputs,
                dto.OutputItemId ?? "",
                dto.OutputQuantity,
                new List<string>(dto.RequiredFacilityTags ?? Array.Empty<string>()),
                dto.FailureReason ?? ""
            )
        );
    }
}

internal sealed class RecipeImportModelValidator
{
    internal IReadOnlyList<ContentJsonDiagnostic> ValidateDomainLocal(
        JsonContentEntryContext context,
        RecipeImportModel import
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(import);
        var diagnostics = new List<ContentJsonDiagnostic>();

        ValidateId(context, "/recipe_id", import.RecipeId, diagnostics);
        if (string.IsNullOrWhiteSpace(import.DisplayName))
            diagnostics.Add(Diagnostic(RecipeJsonRules.DisplayName, context, "/display_name"));

        var inputIds = new HashSet<string>(StringComparer.Ordinal);
        if (import.Inputs.Count == 0)
            diagnostics.Add(Diagnostic(RecipeJsonRules.InputRequired, context, "/inputs"));
        for (int index = 0; index < import.Inputs.Count; index += 1)
        {
            RecipeIngredientImportModel input = import.Inputs[index];
            string pointer = $"/inputs/{index}";
            ValidateId(context, pointer + "/item_id", input.ItemId, diagnostics);
            if (!inputIds.Add(input.ItemId))
                diagnostics.Add(Diagnostic(RecipeJsonRules.DuplicateInput, context, pointer + "/item_id"));
            ValidateQuantity(context, pointer + "/quantity", input.Quantity, diagnostics);
        }

        ValidateId(context, "/output_item_id", import.OutputItemId, diagnostics);
        ValidateQuantity(context, "/output_quantity", import.OutputQuantity, diagnostics);

        var facilities = new HashSet<string>(StringComparer.Ordinal);
        if (import.RequiredFacilityTags.Count == 0)
        {
            diagnostics.Add(Diagnostic(
                RecipeJsonRules.FacilityRequired,
                context,
                "/required_facility_tags"
            ));
        }
        for (int index = 0; index < import.RequiredFacilityTags.Count; index += 1)
        {
            string tag = import.RequiredFacilityTags[index] ?? "";
            string pointer = $"/required_facility_tags/{index}";
            ValidateId(context, pointer, tag, diagnostics);
            if (!facilities.Add(tag))
                diagnostics.Add(Diagnostic(RecipeJsonRules.DuplicateFacility, context, pointer));
        }

        return new ReadOnlyCollection<ContentJsonDiagnostic>(diagnostics);
    }

    private static void ValidateId(
        JsonContentEntryContext context,
        string pointer,
        string? value,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        if (!RecipeJsonRules.IsStableId(value))
            diagnostics.Add(Diagnostic(RecipeJsonRules.InvalidId, context, pointer));
    }

    private static void ValidateQuantity(
        JsonContentEntryContext context,
        string pointer,
        int value,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        if (value < 1 || value > RecipeJsonRules.QuantityMaximum)
            diagnostics.Add(Diagnostic(RecipeJsonRules.Quantity, context, pointer));
    }

    private static ContentJsonDiagnostic Diagnostic(
        string ruleId,
        JsonContentEntryContext context,
        string pointer
    ) => new(
        ruleId,
        $"Recipe entry violates {ruleId}.",
        context.SourceLabel,
        context.JsonPointer + pointer
    );
}

internal static class RecipeContentJsonAuthoringDomain
{
    internal const string DomainId = "recipes";
    internal const int SchemaVersion = 1;
    internal const string EntryIdPropertyName = "recipe_id";
    internal const string ProductionDirectory = "res://data/configs/json/recipes";
    private static readonly RecipeImportModelValidator ImportValidator = new();

    internal static ContentJsonSchemaDomainRegistration SchemaRegistration { get; } = new(
        DomainId,
        SchemaVersion,
        typeof(RecipeJsonDocumentDto),
        "Magic recipe JSON authoring schema",
        "Expanded recipe entries with stable item-ID dependencies.",
        "res://data/schemas/content/recipes.schema.json",
        "/data/configs/json/recipes/**/*.json"
    );

    internal static JsonContentDomainDescriptor<RecipeImportModel, RecipeImportModel>
        CreateImportDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        CreateDescriptor(sourceDirectory, sourceReader, ValidateCurrentContract);

    internal static JsonContentDomainDescriptor<RecipeImportModel, RecipeImportModel>
        CreateSchemaImportDescriptor(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        ) => CreateDescriptor(sourceDirectory, sourceReader, ValidateSchemaOnly);

    private static JsonContentDomainDescriptor<RecipeImportModel, RecipeImportModel>
        CreateDescriptor(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader,
            Func<
                JsonContentEntryContext,
                RecipeImportModel,
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
            RecipeJsonImportParser.Parse,
            static (_, import) => ContentImportStageResult<RecipeImportModel>.Success(import),
            validateDomainLocal
        );

    internal static IReadOnlyList<ContentJsonDiagnostic> ValidateDomainLocal(
        JsonContentEntryContext context,
        RecipeImportModel import
    ) => ImportValidator.ValidateDomainLocal(context, import);

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateCurrentContract(
        JsonContentEntryContext context,
        RecipeImportModel import
    ) => ImportValidator.ValidateDomainLocal(context, import);

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateSchemaOnly(
        JsonContentEntryContext context,
        RecipeImportModel import
    ) => Array.Empty<ContentJsonDiagnostic>();

    internal static IContentJsonOfflineValidationDomain CreateOfflineValidationDomain() =>
        new OfflineDomain();

    private sealed class OfflineDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => RecipeContentJsonAuthoringDomain.DomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<RecipeImportModel> batch =
                CreateImportDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(DomainId, batch.Entries.Count, batch.Diagnostics);
        }
    }
}

#if !CONTENT_JSON_OFFLINE
internal static class RecipeDefinitionProjector
{
    internal static RecipeDefinition Project(RecipeImportModel import)
    {
        ArgumentNullException.ThrowIfNull(import);
        var itemIds = new List<StringName>();
        var quantities = new List<int>();
        foreach (RecipeIngredientImportModel input in import.Inputs)
        {
            itemIds.Add(new StringName(input.ItemId));
            quantities.Add(input.Quantity);
        }
        var facilities = new List<StringName>();
        foreach (string tag in import.RequiredFacilityTags)
            facilities.Add(new StringName(tag));
        return new RecipeDefinition(
            new StringName(import.RecipeId),
            import.DisplayName,
            import.Description,
            itemIds,
            quantities,
            new StringName(import.OutputItemId),
            import.OutputQuantity,
            facilities,
            import.FailureReason
        );
    }
}

internal static class RecipeImportCanonicalJson
{
    private sealed record EmptyTemplates;
    private sealed record Document(string Family, IReadOnlyList<RecipeImportModel> Entries)
    {
        internal EmptyTemplates Templates { get; } = new();
    }

    private static readonly ContentCanonicalJsonValueSchema<string> Text =
        ContentCanonicalJsonValue.Text;
    private static readonly ContentCanonicalJsonObjectSchema<RecipeIngredientImportModel> Ingredient =
        new(
            ContentCanonicalJsonProperty<RecipeIngredientImportModel>.Required("item_id", value => value.ItemId, Text),
            ContentCanonicalJsonProperty<RecipeIngredientImportModel>.Required("quantity", value => value.Quantity, ContentCanonicalJsonValue.Int32)
        );
    internal static readonly ContentCanonicalJsonObjectSchema<RecipeImportModel> Entry =
        new(
            ContentCanonicalJsonProperty<RecipeImportModel>.Required("recipe_id", value => value.RecipeId, Text),
            ContentCanonicalJsonProperty<RecipeImportModel>.Required("display_name", value => value.DisplayName, Text),
            ContentCanonicalJsonProperty<RecipeImportModel>.Required("description", value => value.Description, Text),
            ContentCanonicalJsonProperty<RecipeImportModel>.Required("inputs", value => value.Inputs, ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Object(Ingredient))),
            ContentCanonicalJsonProperty<RecipeImportModel>.Required("output_item_id", value => value.OutputItemId, Text),
            ContentCanonicalJsonProperty<RecipeImportModel>.Required("output_quantity", value => value.OutputQuantity, ContentCanonicalJsonValue.Int32),
            ContentCanonicalJsonProperty<RecipeImportModel>.Required("required_facility_tags", value => value.RequiredFacilityTags, ContentCanonicalJsonValue.Array(Text)),
            ContentCanonicalJsonProperty<RecipeImportModel>.Required("failure_reason", value => value.FailureReason, Text)
        );
    private static readonly ContentCanonicalJsonValueSchema<EmptyTemplates> EmptyTemplateSchema =
        ContentCanonicalJsonValue.Object(new ContentCanonicalJsonObjectSchema<EmptyTemplates>());
    private static readonly ContentCanonicalJsonObjectSchema<Document> DocumentSchema =
        new(
            ContentCanonicalJsonProperty<Document>.Required("schema", _ => RecipeContentJsonAuthoringDomain.SchemaVersion, ContentCanonicalJsonValue.Int32),
            ContentCanonicalJsonProperty<Document>.Required("domain", _ => RecipeContentJsonAuthoringDomain.DomainId, Text),
            ContentCanonicalJsonProperty<Document>.Required("family", value => value.Family, Text),
            ContentCanonicalJsonProperty<Document>.Required("templates", value => value.Templates, EmptyTemplateSchema),
            ContentCanonicalJsonProperty<Document>.Required("entries", value => value.Entries, ContentCanonicalJsonValue.Array(ContentCanonicalJsonValue.Object(Entry)))
        );

    internal static string WriteDocument(
        ContentCanonicalJsonWriter writer,
        string family,
        IReadOnlyList<RecipeImportModel> entries
    ) => writer.Write(new Document(family, entries), DocumentSchema, indented: true) + "\n";

    internal static string WriteEntry(ContentCanonicalJsonWriter writer, RecipeImportModel entry) =>
        writer.Write(entry, Entry, indented: true);
}
#endif
