using Godot;

[GlobalClass]
public partial class SkillDef : Resource
{
    [Export]
    public StringName skill_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export]
    public StringName icon_id { get; set; } = "";

    [Export(PropertyHint.MultilineText)]
    public string description { get; set; } = "";

    [Export]
    public StringName skill_type { get; set; } = "active";

    [Export]
    public int max_level { get; set; } = 1;

    [Export]
    public int non_core_max_level { get; set; }

    [Export]
    public StringName dynamic_max_level_stat_id { get; set; } = "";

    [Export]
    public int dynamic_max_level_base { get; set; }

    [Export]
    public int dynamic_max_level_per_stat { get; set; }

    [Export]
    public int[] mastery_curve { get; set; } = System.Array.Empty<int>();

    [Export]
    public Godot.Collections.Array<StringName> tags { get; set; } = new();

    [Export]
    public StringName learn_source { get; set; } = "book";

    [Export]
    public Godot.Collections.Array<StringName> learn_requirements { get; set; } = new();

    [Export]
    public StringName unlock_mode { get; set; } = "standard";

    [Export]
    public Godot.Collections.Array<StringName> knowledge_requirements { get; set; } = new();

    [Export]
    public Godot.Collections.Dictionary skill_level_requirements { get; set; } = new();

    [Export]
    public Godot.Collections.Dictionary attribute_requirements { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> achievement_requirements { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> upgrade_source_skill_ids { get; set; } = new();

    [Export]
    public bool retain_source_skills_on_unlock { get; set; } = true;

    [Export]
    public StringName core_skill_transition_mode { get; set; } = "inherit";

    [Export]
    public Godot.Collections.Array<StringName> mastery_sources { get; set; } = new();

    [Export]
    public StringName growth_tier { get; set; } = "";

    [Export]
    public Godot.Collections.Dictionary attribute_growth_progress { get; set; } = new();

    [Export]
    public StringName practice_tier { get; set; } = "";

    [Export]
    public Godot.Collections.Array<Resource> attribute_modifiers { get; set; } = new();

    [Export(PropertyHint.MultilineText)]
    public string level_description_template { get; set; } = "";

    [Export]
    public Godot.Collections.Dictionary level_description_configs { get; set; } = new();

    [Export]
    public CombatSkillDef combat_profile { get; set; }

    public int get_mastery_required_for_level(int level)
    {
        if (level < 0)
            return 0;

        if (level < mastery_curve.Length)
            return mastery_curve[level];

        if (mastery_curve.Length <= 0)
            return 0;

        if (mastery_curve.Length == 1)
            return mastery_curve[0];

        int lastIndex = mastery_curve.Length - 1;

        int delta = Mathf.Max(mastery_curve[lastIndex] - mastery_curve[lastIndex - 1], 1);

        return mastery_curve[lastIndex] + delta * (level - lastIndex);
    }

    public bool is_profession_skill() => learn_source == "profession";

    public bool can_use_in_combat() => combat_profile != null;
}
