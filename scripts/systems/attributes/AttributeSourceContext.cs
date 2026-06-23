using System.Collections.Generic;
using Godot;

public sealed class AttributeSourceContext
{
    public UnitProgress unit_progress;
    public Dictionary<StringName, SkillDef> skill_defs = new();
    public Dictionary<StringName, ProfessionDef> profession_defs = new();
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
    public int reserved_mp_max;
    public List<AttributeModifier> trait_attribute_modifiers = new();
    public List<AttributeModifier> equipment_state = new();
    public List<AttributeModifier> passive_state = new();
    public List<AttributeModifier> temporary_effects = new();
    public List<StageAdvancementModifier> stage_advancement_modifiers = new();

    public void SetEffectiveAgeStage(
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
