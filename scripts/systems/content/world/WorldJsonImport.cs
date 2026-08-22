#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

internal static class WorldJsonImport
{
    private static readonly ContentJsonNullabilityPolicy NullabilityPolicy =
        new(Array.Empty<string>());

    private static readonly HashSet<string> EventTypes =
        new(new[] { "enter_submap" }, StringComparer.Ordinal);

    private static readonly HashSet<string> NamePoolIds =
        new(
            new[] { "village", "town", "city", "capital", "metropolis" },
            StringComparer.Ordinal
        );

    internal static JsonContentDomainDescriptor<WorldPresetJsonDto, WorldPresetImportModel>
        CreatePresetDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        new(
            WorldJsonDomains.PresetDomainId,
            WorldJsonDomains.SchemaVersion,
            "preset_id",
            sourceDirectory,
            sourceReader,
            NullabilityPolicy,
            ParsePreset,
            NormalizePreset,
            ValidatePreset
        );

    internal static JsonContentDomainDescriptor<
        WorldGenerationJsonDto,
        WorldGenerationImportModel
    > CreateGenerationDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        new(
            WorldJsonDomains.GenerationDomainId,
            WorldJsonDomains.SchemaVersion,
            "generation_id",
            sourceDirectory,
            sourceReader,
            NullabilityPolicy,
            ParseGeneration,
            NormalizeGeneration,
            ValidateGeneration
        );

    internal static JsonContentDomainDescriptor<WorldSharedJsonDto, WorldSharedImportModel>
        CreateSharedDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        new(
            WorldJsonDomains.SharedDomainId,
            WorldJsonDomains.SchemaVersion,
            "shared_content_id",
            sourceDirectory,
            sourceReader,
            NullabilityPolicy,
            ParseShared,
            NormalizeShared,
            ValidateShared
        );

    internal static IEnumerable<IContentJsonOfflineValidationDomain> CreateOfflineDomains()
    {
        yield return new PresetOfflineDomain();
        yield return new GenerationOfflineDomain();
        yield return new SharedOfflineDomain();
    }

    private static ContentImportStageResult<WorldPresetJsonDto> ParsePreset(
        JsonContentEntryContext context,
        string json
    ) =>
        ContentJsonStrictDtoParser.Parse(
            context,
            json,
            WorldJsonSerializerContext.Default.WorldPresetJsonDto,
            WorldJsonRules.InvalidPresetDto
        );

    private static ContentImportStageResult<WorldGenerationJsonDto> ParseGeneration(
        JsonContentEntryContext context,
        string json
    ) =>
        ContentJsonStrictDtoParser.Parse(
            context,
            json,
            WorldJsonSerializerContext.Default.WorldGenerationJsonDto,
            WorldJsonRules.InvalidGenerationDto
        );

    private static ContentImportStageResult<WorldSharedJsonDto> ParseShared(
        JsonContentEntryContext context,
        string json
    ) =>
        ContentJsonStrictDtoParser.Parse(
            context,
            json,
            WorldJsonSerializerContext.Default.WorldSharedJsonDto,
            WorldJsonRules.InvalidSharedDto
        );

    private static ContentImportStageResult<WorldPresetImportModel> NormalizePreset(
        JsonContentEntryContext context,
        WorldPresetJsonDto dto
    ) =>
        ContentImportStageResult<WorldPresetImportModel>.Success(
            new WorldPresetImportModel(
                Trim(dto.PresetId),
                Trim(dto.DisplayName),
                Trim(dto.SizeLabel),
                Trim(dto.GenerationId)
            )
        );

