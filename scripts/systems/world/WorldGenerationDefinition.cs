using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;

public sealed class WorldGenerationDefinition
{
    internal const string DefaultMainWorldSettlementBundlePath =
        "res://data/configs/world_map/shared/main_world_default_settlement_bundle.tres";
    internal const string DefaultMainWorldWildSpawnBundlePath =
        "res://data/configs/world_map/shared/main_world_default_wild_spawn_bundle.tres";
    internal const string DefaultMainWorldSettlementNamePoolPath =
        "res://data/configs/world_map/shared/main_world_settlement_name_pool.tres";
    internal const string DefaultMainWorldTownNamePoolPath =
        "res://data/configs/world_map/shared/main_world_town_name_pool.tres";
    internal const string DefaultMainWorldCityNamePoolPath =
        "res://data/configs/world_map/shared/main_world_city_name_pool.tres";
    internal const string DefaultMainWorldCapitalNamePoolPath =
        "res://data/configs/world_map/shared/main_world_capital_name_pool.tres";
    internal const string DefaultMainWorldMetropolisNamePoolPath =
        "res://data/configs/world_map/shared/main_world_metropolis_name_pool.tres";

    private static readonly string[] DefaultNamePoolPaths =
    {
        DefaultMainWorldSettlementNamePoolPath,
        DefaultMainWorldTownNamePoolPath,
        DefaultMainWorldCityNamePoolPath,
        DefaultMainWorldCapitalNamePoolPath,
        DefaultMainWorldMetropolisNamePoolPath,
    };

