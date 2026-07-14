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

    internal static AscensionStageDefinition FromResource(
        AscensionStageDef source,
        string path
    )
    {
        IdentityDefinitionProjection.RequireResource(source, path, nameof(AscensionStageDef));
        return new AscensionStageDefinition(
            source.stage_id,
            source.ascension_id,
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
            IdentityDefinitionProjection.CopyRacialGrantedSkills(
                source.RacialGrantedSkillsBorrowed,
                $"{path}.racial_granted_skills"
            ),
            source.body_size_category_override,
            IdentityDefinitionProjection.CopyStrings(
                source.TraitSummaryBorrowed,
                $"{path}.trait_summary"
            )
        );
    }
}
