using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal sealed class RuntimeStateSource
{
    private readonly Func<PartyState> _partyStateProvider;
    private readonly Func<GDictionary> _worldDataProvider;
    private readonly Func<Vector2I> _playerCoordProvider;

    internal RuntimeStateSource(
        Func<PartyState> partyStateProvider,
        Func<GDictionary> worldDataProvider,
        Func<Vector2I> playerCoordProvider
    )
    {
        _partyStateProvider = partyStateProvider;
        _worldDataProvider = worldDataProvider;
        _playerCoordProvider = playerCoordProvider;
    }

    internal PartyState GetPartyStateForCommit() => _partyStateProvider?.Invoke();

    internal GDictionary GetWorldDataForCommit() =>
        _worldDataProvider?.Invoke() ?? new GDictionary();

    internal Vector2I GetPlayerCoordForCommit() =>
        _playerCoordProvider?.Invoke() ?? Vector2I.Zero;
}

internal sealed class RuntimeCommitResult
{
    internal bool Ok =>
        PartyError == (int)Error.Ok
        && WorldError == (int)Error.Ok
        && PlayerError == (int)Error.Ok
        && CommitError == (int)Error.Ok;

    internal int PartyError { get; init; } = (int)Error.Ok;
    internal int WorldError { get; init; } = (int)Error.Ok;
    internal int PlayerError { get; init; } = (int)Error.Ok;
    internal int CommitError { get; init; } = (int)Error.Ok;
    internal string Message { get; init; } = "";

    internal int FirstError()
    {
        if (PartyError != (int)Error.Ok)
            return PartyError;
        if (WorldError != (int)Error.Ok)
            return WorldError;
        if (PlayerError != (int)Error.Ok)
            return PlayerError;
        return CommitError;
    }
}

internal sealed class RuntimeTransactionRollbackState
{
    private sealed class WorldDataRollbackSnapshot
    {
        private const string WorldMapSeedKey = "map_seed";
        private const string WorldEquipmentInstanceSerialKey =
            "next_equipment_instance_serial";

        private readonly long _mapSeed;
        private readonly int _worldStep;
        private readonly int _equipmentInstanceSerial;
        private readonly string _activeSubmapId;
        private readonly List<WorldMapSubmapReturnStackEntry> _submapReturnStack;
        private readonly List<WorldMapSettlementRecordData> _settlements;
        private readonly List<WorldMapEventData> _worldEvents;
        private readonly List<EncounterAnchorData> _encounterAnchors;
        private readonly List<MountedSubmapRollbackSnapshot> _mountedSubmaps;
        private readonly bool _hasWorldNpcs;
        private readonly List<WorldMapNpcData> _worldNpcs;
        private readonly bool _hasPlayerStartCoord;
        private readonly Vector2I _playerStartCoord;
        private readonly bool _hasPlayerStartSettlementId;
        private readonly string _playerStartSettlementId;
        private readonly bool _hasPlayerStartSettlementName;
        private readonly string _playerStartSettlementName;
        private readonly bool _hasFogStates;
        private readonly FogStatesRollbackSnapshot _fogStates;

