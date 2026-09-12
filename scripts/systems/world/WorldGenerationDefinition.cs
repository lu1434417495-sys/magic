using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class WorldGenerationDefinition
{
    public WorldGenerationDefinition(
        StringName generationId,
        int seed,
        Vector2I worldSizeInChunks,
        Vector2I chunkSize,
        Vector2I playerStartCoord,
        int playerVisionRange,
        bool proceduralGenerationEnabled,
        int proceduralWildSpawnChunkChanceDenominator,
        StringName sharedContentId,
        int proceduralVillageCount,
        int proceduralTownCount,
        int proceduralCityCount,
        int proceduralCapitalCount,
        int proceduralWorldStrongholdCount,
        int proceduralMetropolisCount,
        int villageSpacingCells,
        int townSpacingCells,
        int citySpacingCells,
        int capitalSpacingCells,
        int worldStrongholdSpacingCells,
        int metropolisSpacingCells,
        bool guaranteeStartingWildEncounter,
        int startingWildSpawnMinDistance,
        int startingWildSpawnMaxDistance,
        IReadOnlyList<SettlementDefinition> settlementLibrary,
        IReadOnlyList<FacilityDefinition> facilityLibrary,
        IReadOnlyList<SettlementDistributionDefinition> settlementDistribution,
        IReadOnlyList<WildSpawnRuleDefinition> wildMonsterDistribution,
        IReadOnlyList<MountedSubmapDefinition> mountedSubmaps,
        IReadOnlyList<WorldEventDefinition> worldEvents,
        WorldMapSettlementBundleDefinition defaultSettlementBundle,
        WorldMapWildSpawnBundleDefinition defaultWildSpawnBundle,
        IReadOnlyDictionary<SettlementTierKind, WorldMapSettlementNamePoolDefinition> settlementNamePools
    )
    {
        if (generationId == "")
            throw new ArgumentException("World generation ID is required.", nameof(generationId));
        GenerationId = generationId;
        Seed = seed;
        WorldSizeInChunks = worldSizeInChunks;
        ChunkSize = chunkSize;
        PlayerStartCoord = playerStartCoord;
        PlayerVisionRange = playerVisionRange;
        ProceduralGenerationEnabled = proceduralGenerationEnabled;
        ProceduralWildSpawnChunkChanceDenominator = proceduralWildSpawnChunkChanceDenominator;
        SharedContentId = sharedContentId;
        ProceduralVillageCount = proceduralVillageCount;
        ProceduralTownCount = proceduralTownCount;
        ProceduralCityCount = proceduralCityCount;
        ProceduralCapitalCount = proceduralCapitalCount;
        ProceduralWorldStrongholdCount = proceduralWorldStrongholdCount;
        ProceduralMetropolisCount = proceduralMetropolisCount;
        VillageSpacingCells = villageSpacingCells;
        TownSpacingCells = townSpacingCells;
        CitySpacingCells = citySpacingCells;
        CapitalSpacingCells = capitalSpacingCells;
        WorldStrongholdSpacingCells = worldStrongholdSpacingCells;
        MetropolisSpacingCells = metropolisSpacingCells;
        GuaranteeStartingWildEncounter = guaranteeStartingWildEncounter;
        StartingWildSpawnMinDistance = startingWildSpawnMinDistance;
        StartingWildSpawnMaxDistance = startingWildSpawnMaxDistance;
        SettlementLibrary = WorldDefinitionProjection.FreezeValues(
            settlementLibrary,
            nameof(settlementLibrary)
        );
        FacilityLibrary = WorldDefinitionProjection.FreezeValues(
            facilityLibrary,
            nameof(facilityLibrary)
        );
        SettlementDistribution = WorldDefinitionProjection.FreezeValues(
            settlementDistribution,
            nameof(settlementDistribution)
        );
        WildMonsterDistribution = WorldDefinitionProjection.FreezeValues(
            wildMonsterDistribution,
            nameof(wildMonsterDistribution)
        );
        MountedSubmaps = WorldDefinitionProjection.FreezeValues(
            mountedSubmaps,
            nameof(mountedSubmaps)
        );
        WorldEvents = WorldDefinitionProjection.FreezeValues(
            worldEvents,
            nameof(worldEvents)
        );
        DefaultSettlementBundle = defaultSettlementBundle;
        DefaultWildSpawnBundle = defaultWildSpawnBundle;
        SettlementNamePools = WorldDefinitionProjection.FreezeDictionary(
            settlementNamePools,
            nameof(settlementNamePools)
        );
        EffectiveSettlementLibrary = WorldDefinitionProjection.Combine(
            DefaultSettlementBundle?.SettlementLibrary,
            SettlementLibrary
        );
        EffectiveFacilityLibrary = WorldDefinitionProjection.Combine(
            DefaultSettlementBundle?.FacilityLibrary,
            FacilityLibrary
        );
        EffectiveWildSpawnRules = WorldDefinitionProjection.Combine(
            DefaultWildSpawnBundle?.WildMonsterDistribution,
            WildMonsterDistribution
        );
        MountedSubmapsById = BuildMountedSubmapIndex(MountedSubmaps);
    }

    public StringName GenerationId { get; }
    public int Seed { get; }
    public Vector2I WorldSizeInChunks { get; }
    public Vector2I ChunkSize { get; }
    public Vector2I PlayerStartCoord { get; }
    public int PlayerVisionRange { get; }
    public bool ProceduralGenerationEnabled { get; }
    public int ProceduralWildSpawnChunkChanceDenominator { get; }
    public StringName SharedContentId { get; }
    public bool InjectDefaultMainWorldContent => SharedContentId != "";
    public int ProceduralVillageCount { get; }
    public int ProceduralTownCount { get; }
    public int ProceduralCityCount { get; }
    public int ProceduralCapitalCount { get; }
    public int ProceduralWorldStrongholdCount { get; }
    public int ProceduralMetropolisCount { get; }
    public int VillageSpacingCells { get; }
    public int TownSpacingCells { get; }
    public int CitySpacingCells { get; }
    public int CapitalSpacingCells { get; }
    public int WorldStrongholdSpacingCells { get; }
    public int MetropolisSpacingCells { get; }
    public bool GuaranteeStartingWildEncounter { get; }
    public int StartingWildSpawnMinDistance { get; }
    public int StartingWildSpawnMaxDistance { get; }
    public IReadOnlyList<SettlementDefinition> SettlementLibrary { get; }
    public IReadOnlyList<FacilityDefinition> FacilityLibrary { get; }
    public IReadOnlyList<SettlementDistributionDefinition> SettlementDistribution { get; }
    public IReadOnlyList<WildSpawnRuleDefinition> WildMonsterDistribution { get; }
    public IReadOnlyList<MountedSubmapDefinition> MountedSubmaps { get; }
    public IReadOnlyList<WorldEventDefinition> WorldEvents { get; }
    public WorldMapSettlementBundleDefinition DefaultSettlementBundle { get; }
    public WorldMapWildSpawnBundleDefinition DefaultWildSpawnBundle { get; }
    public IReadOnlyDictionary<SettlementTierKind, WorldMapSettlementNamePoolDefinition> SettlementNamePools { get; }
    public IReadOnlyList<SettlementDefinition> EffectiveSettlementLibrary { get; }
    public IReadOnlyList<FacilityDefinition> EffectiveFacilityLibrary { get; }
    public IReadOnlyList<WildSpawnRuleDefinition> EffectiveWildSpawnRules { get; }
    public IReadOnlyDictionary<StringName, MountedSubmapDefinition> MountedSubmapsById { get; }

    public Vector2I GetWorldSizeCells() =>
        new(WorldSizeInChunks.X * ChunkSize.X, WorldSizeInChunks.Y * ChunkSize.Y);

    public int GetTargetSettlementCount(int tier) =>
        tier switch
        {
            (int)SettlementTierKind.Village => Math.Max(ProceduralVillageCount, 1),
            (int)SettlementTierKind.Town => Math.Max(ProceduralTownCount, 0),
            (int)SettlementTierKind.City => Math.Max(ProceduralCityCount, 0),
            (int)SettlementTierKind.Capital => Math.Max(ProceduralCapitalCount, 0),
            (int)SettlementTierKind.WorldStronghold => Math.Max(
                ProceduralWorldStrongholdCount,
                0
            ),
            (int)SettlementTierKind.Metropolis => Math.Max(ProceduralMetropolisCount, 0),
            _ => 0,
        };

    public int GetSettlementSpacingCells(int tier) =>
        tier switch
        {
            (int)SettlementTierKind.Village => VillageSpacingCells,
            (int)SettlementTierKind.Town => TownSpacingCells,
            (int)SettlementTierKind.City => CitySpacingCells,
            (int)SettlementTierKind.Capital => CapitalSpacingCells,
            (int)SettlementTierKind.WorldStronghold => WorldStrongholdSpacingCells,
            (int)SettlementTierKind.Metropolis => MetropolisSpacingCells,
            _ => 64,
        };

    private static IReadOnlyDictionary<StringName, MountedSubmapDefinition> BuildMountedSubmapIndex(
        IReadOnlyList<MountedSubmapDefinition> mountedSubmaps
    )
    {
        var index = new Dictionary<StringName, MountedSubmapDefinition>();
        foreach (MountedSubmapDefinition submap in mountedSubmaps)
        {
            if (submap.SubmapId != "" && !index.ContainsKey(submap.SubmapId))
                index[submap.SubmapId] = submap;
        }
        return new ReadOnlyDictionary<StringName, MountedSubmapDefinition>(index);
    }
}

