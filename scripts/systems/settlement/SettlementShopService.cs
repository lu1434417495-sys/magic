using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;

[GlobalClass]
public partial class SettlementShopService : RefCounted
{
    private const int PriceBasisPointsDefault = 10000;
    private static readonly HashSet<string> ShopStockEntryKeys = new()
    {
        "item_id",
        "quantity",
        "unit_price",
        "sold_out",
    };

    private enum ShopItemId
    {
        HealingHerb,
        TravelRation,
        BandageRoll,
        TorchBundle,
        AntidoteHerb,
        IronOre,
        BeastHide,
        BronzeSword,
        MilitiaAxe,
        LeatherCap,
        LeatherJerkin,
        ScoutCharm,
        IronGreatsword,
        WatchmanMace,
        HardwoodLumber,
        LinenCloth,
    }

    private readonly record struct ShopItemSeed(
        ShopItemId ItemId,
        int MinQty,
        int MaxQty,
        int Weight = 0,
        int PriceBasisPoints = PriceBasisPointsDefault
    );

    private sealed record ShopDefinition(
        string InteractionScriptId,
        string ShopId,
        string Title,
        int RefreshIntervalSteps,
        ShopItemSeed[] GuaranteedItems,
        ShopItemSeed[] RandomPool,
        int MaxRandomItems
    );

    private sealed record ShopStockEntry(string ItemId, ItemDef ItemDef, int Quantity, int UnitPrice);
    private static readonly ShopDefinition[] ShopDefs =
    {
        new(
            "service_basic_supply",
            "village_basic_supply",
            "临时补给",
            12,
            new[]
            {
                new ShopItemSeed(ShopItemId.HealingHerb, 2, 4),
                new ShopItemSeed(ShopItemId.TravelRation, 2, 4),
            },
            new[]
            {
                new ShopItemSeed(ShopItemId.BandageRoll, 1, 3, 6),
                new ShopItemSeed(ShopItemId.TorchBundle, 1, 3, 5),
                new ShopItemSeed(ShopItemId.AntidoteHerb, 1, 2, 4, 11000),
                new ShopItemSeed(ShopItemId.IronOre, 1, 2, 2),
            },
            2
        ),
        new(
            "service_local_trade",
            "town_local_trade",
            "镇集交易",
            10,
            new[]
            {
                new ShopItemSeed(ShopItemId.HealingHerb, 3, 6, PriceBasisPoints: 9500),
                new ShopItemSeed(ShopItemId.BandageRoll, 2, 4),
                new ShopItemSeed(ShopItemId.TravelRation, 2, 5, PriceBasisPoints: 9500),
                new ShopItemSeed(ShopItemId.BeastHide, 2, 4),
                new ShopItemSeed(ShopItemId.BronzeSword, 1, 1),
                new ShopItemSeed(ShopItemId.MilitiaAxe, 1, 1),
                new ShopItemSeed(ShopItemId.LeatherCap, 1, 1),
                new ShopItemSeed(ShopItemId.LeatherJerkin, 1, 1),
            },
            new[]
            {
                new ShopItemSeed(ShopItemId.TorchBundle, 1, 3, 4),
                new ShopItemSeed(ShopItemId.AntidoteHerb, 1, 3, 4),
                new ShopItemSeed(ShopItemId.IronOre, 2, 4, 3),
                new ShopItemSeed(ShopItemId.ScoutCharm, 1, 1, 2, 11000),
                new ShopItemSeed(ShopItemId.IronGreatsword, 1, 1, 1, 11500),
            },
            4
        ),
        new(
            "service_city_market",
            "city_market",
            "城市市场",
            8,
            new[]
            {
                new ShopItemSeed(ShopItemId.BronzeSword, 1, 1, PriceBasisPoints: 9500),
                new ShopItemSeed(ShopItemId.MilitiaAxe, 1, 1),
                new ShopItemSeed(ShopItemId.WatchmanMace, 1, 1),
                new ShopItemSeed(ShopItemId.LeatherCap, 1, 1, PriceBasisPoints: 9500),
                new ShopItemSeed(ShopItemId.LeatherJerkin, 1, 1, PriceBasisPoints: 9500),
                new ShopItemSeed(ShopItemId.ScoutCharm, 1, 1),
                new ShopItemSeed(ShopItemId.IronGreatsword, 1, 1),
                new ShopItemSeed(ShopItemId.AntidoteHerb, 2, 4, PriceBasisPoints: 9500),
                new ShopItemSeed(ShopItemId.HardwoodLumber, 3, 6, PriceBasisPoints: 9500),
                new ShopItemSeed(ShopItemId.LinenCloth, 3, 6, PriceBasisPoints: 9500),
            },
            new[]
            {
                new ShopItemSeed(ShopItemId.BandageRoll, 2, 4, 5, 9500),
                new ShopItemSeed(ShopItemId.TravelRation, 2, 5, 4, 9000),
                new ShopItemSeed(ShopItemId.TorchBundle, 1, 3, 3, 9500),
                new ShopItemSeed(ShopItemId.IronOre, 3, 6, 2, 9500),
            },
            4
        ),
        new(
            "service_military_supply",
            "capital_military_supply",
            "军需总署",
            6,
            new[]
            {
                new ShopItemSeed(ShopItemId.IronGreatsword, 1, 1, PriceBasisPoints: 9500),
                new ShopItemSeed(ShopItemId.LeatherJerkin, 1, 1, PriceBasisPoints: 9000),
                new ShopItemSeed(ShopItemId.BandageRoll, 3, 5, PriceBasisPoints: 9000),
            },
            new[]
            {
                new ShopItemSeed(ShopItemId.BronzeSword, 1, 1, 2, 9000),
                new ShopItemSeed(ShopItemId.ScoutCharm, 1, 1, 3, 9500),
                new ShopItemSeed(ShopItemId.AntidoteHerb, 2, 4, 5, 9000),
            },
            3
        ),
        new(
            "service_grand_auction",
            "metropolis_grand_auction",
            "大拍卖行",
            5,
            new[]
            {
                new ShopItemSeed(ShopItemId.IronGreatsword, 1, 1, PriceBasisPoints: 11000),
                new ShopItemSeed(ShopItemId.ScoutCharm, 1, 1, PriceBasisPoints: 10500),
            },
            new[]
            {
                new ShopItemSeed(ShopItemId.BronzeSword, 1, 1, 1),
                new ShopItemSeed(ShopItemId.LeatherJerkin, 1, 1, 1),
                new ShopItemSeed(ShopItemId.AntidoteHerb, 2, 4, 3),
                new ShopItemSeed(ShopItemId.TorchBundle, 2, 4, 2),
            },
            4
        ),
    };

