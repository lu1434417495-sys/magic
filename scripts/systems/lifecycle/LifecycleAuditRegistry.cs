using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

internal enum LifecycleAuditActiveKind
{
    ContentBorrower = 0,
    Owner,
    Lease,
    Scope,
    Job,
}

internal sealed record LifecycleLegacyDebtSnapshot(
    string DebtId,
    string Source,
    string OwnerDomain,
    int DeletePhase
);

internal sealed record LifecycleWeakDiagnosticSnapshot(
    string DiagnosticId,
    string Kind,
    string OwnerDomain,
    bool IsAlive
);

internal sealed record LifecycleProcessContentRootSnapshot(
    string CanonicalPath,
    string TypeName,
    bool IsAlive
);

internal sealed record LifecycleShutdownPhaseAuditSnapshot(
    ApplicationShutdownPhase Phase,
    TimeSpan Duration,
    string Failure
);

internal sealed record LifecycleAuditSnapshot(
    int ProcessContentRootCount,
    long ActiveContentSnapshotEpoch,
    int ActiveContentBorrowerCount,
    int ActiveOwnerCount,
    int ActiveLeaseCount,
    int ActiveScopeCount,
    int ActiveJobCount,
    long CreatedCount,
    long DisposedCount,
    long TransferredCount,
    long EscapedCount,
    long UnknownCount,
    long ViolationCount,
    long NormalPhaseSuppressCount,
    long QuarantineCount,
    IReadOnlyDictionary<string, int> ActiveCountsByDomain,
    IReadOnlyList<LifecycleWeakDiagnosticSnapshot> WeakDiagnostics,
    IReadOnlyList<LifecycleProcessContentRootSnapshot> ProcessContentRoots,
    IReadOnlyList<LifecycleLegacyDebtSnapshot> LegacyDebt,
    IReadOnlyList<LifecycleShutdownPhaseAuditSnapshot> ShutdownPhases
)
{
    internal int NonTerminalCount =>
        ActiveContentBorrowerCount
        + ActiveOwnerCount
        + ActiveLeaseCount
        + ActiveScopeCount
        + ActiveJobCount;

    internal bool HasLifecycleViolation => ViolationCount != 0;
}

internal sealed class LifecycleAuditRegistry
{
    private sealed record ActiveDiagnostic(
        LifecycleAuditActiveKind Kind,
        string OwnerDomain,
        WeakReference<object> Target
    );

    private sealed record ProcessContentRootDiagnostic(
        Type Type,
        WeakReference<object> Target
    );

    private readonly object _sync = new();
    private readonly Dictionary<string, ActiveDiagnostic> _activeDiagnostics =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProcessContentRootDiagnostic> _processContentRoots =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, LifecycleLegacyDebtSnapshot> _legacyDebt =
        new(StringComparer.Ordinal);
    private readonly List<LifecycleShutdownPhaseAuditSnapshot> _shutdownPhases = new();
    private long _activeContentSnapshotEpoch;
    private long _createdCount;
    private long _disposedCount;
    private long _transferredCount;
    private long _escapedCount;
    private long _unknownCount;
    private long _violationCount;
    private long _normalPhaseSuppressCount;
    private long _quarantineCount;

    internal static LifecycleAuditRegistry Shared { get; } = new();

    internal void RegisterActive(
        LifecycleAuditActiveKind kind,
        string diagnosticId,
        string ownerDomain,
        object target
    )
    {
        if (!ValidateDiagnostic(diagnosticId, ownerDomain, target))
            return;

        lock (_sync)
        {
            if (_activeDiagnostics.ContainsKey(diagnosticId))
            {
                ReportViolation(
                    $"Lifecycle diagnostic id is already active. id={diagnosticId}"
                );
                return;
            }

            _activeDiagnostics.Add(
                diagnosticId,
                new ActiveDiagnostic(kind, ownerDomain, new WeakReference<object>(target))
            );
            _createdCount++;
        }
    }

