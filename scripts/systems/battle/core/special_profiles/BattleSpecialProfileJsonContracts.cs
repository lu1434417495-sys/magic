#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

internal static class BattleSpecialProfileJsonDomains
{
    internal const int SchemaVersion = 1;
    internal const string ManifestDomainId = "skill_special_profile_manifests";
    internal const string ProfileDomainId = "skill_special_profiles";
    internal const string ManifestDirectory =
        "res://data/configs/json/skill_special_profiles/manifests";
    internal const string ProfileDirectory =
        "res://data/configs/json/skill_special_profiles/profiles";

    internal static ContentJsonSchemaDomainRegistration ManifestSchemaRegistration { get; } =
        new(
            ManifestDomainId,
            SchemaVersion,
            typeof(BattleSpecialProfileManifestJsonDocumentDto),
            "Magic battle special profile manifest JSON authoring schema",
            "Strict profile-ID-only ownership and runtime resolver manifest contract.",
            "res://data/schemas/content/skill_special_profile_manifests.schema.json",
            "/data/configs/json/skill_special_profiles/manifests/**/*.json"
        );

    internal static ContentJsonSchemaDomainRegistration ProfileSchemaRegistration { get; } =
        new(
            ProfileDomainId,
            SchemaVersion,
            typeof(BattleSpecialProfileJsonDocumentDto),
            "Magic battle special profile JSON authoring schema",
            "Strict closed-kind battle special profile payload contract without resource paths.",
            "res://data/schemas/content/skill_special_profiles.schema.json",
            "/data/configs/json/skill_special_profiles/profiles/**/*.json"
        );
}

internal static class BattleSpecialProfileJsonRules
{
    internal const string InvalidManifestDto = "special_profile.manifest.dto.invalid";
    internal const string InvalidProfileDto = "special_profile.profile.dto.invalid";
    internal const string InvalidPayload = "special_profile.payload.invalid";
    internal const string UnknownKind = "special_profile.kind.unknown";
    internal const string IdRequired = "special_profile.id.required";
    internal const string IdMismatch = "special_profile.id.mismatch";
    internal const string ValueRequired = "special_profile.value.required";
    internal const string ValueUnsupported = "special_profile.value.unsupported";
    internal const string ValueOutOfRange = "special_profile.value.out_of_range";
    internal const string DuplicateId = "special_profile.id.duplicate";
}

internal enum BattleSpecialProfileImportKind
{
    Unknown = 0,
    MeteorSwarm,
}

