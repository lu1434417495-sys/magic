using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal enum BattleTerrainProfileKind
{
    Unknown = 0,
    Default,
    Canyon,
    NarrowAssault,
    HoldoutPush,
}

public class BattleTerrainGenerator : IDisposable
{
    private static readonly StringName TerrainLand = BattleTerrainRules.ToStringName(
        BattleTerrainKind.Land
    );
    private static readonly StringName TerrainForest = BattleTerrainRules.ToStringName(
        BattleTerrainKind.Forest
    );
    private static readonly StringName TerrainShallowWater = BattleTerrainRules.ToStringName(
        BattleTerrainKind.ShallowWater
    );
    private static readonly StringName TerrainFlowingWater = BattleTerrainRules.ToStringName(
        BattleTerrainKind.FlowingWater
    );
    private static readonly StringName TerrainDeepWater = BattleTerrainRules.ToStringName(
        BattleTerrainKind.DeepWater
    );
    private static readonly StringName TerrainMud = BattleTerrainRules.ToStringName(
        BattleTerrainKind.Mud
    );
    private static readonly StringName TerrainSpike = BattleTerrainRules.ToStringName(
        BattleTerrainKind.Spike
    );
    private static readonly StringName ProfileDefault = "default";
    private static readonly StringName ProfileCanyon = "canyon";
    private static readonly StringName ProfileNarrowAssault = "narrow_assault";
    private static readonly StringName ProfileHoldoutPush = "holdout_push";
    private static readonly StringName PropObjectiveMarker = BattleBoardPropCatalog.ToStringName(
        BattleBoardPropKind.ObjectiveMarker
    );
    private static readonly StringName PropTent = BattleBoardPropCatalog.ToStringName(
        BattleBoardPropKind.Tent
    );
    private static readonly StringName PropTorch = BattleBoardPropCatalog.ToStringName(
        BattleBoardPropKind.Torch
    );

    private const int DefaultMinHeight = 4;
    private const int DefaultMaxHeight = 8;
    private const int SpawnAnchorSearchLimit = 32;
    private const int CandidateAttemptCount = 8;
    private const int MaxOrdinarySlopeDelta = 1;
    private const int LowFrequencyNoiseScale = 5;
    private const int MinSpawnRingCount = 6;
    private static readonly Vector2I CanyonTestSize = new(10, 10);
    private static readonly Vector2I[] DefaultFormalSizes =
    {
        new(11, 9),
        new(13, 9),
        new(13, 11),
        new(15, 11),
    };
    private static readonly Vector2I[] CanyonFormalSizes =
    {
        new(19, 11),
        new(21, 13),
        new(23, 13),
    };
    private static readonly Vector2I[] NarrowAssaultFormalSizes =
    {
        new(17, 11),
        new(19, 11),
        new(21, 13),
    };

    public bool IsDisposed { get; private set; }

    public virtual void Dispose()
    {
        IsDisposed = true;
    }
    private static readonly Vector2I[] HoldoutPushFormalSizes =
    {
        new(19, 11),
        new(21, 11),
        new(21, 13),
    };

    private readonly RuntimeRandom _rng = new();
    private readonly BattleEdgeService _edgeService = new();

    private readonly struct TerrainQualityResult
    {
        public readonly bool Passed;
        public readonly int Score;

        public TerrainQualityResult(bool passed, int score)
        {
            Passed = passed;
            Score = score;
        }
    }

    internal static StringName ToStringName(BattleTerrainProfileKind kind)
    {
        return kind switch
        {
            BattleTerrainProfileKind.Default => ProfileDefault,
            BattleTerrainProfileKind.Canyon => ProfileCanyon,
            BattleTerrainProfileKind.NarrowAssault => ProfileNarrowAssault,
            BattleTerrainProfileKind.HoldoutPush => ProfileHoldoutPush,
            _ => "",
        };
    }

    internal static BattleTerrainProfileKind ToProfileKind(StringName profileId)
    {
        if (profileId == ProfileDefault)
            return BattleTerrainProfileKind.Default;
        if (profileId == ProfileCanyon)
            return BattleTerrainProfileKind.Canyon;
        if (profileId == ProfileNarrowAssault)
            return BattleTerrainProfileKind.NarrowAssault;
        if (profileId == ProfileHoldoutPush)
            return BattleTerrainProfileKind.HoldoutPush;
        return BattleTerrainProfileKind.Unknown;
    }

