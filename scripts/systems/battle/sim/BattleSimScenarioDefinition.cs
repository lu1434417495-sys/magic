using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

/// <summary>
/// Immutable, plain simulation scenario consumed by runtime, reporting, and execution services.
/// Authored Resources are projected before this definition crosses the authoring boundary.
/// </summary>
internal sealed class BattleSimScenarioDefinition
{
    private readonly IReadOnlyList<BattleSimScenarioUnitEntry> _allyUnits;
    private readonly IReadOnlyList<BattleSimScenarioUnitEntry> _enemyUnits;
    private readonly IReadOnlyDictionary<
        Vector2I,
        IReadOnlyDictionary<string, object>
    > _cells;
    private readonly IReadOnlyList<int> _seeds;

    internal BattleSimScenarioDefinition(
        StringName scenarioId,
        string displayName,
        string description,
        Vector2I mapSize,
        StringName terrainProfileId,
        bool useFormalTerrainGeneration,
        Vector2I worldCoord,
        IReadOnlyList<BattleSimScenarioUnitEntry> allyUnits,
        IReadOnlyList<BattleSimScenarioUnitEntry> enemyUnits,
        int authoringAllyUnitCount,
        int authoringEnemyUnitCount,
        IReadOnlyDictionary<Vector2I, IReadOnlyDictionary<string, object>> cells,
        int timelineTicksPerStep,
        int tuPerTick,
        int maxIterations,
        StringName manualPolicy,
        bool traceEnabled,
        IReadOnlyList<int> seeds
    )
    {
        ScenarioId = scenarioId;
        DisplayName = displayName ?? string.Empty;
        Description = description ?? string.Empty;
        MapSize = mapSize;
        TerrainProfileId = terrainProfileId;
        UseFormalTerrainGeneration = useFormalTerrainGeneration;
        WorldCoord = worldCoord;
        _allyUnits = FreezeUnits(allyUnits, "BattleSimScenarioDefinition.ally_units");
        _enemyUnits = FreezeUnits(enemyUnits, "BattleSimScenarioDefinition.enemy_units");
        AuthoringAllyUnitCount = authoringAllyUnitCount;
        AuthoringEnemyUnitCount = authoringEnemyUnitCount;
        _cells = FreezeCells(cells, "BattleSimScenarioDefinition.cells");
        TimelineTicksPerStep = timelineTicksPerStep;
        TuPerTick = tuPerTick;
        MaxIterations = maxIterations;
        ManualPolicy = manualPolicy;
        TraceEnabled = traceEnabled;
        _seeds = FreezeSeeds(seeds);
    }

    internal StringName ScenarioId { get; }
    internal string DisplayName { get; }
    internal string Description { get; }
    internal Vector2I MapSize { get; }
    internal StringName TerrainProfileId { get; }
    internal bool UseFormalTerrainGeneration { get; }
    internal Vector2I WorldCoord { get; }
    internal IReadOnlyList<BattleSimScenarioUnitEntry> AllyUnits => _allyUnits;
    internal IReadOnlyList<BattleSimScenarioUnitEntry> EnemyUnits => _enemyUnits;
    internal int AuthoringAllyUnitCount { get; }
    internal int AuthoringEnemyUnitCount { get; }
    internal int TimelineTicksPerStep { get; }
    internal int TuPerTick { get; }
    internal int MaxIterations { get; }
    internal StringName ManualPolicy { get; }
    internal bool TraceEnabled { get; }
    internal IReadOnlyList<int> Seeds => _seeds;

    internal GodotProjectionLease<Godot.Collections.Dictionary> BuildStartContextLease()
    {
        var contextPlain = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["battle_party"] = BuildUnitPayloadsPlain(_allyUnits),
            ["enemy_units"] = BuildUnitPayloadsPlain(_enemyUnits),
            ["tu_per_tick"] = TuPerTick,
            ["battle_terrain_profile"] = TerrainProfileId,
            ["world_coord"] = WorldCoord,
        };

        if (UseFormalTerrainGeneration)
        {
            if (MapSize != Vector2I.Zero)
                contextPlain["battle_map_size"] = MapSize;
            return RuntimePlainPayload.ProjectDictionaryLease(
                contextPlain,
                "battle-sim-scenario",
                LifetimeDomain.Request,
                "BattleSimScenarioDefinition.BuildStartContextLease"
            );
        }

