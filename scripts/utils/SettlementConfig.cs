using Godot;

[GlobalClass]
public partial class SettlementConfig : Resource
{
    public enum SettlementTier
    {
        VILLAGE = 0,
        TOWN = 1,
        CITY = 2,
        CAPITAL = 3,
        WORLD_STRONGHOLD = 4,
        METROPOLIS = 5,
    }

    [Export]
    public string settlement_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export]
    public int tier { get; set; } = (int)SettlementTier.VILLAGE;

    [Export]
    public Godot.Collections.Array<Resource> facility_slots { get; set; } = new();

    [Export]
    public Godot.Collections.Array<string> guaranteed_facility_ids { get; set; } = new();

    [Export]
    public Godot.Collections.Array<Resource> optional_facility_pool { get; set; } = new();

    [Export]
    public int max_optional_facilities { get; set; } = 0;

    public string GetTemplateId()
    {
        return (settlement_id ?? string.Empty).Trim();
    }

    public Vector2I GetFootprintSize()
    {
        return tier switch
        {
            (int)SettlementTier.VILLAGE => Vector2I.One,
            (int)SettlementTier.TOWN => new Vector2I(2, 2),
            (int)SettlementTier.CITY => new Vector2I(2, 2),
            (int)SettlementTier.CAPITAL => new Vector2I(3, 3),
            (int)SettlementTier.WORLD_STRONGHOLD => new Vector2I(4, 4),
            (int)SettlementTier.METROPOLIS => new Vector2I(5, 5),
            _ => Vector2I.One,
        };
    }

    public string GetTierName()
    {
        return tier switch
        {
            (int)SettlementTier.VILLAGE => "村",
            (int)SettlementTier.TOWN => "镇",
            (int)SettlementTier.CITY => "城市",
            (int)SettlementTier.CAPITAL => "主城",
            (int)SettlementTier.WORLD_STRONGHOLD => "世界据点",
            (int)SettlementTier.METROPOLIS => "都会",
            _ => "未知",
        };
    }

}