    internal virtual GodotProjectionLease<GDictionary> GenerateLease(
        EncounterAnchorData encounterAnchor,
        long seed,
        GDictionary context,
        LifetimeDomain domain = LifetimeDomain.Battle
    )
    {
        GDictionary root = new();
        GodotProjectionLease<GDictionary> lease =
            GodotProjectionLease<GDictionary>.CreateOwnedRoot(
                root,
                "battle-terrain-generator",
                domain,
                "BattleTerrainGenerator.GenerateLease"
            );
        try
        {
            GDictionary generated = Generate(lease, encounterAnchor, seed, context);
            if (generated != null && !ReferenceEquals(generated, root))
                root.Merge(generated, true);
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private GDictionary Generate(
        GodotProjectionLease<GDictionary> lease,
        object encounterAnchorOrContext,
        long seed,
        GDictionary context
    )
    {
        GDictionary encounterContext = BuildEncounterContext(
            lease,
            encounterAnchorOrContext,
            seed,
            context
        );
        StringName terrainProfileId = ResolveTerrainProfileId(
            encounterAnchorOrContext,
            context
        );
        if (terrainProfileId == "")
        {
            return lease.Own(
                new GDictionary(),
                "BattleTerrainGenerator.Generate.empty"
            );
        }

        long battleSeed = BuildBattleSeed(encounterContext);
        _rng.Reseed(battleSeed);

        BattleTerrainProfileKind terrainProfileKind = ToProfileKind(terrainProfileId);
        if (terrainProfileKind == BattleTerrainProfileKind.Canyon)
        {
            return GenerateCanyon(lease, encounterContext, terrainProfileId);
        }
        if (terrainProfileKind == BattleTerrainProfileKind.NarrowAssault)
        {
            return GenerateNarrowAssault(lease, encounterContext, terrainProfileId);
        }
        if (terrainProfileKind == BattleTerrainProfileKind.HoldoutPush)
        {
            return GenerateHoldoutPush(lease, encounterContext, terrainProfileId);
        }
        return GenerateDefault(lease, encounterContext, terrainProfileId);
    }

    private StringName ResolveTerrainProfileId(
        object encounterAnchorOrContext,
        GDictionary context
    )
    {
        string rawProfileId = "";
        bool hasExplicitProfile = false;
        if (context != null && context.ContainsKey("battle_terrain_profile"))
        {
            rawProfileId = context["battle_terrain_profile"].AsString();
            hasExplicitProfile = !string.IsNullOrWhiteSpace(rawProfileId);
        }
        else if (ContextDictionary(encounterAnchorOrContext) is GDictionary encounterContext)
        {
            if (encounterContext.ContainsKey("battle_terrain_profile"))
            {
                rawProfileId = encounterContext["battle_terrain_profile"].AsString();
                hasExplicitProfile = !string.IsNullOrWhiteSpace(rawProfileId);
            }
            else
            {
                using GDictionary monster = GetDictionary(encounterContext, "monster");
                rawProfileId = ReadString(monster, "region_tag", "");
            }
        }
        else
        {
            EncounterAnchorData encounterAnchor = ContextObject(encounterAnchorOrContext);
            if (encounterAnchor != null)
            {
                rawProfileId = encounterAnchor.region_tag.ToString();
            }
        }

        if (string.IsNullOrWhiteSpace(rawProfileId))
        {
            rawProfileId = ResolveRegionTagFromContext(encounterAnchorOrContext);
        }
        StringName terrainProfileId = NormalizeTerrainProfileId(rawProfileId);
        if (terrainProfileId == "" && !hasExplicitProfile)
        {
            return ToStringName(BattleTerrainProfileKind.Default);
        }
        return terrainProfileId;
    }

    private void NormalizeWaterHeights(GDictionary heights, GDictionary waterCells)
    {
        if (heights == null || waterCells == null || waterCells.Count == 0)
        {
            return;
        }

        var visited = new HashSet<Vector2I>();
        foreach (var coordValue in waterCells.Keys)
        {
            if (coordValue.VariantType != Variant.Type.Vector2I)
            {
                continue;
            }
            Vector2I start = coordValue.AsVector2I();
            if (visited.Contains(start))
            {
                continue;
            }

            var component = new List<Vector2I>();
            var frontier = new Queue<Vector2I>();
            frontier.Enqueue(start);
            int minHeight = DefaultMaxHeight;
            while (frontier.Count > 0)
            {
                Vector2I current = frontier.Dequeue();
                if (visited.Contains(current) || !waterCells.ContainsKey(current))
                {
                    continue;
                }
                visited.Add(current);
                component.Add(current);
                minHeight = Math.Min(
                    minHeight,
                    ReadInt(heights, current, DefaultMaxHeight)
                );
                foreach (Vector2I offset in FourWayOffsets())
                {
                    Vector2I neighbor = current + offset;
                    if (!visited.Contains(neighbor) && waterCells.ContainsKey(neighbor))
                    {
                        frontier.Enqueue(neighbor);
                    }
                }
            }
            foreach (Vector2I coord in component)
            {
                heights[coord] = minHeight;
            }
        }
    }

    private GDictionary GenerateDefault(
        GodotProjectionLease<GDictionary> lease,
        GDictionary encounterContext,
        StringName terrainProfileId
    )
    {
        Vector2I mapSize = ResolveMapSize(
            encounterContext,
            DefaultFormalSizes[0],
            DefaultFormalSizes
        );
        long battleSeed = BuildBattleSeed(encounterContext);
        GDictionary bestLayout = null;
        int bestScore = int.MinValue;
        for (int attempt = 0; attempt < CandidateAttemptCount; attempt++)
        {
            Dictionary<Vector2I, BattleCellState> cells = null;
            GDictionary cellPayload = null;
            Dictionary<Vector3I, BattleEdgeFaceState> edgeFaces = null;
            bool candidateTransferredToLayout = false;
            try
            {
                long attemptSeed = battleSeed + attempt * 1777L;
                cells = BuildHeightfieldCells(
                    mapSize,
                    attemptSeed,
                    coord => ResolveDefaultMacroHeight(coord, mapSize),
                    coord =>
                        StableCoordHash(coord, attemptSeed + 11L) % 9 == 0
                            ? TerrainForest
                            : TerrainLand
                );
                AddConnectedWater(
                    cells,
                    mapSize,
                    new Vector2I(Math.Max(1, mapSize.X / 3), Math.Max(1, mapSize.Y / 2)),
                    5
                );
                RelaxOrdinarySlopeDeltas(cells, mapSize);
                cellPayload = ToCellDictionary(lease, cells);
                edgeFaces = BuildEdgeFaces(cellPayload, mapSize);
                Vector2I playerCoord = FirstDryCoord(
                    cellPayload,
                    new Vector2I(1, Math.Max(1, mapSize.Y / 2))
                );
                Vector2I enemyCoord = FirstDryCoord(
                    cellPayload,
                    new Vector2I(mapSize.X - 2, Math.Max(1, mapSize.Y / 2))
                );
                TerrainQualityResult quality = ScoreCandidate(
                    cells,
                    cellPayload,
                    mapSize,
                    edgeFaces,
                    playerCoord,
                    enemyCoord
                );
                if (quality.Score > bestScore)
                {
                    DisposeGeneratedLayout(bestLayout);
                    bestScore = quality.Score;
                    bestLayout = BuildLayout(
                        lease,
                        mapSize,
                        cellPayload,
                        playerCoord,
                        enemyCoord,
                        terrainProfileId
                    );
                    candidateTransferredToLayout = true;
                }
                if (quality.Passed)
                {
                    if (!candidateTransferredToLayout)
                    {
                        DisposeGeneratedLayout(bestLayout);
                        bestLayout = BuildLayout(
                            lease,
                            mapSize,
                            cellPayload,
                            playerCoord,
                            enemyCoord,
                            terrainProfileId
                        );
                        candidateTransferredToLayout = true;
                    }
                    GDictionary result = bestLayout;
                    bestLayout = null;
                    return result;
                }
            }
            finally
            {
                if (!candidateTransferredToLayout)
                    DisposeGeneratedCandidate(cells, cellPayload);
            }
        }
        return bestLayout
            ?? lease.Own(
                new GDictionary(),
                "BattleTerrainGenerator.GenerateDefault.empty"
            );
    }

    private GDictionary GenerateCanyon(
        GodotProjectionLease<GDictionary> lease,
        GDictionary encounterContext,
        StringName terrainProfileId
    )
    {
        Vector2I mapSize = ResolveCanyonMapSize(encounterContext);
        long battleSeed = BuildBattleSeed(encounterContext);
        GDictionary bestLayout = null;
        int bestScore = int.MinValue;
        for (int attempt = 0; attempt < CandidateAttemptCount; attempt++)
        {
            Dictionary<Vector2I, BattleCellState> cells = null;
            GDictionary cellPayload = null;
            Dictionary<Vector3I, BattleEdgeFaceState> edgeFaces = null;
            bool candidateTransferredToLayout = false;
            try
            {
                long attemptSeed = battleSeed + attempt * 1777L;
                _rng.Reseed(attemptSeed);
                cells = BuildHeightfieldCells(
                    mapSize,
                    attemptSeed,
                    coord => ResolveCanyonMacroHeight(coord, mapSize),
                    coord =>
                        StableCoordHash(coord, attemptSeed + 11L) % 7 == 0
                            ? TerrainForest
                            : TerrainLand
                );

                int riverY = Mathf.Clamp(mapSize.Y / 2, 1, mapSize.Y - 2);
                for (
                    int x = Math.Max(1, mapSize.X / 4);
                    x <= Math.Min(mapSize.X - 2, mapSize.X / 4 + 4);
                    x++
                )
                {
                    SetTerrain(
                        cells,
                        new Vector2I(x, riverY),
                        TerrainShallowWater,
                        DefaultMinHeight
                    );
                }
                RelaxOrdinarySlopeDeltas(cells, mapSize);

                cellPayload = ToCellDictionary(lease, cells);
                edgeFaces = BuildEdgeFaces(cellPayload, mapSize);
                if (
                    !TryFindSpawnPair(
                        cellPayload,
                        mapSize,
                        edgeFaces,
                        out var playerCoord,
                        out var enemyCoord
                    )
                )
                {
                    continue;
                }
                AddStandardProps(
                    lease,
                    cellPayload,
                    mapSize,
                    playerCoord,
                    enemyCoord,
                    mapSize.X / 2,
                    true
                );
                TerrainQualityResult quality = ScoreCandidate(
                    cells,
                    cellPayload,
                    mapSize,
                    edgeFaces,
                    playerCoord,
                    enemyCoord
                );
                if (quality.Score > bestScore)
                {
                    DisposeGeneratedLayout(bestLayout);
                    bestScore = quality.Score;
                    bestLayout = BuildLayout(
                        lease,
                        mapSize,
                        cellPayload,
                        playerCoord,
                        enemyCoord,
                        terrainProfileId
                    );
                    candidateTransferredToLayout = true;
                }
                if (quality.Passed)
                {
                    if (!candidateTransferredToLayout)
                    {
                        DisposeGeneratedLayout(bestLayout);
                        bestLayout = BuildLayout(
                            lease,
                            mapSize,
                            cellPayload,
                            playerCoord,
                            enemyCoord,
                            terrainProfileId
                        );
                        candidateTransferredToLayout = true;
                    }
                    GDictionary result = bestLayout;
                    bestLayout = null;
                    return result;
                }
            }
            finally
            {
                if (!candidateTransferredToLayout)
                    DisposeGeneratedCandidate(cells, cellPayload);
            }
        }
        return bestLayout
            ?? lease.Own(
                new GDictionary(),
                "BattleTerrainGenerator.GenerateCanyon.empty"
            );
    }

    private static void DisposeGeneratedCandidate(
        Dictionary<Vector2I, BattleCellState> cells,
        GDictionary cellPayload
    )
    {
        if (cells != null)
        {
            var disposedCells = new HashSet<BattleCellState>();
            foreach (BattleCellState cell in cells.Values)
            {
                if (cell != null && disposedCells.Add(cell))
                    BattleCellState.DisposeRuntimeGraph(cell);
            }
            cells.Clear();
        }
        cellPayload?.Clear();
    }

    private static void DisposeGeneratedLayout(GDictionary layout)
    {
        if (layout == null || layout.Count == 0)
            return;
        layout.Clear();
    }

    private GDictionary GenerateNarrowAssault(
        GodotProjectionLease<GDictionary> lease,
        GDictionary encounterContext,
        StringName terrainProfileId
    )
    {
        Vector2I mapSize = ResolveMapSize(
            encounterContext,
            NarrowAssaultFormalSizes[0],
            NarrowAssaultFormalSizes
        );
        int gateX = Mathf.Clamp(mapSize.X / 2, 3, mapSize.X - 4);
        int laneY = Mathf.Clamp(mapSize.Y / 2, 2, mapSize.Y - 3);
        Dictionary<Vector2I, BattleCellState> cells = BuildCellsTyped(
            mapSize,
            coord =>
            {
                int height = DefaultMinHeight + (Math.Abs(coord.Y - laneY) <= 1 ? 0 : 1);
                StringName terrain = TerrainLand;
                if (coord.X >= gateX - 2 && coord.X <= gateX - 1 && Math.Abs(coord.Y - laneY) <= 1)
                {
                    terrain = TerrainMud;
                }
                if (coord.X >= gateX + 1 && coord.X <= gateX + 2 && coord.Y == laneY)
                {
                    terrain = TerrainSpike;
                }
                return (height, terrain);
            }
        );
        RelaxOrdinarySlopeDeltas(cells, mapSize);
        GDictionary cellPayload = ToCellDictionary(lease, cells);
        AuthorSeamWall(
            lease,
            cellPayload,
            mapSize,
            gateX,
            new HashSet<int> { laneY, Math.Min(laneY + 1, mapSize.Y - 1) }
        );
        Vector2I playerCoord = FirstDryCoord(cellPayload, new Vector2I(1, laneY));
        Vector2I enemyCoord = FirstDryCoord(cellPayload, new Vector2I(mapSize.X - 2, laneY));
        AddStandardProps(
            lease,
            cellPayload,
            mapSize,
            playerCoord,
            enemyCoord,
            gateX,
            true
        );
        return BuildLayout(
            lease,
            mapSize,
            cellPayload,
            playerCoord,
            enemyCoord,
            terrainProfileId
        );
    }

    private GDictionary GenerateHoldoutPush(
        GodotProjectionLease<GDictionary> lease,
        GDictionary encounterContext,
        StringName terrainProfileId
    )
    {
        Vector2I mapSize = ResolveMapSize(
            encounterContext,
            HoldoutPushFormalSizes[0],
            HoldoutPushFormalSizes
        );
        int holdLineX = Mathf.Clamp(
            (int)Math.Round(mapSize.X * 0.62),
            mapSize.X / 2 + 1,
            mapSize.X - 4
        );
        int openingY = Mathf.Clamp(mapSize.Y / 2, 2, mapSize.Y - 3);
        Dictionary<Vector2I, BattleCellState> cells = BuildCellsTyped(
            mapSize,
            coord =>
            {
                int height = DefaultMinHeight;
                if (coord.X == holdLineX + 1)
                {
                    height += 1;
                }
                else if (coord.X > holdLineX + 1)
                {
                    height += 2;
                }
                StringName terrain = TerrainLand;
                if (
                    coord.X >= holdLineX - 2
                    && coord.X <= holdLineX - 1
                    && Math.Abs(coord.Y - openingY) <= 1
                )
                {
                    terrain = TerrainMud;
                }
                if (coord.X == holdLineX + 1 && Math.Abs(coord.Y - openingY) <= 1)
                {
                    terrain = TerrainSpike;
                }
                return (height, terrain);
            }
        );
        RelaxOrdinarySlopeDeltas(cells, mapSize);
        GDictionary cellPayload = ToCellDictionary(lease, cells);
        AuthorSeamWall(
            lease,
            cellPayload,
            mapSize,
            holdLineX,
            new HashSet<int> { openingY }
        );
        Vector2I playerCoord = FirstDryCoord(cellPayload, new Vector2I(1, openingY));
        Vector2I enemyCoord = FirstDryCoord(cellPayload, new Vector2I(mapSize.X - 2, openingY));
        AddStandardProps(
            lease,
            cellPayload,
            mapSize,
            playerCoord,
            enemyCoord,
            holdLineX,
            false
        );
        return BuildLayout(
            lease,
            mapSize,
            cellPayload,
            playerCoord,
            enemyCoord,
            terrainProfileId
        );
    }

    private GDictionary BuildLayout(
        GodotProjectionLease<GDictionary> lease,
        Vector2I mapSize,
        GDictionary cells,
        Vector2I playerCoord,
        Vector2I enemyCoord,
        StringName terrainProfileId
    )
    {
        Dictionary<Vector2I, BattleCellState> typedCells = ParseCellDictionary(cells);
        Dictionary<Vector2I, List<BattleCellState>> cellColumns =
            BattleCellState.BuildColumnsFromSurfaceCells(typedCells);
        Dictionary<Vector3I, BattleEdgeFaceState> edgeFaces = _edgeService.BuildEdgeFacesForCells(
            typedCells,
            mapSize,
            cellColumns
        );
        GArray allySpawns = lease.Own(
            new GArray(),
            "BattleTerrainGenerator.BuildLayout.ally_spawns"
        );
        foreach (Vector2I coord in CollectSpawnRing(cells, mapSize, playerCoord, edgeFaces))
            allySpawns.Add(coord);
        GArray enemySpawns = lease.Own(
            new GArray(),
            "BattleTerrainGenerator.BuildLayout.enemy_spawns"
        );
        foreach (Vector2I coord in CollectSpawnRing(cells, mapSize, enemyCoord, edgeFaces))
            enemySpawns.Add(coord);

        return lease.Own(
            new GDictionary
        {
            ["map_size"] = mapSize,
            ["cells"] = BattleCellState.ProjectCellsToPayload(lease, typedCells),
            ["cell_columns"] = BattleCellState.ProjectColumnsToPayload(lease, cellColumns),
            ["terrain_counts"] = CountTerrainCells(lease, typedCells),
            ["ally_spawns"] = allySpawns,
            ["enemy_spawns"] = enemySpawns,
            ["player_coord"] = playerCoord,
            ["enemy_coord"] = enemyCoord,
            ["terrain_profile_id"] = terrainProfileId,
        },
            "BattleTerrainGenerator.BuildLayout"
        );
    }

    private Dictionary<Vector2I, BattleCellState> BuildCellsTyped(
        Vector2I mapSize,
        Func<Vector2I, (int Height, StringName Terrain)> resolver
    )
    {
        var cells = new Dictionary<Vector2I, BattleCellState>();
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                (int height, StringName terrain) = resolver(coord);
                cells[coord] = CreateCell(coord, height, terrain);
            }
        }
        return cells;
    }

