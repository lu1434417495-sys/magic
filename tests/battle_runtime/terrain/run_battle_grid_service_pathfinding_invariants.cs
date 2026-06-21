using Godot;

public partial class run_battle_grid_service_pathfinding_invariants : SceneTree
{
    public override void _Initialize()
    {
        int exitCode = BattleGridServicePathfindingInvariantsRunner.RunAll();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(exitCode);
    }
}
