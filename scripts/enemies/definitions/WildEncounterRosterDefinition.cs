using System;
using System.Collections.Generic;
using Godot;

internal sealed class WildEncounterRosterDefinition
{
    internal WildEncounterRosterDefinition(
        StringName profileId,
        string displayName,
        int initialStage,
        int growthStepInterval,
        int suppressionStepsOnVictory,
        IReadOnlyList<WildEncounterRosterStageDefinition> stages
    )
    {
        ProfileId = profileId;
        DisplayName = displayName ?? "";
        InitialStage = initialStage;
        GrowthStepInterval = growthStepInterval;
        SuppressionStepsOnVictory = suppressionStepsOnVictory;
        Stages = EnemyDefinitionCollections.FreezeList(stages);
    }

    internal StringName ProfileId { get; }
    internal string DisplayName { get; }
    internal int InitialStage { get; }
    internal int GrowthStepInterval { get; }
    internal int SuppressionStepsOnVictory { get; }
    internal IReadOnlyList<WildEncounterRosterStageDefinition> Stages { get; }

    internal int GetMaxStage()
    {
        int maxStage = -1;
        foreach (WildEncounterRosterStageDefinition stage in Stages)
            maxStage = Math.Max(maxStage, stage.Stage);
        return maxStage;
    }

    internal IReadOnlyList<WildEncounterRosterUnitEntryDefinition> GetStageUnitEntries(int stage)
    {
        int bestStage = -1;
        IReadOnlyList<WildEncounterRosterUnitEntryDefinition> best =
            Array.Empty<WildEncounterRosterUnitEntryDefinition>();
        foreach (WildEncounterRosterStageDefinition candidate in Stages)
        {
            if (candidate.Stage > stage || candidate.Stage < bestStage)
                continue;
            bestStage = candidate.Stage;
            best = candidate.UnitEntries;
        }
        return best;
    }
}
