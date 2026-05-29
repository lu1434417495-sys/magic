using Godot;

[GlobalClass]
public partial class AttributeSourceContext : RefCounted
{
    public UnitProgress unit_progress;
    public Godot.Collections.Dictionary skill_defs = new();
    public Godot.Collections.Dictionary profession_defs = new();
    public RaceDef race_def;
    public SubraceDef subrace_def;
    public AgeStageRule age_stage_rule;
    public StringName age_stage_source_type = "";
    public StringName age_stage_source_id = "";
    public BloodlineDef bloodline_def;
    public BloodlineStageDef bloodline_stage_def;
    public AscensionDef ascension_def;
    public AscensionStageDef ascension_stage_def;
    public StringName versatility_pick = "";
    public Godot.Collections.Array equipment_state = new();
    public Godot.Collections.Array passive_state = new();
    public Godot.Collections.Array temporary_effects = new();
    public Godot.Collections.Array stage_advancement_modifiers = new();

    public void set_effective_age_stage(
        AgeStageRule rule,
        StringName sourceType,
        StringName sourceId
    )
    {
        if (rule == null)
        {
            age_stage_rule = null;
            age_stage_source_type = "";
            age_stage_source_id = "";
            return;
        }
        age_stage_rule = rule;
        age_stage_source_type = sourceType;
        age_stage_source_id = sourceId;
    }
}
