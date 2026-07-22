using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class SettlementShopStockEntryData
{
    public string ItemId { get; }
    public int Quantity { get; }
    public int UnitPrice { get; }
    public bool SoldOut { get; }

    private SettlementShopStockEntryData(
        string itemId,
        int quantity,
        int unitPrice,
        bool soldOut
    )
    {
        ItemId = itemId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        SoldOut = soldOut;
    }

    internal static SettlementShopStockEntryData Create(
        string itemId,
        int quantity,
        int unitPrice
    ) =>
        string.IsNullOrWhiteSpace(itemId) || quantity <= 0 || unitPrice <= 0
            ? null
            : new SettlementShopStockEntryData(itemId.Trim(), quantity, unitPrice, false);

    internal SettlementShopStockEntryData WithQuantity(int quantity) =>
        Create(ItemId, quantity, UnitPrice);

    internal Dictionary<string, object> BuildSnapshotPlain() =>
        new(StringComparer.Ordinal)
        {
            ["item_id"] = ItemId,
            ["quantity"] = Quantity,
            ["unit_price"] = UnitPrice,
            ["sold_out"] = SoldOut,
        };

    internal static bool TryFromPlain(
        IReadOnlyDictionary<string, object> data,
        string path,
        out SettlementShopStockEntryData entry,
        out string error
    )
    {
        entry = null;
        error = "";
        if (!WorldMapSettlementStateData.HasExactKeys(
                data,
                "item_id",
                "quantity",
                "unit_price",
                "sold_out"
            ))
        {
            error = $"{path} fields must exactly match current schema.";
            return false;
        }
        if (!WorldMapSettlementStateData.TryReadNonEmptyString(data, "item_id", out string itemId))
        {
            error = $"{path}.item_id must be a non-empty String.";
            return false;
        }
        if (!WorldMapSettlementStateData.TryReadInt(data, "quantity", out int quantity) || quantity <= 0)
        {
            error = $"{path}.quantity must be a positive int.";
            return false;
        }
        if (!WorldMapSettlementStateData.TryReadInt(data, "unit_price", out int unitPrice) || unitPrice <= 0)
        {
            error = $"{path}.unit_price must be a positive int.";
            return false;
        }
        if (!data.TryGetValue("sold_out", out object rawSoldOut) || rawSoldOut is not bool soldOut)
        {
            error = $"{path}.sold_out must be a bool.";
            return false;
        }
        if (soldOut)
        {
            error = $"{path}.sold_out must be false while the entry remains in current_inventory.";
            return false;
        }
        entry = new SettlementShopStockEntryData(itemId, quantity, unitPrice, false);
        return true;
    }
}

public sealed class SettlementShopStateData
{
    private readonly ReadOnlyCollection<SettlementShopStockEntryData> _currentInventory;

    public string ShopId { get; }
    public IReadOnlyList<SettlementShopStockEntryData> CurrentInventory => _currentInventory;
    public long Seed { get; }
    public int LastRefreshStep { get; }

    private SettlementShopStateData(
        string shopId,
        IReadOnlyList<SettlementShopStockEntryData> currentInventory,
        long seed,
        int lastRefreshStep
    )
    {
        ShopId = shopId;
        _currentInventory = new List<SettlementShopStockEntryData>(currentInventory).AsReadOnly();
        Seed = seed;
        LastRefreshStep = lastRefreshStep;
    }

    internal static SettlementShopStateData Create(
        string shopId,
        IEnumerable<SettlementShopStockEntryData> currentInventory,
        long seed,
        int lastRefreshStep
    )
    {
        if (string.IsNullOrWhiteSpace(shopId) || lastRefreshStep < 0)
            return null;
        var inventory = new List<SettlementShopStockEntryData>();
        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        if (currentInventory != null)
        {
            foreach (SettlementShopStockEntryData entry in currentInventory)
            {
                if (entry == null || !itemIds.Add(entry.ItemId))
                    return null;
                inventory.Add(entry);
            }
        }
        return new SettlementShopStateData(shopId.Trim(), inventory, seed, lastRefreshStep);
    }

    internal SettlementShopStateData WithInventory(
        IEnumerable<SettlementShopStockEntryData> currentInventory
    ) => Create(ShopId, currentInventory, Seed, LastRefreshStep)
        ?? throw new InvalidOperationException("Settlement shop inventory violates the typed state contract.");