    private Dictionary<Vector2I, BattleCellState> BuildHeightfieldCells(
        Vector2I mapSize,
        long seed,
        Func<Vector2I, double> macroHeightResolver,
        Func<Vector2I, StringName> terrainResolver
    )
    {
        return BuildCellsTyped(
            mapSize,
            coord =>
            {
                double macroHeight = macroHeightResolver(coord);
                double noise = (SampleValueNoise(coord, seed, LowFrequencyNoiseScale) - 0.5) * 1.25;
                int height = Mathf.Clamp(
                    (int)Math.Round(macroHeight + noise),
                    DefaultMinHeight,
                    DefaultMaxHeight
                );
                return (height, terrainResolver(coord));
            }
        );
    }

    private static GDictionary ToCellDictionary(
        GodotProjectionLease<GDictionary> lease,
        IReadOnlyDictionary<Vector2I, BattleCellState> cells
    )
    {
        GDictionary payload = lease.Own(
            new GDictionary(),
            "BattleTerrainGenerator.ToCellDictionary"
        );
        foreach (KeyValuePair<Vector2I, BattleCellState> entry in cells)
        {
            payload[entry.Key] =
                entry.Value != null
                    ? RuntimePlainPayload.ProjectDictionaryInto(
                        lease,
                        entry.Value.BuildSnapshotPlain(),
                        $"BattleTerrainGenerator.ToCellDictionary[{entry.Key}]"
                    )
                    : lease.Own(
                        new GDictionary(),
                        $"BattleTerrainGenerator.ToCellDictionary[{entry.Key}].empty"
                    );
        }
        return payload;
    }

