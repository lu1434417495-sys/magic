using Godot;
using Godot.Collections;

[GlobalClass]
public partial class WildSpawnRule : Resource
{
    [Export]
    public string region_tag = "";

    [Export]
    public string monster_name = "野怪";

    [Export]
    public StringName encounter_profile_id = "";

    [Export]
    public StringName settlement_encounter_profile_id = "";

    [Export]
    public string settlement_encounter_display_name = "";

    [Export]
    public int density_per_chunk = 1;

    [Export]
    public int min_distance_to_settlement = 2;

    [Export]
    public int vision_range = 1;

    [Export]
    public Array<Vector2I> chunk_coords = new();

    internal Array<Vector2I> ChunkCoordsProjectionBorrowed => chunk_coords;

    internal WildSpawnRuleDefinition ToDefinition(string path) =>
        WildSpawnRuleDefinition.FromResource(this, path);
}
