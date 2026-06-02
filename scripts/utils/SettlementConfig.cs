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

    public static int TIER_VILLAGE() => (int)SettlementTier.VILLAGE;

    public static int TIER_TOWN() => (int)SettlementTier.TOWN;

    public static int TIER_CITY() => (int)SettlementTier.CITY;

    public static int TIER_CAPITAL() => (int)SettlementTier.CAPITAL;

    public static int TIER_WORLD_STRONGHOLD() => (int)SettlementTier.WORLD_STRONGHOLD;

    public static int TIER_METROPOLIS() => (int)SettlementTier.METROPOLIS;

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

    public string get_template_id()
    {
        return (settlement_id ?? string.Empty).Trim();
    }

    public Vector2I get_footprint_size()
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

    public string get_tier_name()
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

    public static StringName tier_to_string_name(int tier)
    {
        switch (tier)
        {
            case (int)SettlementTier.VILLAGE:
                return new StringName("village");
            case (int)SettlementTier.TOWN:
                return new StringName("town");
            case (int)SettlementTier.CITY:
                return new StringName("city");
            case (int)SettlementTier.CAPITAL:
                return new StringName("capital");
            case (int)SettlementTier.WORLD_STRONGHOLD:
                return new StringName("world_stronghold");
            case (int)SettlementTier.METROPOLIS:
                return new StringName("metropolis");
            default:
                return new StringName("unknown");
        }
    }

    public static int tier_from_string_name(StringName value)
    {
        if (value == new StringName("village"))
            return (int)SettlementTier.VILLAGE;
        if (value == new StringName("town"))
            return (int)SettlementTier.TOWN;
        if (value == new StringName("city"))
            return (int)SettlementTier.CITY;
        if (value == new StringName("capital"))
            return (int)SettlementTier.CAPITAL;
        if (value == new StringName("world_stronghold"))
            return (int)SettlementTier.WORLD_STRONGHOLD;
        if (value == new StringName("metropolis"))
            return (int)SettlementTier.METROPOLIS;
        return (int)SettlementTier.VILLAGE;
    }
}
