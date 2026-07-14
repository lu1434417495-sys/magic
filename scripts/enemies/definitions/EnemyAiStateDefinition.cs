using System.Collections.Generic;
using Godot;

internal sealed class EnemyAiStateDefinition
{
    internal EnemyAiStateDefinition(
        StringName stateId,
        IReadOnlyList<EnemyAiActionDefinition> actions,
        IReadOnlyList<EnemyAiGenerationSlotDefinition> generationSlots
    )
    {
        StateId = stateId;
        Actions = EnemyDefinitionCollections.FreezeList(actions);
        GenerationSlots = EnemyDefinitionCollections.FreezeList(generationSlots);
    }

    internal StringName StateId { get; }
    internal IReadOnlyList<EnemyAiActionDefinition> Actions { get; }
    internal IReadOnlyList<EnemyAiGenerationSlotDefinition> GenerationSlots { get; }
}
