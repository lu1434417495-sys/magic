using System;

internal static class WorldContentKinds
{
    internal static SettlementTierKind ToSettlementTier(string value) => value switch
    {
        "village" => SettlementTierKind.Village,
        "town" => SettlementTierKind.Town,
        "city" => SettlementTierKind.City,
        "capital" => SettlementTierKind.Capital,
        "world_stronghold" => SettlementTierKind.WorldStronghold,
        "metropolis" => SettlementTierKind.Metropolis,
        _ => SettlementTierKind.Unknown,
    };

    internal static WorldVerticalBandKind ToVerticalBand(string value) => value switch
    {
        "all" => WorldVerticalBandKind.All,
        "north" => WorldVerticalBandKind.North,
        "south" => WorldVerticalBandKind.South,
        _ => WorldVerticalBandKind.Unknown,
    };
}