    internal void UnregisterActive(
        LifecycleAuditActiveKind kind,
        string diagnosticId,
        string ownerDomain
    )
    {
        lock (_sync)
        {
            if (!_activeDiagnostics.TryGetValue(diagnosticId, out ActiveDiagnostic diagnostic))
            {
                ReportViolation(
                    $"Lifecycle diagnostic id is not active. id={diagnosticId}"
                );
                return;
            }

            if (diagnostic.Kind != kind || diagnostic.OwnerDomain != ownerDomain)
            {
                ReportViolation(
                    "Lifecycle diagnostic unregister metadata does not match. "
                        + $"id={diagnosticId}, expected_kind={diagnostic.Kind}, actual_kind={kind}, "
                        + $"expected_domain={diagnostic.OwnerDomain}, actual_domain={ownerDomain}"
                );
                return;
            }

            _activeDiagnostics.Remove(diagnosticId);
            _disposedCount++;
        }
    }

    internal void RegisterProcessContentRoot(string canonicalPath, Type type, object target)
    {
        if (string.IsNullOrWhiteSpace(canonicalPath))
        {
            ReportViolation("Process content root canonical path is required.");
            return;
        }
        if (type == null)
        {
            ReportViolation(
                $"Process content root type is required. path={canonicalPath}"
            );
            return;
        }
        if (target == null)
        {
            ReportViolation(
                $"Process content root target is required. path={canonicalPath}"
            );
            return;
        }

        lock (_sync)
        {
            if (_processContentRoots.ContainsKey(canonicalPath))
            {
                ReportViolation(
                    $"Process content root is already registered. path={canonicalPath}"
                );
                return;
            }

            _processContentRoots.Add(
                canonicalPath,
                new ProcessContentRootDiagnostic(type, new WeakReference<object>(target))
            );
            _createdCount++;
        }
    }

    internal void ReleaseProcessContentRoot(string canonicalPath)
    {
        lock (_sync)
        {
            if (!_processContentRoots.Remove(canonicalPath))
            {
                ReportViolation(
                    $"Process content root is not registered. path={canonicalPath}"
                );
                return;
            }

            _disposedCount++;
        }
    }

    internal void SetActiveContentSnapshotEpoch(long epoch)
    {
        if (epoch <= 0)
        {
            ReportViolation($"Content snapshot epoch must be positive. epoch={epoch}");
            return;
        }

        lock (_sync)
            _activeContentSnapshotEpoch = epoch;
    }

    internal void ClearActiveContentSnapshotEpoch()
    {
        lock (_sync)
            _activeContentSnapshotEpoch = 0;
    }

    internal void RecordTransferred()
    {
        lock (_sync)
            _transferredCount++;
    }

    internal void RecordEscaped(string diagnostic)
    {
        lock (_sync)
        {
            _escapedCount++;
            _violationCount++;
        }
        LifecycleViolation.Report(
            string.IsNullOrWhiteSpace(diagnostic)
                ? "Lifecycle object escaped its owner."
                : diagnostic
        );
    }

    internal void RecordUnknown(string diagnostic)
    {
        lock (_sync)
        {
            _unknownCount++;
            _violationCount++;
        }
        LifecycleViolation.Report(
            string.IsNullOrWhiteSpace(diagnostic)
                ? "Lifecycle object has unknown ownership."
                : diagnostic
        );
    }

    internal void RecordNormalPhaseSuppress()
    {
        lock (_sync)
            _normalPhaseSuppressCount++;
    }

    internal void RecordQuarantine()
    {
        lock (_sync)
            _quarantineCount++;
    }

    internal void RegisterLegacyDebt(LifecycleLegacyDebtSnapshot debt)
    {
        if (!ValidateLegacyDebt(debt))
            return;

        lock (_sync)
        {
            if (_legacyDebt.ContainsKey(debt.DebtId))
            {
                ReportViolation(
                    $"Legacy lifecycle debt id is already registered. debt_id={debt.DebtId}"
                );
                return;
            }

            _legacyDebt.Add(debt.DebtId, debt);
        }
    }

    internal void RecordShutdownPhase(
        ApplicationShutdownPhase phase,
        TimeSpan duration,
        Exception failure = null
    )
    {
        lock (_sync)
        {
            _shutdownPhases.Add(
                new LifecycleShutdownPhaseAuditSnapshot(
                    phase,
                    duration,
                    failure?.ToString() ?? string.Empty
                )
            );
        }
    }

