#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

internal static class BarrierJsonDomains
{
    internal const int SchemaVersion = 1;
    internal const string ProfileDomainId = "barriers";
    internal const string LayerDomainId = "barrier_layers";
    internal const string ProfileDirectory = "res://data/configs/json/barriers";
    internal const string LayerDirectory = "res://data/configs/json/barrier_layers";

    internal static ContentJsonSchemaDomainRegistration ProfileSchemaRegistration { get; } =
        new(
            ProfileDomainId,
            SchemaVersion,
            typeof(BarrierProfileJsonDocumentDto),
            "Magic barrier profile JSON authoring schema",
            "Strict barrier profiles referencing canonical barrier layer IDs.",
            "res://data/schemas/content/barriers.schema.json",
            "/data/configs/json/barriers/**/*.json"
        );

    internal static ContentJsonSchemaDomainRegistration LayerSchemaRegistration { get; } =
        new(
            LayerDomainId,
            SchemaVersion,
            typeof(BarrierLayerJsonDocumentDto),
            "Magic barrier layer JSON authoring schema",
            "Strict reusable barrier layers with typed passage outcomes.",
            "res://data/schemas/content/barrier_layers.schema.json",
            "/data/configs/json/barrier_layers/**/*.json"
        );
}

internal static class BarrierJsonRules
{
    internal const string InvalidProfileDto = "barrier.profile.dto.invalid";
    internal const string InvalidLayerDto = "barrier.layer.dto.invalid";
    internal const string IdRequired = "barrier.id.required";
    internal const string IdMismatch = "barrier.id.mismatch";
    internal const string ValueRequired = "barrier.value.required";
    internal const string ValueUnsupported = "barrier.value.unsupported";
    internal const string ValueOutOfRange = "barrier.value.out_of_range";
    internal const string DuplicateId = "barrier.id.duplicate";
}

internal enum BarrierAnchorImportKind
{
    Unknown = 0,
    Fixed,
}

internal enum BarrierAreaPatternImportKind
{
    Unknown = 0,
    Single,
    Diamond,
    Square,
    Radius,
    Cross,
}

internal enum BarrierOutcomeImportKind
{
    None = 0,
    Unknown,
    Damage,
    PoisonDeath,
    Status,
    Banish,
}

[Description("Barrier profile document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BarrierProfileJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired]
    [ContentJsonSchemaConst(BarrierJsonDomains.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain"), JsonRequired]
    [ContentJsonSchemaConst(BarrierJsonDomains.ProfileDomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family"), JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates"), JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, BarrierProfileJsonDto> Templates { get; init; } =
        new ReadOnlyDictionary<string, BarrierProfileJsonDto>(
            new Dictionary<string, BarrierProfileJsonDto>()
        );

    [JsonPropertyName("entries"), JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        "profile_id",
        "template"
    )]
    public IReadOnlyList<BarrierProfileJsonDto> Entries { get; init; } =
        Array.Empty<BarrierProfileJsonDto>();
}

[Description("Barrier layer document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BarrierLayerJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired]
    [ContentJsonSchemaConst(BarrierJsonDomains.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain"), JsonRequired]
    [ContentJsonSchemaConst(BarrierJsonDomains.LayerDomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family"), JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates"), JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, BarrierLayerJsonDto> Templates { get; init; } =
        new ReadOnlyDictionary<string, BarrierLayerJsonDto>(
            new Dictionary<string, BarrierLayerJsonDto>()
        );

    [JsonPropertyName("entries"), JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        "layer_id",
        "template"
    )]
    public IReadOnlyList<BarrierLayerJsonDto> Entries { get; init; } =
        Array.Empty<BarrierLayerJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BarrierProfileJsonDto
{
    [JsonPropertyName("profile_id"), JsonRequired]
    public string ProfileId { get; init; } = "";

    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("anchor_mode"), JsonRequired]
    public string AnchorMode { get; init; } = "";

    [JsonPropertyName("area_pattern"), JsonRequired]
    public string AreaPattern { get; init; } = "";

    [JsonPropertyName("radius_cells"), JsonRequired]
    public int RadiusCells { get; init; }

    [JsonPropertyName("duration_tu"), JsonRequired]
    public int DurationTu { get; init; }

    [JsonPropertyName("catch_all_projected_effects"), JsonRequired]
    public bool CatchAllProjectedEffects { get; init; }

    [JsonPropertyName("layer_ids"), JsonRequired]
    public IReadOnlyList<string> LayerIds { get; init; } = Array.Empty<string>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BarrierLayerJsonDto
{
    [JsonPropertyName("layer_id"), JsonRequired]
    public string LayerId { get; init; } = "";

    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("order"), JsonRequired]
    public int Order { get; init; }

    [JsonPropertyName("blocked_categories"), JsonRequired]
    public IReadOnlyList<string> BlockedCategories { get; init; } = Array.Empty<string>();

    [JsonPropertyName("breaker_skill_ids"), JsonRequired]
    public IReadOnlyList<string> BreakerSkillIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("passage_outcomes"), JsonRequired]
    public IReadOnlyList<BarrierOutcomeJsonDto> PassageOutcomes { get; init; } =
        Array.Empty<BarrierOutcomeJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BarrierOutcomeJsonDto
{
    [JsonPropertyName("outcome_type"), JsonRequired]
    public string OutcomeType { get; init; } = "";

    [JsonPropertyName("amount"), JsonRequired]
    public int Amount { get; init; }

    [JsonPropertyName("damage_tag"), JsonRequired]
    public string DamageTag { get; init; } = "";

    [JsonPropertyName("half_on_success"), JsonRequired]
    public bool HalfOnSuccess { get; init; }

    [JsonPropertyName("success_amount"), JsonRequired]
    public int SuccessAmount { get; init; }

    [JsonPropertyName("success_damage_tag"), JsonRequired]
    public string SuccessDamageTag { get; init; } = "";

    [JsonPropertyName("fatal_damage"), JsonRequired]
    public int FatalDamage { get; init; }

    [JsonPropertyName("status_id"), JsonRequired]
    public string StatusId { get; init; } = "";

    [JsonPropertyName("save_ability"), JsonRequired]
    public string SaveAbility { get; init; } = "";

    [JsonPropertyName("save_tag"), JsonRequired]
    public string SaveTag { get; init; } = "";

    [JsonPropertyName("save_dc"), JsonRequired]
    public int SaveDc { get; init; }
}

internal sealed record BarrierProfileImportModel(
    string ProfileId,
    string DisplayName,
    BarrierAnchorImportKind AnchorMode,
    BarrierAreaPatternImportKind AreaPattern,
    int RadiusCells,
    int DurationTu,
    bool CatchAllProjectedEffects,
    IReadOnlyList<string> LayerIds
);

internal sealed record BarrierLayerImportModel(
    string LayerId,
    string DisplayName,
    int Order,
    IReadOnlyList<string> BlockedCategories,
    IReadOnlyList<string> BreakerSkillIds,
    IReadOnlyList<BarrierOutcomeImportModel> PassageOutcomes
);

internal sealed record BarrierOutcomeImportModel(
    BarrierOutcomeImportKind OutcomeKind,
    int Amount,
    string DamageTag,
    bool HalfOnSuccess,
    int SuccessAmount,
    string SuccessDamageTag,
    int FatalDamage,
    string StatusId,
    string SaveAbility,
    string SaveTag,
    int SaveDc
);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
)]
[JsonSerializable(typeof(BarrierProfileJsonDto))]
[JsonSerializable(typeof(BarrierLayerJsonDto))]
internal partial class BarrierJsonSerializerContext : JsonSerializerContext { }
