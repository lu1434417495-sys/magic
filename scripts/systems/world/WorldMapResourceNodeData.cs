using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class WorldMapResourceNodeData
{
    public const string KindFarm = "farm";
    public const string KindHerbGarden = "herb_garden";
    public const string KindMine = "mine";

    public const string YieldTravelRation = "travel_ration";
    public const string YieldHealingHerb = "healing_herb";
    public const string YieldIronOre = "iron_ore";

    public readonly string NodeId;
    public readonly string NodeKind;
    public readonly string DisplayName;
    public readonly Vector2I WorldCoord;
    public readonly string YieldItemId;
    public readonly string SourceSettlementId;
    public readonly int MaxCharges;
    public readonly int RemainingCharges;

    private WorldMapResourceNodeData(
        string nodeId,
        string nodeKind,
        string displayName,
        Vector2I worldCoord,
        string yieldItemId,
        string sourceSettlementId,
        int maxCharges,
        int remainingCharges
    )
    {
        NodeId = nodeId ?? "";
        NodeKind = nodeKind ?? "";
        DisplayName = displayName ?? "";
        WorldCoord = worldCoord;
        YieldItemId = yieldItemId ?? "";
        SourceSettlementId = sourceSettlementId ?? "";
        MaxCharges = maxCharges;
        RemainingCharges = remainingCharges;
    }

    public bool Exists =>
        NodeId.Length > 0
        && IsKnownKind(NodeKind)
        && DisplayName.Length > 0
        && YieldItemId.Length > 0
        && MaxCharges > 0
        && RemainingCharges >= 0
        && RemainingCharges <= MaxCharges;

    internal static WorldMapResourceNodeData Create(
        string nodeId,
        string nodeKind,
        Vector2I worldCoord,
        string sourceSettlementId = ""
    )
    {
        return new WorldMapResourceNodeData(
            nodeId,
            nodeKind,
            DefaultDisplayName(nodeKind),
            worldCoord,
            DefaultYieldItemId(nodeKind),
            sourceSettlementId,
            DefaultMaxCharges(nodeKind),
            DefaultMaxCharges(nodeKind)
        );
    }

    public static WorldMapResourceNodeData FromDictionary(GDictionary data)
    {
        if (data == null || data.Count == 0)
            return null;
        string nodeKind = ReadString(data, "node_kind");
        var resourceNode = new WorldMapResourceNodeData(
            ReadString(data, "node_id"),
            nodeKind,
            ReadString(data, "display_name"),
            ReadVector2I(data, "world_coord", Vector2I.Zero),
            ReadString(data, "yield_item_id"),
            ReadString(data, "source_settlement_id"),
            ReadInt(data, "max_charges", 0),
            ReadInt(data, "remaining_charges", 0)
        );
        return resourceNode.Exists ? resourceNode : null;
    }

    internal GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["node_id"] = NodeId,
            ["node_kind"] = NodeKind,
            ["display_name"] = DisplayName,
            ["world_coord"] = WorldCoord,
            ["yield_item_id"] = YieldItemId,
            ["source_settlement_id"] = SourceSettlementId,
            ["max_charges"] = MaxCharges,
            ["remaining_charges"] = RemainingCharges,
        };
    }

    internal static bool IsKnownKind(string nodeKind) =>
        nodeKind == KindFarm || nodeKind == KindHerbGarden || nodeKind == KindMine;

    internal static string DefaultDisplayName(string nodeKind) =>
        nodeKind switch
        {
            KindFarm => "农田",
            KindHerbGarden => "药园",
            KindMine => "矿场",
            _ => "",
        };

    internal static string DefaultYieldItemId(string nodeKind) =>
        nodeKind switch
        {
            KindFarm => YieldTravelRation,
            KindHerbGarden => YieldHealingHerb,
            KindMine => YieldIronOre,
            _ => "",
        };

    internal static int DefaultMaxCharges(string nodeKind) =>
        nodeKind switch
        {
            KindFarm => 3,
            KindHerbGarden => 3,
            KindMine => 4,
            _ => 0,
        };

    private static string ReadString(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return "";
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
    }

    private static int ReadInt(GDictionary data, string key, int fallback)
    {
        if (data == null || !data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static Vector2I ReadVector2I(GDictionary data, string key, Vector2I fallback)
    {
        if (data == null || !data.ContainsKey(key))
            return fallback;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Vector2I ? value.AsVector2I() : fallback;
    }
}