    internal LifecycleAuditSnapshot CaptureSnapshot()
    {
        lock (_sync)
        {
            var domainCounts = _activeDiagnostics
                .Values.GroupBy(diagnostic => diagnostic.OwnerDomain, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

            LifecycleWeakDiagnosticSnapshot[] weakDiagnostics = _activeDiagnostics
                .Select(entry =>
                    new LifecycleWeakDiagnosticSnapshot(
                        entry.Key,
                        entry.Value.Kind.ToString(),
                        entry.Value.OwnerDomain,
                        entry.Value.Target.TryGetTarget(out _)
                    )
                )
                .Concat(
                    _processContentRoots.Select(entry =>
                        new LifecycleWeakDiagnosticSnapshot(
                            entry.Key,
                            "ProcessContentRoot",
                            "ProcessContent",
                            entry.Value.Target.TryGetTarget(out _)
                        )
                    )
                )
                .OrderBy(entry => entry.DiagnosticId, StringComparer.Ordinal)
                .ToArray();

            LifecycleProcessContentRootSnapshot[] contentRoots = _processContentRoots
                .Select(entry =>
                    new LifecycleProcessContentRootSnapshot(
                        entry.Key,
                        entry.Value.Type.FullName ?? entry.Value.Type.Name,
                        entry.Value.Target.TryGetTarget(out _)
                    )
                )
                .OrderBy(entry => entry.CanonicalPath, StringComparer.Ordinal)
                .ToArray();

            return new LifecycleAuditSnapshot(
                _processContentRoots.Count,
                _activeContentSnapshotEpoch,
                CountActive(LifecycleAuditActiveKind.ContentBorrower),
                CountActive(LifecycleAuditActiveKind.Owner),
                CountActive(LifecycleAuditActiveKind.Lease),
                CountActive(LifecycleAuditActiveKind.Scope),
                CountActive(LifecycleAuditActiveKind.Job),
                _createdCount,
                _disposedCount,
                _transferredCount,
                _escapedCount,
                _unknownCount,
                _violationCount,
                _normalPhaseSuppressCount,
                _quarantineCount,
                new ReadOnlyDictionary<string, int>(domainCounts),
                weakDiagnostics,
                contentRoots,
                _legacyDebt.Values.OrderBy(debt => debt.DebtId, StringComparer.Ordinal).ToArray(),
                _shutdownPhases.ToArray()
            );
        }
    }

    private int CountActive(LifecycleAuditActiveKind kind)
    {
        return _activeDiagnostics.Values.Count(diagnostic => diagnostic.Kind == kind);
    }

    private bool ValidateDiagnostic(string diagnosticId, string ownerDomain, object target)
    {
        if (string.IsNullOrWhiteSpace(diagnosticId))
        {
            ReportViolation("Lifecycle diagnostic id is required.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(ownerDomain))
        {
            ReportViolation(
                $"Lifecycle diagnostic owner domain is required. id={diagnosticId}"
            );
            return false;
        }
        if (target == null)
        {
            ReportViolation($"Lifecycle diagnostic target is required. id={diagnosticId}");
            return false;
        }
        return true;
    }

    private bool ValidateLegacyDebt(LifecycleLegacyDebtSnapshot debt)
    {
        if (debt == null)
        {
            ReportViolation("Legacy lifecycle debt metadata is required.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(debt.DebtId))
        {
            ReportViolation("Legacy lifecycle debt id is required.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(debt.Source))
        {
            ReportViolation(
                $"Legacy lifecycle debt source is required. debt_id={debt.DebtId}"
            );
            return false;
        }
        if (string.IsNullOrWhiteSpace(debt.OwnerDomain))
        {
            ReportViolation(
                $"Legacy lifecycle debt owner domain is required. debt_id={debt.DebtId}"
            );
            return false;
        }
        if (debt.DeletePhase <= 0)
        {
            ReportViolation(
                $"Legacy lifecycle debt delete phase must be positive. debt_id={debt.DebtId}"
            );
            return false;
        }
        return true;
    }

    private void ReportViolation(string message)
    {
        lock (_sync)
            _violationCount++;
        LifecycleViolation.Report(message);
    }
}
