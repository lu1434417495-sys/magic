using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public enum WorldUniqueEquipmentLocationKind
{
    Reserve = 0,
    Shop = 1,
}

public sealed class WorldUniqueEquipmentPoolState
{
    internal const string PhoenixRebirthPoolId = "phoenix_rebirth_set";

    private sealed class Entry
    {
        internal EquipmentInstanceState Instance;
        internal WorldUniqueEquipmentLocationKind LocationKind;
        internal string SettlementId = "";
        internal string ShopId = "";

        internal Entry Duplicate() =>
            new()
            {
                Instance = Instance?.DuplicateState(),
                LocationKind = LocationKind,
                SettlementId = SettlementId ?? "",
                ShopId = ShopId ?? "",
            };
    }

    private readonly List<Entry> _entries = new();

    public string PoolId { get; private set; } = "";
    public int RemainingInstanceCount => _entries.Count;

    private WorldUniqueEquipmentPoolState() { }

    internal static WorldUniqueEquipmentPoolState CreateNew(
        string poolId,
        IEnumerable<EquipmentInstanceState> instances
    )
    {
        string normalizedPoolId = (poolId ?? "").Trim();
        if (normalizedPoolId.Length == 0 || instances == null)
            return null;

        var result = new WorldUniqueEquipmentPoolState { PoolId = normalizedPoolId };
        var instanceIds = new HashSet<StringName>();
        var itemIds = new HashSet<StringName>();
        foreach (EquipmentInstanceState instance in instances)
        {
            if (
                instance == null
                || instance.instance_id == ""
                || instance.item_id == ""
                || !instanceIds.Add(instance.instance_id)
                || !itemIds.Add(instance.item_id)
            )
            {
                return null;
            }
            result._entries.Add(
                new Entry
                {
                    Instance = instance,
                    LocationKind = WorldUniqueEquipmentLocationKind.Reserve,
                }
            );
        }
        return result._entries.Count > 0 ? result : null;
    }

    internal WorldUniqueEquipmentPoolState DuplicateState()
    {
        var copy = new WorldUniqueEquipmentPoolState { PoolId = PoolId };
        foreach (Entry entry in _entries)
            copy._entries.Add(entry?.Duplicate());
        return copy;
    }

    internal void RestoreFrom(WorldUniqueEquipmentPoolState snapshot)
    {
        if (snapshot == null)
            return;
        PoolId = snapshot.PoolId ?? "";
        _entries.Clear();
        foreach (Entry entry in snapshot._entries)
            _entries.Add(entry?.Duplicate());
    }

    internal int CountAtLocation(WorldUniqueEquipmentLocationKind locationKind)
    {
        int count = 0;
        foreach (Entry entry in _entries)
        {
            if (entry?.Instance != null && entry.LocationKind == locationKind)
                count++;
        }
        return count;
    }

    internal IReadOnlyList<EquipmentInstanceState> SnapshotInstances()
    {
        var result = new List<EquipmentInstanceState>();
        foreach (Entry entry in _entries)
        {
            EquipmentInstanceState copy = entry?.Instance?.DuplicateState();
            if (copy != null)
                result.Add(copy);
        }
        return result.AsReadOnly();
    }

    internal bool TryGetShopOffer(
        string settlementId,
        string shopId,
        StringName itemId,
        out StringName instanceId
    )
    {
        instanceId = "";
        Entry entry = FindShopEntry(settlementId, shopId, itemId);
        if (entry?.Instance == null)
            return false;
        instanceId = entry.Instance.instance_id;
        return instanceId != "";
    }

    internal int ReturnShopOffers(string settlementId, string shopId)
    {
        string normalizedSettlementId = NormalizeId(settlementId);
        string normalizedShopId = NormalizeId(shopId);
        if (normalizedSettlementId.Length == 0 || normalizedShopId.Length == 0)
            return 0;
        int returned = 0;
        foreach (Entry entry in _entries)
        {
            if (
                entry?.Instance == null
                || entry.LocationKind != WorldUniqueEquipmentLocationKind.Shop
                || !string.Equals(entry.SettlementId, normalizedSettlementId, StringComparison.Ordinal)
                || !string.Equals(entry.ShopId, normalizedShopId, StringComparison.Ordinal)
            )
            {
                continue;
            }
            entry.LocationKind = WorldUniqueEquipmentLocationKind.Reserve;
            entry.SettlementId = "";
            entry.ShopId = "";
            returned++;
        }
        return returned;
    }

