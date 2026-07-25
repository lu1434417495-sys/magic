using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_sim_exception_cleanup_regression : LifecycleTestSceneTree
{
    private sealed class ThrowingTerrainGenerator : BattleTerrainGenerator
    {
        internal InvalidOperationException ExpectedFailure { get; } =
            new("expected battle simulation terrain failure");

        internal override BattleTerrainLayout GenerateTyped(
            EncounterAnchorData encounterAnchor,
            long seed,
            GDictionary context
        )
        {
            throw ExpectedFailure;
        }
    }

    private const string ScenarioPath =
        "res://data/configs/battle_sim/scenarios/ai_vs_ai_duel_example.tres";
    private const string TraceEnvironmentName = "SIM_LOOP_TRACE";

    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            TestExecutionLoopFailureRestoresPreviousRecorder();
            TestSimulationFailureDisposesRuntime();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled battle simulation cleanup regression exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle simulation exception cleanup regression"));
    }

    private void TestExecutionLoopFailureRestoresPreviousRecorder()
    {
        bool hadPreviousTraceEnvironment = OS.HasEnvironment(TraceEnvironmentName);
        string previousTraceEnvironment = hadPreviousTraceEnvironment
            ? OS.GetEnvironment(TraceEnvironmentName)
            : "";
        AiTraceRecorder previousRecorder = AiTraceRecorder.GetInstance();
        var outerRecorder = new AiTraceRecorder();
        var expectedFailure = new InvalidOperationException("expected execution step failure");
        var runtime = new BattleRuntimeModule();
        BattleState state = BattleTestFixture.BuildFlatState(
            "battle_sim_trace_cleanup",
            new Vector2I(3, 3)
        );
        state.PhaseKind = BattlePhaseKind.TimelineRunning;
        runtime.SetupStateForTests(state);

        try
        {
            OS.SetEnvironment(TraceEnvironmentName, "1");
            AiTraceRecorder.SetInstance(outerRecorder);
            var loop = new BattleSimExecutionLoop(
                (_, _, _, _) => throw expectedFailure
            );

            Exception observedFailure = null;
            try
            {
                loop.Run(
                    runtime,
                    state,
                    maxIterations: 1,
                    maxIdleLoops: 1
                );
            }
            catch (Exception exception)
            {
                observedFailure = exception;
            }

            _test.True(
                ReferenceEquals(observedFailure, expectedFailure),
                "Execution loop should preserve the failing step exception."
            );
            _test.True(
                ReferenceEquals(AiTraceRecorder.GetInstance(), outerRecorder),
                "Execution-loop failure should restore the recorder that owned the outer scope."
            );
        }
        finally
        {
            runtime.Dispose();
            BattleTestFixture.DisposeBattleState(state);
            AiTraceRecorder.SetInstance(previousRecorder);
            if (hadPreviousTraceEnvironment)
                OS.SetEnvironment(TraceEnvironmentName, previousTraceEnvironment);
            else
                OS.UnsetEnvironment(TraceEnvironmentName);
        }
    }

    private void TestSimulationFailureDisposesRuntime()
    {
        BattleRuntimeModule createdRuntime = null;

        try
        {
            ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
            using var resourceLoader = new TestContentResourceLoader();
            BattleSimScenarioDef scenarioResource =
                resourceLoader.LoadCanonical<BattleSimScenarioDef>(ScenarioPath);
            BattleSimScenarioDefinition scenarioDefinition = scenarioResource.ToDefinition();
            using var contentProvider = new BattleSimContentProvider(snapshot);
            using var terrainGenerator = new ThrowingTerrainGenerator();
            var runner = new BattleSimRunner(
                contentProvider,
                () => createdRuntime = new BattleRuntimeModule()
            );
            runner.Setup(contentProvider, terrainGenerator);

            Exception runFailure = null;
            try
            {
                runner.RunScenario(
                    scenarioDefinition,
                    new List<BattleSimProfileDefinition>
                    {
                        snapshot.BattleSimProfiles["baseline"],
                    }
                );
            }
            catch (Exception exception)
            {
                runFailure = exception;
            }

            _test.True(
                ReferenceEquals(runFailure, terrainGenerator.ExpectedFailure),
                "Runner should preserve the terrain failure that aborted the simulation."
            );
            _test.True(createdRuntime != null, "Runner should create a battle runtime.");
            _test.True(
                createdRuntime?.IsDisposed == true,
                "Runner should dispose its runtime after a simulation exception."
            );
            _test.True(
                createdRuntime?.GetState() == null,
                "Disposed simulation runtime should release its battle state."
            );
            _test.False(
                createdRuntime?.HasAiRuntimeBorrowers == true,
                "Disposed simulation runtime should release AI borrowers."
            );
            _test.False(
                createdRuntime?.HasRuntimeSidecarBindings == true,
                "Disposed simulation runtime should release sidecar bindings."
            );
            _test.False(
                terrainGenerator.IsDisposed,
                "Runner runtime should not dispose the caller-owned shared terrain generator."
            );
        }
        finally
        {
            if (createdRuntime != null && !createdRuntime.IsDisposed)
            {
                try
                {
                    createdRuntime.Dispose();
                }
                catch (Exception cleanupFailure)
                {
                    _test.Fail($"Fallback runtime cleanup failed: {cleanupFailure}");
                }
            }
        }
    }
}
