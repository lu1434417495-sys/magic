using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using System;
using System.Collections.Generic;

public sealed class WorldMapDataContext
{
    private WorldRuntimeData _rootRuntimeData = WorldRuntimeData.Empty();
    private WorldRuntimeData _activeRuntimeData = WorldRuntimeData.Empty();
    private bool _activeWorldUsesRoot = true;

    public Godot.Collections.Dictionary root_world_data => RootWorldDataPayload();

    public Godot.Collections.Dictionary active_world_data
    {
        get => ActiveWorldDataPayload();
        internal set
        {
            UseSeparateActiveWorldData();
            ReplaceActiveWorldDataPayload(
                RuntimePayloadCopy.Dictionary(
                    value,
                    "WorldMapDataContext.active_world_data"
                )
            );
        }
    }
    public string active_map_id = "";
    public string active_map_display_name = "";
    public WorldMapGenerationConfig active_generation_config;
    private readonly Dictionary<Vector2I, WorldMapEventData> _worldEventByCoord = new();
    private readonly Dictionary<string, WorldMapGenerationConfig> _submapGenerationConfigs =
        new(StringComparer.Ordinal);
    private readonly Dictionary<Vector2I, WorldMapSettlementRecordData> _settlementByCoord =
        new();
    private readonly Dictionary<Vector2I, WorldMapNpcData> _worldNpcByCoord = new();
    private readonly Dictionary<string, WorldMapSettlementRecordData> _settlementsById =
        new(StringComparer.Ordinal);
    private readonly Dictionary<Vector2I, EncounterAnchorData> _encounterAnchorByCoord = new();

    internal WorldRuntimeData RootRuntimeData => _rootRuntimeData;

    internal WorldRuntimeData ActiveRuntimeData => _activeRuntimeData;

    public void BindRootWorldData(Godot.Collections.Dictionary worldData)
    {
        _rootRuntimeData = WorldRuntimeData.FromDictionary(worldData) ?? WorldRuntimeData.Empty();
        _activeRuntimeData = _rootRuntimeData;
        UseRootWorldDataAsActive();
    }

    public void Reset()
    {
        _rootRuntimeData = WorldRuntimeData.Empty();
        _activeRuntimeData = WorldRuntimeData.Empty();
        ClearRootWorldDataPayload();
        ClearActiveWorldDataPayload();
        _activeWorldUsesRoot = true;
        active_map_id = "";
        active_map_display_name = "";
        active_generation_config = null;
        _worldEventByCoord.Clear();
        _submapGenerationConfigs.Clear();
        _settlementByCoord.Clear();
        _worldNpcByCoord.Clear();
        _settlementsById.Clear();
        _encounterAnchorByCoord.Clear();
    }

    public void Dispose() => Reset();

    public bool IsSubmapActive() => active_map_id.Length > 0;

    public int GetWorldStep() => _activeRuntimeData?.WorldStep ?? 0;

    internal void SetWorldStep(int worldStep)
    {
        // _activeRuntimeData is the source of truth and payloads project from it on
        // demand, so a typed write suffices — no whole-world round-trip needed.
        _activeRuntimeData.SetWorldStep(worldStep);
    }

    internal string GetPlayerStartSettlementName() =>
        GetString(ActiveWorldDataPayload(), "player_start_settlement_name");

    public Godot.Collections.Dictionary GetActiveWorldData() => ActiveWorldDataPayload();

    internal WorldMapGenerationConfig GetActiveGenerationConfig() => active_generation_config;

    internal GDictionary GetActiveWorldFogState()
    {
        GDictionary activeWorldData = ActiveWorldDataPayload();
        if (activeWorldData.Count == 0)
            return new GDictionary();
        return GetDictionary(activeWorldData, WorldMapFogSystem.WorldDataFogStatesKey);
    }

