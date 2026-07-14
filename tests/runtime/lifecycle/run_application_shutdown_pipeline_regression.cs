using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class run_application_shutdown_pipeline_regression : LifecycleTestSceneTree
{
    private enum FailurePoint
    {
        None = 0,
        Quiesce,
        Runtime,
        Scene,
        Content,
        Barrier,
    }

    private sealed class FakeShutdownHooks : IApplicationShutdownHooks
    {
        private readonly FailurePoint _failurePoint;

        internal FakeShutdownHooks(FailurePoint failurePoint = FailurePoint.None)
        {
            _failurePoint = failurePoint;
        }

        internal List<string> Calls { get; } = new();
        internal bool ReleaseGatePasses { get; set; } = true;
        internal bool BarrierGatePasses { get; set; } = true;
        internal bool ContentCalled { get; private set; }
        internal bool BarrierCalled { get; private set; }

        public ValueTask QuiesceAsync(ShutdownReport report)
        {
            Calls.Add("quiesce");
            ThrowIf(FailurePoint.Quiesce);
            return ValueTask.CompletedTask;
        }

        public ValueTask DrainRuntimeAsync(ShutdownReport report)
        {
            Calls.Add("runtime");
            ThrowIf(FailurePoint.Runtime);
            return ValueTask.CompletedTask;
        }

        public ValueTask DrainSceneAsync(ShutdownReport report)
        {
            Calls.Add("scene");
            ThrowIf(FailurePoint.Scene);
            return ValueTask.CompletedTask;
        }

        public bool CanReleaseProcessContent(ShutdownReport report, out string failure)
        {
            Calls.Add("release-gate");
            failure = ReleaseGatePasses ? string.Empty : "content borrowers are still active";
            return ReleaseGatePasses;
        }

        public ValueTask ReleaseContentAsync(ShutdownReport report)
        {
            Calls.Add("content");
            ContentCalled = true;
            ThrowIf(FailurePoint.Content);
            return ValueTask.CompletedTask;
        }

        public bool CanRunFinalizerBarrier(ShutdownReport report, out string failure)
        {
            Calls.Add("barrier-gate");
            failure = BarrierGatePasses ? string.Empty : "content roots are still active";
            return BarrierGatePasses;
        }

        public void RunFinalizerBarrier(ShutdownReport report)
        {
            Calls.Add("barrier");
            BarrierCalled = true;
            ThrowIf(FailurePoint.Barrier);
        }

        private void ThrowIf(FailurePoint failurePoint)
        {
            if (_failurePoint == failurePoint)
                throw new InvalidOperationException($"{failurePoint} hook failed");
        }
    }

    private readonly TestHarness _test = new();

    public override async void _Initialize()
    {
        try
        {
            await RunAsync();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected shutdown pipeline regression exception: {exception}");
        }

        RequestTestExit(_test.Finish("Application shutdown pipeline regression"));
    }

    private async Task RunAsync()
    {
        await TestSuccessfulOrderAndPhases();
        await TestPreContentHookFailuresSkipUnsafeRelease();
        await TestFalseReleaseGateKeepsContentRoots();
        await TestActiveAuditOwnerKeepsContentRoots();
        await TestContentReleaseFailureSkipsBarrier();
        await TestFalseFinalizerGateSkipsBarrier();
        await TestCanonicalRootAuditSkipsBarrier();
        await TestFinalizerBarrierFailureDoesNotClaimDrain();
    }

    private async Task TestSuccessfulOrderAndPhases()
    {
        var fake = new FakeShutdownHooks();
        var audit = new LifecycleAuditRegistry();
        ShutdownReport report = CreateReport("success");
        var pipeline = new ApplicationShutdownPipeline(fake, audit);

        ShutdownReport result = await pipeline.RunAsync(report);

        _test.True(ReferenceEquals(result, report), "pipeline returns the supplied report");
        _test.Eq(
            string.Join(",", fake.Calls),
            "quiesce,runtime,scene,release-gate,content,barrier-gate,barrier",
            "successful hooks run in shutdown order"
        );
        _test.True(fake.ContentCalled, "successful shutdown releases content");
        _test.True(fake.BarrierCalled, "successful shutdown runs finalizer barrier");
        _test.False(report.FinalizerBarrierSkipped, "successful barrier is not skipped");
        _test.Eq(report.FinalPhase, ApplicationShutdownPhase.QuitRequested, "success reaches quit");
        _test.Eq(report.EffectiveExitCode, 0, "successful shutdown preserves zero exit");
        _test.Eq(report.Failures.Count, 0, "successful shutdown records no failures");
        _test.Eq(
            string.Join(",", report.PhaseHistory),
            "Running,Quiescing,RuntimeDrained,SceneDrained,ContentReleased,FinalizersDrained,QuitRequested",
            "successful shutdown records only completed phases"
        );

        LifecycleAuditSnapshot snapshot = audit.CaptureSnapshot();
        _test.Eq(snapshot.ShutdownPhases.Count, 5, "successful hooks record five phase audits");
        _test.False(
            snapshot.ShutdownPhases.Any(phase => !string.IsNullOrEmpty(phase.Failure)),
            "successful phase audits contain no exceptions"
        );
    }

    private async Task TestPreContentHookFailuresSkipUnsafeRelease()
    {
        await AssertPreContentHookFailure(
            FailurePoint.Quiesce,
            "quiesce",
            "quiesce failure"
        );
        await AssertPreContentHookFailure(
            FailurePoint.Runtime,
            "runtime-drain",
            "runtime failure"
        );
        await AssertPreContentHookFailure(
            FailurePoint.Scene,
            "scene-drain",
            "scene failure"
        );
    }

    private async Task AssertPreContentHookFailure(
        FailurePoint failurePoint,
        string failureStage,
        string label
    )
    {
        var fake = new FakeShutdownHooks(failurePoint);
        ShutdownReport report = await RunPipeline(fake, new LifecycleAuditRegistry(), label);

        _test.Eq(
            string.Join(",", fake.Calls),
            "quiesce,runtime,scene,release-gate",
            $"{label} still attempts safe drains and the release gate"
        );
        _test.False(fake.ContentCalled, $"{label} leaves content untouched");
        _test.False(fake.BarrierCalled, $"{label} does not run finalizer barrier");
        _test.False(
            report.PhaseHistory.Contains(ApplicationShutdownPhase.ContentReleased),
            $"{label} does not claim content release"
        );
        _test.False(
            report.PhaseHistory.Contains(ApplicationShutdownPhase.FinalizersDrained),
            $"{label} does not claim finalizer drain"
        );
        _test.True(
            report.Failures.Any(failure => failure.Stage == failureStage),
            $"{label} records the failing hook"
        );
        AssertSkippedFailure(report, label);
    }

    private async Task TestFalseReleaseGateKeepsContentRoots()
    {
        var fake = new FakeShutdownHooks { ReleaseGatePasses = false };
        ShutdownReport report = await RunPipeline(
            fake,
            new LifecycleAuditRegistry(),
            "false release gate"
        );

        _test.True(report.FinalizerBarrierSkipped, "unsafe barrier is skipped");
        _test.Eq(report.FinalPhase, ApplicationShutdownPhase.QuitRequested, "failure reaches quit");
        _test.Eq(report.EffectiveExitCode, 1, "skipped barrier forces failure");
        _test.False(fake.ContentCalled, "content roots remain while borrowers may be live");
        _test.False(fake.BarrierCalled, "barrier is not forced with active owners");
        _test.Eq(
            string.Join(",", fake.Calls),
            "quiesce,runtime,scene,release-gate",
            "false release gate stops before content"
        );
        _test.True(
            report.Failures.Any(failure => failure.Stage == "content-release-gate"),
            "false release gate records its reason"
        );
    }

    private async Task TestActiveAuditOwnerKeepsContentRoots()
    {
        var fake = new FakeShutdownHooks();
        var audit = new LifecycleAuditRegistry();
        var owner = new object();
        audit.RegisterActive(LifecycleAuditActiveKind.Owner, "active-owner", "Session", owner);

        ShutdownReport report = await RunPipeline(fake, audit, "active audit owner");

        _test.False(fake.ContentCalled, "active audit owner blocks content release");
        _test.False(fake.BarrierCalled, "active audit owner blocks finalizer barrier");
        _test.True(
            report.Failures.Any(failure => failure.Stage == "content-release-gate"),
            "active audit owner records release gate failure"
        );
        AssertSkippedFailure(report, "active audit owner");

        audit.UnregisterActive(LifecycleAuditActiveKind.Owner, "active-owner", "Session");
    }

    private async Task TestContentReleaseFailureSkipsBarrier()
    {
        var fake = new FakeShutdownHooks(FailurePoint.Content);
        ShutdownReport report = await RunPipeline(
            fake,
            new LifecycleAuditRegistry(),
            "content release failure"
        );

        _test.True(fake.ContentCalled, "content release failure records the attempted release");
        _test.False(fake.BarrierCalled, "content release failure stops before barrier");
        _test.False(
            report.PhaseHistory.Contains(ApplicationShutdownPhase.ContentReleased),
            "failed content release is not recorded as completed"
        );
        _test.True(
            report.Failures.Any(failure => failure.Stage == "content-release"),
            "content release exception is recorded"
        );
        AssertSkippedFailure(report, "content release failure");
    }

    private async Task TestFalseFinalizerGateSkipsBarrier()
    {
        var fake = new FakeShutdownHooks { BarrierGatePasses = false };
        ShutdownReport report = await RunPipeline(
            fake,
            new LifecycleAuditRegistry(),
            "false finalizer gate"
        );

        _test.True(fake.ContentCalled, "finalizer gate runs only after content release");
        _test.False(fake.BarrierCalled, "false finalizer gate prevents barrier");
        _test.True(
            report.PhaseHistory.Contains(ApplicationShutdownPhase.ContentReleased),
            "successful content release is recorded before finalizer rejection"
        );
        _test.False(
            report.PhaseHistory.Contains(ApplicationShutdownPhase.FinalizersDrained),
            "false finalizer gate does not claim finalizer drain"
        );
        _test.True(
            report.Failures.Any(failure => failure.Stage == "finalizer-barrier-gate"),
            "false finalizer gate records its reason"
        );
        AssertSkippedFailure(report, "false finalizer gate");
    }

    private async Task TestCanonicalRootAuditSkipsBarrier()
    {
        var fake = new FakeShutdownHooks();
        var audit = new LifecycleAuditRegistry();
        var contentRoot = new object();
        const string path = "res://data/configs/lifecycle_pipeline_probe.tres";
        audit.RegisterProcessContentRoot(path, typeof(object), contentRoot);

        ShutdownReport report = await RunPipeline(fake, audit, "canonical root remains");

        _test.True(fake.ContentCalled, "canonical root audit runs after content release hook");
        _test.False(fake.BarrierCalled, "canonical root audit blocks barrier");
        _test.True(
            report.Failures.Any(failure => failure.Stage == "finalizer-barrier-gate"),
            "canonical root audit records finalizer gate failure"
        );
        AssertSkippedFailure(report, "canonical root remains");

        audit.ReleaseProcessContentRoot(path);
    }

    private async Task TestFinalizerBarrierFailureDoesNotClaimDrain()
    {
        var fake = new FakeShutdownHooks(FailurePoint.Barrier);
        ShutdownReport report = await RunPipeline(
            fake,
            new LifecycleAuditRegistry(),
            "barrier failure"
        );

        _test.True(fake.ContentCalled, "barrier failure occurs after content release");
        _test.True(fake.BarrierCalled, "barrier failure records the attempted barrier");
        _test.False(
            report.PhaseHistory.Contains(ApplicationShutdownPhase.FinalizersDrained),
            "failed barrier is not recorded as completed"
        );
        _test.True(
            report.Failures.Any(failure => failure.Stage == "finalizer-barrier"),
            "barrier exception is recorded"
        );
        AssertSkippedFailure(report, "barrier failure");
    }

    private static async ValueTask<ShutdownReport> RunPipeline(
        FakeShutdownHooks fake,
        LifecycleAuditRegistry audit,
        string label
    )
    {
        var pipeline = new ApplicationShutdownPipeline(fake, audit);
        return await pipeline.RunAsync(CreateReport(label));
    }

    private void AssertSkippedFailure(ShutdownReport report, string label)
    {
        _test.True(report.FinalizerBarrierSkipped, $"{label} marks barrier skipped");
        _test.Eq(
            report.FinalPhase,
            ApplicationShutdownPhase.QuitRequested,
            $"{label} still reaches quit"
        );
        _test.Eq(report.EffectiveExitCode, 1, $"{label} forces nonzero exit");
    }

    private static ShutdownReport CreateReport(string label)
    {
        return new ShutdownReport(
            new ShutdownRequest(
                0,
                ShutdownReason.TestComplete,
                new ShutdownCallerResult(label, true)
            )
        );
    }
}
