using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

[GlobalClass]
public partial class BattleTerrainGenerator : RefCounted
{
    public static readonly StringName TERRAIN_LAND = "land";
    public static readonly StringName TERRAIN_FOREST = "forest";
    public static readonly StringName TERRAIN_WATER = "water";
    public static readonly StringName TERRAIN_SHALLOW_WATER = "shallow_water";
    public static readonly StringName TERRAIN_FLOWING_WATER = "flowing_water";
    public static readonly StringName TERRAIN_DEEP_WATER = "deep_water";
    public static readonly StringName TERRAIN_MUD = "mud";
    public static readonly StringName TERRAIN_SPIKE = "spike";
    public static readonly StringName PROFILE_DEFAULT = "default";
    public static readonly StringName PROFILE_CANYON = "canyon";
    public static readonly StringName PROFILE_NARROW_ASSAULT = "narrow_assault";
    public static readonly StringName PROFILE_HOLDOUT_PUSH = "holdout_push";
    public static readonly StringName PROP_OBJECTIVE_MARKER = "objective_marker";
    public static readonly StringName PROP_TENT = "tent";
    public static readonly StringName PROP_TORCH = "torch";

    private const int DefaultMinHeight = 4;
    private const int DefaultMaxHeight = 8;
    private const int SpawnAnchorSearchLimit = 32;
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
    private static readonly Vector2I[] HoldoutPushFormalSizes =
    {
        new(19, 11),
        new(21, 11),
        new(21, 13),
    };

    private readonly RandomNumberGenerator _rng = new();
    private readonly BattleEdgeService _edgeService = new();

    public GDictionary generate(GDictionary encounterContext)
    {
        return Generate(encounterContext ?? new GDictionary(), 0, new GDictionary());
    }

    public GDictionary generate(GDictionary encounterContext, int seed)
    {
        return Generate(encounterContext ?? new GDictionary(), seed, new GDictionary());
    }

    public GDictionary generate(GDictionary encounterContext, int seed, GDictionary context)
    {
        return Generate(encounterContext ?? new GDictionary(), seed, context ?? new GDictionary());
    }

    public GDictionary generate(GodotObject encounterAnchor, int seed, GDictionary context)
    {
        return Generate(encounterAnchor, seed, context ?? new GDictionary());
    }

    private GDictionary Generate(object encounterAnchorOrContext, int seed, GDictionary context)
    {
        GDictionary encounterContext = BuildEncounterContext(
            encounterAnchorOrContext,
            seed,
            context
        );
        StringName terrainProfileId = ResolveTerrainProfileId(
            encounterAnchorOrContext,
            context
        );
        if (GdInterop.IsEmpty(terrainProfileId))
        {
            return new GDictionary();
        }

        int battleSeed = BuildBattleSeed(encounterContext);
        _rng.Seed = unchecked((ulong)battleSeed);

        if (terrainProfileId == PROFILE_CANYON)
        {
            return GenerateCanyon(encounterContext, terrainProfileId);
        }
        if (terrainProfileId == PROFILE_NARROW_ASSAULT)
        {
            return GenerateNarrowAssault(encounterContext, terrainProfileId);
        }
        if (terrainProfileId == PROFILE_HOLDOUT_PUSH)
        {
            return GenerateHoldoutPush(encounterContext, terrainProfileId);
        }
        return GenerateDefault(encounterContext, terrainProfileId);
    }

    public StringName _resolve_terrain_profile_id(
        GDictionary encounterContext,
        GDictionary context
    )
    {
        return ResolveTerrainProfileId(
            encounterContext ?? new GDictionary(),
            context ?? new GDictionary()
        );
    }

