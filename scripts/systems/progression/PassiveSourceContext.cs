using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class PassiveSourceContext : RefCounted
{
    public PartyMemberState member_state;
    public GodotObject unit_progress;
    public GDictionary skill_progress_by_id = new();

    public RaceDef race_def;
    public SubraceDef subrace_def;
    public GDictionary trait_defs = new();
    public BloodlineDef bloodline_def;
    public BloodlineStageDef bloodline_stage_def;
    public AscensionDef ascension_def;
    public AscensionStageDef ascension_stage_def;
    public GArray stage_advancement_modifiers = new();
}
