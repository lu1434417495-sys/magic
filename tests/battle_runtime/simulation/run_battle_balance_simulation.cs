using System;
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
        int exitCode = 1;
        try
        {
            exitCode = Run();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected battle balance simulation exception: {exception}");
            exitCode = 1;
        }
        finally
        {
            RequestTestExit(_test.Finish("Battle balance simulation", exitCode));
        }
    }

    private int Run()
    {
        string[] args = OS.GetCmdlineUserArgs();
        if (args.Length == 0)
        {
            ConsoleProcessOutput.WriteFailure(
                "Usage: godot --headless --script tests/battle_runtime/simulation/run_battle_balance_simulation.cs -- <scenario_id> [profile_id ...]"
            );
            return 1;
        }

        var scenarioCatalog = new BattleSimContentCatalog();
        scenarioCatalog.Rebuild();
        if (!scenarioCatalog.TryGetScenario(args[0], out BattleSimScenarioDefinition scenario))
        {
            ConsoleProcessOutput.WriteFailure($"Failed to load BattleSim scenario id {args[0]}.");
            return 1;
        }

        var profileRegistry = new BattleSimProfileContentRegistry();
        if (OS.HasEnvironment("BATTLE_SIM_PROFILE_DIRECTORY"))
            profileRegistry.LoadFromDirectory(OS.GetEnvironment("BATTLE_SIM_PROFILE_DIRECTORY").StripEdges());
        else
            profileRegistry.Rebuild();
        var profiles = new List<BattleSimProfileDefinition>();
        for (int index = 1; index < args.Length; index++)
        {
            if (!profileRegistry.TryGetDefinition(args[index], out BattleSimProfileDefinition profile))
            {
                ConsoleProcessOutput.WriteFailure($"Failed to load BattleSim profile id {args[index]}.");
                return 1;
            }
            profiles.Add(profile);
        }

        var runner = new BattleSimRunner(
            new BattleSimContentProvider(GameSessionTestFactory.GetProcessSnapshot())
        );
        runner.SetProgressLoggingEnabled(true);
        // user:// keeps the run out of the work tree; the resolved absolute path is printed below.
        runner.SetProgressLogPath("user://simulation_reports/battle_sim_progress.log");
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
