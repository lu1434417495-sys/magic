using Godot;
using GArray = Godot.Collections.Array;

[Tool]
public partial class MeteorSwarmProfile : Resource
{
    [Export]
    public StringName coverage_shape_id { get; set; } = "square_7x7";

    [Export]
    public int radius { get; set; } = 3;

    [Export]
    public int profile_version { get; set; } = 1;

    [Export]
    public Godot.Collections.Array<MeteorSwarmImpactComponent> impact_components = new();

    [Export]
    public StringName concussed_status_id { get; set; } = "meteor_concussed";

    [Export]
    public GArray terrain_profiles { get; set; } = new();

    [Export]
    public int friendly_fire_soft_expected_hp_percent { get; set; } = 10;

    [Export]
    public int friendly_fire_hard_expected_hp_percent { get; set; } = 25;

    [Export]
    public int friendly_fire_hard_worst_case_hp_percent { get; set; } = 50;

}
