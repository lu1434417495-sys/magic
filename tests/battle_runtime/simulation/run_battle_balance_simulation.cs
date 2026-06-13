using System.Collections.Generic;
using Godot;
public partial class run_battle_balance_simulation : SceneTree
{
    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        string[] args = OS.GetCmdlineUserArgs();
        if (args.Length == 0)
        {
            GD.PushError(
                "Usage: godot --headless --script tests/battle_runtime/simulation/run_battle_balance_simulation.cs -- <scenario.tres> [profile.tres ...]"
            );
            return 1;
        }

        BattleSimScenarioDef scenario = ResourceLoader.Load<BattleSimScenarioDef>(args[0]);
        if (scenario == null)
        {
            GD.PushError($"Failed to load BattleSimScenarioDef from {args[0]}.");
            return 1;
        }

        var profiles = new List<BattleSimProfileDef>();
        for (int index = 1; index < args.Length; index++)
        {
            BattleSimProfileDef profile = ResourceLoader.Load<BattleSimProfileDef>(args[index]);
            if (profile == null)
            {
                GD.PushError($"Failed to load BattleSimProfileDef from {args[index]}.");
                return 1;
            }
            profiles.Add(profile);
        }

        var runner = new BattleSimRunner();
        runner.SetProgressLoggingEnabled(true);
        runner.SetProgressLogPath("res://battle_sim_progress.log");
        BattleSimScenarioReport report = runner.RunScenario(scenario, profiles);

        GD.Print(
            $"[BattleSim] scenario={report.ScenarioId} profiles={report.ProfileEntries.Count} comparisons={report.Comparisons.Count} report_json={report.OutputFiles.ReportJson} traces_jsonl={report.OutputFiles.TurnTraceJsonl}"
        );
        return 0;
    }
}
