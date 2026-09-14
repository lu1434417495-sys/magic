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

}
