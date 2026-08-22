using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal static class TestWorldGenerationDefinitionFactory
{
    internal static WorldGenerationDefinition Load(StringName generationId)
    {
        var registry = new WorldContentRegistry();
        registry.Rebuild();
        if (registry.GetValidationErrors().Count > 0)
        {
            throw new InvalidOperationException(
                "World JSON registry is invalid: "
                    + string.Join(" | ", registry.GetValidationErrors())
            );
        }
        if (!registry.GetGenerations().TryGetValue(generationId, out WorldGenerationDefinition definition))
            throw new InvalidOperationException($"Missing world generation ID {generationId}.");
        return definition;
    }

    internal static WorldGenerationDefinition Create(
        StringName generationId = default,
        Vector2I? worldSizeInChunks = null,
        Vector2I? chunkSize = null,
        Vector2I? playerStartCoord = null,
        bool proceduralGenerationEnabled = false,
        int startingWildSpawnMinDistance = 3,
        int startingWildSpawnMaxDistance = 4,
        StringName sharedContentId = default,
        IReadOnlyList<SettlementDefinition> settlementLibrary = null,
        IReadOnlyList<FacilityDefinition> facilityLibrary = null,
        IReadOnlyList<SettlementDistributionDefinition> settlementDistribution = null,
        IReadOnlyList<WildSpawnRuleDefinition> wildMonsterDistribution = null,
        IReadOnlyList<MountedSubmapDefinition> mountedSubmaps = null,
        IReadOnlyList<WorldEventDefinition> worldEvents = null,
        WorldMapSettlementBundleDefinition defaultSettlementBundle = null,
        WorldMapWildSpawnBundleDefinition defaultWildSpawnBundle = null,
        IReadOnlyDictionary<StringName, WorldMapSettlementNamePoolDefinition> settlementNamePools = null
    ) =>
        new(
            generationId == default ? new StringName("test_fixture") : generationId,
            20260403,
            worldSizeInChunks ?? new Vector2I(2, 2),
            chunkSize ?? new Vector2I(8, 8),
            playerStartCoord ?? new Vector2I(1, 1),
            4,
            proceduralGenerationEnabled,
            2,
            sharedContentId == default ? new StringName("") : sharedContentId,
            1,
            1,
            1,
            1,
            1,
            0,
            80,
            110,
            150,
            220,
            280,
            340,
            false,
            startingWildSpawnMinDistance,
            startingWildSpawnMaxDistance,
            settlementLibrary ?? Array.Empty<SettlementDefinition>(),
            facilityLibrary ?? Array.Empty<FacilityDefinition>(),
            settlementDistribution ?? Array.Empty<SettlementDistributionDefinition>(),
            wildMonsterDistribution ?? Array.Empty<WildSpawnRuleDefinition>(),
            mountedSubmaps ?? Array.Empty<MountedSubmapDefinition>(),
            worldEvents ?? Array.Empty<WorldEventDefinition>(),
            defaultSettlementBundle,
            defaultWildSpawnBundle,
            settlementNamePools
                ?? new ReadOnlyDictionary<StringName, WorldMapSettlementNamePoolDefinition>(
                    new Dictionary<StringName, WorldMapSettlementNamePoolDefinition>()
                )
        );
}