[Description("Battle special profile manifest document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSpecialProfileManifestJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired]
    [ContentJsonSchemaConst(BattleSpecialProfileJsonDomains.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain"), JsonRequired]
    [ContentJsonSchemaConst(BattleSpecialProfileJsonDomains.ManifestDomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family"), JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates"), JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, BattleSpecialProfileManifestJsonDto> Templates { get; init; } =
        new ReadOnlyDictionary<string, BattleSpecialProfileManifestJsonDto>(
            new Dictionary<string, BattleSpecialProfileManifestJsonDto>()
        );

    [JsonPropertyName("entries"), JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        "profile_id",
        "template"
    )]
    public IReadOnlyList<BattleSpecialProfileManifestJsonDto> Entries { get; init; } =
        Array.Empty<BattleSpecialProfileManifestJsonDto>();
}

[Description("Battle special profile document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSpecialProfileJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired]
    [ContentJsonSchemaConst(BattleSpecialProfileJsonDomains.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain"), JsonRequired]
    [ContentJsonSchemaConst(BattleSpecialProfileJsonDomains.ProfileDomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family"), JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates"), JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, BattleSpecialProfileJsonDto> Templates { get; init; } =
        new ReadOnlyDictionary<string, BattleSpecialProfileJsonDto>(
            new Dictionary<string, BattleSpecialProfileJsonDto>()
        );

    [JsonPropertyName("entries"), JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        "profile_id",
        "template"
    )]
    public IReadOnlyList<BattleSpecialProfileJsonDto> Entries { get; init; } =
        Array.Empty<BattleSpecialProfileJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSpecialProfileManifestJsonDto
{
    [JsonPropertyName("profile_id"), JsonRequired]
    public string ProfileId { get; init; } = "";

    [JsonPropertyName("schema_version"), JsonRequired]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("owning_skill_ids"), JsonRequired]
    public IReadOnlyList<string> OwningSkillIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("runtime_resolver_id"), JsonRequired]
    public string RuntimeResolverId { get; init; } = "";

    [JsonPropertyName("runtime_read_policy"), JsonRequired]
    public string RuntimeReadPolicy { get; init; } = "";

    [JsonPropertyName("presentation_metadata"), JsonRequired]
    public BattleSpecialProfilePresentationMetadataJsonDto PresentationMetadata { get; init; } =
        new();

    [JsonPropertyName("deferred_capabilities"), JsonRequired]
    public IReadOnlyList<BattleSpecialProfileDeferredCapabilityJsonDto> DeferredCapabilities { get; init; } =
        Array.Empty<BattleSpecialProfileDeferredCapabilityJsonDto>();

    [JsonPropertyName("sunset_warning_date"), JsonRequired]
    public string SunsetWarningDate { get; init; } = "";

    [JsonPropertyName("sunset_hard_block_date"), JsonRequired]
    public string SunsetHardBlockDate { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSpecialProfilePresentationMetadataJsonDto
{
    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("coverage_shape_id"), JsonRequired]
    public string CoverageShapeId { get; init; } = "";

    [JsonPropertyName("radius"), JsonRequired]
    public int Radius { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSpecialProfileDeferredCapabilityJsonDto
{
    [JsonPropertyName("capability_id"), JsonRequired]
    public string CapabilityId { get; init; } = "";

    [JsonPropertyName("status"), JsonRequired]
    public string Status { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleSpecialProfileJsonDto
{
    [JsonPropertyName("profile_id"), JsonRequired]
    public string ProfileId { get; init; } = "";

    [JsonPropertyName("profile"), JsonRequired]
    public BattleSpecialProfileBodyJsonDto Profile { get; init; } = new();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(BattleSpecialProfileClosedKindSchemaSpec))]
internal sealed class BattleSpecialProfileBodyJsonDto
{

    [JsonPropertyName("kind"), JsonRequired]
    public string Kind { get; init; } = "";

    [JsonPropertyName("payload"), JsonRequired]
    public object Payload { get; init; } = null!;
}

internal sealed class BattleSpecialProfileClosedKindSchemaSpec : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "kind";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } =
        Array.AsReadOnly(
            new[]
            {
                new ContentJsonSchemaClosedKindBranch(
                    "meteor_swarm",
                    typeof(MeteorSwarmProfilePayloadJsonDto)
                ),
            }
        );
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class MeteorSwarmProfilePayloadJsonDto
{
    [JsonPropertyName("coverage_shape_id"), JsonRequired]
    public string CoverageShapeId { get; init; } = "";

    [JsonPropertyName("radius"), JsonRequired]
    public int Radius { get; init; }

    [JsonPropertyName("profile_version"), JsonRequired]
    public int ProfileVersion { get; init; }

    [JsonPropertyName("impact_components"), JsonRequired]
    public IReadOnlyList<MeteorSwarmImpactComponentJsonDto> ImpactComponents { get; init; } =
        Array.Empty<MeteorSwarmImpactComponentJsonDto>();

    [JsonPropertyName("concussed_status_id"), JsonRequired]
    public string ConcussedStatusId { get; init; } = "";

    [JsonPropertyName("terrain_profiles"), JsonRequired]
    public IReadOnlyList<MeteorSwarmTerrainProfileJsonDto> TerrainProfiles { get; init; } =
        Array.Empty<MeteorSwarmTerrainProfileJsonDto>();

    [JsonPropertyName("friendly_fire_soft_expected_hp_percent"), JsonRequired]
    public int FriendlyFireSoftExpectedHpPercent { get; init; }

    [JsonPropertyName("friendly_fire_hard_expected_hp_percent"), JsonRequired]
    public int FriendlyFireHardExpectedHpPercent { get; init; }

    [JsonPropertyName("friendly_fire_hard_worst_case_hp_percent"), JsonRequired]
    public int FriendlyFireHardWorstCaseHpPercent { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class MeteorSwarmImpactComponentJsonDto
{
    [JsonPropertyName("component_id"), JsonRequired]
    public string ComponentId { get; init; } = "";

    [JsonPropertyName("role_label"), JsonRequired]
    public string RoleLabel { get; init; } = "";

    [JsonPropertyName("damage_tag"), JsonRequired]
    public string DamageTag { get; init; } = "";

    [JsonPropertyName("base_power"), JsonRequired]
    public int BasePower { get; init; }

    [JsonPropertyName("dice_count"), JsonRequired]
    public int DiceCount { get; init; }

    [JsonPropertyName("dice_sides"), JsonRequired]
    public int DiceSides { get; init; }

    [JsonPropertyName("ring_weight"), JsonRequired]
    public double RingWeight { get; init; }

    [JsonPropertyName("save_profile_id"), JsonRequired]
    public string SaveProfileId { get; init; } = "";

    [JsonPropertyName("can_crit"), JsonRequired]
    public bool CanCrit { get; init; }

    [JsonPropertyName("mastery_weight"), JsonRequired]
    public double MasteryWeight { get; init; }

    [JsonPropertyName("ring_min"), JsonRequired]
    public int RingMin { get; init; }

    [JsonPropertyName("ring_max"), JsonRequired]
    public int RingMax { get; init; }

    [JsonPropertyName("ring_damage_scale_bp"), JsonRequired]
    public IReadOnlyDictionary<string, double> RingDamageScaleBasisPoints { get; init; } =
        new ReadOnlyDictionary<string, double>(new Dictionary<string, double>());
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class MeteorSwarmTerrainProfileJsonDto
{
    [JsonPropertyName("terrain_profile_id"), JsonRequired]
    public string TerrainProfileId { get; init; } = "";

    [JsonPropertyName("ring_min"), JsonRequired]
    public int RingMin { get; init; }

    [JsonPropertyName("ring_max"), JsonRequired]
    public int RingMax { get; init; }

    [JsonPropertyName("tick_effect_type"), JsonRequired]
    public string TickEffectType { get; init; } = "";

    [JsonPropertyName("lifetime_policy"), JsonRequired]
    public string LifetimePolicy { get; init; } = "";

    [JsonPropertyName("move_cost_delta"), JsonRequired]
    public int MoveCostDelta { get; init; }

    [JsonPropertyName("move_cost_stack_key"), JsonRequired]
    public string MoveCostStackKey { get; init; } = "";

    [JsonPropertyName("move_cost_stack_mode"), JsonRequired]
    public string MoveCostStackMode { get; init; } = "";

    [JsonPropertyName("render_overlay_id"), JsonRequired]
    public string RenderOverlayId { get; init; } = "";

    [JsonPropertyName("overlay_priority"), JsonRequired]
    public int OverlayPriority { get; init; }

    [JsonPropertyName("duration_tu"), JsonRequired]
    public int DurationTu { get; init; }

    [JsonPropertyName("tick_interval_tu"), JsonRequired]
    public int TickIntervalTu { get; init; }

    [JsonPropertyName("accuracy_modifier_spec")]
    public BattleAttackRollModifierJsonDto? AccuracyModifierSpec { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleAttackRollModifierJsonDto
{
    [JsonPropertyName("source_domain"), JsonRequired]
    public string SourceDomain { get; init; } = "";

    [JsonPropertyName("label"), JsonRequired]
    public string Label { get; init; } = "";

    [JsonPropertyName("modifier_delta"), JsonRequired]
    public int ModifierDelta { get; init; }

    [JsonPropertyName("stack_key"), JsonRequired]
    public string StackKey { get; init; } = "";

    [JsonPropertyName("stack_mode"), JsonRequired]
    public string StackMode { get; init; } = "";

    [JsonPropertyName("roll_kind_filter"), JsonRequired]
    public string RollKindFilter { get; init; } = "";

    [JsonPropertyName("endpoint_mode"), JsonRequired]
    public string EndpointMode { get; init; } = "";

    [JsonPropertyName("distance_min_exclusive"), JsonRequired]
    public int DistanceMinExclusive { get; init; }

    [JsonPropertyName("distance_max_inclusive"), JsonRequired]
    public int DistanceMaxInclusive { get; init; }

    [JsonPropertyName("target_team_filter"), JsonRequired]
    public string TargetTeamFilter { get; init; } = "";

    [JsonPropertyName("footprint_mode"), JsonRequired]
    public string FootprintMode { get; init; } = "";

    [JsonPropertyName("applies_to"), JsonRequired]
    public string AppliesTo { get; init; } = "";
}

internal sealed record BattleSpecialProfileManifestImportModel(
    string ProfileId,
    int SchemaVersion,
    IReadOnlyList<string> OwningSkillIds,
    string RuntimeResolverId,
    string RuntimeReadPolicy,
    string DisplayName,
    string CoverageShapeId,
    int Radius,
    IReadOnlyList<BattleSpecialProfileDeferredCapabilityImportModel> DeferredCapabilities,
    string SunsetWarningDate,
    string SunsetHardBlockDate
);

internal sealed record BattleSpecialProfileDeferredCapabilityImportModel(
    string CapabilityId,
    string Status
);

internal sealed record BattleSpecialProfileImportModel(
    string ProfileId,
    BattleSpecialProfileImportKind Kind,
    MeteorSwarmProfileImportModel MeteorSwarm
);

internal sealed record MeteorSwarmProfileImportModel(
    string CoverageShapeId,
    int Radius,
    int ProfileVersion,
    IReadOnlyList<MeteorSwarmImpactComponentImportModel> ImpactComponents,
    string ConcussedStatusId,
    IReadOnlyList<MeteorSwarmTerrainProfileImportModel> TerrainProfiles,
    int FriendlyFireSoftExpectedHpPercent,
    int FriendlyFireHardExpectedHpPercent,
    int FriendlyFireHardWorstCaseHpPercent
);

internal sealed record MeteorSwarmImpactComponentImportModel(
    string ComponentId,
    string RoleLabel,
    string DamageTag,
    int BasePower,
    int DiceCount,
    int DiceSides,
    double RingWeight,
    string SaveProfileId,
    bool CanCrit,
    double MasteryWeight,
    int RingMin,
    int RingMax,
    IReadOnlyDictionary<string, double> RingDamageScaleBasisPoints
);

internal sealed record MeteorSwarmTerrainProfileImportModel(
    string TerrainProfileId,
    int RingMin,
    int RingMax,
    string TickEffectType,
    string LifetimePolicy,
    int MoveCostDelta,
    string MoveCostStackKey,
    string MoveCostStackMode,
    string RenderOverlayId,
    int OverlayPriority,
    int DurationTu,
    int TickIntervalTu,
    BattleAttackRollModifierImportModel? AccuracyModifierSpec
);

internal sealed record BattleAttackRollModifierImportModel(
    string SourceDomain,
    string Label,
    int ModifierDelta,
    string StackKey,
    string StackMode,
    string RollKindFilter,
    string EndpointMode,
    int DistanceMinExclusive,
    int DistanceMaxInclusive,
    string TargetTeamFilter,
    string FootprintMode,
    string AppliesTo
);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
)]
[JsonSerializable(typeof(BattleSpecialProfileManifestJsonDto))]
[JsonSerializable(typeof(BattleSpecialProfileJsonDto))]
[JsonSerializable(typeof(BattleSpecialProfileBodyJsonDto))]
[JsonSerializable(typeof(MeteorSwarmProfilePayloadJsonDto))]
internal partial class BattleSpecialProfileJsonSerializerContext : JsonSerializerContext { }
