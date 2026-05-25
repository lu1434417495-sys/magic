using Godot;

[GlobalClass]
public partial class WorldMapOccupantState : RefCounted
{
	public string occupant_id = "";
	public string footprint_root_id = "";

	public static WorldMapOccupantState create(string next_occupant_id, string next_footprint_root_id = "")
	{
		var state = new WorldMapOccupantState();
		state.occupant_id = next_occupant_id;
		state.footprint_root_id = string.IsNullOrEmpty(next_footprint_root_id) ? next_occupant_id : next_footprint_root_id;
		return state;
	}

	public bool is_empty()
	{
		return string.IsNullOrEmpty(occupant_id) && string.IsNullOrEmpty(footprint_root_id);
	}
}
