using System;
using Godot;

public partial class run_lifecycle_audit_activity_snapshot_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => CallDeferred(nameof(Run));

    private void Run()
    {
        var registry = new LifecycleAuditRegistry();
        object genericOwner = new();
        object nativeWrapper = new();
        object projectionContainer = new();
        object borrower = new();
        object lease = new();
        object scope = new();
        object job = new();

        registry.RegisterActive(
            LifecycleAuditActiveKind.Owner,
            "session-owner",
            "Session",
            genericOwner
        );
        registry.RegisterActive(
            LifecycleAuditActiveKind.Owner,
            "native-owner:Request:native-probe:1",
            "Request",
            nativeWrapper
        );
        registry.RegisterActive(
            LifecycleAuditActiveKind.Owner,
            "native-owner:Battle:projection:probe:2:3",
            "Battle",
            projectionContainer
        );
        registry.RegisterActive(
            LifecycleAuditActiveKind.ContentBorrower,
            "content-borrower:Session:probe",
            "Session",
            borrower
        );
        registry.RegisterActive(
            LifecycleAuditActiveKind.Lease,
            "projection-lease:Request:probe",
            "Request",
            lease
        );
        registry.RegisterActive(
            LifecycleAuditActiveKind.Scope,
            "native-scope:Battle:probe",
            "Battle",
            scope
        );
        registry.RegisterActive(
            LifecycleAuditActiveKind.Job,
            "job:Decision:probe",
            "Decision",
            job
        );

        LifecycleAuditSnapshot active = registry.CaptureSnapshot();
        _test.Eq(active.Activity.OwnersRegistered, 7L, "successful records are registered");
        _test.Eq(active.Activity.OwnersClosed, 0L, "no record is closed before teardown");
        _test.Eq(active.Activity.NativeWrappersOwned, 2L, "native wrappers are classified");
        _test.Eq(
            active.Activity.ProjectionContainersOwned,
            1L,
            "projection containers are classified as the native-owner subset"
        );
        _test.Eq(
            DomainCount(active.ActiveOwnerCountsByDomain, "Session"),
            1,
            "session owner count is exact"
        );
        _test.Eq(
            DomainCount(active.ActiveOwnerCountsByDomain, "Request"),
            1,
            "request owner count is exact"
        );
        _test.Eq(
            DomainCount(active.ActiveOwnerCountsByDomain, "Battle"),
            1,
            "battle owner count is exact"
        );
        _test.Eq(
            DomainCount(active.ActiveContentBorrowerCountsByDomain, "Session"),
            1,
            "content borrower domain count is exact"
        );
        _test.Eq(
            DomainCount(active.ActiveProjectionLeaseCountsByDomain, "Request"),
            1,
            "projection lease domain count is exact"
        );
        _test.Eq(
            DomainCount(active.ActiveNativeScopeCountsByDomain, "Battle"),
            1,
            "native scope domain count is exact"
        );
        _test.Eq(
            DomainCount(active.ActiveJobCountsByDomain, "Decision"),
            1,
            "job domain count is exact"
        );

        LifecycleAuditActivitySnapshot beforeRollback = active.Activity;
        _test.True(
            registry.TryTransferActiveDomain(
                LifecycleAuditActiveKind.Owner,
                "native-owner:Request:native-probe:1",
                "Request",
                "Battle",
                out string transferFailure
            ),
            $"audit domain transfer succeeds: {transferFailure}"
        );
        _test.True(
            registry.TryTransferActiveDomain(
                LifecycleAuditActiveKind.Owner,
                "native-owner:Request:native-probe:1",
                "Battle",
                "Request",
                out string rollbackFailure
            ),
            $"audit domain transfer rollback succeeds: {rollbackFailure}"
        );
        LifecycleAuditSnapshot rolledBack = registry.CaptureSnapshot();
        _test.Eq(
            rolledBack.Activity.TransfersOut,
            beforeRollback.TransfersOut,
            "audit-only rollback does not count as a completed transfer out"
        );
        _test.Eq(
            rolledBack.Activity.TransfersIn,
            beforeRollback.TransfersIn,
            "audit-only rollback does not count as a completed transfer in"
        );

        registry.RecordTransferred();
        LifecycleAuditSnapshot transferred = registry.CaptureSnapshot();
        _test.Eq(transferred.Activity.TransfersOut, 1L, "completed transfer out is monotonic");
        _test.Eq(transferred.Activity.TransfersIn, 1L, "completed transfer in is monotonic");

        AssertStrictFailureDoesNotChangeActivity(
            registry,
            () => registry.RegisterActive(
                LifecycleAuditActiveKind.Owner,
                "session-owner",
                "Session",
                new object()
            ),
            "duplicate registration"
        );
        AssertStrictFailureDoesNotChangeActivity(
            registry,
            () => registry.UnregisterActive(
                LifecycleAuditActiveKind.Owner,
                "session-owner",
                "Battle"
            ),
            "metadata-mismatched close"
        );

        registry.UnregisterActive(
            LifecycleAuditActiveKind.Owner,
            "session-owner",
            "Session"
        );
        registry.UnregisterActive(
            LifecycleAuditActiveKind.Owner,
            "native-owner:Request:native-probe:1",
            "Request"
        );
        registry.UnregisterActive(
            LifecycleAuditActiveKind.Owner,
            "native-owner:Battle:projection:probe:2:3",
            "Battle"
        );
        registry.UnregisterActive(
            LifecycleAuditActiveKind.ContentBorrower,
            "content-borrower:Session:probe",
            "Session"
        );
        registry.UnregisterActive(
            LifecycleAuditActiveKind.Lease,
            "projection-lease:Request:probe",
            "Request"
        );
        registry.UnregisterActive(
            LifecycleAuditActiveKind.Scope,
            "native-scope:Battle:probe",
            "Battle"
        );
        registry.UnregisterActive(
            LifecycleAuditActiveKind.Job,
            "job:Decision:probe",
            "Decision"
        );

        LifecycleAuditSnapshot drained = registry.CaptureSnapshot();
        _test.Eq(drained.Activity.OwnersRegistered, 7L, "failed registration is excluded");
        _test.Eq(drained.Activity.OwnersClosed, 7L, "only successful closes are counted");
        _test.Eq(drained.Activity.NativeWrappersOwned, 2L, "native-owned total is stable");
        _test.Eq(
            drained.Activity.NativeWrappersDisposed,
            2L,
            "native-disposed total balances owned"
        );
        _test.Eq(
            drained.Activity.ProjectionContainersOwned,
            1L,
            "projection-owned total is stable"
        );
        _test.Eq(
            drained.Activity.ProjectionContainersDisposed,
            1L,
            "projection-disposed total balances owned"
        );
        _test.Eq(drained.ActiveOwnerCount, 0, "owners drain to zero");
        _test.Eq(drained.ActiveContentBorrowerCount, 0, "borrowers drain to zero");
        _test.Eq(drained.ActiveLeaseCount, 0, "leases drain to zero");
        _test.Eq(drained.ActiveScopeCount, 0, "scopes drain to zero");
        _test.Eq(drained.ActiveJobCount, 0, "jobs drain to zero");

        long violationBaseline = drained.ViolationCount;
        LifecycleAuditActivitySnapshot activityBeforeViolations = drained.Activity;
        _test.True(
            RunStrict(() => registry.RecordOwnerConflict("owner conflict probe")),
            "owner conflict is rejected in strict mode"
        );
        _test.True(
            RunStrict(() => registry.RecordCloseAfterUse("close-after-use probe")),
            "close-after-use is rejected in strict mode"
        );
        LifecycleAuditSnapshot violated = registry.CaptureSnapshot();
        _test.Eq(violated.OwnerConflictCount, 1L, "owner conflict count is monotonic");
        _test.Eq(violated.CloseAfterUseCount, 1L, "close-after-use count is monotonic");
        _test.Eq(
            violated.ViolationCount,
            violationBaseline + 2,
            "specialized violations contribute to the aggregate count"
        );
        _test.Eq(
            violated.Activity,
            activityBeforeViolations,
            "violation reporting does not mutate activity totals"
        );

        RequestTestExit(_test.Finish("Lifecycle audit activity snapshot regression"));
    }

    private void AssertStrictFailureDoesNotChangeActivity(
        LifecycleAuditRegistry registry,
        Action action,
        string label
    )
    {
        LifecycleAuditActivitySnapshot before = registry.CaptureSnapshot().Activity;
        string previousStrict = System.Environment.GetEnvironmentVariable(
            "MAGIC_LIFECYCLE_STRICT"
        );
        bool rejected = false;
        try
        {
            System.Environment.SetEnvironmentVariable("MAGIC_LIFECYCLE_STRICT", "1");
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(
                "MAGIC_LIFECYCLE_STRICT",
                previousStrict
            );
        }

        LifecycleAuditActivitySnapshot after = registry.CaptureSnapshot().Activity;
        _test.True(rejected, $"{label} is rejected in strict mode");
        _test.Eq(after, before, $"{label} does not change activity totals");
    }

    private static int DomainCount(
        System.Collections.Generic.IReadOnlyDictionary<string, int> counts,
        string domain
    ) => counts.TryGetValue(domain, out int count) ? count : 0;

    private static bool RunStrict(Action action)
    {
        string previousStrict = System.Environment.GetEnvironmentVariable(
            "MAGIC_LIFECYCLE_STRICT"
        );
        try
        {
            System.Environment.SetEnvironmentVariable("MAGIC_LIFECYCLE_STRICT", "1");
            try
            {
                action();
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(
                "MAGIC_LIFECYCLE_STRICT",
                previousStrict
            );
        }
    }
}
