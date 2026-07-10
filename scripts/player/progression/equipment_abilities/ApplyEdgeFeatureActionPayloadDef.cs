using Godot;

[GlobalClass]
public sealed partial class ApplyEdgeFeatureActionPayloadDef : Resource
{
    [Export] public StringName from_selector { get; set; } = "source";
    [Export] public StringName to_selector { get; set; } = "attack_target";
    [Export] public int duration_tu { get; set; }
    [Export] public int max_active_edges { get; set; }
    [Export] public bool refresh_existing { get; set; } = true;
    [Export] public bool require_adjacent { get; set; } = true;
    [Export] public StringName feature_kind { get; set; } = "wall";
    [Export] public StringName render_kind { get; set; } = "wall";
    [Export] public int render_layers { get; set; } = 1;
    [Export] public bool blocks_move { get; set; } = true;
    [Export] public bool blocks_occupancy { get; set; } = true;
    [Export] public bool blocks_los { get; set; }
    [Export] public StringName interaction_kind { get; set; } = "none";
    [Export] public StringName state_tag { get; set; } = "";
}
