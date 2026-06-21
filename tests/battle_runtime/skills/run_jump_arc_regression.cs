using Godot;

public partial class run_jump_arc_regression : SceneTree
{
    public override void _Initialize()
    {
        int exitCode = JumpArcRegressionRunner.RunAll();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(exitCode);
    }
}
