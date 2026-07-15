using System.Collections.Generic;
using Godot;

public partial class run_warehouse_state_item_validator_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestAcceptsValidStackAndEquipmentInstance();
        TestRejectsInvalidWarehouseStateItems();
        TestWarehouseStatePayloadRequiresStringIds();

        RequestTestExit(_test.Finish("Warehouse state item validator regression"));
    }

    private void TestAcceptsValidStackAndEquipmentInstance()
    {
        WarehouseState warehouseState = new()
        {
            stacks = new List<WarehouseStackState>
            {
                new() { item_id = "healing_herb", quantity = 3 },
            },
            equipment_instances = new List<EquipmentInstanceState>
            {
                EquipmentInstanceState.CreateInstance("iron_sword", "eq_000001"),
            },
        };

        List<string> errors = WarehouseStateItemValidator.Validate(
            warehouseState,
            BuildItemDefs(),
            "fixture.warehouse"
        );

        _test.Eq(errors.Count, 0, "合法堆叠和装备实例不应产生校验错误。");
    }

    private void TestRejectsInvalidWarehouseStateItems()
    {
        WarehouseState warehouseState = new()
        {
            stacks = new List<WarehouseStackState>
            {
                new() { item_id = "healing_herb", quantity = 9 },
                new() { item_id = "iron_sword", quantity = 1 },
                new() { item_id = "missing_item", quantity = 1 },
            },
            equipment_instances = new List<EquipmentInstanceState>
            {
                EquipmentInstanceState.CreateInstance("healing_herb", "eq_bad_stack_item"),
                EquipmentInstanceState.CreateInstance("missing_equipment", "eq_missing"),
            },
        };

        List<string> errors = WarehouseStateItemValidator.Validate(
            warehouseState,
            BuildItemDefs(),
            "fixture.warehouse"
        );

        _test.Eq(errors.Count, 5, "非法 warehouse state fixture 应报告每个非法条目。");

        List<string> missingErrors = WarehouseStateItemValidator.Validate(
            null,
            BuildItemDefs(),
            "fixture.missing"
        );
        _test.Eq(missingErrors.Count, 1, "缺失 warehouse state 应只报告一个顶层错误。");
    }

    private void TestWarehouseStatePayloadRequiresStringIds()
    {
        _test.True(
            WarehouseStackState.FromDictionary(new Godot.Collections.Dictionary { ["item_id"] = "healing_herb", ["quantity"] = 1 }) != null,
            "Canonical stack payload should parse string item_id."
        );
        _test.True(
            WarehouseStackState.FromDictionary(new Godot.Collections.Dictionary { ["item_id"] = new StringName("healing_herb"), ["quantity"] = 1 }) == null,
            "StringName stack item_id should be rejected."
        );

        Godot.Collections.Dictionary instancePayload =
            EquipmentInstanceState.CreateInstance("iron_sword", "eq_validator_schema").ToDictionary();
        _test.True(
            instancePayload.ContainsKey("trait_instances"),
            "Canonical equipment instance payload should include trait_instances."
        );
        _test.True(
            EquipmentInstanceState.GetPayloadValidationError(instancePayload).Length == 0,
            "Canonical equipment instance payload should validate."
        );
        instancePayload["item_id"] = new StringName("iron_sword");
        _test.True(
            EquipmentInstanceState.GetPayloadValidationError(instancePayload).Length > 0,
            "StringName equipment instance item_id should be rejected."
        );

        Godot.Collections.Dictionary warehousePayload = new()
        {
            ["stacks"] = new Godot.Collections.Array
            {
                new Godot.Collections.Dictionary { ["item_id"] = new StringName("healing_herb"), ["quantity"] = 1 },
            },
            ["equipment_instances"] = new Godot.Collections.Array(),
        };
        _test.True(
            WarehouseState.FromDictionary(warehousePayload) == null,
            "WarehouseState should reject StringName stack payload ids."
        );
    }

    private static Dictionary<StringName, ItemDefinition> BuildItemDefs()
    {
        ItemDef herb = new()
        {
            item_id = "healing_herb",
            CategoryKind = ItemCategoryKind.Misc,
            is_stackable = true,
            max_stack = 5,
        };
        ItemDef sword = new()
        {
            item_id = "iron_sword",
            CategoryKind = ItemCategoryKind.Equipment,
            EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
            is_stackable = false,
            max_stack = 1,
            equipment_slot_ids = new Godot.Collections.Array<string>
            {
                EquipmentRules.ToStringName(EquipmentSlotKind.MainHand).ToString(),
            },
        };

        ItemDefinition herbDefinition = herb.ToDefinition();
        ItemDefinition swordDefinition = sword.ToDefinition();
        return new Dictionary<StringName, ItemDefinition>
        {
            [herbDefinition.ItemId] = herbDefinition,
            [swordDefinition.ItemId] = swordDefinition,
        };
    }


}
