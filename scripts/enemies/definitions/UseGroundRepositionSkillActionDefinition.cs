using System.Collections.Generic;
using Godot;

internal sealed class UseGroundRepositionSkillActionDefinition : EnemyAiActionDefinition
{
    internal UseGroundRepositionSkillActionDefinition(
        StringName actionId,
        StringName scoreBucketId,
        StringName actionIntent,
        IReadOnlyList<StringName> skillIds,
        StringName targetSelector,
        int minimumSafeDistance,
        int safeDistanceMargin,
        int desiredMaxDistanceBonus,
        int actionBaseScore,
        int minSurvivalMarginGainToEscape
    )
        : base(
            actionId,
            scoreBucketId,
            actionIntent,
            EnemyAiActionKind.UseGroundRepositionSkill,
            skillIds
        )
    {
        SkillIds = EnemyDefinitionCollections.FreezeList(skillIds);
        TargetSelector = targetSelector;
        MinimumSafeDistance = minimumSafeDistance;
        SafeDistanceMargin = safeDistanceMargin;
        DesiredMaxDistanceBonus = desiredMaxDistanceBonus;
        ActionBaseScore = actionBaseScore;
        MinSurvivalMarginGainToEscape = minSurvivalMarginGainToEscape;
    }

    internal IReadOnlyList<StringName> SkillIds { get; }
    internal StringName TargetSelector { get; }
    internal int MinimumSafeDistance { get; }
    internal int SafeDistanceMargin { get; }
    internal int DesiredMaxDistanceBonus { get; }
    internal int ActionBaseScore { get; }
    internal int MinSurvivalMarginGainToEscape { get; }
}
