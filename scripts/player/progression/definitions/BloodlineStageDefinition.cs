using System.Collections.Generic;
using Godot;

public sealed class BloodlineStageDefinition
{
    public BloodlineStageDefinition(
        StringName stageId,
        StringName bloodlineId,
        string displayName,
        string description,
        IReadOnlyList<AttributeModifierDefinition> attributeModifiers,
        IReadOnlyList<StringName> traitIds,
        IReadOnlyList<RacialGrantedSkillDefinition> racialGrantedSkills,
        IReadOnlyList<string> traitSummary
    )
    {
        StageId = stageId;
        BloodlineId = bloodlineId;
        DisplayName = IdentityDefinitionProjection.CopyString(
            displayName,
            "BloodlineStageDefinition.DisplayName"
        );
        Description = IdentityDefinitionProjection.CopyString(
            description,
            "BloodlineStageDefinition.Description"
        );
        AttributeModifiers = IdentityDefinitionProjection.FreezeList(
            attributeModifiers,
            "BloodlineStageDefinition.AttributeModifiers"
        );
        TraitIds = IdentityDefinitionProjection.FreezeList(
            traitIds,
            "BloodlineStageDefinition.TraitIds"
        );
        RacialGrantedSkills = IdentityDefinitionProjection.FreezeList(
            racialGrantedSkills,
            "BloodlineStageDefinition.RacialGrantedSkills"
        );
        TraitSummary = IdentityDefinitionProjection.FreezeList(
            traitSummary,
            "BloodlineStageDefinition.TraitSummary"
        );
    }

    public StringName StageId { get; }
    public StringName BloodlineId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public IReadOnlyList<AttributeModifierDefinition> AttributeModifiers { get; }
    public IReadOnlyList<StringName> TraitIds { get; }
    public IReadOnlyList<RacialGrantedSkillDefinition> RacialGrantedSkills { get; }
    public IReadOnlyList<string> TraitSummary { get; }

}
