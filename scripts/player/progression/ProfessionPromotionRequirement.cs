using Godot;
using Godot.Collections;

[GlobalClass]
public partial class ProfessionPromotionRequirement : Resource
{
    [Export]
    public Array<StringName> required_skill_ids = new();

    [Export]
    public Array<TagRequirement> required_tag_rules = new();

    [Export]
    public Array<ProfessionRankGate> required_profession_ranks = new();

    [Export]
    public Array<AttributeRequirement> required_attribute_rules = new();

    [Export]
    public Array<ReputationRequirement> required_reputation_rules = new();

    [Export]
    public bool assigned_core_must_be_subset_of_qualifiers = false;

    public bool IsEmpty()
    {
        return required_skill_ids.Count == 0
            && required_tag_rules.Count == 0
            && required_profession_ranks.Count == 0
            && required_attribute_rules.Count == 0
            && required_reputation_rules.Count == 0;
    }
}
