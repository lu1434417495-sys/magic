using Godot;

public sealed class MeteorSwarmTerrainEffectFact
{
    public Vector2I coord { get; set; } = new(-1, -1);
    public int ring { get; set; } = 0;
    public StringName terrain_profile_id { get; set; } = "";
    public StringName terrain_effect_id { get; set; } = "";
    public StringName lifetime_policy { get; set; } = "";
    public int move_cost_delta { get; set; } = 0;
    public string render_overlay_id { get; set; } = "";
}
