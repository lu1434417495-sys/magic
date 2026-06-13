using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_warehouse_state_item_validator_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestAcceptsValidStackAndEquipmentInstance();
        TestRejectsInvalidWarehouseStateItems();
        TestWarehouseStatePayloadRequiresStringIds();
        TestWarehouseStateValidatorConsumesTypedReadSide();
        TestWarehouseStateItemValidatorIsPlainStaticHelper();

        Quit(_test.Finish("Warehouse state item validator regression"));
    }

    private void TestAcceptsValidStackAndEquipmentInstance()
    {
        WarehouseState warehouseState = new()
        {
            stacks = new Godot.Collections.Array<WarehouseStackState>
            {
                new() { item_id = "healing_herb", quantity = 3 },
            },
            equipment_instances = new Godot.Collections.Array<EquipmentInstanceState>
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
            stacks = new Godot.Collections.Array<WarehouseStackState>
            {
                new() { item_id = "healing_herb", quantity = 9 },
                new() { item_id = "iron_sword", quantity = 1 },
                new() { item_id = "missing_item", quantity = 1 },
            },
            equipment_instances = new Godot.Collections.Array<EquipmentInstanceState>
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

    private void TestWarehouseStateItemValidatorIsPlainStaticHelper()
    {
        var type = typeof(WarehouseStateItemValidator);
        _test.True(type.IsAbstract && type.IsSealed, "WarehouseStateItemValidator 应是 C# static helper。");

        MethodInfo validate = type.GetMethod("Validate", BindingFlags.Public | BindingFlags.Static);
        _test.Eq(validate?.ReturnType, typeof(List<string>), "Validate 应返回 typed List<string>。");
        ParameterInfo[] parameters = validate?.GetParameters() ?? System.Array.Empty<ParameterInfo>();
        _test.True(parameters.Length >= 2, "Validate 应暴露 warehouse state 和 typed item defs 参数。");
        if (parameters.Length >= 2)
        {
            _test.Eq(
                parameters[1].ParameterType,
                typeof(IReadOnlyDictionary<StringName, ItemDef>),
                "Validate 应消费 typed item-def map。"
            );
        }
    }

    private void TestWarehouseStateValidatorConsumesTypedReadSide()
    {
        _test.Eq(
            typeof(WarehouseState)
                .GetMethod(nameof(WarehouseState.GetStacksTyped))
                ?.ReturnType,
            typeof(IReadOnlyList<WarehouseStackState>),
            "WarehouseState raw stack query should expose IReadOnlyList for validation."
        );
        _test.Eq(
            typeof(WarehouseState)
                .GetMethod(nameof(WarehouseState.GetEquipmentInstancesTyped))
                ?.ReturnType,
            typeof(IReadOnlyList<EquipmentInstanceState>),
            "WarehouseState raw equipment instance query should expose IReadOnlyList for validation."
        );

        WarehouseState state = new()
        {
            stacks = new Godot.Collections.Array<WarehouseStackState>
            {
                null,
                new() { item_id = "healing_herb", quantity = 2 },
            },
            equipment_instances = new Godot.Collections.Array<EquipmentInstanceState>
            {
                null,
                EquipmentInstanceState.CreateInstance("iron_sword", "eq_validator_read_side"),
            },
        };

        _test.Eq(state.GetStacksTyped().Count, 2, "raw typed stack query should retain null entries.");
        _test.Eq(
            state.GetEquipmentInstancesTyped().Count,
            2,
            "raw typed instance query should retain null entries."
        );
    }

    private static Dictionary<StringName, ItemDef> BuildItemDefs()
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

        return new Dictionary<StringName, ItemDef>
        {
            [herb.item_id] = herb,
            [sword.item_id] = sword,
        };
    }


}
