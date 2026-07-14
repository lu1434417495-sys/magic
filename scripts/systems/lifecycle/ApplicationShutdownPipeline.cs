using System;
using System.Diagnostics;
using System.Threading.Tasks;

internal sealed class ApplicationShutdownPipeline
{
    private readonly IApplicationShutdownHooks _hooks;
    private readonly LifecycleAuditRegistry _audit;

    internal ApplicationShutdownPipeline(
        IApplicationShutdownHooks hooks,
        LifecycleAuditRegistry audit
    )
    {
        _hooks = hooks;
        _audit = audit;
    }

    internal async ValueTask<ShutdownReport> RunAsync(ShutdownReport report)
    {
        if (!report.TryAdvancePhase(ApplicationShutdownPhase.Quiescing))
        {
            report.RecordFailure(
                "quiesce",
                $"Cannot start shutdown from phase {report.FinalPhase}."
            );
            return report;
        }

        bool quiesceSucceeded = await RunHookAsync(
            report,
            "quiesce",
            ApplicationShutdownPhase.Quiescing,
            () => _hooks.QuiesceAsync(report)
        );
        bool runtimeSucceeded = await RunHookAsync(
            report,
            "runtime-drain",
            ApplicationShutdownPhase.RuntimeDrained,
            () => _hooks.DrainRuntimeAsync(report)
        );
        bool runtimePhaseRecorded =
            quiesceSucceeded
            && runtimeSucceeded
            && TryRecordPhase(report, ApplicationShutdownPhase.RuntimeDrained, "runtime-drain");

        bool sceneSucceeded = await RunHookAsync(
            report,
            "scene-drain",
            ApplicationShutdownPhase.SceneDrained,
            () => _hooks.DrainSceneAsync(report)
        );
        bool scenePhaseRecorded =
            runtimePhaseRecorded
            && sceneSucceeded
            && TryRecordPhase(report, ApplicationShutdownPhase.SceneDrained, "scene-drain");

        bool releaseGatePassed = TryReleaseGate(report, out string releaseGateFailure);
        LifecycleAuditSnapshot preReleaseAudit = _audit.CaptureSnapshot();
        bool activeRuntimeDrained = preReleaseAudit.NonTerminalCount == 0;
        if (!activeRuntimeDrained)
        {
            releaseGateFailure =
                $"Cannot release process content while {preReleaseAudit.NonTerminalCount} "
                + "non-terminal lifecycle objects are active.";
            report.RecordFailure("content-release-gate", releaseGateFailure);
        }

        if (
            !quiesceSucceeded
            || !runtimeSucceeded
            || !sceneSucceeded
            || !scenePhaseRecorded
            || !releaseGatePassed
            || !activeRuntimeDrained
        )
        {
            SkipBarrierAndRequestQuit(
                report,
                string.IsNullOrWhiteSpace(releaseGateFailure)
                    ? "Shutdown hooks failed before process content could be released safely."
                    : releaseGateFailure
            );
            return report;
        }

        long preReleaseViolationCount = preReleaseAudit.ViolationCount;
        bool contentReleased = await RunHookAsync(
            report,
            "content-release",
            ApplicationShutdownPhase.ContentReleased,
            () => _hooks.ReleaseContentAsync(report)
        );
        if (
            !contentReleased
            || !TryRecordPhase(
                report,
                ApplicationShutdownPhase.ContentReleased,
                "content-release"
            )
        )
        {
            SkipBarrierAndRequestQuit(report, "Process content release did not complete.");
            return report;
        }

        bool barrierGatePassed = TryBarrierGate(report, out string barrierGateFailure);
        LifecycleAuditSnapshot preBarrierAudit = _audit.CaptureSnapshot();
        if (preBarrierAudit.NonTerminalCount != 0)
        {
            barrierGateFailure =
                $"Cannot run the finalizer barrier while {preBarrierAudit.NonTerminalCount} "
                + "non-terminal lifecycle objects are active.";
            report.RecordFailure("finalizer-barrier-gate", barrierGateFailure);
            barrierGatePassed = false;
        }
        if (preBarrierAudit.ProcessContentRootCount != 0)
        {
            barrierGateFailure =
                $"Cannot run the finalizer barrier while "
                + $"{preBarrierAudit.ProcessContentRootCount} canonical content roots remain.";
            report.RecordFailure("finalizer-barrier-gate", barrierGateFailure);
            barrierGatePassed = false;
        }
        if (preBarrierAudit.ViolationCount != preReleaseViolationCount)
        {
            barrierGateFailure =
                "Cannot run the finalizer barrier after a new lifecycle violation. "
                + $"before={preReleaseViolationCount}, after={preBarrierAudit.ViolationCount}.";
            report.RecordFailure("finalizer-barrier-gate", barrierGateFailure);
            barrierGatePassed = false;
        }

        if (!barrierGatePassed)
        {
            SkipBarrierAndRequestQuit(
                report,
                string.IsNullOrWhiteSpace(barrierGateFailure)
                    ? "Finalizer barrier safety gate failed."
                    : barrierGateFailure
            );
            return report;
        }

        bool finalizersDrained = RunBarrier(report);
        if (
            !finalizersDrained
            || !TryRecordPhase(
                report,
                ApplicationShutdownPhase.FinalizersDrained,
                "finalizer-barrier"
            )
        )
        {
            SkipBarrierAndRequestQuit(report, "Finalizer barrier did not complete.");
            return report;
        }

        RequestQuit(report);
        return report;
    }

