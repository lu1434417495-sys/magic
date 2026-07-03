using Godot;

[GlobalClass]
public partial class TraitDamageResistanceEntryDef : Resource
{
    [Export]
    public StringName damage_tag { get; set; } = "";

    [Export]
    public StringName mitigation_tier { get; set; } = "";
}
