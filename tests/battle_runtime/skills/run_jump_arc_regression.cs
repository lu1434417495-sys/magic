using Godot;

public partial class run_jump_arc_regression : LifecycleTestSceneTree
{
    public override void _Initialize()
    {
        RequestTestExit(run_jump_arc_regression_typed.RunForWrapper());
    }
}