    private static ContentImportStageResult<WorldGenerationImportModel> NormalizeGeneration(
        JsonContentEntryContext context,
        WorldGenerationJsonDto dto
    ) =>
        ContentImportStageResult<WorldGenerationImportModel>.Success(
            new WorldGenerationImportModel(
                Trim(dto.GenerationId),
                dto.Seed,
                Vector(dto.WorldSizeInChunks),
                Vector(dto.ChunkSize),
                Vector(dto.PlayerStartCoord),
                dto.PlayerVisionRange,
                dto.ProceduralGenerationEnabled,
                dto.ProceduralWildSpawnChunkChanceDenominator,
                Trim(dto.SharedContentId),
                dto.ProceduralVillageCount,
                dto.ProceduralTownCount,
                dto.ProceduralCityCount,
                dto.ProceduralCapitalCount,
                dto.ProceduralWorldStrongholdCount,
                dto.ProceduralMetropolisCount,
                dto.VillageSpacingCells,
                dto.TownSpacingCells,
                dto.CitySpacingCells,
                dto.CapitalSpacingCells,
                dto.WorldStrongholdSpacingCells,
                dto.MetropolisSpacingCells,
                dto.GuaranteeStartingWildEncounter,
                dto.StartingWildSpawnMinDistance,
                dto.StartingWildSpawnMaxDistance,
                dto.SettlementLibrary.Select(Settlement).ToArray(),
                dto.FacilityLibrary.Select(Facility).ToArray(),
                dto.SettlementDistribution.Select(SettlementDistribution).ToArray(),
                dto.WildMonsterDistribution.Select(WildSpawn).ToArray(),
                dto.MountedSubmaps.Select(MountedSubmap).ToArray(),
                dto.WorldEvents.Select(WorldEvent).ToArray()
            )
        );

    private static ContentImportStageResult<WorldSharedImportModel> NormalizeShared(
        JsonContentEntryContext context,
        WorldSharedJsonDto dto
    ) =>
        ContentImportStageResult<WorldSharedImportModel>.Success(
            new WorldSharedImportModel(
                Trim(dto.SharedContentId),
                dto.SettlementLibrary.Select(Settlement).ToArray(),
                dto.FacilityLibrary.Select(Facility).ToArray(),
                dto.WildMonsterDistribution.Select(WildSpawn).ToArray(),
                dto.SettlementNamePools
                    .Select(pool => new WorldSettlementNamePoolImportModel(
                        Trim(pool.PoolId),
                        pool.DisplayNames.Select(Trim).ToArray()
                    ))
                    .ToArray()
            )
        );

    private static WorldSettlementImportModel Settlement(WorldSettlementJsonDto dto) =>
        new(
            Trim(dto.SettlementId),
            Trim(dto.DisplayName),
            dto.Tier,
            dto.FacilitySlots
                .Select(slot => new WorldFacilitySlotImportModel(
                    Trim(slot.SlotId),
                    Vector(slot.LocalCoord),
                    Trim(slot.SlotTag),
                    slot.Required
                ))
                .ToArray(),
            dto.GuaranteedFacilityIds.Select(Trim).ToArray(),
            dto.OptionalFacilityPool
                .Select(entry => new WorldWeightedFacilityImportModel(
                    Trim(entry.FacilityId),
                    entry.Weight
                ))
                .ToArray(),
            dto.MaxOptionalFacilities
        );

    private static WorldFacilityImportModel Facility(WorldFacilityJsonDto dto) =>
        new(
            Trim(dto.FacilityId),
            Trim(dto.DisplayName),
            Trim(dto.Category),
            dto.MinSettlementTier,
            dto.AllowedSlotTags.Select(Trim).ToArray(),
            dto.BoundServiceNpcs
                .Select(npc => new WorldFacilityNpcImportModel(
                    Trim(npc.NpcId),
                    Trim(npc.DisplayName),
                    Trim(npc.ServiceType),
                    Trim(npc.InteractionScriptId),
                    Trim(npc.LocalSlotId)
                ))
                .ToArray(),
            Trim(dto.InteractionType)
        );

    private static WorldSettlementDistributionImportModel SettlementDistribution(
        WorldSettlementDistributionJsonDto dto
    ) =>
        new(
            Trim(dto.SettlementId),
            Vector(dto.PreferredOrigin),
            Trim(dto.FactionId),
            Trim(dto.CountryId)
        );

    private static WorldWildSpawnImportModel WildSpawn(WorldWildSpawnJsonDto dto) =>
        new(
            Trim(dto.RegionTag),
            Trim(dto.MonsterName),
            Trim(dto.EncounterProfileId),
            Trim(dto.SettlementEncounterProfileId),
            Trim(dto.SettlementEncounterDisplayName),
            dto.DensityPerChunk,
            dto.MinDistanceToSettlement,
            dto.VisionRange,
            dto.ChunkCoords.Select(Vector).ToArray()
        );

