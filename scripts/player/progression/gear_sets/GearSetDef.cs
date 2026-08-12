using Godot;

[GlobalClass]
public sealed partial class GearSetDef : Resource
{
    [Export]
    public StringName gear_set_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export(PropertyHint.MultilineText)]
    public string description { get; set; } = "";

    [Export]
    public Godot.Collections.Array<StringName> member_item_ids { get; set; } = new();

    [Export]
    public StringName usage_anchor_item_id { get; set; } = "";

    [Export]
    public Godot.Collections.Array<GearSetThresholdDef> thresholds { get; set; } = new();
}
