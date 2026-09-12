using System;
using System.Collections.Generic;

public sealed class WorldMapSettlementNamePoolDefinition
{
    public WorldMapSettlementNamePoolDefinition(
        SettlementTierKind settlementTier,
        IReadOnlyList<string> displayNames
    )
    {
        SettlementTier = settlementTier;
        DisplayNames = BuildUniqueDisplayNames(displayNames);
    }

    public SettlementTierKind SettlementTier { get; }
    public IReadOnlyList<string> DisplayNames { get; }

    public IReadOnlyList<string> BuildUniqueDisplayNames() => DisplayNames;

    private static IReadOnlyList<string> BuildUniqueDisplayNames(
        IReadOnlyList<string> displayNames
    )
    {
        ArgumentNullException.ThrowIfNull(displayNames);
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string rawName in displayNames)
        {
            if (rawName == null)
                throw new ArgumentException("Display names must not contain null.", nameof(displayNames));
            string normalized = rawName.Trim();
            if (normalized.Length > 0 && seen.Add(normalized))
                result.Add(normalized);
        }
        return WorldDefinitionProjection.FreezeValues(result, nameof(displayNames));
    }
}
