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

    private readonly RuntimeRandom _rng = new();
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

    internal sealed class WorldBuildData
    {
        public long MapSeed { get; init; }
        public List<SettlementInstanceData> Settlements { get; } = new();
        public List<WorldNpcInstanceData> WorldNpcs { get; } = new();
        public List<EncounterAnchorData> EncounterAnchors { get; } = new();
        public List<WorldEventInstanceData> WorldEvents { get; } = new();
        public List<MountedSubmapInstanceData> MountedSubmaps { get; } = new();
        public Vector2I PlayerStartCoord { get; init; } = Vector2I.Zero;
        public string PlayerStartSettlementId { get; init; } = "";
        public string PlayerStartSettlementName { get; init; } = "";
    }

    internal sealed class SettlementInstanceData
    {
        public string EntityId { get; init; } = "";
        public string TemplateId { get; init; } = "";
        public string SettlementId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public int Tier { get; init; }
        public string TierName { get; init; } = "";
        public string FactionId { get; init; } = "";
        public Vector2I Origin { get; init; } = Vector2I.Zero;
        public Vector2I FootprintSize { get; init; } = Vector2I.One;
        public List<FacilityInstanceData> Facilities { get; } = new();
        public bool IsPlayerStart { get; init; }
        public SettlementStateData SettlementState { get; init; } = new();
        public List<ServiceEntryData> AvailableServices { get; } = new();
        public List<ServiceNpcInstanceData> ServiceNpcs { get; } = new();
    }

    internal sealed class FacilityInstanceData
    {
        public string TemplateId { get; init; } = "";
        public string FacilityId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Category { get; init; } = "";
        public string InteractionType { get; init; } = "";
        public string SlotId { get; init; } = "";
        public string SlotTag { get; init; } = "";
        public Vector2I LocalCoord { get; init; } = Vector2I.Zero;
        public Vector2I WorldCoord { get; init; } = Vector2I.Zero;
        public string SettlementId { get; init; } = "";
        public List<ServiceNpcInstanceData> ServiceNpcs { get; } = new();
    }

    internal sealed class ServiceNpcInstanceData
    {
        public string TemplateId { get; init; } = "";
        public string NpcId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string ServiceType { get; init; } = "";
        public string InteractionScriptId { get; init; } = "";
        public string LocalSlotId { get; init; } = "";
        public string FacilityId { get; init; } = "";
        public string FacilityTemplateId { get; init; } = "";
        public string FacilityName { get; init; } = "";
        public string SettlementId { get; init; } = "";
    }

    internal sealed class ServiceEntryData
    {
        public string SettlementId { get; init; } = "";
        public string FacilityId { get; init; } = "";
        public string FacilityTemplateId { get; init; } = "";
        public string FacilityName { get; init; } = "";
        public string NpcId { get; init; } = "";
        public string NpcTemplateId { get; init; } = "";
        public string NpcName { get; init; } = "";
        public string ServiceType { get; init; } = "";
        public string ActionId { get; init; } = "";
        public string InteractionScriptId { get; init; } = "";
    }

    internal sealed class SettlementStateData
    {
        public bool Visited { get; init; }
        public int Reputation { get; init; }
        public long ShopInventorySeed { get; init; }
        public int ShopLastRefreshStep { get; init; }
    }

    internal sealed class WorldNpcInstanceData
    {
        public string EntityId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public Vector2I Coord { get; init; } = Vector2I.Zero;
        public string Kind { get; init; } = "";
        public string FactionId { get; init; } = "";
        public int VisionRange { get; init; }
    }

    internal sealed class WorldEventInstanceData
    {
        public string EventId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public Vector2I WorldCoord { get; init; } = Vector2I.Zero;
        public string EventType { get; init; } = "";
        public string TargetSubmapId { get; init; } = "";
        public string DiscoveryConditionId { get; init; } = "";
        public string PromptTitle { get; init; } = "";
        public string PromptText { get; init; } = "";
        public bool IsDiscovered { get; init; }
    }

    internal sealed class MountedSubmapInstanceData
    {
        public string SubmapId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string GenerationConfigPath { get; init; } = "";
        public string ReturnHintText { get; init; } = "";
        public bool IsGenerated { get; init; }
        public Vector2I PlayerCoord { get; init; } = new(-1, -1);
    }

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
                    if (!_gridSystem.IsCellInsideWorld(candidate))
                        continue;
                    if (_gridSystem.GetOccupantRoot(candidate) != "")
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
                        if (_gridSystem.IsCellInsideWorld(blockedCoord))
                            blockedCells.Add(blockedCoord);
                    }
                }
            }
            return blockedCells;
        }
    }

    internal WorldBuildData BuildWorldTyped(
        WorldMapGenerationConfig generation_config,
        WorldMapGridSystem grid_system
    )
    {
        _generationConfig = generation_config;
        _gridSystem = grid_system;
        if (_generationConfig == null || _gridSystem == null)
            return new WorldBuildData();

        _mapSeed = TrueRandomSeedService.GenerateSeed();
        _rng.Reseed(_mapSeed);
        BuildLibraries();

        List<SettlementInstanceData> settlements = GenerateSettlements();
        SettlementInstanceData playerStartSettlement = FindPlayerStartSettlement(settlements);
        Vector2I playerStartCoord = ResolvePlayerStartCoord(playerStartSettlement);
        List<WorldNpcInstanceData> worldNpcs = GenerateWorldNpcs(settlements);
        List<EncounterAnchorData> encounterAnchors = GenerateEncounterAnchors(
            settlements,
            playerStartCoord
        );

        var result = new WorldBuildData
        {
            MapSeed = _mapSeed,
            PlayerStartCoord = playerStartCoord,
            PlayerStartSettlementId = playerStartSettlement?.SettlementId ?? "",
            PlayerStartSettlementName = playerStartSettlement?.DisplayName ?? "",
        };
        result.Settlements.AddRange(settlements);
        result.WorldNpcs.AddRange(worldNpcs);
        result.EncounterAnchors.AddRange(encounterAnchors);
        result.WorldEvents.AddRange(GenerateWorldEvents());
        result.MountedSubmaps.AddRange(GenerateMountedSubmaps());
        return result;
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

    private List<SettlementInstanceData> GenerateSettlements()
    {
        return _generationConfig.procedural_generation_enabled
            ? GenerateProceduralSettlements()
            : GenerateFixedSettlements();
    }

    private List<SettlementInstanceData> GenerateFixedSettlements()
    {
        var settlements = new List<SettlementInstanceData>();
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
            SettlementInstanceData settlement = CreateSettlementInstance(
                settlementConfig,
                distributionRule.preferred_origin,
                distributionRule.faction_id,
                instanceCounts,
                false
            );
            if (settlement != null)
                settlements.Add(settlement);
        }
        return settlements;
    }

    private List<SettlementInstanceData> GenerateProceduralSettlements()
    {
        var settlements = new List<SettlementInstanceData>();
        var instanceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        Dictionary<int, List<SettlementConfig>> templatesByTier = BuildSettlementTemplatesByTier();
        SettlementConfig playerVillageTemplate = PickSettlementTemplateForTier(
            templatesByTier,
            (int)SettlementConfig.SettlementTier.VILLAGE,
            0
        );
        if (playerVillageTemplate != null)
        {
            Vector2I playerOrigin = GetCenteredOrigin(playerVillageTemplate.GetFootprintSize());
            SettlementInstanceData playerSettlement = CreateSettlementInstance(
                playerVillageTemplate,
                playerOrigin,
                "player",
                instanceCounts,
                true
            );
            if (playerSettlement != null)
                settlements.Add(playerSettlement);
        }

        int[] generationOrder =
        {
            (int)SettlementConfig.SettlementTier.METROPOLIS,
            (int)SettlementConfig.SettlementTier.WORLD_STRONGHOLD,
            (int)SettlementConfig.SettlementTier.CAPITAL,
            (int)SettlementConfig.SettlementTier.CITY,
            (int)SettlementConfig.SettlementTier.TOWN,
            (int)SettlementConfig.SettlementTier.VILLAGE,
        };
        foreach (int tier in generationOrder)
        {
            int targetCount = _generationConfig.GetTargetSettlementCount(tier);
            if (tier == (int)SettlementConfig.SettlementTier.VILLAGE && settlements.Count > 0)
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
                    settlementTemplate.GetFootprintSize(),
                    settlements,
                    _generationConfig.GetSettlementSpacingCells(tier)
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
                SettlementInstanceData settlement = CreateSettlementInstance(
                    settlementTemplate,
                    origin,
                    "neutral",
                    instanceCounts,
                    false
                );
                if (settlement != null)
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

    private SettlementInstanceData CreateSettlementInstance(
        SettlementConfig settlementConfig,
        Vector2I origin,
        string factionId,
        Dictionary<string, int> instanceCounts,
        bool isPlayerStart
    )
    {
        Vector2I footprintSize = settlementConfig.GetFootprintSize();
        if (!_gridSystem.CanPlaceFootprint(origin, footprintSize))
        {
            GameLog.Error(
                $"Invalid settlement placement for {GetSettlementTemplateId(settlementConfig)} at {origin}",
                "world.spawn.invalid_placement",
                "world"
            );
            return null;
        }
        string templateId = GetSettlementTemplateId(settlementConfig);
        if (templateId.Length == 0)
        {
            GameLog.Error($"Settlement template is missing template_id for placement at {origin}.", "world.spawn.settlement_missing_id", "world");
            return null;
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
        _gridSystem.RegisterFootprint(entityId, origin, footprintSize);

        List<FacilityInstanceData> facilities = GenerateFacilitiesForSettlement(
            settlementId,
            settlementConfig,
            origin
        );
        var settlement = new SettlementInstanceData
        {
            EntityId = entityId,
            TemplateId = templateId,
            SettlementId = settlementId,
            DisplayName = displayName,
            Tier = settlementConfig.tier,
            TierName = settlementConfig.GetTierName(),
            FactionId = factionId,
            Origin = origin,
            FootprintSize = footprintSize,
            IsPlayerStart = isPlayerStart,
            SettlementState = BuildDefaultSettlementState(isPlayerStart),
        };
        settlement.Facilities.AddRange(facilities);
        settlement.AvailableServices.AddRange(CollectServices(settlementId, facilities));
        settlement.ServiceNpcs.AddRange(CollectServiceNpcs(facilities));
        return settlement;
    }

    private List<FacilityInstanceData> GenerateFacilitiesForSettlement(
        string settlementId,
        SettlementConfig settlementConfig,
        Vector2I settlementOrigin
    )
    {
        var generatedFacilities = new List<FacilityInstanceData>();
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
            FacilityInstanceData placedFacility = TryPlaceFacility(
                settlementId,
                facilityConfig,
                settlementConfig,
                settlementOrigin,
                usedSlotIds
            );
            if (placedFacility != null)
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
            FacilityInstanceData placedFacility = TryPlaceFacility(
                settlementId,
                facilityConfig,
                settlementConfig,
                settlementOrigin,
                usedSlotIds
            );
            if (placedFacility == null)
                continue;
            generatedFacilities.Add(placedFacility);
            RemoveWeightedEntry(optionalPool, selectedFacilityTemplateId);
        }
        return generatedFacilities;
    }

    private FacilityInstanceData TryPlaceFacility(
        string settlementId,
        FacilityConfig facilityConfig,
        SettlementConfig settlementConfig,
        Vector2I settlementOrigin,
        HashSet<string> usedSlotIds
    )
    {
        if (facilityConfig.min_settlement_tier > settlementConfig.tier)
            return null;
        string facilityTemplateId = GetFacilityTemplateId(facilityConfig);
        if (facilityTemplateId.Length == 0)
            return null;
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
            var serviceNpcs = new List<ServiceNpcInstanceData>();
            int npcIndex = 0;
            foreach (Resource npcResource in facilityConfig.bound_service_npcs)
            {
                var npcConfig = npcResource as FacilityNpcConfig;
                string npcTemplateId = GetNpcTemplateId(npcConfig);
                if (npcTemplateId.Length == 0)
                    continue;
                serviceNpcs.Add(
                    new ServiceNpcInstanceData
                    {
                        TemplateId = npcTemplateId,
                        NpcId = BuildNpcInstanceId(
                            facilityId,
                            npcTemplateId,
                            npcConfig.local_slot_id,
                            npcIndex
                        ),
                        DisplayName = npcConfig.display_name,
                        ServiceType = npcConfig.service_type,
                        InteractionScriptId = npcConfig.interaction_script_id,
                        LocalSlotId = npcConfig.local_slot_id,
                        FacilityId = facilityId,
                        FacilityTemplateId = facilityTemplateId,
                        FacilityName = facilityConfig.display_name,
                        SettlementId = settlementId,
                    }
                );
                npcIndex++;
            }
            var facility = new FacilityInstanceData
            {
                TemplateId = facilityTemplateId,
                FacilityId = facilityId,
                DisplayName = facilityConfig.display_name,
                Category = facilityConfig.category,
                InteractionType = facilityConfig.interaction_type,
                SlotId = slotConfig.slot_id,
                SlotTag = slotConfig.slot_tag,
                LocalCoord = slotConfig.local_coord,
                WorldCoord = settlementOrigin + slotConfig.local_coord,
                SettlementId = settlementId,
            };
            facility.ServiceNpcs.AddRange(serviceNpcs);
            return facility;
        }
        return null;
    }

    private List<ServiceEntryData> CollectServices(
        string settlementId,
        IReadOnlyList<FacilityInstanceData> facilities
    )
    {
        var services = new List<ServiceEntryData>();
        bool hasPartyWarehouseService = false;
        foreach (FacilityInstanceData facility in facilities)
        {
            if (facility == null)
                continue;
            foreach (ServiceNpcInstanceData npc in facility.ServiceNpcs)
            {
                if (npc == null)
                    continue;
                string interactionScriptId = npc.InteractionScriptId;
                if (interactionScriptId == "party_warehouse")
                    hasPartyWarehouseService = true;
                services.Add(
                    new ServiceEntryData
                    {
                        SettlementId = settlementId,
                        FacilityId = facility.FacilityId,
                        FacilityTemplateId = facility.TemplateId,
                        FacilityName = facility.DisplayName,
                        NpcId = npc.NpcId,
                        NpcTemplateId = npc.TemplateId,
                        NpcName = npc.DisplayName,
                        ServiceType = npc.ServiceType,
                        ActionId = BuildServiceActionId(npc.ServiceType, interactionScriptId),
                        InteractionScriptId = interactionScriptId,
                    }
                );
            }
        }
        if (!hasPartyWarehouseService)
        {
            services.Add(
                new ServiceEntryData
                {
                    SettlementId = settlementId,
                    FacilityId = $"{settlementId}__settlement_service_desk",
                    FacilityTemplateId = "",
                    FacilityName = "据点服务台",
                    NpcId = $"{settlementId}__settlement_quartermaster",
                    NpcTemplateId = "",
                    NpcName = "军需官",
                    ServiceType = "仓储",
                    ActionId = ServiceActionIdByInteraction["party_warehouse"],
                    InteractionScriptId = "party_warehouse",
                }
            );
        }
        return services;
    }

    private static List<ServiceNpcInstanceData> CollectServiceNpcs(
        IReadOnlyList<FacilityInstanceData> facilities
    )
    {
        var serviceNpcs = new List<ServiceNpcInstanceData>();
        foreach (FacilityInstanceData facility in facilities)
        {
            if (facility != null)
                serviceNpcs.AddRange(facility.ServiceNpcs);
        }
        return serviceNpcs;
    }

    private static SettlementStateData BuildDefaultSettlementState(bool isPlayerStart)
    {
        return new SettlementStateData
        {
            Visited = isPlayerStart,
            Reputation = 0,
            ShopInventorySeed = TrueRandomSeedService.GenerateSeed(),
            ShopLastRefreshStep = 0,
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
            : (settlementConfig.GetTemplateId() ?? "").StripEdges();
    }

    private static string GetDistributionRuleTemplateId(SettlementDistributionRule distributionRule)
    {
        return distributionRule == null
            ? ""
            : (distributionRule.GetSettlementTemplateId() ?? "").StripEdges();
    }

    private static string GetFacilityTemplateId(FacilityConfig facilityConfig)
    {
        return facilityConfig == null ? "" : (facilityConfig.GetTemplateId() ?? "").StripEdges();
    }

    private static string GetNpcTemplateId(FacilityNpcConfig npcConfig)
    {
        return npcConfig == null ? "" : (npcConfig.GetTemplateId() ?? "").StripEdges();
    }

    private static string GetWeightedFacilityTemplateId(WeightedFacilityEntry weightedEntry)
    {
        return weightedEntry == null
            ? ""
            : (weightedEntry.GetFacilityTemplateId() ?? "").StripEdges();
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

    private List<WorldNpcInstanceData> GenerateWorldNpcs(
        IReadOnlyList<SettlementInstanceData> settlements
    )
    {
        var worldNpcs = new List<WorldNpcInstanceData>();
        string[] npcNames = { "巡路信使", "驿站商人", "边地向导", "地图学者", "补给联络员" };
        int nameIndex = 0;
        foreach (SettlementInstanceData settlement in settlements)
        {
            if (settlement == null)
                continue;
            Vector2I origin = settlement.Origin;
            Vector2I footprintSize = settlement.FootprintSize;
            Vector2I spawnCoord = FindFreeCoordNear(origin + footprintSize - Vector2I.One);
            if (spawnCoord == new Vector2I(-1, -1))
                continue;
            string npcName = npcNames[nameIndex % npcNames.Length];
            nameIndex++;
            worldNpcs.Add(
                new WorldNpcInstanceData
                {
                    EntityId = $"world_npc_{nameIndex}",
                    DisplayName = npcName,
                    Coord = spawnCoord,
                    Kind = "service_hint",
                    FactionId = settlement.FactionId,
                    VisionRange = 1,
                }
            );
        }
        return worldNpcs;
    }

    private List<EncounterAnchorData> GenerateEncounterAnchors(
        IReadOnlyList<SettlementInstanceData> settlements,
        Vector2I playerStartCoord
    )
    {
        var settlementCells = new List<Vector2I>();
        foreach (SettlementInstanceData settlement in settlements)
        {
            if (settlement == null)
                continue;
            Vector2I origin = settlement.Origin;
            Vector2I footprintSize = settlement.FootprintSize;
            for (int y = 0; y < footprintSize.Y; y++)
            for (int x = 0; x < footprintSize.X; x++)
                settlementCells.Add(origin + new Vector2I(x, y));
        }

        var placementContext = new WildSpawnPlacementContext(
            _gridSystem,
            _generationConfig.chunk_size,
            settlementCells
        );

        List<EncounterAnchorData> encounterAnchors;
        if (_generationConfig.procedural_generation_enabled)
        {
            encounterAnchors = GenerateProceduralEncounterAnchors(placementContext);
        }
        else
        {
            encounterAnchors = new List<EncounterAnchorData>();
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

    private List<EncounterAnchorData> GenerateProceduralEncounterAnchors(
        WildSpawnPlacementContext placementContext
    )
    {
        var encounterAnchors = new List<EncounterAnchorData>();
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
                int chunkSeed = (int)TrueRandomSeedService.GenerateSeed();
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
        List<EncounterAnchorData> encounterAnchors,
        WildSpawnPlacementContext placementContext,
        Vector2I playerStartCoord
    )
    {
        if (!_generationConfig.guarantee_starting_wild_encounter)
            return;
        if (!_gridSystem.IsCellInsideWorld(playerStartCoord))
            return;
        if (_resolvedWildSpawnRules.Count == 0)
            return;
        WildSpawnRule rule = _resolvedWildSpawnRules[0];
        if (_generationConfig.procedural_generation_enabled)
        {
            Vector2I playerChunkCoord = _gridSystem.GetChunkCoord(playerStartCoord);
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
        IEnumerable<EncounterAnchorData> encounterAnchors,
        Vector2I playerStartCoord,
        int maxDistance
    )
    {
        foreach (EncounterAnchorData encounterAnchor in encounterAnchors)
        {
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
        IReadOnlyList<EncounterAnchorData> encounterAnchors,
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
                if (!_gridSystem.IsCellInsideWorld(candidate))
                    continue;
                if (_gridSystem.GetOccupantRoot(candidate) != "")
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

    private static bool HasEncounterAnchorAt(
        IEnumerable<EncounterAnchorData> encounterAnchors,
        Vector2I coord
    )
    {
        foreach (EncounterAnchorData encounterAnchor in encounterAnchors)
        {
            if (encounterAnchor != null && encounterAnchor.world_coord == coord)
                return true;
        }
        return false;
    }

    private void EnsureDefaultSettlementEncounter(
        List<EncounterAnchorData> encounterAnchors,
        WildSpawnPlacementContext placementContext
    )
    {
        foreach (EncounterAnchorData existingAnchor in encounterAnchors)
        {
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
                    (int)TrueRandomSeedService.GenerateSeed()
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
        if (settlementBundle != null)
            GodotContentOwnership.RegisterBorrowedContent(
                settlementBundle,
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
        if (wildSpawnBundle != null)
            GodotContentOwnership.RegisterBorrowedContent(
                wildSpawnBundle,
                DefaultMainWorldWildSpawnBundlePath
            );
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
        var uniqueNames = new List<string>(namePool.BuildUniqueDisplayNames());
        if (uniqueNames.Count == 0)
            return uniqueNames;
        var nameRng = new RuntimeRandom(TrueRandomSeedService.GenerateSeed());
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
        if (namePool != null)
            GodotContentOwnership.RegisterBorrowedContent(namePool, resourcePath);
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

    private List<WorldEventInstanceData> GenerateWorldEvents()
    {
        var generatedEvents = new List<WorldEventInstanceData>();
        foreach (Resource eventResource in _generationConfig.world_events)
        {
            var eventConfig = eventResource as WorldEventConfig;
            if (eventConfig == null || eventConfig.event_id == new StringName(""))
                continue;
            generatedEvents.Add(
                new WorldEventInstanceData
                {
                    EventId = eventConfig.event_id.ToString(),
                    DisplayName = eventConfig.display_name,
                    WorldCoord = eventConfig.world_coord,
                    EventType = eventConfig.event_type.ToString(),
                    TargetSubmapId = eventConfig.target_submap_id.ToString(),
                    DiscoveryConditionId = eventConfig.discovery_condition_id.ToString(),
                    PromptTitle = eventConfig.prompt_title,
                    PromptText = eventConfig.prompt_text,
                    IsDiscovered = IsWorldEventDiscoveredByDefault(eventConfig),
                }
            );
        }
        return generatedEvents;
    }

    private List<MountedSubmapInstanceData> GenerateMountedSubmaps()
    {
        var mountedSubmaps = new List<MountedSubmapInstanceData>();
        foreach (Resource submapResource in _generationConfig.mounted_submaps)
        {
            var submapConfig = submapResource as MountedSubmapConfig;
            if (submapConfig == null || submapConfig.submap_id == new StringName(""))
                continue;
            mountedSubmaps.Add(
                new MountedSubmapInstanceData
                {
                    SubmapId = submapConfig.submap_id.ToString(),
                    DisplayName = submapConfig.display_name,
                    GenerationConfigPath = submapConfig.generation_config_path,
                    ReturnHintText = submapConfig.return_hint_text,
                    IsGenerated = false,
                    PlayerCoord = new Vector2I(-1, -1),
                }
            );
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
            if (!_gridSystem.IsCellInsideWorld(candidate))
                continue;
            if (_gridSystem.GetOccupantRoot(candidate) != "")
                continue;
            return candidate;
        }
        return new Vector2I(-1, -1);
    }

    private static SettlementInstanceData FindPlayerStartSettlement(
        IReadOnlyList<SettlementInstanceData> settlements
    )
    {
        foreach (SettlementInstanceData settlement in settlements)
        {
            if (settlement != null && settlement.IsPlayerStart)
                return settlement;
        }
        foreach (SettlementInstanceData settlement in settlements)
        {
            if (
                settlement != null
                && settlement.Tier == (int)SettlementConfig.SettlementTier.VILLAGE
            )
                return settlement;
        }
        return null;
    }

    private Vector2I ResolvePlayerStartCoord(SettlementInstanceData playerStartSettlement)
    {
        return playerStartSettlement == null ? _generationConfig.player_start_coord : playerStartSettlement.Origin;
    }

    private Vector2I GetCenteredOrigin(Vector2I footprintSize)
    {
        Vector2I worldSize = _generationConfig.GetWorldSizeCells();
        int maxX = Math.Max(worldSize.X - footprintSize.X, 0);
        int maxY = Math.Max(worldSize.Y - footprintSize.Y, 0);
        return new Vector2I(Mathf.Clamp(maxX / 2, 0, maxX), Mathf.Clamp(maxY / 2, 0, maxY));
    }

    private Vector2I FindProceduralOrigin(
        Vector2I footprintSize,
        IReadOnlyList<SettlementInstanceData> existingSettlements,
        int minDistanceCells
    )
    {
        Vector2I worldSize = _generationConfig.GetWorldSizeCells();
        int maxX = worldSize.X - footprintSize.X;
        int maxY = worldSize.Y - footprintSize.Y;
        if (maxX < 0 || maxY < 0)
            return new Vector2I(-1, -1);
        for (int attempt = 0; attempt < 192; attempt++)
        {
            var origin = new Vector2I(_rng.RandiRange(0, maxX), _rng.RandiRange(0, maxY));
            if (!_gridSystem.CanPlaceFootprint(origin, footprintSize))
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
        IReadOnlyList<SettlementInstanceData> existingSettlements,
        int minDistanceCells
    )
    {
        Vector2 candidateCenter =
            new Vector2(candidateOrigin.X, candidateOrigin.Y)
            + new Vector2(candidateSize.X, candidateSize.Y) * 0.5f;
        foreach (SettlementInstanceData settlement in existingSettlements)
        {
            if (settlement == null)
                continue;
            Vector2I otherOrigin = settlement.Origin;
            Vector2I otherSize = settlement.FootprintSize;
            Vector2 otherCenter =
                new Vector2(otherOrigin.X, otherOrigin.Y)
                + new Vector2(otherSize.X, otherSize.Y) * 0.5f;
            int otherTier = settlement.Tier;
            float requiredDistance = Math.Max(
                minDistanceCells,
                _generationConfig.GetSettlementSpacingCells(otherTier)
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

}
