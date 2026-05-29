using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class MeteorSwarmTargetPlan : RefCounted
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
    public Godot.Collections.Array<Vector2I> affected_coords { get; set; } = new();
    public GDictionary ring_by_coord { get; set; } = new();
    public Godot.Collections.Array<StringName> target_unit_ids { get; set; } = new();
    public GDictionary unit_distance_by_id { get; set; } = new();
    public GDictionary unit_primary_coord_by_id { get; set; } = new();
    public bool drift_applied { get; set; } = false;
    public Vector2I drift_from_coord { get; set; } = new(-1, -1);
    public string nominal_plan_signature { get; set; } = "";
    public string final_plan_signature { get; set; } = "";

    public int get_distance_for_unit(StringName unit_id)
    {
        if (!unit_distance_by_id.ContainsKey(unit_id))
            return 999999;
        var value = unit_distance_by_id[unit_id];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 999999;
    }

    public Vector2I get_primary_coord_for_unit(StringName unit_id)
    {
        if (!unit_primary_coord_by_id.ContainsKey(unit_id))
            return new Vector2I(-1, -1);
        var value = unit_primary_coord_by_id[unit_id];
        return value.VariantType == Variant.Type.Vector2I
            ? value.AsVector2I()
            : new Vector2I(-1, -1);
    }

    public int get_ring_for_coord(Vector2I coord)
    {
        if (!ring_by_coord.ContainsKey(coord))
            return 999999;
        var value = ring_by_coord[coord];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 999999;
    }

    public GDictionary to_dict()
    {
        var ring_payload = new GDictionary();
        foreach (var coord in affected_coords)
            ring_payload[$"{coord.X}:{coord.Y}"] = get_ring_for_coord(coord);

        return new GDictionary
        {
            ["skill_id"] = skill_id.ToString(),
            ["source_unit_id"] = source_unit_id.ToString(),
            ["final_anchor_coord"] = final_anchor_coord,
            ["nominal_anchor_coord"] = nominal_anchor_coord,
            ["coverage_shape_id"] = coverage_shape_id.ToString(),
            ["radius"] = radius,
            ["affected_coords"] = affected_coords.Duplicate(),
            ["ring_by_coord"] = ring_payload,
            ["target_unit_ids"] = target_unit_ids.Duplicate(),
            ["drift_applied"] = drift_applied,
            ["drift_from_coord"] = drift_from_coord,
            ["nominal_plan_signature"] = nominal_plan_signature,
            ["final_plan_signature"] = final_plan_signature,
        };
    }
}
