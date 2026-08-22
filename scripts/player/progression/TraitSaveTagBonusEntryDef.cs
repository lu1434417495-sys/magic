using Godot;

[GlobalClass]
public partial class TraitSaveTagBonusEntryDef : Resource
{
    [Export]
    public StringName save_tag { get; set; } = "";

    [Export]
    public int bonus { get; set; }

    [Export]
    public StringName stack_mode { get; set; } = "add";
}
