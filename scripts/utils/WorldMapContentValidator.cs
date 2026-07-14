using System;
using System.Collections.Generic;
using Godot;

public class WorldMapContentValidator
{
    private static readonly StringName WorldEventTypeEnterSubmap = new("enter_submap");

    internal List<string> ValidateGenerationConfigTyped(
        WorldGenerationDefinition generationDefinition,
        string label,
        IEnumerable<StringName> enemyTemplateIds,
        IEnumerable<StringName> wildEncounterRosterIds
    )
    {
        if (generationDefinition == null)
        {
            return new List<string>
            {
                $"World generation definition {label} must not be null.",
            };
        }
        return ValidateGenerationDefinitionInternal(
            generationDefinition,
            label,
            SnapshotIds(enemyTemplateIds),
            SnapshotIds(wildEncounterRosterIds),
            new HashSet<string>(StringComparer.Ordinal)
        );
    }

    private static IReadOnlyCollection<StringName> SnapshotIds(
        IEnumerable<StringName> values
    ) =>
        values == null
            ? null
            : values is IReadOnlyCollection<StringName> readOnlyCollection
                ? readOnlyCollection
                : new HashSet<StringName>(values);

    private static List<string> ValidateGenerationDefinitionInternal(
        WorldGenerationDefinition definition,
        string label,
        IReadOnlyCollection<StringName> enemyTemplateIds,
        IReadOnlyCollection<StringName> wildEncounterRosterIds,
        HashSet<string> validatedPaths
    )
    {
        var errors = new List<string>();
        string canonicalPath = definition.CanonicalPath ?? "";
        if (canonicalPath.Length > 0 && !validatedPaths.Add(canonicalPath))
            return errors;

        Vector2I worldSizeInChunks = definition.WorldSizeInChunks;
        Vector2I chunkSize = definition.ChunkSize;
        if (worldSizeInChunks.X <= 0 || worldSizeInChunks.Y <= 0)
        {
            errors.Add(
                $"World generation config {label} has invalid world_size_in_chunks {worldSizeInChunks}."
            );
        }
        if (chunkSize.X <= 0 || chunkSize.Y <= 0)
            errors.Add($"World generation config {label} has invalid chunk_size {chunkSize}.");
        if (definition.StartingWildSpawnMinDistance > definition.StartingWildSpawnMaxDistance)
        {
            errors.Add(
                $"World generation config {label} has starting_wild_spawn_min_distance greater than max distance."
            );
        }

        HashSet<string> facilityIds = ValidateFacilityDefinitions(
            definition.EffectiveFacilityLibrary,
            label,
            errors
        );
        HashSet<string> settlementIds = ValidateSettlementDefinitions(
            definition.EffectiveSettlementLibrary,
            facilityIds,
            label,
            errors
        );
        ValidateSettlementDistributionDefinitions(
            definition.SettlementDistribution,
            settlementIds,
            label,
            errors
        );
        ValidateWildSpawnRuleDefinitions(
            definition.EffectiveWildSpawnRules,
            definition,
            enemyTemplateIds,
            wildEncounterRosterIds,
            label,
            errors
        );
        HashSet<StringName> mountedSubmapIds = ValidateMountedSubmapDefinitions(
            definition.MountedSubmaps,
            label,
            enemyTemplateIds,
            wildEncounterRosterIds,
            validatedPaths,
            errors
        );
        ValidateWorldEventDefinitions(
            definition.WorldEvents,
            mountedSubmapIds,
            definition.GetWorldSizeCells(),
            label,
            errors
        );
        ValidateNamePoolDefinitions(definition, label, errors);
        return errors;
    }


















