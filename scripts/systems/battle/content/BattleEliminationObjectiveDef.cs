using Godot;

[GlobalClass]
public partial class BattleEliminationObjectiveDef : BattleObjectiveDef
{
    internal override BattleObjectiveDefinition ToDefinition() =>
        BattleEliminationObjectiveDefinition.Instance;
}
