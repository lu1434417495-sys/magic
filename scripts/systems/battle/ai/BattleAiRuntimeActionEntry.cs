using Godot;

internal sealed class BattleAiRuntimeActionEntry
{
    internal BattleAiRuntimeActionEntry(
        EnemyAiActionDefinition action,
        BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata
    )
    {
        Action = action ?? throw new System.ArgumentNullException(nameof(action));
        Metadata = metadata?.Clone() ?? new BattleAiRuntimeActionPlan.RuntimeActionMetadata();
    }

    internal EnemyAiActionDefinition Action { get; }

    internal BattleAiRuntimeActionPlan.RuntimeActionMetadata Metadata { get; }

    internal StringName ActionId => Action.ActionId;

    internal StringName ScoreBucketId => Action.ScoreBucketId;
}