internal static class WorldDefinitionProjection
{
    internal static IReadOnlyList<T> FreezeValues<T>(
        IReadOnlyList<T> source,
        string parameterName
    )
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);
        var result = new List<T>(source.Count);
        foreach (T value in source)
        {
            if (value is null)
                throw new ArgumentException("Definition lists must not contain null.", parameterName);
            result.Add(value);
        }
        return new ReadOnlyCollection<T>(result);
    }

    internal static IReadOnlyDictionary<TKey, TValue> FreezeDictionary<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> source,
        string parameterName
    )
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);
        var result = new Dictionary<TKey, TValue>();
        foreach ((TKey key, TValue value) in source)
        {
            if (key is null || value is null)
                throw new ArgumentException("Definition maps must not contain null.", parameterName);
            result[key] = value;
        }
        return new ReadOnlyDictionary<TKey, TValue>(result);
    }

    internal static IReadOnlyList<T> Combine<T>(
        IReadOnlyList<T> prefix,
        IReadOnlyList<T> suffix
    )
    {
        suffix ??= Array.Empty<T>();
        if (prefix == null || prefix.Count == 0)
            return FreezeValues(suffix, nameof(suffix));
        var result = new List<T>(prefix.Count + suffix.Count);
        result.AddRange(prefix);
        result.AddRange(suffix);
        return FreezeValues(result, nameof(result));
    }
}