    private readonly RandomNumberGenerator _rng = new();

    public GDictionary build_window_data(
        string interactionScriptId,
        GDictionary settlementRecord,
        GDictionary settlementState,
        GDictionary itemDefs,
        GodotObject warehouseService,
        int currentGold)
    {
        return BuildWindowDataTyped(
            interactionScriptId,
            settlementRecord,
            settlementState,
            itemDefs,
            warehouseService as PartyWarehouseService,
            currentGold
        );
    }

    public GDictionary BuildWindowDataTyped(
        string interactionScriptId,
        GDictionary settlementRecord,
        GDictionary settlementState,
        GDictionary itemDefs,
        PartyWarehouseService warehouse,
        int currentGold)
    {
        ShopDefinition shopDef = ResolveShopDef(interactionScriptId);
        if (shopDef == null)
        {
            return new GDictionary();
        }

        int worldStep = GetInt(settlementState, "world_step");
        GDictionary shopState = GetOrRefreshShopState(shopDef, settlementState, itemDefs, worldStep);
        var buyEntries = new GDictionaryArray();
        foreach (GDictionary inventoryEntry in GetShopCurrentInventory(shopState))
        {
            ShopStockEntry stockEntry = ParseShopStockEntry(inventoryEntry, itemDefs);
            if (stockEntry == null)
            {
                continue;
            }

            bool canBuy = stockEntry.Quantity > 0 && currentGold >= stockEntry.UnitPrice;
            string stockText = stockEntry.Quantity <= 0 ? "售罄" : $"库存 {stockEntry.Quantity}";
            string description = stockEntry.ItemDef?.description ?? "";
            buyEntries.Add(new GDictionary
            {
                { "item_id", stockEntry.ItemId },
                { "entry_id", $"buy:{stockEntry.ItemId}" },
                { "display_name", GetItemDisplayName(stockEntry.ItemDef, stockEntry.ItemId) },
                { "description", description },
                { "icon", stockEntry.ItemDef?.icon ?? "" },
                { "quantity", stockEntry.Quantity },
                { "unit_price", stockEntry.UnitPrice },
                { "stock_text", stockText },
                { "can_buy", canBuy },
                { "state_label", canBuy ? "状态：可购" : "状态：不可购" },
                { "cost_label", $"单价 {stockEntry.UnitPrice} 金" },
                { "summary_text", stockText },
                { "details_text", description },
                { "is_enabled", canBuy },
                { "disabled_reason", canBuy ? "" : stockEntry.Quantity <= 0 ? "库存不足" : "金币不足" },
                { "shop_action", "buy" },
            });
        }

        var sellEntries = new GDictionaryArray();
        if (warehouse != null)
        {
            foreach (WarehouseInventoryEntry entryData in warehouse.GetInventoryEntriesTyped())
            {
                ItemDef itemDef = entryData.ItemDef ?? GetItemDef(itemDefs, entryData.ItemId.ToString());
                if (itemDef == null || !itemDef.sellable)
                {
                    continue;
                }

                int unitPrice = ResolveSellPrice(itemDef);
                if (unitPrice <= 0)
                {
                    continue;
                }

                string itemId = entryData.ItemId.ToString();
                string instanceId = entryData.InstanceId.ToString();
                int totalQuantity = entryData.HasEquipmentInstance ? 1 : entryData.TotalQuantity;
                string stockText = BuildSellStockText(totalQuantity, instanceId);
                sellEntries.Add(new GDictionary
                {
                    { "item_id", itemId },
                    { "entry_id", !string.IsNullOrEmpty(instanceId) ? $"sell:{itemId}:{instanceId}" : $"sell:{itemId}" },
                    { "instance_id", instanceId },
                    { "display_name", GetItemDisplayName(itemDef, itemId) },
                    { "description", itemDef.description },
                    { "icon", itemDef.icon },
                    { "quantity", totalQuantity },
                    { "unit_price", unitPrice },
                    { "stock_text", stockText },
                    { "can_sell", true },
                    { "state_label", "状态：可售" },
                    { "cost_label", $"回收 {unitPrice} 金" },
                    { "summary_text", stockText },
                    { "details_text", itemDef.description },
                    { "is_enabled", true },
                    { "disabled_reason", "" },
                    { "shop_action", "sell" },
                });
            }
        }

        SortSellEntries(sellEntries);
        string displayName = GetString(settlementRecord, "display_name", "据点");
        int gold = Mathf.Max(currentGold, 0);
        return new GDictionary
        {
            { "title", $"{displayName} · {shopDef.Title}" },
            { "meta", $"商店：{shopDef.Title}  |  金币：{gold}" },
            { "shop_id", shopDef.ShopId },
            { "interaction_script_id", interactionScriptId },
            { "settlement_id", GetString(settlementRecord, "settlement_id") },
            { "panel_kind", "shop" },
            { "gold", gold },
            { "buy_entries", buyEntries },
            { "sell_entries", sellEntries },
            { "feedback_text", GetString(settlementState, "shop_feedback_text") },
            { "confirm_label", "确认交易" },
            { "cancel_label", "返回据点" },
            { "show_member_selector", true },
            { "entry_title", "交易条目" },
            { "summary_title", "交易概况" },
            { "state_title", "交易状态" },
            { "cost_title", "交易费用" },
            { "details_title", "交易说明" },
            { "member_title", "交易成员" },
            { "empty_state_label", "状态：暂无商品" },
            { "empty_cost_label", "费用：暂无商品" },
            { "empty_details_text", "当前没有可交易条目。" },
        };
    }

