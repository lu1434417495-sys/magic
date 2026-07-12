using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal enum WorldMapFogStateKind
{
    Unexplored = 0,
    Explored = 1,
    Visible = 2,
}

public sealed class WorldMapFogSystem
{
    internal const string WorldDataFogStatesKey = "fog_states";
    internal const int PersistentStateVersion = 1;

    private Vector2I _world_size_cells = Vector2I.Zero;
    private readonly Dictionary<string, WorldMapFogFactionState> _statesByFaction =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<Vector2I>> _revealedByFaction =
        new(StringComparer.Ordinal);
    private long _persistentRevision;

    internal long PersistentRevision => _persistentRevision;

    public void Setup(Vector2I world_size_cells)
    {
        Setup(world_size_cells, null);
    }

    public void Setup(Vector2I world_size_cells, GDictionary persistent_state)
    {
        _world_size_cells = world_size_cells;
        _statesByFaction.Clear();
        _revealedByFaction.Clear();
        if (persistent_state != null && persistent_state.Count > 0)
        {
            LoadPersistentState(persistent_state);
            return;
        }
        MarkPersistentStateChanged();
    }

    public Vector2I GetWorldSizeCells() => _world_size_cells;

    internal void RebuildVisibilityForFaction(
        string factionId,
        IEnumerable<VisionSourceData> sources
    )
    {
        WorldMapFogFactionState factionState = GetOrCreateState(factionId);
        factionState.ClearVisible();
        if (sources == null)
        {
            return;
        }

        bool persistentStateChanged = false;
        foreach (VisionSourceData source in sources)
        {
            if (source == null || source.faction_id != factionId)
            {
                continue;
            }
            int range = source.range;
            Vector2I center = source.center;
            for (int offsetY = -range; offsetY <= range; offsetY += 1)
            {
                for (int offsetX = -range; offsetX <= range; offsetX += 1)
                {
                    if (Math.Abs(offsetX) + Math.Abs(offsetY) > range)
                    {
                        continue;
                    }
                    Vector2I coord = center + new Vector2I(offsetX, offsetY);
                    if (IsInsideWorld(coord))
                    {
                        persistentStateChanged |= factionState.MarkVisible(coord);
                    }
                }
            }
        }
        if (persistentStateChanged)
            MarkPersistentStateChanged();
    }

    public void MarkExplored(Vector2I coord, string faction_id)
    {
        if (IsInsideWorld(coord))
        {
            if (GetOrCreateState(faction_id).MarkExplored(coord))
                MarkPersistentStateChanged();
        }
    }

    internal List<Vector2I> RevealDiamond(
        Vector2I center,
        int revealRange,
        string factionId
    )
    {
        var revealedCoords = new List<Vector2I>();
        int radius = Math.Max(revealRange, 0);
        WorldMapFogFactionState factionState = GetOrCreateState(factionId);
        HashSet<Vector2I> revealedState = GetRevealedState(factionId);
        bool persistentStateChanged = false;
        for (int offsetY = -radius; offsetY <= radius; offsetY += 1)
        {
            for (int offsetX = -radius; offsetX <= radius; offsetX += 1)
            {
                if (Math.Abs(offsetX) + Math.Abs(offsetY) > radius)
                {
                    continue;
                }
                Vector2I coord = center + new Vector2I(offsetX, offsetY);
                if (!IsInsideWorld(coord))
                {
                    continue;
                }
                persistentStateChanged |= factionState.MarkExplored(coord);
                persistentStateChanged |= revealedState.Add(coord);
                revealedCoords.Add(coord);
            }
        }
        if (persistentStateChanged)
            MarkPersistentStateChanged();
        return revealedCoords;
    }

    public bool IsVisible(Vector2I coord, string faction_id) =>
        GetOrCreateState(faction_id).IsVisible(coord);

    public bool IsExplored(Vector2I coord, string faction_id)
    {
        return GetOrCreateState(faction_id).IsExplored(coord)
            || GetRevealedState(faction_id).Contains(coord);
    }

    public int GetFogState(Vector2I coord, string faction_id)
    {
        if (IsVisible(coord, faction_id))
            return ToFogStateValue(WorldMapFogStateKind.Visible);
        if (IsExplored(coord, faction_id))
            return ToFogStateValue(WorldMapFogStateKind.Explored);
        return ToFogStateValue(WorldMapFogStateKind.Unexplored);
    }

    internal static int ToFogStateValue(WorldMapFogStateKind kind) => (int)kind;

