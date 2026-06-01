using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public sealed class WorldMapSpawnSystem
{
    private const string EncounterKindSingle = "single";
    private const string EncounterKindSettlement = "settlement";
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

    private static readonly Dictionary<string, string> ServiceActionIdByInteraction = new(
        StringComparer.Ordinal
    )
    {
        ["party_warehouse"] = "service:warehouse",
        ["service_rest_basic"] = "service:rest_basic",
        ["service_rest_full"] = "service:rest_full",
        ["service_basic_supply"] = "service:basic_supply",
        ["service_local_trade"] = "service:local_trade",
        ["service_city_market"] = "service:city_market",
        ["service_military_supply"] = "service:military_supply",
        ["service_grand_auction"] = "service:grand_auction",
        ["service_village_rumor"] = "service:village_rumor",
        ["service_intel_network"] = "service:intel_network",
        ["service_stagecoach"] = "service:stagecoach",
        ["service_world_gate_travel"] = "service:world_gate_travel",
        ["service_repair_gear"] = "service:repair_gear",
        ["service_contract_board"] = "service:contract_board",
        ["service_join_guild"] = "service:join_guild",
        ["service_identify_relic"] = "service:identify_relic",
        ["service_bounty_registry"] = "service:bounty_registry",
        ["service_recruit_specialist"] = "service:recruit_specialist",
        ["service_issue_regional_edict"] = "service:issue_regional_edict",
        ["service_research"] = "service:research",
        ["service_unlock_archive"] = "service:unlock_archive",
        ["service_diplomatic_clearance"] = "service:diplomatic_clearance",
        ["service_amnesty_review"] = "service:amnesty_review",
        ["service_elite_recruitment"] = "service:elite_recruitment",
        ["service_master_reforge"] = "service:master_reforge",
        ["service_respecialize_build"] = "service:respecialize_build",
        ["service_manage_reputation"] = "service:manage_reputation",
        ["service_open_trade_route"] = "service:open_trade_route",
        ["service_legend_contracts"] = "service:legend_contracts",
        ["service_hire_expert"] = "service:hire_expert",
    };

    private readonly RandomNumberGenerator _rng = new();
    private long _mapSeed;
    private WorldMapGenerationConfig _generationConfig;
    private WorldMapGridSystem _gridSystem;
    private readonly Dictionary<string, FacilityConfig> _facilityLibraryById = new(
        StringComparer.Ordinal
    );
    private readonly Dictionary<string, SettlementConfig> _settlementLibraryById = new(
        StringComparer.Ordinal
    );
    private readonly List<FacilityConfig> _resolvedFacilityLibrary = new();
    private readonly List<SettlementConfig> _resolvedSettlementLibrary = new();
    private readonly List<WildSpawnRule> _resolvedWildSpawnRules = new();
    private WorldMapSettlementBundle _defaultMainWorldSettlementBundle;
    private WorldMapWildSpawnBundle _defaultMainWorldWildSpawnBundle;
    private List<string> _remainingDefaultMainWorldSettlementDisplayNames = new();
    private List<string> _remainingDefaultMainWorldTownDisplayNames = new();
    private List<string> _remainingDefaultMainWorldCityDisplayNames = new();
    private List<string> _remainingDefaultMainWorldCapitalDisplayNames = new();
    private List<string> _remainingDefaultMainWorldMetropolisDisplayNames = new();

    private readonly struct WildSpawnChunkCandidateKey : IEquatable<WildSpawnChunkCandidateKey>
    {
        public readonly Vector2I ChunkCoord;
        public readonly int MinDistanceToSettlement;

        public WildSpawnChunkCandidateKey(Vector2I chunkCoord, int minDistanceToSettlement)
        {
            ChunkCoord = chunkCoord;
            MinDistanceToSettlement = Math.Max(minDistanceToSettlement, 0);
        }

        public bool Equals(WildSpawnChunkCandidateKey other)
        {
            return ChunkCoord == other.ChunkCoord
                && MinDistanceToSettlement == other.MinDistanceToSettlement;
        }

        public override bool Equals(object obj)
        {
            return obj is WildSpawnChunkCandidateKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ChunkCoord.X, ChunkCoord.Y, MinDistanceToSettlement);
        }
    }

    private sealed class WildSpawnPlacementContext
    {
        private readonly WorldMapGridSystem _gridSystem;
        private readonly Vector2I _chunkSize;
        private readonly List<Vector2I> _settlementCells;
        private readonly Dictionary<int, HashSet<Vector2I>> _blockedSettlementCellsByDistance =
            new();
        private readonly Dictionary<
            WildSpawnChunkCandidateKey,
            List<Vector2I>
        > _chunkCandidatesByKey = new();

        public WildSpawnPlacementContext(
            WorldMapGridSystem gridSystem,
            Vector2I chunkSize,
            List<Vector2I> settlementCells
        )
        {
            _gridSystem = gridSystem;
            _chunkSize = new Vector2I(Math.Max(chunkSize.X, 1), Math.Max(chunkSize.Y, 1));
            _settlementCells = settlementCells ?? new List<Vector2I>();
        }

        public List<Vector2I> GetChunkCandidates(Vector2I chunkCoord, int minDistanceToSettlement)
        {
            var key = new WildSpawnChunkCandidateKey(chunkCoord, minDistanceToSettlement);
            if (_chunkCandidatesByKey.TryGetValue(key, out List<Vector2I> cachedCandidates))
                return cachedCandidates;

            List<Vector2I> candidates = BuildChunkCandidates(
                chunkCoord,
                key.MinDistanceToSettlement
            );
            _chunkCandidatesByKey[key] = candidates;
            return candidates;
        }

        public bool IsTooCloseToSettlement(Vector2I candidate, int minDistanceToSettlement)
        {
            int normalizedDistance = Math.Max(minDistanceToSettlement, 0);
            if (normalizedDistance <= 0 || _settlementCells.Count == 0)
                return false;
            return GetBlockedSettlementCells(normalizedDistance).Contains(candidate);
        }

        private List<Vector2I> BuildChunkCandidates(
            Vector2I chunkCoord,
            int minDistanceToSettlement
        )
        {
            var baseOrigin = new Vector2I(chunkCoord.X * _chunkSize.X, chunkCoord.Y * _chunkSize.Y);
            var candidates = new List<Vector2I>();
            for (int y = 0; y < _chunkSize.Y; y++)
            {
                for (int x = 0; x < _chunkSize.X; x++)
                {
                    Vector2I candidate = baseOrigin + new Vector2I(x, y);
                    if (!_gridSystem.is_cell_inside_world(candidate))
                        continue;
                    if (_gridSystem.get_occupant_root(candidate) != "")
                        continue;
                    if (IsTooCloseToSettlement(candidate, minDistanceToSettlement))
                        continue;
                    candidates.Add(candidate);
                }
            }
            return candidates;
        }

        private HashSet<Vector2I> GetBlockedSettlementCells(int minDistanceToSettlement)
        {
            int normalizedDistance = Math.Max(minDistanceToSettlement, 0);
            if (
                _blockedSettlementCellsByDistance.TryGetValue(
                    normalizedDistance,
                    out HashSet<Vector2I> cachedCells
                )
            )
                return cachedCells;

            HashSet<Vector2I> blockedCells = BuildBlockedSettlementCells(normalizedDistance);
            _blockedSettlementCellsByDistance[normalizedDistance] = blockedCells;
            return blockedCells;
        }

        private HashSet<Vector2I> BuildBlockedSettlementCells(int minDistanceToSettlement)
        {
            var blockedCells = new HashSet<Vector2I>();
            if (minDistanceToSettlement <= 0 || _settlementCells.Count == 0)
                return blockedCells;

            int maxOffset = Math.Max(minDistanceToSettlement - 1, 0);
            int minDistanceSquared = minDistanceToSettlement * minDistanceToSettlement;
            foreach (Vector2I settlementCell in _settlementCells)
            {
                for (int y = -maxOffset; y <= maxOffset; y++)
                {
                    for (int x = -maxOffset; x <= maxOffset; x++)
                    {
                        if (x * x + y * y >= minDistanceSquared)
                            continue;
                        Vector2I blockedCoord = settlementCell + new Vector2I(x, y);
                        if (_gridSystem.is_cell_inside_world(blockedCoord))
                            blockedCells.Add(blockedCoord);
                    }
                }
            }
            return blockedCells;
        }
    }

    public GDictionary build_world(WorldMapGenerationConfig generation_config, WorldMapGridSystem grid_system)
    {
        _generationConfig = generation_config;
        _gridSystem = grid_system;
        if (_generationConfig == null || _gridSystem == null)
            return new GDictionary();

        _mapSeed = TrueRandomSeedService.generate_seed();
        _rng.Seed = (ulong)Math.Max(_mapSeed, 1L);
        BuildLibraries();

        GArray settlements = GenerateSettlements();
        GDictionary playerStartSettlement = FindPlayerStartSettlement(settlements);
        Vector2I playerStartCoord = ResolvePlayerStartCoord(playerStartSettlement);
        GArray worldNpcs = GenerateWorldNpcs(settlements);
        GArray encounterAnchors = GenerateEncounterAnchors(settlements, playerStartCoord);

        return new GDictionary
        {
            ["map_seed"] = _mapSeed,
            ["settlements"] = settlements,
            ["world_npcs"] = worldNpcs,
            ["encounter_anchors"] = encounterAnchors,
            ["world_events"] = GenerateWorldEvents(),
            ["mounted_submaps"] = GenerateMountedSubmaps(),
            ["active_submap_id"] = "",
            ["submap_return_stack"] = new GArray(),
            ["world_step"] = 0,
            ["next_equipment_instance_serial"] = 1,
            ["player_start_coord"] = playerStartCoord,
            ["player_start_settlement_id"] = GetString(playerStartSettlement, "settlement_id"),
            ["player_start_settlement_name"] = GetString(playerStartSettlement, "display_name"),
        };
    }

    private void BuildLibraries()
    {
        _facilityLibraryById.Clear();
        _settlementLibraryById.Clear();
        _defaultMainWorldSettlementBundle = LoadDefaultMainWorldSettlementBundle();
        _defaultMainWorldWildSpawnBundle = LoadDefaultMainWorldWildSpawnBundle();
        _remainingDefaultMainWorldSettlementDisplayNames =
            BuildDefaultMainWorldSettlementDisplayNames();
        _remainingDefaultMainWorldTownDisplayNames = BuildDefaultMainWorldTownDisplayNames();
        _remainingDefaultMainWorldCityDisplayNames = BuildDefaultMainWorldCityDisplayNames();
        _remainingDefaultMainWorldCapitalDisplayNames = BuildDefaultMainWorldCapitalDisplayNames();
        _remainingDefaultMainWorldMetropolisDisplayNames =
            BuildDefaultMainWorldMetropolisDisplayNames();

        _resolvedFacilityLibrary.Clear();
        _resolvedFacilityLibrary.AddRange(ResolveEffectiveFacilityLibrary());
        _resolvedSettlementLibrary.Clear();
        _resolvedSettlementLibrary.AddRange(ResolveEffectiveSettlementLibrary());
        _resolvedWildSpawnRules.Clear();
        _resolvedWildSpawnRules.AddRange(ResolveEffectiveWildSpawnRules());

        foreach (FacilityConfig facilityConfig in _resolvedFacilityLibrary)
        {
            string facilityTemplateId = GetFacilityTemplateId(facilityConfig);
            if (facilityTemplateId.Length == 0)
                continue;
            _facilityLibraryById[facilityTemplateId] = facilityConfig;
        }
        foreach (SettlementConfig settlementConfig in _resolvedSettlementLibrary)
        {
            string settlementTemplateId = GetSettlementTemplateId(settlementConfig);
            if (settlementTemplateId.Length == 0)
                continue;
            _settlementLibraryById[settlementTemplateId] = settlementConfig;
        }
    }

    private GArray GenerateSettlements()
    {
        return _generationConfig.procedural_generation_enabled
            ? GenerateProceduralSettlements()
            : GenerateFixedSettlements();
    }

    private GArray GenerateFixedSettlements()
    {
        var settlements = new GArray();
        var instanceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Resource ruleResource in _generationConfig.settlement_distribution)
        {
            var distributionRule = ruleResource as SettlementDistributionRule;
            string settlementTemplateId = GetDistributionRuleTemplateId(distributionRule);
            if (
                !_settlementLibraryById.TryGetValue(
                    settlementTemplateId,
                    out SettlementConfig settlementConfig
                )
            )
                continue;
            GDictionary settlement = CreateSettlementInstance(
                settlementConfig,
                distributionRule.preferred_origin,
                distributionRule.faction_id,
                instanceCounts,
                false
            );
            if (settlement.Count > 0)
                settlements.Add(settlement);
        }
        return settlements;
    }

    private GArray GenerateProceduralSettlements()
    {
        var settlements = new GArray();
        var instanceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        Dictionary<int, List<SettlementConfig>> templatesByTier = BuildSettlementTemplatesByTier();
        SettlementConfig playerVillageTemplate = PickSettlementTemplateForTier(
            templatesByTier,
            SettlementConfig.TIER_VILLAGE(),
            0
        );
        if (playerVillageTemplate != null)
        {
            Vector2I playerOrigin = GetCenteredOrigin(playerVillageTemplate.get_footprint_size());
            GDictionary playerSettlement = CreateSettlementInstance(
                playerVillageTemplate,
                playerOrigin,
                "player",
                instanceCounts,
                true
            );
            if (playerSettlement.Count > 0)
                settlements.Add(playerSettlement);
        }

        int[] generationOrder =
        {
            SettlementConfig.TIER_METROPOLIS(),
            SettlementConfig.TIER_WORLD_STRONGHOLD(),
            SettlementConfig.TIER_CAPITAL(),
            SettlementConfig.TIER_CITY(),
            SettlementConfig.TIER_TOWN(),
            SettlementConfig.TIER_VILLAGE(),
        };
        foreach (int tier in generationOrder)
        {
            int targetCount = _generationConfig.get_target_settlement_count(tier);
            if (tier == SettlementConfig.TIER_VILLAGE() && settlements.Count > 0)
                targetCount = Math.Max(targetCount - 1, 0);
            for (int tierIndex = 0; tierIndex < targetCount; tierIndex++)
            {
                SettlementConfig settlementTemplate = PickSettlementTemplateForTier(
                    templatesByTier,
                    tier,
                    tierIndex
                );
                if (settlementTemplate == null)
                    break;
                Vector2I origin = FindProceduralOrigin(
                    settlementTemplate.get_footprint_size(),
                    settlements,
                    _generationConfig.get_settlement_spacing_cells(tier)
                );
                if (origin == new Vector2I(-1, -1))
                {
                    GameLog.Warning(
                        $"Unable to place settlement for tier {tier} after repeated attempts.",
                        "world.spawn.settlement_placement_failed",
                        "world"
                    );
                    continue;
                }
                GDictionary settlement = CreateSettlementInstance(
                    settlementTemplate,
                    origin,
                    "neutral",
                    instanceCounts,
                    false
                );
                if (settlement.Count > 0)
                    settlements.Add(settlement);
            }
        }
        return settlements;
    }

    private Dictionary<int, List<SettlementConfig>> BuildSettlementTemplatesByTier()
    {
        var templatesByTier = new Dictionary<int, List<SettlementConfig>>();
        foreach (SettlementConfig settlementConfig in _resolvedSettlementLibrary)
        {
            int tier = settlementConfig.tier;
            if (!templatesByTier.ContainsKey(tier))
                templatesByTier[tier] = new List<SettlementConfig>();
            templatesByTier[tier].Add(settlementConfig);
        }
        return templatesByTier;
    }

    private static SettlementConfig PickSettlementTemplateForTier(
        Dictionary<int, List<SettlementConfig>> templatesByTier,
        int tier,
        int index
    )
    {
        if (
            !templatesByTier.TryGetValue(tier, out List<SettlementConfig> tierTemplates)
            || tierTemplates.Count == 0
        )
            return null;
        return tierTemplates[index % tierTemplates.Count];
    }

    private GDictionary CreateSettlementInstance(
        SettlementConfig settlementConfig,
        Vector2I origin,
        string factionId,
        Dictionary<string, int> instanceCounts,
        bool isPlayerStart
    )
    {
        Vector2I footprintSize = settlementConfig.get_footprint_size();
        if (!_gridSystem.can_place_footprint(origin, footprintSize))
        {
            GameLog.Error(
                $"Invalid settlement placement for {GetSettlementTemplateId(settlementConfig)} at {origin}",
                "world.spawn.invalid_placement",
                "world"
            );
            return new GDictionary();
        }
        string templateId = GetSettlementTemplateId(settlementConfig);
        if (templateId.Length == 0)
        {
            GameLog.Error($"Settlement template is missing template_id for placement at {origin}.", "world.spawn.settlement_missing_id", "world");
            return new GDictionary();
        }
        int instanceIndex = instanceCounts.TryGetValue(templateId, out int previousCount)
            ? previousCount + 1
            : 1;
        instanceCounts[templateId] = instanceIndex;
        string settlementId = BuildSettlementInstanceId(templateId, instanceIndex);
        string displayName = ResolveSettlementDisplayName(
            settlementConfig,
            templateId,
            instanceIndex
        );
        string entityId = $"settlement_{settlementId}";
        _gridSystem.register_footprint(entityId, origin, footprintSize);

        GArray facilities = GenerateFacilitiesForSettlement(settlementId, settlementConfig, origin);
        var settlement = new GDictionary
        {
            ["entity_id"] = entityId,
            ["template_id"] = templateId,
            ["settlement_id"] = settlementId,
            ["display_name"] = displayName,
            ["tier"] = settlementConfig.tier,
            ["tier_name"] = settlementConfig.get_tier_name(),
            ["faction_id"] = factionId,
            ["origin"] = origin,
            ["footprint_size"] = footprintSize,
            ["facilities"] = facilities,
            ["is_player_start"] = isPlayerStart,
            ["settlement_state"] = BuildDefaultSettlementState(isPlayerStart),
        };
        settlement["available_services"] = CollectServices(settlementId, facilities);
        settlement["service_npcs"] = CollectServiceNpcs(facilities);
        return settlement;
    }

    private GArray GenerateFacilitiesForSettlement(
        string settlementId,
        SettlementConfig settlementConfig,
        Vector2I settlementOrigin
    )
    {
        var generatedFacilities = new GArray();
        var usedSlotIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string facilityTemplateId in settlementConfig.guaranteed_facility_ids)
        {
            if (
                !_facilityLibraryById.TryGetValue(
                    facilityTemplateId,
                    out FacilityConfig facilityConfig
                )
            )
                continue;
            GDictionary placedFacility = TryPlaceFacility(
                settlementId,
                facilityConfig,
                settlementConfig,
                settlementOrigin,
                usedSlotIds
            );
            if (placedFacility.Count > 0)
                generatedFacilities.Add(placedFacility);
        }
        int optionalLimit = Math.Min(
            settlementConfig.max_optional_facilities,
            Math.Max(settlementConfig.facility_slots.Count - generatedFacilities.Count, 0)
        );
        var optionalPool = new List<WeightedFacilityEntry>();
        foreach (Resource entryResource in settlementConfig.optional_facility_pool)
        {
            if (entryResource is WeightedFacilityEntry entry)
                optionalPool.Add(entry);
        }
        for (int optionalIndex = 0; optionalIndex < optionalLimit; optionalIndex++)
        {
            string selectedFacilityTemplateId = PickWeightedFacility(optionalPool);
            if (selectedFacilityTemplateId.Length == 0)
                break;
            if (
                !_facilityLibraryById.TryGetValue(
                    selectedFacilityTemplateId,
                    out FacilityConfig facilityConfig
                )
            )
                continue;
            GDictionary placedFacility = TryPlaceFacility(
                settlementId,
                facilityConfig,
                settlementConfig,
                settlementOrigin,
                usedSlotIds
            );
            if (placedFacility.Count == 0)
                continue;
            generatedFacilities.Add(placedFacility);
            RemoveWeightedEntry(optionalPool, selectedFacilityTemplateId);
        }
        return generatedFacilities;
    }

    private GDictionary TryPlaceFacility(
        string settlementId,
        FacilityConfig facilityConfig,
        SettlementConfig settlementConfig,
        Vector2I settlementOrigin,
        HashSet<string> usedSlotIds
    )
    {
        if (facilityConfig.min_settlement_tier > settlementConfig.tier)
            return new GDictionary();
        string facilityTemplateId = GetFacilityTemplateId(facilityConfig);
        if (facilityTemplateId.Length == 0)
            return new GDictionary();
        foreach (Resource slotResource in settlementConfig.facility_slots)
        {
            var slotConfig = slotResource as FacilitySlotConfig;
            if (slotConfig == null)
                continue;
            if (usedSlotIds.Contains(slotConfig.slot_id))
                continue;
            if (
                facilityConfig.allowed_slot_tags.Count > 0
                && !facilityConfig.allowed_slot_tags.Contains(slotConfig.slot_tag)
            )
                continue;
            usedSlotIds.Add(slotConfig.slot_id);
            string facilityId = BuildFacilityInstanceId(
                settlementId,
                facilityTemplateId,
                slotConfig.slot_id
            );
            var serviceNpcs = new GArray();
            int npcIndex = 0;
            foreach (Resource npcResource in facilityConfig.bound_service_npcs)
            {
                var npcConfig = npcResource as FacilityNpcConfig;
                string npcTemplateId = GetNpcTemplateId(npcConfig);
                if (npcTemplateId.Length == 0)
                    continue;
                serviceNpcs.Add(
                    new GDictionary
                    {
                        ["template_id"] = npcTemplateId,
                        ["npc_id"] = BuildNpcInstanceId(
                            facilityId,
                            npcTemplateId,
                            npcConfig.local_slot_id,
                            npcIndex
                        ),
                        ["display_name"] = npcConfig.display_name,
                        ["service_type"] = npcConfig.service_type,
                        ["interaction_script_id"] = npcConfig.interaction_script_id,
                        ["local_slot_id"] = npcConfig.local_slot_id,
                        ["facility_id"] = facilityId,
                        ["facility_template_id"] = facilityTemplateId,
                        ["facility_name"] = facilityConfig.display_name,
                        ["settlement_id"] = settlementId,
                    }
                );
                npcIndex++;
            }
            return new GDictionary
            {
                ["template_id"] = facilityTemplateId,
                ["facility_id"] = facilityId,
                ["display_name"] = facilityConfig.display_name,
                ["category"] = facilityConfig.category,
                ["interaction_type"] = facilityConfig.interaction_type,
                ["slot_id"] = slotConfig.slot_id,
                ["slot_tag"] = slotConfig.slot_tag,
                ["local_coord"] = slotConfig.local_coord,
                ["world_coord"] = settlementOrigin + slotConfig.local_coord,
                ["settlement_id"] = settlementId,
                ["service_npcs"] = serviceNpcs,
            };
        }
        return new GDictionary();
    }

    private GArray CollectServices(string settlementId, GArray facilities)
    {
        var services = new GArray();
        bool hasPartyWarehouseService = false;
        foreach (GDictionary facility in ReadDictionaryItems(facilities))
        {
            foreach (GDictionary npc in ReadDictionaryItems(GetArray(facility, "service_npcs")))
            {
                string interactionScriptId = GetString(npc, "interaction_script_id");
                if (interactionScriptId == "party_warehouse")
                    hasPartyWarehouseService = true;
                services.Add(
                    new GDictionary
                    {
                        ["settlement_id"] = settlementId,
                        ["facility_id"] = GetString(facility, "facility_id"),
                        ["facility_template_id"] = GetString(facility, "template_id"),
                        ["facility_name"] = GetString(facility, "display_name"),
                        ["npc_id"] = GetString(npc, "npc_id"),
                        ["npc_template_id"] = GetString(npc, "template_id"),
                        ["npc_name"] = GetString(npc, "display_name"),
                        ["service_type"] = GetString(npc, "service_type"),
                        ["action_id"] = BuildServiceActionId(
                            GetString(npc, "service_type"),
                            interactionScriptId
                        ),
                        ["interaction_script_id"] = interactionScriptId,
                    }
                );
            }
        }
        if (!hasPartyWarehouseService)
        {
            services.Add(
                new GDictionary
                {
                    ["settlement_id"] = settlementId,
                    ["facility_id"] = $"{settlementId}__settlement_service_desk",
                    ["facility_template_id"] = "",
                    ["facility_name"] = "据点服务台",
                    ["npc_id"] = $"{settlementId}__settlement_quartermaster",
                    ["npc_template_id"] = "",
                    ["npc_name"] = "军需官",
                    ["service_type"] = "仓储",
                    ["action_id"] = ServiceActionIdByInteraction["party_warehouse"],
                    ["interaction_script_id"] = "party_warehouse",
                }
            );
        }
        return services;
    }

    private static GArray CollectServiceNpcs(GArray facilities)
    {
        var serviceNpcs = new GArray();
        foreach (GDictionary facility in ReadDictionaryItems(facilities))
        {
            foreach (var npcValue in GetArray(facility, "service_npcs"))
                serviceNpcs.Add(npcValue);
        }
        return serviceNpcs;
    }

    private static GDictionary BuildDefaultSettlementState(bool isPlayerStart)
    {
        return new GDictionary
        {
            ["visited"] = isPlayerStart,
            ["reputation"] = 0,
            ["active_conditions"] = new GArray(),
            ["cooldowns"] = new GDictionary(),
            ["shop_inventory_seed"] = TrueRandomSeedService.generate_seed(),
            ["shop_last_refresh_step"] = 0,
            ["shop_states"] = new GDictionary(),
        };
    }

    private static string BuildServiceActionId(string serviceType, string interactionScriptId)
    {
        if (ServiceActionIdByInteraction.TryGetValue(interactionScriptId, out string actionId))
            return actionId;
        string normalizedServiceType = ToSnakeCase(serviceType.Trim());
        if (normalizedServiceType.Length == 0)
            normalizedServiceType = "service";
        return $"service:{normalizedServiceType}";
    }

    private static string GetSettlementTemplateId(SettlementConfig settlementConfig)
    {
        return settlementConfig == null
            ? ""
            : (settlementConfig.get_template_id() ?? "").StripEdges();
    }

    private static string GetDistributionRuleTemplateId(SettlementDistributionRule distributionRule)
    {
        return distributionRule == null
            ? ""
            : (distributionRule.get_settlement_template_id() ?? "").StripEdges();
    }

    private static string GetFacilityTemplateId(FacilityConfig facilityConfig)
    {
        return facilityConfig == null ? "" : (facilityConfig.get_template_id() ?? "").StripEdges();
    }

    private static string GetNpcTemplateId(FacilityNpcConfig npcConfig)
    {
        return npcConfig == null ? "" : (npcConfig.get_template_id() ?? "").StripEdges();
    }

    private static string GetWeightedFacilityTemplateId(WeightedFacilityEntry weightedEntry)
    {
        return weightedEntry == null
            ? ""
            : (weightedEntry.get_facility_template_id() ?? "").StripEdges();
    }

    private static string BuildSettlementInstanceId(string templateId, int instanceIndex)
    {
        return templateId.Length == 0 ? "" : $"{templateId}_{Math.Max(instanceIndex, 1):00}";
    }

    private static string BuildFacilityInstanceId(
        string settlementId,
        string templateId,
        string slotId
    )
    {
        string normalizedTemplateId = ToSnakeCase((templateId ?? "").StripEdges());
        string normalizedSlotId = ToSnakeCase((slotId ?? "").StripEdges());
        if (normalizedTemplateId.Length == 0)
            normalizedTemplateId = "facility";
        if (normalizedSlotId.Length == 0)
            normalizedSlotId = "slot";
        return $"{settlementId}__{normalizedTemplateId}__{normalizedSlotId}";
    }

    private static string BuildNpcInstanceId(
        string facilityId,
        string templateId,
        string localSlotId,
        int npcIndex
    )
    {
        string normalizedTemplateId = ToSnakeCase((templateId ?? "").StripEdges());
        string normalizedSlotId = ToSnakeCase((localSlotId ?? "").StripEdges());
        if (normalizedTemplateId.Length == 0)
            normalizedTemplateId = "npc";
        if (normalizedSlotId.Length == 0)
            normalizedSlotId = $"slot_{Math.Max(npcIndex + 1, 1):00}";
        return $"{facilityId}__{normalizedTemplateId}__{normalizedSlotId}";
    }

    private string PickWeightedFacility(List<WeightedFacilityEntry> optionalPool)
    {
        if (optionalPool.Count == 0)
            return "";
        int totalWeight = 0;
        foreach (WeightedFacilityEntry entry in optionalPool)
            totalWeight += Math.Max(entry.weight, 0);
        if (totalWeight <= 0)
            return "";
        int roll = _rng.RandiRange(1, totalWeight);
        int cursor = 0;
        foreach (WeightedFacilityEntry entry in optionalPool)
        {
            cursor += Math.Max(entry.weight, 0);
            if (roll <= cursor)
                return GetWeightedFacilityTemplateId(entry);
        }
        return GetWeightedFacilityTemplateId(optionalPool[0]);
    }

    private static void RemoveWeightedEntry(
        List<WeightedFacilityEntry> optionalPool,
        string facilityId
    )
    {
        for (int index = 0; index < optionalPool.Count; index++)
        {
            if (GetWeightedFacilityTemplateId(optionalPool[index]) == facilityId)
            {
                optionalPool.RemoveAt(index);
                return;
            }
        }
    }

    private GArray GenerateWorldNpcs(GArray settlements)
    {
        var worldNpcs = new GArray();
        string[] npcNames = { "巡路信使", "驿站商人", "边地向导", "地图学者", "补给联络员" };
        int nameIndex = 0;
        foreach (GDictionary settlement in ReadDictionaryItems(settlements))
        {
            Vector2I origin = GetVector2I(settlement, "origin", Vector2I.Zero);
            Vector2I footprintSize = GetVector2I(settlement, "footprint_size", Vector2I.One);
            Vector2I spawnCoord = FindFreeCoordNear(origin + footprintSize - Vector2I.One);
            if (spawnCoord == new Vector2I(-1, -1))
                continue;
            string npcName = npcNames[nameIndex % npcNames.Length];
            nameIndex++;
            worldNpcs.Add(
                new GDictionary
                {
                    ["entity_id"] = $"world_npc_{nameIndex}",
                    ["display_name"] = npcName,
                    ["coord"] = spawnCoord,
                    ["kind"] = "service_hint",
                    ["faction_id"] = GetString(settlement, "faction_id", "neutral"),
                    ["vision_range"] = 1,
                }
            );
        }
        return worldNpcs;
    }

    private GArray GenerateEncounterAnchors(GArray settlements, Vector2I playerStartCoord)
    {
        var settlementCells = new List<Vector2I>();
        foreach (GDictionary settlement in ReadDictionaryItems(settlements))
        {
            Vector2I origin = GetVector2I(settlement, "origin", Vector2I.Zero);
            Vector2I footprintSize = GetVector2I(settlement, "footprint_size", Vector2I.One);
            for (int y = 0; y < footprintSize.Y; y++)
            for (int x = 0; x < footprintSize.X; x++)
                settlementCells.Add(origin + new Vector2I(x, y));
        }

        var placementContext = new WildSpawnPlacementContext(
            _gridSystem,
            _generationConfig.chunk_size,
            settlementCells
        );

        GArray encounterAnchors;
        if (_generationConfig.procedural_generation_enabled)
        {
            encounterAnchors = GenerateProceduralEncounterAnchors(placementContext);
        }
        else
        {
            encounterAnchors = new GArray();
            int monsterIndex = 0;
            foreach (WildSpawnRule rule in _resolvedWildSpawnRules)
            {
                foreach (Vector2I chunkCoord in rule.chunk_coords)
                {
                    for (int offset = 0; offset < Math.Max(rule.density_per_chunk, 0); offset++)
                    {
                        Vector2I spawnCoord = PickMonsterCoordForChunk(
                            chunkCoord,
                            rule.min_distance_to_settlement,
                            placementContext,
                            offset
                        );
                        if (spawnCoord == new Vector2I(-1, -1))
                            continue;
                        monsterIndex++;
                        encounterAnchors.Add(
                            BuildEncounterAnchor(
                                new StringName($"wild_{monsterIndex}"),
                                rule.enemy_roster_template_id,
                                rule.monster_name,
                                spawnCoord,
                                rule.vision_range,
                                new StringName(rule.region_tag),
                                new StringName(EncounterKindSingle),
                                rule.encounter_profile_id
                            )
                        );
                    }
                }
            }
        }
        EnsureStartingWildEncounter(encounterAnchors, placementContext, playerStartCoord);
        EnsureDefaultSettlementEncounter(encounterAnchors, placementContext);
        return encounterAnchors;
    }

    private GArray GenerateProceduralEncounterAnchors(WildSpawnPlacementContext placementContext)
    {
        var encounterAnchors = new GArray();
        if (_resolvedWildSpawnRules.Count == 0)
            return encounterAnchors;
        Vector2I worldChunks = _generationConfig.world_size_in_chunks;
        int monsterIndex = 0;
        int spawnChunkChanceDenominator = Math.Max(
            _generationConfig.procedural_wild_spawn_chunk_chance_denominator,
            1
        );
        for (int chunkY = 0; chunkY < worldChunks.Y; chunkY++)
        {
            for (int chunkX = 0; chunkX < worldChunks.X; chunkX++)
            {
                var chunkCoord = new Vector2I(chunkX, chunkY);
                WildSpawnRule rule = ResolveProceduralWildSpawnRuleForChunkY(chunkY);
                if (rule == null)
                    continue;
                int chunkSeed = (int)TrueRandomSeedService.generate_seed();
                if (PosMod(chunkSeed, spawnChunkChanceDenominator) != 0)
                    continue;
                for (int offset = 0; offset < Math.Max(rule.density_per_chunk, 0); offset++)
                {
                    Vector2I spawnCoord = PickMonsterCoordForChunk(
                        chunkCoord,
                        rule.min_distance_to_settlement,
                        placementContext,
                        chunkSeed + offset
                    );
                    if (spawnCoord == new Vector2I(-1, -1))
                        continue;
                    monsterIndex++;
                    encounterAnchors.Add(
                        BuildEncounterAnchor(
                            new StringName($"wild_{monsterIndex}"),
                            rule.enemy_roster_template_id,
                            rule.monster_name,
                            spawnCoord,
                            rule.vision_range,
                            new StringName(rule.region_tag),
                            new StringName(EncounterKindSingle),
                            rule.encounter_profile_id
                        )
                    );
                }
            }
        }
        return encounterAnchors;
    }

    private void EnsureStartingWildEncounter(
        GArray encounterAnchors,
        WildSpawnPlacementContext placementContext,
        Vector2I playerStartCoord
    )
    {
        if (!_generationConfig.guarantee_starting_wild_encounter)
            return;
        if (!_gridSystem.is_cell_inside_world(playerStartCoord))
            return;
        if (_resolvedWildSpawnRules.Count == 0)
            return;
        WildSpawnRule rule = _resolvedWildSpawnRules[0];
        if (_generationConfig.procedural_generation_enabled)
        {
            Vector2I playerChunkCoord = _gridSystem.get_chunk_coord(playerStartCoord);
            rule = ResolveProceduralWildSpawnRuleForChunkY(playerChunkCoord.Y);
        }
        if (rule == null)
            return;
        int minDistance = Math.Max(
            _generationConfig.starting_wild_spawn_min_distance,
            rule.min_distance_to_settlement
        );
        int maxDistance = Math.Max(
            Math.Max(
                _generationConfig.starting_wild_spawn_min_distance,
                _generationConfig.starting_wild_spawn_max_distance
            ),
            minDistance
        );
        if (HasStartingEncounterInRange(encounterAnchors, playerStartCoord, maxDistance))
            return;
        Vector2I spawnCoord = FindStartingWildCoord(
            playerStartCoord,
            placementContext,
            encounterAnchors,
            minDistance,
            maxDistance
        );
        if (spawnCoord == new Vector2I(-1, -1))
        {
            GameLog.Warning(
                $"Unable to place a guaranteed starting wild encounter near {playerStartCoord}.",
                "world.spawn.wild_encounter_failed",
                "world"
            );
            return;
        }
        encounterAnchors.Add(
            BuildEncounterAnchor(
                new StringName($"wild_{encounterAnchors.Count + 1}"),
                rule.enemy_roster_template_id,
                rule.monster_name,
                spawnCoord,
                rule.vision_range,
                new StringName(rule.region_tag),
                new StringName(EncounterKindSingle),
                rule.encounter_profile_id
            )
        );
    }

    private static bool HasStartingEncounterInRange(
        GArray encounterAnchors,
        Vector2I playerStartCoord,
        int maxDistance
    )
    {
        foreach (var encounterValue in encounterAnchors)
        {
            var encounterAnchor = encounterValue.AsGodotObject() as EncounterAnchorData;
            if (encounterAnchor == null)
                continue;
            Vector2I delta = encounterAnchor.world_coord - playerStartCoord;
            if (Math.Abs(delta.X) + Math.Abs(delta.Y) <= maxDistance)
                return true;
        }
        return false;
    }

    private Vector2I FindStartingWildCoord(
        Vector2I playerStartCoord,
        WildSpawnPlacementContext placementContext,
        GArray encounterAnchors,
        int minDistance,
        int maxDistance
    )
    {
        var candidates = new List<Vector2I>();
        for (int offsetY = -maxDistance; offsetY <= maxDistance; offsetY++)
        {
            for (int offsetX = -maxDistance; offsetX <= maxDistance; offsetX++)
            {
                int distance = Math.Abs(offsetX) + Math.Abs(offsetY);
                if (distance < minDistance || distance > maxDistance)
                    continue;
                Vector2I candidate = playerStartCoord + new Vector2I(offsetX, offsetY);
                if (!_gridSystem.is_cell_inside_world(candidate))
                    continue;
                if (_gridSystem.get_occupant_root(candidate) != "")
                    continue;
                if (placementContext.IsTooCloseToSettlement(candidate, minDistance))
                    continue;
                if (HasEncounterAnchorAt(encounterAnchors, candidate))
                    continue;
                candidates.Add(candidate);
            }
        }
        if (candidates.Count == 0)
            return new Vector2I(-1, -1);
        return candidates[_rng.RandiRange(0, candidates.Count - 1)];
    }

    private static bool HasEncounterAnchorAt(GArray encounterAnchors, Vector2I coord)
    {
        foreach (var encounterValue in encounterAnchors)
        {
            var encounterAnchor = encounterValue.AsGodotObject() as EncounterAnchorData;
            if (encounterAnchor != null && encounterAnchor.world_coord == coord)
                return true;
        }
        return false;
    }

    private void EnsureDefaultSettlementEncounter(
        GArray encounterAnchors,
        WildSpawnPlacementContext placementContext
    )
    {
        foreach (var encounterValue in encounterAnchors)
        {
            var existingAnchor = encounterValue.AsGodotObject() as EncounterAnchorData;
            if (existingAnchor == null)
                continue;
            if (existingAnchor.encounter_kind == new StringName(EncounterKindSettlement))
                return;
        }
        foreach (WildSpawnRule rule in _resolvedWildSpawnRules)
        {
            if (rule == null || rule.enemy_roster_template_id != new StringName("wolf_pack"))
                continue;
            foreach (Vector2I chunkCoord in BuildDefaultSettlementCandidateChunks(rule))
            {
                Vector2I spawnCoord = PickMonsterCoordForChunk(
                    chunkCoord,
                    Math.Max(rule.min_distance_to_settlement, 2),
                    placementContext,
                    (int)TrueRandomSeedService.generate_seed()
                );
                if (spawnCoord == new Vector2I(-1, -1))
                    continue;
                if (HasEncounterAnchorAt(encounterAnchors, spawnCoord))
                    continue;
                encounterAnchors.Add(
                    BuildEncounterAnchor(
                        new StringName($"wild_settlement_{encounterAnchors.Count + 1}"),
                        rule.enemy_roster_template_id,
                        "荒狼巢穴",
                        spawnCoord,
                        Math.Max(rule.vision_range, 2),
                        new StringName(rule.region_tag),
                        new StringName(EncounterKindSettlement),
                        new StringName("wolf_den"),
                        0
                    )
                );
                return;
            }
        }
    }

    private List<Vector2I> BuildDefaultSettlementCandidateChunks(WildSpawnRule rule)
    {
        if (rule != null && rule.chunk_coords.Count > 0)
            return new List<Vector2I>(rule.chunk_coords);
        var candidateChunks = new List<Vector2I>();
        Vector2I worldChunks = _generationConfig.world_size_in_chunks;
        int midpointChunkY = worldChunks.Y / 2;
        for (int chunkY = 0; chunkY < worldChunks.Y; chunkY++)
        for (int chunkX = 0; chunkX < worldChunks.X; chunkX++)
        {
            if (
                rule != null
                && rule.enemy_roster_template_id == new StringName("wolf_pack")
                && chunkY >= midpointChunkY
            )
                continue;
            candidateChunks.Add(new Vector2I(chunkX, chunkY));
        }
        return candidateChunks;
    }

    private List<SettlementConfig> ResolveEffectiveSettlementLibrary()
    {
        var resolved = new List<SettlementConfig>();
        if (_defaultMainWorldSettlementBundle != null)
        {
            foreach (
                Resource settlementResource in _defaultMainWorldSettlementBundle.settlement_library
            )
                if (settlementResource is SettlementConfig settlementConfig)
                    resolved.Add(settlementConfig);
        }
        foreach (Resource settlementResource in _generationConfig.settlement_library)
            if (settlementResource is SettlementConfig settlementConfig)
                resolved.Add(settlementConfig);
        return resolved;
    }

    private List<FacilityConfig> ResolveEffectiveFacilityLibrary()
    {
        var resolved = new List<FacilityConfig>();
        if (_defaultMainWorldSettlementBundle != null)
        {
            foreach (
                Resource facilityResource in _defaultMainWorldSettlementBundle.facility_library
            )
                if (facilityResource is FacilityConfig facilityConfig)
                    resolved.Add(facilityConfig);
        }
        foreach (Resource facilityResource in _generationConfig.facility_library)
            if (facilityResource is FacilityConfig facilityConfig)
                resolved.Add(facilityConfig);
        return resolved;
    }

    private List<WildSpawnRule> ResolveEffectiveWildSpawnRules()
    {
        var resolved = new List<WildSpawnRule>();
        if (_defaultMainWorldWildSpawnBundle != null)
        {
            foreach (
                Resource ruleResource in _defaultMainWorldWildSpawnBundle.wild_monster_distribution
            )
                if (ruleResource is WildSpawnRule rule)
                    resolved.Add(rule);
        }
        foreach (Resource ruleResource in _generationConfig.wild_monster_distribution)
            if (ruleResource is WildSpawnRule rule)
                resolved.Add(rule);
        return resolved;
    }

    private WorldMapSettlementBundle LoadDefaultMainWorldSettlementBundle()
    {
        if (_generationConfig == null || !_generationConfig.inject_default_main_world_content)
            return null;
        var settlementBundle = GD.Load<WorldMapSettlementBundle>(
            DefaultMainWorldSettlementBundlePath
        );
        if (settlementBundle == null)
            GameLog.Warning(
                $"Unable to load default main-world settlement bundle from {DefaultMainWorldSettlementBundlePath}.",
                "world.spawn.settlement_bundle_load_failed",
                "world"
            );
        return settlementBundle;
    }

    private WorldMapWildSpawnBundle LoadDefaultMainWorldWildSpawnBundle()
    {
        if (_generationConfig == null || !_generationConfig.inject_default_main_world_content)
            return null;
        var wildSpawnBundle = GD.Load<WorldMapWildSpawnBundle>(DefaultMainWorldWildSpawnBundlePath);
        if (wildSpawnBundle == null)
            GameLog.Warning(
                $"Unable to load default main-world wild spawn bundle from {DefaultMainWorldWildSpawnBundlePath}.",
                "world.spawn.wild_spawn_bundle_load_failed",
                "world"
            );
        return wildSpawnBundle;
    }

    private List<string> BuildDefaultMainWorldSettlementDisplayNames()
    {
        return BuildShuffledDisplayNamesFromPool(
            DefaultMainWorldSettlementNamePoolPath,
            "default main-world settlement"
        );
    }

    private List<string> BuildDefaultMainWorldTownDisplayNames()
    {
        return BuildShuffledDisplayNamesFromPool(
            DefaultMainWorldTownNamePoolPath,
            "default main-world town"
        );
    }

    private List<string> BuildDefaultMainWorldCityDisplayNames()
    {
        return BuildShuffledDisplayNamesFromPool(
            DefaultMainWorldCityNamePoolPath,
            "default main-world city"
        );
    }

    private List<string> BuildDefaultMainWorldCapitalDisplayNames()
    {
        return BuildShuffledDisplayNamesFromPool(
            DefaultMainWorldCapitalNamePoolPath,
            "default main-world capital"
        );
    }

    private List<string> BuildDefaultMainWorldMetropolisDisplayNames()
    {
        return BuildShuffledDisplayNamesFromPool(
            DefaultMainWorldMetropolisNamePoolPath,
            "default main-world metropolis"
        );
    }

    private List<string> BuildShuffledDisplayNamesFromPool(string resourcePath, string warningLabel)
    {
        WorldMapSettlementNamePool namePool = LoadDefaultMainWorldSettlementNamePool(
            resourcePath,
            warningLabel
        );
        if (namePool == null)
            return new List<string>();
        var uniqueNames = new List<string>(namePool.build_unique_display_names());
        if (uniqueNames.Count == 0)
            return uniqueNames;
        var nameRng = new RandomNumberGenerator
        {
            Seed = (ulong)Math.Max(TrueRandomSeedService.generate_seed(), 1L),
        };
        for (int index = uniqueNames.Count - 1; index > 0; index--)
        {
            int swapIndex = nameRng.RandiRange(0, index);
            (uniqueNames[index], uniqueNames[swapIndex]) = (
                uniqueNames[swapIndex],
                uniqueNames[index]
            );
        }
        return uniqueNames;
    }

    private WorldMapSettlementNamePool LoadDefaultMainWorldSettlementNamePool(
        string resourcePath,
        string warningLabel
    )
    {
        if (_generationConfig == null || !_generationConfig.inject_default_main_world_content)
            return null;
        var namePool = GD.Load<WorldMapSettlementNamePool>(resourcePath);
        if (namePool == null)
            GameLog.Warning($"Unable to load {warningLabel} name pool from {resourcePath}.", "world.spawn.name_pool_load_failed", "world");
        return namePool;
    }

    private WildSpawnRule ResolveProceduralWildSpawnRuleForChunkY(int chunkY)
    {
        WildSpawnRule northRule = FindWildSpawnRuleByRegionTag(new StringName("north_wilds"));
        WildSpawnRule southRule = FindWildSpawnRuleByRegionTag(new StringName("south_wilds"));
        if (northRule == null && _resolvedWildSpawnRules.Count > 0)
            northRule = _resolvedWildSpawnRules[0];
        if (southRule == null)
            southRule = _resolvedWildSpawnRules.Count > 1 ? _resolvedWildSpawnRules[1] : northRule;
        if (northRule == null)
            return southRule;
        if (southRule == null)
            return northRule;
        int midpointChunkY = _generationConfig.world_size_in_chunks.Y / 2;
        return chunkY < midpointChunkY ? northRule : southRule;
    }

    private WildSpawnRule FindWildSpawnRuleByRegionTag(StringName regionTag)
    {
        foreach (WildSpawnRule rule in _resolvedWildSpawnRules)
        {
            if (rule == null)
                continue;
            if (new StringName(rule.region_tag) == regionTag)
                return rule;
        }
        return null;
    }

    private string ResolveSettlementDisplayName(
        SettlementConfig settlementConfig,
        string templateId,
        int instanceIndex
    )
    {
        if (templateId == "template_town" && _remainingDefaultMainWorldTownDisplayNames.Count > 0)
            return PopBack(_remainingDefaultMainWorldTownDisplayNames);
        if (templateId == "template_city" && _remainingDefaultMainWorldCityDisplayNames.Count > 0)
            return PopBack(_remainingDefaultMainWorldCityDisplayNames);
        if (
            templateId == "template_capital"
            && _remainingDefaultMainWorldCapitalDisplayNames.Count > 0
        )
            return PopBack(_remainingDefaultMainWorldCapitalDisplayNames);
        if (
            templateId == "template_metropolis"
            && _remainingDefaultMainWorldMetropolisDisplayNames.Count > 0
        )
            return PopBack(_remainingDefaultMainWorldMetropolisDisplayNames);
        if (templateId == "template_world_stronghold")
        {
            string strongholdDisplayName = settlementConfig.display_name;
            if (instanceIndex > 1)
                strongholdDisplayName = $"{strongholdDisplayName} {instanceIndex:00}";
            return strongholdDisplayName;
        }
        if (
            templateId.StartsWith("template_", StringComparison.Ordinal)
            && _remainingDefaultMainWorldSettlementDisplayNames.Count > 0
        )
            return PopBack(_remainingDefaultMainWorldSettlementDisplayNames);
        string displayName = settlementConfig.display_name;
        if (instanceIndex > 1)
            displayName = $"{displayName} {instanceIndex:00}";
        return displayName;
    }

    private static EncounterAnchorData BuildEncounterAnchor(
        StringName entityId,
        StringName enemyRosterTemplateId,
        string displayName,
        Vector2I worldCoord,
        int visionRange,
        StringName regionTag,
        StringName encounterKind,
        StringName encounterProfileId,
        int growthStage = 0
    )
    {
        var encounterAnchor = new EncounterAnchorData
        {
            entity_id = entityId,
            display_name = displayName,
            world_coord = worldCoord,
            faction_id = new StringName("hostile"),
            enemy_roster_template_id = enemyRosterTemplateId,
            region_tag = regionTag,
            vision_range = visionRange,
            is_cleared = false,
            encounter_kind = encounterKind,
            encounter_profile_id = encounterProfileId,
            growth_stage = Math.Max(growthStage, 0),
            suppressed_until_step = 0,
        };
        return encounterAnchor;
    }

    private GArray GenerateWorldEvents()
    {
        var generatedEvents = new GArray();
        foreach (Resource eventResource in _generationConfig.world_events)
        {
            var eventConfig = eventResource as WorldEventConfig;
            if (eventConfig == null || eventConfig.event_id == new StringName(""))
                continue;
            generatedEvents.Add(
                new GDictionary
                {
                    ["event_id"] = eventConfig.event_id.ToString(),
                    ["display_name"] = eventConfig.display_name,
                    ["world_coord"] = eventConfig.world_coord,
                    ["event_type"] = eventConfig.event_type.ToString(),
                    ["target_submap_id"] = eventConfig.target_submap_id.ToString(),
                    ["discovery_condition_id"] = eventConfig.discovery_condition_id.ToString(),
                    ["prompt_title"] = eventConfig.prompt_title,
                    ["prompt_text"] = eventConfig.prompt_text,
                    ["is_discovered"] = IsWorldEventDiscoveredByDefault(eventConfig),
                }
            );
        }
        return generatedEvents;
    }

    private GDictionary GenerateMountedSubmaps()
    {
        var mountedSubmaps = new GDictionary();
        foreach (Resource submapResource in _generationConfig.mounted_submaps)
        {
            var submapConfig = submapResource as MountedSubmapConfig;
            if (submapConfig == null || submapConfig.submap_id == new StringName(""))
                continue;
            mountedSubmaps[submapConfig.submap_id.ToString()] = new GDictionary
            {
                ["submap_id"] = submapConfig.submap_id.ToString(),
                ["display_name"] = submapConfig.display_name,
                ["generation_config_path"] = submapConfig.generation_config_path,
                ["return_hint_text"] = submapConfig.return_hint_text,
                ["is_generated"] = false,
                ["player_coord"] = new Vector2I(-1, -1),
                ["world_data"] = new GDictionary(),
            };
        }
        return mountedSubmaps;
    }

    private static bool IsWorldEventDiscoveredByDefault(WorldEventConfig eventConfig)
    {
        if (eventConfig == null)
            return false;
        string conditionId = eventConfig.discovery_condition_id.ToString().StripEdges();
        return conditionId.Length == 0 || conditionId == "always_true";
    }

    private Vector2I FindFreeCoordNear(Vector2I origin)
    {
        Vector2I[] candidateDirections =
        {
            Vector2I.Right,
            Vector2I.Down,
            Vector2I.Left,
            Vector2I.Up,
            new(1, 1),
        };
        foreach (Vector2I direction in candidateDirections)
        {
            Vector2I candidate = origin + direction;
            if (!_gridSystem.is_cell_inside_world(candidate))
                continue;
            if (_gridSystem.get_occupant_root(candidate) != "")
                continue;
            return candidate;
        }
        return new Vector2I(-1, -1);
    }

    private static GDictionary FindPlayerStartSettlement(GArray settlements)
    {
        foreach (GDictionary settlement in ReadDictionaryItems(settlements))
        {
            if (ReadExactBool(settlement, "is_player_start"))
                return settlement;
        }
        foreach (GDictionary settlement in ReadDictionaryItems(settlements))
        {
            if (GetInt(settlement, "tier", -1) == SettlementConfig.TIER_VILLAGE())
                return settlement;
        }
        return new GDictionary();
    }

    private Vector2I ResolvePlayerStartCoord(GDictionary playerStartSettlement)
    {
        return playerStartSettlement.Count == 0
            ? _generationConfig.player_start_coord
            : GetVector2I(playerStartSettlement, "origin", _generationConfig.player_start_coord);
    }

    private Vector2I GetCenteredOrigin(Vector2I footprintSize)
    {
        Vector2I worldSize = _generationConfig.get_world_size_cells();
        int maxX = Math.Max(worldSize.X - footprintSize.X, 0);
        int maxY = Math.Max(worldSize.Y - footprintSize.Y, 0);
        return new Vector2I(Mathf.Clamp(maxX / 2, 0, maxX), Mathf.Clamp(maxY / 2, 0, maxY));
    }

    private Vector2I FindProceduralOrigin(
        Vector2I footprintSize,
        GArray existingSettlements,
        int minDistanceCells
    )
    {
        Vector2I worldSize = _generationConfig.get_world_size_cells();
        int maxX = worldSize.X - footprintSize.X;
        int maxY = worldSize.Y - footprintSize.Y;
        if (maxX < 0 || maxY < 0)
            return new Vector2I(-1, -1);
        for (int attempt = 0; attempt < 192; attempt++)
        {
            var origin = new Vector2I(_rng.RandiRange(0, maxX), _rng.RandiRange(0, maxY));
            if (!_gridSystem.can_place_footprint(origin, footprintSize))
                continue;
            if (!IsOriginFarEnough(origin, footprintSize, existingSettlements, minDistanceCells))
                continue;
            return origin;
        }
        return new Vector2I(-1, -1);
    }

    private bool IsOriginFarEnough(
        Vector2I candidateOrigin,
        Vector2I candidateSize,
        GArray existingSettlements,
        int minDistanceCells
    )
    {
        Vector2 candidateCenter =
            new Vector2(candidateOrigin.X, candidateOrigin.Y)
            + new Vector2(candidateSize.X, candidateSize.Y) * 0.5f;
        foreach (GDictionary settlement in ReadDictionaryItems(existingSettlements))
        {
            Vector2I otherOrigin = GetVector2I(settlement, "origin", Vector2I.Zero);
            Vector2I otherSize = GetVector2I(settlement, "footprint_size", Vector2I.One);
            Vector2 otherCenter =
                new Vector2(otherOrigin.X, otherOrigin.Y)
                + new Vector2(otherSize.X, otherSize.Y) * 0.5f;
            int otherTier = GetInt(settlement, "tier", SettlementConfig.TIER_VILLAGE());
            float requiredDistance = Math.Max(
                minDistanceCells,
                _generationConfig.get_settlement_spacing_cells(otherTier)
            );
            if (candidateCenter.DistanceTo(otherCenter) < requiredDistance)
                return false;
        }
        return true;
    }

    private Vector2I PickMonsterCoordForChunk(
        Vector2I chunkCoord,
        int minDistanceToSettlement,
        WildSpawnPlacementContext placementContext,
        int offsetSeed
    )
    {
        List<Vector2I> candidates = placementContext.GetChunkCandidates(
            chunkCoord,
            minDistanceToSettlement
        );
        if (candidates.Count == 0)
            return new Vector2I(-1, -1);
        int index = PosMod(offsetSeed * 3 + chunkCoord.X + chunkCoord.Y, candidates.Count);
        return candidates[index];
    }

    private static string PopBack(List<string> values)
    {
        int index = values.Count - 1;
        string value = values[index];
        values.RemoveAt(index);
        return value;
    }

    private static int PosMod(long value, int modulus)
    {
        if (modulus <= 0)
            return 0;
        long result = value % modulus;
        if (result < 0)
            result += modulus;
        return (int)result;
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        var chars = new List<char>();
        bool previousUnderscore = false;
        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            if (char.IsWhiteSpace(ch) || ch == '-' || ch == '.')
            {
                if (!previousUnderscore && chars.Count > 0)
                {
                    chars.Add('_');
                    previousUnderscore = true;
                }
                continue;
            }
            if (char.IsUpper(ch) && chars.Count > 0 && !previousUnderscore)
                chars.Add('_');
            chars.Add(char.ToLower(ch, System.Globalization.CultureInfo.GetCultureInfo("")));
            previousUnderscore = ch == '_';
        }
        string result = new string(chars.ToArray()).Trim('_');
        while (result.Contains("__", StringComparison.Ordinal))
            result = result.Replace("__", "_", StringComparison.Ordinal);
        return result;
    }

    private static string GetString(GDictionary dictionary, string key, string fallback = "")
    {
        if (dictionary == null || string.IsNullOrEmpty(key) || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        if (value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName)
            return value.ToString();
        return fallback;
    }

    private static int GetInt(GDictionary dictionary, string key, int fallback = 0)
    {
        if (dictionary == null || string.IsNullOrEmpty(key) || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static bool ReadExactBool(GDictionary dictionary, string key, bool fallback = false)
    {
        if (dictionary == null || string.IsNullOrEmpty(key) || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static Vector2I GetVector2I(GDictionary dictionary, string key, Vector2I fallback)
    {
        if (dictionary == null || string.IsNullOrEmpty(key) || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    private static GArray GetArray(GDictionary dictionary, string key)
    {
        if (dictionary == null || string.IsNullOrEmpty(key) || !dictionary.ContainsKey(key))
            return new GArray();
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        if (values == null)
            yield break;
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Dictionary)
                yield return value.AsGodotDictionary();
        }
    }
}
