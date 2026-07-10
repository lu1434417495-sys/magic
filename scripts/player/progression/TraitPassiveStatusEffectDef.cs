using Godot;

[GlobalClass]
public partial class TraitPassiveStatusEffectDef : Resource
{
    [Export]
    public StringName status_id { get; set; } = "";

    [Export(PropertyHint.Range, "1,99,1")]
    public int power { get; set; } = 1;

    [Export(PropertyHint.Range, "1,99,1")]
    public int stacks { get; set; } = 1;

    [Export]
    public string display_label { get; set; } = "";

    [Export]
    public bool undispellable { get; set; } = true;

    [Export]
    public bool counts_as_debuff_override { get; set; }

    [Export]
    public bool counts_as_debuff { get; set; }

    [Export]
    public Godot.Collections.Array<StringName> save_immunity_tags { get; set; } = new();
}
