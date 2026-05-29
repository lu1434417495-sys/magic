using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using System;

[GlobalClass]
public partial class WorldMapDataContext : RefCounted
{
    public Godot.Collections.Dictionary root_world_data = new();
    public Godot.Collections.Dictionary active_world_data = new();
    public string active_map_id = "";
    public string active_map_display_name = "";
    public WorldMapGenerationConfig active_generation_config;
    public Godot.Collections.Dictionary world_event_by_coord = new();
    public Godot.Collections.Dictionary submap_generation_configs = new();
    public Godot.Collections.Dictionary settlement_by_coord = new();
    public Godot.Collections.Dictionary world_npc_by_coord = new();
    public Godot.Collections.Dictionary encounter_anchor_by_coord = new();
    public Godot.Collections.Dictionary settlements_by_id = new();

    public void bind_root_world_data(Godot.Collections.Dictionary worldData) =>
        root_world_data = worldData ?? new Godot.Collections.Dictionary();

    public void reset()
    {
        root_world_data = new();
        active_world_data = new();
        active_map_id = "";
        active_map_display_name = "";
        active_generation_config = null;
        world_event_by_coord = new();
        submap_generation_configs = new();
        settlement_by_coord = new();
        world_npc_by_coord = new();
        encounter_anchor_by_coord = new();
        settlements_by_id = new();
    }

    public bool is_submap_active() => active_map_id.Length > 0;

    public int get_world_step() =>
        active_world_data.ContainsKey("world_step") ? active_world_data["world_step"].AsInt32() : 0;

    public Godot.Collections.Dictionary get_active_world_data() => active_world_data;

    public WorldMapGenerationConfig get_active_generation_config() => active_generation_config;

    public Vector2I get_active_world_size_cells() =>
        active_generation_config?.get_world_size_cells() ?? Vector2I.Zero;

    public string get_active_map_id() => active_map_id;

    public string get_active_map_display_name() => active_map_display_name;

    public string get_submap_return_hint_text()
    {
        if (!is_submap_active())
            return "";
        var se = get_mounted_submap_entry(active_map_id);
        return se.ContainsKey("return_hint_text")
            ? se["return_hint_text"].AsString()
            : "点击任意地点返回原位置。";
    }

    public Godot.Collections.Dictionary sync_active_world_context(
        WorldMapGenerationConfig rootGenConfig,
        WorldMapGridSystem gridSystem,
        Vector2I playerCoord,
        Vector2I selectedCoord
    )
    {
        active_map_id = root_world_data.ContainsKey("active_submap_id")
            ? root_world_data["active_submap_id"].AsString()
            : "";
        if (active_map_id.Length > 0 && get_mounted_submap_entry(active_map_id).Count == 0)
        {
            active_map_id = "";
            root_world_data["active_submap_id"] = "";
        }
        active_world_data = _resolve_active_world_data();
        active_generation_config = _resolve_active_generation_config(rootGenConfig);
        active_map_display_name = _resolve_active_map_display_name();
        if (active_generation_config != null && gridSystem != null)
            gridSystem.setup(
                active_generation_config.world_size_in_chunks,
                active_generation_config.chunk_size
            );
        _refresh_world_event_discovery();
        _rebuild_world_coord_lookups();
        _register_settlement_footprints(gridSystem);
        var rpc = playerCoord;
        var rsc = selectedCoord;
        if (gridSystem != null && !gridSystem.is_cell_inside_world(rpc))
            rpc = _resolve_active_map_player_coord(playerCoord);
        if (gridSystem != null && !gridSystem.is_cell_inside_world(rsc))
            rsc = rpc;
        return new Godot.Collections.Dictionary
        {
            { "player_coord", rpc },
            { "selected_coord", rsc },
        };
    }

