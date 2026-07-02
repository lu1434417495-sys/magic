using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

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
    private sealed class SessionRollbackSnapshot
    {
        private readonly bool _battleSaveLockEnabled;
        private readonly bool _battleSaveDirty;
        private readonly bool _runtimeSaveDirty;
        private readonly StringNameList _runtimeSaveDirtyScopes;
        private readonly int _lastSaveError;
        private readonly StringName _lastSaveErrorReason;
        private readonly bool _postDecodeSavePending;
        private readonly StringNameList _postDecodeSaveReasons;

        internal SessionRollbackSnapshot(GameSession session)
        {
            _battleSaveLockEnabled = session?._battle_save_lock_enabled ?? false;
            _battleSaveDirty = session?._battle_save_dirty ?? false;
            _runtimeSaveDirty = session?._runtime_save_dirty ?? false;
            _runtimeSaveDirtyScopes = new StringNameList(session?._runtime_save_dirty_scopes);
            _lastSaveError = session?._last_save_error ?? (int)Error.Ok;
            _lastSaveErrorReason = session?._last_save_error_reason ?? "";
            _postDecodeSavePending = session?._post_decode_save_pending ?? false;
            _postDecodeSaveReasons = new StringNameList(session?._post_decode_save_reasons);
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
    private readonly WorldRuntimeData _worldData;
    private readonly Vector2I _playerCoord;
    private readonly SessionRollbackSnapshot _sessionSnapshot;
    private readonly bool _capturedPartyState;
    private readonly bool _capturedWorldData;

    private RuntimeTransactionRollbackState(
        PartyState partyState,
        WorldRuntimeData worldData,
        Vector2I playerCoord,
        GameSession session,
        bool capturedPartyState,
        bool capturedWorldData
    )
    {
        _partyState = partyState?.DuplicateState();
        _worldData = worldData ?? WorldRuntimeData.Empty();
        _playerCoord = playerCoord;
        _sessionSnapshot = session != null ? new SessionRollbackSnapshot(session) : null;
        _capturedPartyState = capturedPartyState;
        _capturedWorldData = capturedWorldData;
    }

    // scopes == null captures everything. Passing the transaction skips the
    // whole-world dictionary round-trip for party-only mutations, which is a
    // multi-hundred-ms cost on large maps (see world-move perf history).
    internal static RuntimeTransactionRollbackState Capture(
        GameRuntimeFacade runtime,
        RuntimeTransaction scopes = null
    )
    {
        if (runtime == null)
            return new RuntimeTransactionRollbackState(
                null,
                WorldRuntimeData.Empty(),
                Vector2I.Zero,
                null,
                capturedPartyState: false,
                capturedWorldData: false
            );
        bool captureParty = scopes == null || scopes.PersistPartyState;
        bool captureWorld = scopes == null || scopes.PersistWorldData;
        return new RuntimeTransactionRollbackState(
            captureParty ? runtime.GetPartyState() : null,
            captureWorld
                ? runtime._world_map_data_context?.ActiveRuntimeData?.DuplicateState()
                    ?? WorldRuntimeData.Empty()
                : WorldRuntimeData.Empty(),
            runtime.GetPlayerCoord(),
            runtime._game_session,
            captureParty,
            captureWorld
        );
    }

    internal void Restore(GameRuntimeFacade runtime, RuntimeTransaction transaction)
    {
        if (runtime == null || transaction == null)
            return;

        bool restoreParty = transaction.PersistPartyState;
        bool restoreWorld = transaction.PersistWorldData;
        if (restoreParty && !_capturedPartyState)
        {
            GameLog.Error(
                "runtime transaction rollback requested party restore but party state was not captured; party scope skipped.",
                "runtime_transaction.rollback_scope_missing",
                "game_runtime"
            );
            restoreParty = false;
        }
        if (restoreWorld && !_capturedWorldData)
        {
            GameLog.Error(
                "runtime transaction rollback requested world restore but world data was not captured; world scope skipped.",
                "runtime_transaction.rollback_scope_missing",
                "game_runtime"
            );
            restoreWorld = false;
        }

        GameSession session = runtime._game_session;
        if (session != null)
        {
            if (restoreParty)
                session._party_state = _partyState?.DuplicateState() ?? new PartyState();
            if (restoreWorld)
                session.ReplaceWorldDataPayloadForRuntimeRestore(
                    WorldMapDataProjection.Project(_worldData)
                );
            if (transaction.PersistPlayerCoord)
                session._player_coord = _playerCoord;
            _sessionSnapshot?.Restore(session);
        }

        if (restoreParty && _partyState != null)
        {
            PartyState restoredPartyState = session != null
                ? session.GetPartyState()
                : _partyState.DuplicateState();
            runtime.SetPartyState(restoredPartyState);
        }

        bool worldOrCoordRestored = false;
        if (restoreWorld)
        {
            GDictionary restoredWorldData = session != null
                ? session.GetWorldData()
                : WorldMapDataProjection.Project(_worldData);
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
