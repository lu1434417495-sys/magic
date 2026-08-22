using System.Collections.Generic;
using Godot;

public sealed class AgeStageRuleDefinition
{
    public AgeStageRuleDefinition(
        StringName stageId,
        string displayName,
        string description,
        IReadOnlyList<AttributeModifierDefinition> attributeModifiers,
        IReadOnlyList<StringName> traitIds,
        IReadOnlyList<string> traitSummary,
        bool selectableInCreation,
        bool reachableByAging
    )
    {
        StageId = stageId;
        DisplayName = IdentityDefinitionProjection.CopyString(
            displayName,
            "AgeStageRuleDefinition.DisplayName"
        );
        Description = IdentityDefinitionProjection.CopyString(
            description,
            "AgeStageRuleDefinition.Description"
        );
        AttributeModifiers = IdentityDefinitionProjection.FreezeList(
            attributeModifiers,
            "AgeStageRuleDefinition.AttributeModifiers"
        );
        TraitIds = IdentityDefinitionProjection.FreezeList(
            traitIds,
            "AgeStageRuleDefinition.TraitIds"
        );
        TraitSummary = IdentityDefinitionProjection.FreezeList(
            traitSummary,
            "AgeStageRuleDefinition.TraitSummary"
        );
        SelectableInCreation = selectableInCreation;
        ReachableByAging = reachableByAging;
    }

    public StringName StageId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public IReadOnlyList<AttributeModifierDefinition> AttributeModifiers { get; }
    public IReadOnlyList<StringName> TraitIds { get; }
    public IReadOnlyList<string> TraitSummary { get; }
    public bool SelectableInCreation { get; }
    public bool ReachableByAging { get; }

}
