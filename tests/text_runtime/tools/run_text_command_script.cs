using Godot;

// Development script runner for text-runtime scenarios.
// It is intentionally skipped by tests/run_regression_suite.py because it lives under /tools/.
public partial class run_text_command_script : SceneTree
{
    private readonly TestHarness _test = new();

    private const string DefaultScenarioPath =
        "res://tests/text_runtime/scenarios/smoke_startup.txt";

    public override void _Initialize()
    {
        var runner = new GameTextCommandRunner();
        runner.initialize();

        string scenarioPath = DefaultScenarioPath;
        string[] userArgs = OS.GetCmdlineUserArgs();
        if (userArgs.Length > 0)
            scenarioPath = userArgs[0];

        string[] lines;
        Error readError = ReadScenarioLines(scenarioPath, out lines);
        if (readError != Error.Ok)
        {
            _test.Fail($"Failed to read scenario: {scenarioPath}");
            runner.Dispose();
            Quit(_test.Finish("Text command script"));
            return;
        }

        int executedCount = 0;
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            GameTextCommandResult result = runner.ExecuteLine(lines[lineIndex]);
            if (result.skipped)
                continue;
            executedCount += 1;
            GD.Print($"LINE {lineIndex + 1}\n{result.Render()}");
            if (!result.ok)
            {
                _test.Fail($"Scenario failed at line {lineIndex + 1}: {lines[lineIndex]}");
                runner.Dispose();
                Quit(_test.Finish("Text command script"));
                return;
            }
        }

        GD.Print($"Executed {executedCount} command(s)");
        runner.Dispose();
        Quit(_test.Finish("Text command script"));
    }

    private static Error ReadScenarioLines(string scenarioPath, out string[] lines)
    {
        using FileAccess file = FileAccess.Open(scenarioPath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            lines = System.Array.Empty<string>();
            return FileAccess.GetOpenError();
        }
        lines = file.GetAsText().Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
        return Error.Ok;
    }
}
