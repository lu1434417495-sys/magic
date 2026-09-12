using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class SettlementShopService : IDisposable
{
    internal const string ShopActionId = "shop:trade";
    private sealed record ShopStateResolution(
        WorldMapSettlementStateData SettlementState,
        SettlementShopStateData ShopState,
        bool StateChanged
    );
    private IReadOnlyDictionary<StringName, SettlementShopDefinition> _shopDefinitions;

    private readonly RuntimeRandom _rng = new();
    private Func<int, int, int> _uniqueOfferRollRangeForTesting;

    internal SettlementShopService() { }

    public SettlementShopService(
        IReadOnlyDictionary<StringName, SettlementShopDefinition> shopDefinitions
    )
    {
        SetDefinitions(shopDefinitions);
    }

    internal void SetDefinitions(
        IReadOnlyDictionary<StringName, SettlementShopDefinition> shopDefinitions
    )
    {
        ArgumentNullException.ThrowIfNull(shopDefinitions);
        _shopDefinitions = shopDefinitions;
    }

    internal bool HasShop(StringName interactionScriptId)
    {
        IReadOnlyDictionary<StringName, SettlementShopDefinition> definitions =
            RequireDefinitions();
        return interactionScriptId != "" && definitions.ContainsKey(interactionScriptId);
    }

    internal void SetUniqueOfferRollRangeForTesting(Func<int, int, int> rollRange) =>
        _uniqueOfferRollRangeForTesting = rollRange;

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
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs = null,
        WorldUniqueEquipmentPoolState uniqueEquipmentPool = null)
    {
        SettlementShopDefinition shopDef = ResolveShopDef(interactionScriptId);
        if (shopDef == null || settlementState == null)
        {
            return new SettlementShopWindowBuildResult(
                SettlementServiceWindowData.Empty,
                settlementState,
                false
            );
        }

        string settlementId = GetString(settlementRecord, "settlement_id");
        ShopStateResolution resolution = GetOrRefreshShopState(
            shopDef,
            settlementState,
            itemDefs,
            currentWorldStep,
            settlementId,
            uniqueEquipmentPool
        );
        SettlementShopStateData shopState = resolution.ShopState;
        var entries = new List<SettlementServiceWindowEntryData>();
        foreach (SettlementShopStockEntryData stockEntry in shopState.CurrentInventory)
        {
            ItemDefinition itemDef = GetItemDef(itemDefs, stockEntry.ItemId);
            if (itemDef == null)
            {
                continue;
            }

            StringName uniqueInstanceId = "";
            bool isUniqueOffer = uniqueEquipmentPool != null
                && uniqueEquipmentPool.TryGetShopOffer(
                    settlementId,
                    shopDef.ShopId,
                    new StringName(stockEntry.ItemId),
                    out uniqueInstanceId
                );
            bool canBuy = stockEntry.Quantity > 0 && currentGold >= stockEntry.UnitPrice;
            string stockText = stockEntry.Quantity <= 0
                ? "售罄"
                : isUniqueOffer
                    ? $"唯一实例 {uniqueInstanceId}"
                    : $"库存 {stockEntry.Quantity}";
            string description = itemDef.Description;
            entries.Add(
                new SettlementServiceWindowEntryData(
                    isUniqueOffer
                        ? $"buy:{stockEntry.ItemId}:{uniqueInstanceId}"
                        : $"buy:{stockEntry.ItemId}",
                    GetItemDisplayName(itemDef, stockEntry.ItemId),
                    stockText,
                    ItemTraitDetailText.Compose(description, itemDef, traitDefs),
                    canBuy ? "状态：可购" : "状态：不可购",
                    $"单价 {stockEntry.UnitPrice} 金",
                    canBuy,
                    canBuy ? "" : stockEntry.Quantity <= 0 ? "库存不足" : "金币不足",
                    new SettlementShopSelectionData(
                        SettlementShopActionKind.Buy,
                        stockEntry.ItemId,
                        "",
                        stockEntry.Quantity
                    )
                )
            );
        }

        var sellEntries = new List<SettlementServiceWindowEntryData>();
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
                sellEntries.Add(
                    new SettlementServiceWindowEntryData(
                        !string.IsNullOrEmpty(instanceId)
                            ? $"sell:{itemId}:{instanceId}"
                            : $"sell:{itemId}",
                        GetItemDisplayName(itemDef, itemId),
                        stockText,
                        ItemTraitDetailText.Compose(itemDef.Description, itemDef, traitDefs),
                        "状态：可售",
                        $"回收 {unitPrice} 金",
                        true,
                        "",
                        new SettlementShopSelectionData(
                            SettlementShopActionKind.Sell,
                            itemId,
                            instanceId,
                            totalQuantity
                        )
                    )
                );
            }
        }

        SortSellEntries(sellEntries);
        entries.AddRange(sellEntries);
        string displayName = GetString(settlementRecord, "display_name", "据点");
        int gold = Mathf.Max(currentGold, 0);
        return new SettlementShopWindowBuildResult(
            new SettlementServiceWindowData(
                settlementId,
                ShopActionId,
                SettlementPanelKind.Shop,
                $"{displayName} · {shopDef.Title}",
                $"商店：{shopDef.Title}  |  金币：{gold}",
                $"持有金币：{gold}",
                feedbackText ?? "",
                new SettlementServiceWindowLabelsData(
                    "确认交易",
                    "返回据点",
                    "交易条目",
                    "交易概况",
                    "交易状态",
                    "交易费用",
                    "交易说明",
                    "交易成员",
                    "状态：暂无商品",
                    "费用：暂无商品",
                    "当前没有可交易条目。"
                ),
                true,
                interactionScriptId,
                "",
                "",
                "",
                "",
                "",
                entries,
                null,
                "",
                "",
                null
            ),
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
        int quantity,
        WorldUniqueEquipmentPoolState uniqueEquipmentPool = null,
        string settlementId = "")
    {
        SettlementShopDefinition shopDef = ResolveShopDef(interactionScriptId);
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
            currentWorldStep,
            settlementId,
            null
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

        StringName uniqueInstanceId = "";
        bool isUniqueOffer = uniqueEquipmentPool != null
            && uniqueEquipmentPool.TryGetShopOffer(
                settlementId,
                shopDef.ShopId,
                new StringName(normalizedItemId),
                out uniqueInstanceId
            );
        int actualQuantity = isUniqueOffer
            ? 1
            : Mathf.Min(requestedQuantity, stockEntry.Quantity);
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

        int addedQuantity;
        string transferredInstanceId = null;
        if (isUniqueOffer)
        {
            if (
                uniqueEquipmentPool == null
                || !uniqueEquipmentPool.TryTakeShopOffer(
                    settlementId,
                    shopDef.ShopId,
                    itemIdName,
                    out EquipmentInstanceState uniqueInstance
                )
            )
            {
                return BuildFail("该唯一装备已不在当前商店。请刷新商店后重试。");
            }
            var uniqueAddResult = warehouse.AddEquipmentInstanceTyped(uniqueInstance);
            addedQuantity = uniqueAddResult.AddedQuantity;
            transferredInstanceId = uniqueInstance.instance_id.ToString();
            if (addedQuantity <= 0)
            {
                uniqueEquipmentPool.TryReturnToShop(
                    uniqueInstance,
                    settlementId,
                    shopDef.ShopId
                );
            }
        }
        else
        {
            var addResult = warehouse.AddItemTyped(itemIdName, actualQuantity);
            addedQuantity = addResult.AddedQuantity;
        }
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
            transferredInstanceId,
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
        StringName instanceId = default,
        WorldMapSettlementStateData settlementState = null,
        WorldUniqueEquipmentPoolState uniqueEquipmentPool = null,
        string settlementId = "",
        bool transferUniqueInstanceToShop = false)
    {
        SettlementShopDefinition shopDef = ResolveShopDef(interactionScriptId);
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
        EquipmentInstanceState uniqueInstance = null;
        WorldMapSettlementStateData uniqueUpdatedSettlementState = null;
        if (itemDef.IsEquipment())
        {
            if (string.IsNullOrEmpty(normalizedInstanceId) && ownedQuantity > 1)
            {
                return BuildFail($"请选择要出售的 {GetItemDisplayName(itemDef, normalizedItemId)} 装备实例。");
            }

            actualQuantity = 1;
            if (transferUniqueInstanceToShop)
            {
                if (uniqueEquipmentPool == null || settlementState == null)
                    return BuildFail("当前唯一装备商店状态无效，无法出售。");
                if (string.IsNullOrEmpty(normalizedInstanceId))
                {
                    foreach (WarehouseInventoryEntry entry in warehouse.GetInventoryEntriesTyped())
                    {
                        if (!entry.HasEquipmentInstance || entry.ItemId != itemIdName)
                            continue;
                        instanceIdName = entry.InstanceId;
                        normalizedInstanceId = instanceIdName.ToString();
                        break;
                    }
                }
                uniqueInstance = warehouse.GetEquipmentInstanceById(
                    instanceIdName,
                    itemIdName
                );
                if (uniqueInstance == null)
                    return BuildFail("共享仓库中没有指定的唯一装备实例。");
                uniqueUpdatedSettlementState = BuildUniqueResaleSettlementState(
                    shopDef,
                    settlementState,
                    itemDef,
                    normalizedItemId
                );
                if (uniqueUpdatedSettlementState == null)
                    return BuildFail("当前商店无法接收该唯一装备实例。");
            }
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

        if (
            transferUniqueInstanceToShop
            && !uniqueEquipmentPool.TryReturnToShop(
                uniqueInstance,
                settlementId,
                shopDef.ShopId
            )
        )
        {
            PartyWarehouseService.WarehouseAddItemResult restoreResult =
                warehouse.AddEquipmentInstanceTyped(uniqueInstance);
            return BuildFail(
                restoreResult.AddedQuantity > 0
                    ? "该唯一装备无法转移到当前商店，出售已回滚。"
                    : "该唯一装备无法转移到当前商店，且仓库回滚失败。"
            );
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
            normalizedInstanceId,
            uniqueUpdatedSettlementState
        );
    }

    private ShopStateResolution GetOrRefreshShopState(
        SettlementShopDefinition shopDef,
        WorldMapSettlementStateData settlementState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        int currentWorldStep,
        string settlementId = "",
        WorldUniqueEquipmentPoolState uniqueEquipmentPool = null)
    {
        SettlementShopStateData shopState = settlementState.GetShopState(shopDef.ShopId);
        int refreshInterval = Mathf.Max(shopDef.RefreshIntervalSteps, 0);
        int lastRefreshStep = shopState?.LastRefreshStep ?? -refreshInterval;
        bool needsRefresh = shopState == null
            || refreshInterval > 0 && currentWorldStep - lastRefreshStep >= refreshInterval;
        if (needsRefresh)
        {
            uniqueEquipmentPool?.ReturnShopOffers(settlementId, shopDef.ShopId);
            shopState = GenerateShopState(
                shopDef,
                itemDefs,
                currentWorldStep,
                settlementId,
                uniqueEquipmentPool
            );
            WorldMapSettlementStateData updated = settlementState.WithShopState(shopState);
            return new ShopStateResolution(updated, shopState, true);
        }
        return new ShopStateResolution(settlementState, shopState, false);
    }

    private SettlementShopStateData GenerateShopState(
        SettlementShopDefinition shopDef,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        int currentWorldStep,
        string settlementId = "",
        WorldUniqueEquipmentPoolState uniqueEquipmentPool = null
    )
    {
        long seed = TrueRandomSeedService.GenerateSeed();
        _rng.Reseed(seed);
        var inventory = new List<SettlementShopStockEntryData>();
        foreach (SettlementShopItemDefinition source in shopDef.GuaranteedItems)
        {
            SettlementShopStockEntryData built = BuildShopEntry(source, itemDefs);
            if (built != null)
                inventory.Add(built);
        }

        var randomPool = new List<SettlementShopItemDefinition>(shopDef.RandomPool);
        for (int i = 0; i < shopDef.MaxRandomItems; i++)
        {
            SettlementShopItemDefinition picked = PickWeightedRandomEntry(randomPool, _rng);
            if (picked == null)
            {
                break;
            }

            SettlementShopStockEntryData built = BuildShopEntry(picked, itemDefs);
            if (built != null)
                MergeShopEntry(inventory, built);
        }
        TryAppendUniqueEquipmentOffer(
            inventory,
            shopDef,
            settlementId,
            itemDefs,
            uniqueEquipmentPool
        );
        return SettlementShopStateData.Create(
            shopDef.ShopId,
            inventory,
            seed,
            Mathf.Max(currentWorldStep, 0)
        );
    }

    private void TryAppendUniqueEquipmentOffer(
        List<SettlementShopStockEntryData> inventory,
        SettlementShopDefinition shopDef,
        string settlementId,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        WorldUniqueEquipmentPoolState uniqueEquipmentPool
    )
    {
        if (uniqueEquipmentPool == null || string.IsNullOrWhiteSpace(settlementId))
            return;
        int chanceRoll = RollUniqueOfferRange(1, 100);
        if (chanceRoll > shopDef.UniqueEquipmentOfferChancePercent)
            return;
        if (
            !uniqueEquipmentPool.TryAssignRandomReserveToShop(
                settlementId,
                shopDef.ShopId,
                RollUniqueOfferRange,
                out StringName itemId,
                out _
            )
        )
        {
            return;
        }
        ItemDefinition itemDefinition = GetItemDef(itemDefs, itemId.ToString());
        int unitPrice = ResolveBuyPrice(itemDefinition, shopDef.DefaultPriceBasisPoints);
        SettlementShopStockEntryData offer = unitPrice > 0
            ? SettlementShopStockEntryData.Create(itemId.ToString(), 1, unitPrice)
            : null;
        if (offer != null)
        {
            MergeShopEntry(inventory, offer);
            return;
        }

        if (
            uniqueEquipmentPool.TryTakeShopOffer(
                settlementId,
                shopDef.ShopId,
                itemId,
                out EquipmentInstanceState rejectedInstance
            )
        )
        {
            uniqueEquipmentPool.TryReturnToReserve(rejectedInstance);
        }
    }

    private int RollUniqueOfferRange(int minInclusive, int maxInclusive) =>
        _uniqueOfferRollRangeForTesting != null
            ? _uniqueOfferRollRangeForTesting(minInclusive, maxInclusive)
            : _rng.RandiRange(minInclusive, maxInclusive);

    private SettlementShopStockEntryData BuildShopEntry(
        SettlementShopItemDefinition source,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs
    )
    {
        // GameplayConfigurationCrossDomainValidator 已保证商店商品存在且买价为正；
        // 静默返回 null 会让 guaranteed 备货凭空少一格，没人看得出来。
        string itemId = source.ItemId.ToString();
        ItemDefinition itemDef =
            GetItemDef(itemDefs, itemId)
            ?? throw new InvalidOperationException(
                $"Settlement shop stock references unknown item {itemId}."
            );

        int minQty = Mathf.Max(source.MinQuantity, 1);
        int maxQty = Mathf.Max(source.MaxQuantity, minQty);
        int quantity = _rng.RandiRange(minQty, maxQty);
        int unitPrice = ResolveBuyPrice(itemDef, source.PriceBasisPoints);
        if (unitPrice <= 0)
        {
            throw new InvalidOperationException(
                $"Settlement shop stock item {itemId} resolved to a non-positive buy price."
            );
        }
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

    private static SettlementShopItemDefinition PickWeightedRandomEntry(
        List<SettlementShopItemDefinition> pool,
        RuntimeRandom rng
    )
    {
        int totalWeight = 0;
        foreach (SettlementShopItemDefinition entry in pool)
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
            SettlementShopItemDefinition entry = pool[i];
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

    private static WorldMapSettlementStateData BuildUniqueResaleSettlementState(
        SettlementShopDefinition shopDef,
        WorldMapSettlementStateData settlementState,
        ItemDefinition itemDef,
        string itemId
    )
    {
        if (shopDef == null || settlementState == null || itemDef == null)
            return null;
        SettlementShopStateData shopState = settlementState.GetShopState(shopDef.ShopId);
        if (shopState == null)
            return null;
        int unitPrice = ResolveBuyPrice(itemDef, shopDef.DefaultPriceBasisPoints);
        SettlementShopStockEntryData resaleEntry =
            SettlementShopStockEntryData.Create(itemId, 1, unitPrice);
        if (resaleEntry == null)
            return null;
        var inventory = new List<SettlementShopStockEntryData>(shopState.CurrentInventory);
        MergeShopEntry(inventory, resaleEntry);
        return settlementState.WithShopState(shopState.WithInventory(inventory));
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

    private static void SortSellEntries(List<SettlementServiceWindowEntryData> entries)
    {
        // Stable ordering for repeated item ids (equipment instances share an item id):
        // the entry id carries the instance suffix, so it is the tiebreaker.
        entries.Sort(
            (left, right) =>
            {
                int byItemId = string.CompareOrdinal(SellSortKey(left), SellSortKey(right));
                return byItemId != 0
                    ? byItemId
                    : string.CompareOrdinal(left.EntryId.ToString(), right.EntryId.ToString());
            }
        );
    }

    private static string SellSortKey(SettlementServiceWindowEntryData entry) =>
        entry.Selection is SettlementShopSelectionData selection
            ? selection.ItemId.ToString()
            : "";

    private SettlementShopDefinition ResolveShopDef(string interactionScriptId)
    {
        IReadOnlyDictionary<StringName, SettlementShopDefinition> definitions =
            RequireDefinitions();
        if (string.IsNullOrWhiteSpace(interactionScriptId))
            return null;
        return definitions.TryGetValue(
            new StringName(interactionScriptId),
            out SettlementShopDefinition shopDefinition
        ) ? shopDefinition : null;
    }

    private IReadOnlyDictionary<StringName, SettlementShopDefinition> RequireDefinitions() =>
        _shopDefinitions
        ?? throw new InvalidOperationException(
            "SettlementShopService requires published settlement shop definitions before use."
        );

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
