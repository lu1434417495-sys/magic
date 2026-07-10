internal enum ApplicationShutdownPhase
{
    Running = 0,
    Quiescing,
    RuntimeDrained,
    SceneDrained,
    ContentReleased,
    FinalizersDrained,
    FinalizerBarrierSkipped,
    QuitRequested,
}
