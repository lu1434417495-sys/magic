#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

internal static class WorldJsonDomains
{
    internal const int SchemaVersion = 1;
    internal const string PresetDomainId = "world_presets";
    internal const string GenerationDomainId = "world_generations";
    internal const string SharedDomainId = "world_shared";
    internal const string PresetDirectoryPath = "res://data/configs/json/world_presets";
    internal const string GenerationDirectoryPath = "res://data/configs/json/world_generations";
    internal const string SharedDirectoryPath = "res://data/configs/json/world_shared";

    internal static ContentJsonSchemaDomainRegistration PresetSchemaRegistration { get; } =
        new(
            PresetDomainId,
            SchemaVersion,
            typeof(WorldPresetJsonDocumentDto),
            "Magic world preset JSON authoring schema",
            "Strict preset metadata keyed by stable preset and generation IDs.",
            "res://data/schemas/content/world_presets.schema.json",
            "/data/configs/json/world_presets/**/*.json"
        );

    internal static ContentJsonSchemaDomainRegistration GenerationSchemaRegistration { get; } =
        new(
            GenerationDomainId,
            SchemaVersion,
            typeof(WorldGenerationJsonDocumentDto),
            "Magic world generation JSON authoring schema",
            "Strict path-free world generation configuration keyed by stable IDs.",
            "res://data/schemas/content/world_generations.schema.json",
            "/data/configs/json/world_generations/**/*.json"
        );

    internal static ContentJsonSchemaDomainRegistration SharedSchemaRegistration { get; } =
        new(
            SharedDomainId,
            SchemaVersion,
            typeof(WorldSharedJsonDocumentDto),
            "Magic shared world JSON authoring schema",
            "Strict shared settlement, facility, wild-spawn, and name-pool content.",
            "res://data/schemas/content/world_shared.schema.json",
            "/data/configs/json/world_shared/**/*.json"
        );

    internal static IReadOnlyList<ContentJsonSchemaDomainRegistration> SchemaRegistrations { get; } =
        Array.AsReadOnly(
            new[]
            {
                PresetSchemaRegistration,
                GenerationSchemaRegistration,
                SharedSchemaRegistration,
            }
        );
}

internal static class WorldJsonRules
{
    internal const string InvalidPresetDto = "world.preset.dto.invalid";
    internal const string InvalidGenerationDto = "world.generation.dto.invalid";
    internal const string InvalidSharedDto = "world.shared.dto.invalid";
    internal const string IdRequired = "world.id.required";
    internal const string TextRequired = "world.text.required";
    internal const string ValueOutOfRange = "world.value.out_of_range";
    internal const string DuplicateNestedId = "world.nested_id.duplicate";
    internal const string DuplicateTypedKey = "world.typed_key.duplicate";
    internal const string UnknownValue = "world.value.unknown";
}