    private static WorldMountedSubmapImportModel MountedSubmap(WorldMountedSubmapJsonDto dto) =>
        new(
            Trim(dto.SubmapId),
            Trim(dto.DisplayName),
            Trim(dto.GenerationId),
            Trim(dto.ReturnHintText)
        );

    private static WorldEventImportModel WorldEvent(WorldEventJsonDto dto) =>
        new(
            Trim(dto.EventId),
            Trim(dto.DisplayName),
            Vector(dto.WorldCoord),
            Trim(dto.EventType),
            Trim(dto.TargetSubmapId),
            Trim(dto.DiscoveryConditionId),
            Trim(dto.PromptTitle),
            Trim(dto.PromptText)
        );

    private static WorldVector2IImportModel Vector(WorldVector2IJsonDto dto) =>
        new(dto.X, dto.Y);

    private static IReadOnlyList<ContentJsonDiagnostic> ValidatePreset(
        JsonContentEntryContext context,
        WorldPresetImportModel import
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        RequireText(context, diagnostics, import.PresetId, "/preset_id", id: true);
        RequireText(context, diagnostics, import.DisplayName, "/display_name");
        RequireText(context, diagnostics, import.SizeLabel, "/size_label");
        RequireText(context, diagnostics, import.GenerationId, "/generation_id", id: true);
        return diagnostics;
    }

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateGeneration(
        JsonContentEntryContext context,
        WorldGenerationImportModel import
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        RequireText(context, diagnostics, import.GenerationId, "/generation_id", id: true);
        RequirePositiveVector(context, diagnostics, import.WorldSizeInChunks, "/world_size_in_chunks");
        RequirePositiveVector(context, diagnostics, import.ChunkSize, "/chunk_size");
        RequireNonNegativeVector(context, diagnostics, import.PlayerStartCoord, "/player_start_coord");
        RequireNonNegative(context, diagnostics, import.PlayerVisionRange, "/player_vision_range");
        if (import.ProceduralWildSpawnChunkChanceDenominator <= 0)
            Range(context, diagnostics, "/procedural_wild_spawn_chunk_chance_denominator");
        int[] nonNegativeValues =
        {
            import.ProceduralVillageCount,
            import.ProceduralTownCount,
            import.ProceduralCityCount,
            import.ProceduralCapitalCount,
            import.ProceduralWorldStrongholdCount,
            import.ProceduralMetropolisCount,
            import.VillageSpacingCells,
            import.TownSpacingCells,
            import.CitySpacingCells,
            import.CapitalSpacingCells,
            import.WorldStrongholdSpacingCells,
            import.MetropolisSpacingCells,
            import.StartingWildSpawnMinDistance,
            import.StartingWildSpawnMaxDistance,
        };
        string[] nonNegativePointers =
        {
            "/procedural_village_count",
            "/procedural_town_count",
            "/procedural_city_count",
            "/procedural_capital_count",
            "/procedural_world_stronghold_count",
            "/procedural_metropolis_count",
            "/village_spacing_cells",
            "/town_spacing_cells",
            "/city_spacing_cells",
            "/capital_spacing_cells",
            "/world_stronghold_spacing_cells",
            "/metropolis_spacing_cells",
            "/starting_wild_spawn_min_distance",
            "/starting_wild_spawn_max_distance",
        };
        for (int index = 0; index < nonNegativeValues.Length; index++)
            RequireNonNegative(context, diagnostics, nonNegativeValues[index], nonNegativePointers[index]);
        if (import.StartingWildSpawnMinDistance > import.StartingWildSpawnMaxDistance)
            Range(context, diagnostics, "/starting_wild_spawn_min_distance");

        ValidateSettlements(context, diagnostics, import.SettlementLibrary, "/settlement_library");
        ValidateFacilities(context, diagnostics, import.FacilityLibrary, "/facility_library");
        DuplicateIds(
            context,
            diagnostics,
            import.SettlementDistribution.Select(value => value.SettlementId),
            "/settlement_distribution"
        );
        for (int index = 0; index < import.SettlementDistribution.Count; index++)
        {
            WorldSettlementDistributionImportModel value = import.SettlementDistribution[index];
            string pointer = $"/settlement_distribution/{index}";
            RequireText(context, diagnostics, value.SettlementId, pointer + "/settlement_id", id: true);
            RequireText(context, diagnostics, value.FactionId, pointer + "/faction_id", id: true);
            RequireNonNegativeVector(context, diagnostics, value.PreferredOrigin, pointer + "/preferred_origin");
        }
        ValidateWildSpawns(context, diagnostics, import.WildMonsterDistribution, "/wild_monster_distribution");
        DuplicateIds(
            context,
            diagnostics,
            import.MountedSubmaps.Select(value => value.SubmapId),
            "/mounted_submaps"
        );
        for (int index = 0; index < import.MountedSubmaps.Count; index++)
        {
            WorldMountedSubmapImportModel value = import.MountedSubmaps[index];
            string pointer = $"/mounted_submaps/{index}";
            RequireText(context, diagnostics, value.SubmapId, pointer + "/submap_id", id: true);
            RequireText(context, diagnostics, value.DisplayName, pointer + "/display_name");
            RequireText(context, diagnostics, value.GenerationId, pointer + "/generation_id", id: true);
            RequireText(context, diagnostics, value.ReturnHintText, pointer + "/return_hint_text");
        }
        DuplicateIds(
            context,
            diagnostics,
            import.WorldEvents.Select(value => value.EventId),
            "/world_events"
        );
        for (int index = 0; index < import.WorldEvents.Count; index++)
        {
            WorldEventImportModel value = import.WorldEvents[index];
            string pointer = $"/world_events/{index}";
            RequireText(context, diagnostics, value.EventId, pointer + "/event_id", id: true);
            RequireText(context, diagnostics, value.DisplayName, pointer + "/display_name");
            RequireNonNegativeVector(context, diagnostics, value.WorldCoord, pointer + "/world_coord");
            if (!EventTypes.Contains(value.EventType))
                Unknown(context, diagnostics, pointer + "/event_type", value.EventType);
            RequireText(context, diagnostics, value.TargetSubmapId, pointer + "/target_submap_id", id: true);
            RequireText(context, diagnostics, value.DiscoveryConditionId, pointer + "/discovery_condition_id", id: true);
            RequireText(context, diagnostics, value.PromptTitle, pointer + "/prompt_title");
            RequireText(context, diagnostics, value.PromptText, pointer + "/prompt_text");
        }
        return diagnostics;
    }

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateShared(
        JsonContentEntryContext context,
        WorldSharedImportModel import
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        RequireText(context, diagnostics, import.SharedContentId, "/shared_content_id", id: true);
        ValidateSettlements(context, diagnostics, import.SettlementLibrary, "/settlement_library");
        ValidateFacilities(context, diagnostics, import.FacilityLibrary, "/facility_library");
        ValidateWildSpawns(context, diagnostics, import.WildMonsterDistribution, "/wild_monster_distribution");
        DuplicateIds(
            context,
            diagnostics,
            import.SettlementNamePools.Select(value => value.PoolId),
            "/settlement_name_pools"
        );
        for (int index = 0; index < import.SettlementNamePools.Count; index++)
        {
            WorldSettlementNamePoolImportModel pool = import.SettlementNamePools[index];
            string pointer = $"/settlement_name_pools/{index}";
            if (!NamePoolIds.Contains(pool.PoolId))
                Unknown(context, diagnostics, pointer + "/pool_id", pool.PoolId);
            DuplicateIds(context, diagnostics, pool.DisplayNames, pointer + "/display_names");
            for (int nameIndex = 0; nameIndex < pool.DisplayNames.Count; nameIndex++)
                RequireText(context, diagnostics, pool.DisplayNames[nameIndex], $"{pointer}/display_names/{nameIndex}");
        }
        return diagnostics;
    }

