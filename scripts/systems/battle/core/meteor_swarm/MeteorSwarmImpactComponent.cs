using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

[Tool]
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

    public bool AppliesToDistance(int distance_from_anchor, bool center_direct = false)
    {
        if (component_id == (StringName)"center_direct")
            return center_direct;
        return distance_from_anchor >= ring_min && distance_from_anchor <= ring_max;
    }

    public double GetDamageScale(int distance_from_anchor)
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

    public int GetAverageBaseDamage(int distance_from_anchor)
    {
        var dice_average = Math.Max(dice_count, 0) * (Math.Max(dice_sides, 0) + 1.0) / 2.0;
        return Math.Max(
            (int)Math.Round((base_power + dice_average) * GetDamageScale(distance_from_anchor)),
            0
        );
    }

    public int GetWorstCaseBaseDamage(int distance_from_anchor)
    {
        var dice_worst = Math.Max(dice_count, 0) * Math.Max(dice_sides, 0);
        return Math.Max(
            (int)Math.Round((base_power + dice_worst) * GetDamageScale(distance_from_anchor)),
            0
        );
    }

}
