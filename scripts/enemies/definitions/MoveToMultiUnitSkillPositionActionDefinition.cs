using System.Collections.Generic;
using Godot;

internal sealed class MoveToMultiUnitSkillPositionActionDefinition : EnemyAiActionDefinition
{
    internal MoveToMultiUnitSkillPositionActionDefinition(
        StringName actionId,
        StringName scoreBucketId,
        StringName actionIntent,
        IReadOnlyList<StringName> skillIds,
        StringName targetSelector,
        int desiredMinDistance,
        int desiredMaxDistance,
        StringName distanceReference,
        int candidatePoolLimit,
        int candidateGroupLimit,
        int targetCountWeight
    )
        : base(
            actionId,
            scoreBucketId,
            actionIntent,
            EnemyAiActionKind.MoveToMultiUnitSkillPosition,
            skillIds
        )
    {
        SkillIds = EnemyDefinitionCollections.FreezeList(skillIds);
        TargetSelector = targetSelector;
        DesiredMinDistance = desiredMinDistance;
        DesiredMaxDistance = desiredMaxDistance;
        DistanceReference = distanceReference;
        CandidatePoolLimit = candidatePoolLimit;
        CandidateGroupLimit = candidateGroupLimit;
        TargetCountWeight = targetCountWeight;
    }

    internal IReadOnlyList<StringName> SkillIds { get; }
    internal StringName TargetSelector { get; }
    internal int DesiredMinDistance { get; }
    internal int DesiredMaxDistance { get; }
    internal StringName DistanceReference { get; }
    internal EnemyAiDistanceReference DistanceReferenceKind =>
        EnemyAiDistanceReferences.ToKind(DistanceReference);
    internal int CandidatePoolLimit { get; }
    internal int CandidateGroupLimit { get; }
    internal int TargetCountWeight { get; }
}
