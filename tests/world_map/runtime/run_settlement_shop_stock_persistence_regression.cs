using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GArray = Godot.Collections.Array;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_settlement_shop_stock_persistence_regression : SceneTree
{
    public override void _Initialize()
    {
        var itemDefs = new GDictionary
        {
            [new StringName("potion")] = new ItemDef
            {
                item_id = "potion",
                display_name = "Potion",
                base_price = 10,
                max_stack = 99,
                sellable = true,
            }
        };

        var party = new PartyState
        {
            gold = 100,
            active_member_ids = new GStringNameArray { new StringName("hero") },
            leader_member_id = "hero",
            main_character_member_id = "hero",
        };
        var hero = new PartyMemberState
        {
            member_id = "hero",
            display_name = "Hero",
            progression = new UnitProgress
            {
                unit_id = "hero",
                display_name = "Hero",
                unit_base_attributes = new UnitBaseAttributes(),
            },
        };
        hero.progression.unit_base_attributes.custom_stats["storage_space"] = 10;
        party.SetMemberState(hero);

        var warehouse = new PartyWarehouseService();
        warehouse.Setup(party, new System.Collections.Generic.Dictionary<StringName, ItemDef>
        {
            [new StringName("potion")] = (ItemDef)itemDefs[new StringName("potion")],
        });

        var settlementState = new GDictionary
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
        };

        var service = new SettlementShopService();
        SettlementShopTradeResult result = service.BuyTyped(
            "service_basic_supply",
            new GDictionary { ["settlement_id"] = "test_settlement" },
            settlementState,
            new System.Collections.Generic.Dictionary<StringName, ItemDef>
            {
                [new StringName("potion")] = (ItemDef)itemDefs[new StringName("potion")],
            },
            warehouse,
            party,
            "potion",
            1,
            ""
        );
        if (!result.Success)
            throw new System.Exception($"buy should succeed: {result.Message}");

        GDictionary storedShopStates = settlementState["shop_states"].AsGodotDictionary();
        GDictionary storedShopState = storedShopStates["village_basic_supply"].AsGodotDictionary();
        GArray inventory = storedShopState["current_inventory"].AsGodotArray();
        if (inventory.Count != 0)
            throw new System.Exception($"expected authoritative stock inventory to be empty, got {inventory.Count} entries");

        GD.Print("PASS shop stock mutation persists in settlement state");
        Quit(0);
    }
}
