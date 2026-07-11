internal sealed record LifecycleSoakActivityDelta(
    long OwnersRegistered,
    long OwnersClosed,
    long NativeWrappersOwned,
    long NativeWrappersDisposed,
    long ProjectionContainersOwned,
    long ProjectionContainersDisposed,
    long TransfersOut,
    long TransfersIn
);
