using Godot;

[GlobalClass]
public partial class AgeProfileDef : Resource
{
    [Export]
    public StringName profile_id = "";

    [Export]
    public StringName race_id = "";

    [Export]
    public int child_age;

    [Export]
    public int teen_age = 12;

    [Export]
    public int young_adult_age = 16;

    [Export]
    public int adult_age = 18;

    [Export]
    public int middle_age = 35;

    [Export]
    public int old_age = 53;

    [Export]
    public int venerable_age = 70;

    [Export]
    public int max_natural_age = 90;

    [Export]
    public Godot.Collections.Array<AgeStageRule> stage_rules = new();

    [Export]
    public Godot.Collections.Array<StringName> creation_stage_ids = new();

    [Export]
    public Godot.Collections.Dictionary default_age_by_stage = new();
}