    public GDictionary buy(
        string interactionScriptId,
        GDictionary settlementRecord,
        GDictionary settlementState,
        GDictionary itemDefs,
        GodotObject warehouseService,
        GodotObject partyState,
        StringName itemId,
        int quantity,
        StringName instanceId = default)
    {
        return BuyTyped(
            interactionScriptId,
            settlementRecord,
            settlementState,
            itemDefs,
            warehouseService as PartyWarehouseService,
            partyState as PartyState,
            itemId,
            quantity,
            instanceId
        ).ToDictionary();
    }

    public SettlementShopTradeResult BuyTyped(
        string interactionScriptId,
        GDictionary settlementRecord,
        GDictionary settlementState,
        GDictionary itemDefs,
        PartyWarehouseService warehouse,
        PartyState party,
        StringName itemId,
        int quantity,
        StringName instanceId = default)
    {
        ShopDefinition shopDef = ResolveShopDef(interactionScriptId);
        if (shopDef == null)
        {
            return BuildFail("当前据点没有可交易的商店。");
        }
        if (warehouse == null || party == null)
        {
            return BuildFail("商店服务尚未准备完成。");
        }

        int requestedQuantity = Mathf.Max(quantity, 0);
        if (requestedQuantity <= 0)
        {
            return BuildFail("购买数量必须大于 0。");
        }

        int worldStep = GetInt(settlementState, "world_step");
        GDictionary shopState = GetOrRefreshShopState(shopDef, settlementState, itemDefs, worldStep);
        string normalizedItemId = NormalizeId(itemId);
        ShopStockEntry stockEntry = FindInventoryEntry(shopState, normalizedItemId, itemDefs);
        if (stockEntry == null)
        {
            return BuildFail("当前商店没有该商品。");
        }
        if (stockEntry.Quantity <= 0)
        {
            return BuildFail("该商品当前已售罄。");
        }

        int actualQuantity = Mathf.Min(requestedQuantity, stockEntry.Quantity);
        int totalCost = stockEntry.UnitPrice * actualQuantity;
        if (!party.can_afford(totalCost))
        {
            return BuildFail($"金币不足，无法购买 {normalizedItemId}。");
        }

        var itemIdName = new StringName(normalizedItemId);
        GDictionary preview = warehouse.preview_add_item(itemIdName, actualQuantity);
        if (GetInt(preview, "remaining_quantity") > 0)
        {
            return BuildFail("共享仓库空间不足，无法购买该商品。");
        }

        GDictionary addResult = warehouse.add_item(itemIdName, actualQuantity);
        int addedQuantity = GetInt(addResult, "added_quantity");
        if (addedQuantity <= 0)
        {
            return BuildFail("当前无法将商品放入共享仓库。");
        }

        int spendCost = stockEntry.UnitPrice * addedQuantity;
        party.spend_gold(spendCost);

        ConsumeShopStock(shopState, normalizedItemId, addedQuantity, itemDefs);
        string feedback = $"购入 {addedQuantity} 件 {normalizedItemId}，花费 {spendCost} 金。";
        settlementState["shop_feedback_text"] = feedback;
        return new SettlementShopTradeResult(
            true,
            feedback,
            -spendCost,
            normalizedItemId,
            addedQuantity
        );
    }

