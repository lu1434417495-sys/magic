using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using System;
using System.Collections.Generic;

public sealed class WorldMapDataContext
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
    public Godot.Collections.Dictionary settlements_by_id = new();
    private readonly Dictionary<Vector2I, EncounterAnchorData> _encounterAnchorByCoord = new();

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
        settlements_by_id = new();
        _encounterAnchorByCoord.Clear();
    }

    public void Dispose() => reset();

    public bool is_submap_active() => active_map_id.Length > 0;

    public int get_world_step() =>
        active_world_data.ContainsKey("world_step") ? active_world_data["world_step"].AsInt32() : 0;

    public string get_player_start_settlement_name() =>
        GetString(active_world_data, "player_start_settlement_name");

    public Godot.Collections.Dictionary get_active_world_data() => active_world_data;

    public WorldMapGenerationConfig get_active_generation_config() => active_generation_config;

    public GDictionary GetActiveWorldFogState()
    {
        if (active_world_data.Count == 0)
            return new GDictionary();
        return GetDictionary(active_world_data, WorldMapFogSystem.WORLD_DATA_FOG_STATES_KEY_ID());
    }

    public bool SaveActiveWorldFogState(WorldMapFogSystem fogSystem)
    {
        if (
            active_world_data.Count == 0
            || active_generation_config == null
            || fogSystem == null
        )
            return false;
        active_world_data[
            WorldMapFogSystem.WORLD_DATA_FOG_STATES_KEY_ID()
        ] = fogSystem.export_persistent_state();
        if (is_submap_active())
        {
            var submapEntry = get_mounted_submap_entry(active_map_id);
            if (submapEntry.Count > 0)
            {
                submapEntry["world_data"] = active_world_data;
                set_mounted_submap_entry(active_map_id, submapEntry);
            }
        }
        return true;
    }

    public Vector2I get_active_world_size_cells() =>
        active_generation_config?.get_world_size_cells() ?? Vector2I.Zero;

    public string get_active_map_id() => active_map_id;

    public string get_active_map_display_name() => active_map_display_name;

    public string get_submap_return_hint_text()
    {
        if (!is_submap_active())
            return "";
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(
            get_mounted_submap_entry(active_map_id)
        );
        return submap.ReturnHintText.Length > 0
            ? submap.ReturnHintText
            : "点击任意地点返回原位置。";
    }

    public WorldMapContextSyncResult SyncActiveWorldContext(
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
        return new WorldMapContextSyncResult(rpc, rsc);
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

    public WorldMapSettlementData GetSettlementAt(Vector2I coord) =>
        WorldMapSettlementData.FromDictionary(get_settlement_at(coord));

    public Godot.Collections.Dictionary get_world_npc_at(Vector2I coord) =>
        world_npc_by_coord.ContainsKey(coord)
            ? world_npc_by_coord[coord].AsGodotDictionary()
            : new Godot.Collections.Dictionary();

    public WorldMapNpcData GetWorldNpcAt(Vector2I coord) =>
        WorldMapNpcData.FromDictionary(get_world_npc_at(coord));

    public EncounterAnchorData get_encounter_anchor_at(Vector2I coord) =>
        _encounterAnchorByCoord.TryGetValue(coord, out EncounterAnchorData encounterAnchor)
            ? encounterAnchor
            : null;

    public List<EncounterAnchorData> GetActiveEncounterAnchors(bool includeCleared = true)
    {
        var anchors = new List<EncounterAnchorData>();
        foreach (EncounterAnchorData encounterAnchor in Objects<EncounterAnchorData>(
            GetArray(active_world_data, "encounter_anchors")
        ))
        {
            if (!includeCleared && encounterAnchor.is_cleared)
                continue;
            anchors.Add(encounterAnchor);
        }
        return anchors;
    }

    public EncounterAnchorData get_encounter_anchor_by_id(StringName entityId)
    {
        if (entityId == "")
            return null;
        foreach (EncounterAnchorData ea in GetActiveEncounterAnchors())
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

    public WorldMapEventData GetWorldEventAt(Vector2I coord)
    {
        return WorldMapEventData.FromDictionary(get_world_event_at(coord));
    }

    public List<WorldMapEventData> GetDiscoveredWorldEvents()
    {
        var events = new List<WorldMapEventData>();
        foreach (GDictionary worldEvent in Dictionaries(GetArray(active_world_data, "world_events")))
        {
            WorldMapEventData eventData = WorldMapEventData.FromDictionary(worldEvent);
            if (eventData != null && eventData.IsDiscovered)
            {
                events.Add(eventData);
            }
        }
        return events;
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
        foreach (GDictionary s in Dictionaries(GetArray(active_world_data, "settlements")))
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

    public WorldMapSettlementStateData GetSettlementStateData(string settlementId) =>
        WorldMapSettlementStateData.FromDictionary(get_settlement_state(settlementId));

    public bool IsSettlementVisited(string settlementId) =>
        GetSettlementStateData(settlementId).Visited;

    public bool MarkSettlementVisited(string settlementId)
    {
        GDictionary settlementState = get_settlement_state(settlementId);
        if (WorldMapSettlementStateData.FromDictionary(settlementState).Visited)
        {
            return false;
        }
        settlementState["visited"] = true;
        return set_active_settlement_state(settlementId, settlementState);
    }

    public bool set_active_settlement_state(
        string settlementId,
        Godot.Collections.Dictionary settlementState
    )
    {
        var arr = GetArray(active_world_data, "settlements");
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
        foreach (EncounterAnchorData ea in GetActiveEncounterAnchors())
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
        return GetDictionary(GetDictionary(root_world_data, "mounted_submaps"), submapId);
    }

    public void set_mounted_submap_entry(string submapId, Godot.Collections.Dictionary submapEntry)
    {
        var ms = GetDictionary(root_world_data, "mounted_submaps");
        ms[submapId] = submapEntry.Duplicate(true);
        root_world_data["mounted_submaps"] = ms;
    }

    public string GetMountedSubmapDisplayName(string submapId, string fallback = "")
    {
        var submapEntry = get_mounted_submap_entry(submapId);
        if (submapEntry.Count == 0)
        {
            return string.IsNullOrEmpty(fallback) ? submapId : fallback;
        }
        string displayName = GetString(submapEntry, "display_name");
        return string.IsNullOrEmpty(displayName)
            ? (string.IsNullOrEmpty(fallback) ? submapId : fallback)
            : displayName;
    }

    public WorldMapSubmapEnterResult EnterSubmap(
        string submapId,
        string sourceMapId,
        Vector2I sourceCoord
    )
    {
        if (string.IsNullOrEmpty(submapId))
        {
            return WorldMapSubmapEnterResult.Fail("子地图标识不能为空。");
        }
        if (!ensure_submap_generated(submapId))
        {
            return WorldMapSubmapEnterResult.Fail("子地图生成失败。");
        }
        var submapEntry = get_mounted_submap_entry(submapId);
        if (submapEntry.Count == 0)
        {
            return WorldMapSubmapEnterResult.Fail("未找到目标子地图。");
        }

        var returnStack = GetArray(root_world_data, "submap_return_stack");
        returnStack.Add(
            new GDictionary
            {
                ["map_id"] = sourceMapId ?? "",
                ["coord"] = sourceCoord,
            }
        );
        root_world_data["submap_return_stack"] = returnStack;
        root_world_data["active_submap_id"] = submapId;

        WorldMapMountedSubmapData targetSubmap = WorldMapMountedSubmapData.FromDictionary(
            submapEntry
        );
        GDictionary targetWorldData = targetSubmap.WorldData;
        Vector2I targetCoord = targetSubmap.HasPlayerCoord
            ? targetSubmap.PlayerCoord
            : GetVector2I(targetWorldData, "player_start_coord", Vector2I.Zero);
        string targetDisplayName = targetSubmap.DisplayNameOrFallback(submapId);
        return WorldMapSubmapEnterResult.Success(targetCoord, targetDisplayName);
    }

    public WorldMapSubmapReturnResult ReturnFromActiveSubmap(Vector2I currentPlayerCoord)
    {
        if (!is_submap_active())
        {
            return WorldMapSubmapReturnResult.Fail("当前不在子地图中。");
        }

        var submapEntry = get_mounted_submap_entry(active_map_id);
        if (submapEntry.Count > 0)
        {
            submapEntry["player_coord"] = currentPlayerCoord;
            set_mounted_submap_entry(active_map_id, submapEntry);
        }

        var returnStack = GetArray(root_world_data, "submap_return_stack");
        if (returnStack.Count == 0)
        {
            return WorldMapSubmapReturnResult.Fail("当前没有可返回的原坐标。");
        }
        Variant returnEntryValue = returnStack[returnStack.Count - 1];
        returnStack.RemoveAt(returnStack.Count - 1);
        GDictionary returnEntry = TryAsDictionary(returnEntryValue, out var typedReturnEntry)
            ? typedReturnEntry
            : new GDictionary();

        string targetMapId = GetString(returnEntry, "map_id");
        Vector2I targetCoord = GetVector2I(returnEntry, "coord", Vector2I.Zero);
        root_world_data["submap_return_stack"] = returnStack;
        root_world_data["active_submap_id"] = targetMapId;
        return WorldMapSubmapReturnResult.Success(targetMapId, targetCoord);
    }

    public bool ensure_submap_generated(string submapId)
    {
        var se = get_mounted_submap_entry(submapId);
        if (se.Count == 0)
            return false;
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(se);
        if (submap.IsGenerated && submap.WorldData.Count > 0)
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
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(
            get_mounted_submap_entry(submapId)
        );
        string gcp = submap.GenerationConfigPath;
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
        foreach (GDictionary sd in Dictionaries(GetArray(active_world_data, "settlements")))
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
        _encounterAnchorByCoord.Clear();
        world_event_by_coord.Clear();
        foreach (GDictionary sd in Dictionaries(GetArray(active_world_data, "settlements")))
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
        foreach (GDictionary nd in Dictionaries(GetArray(active_world_data, "world_npcs")))
        {
            world_npc_by_coord[
                nd.ContainsKey("coord") ? nd["coord"].AsVector2I() : Vector2I.Zero
            ] = nd;
        }
        foreach (EncounterAnchorData ea in GetActiveEncounterAnchors())
        {
            _encounterAnchorByCoord[ea.world_coord] = ea;
        }
        foreach (GDictionary wed in Dictionaries(GetArray(active_world_data, "world_events")))
        {
            WorldMapEventData worldEvent = WorldMapEventData.FromDictionary(wed);
            if (worldEvent == null || !worldEvent.IsDiscovered)
                continue;
            world_event_by_coord[worldEvent.WorldCoord] = wed;
        }
    }

    private Godot.Collections.Dictionary _resolve_active_world_data()
    {
        if (active_map_id.Length == 0)
            return root_world_data;
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(
            get_mounted_submap_entry(active_map_id)
        );
        GDictionary swd = submap.WorldData;
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
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(
            get_mounted_submap_entry(active_map_id)
        );
        return submap.DisplayNameOrFallback(active_map_id);
    }

    private Vector2I _resolve_active_map_player_coord(Vector2I fallback)
    {
        if (active_map_id.Length == 0)
            return root_world_data.ContainsKey("player_start_coord")
                ? root_world_data["player_start_coord"].AsVector2I()
                : fallback;
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(
            get_mounted_submap_entry(active_map_id)
        );
        if (submap.HasPlayerCoord)
            return submap.PlayerCoord;
        return active_world_data.ContainsKey("player_start_coord")
            ? active_world_data["player_start_coord"].AsVector2I()
            : Vector2I.Zero;
    }

    private void _refresh_world_event_discovery()
    {
        var arr = GetArray(active_world_data, "world_events");
        if (arr.Count == 0)
            return;
        bool changed = false;
        for (int i = 0; i < arr.Count; i++)
        {
            if (!TryAsDictionary(arr[i], out var we))
                continue;
            WorldMapEventData worldEvent = WorldMapEventData.FromDictionary(we);
            if (worldEvent == null || worldEvent.IsDiscovered)
                continue;
            if (!_is_world_event_discovery_condition_met(worldEvent))
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

    private static bool _is_world_event_discovery_condition_met(WorldMapEventData worldEvent)
    {
        string cid = worldEvent?.DiscoveryConditionId.ToString().StripEdges() ?? "";
        return cid.Length == 0 || cid == "always_true";
    }

    private static GDictionary AsDictionary(object rawValue)
    {
        return TryAsDictionary(rawValue, out var value) ? value : new GDictionary();
    }

    private static GArray GetArray(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
        {
            return new GArray();
        }
        Variant value = source[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static GDictionary GetDictionary(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
        {
            return new GDictionary();
        }
        Variant value = source[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static string GetString(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
        {
            return "";
        }
        Variant value = source[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
    }

    private static Vector2I GetVector2I(GDictionary source, string key, Vector2I fallback)
    {
        if (source == null || !source.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = source[key];
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
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

public sealed class WorldMapContextSyncResult
{
    public readonly Vector2I PlayerCoord;
    public readonly Vector2I SelectedCoord;

    public WorldMapContextSyncResult(Vector2I playerCoord, Vector2I selectedCoord)
    {
        PlayerCoord = playerCoord;
        SelectedCoord = selectedCoord;
    }
}

public sealed class WorldMapSubmapEnterResult
{
    public readonly bool Ok;
    public readonly string Message;
    public readonly Vector2I PlayerCoord;
    public readonly string TargetDisplayName;

    private WorldMapSubmapEnterResult(
        bool ok,
        string message,
        Vector2I playerCoord,
        string targetDisplayName
    )
    {
        Ok = ok;
        Message = message ?? "";
        PlayerCoord = playerCoord;
        TargetDisplayName = targetDisplayName ?? "";
    }

    public static WorldMapSubmapEnterResult Success(
        Vector2I playerCoord,
        string targetDisplayName
    ) => new(true, "", playerCoord, targetDisplayName);

    public static WorldMapSubmapEnterResult Fail(string message) =>
        new(false, message, Vector2I.Zero, "");
}

public sealed class WorldMapSubmapReturnResult
{
    public readonly bool Ok;
    public readonly string Message;
    public readonly string TargetMapId;
    public readonly Vector2I PlayerCoord;

    private WorldMapSubmapReturnResult(
        bool ok,
        string message,
        string targetMapId,
        Vector2I playerCoord
    )
    {
        Ok = ok;
        Message = message ?? "";
        TargetMapId = targetMapId ?? "";
        PlayerCoord = playerCoord;
    }

    public static WorldMapSubmapReturnResult Success(
        string targetMapId,
        Vector2I playerCoord
    ) => new(true, "", targetMapId, playerCoord);

    public static WorldMapSubmapReturnResult Fail(string message) =>
        new(false, message, "", Vector2I.Zero);
}

public sealed class WorldMapMountedSubmapData
{
    private static readonly Vector2I UnsetPlayerCoord = new(-1, -1);

    public readonly bool Exists;
    public readonly string DisplayName;
    public readonly string GenerationConfigPath;
    public readonly string ReturnHintText;
    public readonly bool IsGenerated;
    public readonly Vector2I PlayerCoord;
    public readonly GDictionary WorldData;

    private WorldMapMountedSubmapData(
        bool exists,
        string displayName,
        string generationConfigPath,
        string returnHintText,
        bool isGenerated,
        Vector2I playerCoord,
        GDictionary worldData
    )
    {
        Exists = exists;
        DisplayName = displayName ?? "";
        GenerationConfigPath = generationConfigPath ?? "";
        ReturnHintText = returnHintText ?? "";
        IsGenerated = isGenerated;
        PlayerCoord = playerCoord;
        WorldData = worldData ?? new GDictionary();
    }

    public bool HasPlayerCoord => PlayerCoord != UnsetPlayerCoord;

    public string DisplayNameOrFallback(string fallback) =>
        DisplayName.Length > 0 ? DisplayName : fallback;

    public static WorldMapMountedSubmapData FromDictionary(GDictionary data)
    {
        if (data == null || data.Count == 0)
        {
            return new WorldMapMountedSubmapData(
                false,
                "",
                "",
                "",
                false,
                UnsetPlayerCoord,
                new GDictionary()
            );
        }
        return new WorldMapMountedSubmapData(
            true,
            ReadString(data, "display_name"),
            ReadString(data, "generation_config_path"),
            ReadString(data, "return_hint_text"),
            ReadBool(data, "is_generated"),
            ReadVector2I(data, "player_coord", UnsetPlayerCoord),
            ReadDictionary(data, "world_data")
        );
    }

    private static string ReadString(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return "";
        }
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
    }

    private static bool ReadBool(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return false;
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Bool && value.AsBool();
    }

    private static Vector2I ReadVector2I(GDictionary data, string key, Vector2I fallback)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    private static GDictionary ReadDictionary(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return new GDictionary();
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }
}

public sealed class WorldMapSettlementData
{
    public readonly bool Exists;
    public readonly string SettlementId;
    public readonly string DisplayName;

    private WorldMapSettlementData(
        bool exists,
        string settlementId,
        string displayName
    )
    {
        Exists = exists;
        SettlementId = settlementId ?? "";
        DisplayName = displayName ?? "";
    }

    public bool IsEmpty => !Exists;

    public string DisplayNameOrFallback(string fallback) =>
        string.IsNullOrEmpty(DisplayName) ? fallback : DisplayName;

    public static WorldMapSettlementData FromDictionary(GDictionary data)
    {
        if (data == null || data.Count == 0)
        {
            return new WorldMapSettlementData(false, "", "");
        }
        return new WorldMapSettlementData(
            true,
            ReadString(data, "settlement_id"),
            ReadString(data, "display_name")
        );
    }

    private static string ReadString(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return "";
        }
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
    }
}

public sealed class WorldMapSettlementStateData
{
    public readonly bool Visited;
    public readonly int Reputation;
    public readonly IReadOnlyList<string> ActiveConditions;

    private WorldMapSettlementStateData(
        bool visited,
        int reputation,
        IReadOnlyList<string> activeConditions
    )
    {
        Visited = visited;
        Reputation = reputation;
        ActiveConditions = activeConditions ?? Array.Empty<string>();
    }

    public static WorldMapSettlementStateData FromDictionary(GDictionary data)
    {
        if (data == null || data.Count == 0)
        {
            return new WorldMapSettlementStateData(false, 0, Array.Empty<string>());
        }

        var conditions = new List<string>();
        foreach (object condition in ReadArray(data, "active_conditions"))
        {
            conditions.Add(condition.ToString());
        }

        return new WorldMapSettlementStateData(
            ReadBool(data, "visited"),
            ReadInt(data, "reputation"),
            conditions
        );
    }

    private static bool ReadBool(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return false;
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Bool && value.AsBool();
    }

    private static int ReadInt(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return 0;
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
    }

    private static GArray ReadArray(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return new GArray();
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }
}

public sealed class WorldMapNpcData
{
    public readonly bool Exists;
    public readonly string DisplayName;
    public readonly string FactionId;
    private readonly GDictionary _sourceData;

    private WorldMapNpcData(
        bool exists,
        string displayName,
        string factionId,
        GDictionary sourceData
    )
    {
        Exists = exists;
        DisplayName = displayName ?? "";
        FactionId = factionId ?? "";
        _sourceData = sourceData != null ? sourceData.Duplicate(true) : new GDictionary();
    }

    public bool IsEmpty => !Exists;

    public bool HasValidCharacterInfoFields =>
        Exists
        && DisplayName.Length > 0
        && FactionId.Length > 0;

    public GDictionary ToDictionary() => _sourceData.Duplicate(true);

    public static WorldMapNpcData FromDictionary(GDictionary data)
    {
        if (data == null || data.Count == 0)
        {
            return new WorldMapNpcData(false, "", "", new GDictionary());
        }
        return new WorldMapNpcData(
            true,
            ReadTrimmedString(data, "display_name"),
            ReadTrimmedString(data, "faction_id"),
            data
        );
    }

    private static string ReadTrimmedString(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return "";
        }
        Variant value = data[key];
        string text = value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
        return text.Trim();
    }
}

public sealed class WorldMapEventData
{
    public readonly StringName EventId;
    public readonly string DisplayName;
    public readonly Vector2I WorldCoord;
    public readonly bool IsDiscovered;
    public readonly StringName EventType;
    public readonly StringName TargetSubmapId;
    public readonly StringName DiscoveryConditionId;
    public readonly string PromptTitle;
    public readonly string PromptText;

    private WorldMapEventData(
        StringName eventId,
        string displayName,
        Vector2I worldCoord,
        bool isDiscovered,
        StringName eventType,
        StringName targetSubmapId,
        StringName discoveryConditionId,
        string promptTitle,
        string promptText
    )
    {
        EventId = eventId;
        DisplayName = displayName;
        WorldCoord = worldCoord;
        IsDiscovered = isDiscovered;
        EventType = eventType;
        TargetSubmapId = targetSubmapId;
        DiscoveryConditionId = discoveryConditionId;
        PromptTitle = promptTitle ?? "";
        PromptText = promptText ?? "";
    }

    public bool IsTriggerableSubmapEntry =>
        IsDiscovered
        && EventType == WorldEventConfig.EVENT_TYPE_ENTER_SUBMAP
        && TargetSubmapId != "";

    public static WorldMapEventData FromDictionary(GDictionary data)
    {
        if (data == null || data.Count == 0)
        {
            return null;
        }
        return new WorldMapEventData(
            ReadStringName(data, "event_id"),
            ReadString(data, "display_name"),
            ReadVector2I(data, "world_coord"),
            ReadBool(data, "is_discovered"),
            ReadStringName(data, "event_type"),
            ReadStringName(data, "target_submap_id"),
            ReadStringName(data, "discovery_condition_id"),
            ReadString(data, "prompt_title"),
            ReadString(data, "prompt_text")
        );
    }

    private static StringName ReadStringName(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return "";
        }
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName(""),
        };
    }

    private static string ReadString(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return "";
        }
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
    }

    private static Vector2I ReadVector2I(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return Vector2I.Zero;
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : Vector2I.Zero;
    }

    private static bool ReadBool(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
        {
            return false;
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Bool && value.AsBool();
    }
}