    internal bool TryAssignRandomReserveToShop(
        string settlementId,
        string shopId,
        Func<int, int, int> rollRange,
        out StringName itemId,
        out StringName instanceId
    )
    {
        itemId = "";
        instanceId = "";
        string normalizedSettlementId = NormalizeId(settlementId);
        string normalizedShopId = NormalizeId(shopId);
        if (
            normalizedSettlementId.Length == 0
            || normalizedShopId.Length == 0
            || rollRange == null
        )
        {
            return false;
        }

        var reserveIndices = new List<int>();
        for (int index = 0; index < _entries.Count; index++)
        {
            Entry candidate = _entries[index];
            if (
                candidate?.Instance != null
                && candidate.LocationKind == WorldUniqueEquipmentLocationKind.Reserve
            )
            {
                reserveIndices.Add(index);
            }
        }
        if (reserveIndices.Count == 0)
            return false;
        int pickedOrdinal = Math.Clamp(rollRange(0, reserveIndices.Count - 1), 0, reserveIndices.Count - 1);
        Entry picked = _entries[reserveIndices[pickedOrdinal]];
        picked.LocationKind = WorldUniqueEquipmentLocationKind.Shop;
        picked.SettlementId = normalizedSettlementId;
        picked.ShopId = normalizedShopId;
        itemId = picked.Instance.item_id;
        instanceId = picked.Instance.instance_id;
        return itemId != "" && instanceId != "";
    }

    internal bool TryTakeShopOffer(
        string settlementId,
        string shopId,
        StringName itemId,
        out EquipmentInstanceState instance
    )
    {
        instance = null;
        Entry entry = FindShopEntry(settlementId, shopId, itemId);
        if (entry?.Instance == null)
            return false;
        int index = _entries.IndexOf(entry);
        if (index < 0)
            return false;
        instance = entry.Instance;
        _entries.RemoveAt(index);
        return true;
    }

    internal bool TryTakeReserveByItem(
        StringName itemId,
        out EquipmentInstanceState instance
    )
    {
        instance = null;
        StringName normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        if (normalizedItemId == "")
            return false;
        for (int index = 0; index < _entries.Count; index++)
        {
            Entry entry = _entries[index];
            if (
                entry?.Instance == null
                || entry.LocationKind != WorldUniqueEquipmentLocationKind.Reserve
                || entry.Instance.item_id != normalizedItemId
            )
            {
                continue;
            }
            instance = entry.Instance;
            _entries.RemoveAt(index);
            return true;
        }
        return false;
    }

    internal bool TryTakeRandomReserve(
        Func<int, int, int> rollRange,
        out EquipmentInstanceState instance
    )
    {
        instance = null;
        if (rollRange == null)
            return false;
        var reserveIndices = new List<int>();
        for (int index = 0; index < _entries.Count; index++)
        {
            Entry entry = _entries[index];
            if (
                entry?.Instance != null
                && entry.LocationKind == WorldUniqueEquipmentLocationKind.Reserve
            )
            {
                reserveIndices.Add(index);
            }
        }
        if (reserveIndices.Count == 0)
            return false;
        int pickedOrdinal = Math.Clamp(rollRange(0, reserveIndices.Count - 1), 0, reserveIndices.Count - 1);
        int pickedIndex = reserveIndices[pickedOrdinal];
        instance = _entries[pickedIndex].Instance;
        _entries.RemoveAt(pickedIndex);
        return instance != null;
    }

    internal bool TryReturnToReserve(EquipmentInstanceState instance)
    {
        if (instance == null || instance.instance_id == "" || instance.item_id == "")
            return false;
        foreach (Entry entry in _entries)
        {
            if (entry?.Instance?.instance_id == instance.instance_id)
                return false;
        }
        _entries.Add(
            new Entry
            {
                Instance = instance,
                LocationKind = WorldUniqueEquipmentLocationKind.Reserve,
            }
        );
        return true;
    }

    internal bool TryReturnToShop(
        EquipmentInstanceState instance,
        string settlementId,
        string shopId
    )
    {
        if (
            instance == null
            || instance.instance_id == ""
            || instance.item_id == ""
            || NormalizeId(settlementId).Length == 0
            || NormalizeId(shopId).Length == 0
        )
        {
            return false;
        }
        foreach (Entry entry in _entries)
        {
            if (entry?.Instance?.instance_id == instance.instance_id)
                return false;
        }
        _entries.Add(
            new Entry
            {
                Instance = instance,
                LocationKind = WorldUniqueEquipmentLocationKind.Shop,
                SettlementId = NormalizeId(settlementId),
                ShopId = NormalizeId(shopId),
            }
        );
        return true;
    }

