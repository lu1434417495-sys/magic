using Godot;

// Development REPL for the headless text command chain.
// It is intentionally skipped by tests/run_regression_suite.py because it lives under /tools/.
public partial class run_text_command_repl : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        var runner = new GameTextCommandRunner();
        runner.initialize();
        GD.Print("Headless text REPL ready. Type 'help' for commands, 'exit' to quit.");

        while (true)
        {
            string line = System.Console.ReadLine();
            if (string.IsNullOrEmpty(line))
            {
                runner.Dispose();
                Quit(_test.Finish("Text command REPL"));
                return;
            }
            string commandText = line.StripEdges();
            if (commandText == "exit" || commandText == "quit")
            {
                GD.Print("Bye.");
                runner.Dispose();
                Quit(_test.Finish("Text command REPL"));
                return;
            }
            GameTextCommandResult result = runner.ExecuteLine(commandText);
            if (!result.skipped)
                GD.Print(result.Render());
        }
    }
}