    public WorldGenerationDefinition(
        string canonicalPath,
        int seed,
        Vector2I worldSizeInChunks,
        Vector2I chunkSize,
        Vector2I playerStartCoord,
        int playerVisionRange,
        bool proceduralGenerationEnabled,
        int proceduralWildSpawnChunkChanceDenominator,
        bool injectDefaultMainWorldContent,
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
        IReadOnlyDictionary<string, WorldMapSettlementNamePoolDefinition> settlementNamePools
    )
    {
        CanonicalPath = canonicalPath
            ?? throw new ArgumentNullException(nameof(canonicalPath));
        Seed = seed;
        WorldSizeInChunks = worldSizeInChunks;
        ChunkSize = chunkSize;
        PlayerStartCoord = playerStartCoord;
        PlayerVisionRange = playerVisionRange;
        ProceduralGenerationEnabled = proceduralGenerationEnabled;
        ProceduralWildSpawnChunkChanceDenominator =
            proceduralWildSpawnChunkChanceDenominator;
        InjectDefaultMainWorldContent = injectDefaultMainWorldContent;
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

    public string CanonicalPath { get; }
    public int Seed { get; }
    public Vector2I WorldSizeInChunks { get; }
    public Vector2I ChunkSize { get; }
    public Vector2I PlayerStartCoord { get; }
    public int PlayerVisionRange { get; }
    public bool ProceduralGenerationEnabled { get; }
    public int ProceduralWildSpawnChunkChanceDenominator { get; }
    public bool InjectDefaultMainWorldContent { get; }
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
    public IReadOnlyDictionary<string, WorldMapSettlementNamePoolDefinition> SettlementNamePools { get; }
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

    internal static WorldGenerationDefinition FromResource(
        string canonicalPath,
        WorldMapGenerationConfig source,
        IContentResourceLoader loader
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(loader);
        return new ProjectionContext(loader).ProjectGeneration(canonicalPath, source);
    }

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

    private sealed class ProjectionContext
    {
        private readonly IContentResourceLoader _loader;
        private readonly Dictionary<string, WorldGenerationDefinition> _generationCache =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, WorldMapSettlementBundleDefinition> _settlementBundleCache =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, WorldMapWildSpawnBundleDefinition> _wildSpawnBundleCache =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, WorldMapSettlementNamePoolDefinition> _namePoolCache =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> _generationStack = new(StringComparer.Ordinal);
        private readonly List<string> _generationPath = new();

        internal ProjectionContext(IContentResourceLoader loader)
        {
            _loader = loader;
        }

        internal WorldGenerationDefinition ProjectGeneration(
            string resourcePath,
            WorldMapGenerationConfig source
        )
        {
            string canonicalPath = Canonicalize(resourcePath, "world_generation.canonical_path");
            if (_generationCache.TryGetValue(canonicalPath, out var cached))
                return cached;
            if (!_generationStack.Add(canonicalPath))
            {
                throw WorldDefinitionProjection.Invalid(
                    $"world_generation[{canonicalPath}]",
                    $"recursive generation config cycle: {BuildCycleLabel(canonicalPath)}"
                );
            }
            _generationPath.Add(canonicalPath);
            try
            {
                string path = $"world_generation[{canonicalPath}]";
                IReadOnlyList<SettlementDefinition> settlements =
                    WorldDefinitionProjection.ProjectResources<
                        SettlementConfig,
                        SettlementDefinition
                    >(
                        source.SettlementLibraryProjectionBorrowed,
                        path + ".settlement_library",
                        SettlementDefinition.FromResource
                    );
                IReadOnlyList<FacilityDefinition> facilities =
                    WorldDefinitionProjection.ProjectResources<FacilityConfig, FacilityDefinition>(
                        source.FacilityLibraryProjectionBorrowed,
                        path + ".facility_library",
                        FacilityDefinition.FromResource
                    );
                IReadOnlyList<SettlementDistributionDefinition> distribution =
                    WorldDefinitionProjection.ProjectResources<
                        SettlementDistributionRule,
                        SettlementDistributionDefinition
                    >(
                        source.SettlementDistributionProjectionBorrowed,
                        path + ".settlement_distribution",
                        SettlementDistributionDefinition.FromResource
                    );
                IReadOnlyList<WildSpawnRuleDefinition> wildSpawns =
                    WorldDefinitionProjection.ProjectResources<
                        WildSpawnRule,
                        WildSpawnRuleDefinition
                    >(
                        source.WildMonsterDistributionProjectionBorrowed,
                        path + ".wild_monster_distribution",
                        WildSpawnRuleDefinition.FromResource
                    );
                IReadOnlyList<WorldEventDefinition> worldEvents =
                    WorldDefinitionProjection.ProjectResources<WorldEventConfig, WorldEventDefinition>(
                        source.WorldEventsProjectionBorrowed,
                        path + ".world_events",
                        WorldEventDefinition.FromResource
                    );
                IReadOnlyList<MountedSubmapDefinition> mountedSubmaps = ProjectMountedSubmaps(
                    source.MountedSubmapsProjectionBorrowed,
                    path + ".mounted_submaps"
                );

                WorldMapSettlementBundleDefinition defaultSettlementBundle = null;
                WorldMapWildSpawnBundleDefinition defaultWildSpawnBundle = null;
                IReadOnlyDictionary<string, WorldMapSettlementNamePoolDefinition> namePools =
                    new ReadOnlyDictionary<string, WorldMapSettlementNamePoolDefinition>(
                        new Dictionary<string, WorldMapSettlementNamePoolDefinition>(
                            StringComparer.Ordinal
                        )
                    );
                if (source.inject_default_main_world_content)
                {
                    defaultSettlementBundle = ProjectSettlementBundle(
                        DefaultMainWorldSettlementBundlePath,
                        path + ".default_settlement_bundle"
                    );
                    defaultWildSpawnBundle = ProjectWildSpawnBundle(
                        DefaultMainWorldWildSpawnBundlePath,
                        path + ".default_wild_spawn_bundle"
                    );
                    var mutableNamePools = new Dictionary<
                        string,
                        WorldMapSettlementNamePoolDefinition
                    >(StringComparer.Ordinal);
                    foreach (string namePoolPath in DefaultNamePoolPaths)
                    {
                        string canonicalNamePoolPath = Canonicalize(
                            namePoolPath,
                            path + ".settlement_name_pools"
                        );
                        mutableNamePools[canonicalNamePoolPath] = ProjectNamePool(
                            canonicalNamePoolPath,
                            path + $".settlement_name_pools[{canonicalNamePoolPath}]"
                        );
                    }
                    namePools = new ReadOnlyDictionary<
                        string,
                        WorldMapSettlementNamePoolDefinition
                    >(mutableNamePools);
                }

                var definition = new WorldGenerationDefinition(
                    canonicalPath,
                    source.seed,
                    source.world_size_in_chunks,
                    source.chunk_size,
                    source.player_start_coord,
                    source.player_vision_range,
                    source.procedural_generation_enabled,
                    source.procedural_wild_spawn_chunk_chance_denominator,
                    source.inject_default_main_world_content,
                    source.procedural_village_count,
                    source.procedural_town_count,
                    source.procedural_city_count,
                    source.procedural_capital_count,
                    source.procedural_world_stronghold_count,
                    source.procedural_metropolis_count,
                    source.village_spacing_cells,
                    source.town_spacing_cells,
                    source.city_spacing_cells,
                    source.capital_spacing_cells,
                    source.world_stronghold_spacing_cells,
                    source.metropolis_spacing_cells,
                    source.guarantee_starting_wild_encounter,
                    source.starting_wild_spawn_min_distance,
                    source.starting_wild_spawn_max_distance,
                    settlements,
                    facilities,
                    distribution,
                    wildSpawns,
                    mountedSubmaps,
                    worldEvents,
                    defaultSettlementBundle,
                    defaultWildSpawnBundle,
                    namePools
                );
                _generationCache[canonicalPath] = definition;
                return definition;
            }
            finally
            {
                _generationPath.RemoveAt(_generationPath.Count - 1);
                _generationStack.Remove(canonicalPath);
            }
        }

        private IReadOnlyList<MountedSubmapDefinition> ProjectMountedSubmaps(
            Godot.Collections.Array<Resource> source,
            string path
        )
        {
            WorldDefinitionProjection.RequireCollection(source, path);
            var result = new List<MountedSubmapDefinition>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                string itemPath = $"{path}[{index}]";
                if (source[index] is not MountedSubmapConfig mountedSubmap)
                {
                    throw WorldDefinitionProjection.Invalid(
                        itemPath,
                        $"expected {nameof(MountedSubmapConfig)}"
                    );
                }
                string authoredPath = WorldDefinitionProjection.RequireString(
                    mountedSubmap.generation_config_path,
                    itemPath + ".generation_config_path"
                ).Trim();
                string canonicalChildPath = Canonicalize(
                    authoredPath,
                    itemPath + ".generation_config_path"
                );
                if (_generationStack.Contains(canonicalChildPath))
                {
                    throw WorldDefinitionProjection.Invalid(
                        itemPath + ".generation_config_path",
                        $"recursive generation config cycle: {BuildCycleLabel(canonicalChildPath)}"
                    );
                }
                WorldMapGenerationConfig childSource = LoadRequired<WorldMapGenerationConfig>(
                    canonicalChildPath,
                    itemPath + ".generation_config_path"
                );
                WorldGenerationDefinition childDefinition = ProjectGeneration(
                    canonicalChildPath,
                    childSource
                );
                result.Add(
                    MountedSubmapDefinition.FromResource(
                        mountedSubmap,
                        canonicalChildPath,
                        childDefinition,
                        itemPath
                    )
                );
            }
            return new ReadOnlyCollection<MountedSubmapDefinition>(result);
        }

        private WorldMapSettlementBundleDefinition ProjectSettlementBundle(
            string resourcePath,
            string path
        )
        {
            string canonicalPath = Canonicalize(resourcePath, path);
            if (_settlementBundleCache.TryGetValue(canonicalPath, out var cached))
                return cached;
            var source = LoadRequired<WorldMapSettlementBundle>(canonicalPath, path);
            var definition = WorldMapSettlementBundleDefinition.FromResource(source, path);
            _settlementBundleCache[canonicalPath] = definition;
            return definition;
        }

        private WorldMapWildSpawnBundleDefinition ProjectWildSpawnBundle(
            string resourcePath,
            string path
        )
        {
            string canonicalPath = Canonicalize(resourcePath, path);
            if (_wildSpawnBundleCache.TryGetValue(canonicalPath, out var cached))
                return cached;
            var source = LoadRequired<WorldMapWildSpawnBundle>(canonicalPath, path);
            var definition = WorldMapWildSpawnBundleDefinition.FromResource(source, path);
            _wildSpawnBundleCache[canonicalPath] = definition;
            return definition;
        }

        private WorldMapSettlementNamePoolDefinition ProjectNamePool(
            string resourcePath,
            string path
        )
        {
            string canonicalPath = Canonicalize(resourcePath, path);
            if (_namePoolCache.TryGetValue(canonicalPath, out var cached))
                return cached;
            var source = LoadRequired<WorldMapSettlementNamePool>(canonicalPath, path);
            var definition = WorldMapSettlementNamePoolDefinition.FromResource(source, path);
            _namePoolCache[canonicalPath] = definition;
            return definition;
        }

        private T LoadRequired<T>(string canonicalPath, string path)
            where T : Resource
        {
            T resource;
            try
            {
                resource = _loader.LoadCanonical<T>(canonicalPath);
            }
            catch (Exception exception) when (exception is not InvalidDataException)
            {
                throw WorldDefinitionProjection.Invalid(
                    path,
                    $"failed to load canonical {typeof(T).Name} {canonicalPath}",
                    exception
                );
            }
            if (resource == null)
            {
                throw WorldDefinitionProjection.Invalid(
                    path,
                    $"loader returned null for canonical {typeof(T).Name} {canonicalPath}"
                );
            }
            return resource;
        }

        private string BuildCycleLabel(string repeatedPath)
        {
            var paths = new List<string>(_generationPath) { repeatedPath };
            return string.Join(" -> ", paths);
        }

        private static string Canonicalize(string resourcePath, string path)
        {
            try
            {
                return ContentPathCanonicalizer.Canonicalize(resourcePath);
            }
            catch (Exception exception)
            {
                throw WorldDefinitionProjection.Invalid(
                    path,
                    $"invalid canonical resource path '{resourcePath ?? "<null>"}'",
                    exception
                );
            }
        }
    }
}

