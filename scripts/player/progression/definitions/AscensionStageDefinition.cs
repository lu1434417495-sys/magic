using System.Collections.Generic;
using Godot;

public sealed class AscensionStageDefinition
{
    public AscensionStageDefinition(
        StringName stageId,
        StringName ascensionId,
        string displayName,
        string description,
        IReadOnlyList<AttributeModifierDefinition> attributeModifiers,
        IReadOnlyList<StringName> traitIds,
        IReadOnlyList<RacialGrantedSkillDefinition> racialGrantedSkills,
        StringName bodySizeCategoryOverride,
        IReadOnlyList<string> traitSummary
    )
    {
        StageId = stageId;
        AscensionId = ascensionId;
        DisplayName = IdentityDefinitionProjection.CopyString(
            displayName,
            "AscensionStageDefinition.DisplayName"
        );
        Description = IdentityDefinitionProjection.CopyString(
            description,
            "AscensionStageDefinition.Description"
        );
        AttributeModifiers = IdentityDefinitionProjection.FreezeList(
            attributeModifiers,
            "AscensionStageDefinition.AttributeModifiers"
        );
        TraitIds = IdentityDefinitionProjection.FreezeList(
            traitIds,
            "AscensionStageDefinition.TraitIds"
        );
        RacialGrantedSkills = IdentityDefinitionProjection.FreezeList(
            racialGrantedSkills,
            "AscensionStageDefinition.RacialGrantedSkills"
        );
        BodySizeCategoryOverride = bodySizeCategoryOverride;
        TraitSummary = IdentityDefinitionProjection.FreezeList(
            traitSummary,
            "AscensionStageDefinition.TraitSummary"
        );
    }

    public StringName StageId { get; }
    public StringName AscensionId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public IReadOnlyList<AttributeModifierDefinition> AttributeModifiers { get; }
    public IReadOnlyList<StringName> TraitIds { get; }
    public IReadOnlyList<RacialGrantedSkillDefinition> RacialGrantedSkills { get; }
    public StringName BodySizeCategoryOverride { get; }
    public IReadOnlyList<string> TraitSummary { get; }

}
