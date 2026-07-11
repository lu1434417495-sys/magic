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

    internal static WorldMapWildSpawnBundleDefinition FromResource(
        WorldMapWildSpawnBundle source,
        string path
    )
    {
        if (source == null)
            throw WorldDefinitionProjection.Invalid(path, "resource is null");
        return new WorldMapWildSpawnBundleDefinition(
            WorldDefinitionProjection.ProjectResources<WildSpawnRule, WildSpawnRuleDefinition>(
                source.WildMonsterDistributionProjectionBorrowed,
                path + ".wild_monster_distribution",
                WildSpawnRuleDefinition.FromResource
            )
        );
    }
}