    private static void ValidateSettlements(
        JsonContentEntryContext context,
        List<ContentJsonDiagnostic> diagnostics,
        IReadOnlyList<WorldSettlementImportModel> values,
        string basePointer
    )
    {
        DuplicateIds(context, diagnostics, values.Select(value => value.SettlementId), basePointer);
        for (int index = 0; index < values.Count; index++)
        {
            WorldSettlementImportModel value = values[index];
            string pointer = $"{basePointer}/{index}";
            RequireText(context, diagnostics, value.SettlementId, pointer + "/settlement_id", id: true);
            RequireText(context, diagnostics, value.DisplayName, pointer + "/display_name");
            if (value.Tier < 0 || value.Tier > 5)
                Range(context, diagnostics, pointer + "/tier");
            RequireNonNegative(context, diagnostics, value.MaxOptionalFacilities, pointer + "/max_optional_facilities");
            DuplicateIds(context, diagnostics, value.FacilitySlots.Select(slot => slot.SlotId), pointer + "/facility_slots");
            for (int slotIndex = 0; slotIndex < value.FacilitySlots.Count; slotIndex++)
            {
                WorldFacilitySlotImportModel slot = value.FacilitySlots[slotIndex];
                string slotPointer = $"{pointer}/facility_slots/{slotIndex}";
                RequireText(context, diagnostics, slot.SlotId, slotPointer + "/slot_id", id: true);
                RequireText(context, diagnostics, slot.SlotTag, slotPointer + "/slot_tag", id: true);
            }
            for (int itemIndex = 0; itemIndex < value.GuaranteedFacilityIds.Count; itemIndex++)
                RequireText(context, diagnostics, value.GuaranteedFacilityIds[itemIndex], $"{pointer}/guaranteed_facility_ids/{itemIndex}", id: true);
            for (int itemIndex = 0; itemIndex < value.OptionalFacilityPool.Count; itemIndex++)
            {
                WorldWeightedFacilityImportModel entry = value.OptionalFacilityPool[itemIndex];
                string itemPointer = $"{pointer}/optional_facility_pool/{itemIndex}";
                RequireText(context, diagnostics, entry.FacilityId, itemPointer + "/facility_id", id: true);
                if (entry.Weight <= 0)
                    Range(context, diagnostics, itemPointer + "/weight");
            }
        }
    }

