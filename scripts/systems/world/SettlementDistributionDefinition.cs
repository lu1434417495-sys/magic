using System;
using Godot;

public sealed class SettlementDistributionDefinition
{
    public SettlementDistributionDefinition(
        string settlementTemplateId,
        Vector2I preferredOrigin,
        string factionId,
        string countryId
    )
    {
        SettlementTemplateId = settlementTemplateId
            ?? throw new ArgumentNullException(nameof(settlementTemplateId));
        PreferredOrigin = preferredOrigin;
        FactionId = factionId ?? throw new ArgumentNullException(nameof(factionId));
        CountryId = countryId ?? throw new ArgumentNullException(nameof(countryId));
    }

    public string SettlementTemplateId { get; }
    public Vector2I PreferredOrigin { get; }
    public string FactionId { get; }
    public string CountryId { get; }

}
