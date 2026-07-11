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
            session.BindContent(snapshot, host.LegacyEnemyContent);
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
        Action<SyntheticContentSnapshotSeed> configure,
        ILegacyEnemyContentCatalog legacyEnemyContent = null
    )
    {
        SyntheticContentSnapshotSeed seed = SyntheticContentSnapshotFactory.CreateSeed(
            GetProcessSnapshot()
        );
        configure?.Invoke(seed);
        return CreateSynthetic(
            SyntheticContentSnapshotFactory.Create(seed),
            legacyEnemyContent
                ?? CreateLoadedLegacyEnemyContent()
        );
    }

    internal static GameSession CreateSynthetic(
        ContentSnapshot snapshot,
        ILegacyEnemyContentCatalog legacyEnemyContent
    )
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(legacyEnemyContent);
        var session = new GameSession();
        try
        {
            session.BindContent(snapshot, legacyEnemyContent);
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    internal static GameSession CreateSynthetic(ContentSnapshot snapshot) =>
        CreateSynthetic(
            snapshot,
            SyntheticContentSnapshotFactory.CreateEmptyLegacyEnemyContent()
        );

    internal static GameSession CreateSynthetic(
        HeadlessGameTestSession headlessSession,
        Action<SyntheticContentSnapshotSeed> configure,
        ILegacyEnemyContentCatalog legacyEnemyContent = null
    )
    {
        ArgumentNullException.ThrowIfNull(headlessSession);
        GameSession session = CreateSyntheticFromProcessSnapshot(
            configure,
            legacyEnemyContent
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

    internal static ILegacyEnemyContentCatalog CreateLoadedLegacyEnemyContent()
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            throw new InvalidOperationException(
                "A running SceneTree is required to copy process legacy enemy content."
            );
        }
        ILegacyEnemyContentCatalog processLegacy = tree.Root
            .GetNode<ApplicationLifetimeCoordinator>("ApplicationLifetimeCoordinator")
            .ContentHost.LegacyEnemyContent;
        ILegacyEnemyContentCatalog copied =
            SyntheticContentSnapshotFactory.CreateLegacyEnemyContent(
            new Dictionary<StringName, EnemyTemplateDef>(processLegacy.EnemyTemplates),
            new Dictionary<StringName, EnemyAiBrainDef>(processLegacy.EnemyBrains),
            new Dictionary<StringName, WildEncounterRosterDef>(
                processLegacy.EncounterRosters
            ),
            new Dictionary<StringName, BattleSimProfileDef>(processLegacy.SimulationProfiles)
        );
        RegisterBorrowedLegacyGraph(copied);
        return copied;
    }

    private static void RegisterBorrowedLegacyGraph(ILegacyEnemyContentCatalog catalog)
    {
        RegisterBorrowedResources(catalog.EnemyTemplates.Values, catalog, "enemy-template");
        RegisterBorrowedResources(catalog.EnemyBrains.Values, catalog, "enemy-brain");
        RegisterBorrowedResources(catalog.EncounterRosters.Values, catalog, "encounter-roster");
        RegisterBorrowedResources(catalog.SimulationProfiles.Values, catalog, "simulation-profile");
    }

    private static void RegisterBorrowedResources<T>(
        IEnumerable<T> roots,
        object owner,
        string label
    )
        where T : Resource
    {
        foreach (T root in roots)
        {
            if (root == null)
                continue;
            string source = root.ResourcePath ?? label;
            GodotTypedResourceGraphWalker.VisitValueGraph(root, wrapper =>
            {
                if (GodotWrapperOwnershipRegistry.IsBorrowedOrDerivedStaticContent(wrapper))
                    return;
                if (GodotWrapperOwnershipRegistry.IsOwnedTransient(wrapper))
                    return;
                GodotWrapperOwnershipRegistry.Register(
                    wrapper,
                    GodotWrapperOwnershipKind.BorrowedStaticContent,
                    owner,
                    $"synthetic-legacy:{label}:{source}"
                );
            });
        }
    }

}
