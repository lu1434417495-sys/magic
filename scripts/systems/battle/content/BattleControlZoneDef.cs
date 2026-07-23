using Godot;

[GlobalClass]
public partial class BattleControlZoneDef : Resource
{
    [Export]
    public StringName zone_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export]
    public BattleMapEdge placement_edge { get; set; } = BattleMapEdge.Unknown;

    [Export(PropertyHint.Range, "1,32,1")]
    public int placement_depth { get; set; } = 1;

    internal BattleControlZoneDefinition ToDefinition() =>
        new(
            zone_id,
            display_name,
            placement_edge,
            placement_depth
        );
}
