using System.Collections.Generic;

internal sealed class WildEncounterRosterStageDefinition
{
    internal WildEncounterRosterStageDefinition(
        int stage,
        IReadOnlyList<WildEncounterRosterUnitEntryDefinition> unitEntries
    )
    {
        Stage = stage;
        UnitEntries = EnemyDefinitionCollections.FreezeList(unitEntries);
    }

    internal int Stage { get; }
    internal IReadOnlyList<WildEncounterRosterUnitEntryDefinition> UnitEntries { get; }
}
