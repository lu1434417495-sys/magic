using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

public partial class run_application_lifetime_coordinator_regression : LifecycleTestSceneTree
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
        await TestRealRuntimeParticipantRegistrationContracts(coordinator);
        await TestOffMainThreadRequestFailsBeforeShutdown(coordinator);
        await TestApplicationCloseConvergesOnOneShotNormalClose();
        await TestParticipantContractsAndSkippedHistory(gameSession);
        TestExactLifecycleAuditBaseline();
        TestIdempotentRequestAndSuccessfulHistory(coordinator, gameSession);
    }

    private void TestExactLifecycleAuditBaseline()
    {
        LifecycleAuditSnapshot audit = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(
            audit.NormalPhaseSuppressCount,
            0L,
            "normal session close does not suppress process content"
        );
        _test.Eq(
            audit.LegacyDebt.Count,
            0,
            "coordinator starts with no lifecycle legacy debt"
        );
        _test.Eq(
            audit.QuarantineCount,
            0L,
            "coordinator starts with no quarantined wrappers"
        );
    }

    private async Task TestRealRuntimeParticipantRegistrationContracts(
        ApplicationLifetimeCoordinator coordinator
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

        Type worldMapParticipantType = typeof(WorldMapSystem);
        _test.True(
            typeof(IApplicationShutdownParticipant).IsAssignableFrom(worldMapParticipantType),
            "WorldMapSystem implements the shutdown participant contract"
        );
        _test.Eq(
            ReadPrivateConstant<string>(
                worldMapParticipantType,
                "ApplicationShutdownParticipantId"
            ),
            "world-map-system",
            "WorldMapSystem participant ID is stable"
        );
        _test.Eq(
            ReadPrivateConstant<ApplicationShutdownParticipantStage>(
                worldMapParticipantType,
                "ApplicationShutdownStage"
            ),
            ApplicationShutdownParticipantStage.Runtime,
            "WorldMapSystem participates at the Runtime stage"
        );
        _test.Eq(
            ReadPrivateConstant<int>(
                worldMapParticipantType,
                "ApplicationShutdownOrder"
            ),
            0,
            "WorldMapSystem participant order is stable"
        );
        _test.True(
            worldMapParticipantType
                .GetInterfaceMap(typeof(IApplicationShutdownParticipant))
                .TargetMethods.Any(method =>
                    method.Name.Contains("CloseForApplicationShutdownAsync", StringComparison.Ordinal)
                ),
            "WorldMapSystem exposes the application-shutdown close path"
        );
    }

    private static T ReadPrivateConstant<T>(Type ownerType, string fieldName) =>
        (T)ownerType
            .GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic)
            .GetRawConstantValue();

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
        EscalateTerminalFailure(
            coordinator,
            Root.GetNodeOrNull<Node>("GameSession") == null,
            "coordinator frees and awaits the GameSession tree owner"
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

        GD.PushError($"[test] Application lifetime coordinator regression: {message}");
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
