using System;
using Godot;

public sealed class SettlementDistributionDefinition
{
    public SettlementDistributionDefinition(
        string settlementTemplateId,
        Vector2I preferredOrigin,
        string factionId
    )
    {
        SettlementTemplateId = settlementTemplateId
            ?? throw new ArgumentNullException(nameof(settlementTemplateId));
        PreferredOrigin = preferredOrigin;
        FactionId = factionId ?? throw new ArgumentNullException(nameof(factionId));
    }

    public string SettlementTemplateId { get; }
    public Vector2I PreferredOrigin { get; }
    public string FactionId { get; }

    internal static SettlementDistributionDefinition FromResource(
        SettlementDistributionRule source,
        string path
    )
    {
        if (source == null)
            throw WorldDefinitionProjection.Invalid(path, "resource is null");
        return new SettlementDistributionDefinition(
            WorldDefinitionProjection.RequireString(
                source.settlement_id,
                path + ".settlement_id"
            ).Trim(),
            source.preferred_origin,
            WorldDefinitionProjection.RequireString(
                source.faction_id,
                path + ".faction_id"
            ).Trim()
        );
    }
}
