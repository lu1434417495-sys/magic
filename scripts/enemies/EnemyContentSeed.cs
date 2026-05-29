using Godot;

[GlobalClass]
public partial class EnemyContentSeed : Resource
{
    [Export]
    public Godot.Collections.Array<Resource> enemy_ai_brains = new();

    [Export]
    public Godot.Collections.Array<Resource> enemy_templates = new();

    [Export]
    public Godot.Collections.Array<Resource> wild_encounter_rosters = new();
}
