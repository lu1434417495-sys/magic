using Godot;

[GlobalClass]
public partial class TraitRollGroupEntryDef : Resource
{
    [Export]
    public StringName trait_id { get; set; } = "";

    [Export(PropertyHint.Range, "1,999999,1")]
    public int weight { get; set; } = 1;

    [Export]
    public StringName exclusive_group { get; set; } = "";

    internal TraitRollGroupEntryDefinition ToDefinition() =>
        TraitRollGroupEntryDefinition.FromResource(this);
}
