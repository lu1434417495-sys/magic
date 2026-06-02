using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_warehouse_state_item_validator_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestAcceptsValidStackAndEquipmentInstance();
        TestRejectsInvalidWarehouseStateItems();
        TestWarehouseStateValidatorConsumesTypedReadSide();
        TestWarehouseStateItemValidatorIsPlainStaticHelper();

        if (_failures.Count == 0)
        {
            GD.Print("Warehouse state item validator regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Warehouse state item validator regression: FAIL ({_failures.Count})");
        Quit(1);
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
                EquipmentInstanceState.create_instance("iron_sword", "eq_000001"),
            },
        };

        List<string> errors = WarehouseStateItemValidator.Validate(
            warehouseState,
            BuildItemDefs(),
            "fixture.warehouse"
        );

        AssertEq(errors.Count, 0, "合法堆叠和装备实例不应产生校验错误。");
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
                EquipmentInstanceState.create_instance("healing_herb", "eq_bad_stack_item"),
                EquipmentInstanceState.create_instance("missing_equipment", "eq_missing"),
            },
        };

        List<string> errors = WarehouseStateItemValidator.Validate(
            warehouseState,
            BuildItemDefs(),
            "fixture.warehouse"
        );

        AssertContains(
            errors,
            "fixture.warehouse.stacks[0] quantity 9 exceeds max_stack 5",
            "超过 max_stack 的堆叠应报告数量错误。"
        );
        AssertContains(
            errors,
            "fixture.warehouse.stacks[1] stores equipment item 'iron_sword' in stacks",
            "装备物品误入 stacks 应被拒绝。"
        );
        AssertContains(
            errors,
            "fixture.warehouse.stacks[2] has unknown item_id 'missing_item'",
            "未知 stack item_id 应被拒绝。"
        );
        AssertContains(
            errors,
            "fixture.warehouse.equipment_instances[0] stores non-equipment item 'healing_herb'",
            "普通物品误入 equipment_instances 应被拒绝。"
        );
        AssertContains(
            errors,
            "fixture.warehouse.equipment_instances[1] has unknown item_id 'missing_equipment'",
            "未知装备实例 item_id 应被拒绝。"
        );

        List<string> missingErrors = WarehouseStateItemValidator.Validate(
            null,
            BuildItemDefs(),
            "fixture.missing"
        );
        AssertEq(missingErrors.Count, 1, "缺失 warehouse state 应只报告一个顶层错误。");
        AssertEq(missingErrors[0], "fixture.missing is missing.", "缺失 warehouse state 应带 context path。");
    }

    private void TestWarehouseStateItemValidatorIsPlainStaticHelper()
    {
        var type = typeof(WarehouseStateItemValidator);
        AssertTrue(type.IsAbstract && type.IsSealed, "WarehouseStateItemValidator 应是 C# static helper。");
        AssertTrue(
            !typeof(RefCounted).IsAssignableFrom(type),
            "WarehouseStateItemValidator 不应继承 RefCounted。"
        );
        AssertTrue(
            type.GetCustomAttribute<GlobalClassAttribute>() == null,
            "WarehouseStateItemValidator 不应注册为 Godot GlobalClass。"
        );

        MethodInfo validate = type.GetMethod("Validate", BindingFlags.Public | BindingFlags.Static);
        AssertEq(validate?.ReturnType, typeof(List<string>), "Validate 应返回 typed List<string>。");
        ParameterInfo[] parameters = validate?.GetParameters() ?? System.Array.Empty<ParameterInfo>();
        AssertTrue(parameters.Length >= 2, "Validate 应暴露 warehouse state 和 typed item defs 参数。");
        if (parameters.Length >= 2)
        {
            AssertEq(
                parameters[1].ParameterType,
                typeof(IReadOnlyDictionary<StringName, ItemDef>),
                "Validate 应消费 typed item-def map。"
            );
        }
    }

    private void TestWarehouseStateValidatorConsumesTypedReadSide()
    {
        AssertEq(
            typeof(WarehouseState)
                .GetMethod(nameof(WarehouseState.GetStacksTyped))
                ?.ReturnType,
            typeof(IReadOnlyList<WarehouseStackState>),
            "WarehouseState raw stack query should expose IReadOnlyList for validation."
        );
        AssertEq(
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
                EquipmentInstanceState.create_instance("iron_sword", "eq_validator_read_side"),
            },
        };

        AssertEq(state.GetStacksTyped().Count, 2, "raw typed stack query should retain null entries.");
        AssertEq(
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
            item_category = ItemDef.ITEM_CATEGORY_MISC(),
            is_stackable = true,
            max_stack = 5,
        };
        ItemDef sword = new()
        {
            item_id = "iron_sword",
            item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT(),
            equipment_type_id = ItemDef.EQUIPMENT_TYPE_WEAPON(),
            is_stackable = false,
            max_stack = 1,
            equipment_slot_ids = new Godot.Collections.Array<string>
            {
                EquipmentRules.MAIN_HAND().ToString(),
            },
        };

        return new Dictionary<StringName, ItemDef>
        {
            [herb.item_id] = herb,
            [sword.item_id] = sword,
        };
    }

    private void AssertContains(List<string> values, string expectedFragment, string message)
    {
        foreach (string value in values)
        {
            if (value.Contains(expectedFragment, System.StringComparison.Ordinal))
                return;
        }
        _failures.Add($"{message} | expected fragment={expectedFragment} actual={string.Join(" | ", values)}");
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }
}
