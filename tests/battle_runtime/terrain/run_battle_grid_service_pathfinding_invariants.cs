using Godot;

public partial class run_battle_grid_service_pathfinding_invariants : SceneTree
{
    public override void _Initialize()
    {
        int exitCode;
        using var runner = new run_battle_grid_service_pathfinding_invariants_typed();
        exitCode = runner.RunForWrapper();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(exitCode);
    }
}
