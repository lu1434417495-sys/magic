using System;
using System.Collections.Generic;

public sealed class WorldMapSettlementBundleDefinition
{
    public WorldMapSettlementBundleDefinition(
        IReadOnlyList<SettlementDefinition> settlementLibrary,
        IReadOnlyList<FacilityDefinition> facilityLibrary
    )
    {
        SettlementLibrary = WorldDefinitionProjection.FreezeValues(
            settlementLibrary,
            nameof(settlementLibrary)
        );
        FacilityLibrary = WorldDefinitionProjection.FreezeValues(
            facilityLibrary,
            nameof(facilityLibrary)
        );
    }

    public IReadOnlyList<SettlementDefinition> SettlementLibrary { get; }
    public IReadOnlyList<FacilityDefinition> FacilityLibrary { get; }

    internal static WorldMapSettlementBundleDefinition FromResource(
        WorldMapSettlementBundle source,
        string path
    )
    {
        if (source == null)
            throw WorldDefinitionProjection.Invalid(path, "resource is null");
        return new WorldMapSettlementBundleDefinition(
            WorldDefinitionProjection.ProjectResources<SettlementConfig, SettlementDefinition>(
                source.SettlementLibraryProjectionBorrowed,
                path + ".settlement_library",
                SettlementDefinition.FromResource
            ),
            WorldDefinitionProjection.ProjectResources<FacilityConfig, FacilityDefinition>(
                source.FacilityLibraryProjectionBorrowed,
                path + ".facility_library",
                FacilityDefinition.FromResource
            )
        );
    }
}
