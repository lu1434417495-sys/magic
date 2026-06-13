using Godot;

public partial class run_battle_grid_service_pathfinding_invariants : SceneTree
{
    public override void _Initialize()
    {
        using var runner = new run_battle_grid_service_pathfinding_invariants_typed();
        Quit(runner.RunForWrapper());
    }
}

