using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class ApplicationLifetimeCoordinator : Node, IApplicationShutdownHooks
{
    private sealed record ParticipantRegistration(
        string ParticipantId,
        ApplicationShutdownParticipantStage Stage,
        int Order,
        WeakReference<IApplicationShutdownParticipant> Participant
    );

    private readonly object _shutdownSync = new();
    private readonly Dictionary<string, ParticipantRegistration> _participants =
        new(StringComparer.Ordinal);
    private ApplicationShutdownPipeline _pipeline;
    private Task<ShutdownReport> _completion;
    private ShutdownReport _report;
    private ProcessContentHost _contentHost;
    private GameSession _activeSession;
    private long _nextSessionBorrowerSerial;
    private int _mainThreadId;
    private bool _acceptingRegistrations;
    private bool _quitIssued;

    public override void _Ready()
    {
        _mainThreadId = System.Environment.CurrentManagedThreadId;
        _pipeline = new ApplicationShutdownPipeline(this, LifecycleAuditRegistry.Shared);
        GetTree().AutoAcceptQuit = false;
        if (
            ReferenceEquals(
                GetTree().Root.GetNodeOrNull<ApplicationLifetimeCoordinator>(
                    "ApplicationLifetimeCoordinator"
                ),
                this
            )
        )
        {
            try
            {
                _contentHost = new ProcessContentHost();
                _contentHost.BuildAndSeal();
            }
            catch (Exception exception)
            {
                Exception cleanupFailure = ReleaseFailedStartupContentHost();
                BeginStartupFailureShutdown(exception, cleanupFailure);
                return;
            }
        }
        _acceptingRegistrations = true;
    }

    internal bool CanAttachSession
    {
        get
        {
            lock (_shutdownSync)
            {
                return _acceptingRegistrations
                    && _report == null
                    && _contentHost != null;
            }
        }
    }

    internal ProcessContentHost ContentHost =>
        _contentHost
        ?? throw new InvalidOperationException(
            "ApplicationLifetimeCoordinator has not initialized process content."
        );

    internal void AttachSession(GameSession session)
    {
        EnsureMainThread();
        ArgumentNullException.ThrowIfNull(session);
        if (!GodotObject.IsInstanceValid(session) || session.IsClosed)
            throw new ObjectDisposedException(nameof(session));

        lock (_shutdownSync)
        {
            if (!_acceptingRegistrations || _report != null)
            {
                throw new InvalidOperationException(
                    "GameSession cannot attach after application quiescing begins."
                );
            }
            if (_activeSession != null)
            {
                if (ReferenceEquals(_activeSession, session))
                    return;
                throw new InvalidOperationException(
                    "Only one GameSession may borrow process content at a time."
                );
            }
        }

        ProcessContentHost host = ContentHost;
        ContentSnapshot snapshot = host.GetSnapshot();
        string borrowerId =
            $"game-session:{System.Threading.Interlocked.Increment(ref _nextSessionBorrowerSerial)}";
        bool contentBound = false;
        bool borrowerRegistered = false;
        bool participantRegistered = false;
        try
        {
            session.BindContent(snapshot);
            contentBound = true;
            host.RegisterSnapshotBorrower(borrowerId, session);
            borrowerRegistered = true;
            session.BindContentBorrower(host, borrowerId);
            session.BindApplicationLifetimeCoordinator(this);
            RegisterParticipant(session);
            participantRegistered = true;

            lock (_shutdownSync)
                _activeSession = session;
        }
        catch
        {
            if (participantRegistered)
                UnregisterParticipant(session);
            if (contentBound)
            {
                session.RollBackFailedContentAttachment(
                    snapshot,
                    host,
                    borrowerId,
                    this
                );
            }
            if (borrowerRegistered)
                host.UnregisterSnapshotBorrower(borrowerId);
            lock (_shutdownSync)
            {
                if (ReferenceEquals(_activeSession, session))
                    _activeSession = null;
            }
            throw;
        }
    }

    private Exception ReleaseFailedStartupContentHost()
    {
        ProcessContentHost host = _contentHost;
        _contentHost = null;
        if (host == null)
            return null;

        try
        {
            host.Quiesce();
            host.ReleaseSnapshot();
            host.Dispose();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private void BeginStartupFailureShutdown(
        Exception startupFailure,
        Exception cleanupFailure
    )
    {
        var request = new ShutdownRequest(
            1,
            ShutdownReason.RequestedExit,
            new ShutdownCallerResult("Process content startup", false)
        );
        var report = new ShutdownReport(request);
        report.RecordFailure("process-content-startup", startupFailure);
        if (cleanupFailure != null)
            report.RecordFailure("process-content-startup-cleanup", cleanupFailure);

        var completionSource = new TaskCompletionSource<ShutdownReport>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        lock (_shutdownSync)
        {
            _acceptingRegistrations = false;
            _report = report;
            _completion = completionSource.Task;
        }
        _ = CompleteShutdownAndQuitAsync(report, completionSource);
    }

    internal async ValueTask CloseSessionAsync(GameSession session)
    {
        EnsureMainThread();
        if (session == null)
            return;

        bool attached;
        lock (_shutdownSync)
        {
            attached = ReferenceEquals(_activeSession, session);
            if (!attached)
            {
                if (!GodotObject.IsInstanceValid(session))
                    return;
                if (!session.IsClosed)
                {
                    throw new InvalidOperationException(
                        "ApplicationLifetimeCoordinator cannot close a GameSession it did not attach."
                    );
                }
            }
        }

        try
        {
            if (attached)
                session.CloseNormal();
            if (!GodotObject.IsInstanceValid(session))
                return;
            if (session.IsInsideTree())
            {
                session.QueueFree();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            else
            {
                session.Dispose();
            }
        }
        finally
        {
            lock (_shutdownSync)
            {
                if (ReferenceEquals(_activeSession, session))
                    _activeSession = null;
            }
        }
    }

    internal void NotifySessionClosed(GameSession session)
    {
        EnsureMainThread();
        if (session == null)
            return;
        lock (_shutdownSync)
        {
            if (ReferenceEquals(_activeSession, session))
                _activeSession = null;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            _ = RequestShutdownAsync(
                new ShutdownRequest(0, ShutdownReason.WindowClose)
            );
        }
    }

    internal ValueTask<ShutdownReport> RequestShutdownAsync(ShutdownRequest request)
    {
        EnsureMainThread();
        ArgumentNullException.ThrowIfNull(request);

        TaskCompletionSource<ShutdownReport> completionSourceToStart = null;
        ShutdownReport reportToRun = null;
        Task<ShutdownReport> completion;
        lock (_shutdownSync)
        {
            if (_pipeline == null)
            {
                throw new InvalidOperationException(
                    "ApplicationLifetimeCoordinator is not ready."
                );
            }

            if (_completion == null)
            {
                _report = new ShutdownReport(request);
                completionSourceToStart = new TaskCompletionSource<ShutdownReport>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
                _completion = completionSourceToStart.Task;
                reportToRun = _report;
            }
            else
            {
                _report.MergeRequest(request);
            }

            completion = _completion;
        }

        if (completionSourceToStart != null)
            _ = CompleteShutdownAndQuitAsync(reportToRun, completionSourceToStart);

        return new ValueTask<ShutdownReport>(completion);
    }

    internal void RegisterParticipant(IApplicationShutdownParticipant participant)
    {
        EnsureMainThread();
        ArgumentNullException.ThrowIfNull(participant);

        string participantId = participant.ShutdownParticipantId;
        ApplicationShutdownParticipantStage stage = participant.ShutdownStage;
        int order = participant.ShutdownOrder;

        if (string.IsNullOrWhiteSpace(participantId))
        {
            throw new ArgumentException(
                "Shutdown participant ID is required.",
                nameof(participant)
            );
        }
        if (!Enum.IsDefined(stage))
        {
            throw new ArgumentOutOfRangeException(
                nameof(participant),
                stage,
                "Shutdown participant stage is invalid."
            );
        }

        lock (_shutdownSync)
        {
            if (!_acceptingRegistrations || _report != null)
            {
                throw new InvalidOperationException(
                    "Shutdown participants cannot register after application quiescing begins."
                );
            }
            if (_participants.ContainsKey(participantId))
            {
                throw new InvalidOperationException(
                    $"Shutdown participant ID is already registered. id={participantId}"
                );
            }

            _participants.Add(
                participantId,
                new ParticipantRegistration(
                    participantId,
                    stage,
                    order,
                    new WeakReference<IApplicationShutdownParticipant>(participant)
                )
            );
        }
    }

    internal void UnregisterParticipant(IApplicationShutdownParticipant participant)
    {
        EnsureMainThread();
        if (participant == null)
            return;

        lock (_shutdownSync)
        {
            string registrationId = _participants
                .Where(entry =>
                    entry.Value.Participant.TryGetTarget(out IApplicationShutdownParticipant target)
                    && ReferenceEquals(target, participant)
                )
                .Select(entry => entry.Key)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(registrationId))
                _participants.Remove(registrationId);
        }
    }

    ValueTask IApplicationShutdownHooks.QuiesceAsync(ShutdownReport report)
    {
        EnsureMainThread();
        lock (_shutdownSync)
            _acceptingRegistrations = false;

        _contentHost?.Quiesce();

        report.CaptureLegacyDebt(LifecycleAuditRegistry.Shared.CaptureSnapshot().LegacyDebt);
        ApplicationLifetimeDiagnostics.RecordPhase(ApplicationShutdownPhase.Quiescing);
        return ValueTask.CompletedTask;
    }

    async ValueTask IApplicationShutdownHooks.DrainRuntimeAsync(ShutdownReport report)
    {
        EnsureMainThread();
        await CloseParticipantsAsync(ApplicationShutdownParticipantStage.Runtime, report);
        ApplicationLifetimeDiagnostics.RecordPhase(ApplicationShutdownPhase.RuntimeDrained);
    }

    async ValueTask IApplicationShutdownHooks.DrainSceneAsync(ShutdownReport report)
    {
        EnsureMainThread();
        var failures = new List<Exception>();

        try
        {
            await CloseParticipantsAsync(ApplicationShutdownParticipantStage.Session, report);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        SceneTree tree = GetTree();
        QueueSceneTreeOwners(tree, report, failures);
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (QueueSceneTreeOwners(tree, report, failures))
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        ApplicationLifetimeDiagnostics.RecordPhase(ApplicationShutdownPhase.SceneDrained);

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "One or more SceneTree shutdown owners failed to close.",
                failures
            );
        }
    }

    bool IApplicationShutdownHooks.CanReleaseProcessContent(
        ShutdownReport report,
        out string failure
    )
    {
        EnsureMainThread();
        var failures = new List<string>();
        AddParticipantGateFailures(failures);
        AddSceneTreeGateFailures(failures, requireCoordinatorOnly: false);

        LifecycleAuditSnapshot audit = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        if (audit.NonTerminalCount != 0)
        {
            failures.Add(
                $"{audit.NonTerminalCount} non-terminal lifecycle objects remain active"
            );
        }
        if (_contentHost != null)
        {
            IReadOnlyList<string> snapshotBorrowers =
                _contentHost.GetSnapshotBorrowerDiagnostics();
            if (snapshotBorrowers.Count != 0)
            {
                failures.Add(
                    "process content snapshot borrowers remain active: "
                        + string.Join(",", snapshotBorrowers)
                );
            }
        }

        failure = string.Join("; ", failures);
        return failures.Count == 0;
    }

    ValueTask IApplicationShutdownHooks.ReleaseContentAsync(ShutdownReport report)
    {
        EnsureMainThread();
        ProcessContentHost host = _contentHost;
        if (host != null)
        {
            host.ReleaseSnapshot();
            host.Dispose();
            _contentHost = null;
        }
        ApplicationLifetimeDiagnostics.RecordPhase(ApplicationShutdownPhase.ContentReleased);
        return ValueTask.CompletedTask;
    }

    bool IApplicationShutdownHooks.CanRunFinalizerBarrier(
        ShutdownReport report,
        out string failure
    )
    {
        EnsureMainThread();
        var failures = new List<string>();
        AddParticipantGateFailures(failures);
        AddSceneTreeGateFailures(failures, requireCoordinatorOnly: true);

        LifecycleAuditSnapshot audit = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        if (audit.NonTerminalCount != 0)
        {
            failures.Add(
                $"{audit.NonTerminalCount} non-terminal lifecycle objects remain active"
            );
        }
        if (audit.ProcessContentRootCount != 0)
        {
            failures.Add(
                $"{audit.ProcessContentRootCount} canonical process content roots remain active"
            );
        }

        failure = string.Join("; ", failures);
        return failures.Count == 0;
    }

    void IApplicationShutdownHooks.RunFinalizerBarrier(ShutdownReport report)
    {
        EnsureMainThread();
        GodotObjectLifecycle.CollectPendingFinalizers();
        ApplicationLifetimeDiagnostics.RecordPhase(
            ApplicationShutdownPhase.FinalizersDrained
        );
    }

    private async Task<ShutdownReport> RunShutdownAndQuitAsync(ShutdownReport report)
    {
        ShutdownReport completedReport = await _pipeline.RunAsync(report);
        if (completedReport.FinalizerBarrierSkipped)
        {
            ApplicationLifetimeDiagnostics.RecordPhase(
                ApplicationShutdownPhase.FinalizerBarrierSkipped
            );
        }
        ApplicationLifetimeDiagnostics.RecordPhase(completedReport.FinalPhase);
        PrintPreQuitReport(completedReport);
        RequestSceneTreeQuit(completedReport);
        return completedReport;
    }

    private async Task CompleteShutdownAndQuitAsync(
        ShutdownReport report,
        TaskCompletionSource<ShutdownReport> completionSource
    )
    {
        ShutdownReport completedReport;
        try
        {
            completedReport = await RunShutdownAndQuitAsync(report);
        }
        catch (Exception exception)
        {
            completionSource.SetException(exception);
            return;
        }

        completionSource.SetResult(completedReport);
    }

    private async ValueTask CloseParticipantsAsync(
        ApplicationShutdownParticipantStage stage,
        ShutdownReport report
    )
    {
        List<ParticipantRegistration> registrations;
        lock (_shutdownSync)
        {
            registrations = _participants
                .Values.Where(registration => registration.Stage == stage)
                .OrderBy(registration => registration.Stage)
                .ThenBy(registration => registration.Order)
                .ThenBy(registration => registration.ParticipantId, StringComparer.Ordinal)
                .ToList();
            foreach (ParticipantRegistration registration in registrations)
                _participants.Remove(registration.ParticipantId);
        }

        var failures = new List<Exception>();
        foreach (ParticipantRegistration registration in registrations)
        {
            if (!registration.Participant.TryGetTarget(out IApplicationShutdownParticipant participant))
                continue;

            try
            {
                await participant.CloseForApplicationShutdownAsync(report);
            }
            catch (Exception exception)
            {
                report.RecordFailure(
                    $"participant:{registration.ParticipantId}",
                    exception
                );
                failures.Add(
                    new InvalidOperationException(
                        $"Shutdown participant failed. id={registration.ParticipantId}",
                        exception
                    )
                );
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                $"One or more {stage} shutdown participants failed.",
                failures
            );
        }
    }

    private void AddParticipantGateFailures(List<string> failures)
    {
        lock (_shutdownSync)
        {
            string[] remainingParticipants = _participants
                .Values.Where(registration => registration.Participant.TryGetTarget(out _))
                .Select(registration => registration.ParticipantId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (remainingParticipants.Length > 0)
            {
                failures.Add(
                    "shutdown participants remain active: "
                        + string.Join(",", remainingParticipants)
                );
            }

            string[] expiredParticipants = _participants
                .Values.Where(registration => !registration.Participant.TryGetTarget(out _))
                .Select(registration => registration.ParticipantId)
                .ToArray();
            foreach (string participantId in expiredParticipants)
                _participants.Remove(participantId);
        }
    }

    private void AddSceneTreeGateFailures(
        List<string> failures,
        bool requireCoordinatorOnly
    )
    {
        SceneTree tree = GetTree();
        if (tree.CurrentScene != null && GodotObject.IsInstanceValid(tree.CurrentScene))
            failures.Add("current scene remains active");
        if (tree.Root.GetNodeOrNull<Node>("GameSession") != null)
            failures.Add("GameSession remains active");

        if (!requireCoordinatorOnly)
            return;

        string[] remainingRootNodes = tree.Root
            .GetChildren()
            .OfType<Node>()
            .Where(node => !ReferenceEquals(node, this))
            .Select(node => node.Name.ToString())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (remainingRootNodes.Length > 0)
        {
            failures.Add(
                "non-terminal root nodes remain active: "
                    + string.Join(",", remainingRootNodes)
            );
        }
    }

    private static bool QueueSceneTreeOwners(
        SceneTree tree,
        ShutdownReport report,
        List<Exception> failures
    )
    {
        Node currentScene = tree.CurrentScene;
        Node gameSession = tree.Root.GetNodeOrNull<Node>("GameSession");
        bool queuedOwner = TryQueueFreeTreeOwner(
            currentScene,
            "current-scene",
            report,
            failures
        );
        if (!ReferenceEquals(gameSession, currentScene))
        {
            queuedOwner =
                TryQueueFreeTreeOwner(gameSession, "game-session", report, failures)
                || queuedOwner;
        }
        return queuedOwner;
    }

    private static bool TryQueueFreeTreeOwner(
        Node owner,
        string ownerId,
        ShutdownReport report,
        List<Exception> failures
    )
    {
        if (owner == null || !GodotObject.IsInstanceValid(owner))
            return false;

        try
        {
            if (!owner.IsQueuedForDeletion())
                owner.QueueFree();
            return true;
        }
        catch (Exception exception)
        {
            report.RecordFailure($"scene-owner:{ownerId}", exception);
            failures.Add(exception);
            return false;
        }
    }

    private void RequestSceneTreeQuit(ShutdownReport report)
    {
        lock (_shutdownSync)
        {
            if (_quitIssued)
                return;
            _quitIssued = true;
        }

        GetTree().Quit(report.EffectiveExitCode);
    }

    private static void PrintPreQuitReport(ShutdownReport report)
    {
        string callerResult = FormatCallerResult(report);
        if (!string.IsNullOrEmpty(callerResult))
            ConsoleProcessOutput.WriteStandard(callerResult);

        ConsoleProcessOutput.WriteStandard(
            "[lifecycle] shutdown-report "
                + $"reason={report.FirstRequest.Reason} "
                + $"requested={report.RequestedExitCode} "
                + $"effective={report.EffectiveExitCode} "
                + $"phase={report.FinalPhase} "
                + $"barrier_skipped={report.FinalizerBarrierSkipped} "
                + $"duplicates={report.DuplicateRequestDiagnostics.Count} "
                + $"failures={report.Failures.Count} "
                + $"legacy_debt={report.LegacyDebt.Count}"
        );
        foreach (ShutdownFailure failure in report.Failures)
        {
            ConsoleProcessOutput.WriteFailure(
                "[lifecycle] shutdown-failure "
                    + $"stage={failure.Stage} type={failure.ExceptionType} "
                    + $"message={failure.Message}"
            );
        }
    }

    internal static string FormatCallerResult(ShutdownReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ShutdownCallerResult caller = report.FirstRequest.CallerResult;
        if (caller == null)
            return string.Empty;

        bool passed = caller.Passed && report.EffectiveExitCode == 0;
        return $"{caller.Label}: {(passed ? "PASS" : "FAIL")}";
    }

    private void EnsureMainThread()
    {
        int currentThreadId = System.Environment.CurrentManagedThreadId;
        if (_mainThreadId == 0)
        {
            throw new InvalidOperationException(
                "ApplicationLifetimeCoordinator has not captured the main thread yet."
            );
        }
        if (currentThreadId != _mainThreadId)
        {
            throw new InvalidOperationException(
                "Application shutdown must be requested on the captured main thread. "
                    + $"expected={_mainThreadId}, actual={currentThreadId}"
            );
        }
    }
}