        private WorldDataRollbackSnapshot(
            long mapSeed,
            int worldStep,
            int equipmentInstanceSerial,
            string activeSubmapId,
            List<WorldMapSubmapReturnStackEntry> submapReturnStack,
            List<WorldMapSettlementRecordData> settlements,
            List<WorldMapEventData> worldEvents,
            List<EncounterAnchorData> encounterAnchors,
            List<MountedSubmapRollbackSnapshot> mountedSubmaps,
            bool hasWorldNpcs,
            List<WorldMapNpcData> worldNpcs,
            bool hasPlayerStartCoord,
            Vector2I playerStartCoord,
            bool hasPlayerStartSettlementId,
            string playerStartSettlementId,
            bool hasPlayerStartSettlementName,
            string playerStartSettlementName,
            bool hasFogStates,
            FogStatesRollbackSnapshot fogStates
        )
        {
            _mapSeed = mapSeed;
            _worldStep = worldStep;
            _equipmentInstanceSerial = equipmentInstanceSerial;
            _activeSubmapId = activeSubmapId ?? "";
            _submapReturnStack = submapReturnStack ?? new List<WorldMapSubmapReturnStackEntry>();
            _settlements = settlements ?? new List<WorldMapSettlementRecordData>();
            _worldEvents = worldEvents ?? new List<WorldMapEventData>();
            _encounterAnchors = encounterAnchors ?? new List<EncounterAnchorData>();
            _mountedSubmaps = mountedSubmaps ?? new List<MountedSubmapRollbackSnapshot>();
            _hasWorldNpcs = hasWorldNpcs;
            _worldNpcs = worldNpcs ?? new List<WorldMapNpcData>();
            _hasPlayerStartCoord = hasPlayerStartCoord;
            _playerStartCoord = playerStartCoord;
            _hasPlayerStartSettlementId = hasPlayerStartSettlementId;
            _playerStartSettlementId = playerStartSettlementId ?? "";
            _hasPlayerStartSettlementName = hasPlayerStartSettlementName;
            _playerStartSettlementName = playerStartSettlementName ?? "";
            _hasFogStates = hasFogStates;
            _fogStates = fogStates ?? FogStatesRollbackSnapshot.Capture(null);
        }

        internal static WorldDataRollbackSnapshot Capture(GDictionary worldData)
        {
            worldData ??= new GDictionary();
            bool hasWorldNpcs = worldData.ContainsKey("world_npcs");
            bool hasPlayerStartCoord = worldData.ContainsKey("player_start_coord");
            bool hasPlayerStartSettlementId = worldData.ContainsKey(
                "player_start_settlement_id"
            );
            bool hasPlayerStartSettlementName = worldData.ContainsKey(
                "player_start_settlement_name"
            );
            bool hasFogStates = worldData.ContainsKey("fog_states");
            return new WorldDataRollbackSnapshot(
                ReadLong(worldData, WorldMapSeedKey, 1L),
                ReadInt(worldData, "world_step", 0),
                ReadInt(worldData, WorldEquipmentInstanceSerialKey, 1),
                ReadString(worldData, "active_submap_id"),
                CaptureSubmapReturnStack(ReadArray(worldData, "submap_return_stack")),
                CaptureSettlements(ReadArray(worldData, "settlements")),
                CaptureWorldEvents(ReadArray(worldData, "world_events")),
                CaptureEncounterAnchors(ReadArray(worldData, "encounter_anchors")),
                CaptureMountedSubmaps(ReadDictionary(worldData, "mounted_submaps")),
                hasWorldNpcs,
                CaptureWorldNpcs(ReadArray(worldData, "world_npcs")),
                hasPlayerStartCoord,
                ReadVector2I(worldData, "player_start_coord", Vector2I.Zero),
                hasPlayerStartSettlementId,
                ReadString(worldData, "player_start_settlement_id"),
                hasPlayerStartSettlementName,
                ReadString(worldData, "player_start_settlement_name"),
                hasFogStates,
                FogStatesRollbackSnapshot.Capture(ReadDictionary(worldData, "fog_states"))
            );
        }

        internal GDictionary ProjectWorldData()
        {
            var result = new GDictionary
            {
                [WorldMapSeedKey] = _mapSeed,
                ["world_step"] = _worldStep,
                [WorldEquipmentInstanceSerialKey] = _equipmentInstanceSerial,
                ["active_submap_id"] = _activeSubmapId,
                ["submap_return_stack"] = ProjectSubmapReturnStack(),
                ["settlements"] = ProjectSettlements(),
                ["world_events"] = ProjectWorldEvents(),
                ["encounter_anchors"] = ProjectEncounterAnchors(),
                ["mounted_submaps"] = ProjectMountedSubmaps(),
            };
            if (_hasWorldNpcs)
                result["world_npcs"] = ProjectWorldNpcs();
            if (_hasPlayerStartCoord)
                result["player_start_coord"] = _playerStartCoord;
            if (_hasPlayerStartSettlementId)
                result["player_start_settlement_id"] = _playerStartSettlementId;
            if (_hasPlayerStartSettlementName)
                result["player_start_settlement_name"] = _playerStartSettlementName;
            if (_hasFogStates)
                result["fog_states"] = _fogStates.Project();
            return result;
        }

