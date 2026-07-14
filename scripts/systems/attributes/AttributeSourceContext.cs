using System.Collections.Generic;
using Godot;

public sealed class AttributeSourceContext
{
    public UnitProgress unit_progress;
    public Dictionary<StringName, SkillDefinition> skill_definitions = new();
    public Dictionary<StringName, ProfessionDefinition> profession_defs = new();
    public RaceDefinition race_def;
    public SubraceDefinition subrace_def;
    public AgeStageRuleDefinition age_stage_rule;
    public StringName age_stage_source_type = "";
    public StringName age_stage_source_id = "";
    public BloodlineDefinition bloodline_def;
    public BloodlineStageDefinition bloodline_stage_def;
    public AscensionDefinition ascension_def;
    public AscensionStageDefinition ascension_stage_def;
    public StringName versatility_pick = "";
    public int reserved_mp_max;
    public IReadOnlyList<AttributeModifierDefinition> trait_attribute_modifiers =
        System.Array.Empty<AttributeModifierDefinition>();
    public IReadOnlyList<AttributeModifierDefinition> equipment_state =
        System.Array.Empty<AttributeModifierDefinition>();
    public IReadOnlyList<AttributeModifierDefinition> passive_state =
        System.Array.Empty<AttributeModifierDefinition>();
    public IReadOnlyList<AttributeModifierDefinition> temporary_effects =
        System.Array.Empty<AttributeModifierDefinition>();
    public List<StageAdvancementDefinition> stage_advancement_modifiers = new();

    public void SetEffectiveAgeStage(
        AgeStageRuleDefinition rule,
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