    internal Dictionary<string, object> BuildSnapshotPlain()
    {
        var inventory = new List<object>();
        foreach (SettlementShopStockEntryData entry in _currentInventory)
            inventory.Add(entry.BuildSnapshotPlain());
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["shop_id"] = ShopId,
            ["current_inventory"] = inventory,
            ["seed"] = Seed,
            ["last_refresh_step"] = LastRefreshStep,
        };
    }

    internal static bool TryFromPlain(
        string mapShopId,
        IReadOnlyDictionary<string, object> data,
        string path,
        out SettlementShopStateData state,
        out string error
    )
    {
        state = null;
        error = "";
        if (!WorldMapSettlementStateData.HasExactKeys(
                data,
                "shop_id",
                "current_inventory",
                "seed",
                "last_refresh_step"
            ))
        {
            error = $"{path} fields must exactly match current schema.";
            return false;
        }
        if (!WorldMapSettlementStateData.TryReadNonEmptyString(data, "shop_id", out string shopId))
        {
            error = $"{path}.shop_id must be a non-empty String.";
            return false;
        }
        if (!string.Equals(mapShopId, shopId, StringComparison.Ordinal))
        {
            error = $"{path}.shop_id must match its shop_states key.";
            return false;
        }
        if (!WorldMapSettlementStateData.TryReadObjectList(data, "current_inventory", out IReadOnlyList<object> rawInventory))
        {
            error = $"{path}.current_inventory must be an Array.";
            return false;
        }
        var inventory = new List<SettlementShopStockEntryData>();
        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < rawInventory.Count; index++)
        {
            if (rawInventory[index] is not IReadOnlyDictionary<string, object> entryData)
            {
                error = $"{path}.current_inventory[{index}] must be a Dictionary.";
                return false;
            }
            if (!SettlementShopStockEntryData.TryFromPlain(
                    entryData,
                    $"{path}.current_inventory[{index}]",
                    out SettlementShopStockEntryData entry,
                    out error
                ))
            {
                return false;
            }
            if (!itemIds.Add(entry.ItemId))
            {
                error = $"{path}.current_inventory contains duplicate item_id '{entry.ItemId}'.";
                return false;
            }
            inventory.Add(entry);
        }
        if (!WorldMapSettlementStateData.TryReadLong(data, "seed", out long seed))
        {
            error = $"{path}.seed must be an int.";
            return false;
        }
        if (!WorldMapSettlementStateData.TryReadInt(data, "last_refresh_step", out int lastRefreshStep)
            || lastRefreshStep < 0)
        {
            error = $"{path}.last_refresh_step must be a non-negative int.";
            return false;
        }
        state = Create(shopId, inventory, seed, lastRefreshStep);
        if (state != null)
            return true;
        error = $"{path} violates the current typed shop-state contract.";
        return false;
    }
}

public sealed class WorldMapSettlementStateData
{
    private static readonly string[] RequiredKeys =
    {
        "visited",
        "reputation",
        "active_conditions",
        "cooldowns",
        "shop_states",
    };

    private readonly ReadOnlyCollection<string> _activeConditions;
    private readonly ReadOnlyDictionary<string, int> _cooldowns;
    private readonly ReadOnlyDictionary<string, SettlementShopStateData> _shopStates;

    public bool Visited { get; }
    public int Reputation { get; }
    public IReadOnlyList<string> ActiveConditions => _activeConditions;
    public IReadOnlyDictionary<string, int> Cooldowns => _cooldowns;
    public IReadOnlyDictionary<string, SettlementShopStateData> ShopStates => _shopStates;

