using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;

public sealed class SettlementShopService : IDisposable
{
    private const int PriceBasisPointsDefault = 10000;
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

    private sealed record ShopStateResolution(
        WorldMapSettlementStateData SettlementState,
        SettlementShopStateData ShopState,
        bool StateChanged
    );
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

    private readonly RuntimeRandom _rng = new();

    public void Dispose()
    {
        System.GC.SuppressFinalize(this);
    }

    public SettlementShopWindowBuildResult BuildWindowDataTyped(
        string interactionScriptId,
        GDictionary settlementRecord,
        WorldMapSettlementStateData settlementState,
        int currentWorldStep,
        string feedbackText,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        PartyWarehouseService warehouse,
        int currentGold,
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs = null)
    {
        ShopDefinition shopDef = ResolveShopDef(interactionScriptId);
        if (shopDef == null || settlementState == null)
        {
            return new SettlementShopWindowBuildResult(
                new GDictionary(),
                settlementState,
                false
            );
        }

        ShopStateResolution resolution = GetOrRefreshShopState(
            shopDef,
            settlementState,
            itemDefs,
            currentWorldStep
        );
        SettlementShopStateData shopState = resolution.ShopState;
        var buyEntries = new GDictionaryArray();
        foreach (SettlementShopStockEntryData stockEntry in shopState.CurrentInventory)
        {
            ItemDefinition itemDef = GetItemDef(itemDefs, stockEntry.ItemId);
            if (itemDef == null)
            {
                continue;
            }

            bool canBuy = stockEntry.Quantity > 0 && currentGold >= stockEntry.UnitPrice;
            string stockText = stockEntry.Quantity <= 0 ? "售罄" : $"库存 {stockEntry.Quantity}";
            string description = itemDef.Description;
            buyEntries.Add(new GDictionary
            {
                { "item_id", stockEntry.ItemId },
                { "entry_id", $"buy:{stockEntry.ItemId}" },
                { "display_name", GetItemDisplayName(itemDef, stockEntry.ItemId) },
                { "description", description },
                { "icon", itemDef.Icon },
                { "quantity", stockEntry.Quantity },
                { "unit_price", stockEntry.UnitPrice },
                { "stock_text", stockText },
                { "can_buy", canBuy },
                { "state_label", canBuy ? "状态：可购" : "状态：不可购" },
                { "cost_label", $"单价 {stockEntry.UnitPrice} 金" },
                { "summary_text", stockText },
                { "details_text", ItemTraitDetailText.Compose(description, itemDef, traitDefs) },
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
                ItemDefinition itemDef = entryData.ItemDefinition
                    ?? GetItemDef(itemDefs, entryData.ItemId.ToString());
                if (itemDef == null || !itemDef.Sellable)
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
                    { "description", itemDef.Description },
                    { "icon", itemDef.Icon },
                    { "quantity", totalQuantity },
                    { "unit_price", unitPrice },
                    { "stock_text", stockText },
                    { "can_sell", true },
                    { "state_label", "状态：可售" },
                    { "cost_label", $"回收 {unitPrice} 金" },
                    { "summary_text", stockText },
                    { "details_text", ItemTraitDetailText.Compose(itemDef.Description, itemDef, traitDefs) },
                    { "is_enabled", true },
                    { "disabled_reason", "" },
                    { "shop_action", "sell" },
                });
            }
        }

