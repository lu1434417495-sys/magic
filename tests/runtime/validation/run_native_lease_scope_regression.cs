using System;
using System.Collections.Generic;
using Godot;

public partial class run_native_lease_scope_regression : LifecycleTestSceneTree
{
    private sealed class TrackingDisposable : IDisposable
    {
        private readonly string _id;
        private readonly List<string> _order;

        internal TrackingDisposable(string id, List<string> order)
        {
            _id = id;
            _order = order;
        }

        internal int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
            _order.Add(_id);
        }
    }

    private readonly TestHarness _test = new();

    public override void _Initialize() => CallDeferred(nameof(Run));

    private void Run()
    {
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        var order = new List<string>();
        var source = new NativeLeaseScope("native-regression-source", LifetimeDomain.Request);
        var target = new NativeLeaseScope("native-regression-target", LifetimeDomain.Battle);
        var first = source.Own(new TrackingDisposable("first", order), "first");
        var second = source.Own(new TrackingDisposable("second", order), "second");
        var third = source.Own(new TrackingDisposable("third", order), "third");

        _test.True(
            Throws<InvalidOperationException>(() => source.Own(second, "duplicate")),
            "duplicate acquisition by the same scope is rejected"
        );

        _test.True(
            Throws<InvalidOperationException>(() => target.Own(first, "duplicate")),
            "cross-owner acquisition is rejected"
        );
        source.TransferTo(target, first, "handoff");
        LifecycleAuditSnapshot transferred = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(
            DomainCount(transferred, LifetimeDomain.Request),
            DomainCount(baseline, LifetimeDomain.Request) + 3,
            "source domain retains its scope and remaining owner after transfer"
        );
        _test.Eq(
            DomainCount(transferred, LifetimeDomain.Battle),
            DomainCount(baseline, LifetimeDomain.Battle) + 2,
            "target domain contains its scope and transferred owner"
        );
        source.Dispose();
        _test.True(source.IsClosed, "disposed native scope reports closed");
        _test.True(
            Throws<ObjectDisposedException>(
                () => source.Own(new TrackingDisposable("closed", order), "closed")
            ),
            "closed native scope rejects new ownership"
        );
        _test.Eq(second.DisposeCount, 1, "source-owned wrapper is disposed once");
        _test.Eq(third.DisposeCount, 1, "last source-owned wrapper is disposed once");
        _test.Eq(first.DisposeCount, 0, "transferred wrapper survives source disposal");
        target.Dispose();
        target.Dispose();
        _test.Eq(first.DisposeCount, 1, "transferred wrapper is disposed once by target");
        _test.Eq(string.Join(",", order), "third,second,first", "scopes dispose owned wrappers in reverse registration order");

        var rejectionScope = new NativeLeaseScope("native-rejections", LifetimeDomain.SceneTree);
        var node = new Node();
        _test.True(
            Throws<InvalidOperationException>(() => rejectionScope.Own(node, "node")),
            "Node ownership is rejected before retention"
        );
        node.Dispose();

        Resource pathBacked = GD.Load<Resource>(
            "res://data/configs/age_profiles/dragonborn_age_profile.tres"
        );
        _test.True(pathBacked != null, "path-backed fixture loads");
        _test.True(
            Throws<InvalidOperationException>(() => rejectionScope.Own(pathBacked, "path")),
            "path-backed Resource ownership is rejected before retention"
        );
        var legacyScope = new GodotTransientResourceScope("native-regression-legacy");
        Godot.Collections.Array legacyOwned = legacyScope.OwnWrapper(
            new Godot.Collections.Array(),
            "old-scope"
        );
        _test.True(
            Throws<InvalidOperationException>(
                () => rejectionScope.Own(legacyOwned, "old-scope-conflict")
            ),
            "legacy-scope ownership conflicts with a native lease"
        );
        legacyScope.Dispose();
        _test.True(
            Throws<ArgumentOutOfRangeException>(
                () => new NativeLeaseScope("invalid-domain", LifetimeDomain.ProcessContent)
            ),
            "process-content ownership cannot be created through a native lease"
        );
        rejectionScope.Dispose();

        LifecycleAuditSnapshot after = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(after.ActiveOwnerCount, baseline.ActiveOwnerCount, "owner audit returns to baseline");
        _test.Eq(after.ActiveScopeCount, baseline.ActiveScopeCount, "scope audit returns to baseline");
        _test.Eq(after.ActiveLeaseCount, baseline.ActiveLeaseCount, "lease audit remains at baseline");
        _test.True(after.TransferredCount > baseline.TransferredCount, "transfer is recorded");
        RequestTestExit(_test.Finish("Native lease scope regression"));
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

    private static int DomainCount(LifecycleAuditSnapshot snapshot, LifetimeDomain domain) =>
        snapshot.ActiveCountsByDomain.TryGetValue(domain.ToString(), out int count) ? count : 0;
}