    private static void ValidateFacilities(
        JsonContentEntryContext context,
        List<ContentJsonDiagnostic> diagnostics,
        IReadOnlyList<WorldFacilityImportModel> values,
        string basePointer
    )
    {
        DuplicateIds(context, diagnostics, values.Select(value => value.FacilityId), basePointer);
        for (int index = 0; index < values.Count; index++)
        {
            WorldFacilityImportModel value = values[index];
            string pointer = $"{basePointer}/{index}";
            RequireText(context, diagnostics, value.FacilityId, pointer + "/facility_id", id: true);
            RequireText(context, diagnostics, value.DisplayName, pointer + "/display_name");
            RequireText(context, diagnostics, value.Category, pointer + "/category", id: true);
            if (value.MinSettlementTier < 0 || value.MinSettlementTier > 5)
                Range(context, diagnostics, pointer + "/min_settlement_tier");
            RequireText(context, diagnostics, value.InteractionType, pointer + "/interaction_type", id: true);
            for (int tagIndex = 0; tagIndex < value.AllowedSlotTags.Count; tagIndex++)
                RequireText(context, diagnostics, value.AllowedSlotTags[tagIndex], $"{pointer}/allowed_slot_tags/{tagIndex}", id: true);
            DuplicateIds(context, diagnostics, value.BoundServiceNpcs.Select(npc => npc.NpcId), pointer + "/bound_service_npcs");
            for (int npcIndex = 0; npcIndex < value.BoundServiceNpcs.Count; npcIndex++)
            {
                WorldFacilityNpcImportModel npc = value.BoundServiceNpcs[npcIndex];
                string npcPointer = $"{pointer}/bound_service_npcs/{npcIndex}";
                RequireText(context, diagnostics, npc.NpcId, npcPointer + "/npc_id", id: true);
                RequireText(context, diagnostics, npc.DisplayName, npcPointer + "/display_name");
                RequireText(context, diagnostics, npc.ServiceType, npcPointer + "/service_type", id: true);
                RequireText(context, diagnostics, npc.InteractionScriptId, npcPointer + "/interaction_script_id", id: true);
                RequireText(context, diagnostics, npc.LocalSlotId, npcPointer + "/local_slot_id", id: true);
            }
        }
    }

