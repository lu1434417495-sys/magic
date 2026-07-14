using Godot;

[GlobalClass]
public partial class TraitSaveBonusEntryDef : Resource
{
    [Export]
    public StringName save_ability { get; set; } = "";

    [Export]
    public int bonus { get; set; }
}