    public bool validate_world_system_size_consistency(
        WorldMapGridSystem gridSystem,
        WorldMapFogSystem fogSystem
    )
    {
        var ews = get_active_world_size_cells();
        if (ews == Vector2I.Zero)
            return true;
        if (gridSystem == null)
        {
            GameLog.Error("World map grid system is missing while validating active world size.", "world.context.missing_grid", "world");
            return false;
        }
        if (fogSystem == null)
        {
            GameLog.Error("World map fog system is missing while validating active world size.", "world.context.missing_fog", "world");
            return false;
        }
        var gws = gridSystem.get_world_size_cells();
        var fws = fogSystem.get_world_size_cells();
        if (gws != ews)
        {
            GameLog.Error($"World map grid size mismatch: expected {ews}, got {gws}.", "world.context.grid_size_mismatch", "world");
            return false;
        }
        if (fws != ews)
        {
            GameLog.Error($"World map fog size mismatch: expected {ews}, got {fws}.", "world.context.fog_size_mismatch", "world");
            return false;
        }
        return true;
    }

    public Godot.Collections.Dictionary get_settlement_at(Vector2I coord) =>
        settlement_by_coord.ContainsKey(coord)
            ? settlement_by_coord[coord].AsGodotDictionary()
            : new Godot.Collections.Dictionary();

    public Godot.Collections.Dictionary get_world_npc_at(Vector2I coord) =>
        world_npc_by_coord.ContainsKey(coord)
            ? world_npc_by_coord[coord].AsGodotDictionary()
            : new Godot.Collections.Dictionary();

    public EncounterAnchorData get_encounter_anchor_at(Vector2I coord) =>
        encounter_anchor_by_coord.ContainsKey(coord)
            ? encounter_anchor_by_coord[coord].AsGodotObject() as EncounterAnchorData
            : null;

    public EncounterAnchorData get_encounter_anchor_by_id(StringName entityId)
    {
        if (entityId == "")
            return null;
        foreach (
            EncounterAnchorData ea in Objects<EncounterAnchorData>(
                GdInterop.GetArray(active_world_data, "encounter_anchors")
            )
        )
        {
            if (ea.entity_id == entityId)
                return ea;
        }
        return null;
    }

    public Godot.Collections.Dictionary get_world_event_at(Vector2I coord)
    {
        return AsDictionary(
            world_event_by_coord.ContainsKey(coord) ? world_event_by_coord[coord] : (object)null
        );
    }

    public Godot.Collections.Dictionary get_settlement_record(string settlementId) =>
        (
            settlements_by_id.ContainsKey(settlementId)
                ? settlements_by_id[settlementId].AsGodotDictionary()
                : new Godot.Collections.Dictionary()
        ).Duplicate(true);

    public Godot.Collections.Array<Godot.Collections.Dictionary> get_all_settlement_records()
    {
        var r = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (GDictionary s in Dictionaries(GdInterop.GetArray(active_world_data, "settlements")))
            r.Add(s.Duplicate(true));
        return r;
    }

    public Godot.Collections.Dictionary get_settlement_state(string settlementId)
    {
        var s = settlements_by_id.ContainsKey(settlementId)
            ? settlements_by_id[settlementId].AsGodotDictionary()
            : new Godot.Collections.Dictionary();
        return s.ContainsKey("settlement_state")
            ? s["settlement_state"].AsGodotDictionary().Duplicate(true)
            : new Godot.Collections.Dictionary();
    }

    public bool set_active_settlement_state(
        string settlementId,
        Godot.Collections.Dictionary settlementState
    )
    {
        var arr = GdInterop.GetArray(active_world_data, "settlements");
        if (arr.Count == 0)
            return false;
        for (int i = 0; i < arr.Count; i++)
        {
            if (!TryAsDictionary(arr[i], out var sd))
                continue;
            if (
                (sd.ContainsKey("settlement_id") ? sd["settlement_id"].AsString() : "")
                != settlementId
            )
                continue;
            sd["settlement_state"] = settlementState.Duplicate(true);
            arr[i] = sd;
            active_world_data["settlements"] = arr;
            _rebuild_world_coord_lookups();
            return true;
        }
        return false;
    }

    public void remove_encounter_anchor_by_id(StringName encounterId)
    {
        if (encounterId == "")
            return;
        var remaining = new Godot.Collections.Array();
        foreach (
            EncounterAnchorData ea in Objects<EncounterAnchorData>(
                GdInterop.GetArray(active_world_data, "encounter_anchors")
            )
        )
        {
            if (ea.entity_id != encounterId)
                remaining.Add(ea);
        }
        active_world_data["encounter_anchors"] = remaining;
        _rebuild_world_coord_lookups();
    }

    public void refresh_world_event_discovery() => _refresh_world_event_discovery();

