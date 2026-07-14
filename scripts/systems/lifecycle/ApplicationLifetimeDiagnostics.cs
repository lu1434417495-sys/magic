using System;
using System.Threading;

internal static class ApplicationLifetimeDiagnostics
{
    private static int _currentPhase = (int)ApplicationShutdownPhase.Running;
    private static int _finalizersDrained;
    private static int _finalizerBarrierSkipped;

    internal static ApplicationShutdownPhase CurrentPhase =>
        (ApplicationShutdownPhase)Volatile.Read(ref _currentPhase);

    internal static void RecordPhase(ApplicationShutdownPhase phase)
    {
        Volatile.Write(ref _currentPhase, (int)phase);
        if (phase == ApplicationShutdownPhase.FinalizersDrained)
            Volatile.Write(ref _finalizersDrained, 1);
        else if (phase == ApplicationShutdownPhase.FinalizerBarrierSkipped)
            Volatile.Write(ref _finalizerBarrierSkipped, 1);
    }

    internal static void RecordProcessExit(ApplicationShutdownPhase phase)
    {
        bool barrierSkipped =
            phase == ApplicationShutdownPhase.FinalizerBarrierSkipped
            || Volatile.Read(ref _finalizerBarrierSkipped) != 0;
        bool finalizersDrained =
            phase == ApplicationShutdownPhase.FinalizersDrained
            || Volatile.Read(ref _finalizersDrained) != 0;
        string status = barrierSkipped
            ? "failure-finalizer-barrier-skipped"
            : finalizersDrained
                ? "finalizers-drained"
                : "incomplete-shutdown";

        Console.Error.WriteLine($"[lifecycle] process-exit phase={phase} status={status}");
    }
}
