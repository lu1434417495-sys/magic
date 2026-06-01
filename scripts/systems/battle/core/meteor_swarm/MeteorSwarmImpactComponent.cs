using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

[Tool]
[GlobalClass]
public partial class MeteorSwarmImpactComponent : Resource
{
    [Export]
    public StringName component_id { get; set; } = "";

    [Export]
    public StringName role_label { get; set; } = "";

    [Export]
    public StringName damage_tag { get; set; } = "";

    [Export]
    public int base_power { get; set; } = 0;

    [Export]
    public int dice_count { get; set; } = 0;

    [Export]
    public int dice_sides { get; set; } = 0;

    [Export]
    public double ring_weight { get; set; } = 1.0;

    [Export]
    public StringName save_profile_id { get; set; } = "";

    [Export]
    public bool can_crit { get; set; } = false;

    [Export]
    public double mastery_weight { get; set; } = 1.0;

    [Export]
    public int ring_min { get; set; } = 0;

    [Export]
    public int ring_max { get; set; } = 3;

    [Export]
    public GDictionary ring_damage_scale_bp { get; set; } = new();

    public bool applies_to_distance(int distance_from_anchor, bool center_direct = false)
    {
        if (component_id == (StringName)"center_direct")
            return center_direct;
        return distance_from_anchor >= ring_min && distance_from_anchor <= ring_max;
    }

    public double get_damage_scale(int distance_from_anchor)
    {
        var key = distance_from_anchor.ToString();
        var fallback = (int)Math.Round(ring_weight * 10000.0);
        double rawValue = fallback;
        if (ring_damage_scale_bp.ContainsKey(distance_from_anchor))
            rawValue = ring_damage_scale_bp[distance_from_anchor].AsDouble();
        else if (ring_damage_scale_bp.ContainsKey(key))
            rawValue = ring_damage_scale_bp[key].AsDouble();
        return Math.Max(rawValue / 10000.0, 0.0);
    }

    public int get_average_base_damage(int distance_from_anchor)
    {
        var dice_average = Math.Max(dice_count, 0) * (Math.Max(dice_sides, 0) + 1.0) / 2.0;
        return Math.Max(
            (int)Math.Round((base_power + dice_average) * get_damage_scale(distance_from_anchor)),
            0
        );
    }

    public int get_worst_case_base_damage(int distance_from_anchor)
    {
        var dice_worst = Math.Max(dice_count, 0) * Math.Max(dice_sides, 0);
        return Math.Max(
            (int)Math.Round((base_power + dice_worst) * get_damage_scale(distance_from_anchor)),
            0
        );
    }

    public GDictionary to_component_fact(int distance_from_anchor)
    {
        return new GDictionary
        {
            ["component_id"] = component_id.ToString(),
            ["role_label"] = role_label.ToString(),
            ["damage_tag"] = damage_tag.ToString(),
            ["base_power"] = base_power,
            ["dice_count"] = dice_count,
            ["dice_sides"] = dice_sides,
            ["damage_scale"] = get_damage_scale(distance_from_anchor),
            ["save_profile_id"] = save_profile_id.ToString(),
            ["can_crit"] = can_crit,
            ["mastery_weight"] = mastery_weight,
            ["ring_min"] = ring_min,
            ["ring_max"] = ring_max,
        };
    }

}