    private static Dictionary<Vector2I, BattleCellState> ParseCellDictionary(GDictionary cells)
    {
        var result = new Dictionary<Vector2I, BattleCellState>();
        if (cells == null)
            return result;
        foreach (Variant rawKey in cells.Keys)
        {
            if (rawKey.VariantType != Variant.Type.Vector2I)
                continue;
            Vector2I coord = rawKey.AsVector2I();
            if (!TryReadCell(cells, coord, out BattleCellState cell))
                continue;
            cell.SetCoord(coord);
            result[coord] = cell;
        }
        return result;
    }

    private static bool TryReadCell(
        GDictionary cells,
        Vector2I coord,
        out BattleCellState cell
    )
    {
        cell = null;
        return cells != null
            && cells.ContainsKey(coord)
            && BattleCellState.TryReadCellPayload(cells[coord], out cell)
            && cell != null;
    }

    private static void StoreCell(
        GodotProjectionLease<GDictionary> lease,
        GDictionary cells,
        BattleCellState cell
    )
    {
        if (cells == null || cell == null)
            return;
        cells[cell.coord] = RuntimePlainPayload.ProjectDictionaryInto(
            lease,
            cell.BuildSnapshotPlain(),
            $"BattleTerrainGenerator.StoreCell[{cell.coord}]"
        );
    }

    private static double ResolveDefaultMacroHeight(Vector2I coord, Vector2I mapSize)
    {
        int edgeDistance = Math.Min(
            Math.Min(coord.X, Math.Max(mapSize.X - 1 - coord.X, 0)),
            Math.Min(coord.Y, Math.Max(mapSize.Y - 1 - coord.Y, 0))
        );
        double edgeRise = edgeDistance <= 1 ? 0.45 : 0.0;
        double centerBias =
            Math.Abs(coord.X - mapSize.X * 0.5) <= mapSize.X * 0.18
            && Math.Abs(coord.Y - mapSize.Y * 0.5) <= mapSize.Y * 0.22
                ? -0.25
                : 0.0;
        return DefaultMinHeight + 0.75 + edgeRise + centerBias;
    }

    private static double ResolveCanyonMacroHeight(Vector2I coord, Vector2I mapSize)
    {
        double riverY = (mapSize.Y - 1) * 0.5;
        double distanceFromRiver = Math.Abs(coord.Y - riverY);
        double sideRise = Math.Max(0.0, distanceFromRiver - 1.0) * 0.42;
        double endCapRise =
            coord.X <= 1 || coord.X >= mapSize.X - 2
                ? 0.35
                : 0.0;
        return DefaultMinHeight + 0.25 + Math.Min(2.4, sideRise) + endCapRise;
    }

    private static double SampleValueNoise(Vector2I coord, long seed, int scale)
    {
        int resolvedScale = Math.Max(scale, 1);
        double sampleX = coord.X / (double)resolvedScale;
        double sampleY = coord.Y / (double)resolvedScale;
        int x0 = (int)Math.Floor(sampleX);
        int y0 = (int)Math.Floor(sampleY);
        int x1 = x0 + 1;
        int y1 = y0 + 1;
        double tx = SmoothStep(sampleX - x0);
        double ty = SmoothStep(sampleY - y0);
        double a = Lerp(HashUnit(new Vector2I(x0, y0), seed), HashUnit(new Vector2I(x1, y0), seed), tx);
        double b = Lerp(HashUnit(new Vector2I(x0, y1), seed), HashUnit(new Vector2I(x1, y1), seed), tx);
        return Lerp(a, b, ty);
    }

    private static double HashUnit(Vector2I coord, long seed)
    {
        return (StableCoordHash(coord, seed) % 10000) / 9999.0;
    }

    private static double SmoothStep(double value)
    {
        double t = Math.Clamp(value, 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }

    private static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }

    private static void RelaxOrdinarySlopeDeltas(
        IReadOnlyDictionary<Vector2I, BattleCellState> cells,
        Vector2I mapSize
    )
    {
        for (int pass = 0; pass < 4; pass++)
        {
            bool changed = false;
            foreach (Vector2I coord in CollectAllCoords(mapSize))
            {
                if (!cells.TryGetValue(coord, out BattleCellState cell))
                {
                    continue;
                }
                foreach (Vector2I offset in FourWayOffsets())
                {
                    Vector2I neighborCoord = coord + offset;
                    if (!IsInBounds(mapSize, neighborCoord))
                    {
                        continue;
                    }
                    if (!cells.TryGetValue(neighborCoord, out BattleCellState neighbor))
                    {
                        continue;
                    }
                    int delta = cell.current_height - neighbor.current_height;
                    if (Math.Abs(delta) <= MaxOrdinarySlopeDelta)
                    {
                        continue;
                    }
                    BattleCellState highCell = delta > 0 ? cell : neighbor;
                    BattleCellState lowCell = delta > 0 ? neighbor : cell;
                    int relaxedHeight = Mathf.Clamp(
                        lowCell.current_height + MaxOrdinarySlopeDelta,
                        DefaultMinHeight,
                        DefaultMaxHeight
                    );
                    if (highCell.base_height != relaxedHeight)
                    {
                        highCell.base_height = relaxedHeight;
                        highCell.RecalculateRuntimeValues();
                        changed = true;
                    }
                }
            }
            if (!changed)
            {
                return;
            }
        }
    }

    private Dictionary<Vector3I, BattleEdgeFaceState> BuildEdgeFaces(GDictionary cells, Vector2I mapSize)
    {
        Dictionary<Vector2I, BattleCellState> typedCells = ParseCellDictionary(cells);
        Dictionary<Vector2I, List<BattleCellState>> cellColumns =
            BattleCellState.BuildColumnsFromSurfaceCells(typedCells);
        return _edgeService.BuildEdgeFacesForCells(typedCells, mapSize, cellColumns);
    }

    private TerrainQualityResult ScoreCandidate(
        IReadOnlyDictionary<Vector2I, BattleCellState> cells,
        GDictionary cellPayload,
        Vector2I mapSize,
        IReadOnlyDictionary<Vector3I, BattleEdgeFaceState> edgeFaces,
        Vector2I playerCoord,
        Vector2I enemyCoord
    )
    {
        int dryCellCount = CountDryCells(cells);
        int largestDryComponent = CountLargestDryComponent(cells, mapSize, edgeFaces);
        int ordinaryBlockingSlopes = CountOrdinaryBlockingSlopes(cells, mapSize);
        int totalDryEdges = CountDryNeighborEdges(cells, mapSize);
        int playerEnemyDistance = -1;
        if (IsDryCell(cells, playerCoord) && IsDryCell(cells, enemyCoord))
        {
            var distances = BuildDistanceMap(cellPayload, mapSize, playerCoord, edgeFaces);
            distances.TryGetValue(enemyCoord, out playerEnemyDistance);
        }
        int allySpawnCount = CollectSpawnRing(cellPayload, mapSize, playerCoord, edgeFaces).Count;
        int enemySpawnCount = CollectSpawnRing(cellPayload, mapSize, enemyCoord, edgeFaces).Count;

        int score = 0;
        score += largestDryComponent * 16;
        score += Math.Max(playerEnemyDistance, 0) * 10;
        score += Math.Min(allySpawnCount, SpawnAnchorSearchLimit) * 6;
        score += Math.Min(enemySpawnCount, SpawnAnchorSearchLimit) * 6;
        score -= ordinaryBlockingSlopes * 80;
        score -= Math.Max(0, dryCellCount - largestDryComponent) * 12;

        bool passed =
            dryCellCount > 0
            && largestDryComponent * 4 >= dryCellCount * 3
            && ordinaryBlockingSlopes * 100 <= Math.Max(totalDryEdges * 3, 1)
            && playerEnemyDistance >= 4
            && allySpawnCount >= MinSpawnRingCount
            && enemySpawnCount >= MinSpawnRingCount;
        return new TerrainQualityResult(passed, score);
    }

