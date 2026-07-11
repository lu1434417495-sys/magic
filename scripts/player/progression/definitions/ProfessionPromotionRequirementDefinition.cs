using System;
using System.Collections.Generic;
using Godot;

public sealed class ProfessionPromotionRequirementDefinition
{
    public ProfessionPromotionRequirementDefinition(
        IReadOnlyList<StringName> requiredSkillIds,
        IReadOnlyList<TagRequirementDefinition> requiredTagRules,
        IReadOnlyList<ProfessionRankGateDefinition> requiredProfessionRanks,
        IReadOnlyList<AttributeRequirementDefinition> requiredAttributeRules,
        IReadOnlyList<ReputationRequirementDefinition> requiredReputationRules,
        bool assignedCoreMustBeSubsetOfQualifiers
    )
    {
        RequiredSkillIds = ProgressionDefinitionProjection.FreezeValues(
            requiredSkillIds,
            "ProfessionPromotionRequirementDefinition.RequiredSkillIds"
        );
        RequiredTagRules = ProgressionDefinitionProjection.FreezeValues(
            requiredTagRules,
            "ProfessionPromotionRequirementDefinition.RequiredTagRules"
        );
        RequiredProfessionRanks = ProgressionDefinitionProjection.FreezeValues(
            requiredProfessionRanks,
            "ProfessionPromotionRequirementDefinition.RequiredProfessionRanks"
        );
        RequiredAttributeRules = ProgressionDefinitionProjection.FreezeValues(
            requiredAttributeRules,
            "ProfessionPromotionRequirementDefinition.RequiredAttributeRules"
        );
        RequiredReputationRules = ProgressionDefinitionProjection.FreezeValues(
            requiredReputationRules,
            "ProfessionPromotionRequirementDefinition.RequiredReputationRules"
        );
        AssignedCoreMustBeSubsetOfQualifiers = assignedCoreMustBeSubsetOfQualifiers;
    }

    public IReadOnlyList<StringName> RequiredSkillIds { get; }
    public IReadOnlyList<TagRequirementDefinition> RequiredTagRules { get; }
    public IReadOnlyList<ProfessionRankGateDefinition> RequiredProfessionRanks { get; }
    public IReadOnlyList<AttributeRequirementDefinition> RequiredAttributeRules { get; }
    public IReadOnlyList<ReputationRequirementDefinition> RequiredReputationRules { get; }
    public bool AssignedCoreMustBeSubsetOfQualifiers { get; }

    public bool IsEmpty() =>
        RequiredSkillIds.Count == 0
        && RequiredTagRules.Count == 0
        && RequiredProfessionRanks.Count == 0
        && RequiredAttributeRules.Count == 0
        && RequiredReputationRules.Count == 0;

    internal static ProfessionPromotionRequirementDefinition FromResource(
        ProfessionPromotionRequirement source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ProfessionPromotionRequirementDefinition(
            ProgressionDefinitionProjection.CopyBorrowedValues(
                source.RequiredSkillIdsProjectionBorrowed,
                $"{path}.required_skill_ids"
            ),
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.RequiredTagRulesProjectionBorrowed,
                $"{path}.required_tag_rules",
                TagRequirementDefinition.FromResource
            ),
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.RequiredProfessionRanksProjectionBorrowed,
                $"{path}.required_profession_ranks",
                ProfessionRankGateDefinition.FromResource
            ),
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.RequiredAttributeRulesProjectionBorrowed,
                $"{path}.required_attribute_rules",
                AttributeRequirementDefinition.FromResource
            ),
            ProgressionDefinitionProjection.ProjectBorrowedValues(
                source.RequiredReputationRulesProjectionBorrowed,
                $"{path}.required_reputation_rules",
                ReputationRequirementDefinition.FromResource
            ),
            source.assigned_core_must_be_subset_of_qualifiers
        );
    }
}
