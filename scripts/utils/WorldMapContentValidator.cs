using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public class WorldMapContentValidator : IDisposable
{
    private const string DefaultMainWorldSettlementBundlePath =
        "res://data/configs/world_map/shared/main_world_default_settlement_bundle.tres";
    private const string DefaultMainWorldWildSpawnBundlePath =
        "res://data/configs/world_map/shared/main_world_default_wild_spawn_bundle.tres";
    private const string DefaultMainWorldSettlementNamePoolPath =
        "res://data/configs/world_map/shared/main_world_settlement_name_pool.tres";
    private const string DefaultMainWorldTownNamePoolPath =
        "res://data/configs/world_map/shared/main_world_town_name_pool.tres";
    private const string DefaultMainWorldCityNamePoolPath =
        "res://data/configs/world_map/shared/main_world_city_name_pool.tres";
    private const string DefaultMainWorldCapitalNamePoolPath =
        "res://data/configs/world_map/shared/main_world_capital_name_pool.tres";
    private const string DefaultMainWorldMetropolisNamePoolPath =
        "res://data/configs/world_map/shared/main_world_metropolis_name_pool.tres";
    private static readonly StringName WorldEventTypeEnterSubmap = new("enter_submap");
    private static readonly Dictionary<string, Resource> ResourceCache = new();

    public virtual void Dispose()
    {
    }

    public virtual Godot.Collections.Array<string> ValidateWorldPresets(
        GDictionary enemy_templates = null,
        GDictionary wild_encounter_rosters = null
    ) => ProjectErrors(
        ValidateWorldPresetsTyped(
            BuildStringNameSet(enemy_templates),
            BuildStringNameSet(wild_encounter_rosters)
        )
    );

    internal List<string> ValidateWorldPresetsTyped(
        IReadOnlyCollection<StringName> enemyTemplateIds = null,
        IReadOnlyCollection<StringName> wildEncounterRosterIds = null
    )
    {
        var errors = new List<string>();
        var seenPresetIds = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<WorldPresetRegistry.WorldPresetInfo> presets = WorldPresetRegistry.ListPresetsTyped();
        if (presets == null || presets.Count == 0)
        {
            errors.Add("World preset registry is empty.");
            return errors;
        }

        foreach (WorldPresetRegistry.WorldPresetInfo preset in presets)
        {
            string presetId = preset?.PresetId.ToString().Trim() ?? string.Empty;
            string displayName = preset?.DisplayName?.Trim() ?? string.Empty;
            string generationConfigPath = preset?.GenerationConfigPath?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(presetId))
            {
                errors.Add("World preset entry is missing preset_id.");
                continue;
            }
            if (seenPresetIds.Contains(presetId))
            {
                errors.Add($"Duplicate world preset_id registered: {presetId}.");
            }
            seenPresetIds.Add(presetId);
            if (string.IsNullOrEmpty(displayName))
            {
                errors.Add($"World preset {presetId} is missing display_name.");
            }
            if (string.IsNullOrEmpty(generationConfigPath))
            {
                errors.Add($"World preset {presetId} is missing generation_config_path.");
                continue;
            }

            var generationConfig = LoadCachedResource(generationConfigPath);
            if (generationConfig == null)
            {
                errors.Add(
                    $"World preset {presetId} failed to load generation config {generationConfigPath}."
                );
                continue;
            }
            if (generationConfig is not WorldMapGenerationConfig typedGenerationConfig)
            {
                errors.Add(
                    $"World preset {presetId} failed to load generation config {generationConfigPath}."
                );
                continue;
            }
            AddRange(
                errors,
                ValidateGenerationConfigTyped(
                    typedGenerationConfig,
                    generationConfigPath,
                    enemyTemplateIds,
                    wildEncounterRosterIds
                )
            );
        }
        return errors;
    }

    public virtual Godot.Collections.Array<string> ValidateGenerationConfig(
        WorldMapGenerationConfig generation_config,
        string label,
        GDictionary enemy_templates,
        GDictionary wild_encounter_rosters
    )
    {
        if (generation_config == null)
        {
            return new Godot.Collections.Array<string>
            {
                $"World generation config {label} must use WorldMapGenerationConfig.",
            };
        }
        return ProjectErrors(
            ValidateGenerationConfigTyped(
                generation_config,
                label,
                BuildStringNameSet(enemy_templates),
                BuildStringNameSet(wild_encounter_rosters)
            )
        );
    }

    internal List<string> ValidateGenerationConfigTyped(
        WorldMapGenerationConfig generation_config,
        string label,
        IReadOnlyCollection<StringName> enemyTemplateIds,
        IReadOnlyCollection<StringName> wildEncounterRosterIds
    )
    {
        return ValidateGenerationConfigInternal(
            generation_config,
            label,
            enemyTemplateIds,
            wildEncounterRosterIds,
            new HashSet<string>(StringComparer.Ordinal)
        );
    }

    private List<string> ValidateGenerationConfigInternal(
        WorldMapGenerationConfig config,
        string label,
        IReadOnlyCollection<StringName> enemyTemplateIds,
        IReadOnlyCollection<StringName> wildEncounterRosterIds,
        HashSet<string> visitedPaths
    )
    {
        var errors = new List<string>();
        var visitedKey = string.IsNullOrEmpty(config.ResourcePath) ? label : config.ResourcePath;
        visitedPaths.Add(visitedKey);

        var worldSizeInChunks = config.world_size_in_chunks;
        var chunkSize = config.chunk_size;
        if (worldSizeInChunks.X <= 0 || worldSizeInChunks.Y <= 0)
        {
            errors.Add(
                $"World generation config {label} has invalid world_size_in_chunks {worldSizeInChunks}."
            );
        }
        if (chunkSize.X <= 0 || chunkSize.Y <= 0)
        {
            errors.Add($"World generation config {label} has invalid chunk_size {chunkSize}.");
        }
        if (config.starting_wild_spawn_min_distance > config.starting_wild_spawn_max_distance)
        {
            errors.Add(
                $"World generation config {label} has starting_wild_spawn_min_distance greater than max distance."
            );
        }

        List<SettlementConfig> settlementResources = BuildEffectiveSettlementResources(
            config,
            label,
            errors
        );
        List<FacilityConfig> facilityResources = BuildEffectiveFacilityResources(
            config,
            label,
            errors
        );
        List<WildSpawnRule> wildSpawnRules = BuildEffectiveWildSpawnRules(config, label, errors);

        var facilityIds = ValidateFacilityLibrary(facilityResources, label, errors);
        var settlementIds = ValidateSettlementLibrary(
            settlementResources,
            facilityIds,
            label,
            errors
        );
        IReadOnlyList<SettlementDistributionRule> settlementDistribution = CollectTypedResources<
            SettlementDistributionRule
        >(
            config.settlement_distribution,
            $"World generation config {label} has non-SettlementDistributionRule entry.",
            errors
        );
        IReadOnlyList<MountedSubmapConfig> mountedSubmaps = CollectTypedResources<MountedSubmapConfig>(
            config.mounted_submaps,
            $"World generation config {label} has non-MountedSubmapConfig mounted_submaps entry.",
            errors
        );
        IReadOnlyList<WorldEventConfig> worldEvents = CollectTypedResources<WorldEventConfig>(
            config.world_events,
            $"World generation config {label} has non-WorldEventConfig world_events entry.",
            errors
        );
        ValidateSettlementDistribution(
            settlementDistribution,
            settlementIds,
            label,
            errors
        );
        ValidateWildSpawnRules(
            wildSpawnRules,
            config,
            enemyTemplateIds,
            wildEncounterRosterIds,
            label,
            errors
        );
        var mountedSubmapIds = ValidateMountedSubmaps(
            mountedSubmaps,
            label,
            enemyTemplateIds,
            wildEncounterRosterIds,
            visitedPaths,
            errors
        );
        var worldSizeCells = new Vector2I(
            worldSizeInChunks.X * chunkSize.X,
            worldSizeInChunks.Y * chunkSize.Y
        );
        ValidateWorldEvents(worldEvents, mountedSubmapIds, worldSizeCells, label, errors);
        return errors;
    }

    private static List<SettlementConfig> BuildEffectiveSettlementResources(
        WorldMapGenerationConfig generationConfig,
        string label,
        List<string> errors
    )
    {
        var resources = new List<SettlementConfig>();
        if (generationConfig.inject_default_main_world_content)
        {
            var bundle = LoadResource<WorldMapSettlementBundle>(
                DefaultMainWorldSettlementBundlePath,
                label,
                errors
            );
            if (bundle != null)
            {
                AppendTypedResources(
                    resources,
                    bundle.settlement_library,
                    $"World generation config {label} has non-SettlementConfig settlement entry.",
                    errors
                );
            }
            ValidateNamePool(DefaultMainWorldSettlementNamePoolPath, label, errors);
            ValidateNamePool(DefaultMainWorldTownNamePoolPath, label, errors);
            ValidateNamePool(DefaultMainWorldCityNamePoolPath, label, errors);
            ValidateNamePool(DefaultMainWorldCapitalNamePoolPath, label, errors);
            ValidateNamePool(DefaultMainWorldMetropolisNamePoolPath, label, errors);
        }
        AppendTypedResources(
            resources,
            generationConfig.settlement_library,
            $"World generation config {label} has non-SettlementConfig settlement entry.",
            errors
        );
        return resources;
    }

    private static List<FacilityConfig> BuildEffectiveFacilityResources(
        WorldMapGenerationConfig generationConfig,
        string label,
        List<string> errors
    )
    {
        var resources = new List<FacilityConfig>();
        if (generationConfig.inject_default_main_world_content)
        {
            var bundle = LoadResource<WorldMapSettlementBundle>(
                DefaultMainWorldSettlementBundlePath,
                label,
                errors
            );
            if (bundle != null)
            {
                AppendTypedResources(
                    resources,
                    bundle.facility_library,
                    $"World generation config {label} has non-FacilityConfig facility entry.",
                    errors
                );
            }
        }
        AppendTypedResources(
            resources,
            generationConfig.facility_library,
            $"World generation config {label} has non-FacilityConfig facility entry.",
            errors
        );
        return resources;
    }

    private static List<WildSpawnRule> BuildEffectiveWildSpawnRules(
        WorldMapGenerationConfig generationConfig,
        string label,
        List<string> errors
    )
    {
        var resources = new List<WildSpawnRule>();
        if (generationConfig.inject_default_main_world_content)
        {
            var bundle = LoadResource<WorldMapWildSpawnBundle>(
                DefaultMainWorldWildSpawnBundlePath,
                label,
                errors
            );
            if (bundle != null)
            {
                AppendTypedResources(
                    resources,
                    bundle.wild_monster_distribution,
                    $"World generation config {label} has non-WildSpawnRule entry.",
                    errors
                );
            }
        }
        AppendTypedResources(
            resources,
            generationConfig.wild_monster_distribution,
            $"World generation config {label} has non-WildSpawnRule entry.",
            errors
        );
        return resources;
    }

    private static HashSet<string> ValidateFacilityLibrary(
        IReadOnlyList<FacilityConfig> facilityResources,
        string label,
        List<string> errors
    )
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (FacilityConfig facility in facilityResources)
        {
            var facilityId = (facility.GetTemplateId() ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(facilityId))
            {
                errors.Add($"World generation config {label} has facility missing facility_id.");
                continue;
            }
            if (ids.Contains(facilityId))
            {
                errors.Add(
                    $"World generation config {label} has duplicate facility_id {facilityId}."
                );
            }
            ids.Add(facilityId);
            if (string.IsNullOrWhiteSpace(facility.display_name))
            {
                errors.Add($"World facility {facilityId} in {label} is missing display_name.");
            }
            if (
                string.IsNullOrWhiteSpace(facility.interaction_type)
                && facility.bound_service_npcs.Count == 0
            )
            {
                errors.Add(
                    $"World facility {facilityId} in {label} must declare interaction_type or bound service NPCs."
                );
            }
            ValidateFacilityNpcs(
                CollectTypedResources<FacilityNpcConfig>(
                    facility.bound_service_npcs,
                    $"World facility {facilityId} in {label} has non-FacilityNpcConfig service NPC.",
                    errors
                ),
                facilityId,
                label,
                errors
            );
        }
        return ids;
    }

    private static void ValidateFacilityNpcs(
        IReadOnlyList<FacilityNpcConfig> npcResources,
        string facilityId,
        string label,
        List<string> errors
    )
    {
        var npcIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (FacilityNpcConfig npc in npcResources)
        {
            var npcId = (npc.GetTemplateId() ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(npcId))
            {
                errors.Add($"World facility {facilityId} in {label} has NPC missing npc_id.");
                continue;
            }
            if (npcIds.Contains(npcId))
            {
                errors.Add($"World facility {facilityId} in {label} has duplicate npc_id {npcId}.");
            }
            npcIds.Add(npcId);
            if (string.IsNullOrWhiteSpace(npc.service_type))
            {
                errors.Add(
                    $"World facility {facilityId} NPC {npcId} in {label} is missing service_type."
                );
            }
            if (string.IsNullOrWhiteSpace(npc.interaction_script_id))
            {
                errors.Add(
                    $"World facility {facilityId} NPC {npcId} in {label} is missing interaction_script_id."
                );
            }
        }
    }

    private static HashSet<string> ValidateSettlementLibrary(
        IReadOnlyList<SettlementConfig> settlementResources,
        IReadOnlySet<string> facilityIds,
        string label,
        List<string> errors
    )
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (SettlementConfig settlement in settlementResources)
        {
            var settlementId = (settlement.GetTemplateId() ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(settlementId))
            {
                errors.Add(
                    $"World generation config {label} has settlement missing settlement_id."
                );
                continue;
            }
            if (ids.Contains(settlementId))
            {
                errors.Add(
                    $"World generation config {label} has duplicate settlement_id {settlementId}."
                );
            }
            ids.Add(settlementId);
            if (string.IsNullOrWhiteSpace(settlement.display_name))
            {
                errors.Add($"World settlement {settlementId} in {label} is missing display_name.");
            }
            ValidateFacilitySlots(
                CollectTypedResources<FacilitySlotConfig>(
                    settlement.facility_slots,
                    $"World settlement {settlementId} in {label} has non-FacilitySlotConfig slot.",
                    errors
                ),
                settlementId,
                label,
                errors
            );
            foreach (string facilityIdValue in settlement.guaranteed_facility_ids)
            {
                var facilityId = (facilityIdValue ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(facilityId))
                {
                    errors.Add(
                        $"World settlement {settlementId} in {label} has empty guaranteed facility id."
                    );
                }
                else if (!facilityIds.Contains(facilityId))
                {
                    errors.Add(
                        $"World settlement {settlementId} in {label} references missing guaranteed facility {facilityId}."
                    );
                }
            }
            ValidateOptionalFacilityPool(
                CollectTypedResources<WeightedFacilityEntry>(
                    settlement.optional_facility_pool,
                    $"World settlement {settlementId} in {label} has non-WeightedFacilityEntry optional facility.",
                    errors
                ),
                facilityIds,
                settlementId,
                label,
                errors
            );
        }
        return ids;
    }

    private static void ValidateFacilitySlots(
        IReadOnlyList<FacilitySlotConfig> slots,
        string settlementId,
        string label,
        List<string> errors
    )
    {
        var slotIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (FacilitySlotConfig slot in slots)
        {
            var slotId = (slot.slot_id ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(slotId))
            {
                errors.Add($"World settlement {settlementId} in {label} has slot missing slot_id.");
                continue;
            }
            if (slotIds.Contains(slotId))
            {
                errors.Add(
                    $"World settlement {settlementId} in {label} has duplicate slot_id {slotId}."
                );
            }
            slotIds.Add(slotId);
            if (string.IsNullOrWhiteSpace(slot.slot_tag))
            {
                errors.Add(
                    $"World settlement {settlementId} slot {slotId} in {label} is missing slot_tag."
                );
            }
        }
    }

    private static void ValidateOptionalFacilityPool(
        IReadOnlyList<WeightedFacilityEntry> pool,
        IReadOnlySet<string> facilityIds,
        string settlementId,
        string label,
        List<string> errors
    )
    {
        foreach (WeightedFacilityEntry entry in pool)
        {
            var facilityId = (entry.GetFacilityTemplateId() ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(facilityId))
            {
                errors.Add(
                    $"World settlement {settlementId} in {label} has optional facility missing facility_id."
                );
            }
            else if (!facilityIds.Contains(facilityId))
            {
                errors.Add(
                    $"World settlement {settlementId} in {label} references missing optional facility {facilityId}."
                );
            }
            if (entry.weight <= 0)
            {
                errors.Add(
                    $"World settlement {settlementId} in {label} has optional facility {facilityId} with non-positive weight."
                );
            }
        }
    }

    private static void ValidateSettlementDistribution(
        IReadOnlyList<SettlementDistributionRule> distribution,
        IReadOnlySet<string> settlementIds,
        string label,
        List<string> errors
    )
    {
        foreach (SettlementDistributionRule rule in distribution)
        {
            var settlementId = (rule.GetSettlementTemplateId() ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(settlementId))
            {
                errors.Add(
                    $"World generation config {label} has distribution rule missing settlement_id."
                );
            }
            else if (!settlementIds.Contains(settlementId))
            {
                errors.Add(
                    $"World generation config {label} settlement distribution references missing settlement {settlementId}."
                );
            }
            if (string.IsNullOrWhiteSpace(rule.faction_id))
            {
                errors.Add(
                    $"World generation config {label} settlement distribution for {settlementId} is missing faction_id."
                );
            }
        }
    }

    private static void ValidateWildSpawnRules(
        IReadOnlyList<WildSpawnRule> ruleResources,
        WorldMapGenerationConfig generationConfig,
        IReadOnlyCollection<StringName> enemyTemplateIds,
        IReadOnlyCollection<StringName> wildEncounterRosterIds,
        string label,
        List<string> errors
    )
    {
        var worldSizeInChunks = generationConfig.world_size_in_chunks;
        foreach (WildSpawnRule rule in ruleResources)
        {
            var regionTag = (rule.region_tag ?? string.Empty).Trim();
            var enemyRosterTemplateId = rule.enemy_roster_template_id.ToString().Trim();
            var encounterProfileId = rule.encounter_profile_id.ToString().Trim();
            if (string.IsNullOrEmpty(regionTag))
            {
                errors.Add(
                    $"World generation config {label} has wild spawn rule missing region_tag."
                );
            }
            if (
                string.IsNullOrEmpty(enemyRosterTemplateId)
                && string.IsNullOrEmpty(encounterProfileId)
            )
            {
                errors.Add(
                    $"World generation config {label} wild spawn rule {regionTag} must declare enemy_roster_template_id or encounter_profile_id."
                );
            }
            if (
                !string.IsNullOrEmpty(enemyRosterTemplateId)
                && HasEntries(enemyTemplateIds)
                && !ContainsStringName(enemyTemplateIds, enemyRosterTemplateId)
            )
            {
                errors.Add(
                    $"World generation config {label} wild spawn rule {regionTag} references missing enemy roster template {enemyRosterTemplateId}."
                );
            }
            if (
                !string.IsNullOrEmpty(encounterProfileId)
                && HasEntries(wildEncounterRosterIds)
                && !ContainsStringName(wildEncounterRosterIds, encounterProfileId)
            )
            {
                errors.Add(
                    $"World generation config {label} wild spawn rule {regionTag} references missing encounter profile {encounterProfileId}."
                );
            }
            if (rule.density_per_chunk <= 0)
            {
                errors.Add(
                    $"World generation config {label} wild spawn rule {regionTag} has non-positive density_per_chunk."
                );
            }
            if (rule.vision_range < 0)
            {
                errors.Add(
                    $"World generation config {label} wild spawn rule {regionTag} has negative vision_range."
                );
            }
            if (rule.min_distance_to_settlement < 0)
            {
                errors.Add(
                    $"World generation config {label} wild spawn rule {regionTag} has negative min_distance_to_settlement."
                );
            }
            if (rule.chunk_coords.Count == 0)
            {
                errors.Add(
                    $"World generation config {label} wild spawn rule {regionTag} has empty chunk_coords."
                );
            }
            else
            {
                foreach (Vector2I coord in rule.chunk_coords)
                {
                    if (
                        coord.X < 0
                        || coord.Y < 0
                        || coord.X >= worldSizeInChunks.X
                        || coord.Y >= worldSizeInChunks.Y
                    )
                    {
                        errors.Add(
                            $"World generation config {label} wild spawn rule {regionTag} has chunk_coord {coord} outside world chunk range {worldSizeInChunks}."
                        );
                    }
                }
            }
        }
    }

    private HashSet<StringName> ValidateMountedSubmaps(
        IReadOnlyList<MountedSubmapConfig> submaps,
        string label,
        IReadOnlyCollection<StringName> enemyTemplateIds,
        IReadOnlyCollection<StringName> wildEncounterRosterIds,
        HashSet<string> visitedPaths,
        List<string> errors
    )
    {
        var ids = new HashSet<StringName>();
        foreach (MountedSubmapConfig submap in submaps)
        {
            var submapId = submap.submap_id.ToString().Trim();
            if (string.IsNullOrEmpty(submapId))
            {
                errors.Add(
                    $"World generation config {label} has mounted submap missing submap_id."
                );
                continue;
            }
            StringName submapIdName = new(submapId);
            if (ids.Contains(submapIdName))
            {
                errors.Add(
                    $"World generation config {label} has duplicate mounted submap_id {submapId}."
                );
            }
            ids.Add(submapIdName);
            var path = (submap.generation_config_path ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path))
            {
                errors.Add(
                    $"World mounted submap {submapId} in {label} is missing generation_config_path."
                );
                continue;
            }
            if (visitedPaths.Contains(path))
            {
                errors.Add(
                    $"World mounted submap {submapId} in {label} creates recursive generation_config_path {path}."
                );
                continue;
            }
            var submapConfig = LoadCachedResource(path);
            if (submapConfig == null)
            {
                errors.Add(
                    $"World mounted submap {submapId} in {label} failed to load generation_config_path {path}."
                );
                continue;
            }
            if (submapConfig is not WorldMapGenerationConfig)
            {
                errors.Add(
                    $"World mounted submap {submapId} in {label} expected {path} to use WorldMapGenerationConfig."
                );
                continue;
            }
            AddRange(
                errors,
                ValidateGenerationConfigInternal(
                    (WorldMapGenerationConfig)submapConfig,
                    path,
                    enemyTemplateIds,
                    wildEncounterRosterIds,
                    new HashSet<string>(visitedPaths, StringComparer.Ordinal)
                )
            );
        }
        return ids;
    }

    private static void ValidateWorldEvents(
        IReadOnlyList<WorldEventConfig> events,
        IReadOnlySet<StringName> mountedSubmapIds,
        Vector2I worldSizeCells,
        string label,
        List<string> errors
    )
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorldEventConfig worldEvent in events)
        {
            var eventId = worldEvent.event_id.ToString().Trim();
            if (string.IsNullOrEmpty(eventId))
            {
                errors.Add($"World generation config {label} has world event missing event_id.");
                continue;
            }
            if (ids.Contains(eventId))
            {
                errors.Add(
                    $"World generation config {label} has duplicate world event_id {eventId}."
                );
            }
            ids.Add(eventId);
            var coord = worldEvent.world_coord;
            if (
                coord.X < 0
                || coord.Y < 0
                || coord.X >= worldSizeCells.X
                || coord.Y >= worldSizeCells.Y
            )
            {
                errors.Add(
                    $"World event {eventId} in {label} has world_coord {coord} outside world cell range {worldSizeCells}."
                );
            }
            if (worldEvent.event_type == WorldEventTypeEnterSubmap)
            {
                var target = worldEvent.target_submap_id.ToString().Trim();
                if (string.IsNullOrEmpty(target))
                {
                    errors.Add(
                        $"World event {eventId} in {label} with event_type enter_submap is missing target_submap_id."
                    );
                }
                else if (
                    mountedSubmapIds != null
                    && !mountedSubmapIds.Contains(new StringName(target))
                )
                {
                    errors.Add(
                        $"World event {eventId} in {label} references missing target_submap_id {target}."
                    );
                }
            }
        }
    }

    private static void ValidateNamePool(
        string resourcePath,
        string label,
        List<string> errors
    )
    {
        var namePool = LoadResource<WorldMapSettlementNamePool>(resourcePath, label, errors);
        if (namePool == null)
        {
            return;
        }
        var names = namePool.BuildUniqueDisplayNames();
        if (names.Count == 0)
        {
            errors.Add(
                $"World generation config {label} has empty settlement name pool {resourcePath}."
            );
        }
    }

    private static T LoadResource<T>(
        string resourcePath,
        string label,
        List<string> errors
    )
        where T : Resource
    {
        var resource = LoadCachedResource(resourcePath);
        if (resource == null)
        {
            errors.Add($"World generation config {label} failed to load {resourcePath}.");
            return null;
        }
        if (resource is not T typedResource)
        {
            errors.Add(
                $"World generation config {label} expected {resourcePath} to use {typeof(T).Name}."
            );
            return null;
        }
        return typedResource;
    }

    private static List<T> CollectTypedResources<T>(
        Godot.Collections.Array<Resource> source,
        string invalidEntryError,
        List<string> errors
    )
        where T : Resource
    {
        var result = new List<T>();
        AppendTypedResources(result, source, invalidEntryError, errors);
        return result;
    }

    private static void AppendTypedResources<T>(
        List<T> target,
        Godot.Collections.Array<Resource> source,
        string invalidEntryError,
        List<string> errors
    )
        where T : Resource
    {
        if (source == null)
        {
            return;
        }
        foreach (Resource item in source)
        {
            if (item is T typedItem)
            {
                target.Add(typedItem);
                continue;
            }
            errors.Add(invalidEntryError);
        }
    }

    private static Godot.Collections.Array<string> ProjectErrors(IEnumerable<string> errors)
    {
        var result = new Godot.Collections.Array<string>();
        if (errors == null)
        {
            return result;
        }
        foreach (string error in errors)
        {
            result.Add(error ?? string.Empty);
        }
        return result;
    }

    private static bool HasEntries<T>(IReadOnlyCollection<T> values)
    {
        return values != null && values.Count > 0;
    }

    private static Resource LoadCachedResource(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            return null;
        }
        if (
            ResourceCache.TryGetValue(resourcePath, out var cachedResource)
            && GodotObject.IsInstanceValid(cachedResource)
        )
        {
            return cachedResource;
        }
        if (!ResourceLoader.Exists(resourcePath))
        {
            return null;
        }
        var resource = ResourceLoader.Load<Resource>(resourcePath);
        if (resource != null)
        {
            GodotContentOwnership.RegisterBorrowedContent(resource, resourcePath);
            ResourceCache[resourcePath] = resource;
        }
        return resource;
    }

    internal static void SuppressCachedResourceFinalizersForShutdown()
    {
        foreach (Resource resource in ResourceCache.Values)
        {
            GodotObjectOwnershipRegistry.AssertBorrowedOrOwnedKnown(
                resource,
                "WorldMapContentValidator.SuppressCachedResourceFinalizersForShutdown"
            );
        }
        GodotContentOwnership.SuppressBorrowedContentForProcessExit();
    }

    private static bool ContainsStringName(
        IReadOnlyCollection<StringName> values,
        string key
    )
    {
        if (values == null || string.IsNullOrEmpty(key))
        {
            return false;
        }
        StringName target = new(key);
        foreach (StringName value in values)
        {
            if (value == target)
            {
                return true;
            }
        }
        return false;
    }

    private static HashSet<StringName> BuildStringNameSet(GDictionary dictionary)
    {
        var result = new HashSet<StringName>();
        if (dictionary == null)
        {
            return result;
        }
        foreach (Variant rawKey in dictionary.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
            {
                continue;
            }
            StringName keyName = rawKey.AsStringName();
            if (keyName != "")
            {
                result.Add(keyName);
            }
        }
        return result;
    }

    private static void AddRange(List<string> target, IEnumerable<string> source)
    {
        if (source == null)
        {
            return;
        }
        foreach (string item in source)
        {
            target.Add(item);
        }
    }
}
