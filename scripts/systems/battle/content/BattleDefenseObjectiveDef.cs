using Godot;

[GlobalClass]
public partial class BattleDefenseObjectiveDef : BattleObjectiveDef
{
    [Export]
    public StringName target_actor_id { get; set; } = "";

    [Export(PropertyHint.Range, "5,100000,5")]
    public int duration_tu { get; set; } = 200;

    internal override BattleObjectiveDefinition ToDefinition() =>
        new BattleDefenseObjectiveDefinition(target_actor_id, duration_tu);
}
