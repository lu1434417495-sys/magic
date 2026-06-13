using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class MeteorSwarmCommitResult
{
    public int schema_version { get; set; } = 1;
    public MeteorSwarmTargetPlan plan { get; set; }
    public List<MeteorSwarmTargetOutcome> target_outcomes { get; set; } = new();
    public List<MeteorSwarmTerrainEffectFact> terrain_effects { get; set; } = new();
    public List<GDictionary> report_entries { get; set; } = new();
    public List<string> log_lines { get; set; } = new();
    public List<StringName> changed_unit_ids { get; set; } = new();
    public List<Vector2I> changed_coords { get; set; } = new();
    public int total_damage { get; set; } = 0;
    public int total_healing { get; set; } = 0;
    public List<StringName> defeated_unit_ids { get; set; } = new();

    internal void AddChangedUnitId(StringName unit_id)
    {
        if (unit_id == (StringName)"" || changed_unit_ids.Contains(unit_id))
            return;
        changed_unit_ids.Add(unit_id);
    }

    internal void AddChangedCoord(Vector2I coord)
    {
        if (changed_coords.Contains(coord))
            return;
        changed_coords.Add(coord);
    }

    internal void AddDefeatedUnitId(StringName unit_id)
    {
        if (unit_id == (StringName)"" || defeated_unit_ids.Contains(unit_id))
            return;
        defeated_unit_ids.Add(unit_id);
    }
}