        SortSellEntries(sellEntries);
        string displayName = GetString(settlementRecord, "display_name", "据点");
        int gold = Mathf.Max(currentGold, 0);
        return new SettlementShopWindowBuildResult(
            new GDictionary
            {
                { "title", $"{displayName} · {shopDef.Title}" },
                { "meta", $"商店：{shopDef.Title}  |  金币：{gold}" },
                { "shop_id", shopDef.ShopId },
                { "interaction_script_id", interactionScriptId },
                { "settlement_id", GetString(settlementRecord, "settlement_id") },
                { "panel_kind", SettlementPanelKinds.ToPayloadValue(SettlementPanelKind.Shop) },
                { "gold", gold },
                { "buy_entries", buyEntries },
                { "sell_entries", sellEntries },
                { "feedback_text", feedbackText ?? "" },
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
            },
            resolution.SettlementState,
            resolution.StateChanged
        );
    }

    public SettlementShopTradeResult BuyTyped(
        string interactionScriptId,
        WorldMapSettlementStateData settlementState,
        int currentWorldStep,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        PartyWarehouseService warehouse,
        PartyState party,
        StringName itemId,
        int quantity)
    {
        ShopDefinition shopDef = ResolveShopDef(interactionScriptId);
        if (shopDef == null)
        {
            return BuildFail("当前据点没有可交易的商店。");
        }
        if (settlementState == null || warehouse == null || party == null)
        {
            return BuildFail("商店服务尚未准备完成。");
        }

        int requestedQuantity = Mathf.Max(quantity, 0);
        if (requestedQuantity <= 0)
        {
            return BuildFail("购买数量必须大于 0。");
        }

        ShopStateResolution resolution = GetOrRefreshShopState(
            shopDef,
            settlementState,
            itemDefs,
            currentWorldStep
        );
        SettlementShopStateData shopState = resolution.ShopState;
        string normalizedItemId = NormalizeId(itemId);
        SettlementShopStockEntryData stockEntry = FindInventoryEntry(
            shopState,
            normalizedItemId
        );
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
        if (!party.CanAfford(totalCost))
        {
            return BuildFail($"金币不足，无法购买 {normalizedItemId}。");
        }

        var itemIdName = new StringName(normalizedItemId);
        var preview = warehouse.PreviewAddItemTyped(itemIdName, actualQuantity);
        if (preview.RemainingQuantity > 0)
        {
            return BuildFail("共享仓库空间不足，无法购买该商品。");
        }

        var addResult = warehouse.AddItemTyped(itemIdName, actualQuantity);
        int addedQuantity = addResult.AddedQuantity;
        if (addedQuantity <= 0)
        {
            return BuildFail("当前无法将商品放入共享仓库。");
        }

        int spendCost = stockEntry.UnitPrice * addedQuantity;
        party.SpendGold(spendCost);

        SettlementShopStateData nextShopState = ConsumeShopStock(
            shopState,
            normalizedItemId,
            addedQuantity
        );
        WorldMapSettlementStateData nextSettlementState =
            resolution.SettlementState.WithShopState(nextShopState);
        string feedback = $"购入 {addedQuantity} 件 {normalizedItemId}，花费 {spendCost} 金。";
        return new SettlementShopTradeResult(
            true,
            feedback,
            -spendCost,
            normalizedItemId,
            addedQuantity,
            null,
            nextSettlementState
        );
    }

    public SettlementShopTradeResult SellTyped(
        string interactionScriptId,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
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
        ItemDefinition itemDef = GetItemDef(itemDefs, normalizedItemId);
        if (itemDef == null)
        {
            return BuildFail("未找到该物品的定义。");
        }
        if (!itemDef.Sellable)
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
        int ownedQuantity = warehouse.CountItem(itemIdName);
        if (ownedQuantity <= 0)
        {
            return BuildFail("共享仓库中没有该物品。");
        }

        int actualQuantity = Mathf.Min(requestedQuantity, ownedQuantity);
        PartyWarehouseService.WarehouseRemoveItemResult removeResult;
        if (itemDef.IsEquipment())
        {
            if (string.IsNullOrEmpty(normalizedInstanceId) && ownedQuantity > 1)
            {
                return BuildFail($"请选择要出售的 {GetItemDisplayName(itemDef, normalizedItemId)} 装备实例。");
            }

            actualQuantity = 1;
            removeResult = !string.IsNullOrEmpty(normalizedInstanceId)
                ? warehouse.RemoveEquipmentInstanceTyped(itemIdName, instanceIdName)
                : warehouse.RemoveItemTyped(itemIdName, 1);
        }
        else
        {
            removeResult = warehouse.RemoveItemTyped(itemIdName, actualQuantity);
        }

        int removedQuantity = removeResult.RemovedQuantity;
        if (removedQuantity <= 0)
        {
            return BuildFail(BuildSellRemoveFailureMessage(itemDef, normalizedItemId, removeResult));
        }

        int totalGain = unitPrice * removedQuantity;
        party.AddGold(totalGain);

        string feedback = $"售出 {removedQuantity} 件 {GetItemDisplayName(itemDef, normalizedItemId)}，获得 {totalGain} 金。";
        return new SettlementShopTradeResult(
            true,
            feedback,
            totalGain,
            normalizedItemId,
            removedQuantity,
            normalizedInstanceId
        );
    }

    private ShopStateResolution GetOrRefreshShopState(
        ShopDefinition shopDef,
        WorldMapSettlementStateData settlementState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        int currentWorldStep)
    {
        SettlementShopStateData shopState = settlementState.GetShopState(shopDef.ShopId);
        int refreshInterval = Mathf.Max(shopDef.RefreshIntervalSteps, 0);
        int lastRefreshStep = shopState?.LastRefreshStep ?? -refreshInterval;
        bool needsRefresh = shopState == null
            || refreshInterval > 0 && currentWorldStep - lastRefreshStep >= refreshInterval;
        if (needsRefresh)
        {
            shopState = GenerateShopState(shopDef, itemDefs, currentWorldStep);
            WorldMapSettlementStateData updated = settlementState.WithShopState(shopState);
            return new ShopStateResolution(updated, shopState, true);
        }
        return new ShopStateResolution(settlementState, shopState, false);
    }

    private SettlementShopStateData GenerateShopState(
        ShopDefinition shopDef,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        int currentWorldStep
    )
    {
        long seed = TrueRandomSeedService.GenerateSeed();
        _rng.Reseed(seed);
        var inventory = new List<SettlementShopStockEntryData>();
        foreach (ShopItemSeed source in shopDef.GuaranteedItems)
        {
            SettlementShopStockEntryData built = BuildShopEntry(source, itemDefs);
            if (built != null)
                inventory.Add(built);
        }

        var randomPool = new List<ShopItemSeed>(shopDef.RandomPool);
        for (int i = 0; i < shopDef.MaxRandomItems; i++)
        {
            ShopItemSeed? picked = PickWeightedRandomEntry(randomPool, _rng);
            if (!picked.HasValue)
            {
                break;
            }

            SettlementShopStockEntryData built = BuildShopEntry(picked.Value, itemDefs);
            if (built != null)
                MergeShopEntry(inventory, built);
        }
        return SettlementShopStateData.Create(
            shopDef.ShopId,
            inventory,
            seed,
            Mathf.Max(currentWorldStep, 0)
        );
    }

    private SettlementShopStockEntryData BuildShopEntry(
        ShopItemSeed source,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs
    )
    {
        string itemId = ToItemIdString(source.ItemId);
        ItemDefinition itemDef = GetItemDef(itemDefs, itemId);
        if (string.IsNullOrEmpty(itemId) || itemDef == null)
            return null;

        int minQty = Mathf.Max(source.MinQty, 1);
        int maxQty = Mathf.Max(source.MaxQty, minQty);
        int quantity = _rng.RandiRange(minQty, maxQty);
        int unitPrice = ResolveBuyPrice(itemDef, source.PriceBasisPoints);
        if (unitPrice <= 0)
            return null;
        return SettlementShopStockEntryData.Create(itemId, quantity, unitPrice);
    }

    private static void MergeShopEntry(
        List<SettlementShopStockEntryData> inventory,
        SettlementShopStockEntryData builtEntry
    )
    {
        string itemId = builtEntry.ItemId;
        for (int i = 0; i < inventory.Count; i++)
        {
            SettlementShopStockEntryData existing = inventory[i];
            if (existing.ItemId != itemId)
            {
                continue;
            }
            inventory[i] = SettlementShopStockEntryData.Create(
                existing.ItemId,
                existing.Quantity + builtEntry.Quantity,
                existing.UnitPrice
            );
            return;
        }
        inventory.Add(builtEntry);
    }

    private static ShopItemSeed? PickWeightedRandomEntry(List<ShopItemSeed> pool, RuntimeRandom rng)
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

    private static int ResolveBuyPrice(ItemDefinition itemDef, int priceBasisPoints)
    {
        if (itemDef == null)
        {
            return 0;
        }
        int price = itemDef.GetBuyPrice(priceBasisPoints);
        return price > 0 ? price : 0;
    }

    private static int ResolveSellPrice(ItemDefinition itemDef)
    {
        if (itemDef == null)
        {
            return 0;
        }
        int price = itemDef.GetSellPrice();
        return price > 0 ? price : 0;
    }

    private static SettlementShopStockEntryData FindInventoryEntry(
        SettlementShopStateData shopState,
        string itemId
    )
    {
        if (shopState == null)
            return null;
        foreach (SettlementShopStockEntryData stockEntry in shopState.CurrentInventory)
        {
            if (stockEntry.ItemId == itemId)
                return stockEntry;
        }
        return null;
    }

    private static SettlementShopStateData ConsumeShopStock(
        SettlementShopStateData shopState,
        string itemId,
        int quantity
    )
    {
        var inventory = new List<SettlementShopStockEntryData>(shopState.CurrentInventory);
        for (int i = 0; i < inventory.Count; i++)
        {
            SettlementShopStockEntryData stockEntry = inventory[i];
            if (stockEntry.ItemId != itemId)
                continue;
            int remaining = stockEntry.Quantity - Mathf.Max(quantity, 0);
            if (remaining <= 0)
                inventory.RemoveAt(i);
            else
                inventory[i] = stockEntry.WithQuantity(remaining);
            break;
        }
        return shopState.WithInventory(inventory);
    }

    private static string BuildSellStockText(int totalQuantity, string instanceId)
    {
        return !string.IsNullOrEmpty(instanceId) ? $"持有 1 · 实例 {instanceId}" : $"持有 {totalQuantity}";
    }

    private static string BuildSellRemoveFailureMessage(
        ItemDefinition itemDef,
        string itemId,
        PartyWarehouseService.WarehouseRemoveItemResult removeResult
    )
    {
        string itemName = GetItemDisplayName(itemDef, itemId);
        return (removeResult?.ErrorCode ?? "") switch
        {
            "equipment_instance_id_required" => $"请选择要出售的 {itemName} 装备实例。",
            "warehouse_missing_instance" => $"共享仓库中没有指定的 {itemName} 装备实例。",
            "equipment_instance_item_mismatch" => $"指定装备实例不属于 {itemName}。",
            _ => "当前无法出售该物品。",
        };
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

    private static ItemDefinition GetItemDef(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        string itemId
    )
    {
        if (itemDefs == null || string.IsNullOrEmpty(itemId))
        {
            return null;
        }
        return itemDefs.TryGetValue(new StringName(itemId), out ItemDefinition itemDef)
            ? itemDef
            : null;
    }

    private static string GetItemDisplayName(ItemDefinition itemDef, string itemId)
    {
        return itemDef != null && itemDef.DisplayName.Length > 0
            ? itemDef.DisplayName
            : itemId;
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

    private static string GetString(GDictionary dictionary, string key, string fallback = "")
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    private static SettlementShopTradeResult BuildFail(string message) => new(false, message);
}