    public Godot.Collections.Dictionary get_mounted_submap_entry(string submapId)
    {
        return GdInterop.GetDictionary(
            GdInterop.GetDictionary(root_world_data, "mounted_submaps"),
            submapId
        );
    }

    public void set_mounted_submap_entry(string submapId, Godot.Collections.Dictionary submapEntry)
    {
        var ms = GdInterop.GetDictionary(root_world_data, "mounted_submaps");
        ms[submapId] = submapEntry.Duplicate(true);
        root_world_data["mounted_submaps"] = ms;
    }

    public bool ensure_submap_generated(string submapId)
    {
        var se = get_mounted_submap_entry(submapId);
        if (se.Count == 0)
            return false;
        var cwd = GdInterop.GetDictionary(se, "world_data");
        if (
            se.ContainsKey("is_generated")
            && se["is_generated"].AsBool()
            && cwd.Count > 0
        )
            return true;
        var sgc = load_submap_generation_config(submapId);
        if (sgc == null)
            return false;
        var gg = new WorldMapGridSystem();
        gg.setup(sgc.world_size_in_chunks, sgc.chunk_size);
        var ss = new WorldMapSpawnSystem();
        var swd = ss.build_world(sgc, gg);
        se["world_data"] = swd;
        se["player_coord"] = swd.ContainsKey("player_start_coord")
            ? swd["player_start_coord"]
            : sgc.player_start_coord;
        se["is_generated"] = true;
        set_mounted_submap_entry(submapId, se);
        return true;
    }

    public WorldMapGenerationConfig load_submap_generation_config(string submapId)
    {
        if (submap_generation_configs.ContainsKey(submapId))
            return submap_generation_configs[submapId].AsGodotObject() as WorldMapGenerationConfig;
        var se = get_mounted_submap_entry(submapId);
        string gcp = se.ContainsKey("generation_config_path")
            ? se["generation_config_path"].AsString()
            : "";
        if (gcp.Length == 0)
            return null;
        var gc = GD.Load<Resource>(gcp);
        if (gc is WorldMapGenerationConfig config)
        {
            submap_generation_configs[submapId] = config;
            return config;
        }
        return null;
    }

    private void _register_settlement_footprints(WorldMapGridSystem gridSystem)
    {
        if (gridSystem == null)
            return;
        foreach (GDictionary sd in Dictionaries(GdInterop.GetArray(active_world_data, "settlements")))
        {
            string eid = sd.ContainsKey("entity_id") ? sd["entity_id"].AsString() : "";
            var origin = sd.ContainsKey("origin") ? sd["origin"].AsVector2I() : Vector2I.Zero;
            var size = sd.ContainsKey("footprint_size")
                ? sd["footprint_size"].AsVector2I()
                : Vector2I.One;
            if (eid.Length == 0)
                continue;
            if (gridSystem.can_place_footprint(origin, size))
                gridSystem.register_footprint(eid, origin, size);
        }
    }

