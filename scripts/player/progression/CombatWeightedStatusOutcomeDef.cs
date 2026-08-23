using Godot;

[GlobalClass]
public partial class CombatWeightedStatusOutcomeDef : RefCounted
{
    [Export]
    public StringName outcome_id { get; set; } = "";

    [Export]
    public int weight { get; set; } = 1;

    [Export]
    public CombatEffectDef status_effect { get; set; }
}
