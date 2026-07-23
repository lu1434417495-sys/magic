using Godot;

[GlobalClass]
public partial class BattleBossObjectiveDef : BattleObjectiveDef
{
    [Export]
    public StringName target_actor_id { get; set; } = "";

    internal override BattleObjectiveDefinition ToDefinition() =>
        new BattleBossObjectiveDefinition(target_actor_id);
}
