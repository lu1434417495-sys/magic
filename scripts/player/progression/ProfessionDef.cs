using Godot;

[GlobalClass]
public partial class ProfessionDef : Resource
{
    [Export]
    public StringName profession_id = "";

    [Export]
    public string display_name = "";

    [Export(PropertyHint.MultilineText)]
    public string description = "";

    [Export]
    public int max_rank = 1;

    [Export]
    public int hit_die_sides = 8;

    [Export]
    public StringName bab_progression = "half";

    [Export]
    public bool is_initial_profession;

    [Export]
    public StringName unlock_knowledge_id = "";

    [Export]
    public ProfessionPromotionRequirement unlock_requirement;

    [Export]
    public Godot.Collections.Array<ProfessionRankRequirement> rank_requirements = new();

    [Export]
    public Godot.Collections.Array<ProfessionGrantedSkill> granted_skills = new();

    [Export]
    public Godot.Collections.Array<AttributeModifier> attribute_modifiers = new();

    [Export]
    public Godot.Collections.Array<ProfessionActiveCondition> active_conditions = new();

    [Export]
    public StringName reactivation_mode = "auto";

    [Export]
    public StringName dependency_visibility_mode = "count_when_hidden";

    public bool requires_knowledge_unlock() => !is_initial_profession;

    public ProfessionRankRequirement get_rank_requirement(int target_rank)
    {
        foreach (var r in rank_requirements)
        {
            if (r != null && (int)r.Get("target_rank") == target_rank)
                return r;
        }
        return null;
    }

    public Godot.Collections.Array<ProfessionGrantedSkill> get_granted_skills_for_rank(
        int target_rank
    )
    {
        var result = new Godot.Collections.Array<ProfessionGrantedSkill>();
        foreach (var gs in granted_skills)
        {
            if (gs != null && (int)gs.Get("unlock_rank") == target_rank)
                result.Add(gs);
        }
        return result;
    }
}
