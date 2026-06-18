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

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}
