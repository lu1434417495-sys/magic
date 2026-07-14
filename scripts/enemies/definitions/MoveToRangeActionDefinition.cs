using System.Collections.Generic;
using Godot;

internal sealed class MoveToRangeActionDefinition : EnemyAiActionDefinition
{
    internal MoveToRangeActionDefinition(
        StringName actionId,
        StringName scoreBucketId,
        StringName actionIntent,
        StringName aiEvaluationMode,
        StringName targetSelector,
        int desiredMinDistance,
        int desiredMaxDistance,
        IReadOnlyList<StringName> rangeSkillIds,
        StringName screeningMode,
        bool enableAoeSetupPositioning,
        int aoeSetupMinTargetCount,
        int aoeSetupTargetCountWeight,
        int aoeSetupImprovementWeight,
        int aoeSetupFriendlyFirePenalty,
        int screeningMinHpBasisPoints,
        int screeningAllyMinAttackRange,
        int screeningEnemyMaxContactRange,
        int screeningThreatDistanceBuffer,
        int screeningPathBonus
    )
        : base(actionId, scoreBucketId, actionIntent, EnemyAiActionKind.MoveToRange, rangeSkillIds)
    {
        AiEvaluationMode = aiEvaluationMode;
        TargetSelector = targetSelector;
        DesiredMinDistance = desiredMinDistance;
        DesiredMaxDistance = desiredMaxDistance;
        RangeSkillIds = EnemyDefinitionCollections.FreezeList(rangeSkillIds);
        ScreeningMode = screeningMode;
        EnableAoeSetupPositioning = enableAoeSetupPositioning;
        AoeSetupMinTargetCount = aoeSetupMinTargetCount;
        AoeSetupTargetCountWeight = aoeSetupTargetCountWeight;
        AoeSetupImprovementWeight = aoeSetupImprovementWeight;
        AoeSetupFriendlyFirePenalty = aoeSetupFriendlyFirePenalty;
        ScreeningMinHpBasisPoints = screeningMinHpBasisPoints;
        ScreeningAllyMinAttackRange = screeningAllyMinAttackRange;
        ScreeningEnemyMaxContactRange = screeningEnemyMaxContactRange;
        ScreeningThreatDistanceBuffer = screeningThreatDistanceBuffer;
        ScreeningPathBonus = screeningPathBonus;
    }

    internal StringName AiEvaluationMode { get; }
    internal StringName TargetSelector { get; }
    internal int DesiredMinDistance { get; }
    internal int DesiredMaxDistance { get; }
    internal IReadOnlyList<StringName> RangeSkillIds { get; }
    internal StringName ScreeningMode { get; }
    internal bool EnableAoeSetupPositioning { get; }
    internal int AoeSetupMinTargetCount { get; }
    internal int AoeSetupTargetCountWeight { get; }
    internal int AoeSetupImprovementWeight { get; }
    internal int AoeSetupFriendlyFirePenalty { get; }
    internal int ScreeningMinHpBasisPoints { get; }
    internal int ScreeningAllyMinAttackRange { get; }
    internal int ScreeningEnemyMaxContactRange { get; }
    internal int ScreeningThreatDistanceBuffer { get; }
    internal int ScreeningPathBonus { get; }
}
