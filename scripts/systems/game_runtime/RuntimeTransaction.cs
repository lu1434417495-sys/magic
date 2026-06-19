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
    private readonly PartyState _partyState;
    private readonly GDictionary _worldData;
    private readonly Vector2I _playerCoord;
    private readonly GDictionary _sessionRuntimeState;

    internal RuntimeTransactionRollbackState(
        PartyState partyState,
        GDictionary worldData,
        Vector2I playerCoord,
        GDictionary sessionRuntimeState
    )
    {
        _partyState = partyState?.DuplicateState();
        _worldData = worldData?.Duplicate(true) ?? new GDictionary();
        _playerCoord = playerCoord;
        _sessionRuntimeState = sessionRuntimeState?.Duplicate(true) ?? new GDictionary();
        if (_sessionRuntimeState.Count != 0)
        {
            _sessionRuntimeState["world_data"] = _worldData.Duplicate(true);
            _sessionRuntimeState["player_coord"] = _playerCoord;
            _sessionRuntimeState["party_state"] = _partyState?.DuplicateState() ?? new PartyState();
        }
    }

    internal void Restore(GameRuntimeFacade runtime, RuntimeTransaction transaction)
    {
        if (runtime == null || transaction == null)
            return;

        GameSession session = runtime._game_session;
        bool restoredSessionSnapshot = false;
        if (session != null && _sessionRuntimeState.Count != 0)
        {
            GDictionary restoredRuntimeState = (GDictionary)_sessionRuntimeState.Duplicate(true);
            restoredRuntimeState["world_data"] = _worldData.Duplicate(true);
            restoredRuntimeState["player_coord"] = _playerCoord;
            restoredRuntimeState["party_state"] = _partyState?.DuplicateState() ?? new PartyState();
            session.RestoreRuntimeState(restoredRuntimeState);
            restoredSessionSnapshot = true;
        }

        if (transaction.PersistPartyState && _partyState != null)
        {
            PartyState restoredPartyState = restoredSessionSnapshot
                ? session.GetPartyState()
                : _partyState.DuplicateState();
            runtime.SetPartyState(restoredPartyState);
        }

        bool worldOrCoordRestored = false;
        if (transaction.PersistWorldData)
        {
            GDictionary restoredWorldData = restoredSessionSnapshot
                ? session.GetWorldData()
                : _worldData.Duplicate(true);
            runtime._world_map_data_context.BindRootWorldData(restoredWorldData);
            runtime._world_map_data_context.active_world_data = restoredWorldData;
            worldOrCoordRestored = true;
        }

        if (transaction.PersistPlayerCoord)
        {
            Vector2I restoredPlayerCoord = restoredSessionSnapshot
                ? session.GetPlayerCoord()
                : _playerCoord;
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
