using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class MeteorSwarmTargetOutcome : RefCounted
{
	public StringName target_unit_id { get; set; } = "";
	public Vector2I target_coord { get; set; } = new(-1, -1);
	public StringName target_faction_id { get; set; } = "";
	public int distance_from_anchor { get; set; } = 0;
	public Godot.Collections.Array<MeteorSwarmImpactComponent> components { get; set; } = new();
	public GArray damage_events { get; set; } = new();
	public Godot.Collections.Array<StringName> status_effect_ids { get; set; } = new();
	public Godot.Collections.Array<StringName> terrain_effect_ids { get; set; } = new();
	public GArray attack_roll_modifier_breakdown { get; set; } = new();
	public GArray report_component_breakdown { get; set; } = new();
	public int total_damage { get; set; } = 0;
	public int total_healing { get; set; } = 0;
	public bool defeated { get; set; } = false;

	public void add_component(MeteorSwarmImpactComponent component)
	{
		if (component != null)
			components.Add(component);
	}

	public void add_status_effect_id(StringName status_id)
	{
		if (status_id == (StringName)"" || status_effect_ids.Contains(status_id))
			return;
		status_effect_ids.Add(status_id);
	}

	public GDictionary to_summary_dict()
	{
		return new GDictionary
		{
			["target_unit_id"] = target_unit_id.ToString(),
			["target_coord"] = target_coord,
			["target_faction_id"] = target_faction_id.ToString(),
			["distance_from_anchor"] = distance_from_anchor,
			["total_damage"] = total_damage,
			["total_healing"] = total_healing,
			["defeated"] = defeated,
			["status_effect_ids"] = status_effect_ids.Duplicate(),
			["component_breakdown"] = report_component_breakdown.Duplicate(true),
		};
	}
}
