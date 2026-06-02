using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class WorldMapContentValidator : RefCounted
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

    public Godot.Collections.Array<string> validate_world_presets(
        GDictionary enemy_templates = null,
        GDictionary wild_encounter_rosters = null
    )
    {
        var errors = new Godot.Collections.Array<string>();
        var seenPresetIds = new HashSet<string>();
        var presets = GetWorldPresets();
        if (presets.Count == 0)
        {
            errors.Add("World preset registry is empty.");
            return errors;
        }

        foreach (var presetValue in presets)
        {
            if (presetValue.VariantType != Variant.Type.Dictionary)
            {
                errors.Add("World preset entry must be a Dictionary.");
                continue;
            }

            var preset = presetValue.AsGodotDictionary();
            var presetId = GetDictionaryString(preset, "preset_id").Trim();
            var displayName = GetDictionaryString(preset, "display_name").Trim();
            var generationConfigPath = GetDictionaryString(preset, "generation_config_path").Trim();
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
            AddRange(
                errors,
                validate_generation_config(
                    generationConfig,
                    generationConfigPath,
                    enemy_templates,
                    wild_encounter_rosters
                )
            );
        }
        return errors;
    }

    public Godot.Collections.Array<string> validate_generation_config(
        GodotObject generation_config,
        string label,
        GDictionary enemy_templates,
        GDictionary wild_encounter_rosters
    )
    {
        return ValidateGenerationConfigInternal(
            generation_config,
            label,
            enemy_templates,
            wild_encounter_rosters,
            new GDictionary()
        );
    }

    private Godot.Collections.Array<string> ValidateGenerationConfigInternal(
        GodotObject generation_config,
        string label,
        GDictionary enemy_templates,
        GDictionary wild_encounter_rosters,
        GDictionary visited_paths
    )
    {
        var errors = new Godot.Collections.Array<string>();
        if (generation_config is not WorldMapGenerationConfig config)
        {
            errors.Add($"World generation config {label} must use WorldMapGenerationConfig.");
            return errors;
        }

        var visitedKey = string.IsNullOrEmpty(config.ResourcePath) ? label : config.ResourcePath;
        visited_paths[visitedKey] = true;

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

        var settlementResources = BuildEffectiveSettlementResources(config, label, errors);
        var facilityResources = BuildEffectiveFacilityResources(config, label, errors);
        var wildSpawnRules = BuildEffectiveWildSpawnRules(config, label, errors);

        var facilityIds = ValidateFacilityLibrary(facilityResources, label, errors);
        var settlementIds = ValidateSettlementLibrary(
            settlementResources,
            facilityIds,
            label,
            errors
        );
        ValidateSettlementDistribution(
            config.settlement_distribution,
            settlementIds,
            label,
            errors
        );
        ValidateWildSpawnRules(
            wildSpawnRules,
            config,
            enemy_templates,
            wild_encounter_rosters,
            label,
            errors
        );
        var mountedSubmapIds = ValidateMountedSubmaps(
            config.mounted_submaps,
            label,
            enemy_templates,
            wild_encounter_rosters,
            visited_paths,
            errors
        );
        var worldSizeCells = new Vector2I(
            worldSizeInChunks.X * chunkSize.X,
            worldSizeInChunks.Y * chunkSize.Y
        );
        ValidateWorldEvents(config.world_events, mountedSubmapIds, worldSizeCells, label, errors);
        return errors;
    }

    private static GArray GetWorldPresets()
    {
        var presets = new GArray();
        foreach (WorldPresetRegistry.WorldPresetInfo preset in WorldPresetRegistry.ListPresetsTyped())
        {
            presets.Add(preset.ToDictionary());
        }
        return presets;
    }

    private static GArray BuildEffectiveSettlementResources(
        WorldMapGenerationConfig generationConfig,
        string label,
        Godot.Collections.Array<string> errors
    )
    {
        var resources = new GArray();
        if (generationConfig.inject_default_main_world_content)
        {
            var bundle = LoadResource<WorldMapSettlementBundle>(
                DefaultMainWorldSettlementBundlePath,
                label,
                errors
            );
            if (bundle != null)
            {
                AppendResources(resources, bundle.settlement_library);
            }
            ValidateNamePool(DefaultMainWorldSettlementNamePoolPath, label, errors);
            ValidateNamePool(DefaultMainWorldTownNamePoolPath, label, errors);
            ValidateNamePool(DefaultMainWorldCityNamePoolPath, label, errors);
            ValidateNamePool(DefaultMainWorldCapitalNamePoolPath, label, errors);
            ValidateNamePool(DefaultMainWorldMetropolisNamePoolPath, label, errors);
        }
        AppendResources(resources, generationConfig.settlement_library);
        return resources;
    }

    private static GArray BuildEffectiveFacilityResources(
        WorldMapGenerationConfig generationConfig,
        string label,
        Godot.Collections.Array<string> errors
    )
    {
        var resources = new GArray();
        if (generationConfig.inject_default_main_world_content)
        {
            var bundle = LoadResource<WorldMapSettlementBundle>(
                DefaultMainWorldSettlementBundlePath,
                label,
                errors
            );
            if (bundle != null)
            {
                AppendResources(resources, bundle.facility_library);
            }
        }
        AppendResources(resources, generationConfig.facility_library);
        return resources;
    }

    private static GArray BuildEffectiveWildSpawnRules(
        WorldMapGenerationConfig generationConfig,
        string label,
        Godot.Collections.Array<string> errors
    )
    {
        var resources = new GArray();
        if (generationConfig.inject_default_main_world_content)
        {
            var bundle = LoadResource<WorldMapWildSpawnBundle>(
                DefaultMainWorldWildSpawnBundlePath,
                label,
                errors
            );
            if (bundle != null)
            {
                AppendResources(resources, bundle.wild_monster_distribution);
            }
        }
        AppendResources(resources, generationConfig.wild_monster_distribution);
        return resources;
    }

    private static HashSet<string> ValidateFacilityLibrary(
        GArray facilityResources,
        string label,
        Godot.Collections.Array<string> errors
    )
    {
        var ids = new HashSet<string>();
        foreach (var facilityValue in facilityResources)
        {
            if (facilityValue.AsGodotObject() is not FacilityConfig facility)
            {
                errors.Add(
                    $"World generation config {label} has non-FacilityConfig facility entry."
                );
                continue;
            }
            var facilityId = (facility.get_template_id() ?? string.Empty).Trim();
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
            ValidateFacilityNpcs(facility.bound_service_npcs, facilityId, label, errors);
        }
        return ids;
    }

    private static void ValidateFacilityNpcs(
        Godot.Collections.Array<Resource> npcResources,
        string facilityId,
        string label,
        Godot.Collections.Array<string> errors
    )
    {
        var npcIds = new HashSet<string>();
        foreach (var npcResource in npcResources)
        {
            if (npcResource is not FacilityNpcConfig npc)
            {
                errors.Add(
                    $"World facility {facilityId} in {label} has non-FacilityNpcConfig service NPC."
                );
                continue;
            }
            var npcId = (npc.get_template_id() ?? string.Empty).Trim();
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
        GArray settlementResources,
        HashSet<string> facilityIds,
        string label,
        Godot.Collections.Array<string> errors
    )
    {
        var ids = new HashSet<string>();
        foreach (var settlementValue in settlementResources)
        {
            if (settlementValue.AsGodotObject() is not SettlementConfig settlement)
            {
                errors.Add(
                    $"World generation config {label} has non-SettlementConfig settlement entry."
                );
                continue;
            }
            var settlementId = (settlement.get_template_id() ?? string.Empty).Trim();
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
            ValidateFacilitySlots(settlement.facility_slots, settlementId, label, errors);
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
                settlement.optional_facility_pool,
                facilityIds,
                settlementId,
                label,
                errors
            );
        }
        return ids;
    }

    private static void ValidateFacilitySlots(
        Godot.Collections.Array<Resource> slots,
        string settlementId,
        string label,
        Godot.Collections.Array<string> errors
    )
    {
        var slotIds = new HashSet<string>();
        foreach (var slotResource in slots)
        {
            if (slotResource is not FacilitySlotConfig slot)
            {
                errors.Add(
                    $"World settlement {settlementId} in {label} has non-FacilitySlotConfig slot."
                );
                continue;
            }
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
        Godot.Collections.Array<Resource> pool,
        HashSet<string> facilityIds,
        string settlementId,
        string label,
        Godot.Collections.Array<string> errors
    )
    {
        foreach (var entryResource in pool)
        {
            if (entryResource is not WeightedFacilityEntry entry)
            {
                errors.Add(
                    $"World settlement {settlementId} in {label} has non-WeightedFacilityEntry optional facility."
                );
                continue;
            }
            var facilityId = (entry.get_facility_template_id() ?? string.Empty).Trim();
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
        Godot.Collections.Array<Resource> distribution,
        HashSet<string> settlementIds,
        string label,
        Godot.Collections.Array<string> errors
    )
    {
        foreach (var ruleResource in distribution)
        {
            if (ruleResource is not SettlementDistributionRule rule)
            {
                errors.Add(
                    $"World generation config {label} has non-SettlementDistributionRule entry."
                );
                continue;
            }
            var settlementId = (rule.get_settlement_template_id() ?? string.Empty).Trim();
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
        GArray ruleResources,
        WorldMapGenerationConfig generationConfig,
        GDictionary enemyTemplates,
        GDictionary wildEncounterRosters,
        string label,
        Godot.Collections.Array<string> errors
    )
    {
        var worldSizeInChunks = generationConfig.world_size_in_chunks;
        foreach (var ruleValue in ruleResources)
        {
            if (ruleValue.AsGodotObject() is not WildSpawnRule rule)
            {
                errors.Add($"World generation config {label} has non-WildSpawnRule entry.");
                continue;
            }
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
                && HasEntries(enemyTemplates)
                && !HasStringNameKey(enemyTemplates, enemyRosterTemplateId)
            )
            {
                errors.Add(
                    $"World generation config {label} wild spawn rule {regionTag} references missing enemy roster template {enemyRosterTemplateId}."
                );
            }
            if (
                !string.IsNullOrEmpty(encounterProfileId)
                && HasEntries(wildEncounterRosters)
                && !HasStringNameKey(wildEncounterRosters, encounterProfileId)
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

    private Godot.Collections.Dictionary ValidateMountedSubmaps(
        Godot.Collections.Array<Resource> submaps,
        string label,
        GDictionary enemyTemplates,
        GDictionary wildEncounterRosters,
        GDictionary visitedPaths,
        Godot.Collections.Array<string> errors
    )
    {
        var ids = new GDictionary();
        foreach (var submapResource in submaps)
        {
            if (submapResource is not MountedSubmapConfig submap)
            {
                errors.Add(
                    $"World generation config {label} has non-MountedSubmapConfig mounted_submaps entry."
                );
                continue;
            }
            var submapId = submap.submap_id.ToString().Trim();
            if (string.IsNullOrEmpty(submapId))
            {
                errors.Add(
                    $"World generation config {label} has mounted submap missing submap_id."
                );
                continue;
            }
            if (ids.ContainsKey(submapId))
            {
                errors.Add(
                    $"World generation config {label} has duplicate mounted submap_id {submapId}."
                );
            }
            ids[submapId] = true;
            var path = (submap.generation_config_path ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(path))
            {
                errors.Add(
                    $"World mounted submap {submapId} in {label} is missing generation_config_path."
                );
                continue;
            }
            if (visitedPaths.ContainsKey(path))
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
                    submapConfig,
                    path,
                    enemyTemplates,
                    wildEncounterRosters,
                    visitedPaths
                )
            );
        }
        return ids;
    }

    private static void ValidateWorldEvents(
        Godot.Collections.Array<Resource> events,
        GDictionary mountedSubmapIds,
        Vector2I worldSizeCells,
        string label,
        Godot.Collections.Array<string> errors
    )
    {
        var ids = new HashSet<string>();
        foreach (var eventResource in events)
        {
            if (eventResource is not WorldEventConfig worldEvent)
            {
                errors.Add(
                    $"World generation config {label} has non-WorldEventConfig world_events entry."
                );
                continue;
            }
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
                else if (!mountedSubmapIds.ContainsKey(target))
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
        Godot.Collections.Array<string> errors
    )
    {
        var namePool = LoadResource<WorldMapSettlementNamePool>(resourcePath, label, errors);
        if (namePool == null)
        {
            return;
        }
        var names = namePool.build_unique_display_names();
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
        Godot.Collections.Array<string> errors
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

    private static void AppendResources(GArray target, Godot.Collections.Array<Resource> source)
    {
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private static bool HasEntries(GDictionary dictionary)
    {
        return dictionary != null && dictionary.Count > 0;
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
            ResourceCache[resourcePath] = resource;
        }
        return resource;
    }

    private static bool HasStringNameKey(GDictionary dictionary, string key)
    {
        return dictionary.ContainsKey(new StringName(key)) || dictionary.ContainsKey(key);
    }

    private static string GetDictionaryString(GDictionary dictionary, string key)
    {
        if (dictionary == null)
        {
            return string.Empty;
        }
        if (dictionary.ContainsKey(key))
        {
            return dictionary[key].ToString();
        }
        var stringNameKey = new StringName(key);
        return dictionary.ContainsKey(stringNameKey)
            ? dictionary[stringNameKey].ToString()
            : string.Empty;
    }

    private static void AddRange(
        Godot.Collections.Array<string> target,
        Godot.Collections.Array<string> source
    )
    {
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}