    private void _rebuild_world_coord_lookups()
    {
        settlement_by_coord.Clear();
        settlements_by_id.Clear();
        world_npc_by_coord.Clear();
        encounter_anchor_by_coord.Clear();
        world_event_by_coord.Clear();
        foreach (GDictionary sd in Dictionaries(GdInterop.GetArray(active_world_data, "settlements")))
        {
            settlements_by_id[
                sd.ContainsKey("settlement_id") ? sd["settlement_id"].AsString() : ""
            ] = sd;
            var origin = sd.ContainsKey("origin") ? sd["origin"].AsVector2I() : Vector2I.Zero;
            var size = sd.ContainsKey("footprint_size")
                ? sd["footprint_size"].AsVector2I()
                : Vector2I.One;
            for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++)
                settlement_by_coord[origin + new Vector2I(x, y)] = sd;
        }
        foreach (GDictionary nd in Dictionaries(GdInterop.GetArray(active_world_data, "world_npcs")))
        {
            world_npc_by_coord[
                nd.ContainsKey("coord") ? nd["coord"].AsVector2I() : Vector2I.Zero
            ] = nd;
        }
        foreach (
            EncounterAnchorData ea in Objects<EncounterAnchorData>(
                GdInterop.GetArray(active_world_data, "encounter_anchors")
            )
        )
        {
            encounter_anchor_by_coord[ea.world_coord] = ea;
        }
        foreach (GDictionary wed in Dictionaries(GdInterop.GetArray(active_world_data, "world_events")))
        {
            if (!wed.ContainsKey("is_discovered") || !wed["is_discovered"].AsBool())
                continue;
            world_event_by_coord[
                wed.ContainsKey("world_coord") ? wed["world_coord"].AsVector2I() : Vector2I.Zero
            ] = wed;
        }
    }

    private Godot.Collections.Dictionary _resolve_active_world_data()
    {
        if (active_map_id.Length == 0)
            return root_world_data;
        var se = get_mounted_submap_entry(active_map_id);
        var swd = GdInterop.GetDictionary(se, "world_data");
        return swd.Count > 0 ? swd : root_world_data;
    }

    private WorldMapGenerationConfig _resolve_active_generation_config(
        WorldMapGenerationConfig rootGenConfig
    ) =>
        active_map_id.Length == 0 ? rootGenConfig : load_submap_generation_config(active_map_id);

    private string _resolve_active_map_display_name()
    {
        if (active_map_id.Length == 0)
            return "大地图";
        var se = get_mounted_submap_entry(active_map_id);
        string dn = se.ContainsKey("display_name") ? se["display_name"].AsString() : "";
        return dn.Length > 0 ? dn : active_map_id;
    }

    private Vector2I _resolve_active_map_player_coord(Vector2I fallback)
    {
        if (active_map_id.Length == 0)
            return root_world_data.ContainsKey("player_start_coord")
                ? root_world_data["player_start_coord"].AsVector2I()
                : fallback;
        var se = get_mounted_submap_entry(active_map_id);
        var sc = se.ContainsKey("player_coord")
            ? se["player_coord"].AsVector2I()
            : new Vector2I(-1, -1);
        if (sc != new Vector2I(-1, -1))
            return sc;
        return active_world_data.ContainsKey("player_start_coord")
            ? active_world_data["player_start_coord"].AsVector2I()
            : Vector2I.Zero;
    }

    private void _refresh_world_event_discovery()
    {
        var arr = GdInterop.GetArray(active_world_data, "world_events");
        if (arr.Count == 0)
            return;
        bool changed = false;
        for (int i = 0; i < arr.Count; i++)
        {
            if (!TryAsDictionary(arr[i], out var we))
                continue;
            if (we.ContainsKey("is_discovered") && we["is_discovered"].AsBool())
                continue;
            if (!_is_world_event_discovery_condition_met(we))
                continue;
            we["is_discovered"] = true;
            arr[i] = we;
            changed = true;
        }
        if (changed)
        {
            active_world_data["world_events"] = arr;
            _rebuild_world_coord_lookups();
        }
    }

    private static bool _is_world_event_discovery_condition_met(Godot.Collections.Dictionary we)
    {
        string cid = (
            we.ContainsKey("discovery_condition_id") ? we["discovery_condition_id"].AsString() : ""
        ).StripEdges();
        return cid.Length == 0 || cid == "always_true";
    }

    private static GDictionary AsDictionary(object rawValue)
    {
        return TryAsDictionary(rawValue, out var value) ? value : new GDictionary();
    }

    private static bool TryAsDictionary(object rawValue, out GDictionary value)
    {
        if (rawValue is GDictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            value = variant.AsGodotDictionary();
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static T AsObject<T>(object rawValue)
        where T : GodotObject
    {
        if (rawValue is T typed)
            return typed;
        if (rawValue is GodotObject obj)
            return obj as T;
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Object)
            return variant.AsGodotObject() as T;
        return null;
    }

    private static System.Collections.Generic.IEnumerable<GDictionary> Dictionaries(GArray values)
    {
        if (values == null)
            yield break;
        foreach (object rawValue in values)
        {
            if (TryAsDictionary(rawValue, out var value))
                yield return value;
        }
    }

    private static System.Collections.Generic.IEnumerable<T> Objects<T>(GArray values)
        where T : GodotObject
    {
        if (values == null)
            yield break;
        foreach (object rawValue in values)
        {
            T value = AsObject<T>(rawValue);
            if (value != null)
                yield return value;
        }
    }
}
