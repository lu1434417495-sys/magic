using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GArray = Godot.Collections.Array;

public partial class run_settlement_shop_stock_persistence_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private readonly GodotTransientResourceScope _runtimeScope =
        new("settlement_shop_stock_persistence");

    public override void _Initialize()
    {
        PartyWarehouseService warehouse = null;
        SettlementShopService service = null;
        try
        {
            var itemDefs = _runtimeScope.OwnWrapper(
                new GDictionary
                {
                    [new StringName("potion")] = _runtimeScope.OwnWrapper(
                        new ItemDef
                        {
                            item_id = "potion",
                            display_name = "Potion",
                            base_price = 10,
                            max_stack = 99,
                            sellable = true,
                        },
                        "potion-item"
                    ),
                },
                "item-defs"
            );
            ItemDefinition potionDefinition = ((ItemDef)
                itemDefs[new StringName("potion")]).ToDefinition();
            var typedItemDefs = new Dictionary<StringName, ItemDefinition>
            {
                [new StringName("potion")] = potionDefinition,
            };

            var party = _runtimeScope.OwnWrapper(
                new PartyState
                {
                    gold = 100,
                    active_member_ids = new StringNameList { new StringName("hero") },
                    leader_member_id = "hero",
                    main_character_member_id = "hero",
                },
                "party"
            );
            var hero = _runtimeScope.OwnWrapper(
                new PartyMemberState
                {
                    member_id = "hero",
                    display_name = "Hero",
                    progression = new UnitProgress
                    {
                        unit_id = "hero",
                        display_name = "Hero",
                        unit_base_attributes = new UnitBaseAttributes(),
                    },
                },
                "hero"
            );
            hero.progression.unit_base_attributes.custom_stats["storage_space"] = 10;
            party.SetMemberState(hero);
            warehouse = new PartyWarehouseService();
            warehouse.Setup(party, typedItemDefs);

            SettlementShopStockEntryData stock = SettlementShopStockEntryData.Create(
                "potion",
                1,
                10
            );
            SettlementShopStateData shopState = SettlementShopStateData.Create(
                "village_basic_supply",
                new[] { stock },
                11,
                1
            );
            SettlementShopStateData otherShopState = SettlementShopStateData.Create(
                "town_local_trade",
                System.Array.Empty<SettlementShopStockEntryData>(),
                22,
                5
            );
            WorldMapSettlementStateData settlementState = WorldMapSettlementStateData.Create(
                true,
                0,
                System.Array.Empty<string>(),
                new System.Collections.Generic.Dictionary<string, int>(),
                new System.Collections.Generic.Dictionary<string, SettlementShopStateData>
                {
                    ["village_basic_supply"] = shopState,
                    ["town_local_trade"] = otherShopState,
                }
            );

            service = new SettlementShopService();
            SettlementShopWindowBuildResult windowResult = service.BuildWindowDataTyped(
                "service_basic_supply",
                new GDictionary
                {
                    ["display_name"] = "Village",
                    ["settlement_id"] = "village",
                },
                settlementState,
                7,
                "",
                typedItemDefs,
                warehouse,
                party.GetGold()
            );
            _test.False(
                windowResult.StateChanged,
                "未到刷新周期时打开商店不应制造顶层镜像状态变更。"
            );
            SettlementShopStateData unchangedPrimary = windowResult.UpdatedSettlementState
                .GetShopState("village_basic_supply");
            SettlementShopStateData unchangedOther = windowResult.UpdatedSettlementState
                .GetShopState("town_local_trade");
            _test.True(
                unchangedPrimary != null
                    && unchangedPrimary.Seed == 11
                    && unchangedPrimary.LastRefreshStep == 1,
                "目标商店应保留自身 seed 与刷新步。"
            );
            _test.True(
                unchangedOther != null
                    && unchangedOther.Seed == 22
                    && unchangedOther.LastRefreshStep == 5,
                "打开一家商店不得覆盖另一家商店的 seed 与刷新步。"
            );

            SettlementShopWindowBuildResult refreshResult = service.BuildWindowDataTyped(
                "service_basic_supply",
                new GDictionary
                {
                    ["display_name"] = "Village",
                    ["settlement_id"] = "village",
                },
                settlementState,
                13,
                "",
                typedItemDefs,
                warehouse,
                party.GetGold()
            );
            _test.True(refreshResult.StateChanged, "目标商店到期时应独立刷新。");
            SettlementShopStateData refreshedPrimary = refreshResult.UpdatedSettlementState
                .GetShopState("village_basic_supply");
            SettlementShopStateData refreshPreservedOther = refreshResult.UpdatedSettlementState
                .GetShopState("town_local_trade");
            _test.True(
                refreshedPrimary != null && refreshedPrimary.LastRefreshStep == 13,
                "刷新应只推进目标商店自己的刷新步。"
            );
            _test.True(
                refreshPreservedOther != null
                    && refreshPreservedOther.Seed == 22
                    && refreshPreservedOther.LastRefreshStep == 5,
                "刷新一家商店不得覆盖另一家商店的独立随机状态。"
            );

            SettlementShopTradeResult result = service.BuyTyped(
                "service_basic_supply",
                settlementState,
                7,
                typedItemDefs,
                warehouse,
                party,
                "potion",
                1
            );
            _test.True(result.Success, $"buy should succeed: {result.Message}");

            IReadOnlyList<SettlementShopStockEntryData> inventory = result
                .UpdatedSettlementState
                .GetShopState("village_basic_supply")
                .CurrentInventory;
            _test.Eq(
                inventory.Count,
                0,
                $"expected authoritative stock inventory to be empty, got {inventory.Count} entries"
            );
            SettlementShopStateData persistedOther = result.UpdatedSettlementState.GetShopState(
                "town_local_trade"
            );
            _test.True(
                persistedOther != null
                    && persistedOther.Seed == 22
                    && persistedOther.LastRefreshStep == 5,
                "购买写回不得覆盖其他商店的独立随机状态。"
            );
        }
        catch (System.Exception exception)
        {
            _test.Fail(exception.ToString());
        }
        finally
        {
            service?.Dispose();
            warehouse?.Dispose();
            _runtimeScope.Close();
        }

        RequestTestExit(_test.Finish("shop stock mutation persists in settlement state"));
    }
}
