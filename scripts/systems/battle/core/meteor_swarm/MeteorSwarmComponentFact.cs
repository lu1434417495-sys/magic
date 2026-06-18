using System.Collections.Generic;
using Godot;

internal sealed class MeteorSwarmComponentFact
{
    public StringName component_id { get; set; } = "";
    public StringName role_label { get; set; } = "";
    public StringName damage_tag { get; set; } = "";
    public int base_power { get; set; } = 0;
    public int dice_count { get; set; } = 0;
    public int dice_sides { get; set; } = 0;
    public double damage_scale { get; set; } = 0.0;
    public StringName save_profile_id { get; set; } = "";
    public bool can_crit { get; set; } = false;
    public double mastery_weight { get; set; } = 0.0;
    public int ring_min { get; set; } = 0;
    public int ring_max { get; set; } = 0;
    public int distance_from_anchor { get; set; } = 0;
    public int damage { get; set; } = 0;
    public int healing { get; set; } = 0;

    internal Dictionary<string, object> ToTraceDictionary()
    {
        return new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["component_id"] = component_id.ToString(),
            ["role_label"] = role_label.ToString(),
            ["damage_tag"] = damage_tag.ToString(),
            ["base_power"] = base_power,
            ["dice_count"] = dice_count,
            ["dice_sides"] = dice_sides,
            ["damage_scale"] = damage_scale,
            ["save_profile_id"] = save_profile_id.ToString(),
            ["can_crit"] = can_crit,
            ["mastery_weight"] = mastery_weight,
            ["ring_min"] = ring_min,
            ["ring_max"] = ring_max,
            ["distance_from_anchor"] = distance_from_anchor,
            ["damage"] = damage,
            ["healing"] = healing,
        };
    }
}
