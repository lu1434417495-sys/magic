using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[Tool]
[GlobalClass]
public partial class MeteorSwarmProfile : Resource
{
	[Export] public StringName coverage_shape_id { get; set; } = "square_7x7";
	[Export] public int radius { get; set; } = 3;
	[Export] public int profile_version { get; set; } = 1;
	[Export] public Godot.Collections.Array<MeteorSwarmImpactComponent> impact_components = new();
	[Export] public StringName concussed_status_id { get; set; } = "meteor_concussed";
	[Export] public GArray terrain_profiles { get; set; } = new();
	[Export] public int friendly_fire_soft_expected_hp_percent { get; set; } = 10;
	[Export] public int friendly_fire_hard_expected_hp_percent { get; set; } = 25;
	[Export] public int friendly_fire_hard_worst_case_hp_percent { get; set; } = 50;

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
		foreach (var terrain_profile_variant in terrain_profiles)
		{
			if (terrain_profile_variant.VariantType != Variant.Type.Dictionary)
				continue;
			var terrain_profile = terrain_profile_variant.AsGodotDictionary();
			var ring_min = _get_int(terrain_profile, "ring_min", _get_int(terrain_profile, new StringName("ring_min"), 0));
			var ring_max = _get_int(terrain_profile, "ring_max", _get_int(terrain_profile, new StringName("ring_max"), 0));
			if (ring >= ring_min && ring <= ring_max)
				result.Add(terrain_profile.Duplicate(true));
		}
		return result;
	}

	private static int _get_int(GDictionary source, Variant key, int fallback)
	{
		if (!source.ContainsKey(key))
			return fallback;
		var value = source[key];
		return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
	}
}