        private GArray ProjectSubmapReturnStack()
        {
            var result = new GArray();
            foreach (WorldMapSubmapReturnStackEntry entry in _submapReturnStack)
                result.Add(WorldMapDataProjection.Project(entry));
            return result;
        }

        private GArray ProjectSettlements()
        {
            var result = new GArray();
            foreach (WorldMapSettlementRecordData settlement in _settlements)
                result.Add(WorldMapDataProjection.Project(settlement));
            return result;
        }

        private GArray ProjectWorldEvents()
        {
            var result = new GArray();
            foreach (WorldMapEventData worldEvent in _worldEvents)
                result.Add(WorldMapDataProjection.Project(worldEvent));
            return result;
        }

        private GArray ProjectEncounterAnchors()
        {
            var result = new GArray();
            foreach (EncounterAnchorData encounterAnchor in _encounterAnchors)
            {
                EncounterAnchorData copy = DuplicateEncounterAnchor(encounterAnchor);
                if (copy != null)
                    result.Add(copy);
            }
            return result;
        }

        private GDictionary ProjectMountedSubmaps()
        {
            var result = new GDictionary();
            foreach (MountedSubmapRollbackSnapshot mountedSubmap in _mountedSubmaps)
                if (!string.IsNullOrEmpty(mountedSubmap.SubmapId))
                    result[mountedSubmap.SubmapId] = mountedSubmap.Project();
            return result;
        }

        private GArray ProjectWorldNpcs()
        {
            var result = new GArray();
            foreach (WorldMapNpcData npc in _worldNpcs)
                result.Add(WorldMapDataProjection.Project(npc));
            return result;
        }
    }

    private sealed class MountedSubmapRollbackSnapshot
    {
        internal string SubmapId { get; }
        private readonly string _displayName;
        private readonly string _generationConfigPath;
        private readonly string _returnHintText;
        private readonly bool _isGenerated;
        private readonly Vector2I _playerCoord;
        private readonly bool _hasWorldData;
        private readonly WorldDataRollbackSnapshot _worldData;

        private MountedSubmapRollbackSnapshot(
            string submapId,
            string displayName,
            string generationConfigPath,
            string returnHintText,
            bool isGenerated,
            Vector2I playerCoord,
            bool hasWorldData,
            WorldDataRollbackSnapshot worldData
        )
        {
            SubmapId = submapId ?? "";
            _displayName = displayName ?? "";
            _generationConfigPath = generationConfigPath ?? "";
            _returnHintText = returnHintText ?? "";
            _isGenerated = isGenerated;
            _playerCoord = playerCoord;
            _hasWorldData = hasWorldData;
            _worldData = worldData ?? WorldDataRollbackSnapshot.Capture(null);
        }

        internal static MountedSubmapRollbackSnapshot Capture(
            string submapId,
            GDictionary data
        )
        {
            data ??= new GDictionary();
            GDictionary worldData = ReadDictionary(data, "world_data");
            bool hasWorldData = worldData.Count > 0;
            return new MountedSubmapRollbackSnapshot(
                ReadString(data, "submap_id", submapId),
                ReadString(data, "display_name"),
                ReadString(data, "generation_config_path"),
                ReadString(data, "return_hint_text"),
                ReadBool(data, "is_generated"),
                ReadVector2I(data, "player_coord", Vector2I.Zero),
                hasWorldData,
                hasWorldData ? WorldDataRollbackSnapshot.Capture(worldData) : null
            );
        }

        internal GDictionary Project()
        {
            return new GDictionary
            {
                ["submap_id"] = SubmapId,
                ["display_name"] = _displayName,
                ["generation_config_path"] = _generationConfigPath,
                ["return_hint_text"] = _returnHintText,
                ["is_generated"] = _isGenerated,
                ["player_coord"] = _playerCoord,
                ["world_data"] = _hasWorldData
                    ? _worldData.ProjectWorldData()
                    : new GDictionary(),
            };
        }
    }

