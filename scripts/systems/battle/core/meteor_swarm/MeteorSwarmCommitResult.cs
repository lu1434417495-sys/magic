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

	public GDictionary to_common_outcome_payload()
	{
		return new GDictionary
		{
			["commit_schema_id"] = "meteor_swarm_ground_commit",
			["schema_version"] = schema_version,
			["boundary_kind"] = "common_outcome_payload",
			["skill_id"] = plan != null ? plan.skill_id.ToString() : "",
			["source_unit_id"] = plan != null ? plan.source_unit_id.ToString() : "",
			["anchor_coord"] = plan != null ? plan.final_anchor_coord : new Vector2I(-1, -1),
			["nominal_plan_signature"] = plan != null ? plan.nominal_plan_signature : "",
			["final_plan_signature"] = plan != null ? plan.final_plan_signature : "",
			["target_count"] = target_outcomes.Count,
			["terrain_effect_count"] = terrain_effects.Count,
			["total_damage"] = total_damage,
			["total_healing"] = total_healing,
			["defeated_unit_ids"] = defeated_unit_ids.Duplicate(),
			["changed_unit_ids"] = changed_unit_ids.Duplicate(),
			["changed_coords"] = changed_coords.Duplicate(),
			["target_summaries"] = _build_target_summaries(),
			["terrain_effects"] = terrain_effects.Duplicate(true),
			["report_entries"] = report_entries.Duplicate(true),
			["log_lines"] = log_lines.Duplicate(),
		};
	}

	private GArray _build_target_summaries()
	{
		var summaries = new GArray();
		foreach (var target_outcome in target_outcomes)
		{
			if (target_outcome != null)
				summaries.Add(target_outcome.to_summary_dict());
		}
		return summaries;
	}
}
