using Godot;

[GlobalClass]
public partial class FaithRankDef : Resource
{
    [Export] public int rank_index { get; set; } = 1;
    [Export] public string rank_name { get; set; } = "";
    [Export] public int required_gold { get; set; }
    [Export] public int required_level { get; set; }
    [Export] public StringName required_custom_stat_id { get; set; } = "";
    [Export] public int required_custom_stat_min_value { get; set; }
    [Export] public StringName required_achievement_id { get; set; } = "";
    [Export] public Godot.Collections.Array<Godot.Collections.Dictionary> reward_entries { get; set; } = new();
}
