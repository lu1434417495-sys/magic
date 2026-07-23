using Godot;

[GlobalClass]
public partial class BattleScenarioActorDef : Resource
{
    [Export]
    public StringName actor_id { get; set; } = "";

    [Export]
    public StringName template_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export]
    public StringName spawn_zone_id { get; set; } = "";

    [Export]
    public BattleMapEdge spawn_edge { get; set; } = BattleMapEdge.Unknown;

    [Export(PropertyHint.Range, "1,32,1")]
    public int spawn_depth { get; set; } = 1;

    internal BattleScenarioActorDefinition ToDefinition() =>
        new(
            actor_id,
            template_id,
            display_name,
            spawn_zone_id,
            spawn_edge,
            spawn_depth
        );
}
