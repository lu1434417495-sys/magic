using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal sealed class MeteorSwarmTargetPlan
{
    public StringName skill_id { get; set; } = "mage_meteor_swarm";
    public StringName source_unit_id { get; set; } = "";
    public BattleUnitState source_unit { get; set; }
    public SkillDef skill_def { get; set; }
    public MeteorSwarmProfile profile { get; set; }
    public Vector2I final_anchor_coord { get; set; } = new(-1, -1);
    public Vector2I nominal_anchor_coord { get; set; } = new(-1, -1);
    public StringName coverage_shape_id { get; set; } = "square_7x7";
    public int radius { get; set; } = 3;
    public List<Vector2I> affected_coords { get; set; } = new();
    public Dictionary<Vector2I, int> ring_by_coord { get; set; } = new();
    public List<StringName> target_unit_ids { get; set; } = new();
    public Dictionary<StringName, int> unit_distance_by_id { get; set; } = new();
    public Dictionary<StringName, Vector2I> unit_primary_coord_by_id { get; set; } = new();
    public bool drift_applied { get; set; } = false;
    public Vector2I drift_from_coord { get; set; } = new(-1, -1);
    public string nominal_plan_signature { get; set; } = "";
    public string final_plan_signature { get; set; } = "";

    internal int GetDistanceForUnit(StringName unit_id)
    {
        if (!unit_distance_by_id.TryGetValue(unit_id, out int distance))
            return 999999;
        return distance;
    }

    internal Vector2I GetPrimaryCoordForUnit(StringName unit_id)
    {
        if (!unit_primary_coord_by_id.TryGetValue(unit_id, out Vector2I coord))
            return new Vector2I(-1, -1);
        return coord;
    }

    internal int GetRingForCoord(Vector2I coord)
    {
        if (!ring_by_coord.TryGetValue(coord, out int ring))
            return 999999;
        return ring;
    }

    internal GDictionary ToDictionary()
    {
        var ring_payload = new GDictionary();
        foreach (var coord in affected_coords)
            ring_payload[$"{coord.X}:{coord.Y}"] = GetRingForCoord(coord);
        var affectedCoords = new GVector2IArray();
        foreach (Vector2I coord in affected_coords)
            affectedCoords.Add(coord);
        var targetUnitIds = new GStringNameArray();
        foreach (StringName unitId in target_unit_ids)
            targetUnitIds.Add(unitId);

        return new GDictionary
        {
            ["skill_id"] = skill_id.ToString(),
            ["source_unit_id"] = source_unit_id.ToString(),
            ["final_anchor_coord"] = final_anchor_coord,
            ["nominal_anchor_coord"] = nominal_anchor_coord,
            ["coverage_shape_id"] = coverage_shape_id.ToString(),
            ["radius"] = radius,
            ["affected_coords"] = affectedCoords,
            ["ring_by_coord"] = ring_payload,
            ["target_unit_ids"] = targetUnitIds,
            ["drift_applied"] = drift_applied,
            ["drift_from_coord"] = drift_from_coord,
            ["nominal_plan_signature"] = nominal_plan_signature,
            ["final_plan_signature"] = final_plan_signature,
        };
    }
}