    private sealed class FogStatesRollbackSnapshot
    {
        private readonly bool _hasStructuredState;
        private readonly int _version;
        private readonly List<FogFactionRollbackSnapshot> _factions;

        private FogStatesRollbackSnapshot(
            bool hasStructuredState,
            int version,
            List<FogFactionRollbackSnapshot> factions
        )
        {
            _hasStructuredState = hasStructuredState;
            _version = version;
            _factions = factions ?? new List<FogFactionRollbackSnapshot>();
        }

        internal static FogStatesRollbackSnapshot Capture(GDictionary fogStates)
        {
            if (fogStates == null || fogStates.Count == 0)
                return new FogStatesRollbackSnapshot(
                    false,
                    WorldMapFogSystem.PersistentStateVersion,
                    new List<FogFactionRollbackSnapshot>()
                );

            return new FogStatesRollbackSnapshot(
                true,
                ReadInt(fogStates, "version", WorldMapFogSystem.PersistentStateVersion),
                CaptureFogFactions(ReadDictionary(fogStates, "factions"))
            );
        }

        internal GDictionary Project()
        {
            if (!_hasStructuredState)
                return new GDictionary();
            var factions = new GDictionary();
            foreach (FogFactionRollbackSnapshot faction in _factions)
                if (!string.IsNullOrEmpty(faction.FactionId))
                    factions[faction.FactionId] = faction.Project();
            return new GDictionary { ["version"] = _version, ["factions"] = factions };
        }
    }

    private sealed class FogFactionRollbackSnapshot
    {
        internal string FactionId { get; }
        private readonly List<string> _exploredCoordKeys;
        private readonly List<string> _revealedCoordKeys;

        private FogFactionRollbackSnapshot(
            string factionId,
            List<string> exploredCoordKeys,
            List<string> revealedCoordKeys
        )
        {
            FactionId = factionId ?? "";
            _exploredCoordKeys = exploredCoordKeys ?? new List<string>();
            _revealedCoordKeys = revealedCoordKeys ?? new List<string>();
        }

        internal static FogFactionRollbackSnapshot Capture(
            string factionId,
            GDictionary data
        )
        {
            return new FogFactionRollbackSnapshot(
                factionId,
                CaptureCoordKeys(ReadArray(data, "explored")),
                CaptureCoordKeys(ReadArray(data, "revealed"))
            );
        }

        internal GDictionary Project()
        {
            return new GDictionary
            {
                ["explored"] = ProjectCoordKeys(_exploredCoordKeys),
                ["revealed"] = ProjectCoordKeys(_revealedCoordKeys),
            };
        }
    }

    private sealed class SessionRollbackSnapshot
    {
        private readonly bool _battleSaveLockEnabled;
        private readonly bool _battleSaveDirty;
        private readonly bool _runtimeSaveDirty;
        private readonly GStringNameArray _runtimeSaveDirtyScopes;
        private readonly int _lastSaveError;
        private readonly StringName _lastSaveErrorReason;
        private readonly bool _postDecodeSavePending;
        private readonly GStringNameArray _postDecodeSaveReasons;

        internal SessionRollbackSnapshot(GameSession session)
        {
            _battleSaveLockEnabled = session?._battle_save_lock_enabled ?? false;
            _battleSaveDirty = session?._battle_save_dirty ?? false;
            _runtimeSaveDirty = session?._runtime_save_dirty ?? false;
            _runtimeSaveDirtyScopes = session?._runtime_save_dirty_scopes?.Duplicate()
                ?? new GStringNameArray();
            _lastSaveError = session?._last_save_error ?? (int)Error.Ok;
            _lastSaveErrorReason = session?._last_save_error_reason ?? "";
            _postDecodeSavePending = session?._post_decode_save_pending ?? false;
            _postDecodeSaveReasons = session?._post_decode_save_reasons?.Duplicate()
                ?? new GStringNameArray();
        }

