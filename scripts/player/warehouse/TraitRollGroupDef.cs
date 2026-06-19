using Godot;

[GlobalClass]
public partial class TraitRollGroupDef : Resource
{
    [Export]
    public StringName group_id { get; set; } = "";

    [Export(PropertyHint.Range, "1,99,1")]
    public int roll_count { get; set; } = 1;

    [Export]
    public Godot.Collections.Array<TraitRollGroupEntryDef> entries { get; set; } = new();
}
