using System.Collections.Generic;
using Godot;

internal sealed class MoveToAdvantagePositionActionDefinition : EnemyAiActionDefinition
{
    internal MoveToAdvantagePositionActionDefinition(
        StringName actionId,
        StringName scoreBucketId,
        StringName actionIntent,
        StringName targetSelector,
        int desiredMinDistance,
        int desiredMaxDistance,
        IReadOnlyList<StringName> rangeSkillIds,
        int minimumSafeDistance,
        int safeDistanceMargin,
        int minSurvivalMarginGainToEscape,
        int minDistanceProgressWhenBeyondBand,
        StringName positioningMode,
        int highGroundWeight,
        int safetyWeight,
        int distanceBandWeight,
        int candidateLimit
    )
        : base(
            actionId,
            scoreBucketId,
            actionIntent,
            EnemyAiActionKind.MoveToAdvantagePosition,
            rangeSkillIds
        )
    {
        TargetSelector = targetSelector;
        DesiredMinDistance = desiredMinDistance;
        DesiredMaxDistance = desiredMaxDistance;
        RangeSkillIds = EnemyDefinitionCollections.FreezeList(rangeSkillIds);
        MinimumSafeDistance = minimumSafeDistance;
        SafeDistanceMargin = safeDistanceMargin;
        MinSurvivalMarginGainToEscape = minSurvivalMarginGainToEscape;
        MinDistanceProgressWhenBeyondBand = minDistanceProgressWhenBeyondBand;
        PositioningMode = positioningMode;
        HighGroundWeight = highGroundWeight;
        SafetyWeight = safetyWeight;
        DistanceBandWeight = distanceBandWeight;
        CandidateLimit = candidateLimit;
    }

    internal StringName TargetSelector { get; }
    internal int DesiredMinDistance { get; }
    internal int DesiredMaxDistance { get; }
    internal IReadOnlyList<StringName> RangeSkillIds { get; }
    internal int MinimumSafeDistance { get; }
    internal int SafeDistanceMargin { get; }
    internal int MinSurvivalMarginGainToEscape { get; }
    internal int MinDistanceProgressWhenBeyondBand { get; }
    internal StringName PositioningMode { get; }
    internal int HighGroundWeight { get; }
    internal int SafetyWeight { get; }
    internal int DistanceBandWeight { get; }
    internal int CandidateLimit { get; }
}
