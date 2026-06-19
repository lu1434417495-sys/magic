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
    private enum PayloadValueKind
    {
        Nil,
        Bool,
        Int,
        Float,
        String,
        StringName,
        Vector2I,
        Array,
        Map,
    }

    private sealed class PayloadEntrySnapshot
    {
        private readonly string _key;
        private readonly bool _isStringNameKey;
        private readonly PayloadValueSnapshot _value;

        internal PayloadEntrySnapshot(
            string key,
            bool isStringNameKey,
            PayloadValueSnapshot value
        )
        {
            _key = key ?? "";
            _isStringNameKey = isStringNameKey;
            _value = value ?? PayloadValueSnapshot.Nil();
        }

        internal void ProjectInto(GDictionary target)
        {
            if (target == null)
                return;
            if (_isStringNameKey)
                target[new StringName(_key)] = _value.Project();
            else
                target[_key] = _value.Project();
        }
    }

    private sealed class PayloadValueSnapshot
    {
        private readonly PayloadValueKind _kind;
        private readonly bool _boolValue;
        private readonly long _intValue;
        private readonly double _floatValue;
        private readonly string _stringValue;
        private readonly StringName _stringNameValue;
        private readonly Vector2I _vector2IValue;
        private readonly List<PayloadValueSnapshot> _arrayValues;
        private readonly List<PayloadEntrySnapshot> _mapEntries;

        private PayloadValueSnapshot(
            PayloadValueKind kind,
            bool boolValue = false,
            long intValue = 0L,
            double floatValue = 0.0,
            string stringValue = "",
            StringName stringNameValue = default,
            Vector2I vector2IValue = default,
            List<PayloadValueSnapshot> arrayValues = null,
            List<PayloadEntrySnapshot> mapEntries = null
        )
        {
            _kind = kind;
            _boolValue = boolValue;
            _intValue = intValue;
            _floatValue = floatValue;
            _stringValue = stringValue ?? "";
            _stringNameValue = stringNameValue;
            _vector2IValue = vector2IValue;
            _arrayValues = arrayValues ?? new List<PayloadValueSnapshot>();
            _mapEntries = mapEntries ?? new List<PayloadEntrySnapshot>();
        }

        internal static PayloadValueSnapshot Nil() => new(PayloadValueKind.Nil);

        internal static PayloadValueSnapshot Capture(Variant value)
        {
            return value.VariantType switch
            {
                Variant.Type.Nil => Nil(),
                Variant.Type.Bool => new PayloadValueSnapshot(
                    PayloadValueKind.Bool,
                    boolValue: value.AsBool()
                ),
                Variant.Type.Int => new PayloadValueSnapshot(
                    PayloadValueKind.Int,
                    intValue: value.AsInt64()
                ),
                Variant.Type.Float => new PayloadValueSnapshot(
                    PayloadValueKind.Float,
                    floatValue: value.AsDouble()
                ),
                Variant.Type.String => new PayloadValueSnapshot(
                    PayloadValueKind.String,
                    stringValue: value.AsString()
                ),
                Variant.Type.StringName => new PayloadValueSnapshot(
                    PayloadValueKind.StringName,
                    stringNameValue: value.AsStringName()
                ),
                Variant.Type.Vector2I => new PayloadValueSnapshot(
                    PayloadValueKind.Vector2I,
                    vector2IValue: value.AsVector2I()
                ),
                Variant.Type.Array => CaptureArray(value.AsGodotArray()),
                Variant.Type.Dictionary => CaptureMap(value.AsGodotDictionary()),
                Variant.Type.Object => CaptureObject(value.AsGodotObject()),
                _ => Nil(),
            };
        }

        internal Variant Project()
        {
            return _kind switch
            {
                PayloadValueKind.Nil => new Variant(),
                PayloadValueKind.Bool => Variant.From(_boolValue),
                PayloadValueKind.Int => Variant.From(_intValue),
                PayloadValueKind.Float => Variant.From(_floatValue),
                PayloadValueKind.String => Variant.From(_stringValue),
                PayloadValueKind.StringName => Variant.From(_stringNameValue),
                PayloadValueKind.Vector2I => Variant.From(_vector2IValue),
                PayloadValueKind.Array => Variant.From(ProjectArray()),
                PayloadValueKind.Map => Variant.From(ProjectMap()),
                _ => new Variant(),
            };
        }

        private static PayloadValueSnapshot CaptureArray(GArray values)
        {
            var snapshots = new List<PayloadValueSnapshot>();
            if (values != null)
            {
                foreach (Variant value in values)
                    snapshots.Add(Capture(value));
            }
            return new PayloadValueSnapshot(
                PayloadValueKind.Array,
                arrayValues: snapshots
            );
        }

        private static PayloadValueSnapshot CaptureMap(GDictionary values) =>
            new(PayloadValueKind.Map, mapEntries: CaptureEntries(values));

        private static PayloadValueSnapshot CaptureObject(GodotObject value)
        {
            if (value is EncounterAnchorData encounterAnchor)
                return CaptureMap(WorldMapDataProjection.Project(encounterAnchor));
            return Nil();
        }

        private GArray ProjectArray()
        {
            var result = new GArray();
            foreach (PayloadValueSnapshot value in _arrayValues)
                result.Add(value.Project());
            return result;
        }

        private GDictionary ProjectMap()
        {
            var result = new GDictionary();
            foreach (PayloadEntrySnapshot entry in _mapEntries)
                entry.ProjectInto(result);
            return result;
        }
    }

    private sealed class WorldDataRollbackSnapshot
    {
        private readonly List<PayloadEntrySnapshot> _entries;

        private WorldDataRollbackSnapshot(List<PayloadEntrySnapshot> entries)
        {
            _entries = entries ?? new List<PayloadEntrySnapshot>();
        }

        internal static WorldDataRollbackSnapshot Capture(GDictionary worldData) =>
            new(CaptureEntries(worldData));

        internal GDictionary ProjectWorldData()
        {
            var result = new GDictionary();
            foreach (PayloadEntrySnapshot entry in _entries)
                entry.ProjectInto(result);
            return result;
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

    private static List<PayloadEntrySnapshot> CaptureEntries(GDictionary values)
    {
        var entries = new List<PayloadEntrySnapshot>();
        if (values == null)
            return entries;
        foreach (object key in values.Keys)
        {
            entries.Add(
                new PayloadEntrySnapshot(
                    key?.ToString() ?? "",
                    key is StringName,
                    PayloadValueSnapshot.Capture(values[BuildKeyVariant(key)])
                )
            );
        }
        return entries;
    }

    private static Variant BuildKeyVariant(object key) =>
        key switch
        {
            StringName stringName => Variant.From(stringName),
            string text => Variant.From(text),
            int intValue => Variant.From(intValue),
            long longValue => Variant.From(longValue),
            _ => Variant.From(key?.ToString() ?? ""),
        };
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
