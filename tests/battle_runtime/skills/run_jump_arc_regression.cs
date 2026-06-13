using Godot;

public partial class run_jump_arc_regression : SceneTree
{
    public override void _Initialize()
    {
        using var runner = new run_jump_arc_regression_typed();
        Quit(runner.RunForWrapper());
    }
}