    public GDictionary sell(
        string interactionScriptId,
        GDictionary settlementRecord,
        GDictionary settlementState,
        GDictionary itemDefs,
        GodotObject warehouseService,
        GodotObject partyState,
        StringName itemId,
        int quantity,
        StringName instanceId = default)
    {
        return SellTyped(
            interactionScriptId,
            settlementRecord,
            settlementState,
            itemDefs,
            warehouseService as PartyWarehouseService,
            partyState as PartyState,
            itemId,
            quantity,
            instanceId
        ).ToDictionary();
    }

    public SettlementShopTradeResult SellTyped(
        string interactionScriptId,
        GDictionary settlementRecord,
        GDictionary settlementState,
        GDictionary itemDefs,
        PartyWarehouseService warehouse,
        PartyState party,
        StringName itemId,
        int quantity,
        StringName instanceId = default)
    {
        ShopDefinition shopDef = ResolveShopDef(interactionScriptId);
        if (shopDef == null)
        {
            return BuildFail("当前据点没有可交易的商店。");
        }
        if (warehouse == null || party == null)
        {
            return BuildFail("商店服务尚未准备完成。");
        }

        int requestedQuantity = Mathf.Max(quantity, 0);
        if (requestedQuantity <= 0)
        {
            return BuildFail("出售数量必须大于 0。");
        }

        string normalizedItemId = NormalizeId(itemId);
        string normalizedInstanceId = NormalizeId(instanceId);
        ItemDef itemDef = GetItemDef(itemDefs, normalizedItemId);
        if (itemDef == null)
        {
            return BuildFail("未找到该物品的定义。");
        }
        if (!itemDef.sellable)
        {
            return BuildFail($"{GetItemDisplayName(itemDef, normalizedItemId)} 当前不能出售。");
        }

        int unitPrice = ResolveSellPrice(itemDef);
        if (unitPrice <= 0)
        {
            return BuildFail($"{GetItemDisplayName(itemDef, normalizedItemId)} 当前没有有效回收价格。");
        }

        var itemIdName = new StringName(normalizedItemId);
        var instanceIdName = new StringName(normalizedInstanceId);
        int ownedQuantity = warehouse.count_item(itemIdName);
        if (ownedQuantity <= 0)
        {
            return BuildFail("共享仓库中没有该物品。");
        }

        int actualQuantity = Mathf.Min(requestedQuantity, ownedQuantity);
        GDictionary removeResult;
        if (itemDef.is_equipment())
        {
            if (string.IsNullOrEmpty(normalizedInstanceId) && ownedQuantity > 1)
            {
                return BuildFail($"请选择要出售的 {GetItemDisplayName(itemDef, normalizedItemId)} 装备实例。");
            }

            actualQuantity = 1;
            removeResult = !string.IsNullOrEmpty(normalizedInstanceId)
                ? warehouse.remove_equipment_instance(itemIdName, instanceIdName)
                : warehouse.remove_item(itemIdName, 1);
        }
        else
        {
            removeResult = warehouse.remove_item(itemIdName, actualQuantity);
        }

        int removedQuantity = GetInt(removeResult, "removed_quantity");
        if (removedQuantity <= 0)
        {
            return BuildFail(BuildSellRemoveFailureMessage(itemDef, normalizedItemId, removeResult));
        }

        int totalGain = unitPrice * removedQuantity;
        party.add_gold(totalGain);

        string feedback = $"售出 {removedQuantity} 件 {GetItemDisplayName(itemDef, normalizedItemId)}，获得 {totalGain} 金。";
        settlementState["shop_feedback_text"] = feedback;
        return new SettlementShopTradeResult(
            true,
            feedback,
            totalGain,
            normalizedItemId,
            removedQuantity,
            normalizedInstanceId
        );
    }

