using Godot;
using Godot.Collections;

public partial class BattleSimProfileDef : Resource
{
    [Export]
    public StringName profile_id = "baseline";

    [Export]
    public string display_name = "Baseline";

    [Export(PropertyHint.MultilineText)]
    public string description = "";

    [Export]
    public BattleAiScoreProfile ai_score_profile = null;

    [Export]
    public Array override_patches = new();

}
