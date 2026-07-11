using System.Collections.Generic;
using Godot;

internal sealed class EnemyAiTransitionRuleDefinition
{
    internal EnemyAiTransitionRuleDefinition(
        StringName ruleId,
        int order,
        IReadOnlyList<StringName> fromStateIds,
        StringName targetStateId,
        IReadOnlyList<EnemyAiTransitionConditionDefinition> conditions,
        string designerNote
    )
    {
        RuleId = ruleId;
        Order = order;
        FromStateIds = EnemyDefinitionCollections.FreezeList(fromStateIds);
        TargetStateId = targetStateId;
        Conditions = EnemyDefinitionCollections.FreezeList(conditions);
        DesignerNote = designerNote ?? "";
    }

    internal StringName RuleId { get; }
    internal int Order { get; }
    internal IReadOnlyList<StringName> FromStateIds { get; }
    internal StringName TargetStateId { get; }
    internal IReadOnlyList<EnemyAiTransitionConditionDefinition> Conditions { get; }
    internal string DesignerNote { get; }

    internal bool AppliesToState(StringName stateId) =>
        FromStateIds.Count == 0 || Contains(FromStateIds, stateId);

    private static bool Contains(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values)
        {
            if (value == expected)
                return true;
        }
        return false;
    }
}
