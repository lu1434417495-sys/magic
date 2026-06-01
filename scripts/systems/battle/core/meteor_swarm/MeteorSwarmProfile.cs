using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[Tool]
[GlobalClass]
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

    public GArray get_impact_components()
    {
        var components = new GArray();
        foreach (var component in impact_components)
        {
            if (component != null)
                components.Add(component);
        }
        return components;
    }

    public GArray get_terrain_profiles_for_ring(int ring)
    {
        var result = new GArray();
        foreach (var terrain_profile_option in terrain_profiles)
        {
            var terrain_profile = terrain_profile_option.AsGodotDictionary();
            var ring_min = _get_int(
                terrain_profile,
                "ring_min",
                _get_int(terrain_profile, new StringName("ring_min"), 0)
            );
            var ring_max = _get_int(
                terrain_profile,
                "ring_max",
                _get_int(terrain_profile, new StringName("ring_max"), 0)
            );
            if (ring >= ring_min && ring <= ring_max)
                result.Add(terrain_profile.Duplicate(true));
        }
        return result;
    }

    private static int _get_int(GDictionary source, object key, int fallback)
    {
        if (source == null || key == null)
            return fallback;
        if (key is StringName stringNameKey)
        {
            if (!source.ContainsKey(stringNameKey))
                return fallback;
            return source[stringNameKey].AsInt32();
        }
        string stringKey = key.ToString();
        if (!source.ContainsKey(stringKey))
            return fallback;
        return source[stringKey].AsInt32();
    }
}
