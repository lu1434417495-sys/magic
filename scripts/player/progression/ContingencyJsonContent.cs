#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

internal static class ContingencyJsonContentDomain
{
    internal const int SchemaVersion = 1;
    internal const string DomainId = "contingency_templates";
    internal const string DirectoryPath = "res://data/configs/json/contingency_templates";

    internal static ContentJsonSchemaDomainRegistration SchemaRegistration { get; } =
        new(
            DomainId,
            SchemaVersion,
            typeof(ContingencyJsonDocumentDto),
            "Magic contingency template JSON authoring schema",
            "Strict closed trigger and target-resolver contract.",
            "res://data/schemas/content/contingency_templates.schema.json",
            "/data/configs/json/contingency_templates/**/*.json"
        );

    internal static IReadOnlyList<ContentJsonSchemaDomainRegistration> SchemaRegistrations { get; } =
        Array.AsReadOnly(new[] { SchemaRegistration });

    internal static JsonContentDomainDescriptor<
        ContingencyTemplateJsonDto,
        ContingencyTemplateImportModel
    > CreateDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        new(
            DomainId,
            SchemaVersion,
            "template_id",
            sourceDirectory,
            sourceReader,
            new ContentJsonNullabilityPolicy(Array.Empty<string>()),
            ContingencyJsonImportParser.Parse,
            ContingencyJsonImportParser.Normalize,
            ContingencyImportValidator.Validate
        );

    internal static IEnumerable<IContentJsonOfflineValidationDomain> CreateOfflineDomains()
    {
        yield return new OfflineDomain();
    }

    private sealed class OfflineDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => ContingencyJsonContentDomain.DomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<ContingencyTemplateImportModel> batch = CreateDescriptor(
                sourceDirectory,
                sourceReader
            ).Import();
            return new ContentJsonOfflineValidationReport(
                DomainId,
                batch.Entries.Count,
                batch.Diagnostics
            );
        }
    }
}

internal static class ContingencyJsonRules
{
    internal const string InvalidDto = "contingency.dto.invalid";
    internal const string InvalidPayload = "contingency.payload.invalid";
    internal const string UnknownKind = "contingency.kind.unknown";
    internal const string IdRequired = "contingency.id.required";
    internal const string IdMismatch = "contingency.id.mismatch";
    internal const string CollectionRequired = "contingency.collection.required";
}

