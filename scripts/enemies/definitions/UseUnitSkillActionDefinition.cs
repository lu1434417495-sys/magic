using System.Collections.Generic;
using Godot;

internal sealed class UseUnitSkillActionDefinition : EnemyAiActionDefinition
{
    internal UseUnitSkillActionDefinition(
        StringName actionId,
        StringName scoreBucketId,
        StringName actionIntent,
        IReadOnlyList<StringName> skillIds,
        StringName targetSelector,
        int minimumEffectiveTargetCount,
        int maximumFriendlyFireTargetCount,
        bool allowFriendlyLethal,
        int desiredMinDistance,
        int desiredMaxDistance,
        StringName distanceReference
    )
        : base(actionId, scoreBucketId, actionIntent, EnemyAiActionKind.UseUnitSkill, skillIds)
    {
        SkillIds = EnemyDefinitionCollections.FreezeList(skillIds);
        TargetSelector = targetSelector;
        MinimumEffectiveTargetCount = minimumEffectiveTargetCount;
        MaximumFriendlyFireTargetCount = maximumFriendlyFireTargetCount;
        AllowFriendlyLethal = allowFriendlyLethal;
        DesiredMinDistance = desiredMinDistance;
        DesiredMaxDistance = desiredMaxDistance;
        DistanceReference = distanceReference;
    }

    internal IReadOnlyList<StringName> SkillIds { get; }
    internal StringName TargetSelector { get; }
    internal int MinimumEffectiveTargetCount { get; }
    internal int MaximumFriendlyFireTargetCount { get; }
    internal bool AllowFriendlyLethal { get; }
    internal int DesiredMinDistance { get; }
    internal int DesiredMaxDistance { get; }
    internal StringName DistanceReference { get; }
    internal EnemyAiDistanceReference DistanceReferenceKind =>
        EnemyAiDistanceReferences.ToKind(DistanceReference);
}
