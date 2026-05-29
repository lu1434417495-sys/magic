using Godot;

[GlobalClass]
public partial class AscensionDef : Resource
{
    [Export]
    public StringName ascension_id = "";

    [Export]
    public string display_name = "";

    [Export(PropertyHint.MultilineText)]
    public string description = "";

    [Export]
    public Godot.Collections.Array<StringName> stage_ids = new();

    [Export]
    public Godot.Collections.Array<StringName> trait_ids = new();

    [Export]
    public Godot.Collections.Array<Resource> racial_granted_skills = new();

    [Export]
    public Godot.Collections.Array<StringName> allowed_race_ids = new();

    [Export]
    public Godot.Collections.Array<StringName> allowed_subrace_ids = new();

    [Export]
    public Godot.Collections.Array<StringName> allowed_bloodline_ids = new();

    [Export]
    public Godot.Collections.Array<string> trait_summary = new();

    [Export]
    public bool replaces_age_growth;

    [Export]
    public bool suppresses_original_race_traits;
}
