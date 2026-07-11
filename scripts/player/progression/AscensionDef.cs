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
    public Godot.Collections.Array<RacialGrantedSkill> racial_granted_skills = new();

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

    internal Godot.Collections.Array<StringName> StageIdsBorrowed => stage_ids;
    internal Godot.Collections.Array<StringName> TraitIdsBorrowed => trait_ids;
    internal Godot.Collections.Array<RacialGrantedSkill> RacialGrantedSkillsBorrowed =>
        racial_granted_skills;
    internal Godot.Collections.Array<StringName> AllowedRaceIdsBorrowed => allowed_race_ids;
    internal Godot.Collections.Array<StringName> AllowedSubraceIdsBorrowed =>
        allowed_subrace_ids;
    internal Godot.Collections.Array<StringName> AllowedBloodlineIdsBorrowed =>
        allowed_bloodline_ids;
    internal Godot.Collections.Array<string> TraitSummaryBorrowed => trait_summary;
}