        internal void Restore(GameSession session)
        {
            if (session == null)
                return;

            session._battle_save_lock_enabled = _battleSaveLockEnabled;
            session._battle_save_dirty = _battleSaveDirty;
            session._runtime_save_dirty = _runtimeSaveDirty;
            session._runtime_save_dirty_scopes = _runtimeSaveDirtyScopes.Duplicate();
            session._last_save_error = _lastSaveError;
            session._last_save_error_reason = _lastSaveErrorReason;
            session._post_decode_save_pending = _postDecodeSavePending;
            session._post_decode_save_reasons = _postDecodeSaveReasons.Duplicate();
        }
    }

    private readonly PartyState _partyState;
    private readonly WorldDataRollbackSnapshot _worldData;
    private readonly Vector2I _playerCoord;
    private readonly SessionRollbackSnapshot _sessionSnapshot;

    private RuntimeTransactionRollbackState(
        PartyState partyState,
        WorldDataRollbackSnapshot worldData,
        Vector2I playerCoord,
        GameSession session
    )
    {
        _partyState = partyState?.DuplicateState();
        _worldData = worldData ?? WorldDataRollbackSnapshot.Capture(null);
        _playerCoord = playerCoord;
        _sessionSnapshot = session != null ? new SessionRollbackSnapshot(session) : null;
    }

    internal static RuntimeTransactionRollbackState Capture(GameRuntimeFacade runtime)
    {
        if (runtime == null)
            return new RuntimeTransactionRollbackState(
                null,
                WorldDataRollbackSnapshot.Capture(null),
                Vector2I.Zero,
                null
            );
        return new RuntimeTransactionRollbackState(
            runtime.GetPartyState(),
            WorldDataRollbackSnapshot.Capture(runtime.GetWorldData()),
            runtime.GetPlayerCoord(),
            runtime._game_session
        );
    }

    internal void Restore(GameRuntimeFacade runtime, RuntimeTransaction transaction)
    {
        if (runtime == null || transaction == null)
            return;

        GameSession session = runtime._game_session;
        if (session != null)
        {
            if (transaction.PersistPartyState)
                session._party_state = _partyState?.DuplicateState() ?? new PartyState();
            if (transaction.PersistWorldData)
                session._world_data = _worldData.ProjectWorldData();
            if (transaction.PersistPlayerCoord)
                session._player_coord = _playerCoord;
            _sessionSnapshot?.Restore(session);
        }

        if (transaction.PersistPartyState && _partyState != null)
        {
            PartyState restoredPartyState = session != null
                ? session.GetPartyState()
                : _partyState.DuplicateState();
            runtime.SetPartyState(restoredPartyState);
        }

        bool worldOrCoordRestored = false;
        if (transaction.PersistWorldData)
        {
            GDictionary restoredWorldData = session != null
                ? session.GetWorldData()
                : _worldData.ProjectWorldData();
            runtime._world_map_data_context.BindRootWorldData(restoredWorldData);
            runtime._world_map_data_context.active_world_data = restoredWorldData;
            worldOrCoordRestored = true;
        }

        if (transaction.PersistPlayerCoord)
        {
            Vector2I restoredPlayerCoord = session != null ? session.GetPlayerCoord() : _playerCoord;
            runtime.SetPlayerCoord(restoredPlayerCoord);
            worldOrCoordRestored = true;
        }

        if (worldOrCoordRestored)
        {
            runtime._world_map_data_context.SyncActiveWorldContext(
                runtime.GetGenerationConfig(),
                runtime.GetGridSystem(),
                runtime.GetPlayerCoord(),
                runtime.GetSelectedCoord()
            );
            runtime.RefreshWorldVisibility();
        }
    }

    private static List<WorldMapSubmapReturnStackEntry> CaptureSubmapReturnStack(
        GArray values
    )
    {
        var result = new List<WorldMapSubmapReturnStackEntry>();
        foreach (GDictionary entry in Dictionaries(values))
            result.Add(WorldMapSubmapReturnStackEntry.FromDictionary(entry));
        return result;
    }

    private static List<WorldMapSettlementRecordData> CaptureSettlements(GArray values)
    {
        var result = new List<WorldMapSettlementRecordData>();
        foreach (GDictionary entry in Dictionaries(values))
        {
            WorldMapSettlementRecordData settlement =
                WorldMapSettlementRecordData.FromDictionary(entry);
            if (settlement != null)
                result.Add(settlement);
        }
        return result;
    }