    private static void ValidateWildSpawns(
        JsonContentEntryContext context,
        List<ContentJsonDiagnostic> diagnostics,
        IReadOnlyList<WorldWildSpawnImportModel> values,
        string basePointer
    )
    {
        for (int index = 0; index < values.Count; index++)
        {
            WorldWildSpawnImportModel value = values[index];
            string pointer = $"{basePointer}/{index}";
            RequireText(context, diagnostics, value.RegionTag, pointer + "/region_tag", id: true);
            RequireText(context, diagnostics, value.MonsterName, pointer + "/monster_name");
            RequireText(context, diagnostics, value.EncounterProfileId, pointer + "/encounter_profile_id", id: true);
            if (value.DensityPerChunk <= 0)
                Range(context, diagnostics, pointer + "/density_per_chunk");
            RequireNonNegative(context, diagnostics, value.MinDistanceToSettlement, pointer + "/min_distance_to_settlement");
            RequireNonNegative(context, diagnostics, value.VisionRange, pointer + "/vision_range");
            for (int coordIndex = 0; coordIndex < value.ChunkCoords.Count; coordIndex++)
                RequireNonNegativeVector(context, diagnostics, value.ChunkCoords[coordIndex], $"{pointer}/chunk_coords/{coordIndex}");
        }
    }

    private static void DuplicateIds(
        JsonContentEntryContext context,
        List<ContentJsonDiagnostic> diagnostics,
        IEnumerable<string> ids,
        string pointer
    )
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (id.Length > 0 && !seen.Add(id))
            {
                diagnostics.Add(
                    Diagnostic(
                        context,
                        WorldJsonRules.DuplicateNestedId,
                        $"Duplicate nested ID '{id}'.",
                        pointer
                    )
                );
            }
        }
    }

    private static void RequireText(
        JsonContentEntryContext context,
        List<ContentJsonDiagnostic> diagnostics,
        string value,
        string pointer,
        bool id = false
    )
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(
                Diagnostic(
                    context,
                    id ? WorldJsonRules.IdRequired : WorldJsonRules.TextRequired,
                    id ? "Stable ID is required." : "Non-blank text is required.",
                    pointer
                )
            );
        }
    }

    private static void RequirePositiveVector(
        JsonContentEntryContext context,
        List<ContentJsonDiagnostic> diagnostics,
        WorldVector2IImportModel value,
        string pointer
    )
    {
        if (value.X <= 0 || value.Y <= 0)
            Range(context, diagnostics, pointer);
    }

    private static void RequireNonNegativeVector(
        JsonContentEntryContext context,
        List<ContentJsonDiagnostic> diagnostics,
        WorldVector2IImportModel value,
        string pointer
    )
    {
        if (value.X < 0 || value.Y < 0)
            Range(context, diagnostics, pointer);
    }

    private static void RequireNonNegative(
        JsonContentEntryContext context,
        List<ContentJsonDiagnostic> diagnostics,
        int value,
        string pointer
    )
    {
        if (value < 0)
            Range(context, diagnostics, pointer);
    }

    private static void Range(
        JsonContentEntryContext context,
        List<ContentJsonDiagnostic> diagnostics,
        string pointer
    ) =>
        diagnostics.Add(
            Diagnostic(
                context,
                WorldJsonRules.ValueOutOfRange,
                "Value is outside the world content contract.",
                pointer
            )
        );

    private static void Unknown(
        JsonContentEntryContext context,
        List<ContentJsonDiagnostic> diagnostics,
        string pointer,
        string value
    ) =>
        diagnostics.Add(
            Diagnostic(
                context,
                WorldJsonRules.UnknownValue,
                $"Unknown closed value '{value}'.",
                pointer
            )
        );

    private static ContentJsonDiagnostic Diagnostic(
        JsonContentEntryContext context,
        string ruleId,
        string message,
        string relativePointer
    ) =>
        new(ruleId, message, context.SourceLabel, context.JsonPointer + relativePointer);

    private static string Trim(string? value) => (value ?? "").Trim();

    private sealed class PresetOfflineDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => WorldJsonDomains.PresetDomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<WorldPresetImportModel> batch =
                CreatePresetDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(DomainId, batch.Entries.Count, batch.Diagnostics);
        }
    }

    private sealed class GenerationOfflineDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => WorldJsonDomains.GenerationDomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<WorldGenerationImportModel> batch =
                CreateGenerationDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(DomainId, batch.Entries.Count, batch.Diagnostics);
        }
    }

    private sealed class SharedOfflineDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => WorldJsonDomains.SharedDomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<WorldSharedImportModel> batch =
                CreateSharedDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(DomainId, batch.Entries.Count, batch.Diagnostics);
        }
    }
}
