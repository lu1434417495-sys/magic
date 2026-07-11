using System.Collections.Generic;
using Godot;

internal sealed class UseRandomChainSkillActionDefinition : EnemyAiActionDefinition
{
    internal UseRandomChainSkillActionDefinition(
        StringName actionId,
        StringName scoreBucketId,
        StringName actionIntent,
        IReadOnlyList<StringName> skillIds,
        StringName targetSelector,
        int desiredMinDistance,
        int desiredMaxDistance,
        StringName distanceReference,
        int minimumCandidateCount
    )
        : base(actionId, scoreBucketId, actionIntent, EnemyAiActionKind.UseRandomChainSkill, skillIds)
    {
        SkillIds = EnemyDefinitionCollections.FreezeList(skillIds);
        TargetSelector = targetSelector;
        DesiredMinDistance = desiredMinDistance;
        DesiredMaxDistance = desiredMaxDistance;
        DistanceReference = distanceReference;
        MinimumCandidateCount = minimumCandidateCount;
    }

    internal IReadOnlyList<StringName> SkillIds { get; }
    internal StringName TargetSelector { get; }
    internal int DesiredMinDistance { get; }
    internal int DesiredMaxDistance { get; }
    internal StringName DistanceReference { get; }
    internal EnemyAiDistanceReference DistanceReferenceKind =>
        EnemyAiDistanceReferences.ToKind(DistanceReference);
    internal int MinimumCandidateCount { get; }
}
