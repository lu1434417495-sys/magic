using Godot;

// Development REPL for the headless text command chain.
// It is intentionally skipped by tests/run_regression_suite.py because it lives under /tools/.
public partial class run_text_command_repl : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        var runner = new GameTextCommandRunner();
        runner.initialize();
        ConsoleProcessOutput.WriteStandard("Headless text REPL ready. Type 'help' for commands, 'exit' to quit.");

        while (true)
        {
            string line = System.Console.ReadLine();
            if (string.IsNullOrEmpty(line))
            {
                runner.Dispose();
                RequestTestExit(_test.Finish("Text command REPL"));
                return;
            }
            string commandText = line.StripEdges();
            if (commandText == "exit" || commandText == "quit")
            {
                ConsoleProcessOutput.WriteStandard("Bye.");
                runner.Dispose();
                RequestTestExit(_test.Finish("Text command REPL"));
                return;
            }
            GameTextCommandResult result = runner.ExecuteLine(commandText);
            if (!result.skipped)
                ConsoleProcessOutput.WriteStandard(result.Render());
        }
    }
}
