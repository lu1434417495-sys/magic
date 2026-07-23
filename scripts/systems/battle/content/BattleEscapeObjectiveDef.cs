using Godot;

[GlobalClass]
public partial class BattleEscapeObjectiveDef : BattleObjectiveDef
{
    [Export]
    public StringName exit_zone_id { get; set; } = "";

    [Export]
    public BattleMapEdge exit_edge { get; set; } = BattleMapEdge.Unknown;

    [Export(PropertyHint.Range, "1,32,1")]
    public int exit_depth { get; set; } = 1;

    internal override BattleObjectiveDefinition ToDefinition() =>
        new BattleEscapeObjectiveDefinition(exit_zone_id, exit_edge, exit_depth);
}
