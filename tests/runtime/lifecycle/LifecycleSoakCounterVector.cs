internal sealed record LifecycleSoakCounterVector(
    int SessionOwners,
    int BattleOwners,
    int DecisionOwners,
    int RequestOwners,
    int SceneTreeOwners,
    int ContentBorrowers,
    int ActiveJobs,
    string NativeScopesByDomain,
    string ProjectionLeasesByDomain,
    long SnapshotEpoch,
    string ProcessContentRootFingerprint,
    int UnknownOwnershipViolations,
    int OwnerConflictViolations,
    int EscapedLeaseViolations,
    int CloseAfterUseViolations,
    int NormalSuppressions,
    int QuarantinedWrappers
);
