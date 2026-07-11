using System;
using Godot;

public partial class run_application_shutdown_contract_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestResult exitCode = Run();
        RequestTestExit(exitCode);
    }

    private TestResult Run()
    {
        TestShutdownStateMachineContracts();
        TestShutdownReportContracts();
        TestShutdownReportSkippedBarrierInvariant();
        TestExitDiagnosticContracts();
        TestLifecycleAuditRegistryContracts();

        return _test.Finish("Application shutdown contract regression");
    }

    private void TestShutdownStateMachineContracts()
    {
        var machine = new ApplicationShutdownStateMachine();
        _test.Eq(machine.Phase, ApplicationShutdownPhase.Running, "starts running");
        _test.True(machine.TryAdvance(ApplicationShutdownPhase.Quiescing), "enter quiescing");
        _test.False(
            machine.TryAdvance(ApplicationShutdownPhase.SceneDrained),
            "cannot skip runtime"
        );
        _test.True(
            machine.TryAdvance(ApplicationShutdownPhase.RuntimeDrained),
            "runtime drained"
        );
        _test.True(machine.TryAdvance(ApplicationShutdownPhase.SceneDrained), "scene drained");
        _test.True(
            machine.TryAdvance(ApplicationShutdownPhase.ContentReleased),
            "content released"
        );
        _test.True(
            machine.TryAdvance(ApplicationShutdownPhase.FinalizersDrained),
            "finalizers drained"
        );
        _test.True(
            machine.TryAdvance(ApplicationShutdownPhase.QuitRequested),
            "quit requested"
        );
        _test.False(
            machine.TryAdvance(ApplicationShutdownPhase.Quiescing),
            "terminal success cannot restart"
        );

        var failed = new ApplicationShutdownStateMachine();
        _test.True(failed.TryAdvance(ApplicationShutdownPhase.Quiescing), "failure quiesces");
        _test.True(
            failed.TryAdvance(ApplicationShutdownPhase.FinalizerBarrierSkipped),
            "unsafe pre-content state enters explicit failure"
        );
        _test.True(
            failed.TryAdvance(ApplicationShutdownPhase.QuitRequested),
            "failed shutdown still requests quit"
        );
        _test.False(
            failed.TryAdvance(ApplicationShutdownPhase.ContentReleased),
            "failed shutdown cannot claim content release"
        );

        AssertFailureBranch(
            "runtime failure",
            ApplicationShutdownPhase.Quiescing,
            ApplicationShutdownPhase.RuntimeDrained
        );
        AssertFailureBranch(
            "scene failure",
            ApplicationShutdownPhase.Quiescing,
            ApplicationShutdownPhase.RuntimeDrained,
            ApplicationShutdownPhase.SceneDrained
        );
        AssertFailureBranch(
            "content failure",
            ApplicationShutdownPhase.Quiescing,
            ApplicationShutdownPhase.RuntimeDrained,
            ApplicationShutdownPhase.SceneDrained,
            ApplicationShutdownPhase.ContentReleased
        );
    }

    private void TestShutdownReportContracts()
    {
        var failedRequest = new ShutdownRequest(
            0,
            ShutdownReason.TestComplete,
            new ShutdownCallerResult("contract", false)
        );
        var failedReport = new ShutdownReport(failedRequest);
        _test.Eq(failedReport.EffectiveExitCode, 1, "failed caller forces nonzero exit");

        var firstRequest = new ShutdownRequest(
            0,
            ShutdownReason.TestComplete,
            new ShutdownCallerResult("first", true)
        );
        var report = new ShutdownReport(firstRequest);
        _test.True(
            ReferenceEquals(report.FirstRequest, firstRequest),
            "report preserves the first request"
        );
        _test.Eq(report.RequestedExitCode, 0, "requested code comes from first request");
        _test.Eq(report.EffectiveExitCode, 0, "passing request starts successful");
        _test.Eq(report.FinalPhase, ApplicationShutdownPhase.Running, "report starts running");
        _test.Eq(report.PhaseHistory.Count, 1, "phase history includes running");

        report.MergeRequest(
            new ShutdownRequest(
                7,
                ShutdownReason.RequestedExit,
                new ShutdownCallerResult("duplicate", true)
            )
        );
        _test.Eq(
            report.FirstRequest.Reason,
            ShutdownReason.TestComplete,
            "duplicate request cannot replace first reason"
        );
        _test.Eq(report.EffectiveExitCode, 7, "later nonzero request raises exit code");
        _test.Eq(
            report.DuplicateRequestDiagnostics.Count,
            1,
            "duplicate request is diagnosed"
        );

        report.MergeRequest(
            new ShutdownRequest(
                0,
                ShutdownReason.WindowClose,
                new ShutdownCallerResult("later-success", true)
            )
        );
        _test.Eq(report.EffectiveExitCode, 7, "later success cannot lower exit code");

        var laterFailedReport = new ShutdownReport(firstRequest);
        laterFailedReport.MergeRequest(
            new ShutdownRequest(
                0,
                ShutdownReason.RequestedExit,
                new ShutdownCallerResult("later-failure", false)
            )
        );
        _test.Eq(
            laterFailedReport.EffectiveExitCode,
            1,
            "later failed caller raises exit code"
        );

        report.RecordFailure("runtime", "runtime close failed");
        _test.Eq(report.Failures.Count, 1, "structured failure is retained");
        _test.Eq(report.EffectiveExitCode, 7, "failure keeps an existing nonzero code");

        _test.True(
            report.TryAdvancePhase(ApplicationShutdownPhase.Quiescing),
            "report records legal phase"
        );
        report.MarkFinalizerBarrierSkipped("unsafe active owner");
        _test.True(report.FinalizerBarrierSkipped, "barrier skip is explicit");
        _test.Eq(
            report.FinalPhase,
            ApplicationShutdownPhase.FinalizerBarrierSkipped,
            "barrier skip updates final pre-quit phase"
        );
        _test.Eq(report.PhaseHistory.Count, 3, "phase history records failure branch");

        var debt = new LifecycleLegacyDebtSnapshot(
            "battle-board-controller-quarantine",
            "scripts/ui/BattleBoardController.cs",
            "SceneTree",
            2
        );
        report.CaptureLegacyDebt(new[] { debt });
        _test.Eq(report.LegacyDebt.Count, 1, "report captures exact legacy debt snapshot");
        _test.Eq(
            report.LegacyDebt[0].DebtId,
            "battle-board-controller-quarantine",
            "legacy debt identity is retained"
        );
    }

    private void TestExitDiagnosticContracts()
    {
        var passingReport = new ShutdownReport(
            new ShutdownRequest(
                0,
                ShutdownReason.TestComplete,
                new ShutdownCallerResult("diagnostic-contract", true)
            )
        );
        _test.Eq(
            ApplicationLifetimeCoordinator.FormatCallerResult(passingReport),
            "diagnostic-contract: PASS",
            "coordinator emits the caller label and successful result"
        );

        passingReport.RecordFailure("diagnostic-contract", "forced failure");
        _test.Eq(
            ApplicationLifetimeCoordinator.FormatCallerResult(passingReport),
            "diagnostic-contract: FAIL",
            "coordinator result reflects the effective shutdown outcome"
        );
        _test.Eq(
            TestExitCoordinator.FormatFailureDiagnostic(
                "diagnostic-contract",
                "assertion detail"
            ),
            "[test] diagnostic-contract: assertion detail",
            "test exit adapter retains actionable assertion detail"
        );
    }

    private void TestLifecycleAuditRegistryContracts()
    {
        var registry = new LifecycleAuditRegistry();
        var owner = new object();
        var borrower = new object();
        var lease = new object();
        var scope = new object();
        var job = new object();
        var contentRoot = new object();

        registry.RegisterActive(
            LifecycleAuditActiveKind.Owner,
            "session-owner",
            "Session",
            owner
        );
        registry.RegisterActive(
            LifecycleAuditActiveKind.ContentBorrower,
            "content-borrower",
            "Session",
            borrower
        );
        registry.RegisterActive(LifecycleAuditActiveKind.Lease, "request-lease", "Request", lease);
        registry.RegisterActive(LifecycleAuditActiveKind.Scope, "battle-scope", "Battle", scope);
        registry.RegisterActive(LifecycleAuditActiveKind.Job, "worker-job", "Process", job);
        registry.RegisterProcessContentRoot(
            "res://data/configs/probe.tres",
            typeof(object),
            contentRoot
        );
        registry.SetActiveContentSnapshotEpoch(3);
        registry.RecordTransferred();
        registry.RecordNormalPhaseSuppress();
        registry.RecordQuarantine();
        registry.RegisterLegacyDebt(
            new LifecycleLegacyDebtSnapshot(
                "battle-board-controller-quarantine",
                "scripts/ui/BattleBoardController.cs",
                "SceneTree",
                2
            )
        );

        LifecycleAuditSnapshot active = registry.CaptureSnapshot();
        _test.Eq(active.ActiveOwnerCount, 1, "active owner is counted");
        _test.Eq(active.ActiveContentBorrowerCount, 1, "active content borrower is counted");
        _test.Eq(active.ActiveLeaseCount, 1, "active lease is counted");
        _test.Eq(active.ActiveScopeCount, 1, "active scope is counted");
        _test.Eq(active.ActiveJobCount, 1, "active job is counted");
        _test.Eq(active.NonTerminalCount, 5, "non-terminal count includes every active kind");
        _test.Eq(active.ProcessContentRootCount, 1, "process content root is counted");
        _test.Eq(active.ActiveContentSnapshotEpoch, 3L, "content epoch is captured");
        _test.Eq(active.CreatedCount, 6L, "created total is monotonic");
        _test.Eq(active.TransferredCount, 1L, "transfer total is captured");
        _test.Eq(active.NormalPhaseSuppressCount, 1L, "normal suppress debt is counted");
        _test.Eq(active.QuarantineCount, 1L, "quarantine debt is counted");
        _test.Eq(active.WeakDiagnostics.Count, 6, "diagnostic registry sees every live object");
        _test.Eq(active.ActiveCountsByDomain["Session"], 2, "domain counts are exact");
        _test.Eq(active.LegacyDebt.Count, 1, "audit snapshot includes exact legacy debt");

        registry.UnregisterActive(LifecycleAuditActiveKind.Owner, "session-owner", "Session");
        registry.UnregisterActive(
            LifecycleAuditActiveKind.ContentBorrower,
            "content-borrower",
            "Session"
        );
        registry.UnregisterActive(LifecycleAuditActiveKind.Lease, "request-lease", "Request");
        registry.UnregisterActive(LifecycleAuditActiveKind.Scope, "battle-scope", "Battle");
        registry.UnregisterActive(LifecycleAuditActiveKind.Job, "worker-job", "Process");
        registry.ReleaseProcessContentRoot("res://data/configs/probe.tres");
        registry.ClearActiveContentSnapshotEpoch();
        registry.RecordShutdownPhase(
            ApplicationShutdownPhase.RuntimeDrained,
            TimeSpan.FromMilliseconds(2),
            new InvalidOperationException("phase audit probe")
        );

        LifecycleAuditSnapshot drained = registry.CaptureSnapshot();
        _test.Eq(drained.ActiveOwnerCount, 0, "owner count drains to zero");
        _test.Eq(drained.ProcessContentRootCount, 0, "content root count drains to zero");
        _test.Eq(drained.ActiveContentSnapshotEpoch, 0L, "content epoch clears");
        _test.Eq(drained.DisposedCount, 6L, "disposed total is monotonic");
        _test.Eq(drained.ShutdownPhases.Count, 1, "shutdown phase audit is retained");
        _test.True(
            drained.ShutdownPhases[0].Failure.Contains("phase audit probe", StringComparison.Ordinal),
            "shutdown phase exception is structured into the snapshot"
        );

        string previousStrict = System.Environment.GetEnvironmentVariable(
            "MAGIC_LIFECYCLE_STRICT"
        );
        bool duplicateDebtRejected = false;
        bool escapedRejected = false;
        bool unknownRejected = false;
        try
        {
            System.Environment.SetEnvironmentVariable("MAGIC_LIFECYCLE_STRICT", "1");
            try
            {
                registry.RegisterLegacyDebt(drained.LegacyDebt[0]);
            }
            catch (InvalidOperationException)
            {
                duplicateDebtRejected = true;
            }
            try
            {
                registry.RecordEscaped("escaped probe");
            }
            catch (InvalidOperationException)
            {
                escapedRejected = true;
            }
            try
            {
                registry.RecordUnknown("unknown probe");
            }
            catch (InvalidOperationException)
            {
                unknownRejected = true;
            }
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(
                "MAGIC_LIFECYCLE_STRICT",
                previousStrict
            );
        }

        LifecycleAuditSnapshot violated = registry.CaptureSnapshot();
        _test.True(duplicateDebtRejected, "strict mode rejects duplicate legacy debt metadata");
        _test.True(escapedRejected, "strict mode rejects escaped ownership");
        _test.True(unknownRejected, "strict mode rejects unknown ownership");
        _test.Eq(violated.EscapedCount, 1L, "escaped total is captured");
        _test.Eq(violated.UnknownCount, 1L, "unknown total is captured");
        _test.Eq(violated.ViolationCount, 3L, "all registry violations are monotonic");
        _test.True(violated.HasLifecycleViolation, "violation summary is explicit");
    }

    private void TestShutdownReportSkippedBarrierInvariant()
    {
        var request = new ShutdownRequest(
            0,
            ShutdownReason.TestComplete,
            new ShutdownCallerResult("skip-invariant", true)
        );
        var report = new ShutdownReport(request);
        _test.True(
            report.TryAdvancePhase(ApplicationShutdownPhase.Quiescing),
            "skip invariant reaches quiescing"
        );

        _test.False(
            report.TryAdvancePhase(ApplicationShutdownPhase.FinalizerBarrierSkipped),
            "generic phase API rejects the dedicated skipped state"
        );
        _test.Eq(
            report.FinalPhase,
            ApplicationShutdownPhase.Quiescing,
            "generic skipped transition leaves phase unchanged"
        );
        _test.False(
            report.FinalizerBarrierSkipped,
            "generic skipped transition leaves flag unchanged"
        );
        _test.Eq(report.EffectiveExitCode, 0, "generic skipped transition leaves code unchanged");
        _test.Eq(report.PhaseHistory.Count, 2, "generic skipped transition leaves history unchanged");
        _test.Eq(report.Failures.Count, 0, "generic skipped transition records no failure");

        report.MarkFinalizerBarrierSkipped("unsafe active owner");
        _test.Eq(
            report.FinalPhase,
            ApplicationShutdownPhase.FinalizerBarrierSkipped,
            "dedicated marker enters skipped phase"
        );
        _test.True(report.FinalizerBarrierSkipped, "dedicated marker sets skipped flag");
        _test.Eq(report.EffectiveExitCode, 1, "dedicated marker forces nonzero exit");
        _test.Eq(report.PhaseHistory.Count, 3, "dedicated marker records phase history");
        _test.Eq(
            report.PhaseHistory[2],
            ApplicationShutdownPhase.FinalizerBarrierSkipped,
            "dedicated marker appends skipped phase"
        );
        _test.Eq(report.Failures.Count, 1, "dedicated marker records one failure");
        _test.Eq(
            report.Failures[0].Stage,
            "finalizer-barrier",
            "dedicated marker records failure stage"
        );
        _test.Eq(
            report.Failures[0].Message,
            "unsafe active owner",
            "dedicated marker records failure reason"
        );

        report.MarkFinalizerBarrierSkipped("duplicate marker");
        _test.Eq(report.PhaseHistory.Count, 3, "duplicate marker is history-idempotent");
        _test.Eq(report.Failures.Count, 1, "duplicate marker is failure-idempotent");
        _test.Eq(report.EffectiveExitCode, 1, "duplicate marker cannot change failure code");

        var illegal = new ShutdownReport(request);
        illegal.MarkFinalizerBarrierSkipped("illegal from running");
        _test.Eq(
            illegal.FinalPhase,
            ApplicationShutdownPhase.Running,
            "illegal marker leaves phase unchanged"
        );
        _test.False(illegal.FinalizerBarrierSkipped, "illegal marker leaves flag unchanged");
        _test.Eq(illegal.EffectiveExitCode, 0, "illegal marker leaves code unchanged");
        _test.Eq(illegal.PhaseHistory.Count, 1, "illegal marker leaves history unchanged");
        _test.Eq(illegal.Failures.Count, 0, "illegal marker records no contradictory failure");
    }

    private void AssertFailureBranch(string label, params ApplicationShutdownPhase[] prefix)
    {
        var machine = new ApplicationShutdownStateMachine();
        foreach (ApplicationShutdownPhase phase in prefix)
            _test.True(machine.TryAdvance(phase), $"{label} reaches {phase}");
        _test.True(
            machine.TryAdvance(ApplicationShutdownPhase.FinalizerBarrierSkipped),
            $"{label} enters explicit skip"
        );
        _test.True(
            machine.TryAdvance(ApplicationShutdownPhase.QuitRequested),
            $"{label} still requests quit"
        );
    }
}
