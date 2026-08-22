#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Godot;

internal sealed class WorldContentRegistry
{
    private readonly IContentJsonSourceReader _sourceReader;
    private readonly Dictionary<StringName, WorldPresetDefinition> _presets = new();
    private readonly Dictionary<string, WorldGenerationImportModel> _generationImports =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorldSharedImportModel> _sharedImports =
        new(StringComparer.Ordinal);
    private readonly Dictionary<StringName, WorldGenerationDefinition> _generations = new();
    private readonly List<string> _validationErrors = new();

    internal WorldContentRegistry()
        : this(new GodotContentJsonSourceReader()) { }

    internal WorldContentRegistry(IContentJsonSourceReader sourceReader)
    {
        _sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
    }

    internal void Rebuild(
        string presetDirectory = WorldJsonDomains.PresetDirectoryPath,
        string generationDirectory = WorldJsonDomains.GenerationDirectoryPath,
        string sharedDirectory = WorldJsonDomains.SharedDirectoryPath
    )
    {
        _presets.Clear();
        _generationImports.Clear();
        _sharedImports.Clear();
        _generations.Clear();
        _validationErrors.Clear();

        ContentImportBatch<WorldSharedImportModel> sharedBatch = WorldJsonImport
            .CreateSharedDescriptor(sharedDirectory, _sourceReader)
            .Import();
        AppendDiagnostics(sharedBatch.Diagnostics);
        foreach (ContentImportEntry<WorldSharedImportModel> entry in sharedBatch.Entries)
        {
            if (!_sharedImports.TryAdd(entry.Import.SharedContentId, entry.Import))
                _validationErrors.Add($"WorldContentRegistry: duplicate shared_content_id '{entry.Import.SharedContentId}'.");
        }

        ContentImportBatch<WorldGenerationImportModel> generationBatch = WorldJsonImport
            .CreateGenerationDescriptor(generationDirectory, _sourceReader)
            .Import();
        AppendDiagnostics(generationBatch.Diagnostics);
        foreach (ContentImportEntry<WorldGenerationImportModel> entry in generationBatch.Entries)
        {
            if (!_generationImports.TryAdd(entry.Import.GenerationId, entry.Import))
                _validationErrors.Add($"WorldContentRegistry: duplicate generation_id '{entry.Import.GenerationId}'.");
        }

        foreach (string generationId in _generationImports.Keys.OrderBy(value => value, StringComparer.Ordinal))
        {
            try
            {
                ProjectGeneration(generationId, new List<string>(), new HashSet<string>(StringComparer.Ordinal));
            }
            catch (InvalidDataException exception)
            {
                _validationErrors.Add($"WorldContentRegistry: {exception.Message}");
            }
        }

        ContentImportBatch<WorldPresetImportModel> presetBatch = WorldJsonImport
            .CreatePresetDescriptor(presetDirectory, _sourceReader)
            .Import();
        AppendDiagnostics(presetBatch.Diagnostics);
        foreach (ContentImportEntry<WorldPresetImportModel> entry in presetBatch.Entries)
        {
            WorldPresetImportModel import = entry.Import;
            if (!_generations.ContainsKey(new StringName(import.GenerationId)))
            {
                _validationErrors.Add(
                    $"WorldContentRegistry: preset '{import.PresetId}' references missing generation_id '{import.GenerationId}'."
                );
                continue;
            }
            var definition = new WorldPresetDefinition(
                new StringName(import.PresetId),
                import.DisplayName,
                import.SizeLabel,
                new StringName(import.GenerationId)
            );
            if (!_presets.TryAdd(definition.PresetId, definition))
                _validationErrors.Add($"WorldContentRegistry: duplicate preset_id '{definition.PresetId}'.");
        }
    }

    internal IReadOnlyDictionary<StringName, WorldPresetDefinition> GetPresets() =>
        new ReadOnlyDictionary<StringName, WorldPresetDefinition>(
            new Dictionary<StringName, WorldPresetDefinition>(_presets)
        );

    internal IReadOnlyDictionary<StringName, WorldGenerationDefinition> GetGenerations() =>
        new ReadOnlyDictionary<StringName, WorldGenerationDefinition>(
            new Dictionary<StringName, WorldGenerationDefinition>(_generations)
        );

    internal IReadOnlyList<string> GetValidationErrors() => _validationErrors;