[Description("Contingency template document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContingencyJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired]
    [ContentJsonSchemaConst(ContingencyJsonContentDomain.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain"), JsonRequired]
    [ContentJsonSchemaConst(ContingencyJsonContentDomain.DomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family"), JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates"), JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, ContingencyTemplateJsonDto> Templates { get; init; } =
        EmptyMap<ContingencyTemplateJsonDto>.Value;

    [JsonPropertyName("entries"), JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        "template_id",
        "template"
    )]
    public IReadOnlyList<ContingencyTemplateJsonDto> Entries { get; init; } =
        Array.Empty<ContingencyTemplateJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContingencyTemplateJsonDto
{
    [JsonPropertyName("template_id"), JsonRequired]
    public string TemplateId { get; init; } = "";

    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("source_skill_id"), JsonRequired]
    public string SourceSkillId { get; init; } = "";

    [JsonPropertyName("matrix_load"), JsonRequired]
    public int MatrixLoad { get; init; }

    [JsonPropertyName("reserved_mp_per_matrix_load"), JsonRequired]
    public int ReservedMpPerMatrixLoad { get; init; }

    [JsonPropertyName("charge_material_costs"), JsonRequired]
    public IReadOnlyList<ContingencyMaterialCostJsonDto> ChargeMaterialCosts { get; init; } =
        Array.Empty<ContingencyMaterialCostJsonDto>();

    [JsonPropertyName("release_mode"), JsonRequired]
    public string ReleaseMode { get; init; } = "";

    [JsonPropertyName("trigger"), JsonRequired]
    public ContingencyTriggerJsonDto Trigger { get; init; } = new();

    [JsonPropertyName("stored_spells"), JsonRequired]
    public IReadOnlyList<ContingencyStoredSpellJsonDto> StoredSpells { get; init; } =
        Array.Empty<ContingencyStoredSpellJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContingencyMaterialCostJsonDto
{
    [JsonPropertyName("item_id"), JsonRequired]
    public string ItemId { get; init; } = "";

    [JsonPropertyName("quantity"), JsonRequired]
    public int Quantity { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(ContingencyTriggerClosedKindSpec))]
internal sealed class ContingencyTriggerJsonDto
{
    [JsonPropertyName("kind"), JsonRequired]
    public string Kind { get; init; } = "";

    [JsonPropertyName("payload"), JsonRequired]
    public object Payload { get; init; } = null!;
}

internal sealed class ContingencyTriggerClosedKindSpec : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "kind";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } =
        Array.AsReadOnly(
            new[]
            {
                new ContentJsonSchemaClosedKindBranch("combat_started", typeof(ContingencySimpleTriggerPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("hp_below_percent", typeof(ContingencyHpTriggerPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("incoming_damage_percent", typeof(ContingencyIncomingDamageTriggerPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("fatal_damage_incoming", typeof(ContingencySimpleTriggerPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("status_applied", typeof(ContingencyStatusTriggerPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("enemy_enter_radius", typeof(ContingencyEnemyRadiusTriggerPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("affected_by_spell", typeof(ContingencySpellTriggerPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("owner_turn_started", typeof(ContingencySimpleTriggerPayloadJsonDto)),
            }
        );
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContingencySimpleTriggerPayloadJsonDto
{
    [JsonPropertyName("subject"), JsonRequired]
    public string Subject { get; init; } = "";

    [JsonPropertyName("timing"), JsonRequired]
    public string Timing { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContingencyHpTriggerPayloadJsonDto
{
    [JsonPropertyName("subject"), JsonRequired]
    public string Subject { get; init; } = "";

    [JsonPropertyName("percent"), JsonRequired]
    public int Percent { get; init; }

    [JsonPropertyName("crossing_only"), JsonRequired]
    public bool CrossingOnly { get; init; }

    [JsonPropertyName("timing"), JsonRequired]
    public string Timing { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContingencyIncomingDamageTriggerPayloadJsonDto
{
    [JsonPropertyName("subject"), JsonRequired]
    public string Subject { get; init; } = "";

    [JsonPropertyName("damage_percent"), JsonRequired]
    public int DamagePercent { get; init; }

    [JsonPropertyName("damage_basis"), JsonRequired]
    public string DamageBasis { get; init; } = "";

    [JsonPropertyName("damage_amount_mode"), JsonRequired]
    public string DamageAmountMode { get; init; } = "";

    [JsonPropertyName("timing"), JsonRequired]
    public string Timing { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContingencyEnemyRadiusTriggerPayloadJsonDto
{
    [JsonPropertyName("center"), JsonRequired]
    public string Center { get; init; } = "";

    [JsonPropertyName("radius"), JsonRequired]
    public int Radius { get; init; }

    [JsonPropertyName("radius_metric"), JsonRequired]
    public string RadiusMetric { get; init; } = "";

    [JsonPropertyName("source_team"), JsonRequired]
    public string SourceTeam { get; init; } = "";

    [JsonPropertyName("timing"), JsonRequired]
    public string Timing { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContingencyStatusTriggerPayloadJsonDto
{
    [JsonPropertyName("subject"), JsonRequired]
    public string Subject { get; init; } = "";

    [JsonPropertyName("status_tags"), JsonRequired]
    public IReadOnlyList<string> StatusTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("application_match"), JsonRequired]
    public string ApplicationMatch { get; init; } = "";

    [JsonPropertyName("timing"), JsonRequired]
    public string Timing { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContingencySpellTriggerPayloadJsonDto
{
    [JsonPropertyName("subject"), JsonRequired]
    public string Subject { get; init; } = "";

    [JsonPropertyName("source_team"), JsonRequired]
    public string SourceTeam { get; init; } = "";

    [JsonPropertyName("spell_match"), JsonRequired]
    public string SpellMatch { get; init; } = "";

    [JsonPropertyName("timing"), JsonRequired]
    public string Timing { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContingencyStoredSpellJsonDto
{
    [JsonPropertyName("stored_skill_id"), JsonRequired]
    public string StoredSkillId { get; init; } = "";

    [JsonPropertyName("max_cast_level"), JsonRequired]
    public int MaxCastLevel { get; init; }

    [JsonPropertyName("order"), JsonRequired]
    public int Order { get; init; }

    [JsonPropertyName("target_resolver"), JsonRequired]
    public ContingencyTargetResolverJsonDto TargetResolver { get; init; } = new();

    [JsonPropertyName("parameter_bindings"), JsonRequired]
    [ContentJsonSchemaScalarOrStringArrayDictionaryValues]
    public IReadOnlyDictionary<string, JsonElement> ParameterBindings { get; init; } =
        EmptyMap<JsonElement>.Value;

    [JsonPropertyName("fallback_policy"), JsonRequired]
    public string FallbackPolicy { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(ContingencyTargetResolverClosedKindSpec))]
internal sealed class ContingencyTargetResolverJsonDto
{
    [JsonPropertyName("kind"), JsonRequired]
    public string Kind { get; init; } = "";

    [JsonPropertyName("payload"), JsonRequired]
    public object Payload { get; init; } = null!;
}

internal sealed class ContingencyTargetResolverClosedKindSpec : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "kind";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } =
        Array.AsReadOnly(
            new[]
            {
                new ContentJsonSchemaClosedKindBranch("self", typeof(ContingencyEmptyPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("trigger_source", typeof(ContingencyEmptyPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("trigger_target", typeof(ContingencyEmptyPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("nearest_enemy_to_owner", typeof(ContingencyEmptyPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("nearest_enemy_to_trigger_cell", typeof(ContingencyEmptyPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("owner_centered_area", typeof(ContingencyEmptyPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("attacker_cell", typeof(ContingencyEmptyPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("empty_cell_near_owner", typeof(ContingencyEmptyCellResolverPayloadJsonDto)),
            }
        );
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContingencyEmptyPayloadJsonDto { }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ContingencyEmptyCellResolverPayloadJsonDto
{
    [JsonPropertyName("preference"), JsonRequired]
    public string Preference { get; init; } = "";

    [JsonPropertyName("max_distance"), JsonRequired]
    public int MaxDistance { get; init; }
}

internal sealed record ContingencyTemplateImportModel(
    string TemplateId,
    string DisplayName,
    string SourceSkillId,
    int MatrixLoad,
    int ReservedMpPerMatrixLoad,
    IReadOnlyList<ContingencyMaterialCostImportModel> ChargeMaterialCosts,
    string ReleaseMode,
    ContingencyTriggerImportModel Trigger,
    IReadOnlyList<ContingencyStoredSpellImportModel> StoredSpells
);

internal sealed record ContingencyMaterialCostImportModel(string ItemId, int Quantity);

internal sealed record ContingencyTriggerImportModel(
    string Type,
    string Subject,
    string Timing,
    int Percent,
    bool CrossingOnly,
    int DamagePercent,
    string DamageBasis,
    string DamageAmountMode,
    string Center,
    int Radius,
    string RadiusMetric,
    string SourceTeam,
    IReadOnlyList<string> StatusTags,
    string ApplicationMatch,
    string SpellMatch
);

internal sealed record ContingencyStoredSpellImportModel(
    string StoredSkillId,
    int MaxCastLevel,
    int Order,
    ContingencyTargetResolverImportModel TargetResolver,
    IReadOnlyDictionary<string, object> ParameterBindings,
    string FallbackPolicy
);

internal sealed record ContingencyTargetResolverImportModel(
    string Type,
    string Preference,
    int MaxDistance
);

internal static class ContingencyJsonImportParser
{
    internal static ContentImportStageResult<ContingencyTemplateJsonDto> Parse(
        JsonContentEntryContext context,
        string json
    ) =>
        ContentJsonStrictDtoParser.Parse(
            context,
            json,
            ContingencyJsonSerializerContext.Default.ContingencyTemplateJsonDto,
            ContingencyJsonRules.InvalidDto
        );

    internal static ContentImportStageResult<ContingencyTemplateImportModel> Normalize(
        JsonContentEntryContext context,
        ContingencyTemplateJsonDto dto
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        ContingencyTriggerImportModel? trigger = NormalizeTrigger(
            context,
            dto.Trigger,
            diagnostics
        );
        var spells = new List<ContingencyStoredSpellImportModel>(dto.StoredSpells.Count);
        for (int index = 0; index < dto.StoredSpells.Count; index++)
        {
            ContingencyStoredSpellJsonDto spell = dto.StoredSpells[index];
            ContingencyTargetResolverImportModel? resolver = NormalizeResolver(
                context,
                spell.TargetResolver,
                $"/stored_spells/{index}/target_resolver",
                diagnostics
            );
            IReadOnlyDictionary<string, object>? bindings = NormalizeBindings(
                context,
                spell.ParameterBindings,
                $"/stored_spells/{index}/parameter_bindings",
                diagnostics
            );
            if (resolver != null && bindings != null)
            {
                spells.Add(
                    new ContingencyStoredSpellImportModel(
                        spell.StoredSkillId,
                        spell.MaxCastLevel,
                        spell.Order,
                        resolver,
                        bindings,
                        spell.FallbackPolicy
                    )
                );
            }
        }
        if (diagnostics.Count > 0 || trigger == null)
            return ContentImportStageResult<ContingencyTemplateImportModel>.Failure(diagnostics);
        return ContentImportStageResult<ContingencyTemplateImportModel>.Success(
            new ContingencyTemplateImportModel(
                dto.TemplateId,
                dto.DisplayName,
                dto.SourceSkillId,
                dto.MatrixLoad,
                dto.ReservedMpPerMatrixLoad,
                dto.ChargeMaterialCosts
                    .Select(cost => new ContingencyMaterialCostImportModel(
                        cost.ItemId,
                        cost.Quantity
                    ))
                    .ToArray(),
                dto.ReleaseMode,
                trigger,
                new ReadOnlyCollection<ContingencyStoredSpellImportModel>(spells)
            )
        );
    }

    private static ContingencyTriggerImportModel? NormalizeTrigger(
        JsonContentEntryContext context,
        ContingencyTriggerJsonDto trigger,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        const string Pointer = "/trigger";
        if (!TryPayload(context, trigger.Payload, Pointer, diagnostics, out string json))
            return null;
        JsonContentEntryContext nested = Nested(context, Pointer + "/payload");
        switch (trigger.Kind)
        {
            case "combat_started":
            case "fatal_damage_incoming":
            case "owner_turn_started":
                if (TryParse(nested, json, ContingencyJsonSerializerContext.Default.ContingencySimpleTriggerPayloadJsonDto, out ContingencySimpleTriggerPayloadJsonDto? simple, out IReadOnlyList<ContentJsonDiagnostic> simpleErrors))
                    return Trigger(trigger.Kind, subject: simple!.Subject, timing: simple.Timing);
                diagnostics.AddRange(simpleErrors);
                return null;
            case "hp_below_percent":
                if (TryParse(nested, json, ContingencyJsonSerializerContext.Default.ContingencyHpTriggerPayloadJsonDto, out ContingencyHpTriggerPayloadJsonDto? hp, out IReadOnlyList<ContentJsonDiagnostic> hpErrors))
                    return Trigger(trigger.Kind, subject: hp!.Subject, timing: hp.Timing, percent: hp.Percent, crossingOnly: hp.CrossingOnly);
                diagnostics.AddRange(hpErrors);
                return null;
            case "incoming_damage_percent":
                if (TryParse(nested, json, ContingencyJsonSerializerContext.Default.ContingencyIncomingDamageTriggerPayloadJsonDto, out ContingencyIncomingDamageTriggerPayloadJsonDto? damage, out IReadOnlyList<ContentJsonDiagnostic> damageErrors))
                    return Trigger(trigger.Kind, subject: damage!.Subject, timing: damage.Timing, damagePercent: damage.DamagePercent, damageBasis: damage.DamageBasis, damageAmountMode: damage.DamageAmountMode);
                diagnostics.AddRange(damageErrors);
                return null;
            case "enemy_enter_radius":
                if (TryParse(nested, json, ContingencyJsonSerializerContext.Default.ContingencyEnemyRadiusTriggerPayloadJsonDto, out ContingencyEnemyRadiusTriggerPayloadJsonDto? radius, out IReadOnlyList<ContentJsonDiagnostic> radiusErrors))
                    return Trigger(trigger.Kind, timing: radius!.Timing, center: radius.Center, radius: radius.Radius, radiusMetric: radius.RadiusMetric, sourceTeam: radius.SourceTeam);
                diagnostics.AddRange(radiusErrors);
                return null;
            case "status_applied":
                if (TryParse(nested, json, ContingencyJsonSerializerContext.Default.ContingencyStatusTriggerPayloadJsonDto, out ContingencyStatusTriggerPayloadJsonDto? status, out IReadOnlyList<ContentJsonDiagnostic> statusErrors))
                    return Trigger(trigger.Kind, subject: status!.Subject, timing: status.Timing, statusTags: status.StatusTags, applicationMatch: status.ApplicationMatch);
                diagnostics.AddRange(statusErrors);
                return null;
            case "affected_by_spell":
                if (TryParse(nested, json, ContingencyJsonSerializerContext.Default.ContingencySpellTriggerPayloadJsonDto, out ContingencySpellTriggerPayloadJsonDto? spell, out IReadOnlyList<ContentJsonDiagnostic> spellErrors))
                    return Trigger(trigger.Kind, subject: spell!.Subject, timing: spell.Timing, sourceTeam: spell.SourceTeam, spellMatch: spell.SpellMatch);
                diagnostics.AddRange(spellErrors);
                return null;
            default:
                AddUnknown(context, diagnostics, Pointer + "/kind");
                return null;
        }
    }

    private static ContingencyTriggerImportModel Trigger(
        string type,
        string subject = "",
        string timing = "",
        int percent = 0,
        bool crossingOnly = false,
        int damagePercent = 0,
        string damageBasis = "",
        string damageAmountMode = "",
        string center = "",
        int radius = 0,
        string radiusMetric = "",
        string sourceTeam = "",
        IReadOnlyList<string>? statusTags = null,
        string applicationMatch = "",
        string spellMatch = ""
    ) =>
        new(
            type,
            subject,
            timing,
            percent,
            crossingOnly,
            damagePercent,
            damageBasis,
            damageAmountMode,
            center,
            radius,
            radiusMetric,
            sourceTeam,
            statusTags ?? Array.Empty<string>(),
            applicationMatch,
            spellMatch
        );

    private static ContingencyTargetResolverImportModel? NormalizeResolver(
        JsonContentEntryContext context,
        ContingencyTargetResolverJsonDto resolver,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (!TryPayload(context, resolver.Payload, pointer, diagnostics, out string json))
            return null;
        JsonContentEntryContext nested = Nested(context, pointer + "/payload");
        if (resolver.Kind == "empty_cell_near_owner")
        {
            if (TryParse(nested, json, ContingencyJsonSerializerContext.Default.ContingencyEmptyCellResolverPayloadJsonDto, out ContingencyEmptyCellResolverPayloadJsonDto? payload, out IReadOnlyList<ContentJsonDiagnostic> errors))
                return new ContingencyTargetResolverImportModel(resolver.Kind, payload!.Preference, payload.MaxDistance);
            diagnostics.AddRange(errors);
            return null;
        }
        if (
            resolver.Kind
                is "self"
                    or "trigger_source"
                    or "trigger_target"
                    or "nearest_enemy_to_owner"
                    or "nearest_enemy_to_trigger_cell"
                    or "owner_centered_area"
                    or "attacker_cell"
        )
        {
            if (TryParse(nested, json, ContingencyJsonSerializerContext.Default.ContingencyEmptyPayloadJsonDto, out ContingencyEmptyPayloadJsonDto? _, out IReadOnlyList<ContentJsonDiagnostic> errors))
                return new ContingencyTargetResolverImportModel(resolver.Kind, "", 0);
            diagnostics.AddRange(errors);
            return null;
        }
        AddUnknown(context, diagnostics, pointer + "/kind");
        return null;
    }

    private static IReadOnlyDictionary<string, object>? NormalizeBindings(
        JsonContentEntryContext context,
        IReadOnlyDictionary<string, JsonElement> source,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach ((string key, JsonElement value) in source)
        {
            object? plain = PlainBinding(value);
            if (string.IsNullOrEmpty(key) || plain == null)
            {
                diagnostics.Add(new ContentJsonDiagnostic(ContingencyJsonRules.InvalidPayload, "parameter_bindings must contain non-empty keys and scalar/string-array values.", context.SourceLabel, $"{context.JsonPointer}{pointer}/{key}"));
                continue;
            }
            result.Add(key, plain);
        }
        return diagnostics.Count == 0
            ? new ReadOnlyDictionary<string, object>(result)
            : null;
    }

    private static object? PlainBinding(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt64(out long integer) => integer,
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Array => PlainStringArray(value),
            _ => null,
        };

    private static IReadOnlyList<object>? PlainStringArray(JsonElement value)
    {
        var result = new List<object>();
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                return null;
            result.Add(item.GetString() ?? "");
        }
        return new ReadOnlyCollection<object>(result);
    }

    private static bool TryPayload(JsonContentEntryContext context, object raw, string pointer, List<ContentJsonDiagnostic> diagnostics, out string json)
    {
        if (raw is JsonElement payload && payload.ValueKind == JsonValueKind.Object)
        {
            json = payload.GetRawText();
            return true;
        }
        diagnostics.Add(new ContentJsonDiagnostic(ContingencyJsonRules.InvalidPayload, "Closed-kind payload must be a JSON object.", context.SourceLabel, $"{context.JsonPointer}{pointer}/payload"));
        json = "";
        return false;
    }

    private static bool TryParse<T>(JsonContentEntryContext context, string json, JsonTypeInfo<T> typeInfo, out T? result, out IReadOnlyList<ContentJsonDiagnostic> diagnostics)
        where T : class
    {
        ContentImportStageResult<T> parsed = ContentJsonStrictDtoParser.Parse(context, json, typeInfo, ContingencyJsonRules.InvalidPayload);
        result = parsed.HasValue ? parsed.Value : null;
        diagnostics = parsed.Diagnostics;
        return parsed.HasValue;
    }

    private static JsonContentEntryContext Nested(JsonContentEntryContext context, string pointer) =>
        new(context.DomainId, context.EntryId, context.SourceLabel, $"{context.JsonPointer}{pointer}");

    private static void AddUnknown(JsonContentEntryContext context, List<ContentJsonDiagnostic> diagnostics, string pointer) =>
        diagnostics.Add(new ContentJsonDiagnostic(ContingencyJsonRules.UnknownKind, "Contingency closed-kind discriminator is not registered.", context.SourceLabel, $"{context.JsonPointer}{pointer}"));
}

internal static class ContingencyImportValidator
{
    internal static IReadOnlyList<ContentJsonDiagnostic> Validate(
        JsonContentEntryContext context,
        ContingencyTemplateImportModel import
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        if (string.IsNullOrWhiteSpace(import.TemplateId))
            Add(diagnostics, context, ContingencyJsonRules.IdRequired, "template_id is required.", "/template_id");
        else if (!string.Equals(import.TemplateId, context.EntryId, StringComparison.Ordinal))
            Add(diagnostics, context, ContingencyJsonRules.IdMismatch, "template_id must match the envelope entry ID.", "/template_id");
        if (import.StoredSpells.Count == 0)
            Add(diagnostics, context, ContingencyJsonRules.CollectionRequired, "stored_spells must not be empty.", "/stored_spells");
        if (import.ReservedMpPerMatrixLoad <= 0)
            Add(diagnostics, context, ContingencyJsonRules.CollectionRequired, "reserved_mp_per_matrix_load must be positive.", "/reserved_mp_per_matrix_load");
        if (import.ChargeMaterialCosts.Count == 0)
            Add(diagnostics, context, ContingencyJsonRules.CollectionRequired, "charge_material_costs must not be empty.", "/charge_material_costs");
        for (int index = 0; index < import.ChargeMaterialCosts.Count; index++)
        {
            ContingencyMaterialCostImportModel cost = import.ChargeMaterialCosts[index];
            if (string.IsNullOrWhiteSpace(cost.ItemId) || cost.Quantity <= 0)
                Add(diagnostics, context, ContingencyJsonRules.CollectionRequired, "charge material item_id and positive quantity are required.", $"/charge_material_costs/{index}");
        }
        return diagnostics;
    }

    private static void Add(List<ContentJsonDiagnostic> target, JsonContentEntryContext context, string ruleId, string message, string pointer) =>
        target.Add(new ContentJsonDiagnostic(ruleId, message, context.SourceLabel, $"{context.JsonPointer}{pointer}"));
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified)]
[JsonSerializable(typeof(ContingencyTemplateJsonDto))]
[JsonSerializable(typeof(ContingencyMaterialCostJsonDto))]
[JsonSerializable(typeof(ContingencySimpleTriggerPayloadJsonDto))]
[JsonSerializable(typeof(ContingencyHpTriggerPayloadJsonDto))]
[JsonSerializable(typeof(ContingencyIncomingDamageTriggerPayloadJsonDto))]
[JsonSerializable(typeof(ContingencyEnemyRadiusTriggerPayloadJsonDto))]
[JsonSerializable(typeof(ContingencyStatusTriggerPayloadJsonDto))]
[JsonSerializable(typeof(ContingencySpellTriggerPayloadJsonDto))]
[JsonSerializable(typeof(ContingencyEmptyPayloadJsonDto))]
[JsonSerializable(typeof(ContingencyEmptyCellResolverPayloadJsonDto))]
internal partial class ContingencyJsonSerializerContext : JsonSerializerContext { }
