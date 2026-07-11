using Godot;

internal sealed class WaitActionDefinition : EnemyAiActionDefinition
{
    internal WaitActionDefinition(
        StringName actionId,
        StringName scoreBucketId,
        StringName actionIntent,
        int activeRestActionBaseScore,
        int activeRestMinStaminaResidue
    )
        : base(
            actionId,
            scoreBucketId,
            actionIntent,
            EnemyAiActionKind.Wait,
            System.Array.Empty<StringName>()
        )
    {
        ActiveRestActionBaseScore = activeRestActionBaseScore;
        ActiveRestMinStaminaResidue = activeRestMinStaminaResidue;
    }

    internal int ActiveRestActionBaseScore { get; }
    internal int ActiveRestMinStaminaResidue { get; }
}
