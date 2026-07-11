using System;
using System.Collections.Generic;
using Godot;

internal sealed class EnemyAiBrainDefinition
{
    internal EnemyAiBrainDefinition(
        StringName brainId,
        StringName defaultStateId,
        BattleAiScoreProfileDefinition scoreProfile,
        IReadOnlyList<EnemyAiStateDefinition> states,
        IReadOnlyList<EnemyAiTransitionRuleDefinition> transitionRules
    )
    {
        BrainId = brainId;
        DefaultStateId = defaultStateId;
        ScoreProfile = scoreProfile ?? BattleAiScoreProfileDefinition.Default;
        StateOrder = EnemyDefinitionCollections.FreezeList(states);
        var stateMap = new Dictionary<StringName, EnemyAiStateDefinition>();
        foreach (EnemyAiStateDefinition state in StateOrder)
        {
            if (state == null || !stateMap.TryAdd(state.StateId, state))
                throw new ArgumentException($"Duplicate or null enemy AI state {state?.StateId}.", nameof(states));
        }
        States = EnemyDefinitionCollections.FreezeDictionary(stateMap);
        TransitionRules = EnemyDefinitionCollections.FreezeList(transitionRules);
    }

    internal StringName BrainId { get; }
    internal StringName DefaultStateId { get; }
    internal BattleAiScoreProfileDefinition ScoreProfile { get; }
    internal IReadOnlyDictionary<StringName, EnemyAiStateDefinition> States { get; }
    internal IReadOnlyList<EnemyAiStateDefinition> StateOrder { get; }
    internal IReadOnlyList<EnemyAiTransitionRuleDefinition> TransitionRules { get; }

    internal bool TryGetState(StringName stateId, out EnemyAiStateDefinition state) =>
        States.TryGetValue(stateId, out state);

    internal EnemyAiStateDefinition GetState(StringName stateId) =>
        TryGetState(stateId, out EnemyAiStateDefinition state) ? state : null;

    internal bool HasState(StringName stateId) => States.ContainsKey(stateId);
}
