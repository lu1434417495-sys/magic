using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class WorldMapFogSystem : RefCounted
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
    private GDictionary _states_by_faction = new();
    private GDictionary _revealed_by_faction = new();

    public void setup(Vector2I world_size_cells)
    {
        setup(world_size_cells, null);
    }

    public void setup(Vector2I world_size_cells, GDictionary persistent_state)
    {
        _world_size_cells = world_size_cells;
        _states_by_faction.Clear();
        _revealed_by_faction.Clear();
        if (persistent_state != null && persistent_state.Count > 0)
        {
            load_persistent_state(persistent_state);
        }
    }

    public Vector2I get_world_size_cells() => _world_size_cells;

    public void rebuild_visibility_for_faction(string faction_id, GArray sources)
    {
        WorldMapFogFactionState factionState = GetOrCreateState(faction_id);
        factionState.clear_visible();
        if (sources == null)
        {
            return;
        }

        foreach (var sourceValue in sources)
        {
            GodotObject source = sourceValue.AsGodotObject();
            if (source == null || source.Get("faction_id").ToString() != faction_id)
            {
                continue;
            }
            int range = source.Get("range").AsInt32();
            Vector2I center = source.Get("center").AsVector2I();
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
                        factionState.mark_visible(coord);
                    }
                }
            }
        }
    }

    public void mark_explored(Vector2I coord, string faction_id)
    {
        if (IsInsideWorld(coord))
        {
            GetOrCreateState(faction_id).explored[coord] = true;
        }
    }

    public Godot.Collections.Array<Vector2I> reveal_diamond(
        Vector2I center,
        int reveal_range,
        string faction_id
    )
    {
        var revealedCoords = new Godot.Collections.Array<Vector2I>();
        int radius = Math.Max(reveal_range, 0);
        WorldMapFogFactionState factionState = GetOrCreateState(faction_id);
        GDictionary revealedState = GetRevealedState(faction_id);
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
                factionState.explored[coord] = true;
                revealedState[coord] = true;
                revealedCoords.Add(coord);
            }
        }
        return revealedCoords;
    }

    public bool is_visible(Vector2I coord, string faction_id) =>
        GetOrCreateState(faction_id).is_visible(coord);

    public bool is_explored(Vector2I coord, string faction_id)
    {
        return GetOrCreateState(faction_id).is_explored(coord)
            || GetRevealedState(faction_id).ContainsKey(coord);
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
                ["explored"] = SerializeCoordKeys(GetOrCreateState(factionId).explored),
                ["revealed"] = SerializeCoordKeys(GetRevealedState(factionId)),
            };
        }
        return new GDictionary { ["version"] = PERSISTENT_STATE_VERSION, ["factions"] = factions };
    }

    public bool load_persistent_state(GDictionary persistent_state)
    {
        _states_by_faction.Clear();
        _revealed_by_faction.Clear();
        if (persistent_state == null || persistent_state.Count == 0)
        {
            return true;
        }
        if (
            !persistent_state.ContainsKey("version")
            || !GdInterop.HasInt(persistent_state, "version")
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
            || !GdInterop.HasDictionary(persistent_state, "factions")
        )
        {
            GameLog.Error("Invalid world fog state: factions must be a Dictionary.", "world.fog.invalid_factions", "world");
            return false;
        }

        var nextStates = new GDictionary();
        var nextRevealed = new GDictionary();
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
            GDictionary exploredResult = ParseCoordArray(factionPayload, "explored");
            GDictionary revealedResult = ParseCoordArray(factionPayload, "revealed");
            if (!exploredResult["ok"].AsBool() || !revealedResult["ok"].AsBool())
            {
                GameLog.Error(
                    "Invalid world fog state: explored/revealed must contain current coordinate payloads.",
                    "world.fog.invalid_coord_payload",
                    "world"
                );
                return false;
            }

            var factionState = new WorldMapFogFactionState();
            foreach (var coordValue in exploredResult["coords"].AsGodotArray())
            {
                factionState.explored[coordValue.AsVector2I()] = true;
            }
            var revealedState = new GDictionary();
            foreach (var coordValue in revealedResult["coords"].AsGodotArray())
            {
                Vector2I coord = coordValue.AsVector2I();
                factionState.explored[coord] = true;
                revealedState[coord] = true;
            }
            nextStates[factionId] = factionState;
            nextRevealed[factionId] = revealedState;
        }
        _states_by_faction = nextStates;
        _revealed_by_faction = nextRevealed;
        return true;
    }

    private WorldMapFogFactionState GetOrCreateState(string factionId)
    {
        string normalized = string.IsNullOrWhiteSpace(factionId)
            ? "neutral"
            : factionId.StripEdges();
        GdInterop.TryGet(_states_by_faction, normalized, out var existing);
        if (existing.TryAsObject(out WorldMapFogFactionState state))
        {
            return state;
        }
        state = new WorldMapFogFactionState();
        _states_by_faction[normalized] = state;
        return state;
    }

    private GDictionary GetRevealedState(string factionId)
    {
        string normalized = string.IsNullOrWhiteSpace(factionId)
            ? "neutral"
            : factionId.StripEdges();
        GdInterop.TryGet(_revealed_by_faction, normalized, out var existing);
        if (existing.TryAsDictionary(out GDictionary existingDict))
        {
            return existingDict;
        }
        var state = new GDictionary();
        _revealed_by_faction[normalized] = state;
        return state;
    }

    private Godot.Collections.Array<string> CollectFactionIds()
    {
        var factionIds = new Godot.Collections.Array<string>();
        foreach (var factionKey in _states_by_faction.Keys)
        {
            string factionId = factionKey.ToString().StripEdges();
            if (!string.IsNullOrEmpty(factionId) && !factionIds.Contains(factionId))
            {
                factionIds.Add(factionId);
            }
        }
        foreach (var factionKey in _revealed_by_faction.Keys)
        {
            string factionId = factionKey.ToString().StripEdges();
            if (!string.IsNullOrEmpty(factionId) && !factionIds.Contains(factionId))
            {
                factionIds.Add(factionId);
            }
        }
        return factionIds;
    }

    private GArray SerializeCoordKeys(GDictionary coordSet)
    {
        var coords = new Godot.Collections.Array<Vector2I>();
        foreach (var coordValue in coordSet.Keys)
        {
            if (
                coordValue.VariantType == Variant.Type.Vector2I
                && IsInsideWorld(coordValue.AsVector2I())
            )
            {
                coords.Add(coordValue.AsVector2I());
            }
        }
        coords.Sort();
        var serialized = new GArray();
        foreach (Vector2I coord in coords)
        {
            serialized.Add(new GDictionary { ["x"] = coord.X, ["y"] = coord.Y });
        }
        return serialized;
    }

    private GDictionary ParseCoordArray(GDictionary payload, string key)
    {
        var coords = new Godot.Collections.Array<Vector2I>();
        if (payload == null || !payload.ContainsKey(key))
        {
            return new GDictionary { ["ok"] = false, ["coords"] = coords };
        }
        if (!GdInterop.HasArray(payload, key))
        {
            return new GDictionary { ["ok"] = false, ["coords"] = coords };
        }
        foreach (var coordValue in GdInterop.GetArray(payload, key))
        {
            if (!coordValue.TryAsDictionary(out GDictionary coordPayload))
            {
                return new GDictionary { ["ok"] = false, ["coords"] = coords };
            }
            if (!coordPayload.ContainsKey("x") || !coordPayload.ContainsKey("y"))
            {
                return new GDictionary { ["ok"] = false, ["coords"] = coords };
            }
            if (
                !GdInterop.HasInt(coordPayload, "x")
                || !GdInterop.HasInt(coordPayload, "y")
            )
            {
                return new GDictionary { ["ok"] = false, ["coords"] = coords };
            }
            Vector2I coord = new(coordPayload["x"].AsInt32(), coordPayload["y"].AsInt32());
            if (!IsInsideWorld(coord))
            {
                return new GDictionary { ["ok"] = false, ["coords"] = coords };
            }
            if (!coords.Contains(coord))
            {
                coords.Add(coord);
            }
        }
        return new GDictionary { ["ok"] = true, ["coords"] = coords };
    }

    private bool IsInsideWorld(Vector2I coord)
    {
        return coord.X >= 0
            && coord.Y >= 0
            && coord.X < _world_size_cells.X
            && coord.Y < _world_size_cells.Y;
    }
}
