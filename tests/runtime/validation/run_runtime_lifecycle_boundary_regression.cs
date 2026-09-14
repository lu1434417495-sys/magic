using System;
using System.Collections.Generic;
using Godot;

public partial class run_runtime_lifecycle_boundary_regression : LifecycleTestSceneTree
{
    private static readonly LifetimeDomain[] PhaseTwoLeaseDomains =
    {
        LifetimeDomain.Request,
        LifetimeDomain.Battle,
        LifetimeDomain.SceneTree,
    };

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        AssertExactLegacyDebt();
        AssertLifecycleStopgapCountersAreZero();
        AssertProcessSnapshotBinding();
        AssertPhaseTwoLeaseDomainsReturnToBaseline();
        RequestTestExit(_test.Finish("Runtime lifecycle boundary regression"));
    }

    private void AssertExactLegacyDebt()
    {
        IReadOnlyList<LifecycleLegacyDebtSnapshot> debt =
            LifecycleAuditRegistry.Shared.CaptureSnapshot().LegacyDebt;
        _test.Eq(debt.Count, 0, "runtime permits no lifecycle legacy debt");
    }

    private void AssertLifecycleStopgapCountersAreZero()
    {
        LifecycleAuditSnapshot audit = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(
            audit.NormalPhaseSuppressCount,
            0L,
            "normal runtime paths perform no finalizer suppression"
        );
        _test.Eq(
            audit.QuarantineCount,
            0L,
            "runtime paths retain no quarantined wrapper graphs"
        );
    }

    private void AssertProcessSnapshotBinding()
    {
        ApplicationLifetimeCoordinator coordinator =
            Root.GetNodeOrNull<ApplicationLifetimeCoordinator>(
                "ApplicationLifetimeCoordinator"
            );
        GameSession session = Root.GetNodeOrNull<GameSession>("GameSession");
        _test.True(
            coordinator != null && session != null,
            "coordinator and canonical GameSession autoloads are active"
        );
        if (coordinator == null || session == null)
            return;

        ContentSnapshot publishedSnapshot = coordinator.ContentHost.GetSnapshot();
        _test.Eq(
            session.GetContentSnapshotEpoch(),
            publishedSnapshot.Epoch,
            "canonical GameSession borrows the published process snapshot epoch"
        );
        _test.True(
            ReferenceEquals(session.GetSkillDefinitionsTyped(), publishedSnapshot.Skills),
            "canonical GameSession reads the published skill definition index directly"
        );
        _test.True(
            coordinator.ContentHost.GetSnapshotBorrowerDiagnostics().Count > 0,
            "process content host records the attached session borrower"
        );
    }

    private void AssertPhaseTwoLeaseDomainsReturnToBaseline()
    {
        foreach (LifetimeDomain domain in PhaseTwoLeaseDomains)
        {
            LifecycleAuditSnapshot projectionBaseline =
                LifecycleAuditRegistry.Shared.CaptureSnapshot();
            using (
                GodotProjectionLease<Godot.Collections.Dictionary> lease =
                    RuntimePlainPayload.ProjectDictionaryLease(
                        new Dictionary<string, object>
                        {
                            ["domain"] = domain.ToString(),
                            ["nested"] = new List<object>
                            {
                                1L,
                                new Dictionary<string, object> { ["value"] = true },
                            },
                        },
                        $"phase-two-boundary-{domain}",
                        domain,
                        "cumulative projection vector"
                    )
            )
            {
                LifecycleAuditSnapshot active =
                    LifecycleAuditRegistry.Shared.CaptureSnapshot();
                _test.Eq(
                    active.ActiveLeaseCount,
                    projectionBaseline.ActiveLeaseCount + 1,
                    $"{domain} projection opens exactly one lease"
                );
                _test.True(
                    active.ActiveOwnerCount > projectionBaseline.ActiveOwnerCount,
                    $"{domain} projection explicitly owns its container graph"
                );
                _test.Eq(
                    active.ActiveScopeCount,
                    projectionBaseline.ActiveScopeCount,
                    $"{domain} projection does not expose its internal owner as a native scope"
                );
                int activeOwnerDelta =
                    active.ActiveOwnerCount - projectionBaseline.ActiveOwnerCount;
                AssertSingleDomainDelta(
                    projectionBaseline.ActiveProjectionLeaseCountsByDomain,
                    active.ActiveProjectionLeaseCountsByDomain,
                    domain,
                    1,
                    $"{domain} projection lease attribution"
                );
                AssertSingleDomainDelta(
                    projectionBaseline.ActiveOwnerCountsByDomain,
                    active.ActiveOwnerCountsByDomain,
                    domain,
                    activeOwnerDelta,
                    $"{domain} projection owner attribution"
                );
                AssertSingleDomainDelta(
                    projectionBaseline.ActiveCountsByDomain,
                    active.ActiveCountsByDomain,
                    domain,
                    activeOwnerDelta + 1,
                    $"{domain} projection aggregate attribution"
                );
            }
            AssertActiveVector(
                projectionBaseline,
                LifecycleAuditRegistry.Shared.CaptureSnapshot(),
                $"{domain} projection close"
            );

            LifecycleAuditSnapshot scopeBaseline =
                LifecycleAuditRegistry.Shared.CaptureSnapshot();
            using (var scope = new NativeLeaseScope($"phase-two-boundary-{domain}", domain))
            {
                scope.Own(
                    new Godot.Collections.Dictionary(),
                    "cumulative native owner vector"
                );
                LifecycleAuditSnapshot active =
                    LifecycleAuditRegistry.Shared.CaptureSnapshot();
                _test.Eq(
                    active.ActiveOwnerCount,
                    scopeBaseline.ActiveOwnerCount + 1,
                    $"{domain} native scope registers one explicit owner"
                );
                _test.Eq(
                    active.ActiveScopeCount,
                    scopeBaseline.ActiveScopeCount + 1,
                    $"{domain} native scope registers one scope"
                );
                AssertSingleDomainDelta(
                    scopeBaseline.ActiveOwnerCountsByDomain,
                    active.ActiveOwnerCountsByDomain,
                    domain,
                    1,
                    $"{domain} native owner attribution"
                );
                AssertSingleDomainDelta(
                    scopeBaseline.ActiveNativeScopeCountsByDomain,
                    active.ActiveNativeScopeCountsByDomain,
                    domain,
                    1,
                    $"{domain} native scope attribution"
                );
                AssertSingleDomainDelta(
                    scopeBaseline.ActiveCountsByDomain,
                    active.ActiveCountsByDomain,
                    domain,
                    2,
                    $"{domain} native aggregate attribution"
                );
            }
            AssertActiveVector(
                scopeBaseline,
                LifecycleAuditRegistry.Shared.CaptureSnapshot(),
                $"{domain} native scope close"
            );
        }
    }

    private void AssertSingleDomainDelta(
        IReadOnlyDictionary<string, int> baseline,
        IReadOnlyDictionary<string, int> active,
        LifetimeDomain targetDomain,
        int expectedTargetDelta,
        string label
    )
    {
        string target = targetDomain.ToString();
        var domains = new HashSet<string>(baseline.Keys, StringComparer.Ordinal);
        domains.UnionWith(active.Keys);
        domains.Add(target);

        foreach (string domain in domains)
        {
            int baselineCount = DomainCount(baseline, domain);
            int expectedCount =
                baselineCount + (string.Equals(domain, target, StringComparison.Ordinal)
                    ? expectedTargetDelta
                    : 0);
            _test.Eq(
                DomainCount(active, domain),
                expectedCount,
                $"{label}: {domain}"
            );
        }
    }

    private static int DomainCount(
        IReadOnlyDictionary<string, int> counts,
        string domain
    ) => counts.TryGetValue(domain, out int count) ? count : 0;

    private void AssertActiveVector(
        LifecycleAuditSnapshot expected,
        LifecycleAuditSnapshot actual,
        string label
    )
    {
        _test.Eq(actual.ActiveContentBorrowerCount, expected.ActiveContentBorrowerCount, $"{label}: borrowers");
        _test.Eq(actual.ActiveOwnerCount, expected.ActiveOwnerCount, $"{label}: owners");
        _test.Eq(actual.ActiveLeaseCount, expected.ActiveLeaseCount, $"{label}: leases");
        _test.Eq(actual.ActiveScopeCount, expected.ActiveScopeCount, $"{label}: scopes");
        _test.Eq(actual.ActiveJobCount, expected.ActiveJobCount, $"{label}: jobs");
        _test.Eq(actual.ViolationCount, expected.ViolationCount, $"{label}: violations");
        _test.Eq(
            actual.NormalPhaseSuppressCount,
            expected.NormalPhaseSuppressCount,
            $"{label}: normal suppressions"
        );
        _test.Eq(actual.QuarantineCount, expected.QuarantineCount, $"{label}: quarantine");
        _test.Eq(
            actual.ActiveCountsByDomain.Count,
            expected.ActiveCountsByDomain.Count,
            $"{label}: active domain count"
        );
        foreach (KeyValuePair<string, int> entry in expected.ActiveCountsByDomain)
        {
            _test.True(
                actual.ActiveCountsByDomain.TryGetValue(entry.Key, out int actualCount),
                $"{label}: active domain remains present: {entry.Key}"
            );
            if (actual.ActiveCountsByDomain.TryGetValue(entry.Key, out actualCount))
                _test.Eq(actualCount, entry.Value, $"{label}: active domain {entry.Key}");
        }
    }
}