    private static int CountDryCells(IReadOnlyDictionary<Vector2I, BattleCellState> cells)
    {
        int count = 0;
        foreach (var value in cells.Values)
        {
            if (
                value != null
                && value.passable
                && !BattleTerrainRules.IsWaterTerrain(value.base_terrain)
            )
            {
                count++;
            }
        }
        return count;
    }

    private int CountLargestDryComponent(
        IReadOnlyDictionary<Vector2I, BattleCellState> cells,
        Vector2I mapSize,
        IReadOnlyDictionary<Vector3I, BattleEdgeFaceState> edgeFaces
    )
    {
        var visited = new HashSet<Vector2I>();
        int best = 0;
        foreach (Vector2I coord in CollectAllCoords(mapSize))
        {
            if (visited.Contains(coord) || !IsDryCell(cells, coord))
            {
                continue;
            }
            best = Math.Max(best, CountConnectedDryCells(cells, mapSize, coord, visited, edgeFaces));
        }
        return best;
    }

    private int CountConnectedDryCells(
        IReadOnlyDictionary<Vector2I, BattleCellState> cells,
        Vector2I mapSize,
        Vector2I start,
        HashSet<Vector2I> visited,
        IReadOnlyDictionary<Vector3I, BattleEdgeFaceState> edgeFaces
    )
    {
        int count = 0;
        var frontier = new Stack<Vector2I>();
        visited.Add(start);
        frontier.Push(start);
        while (frontier.Count > 0)
        {
            Vector2I current = frontier.Pop();
            count++;
            foreach (Vector2I neighbor in GetNeighbors4(mapSize, current))
            {
                if (visited.Contains(neighbor))
                {
                    continue;
                }
                if (
                    IsDryCell(cells, neighbor)
                    && _edgeService.IsTraversableInCache(edgeFaces, current, neighbor)
                )
                {
                    visited.Add(neighbor);
                    frontier.Push(neighbor);
                }
            }
        }
        return count;
    }