    private StringName ResolveTerrainProfileId(
        object encounterAnchorOrContext,
        GDictionary context
    )
    {
        context ??= new GDictionary();
        string rawProfileId = "";
        bool hasExplicitProfile = false;
        if (context.ContainsKey("battle_terrain_profile"))
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
                GDictionary monster = GetDictionary(encounterContext, "monster");
                rawProfileId = GdInterop.GetString(monster, "region_tag", "");
            }
        }
        else
        {
            GodotObject encounterAnchor = ContextObject(encounterAnchorOrContext);
            if (encounterAnchor != null)
            {
                rawProfileId = GdInterop.GetString(encounterAnchor, "region_tag", "");
            }
        }

        if (string.IsNullOrWhiteSpace(rawProfileId))
        {
            rawProfileId = ResolveRegionTagFromContext(encounterAnchorOrContext);
        }
        StringName terrainProfileId = NormalizeTerrainProfileId(rawProfileId);
        if (GdInterop.IsEmpty(terrainProfileId) && !hasExplicitProfile)
        {
            return PROFILE_DEFAULT;
        }
        return terrainProfileId;
    }

    public void _normalize_water_heights(GDictionary heights, GDictionary waterCells)
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
                    GdInterop.GetInt(heights, current, DefaultMaxHeight)
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

    private GDictionary GenerateDefault(GDictionary encounterContext, StringName terrainProfileId)
    {
        Vector2I mapSize = ResolveMapSize(
            encounterContext,
            DefaultFormalSizes[0],
            DefaultFormalSizes
        );
        GDictionary cells = BuildCells(
            mapSize,
            coord =>
            {
                int height =
                    DefaultMinHeight
                    + StableCoordHash(coord, BuildBattleSeed(encounterContext)) % 3;
                StringName terrain = (coord.X + coord.Y) % 7 == 0 ? TERRAIN_FOREST : TERRAIN_LAND;
                return (height, terrain);
            }
        );
        AddConnectedWater(
            cells,
            mapSize,
            new Vector2I(Math.Max(1, mapSize.X / 3), Math.Max(1, mapSize.Y / 2)),
            5
        );
        Vector2I playerCoord = FirstDryCoord(cells, new Vector2I(1, Math.Max(1, mapSize.Y / 2)));
        Vector2I enemyCoord = FirstDryCoord(
            cells,
            new Vector2I(mapSize.X - 2, Math.Max(1, mapSize.Y / 2))
        );
        return BuildLayout(mapSize, cells, playerCoord, enemyCoord, terrainProfileId);
    }

    private GDictionary GenerateCanyon(GDictionary encounterContext, StringName terrainProfileId)
    {
        Vector2I mapSize = ResolveCanyonMapSize(encounterContext);
        int battleSeed = BuildBattleSeed(encounterContext);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            int seed = battleSeed + attempt * 1777;
            _rng.Seed = unchecked((ulong)seed);
            GDictionary cells = BuildCells(
                mapSize,
                coord =>
                {
                    int ridge = Math.Abs(coord.Y - mapSize.Y / 2) <= 1 ? 0 : 1;
                    int height = Mathf.Clamp(
                        DefaultMinHeight + ridge + StableCoordHash(coord, seed) % 3,
                        DefaultMinHeight,
                        DefaultMaxHeight
                    );
                    StringName terrain =
                        StableCoordHash(coord, seed + 11) % 5 == 0
                            ? TERRAIN_FOREST
                            : TERRAIN_LAND;
                    return (height, terrain);
                }
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
                    TERRAIN_SHALLOW_WATER,
                    DefaultMinHeight
                );
            }

            GDictionary cellColumns = BattleCellState.build_columns_from_surface_cells(cells);
            GDictionary edgeFaces = _edgeService.build_edge_faces_for_cells(
                cells,
                mapSize,
                cellColumns
            );
            if (!TryFindSpawnPair(cells, mapSize, edgeFaces, out var playerCoord, out var enemyCoord))
                continue;
            AddStandardProps(cells, mapSize, playerCoord, enemyCoord, mapSize.X / 2, true);
            return BuildLayout(mapSize, cells, playerCoord, enemyCoord, terrainProfileId);
        }
        return new GDictionary();
    }

    private GDictionary GenerateNarrowAssault(
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
        GDictionary cells = BuildCells(
            mapSize,
            coord =>
            {
                int height = DefaultMinHeight + (Math.Abs(coord.Y - laneY) <= 1 ? 0 : 1);
                StringName terrain = TERRAIN_LAND;
                if (coord.X >= gateX - 2 && coord.X <= gateX - 1 && Math.Abs(coord.Y - laneY) <= 1)
                {
                    terrain = TERRAIN_MUD;
                }
                if (coord.X >= gateX + 1 && coord.X <= gateX + 2 && coord.Y == laneY)
                {
                    terrain = TERRAIN_SPIKE;
                }
                return (height, terrain);
            }
        );
        AuthorSeamWall(
            cells,
            mapSize,
            gateX,
            new HashSet<int> { laneY, Math.Min(laneY + 1, mapSize.Y - 1) }
        );
        Vector2I playerCoord = FirstDryCoord(cells, new Vector2I(1, laneY));
        Vector2I enemyCoord = FirstDryCoord(cells, new Vector2I(mapSize.X - 2, laneY));
        AddStandardProps(cells, mapSize, playerCoord, enemyCoord, gateX, true);
        return BuildLayout(mapSize, cells, playerCoord, enemyCoord, terrainProfileId);
    }

    private GDictionary GenerateHoldoutPush(
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
        GDictionary cells = BuildCells(
            mapSize,
            coord =>
            {
                int height = DefaultMinHeight + (coord.X > holdLineX ? 2 : 0);
                StringName terrain = TERRAIN_LAND;
                if (
                    coord.X >= holdLineX - 2
                    && coord.X <= holdLineX - 1
                    && Math.Abs(coord.Y - openingY) <= 1
                )
                {
                    terrain = TERRAIN_MUD;
                }
                if (coord.X == holdLineX + 1 && Math.Abs(coord.Y - openingY) <= 1)
                {
                    terrain = TERRAIN_SPIKE;
                }
                return (height, terrain);
            }
        );
        AuthorSeamWall(cells, mapSize, holdLineX, new HashSet<int> { openingY });
        Vector2I playerCoord = FirstDryCoord(cells, new Vector2I(1, openingY));
        Vector2I enemyCoord = FirstDryCoord(cells, new Vector2I(mapSize.X - 2, openingY));
        AddStandardProps(cells, mapSize, playerCoord, enemyCoord, holdLineX, false);
        return BuildLayout(mapSize, cells, playerCoord, enemyCoord, terrainProfileId);
    }

    private GDictionary BuildLayout(
        Vector2I mapSize,
        GDictionary cells,
        Vector2I playerCoord,
        Vector2I enemyCoord,
        StringName terrainProfileId
    )
    {
        GDictionary cellColumns = BattleCellState.build_columns_from_surface_cells(cells);
        GDictionary edgeFaces = _edgeService.build_edge_faces_for_cells(
            cells,
            mapSize,
            cellColumns
        );
        return new GDictionary
        {
            ["map_size"] = mapSize,
            ["cells"] = cells,
            ["cell_columns"] = cellColumns,
            ["terrain_counts"] = CountTerrainCells(cells),
            ["ally_spawns"] = CollectSpawnRing(cells, mapSize, playerCoord, edgeFaces),
            ["enemy_spawns"] = CollectSpawnRing(cells, mapSize, enemyCoord, edgeFaces),
            ["player_coord"] = playerCoord,
            ["enemy_coord"] = enemyCoord,
            ["terrain_profile_id"] = terrainProfileId,
        };
    }

    private GDictionary BuildCells(
        Vector2I mapSize,
        Func<Vector2I, (int Height, StringName Terrain)> resolver
    )
    {
        var cells = new GDictionary();
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

    private static BattleCellState CreateCell(Vector2I coord, int height, StringName terrain)
    {
        var cell = new BattleCellState
        {
            coord = coord,
            base_height = Mathf.Clamp(height, DefaultMinHeight, DefaultMaxHeight),
            base_terrain = terrain,
            height_offset = 0,
            prop_ids = new Godot.Collections.Array<StringName>(),
            terrain_effect_ids = new Godot.Collections.Array<StringName>(),
            timed_terrain_effects = new Godot.Collections.Array<BattleTerrainEffectState>(),
            edge_feature_east = BattleEdgeFeatureState.make_none(),
            edge_feature_south = BattleEdgeFeatureState.make_none(),
        };
        cell.recalculate_runtime_values();
        return cell;
    }

    private static void SetTerrain(
        GDictionary cells,
        Vector2I coord,
        StringName terrain,
        int height
    )
    {
        if (cells[coord].AsGodotObject() is not BattleCellState cell)
        {
            return;
        }
        cell.base_terrain = terrain;
        cell.base_height = height;
        cell.recalculate_runtime_values();
    }

    private static void AddConnectedWater(
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
            SetTerrain(cells, current, TERRAIN_SHALLOW_WATER, DefaultMinHeight);
            foreach (Vector2I offset in FourWayOffsets())
            {
                frontier.Enqueue(current + offset);
            }
        }
    }

    private static void AuthorSeamWall(
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
            if (cells[new Vector2I(seamX, y)].AsGodotObject() is BattleCellState cell)
            {
                cell.set_edge_feature(Vector2I.Right, BattleEdgeFeatureState.make_wall());
            }
        }
    }

    private static void AddStandardProps(
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
            cells,
            FindFreePropCoord(cells, mapSize, objective, occupied),
            PROP_OBJECTIVE_MARKER,
            occupied
        );
        AddProp(
            cells,
            FindFreePropCoord(cells, mapSize, playerTent, occupied),
            PROP_TENT,
            occupied
        );
        AddProp(cells, FindFreePropCoord(cells, mapSize, enemyTent, occupied), PROP_TENT, occupied);
        AddProp(
            cells,
            FindFreePropCoord(cells, mapSize, leftTorch, occupied),
            PROP_TORCH,
            occupied
        );
        AddProp(
            cells,
            FindFreePropCoord(cells, mapSize, rightTorch, occupied),
            PROP_TORCH,
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
                        cells[coord].AsGodotObject() is BattleCellState cell
                        && cell.passable
                        && !BattleTerrainRules.is_water_terrain(cell.base_terrain)
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
        GDictionary cells,
        Vector2I coord,
        StringName propId,
        HashSet<Vector2I> occupied
    )
    {
        if (cells[coord].AsGodotObject() is not BattleCellState cell)
        {
            return;
        }
        if (!cell.prop_ids.Contains(propId))
        {
            cell.prop_ids.Add(propId);
        }
        occupied.Add(coord);
    }

    private bool TryFindSpawnPair(
        GDictionary cells,
        Vector2I mapSize,
        GDictionary edgeFaces,
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
        GDictionary edgeFaces
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
        GDictionary edgeFaces
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
            var cell = cells[coord].AsGodotObject() as BattleCellState;
            bool isSafeTerrain =
                cell != null
                && (cell.base_terrain == TERRAIN_LAND || cell.base_terrain == TERRAIN_FOREST);
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
        GDictionary edgeFaces,
        Vector2I fromCoord,
        Vector2I toCoord
    )
    {
        return IsDrySpawnCell(cells, fromCoord)
            && IsDrySpawnCell(cells, toCoord)
            && _edgeService.is_traversable_in_cache(edgeFaces, fromCoord, toCoord);
    }

    private GVector2IArray CollectSpawnRing(
        GDictionary cells,
        Vector2I mapSize,
        Vector2I center,
        GDictionary edgeFaces
    )
    {
        var result = new GVector2IArray();
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
                if (!_edgeService.is_traversable_in_cache(edgeFaces, current, candidate))
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
        return cells.ContainsKey(coord)
            && cells[coord].AsGodotObject() is BattleCellState cell
            && cell.passable
            && !BattleTerrainRules.is_water_terrain(cell.base_terrain);
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

    private static GDictionary CountTerrainCells(GDictionary cells)
    {
        var counts = new GDictionary
        {
            [TERRAIN_LAND] = 0,
            [TERRAIN_FOREST] = 0,
            [TERRAIN_SHALLOW_WATER] = 0,
            [TERRAIN_FLOWING_WATER] = 0,
            [TERRAIN_DEEP_WATER] = 0,
            [TERRAIN_MUD] = 0,
            [TERRAIN_SPIKE] = 0,
        };
        foreach (var value in cells.Values)
        {
            if (value.AsGodotObject() is not BattleCellState cell)
            {
                continue;
            }
            StringName terrain = cell.base_terrain;
            counts[terrain] = GdInterop.GetInt(counts, terrain, 0) + 1;
        }
        return counts;
    }

    private static GDictionary BuildEncounterContext(
        object encounterAnchorOrContext,
        int seed,
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
            return rawEncounterContext.Duplicate(true);
        }

        GodotObject encounterAnchor = ContextObject(encounterAnchorOrContext);
        var monster = new GDictionary
        {
            ["entity_id"] =
                encounterAnchor != null
                    ? GdInterop.GetString(encounterAnchor, "entity_id", "")
                    : "",
            ["display_name"] =
                encounterAnchor != null
                    ? GdInterop.GetString(encounterAnchor, "display_name", "")
                    : "",
            ["faction_id"] =
                encounterAnchor != null
                    ? GdInterop.GetString(encounterAnchor, "faction_id", "")
                    : "",
            ["region_tag"] =
                encounterAnchor != null
                    ? GdInterop.GetString(encounterAnchor, "region_tag", "")
                    : "",
        };
        return new GDictionary
        {
            ["monster"] = monster,
            ["world_coord"] =
                context != null && context.ContainsKey("world_coord") ? context["world_coord"]
                : encounterAnchor != null ? encounterAnchor.Get("world_coord")
                : Vector2I.Zero,
            ["world_seed"] = seed,
            ["action_points"] =
                context != null && context.ContainsKey("action_points")
                    ? context["action_points"]
                    : 6,
            ["battle_terrain_profile"] =
                context != null && context.ContainsKey("battle_terrain_profile")
                    ? context["battle_terrain_profile"]
                    : "",
            ["battle_map_size"] =
                context != null && context.ContainsKey("battle_map_size")
                    ? context["battle_map_size"]
                    : default(Variant),
            ["battle_test_vertical_slice"] =
                context != null && context.ContainsKey("battle_test_vertical_slice")
                    ? context["battle_test_vertical_slice"]
                    : false,
        };
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
        if (GdInterop.GetBool(encounterContext, "battle_test_vertical_slice", false))
        {
            return CanyonTestSize;
        }
        return CanyonFormalSizes[0];
    }

    private static int BuildBattleSeed(GDictionary encounterContext)
    {
        GDictionary monster = GetDictionary(encounterContext, "monster");
        Vector2I worldCoord = GdInterop.GetVector2I(encounterContext, "world_coord", Vector2I.Zero);
        int worldSeed = GdInterop.GetInt(encounterContext, "world_seed", 0);
        string entityId = GdInterop.GetString(monster, "entity_id", "wild");
        return worldSeed + StableStringHash(entityId) + worldCoord.X * 92821 + worldCoord.Y * 68917;
    }

    private static string ResolveRegionTagFromContext(object encounterAnchorOrContext)
    {
        if (ContextDictionary(encounterAnchorOrContext) is GDictionary encounterContext)
        {
            GDictionary monster = GetDictionary(encounterContext, "monster");
            return monster.Count > 0
                ? GdInterop.GetString(monster, "region_tag", "")
                : GdInterop.GetString(encounterContext, "region_tag", "");
        }
        GodotObject encounterAnchor = ContextObject(encounterAnchorOrContext);
        return encounterAnchor != null
            ? GdInterop.GetString(encounterAnchor, "region_tag", "")
            : "";
    }

    private static StringName NormalizeTerrainProfileId(string rawProfileId)
    {
        string normalized = (rawProfileId ?? "").Trim().ToLower(System.Globalization.CultureInfo.GetCultureInfo(""));
        return normalized switch
        {
            "" or "default" => PROFILE_DEFAULT,
            "canyon" => PROFILE_CANYON,
            "narrow_assault" => PROFILE_NARROW_ASSAULT,
            "holdout_push" => PROFILE_HOLDOUT_PUSH,
            _ => new StringName(""),
        };
    }

    private static GDictionary GetDictionary(GDictionary source, object key)
    {
        if (source == null || key == null)
        {
            return new GDictionary();
        }
        Variant value;
        if (key is Variant variantKey)
        {
            if (!source.ContainsKey(variantKey))
                return new GDictionary();
            value = source[variantKey];
        }
        else if (key is StringName stringNameKey)
        {
            if (!source.ContainsKey(stringNameKey))
                return new GDictionary();
            value = source[stringNameKey];
        }
        else
        {
            string stringKey = key.ToString();
            if (!source.ContainsKey(stringKey))
                return new GDictionary();
            value = source[stringKey];
        }
        if (value.VariantType != Variant.Type.Dictionary)
        {
            return new GDictionary();
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

    private static GodotObject ContextObject(object rawValue)
    {
        if (rawValue is GodotObject obj)
        {
            return obj;
        }
        if (rawValue is Variant value && value.VariantType == Variant.Type.Object)
        {
            return value.AsGodotObject();
        }
        return null;
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

    private static int StableCoordHash(Vector2I coord, int salt = 0)
    {
        return Math.Abs(coord.X * 73856093 + coord.Y * 19349663 + salt * 83492791);
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
