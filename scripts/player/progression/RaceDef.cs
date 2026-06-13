using Godot;

[GlobalClass]
public partial class RaceDef : Resource
{
    [Export]
    public StringName race_id = "";

    [Export]
    public string display_name = "";

    [Export(PropertyHint.MultilineText)]
    public string description = "";

    [Export]
    public StringName age_profile_id = "";

    [Export]
    public StringName default_subrace_id = "";

    [Export]
    public Godot.Collections.Array<StringName> subrace_ids = new();

    [Export]
    public StringName body_size_category = "medium";

    [Export]
    public int base_speed = 6;

    [Export]
    public Godot.Collections.Array<Resource> attribute_modifiers = new();

    [Export]
    public Godot.Collections.Array<StringName> trait_ids = new();

    [Export]
    public Godot.Collections.Array<RacialGrantedSkill> racial_granted_skills = new();

    [Export]
    public Godot.Collections.Array<StringName> proficiency_tags = new();

    [Export]
    public Godot.Collections.Array<StringName> vision_tags = new();

    [Export]
    public Godot.Collections.Array<StringName> save_advantage_tags = new();

    [Export]
    public Godot.Collections.Dictionary damage_resistances = new();

    [Export]
    public Godot.Collections.Array<StringName> dialogue_tags = new();

    [Export]
    public Godot.Collections.Array<string> racial_trait_summary = new();
}
