using Godot;

public partial class run_ci_failure_probe_regression : SceneTree
{
    public override void _Initialize()
    {
        GD.PushError("Intentional CI failure probe: remove this test after verifying PR CI failure detection.");
        Quit(1);
    }
}