    public GDictionary get_shop_def(string interactionScriptId)
    {
        ShopDefinition shopDef = ResolveShopDef(interactionScriptId);
        return shopDef != null ? ToShopDefDictionary(shopDef) : new GDictionary();
    }

    private GDictionary GetOrRefreshShopState(
        ShopDefinition shopDef,
        GDictionary settlementState,
        GDictionary itemDefs,
        int currentWorldStep)
    {
        GDictionary shopStates = GetDictionary(settlementState, "shop_states");
        GDictionary shopState = TryGetValue(shopStates, shopDef.ShopId, out object storedStateValue)
            && TryAsDictionary(storedStateValue, out GDictionary storedState)
            ? storedState.Duplicate(true)
            : new GDictionary();

        int refreshInterval = Mathf.Max(shopDef.RefreshIntervalSteps, 0);
        int lastRefreshStep = GetInt(shopState, "last_refresh_step", -refreshInterval);
        bool needsRefresh = shopState.Count == 0
            || refreshInterval > 0 && currentWorldStep - lastRefreshStep >= refreshInterval;
        if (needsRefresh)
        {
            shopState = GenerateShopState(shopDef, itemDefs, currentWorldStep);
            shopStates[shopDef.ShopId] = shopState.Duplicate(true);
            settlementState["shop_states"] = shopStates;
        }

        settlementState["shop_last_refresh_step"] = GetInt(shopState, "last_refresh_step");
        return shopState;
    }

    private GDictionary GenerateShopState(ShopDefinition shopDef, GDictionary itemDefs, int currentWorldStep)
    {
        ulong seed = (ulong)TrueRandomSeedService.GenerateSeed();
        _rng.Seed = seed;
        var inventory = new GDictionaryArray();
        foreach (ShopItemSeed source in shopDef.GuaranteedItems)
        {
            GDictionary built = BuildShopEntry(source, itemDefs);
            if (built.Count > 0)
            {
                inventory.Add(built);
            }
        }

        var randomPool = new List<ShopItemSeed>(shopDef.RandomPool);
        for (int i = 0; i < shopDef.MaxRandomItems; i++)
        {
            ShopItemSeed? picked = PickWeightedRandomEntry(randomPool, _rng);
            if (!picked.HasValue)
            {
                break;
            }

            GDictionary built = BuildShopEntry(picked.Value, itemDefs);
            if (built.Count > 0)
            {
                MergeShopEntry(inventory, built);
            }
        }

        return new GDictionary
        {
            { "shop_id", shopDef.ShopId },
            { "current_inventory", inventory },
            { "seed", (long)seed },
            { "last_refresh_step", currentWorldStep },
        };
    }

    private GDictionary BuildShopEntry(ShopItemSeed source, GDictionary itemDefs)
    {
        string itemId = ToItemIdString(source.ItemId);
        ItemDef itemDef = GetItemDef(itemDefs, itemId);
        if (string.IsNullOrEmpty(itemId) || itemDef == null)
        {
            return new GDictionary();
        }

        int minQty = Mathf.Max(source.MinQty, 1);
        int maxQty = Mathf.Max(source.MaxQty, minQty);
        int quantity = _rng.RandiRange(minQty, maxQty);
        int unitPrice = ResolveBuyPrice(itemDef, source.PriceBasisPoints);
        if (unitPrice <= 0)
        {
            return new GDictionary();
        }

        return new GDictionary
        {
            { "item_id", itemId },
            { "quantity", quantity },
            { "unit_price", unitPrice },
            { "sold_out", false },
        };
    }