    private WorldMapSettlementStateData(
        bool visited,
        int reputation,
        IEnumerable<string> activeConditions,
        IReadOnlyDictionary<string, int> cooldowns,
        IReadOnlyDictionary<string, SettlementShopStateData> shopStates
    )
    {
        Visited = visited;
        Reputation = reputation;
        _activeConditions = new List<string>(activeConditions ?? Array.Empty<string>()).AsReadOnly();
        _cooldowns = new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(cooldowns ?? new Dictionary<string, int>(), StringComparer.Ordinal)
        );
        _shopStates = new ReadOnlyDictionary<string, SettlementShopStateData>(
            new Dictionary<string, SettlementShopStateData>(
                shopStates ?? new Dictionary<string, SettlementShopStateData>(),
                StringComparer.Ordinal
            )
        );
    }

    public static WorldMapSettlementStateData Create(
        bool visited,
        int reputation,
        IEnumerable<string> activeConditions,
        IReadOnlyDictionary<string, int> cooldowns,
        IReadOnlyDictionary<string, SettlementShopStateData> shopStates
    )
    {
        var conditions = new List<string>();
        if (activeConditions != null)
        {
            foreach (string condition in activeConditions)
            {
                if (string.IsNullOrWhiteSpace(condition))
                    return null;
                conditions.Add(condition);
            }
        }
        var normalizedCooldowns = new Dictionary<string, int>(StringComparer.Ordinal);
        if (cooldowns != null)
        {
            foreach (KeyValuePair<string, int> entry in cooldowns)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value < 0)
                    return null;
                if (!normalizedCooldowns.TryAdd(entry.Key.Trim(), entry.Value))
                    return null;
            }
        }
        var normalizedShopStates = new Dictionary<string, SettlementShopStateData>(StringComparer.Ordinal);
        if (shopStates != null)
        {
            foreach (KeyValuePair<string, SettlementShopStateData> entry in shopStates)
            {
                if (string.IsNullOrWhiteSpace(entry.Key)
                    || entry.Value == null
                    || !string.Equals(entry.Key, entry.Value.ShopId, StringComparison.Ordinal))
                {
                    return null;
                }
                normalizedShopStates[entry.Key] = entry.Value;
            }
        }
        return new WorldMapSettlementStateData(
            visited,
            reputation,
            conditions,
            normalizedCooldowns,
            normalizedShopStates
        );
    }

    internal static WorldMapSettlementStateData CreateDefault(bool visited) =>
        Create(
            visited,
            0,
            Array.Empty<string>(),
            new Dictionary<string, int>(StringComparer.Ordinal),
            new Dictionary<string, SettlementShopStateData>(StringComparer.Ordinal)
        );

    public static WorldMapSettlementStateData FromDictionary(GDictionary data) =>
        TryFromDictionary(data, out WorldMapSettlementStateData state, out _) ? state : null;

    internal static bool TryFromDictionary(
        GDictionary data,
        out WorldMapSettlementStateData state,
        out string error
    )
    {
        state = null;
        error = "";
        Dictionary<string, object> normalized;
        try
        {
            normalized = RuntimePlainPayload.NormalizeDictionaryStrict(data, "settlement_state");
        }
        catch (InvalidOperationException exception)
        {
            error = exception.Message;
            return false;
        }
        return TryFromPlain(normalized, out state, out error);
    }

    internal static WorldMapSettlementStateData FromPlain(
        IReadOnlyDictionary<string, object> data
    ) => TryFromPlain(data, out WorldMapSettlementStateData state, out _) ? state : null;

    internal static bool TryFromPlain(
        IReadOnlyDictionary<string, object> data,
        out WorldMapSettlementStateData state,
        out string error
    )
    {
        state = null;
        error = "";
        if (!HasExactKeys(data, RequiredKeys))
        {
            error = "settlement_state fields must exactly match current schema.";
            return false;
        }
        if (!data.TryGetValue("visited", out object rawVisited) || rawVisited is not bool visited)
        {
            error = "settlement_state.visited must be a bool.";
            return false;
        }
        if (!TryReadInt(data, "reputation", out int reputation))
        {
            error = "settlement_state.reputation must be an int.";
            return false;
        }
        if (!TryReadObjectList(data, "active_conditions", out IReadOnlyList<object> rawConditions))
        {
            error = "settlement_state.active_conditions must be an Array.";
            return false;
        }
        var conditions = new List<string>();
        foreach (object rawCondition in rawConditions)
        {
            if (rawCondition is not string condition || string.IsNullOrWhiteSpace(condition))
            {
                error = "settlement_state.active_conditions entries must be non-empty Strings.";
                return false;
            }
            conditions.Add(condition);
        }
        if (!TryReadDictionary(data, "cooldowns", out IReadOnlyDictionary<string, object> rawCooldowns))
        {
            error = "settlement_state.cooldowns must be a Dictionary.";
            return false;
        }
        var cooldowns = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object> entry in rawCooldowns)
        {
            if (string.IsNullOrWhiteSpace(entry.Key)
                || !TryConvertInt(entry.Value, out int value)
                || value < 0)
            {
                error = "settlement_state.cooldowns must map non-empty String keys to non-negative ints.";
                return false;
            }
            if (!cooldowns.TryAdd(entry.Key.Trim(), value))
            {
                error = "settlement_state.cooldowns contains duplicate canonical keys.";
                return false;
            }
        }
        if (!TryReadDictionary(data, "shop_states", out IReadOnlyDictionary<string, object> rawShopStates))
        {
            error = "settlement_state.shop_states must be a Dictionary.";
            return false;
        }
        var shopStates = new Dictionary<string, SettlementShopStateData>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object> entry in rawShopStates)
        {
            if (string.IsNullOrWhiteSpace(entry.Key)
                || entry.Value is not IReadOnlyDictionary<string, object> shopStateData)
            {
                error = $"settlement_state.shop_states.{entry.Key} must be a Dictionary.";
                return false;
            }
            if (!SettlementShopStateData.TryFromPlain(
                    entry.Key,
                    shopStateData,
                    $"settlement_state.shop_states.{entry.Key}",
                    out SettlementShopStateData shopState,
                    out error
                ))
            {
                return false;
            }
            shopStates[entry.Key] = shopState;
        }
        state = Create(
            visited,
            reputation,
            conditions,
            cooldowns,
            shopStates
        );
        if (state != null)
            return true;
        error = "settlement_state violates the current typed state contract.";
        return false;
    }

    internal WorldMapSettlementStateData WithVisited(bool visited) =>
        new(
            visited,
            Reputation,
            _activeConditions,
            _cooldowns,
            _shopStates
        );

    internal WorldMapSettlementStateData WithShopState(SettlementShopStateData shopState)
    {
        if (shopState == null)
            return this;
        var shopStates = new Dictionary<string, SettlementShopStateData>(_shopStates, StringComparer.Ordinal)
        {
            [shopState.ShopId] = shopState,
        };
        return new WorldMapSettlementStateData(
            Visited,
            Reputation,
            _activeConditions,
            _cooldowns,
            shopStates
        );
    }

    internal SettlementShopStateData GetShopState(string shopId) =>
        !string.IsNullOrEmpty(shopId)
            && _shopStates.TryGetValue(shopId, out SettlementShopStateData state)
                ? state
                : null;

    internal Dictionary<string, object> BuildSnapshotPlain()
    {
        var conditions = new List<object>();
        foreach (string condition in _activeConditions)
            conditions.Add(condition);
        var cooldowns = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, int> entry in _cooldowns)
            cooldowns[entry.Key] = entry.Value;
        var shopStates = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, SettlementShopStateData> entry in _shopStates)
            shopStates[entry.Key] = entry.Value.BuildSnapshotPlain();
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["visited"] = Visited,
            ["reputation"] = Reputation,
            ["active_conditions"] = conditions,
            ["cooldowns"] = cooldowns,
            ["shop_states"] = shopStates,
        };
    }

    internal static bool HasExactKeys(
        IReadOnlyDictionary<string, object> data,
        params string[] requiredKeys
    )
    {
        if (data == null || data.Count != requiredKeys.Length)
            return false;
        foreach (string key in requiredKeys)
        {
            if (!data.ContainsKey(key))
                return false;
        }
        return true;
    }

    internal static bool TryReadDictionary(
        IReadOnlyDictionary<string, object> data,
        string key,
        out IReadOnlyDictionary<string, object> value
    )
    {
        if (data != null
            && data.TryGetValue(key, out object rawValue)
            && rawValue is IReadOnlyDictionary<string, object> dictionary)
        {
            value = dictionary;
            return true;
        }
        value = null;
        return false;
    }

    internal static bool TryReadObjectList(
        IReadOnlyDictionary<string, object> data,
        string key,
        out IReadOnlyList<object> value
    )
    {
        if (data != null
            && data.TryGetValue(key, out object rawValue)
            && rawValue is IReadOnlyList<object> list)
        {
            value = list;
            return true;
        }
        value = null;
        return false;
    }

    internal static bool TryReadNonEmptyString(
        IReadOnlyDictionary<string, object> data,
        string key,
        out string value
    )
    {
        if (data != null
            && data.TryGetValue(key, out object rawValue)
            && rawValue is string text
            && !string.IsNullOrWhiteSpace(text))
        {
            value = text.Trim();
            return true;
        }
        value = "";
        return false;
    }

    internal static bool TryReadInt(
        IReadOnlyDictionary<string, object> data,
        string key,
        out int value
    )
    {
        if (data != null
            && data.TryGetValue(key, out object rawValue)
            && TryConvertInt(rawValue, out value))
        {
            return true;
        }
        value = 0;
        return false;
    }

    internal static bool TryReadLong(
        IReadOnlyDictionary<string, object> data,
        string key,
        out long value
    )
    {
        if (data != null && data.TryGetValue(key, out object rawValue))
        {
            switch (rawValue)
            {
                case int intValue:
                    value = intValue;
                    return true;
                case long longValue:
                    value = longValue;
                    return true;
            }
        }
        value = 0L;
        return false;
    }

    private static bool TryConvertInt(object rawValue, out int value)
    {
        switch (rawValue)
        {
            case int intValue:
                value = intValue;
                return true;
            case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                value = (int)longValue;
                return true;
            default:
                value = 0;
                return false;
        }
    }
}
