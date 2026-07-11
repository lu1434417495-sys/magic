using System.Collections.Generic;
using Godot;

internal sealed class EnemyAiTransitionConditionDefinition
{
    internal EnemyAiTransitionConditionDefinition(
        StringName predicate,
        int basisPoints,
        int maxDistance,
        IReadOnlyList<StringName> stateIds,
        IReadOnlyList<StringName> affordances
    )
    {
        Predicate = predicate;
        BasisPoints = basisPoints;
        MaxDistance = maxDistance;
        StateIds = EnemyDefinitionCollections.FreezeList(stateIds);
        Affordances = EnemyDefinitionCollections.FreezeList(affordances);
    }

    internal StringName Predicate { get; }
    internal EnemyAiTransitionPredicate PredicateKind =>
        EnemyAiTransitionConditionDef.ToPredicate(Predicate);
    internal int BasisPoints { get; }
    internal int MaxDistance { get; }
    internal IReadOnlyList<StringName> StateIds { get; }
    internal IReadOnlyList<StringName> Affordances { get; }
}
