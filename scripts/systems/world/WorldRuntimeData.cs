using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class WorldRuntimeData
{
    private readonly List<WorldMapSubmapReturnStackEntry> _submapReturnStack = new();
    private readonly List<WorldMapSettlementRecordData> _settlements = new();
    private readonly List<WorldMapEventData> _worldEvents = new();
    private readonly List<EncounterAnchorData> _encounterAnchors = new();
    private readonly List<WorldMapResourceNodeData> _resourceNodes = new();
    private readonly Dictionary<string, WorldMapMountedSubmapData> _mountedSubmaps =
        new(System.StringComparer.Ordinal);
    private readonly List<WorldMapNpcData> _worldNpcs = new();
    private readonly Dictionary<string, object> _fogStates = new(System.StringComparer.Ordinal);

    public long MapSeed { get; private set; } = 1;
    public int WorldStep { get; private set; }
    public int NextEquipmentInstanceSerial { get; private set; } = 1;
    public string ActiveSubmapId { get; private set; } = "";
    public Vector2I PlayerStartCoord { get; private set; } = Vector2I.Zero;
    public bool HasPlayerStartCoord { get; private set; }
    public string PlayerStartSettlementId { get; private set; } = "";
    public bool HasPlayerStartSettlementId { get; private set; }
    public string PlayerStartSettlementName { get; private set; } = "";
    public bool HasPlayerStartSettlementName { get; private set; }
    public bool HasFogStates { get; private set; }
    public bool HasWorldNpcs { get; private set; }

    public IReadOnlyList<WorldMapSettlementRecordData> Settlements => _settlements;
    public IReadOnlyList<EncounterAnchorData> EncounterAnchors => _encounterAnchors;
    public IReadOnlyList<WorldMapResourceNodeData> ResourceNodes => _resourceNodes;
    public IReadOnlyList<WorldMapEventData> WorldEvents => _worldEvents;
    public IReadOnlyList<WorldMapNpcData> WorldNpcs => _worldNpcs;

    private WorldRuntimeData() { }

    internal static WorldRuntimeData Empty() => new();

    // Typed deep copy for rollback snapshots — replaces the whole-map
    // ToDictionary/FromDictionary round-trip on the capture path. Immutable
    // record elements (settlements, events, nodes, npcs, submaps, return stack)
    // are shared by reference; mutable EncounterAnchorData is copied per element;
    // fog states are plain payload graphs cloned via RuntimePlainPayload.
    internal WorldRuntimeData DuplicateState()
    {
        WorldRuntimeData copy = new()
        {
            MapSeed = MapSeed,
            WorldStep = WorldStep,
            NextEquipmentInstanceSerial = NextEquipmentInstanceSerial,
            ActiveSubmapId = ActiveSubmapId,
            PlayerStartCoord = PlayerStartCoord,
            HasPlayerStartCoord = HasPlayerStartCoord,
            PlayerStartSettlementId = PlayerStartSettlementId,
            HasPlayerStartSettlementId = HasPlayerStartSettlementId,
            PlayerStartSettlementName = PlayerStartSettlementName,
            HasPlayerStartSettlementName = HasPlayerStartSettlementName,
            HasFogStates = HasFogStates,
            HasWorldNpcs = HasWorldNpcs,
        };
        copy._submapReturnStack.AddRange(_submapReturnStack);
        copy._settlements.AddRange(_settlements);
        copy._worldEvents.AddRange(_worldEvents);
        foreach (EncounterAnchorData encounterAnchor in _encounterAnchors)
        {
            if (encounterAnchor != null)
                copy._encounterAnchors.Add(encounterAnchor.DuplicateState());
        }
        copy._resourceNodes.AddRange(_resourceNodes);
        foreach (KeyValuePair<string, WorldMapMountedSubmapData> entry in _mountedSubmaps)
            copy._mountedSubmaps[entry.Key] = entry.Value;
        copy._worldNpcs.AddRange(_worldNpcs);
        foreach (
            KeyValuePair<string, object> entry in RuntimePlainPayload.CloneDictionary(_fogStates)
        )
        {
            copy._fogStates[entry.Key] = entry.Value;
        }
        return copy;
    }

    internal static WorldRuntimeData FromDictionary(GDictionary data)
    {
        if (data == null)
        {
            return null;
        }
        WorldRuntimeData result = new();
        result.MapSeed = ReadLong(data, WorldRuntimeSaveSchema.MapSeed, 1L);
        result.WorldStep = ReadInt(data, WorldRuntimeSaveSchema.WorldStep, 0);
        result.NextEquipmentInstanceSerial = ReadInt(
            data,
            WorldRuntimeSaveSchema.NextEquipmentInstanceSerial,
            1
        );
        result.ActiveSubmapId = ReadString(data, WorldRuntimeSaveSchema.ActiveSubmapId);
        result.HasWorldNpcs = data.ContainsKey(WorldRuntimeSaveSchema.WorldNpcs);
        result.HasPlayerStartCoord = data.ContainsKey(WorldRuntimeSaveSchema.PlayerStartCoord);
        result.PlayerStartCoord = ReadVector2I(
            data,
            WorldRuntimeSaveSchema.PlayerStartCoord,
            Vector2I.Zero
        );
        result.HasPlayerStartSettlementId =
            data.ContainsKey(WorldRuntimeSaveSchema.PlayerStartSettlementId);
        result.PlayerStartSettlementId = ReadString(
            data,
            WorldRuntimeSaveSchema.PlayerStartSettlementId
        );
        result.HasPlayerStartSettlementName =
            data.ContainsKey(WorldRuntimeSaveSchema.PlayerStartSettlementName);
        result.PlayerStartSettlementName = ReadString(
            data,
            WorldRuntimeSaveSchema.PlayerStartSettlementName
        );
        result.HasFogStates = data.ContainsKey(WorldRuntimeSaveSchema.FogStates);
        if (result.HasFogStates)
        {
            using GDictionary fogStateValues =
                ReadDictionary(data, WorldRuntimeSaveSchema.FogStates);
            Dictionary<string, object> fogStates;
            try
            {
                fogStates = RuntimePlainPayload.NormalizeDictionaryStrict(
                    fogStateValues,
                    "WorldRuntimeData.fog_states"
                );
            }
            catch (System.InvalidOperationException)
            {
                return null;
            }
            foreach (KeyValuePair<string, object> entry in fogStates)
            {
                result._fogStates[entry.Key] = entry.Value;
            }
        }

        using GArray returnStackValues =
            ReadArray(data, WorldRuntimeSaveSchema.SubmapReturnStack);
        if (!ReadReturnStack(result._submapReturnStack, returnStackValues))
            return null;
        using GArray settlementValues = ReadArray(data, WorldRuntimeSaveSchema.Settlements);
        if (!ReadSettlements(result._settlements, settlementValues))
            return null;
        using GArray eventValues = ReadArray(data, WorldRuntimeSaveSchema.WorldEvents);
        if (!ReadWorldEvents(result._worldEvents, eventValues))
            return null;
        using GArray encounterAnchorValues =
            ReadArray(data, WorldRuntimeSaveSchema.EncounterAnchors);
        if (!ReadEncounterAnchors(result._encounterAnchors, encounterAnchorValues))
            return null;
        using GArray resourceNodeValues =
            ReadArray(data, WorldRuntimeSaveSchema.ResourceNodes);
        if (!ReadResourceNodes(result._resourceNodes, resourceNodeValues))
            return null;
        using GDictionary mountedSubmapValues =
            ReadDictionary(data, WorldRuntimeSaveSchema.MountedSubmaps);
        if (!ReadMountedSubmaps(result._mountedSubmaps, mountedSubmapValues))
            return null;
        using GArray worldNpcValues = ReadArray(data, WorldRuntimeSaveSchema.WorldNpcs);
        if (!ReadWorldNpcs(result._worldNpcs, worldNpcValues))
            return null;
        return result;
    }

    internal Dictionary<string, object> BuildSaveSnapshotPlain()
    {
        var returnStack = new List<object>();
        foreach (WorldMapSubmapReturnStackEntry entry in _submapReturnStack)
        {
            if (entry == null)
                continue;
            returnStack.Add(
                new Dictionary<string, object>(System.StringComparer.Ordinal)
                {
                    ["map_id"] = entry.MapId,
                    ["coord"] = entry.Coord,
                }
            );
        }

        var settlements = new List<object>();
        foreach (WorldMapSettlementRecordData settlement in _settlements)
        {
            if (settlement != null)
                settlements.Add(settlement.BuildSaveSnapshotPlain());
        }

        var worldEvents = new List<object>();
        foreach (WorldMapEventData worldEvent in _worldEvents)
        {
            if (worldEvent != null)
                worldEvents.Add(worldEvent.BuildSaveSnapshotPlain());
        }

        var encounterAnchors = new List<object>();
        foreach (EncounterAnchorData encounterAnchor in _encounterAnchors)
        {
            if (encounterAnchor == null)
                continue;
            encounterAnchors.Add(encounterAnchor.BuildSaveSnapshotPlain());
        }

        var resourceNodes = new List<object>();
        foreach (WorldMapResourceNodeData resourceNode in _resourceNodes)
        {
            if (resourceNode == null || !resourceNode.Exists)
                continue;
            resourceNodes.Add(
                new Dictionary<string, object>(System.StringComparer.Ordinal)
                {
                    ["node_id"] = resourceNode.NodeId,
                    ["node_kind"] = resourceNode.NodeKind,
                    ["display_name"] = resourceNode.DisplayName,
                    ["world_coord"] = resourceNode.WorldCoord,
                    ["yield_item_id"] = resourceNode.YieldItemId,
                    ["source_settlement_id"] = resourceNode.SourceSettlementId,
                    ["max_charges"] = resourceNode.MaxCharges,
                    ["remaining_charges"] = resourceNode.RemainingCharges,
                }
            );
        }

        var mountedSubmaps = new Dictionary<string, object>(System.StringComparer.Ordinal);
        foreach (KeyValuePair<string, WorldMapMountedSubmapData> entry in _mountedSubmaps)
        {
            WorldMapMountedSubmapData submap = entry.Value;
            if (submap == null || string.IsNullOrEmpty(entry.Key))
                continue;
            mountedSubmaps[entry.Key] = new Dictionary<string, object>(
                System.StringComparer.Ordinal
            )
            {
                ["submap_id"] = entry.Key,
                ["display_name"] = submap.DisplayName,
                ["generation_config_path"] = submap.GenerationConfigPath,
                ["return_hint_text"] = submap.ReturnHintText,
                ["is_generated"] = submap.IsGenerated,
                ["player_coord"] = submap.PlayerCoord,
                ["world_data"] = submap.BuildWorldDataSnapshotPlain(),
            };
        }

        var result = new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            [WorldRuntimeSaveSchema.MapSeed] = MapSeed,
            [WorldRuntimeSaveSchema.WorldStep] = WorldStep,
            [WorldRuntimeSaveSchema.NextEquipmentInstanceSerial] =
                NextEquipmentInstanceSerial,
            [WorldRuntimeSaveSchema.ActiveSubmapId] = ActiveSubmapId,
            [WorldRuntimeSaveSchema.SubmapReturnStack] = returnStack,
            [WorldRuntimeSaveSchema.Settlements] = settlements,
            [WorldRuntimeSaveSchema.WorldEvents] = worldEvents,
            [WorldRuntimeSaveSchema.EncounterAnchors] = encounterAnchors,
            [WorldRuntimeSaveSchema.ResourceNodes] = resourceNodes,
            [WorldRuntimeSaveSchema.MountedSubmaps] = mountedSubmaps,
        };
        if (HasWorldNpcs)
        {
            var worldNpcs = new List<object>();
            foreach (WorldMapNpcData npc in _worldNpcs)
            {
                if (npc != null && !npc.IsEmpty)
                    worldNpcs.Add(npc.BuildSaveSnapshotPlain());
            }
            result[WorldRuntimeSaveSchema.WorldNpcs] = worldNpcs;
        }
        if (HasPlayerStartCoord)
            result[WorldRuntimeSaveSchema.PlayerStartCoord] = PlayerStartCoord;
        if (HasPlayerStartSettlementId)
            result[WorldRuntimeSaveSchema.PlayerStartSettlementId] =
                PlayerStartSettlementId;
        if (HasPlayerStartSettlementName)
            result[WorldRuntimeSaveSchema.PlayerStartSettlementName] =
                PlayerStartSettlementName;
        if (HasFogStates)
            result[WorldRuntimeSaveSchema.FogStates] =
                RuntimePlainPayload.CloneDictionary(_fogStates);
        return result;
    }

    // Write fog states straight into the typed payload, so saving fog after a move
    // doesn't have to ToDictionary/FromDictionary the whole world.
    internal void SetFogStates(IReadOnlyDictionary<string, object> fogStates)
    {
        _fogStates.Clear();
        HasFogStates = fogStates != null;
        if (fogStates == null)
        {
            return;
        }
        Dictionary<string, object> snapshot = RuntimePlainPayload.CloneDictionary(fogStates);
        foreach (KeyValuePair<string, object> entry in snapshot)
        {
            _fogStates[entry.Key] = entry.Value;
        }
    }

    // Flip a world event to discovered in place. WorldMapEventData is immutable, so
    // rebuild just the one record from its source payload; runs only on an actual
    // discovery transition (rare), not on every move.
    internal bool MarkWorldEventDiscovered(StringName eventId)
    {
        for (int index = 0; index < _worldEvents.Count; index++)
        {
            WorldMapEventData worldEvent = _worldEvents[index];
            if (worldEvent == null || worldEvent.EventId != eventId || worldEvent.IsDiscovered)
            {
                continue;
            }
            using GodotProjectionLease<GDictionary> payloadLease =
                worldEvent.DuplicateSourcePayloadLease();
            GDictionary payload = payloadLease.Value;
            payload["is_discovered"] = true;
            WorldMapEventData updated = WorldMapEventData.FromDictionary(payload);
            if (updated == null)
            {
                return false;
            }
            _worldEvents[index] = updated;
            return true;
        }
        return false;
    }

    // Spend one charge from the resource node at coord. WorldMapResourceNodeData is
    // immutable, so rebuild just the one node with a lowered charge count, and drop it
    // from the world once depleted. Runs only on an actual harvest, not on every move.
    internal bool TryHarvestResourceNode(
        Vector2I coord,
        out WorldMapResourceNodeData node,
        out int remainingAfter
    )
    {
        node = null;
        remainingAfter = 0;
        for (int index = 0; index < _resourceNodes.Count; index++)
        {
            WorldMapResourceNodeData current = _resourceNodes[index];
            if (current == null || !current.Exists || current.WorldCoord != coord)
            {
                continue;
            }
            if (current.RemainingCharges <= 0)
            {
                return false;
            }
            node = current;
            remainingAfter = current.RemainingCharges - 1;
            if (remainingAfter <= 0)
            {
                _resourceNodes.RemoveAt(index);
            }
            else
            {
                _resourceNodes[index] = current.WithRemainingCharges(remainingAfter);
            }
            return true;
        }
        return false;
    }

    internal bool TrySetSettlementState(string settlementId, WorldMapSettlementStateData state)
    {
        if (string.IsNullOrEmpty(settlementId) || state == null)
        {
            return false;
        }
        for (int index = 0; index < _settlements.Count; index++)
        {
            WorldMapSettlementRecordData settlement = _settlements[index];
            if (settlement == null || settlement.SettlementId != settlementId)
            {
                continue;
            }
            WorldMapSettlementRecordData updated = settlement.WithSettlementState(state);
            if (updated == null)
                return false;
            _settlements[index] = updated;
            return true;
        }
        return false;
    }

    internal bool MarkSettlementVisited(string settlementId)
    {
        WorldMapSettlementStateData current = GetSettlementStateData(settlementId);
        if (current == null || current.Visited)
        {
            return false;
        }
        return TrySetSettlementState(settlementId, current.WithVisited(true));
    }

    internal bool RemoveEncounterAnchorById(StringName encounterId)
    {
        if (encounterId == "")
        {
            return false;
        }
        for (int index = 0; index < _encounterAnchors.Count; index++)
        {
            EncounterAnchorData encounterAnchor = _encounterAnchors[index];
            if (encounterAnchor == null || encounterAnchor.entity_id != encounterId)
            {
                continue;
            }
            _encounterAnchors.RemoveAt(index);
            return true;
        }
        return false;
    }

    internal bool TryAddEncounterAnchor(EncounterAnchorData encounterAnchor)
    {
        if (
            encounterAnchor == null
            || encounterAnchor.entity_id == ""
            || encounterAnchor.encounter_profile_id == ""
            || string.IsNullOrWhiteSpace(encounterAnchor.display_name)
            || encounterAnchor.world_coord.X < 0
            || encounterAnchor.world_coord.Y < 0
        )
        {
            return false;
        }
        foreach (EncounterAnchorData existing in _encounterAnchors)
        {
            if (
                existing != null
                && (
                    existing.entity_id == encounterAnchor.entity_id
                    || existing.world_coord == encounterAnchor.world_coord
                )
            )
            {
                return false;
            }
        }
        _encounterAnchors.Add(encounterAnchor.DuplicateState());
        return true;
    }

    internal void SetWorldStep(int worldStep)
    {
        WorldStep = worldStep;
    }

    internal WorldMapSettlementStateData GetSettlementStateData(string settlementId)
    {
        if (string.IsNullOrEmpty(settlementId))
        {
            return null;
        }
        foreach (WorldMapSettlementRecordData settlement in _settlements)
        {
            if (settlement != null && settlement.SettlementId == settlementId)
            {
                return settlement.SettlementState;
            }
        }
        return null;
    }

    private static bool ReadReturnStack(
        List<WorldMapSubmapReturnStackEntry> target,
        GArray values
    )
    {
        foreach (Variant value in values)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return false;
            using GDictionary payload = value.AsGodotDictionary();
            target.Add(WorldMapSubmapReturnStackEntry.FromDictionary(payload));
        }
        return true;
    }

    private static bool ReadSettlements(List<WorldMapSettlementRecordData> target, GArray values)
    {
        foreach (Variant value in values)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return false;
            using GDictionary payload = value.AsGodotDictionary();
            if (
                payload.ContainsKey("settlement_state")
                && payload["settlement_state"].VariantType != Variant.Type.Dictionary
            )
                return false;
            WorldMapSettlementRecordData settlement =
                WorldMapSettlementRecordData.FromDictionary(payload);
            if (settlement == null)
                return false;
            target.Add(settlement);
        }
        return true;
    }

    private static bool ReadWorldEvents(List<WorldMapEventData> target, GArray values)
    {
        foreach (Variant value in values)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return false;
            using GDictionary payload = value.AsGodotDictionary();
            WorldMapEventData worldEvent = WorldMapEventData.FromDictionary(payload);
            if (worldEvent != null)
            {
                target.Add(worldEvent);
            }
        }
        return true;
    }

    private static bool ReadEncounterAnchors(List<EncounterAnchorData> target, GArray values)
    {
        foreach (Variant value in values)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return false;
            using GDictionary payload = value.AsGodotDictionary();
            EncounterAnchorData encounterAnchor = EncounterAnchorData.FromDictionary(payload);
            if (encounterAnchor == null)
                return false;
            target.Add(encounterAnchor);
        }
        return true;
    }

    private static bool ReadResourceNodes(List<WorldMapResourceNodeData> target, GArray values)
    {
        foreach (Variant value in values)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return false;
            using GDictionary payload = value.AsGodotDictionary();
            WorldMapResourceNodeData resourceNode = WorldMapResourceNodeData.FromDictionary(
                payload
            );
            if (resourceNode != null && resourceNode.Exists)
            {
                target.Add(resourceNode);
            }
        }
        return true;
    }

    private static bool ReadMountedSubmaps(
        Dictionary<string, WorldMapMountedSubmapData> target,
        GDictionary values
    )
    {
        if (values == null)
        {
            return true;
        }
        foreach (KeyValuePair<Variant, Variant> entry in values)
        {
            Variant key = entry.Key;
            Variant value = entry.Value;
            if (value.VariantType != Variant.Type.Dictionary)
                return false;
            string submapId = VariantText(key);
            using GDictionary payload = value.AsGodotDictionary();
            WorldMapMountedSubmapData submap = WorldMapMountedSubmapData.FromDictionary(payload);
            if (!string.IsNullOrEmpty(submapId) && submap != null && submap.Exists)
            {
                target[submapId] = submap;
            }
        }
        return true;
    }

    private static bool ReadWorldNpcs(List<WorldMapNpcData> target, GArray values)
    {
        foreach (Variant value in values)
        {
            if (value.VariantType != Variant.Type.Dictionary)
                return false;
            using GDictionary payload = value.AsGodotDictionary();
            WorldMapNpcData npc = WorldMapNpcData.FromDictionary(payload);
            if (npc != null && !npc.IsEmpty)
            {
                target.Add(npc);
            }
        }
        return true;
    }

    private static GArray ReadArray(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return new GArray();
        Variant value = data[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static GDictionary ReadDictionary(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return new GDictionary();
        Variant value = data[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static string ReadString(GDictionary data, string key, string fallback = "")
    {
        if (data == null || !data.ContainsKey(key))
            return fallback ?? "";
        return VariantText(data[key], fallback);
    }

    private static int ReadInt(GDictionary data, string key, int fallback)
    {
        if (data == null || !data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static long ReadLong(GDictionary data, string key, long fallback)
    {
        if (data == null || !data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt64() : fallback;
    }

    private static Vector2I ReadVector2I(GDictionary data, string key, Vector2I fallback)
    {
        if (data == null || !data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }

    private static string VariantText(Variant value, string fallback = "")
    {
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback ?? "",
        };
    }
}
