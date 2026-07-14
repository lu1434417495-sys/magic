using Godot;

[GlobalClass]
public sealed partial class CombatEffectSlotWeightDef : Resource
{
    [Export] public StringName slot_id { get; set; } = "";
    [Export] public int weight { get; set; }
}
