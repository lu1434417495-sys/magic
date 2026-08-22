using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

public partial class run_application_lifetime_coordinator_regression : LifecycleTestSceneTree
{
    private const string TestWorldMapConfigPath =
        "test";

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

    private sealed class SynchronousReentrantParticipant : IApplicationShutdownParticipant
    {
        private readonly ApplicationLifetimeCoordinator _coordinator;
        private readonly List<string> _calls;

        internal SynchronousReentrantParticipant(
            ApplicationLifetimeCoordinator coordinator,
            List<string> calls
        )
        {
            _coordinator = coordinator;
            _calls = calls;
        }

        public string ShutdownParticipantId => "synchronous-reentrant-runtime";
        public ApplicationShutdownParticipantStage ShutdownStage =>
            ApplicationShutdownParticipantStage.Runtime;
        public int ShutdownOrder => -2;
        internal int CloseCount { get; private set; }
        internal ShutdownReport ObservedReport { get; private set; }
        internal Task<ShutdownReport> ReentrantCompletion { get; private set; }

        public ValueTask CloseForApplicationShutdownAsync(ShutdownReport report)
        {
            CloseCount++;
            ObservedReport = report;
            _calls.Add(ShutdownParticipantId);
            ReentrantCompletion = _coordinator
                .RequestShutdownAsync(
                    new ShutdownRequest(
                        0,
                        ShutdownReason.RequestedExit,
                        new ShutdownCallerResult("synchronous-reentrant", true)
                    )
                )
                .AsTask();
            return ValueTask.CompletedTask;
        }
    }

    private readonly TestHarness _test = new();
    private bool _terminalExitRequested;

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
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

