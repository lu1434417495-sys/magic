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

    public string get_settlement_template_id()
    {
        return (settlement_id ?? string.Empty).Trim();
    }
}
