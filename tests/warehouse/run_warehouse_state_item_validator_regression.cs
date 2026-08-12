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
        TestClampsOverMaxStackQuantities();
        TestStructuralViolationsSeparateFromTolerableIssues();
        TestDropsMisplacedEntriesOnLoad();

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

    private void TestClampsOverMaxStackQuantities()
    {
        WarehouseState warehouseState = new()
        {
            stacks = new List<WarehouseStackState>
            {
                new() { item_id = "healing_herb", quantity = 999 },
                new() { item_id = "healing_herb", quantity = 5 },
                new() { item_id = "healing_herb", quantity = 2 },
                new() { item_id = "missing_item", quantity = 40 },
            },
            equipment_instances = new List<EquipmentInstanceState>(),
        };

        WarehouseStateRepairResult repair = WarehouseStateItemValidator.RepairForLoad(
            warehouseState,
            BuildItemDefs()
        );
        _test.True(repair.Changed, "超过 max_stack 的堆叠应报告发生了修复。");
        _test.Eq(repair.ClampedStackCount, 1, "只有一条堆叠超限，应只钳制一条。");
        _test.Eq(repair.DroppedStackCount, 0, "超量堆叠只钳制，不应丢整条。");

        IReadOnlyList<WarehouseStackState> stacks = warehouseState.GetStacksTyped();
        _test.Eq(stacks.Count, 4, "钳制不应增删堆叠条目。");
        _test.Eq(stacks[0].quantity, 5, "超量堆叠应被钳制到 max_stack，多余数量丢弃。");
        _test.Eq(stacks[1].quantity, 5, "恰好等于 max_stack 的堆叠应保持不变。");
        _test.Eq(stacks[2].quantity, 2, "未超限堆叠应保持不变。");
        _test.Eq(stacks[3].quantity, 40, "缺失物品定义的堆叠应保持宽容不动。");

        _test.Eq(
            WarehouseStateItemValidator
                .Validate(warehouseState, BuildItemDefs(), "fixture.clamped")
                .Count,
            1,
            "钳制后应只剩缺失定义这一条校验错误。"
        );

        _test.True(
            !WarehouseStateItemValidator.RepairForLoad(warehouseState, BuildItemDefs()).Changed,
            "修复应当幂等，第二次不应再报告改动。"
        );
        _test.True(
            !WarehouseStateItemValidator.RepairForLoad(null, BuildItemDefs()).Changed,
            "缺失 warehouse state 不应报告修复。"
        );
    }

    private void TestStructuralViolationsSeparateFromTolerableIssues()
    {
        WarehouseState warehouseState = new()
        {
            stacks = new List<WarehouseStackState>
            {
                new() { item_id = "iron_sword", quantity = 3 },
                new() { item_id = "healing_herb", quantity = 999 },
                new() { item_id = "missing_item", quantity = 1 },
            },
            equipment_instances = new List<EquipmentInstanceState>
            {
                EquipmentInstanceState.CreateInstance("healing_herb", "eq_wrong_collection"),
                EquipmentInstanceState.CreateInstance("missing_equipment", "eq_missing"),
            },
        };

        List<WarehouseStateItemIssue> issues = WarehouseStateItemValidator.Collect(
            warehouseState,
            BuildItemDefs(),
            "fixture.warehouse"
        );
        _test.Eq(issues.Count, 5, "Collect 应报告全部 5 个问题。");

        List<string> violations = WarehouseStateItemValidator.CollectStructuralViolations(
            warehouseState,
            BuildItemDefs(),
            "fixture.warehouse"
        );
        _test.Eq(violations.Count, 2, "只有放错集合的两条才算结构性违规。");
        _test.True(
            violations[0].Contains("stores equipment item 'iron_sword' in stacks"),
            "装备躺在 stacks 里应被判为结构性违规。"
        );
        _test.True(
            violations[1].Contains("stores non-equipment item 'healing_herb' in equipment_instances"),
            "非装备躺在 equipment_instances 里应被判为结构性违规。"
        );

        _test.True(
            !WarehouseStateItemValidator.IsStructuralViolation(
                WarehouseStateItemIssueKind.UnknownStackItem
            ),
            "缺失物品定义应保持宽容，不算结构性违规。"
        );
        _test.True(
            !WarehouseStateItemValidator.IsStructuralViolation(
                WarehouseStateItemIssueKind.StackQuantityOverMaxStack
            ),
            "超量堆叠由钳制修复，不算结构性违规。"
        );
        _test.True(
            WarehouseStateItemValidator.IsStructuralViolation(
                WarehouseStateItemIssueKind.EquipmentStoredInStack
            ),
            "装备错放 stacks 应算结构性违规。"
        );

        WarehouseState healthyState = new()
        {
            stacks = new List<WarehouseStackState>
            {
                new() { item_id = "healing_herb", quantity = 5 },
                new() { item_id = "missing_item", quantity = 99 },
            },
            equipment_instances = new List<EquipmentInstanceState>
            {
                EquipmentInstanceState.CreateInstance("iron_sword", "eq_000002"),
            },
        };
        _test.Eq(
            WarehouseStateItemValidator
                .CollectStructuralViolations(healthyState, BuildItemDefs(), "fixture.healthy")
                .Count,
            0,
            "仅含缺失定义条目的存档不应被判为结构性违规。"
        );
    }

    private void TestDropsMisplacedEntriesOnLoad()
    {
        WarehouseState warehouseState = new()
        {
            stacks = new List<WarehouseStackState>
            {
                new() { item_id = "healing_herb", quantity = 3 },
                new() { item_id = "iron_sword", quantity = 3 },
                new() { item_id = "missing_item", quantity = 40 },
                new() { item_id = "healing_herb", quantity = 999 },
            },
            equipment_instances = new List<EquipmentInstanceState>
            {
                EquipmentInstanceState.CreateInstance("iron_sword", "eq_keep"),
                EquipmentInstanceState.CreateInstance("healing_herb", "eq_wrong_collection"),
                EquipmentInstanceState.CreateInstance("missing_equipment", "eq_unknown"),
            },
        };

        WarehouseStateRepairResult repair = WarehouseStateItemValidator.RepairForLoad(
            warehouseState,
            BuildItemDefs()
        );
        _test.Eq(repair.DroppedStackCount, 1, "装备错放 stacks 应被整条丢弃。");
        _test.Eq(repair.DroppedInstanceCount, 1, "非装备错放 equipment_instances 应被整条丢弃。");
        _test.Eq(repair.ClampedStackCount, 1, "同一趟修复里超量堆叠仍应被钳制。");

        IReadOnlyList<WarehouseStackState> stacks = warehouseState.GetStacksTyped();
        _test.Eq(stacks.Count, 3, "只应丢掉错放的那一条堆叠。");
        _test.Eq(stacks[0].item_id.ToString(), "healing_herb", "合法堆叠应保留。");
        _test.Eq(stacks[1].item_id.ToString(), "missing_item", "缺失定义的堆叠应宽容保留。");
        _test.Eq(stacks[1].quantity, 40, "缺失定义的堆叠数量不应被钳制。");
        _test.Eq(stacks[2].quantity, 5, "丢弃与钳制应在同一趟里各自生效，下标不应错位。");

        IReadOnlyList<EquipmentInstanceState> instances =
            warehouseState.GetEquipmentInstancesTyped();
        _test.Eq(instances.Count, 2, "只应丢掉错放的那一个实例。");
        _test.Eq(instances[0].instance_id.ToString(), "eq_keep", "合法装备实例应保留。");
        _test.Eq(
            instances[1].instance_id.ToString(),
            "eq_unknown",
            "缺失定义的实例应宽容保留。"
        );

        _test.Eq(
            WarehouseStateItemValidator
                .CollectStructuralViolations(warehouseState, BuildItemDefs(), "fixture.repaired")
                .Count,
            0,
            "修复后不应再有结构性违规。"
        );
        _test.True(
            !WarehouseStateItemValidator.RepairForLoad(warehouseState, BuildItemDefs()).Changed,
            "丢弃修复应当幂等。"
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