    private WorldGenerationDefinition ProjectGeneration(
        string generationId,
        List<string> path,
        HashSet<string> stack
    )
    {
        StringName generationKey = new(generationId);
        if (_generations.TryGetValue(generationKey, out WorldGenerationDefinition? existing))
            return existing;
        if (!_generationImports.TryGetValue(generationId, out WorldGenerationImportModel? import))
            throw new InvalidDataException($"missing generation_id '{generationId}'.");
        if (!stack.Add(generationId))
            throw new InvalidDataException($"generation cycle: {string.Join(" -> ", path.Append(generationId))}.");
        path.Add(generationId);
        try
        {
            WorldSharedImportModel? shared = null;
            if (import.SharedContentId.Length > 0 && !_sharedImports.TryGetValue(import.SharedContentId, out shared))
            {
                throw new InvalidDataException(
                    $"generation '{generationId}' references missing shared_content_id '{import.SharedContentId}'."
                );
            }

            var mountedSubmaps = new List<MountedSubmapDefinition>(import.MountedSubmaps.Count);
            foreach (WorldMountedSubmapImportModel mounted in import.MountedSubmaps)
            {
                WorldGenerationDefinition child = ProjectGeneration(mounted.GenerationId, path, stack);
                mountedSubmaps.Add(
                    new MountedSubmapDefinition(
                        new StringName(mounted.SubmapId),
                        mounted.DisplayName,
                        new StringName(mounted.GenerationId),
                        mounted.ReturnHintText,
                        child
                    )
                );
            }

            WorldMapSettlementBundleDefinition? settlementBundle = shared == null
                ? null
                : new WorldMapSettlementBundleDefinition(
                    shared.SettlementLibrary.Select(ProjectSettlement).ToArray(),
                    shared.FacilityLibrary.Select(ProjectFacility).ToArray()
                );
            WorldMapWildSpawnBundleDefinition? wildSpawnBundle = shared == null
                ? null
                : new WorldMapWildSpawnBundleDefinition(
                    shared.WildMonsterDistribution.Select(ProjectWildSpawn).ToArray()
                );
            IReadOnlyDictionary<StringName, WorldMapSettlementNamePoolDefinition> namePools =
                shared == null
                    ? new ReadOnlyDictionary<StringName, WorldMapSettlementNamePoolDefinition>(
                        new Dictionary<StringName, WorldMapSettlementNamePoolDefinition>()
                    )
                    : new ReadOnlyDictionary<StringName, WorldMapSettlementNamePoolDefinition>(
                        shared.SettlementNamePools.ToDictionary(
                            pool => new StringName(pool.PoolId),
                            pool => new WorldMapSettlementNamePoolDefinition(pool.DisplayNames)
                        )
                    );

            var definition = new WorldGenerationDefinition(
                generationKey,
                import.Seed,
                ProjectVector(import.WorldSizeInChunks),
                ProjectVector(import.ChunkSize),
                ProjectVector(import.PlayerStartCoord),
                import.PlayerVisionRange,
                import.ProceduralGenerationEnabled,
                import.ProceduralWildSpawnChunkChanceDenominator,
                new StringName(import.SharedContentId),
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
                import.GuaranteeStartingWildEncounter,
                import.StartingWildSpawnMinDistance,
                import.StartingWildSpawnMaxDistance,
                import.SettlementLibrary.Select(ProjectSettlement).ToArray(),
                import.FacilityLibrary.Select(ProjectFacility).ToArray(),
                import.SettlementDistribution.Select(ProjectSettlementDistribution).ToArray(),
                import.WildMonsterDistribution.Select(ProjectWildSpawn).ToArray(),
                mountedSubmaps,
                import.WorldEvents.Select(ProjectWorldEvent).ToArray(),
                settlementBundle,
                wildSpawnBundle,
                namePools
            );
            _generations.Add(generationKey, definition);
            return definition;
        }
        finally
        {
            path.RemoveAt(path.Count - 1);
            stack.Remove(generationId);
        }
    }

    private static SettlementDefinition ProjectSettlement(WorldSettlementImportModel import) =>
        new(
            import.SettlementId,
            import.DisplayName,
            import.Tier,
            import.FacilitySlots
                .Select(slot => new FacilitySlotDefinition(
                    slot.SlotId,
                    ProjectVector(slot.LocalCoord),
                    slot.SlotTag,
                    slot.Required
                ))
                .ToArray(),
            import.GuaranteedFacilityIds,
            import.OptionalFacilityPool
                .Select(entry => new WeightedFacilityDefinition(entry.FacilityId, entry.Weight))
                .ToArray(),
            import.MaxOptionalFacilities
        );

    private static FacilityDefinition ProjectFacility(WorldFacilityImportModel import) =>
        new(
            import.FacilityId,
            import.DisplayName,
            import.Category,
            import.MinSettlementTier,
            import.AllowedSlotTags,
            import.BoundServiceNpcs
                .Select(npc => new FacilityNpcDefinition(
                    npc.NpcId,
                    npc.DisplayName,
                    npc.ServiceType,
                    npc.InteractionScriptId,
                    npc.LocalSlotId
                ))
                .ToArray(),
            import.InteractionType
        );

    private static SettlementDistributionDefinition ProjectSettlementDistribution(
        WorldSettlementDistributionImportModel import
    ) =>
        new(import.SettlementId, ProjectVector(import.PreferredOrigin), import.FactionId, import.CountryId);

    private static WildSpawnRuleDefinition ProjectWildSpawn(WorldWildSpawnImportModel import) =>
        new(
            import.RegionTag,
            import.MonsterName,
            new StringName(import.EncounterProfileId),
            new StringName(import.SettlementEncounterProfileId),
            import.SettlementEncounterDisplayName,
            import.DensityPerChunk,
            import.MinDistanceToSettlement,
            import.VisionRange,
            import.ChunkCoords.Select(ProjectVector).ToArray()
        );

    private static WorldEventDefinition ProjectWorldEvent(WorldEventImportModel import) =>
        new(
            new StringName(import.EventId),
            import.DisplayName,
            ProjectVector(import.WorldCoord),
            new StringName(import.EventType),
            new StringName(import.TargetSubmapId),
            new StringName(import.DiscoveryConditionId),
            import.PromptTitle,
            import.PromptText
        );

    private static Vector2I ProjectVector(WorldVector2IImportModel value) =>
        new(value.X, value.Y);

    private void AppendDiagnostics(IReadOnlyList<ContentJsonDiagnostic> diagnostics)
    {
        foreach (ContentJsonDiagnostic diagnostic in diagnostics)
        {
            _validationErrors.Add(
                $"[{diagnostic.RuleId}] {diagnostic.SourceLabel}{diagnostic.JsonPointer}: {diagnostic.Message}"
            );
        }
    }
}
