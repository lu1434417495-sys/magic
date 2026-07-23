using Godot;

[GlobalClass]
public partial class BattleRescueObjectiveDef : BattleObjectiveDef
{
    [Export]
    public StringName target_actor_id { get; set; } = "";

    internal override BattleObjectiveDefinition ToDefinition() =>
        new BattleRescueObjectiveDefinition(target_actor_id);
}