    private async ValueTask<bool> RunHookAsync(
        ShutdownReport report,
        string stage,
        ApplicationShutdownPhase auditPhase,
        Func<ValueTask> hook
    )
    {
        long startedAt = Stopwatch.GetTimestamp();
        Exception failure = null;
        try
        {
            await hook();
            return true;
        }
        catch (Exception exception)
        {
            failure = exception;
            report.RecordFailure(stage, exception);
            return false;
        }
        finally
        {
            _audit.RecordShutdownPhase(
                auditPhase,
                Stopwatch.GetElapsedTime(startedAt),
                failure
            );
        }
    }

    private bool TryReleaseGate(ShutdownReport report, out string failure)
    {
        try
        {
            bool passed = _hooks.CanReleaseProcessContent(report, out failure);
            if (!passed)
            {
                failure = GateFailureOrDefault(
                    failure,
                    "Process content release safety gate failed."
                );
                report.RecordFailure("content-release-gate", failure);
            }
            return passed;
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            report.RecordFailure("content-release-gate", exception);
            return false;
        }
    }

    private bool TryBarrierGate(ShutdownReport report, out string failure)
    {
        try
        {
            bool passed = _hooks.CanRunFinalizerBarrier(report, out failure);
            if (!passed)
            {
                failure = GateFailureOrDefault(
                    failure,
                    "Finalizer barrier safety gate failed."
                );
                report.RecordFailure("finalizer-barrier-gate", failure);
            }
            return passed;
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            report.RecordFailure("finalizer-barrier-gate", exception);
            return false;
        }
    }

    private bool RunBarrier(ShutdownReport report)
    {
        long startedAt = Stopwatch.GetTimestamp();
        Exception failure = null;
        try
        {
            _hooks.RunFinalizerBarrier(report);
            return true;
        }
        catch (Exception exception)
        {
            failure = exception;
            report.RecordFailure("finalizer-barrier", exception);
            return false;
        }
        finally
        {
            _audit.RecordShutdownPhase(
                ApplicationShutdownPhase.FinalizersDrained,
                Stopwatch.GetElapsedTime(startedAt),
                failure
            );
        }
    }

    private static bool TryRecordPhase(
        ShutdownReport report,
        ApplicationShutdownPhase phase,
        string stage
    )
    {
        if (report.TryAdvancePhase(phase))
            return true;

        report.RecordFailure(stage, $"Cannot record completed shutdown phase {phase}.");
        return false;
    }

    private static void SkipBarrierAndRequestQuit(ShutdownReport report, string failure)
    {
        report.MarkFinalizerBarrierSkipped(failure);
        RequestQuit(report);
    }

    private static void RequestQuit(ShutdownReport report)
    {
        if (!report.TryAdvancePhase(ApplicationShutdownPhase.QuitRequested))
        {
            report.RecordFailure(
                "quit-request",
                $"Cannot request quit from phase {report.FinalPhase}."
            );
        }
    }

    private static string GateFailureOrDefault(string failure, string fallback)
    {
        return string.IsNullOrWhiteSpace(failure) ? fallback : failure;
    }
}
