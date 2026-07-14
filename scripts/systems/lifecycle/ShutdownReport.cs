using System;
using System.Collections.Generic;
using System.Linq;

internal sealed record ShutdownDuplicateRequestDiagnostic(
    int RequestedExitCode,
    ShutdownReason Reason,
    string CallerLabel,
    bool? CallerPassed
);

internal sealed record ShutdownFailure(string Stage, string Message, string ExceptionType);

internal sealed class ShutdownReport
{
    private readonly object _sync = new();
    private readonly ApplicationShutdownStateMachine _stateMachine = new();
    private readonly List<ApplicationShutdownPhase> _phaseHistory = new()
    {
        ApplicationShutdownPhase.Running,
    };
    private readonly List<ShutdownDuplicateRequestDiagnostic> _duplicateRequestDiagnostics =
        new();
    private readonly List<ShutdownFailure> _failures = new();
    private readonly List<LifecycleLegacyDebtSnapshot> _legacyDebt = new();
    private int _effectiveExitCode;
    private bool _finalizerBarrierSkipped;

    internal ShutdownReport(ShutdownRequest request)
    {
        FirstRequest = request ?? throw new ArgumentNullException(nameof(request));
        _effectiveExitCode = InitialEffectiveExitCode(request);
    }

    internal ShutdownRequest FirstRequest { get; }

    internal int RequestedExitCode => FirstRequest.RequestedExitCode;

    internal int EffectiveExitCode
    {
        get
        {
            lock (_sync)
                return _effectiveExitCode;
        }
    }

    internal ApplicationShutdownPhase FinalPhase
    {
        get
        {
            lock (_sync)
                return _stateMachine.Phase;
        }
    }

    internal bool FinalizerBarrierSkipped
    {
        get
        {
            lock (_sync)
                return _finalizerBarrierSkipped;
        }
    }

    internal IReadOnlyList<ApplicationShutdownPhase> PhaseHistory
    {
        get
        {
            lock (_sync)
                return _phaseHistory.ToArray();
        }
    }

    internal IReadOnlyList<ShutdownDuplicateRequestDiagnostic> DuplicateRequestDiagnostics
    {
        get
        {
            lock (_sync)
                return _duplicateRequestDiagnostics.ToArray();
        }
    }

    internal IReadOnlyList<ShutdownFailure> Failures
    {
        get
        {
            lock (_sync)
                return _failures.ToArray();
        }
    }

    internal IReadOnlyList<LifecycleLegacyDebtSnapshot> LegacyDebt
    {
        get
        {
            lock (_sync)
                return _legacyDebt.ToArray();
        }
    }

    internal void MergeRequest(ShutdownRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        lock (_sync)
        {
            _duplicateRequestDiagnostics.Add(
                new ShutdownDuplicateRequestDiagnostic(
                    request.RequestedExitCode,
                    request.Reason,
                    request.CallerResult?.Label ?? string.Empty,
                    request.CallerResult?.Passed
                )
            );
            RaiseEffectiveExitCode(request);
        }
    }

    internal bool TryAdvancePhase(ApplicationShutdownPhase nextPhase)
    {
        lock (_sync)
        {
            if (nextPhase == ApplicationShutdownPhase.FinalizerBarrierSkipped)
                return false;

            if (!_stateMachine.TryAdvance(nextPhase))
                return false;

            _phaseHistory.Add(nextPhase);
            return true;
        }
    }

    internal void RecordFailure(string stage, string message)
    {
        RecordFailure(stage, message, string.Empty);
    }

    internal void RecordFailure(string stage, Exception exception)
    {
        if (exception == null)
            throw new ArgumentNullException(nameof(exception));

        RecordFailure(stage, exception.Message, exception.GetType().FullName ?? string.Empty);
    }

    internal void MarkFinalizerBarrierSkipped(string failure)
    {
        lock (_sync)
        {
            if (_finalizerBarrierSkipped)
                return;
            if (!_stateMachine.TryAdvance(ApplicationShutdownPhase.FinalizerBarrierSkipped))
                return;

            _phaseHistory.Add(ApplicationShutdownPhase.FinalizerBarrierSkipped);
            _finalizerBarrierSkipped = true;
            _failures.Add(
                new ShutdownFailure(
                    "finalizer-barrier",
                    string.IsNullOrWhiteSpace(failure)
                        ? "Finalizer barrier skipped."
                        : failure,
                    string.Empty
                )
            );
            ForceFailureExitCode();
        }
    }

    internal void CaptureLegacyDebt(IEnumerable<LifecycleLegacyDebtSnapshot> legacyDebt)
    {
        if (legacyDebt == null)
            throw new ArgumentNullException(nameof(legacyDebt));

        LifecycleLegacyDebtSnapshot[] snapshot = legacyDebt.ToArray();
        if (snapshot.Any(debt => debt == null))
            throw new ArgumentException("Legacy debt snapshots cannot contain null entries.", nameof(legacyDebt));
        if (snapshot.Select(debt => debt.DebtId).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
            throw new ArgumentException("Legacy debt snapshot IDs must be unique.", nameof(legacyDebt));

        lock (_sync)
        {
            _legacyDebt.Clear();
            _legacyDebt.AddRange(snapshot.OrderBy(debt => debt.DebtId, StringComparer.Ordinal));
        }
    }

    private void RecordFailure(string stage, string message, string exceptionType)
    {
        lock (_sync)
        {
            _failures.Add(
                new ShutdownFailure(
                    string.IsNullOrWhiteSpace(stage) ? "unknown" : stage,
                    message ?? string.Empty,
                    exceptionType ?? string.Empty
                )
            );
            ForceFailureExitCode();
        }
    }

    private void RaiseEffectiveExitCode(ShutdownRequest request)
    {
        if (_effectiveExitCode != 0)
            return;

        if (request.RequestedExitCode != 0)
            _effectiveExitCode = request.RequestedExitCode;
        else if (request.CallerResult?.Passed == false)
            _effectiveExitCode = 1;
    }

    private void ForceFailureExitCode()
    {
        if (_effectiveExitCode == 0)
            _effectiveExitCode = 1;
    }

    private static int InitialEffectiveExitCode(ShutdownRequest request)
    {
        if (request.RequestedExitCode != 0)
            return request.RequestedExitCode;
        return request.CallerResult?.Passed == false ? 1 : 0;
    }
}
