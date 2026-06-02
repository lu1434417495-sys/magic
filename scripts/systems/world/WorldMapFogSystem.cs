using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public sealed class WorldMapFogSystem
{
    public const int FOG_UNEXPLORED = 0;
    public const int FOG_EXPLORED = 1;
    public const int FOG_VISIBLE = 2;
    public const string WORLD_DATA_FOG_STATES_KEY = "fog_states";
    public const int PERSISTENT_STATE_VERSION = 1;

    public static int FOG_UNEXPLORED_ID() => FOG_UNEXPLORED;

    public static int FOG_EXPLORED_ID() => FOG_EXPLORED;

    public static int FOG_VISIBLE_ID() => FOG_VISIBLE;

    public static string WORLD_DATA_FOG_STATES_KEY_ID() => WORLD_DATA_FOG_STATES_KEY;

    public static int PERSISTENT_STATE_VERSION_ID() => PERSISTENT_STATE_VERSION;

    private Vector2I _world_size_cells = Vector2I.Zero;
    private readonly Dictionary<string, WorldMapFogFactionState> _statesByFaction =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<Vector2I>> _revealedByFaction =
        new(StringComparer.Ordinal);

    public void setup(Vector2I world_size_cells)
    {
        setup(world_size_cells, null);
    }

    public void setup(Vector2I world_size_cells, GDictionary persistent_state)
    {
        _world_size_cells = world_size_cells;
        _statesByFaction.Clear();
        _revealedByFaction.Clear();
        if (persistent_state != null && persistent_state.Count > 0)
        {
            load_persistent_state(persistent_state);
        }
    }

    public Vector2I get_world_size_cells() => _world_size_cells;

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
                        factionState.MarkVisible(coord);
                    }
                }
            }
        }
    }

    public void mark_explored(Vector2I coord, string faction_id)
    {
        if (IsInsideWorld(coord))
        {
            GetOrCreateState(faction_id).MarkExplored(coord);
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
                factionState.MarkExplored(coord);
                revealedState.Add(coord);
                revealedCoords.Add(coord);
            }
        }
        return revealedCoords;
    }

    public bool is_visible(Vector2I coord, string faction_id) =>
        GetOrCreateState(faction_id).IsVisible(coord);

    public bool is_explored(Vector2I coord, string faction_id)
    {
        return GetOrCreateState(faction_id).IsExplored(coord)
            || GetRevealedState(faction_id).Contains(coord);
    }

    public int get_fog_state(Vector2I coord, string faction_id)
    {
        if (is_visible(coord, faction_id))
            return FOG_VISIBLE;
        if (is_explored(coord, faction_id))
            return FOG_EXPLORED;
        return FOG_UNEXPLORED;
    }

    public GDictionary export_persistent_state()
    {
        var factions = new GDictionary();
        var factionIds = CollectFactionIds();
        factionIds.Sort();
        foreach (string factionId in factionIds)
        {
            factions[factionId] = new GDictionary
            {
                ["explored"] = SerializeCoordKeys(GetOrCreateState(factionId).ExploredCoords),
                ["revealed"] = SerializeCoordKeys(GetRevealedState(factionId)),
            };
        }
        return new GDictionary { ["version"] = PERSISTENT_STATE_VERSION, ["factions"] = factions };
    }

    public bool load_persistent_state(GDictionary persistent_state)
    {
        _statesByFaction.Clear();
        _revealedByFaction.Clear();
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
        if (persistent_state["version"].AsInt32() != PERSISTENT_STATE_VERSION)
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
        GDictionary factions = persistent_state["factions"].AsGodotDictionary();
        foreach (var factionKey in factions.Keys)
        {
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
            var factionPayloadValue = factions[factionKey];
            if (!factionPayloadValue.TryAsDictionary(out GDictionary factionPayload))
            {
                GameLog.Error("Invalid world fog state: faction payload must be a Dictionary.", "world.fog.invalid_faction_payload", "world");
                return false;
            }
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
        return state;
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

    private GArray SerializeCoordKeys(IEnumerable<Vector2I> coordSet)
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
        var serialized = new GArray();
        foreach (Vector2I coord in coords)
        {
            serialized.Add(new GDictionary { ["x"] = coord.X, ["y"] = coord.Y });
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
        foreach (var coordValue in payload[key].AsGodotArray())
        {
            if (!coordValue.TryAsDictionary(out GDictionary coordPayload))
            {
                return CoordParseResult.Fail();
            }
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
