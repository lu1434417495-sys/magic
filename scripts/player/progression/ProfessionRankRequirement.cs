using Godot;

[GlobalClass]
public partial class ProfessionRankRequirement : Resource
{
    [Export]
    public int target_rank = 1;

    [Export]
    public Godot.Collections.Array<TagRequirement> required_tag_rules = new();

    [Export]
    public Godot.Collections.Array<ProfessionRankGate> required_profession_ranks = new();

    [Export]
    public Godot.Collections.Array<AttributeRequirement> required_attribute_rules = new();

    [Export]
    public Godot.Collections.Array<ReputationRequirement> required_reputation_rules = new();

    internal Godot.Collections.Array<TagRequirement> RequiredTagRulesProjectionBorrowed =>
        required_tag_rules;
    internal Godot.Collections.Array<ProfessionRankGate> RequiredProfessionRanksProjectionBorrowed =>
        required_profession_ranks;
    internal Godot.Collections.Array<AttributeRequirement> RequiredAttributeRulesProjectionBorrowed =>
        required_attribute_rules;
    internal Godot.Collections.Array<ReputationRequirement> RequiredReputationRulesProjectionBorrowed =>
        required_reputation_rules;

    public bool IsEmpty() =>
        required_tag_rules.Count == 0
        && required_profession_ranks.Count == 0
        && required_attribute_rules.Count == 0
        && required_reputation_rules.Count == 0;
}
