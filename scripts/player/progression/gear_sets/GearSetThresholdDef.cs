using Godot;

[GlobalClass]
public sealed partial class GearSetThresholdDef : Resource
{
    [Export]
    public StringName threshold_id { get; set; } = "";

    [Export]
    public int required_piece_count { get; set; }

    [Export]
    public string display_name { get; set; } = "";

    [Export(PropertyHint.MultilineText)]
    public string description { get; set; } = "";

    [Export]
    public Godot.Collections.Array<StringName> mandatory_member_item_ids { get; set; } = new();

    [Export]
    public Godot.Collections.Array<AttributeModifier> attribute_modifiers { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> granted_trait_ids { get; set; } = new();
}