    private static List<WorldMapEventData> CaptureWorldEvents(GArray values)
    {
        var result = new List<WorldMapEventData>();
        foreach (GDictionary entry in Dictionaries(values))
        {
            WorldMapEventData worldEvent = WorldMapEventData.FromDictionary(entry);
            if (worldEvent != null)
                result.Add(worldEvent);
        }
        return result;
    }

    private static List<EncounterAnchorData> CaptureEncounterAnchors(GArray values)
    {
        var result = new List<EncounterAnchorData>();
        if (values == null)
            return result;
        foreach (Variant value in values)
        {
            EncounterAnchorData encounterAnchor = CaptureEncounterAnchor(value);
            if (encounterAnchor != null)
                result.Add(encounterAnchor);
        }
        return result;
    }

    private static List<MountedSubmapRollbackSnapshot> CaptureMountedSubmaps(
        GDictionary values
    )
    {
        var result = new List<MountedSubmapRollbackSnapshot>();
        if (values == null)
            return result;
        foreach (Variant key in values.Keys)
        {
            Variant value = values[key];
            if (value.VariantType != Variant.Type.Dictionary)
                continue;
            result.Add(
                MountedSubmapRollbackSnapshot.Capture(
                    VariantText(key),
                    value.AsGodotDictionary()
                )
            );
        }
        return result;
    }

    private static List<WorldMapNpcData> CaptureWorldNpcs(GArray values)
    {
        var result = new List<WorldMapNpcData>();
        foreach (GDictionary entry in Dictionaries(values))
        {
            WorldMapNpcData npc = WorldMapNpcData.FromDictionary(entry);
            if (npc != null && !npc.IsEmpty)
                result.Add(npc);
        }
        return result;
    }

    private static List<FogFactionRollbackSnapshot> CaptureFogFactions(
        GDictionary values
    )
    {
        var result = new List<FogFactionRollbackSnapshot>();
        if (values == null)
            return result;
        foreach (Variant key in values.Keys)
        {
            string factionId = VariantText(key).Trim();
            if (string.IsNullOrEmpty(factionId))
                continue;
            Variant value = values[key];
            if (value.VariantType != Variant.Type.Dictionary)
                continue;
            result.Add(
                FogFactionRollbackSnapshot.Capture(factionId, value.AsGodotDictionary())
            );
        }
        return result;
    }

    private static List<string> CaptureCoordKeys(GArray values)
    {
        var result = new List<string>();
        if (values == null)
            return result;
        foreach (Variant value in values)
        {
            if (TryReadCoordKey(value, out string coordKey))
                result.Add(coordKey);
        }
        return result;
    }

    private static EncounterAnchorData CaptureEncounterAnchor(Variant value)
    {
        if (value.VariantType == Variant.Type.Object)
            return DuplicateEncounterAnchor(value.AsGodotObject() as EncounterAnchorData);
        if (value.VariantType == Variant.Type.Dictionary)
            return EncounterAnchorData.FromDictionary(value.AsGodotDictionary());
        return null;
    }

    private static EncounterAnchorData DuplicateEncounterAnchor(EncounterAnchorData value)
    {
        return value == null
            ? null
            : EncounterAnchorData.FromDictionary(WorldMapDataProjection.Project(value));
    }

    private static GArray ProjectCoordKeys(IEnumerable<string> coordKeys)
    {
        var result = new GArray();
        if (coordKeys == null)
            return result;
        foreach (string coordKey in coordKeys)
        {
            if (TryParseCoordKey(coordKey, out Vector2I coord))
                result.Add(new GDictionary { ["x"] = coord.X, ["y"] = coord.Y });
        }
        return result;
    }

    private static bool TryReadCoordKey(Variant value, out string coordKey)
    {
        coordKey = "";
        if (value.VariantType != Variant.Type.Dictionary)
            return false;
        GDictionary payload = value.AsGodotDictionary();
        if (
            !payload.ContainsKey("x")
            || !payload.ContainsKey("y")
            || payload["x"].VariantType != Variant.Type.Int
            || payload["y"].VariantType != Variant.Type.Int
        )
            return false;
        coordKey = CoordKey(new Vector2I(payload["x"].AsInt32(), payload["y"].AsInt32()));
        return true;
    }

