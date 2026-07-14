using System;
using System.Collections.Generic;

public sealed class ProfessionRankRequirementDefinition
{
    public ProfessionRankRequirementDefinition(
        int targetRank,
        IReadOnlyList<TagRequirementDefinition> requiredTagRules,
        IReadOnlyList<ProfessionRankGateDefinition> requiredProfessionRanks,
        IReadOnlyList<AttributeRequirementDefinition> requiredAttributeRules,
        IReadOnlyList<ReputationRequirementDefinition> requiredReputationRules
    )
    {
        TargetRank = targetRank;
        RequiredTagRules = ProgressionDefinitionProjection.FreezeValues(
            requiredTagRules,
            "ProfessionRankRequirementDefinition.RequiredTagRules"
        );
        RequiredProfessionRanks = ProgressionDefinitionProjection.FreezeValues(
            requiredProfessionRanks,
            "ProfessionRankRequirementDefinition.RequiredProfessionRanks"
        );
        RequiredAttributeRules = ProgressionDefinitionProjection.FreezeValues(
            requiredAttributeRules,
            "ProfessionRankRequirementDefinition.RequiredAttributeRules"
        );
        RequiredReputationRules = ProgressionDefinitionProjection.FreezeValues(
            requiredReputationRules,
            "ProfessionRankRequirementDefinition.RequiredReputationRules"
        );
    }

    public int TargetRank { get; }
    public IReadOnlyList<TagRequirementDefinition> RequiredTagRules { get; }
    public IReadOnlyList<ProfessionRankGateDefinition> RequiredProfessionRanks { get; }
    public IReadOnlyList<AttributeRequirementDefinition> RequiredAttributeRules { get; }
    public IReadOnlyList<ReputationRequirementDefinition> RequiredReputationRules { get; }

    public bool IsEmpty() =>
        RequiredTagRules.Count == 0
        && RequiredProfessionRanks.Count == 0
        && RequiredAttributeRules.Count == 0
        && RequiredReputationRules.Count == 0;

    internal static ProfessionRankRequirementDefinition FromResource(
        ProfessionRankRequirement source,
        string path
    )
    {
        ArgumentNullException.ThrowIfNull(source);
        return new ProfessionRankRequirementDefinition(
            source.target_rank,
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
            )
        );
    }
}
