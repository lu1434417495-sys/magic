using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class MeteorSwarmCommitResult : RefCounted
{
    public int schema_version { get; set; } = 1;
    public MeteorSwarmTargetPlan plan { get; set; }
    public Godot.Collections.Array<MeteorSwarmTargetOutcome> target_outcomes { get; set; } = new();
    public GArray terrain_effects { get; set; } = new();
    public GArray report_entries { get; set; } = new();
    public Godot.Collections.Array<string> log_lines { get; set; } = new();
    public Godot.Collections.Array<StringName> changed_unit_ids { get; set; } = new();
    public Godot.Collections.Array<Vector2I> changed_coords { get; set; } = new();
    public int total_damage { get; set; } = 0;
    public int total_healing { get; set; } = 0;
    public Godot.Collections.Array<StringName> defeated_unit_ids { get; set; } = new();

    public void add_changed_unit_id(StringName unit_id)
    {
        if (unit_id == (StringName)"" || changed_unit_ids.Contains(unit_id))
            return;
        changed_unit_ids.Add(unit_id);
    }

    public void add_changed_coord(Vector2I coord)
    {
        if (changed_coords.Contains(coord))
            return;
        changed_coords.Add(coord);
    }

    public void add_defeated_unit_id(StringName unit_id)
    {
        if (unit_id == (StringName)"" || defeated_unit_ids.Contains(unit_id))
            return;
        defeated_unit_ids.Add(unit_id);
    }
}