    public bool SaveActiveWorldFogState(WorldMapFogSystem fogSystem)
    {
        if (active_generation_config == null || fogSystem == null || _activeRuntimeData == null)
            return false;
        GDictionary fogStates = fogSystem.ExportPersistentState();
        // Write fog directly into the typed active world data — no whole-world
        // ToDictionary/FromDictionary round-trip. On the root map _activeRuntimeData
        // and _rootRuntimeData are the same instance, so this updates root too.
        _activeRuntimeData.SetFogStates(fogStates);
        if (IsSubmapActive())
        {
            // The mounted-submap entry keeps a dict snapshot; sync just its fog key
            // (submaps are entered rarely, so this targeted update is cheap).
            var submapEntry = GetMountedSubmapEntry(active_map_id);
            if (
                submapEntry.Count > 0
                && submapEntry.ContainsKey("world_data")
                && submapEntry["world_data"].VariantType == Variant.Type.Dictionary
            )
            {
                GDictionary submapWorldData = submapEntry["world_data"].AsGodotDictionary();
                submapWorldData[WorldMapFogSystem.WorldDataFogStatesKey] = fogStates;
                submapEntry["world_data"] = submapWorldData;
                SetMountedSubmapEntry(active_map_id, submapEntry);
            }
        }
        return true;
    }

    internal Vector2I GetActiveWorldSizeCells() =>
        active_generation_config?.GetWorldSizeCells() ?? Vector2I.Zero;

    public string GetActiveMapId() => active_map_id;

    public string GetActiveMapDisplayName() => active_map_display_name;