    private static int CountDryNeighborEdges(
        IReadOnlyDictionary<Vector2I, BattleCellState> cells,
        Vector2I mapSize
    )
    {
        int count = 0;
        foreach (Vector2I coord in CollectAllCoords(mapSize))
        {
            foreach (Vector2I offset in new[] { Vector2I.Right, Vector2I.Down })
            {
                Vector2I neighborCoord = coord + offset;
                if (
                    IsInBounds(mapSize, neighborCoord)
                    && IsDryCell(cells, coord)
                    && IsDryCell(cells, neighborCoord)
                )
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static int CountOrdinaryBlockingSlopes(
        IReadOnlyDictionary<Vector2I, BattleCellState> cells,
        Vector2I mapSize
    )
    {
        int count = 0;
        foreach (Vector2I coord in CollectAllCoords(mapSize))
        {
            if (!cells.TryGetValue(coord, out BattleCellState cell))
            {
                continue;
            }
            foreach (Vector2I offset in new[] { Vector2I.Right, Vector2I.Down })
            {
                Vector2I neighborCoord = coord + offset;
                if (!IsInBounds(mapSize, neighborCoord))
                {
                    continue;
                }
                if (!cells.TryGetValue(neighborCoord, out BattleCellState neighbor))
                {
                    continue;
                }
                if (
                    !cell.passable
                    || !neighbor.passable
                    || BattleTerrainRules.IsWaterTerrain(cell.base_terrain)
                    || BattleTerrainRules.IsWaterTerrain(neighbor.base_terrain)
                )
                {
                    continue;
                }
                if (EdgeHasAuthoredMoveBlock(cell, offset))
                {
                    continue;
                }
                if (Math.Abs(cell.current_height - neighbor.current_height) > MaxOrdinarySlopeDelta)
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static bool IsDryCell(
        IReadOnlyDictionary<Vector2I, BattleCellState> cells,
        Vector2I coord
    )
    {
        return cells.TryGetValue(coord, out BattleCellState cell)
            && cell != null
            && cell.passable
            && !BattleTerrainRules.IsWaterTerrain(cell.base_terrain);
    }

    private static bool EdgeHasAuthoredMoveBlock(BattleCellState cell, Vector2I direction)
    {
        BattleEdgeFeatureState feature =
            direction == Vector2I.Right ? cell.edge_feature_east
            : direction == Vector2I.Down ? cell.edge_feature_south
            : null;
        return feature != null && feature.blocks_move;
    }

    private static BattleCellState CreateCell(Vector2I coord, int height, StringName terrain)
    {
        var cell = new BattleCellState();
        cell.SetCoord(coord);
        cell.SetBaseHeight(Mathf.Clamp(height, DefaultMinHeight, DefaultMaxHeight));
        cell.SetTerrain(terrain);
        cell.SetHeightOffset(0);
        return cell;
    }

    private static void SetTerrain(
        GodotProjectionLease<GDictionary> lease,
        GDictionary cells,
        Vector2I coord,
        StringName terrain,
        int height
    )
    {
        if (!TryReadCell(cells, coord, out BattleCellState cell))
        {
            return;
        }
        cell.base_terrain = terrain;
        cell.base_height = height;
        cell.RecalculateRuntimeValues();
        StoreCell(lease, cells, cell);
    }

    private static void SetTerrain(
        IReadOnlyDictionary<Vector2I, BattleCellState> cells,
        Vector2I coord,
        StringName terrain,
        int height
    )
    {
        if (!cells.TryGetValue(coord, out BattleCellState cell) || cell == null)
        {
            return;
        }
        cell.base_terrain = terrain;
        cell.base_height = height;
        cell.RecalculateRuntimeValues();
    }

    private static void AddConnectedWater(
        GodotProjectionLease<GDictionary> lease,
        GDictionary cells,
        Vector2I mapSize,
        Vector2I start,
        int count
    )
    {
        var frontier = new Queue<Vector2I>();
        var visited = new HashSet<Vector2I>();
        frontier.Enqueue(start);
        while (frontier.Count > 0 && visited.Count < count)
        {
            Vector2I current = frontier.Dequeue();
            if (visited.Contains(current) || !IsInBounds(mapSize, current))
            {
                continue;
            }
            visited.Add(current);
            SetTerrain(lease, cells, current, TerrainShallowWater, DefaultMinHeight);
            foreach (Vector2I offset in FourWayOffsets())
            {
                frontier.Enqueue(current + offset);
            }
        }
    }

    private static void AddConnectedWater(
        IReadOnlyDictionary<Vector2I, BattleCellState> cells,
        Vector2I mapSize,
        Vector2I start,
        int count
    )
    {
        var frontier = new Queue<Vector2I>();
        var visited = new HashSet<Vector2I>();
        frontier.Enqueue(start);
        while (frontier.Count > 0 && visited.Count < count)
        {
            Vector2I current = frontier.Dequeue();
            if (visited.Contains(current) || !IsInBounds(mapSize, current))
            {
                continue;
            }
            visited.Add(current);
            SetTerrain(cells, current, TerrainShallowWater, DefaultMinHeight);
            foreach (Vector2I offset in FourWayOffsets())
            {
                frontier.Enqueue(current + offset);
            }
        }
    }

    private static void AuthorSeamWall(
        GodotProjectionLease<GDictionary> lease,
        GDictionary cells,
        Vector2I mapSize,
        int seamX,
        HashSet<int> openRows
    )
    {
        for (int y = 0; y < mapSize.Y; y++)
        {
            if (openRows.Contains(y))
            {
                continue;
            }
            Vector2I coord = new(seamX, y);
            if (TryReadCell(cells, coord, out BattleCellState cell))
            {
                BattleEdgeFeatureState wall = BattleEdgeFeatureState.MakeWall();
                cell.SetEdgeFeature(Vector2I.Right, wall);
                StoreCell(lease, cells, cell);
            }
        }
    }

    private static void AddStandardProps(
        GodotProjectionLease<GDictionary> lease,
        GDictionary cells,
        Vector2I mapSize,
        Vector2I playerCoord,
        Vector2I enemyCoord,
        int lineX,
        bool objectiveNearLine
    )
    {
        int midY = Mathf.Clamp(mapSize.Y / 2, 1, mapSize.Y - 2);
        Vector2I objective = objectiveNearLine
            ? new Vector2I(Mathf.Clamp(lineX, 1, mapSize.X - 2), midY)
            : new Vector2I(Mathf.Clamp(lineX + 2, 1, mapSize.X - 2), midY);
        Vector2I playerTent = new(
            Mathf.Clamp(playerCoord.X + 1, 1, mapSize.X - 2),
            Mathf.Clamp(playerCoord.Y - 2, 1, mapSize.Y - 2)
        );
        Vector2I enemyTent = new(
            Mathf.Clamp(enemyCoord.X - 1, 1, mapSize.X - 2),
            Mathf.Clamp(enemyCoord.Y + 2, 1, mapSize.Y - 2)
        );
        Vector2I leftTorch = new(Mathf.Clamp(lineX - 2, 1, mapSize.X - 2), 1);
        Vector2I rightTorch = new(Mathf.Clamp(lineX + 2, 1, mapSize.X - 2), mapSize.Y - 2);

        var occupied = new HashSet<Vector2I> { playerCoord, enemyCoord };
        AddProp(
            lease,
            cells,
            FindFreePropCoord(cells, mapSize, objective, occupied),
            PropObjectiveMarker,
            occupied
        );
        AddProp(
            lease,
            cells,
            FindFreePropCoord(cells, mapSize, playerTent, occupied),
            PropTent,
            occupied
        );
        AddProp(
            lease,
            cells,
            FindFreePropCoord(cells, mapSize, enemyTent, occupied),
            PropTent,
            occupied
        );
        AddProp(
            lease,
            cells,
            FindFreePropCoord(cells, mapSize, leftTorch, occupied),
            PropTorch,
            occupied
        );
        AddProp(
            lease,
            cells,
            FindFreePropCoord(cells, mapSize, rightTorch, occupied),
            PropTorch,
            occupied
        );
    }

    private static Vector2I FindFreePropCoord(
        GDictionary cells,
        Vector2I mapSize,
        Vector2I preferred,
        HashSet<Vector2I> occupied
    )
    {
        for (int radius = 0; radius <= 3; radius++)
        {
            for (int y = preferred.Y - radius; y <= preferred.Y + radius; y++)
            {
                for (int x = preferred.X - radius; x <= preferred.X + radius; x++)
                {
                    Vector2I coord = new(x, y);
                    if (!IsInBounds(mapSize, coord) || occupied.Contains(coord))
                    {
                        continue;
                    }
                    if (
                        TryReadCell(cells, coord, out BattleCellState cell)
                        && cell.passable
                        && !BattleTerrainRules.IsWaterTerrain(cell.base_terrain)
                    )
                    {
                        return coord;
                    }
                }
            }
        }
        return preferred;
    }

    private static void AddProp(
        GodotProjectionLease<GDictionary> lease,
        GDictionary cells,
        Vector2I coord,
        StringName propId,
        HashSet<Vector2I> occupied
    )
    {
        if (!TryReadCell(cells, coord, out BattleCellState cell))
        {
            return;
        }
        if (!cell.prop_ids.Contains(propId))
        {
            cell.prop_ids.Add(propId);
        }
        StoreCell(lease, cells, cell);
        occupied.Add(coord);
    }

    private bool TryFindSpawnPair(
        GDictionary cells,
        Vector2I mapSize,
        IReadOnlyDictionary<Vector3I, BattleEdgeFaceState> edgeFaces,
        out Vector2I playerCoord,
        out Vector2I enemyCoord
    )
    {
        playerCoord = new Vector2I(-1, -1);
        enemyCoord = new Vector2I(-1, -1);
        var visited = new HashSet<Vector2I>();
        var largestComponent = new List<Vector2I>();
        foreach (Vector2I coord in CollectAllCoords(mapSize))
        {
            if (visited.Contains(coord) || !IsDrySpawnCell(cells, coord))
                continue;
            var component = CollectConnectedComponent(cells, mapSize, coord, visited, edgeFaces);
            if (component.Count > largestComponent.Count)
                largestComponent = component;
        }
        if (largestComponent.Count < 8)
            return false;

        Vector2I anchor = largestComponent[_rng.RandiRange(0, largestComponent.Count - 1)];
        var distanceFromAnchor = BuildDistanceMap(cells, mapSize, anchor, edgeFaces);
        playerCoord = PickFarthestCoord(
            largestComponent,
            distanceFromAnchor,
            cells,
            new Vector2I(-1, -1),
            true
        );
        if (playerCoord == new Vector2I(-1, -1))
            return false;

        var distanceFromPlayer = BuildDistanceMap(cells, mapSize, playerCoord, edgeFaces);
        var enemyCandidates = FilterOpposingSpawnSideCoords(
            largestComponent,
            playerCoord,
            mapSize
        );
        enemyCoord = PickFarthestCoord(
            enemyCandidates,
            distanceFromPlayer,
            cells,
            playerCoord,
            false
        );
        if (enemyCoord == new Vector2I(-1, -1))
            return false;
        return distanceFromPlayer.TryGetValue(enemyCoord, out int distance) && distance >= 4;
    }

    private List<Vector2I> CollectConnectedComponent(
        GDictionary cells,
        Vector2I mapSize,
        Vector2I start,
        HashSet<Vector2I> visited,
        IReadOnlyDictionary<Vector3I, BattleEdgeFaceState> edgeFaces
    )
    {
        var component = new List<Vector2I>();
        var frontier = new Stack<Vector2I>();
        visited.Add(start);
        frontier.Push(start);
        while (frontier.Count > 0)
        {
            Vector2I current = frontier.Pop();
            component.Add(current);
            foreach (Vector2I neighbor in GetNeighbors4(mapSize, current))
            {
                if (visited.Contains(neighbor))
                    continue;
                if (!CanTraverseEdge(cells, edgeFaces, current, neighbor))
                    continue;
                visited.Add(neighbor);
                frontier.Push(neighbor);
            }
        }
        return component;
    }

    private Dictionary<Vector2I, int> BuildDistanceMap(
        GDictionary cells,
        Vector2I mapSize,
        Vector2I start,
        IReadOnlyDictionary<Vector3I, BattleEdgeFaceState> edgeFaces
    )
    {
        var distances = new Dictionary<Vector2I, int> { [start] = 0 };
        var frontier = new Queue<Vector2I>();
        frontier.Enqueue(start);
        while (frontier.Count > 0)
        {
            Vector2I current = frontier.Dequeue();
            int currentDistance = distances[current];
            foreach (Vector2I neighbor in GetNeighbors4(mapSize, current))
            {
                if (distances.ContainsKey(neighbor))
                    continue;
                if (!CanTraverseEdge(cells, edgeFaces, current, neighbor))
                    continue;
                distances[neighbor] = currentDistance + 1;
                frontier.Enqueue(neighbor);
            }
        }
        return distances;
    }

    private static Vector2I PickFarthestCoord(
        IReadOnlyList<Vector2I> component,
        Dictionary<Vector2I, int> distances,
        GDictionary cells,
        Vector2I excludedCoord,
        bool preferSafeTerrain
    )
    {
        var bestCoord = new Vector2I(-1, -1);
        int bestScore = -1;
        foreach (Vector2I coord in component)
        {
            if (coord == excludedCoord || !distances.TryGetValue(coord, out int distance))
                continue;
            if (!IsDrySpawnCell(cells, coord))
                continue;
            TryReadCell(cells, coord, out BattleCellState cell);
            bool isSafeTerrain =
                cell != null
                && (cell.base_terrain == TerrainLand || cell.base_terrain == TerrainForest);
            int terrainBonus = isSafeTerrain ? (preferSafeTerrain ? 2 : 1) : 0;
            int score = distance * 10 + terrainBonus;
            if (score > bestScore)
            {
                bestScore = score;
                bestCoord = coord;
            }
        }
        return bestCoord;
    }

    private static List<Vector2I> FilterOpposingSpawnSideCoords(
        IReadOnlyList<Vector2I> coords,
        Vector2I playerCoord,
        Vector2I mapSize
    )
    {
        var result = new List<Vector2I>();
        if (GetLongEdgeSideExtent(mapSize) <= 1)
            return result;
        bool playerIsNearLongEdge = IsNearLongEdgeSpawnSide(playerCoord, mapSize);
        foreach (Vector2I coord in coords)
        {
            if (IsNearLongEdgeSpawnSide(coord, mapSize) != playerIsNearLongEdge)
                result.Add(coord);
        }
        return result;
    }

    private bool CanTraverseEdge(
        GDictionary cells,
        IReadOnlyDictionary<Vector3I, BattleEdgeFaceState> edgeFaces,
        Vector2I fromCoord,
        Vector2I toCoord
    )
    {
        return IsDrySpawnCell(cells, fromCoord)
            && IsDrySpawnCell(cells, toCoord)
            && _edgeService.IsTraversableInCache(edgeFaces, fromCoord, toCoord);
    }

    private List<Vector2I> CollectSpawnRing(
        GDictionary cells,
        Vector2I mapSize,
        Vector2I center,
        IReadOnlyDictionary<Vector3I, BattleEdgeFaceState> edgeFaces
    )
    {
        var result = new List<Vector2I>();
        if (!IsDrySpawnCell(cells, center))
        {
            return result;
        }
        var frontier = new Queue<Vector2I>();
        var seen = new HashSet<Vector2I> { center };
        var depthByCoord = new Dictionary<Vector2I, int> { [center] = 0 };
        int maxDepth = Math.Max(4, Math.Min(mapSize.X, mapSize.Y) / 2);
        frontier.Enqueue(center);
        while (frontier.Count > 0 && result.Count < SpawnAnchorSearchLimit)
        {
            Vector2I current = frontier.Dequeue();
            result.Add(current);
            int depth = depthByCoord[current];
            if (depth >= maxDepth)
                continue;
            foreach (Vector2I offset in FourWayOffsets())
            {
                Vector2I candidate = current + offset;
                if (seen.Contains(candidate))
                    continue;
                seen.Add(candidate);
                if (!IsInBounds(mapSize, candidate) || !IsDrySpawnCell(cells, candidate))
                    continue;
                if (!SpawnCandidateMatchesSpawnSide(candidate, mapSize, center))
                    continue;
                if (!_edgeService.IsTraversableInCache(edgeFaces, current, candidate))
                    continue;
                depthByCoord[candidate] = depth + 1;
                frontier.Enqueue(candidate);
            }
        }
        return result;
    }

    private static bool SpawnCandidateMatchesSpawnSide(
        Vector2I coord,
        Vector2I mapSize,
        Vector2I center
    )
    {
        return IsNearLongEdgeSpawnSide(coord, mapSize) == IsNearLongEdgeSpawnSide(center, mapSize);
    }

    private static bool IsDrySpawnCell(GDictionary cells, Vector2I coord)
    {
        return TryReadCell(cells, coord, out BattleCellState cell)
            && cell.passable
            && !BattleTerrainRules.IsWaterTerrain(cell.base_terrain);
    }

    private static Vector2I FirstDryCoord(GDictionary cells, Vector2I preferred)
    {
        if (IsDrySpawnCell(cells, preferred))
        {
            return preferred;
        }
        foreach (var key in cells.Keys)
        {
            if (key.VariantType == Variant.Type.Vector2I && IsDrySpawnCell(cells, key.AsVector2I()))
            {
                return key.AsVector2I();
            }
        }
        return preferred;
    }

    private static GDictionary CountTerrainCells(
        GodotProjectionLease<GDictionary> lease,
        IReadOnlyDictionary<Vector2I, BattleCellState> cells
    )
    {
        GDictionary counts = lease.Own(
            new GDictionary
            {
                [TerrainLand] = 0,
                [TerrainForest] = 0,
                [TerrainShallowWater] = 0,
                [TerrainFlowingWater] = 0,
                [TerrainDeepWater] = 0,
                [TerrainMud] = 0,
                [TerrainSpike] = 0,
            },
            "BattleTerrainGenerator.CountTerrainCells"
        );
        foreach (BattleCellState cell in cells.Values)
        {
            if (cell == null)
            {
                continue;
            }
            StringName terrain = cell.base_terrain;
            counts[terrain] = ReadInt(counts, terrain, 0) + 1;
        }
        return counts;
    }

    private static GDictionary BuildEncounterContext(
        GodotProjectionLease<GDictionary> lease,
        object encounterAnchorOrContext,
        long seed,
        GDictionary context
    )
    {
        GDictionary rawEncounterContext = ContextDictionary(encounterAnchorOrContext);
        if (
            rawEncounterContext != null
            && seed == 0
            && (context == null || context.Count == 0)
        )
        {
            return RuntimePlainPayload.ProjectDictionaryInto(
                lease,
                RuntimePlainPayload.NormalizeDictionary(
                    rawEncounterContext,
                    "BattleTerrainGenerator.BuildEncounterContext.raw"
                ),
                "BattleTerrainGenerator.BuildEncounterContext.raw"
            );
        }

        EncounterAnchorData encounterAnchor = ContextObject(encounterAnchorOrContext);
        var monster = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["entity_id"] =
                encounterAnchor != null
                    ? encounterAnchor.entity_id.ToString()
                    : "",
            ["display_name"] =
                encounterAnchor != null
                    ? encounterAnchor.display_name
                    : "",
            ["faction_id"] =
                encounterAnchor != null
                    ? encounterAnchor.faction_id.ToString()
                    : "",
            ["region_tag"] =
                encounterAnchor != null
                    ? encounterAnchor.region_tag.ToString()
                    : "",
        };
        var snapshot = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["monster"] = monster,
            ["world_coord"] =
                context != null
                    && context.ContainsKey("world_coord")
                    && context["world_coord"].VariantType == Variant.Type.Vector2I
                    ? context["world_coord"].AsVector2I()
                : encounterAnchor != null ? encounterAnchor.world_coord
                : Vector2I.Zero,
            ["world_seed"] = seed,
            ["action_points"] =
                context != null
                    && context.ContainsKey("action_points")
                    && context["action_points"].VariantType == Variant.Type.Int
                    ? context["action_points"].AsInt32()
                    : 6,
            ["battle_terrain_profile"] =
                context != null && context.ContainsKey("battle_terrain_profile")
                    ? context["battle_terrain_profile"].ToString()
                    : "",
            ["battle_map_size"] =
                context != null
                    && context.ContainsKey("battle_map_size")
                    && context["battle_map_size"].VariantType == Variant.Type.Vector2I
                    ? context["battle_map_size"].AsVector2I()
                    : Vector2I.Zero,
            ["battle_test_vertical_slice"] =
                context != null
                    && context.ContainsKey("battle_test_vertical_slice")
                    && context["battle_test_vertical_slice"].VariantType == Variant.Type.Bool
                    ? context["battle_test_vertical_slice"].AsBool()
                    : false,
        };
        return RuntimePlainPayload.ProjectDictionaryInto(
            lease,
            snapshot,
            "BattleTerrainGenerator.BuildEncounterContext"
        );
    }

    private static Vector2I ResolveMapSize(
        GDictionary encounterContext,
        Vector2I fallback,
        IReadOnlyList<Vector2I> formalSizes
    )
    {
        var configured =
            encounterContext != null && encounterContext.ContainsKey("battle_map_size")
                ? encounterContext["battle_map_size"]
                : default;
        if (configured.VariantType == Variant.Type.Vector2I)
        {
            Vector2I mapSize = configured.AsVector2I();
            if (mapSize.X > 0 && mapSize.Y > 0)
            {
                return mapSize;
            }
        }
        return formalSizes.Count > 0 ? formalSizes[0] : fallback;
    }

    private static Vector2I ResolveCanyonMapSize(GDictionary encounterContext)
    {
        var configured =
            encounterContext != null && encounterContext.ContainsKey("battle_map_size")
                ? encounterContext["battle_map_size"]
                : default;
        if (configured.VariantType == Variant.Type.Vector2I)
        {
            return configured.AsVector2I();
        }
        if (ReadBool(encounterContext, "battle_test_vertical_slice", false))
        {
            return CanyonTestSize;
        }
        return CanyonFormalSizes[0];
    }

    private static long BuildBattleSeed(GDictionary encounterContext)
    {
        using GDictionary monster = GetDictionary(encounterContext, "monster");
        Vector2I worldCoord = ReadVector2I(encounterContext, "world_coord", Vector2I.Zero);
        long worldSeed = ReadLong(encounterContext, "world_seed", 0);
        string entityId = ReadString(monster, "entity_id", "wild");
        if (worldSeed >= int.MinValue && worldSeed <= int.MaxValue)
        {
            unchecked
            {
                return (int)worldSeed
                    + StableStringHash(entityId)
                    + worldCoord.X * 92821
                    + worldCoord.Y * 68917;
            }
        }
        unchecked
        {
            return worldSeed
                + StableStringHash(entityId)
                + worldCoord.X * 92821L
                + worldCoord.Y * 68917L;
        }
    }

    private static string ResolveRegionTagFromContext(object encounterAnchorOrContext)
    {
        if (ContextDictionary(encounterAnchorOrContext) is GDictionary encounterContext)
        {
            using GDictionary monster = GetDictionary(encounterContext, "monster");
            return monster != null && monster.Count > 0
                ? ReadString(monster, "region_tag", "")
                : ReadString(encounterContext, "region_tag", "");
        }
        EncounterAnchorData encounterAnchor = ContextObject(encounterAnchorOrContext);
        return encounterAnchor != null
            ? encounterAnchor.region_tag.ToString()
            : "";
    }

    private static StringName NormalizeTerrainProfileId(string rawProfileId)
    {
        string normalized = (rawProfileId ?? "").Trim().ToLower(System.Globalization.CultureInfo.GetCultureInfo(""));
        BattleTerrainProfileKind kind = normalized switch
        {
            "" or "default" => BattleTerrainProfileKind.Default,
            "canyon" => BattleTerrainProfileKind.Canyon,
            "narrow_assault" => BattleTerrainProfileKind.NarrowAssault,
            "holdout_push" => BattleTerrainProfileKind.HoldoutPush,
            _ => BattleTerrainProfileKind.Unknown,
        };
        return ToStringName(kind);
    }

    private static GDictionary GetDictionary(GDictionary source, object key)
    {
        if (source == null || key == null)
        {
            return null;
        }
        Variant value;
        if (key is Variant variantKey)
        {
            if (!source.ContainsKey(variantKey))
                return null;
            value = source[variantKey];
        }
        else if (key is StringName stringNameKey)
        {
            if (!source.ContainsKey(stringNameKey))
                return null;
            value = source[stringNameKey];
        }
        else
        {
            string stringKey = key.ToString();
            if (!TryGetDictionaryValue(source, stringKey, out value))
                return null;
        }
        if (value.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        return value.AsGodotDictionary();
    }

    private static GDictionary ContextDictionary(object rawValue)
    {
        if (rawValue is GDictionary dictionary)
        {
            return dictionary;
        }
        if (rawValue is Variant value && value.VariantType == Variant.Type.Dictionary)
        {
            return value.AsGodotDictionary();
        }
        return null;
    }

    private static EncounterAnchorData ContextObject(object rawValue)
    {
        if (rawValue is EncounterAnchorData encounterAnchor)
        {
            return encounterAnchor;
        }
        if (rawValue is GDictionary dictionary)
        {
            return EncounterAnchorData.FromDictionary(dictionary);
        }
        if (rawValue is Variant value && value.VariantType == Variant.Type.Dictionary)
        {
            return EncounterAnchorData.FromDictionary(value.AsGodotDictionary());
        }
        return null;
    }

    private static string ReadString(GDictionary data, string key, string fallback = "")
    {
        if (!TryGetDictionaryValue(data, key, out Variant value))
        {
            return fallback;
        }
        if (value.VariantType == Variant.Type.String)
        {
            return value.AsString();
        }
        if (value.VariantType == Variant.Type.StringName)
        {
            return value.AsStringName().ToString();
        }
        return fallback;
    }

    private static int ReadInt(GDictionary data, string key, int fallback = 0)
    {
        if (!TryGetDictionaryValue(data, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static long ReadLong(GDictionary data, string key, long fallback = 0)
    {
        if (!TryGetDictionaryValue(data, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType == Variant.Type.Int ? value.AsInt64() : fallback;
    }

    private static int ReadInt(GDictionary data, StringName key, int fallback = 0)
    {
        if (data == null || key == "" || !data.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static int ReadInt(GDictionary data, Vector2I key, int fallback = 0)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static bool ReadBool(GDictionary data, string key, bool fallback = false)
    {
        if (!TryGetDictionaryValue(data, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static Vector2I ReadVector2I(GDictionary data, string key, Vector2I fallback)
    {
        if (!TryGetDictionaryValue(data, key, out Variant value))
        {
            return fallback;
        }
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    private static bool TryGetDictionaryValue(GDictionary data, string key, out Variant value)
    {
        value = default;
        if (data == null || string.IsNullOrEmpty(key))
        {
            return false;
        }
        if (data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        return false;
    }

    private static bool IsInBounds(Vector2I mapSize, Vector2I coord)
    {
        return coord.X >= 0 && coord.Y >= 0 && coord.X < mapSize.X && coord.Y < mapSize.Y;
    }

    private static IEnumerable<Vector2I> CollectAllCoords(Vector2I mapSize)
    {
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
                yield return new Vector2I(x, y);
        }
    }

    private static IEnumerable<Vector2I> GetNeighbors4(Vector2I mapSize, Vector2I coord)
    {
        foreach (Vector2I offset in FourWayOffsets())
        {
            Vector2I neighbor = coord + offset;
            if (IsInBounds(mapSize, neighbor))
                yield return neighbor;
        }
    }

    private static bool IsNearLongEdgeSpawnSide(Vector2I coord, Vector2I mapSize)
    {
        int splitValue = Mathf.FloorToInt(GetLongEdgeSideExtent(mapSize) * 0.5f);
        return GetLongEdgeSideAxisValue(coord, mapSize) < splitValue;
    }

    private static int GetLongEdgeSideAxisValue(Vector2I coord, Vector2I mapSize) =>
        mapSize.X >= mapSize.Y ? coord.Y : coord.X;

    private static int GetLongEdgeSideExtent(Vector2I mapSize) =>
        mapSize.X >= mapSize.Y ? mapSize.Y : mapSize.X;

    private static IReadOnlyList<Vector2I> FourWayOffsets()
    {
        return new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
    }

    private static int StableCoordHash(Vector2I coord, long salt = 0)
    {
        if (salt >= int.MinValue && salt <= int.MaxValue)
        {
            unchecked
            {
                int hashValue = coord.X * 73856093 + coord.Y * 19349663 + (int)salt * 83492791;
                return Math.Abs(hashValue);
            }
        }
        unchecked
        {
            long hashValue = coord.X * 73856093L + coord.Y * 19349663L + salt * 83492791L;
            return (int)(hashValue & 0x7fffffffL);
        }
    }

    private static int StableStringHash(string value)
    {
        unchecked
        {
            int hash = 23;
            foreach (char c in value ?? "")
            {
                hash = hash * 31 + c;
            }
            return hash;
        }
    }
}
