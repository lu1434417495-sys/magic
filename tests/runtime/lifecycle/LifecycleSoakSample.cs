internal sealed record LifecycleSoakSample(
    int Cycle,
    LifecycleSoakCounterVector ActiveCounters,
    LifecycleSoakActivityDelta ActivityDelta,
    long ManagedMemoryBytes,
    long PrivateMemoryBytes
);
