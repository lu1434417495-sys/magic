using System;
using System.Collections.Generic;

public sealed class WorldMapWildSpawnBundleDefinition
{
    public WorldMapWildSpawnBundleDefinition(
        IReadOnlyList<WildSpawnRuleDefinition> wildMonsterDistribution
    )
    {
        WildMonsterDistribution = WorldDefinitionProjection.FreezeValues(
            wildMonsterDistribution,
            nameof(wildMonsterDistribution)
        );
    }

    public IReadOnlyList<WildSpawnRuleDefinition> WildMonsterDistribution { get; }
}
