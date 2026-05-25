using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleSimProfileDef : Resource
{
    [Export] public StringName profile_id = "baseline";
    [Export] public string display_name = "Baseline";
    [Export(PropertyHint.MultilineText)] public string description = "";
    [Export] public GodotObject ai_score_profile = null;
    [Export] public Array override_patches = new();

    public Dictionary ToDict()
    {
        return new Dictionary()
        {
            { "profile_id", (string)profile_id },
            { "display_name", display_name },
            { "description", description },
            { "ai_score_profile", ai_score_profile != null ? ai_score_profile.Call("to_dict") : new Dictionary() },
            { "override_patch_count", override_patches.Count },
        };
    }
}
