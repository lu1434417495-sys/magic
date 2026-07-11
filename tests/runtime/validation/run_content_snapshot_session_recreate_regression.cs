using System;
using System.Threading.Tasks;
using Godot;

public partial class run_content_snapshot_session_recreate_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => CallDeferred(nameof(Run));

    private async void Run()
    {
        await RunScenario();
        await LifecycleMeasurementBarrier.RunAsync(this);
        RequestTestExit(_test.Finish("Content snapshot session recreate regression"));
    }

    private async Task RunScenario()
    {
        ApplicationLifetimeCoordinator coordinator = Root.GetNode<ApplicationLifetimeCoordinator>(
            "ApplicationLifetimeCoordinator"
        );
        ProcessContentHost host = coordinator.ContentHost;
        ContentSnapshot processSnapshot = host.GetSnapshot();
        long epoch = processSnapshot.Epoch;
        int rootCount = host.CanonicalRootCount;

        GameSession initial = Root.GetNode<GameSession>("GameSession");
        await coordinator.CloseSessionAsync(initial);
        initial = null;
        await LifecycleMeasurementBarrier.RunAsync(this);
        AssertHostStable(host, processSnapshot, epoch, rootCount, "after initial close");

        GameSession sessionA = GameSessionTestFactory.CreateForCoordinatorAttachment();
        Root.AddChild(sessionA);
        _test.Eq(sessionA.GetContentSnapshotEpoch(), epoch, "session A should borrow process epoch");
        _test.True(
            ReferenceEquals(sessionA.GetContentCatalogTyped().GetSkillDefinitionsTyped(), processSnapshot.Skills),
            "session A catalog should read the published skill snapshot directly"
        );
        await coordinator.CloseSessionAsync(sessionA);
        sessionA = null;
        await LifecycleMeasurementBarrier.RunAsync(this);
        AssertHostStable(host, processSnapshot, epoch, rootCount, "after session A close");

        GameSession sessionB = GameSessionTestFactory.CreateForCoordinatorAttachment();
        Root.AddChild(sessionB);
        _test.Eq(sessionB.GetContentSnapshotEpoch(), epoch, "session B should reuse process epoch");
        _test.True(
            sessionB.GetContentCatalogTyped().GetItemDefsTyped().Count > 0,
            "session B should use content after the A/B lifecycle barrier"
        );
        await coordinator.CloseSessionAsync(sessionB);
        sessionB = null;
        await LifecycleMeasurementBarrier.RunAsync(this);
        AssertHostStable(host, processSnapshot, epoch, rootCount, "after session B close");

        SyntheticContentSnapshotSeed syntheticSeed =
            SyntheticContentSnapshotFactory.CreateSeed(processSnapshot);
        syntheticSeed.Epoch = epoch + 1000;
        ContentSnapshot synthetic = SyntheticContentSnapshotFactory.Create(syntheticSeed);
        using (
            GameSession syntheticSession = GameSessionTestFactory.CreateSynthetic(
                synthetic,
                host.LegacyEnemyContent
            )
        )
        {
            _test.Eq(
                syntheticSession.GetContentSnapshotEpoch(),
                synthetic.Epoch,
                "same-process tests should accept a pure synthetic snapshot"
            );
        }
        _test.Eq(
            host.CanonicalRootCount,
            rootCount,
            "synthetic snapshot construction should not add raw roots"
        );
        _test.True(
            Throws<InvalidOperationException>(() => _ = new ProcessContentHost()),
            "same-process tests should not create a second raw content host"
        );

    }

    private void AssertHostStable(
        ProcessContentHost host,
        ContentSnapshot expectedSnapshot,
        long expectedEpoch,
        int expectedRootCount,
        string label
    )
    {
        _test.True(ReferenceEquals(host.GetSnapshot(), expectedSnapshot), $"{label}: snapshot identity");
        _test.Eq(host.Epoch, expectedEpoch, $"{label}: epoch");
        _test.Eq(host.CanonicalRootCount, expectedRootCount, $"{label}: canonical roots");
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }
}
