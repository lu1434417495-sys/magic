using System.Collections.Generic;
using Godot;
public partial class run_battle_balance_simulation : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(RunDeferred);
    }

    private void RunDeferred()
    {
        int exitCode = Run();
        RequestTestExit(_test.Finish("Battle balance simulation", exitCode));
    }

    private int Run()
    {
        string[] args = OS.GetCmdlineUserArgs();
        if (args.Length == 0)
        {
            ConsoleProcessOutput.WriteFailure(
                "Usage: godot --headless --script tests/battle_runtime/simulation/run_battle_balance_simulation.cs -- <scenario.tres> [profile.tres ...]"
            );
            return 1;
        }

        BattleSimScenarioDef scenarioResource =
            ResourceLoader.Load<BattleSimScenarioDef>(args[0]);
        if (scenarioResource == null)
        {
            ConsoleProcessOutput.WriteFailure($"Failed to load BattleSimScenarioDef from {args[0]}.");
            return 1;
        }
        BattleSimScenarioDefinition scenario = scenarioResource.ToDefinition();
        scenarioResource = null;

        var profiles = new List<BattleSimProfileDefinition>();
        for (int index = 1; index < args.Length; index++)
        {
            BattleSimProfileDef authoredProfile = ResourceLoader.Load<BattleSimProfileDef>(
                args[index]
            );
            BattleSimProfileDefinition profile = authoredProfile?.ToDefinition();
            if (profile == null)
            {
                ConsoleProcessOutput.WriteFailure($"Failed to load BattleSimProfileDef from {args[index]}.");
                return 1;
            }
            profiles.Add(profile);
        }

        var runner = new BattleSimRunner(
            new BattleSimContentProvider(GameSessionTestFactory.GetProcessSnapshot())
        );
        runner.SetProgressLoggingEnabled(true);
        runner.SetProgressLogPath("res://battle_sim_progress.log");
        BattleSimScenarioReport report = runner.RunScenario(scenario, profiles);

        ConsoleProcessOutput.WriteStandard(
            $"[BattleSim] scenario={report.ScenarioId} profiles={report.ProfileEntries.Count} comparisons={report.Comparisons.Count} runs={report.RunCount} completed={report.CompletedRunCount} unfinished={report.UnfinishedRunCount} report_json={report.OutputFiles.ReportJson} traces_jsonl={report.OutputFiles.TurnTraceJsonl}"
        );
        if (!report.IsComplete)
        {
            ConsoleProcessOutput.WriteFailure(
                $"[BattleSim] scenario incomplete: stalled={report.StalledRunCount} iteration_budget_exhausted={report.IterationBudgetExhaustedRunCount} invalid_runtime={report.InvalidRuntimeRunCount}. Diagnostic report was written, but the result is not valid for balance conclusions."
            );
            return 2;
        }
        return 0;
    }
}
