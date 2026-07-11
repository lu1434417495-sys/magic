using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using Godot;

public partial class run_application_lifecycle_soak_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => CallDeferred(nameof(Run));

    private async void Run()
    {
        try
        {
            await RunSoakAsync();
        }
        catch (Exception exception)
        {
            _test.Fail($"application lifecycle soak threw: {exception}");
        }

        RequestTestExit(_test.Finish("Application lifecycle soak regression"));
    }

    private async Task RunSoakAsync()
    {
        ApplicationLifetimeCoordinator coordinator = Root.GetNode<ApplicationLifetimeCoordinator>(
            "ApplicationLifetimeCoordinator"
        );
        ProcessContentHost contentHost = coordinator.ContentHost;
        ContentSnapshot processSnapshot = contentHost.GetSnapshot();
        _test.True(contentHost.IsSealed, "process content host is sealed before lifecycle soak");
        _test.True(
            processSnapshot != null && processSnapshot.Epoch == contentHost.Epoch,
            "process content snapshot is published exactly once before lifecycle soak"
        );
        _test.True(
            ReferenceEquals(processSnapshot, contentHost.BuildAndSeal()),
            "sealed host returns the single already-published process snapshot"
        );

        GameSession bootstrap = Root.GetNodeOrNull<GameSession>("GameSession");
        if (bootstrap != null)
            await coordinator.CloseSessionAsync(bootstrap);
        bootstrap = null;
        await LifecycleMeasurementBarrier.RunAsync(this);
        _test.Eq(
            LifecycleAuditRegistry.Shared.CaptureSnapshot().ActiveContentBorrowerCount,
            0,
            "bootstrap session borrower closes before lifecycle soak"
        );

        string runId;
        using (Process process = Process.GetCurrentProcess())
            runId = $"{process.Id}-{Guid.NewGuid():N}";
        GameSessionPersistenceOptions persistenceOptions =
            GameSessionPersistenceOptions.ForLifecycleSoak(runId);
        var scenario = new LifecycleSoakScenario(
            this,
            coordinator,
            contentHost,
            persistenceOptions
        );
        var samples = new List<LifecycleSoakSample>(LifecycleSoakStatistics.TotalCycleCount);

        for (int cycle = 1; cycle <= LifecycleSoakStatistics.TotalCycleCount; cycle++)
        {
            LifecycleSoakSample sample = await scenario.RunCycleAsync(cycle);
            samples.Add(sample);
            GD.Print(FormatCycle(sample));
        }

        LifecycleSoakStatisticsReport report = LifecycleSoakStatistics.Evaluate(samples);
        GD.Print(FormatSummary(report));
        foreach (LifecycleSoakFailure failure in report.Failures)
        {
            GD.Print(
                $"[LIFECYCLE-SOAK] failure cycle={failure.Cycle} "
                    + $"counter={failure.CounterName} message={failure.Message}"
            );
            _test.Fail(
                $"cycle {failure.Cycle} {failure.CounterName}: {failure.Message}"
            );
        }

        _test.Eq(
            samples.Count,
            LifecycleSoakStatistics.TotalCycleCount,
            "lifecycle soak emits exactly 110 cycle samples"
        );
        _test.True(report.Passed, "lifecycle soak counters and memory stay within contract");
    }

    private static string FormatCycle(LifecycleSoakSample sample)
    {
        LifecycleSoakCounterVector counters = sample.ActiveCounters;
        LifecycleSoakActivityDelta activity = sample.ActivityDelta;
        return
            $"[LIFECYCLE-SOAK] cycle={sample.Cycle} "
            + $"managed={sample.ManagedMemoryBytes} private={sample.PrivateMemoryBytes} "
            + $"owners=session:{counters.SessionOwners},battle:{counters.BattleOwners},"
            + $"decision:{counters.DecisionOwners},request:{counters.RequestOwners},"
            + $"scene_tree:{counters.SceneTreeOwners} "
            + $"borrowers={counters.ContentBorrowers} jobs={counters.ActiveJobs} "
            + $"scopes={Escape(counters.NativeScopesByDomain)} "
            + $"leases={Escape(counters.ProjectionLeasesByDomain)} "
            + $"epoch={counters.SnapshotEpoch} "
            + $"violations=unknown:{counters.UnknownOwnershipViolations},"
            + $"conflict:{counters.OwnerConflictViolations},"
            + $"escaped:{counters.EscapedLeaseViolations},"
            + $"after_close:{counters.CloseAfterUseViolations} "
            + $"suppressions={counters.NormalSuppressions} "
            + $"quarantine={counters.QuarantinedWrappers} "
            + $"activity=owners:{activity.OwnersRegistered}/{activity.OwnersClosed},"
            + $"native:{activity.NativeWrappersOwned}/{activity.NativeWrappersDisposed},"
            + $"projection:{activity.ProjectionContainersOwned}/{activity.ProjectionContainersDisposed},"
            + $"transfer:{activity.TransfersOut}/{activity.TransfersIn}";
    }

    private static string FormatSummary(LifecycleSoakStatisticsReport report) =>
        "[LIFECYCLE-SOAK] summary "
        + $"samples={report.SampleCount} passed={report.Passed} "
        + $"managed_baseline={Format(report.ManagedMemory.BaselineMedianBytes)} "
        + $"managed_final={Format(report.ManagedMemory.FinalMedianBytes)} "
        + $"managed_delta={Format(report.ManagedMemory.DeltaBytes)} "
        + $"managed_allowed={Format(report.ManagedMemory.AllowedDeltaBytes)} "
        + $"managed_slope={Format(report.ManagedMemory.SlopeBytesPerCycle)} "
        + $"private_baseline={Format(report.PrivateMemory.BaselineMedianBytes)} "
        + $"private_final={Format(report.PrivateMemory.FinalMedianBytes)} "
        + $"private_delta={Format(report.PrivateMemory.DeltaBytes)} "
        + $"private_allowed={Format(report.PrivateMemory.AllowedDeltaBytes)} "
        + $"private_slope={Format(report.PrivateMemory.SlopeBytesPerCycle)} "
        + $"failures={report.Failures.Count}";

    private static string Format(double value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        string.IsNullOrEmpty(value) ? "<empty>" : value;
}
