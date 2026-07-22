using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public sealed class WorldMapSpawnSystem
{
    private const string EncounterKindSingle = "single";
    private const string EncounterKindSettlement = "settlement";
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
        ["npc_village_chief"] = "service:npc_village_chief",
        ["npc_village_healer"] = "service:npc_village_healer",
        ["npc_old_hunter"] = "service:npc_old_hunter",
    };

    private readonly RuntimeRandom _rng = new();
    private long _mapSeed;
    private WorldGenerationDefinition _generationDefinition;
    private WorldMapGridSystem _gridSystem;
    private readonly Dictionary<string, FacilityDefinition> _facilityLibraryById = new(
        StringComparer.Ordinal
    );
    private readonly Dictionary<string, SettlementDefinition> _settlementLibraryById = new(
        StringComparer.Ordinal
    );
    private readonly List<FacilityDefinition> _resolvedFacilityLibrary = new();
    private readonly List<SettlementDefinition> _resolvedSettlementLibrary = new();
    private readonly List<WildSpawnRuleDefinition> _resolvedWildSpawnRules = new();
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
        public List<WorldMapResourceNodeData> ResourceNodes { get; } = new();
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
        WorldGenerationDefinition generationDefinition,
        WorldMapGridSystem grid_system
    )
    {
        _generationDefinition = generationDefinition;
        _gridSystem = grid_system;
        if (_generationDefinition == null || _gridSystem == null)
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
        List<WorldEventInstanceData> worldEvents = GenerateWorldEvents();
        List<WorldMapResourceNodeData> resourceNodes = GenerateResourceNodes(
            settlements,
            worldNpcs,
            encounterAnchors,
            worldEvents,
            playerStartSettlement,
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
        result.ResourceNodes.AddRange(resourceNodes);
        result.WorldEvents.AddRange(worldEvents);
        result.MountedSubmaps.AddRange(GenerateMountedSubmaps());
        return result;
    }

    private void BuildLibraries()
    {
        _facilityLibraryById.Clear();
        _settlementLibraryById.Clear();
        _remainingDefaultMainWorldSettlementDisplayNames =
            BuildDefaultMainWorldSettlementDisplayNames();
        _remainingDefaultMainWorldTownDisplayNames = BuildDefaultMainWorldTownDisplayNames();
        _remainingDefaultMainWorldCityDisplayNames = BuildDefaultMainWorldCityDisplayNames();
        _remainingDefaultMainWorldCapitalDisplayNames = BuildDefaultMainWorldCapitalDisplayNames();
        _remainingDefaultMainWorldMetropolisDisplayNames =
            BuildDefaultMainWorldMetropolisDisplayNames();

        _resolvedFacilityLibrary.Clear();
        _resolvedFacilityLibrary.AddRange(_generationDefinition.EffectiveFacilityLibrary);
        _resolvedSettlementLibrary.Clear();
        _resolvedSettlementLibrary.AddRange(_generationDefinition.EffectiveSettlementLibrary);
        _resolvedWildSpawnRules.Clear();
        _resolvedWildSpawnRules.AddRange(_generationDefinition.EffectiveWildSpawnRules);

        foreach (FacilityDefinition facilityDefinition in _resolvedFacilityLibrary)
        {
            string facilityTemplateId = GetFacilityTemplateId(facilityDefinition);
            if (facilityTemplateId.Length == 0)
                continue;
            _facilityLibraryById[facilityTemplateId] = facilityDefinition;
        }
        foreach (SettlementDefinition settlementDefinition in _resolvedSettlementLibrary)
        {
            string settlementTemplateId = GetSettlementTemplateId(settlementDefinition);
            if (settlementTemplateId.Length == 0)
                continue;
            _settlementLibraryById[settlementTemplateId] = settlementDefinition;
        }
    }

    private List<SettlementInstanceData> GenerateSettlements()
    {
        return _generationDefinition.ProceduralGenerationEnabled
            ? GenerateProceduralSettlements()
            : GenerateFixedSettlements();
    }

    private List<SettlementInstanceData> GenerateFixedSettlements()
    {
        var settlements = new List<SettlementInstanceData>();
        var instanceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (
            SettlementDistributionDefinition distributionRule in _generationDefinition.SettlementDistribution
        )
        {
            string settlementTemplateId = GetDistributionRuleTemplateId(distributionRule);
            if (
                !_settlementLibraryById.TryGetValue(
                    settlementTemplateId,
                    out SettlementDefinition settlementDefinition
                )
            )
                continue;
            SettlementInstanceData settlement = CreateSettlementInstance(
                settlementDefinition,
                distributionRule.PreferredOrigin,
                distributionRule.FactionId,
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
        Dictionary<int, List<SettlementDefinition>> templatesByTier =
            BuildSettlementTemplatesByTier();
        SettlementDefinition playerVillageTemplate = PickSettlementTemplateForTier(
            templatesByTier,
            (int)SettlementTierKind.Village,
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
            (int)SettlementTierKind.Metropolis,
            (int)SettlementTierKind.WorldStronghold,
            (int)SettlementTierKind.Capital,
            (int)SettlementTierKind.City,
            (int)SettlementTierKind.Town,
            (int)SettlementTierKind.Village,
        };
        foreach (int tier in generationOrder)
        {
            int targetCount = _generationDefinition.GetTargetSettlementCount(tier);
            if (tier == (int)SettlementTierKind.Village && settlements.Count > 0)
                targetCount = Math.Max(targetCount - 1, 0);
            for (int tierIndex = 0; tierIndex < targetCount; tierIndex++)
            {
                SettlementDefinition settlementTemplate = PickSettlementTemplateForTier(
                    templatesByTier,
                    tier,
                    tierIndex
                );
                if (settlementTemplate == null)
                    break;
                Vector2I origin = FindProceduralOrigin(
                    settlementTemplate.GetFootprintSize(),
                    settlements,
                    _generationDefinition.GetSettlementSpacingCells(tier)
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

    private Dictionary<int, List<SettlementDefinition>> BuildSettlementTemplatesByTier()
    {
        var templatesByTier = new Dictionary<int, List<SettlementDefinition>>();
        foreach (SettlementDefinition settlementDefinition in _resolvedSettlementLibrary)
        {
            int tier = settlementDefinition.Tier;
            if (!templatesByTier.ContainsKey(tier))
                templatesByTier[tier] = new List<SettlementDefinition>();
            templatesByTier[tier].Add(settlementDefinition);
        }
        return templatesByTier;
    }

    private static SettlementDefinition PickSettlementTemplateForTier(
        Dictionary<int, List<SettlementDefinition>> templatesByTier,
        int tier,
        int index
    )
    {
        if (
            !templatesByTier.TryGetValue(tier, out List<SettlementDefinition> tierTemplates)
            || tierTemplates.Count == 0
        )
            return null;
        return tierTemplates[index % tierTemplates.Count];
    }

    private SettlementInstanceData CreateSettlementInstance(
        SettlementDefinition settlementDefinition,
        Vector2I origin,
        string factionId,
        Dictionary<string, int> instanceCounts,
        bool isPlayerStart
    )
    {
        Vector2I footprintSize = settlementDefinition.FootprintSize;
        if (!_gridSystem.CanPlaceFootprint(origin, footprintSize))
        {
            GameLog.Error(
                $"Invalid settlement placement for {GetSettlementTemplateId(settlementDefinition)} at {origin}",
                "world.spawn.invalid_placement",
                "world"
            );
            return null;
        }
        string templateId = GetSettlementTemplateId(settlementDefinition);
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
            settlementDefinition,
            templateId,
            instanceIndex
        );
        string entityId = $"settlement_{settlementId}";
        _gridSystem.RegisterFootprint(entityId, origin, footprintSize);

        List<FacilityInstanceData> facilities = GenerateFacilitiesForSettlement(
            settlementId,
            settlementDefinition,
            origin
        );
        var settlement = new SettlementInstanceData
        {
            EntityId = entityId,
            TemplateId = templateId,
            SettlementId = settlementId,
            DisplayName = displayName,
            Tier = settlementDefinition.Tier,
            TierName = settlementDefinition.TierName,
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
        SettlementDefinition settlementDefinition,
        Vector2I settlementOrigin
    )
    {
        var generatedFacilities = new List<FacilityInstanceData>();
        var usedSlotIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string facilityTemplateId in settlementDefinition.GuaranteedFacilityIds)
        {
            if (
                !_facilityLibraryById.TryGetValue(
                    facilityTemplateId,
                    out FacilityDefinition facilityDefinition
                )
            )
                continue;
            FacilityInstanceData placedFacility = TryPlaceFacility(
                settlementId,
                facilityDefinition,
                settlementDefinition,
                settlementOrigin,
                usedSlotIds
            );
            if (placedFacility != null)
                generatedFacilities.Add(placedFacility);
        }
        int optionalLimit = Math.Min(
            settlementDefinition.MaxOptionalFacilities,
            Math.Max(settlementDefinition.FacilitySlots.Count - generatedFacilities.Count, 0)
        );
        var optionalPool = new List<WeightedFacilityDefinition>(
            settlementDefinition.OptionalFacilityPool
        );
        for (int optionalIndex = 0; optionalIndex < optionalLimit; optionalIndex++)
        {
            string selectedFacilityTemplateId = PickWeightedFacility(optionalPool);
            if (selectedFacilityTemplateId.Length == 0)
                break;
            if (
                !_facilityLibraryById.TryGetValue(
                    selectedFacilityTemplateId,
                    out FacilityDefinition facilityDefinition
                )
            )
                continue;
            FacilityInstanceData placedFacility = TryPlaceFacility(
                settlementId,
                facilityDefinition,
                settlementDefinition,
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
        FacilityDefinition facilityDefinition,
        SettlementDefinition settlementDefinition,
        Vector2I settlementOrigin,
        HashSet<string> usedSlotIds
    )
    {
        if (facilityDefinition.MinSettlementTier > settlementDefinition.Tier)
            return null;
        string facilityTemplateId = GetFacilityTemplateId(facilityDefinition);
        if (facilityTemplateId.Length == 0)
            return null;
        foreach (FacilitySlotDefinition slotDefinition in settlementDefinition.FacilitySlots)
        {
            if (slotDefinition == null)
                continue;
            if (usedSlotIds.Contains(slotDefinition.SlotId))
                continue;
            if (
                facilityDefinition.AllowedSlotTags.Count > 0
                && !facilityDefinition.AllowedSlotTags.Contains(slotDefinition.SlotTag)
            )
                continue;
            usedSlotIds.Add(slotDefinition.SlotId);
            string facilityId = BuildFacilityInstanceId(
                settlementId,
                facilityTemplateId,
                slotDefinition.SlotId
            );
            var serviceNpcs = new List<ServiceNpcInstanceData>();
            int npcIndex = 0;
            foreach (FacilityNpcDefinition npcDefinition in facilityDefinition.BoundServiceNpcs)
            {
                string npcTemplateId = GetNpcTemplateId(npcDefinition);
                if (npcTemplateId.Length == 0)
                    continue;
                serviceNpcs.Add(
                    new ServiceNpcInstanceData
                    {
                        TemplateId = npcTemplateId,
                        NpcId = BuildNpcInstanceId(
                            facilityId,
                            npcTemplateId,
                            npcDefinition.LocalSlotId,
                            npcIndex
                        ),
                        DisplayName = npcDefinition.DisplayName,
                        ServiceType = npcDefinition.ServiceType,
                        InteractionScriptId = npcDefinition.InteractionScriptId,
                        LocalSlotId = npcDefinition.LocalSlotId,
                        FacilityId = facilityId,
                        FacilityTemplateId = facilityTemplateId,
                        FacilityName = facilityDefinition.DisplayName,
                        SettlementId = settlementId,
                    }
                );
                npcIndex++;
            }
            var facility = new FacilityInstanceData
            {
                TemplateId = facilityTemplateId,
                FacilityId = facilityId,
                DisplayName = facilityDefinition.DisplayName,
                Category = facilityDefinition.Category,
                InteractionType = facilityDefinition.InteractionType,
                SlotId = slotDefinition.SlotId,
                SlotTag = slotDefinition.SlotTag,
                LocalCoord = slotDefinition.LocalCoord,
                WorldCoord = settlementOrigin + slotDefinition.LocalCoord,
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
        foreach (FacilityInstanceData facility in facilities)
        {
            if (facility == null)
                continue;
            foreach (ServiceNpcInstanceData npc in facility.ServiceNpcs)
            {
                if (npc == null)
                    continue;
                string interactionScriptId = npc.InteractionScriptId;
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

    private static string GetSettlementTemplateId(SettlementDefinition settlementDefinition)
    {
        return settlementDefinition == null
            ? ""
            : (settlementDefinition.TemplateId ?? "").StripEdges();
    }

    private static string GetDistributionRuleTemplateId(
        SettlementDistributionDefinition distributionRule
    )
    {
        return distributionRule == null
            ? ""
            : (distributionRule.SettlementTemplateId ?? "").StripEdges();
    }

    private static string GetFacilityTemplateId(FacilityDefinition facilityDefinition)
    {
        return facilityDefinition == null
            ? ""
            : (facilityDefinition.TemplateId ?? "").StripEdges();
    }

    private static string GetNpcTemplateId(FacilityNpcDefinition npcDefinition)
    {
        return npcDefinition == null ? "" : (npcDefinition.TemplateId ?? "").StripEdges();
    }

    private static string GetWeightedFacilityTemplateId(
        WeightedFacilityDefinition weightedEntry
    )
    {
        return weightedEntry == null
            ? ""
            : (weightedEntry.FacilityTemplateId ?? "").StripEdges();
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

    private string PickWeightedFacility(List<WeightedFacilityDefinition> optionalPool)
    {
        if (optionalPool.Count == 0)
            return "";
        int totalWeight = 0;
        foreach (WeightedFacilityDefinition entry in optionalPool)
            totalWeight += Math.Max(entry.Weight, 0);
        if (totalWeight <= 0)
            return "";
        int roll = _rng.RandiRange(1, totalWeight);
        int cursor = 0;
        foreach (WeightedFacilityDefinition entry in optionalPool)
        {
            cursor += Math.Max(entry.Weight, 0);
            if (roll <= cursor)
                return GetWeightedFacilityTemplateId(entry);
        }
        return GetWeightedFacilityTemplateId(optionalPool[0]);
    }

    private static void RemoveWeightedEntry(
        List<WeightedFacilityDefinition> optionalPool,
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

    private List<WorldMapResourceNodeData> GenerateResourceNodes(
        IReadOnlyList<SettlementInstanceData> settlements,
        IReadOnlyList<WorldNpcInstanceData> worldNpcs,
        IReadOnlyList<EncounterAnchorData> encounterAnchors,
        IReadOnlyList<WorldEventInstanceData> worldEvents,
        SettlementInstanceData playerStartSettlement,
        Vector2I playerStartCoord
    )
    {
        var resourceNodes = new List<WorldMapResourceNodeData>();
        var reservedCoords = BuildResourceReservedCoords(worldNpcs, encounterAnchors, worldEvents);
        int nextIndex = 1;

        if (playerStartSettlement != null)
        {
            TryAddSettlementResourceNode(
                resourceNodes,
                reservedCoords,
                playerStartSettlement,
                WorldMapResourceNodeData.KindHerbGarden,
                ref nextIndex,
                1,
                6
            );
            TryAddSettlementResourceNode(
                resourceNodes,
                reservedCoords,
                playerStartSettlement,
                WorldMapResourceNodeData.KindFarm,
                ref nextIndex,
                1,
                5
            );
        }

        foreach (SettlementInstanceData settlement in settlements)
        {
            if (settlement == null)
                continue;
            int farmCount = GetSettlementFarmTargetCount(settlement);
            int herbGardenCount = GetSettlementHerbGardenTargetCount(settlement);
            if (ReferenceEquals(settlement, playerStartSettlement))
            {
                farmCount = Math.Max(farmCount - 1, 0);
                herbGardenCount = Math.Max(herbGardenCount - 1, 0);
            }
            for (int index = 0; index < farmCount; index++)
            {
                TryAddSettlementResourceNode(
                    resourceNodes,
                    reservedCoords,
                    settlement,
                    WorldMapResourceNodeData.KindFarm,
                    ref nextIndex,
                    1,
                    6
                );
            }
            for (int index = 0; index < herbGardenCount; index++)
            {
                TryAddSettlementResourceNode(
                    resourceNodes,
                    reservedCoords,
                    settlement,
                    WorldMapResourceNodeData.KindHerbGarden,
                    ref nextIndex,
                    2,
                    8
                );
            }
        }

        PlaceMineResourceNodes(
            resourceNodes,
            reservedCoords,
            settlements,
            playerStartCoord,
            ref nextIndex
        );
        return resourceNodes;
    }

    private HashSet<Vector2I> BuildResourceReservedCoords(
        IReadOnlyList<WorldNpcInstanceData> worldNpcs,
        IReadOnlyList<EncounterAnchorData> encounterAnchors,
        IReadOnlyList<WorldEventInstanceData> worldEvents
    )
    {
        var reservedCoords = new HashSet<Vector2I>();
        if (worldNpcs != null)
        {
            foreach (WorldNpcInstanceData npc in worldNpcs)
                if (npc != null)
                    reservedCoords.Add(npc.Coord);
        }
        if (encounterAnchors != null)
        {
            foreach (EncounterAnchorData encounterAnchor in encounterAnchors)
                if (encounterAnchor != null)
                    reservedCoords.Add(encounterAnchor.world_coord);
        }
        if (worldEvents != null)
        {
            foreach (WorldEventInstanceData worldEvent in worldEvents)
                if (worldEvent != null)
                    reservedCoords.Add(worldEvent.WorldCoord);
        }
        return reservedCoords;
    }

    private bool TryAddSettlementResourceNode(
        List<WorldMapResourceNodeData> resourceNodes,
        HashSet<Vector2I> reservedCoords,
        SettlementInstanceData settlement,
        string nodeKind,
        ref int nextIndex,
        int minDistance,
        int maxDistance
    )
    {
        Vector2I coord = FindSettlementResourceCoord(
            settlement,
            nodeKind,
            resourceNodes,
            reservedCoords,
            minDistance,
            maxDistance
        );
        if (coord == new Vector2I(-1, -1))
            return false;
        return AddResourceNode(
            resourceNodes,
            reservedCoords,
            nodeKind,
            coord,
            settlement?.SettlementId ?? "",
            ref nextIndex
        );
    }

    private Vector2I FindSettlementResourceCoord(
        SettlementInstanceData settlement,
        string nodeKind,
        IReadOnlyList<WorldMapResourceNodeData> resourceNodes,
        HashSet<Vector2I> reservedCoords,
        int minDistance,
        int maxDistance
    )
    {
        if (settlement == null)
            return new Vector2I(-1, -1);
        Vector2I center = settlement.Origin + settlement.FootprintSize / 2;
        Vector2I bestCoord = new(-1, -1);
        double bestScore = double.NegativeInfinity;
        int minDistanceToFootprint = Math.Max(minDistance, 1);
        int maxDistanceToFootprint = Math.Max(maxDistance, minDistanceToFootprint);
        for (int y = -maxDistanceToFootprint; y <= maxDistanceToFootprint; y++)
        {
            for (int x = -maxDistanceToFootprint; x <= maxDistanceToFootprint; x++)
            {
                Vector2I candidate = center + new Vector2I(x, y);
                int distance = DistanceToSettlementFootprint(candidate, settlement);
                if (distance < minDistanceToFootprint || distance > maxDistanceToFootprint)
                    continue;
                if (
                    !IsResourceCoordAvailable(
                        candidate,
                        reservedCoords,
                        resourceNodes,
                        nodeKind,
                        2
                    )
                )
                    continue;
                double score = ScoreSettlementResourceCoord(
                    candidate,
                    settlement,
                    nodeKind,
                    distance
                );
                if (score <= bestScore)
                    continue;
                bestScore = score;
                bestCoord = candidate;
            }
        }
        return bestCoord;
    }

    private double ScoreSettlementResourceCoord(
        Vector2I candidate,
        SettlementInstanceData settlement,
        string nodeKind,
        int distanceToFootprint
    )
    {
        double jitter = Hash01($"resource_{nodeKind}", candidate) * 4.0;
        if (nodeKind == WorldMapResourceNodeData.KindFarm)
        {
            return -Math.Abs(distanceToFootprint - 1) * 8.0 - settlement.Tier + jitter;
        }
        if (nodeKind == WorldMapResourceNodeData.KindHerbGarden)
        {
            return -Math.Abs(distanceToFootprint - 3) * 7.0 + settlement.Tier * 0.5 + jitter;
        }
        return jitter;
    }

    private int GetSettlementFarmTargetCount(SettlementInstanceData settlement)
    {
        if (settlement == null)
            return 0;
        return settlement.Tier switch
        {
            (int)SettlementTierKind.Village => 1,
            (int)SettlementTierKind.Town => 2,
            (int)SettlementTierKind.City => 2,
            (int)SettlementTierKind.Capital => 1,
            (int)SettlementTierKind.Metropolis => 1,
            _ => 0,
        };
    }

    private int GetSettlementHerbGardenTargetCount(SettlementInstanceData settlement)
    {
        if (settlement == null)
            return 0;
        if (settlement.Tier == (int)SettlementTierKind.Village)
            return 1;
        if (settlement.Tier == (int)SettlementTierKind.Town)
            return 1;
        if (
            settlement.Tier == (int)SettlementTierKind.City
            && Hash01("resource_city_herb", settlement.Origin) >= 0.45
        )
            return 1;
        if (
            settlement.Tier == (int)SettlementTierKind.Capital
            && Hash01("resource_capital_herb", settlement.Origin) >= 0.65
        )
            return 1;
        return 0;
    }

    private void PlaceMineResourceNodes(
        List<WorldMapResourceNodeData> resourceNodes,
        HashSet<Vector2I> reservedCoords,
        IReadOnlyList<SettlementInstanceData> settlements,
        Vector2I playerStartCoord,
        ref int nextIndex
    )
    {
        int targetCount = GetMineResourceTargetCount();
        if (targetCount <= 0)
            return;

        List<Vector2I> chunkCoords = BuildAllChunkCoords();
        chunkCoords.Sort(
            (a, b) => ScoreMineChunk(b, settlements).CompareTo(ScoreMineChunk(a, settlements))
        );
        foreach (Vector2I chunkCoord in chunkCoords)
        {
            if (CountResourceNodesOfKind(resourceNodes, WorldMapResourceNodeData.KindMine) >= targetCount)
                return;
            Vector2I coord = FindMineResourceCoordInChunk(
                chunkCoord,
                settlements,
                resourceNodes,
                reservedCoords,
                playerStartCoord,
                8,
                10
            );
            if (coord == new Vector2I(-1, -1))
                continue;
            AddResourceNode(
                resourceNodes,
                reservedCoords,
                WorldMapResourceNodeData.KindMine,
                coord,
                "",
                ref nextIndex
            );
        }
    }

    private int GetMineResourceTargetCount()
    {
        Vector2I worldChunks = _generationDefinition.WorldSizeInChunks;
        int chunkCount = Math.Max(worldChunks.X * worldChunks.Y, 1);
        return Math.Min(Math.Max(3, chunkCount / 90), 24);
    }

    private List<Vector2I> BuildAllChunkCoords()
    {
        var chunkCoords = new List<Vector2I>();
        Vector2I worldChunks = _generationDefinition.WorldSizeInChunks;
        for (int y = 0; y < worldChunks.Y; y++)
        for (int x = 0; x < worldChunks.X; x++)
            chunkCoords.Add(new Vector2I(x, y));
        return chunkCoords;
    }

    private Vector2I FindMineResourceCoordInChunk(
        Vector2I chunkCoord,
        IReadOnlyList<SettlementInstanceData> settlements,
        IReadOnlyList<WorldMapResourceNodeData> resourceNodes,
        HashSet<Vector2I> reservedCoords,
        Vector2I playerStartCoord,
        int minDistanceToSettlement,
        int minDistanceToPlayerStart
    )
    {
        Vector2I origin = new(
            chunkCoord.X * _generationDefinition.ChunkSize.X,
            chunkCoord.Y * _generationDefinition.ChunkSize.Y
        );
        Vector2I bestCoord = new(-1, -1);
        double bestScore = double.NegativeInfinity;
        for (int y = 0; y < _generationDefinition.ChunkSize.Y; y++)
        {
            for (int x = 0; x < _generationDefinition.ChunkSize.X; x++)
            {
                Vector2I candidate = origin + new Vector2I(x, y);
                if (
                    !IsResourceCoordAvailable(
                        candidate,
                        reservedCoords,
                        resourceNodes,
                        WorldMapResourceNodeData.KindMine,
                        12
                    )
                )
                    continue;
                int settlementDistance = DistanceToNearestSettlementFootprint(
                    candidate,
                    settlements
                );
                if (settlementDistance < minDistanceToSettlement)
                    continue;
                int startDistance = ManhattanDistance(candidate, playerStartCoord);
                if (startDistance < minDistanceToPlayerStart)
                    continue;

                double score =
                    ScoreMineChunk(chunkCoord, settlements) * 100.0
                    + settlementDistance * 0.35
                    + Math.Min(startDistance, 40) * 0.2
                    + Hash01("resource_mine_coord", candidate) * 8.0;
                if (score <= bestScore)
                    continue;
                bestScore = score;
                bestCoord = candidate;
            }
        }
        return bestCoord;
    }

    private double ScoreMineChunk(
        Vector2I chunkCoord,
        IReadOnlyList<SettlementInstanceData> settlements
    )
    {
        double noise = Hash01("resource_mine_geology", chunkCoord);
        double ridge = Math.Abs(
            Math.Sin((chunkCoord.X + (_mapSeed % 97)) * 0.73)
                + Math.Cos((chunkCoord.Y - (_mapSeed % 53)) * 0.61)
        ) * 0.5;
        int distance = DistanceToNearestSettlementFootprint(
            new Vector2I(
                chunkCoord.X * _generationDefinition.ChunkSize.X
                    + _generationDefinition.ChunkSize.X / 2,
                chunkCoord.Y * _generationDefinition.ChunkSize.Y
                    + _generationDefinition.ChunkSize.Y / 2
            ),
            settlements
        );
        double remoteBonus = Math.Min(distance, 50) / 50.0;
        return noise * 0.55 + ridge * 0.3 + remoteBonus * 0.15;
    }

    private bool AddResourceNode(
        List<WorldMapResourceNodeData> resourceNodes,
        HashSet<Vector2I> reservedCoords,
        string nodeKind,
        Vector2I coord,
        string sourceSettlementId,
        ref int nextIndex
    )
    {
        string nodeId = $"resource_{nodeKind}_{nextIndex}";
        WorldMapResourceNodeData resourceNode = WorldMapResourceNodeData.Create(
            nodeId,
            nodeKind,
            coord,
            sourceSettlementId
        );
        if (resourceNode == null || !resourceNode.Exists)
            return false;
        resourceNodes.Add(resourceNode);
        reservedCoords.Add(coord);
        nextIndex++;
        return true;
    }

    private bool IsResourceCoordAvailable(
        Vector2I candidate,
        HashSet<Vector2I> reservedCoords,
        IReadOnlyList<WorldMapResourceNodeData> resourceNodes,
        string nodeKind,
        int minSameKindDistance
    )
    {
        if (!_gridSystem.IsCellInsideWorld(candidate))
            return false;
        if (_gridSystem.GetOccupantRoot(candidate) != "")
            return false;
        if (reservedCoords.Contains(candidate))
            return false;
        foreach (WorldMapResourceNodeData resourceNode in resourceNodes)
        {
            if (resourceNode == null)
                continue;
            int distance = ManhattanDistance(resourceNode.WorldCoord, candidate);
            if (distance == 0)
                return false;
            if (resourceNode.NodeKind == nodeKind && distance < minSameKindDistance)
                return false;
        }
        return true;
    }

    private static int CountResourceNodesOfKind(
        IReadOnlyList<WorldMapResourceNodeData> resourceNodes,
        string nodeKind
    )
    {
        int count = 0;
        foreach (WorldMapResourceNodeData resourceNode in resourceNodes)
        {
            if (resourceNode != null && resourceNode.NodeKind == nodeKind)
                count++;
        }
        return count;
    }

    private static int DistanceToNearestSettlementFootprint(
        Vector2I coord,
        IReadOnlyList<SettlementInstanceData> settlements
    )
    {
        if (settlements == null || settlements.Count == 0)
            return int.MaxValue / 4;
        int nearestDistance = int.MaxValue / 4;
        foreach (SettlementInstanceData settlement in settlements)
        {
            if (settlement == null)
                continue;
            nearestDistance = Math.Min(
                nearestDistance,
                DistanceToSettlementFootprint(coord, settlement)
            );
        }
        return nearestDistance;
    }

    private static int DistanceToSettlementFootprint(
        Vector2I coord,
        SettlementInstanceData settlement
    )
    {
        if (settlement == null)
            return int.MaxValue / 4;
        Vector2I origin = settlement.Origin;
        Vector2I size = settlement.FootprintSize;
        int maxX = origin.X + Math.Max(size.X, 1) - 1;
        int maxY = origin.Y + Math.Max(size.Y, 1) - 1;
        int dx = coord.X < origin.X ? origin.X - coord.X : coord.X > maxX ? coord.X - maxX : 0;
        int dy = coord.Y < origin.Y ? origin.Y - coord.Y : coord.Y > maxY ? coord.Y - maxY : 0;
        return dx + dy;
    }

    private double Hash01(string salt, Vector2I coord)
    {
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            Mix(ref hash, (ulong)_mapSeed);
            Mix(ref hash, (ulong)(uint)coord.X);
            Mix(ref hash, (ulong)(uint)coord.Y);
            foreach (char ch in salt ?? "")
                Mix(ref hash, ch);
            return (hash & ((1UL << 53) - 1UL)) / (double)(1UL << 53);
        }
    }

    private static void Mix(ref ulong hash, ulong value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
        hash ^= value >> 32;
        hash *= 1099511628211UL;
    }

    private static int ManhattanDistance(Vector2I a, Vector2I b) =>
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

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
            _generationDefinition.ChunkSize,
            settlementCells
        );

        List<EncounterAnchorData> encounterAnchors;
        if (_generationDefinition.ProceduralGenerationEnabled)
        {
            encounterAnchors = GenerateProceduralEncounterAnchors(placementContext);
        }
        else
        {
            encounterAnchors = new List<EncounterAnchorData>();
            int monsterIndex = 0;
            foreach (WildSpawnRuleDefinition rule in _resolvedWildSpawnRules)
            {
                foreach (Vector2I chunkCoord in rule.ChunkCoords)
                {
                    for (int offset = 0; offset < Math.Max(rule.DensityPerChunk, 0); offset++)
                    {
                        Vector2I spawnCoord = PickMonsterCoordForChunk(
                            chunkCoord,
                            rule.MinDistanceToSettlement,
                            placementContext,
                            offset
                        );
                        if (spawnCoord == new Vector2I(-1, -1))
                            continue;
                        monsterIndex++;
                        encounterAnchors.Add(
                            BuildEncounterAnchor(
                                new StringName($"wild_{monsterIndex}"),
                                rule.MonsterName,
                                spawnCoord,
                                rule.VisionRange,
                                rule.RegionTag,
                                new StringName(EncounterKindSingle),
                                rule.EncounterProfileId
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
        Vector2I worldChunks = _generationDefinition.WorldSizeInChunks;
        int monsterIndex = 0;
        int spawnChunkChanceDenominator = Math.Max(
            _generationDefinition.ProceduralWildSpawnChunkChanceDenominator,
            1
        );
        for (int chunkY = 0; chunkY < worldChunks.Y; chunkY++)
        {
            for (int chunkX = 0; chunkX < worldChunks.X; chunkX++)
            {
                var chunkCoord = new Vector2I(chunkX, chunkY);
                WildSpawnRuleDefinition rule = ResolveProceduralWildSpawnRuleForChunkY(chunkY);
                if (rule == null)
                    continue;
                int chunkSeed = (int)TrueRandomSeedService.GenerateSeed();
                if (PosMod(chunkSeed, spawnChunkChanceDenominator) != 0)
                    continue;
                for (int offset = 0; offset < Math.Max(rule.DensityPerChunk, 0); offset++)
                {
                    Vector2I spawnCoord = PickMonsterCoordForChunk(
                        chunkCoord,
                        rule.MinDistanceToSettlement,
                        placementContext,
                        chunkSeed + offset
                    );
                    if (spawnCoord == new Vector2I(-1, -1))
                        continue;
                    monsterIndex++;
                    encounterAnchors.Add(
                        BuildEncounterAnchor(
                            new StringName($"wild_{monsterIndex}"),
                            rule.MonsterName,
                            spawnCoord,
                            rule.VisionRange,
                            rule.RegionTag,
                            new StringName(EncounterKindSingle),
                            rule.EncounterProfileId
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
        if (!_generationDefinition.GuaranteeStartingWildEncounter)
            return;
        if (!_gridSystem.IsCellInsideWorld(playerStartCoord))
            return;
        if (_resolvedWildSpawnRules.Count == 0)
            return;
        WildSpawnRuleDefinition rule = _resolvedWildSpawnRules[0];
        if (_generationDefinition.ProceduralGenerationEnabled)
        {
            Vector2I playerChunkCoord = _gridSystem.GetChunkCoord(playerStartCoord);
            rule = ResolveProceduralWildSpawnRuleForChunkY(playerChunkCoord.Y);
        }
        if (rule == null)
            return;
        int minDistance = Math.Max(
            _generationDefinition.StartingWildSpawnMinDistance,
            rule.MinDistanceToSettlement
        );
        int maxDistance = Math.Max(
            Math.Max(
                _generationDefinition.StartingWildSpawnMinDistance,
                _generationDefinition.StartingWildSpawnMaxDistance
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
                rule.MonsterName,
                spawnCoord,
                rule.VisionRange,
                rule.RegionTag,
                new StringName(EncounterKindSingle),
                rule.EncounterProfileId
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
        foreach (WildSpawnRuleDefinition rule in _resolvedWildSpawnRules)
        {
            if (rule == null || rule.SettlementEncounterProfileId == "")
                continue;
            foreach (Vector2I chunkCoord in BuildDefaultSettlementCandidateChunks(rule))
            {
                Vector2I spawnCoord = PickMonsterCoordForChunk(
                    chunkCoord,
                    Math.Max(rule.MinDistanceToSettlement, 2),
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
                        rule.SettlementEncounterDisplayName,
                        spawnCoord,
                        Math.Max(rule.VisionRange, 2),
                        rule.RegionTag,
                        new StringName(EncounterKindSettlement),
                        rule.SettlementEncounterProfileId,
                        0
                    )
                );
                return;
            }
        }
    }

    private List<Vector2I> BuildDefaultSettlementCandidateChunks(WildSpawnRuleDefinition rule)
    {
        if (rule != null && rule.ChunkCoords.Count > 0)
            return new List<Vector2I>(rule.ChunkCoords);
        var candidateChunks = new List<Vector2I>();
        Vector2I worldChunks = _generationDefinition.WorldSizeInChunks;
        for (int chunkY = 0; chunkY < worldChunks.Y; chunkY++)
        for (int chunkX = 0; chunkX < worldChunks.X; chunkX++)
        {
            candidateChunks.Add(new Vector2I(chunkX, chunkY));
        }
        return candidateChunks;
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
        if (
            _generationDefinition == null
            || !_generationDefinition.InjectDefaultMainWorldContent
        )
            return new List<string>();
        if (
            !_generationDefinition.SettlementNamePools.TryGetValue(
                ContentPathCanonicalizer.Canonicalize(resourcePath),
                out WorldMapSettlementNamePoolDefinition namePool
            )
            || namePool == null
        )
        {
            GameLog.Warning(
                $"Unable to resolve {warningLabel} name pool from projected content {resourcePath}.",
                "world.spawn.name_pool_missing",
                "world"
            );
            return new List<string>();
        }
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

    private WildSpawnRuleDefinition ResolveProceduralWildSpawnRuleForChunkY(int chunkY)
    {
        WildSpawnRuleDefinition northRule = FindWildSpawnRuleByRegionTag("north_wilds");
        WildSpawnRuleDefinition southRule = FindWildSpawnRuleByRegionTag("south_wilds");
        if (northRule == null && _resolvedWildSpawnRules.Count > 0)
            northRule = _resolvedWildSpawnRules[0];
        if (southRule == null)
            southRule = _resolvedWildSpawnRules.Count > 1 ? _resolvedWildSpawnRules[1] : northRule;
        if (northRule == null)
            return southRule;
        if (southRule == null)
            return northRule;
        int midpointChunkY = _generationDefinition.WorldSizeInChunks.Y / 2;
        return chunkY < midpointChunkY ? northRule : southRule;
    }

    private WildSpawnRuleDefinition FindWildSpawnRuleByRegionTag(StringName regionTag)
    {
        foreach (WildSpawnRuleDefinition rule in _resolvedWildSpawnRules)
        {
            if (rule == null)
                continue;
            if (rule.RegionTag == regionTag)
                return rule;
        }
        return null;
    }

    private string ResolveSettlementDisplayName(
        SettlementDefinition settlementDefinition,
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
            string strongholdDisplayName = settlementDefinition.DisplayName;
            if (instanceIndex > 1)
                strongholdDisplayName = $"{strongholdDisplayName} {instanceIndex:00}";
            return strongholdDisplayName;
        }
        if (
            templateId.StartsWith("template_", StringComparison.Ordinal)
            && _remainingDefaultMainWorldSettlementDisplayNames.Count > 0
        )
            return PopBack(_remainingDefaultMainWorldSettlementDisplayNames);
        string displayName = settlementDefinition.DisplayName;
        if (instanceIndex > 1)
            displayName = $"{displayName} {instanceIndex:00}";
        return displayName;
    }

    private static EncounterAnchorData BuildEncounterAnchor(
        StringName entityId,
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
        foreach (WorldEventDefinition eventDefinition in _generationDefinition.WorldEvents)
        {
            if (eventDefinition == null || eventDefinition.EventId == new StringName(""))
                continue;
            generatedEvents.Add(
                new WorldEventInstanceData
                {
                    EventId = eventDefinition.EventId.ToString(),
                    DisplayName = eventDefinition.DisplayName,
                    WorldCoord = eventDefinition.WorldCoord,
                    EventType = eventDefinition.EventType.ToString(),
                    TargetSubmapId = eventDefinition.TargetSubmapId.ToString(),
                    DiscoveryConditionId = eventDefinition.DiscoveryConditionId.ToString(),
                    PromptTitle = eventDefinition.PromptTitle,
                    PromptText = eventDefinition.PromptText,
                    IsDiscovered = IsWorldEventDiscoveredByDefault(eventDefinition),
                }
            );
        }
        return generatedEvents;
    }

    private List<MountedSubmapInstanceData> GenerateMountedSubmaps()
    {
        var mountedSubmaps = new List<MountedSubmapInstanceData>();
        foreach (MountedSubmapDefinition submapDefinition in _generationDefinition.MountedSubmaps)
        {
            if (submapDefinition == null || submapDefinition.SubmapId == new StringName(""))
                continue;
            mountedSubmaps.Add(
                new MountedSubmapInstanceData
                {
                    SubmapId = submapDefinition.SubmapId.ToString(),
                    DisplayName = submapDefinition.DisplayName,
                    GenerationConfigPath = submapDefinition.GenerationConfigPath,
                    ReturnHintText = submapDefinition.ReturnHintText,
                    IsGenerated = false,
                    PlayerCoord = new Vector2I(-1, -1),
                }
            );
        }
        return mountedSubmaps;
    }

    private static bool IsWorldEventDiscoveredByDefault(WorldEventDefinition eventDefinition)
    {
        if (eventDefinition == null)
            return false;
        string conditionId = eventDefinition.DiscoveryConditionId.ToString().StripEdges();
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
                && settlement.Tier == (int)SettlementTierKind.Village
            )
                return settlement;
        }
        return null;
    }

    private Vector2I ResolvePlayerStartCoord(SettlementInstanceData playerStartSettlement)
    {
        return playerStartSettlement == null
            ? _generationDefinition.PlayerStartCoord
            : playerStartSettlement.Origin;
    }

    private Vector2I GetCenteredOrigin(Vector2I footprintSize)
    {
        Vector2I worldSize = _generationDefinition.GetWorldSizeCells();
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
        Vector2I worldSize = _generationDefinition.GetWorldSizeCells();
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
                _generationDefinition.GetSettlementSpacingCells(otherTier)
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
