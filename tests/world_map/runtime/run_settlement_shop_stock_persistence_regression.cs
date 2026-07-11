using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GArray = Godot.Collections.Array;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_settlement_shop_stock_persistence_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private readonly GodotTransientResourceScope _runtimeScope =
        GodotTransientResourceScope.CreateTestQuarantine(
            "settlement_shop_stock_persistence"
        );

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

            var party = _runtimeScope.OwnWrapper(
                new PartyState
                {
                    gold = 100,
                    active_member_ids = new GStringNameArray { new StringName("hero") },
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
            _runtimeScope.OwnValueGraph(party, "party-built");

            warehouse = new PartyWarehouseService();
            warehouse.Setup(party, new System.Collections.Generic.Dictionary<StringName, ItemDefinition>
            {
                [new StringName("potion")] = potionDefinition,
            });

            var settlementState = _runtimeScope.OwnWrapper(
                new GDictionary
                {
                    ["shop_states"] = new GDictionary
                    {
                        ["village_basic_supply"] = new GDictionary
                        {
                            ["shop_id"] = "village_basic_supply",
                            ["current_inventory"] = new GArray
                            {
                                new GDictionary
                                {
                                    ["item_id"] = "potion",
                                    ["quantity"] = 1,
                                    ["unit_price"] = 10,
                                    ["sold_out"] = false,
                                }
                            },
                            ["seed"] = 1,
                            ["last_refresh_step"] = 0,
                        },
                    },
                    ["world_step"] = 0,
                },
                "settlement-state"
            );

            service = new SettlementShopService();
            SettlementShopTradeResult result = service.BuyTyped(
                "service_basic_supply",
                _runtimeScope.OwnWrapper(
                    new GDictionary { ["settlement_id"] = "test_settlement" },
                    "settlement-context"
                ),
                settlementState,
                new System.Collections.Generic.Dictionary<StringName, ItemDefinition>
                {
                    [new StringName("potion")] = potionDefinition,
                },
                warehouse,
                party,
                "potion",
                1,
                ""
            );
            _test.True(result.Success, $"buy should succeed: {result.Message}");

            GDictionary storedShopStates = settlementState["shop_states"].AsGodotDictionary();
            GDictionary storedShopState = storedShopStates["village_basic_supply"].AsGodotDictionary();
            GArray inventory = storedShopState["current_inventory"].AsGodotArray();
            _test.Eq(
                inventory.Count,
                0,
                $"expected authoritative stock inventory to be empty, got {inventory.Count} entries"
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