        if (!_terminalExitRequested)
            RequestTestExit(_test.Finish("Application lifetime coordinator regression"));
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
        await TestRealRuntimeParticipantRegistrationContracts(coordinator, gameSession);
        await TestOffMainThreadRequestFailsBeforeShutdown(coordinator);
        await TestApplicationCloseConvergesOnOneShotNormalClose();
        await TestParticipantContractsAndSkippedHistory(gameSession);
        TestTestExitCoordinatorRequestMapping();
        await TestTestExitCoordinatorAsyncFailureRecovery();
        TestIdempotentRequestAndSuccessfulHistory(coordinator, gameSession);
    }

    private void TestTestExitCoordinatorRequestMapping()
    {
        AssertTestExitCoordinatorRequestMapping(
            new TestResult(
                "passing test result",
                true,
                0,
                Array.Empty<string>()
            ),
            expectedExitCode: 0,
            expectedPassed: true
        );
        AssertTestExitCoordinatorRequestMapping(
            new TestResult(
                "failing test result",
                false,
                1,
                new[] { "expected failure detail" }
            ),
            expectedExitCode: 1,
            expectedPassed: false
        );
    }

    private void AssertTestExitCoordinatorRequestMapping(
        TestResult result,
        int expectedExitCode,
        bool expectedPassed
    )
    {
        ShutdownRequest request = TestExitCoordinator.BuildShutdownRequest(result);
        _test.Eq(
            request.RequestedExitCode,
            expectedExitCode,
            $"{result.Label}: TestExitCoordinator preserves the requested exit code"
        );
        _test.Eq(
            request.Reason,
            ShutdownReason.TestComplete,
            $"{result.Label}: TestExitCoordinator uses the TestComplete reason"
        );
        _test.Eq(
            request.CallerResult?.Label,
            result.Label,
            $"{result.Label}: TestExitCoordinator preserves the caller label"
        );
        _test.Eq(
            request.CallerResult?.Passed,
            expectedPassed,
            $"{result.Label}: TestExitCoordinator preserves the caller result"
        );
    }

    private async Task TestTestExitCoordinatorAsyncFailureRecovery()
    {
        var sourceResult = new TestResult(
            "test exit async failure probe",
            true,
            0,
            Array.Empty<string>()
        );

        TestResult frameFailureSubmission = null;
        var frameDiagnostics = new List<string>();
        await TestExitCoordinator.CompleteAsync(
            this,
            sourceResult,
            _ => Task.FromException(
                new InvalidOperationException("injected process-frame wait failure")
            ),
            (_, submittedResult) =>
            {
                frameFailureSubmission = submittedResult;
                return new ValueTask<ShutdownReport>(
                    new ShutdownReport(
                        TestExitCoordinator.BuildShutdownRequest(submittedResult)
                    )
                );
            },
            frameDiagnostics.Add
        );

        _test.True(
            frameFailureSubmission != null,
            "a process-frame await failure still submits a terminal result"
        );
        _test.False(
            frameFailureSubmission?.Passed ?? true,
            "a process-frame await failure becomes a failed terminal result"
        );
        _test.Eq(
            frameFailureSubmission?.ExitCode ?? 0,
            1,
            "a process-frame await failure requests a deterministic nonzero exit"
        );
        _test.True(
            frameFailureSubmission?.Failures.LastOrDefault()?.Contains(
                "stage=wait-for-process-frame",
                StringComparison.Ordinal
            ) == true,
            "a process-frame await failure retains its failing async stage"
        );
        _test.Eq(
            frameDiagnostics.Count,
            0,
            "successful failure-result submission needs no recovery diagnostic"
        );

        int submitAttempts = 0;
        TestResult recoveredSubmission = null;
        var submitDiagnostics = new List<string>();
        await TestExitCoordinator.CompleteAsync(
            this,
            sourceResult,
            _ => Task.CompletedTask,
            async (_, submittedResult) =>
            {
                submitAttempts++;
                if (submitAttempts == 1)
                {
                    await Task.Yield();
                    throw new InvalidOperationException(
                        "injected async shutdown submission failure"
                    );
                }

                recoveredSubmission = submittedResult;
                return new ShutdownReport(
                    TestExitCoordinator.BuildShutdownRequest(submittedResult)
                );
            },
            submitDiagnostics.Add
        );

        _test.Eq(
            submitAttempts,
            2,
            "an asynchronous shutdown submission failure is retried once"
        );
        _test.False(
            recoveredSubmission?.Passed ?? true,
            "the retry submits a failed terminal result"
        );
        _test.Eq(
            recoveredSubmission?.ExitCode ?? 0,
            1,
            "the retry preserves a deterministic nonzero exit"
        );
        _test.True(
            recoveredSubmission?.Failures.LastOrDefault()?.Contains(
                "stage=submit-shutdown",
                StringComparison.Ordinal
            ) == true,
            "the retry result identifies the shutdown submission stage"
        );
        _test.Eq(
            submitDiagnostics.Count,
            1,
            "the first shutdown submission failure emits one observable diagnostic"
        );
        _test.True(
            submitDiagnostics[0].Contains(
                "injected async shutdown submission failure",
                StringComparison.Ordinal
            ),
            "the recovery diagnostic retains the asynchronous failure reason"
        );
        ShutdownRequest recoveredRequest =
            TestExitCoordinator.BuildShutdownRequest(recoveredSubmission);
        _test.Eq(
            recoveredRequest.RequestedExitCode,
            1,
            "the recovered shutdown request carries the nonzero exit code"
        );
        _test.False(
            recoveredRequest.CallerResult?.Passed ?? true,
            "the recovered shutdown request carries the failed caller result"
        );
    }

    private async Task TestRealRuntimeParticipantRegistrationContracts(
        ApplicationLifetimeCoordinator coordinator,
        GameSession gameSession
    )
    {
        var report = new ShutdownReport(
            new ShutdownRequest(0, ShutdownReason.RequestedExit)
        );

        var headlessSession = new HeadlessGameTestSession();
        headlessSession.initialize();
        IApplicationShutdownParticipant headlessParticipant = headlessSession;
        _test.Eq(
            headlessParticipant.ShutdownParticipantId,
            "headless-game-test-session",
            "HeadlessGameTestSession participant ID is stable"
        );
        _test.Eq(
            headlessParticipant.ShutdownStage,
            ApplicationShutdownParticipantStage.Runtime,
            "HeadlessGameTestSession participates at the Runtime stage"
        );
        _test.Eq(
            headlessParticipant.ShutdownOrder,
            0,
            "HeadlessGameTestSession participant order is stable"
        );
        AssertRealParticipantRegistered(
            coordinator,
            headlessParticipant,
            "HeadlessGameTestSession registers when initialized"
        );
        AssertRealParticipantUnregistersIdempotently(
            coordinator,
            headlessParticipant,
            "HeadlessGameTestSession unregister is idempotent and permits re-registration"
        );
        await headlessParticipant.CloseForApplicationShutdownAsync(report);
        await headlessParticipant.CloseForApplicationShutdownAsync(report);
        _test.True(
            headlessSession.GetGameSessionTyped() == null,
            "HeadlessGameTestSession application close is idempotent"
        );
        bool headlessRegistrationReleased = true;
        var releasedIdProbe = new FakeParticipant(
            headlessParticipant.ShutdownParticipantId,
            headlessParticipant.ShutdownStage,
            headlessParticipant.ShutdownOrder,
            new List<string>()
        );
        try
        {
            coordinator.RegisterParticipant(releasedIdProbe);
            coordinator.UnregisterParticipant(releasedIdProbe);
        }
        catch (Exception)
        {
            headlessRegistrationReleased = false;
        }
        _test.True(
            headlessRegistrationReleased,
            "HeadlessGameTestSession close releases its participant registration"
        );

        gameSession.ClearPersistedGame();
        await ProcessFrames(1);
        WorldMapSystem worldMap = null;
        try
        {
            Error createError = (Error)gameSession.StartNewGame(TestWorldMapConfigPath);
            _test.Eq(
                createError,
                Error.Ok,
                "WorldMapSystem registration regression creates an active test world"
            );
            if (createError != Error.Ok)
                return;

            PackedScene worldMapScene = GD.Load<PackedScene>(
                "res://scenes/main/world_map.tscn"
            );
            _test.True(
                worldMapScene != null,
                "WorldMapSystem registration regression loads the real world map scene"
            );
            if (worldMapScene == null)
                return;

            worldMap = worldMapScene.Instantiate<WorldMapSystem>();
            Root.AddChild(worldMap);
            await ProcessFrames(2);

            IApplicationShutdownParticipant worldMapParticipant = worldMap;
            _test.Eq(
                worldMapParticipant.ShutdownParticipantId,
                "world-map-system",
                "WorldMapSystem exposes the stable participant ID"
            );
            _test.Eq(
                worldMapParticipant.ShutdownStage,
                ApplicationShutdownParticipantStage.Runtime,
                "WorldMapSystem participates at the Runtime stage"
            );
            _test.Eq(
                worldMapParticipant.ShutdownOrder,
                0,
                "WorldMapSystem exposes the stable participant order"
            );
            _test.True(
                worldMap._runtime != null && worldMap._runtime_proxy != null,
                "the real WorldMapSystem scene completes runtime setup before registration checks"
            );
            AssertRealParticipantRegistered(
                coordinator,
                worldMapParticipant,
                "WorldMapSystem registers with the application coordinator from _Ready"
            );

            await worldMapParticipant.CloseForApplicationShutdownAsync(report);
            await worldMapParticipant.CloseForApplicationShutdownAsync(report);
            _test.True(
                worldMap._runtime == null
                    && worldMap._runtime_proxy == null
                    && worldMap._game_session == null,
                "WorldMapSystem application close releases its runtime owners idempotently"
            );

            bool worldMapRegistrationReleased = true;
            var releasedWorldMapProbe = new FakeParticipant(
                "world-map-system",
                ApplicationShutdownParticipantStage.Runtime,
                0,
                new List<string>()
            );
            try
            {
                coordinator.RegisterParticipant(releasedWorldMapProbe);
                coordinator.UnregisterParticipant(releasedWorldMapProbe);
            }
            catch (Exception)
            {
                worldMapRegistrationReleased = false;
            }
            _test.True(
                worldMapRegistrationReleased,
                "WorldMapSystem close releases its participant ID for re-registration"
            );
        }
        finally
        {
            if (worldMap != null && GodotObject.IsInstanceValid(worldMap))
            {
                worldMap.QueueFree();
                await ProcessFrames(2);
                _test.False(
                    GodotObject.IsInstanceValid(worldMap),
                    "WorldMapSystem scene finishes queued deletion after application close"
                );
            }
            gameSession.ClearPersistedGame();
            await ProcessFrames(1);
        }
    }

    private async Task ProcessFrames(int count)
    {
        for (int index = 0; index < count; index++)
            await ToSignal(this, SceneTree.SignalName.ProcessFrame);
    }

    private void AssertRealParticipantRegistered(
        ApplicationLifetimeCoordinator coordinator,
        IApplicationShutdownParticipant participant,
        string message
    )
    {
        bool duplicateRejected = false;
        try
        {
            coordinator.RegisterParticipant(
                new FakeParticipant(
                    participant.ShutdownParticipantId,
                    participant.ShutdownStage,
                    participant.ShutdownOrder,
                    new List<string>()
                )
            );
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }
        _test.True(duplicateRejected, message);
    }

    private void AssertRealParticipantUnregistersIdempotently(
        ApplicationLifetimeCoordinator coordinator,
        IApplicationShutdownParticipant participant,
        string message
    )
    {
        bool unregisterWasIdempotent = true;
        try
        {
            coordinator.UnregisterParticipant(participant);
            coordinator.UnregisterParticipant(participant);
            coordinator.RegisterParticipant(participant);
        }
        catch (Exception)
        {
            unregisterWasIdempotent = false;
        }
        _test.True(unregisterWasIdempotent, message);
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
        GameSession session = GameSessionTestFactory.CreateBorrowingProcessSnapshot(
            "ApplicationCloseLifecycleSession"
        );
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

    private void TestIdempotentRequestAndSuccessfulHistory(
        ApplicationLifetimeCoordinator coordinator,
        GameSession gameSession
    )
    {
        var calls = new List<string>();
        var headlessSession = new HeadlessGameTestSession();
        headlessSession.initialize();
        IApplicationShutdownParticipant headlessParticipant = headlessSession;

        GameContentCatalog catalog = gameSession.GetContentCatalogTyped();
        long revisionBeforeShutdown = catalog.GetRevision();
        var runtimeBefore = new FakeParticipant(
            "runtime-before-real-owners",
            ApplicationShutdownParticipantStage.Runtime,
            -1,
            calls,
            onClose: () =>
            {
                Require(
                    headlessSession.GetGameSessionTyped() != null,
                    "lower-order Runtime participant closes before real Runtime owners"
                );
                return ValueTask.CompletedTask;
            }
        );
        var synchronousReentrant = new SynchronousReentrantParticipant(
            coordinator,
            calls
        );
        var runtimeAfter = new FakeParticipant(
            "runtime-after-real-owners",
            ApplicationShutdownParticipantStage.Runtime,
            1,
            calls,
            onClose: () =>
            {
                Require(
                    headlessSession.GetGameSessionTyped() == null,
                    "higher-order Runtime participant closes after real Runtime owners"
                );
                return ValueTask.CompletedTask;
            }
        );
        coordinator.RegisterParticipant(synchronousReentrant);
        coordinator.RegisterParticipant(runtimeBefore);
        coordinator.RegisterParticipant(runtimeAfter);
        var sessionBefore = new FakeParticipant(
            "session-before-game-session",
            ApplicationShutdownParticipantStage.Session,
            -1,
            calls,
            onClose: () =>
            {
                RequireEqual(
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
                RequireEqual(
                    catalog.GetRevision(),
                    revisionBeforeShutdown + 1,
                    "higher-order Session participant closes after GameSession"
                );
                return ValueTask.CompletedTask;
            }
        );
        coordinator.RegisterParticipant(sessionBefore);
        coordinator.RegisterParticipant(sessionAfter);

        TestResult finalResult = _test.Finish(
            "Application lifetime coordinator regression"
        );
        Task<ShutdownReport> first = TestExitCoordinator
            .SubmitAsync(this, finalResult)
            .AsTask();
        _terminalExitRequested = true;

        Task<ShutdownReport> laterDuplicate = coordinator
            .RequestShutdownAsync(
                new ShutdownRequest(
                    0,
                    ShutdownReason.RequestedExit,
                    new ShutdownCallerResult("later-duplicate", true)
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

        EscalateTerminalFailure(
            coordinator,
            ReferenceEquals(first, synchronousReentrant.ReentrantCompletion)
                && ReferenceEquals(first, laterDuplicate)
                && ReferenceEquals(first, laterSuccess),
            "synchronous reentrant and later shutdown requests share one completion task"
        );
        EscalateTerminalFailure(
            coordinator,
            synchronousReentrant.ObservedReport != null,
            "synchronous reentrant shutdown observes the first request report"
        );
        ShutdownRequest firstRequest = synchronousReentrant.ObservedReport?.FirstRequest;
        EscalateTerminalFailure(
            coordinator,
            firstRequest?.Reason == ShutdownReason.TestComplete,
            "TestExitCoordinator submits a TestComplete shutdown request"
        );
        EscalateTerminalFailure(
            coordinator,
            firstRequest?.RequestedExitCode == finalResult.ExitCode,
            "TestExitCoordinator preserves the test result exit code"
        );
        EscalateTerminalFailure(
            coordinator,
            firstRequest?.CallerResult?.Label == finalResult.Label
                && firstRequest.CallerResult.Passed == finalResult.Passed,
            "TestExitCoordinator preserves the test result caller facts"
        );
        EscalateTerminalFailure(
            coordinator,
            synchronousReentrant.CloseCount == 1,
            "synchronous reentrant participant closes exactly once"
        );
        EscalateTerminalFailure(
            coordinator,
            string.Join(",", calls)
                == "synchronous-reentrant-runtime,runtime-before-real-owners,runtime-after-real-owners,session-before-game-session,session-after-game-session",
            "idempotent requests close each participant once"
        );
        EscalateTerminalFailure(
            coordinator,
            headlessSession.GetRuntimeFacadeTyped() == null
                && headlessSession.GetGameSessionTyped() == null,
            "real Runtime participants close before terminal exit"
        );
        EscalateTerminalFailure(
            coordinator,
            catalog.GetRevision() == revisionBeforeShutdown + 1,
            "participant close plus SceneTree teardown closes GameSession exactly once"
        );
        Node gameSessionOwner = Root.GetNodeOrNull<Node>("GameSession");
        EscalateTerminalFailure(
            coordinator,
            gameSessionOwner == null || gameSessionOwner.IsQueuedForDeletion(),
            "coordinator queues the GameSession tree owner for awaited deletion"
        );
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireEqual<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            throw new InvalidOperationException(
                $"{message} | actual={actual} expected={expected}"
            );
        }
    }

    private static void EscalateTerminalFailure(
        ApplicationLifetimeCoordinator coordinator,
        bool condition,
        string message
    )
    {
        if (condition)
            return;

        ConsoleProcessOutput.WriteFailure($"[test] Application lifetime coordinator regression: {message}");
        _ = coordinator.RequestShutdownAsync(
            new ShutdownRequest(
                1,
                ShutdownReason.TestComplete,
                new ShutdownCallerResult(
                    "Application lifetime coordinator regression terminal assertion",
                    false
                )
            )
        );
    }
}
