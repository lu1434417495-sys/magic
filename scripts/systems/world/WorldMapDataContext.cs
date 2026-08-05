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
    private WorldMapFogSystem _materializedFogOwner;
    private string _materializedFogMapId = "";
    private long _materializedFogRevision = -1;

    internal void SetActiveWorldData(GDictionary value)
    {
        UseSeparateActiveWorldData();
        ReplaceActiveWorldDataPayload(value);
        InvalidateFogMaterialization();
    }
    public string active_map_id = "";
    public string active_map_display_name = "";
    public WorldGenerationDefinition active_generation_definition;
    private WorldGenerationDefinition _rootGenerationDefinition;
    private readonly Dictionary<Vector2I, WorldMapEventData> _worldEventByCoord = new();
    private readonly Dictionary<string, WorldGenerationDefinition> _submapGenerationDefinitions =
        new(StringComparer.Ordinal);
    private readonly Dictionary<Vector2I, WorldMapSettlementRecordData> _settlementByCoord =
        new();
    private readonly Dictionary<Vector2I, WorldMapNpcData> _worldNpcByCoord = new();
    private readonly Dictionary<string, WorldMapSettlementRecordData> _settlementsById =
        new(StringComparer.Ordinal);
    private readonly Dictionary<Vector2I, EncounterAnchorData> _encounterAnchorByCoord = new();
    private readonly Dictionary<Vector2I, WorldMapResourceNodeData> _resourceNodeByCoord = new();

    internal WorldRuntimeData RootRuntimeData => _rootRuntimeData;

    internal WorldRuntimeData ActiveRuntimeData => _activeRuntimeData;

    public void BindRootWorldData(Godot.Collections.Dictionary worldData)
    {
        _rootRuntimeData = WorldRuntimeData.FromDictionary(worldData) ?? WorldRuntimeData.Empty();
        _activeRuntimeData = _rootRuntimeData;
        UseRootWorldDataAsActive();
        InvalidateFogMaterialization();
    }

    internal void BindRootWorldData(WorldRuntimeData worldData)
    {
        _rootRuntimeData = worldData?.DuplicateState() ?? WorldRuntimeData.Empty();
        _activeRuntimeData = _rootRuntimeData;
        UseRootWorldDataAsActive();
        InvalidateFogMaterialization();
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
        active_generation_definition = null;
        _rootGenerationDefinition = null;
        _worldEventByCoord.Clear();
        _submapGenerationDefinitions.Clear();
        _settlementByCoord.Clear();
        _worldNpcByCoord.Clear();
        _settlementsById.Clear();
        _encounterAnchorByCoord.Clear();
        _resourceNodeByCoord.Clear();
        InvalidateFogMaterialization();
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
        _activeRuntimeData?.PlayerStartSettlementName ?? "";

    internal GodotProjectionLease<GDictionary> GetActiveWorldDataLease() =>
        ActiveWorldDataPayloadLease();

    public IReadOnlyDictionary<string, object> GetActiveWorldDataSnapshotPlain() =>
        _activeRuntimeData?.BuildSaveSnapshotPlain()
        ?? new Dictionary<string, object>(StringComparer.Ordinal);

    internal GodotProjectionLease<GDictionary> GetRootWorldDataLease() =>
        RootWorldDataPayloadLease();

    public IReadOnlyDictionary<string, object> GetRootWorldDataSnapshotPlain() =>
        _rootRuntimeData?.BuildSaveSnapshotPlain()
        ?? new Dictionary<string, object>(StringComparer.Ordinal);

    internal WorldGenerationDefinition GetActiveGenerationDefinition() =>
        active_generation_definition;

    internal GodotProjectionLease<GDictionary> GetActiveWorldFogStateLease()
    {
        IReadOnlyDictionary<string, object> worldData = GetActiveWorldDataSnapshotPlain();
        IReadOnlyDictionary<string, object> fogState =
            worldData.TryGetValue(WorldMapFogSystem.WorldDataFogStatesKey, out object rawFogState)
            && rawFogState is IReadOnlyDictionary<string, object> typedFogState
                ? typedFogState
                : new Dictionary<string, object>(StringComparer.Ordinal);
        return RuntimePlainPayload.ProjectDictionaryLease(
            fogState,
            "WorldMapDataContext.active_fog_state",
            LifetimeDomain.Request,
            "WorldMapDataContext.active_fog_state"
        );
    }

    public bool SaveActiveWorldFogState(WorldMapFogSystem fogSystem)
    {
        if (active_generation_definition == null || fogSystem == null || _activeRuntimeData == null)
            return false;
        if (!NeedsActiveWorldFogSave(fogSystem))
            return true;
        Dictionary<string, object> fogStates = fogSystem.BuildPersistentStatePlain();
        // Write fog directly into the typed active world data — no whole-world
        // ToDictionary/FromDictionary round-trip. On the root map _activeRuntimeData
        // and _rootRuntimeData are the same instance, so this updates root too.
        _activeRuntimeData.SetFogStates(fogStates);
        if (IsSubmapActive())
        {
            // The mounted-submap entry keeps a dict snapshot; sync just its fog key
            // (submaps are entered rarely, so this targeted update is cheap).
            using GodotProjectionLease<GDictionary> submapEntryLease =
                GetMountedSubmapEntryLease(active_map_id);
            GDictionary submapEntry = submapEntryLease.Value;
            if (
                submapEntry.Count > 0
                && submapEntry.ContainsKey("world_data")
                && submapEntry["world_data"].VariantType == Variant.Type.Dictionary
            )
            {
                using (GDictionary submapWorldData =
                    submapEntry["world_data"].AsGodotDictionary())
                {
                    submapWorldData[WorldMapFogSystem.WorldDataFogStatesKey] =
                        RuntimePlainPayload.ProjectDictionaryInto(
                            submapEntryLease,
                            fogStates,
                            $"WorldMapDataContext.active_submap.{active_map_id}.fog_states"
                        );
                    submapEntry["world_data"] = submapWorldData;
                }
                SetMountedSubmapEntry(active_map_id, submapEntry);
            }
        }
        _materializedFogOwner = fogSystem;
        _materializedFogMapId = active_map_id ?? "";
        _materializedFogRevision = fogSystem.PersistentRevision;
        return true;
    }

    internal bool NeedsActiveWorldFogSave(WorldMapFogSystem fogSystem)
    {
        return fogSystem != null
            && (
                !ReferenceEquals(_materializedFogOwner, fogSystem)
                || !string.Equals(
                    _materializedFogMapId,
                    active_map_id ?? "",
                    StringComparison.Ordinal
                )
                || _materializedFogRevision != fogSystem.PersistentRevision
            );
    }

    private void InvalidateFogMaterialization()
    {
        _materializedFogOwner = null;
        _materializedFogMapId = "";
        _materializedFogRevision = -1;
    }

    internal Vector2I GetActiveWorldSizeCells() =>
        active_generation_definition?.GetWorldSizeCells() ?? Vector2I.Zero;

    public string GetActiveMapId() => active_map_id;

    public string GetActiveMapDisplayName() => active_map_display_name;

    public string GetSubmapReturnHintText()
    {
        if (!IsSubmapActive())
            return "";
        using GodotProjectionLease<GDictionary> submapEntryLease =
            GetMountedSubmapEntryLease(active_map_id);
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(
            submapEntryLease.Value
        );
        return submap.ReturnHintText.Length > 0
            ? submap.ReturnHintText
            : "点击任意地点返回原位置。";
    }

    public WorldMapContextSyncResult SyncActiveWorldContext(
        WorldGenerationDefinition rootGenerationDefinition,
        WorldMapGridSystem gridSystem,
        Vector2I playerCoord,
        Vector2I selectedCoord
    )
    {
        using GodotProjectionLease<GDictionary> rootWorldDataLease =
            RootWorldDataPayloadLease();
        GDictionary rootWorldData = rootWorldDataLease.Value;
        active_map_id = rootWorldData.ContainsKey("active_submap_id")
            ? rootWorldData["active_submap_id"].AsString()
            : "";
        using GodotProjectionLease<GDictionary> activeSubmapEntryLease =
            GetMountedSubmapEntryLease(active_map_id);
        if (active_map_id.Length > 0 && activeSubmapEntryLease.Value.Count == 0)
        {
            active_map_id = "";
            rootWorldData["active_submap_id"] = "";
            ReplaceRootWorldDataPayload(rootWorldData);
        }
        if (active_map_id.Length == 0)
            UseRootWorldDataAsActive();
        else
            UseSeparateActiveWorldData();

        using GodotProjectionLease<GDictionary> resolvedActiveWorldDataLease =
            _resolve_active_world_data_lease();
        GDictionary resolvedActiveWorldData = resolvedActiveWorldDataLease.Value;
        ReplaceActiveWorldDataPayload(resolvedActiveWorldData);
        _activeRuntimeData =
            WorldRuntimeData.FromDictionary(resolvedActiveWorldData) ?? WorldRuntimeData.Empty();
        if (active_map_id.Length == 0)
        {
            _rootRuntimeData = _activeRuntimeData;
            UseRootWorldDataAsActive();
        }
        BindGenerationDefinitions(rootGenerationDefinition);
        active_generation_definition = ResolveActiveGenerationDefinition();
        active_map_display_name = _resolve_active_map_display_name();
        if (active_generation_definition != null && gridSystem != null)
            gridSystem.Setup(
                active_generation_definition.WorldSizeInChunks,
                active_generation_definition.ChunkSize
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
            : WorldMapSettlementData.Empty;

    internal WorldMapNpcData GetWorldNpcAt(Vector2I coord) =>
        _worldNpcByCoord.TryGetValue(coord, out WorldMapNpcData worldNpc)
            ? worldNpc
            : WorldMapNpcData.Empty;

    internal EncounterAnchorData GetEncounterAnchorAt(Vector2I coord) =>
        _encounterAnchorByCoord.TryGetValue(coord, out EncounterAnchorData encounterAnchor)
            ? encounterAnchor
            : null;

    internal WorldMapResourceNodeData GetResourceNodeAt(Vector2I coord) =>
        _resourceNodeByCoord.TryGetValue(coord, out WorldMapResourceNodeData resourceNode)
            ? resourceNode
            : null;

    internal List<WorldMapResourceNodeData> GetActiveResourceNodes()
    {
        var resourceNodes = new List<WorldMapResourceNodeData>();
        foreach (WorldMapResourceNodeData resourceNode in _activeRuntimeData.ResourceNodes)
        {
            if (resourceNode != null && resourceNode.Exists)
                resourceNodes.Add(resourceNode);
        }
        return resourceNodes;
    }

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

    internal GodotProjectionLease<GDictionary> GetSettlementRecordLease(string settlementId) =>
        _settlementsById.TryGetValue(
            settlementId ?? "",
            out WorldMapSettlementRecordData settlement
        )
            ? WorldMapDataProjection.ProjectLease(settlement)
            : RuntimePlainPayload.ProjectDictionaryLease(
                new Dictionary<string, object>(StringComparer.Ordinal),
                "WorldMapDataContext.empty_settlement",
                LifetimeDomain.Request,
                "WorldMapDataContext.empty_settlement"
            );

    internal GodotProjectionLease<GArray> GetAllSettlementRecordsLease() =>
        WorldMapDataProjection.ProjectSettlementRecordsLease(_settlementsById.Values);

    internal WorldMapSettlementStateData GetSettlementStateData(string settlementId) =>
        _activeRuntimeData?.GetSettlementStateData(settlementId);

    internal bool IsSettlementVisited(string settlementId) =>
        GetSettlementStateData(settlementId)?.Visited ?? false;

    public bool MarkSettlementVisited(string settlementId)
    {
        WorldMapSettlementStateData current = _activeRuntimeData?.GetSettlementStateData(
            settlementId
        );
        if (current == null || current.Visited)
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

    public bool TryHarvestResourceNodeAt(Vector2I coord)
    {
        if (!_activeRuntimeData.TryHarvestResourceNode(coord, out _, out _))
        {
            return false;
        }
        _sync_active_world_payload_from_typed();
        _rebuild_world_coord_lookups();
        return true;
    }

    public bool SetActiveSettlementState(
        string settlementId,
        WorldMapSettlementStateData settlementState
    )
    {
        if (
            _activeRuntimeData == null
            || !_activeRuntimeData.TrySetSettlementState(settlementId, settlementState)
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

    internal bool TryAddEncounterAnchor(EncounterAnchorData encounterAnchor)
    {
        if (
            _activeRuntimeData == null
            || !_activeRuntimeData.TryAddEncounterAnchor(encounterAnchor)
        )
        {
            return false;
        }
        _sync_active_world_payload_from_typed();
        _rebuild_world_coord_lookups();
        return true;
    }

    internal bool IsEncounterPlacementCoordAvailable(Vector2I coord)
    {
        if (
            _settlementByCoord.ContainsKey(coord)
            || _worldNpcByCoord.ContainsKey(coord)
            || _encounterAnchorByCoord.ContainsKey(coord)
            || _resourceNodeByCoord.ContainsKey(coord)
        )
        {
            return false;
        }
        foreach (WorldMapEventData worldEvent in _activeRuntimeData.WorldEvents)
        {
            if (worldEvent != null && worldEvent.WorldCoord == coord)
                return false;
        }
        return true;
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

    internal GodotProjectionLease<GDictionary> GetMountedSubmapEntryLease(string submapId) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            GetMountedSubmapEntrySnapshotPlain(submapId),
            $"WorldMapDataContext.mounted_submap.{submapId}",
            LifetimeDomain.Request,
            $"WorldMapDataContext.mounted_submap.{submapId}"
        );

    private IReadOnlyDictionary<string, object> GetMountedSubmapEntrySnapshotPlain(
        string submapId
    )
    {
        IReadOnlyDictionary<string, object> rootWorldData =
            GetRootWorldDataSnapshotPlain();
        if (
            rootWorldData.TryGetValue("mounted_submaps", out object rawMountedSubmaps)
            && rawMountedSubmaps is IReadOnlyDictionary<string, object> mountedSubmaps
            && mountedSubmaps.TryGetValue(submapId ?? "", out object rawSubmap)
            && rawSubmap is IReadOnlyDictionary<string, object> submap
        )
        {
            return RuntimePlainPayload.CloneDictionary(submap);
        }
        return new Dictionary<string, object>(StringComparer.Ordinal);
    }

    internal void SetMountedSubmapEntry(string submapId, Godot.Collections.Dictionary submapEntry)
    {
        using GodotProjectionLease<GDictionary> rootWorldDataLease =
            RootWorldDataPayloadLease();
        GDictionary rootWorldData = rootWorldDataLease.Value;
        using GDictionary mountedSubmaps = GetDictionary(rootWorldData, "mounted_submaps");
        Dictionary<string, object> submapEntryPlain =
            submapEntry == null
                ? new Dictionary<string, object>(StringComparer.Ordinal)
                : RuntimePlainPayload.NormalizeDictionaryStrict(
                    submapEntry,
                    $"WorldMapDataContext.mounted_submap.{submapId}"
                );
        mountedSubmaps[submapId] = RuntimePlainPayload.ProjectDictionaryInto(
            rootWorldDataLease,
            submapEntryPlain,
            $"WorldMapDataContext.mounted_submap.{submapId}"
        );
        rootWorldData["mounted_submaps"] = mountedSubmaps;
        ReplaceRootWorldDataPayload(rootWorldData);
    }

    internal string GetMountedSubmapDisplayName(string submapId, string fallback = "")
    {
        using GodotProjectionLease<GDictionary> submapEntryLease =
            GetMountedSubmapEntryLease(submapId);
        GDictionary submapEntry = submapEntryLease.Value;
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
        using GodotProjectionLease<GDictionary> submapEntryLease =
            GetMountedSubmapEntryLease(submapId);
        GDictionary submapEntry = submapEntryLease.Value;
        if (submapEntry.Count == 0)
        {
            return WorldMapSubmapEnterResult.Fail("未找到目标子地图。");
        }

        using GodotProjectionLease<GDictionary> rootWorldDataLease =
            RootWorldDataPayloadLease();
        GDictionary rootWorldData = rootWorldDataLease.Value;
        using GArray returnStack = GetArray(rootWorldData, "submap_return_stack");
        returnStack.Add(
            RuntimePlainPayload.ProjectDictionaryInto(
                rootWorldDataLease,
                new WorldMapSubmapReturnStackEntry(
                    sourceMapId,
                    sourceCoord
                ).BuildSaveSnapshotPlain(),
                "WorldMapDataContext.submap_return"
            )
        );
        rootWorldData["submap_return_stack"] = returnStack;
        rootWorldData["active_submap_id"] = submapId;
        ReplaceRootWorldDataPayload(rootWorldData);

        WorldMapMountedSubmapData targetSubmap = WorldMapMountedSubmapData.FromDictionary(
            submapEntry
        );
        using GodotProjectionLease<GDictionary> targetWorldDataLease =
            targetSubmap.ProjectWorldDataPayloadLease();
        GDictionary targetWorldData = targetWorldDataLease.Value;
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

        using GodotProjectionLease<GDictionary> submapEntryLease =
            GetMountedSubmapEntryLease(active_map_id);
        GDictionary submapEntry = submapEntryLease.Value;
        if (submapEntry.Count > 0)
        {
            submapEntry["player_coord"] = currentPlayerCoord;
            SetMountedSubmapEntry(active_map_id, submapEntry);
        }

        using GodotProjectionLease<GDictionary> rootWorldDataLease =
            RootWorldDataPayloadLease();
        GDictionary rootWorldData = rootWorldDataLease.Value;
        using GArray returnStack = GetArray(rootWorldData, "submap_return_stack");
        if (returnStack.Count == 0)
        {
            return WorldMapSubmapReturnResult.Fail("当前没有可返回的原坐标。");
        }
        Variant returnEntryValue = returnStack[returnStack.Count - 1];
        if (returnEntryValue.VariantType != Variant.Type.Dictionary)
            return WorldMapSubmapReturnResult.Fail("子地图返回坐标数据无效。");
        WorldMapSubmapReturnStackEntry typedReturnEntry;
        using (GDictionary returnEntry = returnEntryValue.AsGodotDictionary())
        {
            typedReturnEntry = WorldMapSubmapReturnStackEntry.FromDictionary(returnEntry);
        }
        returnStack.RemoveAt(returnStack.Count - 1);
        rootWorldData["submap_return_stack"] = returnStack;
        rootWorldData["active_submap_id"] = typedReturnEntry.MapId;
        ReplaceRootWorldDataPayload(rootWorldData);
        return WorldMapSubmapReturnResult.Success(
            typedReturnEntry.MapId,
            typedReturnEntry.Coord
        );
    }

    internal bool EnsureSubmapGenerated(string submapId)
    {
        using GodotProjectionLease<GDictionary> submapEntryLease =
            GetMountedSubmapEntryLease(submapId);
        GDictionary submapEntry = submapEntryLease.Value;
        if (submapEntry.Count == 0)
            return false;
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(submapEntry);
        if (submap.IsGenerated)
        {
            using GodotProjectionLease<GDictionary> worldDataLease =
                submap.ProjectWorldDataPayloadLease();
            if (worldDataLease.Value.Count > 0)
                return true;
        }
        WorldGenerationDefinition generationDefinition = GetSubmapGenerationDefinition(submapId);
        if (generationDefinition == null)
            return false;
        var gg = new WorldMapGridSystem();
        gg.Setup(generationDefinition.WorldSizeInChunks, generationDefinition.ChunkSize);
        var ss = new WorldMapSpawnSystem();
        WorldMapSpawnSystem.WorldBuildData swd = ss.BuildWorldTyped(generationDefinition, gg);
        submapEntry["world_data"] = RuntimePlainPayload.ProjectDictionaryInto(
            submapEntryLease,
            WorldMapSpawnProjection.BuildSnapshotPlain(swd),
            $"WorldMapDataContext.submap-generation.{submapId}"
        );
        submapEntry["player_coord"] = swd.PlayerStartCoord;
        submapEntry["is_generated"] = true;
        SetMountedSubmapEntry(submapId, submapEntry);
        return true;
    }

    internal WorldGenerationDefinition GetSubmapGenerationDefinition(string submapId)
    {
        if (string.IsNullOrEmpty(submapId))
            return null;
        return _submapGenerationDefinitions.TryGetValue(
            submapId,
            out WorldGenerationDefinition definition
        )
            ? definition
            : null;
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
        _resourceNodeByCoord.Clear();
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
        foreach (WorldMapResourceNodeData resourceNode in _activeRuntimeData.ResourceNodes)
        {
            if (resourceNode == null || !resourceNode.Exists)
                continue;
            _resourceNodeByCoord[resourceNode.WorldCoord] = resourceNode;
        }
        foreach (WorldMapEventData worldEvent in _activeRuntimeData.WorldEvents)
        {
            if (worldEvent == null || !worldEvent.IsDiscovered)
                continue;
            _worldEventByCoord[worldEvent.WorldCoord] = worldEvent;
        }
    }

    private GodotProjectionLease<GDictionary> _resolve_active_world_data_lease()
    {
        if (active_map_id.Length == 0)
        {
            return RuntimePlainPayload.ProjectDictionaryLease(
                _rootRuntimeData?.BuildSaveSnapshotPlain()
                    ?? new Dictionary<string, object>(StringComparer.Ordinal),
                "WorldMapDataContext.resolve_active_world_data.root",
                LifetimeDomain.Request,
                "WorldMapDataContext.resolve_active_world_data.root"
            );
        }
        using GodotProjectionLease<GDictionary> submapEntryLease =
            GetMountedSubmapEntryLease(active_map_id);
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(
            submapEntryLease.Value
        );
        Dictionary<string, object> submapWorldData = submap.BuildWorldDataSnapshotPlain();
        return RuntimePlainPayload.ProjectDictionaryLease(
            submapWorldData.Count > 0
                ? submapWorldData
                : _rootRuntimeData?.BuildSaveSnapshotPlain()
                    ?? new Dictionary<string, object>(StringComparer.Ordinal),
            "WorldMapDataContext.resolve_active_world_data",
            LifetimeDomain.Request,
            "WorldMapDataContext.resolve_active_world_data"
        );
    }

    private void BindGenerationDefinitions(WorldGenerationDefinition rootDefinition)
    {
        if (ReferenceEquals(_rootGenerationDefinition, rootDefinition))
            return;
        _rootGenerationDefinition = rootDefinition;
        _submapGenerationDefinitions.Clear();
        IndexMountedSubmapDefinitions(rootDefinition);
    }

    private void IndexMountedSubmapDefinitions(WorldGenerationDefinition definition)
    {
        if (definition == null)
            return;
        foreach (MountedSubmapDefinition submap in definition.MountedSubmaps)
        {
            if (submap == null || submap.SubmapId == "" || submap.Generation == null)
                continue;
            string submapId = submap.SubmapId.ToString();
            if (!_submapGenerationDefinitions.TryAdd(submapId, submap.Generation))
            {
                throw new InvalidOperationException(
                    $"Duplicate mounted submap definition id '{submapId}'."
                );
            }
            IndexMountedSubmapDefinitions(submap.Generation);
        }
    }

    private WorldGenerationDefinition ResolveActiveGenerationDefinition() =>
        active_map_id.Length == 0
            ? _rootGenerationDefinition
            : GetSubmapGenerationDefinition(active_map_id);

    private string _resolve_active_map_display_name()
    {
        if (active_map_id.Length == 0)
            return "大地图";
        using GodotProjectionLease<GDictionary> submapEntryLease =
            GetMountedSubmapEntryLease(active_map_id);
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(
            submapEntryLease.Value
        );
        return submap.DisplayNameOrFallback(active_map_id);
    }

    private Vector2I _resolve_active_map_player_coord(Vector2I fallback)
    {
        if (active_map_id.Length == 0)
        {
            return _rootRuntimeData?.HasPlayerStartCoord == true
                ? _rootRuntimeData.PlayerStartCoord
                : fallback;
        }
        using GodotProjectionLease<GDictionary> submapEntryLease =
            GetMountedSubmapEntryLease(active_map_id);
        WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(
            submapEntryLease.Value
        );
        if (submap.HasPlayerCoord)
            return submap.PlayerCoord;
        return _activeRuntimeData?.HasPlayerStartCoord == true
            ? _activeRuntimeData.PlayerStartCoord
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
        using GodotProjectionLease<GDictionary> submapEntryLease =
            GetMountedSubmapEntryLease(active_map_id);
        GDictionary submapEntry = submapEntryLease.Value;
        if (submapEntry.Count > 0)
        {
            submapEntry["world_data"] = RuntimePlainPayload.ProjectDictionaryInto(
                submapEntryLease,
                _activeRuntimeData?.BuildSaveSnapshotPlain()
                    ?? new Dictionary<string, object>(StringComparer.Ordinal),
                $"WorldMapDataContext.active_submap.{active_map_id}"
            );
            SetMountedSubmapEntry(active_map_id, submapEntry);
        }
    }

    private GodotProjectionLease<GDictionary> RootWorldDataPayloadLease() =>
        WorldMapDataProjection.ProjectLease(_rootRuntimeData);

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

    private GodotProjectionLease<GDictionary> ActiveWorldDataPayloadLease() =>
        _activeWorldUsesRoot
            ? RootWorldDataPayloadLease()
            : WorldMapDataProjection.ProjectLease(_activeRuntimeData);

    private void ReplaceActiveWorldDataPayload(GDictionary payload)
    {
        if (_activeWorldUsesRoot)
        {
            ReplaceRootWorldDataPayload(payload);
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

    private static GArray GetArray(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            throw new InvalidOperationException($"World payload requires array field '{key}'.");
        Variant value = source[key];
        if (value.VariantType != Variant.Type.Array)
            throw new InvalidOperationException($"World payload field '{key}' must be an Array.");
        return value.AsGodotArray();
    }

    private static GDictionary GetDictionary(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            throw new InvalidOperationException($"World payload requires dictionary field '{key}'.");
        Variant value = source[key];
        if (value.VariantType != Variant.Type.Dictionary)
            throw new InvalidOperationException(
                $"World payload field '{key}' must be a Dictionary."
            );
        return value.AsGodotDictionary();
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
    internal static readonly string[] SaveFields = { "map_id", "coord" };

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

    internal Dictionary<string, object> BuildSaveSnapshotPlain() =>
        new(StringComparer.Ordinal)
        {
            ["map_id"] = MapId,
            ["coord"] = Coord,
        };
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
        if (source == null)
            return;
        Dictionary<string, object> normalized =
            RuntimePlainPayload.NormalizeDictionaryStrict(source, ownerPath);
        foreach (KeyValuePair<string, object> entry in normalized)
        {
            target[entry.Key] = entry.Value;
        }
    }

    internal static void ReplacePlain(
        Dictionary<string, object> target,
        IReadOnlyDictionary<string, object> source
    )
    {
        target.Clear();
        Dictionary<string, object> cloned = RuntimePlainPayload.CloneDictionary(source);
        foreach (KeyValuePair<string, object> entry in cloned)
            target[entry.Key] = entry.Value;
    }

    internal static GodotProjectionLease<GDictionary> ProjectLease(
        IReadOnlyDictionary<string, object> source,
        string ownerPath
    ) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            source,
            ownerPath,
            LifetimeDomain.Request,
            ownerPath
        );
}

public sealed class WorldMapMountedSubmapData
{
    private static readonly Vector2I UnsetPlayerCoord = new(-1, -1);
    internal static readonly string[] SaveFields =
    {
        "submap_id",
        "display_name",
        "generation_config_path",
        "return_hint_text",
        "is_generated",
        "player_coord",
        "world_data",
    };
    internal static readonly string[] SaveStringFields =
    {
        "display_name",
        "generation_config_path",
        "return_hint_text",
    };

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
        IReadOnlyDictionary<string, object> worldData
    )
    {
        Exists = exists;
        DisplayName = displayName ?? "";
        GenerationConfigPath = generationConfigPath ?? "";
        ReturnHintText = returnHintText ?? "";
        IsGenerated = isGenerated;
        PlayerCoord = playerCoord;
        WorldMapPlainPayload.ReplacePlain(_worldData, worldData);
    }

    public bool HasPlayerCoord => PlayerCoord != UnsetPlayerCoord;

    internal GodotProjectionLease<GDictionary> ProjectWorldDataPayloadLease() =>
        WorldMapPlainPayload.ProjectLease(
            _worldData,
            "WorldMapMountedSubmapData.worldData"
        );

    internal Dictionary<string, object> BuildWorldDataSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(_worldData);

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
                null
            );
        }
        Dictionary<string, object> worldData = ReadDictionaryPlain(data, "world_data");
        return new WorldMapMountedSubmapData(
            true,
            ReadString(data, "display_name"),
            ReadString(data, "generation_config_path"),
            ReadString(data, "return_hint_text"),
            ReadBool(data, "is_generated"),
            ReadVector2I(data, "player_coord", UnsetPlayerCoord),
            worldData
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

    private static Dictionary<string, object> ReadDictionaryPlain(
        GDictionary data,
        string key
    )
    {
        if (data == null || !data.ContainsKey(key))
            return new Dictionary<string, object>(StringComparer.Ordinal);
        Variant value = data[key];
        if (value.VariantType != Variant.Type.Dictionary)
            return new Dictionary<string, object>(StringComparer.Ordinal);
        using GDictionary worldData = value.AsGodotDictionary();
        return RuntimePlainPayload.NormalizeDictionaryStrict(
            worldData,
            "WorldMapMountedSubmapData.worldData"
        );
    }
}

public sealed class WorldMapSettlementRecordData
{
    internal static readonly string[] SaveFields =
    {
        "entity_id",
        "template_id",
        "settlement_id",
        "display_name",
        "tier",
        "tier_name",
        "faction_id",
        "country_id",
        "origin",
        "footprint_size",
        "facilities",
        "service_npcs",
        "available_services",
        "is_player_start",
        "settlement_state",
    };
    internal static readonly string[] SaveStringFields =
    {
        "entity_id",
        "template_id",
        "settlement_id",
        "display_name",
        "tier_name",
        "faction_id",
        "country_id",
    };
    internal static readonly string[] SaveCoordFields = { "origin", "footprint_size" };
    internal static readonly string[] SaveArrayFields =
    {
        "facilities",
        "service_npcs",
        "available_services",
    };

    public readonly string EntityId;
    public readonly string SettlementId;
    public readonly string DisplayName;
    public readonly string CountryId;
    public readonly Vector2I Origin;
    public readonly Vector2I FootprintSize;
    public readonly int Tier;
    public WorldMapSettlementStateData SettlementState { get; }
    private readonly Dictionary<string, object> _sourceData = new(StringComparer.Ordinal);

    private WorldMapSettlementRecordData(
        string entityId,
        string settlementId,
        string displayName,
        string countryId,
        Vector2I origin,
        Vector2I footprintSize,
        int tier,
        WorldMapSettlementStateData settlementState,
        IReadOnlyDictionary<string, object> sourceData
    )
    {
        EntityId = entityId ?? "";
        SettlementId = settlementId ?? "";
        DisplayName = displayName ?? "";
        CountryId = countryId ?? "";
        Origin = origin;
        FootprintSize = footprintSize;
        Tier = tier;
        SettlementState = settlementState;
        foreach (
            KeyValuePair<string, object> entry in RuntimePlainPayload.CloneDictionary(sourceData)
        )
        {
            _sourceData[entry.Key] = entry.Value;
        }
        _sourceData.Remove("settlement_state");
    }

    internal Dictionary<string, object> BuildSaveSnapshotPlain()
    {
        Dictionary<string, object> snapshot = RuntimePlainPayload.CloneDictionary(_sourceData);
        snapshot["settlement_state"] = SettlementState.BuildSnapshotPlain();
        return snapshot;
    }

    internal WorldMapSettlementRecordData WithSettlementState(
        WorldMapSettlementStateData settlementState
    ) =>
        settlementState == null
            ? null
            : new WorldMapSettlementRecordData(
                EntityId,
                SettlementId,
                DisplayName,
                CountryId,
                Origin,
                FootprintSize,
                Tier,
                settlementState,
                _sourceData
            );

    public WorldMapSettlementData ToSettlementData() =>
        WorldMapSettlementData.Create(SettlementId, DisplayName);

    public static WorldMapSettlementRecordData FromDictionary(GDictionary data)
    {
        if (data == null || data.Count == 0)
            return null;
        if (
            !data.ContainsKey("settlement_state")
            || data["settlement_state"].VariantType != Variant.Type.Dictionary
        )
        {
            return null;
        }
        using GDictionary statePayload = data["settlement_state"].AsGodotDictionary();
        WorldMapSettlementStateData settlementState =
            WorldMapSettlementStateData.FromDictionary(statePayload);
        if (settlementState == null)
            return null;
        Dictionary<string, object> sourceData;
        try
        {
            sourceData = RuntimePlainPayload.NormalizeDictionaryStrict(
                data,
                "WorldMapSettlementRecordData.sourceData"
            );
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        return new WorldMapSettlementRecordData(
            WorldMapDictionaryReaders.ReadString(data, "entity_id"),
            WorldMapDictionaryReaders.ReadString(data, "settlement_id"),
            WorldMapDictionaryReaders.ReadString(data, "display_name"),
            WorldMapDictionaryReaders.ReadString(data, "country_id"),
            WorldMapDictionaryReaders.ReadVector2I(data, "origin", Vector2I.Zero),
            WorldMapDictionaryReaders.ReadVector2I(data, "footprint_size", Vector2I.One),
            WorldMapDictionaryReaders.ReadInt(data, "tier", 0),
            settlementState,
            sourceData
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

    internal static WorldMapSettlementData Empty { get; } = new(false, "", "");

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

public sealed class WorldMapNpcData
{
    public readonly bool Exists;
    public readonly Vector2I Coord;
    public readonly string DisplayName;
    public readonly string FactionId;
    public readonly string ServiceType;
    public readonly string FacilityName;
    private readonly Dictionary<string, object> _sourceData = new(StringComparer.Ordinal);

    private WorldMapNpcData(
        bool exists,
        Vector2I coord,
        string displayName,
        string factionId,
        string serviceType,
        string facilityName,
        GDictionary sourceData
    )
    {
        Exists = exists;
        Coord = coord;
        DisplayName = displayName ?? "";
        FactionId = factionId ?? "";
        ServiceType = serviceType ?? "";
        FacilityName = facilityName ?? "";
        WorldMapPlainPayload.Replace(
            _sourceData,
            sourceData,
            "WorldMapNpcData.sourceData"
        );
    }

    public bool IsEmpty => !Exists;

    internal static WorldMapNpcData Empty { get; } =
        new(false, Vector2I.Zero, "", "", "", "", null);

    public bool HasValidCharacterInfoFields =>
        Exists
        && DisplayName.Length > 0
        && FactionId.Length > 0;

    internal GodotProjectionLease<GDictionary> DuplicateSourcePayloadLease() =>
        WorldMapPlainPayload.ProjectLease(_sourceData, "WorldMapNpcData.sourceData");

    internal Dictionary<string, object> BuildSaveSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(_sourceData);

    public static WorldMapNpcData FromDictionary(GDictionary data)
    {
        if (data == null || data.Count == 0)
        {
            return new WorldMapNpcData(false, Vector2I.Zero, "", "", "", "", null);
        }
        return new WorldMapNpcData(
            true,
            WorldMapDictionaryReaders.ReadVector2I(data, "coord", Vector2I.Zero),
            ReadTrimmedString(data, "display_name"),
            ReadTrimmedString(data, "faction_id"),
            ReadExactTrimmedString(data, "service_type"),
            ReadExactTrimmedString(data, "facility_name"),
            data
        );
    }

    private static string ReadExactTrimmedString(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return "";
        Variant value = data[key];
        return value.VariantType == Variant.Type.String ? value.AsString().StripEdges() : "";
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
    internal static readonly string[] SaveFields =
    {
        "event_id",
        "display_name",
        "world_coord",
        "event_type",
        "target_submap_id",
        "discovery_condition_id",
        "prompt_title",
        "prompt_text",
        "is_discovered",
    };
    internal static readonly string[] SaveStringFields =
    {
        "event_id",
        "display_name",
        "event_type",
        "target_submap_id",
        "discovery_condition_id",
        "prompt_title",
        "prompt_text",
    };

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
            sourceData,
            "WorldMapEventData.sourceData"
        );
    }

    public bool IsTriggerableSubmapEntry =>
        IsDiscovered
        && WorldEventDefinition.IsEnterSubmapEventType(EventType)
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

    internal GodotProjectionLease<GDictionary> DuplicateSourcePayloadLease() =>
        WorldMapPlainPayload.ProjectLease(_sourceData, "WorldMapEventData.sourceData");

    internal Dictionary<string, object> BuildSaveSnapshotPlain() =>
        RuntimePlainPayload.CloneDictionary(_sourceData);

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

}
