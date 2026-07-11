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

    private sealed partial class FactoryProbeAction : EnemyAiAction
    {
        internal static int ConstructionCount { get; private set; }

        public FactoryProbeAction()
        {
            ConstructionCount++;
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

        AssertNativeFactoryFailureCleanup(baseline);

        LifecycleAuditSnapshot after = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(after.ActiveOwnerCount, baseline.ActiveOwnerCount, "owner audit returns to baseline");
        _test.Eq(after.ActiveScopeCount, baseline.ActiveScopeCount, "scope audit returns to baseline");
        _test.Eq(after.ActiveLeaseCount, baseline.ActiveLeaseCount, "lease audit remains at baseline");
        _test.True(after.TransferredCount > baseline.TransferredCount, "transfer is recorded");
        RequestTestExit(_test.Finish("Native lease scope regression"));
    }

    private void AssertNativeFactoryFailureCleanup(LifecycleAuditSnapshot baseline)
    {
        var configureFailureScope = new NativeLeaseScope(
            "native-factory-configure-failure",
            LifetimeDomain.Request
        );
        var configureFailureFactory = new RuntimeEnemyAiResourceFactory(
            configureFailureScope,
            "native-factory-regression"
        );
        FactoryProbeAction configuredAction = null;
        _test.True(
            Throws<InvalidOperationException>(
                () =>
                    configureFailureFactory.NewAction<FactoryProbeAction>(
                        action =>
                        {
                            configuredAction = action;
                            throw new InvalidOperationException("expected configure failure");
                        },
                        "configure-failure"
                    )
            ),
            "native factory propagates configure failures"
        );
        _test.True(
            configuredAction != null && !GodotObject.IsInstanceValid(configuredAction),
            "native factory disposes a newly created Resource when configuration fails"
        );
        configureFailureScope.Dispose();

        var configureCloseScope = new NativeLeaseScope(
            "native-factory-configure-close",
            LifetimeDomain.Request
        );
        var configureCloseFactory = new RuntimeEnemyAiResourceFactory(
            configureCloseScope,
            "native-factory-regression"
        );
        FactoryProbeAction closeRaceAction = null;
        _test.True(
            Throws<ObjectDisposedException>(
                () =>
                    configureCloseFactory.NewAction<FactoryProbeAction>(
                        action =>
                        {
                            closeRaceAction = action;
                            configureCloseScope.Dispose();
                        },
                        "configure-close"
                    )
            ),
            "native factory reports a scope closed during configuration"
        );
        _test.True(
            closeRaceAction != null && !GodotObject.IsInstanceValid(closeRaceAction),
            "native factory disposes an unclaimed Resource when its scope closes during configuration"
        );

        var selfDisposedScope = new NativeLeaseScope(
            "native-factory-self-disposed",
            LifetimeDomain.Request
        );
        var selfDisposedFactory = new RuntimeEnemyAiResourceFactory(
            selfDisposedScope,
            "native-factory-regression"
        );
        FactoryProbeAction selfDisposedAction = null;
        _test.True(
            Throws<InvalidOperationException>(
                () =>
                    selfDisposedFactory.NewAction<FactoryProbeAction>(
                        action =>
                        {
                            selfDisposedAction = action;
                            action.Dispose();
                            throw new InvalidOperationException("expected self-dispose failure");
                        },
                        "self-disposed"
                    )
            ),
            "native factory preserves a configure failure after the Resource was already disposed"
        );
        _test.True(
            selfDisposedAction != null && !GodotObject.IsInstanceValid(selfDisposedAction),
            "native factory does not revive or re-own a Resource disposed during configuration"
        );
        selfDisposedScope.Dispose();

        var closedScope = new NativeLeaseScope(
            "native-factory-closed",
            LifetimeDomain.Request
        );
        var closedFactory = new RuntimeEnemyAiResourceFactory(
            closedScope,
            "native-factory-regression"
        );
        closedScope.Dispose();
        int constructionsBeforeClosedCall = FactoryProbeAction.ConstructionCount;
        _test.True(
            Throws<ObjectDisposedException>(
                () =>
                    closedFactory.NewAction<FactoryProbeAction>(
                        configure: null,
                        reason: "closed"
                    )
            ),
            "native factory rejects creation after its scope closes"
        );
        _test.Eq(
            FactoryProbeAction.ConstructionCount,
            constructionsBeforeClosedCall,
            "closed native factory rejects before allocating a Resource"
        );

        var externalAction = new FactoryProbeAction();
        _test.True(
            Throws<ObjectDisposedException>(
                () => closedFactory.OwnAction(externalAction, "closed-external")
            ),
            "closed native factory rejects an externally supplied action"
        );
        _test.True(
            GodotObject.IsInstanceValid(externalAction),
            "failed OwnAction leaves the externally supplied action alive"
        );
        externalAction.Dispose();

        LifecycleAuditSnapshot afterFactoryFailures =
            LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(
            afterFactoryFailures.ActiveOwnerCount,
            baseline.ActiveOwnerCount,
            "failed native factory creation leaves no active owner"
        );
        _test.Eq(
            afterFactoryFailures.ActiveScopeCount,
            baseline.ActiveScopeCount,
            "failed native factory creation returns scope audit to baseline"
        );
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
