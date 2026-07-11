using System;
using System.Collections.Generic;
using System.Threading;
using Godot;

internal static class GameSessionTestFactory
{
    private static long _borrowerSerial;

    internal static GameSession CreateBorrowingProcessSnapshot()
    {
        return Engine.GetMainLoop() is SceneTree tree
            ? CreateBorrowingProcessSnapshot(tree)
            : throw new InvalidOperationException(
                "A running SceneTree is required to borrow process content."
            );
    }

    internal static GameSession CreateBorrowingProcessSnapshot(string nodeName)
    {
        GameSession session = CreateBorrowingProcessSnapshot();
        session.Name = nodeName ?? string.Empty;
        return session;
    }

    internal static GameSession CreateForCoordinatorAttachment(string nodeName = "GameSession")
    {
        return new GameSession { Name = nodeName ?? "GameSession" };
    }

    internal static GameSession CreateForCoordinatorAttachment(
        GameSessionPersistenceOptions persistenceOptions,
        string nodeName = "GameSession"
    )
    {
        ArgumentNullException.ThrowIfNull(persistenceOptions);
        return new GameSession(persistenceOptions) { Name = nodeName ?? "GameSession" };
    }

    internal static GameSession CreateBorrowingProcessSnapshot(SceneTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ApplicationLifetimeCoordinator coordinator = tree.Root.GetNode<ApplicationLifetimeCoordinator>(
            "ApplicationLifetimeCoordinator"
        );
        ProcessContentHost host = coordinator.ContentHost;
        ContentSnapshot snapshot = host.GetSnapshot();
        var session = new GameSession();
        string borrowerId =
            $"content-snapshot:test-session:{Interlocked.Increment(ref _borrowerSerial)}";
        host.RegisterSnapshotBorrower(borrowerId, session);
        try
        {
            session.BindContent(snapshot);
            session.BindContentBorrower(host, borrowerId);
            return session;
        }
        catch
        {
            host.UnregisterSnapshotBorrower(borrowerId);
            session.Dispose();
            throw;
        }
    }

    internal static ContentSnapshot GetProcessSnapshot()
    {
        return Engine.GetMainLoop() is SceneTree tree
            ? tree.Root
                .GetNode<ApplicationLifetimeCoordinator>("ApplicationLifetimeCoordinator")
                .ContentHost.GetSnapshot()
            : throw new InvalidOperationException(
                "A running SceneTree is required to read process content."
            );
    }

    internal static GameSession CreateSyntheticFromProcessSnapshot(
        Action<SyntheticContentSnapshotSeed> configure
    )
    {
        SyntheticContentSnapshotSeed seed = SyntheticContentSnapshotFactory.CreateSeed(
            GetProcessSnapshot()
        );
        configure?.Invoke(seed);
        return CreateSynthetic(SyntheticContentSnapshotFactory.Create(seed));
    }

    internal static GameSession CreateSynthetic(ContentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var session = new GameSession();
        try
        {
            session.BindContent(snapshot);
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    internal static GameSession CreateSynthetic(
        HeadlessGameTestSession headlessSession,
        Action<SyntheticContentSnapshotSeed> configure
    )
    {
        ArgumentNullException.ThrowIfNull(headlessSession);
        GameSession session = CreateSyntheticFromProcessSnapshot(
            configure
        );
        try
        {
            headlessSession.BindOwnedGameSessionForTests(session);
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

}
