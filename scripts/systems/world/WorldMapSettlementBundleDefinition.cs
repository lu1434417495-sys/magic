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
}
