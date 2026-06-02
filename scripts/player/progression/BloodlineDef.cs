using Godot;

[GlobalClass]
public partial class BloodlineDef : Resource
{
    [Export]
    public StringName bloodline_id = "";

    [Export]
    public string display_name = "";

    [Export(PropertyHint.MultilineText)]
    public string description = "";

    [Export]
    public Godot.Collections.Array<StringName> stage_ids = new();

    [Export]
    public Godot.Collections.Array<StringName> trait_ids = new();

    [Export]
    public Godot.Collections.Array<RacialGrantedSkill> racial_granted_skills = new();

    [Export]
    public Godot.Collections.Array<Resource> attribute_modifiers = new();

    [Export]
    public Godot.Collections.Array<string> trait_summary = new();
}