    private static string CoordKey(Vector2I coord) => $"{coord.X},{coord.Y}";

    private static bool TryParseCoordKey(string coordKey, out Vector2I coord)
    {
        coord = Vector2I.Zero;
        if (string.IsNullOrEmpty(coordKey))
            return false;
        string[] parts = coordKey.Split(',', 2);
        if (
            parts.Length != 2
            || !int.TryParse(parts[0], out int x)
            || !int.TryParse(parts[1], out int y)
        )
            return false;
        coord = new Vector2I(x, y);
        return true;
    }

    private static IEnumerable<GDictionary> Dictionaries(GArray values)
    {
        if (values == null)
            yield break;
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Dictionary)
                yield return value.AsGodotDictionary();
        }
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

    private static string ReadString(
        GDictionary data,
        string key,
        string fallback = ""
    )
    {
        if (data == null || !data.ContainsKey(key))
            return fallback ?? "";
        return VariantText(data[key], fallback);
    }

    private static int ReadInt(GDictionary data, string key, int fallback = 0)
    {
        if (data == null || !data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static long ReadLong(GDictionary data, string key, long fallback = 0L)
    {
        if (data == null || !data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt64() : fallback;
    }

    private static bool ReadBool(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return false;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Bool && value.AsBool();
    }

    private static Vector2I ReadVector2I(
        GDictionary data,
        string key,
        Vector2I fallback
    )
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

internal sealed class RuntimeTransaction
{
    internal bool PersistPartyState { get; private set; }
    internal bool PersistWorldData { get; private set; }
    internal bool PersistPlayerCoord { get; private set; }

    internal bool HasChanges =>
        PersistPartyState || PersistWorldData || PersistPlayerCoord;

    internal RuntimeTransaction MarkPartyChanged()
    {
        PersistPartyState = true;
        return this;
    }

    internal RuntimeTransaction MarkWorldChanged()
    {
        PersistWorldData = true;
        return this;
    }

    internal RuntimeTransaction MarkPlayerCoordChanged()
    {
        PersistPlayerCoord = true;
        return this;
    }

    internal RuntimeCommitResult Commit(
        GameSession session,
        RuntimeStateSource source,
        StringName reason
    )
    {
        if (!HasChanges)
            return new RuntimeCommitResult();
        if (session == null || source == null)
        {
            int unavailable = (int)Error.Unavailable;
            return new RuntimeCommitResult
            {
                PartyError = PersistPartyState ? unavailable : (int)Error.Ok,
                WorldError = PersistWorldData ? unavailable : (int)Error.Ok,
                PlayerError = PersistPlayerCoord ? unavailable : (int)Error.Ok,
                CommitError = unavailable,
                Message = "runtime transaction requires an active session and state source.",
            };
        }

        int partyError = (int)Error.Ok;
        int worldError = (int)Error.Ok;
        int playerError = (int)Error.Ok;

        if (PersistPartyState)
            partyError = session.SetPartyState(source.GetPartyStateForCommit());
        if (PersistWorldData)
            worldError = session.SetWorldData(source.GetWorldDataForCommit());
        if (PersistPlayerCoord)
            playerError = session.SetPlayerCoord(source.GetPlayerCoordForCommit());

        bool staged =
            partyError == (int)Error.Ok
            && worldError == (int)Error.Ok
            && playerError == (int)Error.Ok;
        int commitError = staged
            ? session.CommitRuntimeState(IsEmpty(reason) ? "runtime_transaction" : reason)
            : (int)Error.Ok;

        return new RuntimeCommitResult
        {
            PartyError = partyError,
            WorldError = worldError,
            PlayerError = playerError,
            CommitError = commitError,
            Message = staged && commitError != (int)Error.Ok
                ? "runtime transaction commit failed."
                : "",
        };
    }

    internal void Rollback(
        GameRuntimeFacade runtime,
        RuntimeTransactionRollbackState rollbackState
    )
    {
        rollbackState?.Restore(runtime, this);
    }

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}