        contextPlain["ally_spawns"] = BuildSpawnCoordsPlain(_allyUnits);
        contextPlain["enemy_spawns"] = BuildSpawnCoordsPlain(_enemyUnits);
        contextPlain["map_size"] = MapSize;
        GodotProjectionLease<Godot.Collections.Dictionary> lease =
            RuntimePlainPayload.ProjectDictionaryLease(
                contextPlain,
                "battle-sim-scenario",
                LifetimeDomain.Request,
                "BattleSimScenarioDefinition.BuildStartContextLease"
            );
        try
        {
            Godot.Collections.Dictionary projectedCells = lease.Own(
                new Godot.Collections.Dictionary(),
                "BattleSimScenarioDefinition.BuildStartContextLease.cells"
            );
            foreach (
                KeyValuePair<Vector2I, IReadOnlyDictionary<string, object>> entry in _cells
            )
            {
                projectedCells[entry.Key] = RuntimePlainPayload.ProjectDictionaryInto(
                    lease,
                    entry.Value,
                    $"BattleSimScenarioDefinition.BuildStartContextLease.cells[{entry.Key}]"
                );
            }
            lease.Value["cells"] = projectedCells;
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static IReadOnlyList<BattleSimScenarioUnitEntry> FreezeUnits(
        IReadOnlyList<BattleSimScenarioUnitEntry> source,
        string sourceLabel
    )
    {
        if (source == null || source.Count == 0)
            return Array.Empty<BattleSimScenarioUnitEntry>();
        var copy = new List<BattleSimScenarioUnitEntry>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            BattleSimScenarioUnitEntry entry = source[index];
            if (entry == null)
                throw new ArgumentException("Scenario unit definitions cannot contain null.", nameof(source));
            copy.Add(entry.DeepClone($"{sourceLabel}[{index}]"));
        }
        return new ReadOnlyCollection<BattleSimScenarioUnitEntry>(copy);
    }

    private static IReadOnlyDictionary<Vector2I, IReadOnlyDictionary<string, object>> FreezeCells(
        IReadOnlyDictionary<Vector2I, IReadOnlyDictionary<string, object>> source,
        string sourceLabel
    )
    {
        var copy = new Dictionary<Vector2I, IReadOnlyDictionary<string, object>>();
        if (source != null)
        {
            foreach (
                KeyValuePair<Vector2I, IReadOnlyDictionary<string, object>> entry in source
            )
            {
                if (entry.Value == null)
                    throw new ArgumentException("Scenario cell snapshots cannot be null.", nameof(source));
                copy[entry.Key] = ContentValueNormalizer.NormalizeDictionary(
                    RuntimePlainPayload.CloneDictionary(entry.Value),
                    $"{sourceLabel}[{entry.Key}]"
                );
            }
        }
        return new ReadOnlyDictionary<
            Vector2I,
            IReadOnlyDictionary<string, object>
        >(copy);
    }

    private static IReadOnlyList<int> FreezeSeeds(IReadOnlyList<int> source)
    {
        if (source == null || source.Count == 0)
            return Array.AsReadOnly(new[] { 101 });
        var copy = new int[source.Count];
        for (int index = 0; index < source.Count; index++)
            copy[index] = source[index];
        return Array.AsReadOnly(copy);
    }

    private static List<object> BuildUnitPayloadsPlain(
        IReadOnlyList<BattleSimScenarioUnitEntry> unitEntries
    )
    {
        var payloads = new List<object>();
        foreach (BattleSimScenarioUnitEntry entry in unitEntries)
            payloads.Add(entry.UnitDefinition.UnitSnapshot);
        return payloads;
    }

    private static List<object> BuildSpawnCoordsPlain(
        IReadOnlyList<BattleSimScenarioUnitEntry> unitEntries
    )
    {
        var coords = new List<object>(unitEntries.Count);
        foreach (BattleSimScenarioUnitEntry entry in unitEntries)
            coords.Add(entry.Coord);
        return coords;
    }
}