    public string GetSubmapReturnHintText()
    {
        if (!IsSubmapActive())
            return "";
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(
            GetMountedSubmapEntry(active_map_id)
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
        GDictionary rootWorldData = RootWorldDataPayload();
        active_map_id = rootWorldData.ContainsKey("active_submap_id")
            ? rootWorldData["active_submap_id"].AsString()
            : "";
        if (active_map_id.Length > 0 && GetMountedSubmapEntry(active_map_id).Count == 0)
        {
            active_map_id = "";
            rootWorldData["active_submap_id"] = "";
            ReplaceRootWorldDataPayload(rootWorldData);
        }
        if (active_map_id.Length == 0)
            UseRootWorldDataAsActive();
        else
            UseSeparateActiveWorldData();

        GDictionary resolvedActiveWorldData = _resolve_active_world_data();
        ReplaceActiveWorldDataPayload(resolvedActiveWorldData);
        _activeRuntimeData =
            WorldRuntimeData.FromDictionary(resolvedActiveWorldData) ?? WorldRuntimeData.Empty();
        if (active_map_id.Length == 0)
        {
            _rootRuntimeData = _activeRuntimeData;
            ReplaceRootWorldDataPayload(WorldMapDataProjection.Project(_activeRuntimeData));
            UseRootWorldDataAsActive();
        }
        else
        {
            ReplaceActiveWorldDataPayload(WorldMapDataProjection.Project(_activeRuntimeData));
        }
        active_generation_config = _resolve_active_generation_config(rootGenConfig);
        active_map_display_name = _resolve_active_map_display_name();
        if (active_generation_config != null && gridSystem != null)
            gridSystem.Setup(
                active_generation_config.world_size_in_chunks,
                active_generation_config.chunk_size
            );
        _refresh_world_event_discovery();
        _rebuild_world_coord_lookups();
        _register_settlement_footprints(gridSystem);
        var rpc = playerCoord;
        var rsc = selectedCoord;
        if (gridSystem != null && !gridSystem.IsCellInsideWorld(rpc))
            rpc = _resolve_active_map_player_coord(playerCoord);
        if (gridSystem != null && !gridSystem.IsCellInsideWorld(rsc))
            rsc = rpc;
        return new WorldMapContextSyncResult(rpc, rsc);
    }

    internal bool ValidateWorldSystemSizeConsistency(
        WorldMapGridSystem gridSystem,
        WorldMapFogSystem fogSystem
    )
    {
        var ews = GetActiveWorldSizeCells();
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
        var gws = gridSystem.GetWorldSizeCells();
        var fws = fogSystem.GetWorldSizeCells();
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

    internal WorldMapSettlementData GetSettlementAt(Vector2I coord) =>
        _settlementByCoord.TryGetValue(coord, out WorldMapSettlementRecordData settlement)
            ? settlement.ToSettlementData()
            : WorldMapSettlementData.FromDictionary(new GDictionary());

    internal WorldMapNpcData GetWorldNpcAt(Vector2I coord) =>
        _worldNpcByCoord.TryGetValue(coord, out WorldMapNpcData worldNpc)
            ? worldNpc
            : WorldMapNpcData.FromDictionary(new GDictionary());

    internal EncounterAnchorData GetEncounterAnchorAt(Vector2I coord) =>
        _encounterAnchorByCoord.TryGetValue(coord, out EncounterAnchorData encounterAnchor)
            ? encounterAnchor
            : null;

    internal List<EncounterAnchorData> GetActiveEncounterAnchors(bool includeCleared = true)
    {
        var anchors = new List<EncounterAnchorData>();
        foreach (EncounterAnchorData encounterAnchor in _activeRuntimeData.EncounterAnchors)
        {
            if (!includeCleared && encounterAnchor.is_cleared)
                continue;
            anchors.Add(encounterAnchor);
        }
        return anchors;
    }

    internal EncounterAnchorData GetEncounterAnchorById(StringName entityId)
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

    internal WorldMapEventData GetWorldEventAt(Vector2I coord) =>
        _worldEventByCoord.TryGetValue(coord, out WorldMapEventData worldEvent)
            ? worldEvent
            : null;

    internal List<WorldMapEventData> GetDiscoveredWorldEvents()
    {
        var events = new List<WorldMapEventData>();
        foreach (WorldMapEventData worldEvent in _worldEventByCoord.Values)
            events.Add(worldEvent);
        return events;
    }

    internal Godot.Collections.Dictionary GetSettlementRecord(string settlementId) =>
        _settlementsById.TryGetValue(settlementId ?? "", out WorldMapSettlementRecordData settlement)
            ? WorldMapDataProjection.Project(settlement)
            : new Godot.Collections.Dictionary();

    internal Godot.Collections.Array<Godot.Collections.Dictionary> GetAllSettlementRecords() =>
        WorldMapDataProjection.ProjectSettlementRecords(_settlementsById.Values);

    internal Godot.Collections.Dictionary GetSettlementState(string settlementId)
    {
        return _settlementsById.TryGetValue(settlementId ?? "", out WorldMapSettlementRecordData settlement)
            ? settlement.GetSettlementStateDictionary()
            : new Godot.Collections.Dictionary();
    }

    internal WorldMapSettlementStateData GetSettlementStateData(string settlementId) =>
        WorldMapSettlementStateData.FromDictionary(GetSettlementState(settlementId));

    internal bool IsSettlementVisited(string settlementId) =>
        GetSettlementStateData(settlementId).Visited;

    public bool MarkSettlementVisited(string settlementId)
    {
        if (_activeRuntimeData.GetSettlementStateData(settlementId).Visited)
        {
            return false;
        }
        if (!_activeRuntimeData.MarkSettlementVisited(settlementId))
        {
            return false;
        }
        _sync_active_world_payload_from_typed();
        _rebuild_world_coord_lookups();
        return true;
    }

    public bool SetActiveSettlementState(
        string settlementId,
        Godot.Collections.Dictionary settlementState
    )
    {
        if (
            !_activeRuntimeData.TrySetSettlementState(
                settlementId,
                WorldMapSettlementStateData.FromDictionary(settlementState)
            )
        )
        {
            return false;
        }
        _sync_active_world_payload_from_typed();
        _rebuild_world_coord_lookups();
        return true;
    }

    public void RemoveEncounterAnchorById(StringName encounterId)
    {
        if (!_activeRuntimeData.RemoveEncounterAnchorById(encounterId))
            return;
        _sync_active_world_payload_from_typed();
        _rebuild_world_coord_lookups();
    }

    internal void SyncActiveWorldPayloadFromTypedState() =>
        SyncActiveWorldPayloadFromTypedState(rebuildLookups: true);

    internal void SyncActiveWorldPayloadFromTypedState(bool rebuildLookups)
    {
        _sync_active_world_payload_from_typed();
        // Coord lookups only need rebuilding when entity positions/existence change
        // (settlement state, anchor add/remove). Encounter growth only mutates an
        // anchor's growth_stage in place, so the caller can skip the O(all markers)
        // rebuild.
        if (rebuildLookups)
            _rebuild_world_coord_lookups();
    }

    internal void RefreshWorldEventDiscovery() => _refresh_world_event_discovery();

    internal Godot.Collections.Dictionary GetMountedSubmapEntry(string submapId)
    {
        return GetDictionary(GetDictionary(RootWorldDataPayload(), "mounted_submaps"), submapId);
    }

    internal void SetMountedSubmapEntry(string submapId, Godot.Collections.Dictionary submapEntry)
    {
        GDictionary rootWorldData = RootWorldDataPayload();
        var mountedSubmaps = GetDictionary(rootWorldData, "mounted_submaps");
        mountedSubmaps[submapId] = RuntimePayloadCopy.Dictionary(
            submapEntry,
            "WorldMapDataContext.SetMountedSubmapEntry"
        );
        rootWorldData["mounted_submaps"] = mountedSubmaps;
        ReplaceRootWorldDataPayload(rootWorldData);
        _rootRuntimeData = WorldRuntimeData.FromDictionary(rootWorldData) ?? WorldRuntimeData.Empty();
    }

    internal string GetMountedSubmapDisplayName(string submapId, string fallback = "")
    {
        var submapEntry = GetMountedSubmapEntry(submapId);
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
        if (!EnsureSubmapGenerated(submapId))
        {
            return WorldMapSubmapEnterResult.Fail("子地图生成失败。");
        }
        var submapEntry = GetMountedSubmapEntry(submapId);
        if (submapEntry.Count == 0)
        {
            return WorldMapSubmapEnterResult.Fail("未找到目标子地图。");
        }

        GDictionary rootWorldData = RootWorldDataPayload();
        var returnStack = GetArray(rootWorldData, "submap_return_stack");
        returnStack.Add(
            WorldMapDataProjection.Project(new WorldMapSubmapReturnStackEntry(sourceMapId, sourceCoord))
        );
        rootWorldData["submap_return_stack"] = returnStack;
        rootWorldData["active_submap_id"] = submapId;
        ReplaceRootWorldDataPayload(rootWorldData);
        _rootRuntimeData = WorldRuntimeData.FromDictionary(rootWorldData) ?? WorldRuntimeData.Empty();

        WorldMapMountedSubmapData targetSubmap = WorldMapMountedSubmapData.FromDictionary(
            submapEntry
        );
        GDictionary targetWorldData = targetSubmap.ProjectWorldDataPayload();
        Vector2I targetCoord = targetSubmap.HasPlayerCoord
            ? targetSubmap.PlayerCoord
            : GetVector2I(targetWorldData, "player_start_coord", Vector2I.Zero);
        string targetDisplayName = targetSubmap.DisplayNameOrFallback(submapId);
        return WorldMapSubmapEnterResult.Success(targetCoord, targetDisplayName);
    }

    public WorldMapSubmapReturnResult ReturnFromActiveSubmap(Vector2I currentPlayerCoord)
    {
        if (!IsSubmapActive())
        {
            return WorldMapSubmapReturnResult.Fail("当前不在子地图中。");
        }

        var submapEntry = GetMountedSubmapEntry(active_map_id);
        if (submapEntry.Count > 0)
        {
            submapEntry["player_coord"] = currentPlayerCoord;
            SetMountedSubmapEntry(active_map_id, submapEntry);
        }

        GDictionary rootWorldData = RootWorldDataPayload();
        var returnStack = GetArray(rootWorldData, "submap_return_stack");
        if (returnStack.Count == 0)
        {
            return WorldMapSubmapReturnResult.Fail("当前没有可返回的原坐标。");
        }
        GDictionary returnEntry = returnStack[returnStack.Count - 1].AsGodotDictionary();
        returnStack.RemoveAt(returnStack.Count - 1);
        WorldMapSubmapReturnStackEntry typedReturnEntry =
            WorldMapSubmapReturnStackEntry.FromDictionary(returnEntry);
        rootWorldData["submap_return_stack"] = returnStack;
        rootWorldData["active_submap_id"] = typedReturnEntry.MapId;
        ReplaceRootWorldDataPayload(rootWorldData);
        _rootRuntimeData = WorldRuntimeData.FromDictionary(rootWorldData) ?? WorldRuntimeData.Empty();
        return WorldMapSubmapReturnResult.Success(
            typedReturnEntry.MapId,
            typedReturnEntry.Coord
        );
    }

    internal bool EnsureSubmapGenerated(string submapId)
    {
        var submapEntry = GetMountedSubmapEntry(submapId);
        if (submapEntry.Count == 0)
            return false;
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(submapEntry);
        if (submap.IsGenerated && submap.ProjectWorldDataPayload().Count > 0)
            return true;
        var sgc = LoadSubmapGenerationConfig(submapId);
        if (sgc == null)
            return false;
        var gg = new WorldMapGridSystem();
        gg.Setup(sgc.world_size_in_chunks, sgc.chunk_size);
        var ss = new WorldMapSpawnSystem();
        WorldMapSpawnSystem.WorldBuildData swd = ss.BuildWorldTyped(sgc, gg);
        submapEntry["world_data"] = WorldMapSpawnProjection.Project(swd);
        submapEntry["player_coord"] = swd.PlayerStartCoord;
        submapEntry["is_generated"] = true;
        SetMountedSubmapEntry(submapId, submapEntry);
        return true;
    }

    internal WorldMapGenerationConfig LoadSubmapGenerationConfig(string submapId)
    {
        if (
            !string.IsNullOrEmpty(submapId)
            && _submapGenerationConfigs.TryGetValue(submapId, out WorldMapGenerationConfig cached)
        )
            return cached;
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(
            GetMountedSubmapEntry(submapId)
        );
        string gcp = submap.GenerationConfigPath;
        if (gcp.Length == 0)
            return null;
        var gc = GD.Load<Resource>(gcp);
        if (gc is WorldMapGenerationConfig config)
        {
            GodotContentOwnership.RegisterBorrowedContent(config, gcp);
            _submapGenerationConfigs[submapId] = config;
            return config;
        }
        return null;
    }

    private void _register_settlement_footprints(WorldMapGridSystem gridSystem)
    {
        if (gridSystem == null)
            return;
        foreach (WorldMapSettlementRecordData settlement in _settlementsById.Values)
        {
            string eid = settlement?.EntityId ?? "";
            Vector2I origin = settlement?.Origin ?? Vector2I.Zero;
            Vector2I size = settlement?.FootprintSize ?? Vector2I.One;
            if (eid.Length == 0)
                continue;
            if (gridSystem.CanPlaceFootprint(origin, size))
                gridSystem.RegisterFootprint(eid, origin, size);
        }
    }

    private void _rebuild_world_coord_lookups()
    {
        _settlementByCoord.Clear();
        _settlementsById.Clear();
        _worldNpcByCoord.Clear();
        _encounterAnchorByCoord.Clear();
        _worldEventByCoord.Clear();
        // Iterate the typed source of truth directly — no ActiveWorldDataPayload()
        // ToDictionary and no per-item FromDictionary. Lookups are read-only, so
        // sharing the typed record references is safe.
        foreach (WorldMapSettlementRecordData settlement in _activeRuntimeData.Settlements)
        {
            if (settlement == null || settlement.SettlementId.Length == 0)
                continue;
            _settlementsById[settlement.SettlementId] = settlement;
            Vector2I origin = settlement.Origin;
            Vector2I size = settlement.FootprintSize;
            for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++)
                _settlementByCoord[origin + new Vector2I(x, y)] = settlement;
        }
        foreach (WorldMapNpcData worldNpc in _activeRuntimeData.WorldNpcs)
        {
            if (worldNpc == null || !worldNpc.Exists)
                continue;
            _worldNpcByCoord[worldNpc.Coord] = worldNpc;
        }
        foreach (EncounterAnchorData ea in _activeRuntimeData.EncounterAnchors)
        {
            if (ea == null)
                continue;
            _encounterAnchorByCoord[ea.world_coord] = ea;
        }
        foreach (WorldMapEventData worldEvent in _activeRuntimeData.WorldEvents)
        {
            if (worldEvent == null || !worldEvent.IsDiscovered)
                continue;
            _worldEventByCoord[worldEvent.WorldCoord] = worldEvent;
        }
    }

    private Godot.Collections.Dictionary _resolve_active_world_data()
    {
        if (active_map_id.Length == 0)
            return RootWorldDataPayload();
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(
            GetMountedSubmapEntry(active_map_id)
        );
        GDictionary swd = submap.ProjectWorldDataPayload();
        return swd.Count > 0 ? swd : RootWorldDataPayload();
    }

    private WorldMapGenerationConfig _resolve_active_generation_config(
        WorldMapGenerationConfig rootGenConfig
    ) =>
        active_map_id.Length == 0 ? rootGenConfig : LoadSubmapGenerationConfig(active_map_id);

    private string _resolve_active_map_display_name()
    {
        if (active_map_id.Length == 0)
            return "大地图";
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(
            GetMountedSubmapEntry(active_map_id)
        );
        return submap.DisplayNameOrFallback(active_map_id);
    }

    private Vector2I _resolve_active_map_player_coord(Vector2I fallback)
    {
        if (active_map_id.Length == 0)
        {
            GDictionary rootWorldData = RootWorldDataPayload();
            return rootWorldData.ContainsKey("player_start_coord")
                ? rootWorldData["player_start_coord"].AsVector2I()
                : fallback;
        }
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(
            GetMountedSubmapEntry(active_map_id)
        );
        if (submap.HasPlayerCoord)
            return submap.PlayerCoord;
        GDictionary activeWorldData = ActiveWorldDataPayload();
        return activeWorldData.ContainsKey("player_start_coord")
            ? activeWorldData["player_start_coord"].AsVector2I()
            : Vector2I.Zero;
    }

    private void _refresh_world_event_discovery()
    {
        // Scan the typed events directly — no whole-world ToDictionary every move.
        // Collect ids to mark first, then mutate, to avoid modifying while iterating.
        List<StringName> toDiscover = null;
        foreach (WorldMapEventData worldEvent in _activeRuntimeData.WorldEvents)
        {
            if (worldEvent == null || worldEvent.IsDiscovered)
                continue;
            if (!_is_world_event_discovery_condition_met(worldEvent))
                continue;
            (toDiscover ??= new List<StringName>()).Add(worldEvent.EventId);
        }
        if (toDiscover == null)
            return;
        bool changed = false;
        foreach (StringName eventId in toDiscover)
        {
            if (_activeRuntimeData.MarkWorldEventDiscovered(eventId))
                changed = true;
        }
        if (changed)
        {
            _sync_active_world_payload_from_typed();
            _rebuild_world_coord_lookups();
        }
    }

    private void _sync_active_world_payload_from_typed()
    {
        if (active_map_id.Length == 0)
        {
            // Root map: _activeRuntimeData is already the mutated source of truth and
            // is the same instance as _rootRuntimeData, and payloads project from it
            // on demand — so there is nothing to round-trip.
            _rootRuntimeData = _activeRuntimeData ?? WorldRuntimeData.Empty();
            return;
        }
        // Submap (entered rarely): keep the mounted-entry dict snapshot in sync.
        GDictionary submapEntry = GetMountedSubmapEntry(active_map_id);
        if (submapEntry.Count > 0)
        {
            submapEntry["world_data"] = WorldMapDataProjection.Project(_activeRuntimeData);
            SetMountedSubmapEntry(active_map_id, submapEntry);
        }
    }

    private GDictionary RootWorldDataPayload() => WorldMapDataProjection.Project(_rootRuntimeData);

    private void ReplaceRootWorldDataPayload(GDictionary payload)
    {
        _rootRuntimeData = WorldRuntimeData.FromDictionary(payload) ?? WorldRuntimeData.Empty();
        if (_activeWorldUsesRoot)
            _activeRuntimeData = _rootRuntimeData;
    }

    private void ClearRootWorldDataPayload()
    {
        _rootRuntimeData = WorldRuntimeData.Empty();
        if (_activeWorldUsesRoot)
            _activeRuntimeData = _rootRuntimeData;
    }

    private GDictionary ActiveWorldDataPayload() =>
        _activeWorldUsesRoot
            ? RootWorldDataPayload()
            : WorldMapDataProjection.Project(_activeRuntimeData);

    private void ReplaceActiveWorldDataPayload(GDictionary payload)
    {
        if (_activeWorldUsesRoot)
        {
            ReplaceRootWorldDataPayload(payload ?? new GDictionary());
            return;
        }

        _activeRuntimeData = WorldRuntimeData.FromDictionary(payload) ?? WorldRuntimeData.Empty();
    }

    private void ClearActiveWorldDataPayload()
    {
        if (!_activeWorldUsesRoot)
            _activeRuntimeData = WorldRuntimeData.Empty();
    }

    private void UseRootWorldDataAsActive()
    {
        _activeWorldUsesRoot = true;
        _activeRuntimeData = _rootRuntimeData;
    }

    private void UseSeparateActiveWorldData()
    {
        _activeWorldUsesRoot = false;
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

public sealed class WorldMapSubmapReturnStackEntry
{
    public readonly string MapId;
    public readonly Vector2I Coord;

    public WorldMapSubmapReturnStackEntry(string mapId, Vector2I coord)
    {
        MapId = mapId ?? "";
        Coord = coord;
    }

    public static WorldMapSubmapReturnStackEntry FromDictionary(GDictionary data) =>
        new(
            WorldMapDictionaryReaders.ReadString(data, "map_id"),
            WorldMapDictionaryReaders.ReadVector2I(data, "coord", Vector2I.Zero)
        );
}

internal static class WorldMapPlainPayload
{
    internal static void Replace(
        Dictionary<string, object> target,
        GDictionary source,
        string ownerPath
    )
    {
        target.Clear();
        Dictionary<string, object> normalized =
            RuntimePlainPayload.NormalizeDictionary(source ?? new GDictionary(), ownerPath);
        foreach (KeyValuePair<string, object> entry in normalized)
        {
            target[entry.Key] = entry.Value;
        }
    }

    internal static GDictionary Project(
        IReadOnlyDictionary<string, object> source,
        string ownerPath
    ) =>
        RuntimePlainPayload.ProjectDictionary(source, ownerPath);
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
    private readonly Dictionary<string, object> _worldData = new(StringComparer.Ordinal);

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
        WorldMapPlainPayload.Replace(
            _worldData,
            worldData ?? new GDictionary(),
            "WorldMapMountedSubmapData.worldData"
        );
    }

    public bool HasPlayerCoord => PlayerCoord != UnsetPlayerCoord;

    internal GDictionary ProjectWorldDataPayload() =>
        WorldMapPlainPayload.Project(_worldData, "WorldMapMountedSubmapData.worldData");

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

public sealed class WorldMapSettlementRecordData
{
    public readonly string EntityId;
    public readonly string SettlementId;
    public readonly string DisplayName;
    public readonly Vector2I Origin;
    public readonly Vector2I FootprintSize;
    public readonly int Tier;
    private readonly Dictionary<string, object> _sourceData = new(StringComparer.Ordinal);

    private WorldMapSettlementRecordData(
        string entityId,
        string settlementId,
        string displayName,
        Vector2I origin,
        Vector2I footprintSize,
        int tier,
        GDictionary sourceData
    )
    {
        EntityId = entityId ?? "";
        SettlementId = settlementId ?? "";
        DisplayName = displayName ?? "";
        Origin = origin;
        FootprintSize = footprintSize;
        Tier = tier;
        WorldMapPlainPayload.Replace(
            _sourceData,
            RuntimePayloadCopy.Dictionary(
                sourceData,
                "WorldMapSettlementRecordData.sourceData"
            ),
            "WorldMapSettlementRecordData.sourceData"
        );
    }

    internal GDictionary DuplicateSourcePayload() =>
        WorldMapPlainPayload.Project(_sourceData, "WorldMapSettlementRecordData.sourceData");

    internal GDictionary GetSettlementStateDictionary() =>
        WorldMapDictionaryReaders.ReadDictionary(
            DuplicateSourcePayload(),
            "settlement_state"
        );

    public WorldMapSettlementData ToSettlementData() =>
        WorldMapSettlementData.Create(SettlementId, DisplayName);

    public static WorldMapSettlementRecordData FromDictionary(GDictionary data)
    {
        if (data == null || data.Count == 0)
            return null;
        return new WorldMapSettlementRecordData(
            WorldMapDictionaryReaders.ReadString(data, "entity_id"),
            WorldMapDictionaryReaders.ReadString(data, "settlement_id"),
            WorldMapDictionaryReaders.ReadString(data, "display_name"),
            WorldMapDictionaryReaders.ReadVector2I(data, "origin", Vector2I.Zero),
            WorldMapDictionaryReaders.ReadVector2I(data, "footprint_size", Vector2I.One),
            WorldMapDictionaryReaders.ReadInt(data, "tier", 0),
            data
        );
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

    internal static WorldMapSettlementData Create(string settlementId, string displayName) =>
        new(true, settlementId, displayName);

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

    public static WorldMapSettlementStateData Create(
        bool visited,
        int reputation,
        IEnumerable<string> activeConditions
    )
    {
        var conditions = new List<string>();
        if (activeConditions != null)
        {
            foreach (string condition in activeConditions)
            {
                if (!string.IsNullOrEmpty(condition))
                    conditions.Add(condition);
            }
        }
        return new WorldMapSettlementStateData(visited, reputation, conditions);
    }

    internal GDictionary ToDictionary()
    {
        GArray conditions = new();
        foreach (string condition in ActiveConditions)
        {
            if (!string.IsNullOrEmpty(condition))
                conditions.Add(condition);
        }
        return new GDictionary
        {
            ["visited"] = Visited,
            ["reputation"] = Reputation,
            ["active_conditions"] = conditions,
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
    public readonly Vector2I Coord;
    public readonly string DisplayName;
    public readonly string FactionId;
    private readonly Dictionary<string, object> _sourceData = new(StringComparer.Ordinal);

    private WorldMapNpcData(
        bool exists,
        Vector2I coord,
        string displayName,
        string factionId,
        GDictionary sourceData
    )
    {
        Exists = exists;
        Coord = coord;
        DisplayName = displayName ?? "";
        FactionId = factionId ?? "";
        WorldMapPlainPayload.Replace(
            _sourceData,
            RuntimePayloadCopy.Dictionary(sourceData, "WorldMapNpcData.sourceData"),
            "WorldMapNpcData.sourceData"
        );
    }

    public bool IsEmpty => !Exists;

    public bool HasValidCharacterInfoFields =>
        Exists
        && DisplayName.Length > 0
        && FactionId.Length > 0;

    internal GDictionary DuplicateSourcePayload() =>
        WorldMapPlainPayload.Project(_sourceData, "WorldMapNpcData.sourceData");

    public static WorldMapNpcData FromDictionary(GDictionary data)
    {
        if (data == null || data.Count == 0)
        {
            return new WorldMapNpcData(false, Vector2I.Zero, "", "", new GDictionary());
        }
        return new WorldMapNpcData(
            true,
            WorldMapDictionaryReaders.ReadVector2I(data, "coord", Vector2I.Zero),
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
    private readonly Dictionary<string, object> _sourceData = new(StringComparer.Ordinal);

    private WorldMapEventData(
        StringName eventId,
        string displayName,
        Vector2I worldCoord,
        bool isDiscovered,
        StringName eventType,
        StringName targetSubmapId,
        StringName discoveryConditionId,
        string promptTitle,
        string promptText,
        GDictionary sourceData
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
        WorldMapPlainPayload.Replace(
            _sourceData,
            RuntimePayloadCopy.Dictionary(sourceData, "WorldMapEventData.sourceData"),
            "WorldMapEventData.sourceData"
        );
    }

    public bool IsTriggerableSubmapEntry =>
        IsDiscovered
        && WorldEventConfig.ToEventTypeKind(EventType) == WorldEventTypeKind.EnterSubmap
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
            ReadString(data, "prompt_text"),
            data
        );
    }

    internal GDictionary DuplicateSourcePayload() =>
        WorldMapPlainPayload.Project(_sourceData, "WorldMapEventData.sourceData");

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

internal static class WorldMapDictionaryReaders
{
    internal static string ReadString(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return "";
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
    }

    internal static Vector2I ReadVector2I(GDictionary data, string key, Vector2I fallback)
    {
        if (data == null || !data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    internal static int ReadInt(GDictionary data, string key, int fallback = 0)
    {
        if (data == null || !data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    internal static GDictionary ReadDictionary(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return new GDictionary();
        Variant value = data[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }
}
