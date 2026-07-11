using Godot;

internal sealed class UseChargeActionDefinition : EnemyAiActionDefinition
{
    internal UseChargeActionDefinition(
        StringName actionId,
        StringName scoreBucketId,
        StringName actionIntent,
        StringName skillId,
        StringName targetSelector,
        int minimumChargeMoveDistance
    )
        : base(
            actionId,
            scoreBucketId,
            actionIntent,
            EnemyAiActionKind.UseCharge,
            skillId == "" ? System.Array.Empty<StringName>() : new[] { skillId }
        )
    {
        SkillId = skillId;
        TargetSelector = targetSelector;
        MinimumChargeMoveDistance = minimumChargeMoveDistance;
    }

    internal StringName SkillId { get; }
    internal StringName TargetSelector { get; }
    internal int MinimumChargeMoveDistance { get; }
}