[Description("World preset document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldPresetJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired]
    [ContentJsonSchemaConst(WorldJsonDomains.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain"), JsonRequired]
    [ContentJsonSchemaConst(WorldJsonDomains.PresetDomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family"), JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates"), JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, WorldPresetJsonDto> Templates { get; init; } =
        EmptyMap<WorldPresetJsonDto>.Value;

    [JsonPropertyName("entries"), JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        "preset_id",
        "template"
    )]
    public IReadOnlyList<WorldPresetJsonDto> Entries { get; init; } =
        Array.Empty<WorldPresetJsonDto>();
}

[Description("World generation document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldGenerationJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired]
    [ContentJsonSchemaConst(WorldJsonDomains.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain"), JsonRequired]
    [ContentJsonSchemaConst(WorldJsonDomains.GenerationDomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family"), JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates"), JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, WorldGenerationJsonDto> Templates { get; init; } =
        EmptyMap<WorldGenerationJsonDto>.Value;

    [JsonPropertyName("entries"), JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        "generation_id",
        "template"
    )]
    public IReadOnlyList<WorldGenerationJsonDto> Entries { get; init; } =
        Array.Empty<WorldGenerationJsonDto>();
}

[Description("Shared world content document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldSharedJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired]
    [ContentJsonSchemaConst(WorldJsonDomains.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain"), JsonRequired]
    [ContentJsonSchemaConst(WorldJsonDomains.SharedDomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family"), JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates"), JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, WorldSharedJsonDto> Templates { get; init; } =
        EmptyMap<WorldSharedJsonDto>.Value;

    [JsonPropertyName("entries"), JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        "shared_content_id",
        "template"
    )]
    public IReadOnlyList<WorldSharedJsonDto> Entries { get; init; } =
        Array.Empty<WorldSharedJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldPresetJsonDto
{
    [JsonPropertyName("preset_id"), JsonRequired]
    public string PresetId { get; init; } = "";

    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("size_label"), JsonRequired]
    public string SizeLabel { get; init; } = "";

    [JsonPropertyName("generation_id"), JsonRequired]
    public string GenerationId { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldGenerationJsonDto
{
    [JsonPropertyName("generation_id"), JsonRequired]
    public string GenerationId { get; init; } = "";

    [JsonPropertyName("seed"), JsonRequired]
    public int Seed { get; init; }

    [JsonPropertyName("world_size_in_chunks"), JsonRequired]
    public WorldVector2IJsonDto WorldSizeInChunks { get; init; } = new();

    [JsonPropertyName("chunk_size"), JsonRequired]
    public WorldVector2IJsonDto ChunkSize { get; init; } = new();

    [JsonPropertyName("player_start_coord"), JsonRequired]
    public WorldVector2IJsonDto PlayerStartCoord { get; init; } = new();

    [JsonPropertyName("player_vision_range"), JsonRequired]
    public int PlayerVisionRange { get; init; }

    [JsonPropertyName("procedural_generation_enabled"), JsonRequired]
    public bool ProceduralGenerationEnabled { get; init; }

    [JsonPropertyName("procedural_wild_spawn_chunk_chance_denominator"), JsonRequired]
    public int ProceduralWildSpawnChunkChanceDenominator { get; init; }

    [JsonPropertyName("shared_content_id"), JsonRequired]
    public string SharedContentId { get; init; } = "";

    [JsonPropertyName("procedural_village_count"), JsonRequired]
    public int ProceduralVillageCount { get; init; }

    [JsonPropertyName("procedural_town_count"), JsonRequired]
    public int ProceduralTownCount { get; init; }

    [JsonPropertyName("procedural_city_count"), JsonRequired]
    public int ProceduralCityCount { get; init; }

    [JsonPropertyName("procedural_capital_count"), JsonRequired]
    public int ProceduralCapitalCount { get; init; }

    [JsonPropertyName("procedural_world_stronghold_count"), JsonRequired]
    public int ProceduralWorldStrongholdCount { get; init; }

    [JsonPropertyName("procedural_metropolis_count"), JsonRequired]
    public int ProceduralMetropolisCount { get; init; }

    [JsonPropertyName("village_spacing_cells"), JsonRequired]
    public int VillageSpacingCells { get; init; }

    [JsonPropertyName("town_spacing_cells"), JsonRequired]
    public int TownSpacingCells { get; init; }

    [JsonPropertyName("city_spacing_cells"), JsonRequired]
    public int CitySpacingCells { get; init; }

    [JsonPropertyName("capital_spacing_cells"), JsonRequired]
    public int CapitalSpacingCells { get; init; }

    [JsonPropertyName("world_stronghold_spacing_cells"), JsonRequired]
    public int WorldStrongholdSpacingCells { get; init; }

    [JsonPropertyName("metropolis_spacing_cells"), JsonRequired]
    public int MetropolisSpacingCells { get; init; }

    [JsonPropertyName("guarantee_starting_wild_encounter"), JsonRequired]
    public bool GuaranteeStartingWildEncounter { get; init; }

    [JsonPropertyName("starting_wild_spawn_min_distance"), JsonRequired]
    public int StartingWildSpawnMinDistance { get; init; }

    [JsonPropertyName("starting_wild_spawn_max_distance"), JsonRequired]
    public int StartingWildSpawnMaxDistance { get; init; }

    [JsonPropertyName("settlement_library"), JsonRequired]
    public IReadOnlyList<WorldSettlementJsonDto> SettlementLibrary { get; init; } =
        Array.Empty<WorldSettlementJsonDto>();

    [JsonPropertyName("facility_library"), JsonRequired]
    public IReadOnlyList<WorldFacilityJsonDto> FacilityLibrary { get; init; } =
        Array.Empty<WorldFacilityJsonDto>();

    [JsonPropertyName("settlement_distribution"), JsonRequired]
    public IReadOnlyList<WorldSettlementDistributionJsonDto> SettlementDistribution { get; init; } =
        Array.Empty<WorldSettlementDistributionJsonDto>();

    [JsonPropertyName("wild_monster_distribution"), JsonRequired]
    public IReadOnlyList<WorldWildSpawnJsonDto> WildMonsterDistribution { get; init; } =
        Array.Empty<WorldWildSpawnJsonDto>();

    [JsonPropertyName("mounted_submaps"), JsonRequired]
    public IReadOnlyList<WorldMountedSubmapJsonDto> MountedSubmaps { get; init; } =
        Array.Empty<WorldMountedSubmapJsonDto>();

    [JsonPropertyName("world_events"), JsonRequired]
    public IReadOnlyList<WorldEventJsonDto> WorldEvents { get; init; } =
        Array.Empty<WorldEventJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldSharedJsonDto
{
    [JsonPropertyName("shared_content_id"), JsonRequired]
    public string SharedContentId { get; init; } = "";

    [JsonPropertyName("settlement_library"), JsonRequired]
    public IReadOnlyList<WorldSettlementJsonDto> SettlementLibrary { get; init; } =
        Array.Empty<WorldSettlementJsonDto>();

    [JsonPropertyName("facility_library"), JsonRequired]
    public IReadOnlyList<WorldFacilityJsonDto> FacilityLibrary { get; init; } =
        Array.Empty<WorldFacilityJsonDto>();

    [JsonPropertyName("wild_monster_distribution"), JsonRequired]
    public IReadOnlyList<WorldWildSpawnJsonDto> WildMonsterDistribution { get; init; } =
        Array.Empty<WorldWildSpawnJsonDto>();

    [JsonPropertyName("settlement_name_pools"), JsonRequired]
    public IReadOnlyList<WorldSettlementNamePoolJsonDto> SettlementNamePools { get; init; } =
        Array.Empty<WorldSettlementNamePoolJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldVector2IJsonDto
{
    [JsonPropertyName("x"), JsonRequired]
    public int X { get; init; }

    [JsonPropertyName("y"), JsonRequired]
    public int Y { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldSettlementJsonDto
{
    [JsonPropertyName("settlement_id"), JsonRequired]
    public string SettlementId { get; init; } = "";

    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("tier"), JsonRequired]
    public int Tier { get; init; }

    [JsonPropertyName("facility_slots"), JsonRequired]
    public IReadOnlyList<WorldFacilitySlotJsonDto> FacilitySlots { get; init; } =
        Array.Empty<WorldFacilitySlotJsonDto>();

    [JsonPropertyName("guaranteed_facility_ids"), JsonRequired]
    public IReadOnlyList<string> GuaranteedFacilityIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("optional_facility_pool"), JsonRequired]
    public IReadOnlyList<WorldWeightedFacilityJsonDto> OptionalFacilityPool { get; init; } =
        Array.Empty<WorldWeightedFacilityJsonDto>();

    [JsonPropertyName("max_optional_facilities"), JsonRequired]
    public int MaxOptionalFacilities { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldFacilityJsonDto
{
    [JsonPropertyName("facility_id"), JsonRequired]
    public string FacilityId { get; init; } = "";

    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("category"), JsonRequired]
    public string Category { get; init; } = "";

    [JsonPropertyName("min_settlement_tier"), JsonRequired]
    public int MinSettlementTier { get; init; }

    [JsonPropertyName("allowed_slot_tags"), JsonRequired]
    public IReadOnlyList<string> AllowedSlotTags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("bound_service_npcs"), JsonRequired]
    public IReadOnlyList<WorldFacilityNpcJsonDto> BoundServiceNpcs { get; init; } =
        Array.Empty<WorldFacilityNpcJsonDto>();

    [JsonPropertyName("interaction_type"), JsonRequired]
    public string InteractionType { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldFacilitySlotJsonDto
{
    [JsonPropertyName("slot_id"), JsonRequired]
    public string SlotId { get; init; } = "";

    [JsonPropertyName("local_coord"), JsonRequired]
    public WorldVector2IJsonDto LocalCoord { get; init; } = new();

    [JsonPropertyName("slot_tag"), JsonRequired]
    public string SlotTag { get; init; } = "";

    [JsonPropertyName("required"), JsonRequired]
    public bool Required { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldFacilityNpcJsonDto
{
    [JsonPropertyName("npc_id"), JsonRequired]
    public string NpcId { get; init; } = "";

    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("service_type"), JsonRequired]
    public string ServiceType { get; init; } = "";

    [JsonPropertyName("interaction_script_id"), JsonRequired]
    public string InteractionScriptId { get; init; } = "";

    [JsonPropertyName("local_slot_id"), JsonRequired]
    public string LocalSlotId { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldWeightedFacilityJsonDto
{
    [JsonPropertyName("facility_id"), JsonRequired]
    public string FacilityId { get; init; } = "";

    [JsonPropertyName("weight"), JsonRequired]
    public int Weight { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldSettlementDistributionJsonDto
{
    [JsonPropertyName("settlement_id"), JsonRequired]
    public string SettlementId { get; init; } = "";

    [JsonPropertyName("preferred_origin"), JsonRequired]
    public WorldVector2IJsonDto PreferredOrigin { get; init; } = new();

    [JsonPropertyName("faction_id"), JsonRequired]
    public string FactionId { get; init; } = "";

    [JsonPropertyName("country_id"), JsonRequired]
    public string CountryId { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldWildSpawnJsonDto
{
    [JsonPropertyName("region_tag"), JsonRequired]
    public string RegionTag { get; init; } = "";

    [JsonPropertyName("vertical_band"), JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(WorldVerticalBandJsonValues))]
    public string VerticalBand { get; init; } = "";

    [JsonPropertyName("monster_name"), JsonRequired]
    public string MonsterName { get; init; } = "";

    [JsonPropertyName("encounter_profile_id"), JsonRequired]
    public string EncounterProfileId { get; init; } = "";

    [JsonPropertyName("settlement_encounter_profile_id"), JsonRequired]
    public string SettlementEncounterProfileId { get; init; } = "";

    [JsonPropertyName("settlement_encounter_display_name"), JsonRequired]
    public string SettlementEncounterDisplayName { get; init; } = "";

    [JsonPropertyName("density_per_chunk"), JsonRequired]
    public int DensityPerChunk { get; init; }

    [JsonPropertyName("min_distance_to_settlement"), JsonRequired]
    public int MinDistanceToSettlement { get; init; }

    [JsonPropertyName("vision_range"), JsonRequired]
    public int VisionRange { get; init; }

    [JsonPropertyName("chunk_coords"), JsonRequired]
    public IReadOnlyList<WorldVector2IJsonDto> ChunkCoords { get; init; } =
        Array.Empty<WorldVector2IJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldMountedSubmapJsonDto
{
    [JsonPropertyName("submap_id"), JsonRequired]
    public string SubmapId { get; init; } = "";

    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("generation_id"), JsonRequired]
    public string GenerationId { get; init; } = "";

    [JsonPropertyName("return_hint_text"), JsonRequired]
    public string ReturnHintText { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldEventJsonDto
{
    [JsonPropertyName("event_id"), JsonRequired]
    public string EventId { get; init; } = "";

    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("world_coord"), JsonRequired]
    public WorldVector2IJsonDto WorldCoord { get; init; } = new();

    [JsonPropertyName("event_type"), JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(WorldEventTypeJsonValues))]
    public string EventType { get; init; } = "";

    [JsonPropertyName("target_submap_id"), JsonRequired]
    public string TargetSubmapId { get; init; } = "";

    [JsonPropertyName("discovery_condition_id"), JsonRequired]
    public string DiscoveryConditionId { get; init; } = "";

    [JsonPropertyName("prompt_title"), JsonRequired]
    public string PromptTitle { get; init; } = "";

    [JsonPropertyName("prompt_text"), JsonRequired]
    public string PromptText { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorldSettlementNamePoolJsonDto
{
    [JsonPropertyName("settlement_tier"), JsonRequired]
    [ContentJsonSchemaStableStringValues(typeof(WorldSettlementTierJsonValues))]
    public string SettlementTier { get; init; } = "";

    [JsonPropertyName("display_names"), JsonRequired]
    public IReadOnlyList<string> DisplayNames { get; init; } = Array.Empty<string>();
}

internal sealed class WorldEventTypeJsonValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } = Array.AsReadOnly(new[] { "enter_submap" });
}

internal sealed class WorldSettlementTierJsonValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } =
        Array.AsReadOnly(new[] { "village", "town", "city", "capital", "world_stronghold", "metropolis" });
}

internal sealed class WorldVerticalBandJsonValues : IContentJsonSchemaStableStringValues
{
    public IReadOnlyList<string> Values { get; } =
        Array.AsReadOnly(new[] { "all", "north", "south" });
}

internal sealed record WorldVector2IImportModel(int X, int Y);
internal sealed record WorldPresetImportModel(
    string PresetId,
    string DisplayName,
    string SizeLabel,
    string GenerationId
);
internal sealed record WorldGenerationImportModel(
    string GenerationId,
    int Seed,
    WorldVector2IImportModel WorldSizeInChunks,
    WorldVector2IImportModel ChunkSize,
    WorldVector2IImportModel PlayerStartCoord,
    int PlayerVisionRange,
    bool ProceduralGenerationEnabled,
    int ProceduralWildSpawnChunkChanceDenominator,
    string SharedContentId,
    int ProceduralVillageCount,
    int ProceduralTownCount,
    int ProceduralCityCount,
    int ProceduralCapitalCount,
    int ProceduralWorldStrongholdCount,
    int ProceduralMetropolisCount,
    int VillageSpacingCells,
    int TownSpacingCells,
    int CitySpacingCells,
    int CapitalSpacingCells,
    int WorldStrongholdSpacingCells,
    int MetropolisSpacingCells,
    bool GuaranteeStartingWildEncounter,
    int StartingWildSpawnMinDistance,
    int StartingWildSpawnMaxDistance,
    IReadOnlyList<WorldSettlementImportModel> SettlementLibrary,
    IReadOnlyList<WorldFacilityImportModel> FacilityLibrary,
    IReadOnlyList<WorldSettlementDistributionImportModel> SettlementDistribution,
    IReadOnlyList<WorldWildSpawnImportModel> WildMonsterDistribution,
    IReadOnlyList<WorldMountedSubmapImportModel> MountedSubmaps,
    IReadOnlyList<WorldEventImportModel> WorldEvents
);
internal sealed record WorldSharedImportModel(
    string SharedContentId,
    IReadOnlyList<WorldSettlementImportModel> SettlementLibrary,
    IReadOnlyList<WorldFacilityImportModel> FacilityLibrary,
    IReadOnlyList<WorldWildSpawnImportModel> WildMonsterDistribution,
    IReadOnlyList<WorldSettlementNamePoolImportModel> SettlementNamePools
);
internal sealed record WorldSettlementImportModel(
    string SettlementId,
    string DisplayName,
    int Tier,
    IReadOnlyList<WorldFacilitySlotImportModel> FacilitySlots,
    IReadOnlyList<string> GuaranteedFacilityIds,
    IReadOnlyList<WorldWeightedFacilityImportModel> OptionalFacilityPool,
    int MaxOptionalFacilities
);
internal sealed record WorldFacilityImportModel(
    string FacilityId,
    string DisplayName,
    string Category,
    int MinSettlementTier,
    IReadOnlyList<string> AllowedSlotTags,
    IReadOnlyList<WorldFacilityNpcImportModel> BoundServiceNpcs,
    string InteractionType
);
internal sealed record WorldFacilitySlotImportModel(
    string SlotId,
    WorldVector2IImportModel LocalCoord,
    string SlotTag,
    bool Required
);
internal sealed record WorldFacilityNpcImportModel(
    string NpcId,
    string DisplayName,
    string ServiceType,
    string InteractionScriptId,
    string LocalSlotId
);
internal sealed record WorldWeightedFacilityImportModel(string FacilityId, int Weight);
internal sealed record WorldSettlementDistributionImportModel(
    string SettlementId,
    WorldVector2IImportModel PreferredOrigin,
    string FactionId,
    string CountryId
);
internal sealed record WorldWildSpawnImportModel(
    string RegionTag,
    string VerticalBand,
    string MonsterName,
    string EncounterProfileId,
    string SettlementEncounterProfileId,
    string SettlementEncounterDisplayName,
    int DensityPerChunk,
    int MinDistanceToSettlement,
    int VisionRange,
    IReadOnlyList<WorldVector2IImportModel> ChunkCoords
);
internal sealed record WorldMountedSubmapImportModel(
    string SubmapId,
    string DisplayName,
    string GenerationId,
    string ReturnHintText
);
internal sealed record WorldEventImportModel(
    string EventId,
    string DisplayName,
    WorldVector2IImportModel WorldCoord,
    string EventType,
    string TargetSubmapId,
    string DiscoveryConditionId,
    string PromptTitle,
    string PromptText
);
internal sealed record WorldSettlementNamePoolImportModel(
    string SettlementTier,
    IReadOnlyList<string> DisplayNames
);

[JsonSerializable(typeof(WorldPresetJsonDto))]
[JsonSerializable(typeof(WorldGenerationJsonDto))]
[JsonSerializable(typeof(WorldSharedJsonDto))]
internal partial class WorldJsonSerializerContext : JsonSerializerContext { }
