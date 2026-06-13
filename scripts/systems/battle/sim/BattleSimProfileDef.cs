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

    internal Dictionary ToDictionary() => ToDict();

    internal Dictionary ToDict()
    {
        return new Dictionary()
        {
            { "profile_id", (string)profile_id },
            { "display_name", display_name },
            { "description", description },
            {
                "ai_score_profile",
                ai_score_profile?.ToDictionary() ?? new Dictionary()
            },
            { "override_patch_count", override_patches.Count },
        };
    }
}
