using System.Collections.Generic;
using Godot;

public sealed class CombatSequentialLineHitDefinition
{
    public CombatSequentialLineHitDefinition(
        IReadOnlyList<int> minimumPrimaryDistanceCurve,
        IReadOnlyList<int> continuationRangeCurve,
        IReadOnlyList<int> followUpAttackPenaltyCurve
    )
    {
        MinimumPrimaryDistanceCurve = SkillDefinitionCollectionFreeze.List(
            minimumPrimaryDistanceCurve
        );
        ContinuationRangeCurve = SkillDefinitionCollectionFreeze.List(
            continuationRangeCurve
        );
        FollowUpAttackPenaltyCurve = SkillDefinitionCollectionFreeze.List(
            followUpAttackPenaltyCurve
        );
    }

    public IReadOnlyList<int> MinimumPrimaryDistanceCurve { get; }
    public IReadOnlyList<int> ContinuationRangeCurve { get; }
    public IReadOnlyList<int> FollowUpAttackPenaltyCurve { get; }

    public int GetMinimumPrimaryDistance(int skillLevel) =>
        Mathf.Max(ReadCurveValue(MinimumPrimaryDistanceCurve, skillLevel, 1), 1);

    public int GetContinuationRange(int skillLevel) =>
        Mathf.Max(ReadCurveValue(ContinuationRangeCurve, skillLevel, 1), 1);

    public int GetFollowUpAttackPenalty(int skillLevel) =>
        Mathf.Max(ReadCurveValue(FollowUpAttackPenaltyCurve, skillLevel, 0), 0);

    internal static CombatSequentialLineHitDefinition FromDiagnosticFixture(
        CombatSequentialLineHitDef source
    ) =>
        source == null
            ? null
            : new CombatSequentialLineHitDefinition(
                source.minimum_primary_distance_curve,
                source.continuation_range_curve,
                source.follow_up_attack_penalty_curve
            );

    private static int ReadCurveValue(
        IReadOnlyList<int> curve,
        int skillLevel,
        int fallback
    )
    {
        if (curve == null || curve.Count == 0)
            return fallback;
        return curve[Mathf.Clamp(skillLevel, 0, curve.Count - 1)];
    }
}