    internal static WorldMapFogStateKind ToFogStateKind(int value)
    {
        return value switch
        {
            (int)WorldMapFogStateKind.Explored => WorldMapFogStateKind.Explored,
            (int)WorldMapFogStateKind.Visible => WorldMapFogStateKind.Visible,
            _ => WorldMapFogStateKind.Unexplored,
        };
    }

    internal Dictionary<string, object> BuildPersistentStatePlain()
    {
        var factions = new Dictionary<string, object>(StringComparer.Ordinal);
        var factionIds = CollectFactionIds();
        factionIds.Sort();
        foreach (string factionId in factionIds)
        {
            factions[factionId] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["explored"] = SerializeCoordKeysPlain(
                    GetOrCreateState(factionId).ExploredCoords
                ),
                ["revealed"] = SerializeCoordKeysPlain(GetRevealedState(factionId)),
            };
        }
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["version"] = PersistentStateVersion,
            ["factions"] = factions,
        };
    }

    public bool LoadPersistentState(GDictionary persistent_state)
    {
        _statesByFaction.Clear();
        _revealedByFaction.Clear();
        MarkPersistentStateChanged();
        if (persistent_state == null || persistent_state.Count == 0)
        {
            return true;
        }
        if (
            !persistent_state.ContainsKey("version")
            || persistent_state["version"].VariantType != Variant.Type.Int
        )
        {
            GameLog.Error("Invalid world fog state: version must be an int.", "world.fog.invalid_version", "world");
            return false;
        }
        if (persistent_state["version"].AsInt32() != PersistentStateVersion)
        {
            GameLog.Error(
                $"Invalid world fog state: unsupported version {persistent_state["version"]}.",
                "world.fog.unsupported_version",
                "world"
            );
            return false;
        }
        if (
            !persistent_state.ContainsKey("factions")
            || persistent_state["factions"].VariantType != Variant.Type.Dictionary
        )
        {
            GameLog.Error("Invalid world fog state: factions must be a Dictionary.", "world.fog.invalid_factions", "world");
            return false;
        }

        var nextStates = new Dictionary<string, WorldMapFogFactionState>(StringComparer.Ordinal);
        var nextRevealed = new Dictionary<string, HashSet<Vector2I>>(StringComparer.Ordinal);
        using GDictionary factions = persistent_state["factions"].AsGodotDictionary();
        foreach (KeyValuePair<Variant, Variant> factionEntry in factions)
        {
            Variant factionKey = factionEntry.Key;
            if (
                factionKey.VariantType != Variant.Type.String
                && factionKey.VariantType != Variant.Type.StringName
            )
            {
                GameLog.Error("Invalid world fog state: faction keys must be String.", "world.fog.invalid_faction_key", "world");
                return false;
            }
            string factionId = factionKey.ToString().StripEdges();
            if (string.IsNullOrEmpty(factionId))
            {
                GameLog.Error("Invalid world fog state: faction id must be non-empty.", "world.fog.empty_faction_id", "world");
                return false;
            }
            Variant factionPayloadValue = factionEntry.Value;
            if (!factionPayloadValue.TryAsDictionary(out GDictionary factionPayload))
            {
                GameLog.Error("Invalid world fog state: faction payload must be a Dictionary.", "world.fog.invalid_faction_payload", "world");
                return false;
            }
            using (factionPayload)
            {
                if (!factionPayload.ContainsKey("explored") || !factionPayload.ContainsKey("revealed"))
                {
                    GameLog.Error(
                        "Invalid world fog state: faction payload requires explored and revealed arrays.",
                        "world.fog.missing_arrays",
                        "world"
                    );
                    return false;
                }
                CoordParseResult exploredResult = ParseCoordArray(factionPayload, "explored");
                CoordParseResult revealedResult = ParseCoordArray(factionPayload, "revealed");
                if (!exploredResult.Ok || !revealedResult.Ok)
                {
                    GameLog.Error(
                        "Invalid world fog state: explored/revealed must contain current coordinate payloads.",
                        "world.fog.invalid_coord_payload",
                        "world"
                    );
                    return false;
                }

                var factionState = new WorldMapFogFactionState();
                foreach (Vector2I coord in exploredResult.Coords)
                {
                    factionState.MarkExplored(coord);
                }
                var revealedState = new HashSet<Vector2I>();
                foreach (Vector2I coord in revealedResult.Coords)
                {
                    factionState.MarkExplored(coord);
                    revealedState.Add(coord);
                }
                nextStates[factionId] = factionState;
                nextRevealed[factionId] = revealedState;
            }
        }
        _statesByFaction.Clear();
        foreach (var entry in nextStates)
        {
            _statesByFaction[entry.Key] = entry.Value;
        }
        _revealedByFaction.Clear();
        foreach (var entry in nextRevealed)
        {
            _revealedByFaction[entry.Key] = entry.Value;
        }
        return true;
    }

    private WorldMapFogFactionState GetOrCreateState(string factionId)
    {
        string normalized = NormalizeFactionId(factionId);
        if (_statesByFaction.TryGetValue(normalized, out WorldMapFogFactionState state))
        {
            return state;
        }
        state = new WorldMapFogFactionState();
        _statesByFaction[normalized] = state;
        MarkPersistentStateChanged();
        return state;
    }

    private HashSet<Vector2I> GetRevealedState(string factionId)
    {
        string normalized = NormalizeFactionId(factionId);
        if (_revealedByFaction.TryGetValue(normalized, out HashSet<Vector2I> state))
        {
            return state;
        }
        state = new HashSet<Vector2I>();
        _revealedByFaction[normalized] = state;
        MarkPersistentStateChanged();
        return state;
    }

    private void MarkPersistentStateChanged()
    {
        unchecked
        {
            _persistentRevision += 1;
        }
    }

    private List<string> CollectFactionIds()
    {
        var factionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string factionId in _statesByFaction.Keys)
        {
            factionIds.Add(factionId);
        }
        foreach (string factionId in _revealedByFaction.Keys)
        {
            factionIds.Add(factionId);
        }
        var orderedIds = new List<string>(factionIds);
        orderedIds.Sort(StringComparer.Ordinal);
        return orderedIds;
    }

    private List<object> SerializeCoordKeysPlain(IEnumerable<Vector2I> coordSet)
    {
        var coords = new List<Vector2I>();
        foreach (Vector2I coord in coordSet)
        {
            if (IsInsideWorld(coord))
            {
                coords.Add(coord);
            }
        }
        coords.Sort(CompareCoords);
        var serialized = new List<object>();
        foreach (Vector2I coord in coords)
        {
            serialized.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["x"] = coord.X,
                    ["y"] = coord.Y,
                }
            );
        }
        return serialized;
    }

    private CoordParseResult ParseCoordArray(GDictionary payload, string key)
    {
        var coords = new List<Vector2I>();
        var seen = new HashSet<Vector2I>();
        if (payload == null || !payload.ContainsKey(key))
        {
            return CoordParseResult.Fail();
        }
        if (payload[key].VariantType != Variant.Type.Array)
        {
            return CoordParseResult.Fail();
        }
        using GArray coordValues = payload[key].AsGodotArray();
        foreach (var coordValue in coordValues)
        {
            if (!coordValue.TryAsDictionary(out GDictionary coordPayload))
            {
                return CoordParseResult.Fail();
            }
            using (coordPayload)
            {
                if (!coordPayload.ContainsKey("x") || !coordPayload.ContainsKey("y"))
                {
                    return CoordParseResult.Fail();
                }
                if (
                    coordPayload["x"].VariantType != Variant.Type.Int
                    || coordPayload["y"].VariantType != Variant.Type.Int
                )
                {
                    return CoordParseResult.Fail();
                }
                Vector2I coord = new(coordPayload["x"].AsInt32(), coordPayload["y"].AsInt32());
                if (!IsInsideWorld(coord))
                {
                    return CoordParseResult.Fail();
                }
                if (seen.Add(coord))
                {
                    coords.Add(coord);
                }
            }
        }
        return CoordParseResult.Success(coords);
    }

    private static string NormalizeFactionId(string factionId)
    {
        return string.IsNullOrWhiteSpace(factionId) ? "neutral" : factionId.StripEdges();
    }

    private static int CompareCoords(Vector2I left, Vector2I right)
    {
        int xCompare = left.X.CompareTo(right.X);
        return xCompare != 0 ? xCompare : left.Y.CompareTo(right.Y);
    }

    private bool IsInsideWorld(Vector2I coord)
    {
        return coord.X >= 0
            && coord.Y >= 0
            && coord.X < _world_size_cells.X
            && coord.Y < _world_size_cells.Y;
    }

    private readonly struct CoordParseResult
    {
        public readonly bool Ok;
        public readonly List<Vector2I> Coords;

        private CoordParseResult(bool ok, List<Vector2I> coords)
        {
            Ok = ok;
            Coords = coords;
        }

        public static CoordParseResult Success(List<Vector2I> coords) =>
            new(true, coords ?? new List<Vector2I>());

        public static CoordParseResult Fail() => new(false, new List<Vector2I>());
    }
}