internal static class WorldDefinitionProjection
{
    internal static InvalidDataException Invalid(
        string path,
        string message,
        Exception innerException = null
    ) =>
        innerException == null
            ? new InvalidDataException($"{path}: {message}")
            : new InvalidDataException($"{path}: {message}", innerException);

    internal static string RequireString(string value, string path) =>
        value ?? throw Invalid(path, "string is null");

    internal static TCollection RequireCollection<TCollection>(
        TCollection collection,
        string path
    )
        where TCollection : class =>
        collection ?? throw Invalid(path, "collection is null");

    internal static IReadOnlyList<TDefinition> ProjectResources<TRaw, TDefinition>(
        Godot.Collections.Array<Resource> source,
        string path,
        Func<TRaw, string, TDefinition> projector
    )
        where TRaw : Resource
        where TDefinition : class
    {
        RequireCollection(source, path);
        ArgumentNullException.ThrowIfNull(projector);
        var result = new List<TDefinition>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            string itemPath = $"{path}[{index}]";
            if (source[index] is not TRaw typed)
                throw Invalid(itemPath, $"expected {typeof(TRaw).Name}");
            TDefinition definition = projector(typed, itemPath);
            if (definition == null)
                throw Invalid(itemPath, "projector returned null");
            result.Add(definition);
        }
        return new ReadOnlyCollection<TDefinition>(result);
    }

    internal static IReadOnlyList<string> CopyStrings(
        IEnumerable<string> source,
        string path,
        bool trim = false
    )
    {
        if (source == null)
            throw Invalid(path, "collection is null");
        var result = new List<string>();
        int index = 0;
        foreach (string value in source)
        {
            if (value == null)
                throw Invalid($"{path}[{index}]", "string is null");
            result.Add(trim ? value.Trim() : value);
            index++;
        }
        return new ReadOnlyCollection<string>(result);
    }

    internal static IReadOnlyList<T> CopyValues<T>(IEnumerable<T> source, string path)
    {
        if (source == null)
            throw Invalid(path, "collection is null");
        return new ReadOnlyCollection<T>(new List<T>(source));
    }

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