    private static void MergeShopEntry(GDictionaryArray inventory, GDictionary builtEntry)
    {
        string itemId = GetString(builtEntry, "item_id");
        for (int i = 0; i < inventory.Count; i++)
        {
            GDictionary existing = inventory[i];
            if (GetString(existing, "item_id") != itemId)
            {
                continue;
            }

            existing["quantity"] = GetInt(existing, "quantity") + GetInt(builtEntry, "quantity");
            inventory[i] = existing;
            return;
        }
        inventory.Add(builtEntry);
    }

    private static ShopItemSeed? PickWeightedRandomEntry(List<ShopItemSeed> pool, RandomNumberGenerator rng)
    {
        int totalWeight = 0;
        foreach (ShopItemSeed entry in pool)
        {
            totalWeight += Mathf.Max(entry.Weight, 0);
        }
        if (totalWeight <= 0)
        {
            return null;
        }

        int roll = rng.RandiRange(1, totalWeight);
        int cursor = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            ShopItemSeed entry = pool[i];
            cursor += Mathf.Max(entry.Weight, 0);
            if (roll > cursor)
            {
                continue;
            }

            pool.RemoveAt(i);
            return entry;
        }
        return null;
    }

    private static int ResolveBuyPrice(ItemDef itemDef, int priceBasisPoints)
    {
        if (itemDef == null)
        {
            return 0;
        }
        int price = itemDef.get_buy_price(priceBasisPoints);
        return price > 0 ? price : 0;
    }

    private static int ResolveSellPrice(ItemDef itemDef)
    {
        if (itemDef == null)
        {
            return 0;
        }
        int price = itemDef.get_sell_price();
        return price > 0 ? price : 0;
    }

    private static ShopStockEntry FindInventoryEntry(GDictionary shopState, string itemId, GDictionary itemDefs)
    {
        foreach (GDictionary inventoryEntry in GetShopCurrentInventory(shopState))
        {
            ShopStockEntry stockEntry = ParseShopStockEntry(inventoryEntry, itemDefs);
            if (stockEntry != null && stockEntry.ItemId == itemId)
            {
                return stockEntry;
            }
        }
        return null;
    }

    private static void ConsumeShopStock(GDictionary shopState, string itemId, int quantity, GDictionary itemDefs)
    {
        GDictionaryArray inventory = GetShopCurrentInventory(shopState);
        for (int i = 0; i < inventory.Count; i++)
        {
            ShopStockEntry stockEntry = ParseShopStockEntry(inventory[i], itemDefs);
            if (stockEntry == null || stockEntry.ItemId != itemId)
            {
                continue;
            }

            GDictionary entryData = inventory[i];
            int remaining = stockEntry.Quantity - Mathf.Max(quantity, 0);
            if (remaining <= 0)
            {
                inventory.RemoveAt(i);
            }
            else
            {
                entryData["quantity"] = remaining;
                entryData["sold_out"] = false;
                inventory[i] = entryData;
            }
            break;
        }
        shopState["current_inventory"] = inventory;
    }

    private static GDictionaryArray GetShopCurrentInventory(GDictionary shopState)
    {
        if (shopState == null || !shopState.ContainsKey("current_inventory"))
        {
            return new GDictionaryArray();
        }

        if (!TryGetValue(shopState, "current_inventory", out object inventoryValue)
            || !TryAsArray(inventoryValue, out GArray inventoryEntries))
        {
            return new GDictionaryArray();
        }

        var result = new GDictionaryArray();
        foreach (GDictionary entry in Dictionaries(inventoryEntries))
        {
            result.Add(entry);
        }
        return result;
    }

    private static ShopStockEntry ParseShopStockEntry(GDictionary entryData, GDictionary itemDefs)
    {
        if (!TryGetStrictInt(entryData, "quantity", out int quantity)
            || !TryGetStrictInt(entryData, "unit_price", out int unitPrice))
        {
            return null;
        }
        if (!HasOnlyAllowedKeys(entryData, ShopStockEntryKeys)
            || !HasNonEmptyId(entryData, "item_id")
            || quantity <= 0
            || unitPrice <= 0)
        {
            return null;
        }
        if (TryGetValue(entryData, "sold_out", out object soldOutValue))
        {
            if (!TryAsBool(soldOutValue, out bool soldOut))
            {
                return null;
            }
            if (soldOut)
            {
                return null;
            }
        }

        string itemId = NormalizeId(entryData, "item_id");
        ItemDef itemDef = GetItemDef(itemDefs, itemId);
        return itemDef != null
            ? new ShopStockEntry(itemId, itemDef, quantity, unitPrice)
            : null;
    }

    private static string BuildSellStockText(int totalQuantity, string instanceId)
    {
        return !string.IsNullOrEmpty(instanceId) ? $"持有 1 · 实例 {instanceId}" : $"持有 {totalQuantity}";
    }

    private static string BuildSellRemoveFailureMessage(ItemDef itemDef, string itemId, GDictionary removeResult)
    {
        string itemName = GetItemDisplayName(itemDef, itemId);
        return GetString(removeResult, "error_code") switch
        {
            "equipment_instance_id_required" => $"请选择要出售的 {itemName} 装备实例。",
            "warehouse_missing_instance" => $"共享仓库中没有指定的 {itemName} 装备实例。",
            "equipment_instance_item_mismatch" => $"指定装备实例不属于 {itemName}。",
            _ => "当前无法出售该物品。",
        };
    }

    private static bool HasOnlyAllowedKeys(GDictionary data, HashSet<string> allowedKeys)
    {
        foreach (object key in data.Keys)
        {
            if (!TryAsIdString(key, out string keyText))
            {
                return false;
            }
            if (!allowedKeys.Contains(keyText))
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasNonEmptyId(GDictionary data, string fieldName)
    {
        return !string.IsNullOrEmpty(NormalizeId(data, fieldName));
    }

    private static void SortSellEntries(GDictionaryArray entries)
    {
        for (int i = 0; i < entries.Count - 1; i++)
        {
            for (int j = i + 1; j < entries.Count; j++)
            {
                string leftId = GetString(entries[i], "item_id");
                string rightId = GetString(entries[j], "item_id");
                if (string.Compare(leftId, rightId, StringComparison.Ordinal) <= 0)
                {
                    continue;
                }
                (entries[i], entries[j]) = (entries[j], entries[i]);
            }
        }
    }

    private static ShopDefinition ResolveShopDef(string interactionScriptId)
    {
        foreach (ShopDefinition shopDef in ShopDefs)
        {
            if (shopDef.InteractionScriptId == interactionScriptId)
            {
                return shopDef;
            }
        }
        return null;
    }

    private static GDictionary ToShopDefDictionary(ShopDefinition shopDef)
    {
        return new GDictionary
        {
            { "shop_id", shopDef.ShopId },
            { "title", shopDef.Title },
            { "refresh_interval_steps", shopDef.RefreshIntervalSteps },
            { "guaranteed_items", ToSeedArray(shopDef.GuaranteedItems, false) },
            { "random_pool", ToSeedArray(shopDef.RandomPool, true) },
            { "max_random_items", shopDef.MaxRandomItems },
        };
    }

    private static GArray ToSeedArray(IEnumerable<ShopItemSeed> seeds, bool includeWeight)
    {
        var result = new GArray();
        foreach (ShopItemSeed seed in seeds)
        {
            var data = new GDictionary
            {
                { "item_id", ToItemIdString(seed.ItemId) },
                { "min_qty", seed.MinQty },
                { "max_qty", seed.MaxQty },
            };
            if (includeWeight)
            {
                data["weight"] = seed.Weight;
            }
            if (seed.PriceBasisPoints != PriceBasisPointsDefault)
            {
                data["price_basis_points"] = seed.PriceBasisPoints;
            }
            result.Add(data);
        }
        return result;
    }

    private static ItemDef GetItemDef(GDictionary itemDefs, string itemId)
    {
        if (itemDefs == null || string.IsNullOrEmpty(itemId))
        {
            return null;
        }
        return TryGetValue(itemDefs, new StringName(itemId), out object itemDefValue)
            && TryAsObject(itemDefValue, out ItemDef itemDef)
            ? itemDef
            : null;
    }

    private static string GetItemDisplayName(ItemDef itemDef, string itemId)
    {
        return itemDef != null && itemDef.display_name.Length > 0 ? itemDef.display_name : itemId;
    }

    private static string ToItemIdString(ShopItemId itemId)
    {
        return itemId switch
        {
            ShopItemId.HealingHerb => "healing_herb",
            ShopItemId.TravelRation => "travel_ration",
            ShopItemId.BandageRoll => "bandage_roll",
            ShopItemId.TorchBundle => "torch_bundle",
            ShopItemId.AntidoteHerb => "antidote_herb",
            ShopItemId.IronOre => "iron_ore",
            ShopItemId.BeastHide => "beast_hide",
            ShopItemId.BronzeSword => "bronze_sword",
            ShopItemId.MilitiaAxe => "militia_axe",
            ShopItemId.LeatherCap => "leather_cap",
            ShopItemId.LeatherJerkin => "leather_jerkin",
            ShopItemId.ScoutCharm => "scout_charm",
            ShopItemId.IronGreatsword => "iron_greatsword",
            ShopItemId.WatchmanMace => "watchman_mace",
            ShopItemId.HardwoodLumber => "hardwood_lumber",
            ShopItemId.LinenCloth => "linen_cloth",
            _ => "",
        };
    }

    private static string NormalizeId(StringName value)
    {
        return value == null ? "" : value.ToString().StripEdges();
    }

    private static string NormalizeId(GDictionary data, string key)
    {
        return TryGetValue(data, key, out object value)
            && TryAsIdString(value, out string id)
            ? id
            : "";
    }

    private static GDictionary GetDictionary(GDictionary dictionary, string key)
    {
        return TryGetValue(dictionary, key, out object value)
            && TryAsDictionary(value, out GDictionary result)
            ? result
            : new GDictionary();
    }

    private static string GetString(GDictionary dictionary, string key, string fallback = "")
    {
        if (!TryGetValue(dictionary, key, out object value) || IsNil(value))
            return fallback;
        return value.ToString();
    }

    private static int GetInt(GDictionary dictionary, string key, int fallback = 0)
    {
        if (!TryGetValue(dictionary, key, out object value))
            return fallback;
        return TryAsInt(value, out int result) ? result : fallback;
    }

    private static IEnumerable<GDictionary> Dictionaries(GArray values)
    {
        if (values == null)
        {
            yield break;
        }
        foreach (object rawValue in values)
        {
            if (TryAsDictionary(rawValue, out GDictionary value))
            {
                yield return value;
            }
        }
    }

    private static bool TryGetStrictInt(GDictionary data, string key, out int value)
    {
        if (TryGetValue(data, key, out object rawValue) && TryAsStrictInt(rawValue, out value))
        {
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryAsArray(object rawValue, out GArray value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Array)
        {
            value = variant.AsGodotArray();
            return true;
        }
        if (rawValue is GArray array)
        {
            value = array;
            return true;
        }
        value = new GArray();
        return false;
    }

    private static bool TryAsDictionary(object rawValue, out GDictionary value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            value = variant.AsGodotDictionary();
            return true;
        }
        if (rawValue is GDictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static bool TryAsObject<T>(object rawValue, out T value)
        where T : GodotObject
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Object)
        {
            value = variant.AsGodotObject() as T;
            return value != null;
        }
        if (rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryAsStrictInt(object rawValue, out int value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Int)
        {
            value = variant.AsInt32();
            return true;
        }
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryAsInt(object rawValue, out int value)
    {
        if (rawValue is Variant variant)
        {
            if (variant.VariantType == Variant.Type.Nil)
            {
                value = 0;
                return false;
            }
            value = variant.AsInt32();
            return true;
        }
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        if (rawValue is long longValue)
        {
            value = (int)longValue;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryAsBool(object rawValue, out bool value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Bool)
        {
            value = variant.AsBool();
            return true;
        }
        if (rawValue is bool boolValue)
        {
            value = boolValue;
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryAsIdString(object rawValue, out string value)
    {
        if (rawValue is Variant variant)
        {
            if (variant.VariantType == Variant.Type.String)
            {
                value = variant.AsString().StripEdges();
                return true;
            }
            if (variant.VariantType == Variant.Type.StringName)
            {
                value = variant.AsStringName().ToString().StripEdges();
                return true;
            }
            value = "";
            return false;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue.StripEdges();
            return true;
        }
        if (rawValue is StringName stringNameValue)
        {
            value = stringNameValue.ToString().StripEdges();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetValue(GDictionary data, string key, out object value)
    {
        if (data != null && data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        var stringNameKey = new StringName(key);
        if (data != null && data.ContainsKey(stringNameKey))
        {
            value = data[stringNameKey];
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryGetValue(GDictionary data, StringName key, out object value)
    {
        if (data != null && data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        string stringKey = key.ToString();
        if (data != null && data.ContainsKey(stringKey))
        {
            value = data[stringKey];
            return true;
        }
        value = null;
        return false;
    }

    private static bool IsNil(object rawValue)
    {
        return rawValue == null
            || rawValue is Variant variant && variant.VariantType == Variant.Type.Nil;
    }

    private static SettlementShopTradeResult BuildFail(string message) => new(false, message);
}
