using System.Collections.Generic;
using Godot;

internal sealed class UseChargePathAoeActionDefinition : EnemyAiActionDefinition
{
    internal UseChargePathAoeActionDefinition(
        StringName actionId,
        StringName scoreBucketId,
        StringName actionIntent,
        IReadOnlyList<StringName> skillIds,
        StringName targetSelector,
        int minimumHitCount,
        int desiredMinDistance,
        int desiredMaxDistance
    )
        : base(actionId, scoreBucketId, actionIntent, EnemyAiActionKind.UseChargePathAoe, skillIds)
    {
        SkillIds = EnemyDefinitionCollections.FreezeList(skillIds);
        TargetSelector = targetSelector;
        MinimumHitCount = minimumHitCount;
        DesiredMinDistance = desiredMinDistance;
        DesiredMaxDistance = desiredMaxDistance;
    }

    internal IReadOnlyList<StringName> SkillIds { get; }
    internal StringName TargetSelector { get; }
    internal int MinimumHitCount { get; }
    internal int DesiredMinDistance { get; }
    internal int DesiredMaxDistance { get; }
}
