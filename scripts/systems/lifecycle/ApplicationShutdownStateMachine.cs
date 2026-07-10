internal sealed class ApplicationShutdownStateMachine
{
    internal ApplicationShutdownPhase Phase { get; private set; } =
        ApplicationShutdownPhase.Running;

    internal bool TryAdvance(ApplicationShutdownPhase nextPhase)
    {
        if (!IsLegalTransition(Phase, nextPhase))
            return false;

        Phase = nextPhase;
        return true;
    }

    private static bool IsLegalTransition(
        ApplicationShutdownPhase currentPhase,
        ApplicationShutdownPhase nextPhase
    )
    {
        return currentPhase switch
        {
            ApplicationShutdownPhase.Running =>
                nextPhase == ApplicationShutdownPhase.Quiescing,
            ApplicationShutdownPhase.Quiescing =>
                nextPhase == ApplicationShutdownPhase.RuntimeDrained
                || nextPhase == ApplicationShutdownPhase.FinalizerBarrierSkipped,
            ApplicationShutdownPhase.RuntimeDrained =>
                nextPhase == ApplicationShutdownPhase.SceneDrained
                || nextPhase == ApplicationShutdownPhase.FinalizerBarrierSkipped,
            ApplicationShutdownPhase.SceneDrained =>
                nextPhase == ApplicationShutdownPhase.ContentReleased
                || nextPhase == ApplicationShutdownPhase.FinalizerBarrierSkipped,
            ApplicationShutdownPhase.ContentReleased =>
                nextPhase == ApplicationShutdownPhase.FinalizersDrained
                || nextPhase == ApplicationShutdownPhase.FinalizerBarrierSkipped,
            ApplicationShutdownPhase.FinalizersDrained =>
                nextPhase == ApplicationShutdownPhase.QuitRequested,
            ApplicationShutdownPhase.FinalizerBarrierSkipped =>
                nextPhase == ApplicationShutdownPhase.QuitRequested,
            _ => false,
        };
    }
}