    internal Dictionary<string, object> BuildSaveSnapshotPlain()
    {
        var entries = new List<object>();
        foreach (Entry entry in _entries)
        {
            if (entry?.Instance == null)
                continue;
            entries.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["location_kind"] = ToWireValue(entry.LocationKind),
                    ["settlement_id"] = entry.SettlementId ?? "",
                    ["shop_id"] = entry.ShopId ?? "",
                    ["equipment_instance"] = entry.Instance.BuildSaveSnapshotPlain(),
                }
            );
        }
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["pool_id"] = PoolId ?? "",
            ["entries"] = entries,
        };
    }

    internal static WorldUniqueEquipmentPoolState FromDictionary(GDictionary payload)
    {
        if (!TryFromDictionary(payload, out WorldUniqueEquipmentPoolState state, out _))
            return null;
        return state;
    }

    internal static string GetPayloadValidationError(GDictionary payload) =>
        TryFromDictionary(payload, out _, out string error) ? "" : error;

    private static bool TryFromDictionary(
        GDictionary payload,
        out WorldUniqueEquipmentPoolState state,
        out string error
    )
    {
        state = null;
        error = "";
        if (payload == null)
        {
            error = "world unique equipment pool must be a Dictionary.";
            return false;
        }
        if (
            payload.Count != 2
            || !payload.ContainsKey("pool_id")
            || !payload.ContainsKey("entries")
            || payload["pool_id"].VariantType != Variant.Type.String
            || payload["entries"].VariantType != Variant.Type.Array
        )
        {
            error = "world unique equipment pool fields must exactly match current schema.";
            return false;
        }
        string poolId = payload["pool_id"].AsString().StripEdges();
        if (poolId.Length == 0)
        {
            error = "world unique equipment pool pool_id is required.";
            return false;
        }
        var parsed = new WorldUniqueEquipmentPoolState { PoolId = poolId };
        using GArray entries = payload["entries"].AsGodotArray();
        for (int index = 0; index < entries.Count; index++)
        {
            Variant rawEntry = entries[index];
            if (rawEntry.VariantType != Variant.Type.Dictionary)
            {
                error = $"world unique equipment pool entries[{index}] must be a Dictionary.";
                return false;
            }
            using GDictionary entryPayload = rawEntry.AsGodotDictionary();
            if (
                entryPayload.Count != 4
                || !entryPayload.ContainsKey("location_kind")
                || !entryPayload.ContainsKey("settlement_id")
                || !entryPayload.ContainsKey("shop_id")
                || !entryPayload.ContainsKey("equipment_instance")
                || entryPayload["location_kind"].VariantType != Variant.Type.String
                || entryPayload["settlement_id"].VariantType != Variant.Type.String
                || entryPayload["shop_id"].VariantType != Variant.Type.String
                || entryPayload["equipment_instance"].VariantType != Variant.Type.Dictionary
            )
            {
                error = $"world unique equipment pool entries[{index}] fields must exactly match current schema.";
                return false;
            }
            if (!TryParseLocation(entryPayload["location_kind"].AsString(), out WorldUniqueEquipmentLocationKind locationKind))
            {
                error = $"world unique equipment pool entries[{index}] has invalid location_kind.";
                return false;
            }
            string settlementId = entryPayload["settlement_id"].AsString().StripEdges();
            string shopId = entryPayload["shop_id"].AsString().StripEdges();
            if (
                locationKind == WorldUniqueEquipmentLocationKind.Reserve
                    ? settlementId.Length != 0 || shopId.Length != 0
                    : settlementId.Length == 0 || shopId.Length == 0
            )
            {
                error = $"world unique equipment pool entries[{index}] location fields are inconsistent.";
                return false;
            }
            using GDictionary instancePayload =
                entryPayload["equipment_instance"].AsGodotDictionary();
            string instanceError = EquipmentInstanceState.GetPayloadValidationError(instancePayload);
            if (instanceError.Length > 0)
            {
                error = $"world unique equipment pool entries[{index}] is invalid: {instanceError}";
                return false;
            }
            EquipmentInstanceState instance = EquipmentInstanceState.FromDictionary(instancePayload);
            if (instance == null)
            {
                error = $"world unique equipment pool entries[{index}] could not be decoded.";
                return false;
            }
            parsed._entries.Add(
                new Entry
                {
                    Instance = instance,
                    LocationKind = locationKind,
                    SettlementId = settlementId,
                    ShopId = shopId,
                }
            );
        }
        state = parsed;
        return true;
    }

    private Entry FindShopEntry(string settlementId, string shopId, StringName itemId)
    {
        string normalizedSettlementId = NormalizeId(settlementId);
        string normalizedShopId = NormalizeId(shopId);
        StringName normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        foreach (Entry entry in _entries)
        {
            if (
                entry?.Instance != null
                && entry.LocationKind == WorldUniqueEquipmentLocationKind.Shop
                && entry.Instance.item_id == normalizedItemId
                && string.Equals(entry.SettlementId, normalizedSettlementId, StringComparison.Ordinal)
                && string.Equals(entry.ShopId, normalizedShopId, StringComparison.Ordinal)
            )
            {
                return entry;
            }
        }
        return null;
    }

    private static string NormalizeId(string value) => (value ?? "").Trim();

    private static string ToWireValue(WorldUniqueEquipmentLocationKind kind) =>
        kind == WorldUniqueEquipmentLocationKind.Shop ? "shop" : "reserve";

    private static bool TryParseLocation(
        string value,
        out WorldUniqueEquipmentLocationKind kind
    )
    {
        switch ((value ?? "").Trim())
        {
            case "reserve":
                kind = WorldUniqueEquipmentLocationKind.Reserve;
                return true;
            case "shop":
                kind = WorldUniqueEquipmentLocationKind.Shop;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}
