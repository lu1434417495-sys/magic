using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class BattleNodeOperationObjectiveDef : BattleObjectiveDef
{
    [Export]
    public Godot.Collections.Array<BattleOperationNodeDef> operation_nodes { get; set; } =
        new();

    internal override BattleObjectiveDefinition ToDefinition()
    {
        var definitions = new List<BattleOperationNodeDefinition>();
        foreach (
            BattleOperationNodeDef node in
            operation_nodes ?? new Godot.Collections.Array<BattleOperationNodeDef>()
        )
        {
            if (node != null)
                definitions.Add(node.ToDefinition());
        }
        return new BattleNodeOperationObjectiveDefinition(definitions);
    }
}
