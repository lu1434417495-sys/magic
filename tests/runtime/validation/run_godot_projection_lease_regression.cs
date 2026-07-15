using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_godot_projection_lease_regression : LifecycleTestSceneTree
{
    private sealed class BorrowedDisposable : IDisposable
    {
        internal int DisposeCount { get; private set; }
        public void Dispose() => DisposeCount++;
    }

    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        var borrowed = new BorrowedDisposable();
        var root = new GDictionary();
        var lease = GodotProjectionLease<GDictionary>.CreateOwnedRoot(
            root,
            "projection-regression",
            LifetimeDomain.Request,
            "root"
        );
        GArray nestedArray = lease.Own(new GArray(), "nested-array");
        GDictionary nestedDictionary = lease.Own(new GDictionary(), "nested-dictionary");
        nestedArray.Add(nestedDictionary);
        lease.Value["nested"] = nestedArray;
        _test.True(ReferenceEquals(lease.Borrow(borrowed, "borrowed-child"), borrowed), "borrow returns the same instance");

        LifecycleAuditSnapshot active = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(active.ActiveOwnerCount, baseline.ActiveOwnerCount + 3, "root and explicit nested wrappers are owned");
        _test.Eq(active.ActiveLeaseCount, baseline.ActiveLeaseCount + 1, "projection lease is audited");
        _test.Eq(active.ActiveContentBorrowerCount, baseline.ActiveContentBorrowerCount + 1, "borrower is audited weakly");
        _test.Eq(active.ActiveScopeCount, baseline.ActiveScopeCount, "projection does not add a native scope diagnostic");

        _test.True(
            Throws<InvalidOperationException>(() => lease.Own(nestedArray, "duplicate")),
            "duplicate explicit ownership is rejected without partial mutation"
        );
        lease.Dispose();
        lease.Dispose();
        _test.Eq(borrowed.DisposeCount, 0, "borrowed child is never disposed by the lease");
        _test.True(Throws<ObjectDisposedException>(() => _ = lease.Value), "Value fails after close");
        var rejectedAfterClose = new GArray();
        _test.True(
            Throws<ObjectDisposedException>(() => lease.Own(rejectedAfterClose, "closed")),
            "Own fails after close"
        );
        _test.True(
            Throws<ObjectDisposedException>(() => lease.Borrow(borrowed, "closed")),
            "Borrow fails after close"
        );
        rejectedAfterClose.Dispose();

        LifecycleAuditSnapshot after = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(after.ActiveOwnerCount, baseline.ActiveOwnerCount, "projection owners return to baseline");
        _test.Eq(after.ActiveLeaseCount, baseline.ActiveLeaseCount, "projection lease returns to baseline");
        _test.Eq(after.ActiveScopeCount, baseline.ActiveScopeCount, "scope count returns to baseline");
        _test.Eq(after.ActiveContentBorrowerCount, baseline.ActiveContentBorrowerCount, "borrower count returns to baseline");
        RequestTestExit(_test.Finish("Godot projection lease regression"));
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }
}
