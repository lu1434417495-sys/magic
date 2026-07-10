using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class run_application_lifetime_coordinator_regression : SceneTree
{
    private sealed class FakeParticipant : IApplicationShutdownParticipant
    {
        private readonly List<string> _calls;
        private readonly bool _throws;
        private readonly TaskCompletionSource<bool> _started;
        private readonly TaskCompletionSource<bool> _release;
        private readonly Func<ValueTask> _onClose;

        internal FakeParticipant(
            string participantId,
            ApplicationShutdownParticipantStage stage,
            int order,
            List<string> calls,
            bool throws = false,
            TaskCompletionSource<bool> started = null,
            TaskCompletionSource<bool> release = null,
            Func<ValueTask> onClose = null
        )
        {
            ShutdownParticipantId = participantId;
            ShutdownStage = stage;
            ShutdownOrder = order;
            _calls = calls;
            _throws = throws;
            _started = started;
            _release = release;
            _onClose = onClose;
        }

        public string ShutdownParticipantId { get; }
        public ApplicationShutdownParticipantStage ShutdownStage { get; }
        public int ShutdownOrder { get; }

        public async ValueTask CloseForApplicationShutdownAsync(ShutdownReport report)
        {
            _calls.Add(ShutdownParticipantId);
            _started?.TrySetResult(true);
            if (_release != null)
                await _release.Task;
            if (_onClose != null)
                await _onClose();
            if (_throws)
                throw new InvalidOperationException($"{ShutdownParticipantId} failed");
        }
    }

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private async void Run()
    {
        try
        {
            await RunAsync();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected coordinator regression exception: {exception}");
        }

        Quit(_test.Finish("Application lifetime coordinator regression"));
    }

    private async Task RunAsync()
    {
        ApplicationLifetimeCoordinator coordinator =
            Root.GetNodeOrNull<ApplicationLifetimeCoordinator>(
                "ApplicationLifetimeCoordinator"
            );
        GameSession gameSession = Root.GetNodeOrNull<GameSession>("GameSession");

        _test.True(coordinator != null, "coordinator autoload exists");
        _test.True(gameSession != null, "GameSession autoload exists");
        if (coordinator == null || gameSession == null)
            return;

        int coordinatorIndex = Root.GetChildren().IndexOf(coordinator);
        int gameSessionIndex = Root.GetChildren().IndexOf(gameSession);
        _test.True(
            coordinatorIndex >= 0 && coordinatorIndex < gameSessionIndex,
            "coordinator autoload precedes GameSession"
        );
        _test.False(AutoAcceptQuit, "coordinator disables automatic quit acceptance");

        TestRealGameSessionRegistrationContract(coordinator, gameSession);
        await TestOffMainThreadRequestFailsBeforeShutdown(coordinator);
        await TestApplicationCloseConvergesOnOneShotNormalClose();
        await TestParticipantContractsAndSkippedHistory(gameSession);
        await TestIdempotentRequestAndSuccessfulHistory(coordinator, gameSession);
    }

    private void TestRealGameSessionRegistrationContract(
        ApplicationLifetimeCoordinator coordinator,
        GameSession gameSession
    )
    {
        IApplicationShutdownParticipant participant = gameSession;
        _test.Eq(
            participant.ShutdownParticipantId,
            "game-session",
            "GameSession participant ID is stable"
        );
        _test.Eq(
            participant.ShutdownStage,
            ApplicationShutdownParticipantStage.Session,
            "GameSession participates at the Session stage"
        );
        _test.Eq(participant.ShutdownOrder, 0, "GameSession participant order is stable");

        bool activationRegisteredParticipant = false;
        try
        {
            coordinator.RegisterParticipant(
                new FakeParticipant(
                    participant.ShutdownParticipantId,
                    ApplicationShutdownParticipantStage.Session,
                    participant.ShutdownOrder,
                    new List<string>()
                )
            );
        }
        catch (InvalidOperationException)
        {
            activationRegisteredParticipant = true;
        }
        _test.True(
            activationRegisteredParticipant,
            "canonical GameSession registers when its owner activates"
        );

        bool realUnregisterIsIdempotent = true;
        try
        {
            coordinator.UnregisterParticipant(participant);
            coordinator.UnregisterParticipant(participant);
            coordinator.RegisterParticipant(participant);
        }
        catch (Exception)
        {
            realUnregisterIsIdempotent = false;
        }
        _test.True(
            realUnregisterIsIdempotent,
            "real GameSession unregister is idempotent and permits re-registration"
        );
    }

    private async Task TestApplicationCloseConvergesOnOneShotNormalClose()
    {
        GameSession session = new() { Name = "ApplicationCloseLifecycleSession" };
        IApplicationShutdownParticipant participant = session;
        GameContentCatalog catalog = session.GetContentCatalogTyped();
        long revisionBeforeClose = catalog.GetRevision();
        var report = new ShutdownReport(
            new ShutdownRequest(0, ShutdownReason.RequestedExit)
        );

        await participant.CloseForApplicationShutdownAsync(report);
        await participant.CloseForApplicationShutdownAsync(report);
        _test.Eq(
            catalog.GetRevision(),
            revisionBeforeClose + 1,
            "application shutdown and repeated close share one-shot normal close"
        );
        _test.True(
            GodotObject.IsInstanceValid(session),
            "application close leaves later explicit native Dispose available"
        );

        session.Dispose();
        _test.False(
            GodotObject.IsInstanceValid(session),
            "explicit Dispose after application close releases the native session object"
        );
    }

    private async Task TestOffMainThreadRequestFailsBeforeShutdown(
        ApplicationLifetimeCoordinator coordinator
    )
    {
        Exception failure = await Task.Run(() =>
        {
            try
            {
                coordinator.RequestShutdownAsync(
                    new ShutdownRequest(0, ShutdownReason.RequestedExit)
                );
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        });

        _test.True(
            failure is InvalidOperationException,
            "non-main-thread shutdown request is rejected"
        );
        _test.True(
            Root.GetNodeOrNull<GameSession>("GameSession") != null,
            "rejected non-main-thread request does not touch SceneTree owners"
        );
    }

    private async Task TestParticipantContractsAndSkippedHistory(GameSession gameSession)
    {
        var localCoordinator = new ApplicationLifetimeCoordinator
        {
            Name = "ParticipantContractCoordinator",
        };
        Root.AddChild(localCoordinator);

        var calls = new List<string>();
        var runtimeLate = new FakeParticipant(
            "runtime-z",
            ApplicationShutdownParticipantStage.Runtime,
            20,
            calls
        );
        var runtimeBeta = new FakeParticipant(
            "runtime-beta",
            ApplicationShutdownParticipantStage.Runtime,
            10,
            calls
        );
        var runtimeAlpha = new FakeParticipant(
            "runtime-alpha",
            ApplicationShutdownParticipantStage.Runtime,
            10,
            calls
        );
        var runtimeThrowing = new FakeParticipant(
            "runtime-throwing",
            ApplicationShutdownParticipantStage.Runtime,
            30,
            calls,
            throws: true
        );
        var session = new FakeParticipant(
            "session",
            ApplicationShutdownParticipantStage.Session,
            -10,
            calls
        );

        localCoordinator.RegisterParticipant(runtimeLate);
        localCoordinator.RegisterParticipant(runtimeBeta);
        localCoordinator.RegisterParticipant(runtimeAlpha);
        localCoordinator.RegisterParticipant(runtimeThrowing);
        localCoordinator.RegisterParticipant(session);

        bool unregisterWasIdempotent = false;
        try
        {
            var unregisterProbe = new FakeParticipant(
                "unregister-probe",
                ApplicationShutdownParticipantStage.Runtime,
                0,
                calls
            );
            localCoordinator.RegisterParticipant(unregisterProbe);
            localCoordinator.UnregisterParticipant(unregisterProbe);
            localCoordinator.UnregisterParticipant(unregisterProbe);
            var reRegisteredProbe = new FakeParticipant(
                "unregister-probe",
                ApplicationShutdownParticipantStage.Runtime,
                0,
                calls
            );
            localCoordinator.RegisterParticipant(reRegisteredProbe);
            localCoordinator.UnregisterParticipant(reRegisteredProbe);
            unregisterWasIdempotent = true;
        }
        catch (Exception)
        {
        }
        _test.True(
            unregisterWasIdempotent,
            "participant unregister is idempotent and releases the ID"
        );

        bool duplicateRejected = false;
        try
        {
            localCoordinator.RegisterParticipant(
                new FakeParticipant(
                    "runtime-alpha",
                    ApplicationShutdownParticipantStage.Session,
                    0,
                    calls
                )
            );
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }
        _test.True(duplicateRejected, "duplicate participant IDs are rejected");

        var audit = new LifecycleAuditRegistry();
        var pipeline = new ApplicationShutdownPipeline(localCoordinator, audit);
        var report = new ShutdownReport(
            new ShutdownRequest(0, ShutdownReason.RequestedExit)
        );
        StringName originalGameSessionName = gameSession.Name;
        gameSession.Name = "GameSessionParticipantProbe";
        ShutdownReport result;
        try
        {
            result = await pipeline.RunAsync(report);
        }
        finally
        {
            gameSession.Name = originalGameSessionName;
        }

        _test.Eq(
            string.Join(",", calls),
            "runtime-alpha,runtime-beta,runtime-z,runtime-throwing,session",
            "participants close in stage, order, and ordinal ID order"
        );
        _test.True(
            report.Failures.Any(failure =>
                failure.Stage == "participant:runtime-throwing"
            ),
            "participant exceptions are recorded with the participant ID"
        );
        _test.True(ReferenceEquals(result, report), "failed pipeline returns its report");
        _test.Eq(
            string.Join(",", report.PhaseHistory),
            "Running,Quiescing,FinalizerBarrierSkipped,QuitRequested",
            "participant failure emits the legal skipped-barrier history"
        );

        bool lateRegistrationRejected = false;
        try
        {
            localCoordinator.RegisterParticipant(
                new FakeParticipant(
                    "late",
                    ApplicationShutdownParticipantStage.Runtime,
                    0,
                    calls
                )
            );
        }
        catch (InvalidOperationException)
        {
            lateRegistrationRejected = true;
        }
        _test.True(
            lateRegistrationRejected,
            "participant registration is rejected after quiescing"
        );

        if (GodotObject.IsInstanceValid(localCoordinator))
            localCoordinator.Free();
        await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private async Task TestIdempotentRequestAndSuccessfulHistory(
        ApplicationLifetimeCoordinator coordinator,
        GameSession gameSession
    )
    {
        var calls = new List<string>();
        GameContentCatalog catalog = gameSession.GetContentCatalogTyped();
        long revisionBeforeShutdown = catalog.GetRevision();
        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var blocker = new FakeParticipant(
            "blocking-runtime",
            ApplicationShutdownParticipantStage.Runtime,
            0,
            calls,
            started: started,
            release: release
        );
        coordinator.RegisterParticipant(blocker);
        var sessionBefore = new FakeParticipant(
            "session-before-game-session",
            ApplicationShutdownParticipantStage.Session,
            -1,
            calls,
            onClose: () =>
            {
                _test.Eq(
                    catalog.GetRevision(),
                    revisionBeforeShutdown,
                    "lower-order Session participant closes before GameSession"
                );
                return ValueTask.CompletedTask;
            }
        );
        var sessionAfter = new FakeParticipant(
            "session-after-game-session",
            ApplicationShutdownParticipantStage.Session,
            1,
            calls,
            onClose: () =>
            {
                _test.Eq(
                    catalog.GetRevision(),
                    revisionBeforeShutdown + 1,
                    "higher-order Session participant closes after GameSession"
                );
                return ValueTask.CompletedTask;
            }
        );
        coordinator.RegisterParticipant(sessionBefore);
        coordinator.RegisterParticipant(sessionAfter);

        Task<ShutdownReport> first = coordinator
            .RequestShutdownAsync(
                new ShutdownRequest(
                    0,
                    ShutdownReason.TestComplete,
                    new ShutdownCallerResult("first", true)
                )
            )
            .AsTask();
        await started.Task;

        Task<ShutdownReport> laterFailure = coordinator
            .RequestShutdownAsync(
                new ShutdownRequest(
                    7,
                    ShutdownReason.RequestedExit,
                    new ShutdownCallerResult("later-failure", false)
                )
            )
            .AsTask();
        Task<ShutdownReport> laterSuccess = coordinator
            .RequestShutdownAsync(
                new ShutdownRequest(
                    0,
                    ShutdownReason.RequestedExit,
                    new ShutdownCallerResult("later-success", true)
                )
            )
            .AsTask();
        coordinator._Notification((int)Node.NotificationWMCloseRequest);

        _test.True(
            ReferenceEquals(first, laterFailure) && ReferenceEquals(first, laterSuccess),
            "duplicate shutdown requests share one completion task"
        );

        release.TrySetResult(true);
        ShutdownReport report = await first;

        _test.Eq(report.FirstRequest.Reason, ShutdownReason.TestComplete, "first reason wins");
        _test.Eq(report.EffectiveExitCode, 7, "later failure raises effective exit code");
        _test.Eq(
            report.DuplicateRequestDiagnostics.Count,
            3,
            "later requests and window close share the cached report"
        );
        _test.True(
            report.DuplicateRequestDiagnostics.Any(diagnostic =>
                diagnostic.Reason == ShutdownReason.WindowClose
            ),
            "window close routes through RequestShutdownAsync"
        );
        _test.Eq(
            string.Join(",", report.PhaseHistory),
            "Running,Quiescing,RuntimeDrained,SceneDrained,ContentReleased,FinalizersDrained,QuitRequested",
            "successful coordinator shutdown emits the legal success history"
        );
        _test.Eq(
            string.Join(",", calls),
            "blocking-runtime,session-before-game-session,session-after-game-session",
            "idempotent requests close each participant once"
        );
        _test.Eq(
            catalog.GetRevision(),
            revisionBeforeShutdown + 1,
            "participant close plus SceneTree teardown closes GameSession exactly once"
        );
        _test.True(
            Root.GetNodeOrNull<Node>("GameSession") == null,
            "coordinator frees and awaits the GameSession tree owner"
        );
    }
}
