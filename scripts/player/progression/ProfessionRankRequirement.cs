using Godot;

[GlobalClass]
public partial class ProfessionRankRequirement : Resource
{
    [Export] public int target_rank = 1;
    [Export] public Godot.Collections.Array<TagRequirement> required_tag_rules = new();
    [Export] public Godot.Collections.Array<ProfessionRankGate> required_profession_ranks = new();

    public bool is_empty() => required_tag_rules.Count == 0 && required_profession_ranks.Count == 0;
}