    private static HashSet<string> ValidateFacilityDefinitions(
        IReadOnlyList<FacilityDefinition> facilities,
        string label,
        List<string> errors
    )
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (FacilityDefinition facility in facilities)
        {
            string facilityId = (facility.TemplateId ?? string.Empty).Trim();
            if (facilityId.Length == 0)
            {
                errors.Add($"World generation config {label} has facility missing facility_id.");
                continue;
            }
            if (!ids.Add(facilityId))
            {
                errors.Add(
                    $"World generation config {label} has duplicate facility_id {facilityId}."
                );
            }
            if (string.IsNullOrWhiteSpace(facility.DisplayName))
                errors.Add($"World facility {facilityId} in {label} is missing display_name.");
            if (
                string.IsNullOrWhiteSpace(facility.InteractionType)
                && facility.BoundServiceNpcs.Count == 0
            )
            {
                errors.Add(
                    $"World facility {facilityId} in {label} must declare interaction_type or bound service NPCs."
                );
            }
            ValidateFacilityNpcDefinitions(
                facility.BoundServiceNpcs,
                facilityId,
                label,
                errors
            );
        }
        return ids;
    }

    private static void ValidateFacilityNpcDefinitions(
        IReadOnlyList<FacilityNpcDefinition> npcs,
        string facilityId,
        string label,
        List<string> errors
    )
    {
        var npcIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (FacilityNpcDefinition npc in npcs)
        {
            string npcId = (npc.TemplateId ?? string.Empty).Trim();
            if (npcId.Length == 0)
            {
                errors.Add($"World facility {facilityId} in {label} has NPC missing npc_id.");
                continue;
            }
            if (!npcIds.Add(npcId))
                errors.Add($"World facility {facilityId} in {label} has duplicate npc_id {npcId}.");
            if (string.IsNullOrWhiteSpace(npc.ServiceType))
            {
                errors.Add(
                    $"World facility {facilityId} NPC {npcId} in {label} is missing service_type."
                );
            }
            if (string.IsNullOrWhiteSpace(npc.InteractionScriptId))
            {
                errors.Add(
                    $"World facility {facilityId} NPC {npcId} in {label} is missing interaction_script_id."
                );
            }
        }
    }

    private static HashSet<string> ValidateSettlementDefinitions(
        IReadOnlyList<SettlementDefinition> settlements,
        IReadOnlySet<string> facilityIds,
        string label,
        List<string> errors
    )
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (SettlementDefinition settlement in settlements)
        {
            string settlementId = (settlement.TemplateId ?? string.Empty).Trim();
            if (settlementId.Length == 0)
            {
                errors.Add(
                    $"World generation config {label} has settlement missing settlement_id."
                );
                continue;
            }
            if (!ids.Add(settlementId))
            {
                errors.Add(
                    $"World generation config {label} has duplicate settlement_id {settlementId}."
                );
            }
            if (string.IsNullOrWhiteSpace(settlement.DisplayName))
                errors.Add($"World settlement {settlementId} in {label} is missing display_name.");
            ValidateFacilitySlotDefinitions(
                settlement.FacilitySlots,
                settlementId,
                label,
                errors
            );
            foreach (string rawFacilityId in settlement.GuaranteedFacilityIds)
            {
                string facilityId = (rawFacilityId ?? string.Empty).Trim();
                if (facilityId.Length == 0)
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
            ValidateOptionalFacilityDefinitions(
                settlement.OptionalFacilityPool,
                facilityIds,
                settlementId,
                label,
                errors
            );
        }
        return ids;
    }

    private static void ValidateFacilitySlotDefinitions(
        IReadOnlyList<FacilitySlotDefinition> slots,
        string settlementId,
        string label,
        List<string> errors
    )
    {
        var slotIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (FacilitySlotDefinition slot in slots)
        {
            string slotId = (slot.SlotId ?? string.Empty).Trim();
            if (slotId.Length == 0)
            {
                errors.Add($"World settlement {settlementId} in {label} has slot missing slot_id.");
                continue;
            }
            if (!slotIds.Add(slotId))
            {
                errors.Add(
                    $"World settlement {settlementId} in {label} has duplicate slot_id {slotId}."
                );
            }
            if (string.IsNullOrWhiteSpace(slot.SlotTag))
            {
                errors.Add(
                    $"World settlement {settlementId} slot {slotId} in {label} is missing slot_tag."
                );
            }
        }
    }

    private static void ValidateOptionalFacilityDefinitions(
        IReadOnlyList<WeightedFacilityDefinition> pool,
        IReadOnlySet<string> facilityIds,
        string settlementId,
        string label,
        List<string> errors
    )
    {
        foreach (WeightedFacilityDefinition entry in pool)
        {
            string facilityId = (entry.FacilityTemplateId ?? string.Empty).Trim();
            if (facilityId.Length == 0)
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
            if (entry.Weight <= 0)
            {
                errors.Add(
                    $"World settlement {settlementId} in {label} has optional facility {facilityId} with non-positive weight."
                );
            }
        }
    }

    private static void ValidateSettlementDistributionDefinitions(
        IReadOnlyList<SettlementDistributionDefinition> distribution,
        IReadOnlySet<string> settlementIds,
        string label,
        List<string> errors
    )
    {
        foreach (SettlementDistributionDefinition rule in distribution)
        {
            string settlementId = (rule.SettlementTemplateId ?? string.Empty).Trim();
            if (settlementId.Length == 0)
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
            if (string.IsNullOrWhiteSpace(rule.FactionId))
            {
                errors.Add(
                    $"World generation config {label} settlement distribution for {settlementId} is missing faction_id."
                );
            }
        }
    }

    private static void ValidateWildSpawnRuleDefinitions(
        IReadOnlyList<WildSpawnRuleDefinition> rules,
        WorldGenerationDefinition generationDefinition,
        IReadOnlyCollection<StringName> enemyTemplateIds,
        IReadOnlyCollection<StringName> wildEncounterRosterIds,
        string label,
        List<string> errors
    )
    {
        Vector2I worldSizeInChunks = generationDefinition.WorldSizeInChunks;
        foreach (WildSpawnRuleDefinition rule in rules)
        {
            string regionTag = (rule.RegionTag ?? string.Empty).Trim();
            string enemyRosterTemplateId = rule.EnemyRosterTemplateId.ToString().Trim();
            string encounterProfileId = rule.EncounterProfileId.ToString().Trim();
            if (regionTag.Length == 0)
            {
                errors.Add(
                    $"World generation config {label} has wild spawn rule missing region_tag."
                );
            }
            if (enemyRosterTemplateId.Length == 0 && encounterProfileId.Length == 0)
            {
                errors.Add(
                    $"World generation config {label} wild spawn rule {regionTag} must declare enemy_roster_template_id or encounter_profile_id."
                );
            }
            if (
                enemyRosterTemplateId.Length > 0
                && HasEntries(enemyTemplateIds)
                && !ContainsStringName(enemyTemplateIds, enemyRosterTemplateId)
            )
            {
                errors.Add(
                    $"World generation config {label} wild spawn rule {regionTag} references missing enemy roster template {enemyRosterTemplateId}."
                );
            }
            if (
                encounterProfileId.Length > 0
                && HasEntries(wildEncounterRosterIds)
                && !ContainsStringName(wildEncounterRosterIds, encounterProfileId)
            )
            {
                errors.Add(
                    $"World generation config {label} wild spawn rule {regionTag} references missing encounter profile {encounterProfileId}."
                );
            }
            if (rule.DensityPerChunk <= 0)
            {
                errors.Add(
                    $"World generation config {label} wild spawn rule {regionTag} has non-positive density_per_chunk."
                );
            }
            if (rule.VisionRange < 0)
            {
                errors.Add(
                    $"World generation config {label} wild spawn rule {regionTag} has negative vision_range."
                );
            }
            if (rule.MinDistanceToSettlement < 0)
            {
                errors.Add(
                    $"World generation config {label} wild spawn rule {regionTag} has negative min_distance_to_settlement."
                );
            }
            if (rule.ChunkCoords.Count == 0)
            {
                errors.Add(
                    $"World generation config {label} wild spawn rule {regionTag} has empty chunk_coords."
                );
                continue;
            }
            foreach (Vector2I coord in rule.ChunkCoords)
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

    private static HashSet<StringName> ValidateMountedSubmapDefinitions(
        IReadOnlyList<MountedSubmapDefinition> submaps,
        string label,
        IReadOnlyCollection<StringName> enemyTemplateIds,
        IReadOnlyCollection<StringName> wildEncounterRosterIds,
        HashSet<string> validatedPaths,
        List<string> errors
    )
    {
        var ids = new HashSet<StringName>();
        foreach (MountedSubmapDefinition submap in submaps)
        {
            string submapId = submap.SubmapId.ToString().Trim();
            if (submapId.Length == 0)
            {
                errors.Add(
                    $"World generation config {label} has mounted submap missing submap_id."
                );
                continue;
            }
            StringName submapIdName = new(submapId);
            if (!ids.Add(submapIdName))
            {
                errors.Add(
                    $"World generation config {label} has duplicate mounted submap_id {submapId}."
                );
            }
            if (string.IsNullOrWhiteSpace(submap.GenerationConfigPath))
            {
                errors.Add(
                    $"World mounted submap {submapId} in {label} is missing generation_config_path."
                );
                continue;
            }
            if (submap.Generation == null)
            {
                errors.Add(
                    $"World mounted submap {submapId} in {label} failed to project generation_config_path {submap.GenerationConfigPath}."
                );
                continue;
            }
            AddRange(
                errors,
                ValidateGenerationDefinitionInternal(
                    submap.Generation,
                    submap.Generation.CanonicalPath,
                    enemyTemplateIds,
                    wildEncounterRosterIds,
                    validatedPaths
                )
            );
        }
        return ids;
    }

    private static void ValidateWorldEventDefinitions(
        IReadOnlyList<WorldEventDefinition> events,
        IReadOnlySet<StringName> mountedSubmapIds,
        Vector2I worldSizeCells,
        string label,
        List<string> errors
    )
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorldEventDefinition worldEvent in events)
        {
            string eventId = worldEvent.EventId.ToString().Trim();
            if (eventId.Length == 0)
            {
                errors.Add($"World generation config {label} has world event missing event_id.");
                continue;
            }
            if (!ids.Add(eventId))
            {
                errors.Add(
                    $"World generation config {label} has duplicate world event_id {eventId}."
                );
            }
            Vector2I coord = worldEvent.WorldCoord;
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
            if (worldEvent.EventType != WorldEventTypeEnterSubmap)
                continue;
            string target = worldEvent.TargetSubmapId.ToString().Trim();
            if (target.Length == 0)
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

    private static void ValidateNamePoolDefinitions(
        WorldGenerationDefinition definition,
        string label,
        List<string> errors
    )
    {
        if (!definition.InjectDefaultMainWorldContent)
            return;
        string[] requiredPaths =
        {
            WorldGenerationDefinition.DefaultMainWorldSettlementNamePoolPath,
            WorldGenerationDefinition.DefaultMainWorldTownNamePoolPath,
            WorldGenerationDefinition.DefaultMainWorldCityNamePoolPath,
            WorldGenerationDefinition.DefaultMainWorldCapitalNamePoolPath,
            WorldGenerationDefinition.DefaultMainWorldMetropolisNamePoolPath,
        };
        foreach (string resourcePath in requiredPaths)
        {
            string canonicalPath = ContentPathCanonicalizer.Canonicalize(resourcePath);
            if (
                !definition.SettlementNamePools.TryGetValue(
                    canonicalPath,
                    out WorldMapSettlementNamePoolDefinition namePool
                )
                || namePool == null
                || namePool.DisplayNames.Count == 0
            )
            {
                errors.Add(
                    $"World generation config {label} has empty settlement name pool {canonicalPath}."
                );
            }
        }
    }

    private static bool HasEntries<T>(IReadOnlyCollection<T> values)
    {
        return values != null && values.Count > 0;
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
