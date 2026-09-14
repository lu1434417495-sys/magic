using System.Collections.Generic;
using Godot;

public sealed class BloodlineDefinition
{
    public BloodlineDefinition(
        StringName bloodlineId,
        string displayName,
        string description,
        IReadOnlyList<StringName> stageIds,
        IReadOnlyList<StringName> traitIds,
        IReadOnlyList<RacialGrantedSkillDefinition> racialGrantedSkills,
        IReadOnlyList<AttributeModifierDefinition> attributeModifiers,
        IReadOnlyList<string> traitSummary
    )
    {
        BloodlineId = bloodlineId;
        DisplayName = IdentityDefinitionProjection.CopyString(
            displayName,
            "BloodlineDefinition.DisplayName"
        );
        Description = IdentityDefinitionProjection.CopyString(
            description,
            "BloodlineDefinition.Description"
        );
        StageIds = IdentityDefinitionProjection.FreezeList(
            stageIds,
            "BloodlineDefinition.StageIds"
        );
        TraitIds = IdentityDefinitionProjection.FreezeList(
            traitIds,
            "BloodlineDefinition.TraitIds"
        );
        RacialGrantedSkills = IdentityDefinitionProjection.FreezeList(
            racialGrantedSkills,
            "BloodlineDefinition.RacialGrantedSkills"
        );
        AttributeModifiers = IdentityDefinitionProjection.FreezeList(
            attributeModifiers,
            "BloodlineDefinition.AttributeModifiers"
        );
        TraitSummary = IdentityDefinitionProjection.FreezeList(
            traitSummary,
            "BloodlineDefinition.TraitSummary"
        );
    }

    public StringName BloodlineId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public IReadOnlyList<StringName> StageIds { get; }
    public IReadOnlyList<StringName> TraitIds { get; }
    public IReadOnlyList<RacialGrantedSkillDefinition> RacialGrantedSkills { get; }
    public IReadOnlyList<AttributeModifierDefinition> AttributeModifiers { get; }
    public IReadOnlyList<string> TraitSummary { get; }

}
