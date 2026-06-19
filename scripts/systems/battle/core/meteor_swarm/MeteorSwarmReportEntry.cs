using System.Collections.Generic;
using Godot;

internal sealed class MeteorSwarmReportEntry
{
    public StringName entry_type { get; set; } = "";
    public StringName skill_id { get; set; } = "";
    public StringName source_unit_id { get; set; } = "";
    public Vector2I anchor_coord { get; set; } = new(-1, -1);
    public Vector2I nominal_anchor_coord { get; set; } = new(-1, -1);
    public string nominal_plan_signature { get; set; } = "";
    public string final_plan_signature { get; set; } = "";
    public int target_count { get; set; } = 0;
    public int terrain_effect_count { get; set; } = 0;
    public int total_damage { get; set; } = 0;
    public int defeated_count { get; set; } = 0;
    public List<MeteorSwarmComponentFact> component_breakdown { get; set; } = new();
    public List<MeteorSwarmTargetOutcome> target_summaries { get; set; } = new();
    public MeteorSwarmTerrainSummaryFact terrain_summary { get; set; } = new();
    public string text { get; set; } = "";

    internal bool IsValid()
    {
        return entry_type != "" && !string.IsNullOrEmpty(text) && terrain_summary != null;
    }

    internal MeteorSwarmReportEntry Copy()
    {
        var copy = new MeteorSwarmReportEntry
        {
            entry_type = entry_type,
            skill_id = skill_id,
            source_unit_id = source_unit_id,
            anchor_coord = anchor_coord,
            nominal_anchor_coord = nominal_anchor_coord,
            nominal_plan_signature = nominal_plan_signature ?? "",
            final_plan_signature = final_plan_signature ?? "",
            target_count = target_count,
            terrain_effect_count = terrain_effect_count,
            total_damage = total_damage,
            defeated_count = defeated_count,
            terrain_summary = CopyTerrainSummary(terrain_summary),
            text = text ?? "",
        };
        foreach (MeteorSwarmComponentFact component in component_breakdown ?? new())
        {
            if (component != null)
                copy.component_breakdown.Add(CopyComponent(component));
        }
        foreach (MeteorSwarmTargetOutcome targetSummary in target_summaries ?? new())
        {
            if (targetSummary != null)
                copy.target_summaries.Add(CopyTargetSummary(targetSummary));
        }
        return copy;
    }

    private static MeteorSwarmComponentFact CopyComponent(MeteorSwarmComponentFact source) =>
        new()
        {
            component_id = source.component_id,
            role_label = source.role_label,
            damage_tag = source.damage_tag,
            base_power = source.base_power,
            dice_count = source.dice_count,
            dice_sides = source.dice_sides,
            damage_scale = source.damage_scale,
            save_profile_id = source.save_profile_id,
            can_crit = source.can_crit,
            mastery_weight = source.mastery_weight,
            ring_min = source.ring_min,
            ring_max = source.ring_max,
            distance_from_anchor = source.distance_from_anchor,
            damage = source.damage,
            healing = source.healing,
        };

    private static MeteorSwarmTargetOutcome CopyTargetSummary(MeteorSwarmTargetOutcome source)
    {
        var copy = new MeteorSwarmTargetOutcome
        {
            target_unit_id = source.target_unit_id,
            target_coord = source.target_coord,
            target_faction_id = source.target_faction_id,
            distance_from_anchor = source.distance_from_anchor,
            total_damage = source.total_damage,
            total_healing = source.total_healing,
            defeated = source.defeated,
        };
        copy.status_effect_ids.AddRange(source.status_effect_ids ?? new());
        foreach (MeteorSwarmComponentFact component in source.report_component_breakdown ?? new())
        {
            if (component != null)
                copy.report_component_breakdown.Add(CopyComponent(component));
        }
        return copy;
    }

    private static MeteorSwarmTerrainSummaryFact CopyTerrainSummary(
        MeteorSwarmTerrainSummaryFact source
    )
    {
        if (source == null)
            return null;
        return new MeteorSwarmTerrainSummaryFact
        {
            coverage_shape_id = source.coverage_shape_id,
            radius = source.radius,
            affected_coord_count = source.affected_coord_count,
            terrain_effect_count = source.terrain_effect_count,
            crater_count = source.crater_count,
            rubble_count = source.rubble_count,
            dust_count = source.dust_count,
        };
    }
}
