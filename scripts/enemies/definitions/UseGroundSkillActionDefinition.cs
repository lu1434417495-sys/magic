using System.Collections.Generic;
using Godot;

internal sealed class UseGroundSkillActionDefinition : EnemyAiActionDefinition
{
    internal UseGroundSkillActionDefinition(
        StringName actionId,
        StringName scoreBucketId,
        StringName actionIntent,
        IReadOnlyList<StringName> skillIds,
        int minimumHitCount,
        bool allowEmptyGroundControl,
        bool allowGroundControlSupplementPartialHits,
        int minimumGroundControlScore,
        int minimumAllyThreatHitCount,
        int maximumFriendlyFireTargetCount,
        bool allowFriendlyLethal,
        int threatMinimumSafeDistance,
        int threatSafeDistanceMargin,
        int desiredMinDistance,
        int desiredMaxDistance,
        StringName distanceReference
    )
        : base(actionId, scoreBucketId, actionIntent, EnemyAiActionKind.UseGroundSkill, skillIds)
    {
        SkillIds = EnemyDefinitionCollections.FreezeList(skillIds);
        MinimumHitCount = minimumHitCount;
        AllowEmptyGroundControl = allowEmptyGroundControl;
        AllowGroundControlSupplementPartialHits = allowGroundControlSupplementPartialHits;
        MinimumGroundControlScore = minimumGroundControlScore;
        MinimumAllyThreatHitCount = minimumAllyThreatHitCount;
        MaximumFriendlyFireTargetCount = maximumFriendlyFireTargetCount;
        AllowFriendlyLethal = allowFriendlyLethal;
        ThreatMinimumSafeDistance = threatMinimumSafeDistance;
        ThreatSafeDistanceMargin = threatSafeDistanceMargin;
        DesiredMinDistance = desiredMinDistance;
        DesiredMaxDistance = desiredMaxDistance;
        DistanceReference = distanceReference;
    }

    internal IReadOnlyList<StringName> SkillIds { get; }
    internal int MinimumHitCount { get; }
    internal bool AllowEmptyGroundControl { get; }
    internal bool AllowGroundControlSupplementPartialHits { get; }
    internal int MinimumGroundControlScore { get; }
    internal int MinimumAllyThreatHitCount { get; }
    internal int MaximumFriendlyFireTargetCount { get; }
    internal bool AllowFriendlyLethal { get; }
    internal int ThreatMinimumSafeDistance { get; }
    internal int ThreatSafeDistanceMargin { get; }
    internal int DesiredMinDistance { get; }
    internal int DesiredMaxDistance { get; }
    internal StringName DistanceReference { get; }
    internal EnemyAiDistanceReference DistanceReferenceKind =>
        EnemyAiDistanceReferences.ToKind(DistanceReference);
}
