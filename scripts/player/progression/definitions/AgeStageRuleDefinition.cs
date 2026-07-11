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

    internal static AgeStageRuleDefinition FromResource(AgeStageRule source, string path)
    {
        IdentityDefinitionProjection.RequireResource(source, path, nameof(AgeStageRule));
        return new AgeStageRuleDefinition(
            source.stage_id,
            IdentityDefinitionProjection.CopyString(source.display_name, $"{path}.display_name"),
            IdentityDefinitionProjection.CopyString(source.description, $"{path}.description"),
            IdentityDefinitionProjection.CopyAttributeModifiers(
                source.AttributeModifiersBorrowed,
                $"{path}.attribute_modifiers"
            ),
            IdentityDefinitionProjection.CopyStringNames(
                source.TraitIdsBorrowed,
                $"{path}.trait_ids"
            ),
            IdentityDefinitionProjection.CopyStrings(
                source.TraitSummaryBorrowed,
                $"{path}.trait_summary"
            ),
            source.selectable_in_creation,
            source.reachable_by_aging
        );
    }
}
