using Godot;

internal sealed class RetreatActionDefinition : EnemyAiActionDefinition
{
    internal RetreatActionDefinition(
        StringName actionId,
        StringName scoreBucketId,
        StringName actionIntent,
        StringName targetSelector,
        int minimumSafeDistance,
        bool useDynamicThreatSafeDistance,
        int safeDistanceMargin
    )
        : base(
            actionId,
            scoreBucketId,
            actionIntent,
            EnemyAiActionKind.Retreat,
            System.Array.Empty<StringName>()
        )
    {
        TargetSelector = targetSelector;
        MinimumSafeDistance = minimumSafeDistance;
        UseDynamicThreatSafeDistance = useDynamicThreatSafeDistance;
        SafeDistanceMargin = safeDistanceMargin;
    }

    internal StringName TargetSelector { get; }
    internal int MinimumSafeDistance { get; }
    internal bool UseDynamicThreatSafeDistance { get; }
    internal int SafeDistanceMargin { get; }
}
