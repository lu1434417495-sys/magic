using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class BattleControlObjectiveDef : BattleObjectiveDef
{
    [Export]
    public Godot.Collections.Array<BattleControlZoneDef> control_zones { get; set; } =
        new();

    [Export(PropertyHint.Range, "5,100000,5")]
    public int score_target { get; set; } = 100;

    internal override BattleObjectiveDefinition ToDefinition()
    {
        var definitions = new List<BattleControlZoneDefinition>();
        foreach (
            BattleControlZoneDef zone in
            control_zones ?? new Godot.Collections.Array<BattleControlZoneDef>()
        )
        {
            if (zone != null)
                definitions.Add(zone.ToDefinition());
        }
        return new BattleControlObjectiveDefinition(definitions, score_target);
    }
}
