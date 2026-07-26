using Godot;

[GlobalClass]
public partial class SettlementDistributionRule : Resource
{
    [Export]
    public string settlement_id { get; set; } = "";

    [Export]
    public Vector2I preferred_origin { get; set; } = Vector2I.Zero;

    [Export]
    public string faction_id { get; set; } = "neutral";

    internal SettlementDistributionDefinition ToDefinition(string path) =>
        SettlementDistributionDefinition.FromResource(this, path);

    public string GetSettlementTemplateId()
    {
        return (settlement_id ?? string.Empty).Trim();
    }
}
