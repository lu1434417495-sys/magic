using Godot;

public partial class run_jump_arc_regression : SceneTree
{
    public override void _Initialize()
    {
        int exitCode;
        using var runner = new run_jump_arc_regression_typed();
        exitCode = runner.RunForWrapper();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(exitCode);
    }
}
