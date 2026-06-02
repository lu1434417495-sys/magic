using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_party_warehouse_batch_swap_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestCommitBatchSwapRollsBackOnCapacityFailure();
        TestBatchSwapEntriesClonesEquipmentInstancePayload();
        TestBatchSwapEntriesAcceptsEquipmentInstanceDictionaryPayload();
        TestRemoveEquipmentInstanceTypedContracts();
        TestWarehouseServiceKeepsTypedItemDefIndex();
        TestWarehouseAddItemResultKeepsTypedAllocatedIds();
        TestWarehouseRemoveItemResultKeepsTypedFields();
        TestEquipmentInstanceIndexLookupUsesTypedList();
        TestBatchSwapItemIdTypedOverloadsUseReadOnlyLists();
        TestWarehouseStateReadSideUsesTypedLists();
        TestWarehouseStateWriteSideUsesTypedMethods();

        if (_failures.Count == 0)
        {
            GD.Print("Party warehouse batch swap regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Party warehouse batch swap regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestCommitBatchSwapRollsBackOnCapacityFailure()
    {
        PartyState partyState = BuildPartyState(capacity: 1);
        PartyWarehouseService service = BuildService(partyState);
        service.add_item("potion", 1);

        GDictionary result = service.commit_batch_swap(
            new GStringNameArray { "potion" },
            new GStringNameArray { "herb", "gem" }
        );

        AssertFalse(DictBool(result, "allowed", true), "容量不足时 batch swap 应拒绝。");
        AssertEq(DictString(result, "error_code", ""), "warehouse_blocked_swap", "容量不足应返回稳定错误码。");
        AssertEq(service.count_item("potion"), 1, "失败 commit 应恢复被 withdraw 的物品。");
        AssertEq(service.count_item("herb"), 0, "失败 commit 不应保留中间 deposit。");
        AssertEq(service.count_item("gem"), 0, "失败 commit 不应写入阻塞物品。");
        AssertEq(service.get_used_slots(), 1, "失败 commit 后占用格应回滚。");
    }

    private void TestBatchSwapEntriesClonesEquipmentInstancePayload()
    {
        PartyState partyState = BuildPartyState(capacity: 2);
        PartyWarehouseService service = BuildService(partyState);
        EquipmentInstanceState sourceInstance = EquipmentInstanceState.create(
            "iron_sword",
            "eq_000001"
        );
        sourceInstance.current_durability = 7;

        GDictionary result = service.commit_batch_swap_entries(
            new Godot.Collections.Array(),
            new Godot.Collections.Array
            {
                new GDictionary
                {
                    ["item_id"] = "iron_sword",
                    ["instance_id"] = "eq_000001",
                    ["equipment_instance"] = sourceInstance,
                },
            }
        );

        AssertTrue(DictBool(result, "allowed", false), "装备实例 batch swap entry 应提交成功。");
        AssertEq(
            partyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped().Count,
            1,
            "装备实例 entry 应写入共享仓库。"
        );
        EquipmentInstanceState storedInstance = partyState
            .warehouse_state
            .GetNonEmptyEquipmentInstancesTyped()[0];
        AssertFalse(
            ReferenceEquals(storedInstance, sourceInstance),
            "batch swap entry 不应共享外部 EquipmentInstanceState 引用。"
        );
        storedInstance.current_durability = 2;
        AssertEq(sourceInstance.current_durability, 7, "修改仓库实例不应影响外部输入实例。");
    }

    private void TestBatchSwapEntriesAcceptsEquipmentInstanceDictionaryPayload()
    {
        PartyState partyState = BuildPartyState(capacity: 2);
        PartyWarehouseService service = BuildService(partyState);
        EquipmentInstanceState sourceInstance = EquipmentInstanceState.create(
            "iron_sword",
            "eq_000002"
        );

        GDictionary result = service.commit_batch_swap_entries(
            new Godot.Collections.Array(),
            new Godot.Collections.Array
            {
                new GDictionary
                {
                    ["item_id"] = "iron_sword",
                    ["instance_id"] = "eq_000002",
                    ["equipment_instance"] = sourceInstance.to_dict(),
                },
            }
        );

        AssertTrue(DictBool(result, "allowed", false), "装备实例 Dictionary payload 应提交成功。");
        AssertEq(
            partyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped().Count,
            1,
            "装备实例 Dictionary payload 应写入共享仓库。"
        );
        AssertEq(
            partyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped()[0].instance_id.ToString(),
            "eq_000002",
            "装备实例 Dictionary payload 应保留 instance_id。"
        );
    }

    private void TestRemoveEquipmentInstanceTypedContracts()
    {
        PartyState partyState = BuildPartyState(capacity: 4);
        PartyWarehouseService service = BuildService(partyState);
        partyState.warehouse_state.AddEquipmentInstance(
            EquipmentInstanceState.create("iron_sword", "eq_common_sword")
        );
        partyState.warehouse_state.AddEquipmentInstance(
            EquipmentInstanceState.create("iron_sword", "eq_rare_sword")
        );
        partyState.warehouse_state.AddEquipmentInstance(
            EquipmentInstanceState.create("other_sword", "eq_wrong_item")
        );

        var itemOnlyRemove = service.RemoveItemTyped("iron_sword", 1);
        AssertEq(
            itemOnlyRemove.RemovedQuantity,
            0,
            "重复装备实例的 item-id remove 不应删除任意一件。"
        );
        AssertEq(
            itemOnlyRemove.ErrorCode,
            "equipment_instance_id_required",
            "重复装备实例 remove 应要求 instance_id。"
        );

        var mismatchRemove = service.RemoveEquipmentInstanceTyped(
            "iron_sword",
            "eq_wrong_item"
        );
        AssertEq(mismatchRemove.RemovedQuantity, 0, "错 item 的 instance_id 不应删除装备。");
        AssertEq(
            mismatchRemove.ErrorCode,
            "equipment_instance_item_mismatch",
            "错 item 的 instance_id 应返回 mismatch。"
        );

        var missingRemove = service.RemoveEquipmentInstanceTyped(
            "iron_sword",
            "eq_missing"
        );
        AssertEq(missingRemove.RemovedQuantity, 0, "不存在的 instance_id 不应删除装备。");
        AssertEq(
            missingRemove.ErrorCode,
            "warehouse_missing_instance",
            "不存在的 instance_id 应返回 missing_instance。"
        );

        var rareRemove = service.RemoveEquipmentInstanceTyped(
            "iron_sword",
            "eq_rare_sword"
        );
        AssertEq(rareRemove.RemovedQuantity, 1, "指定 instance_id 应删除对应装备。");
        AssertEq(
            service.has_equipment_instance("eq_rare_sword", "iron_sword"),
            false,
            "指定 instance_id 删除后不应留在仓库。"
        );
        AssertEq(
            service.has_equipment_instance("eq_common_sword", "iron_sword"),
            true,
            "指定 instance_id 删除不应影响同 item_id 的其他装备。"
        );
    }

    private void TestWarehouseServiceKeepsTypedItemDefIndex()
    {
        AssertEq(
            typeof(PartyWarehouseService)
                .GetField("_item_defs", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.FieldType,
            typeof(Dictionary<StringName, ItemDef>),
            "PartyWarehouseService 内部 item-def cache 应保持 typed Dictionary。"
        );
    }

    private void TestWarehouseAddItemResultKeepsTypedAllocatedIds()
    {
        System.Type resultType = typeof(PartyWarehouseService).GetNestedType(
            "WarehouseAddItemResult",
            BindingFlags.NonPublic
        );
        AssertEq(
            resultType
                ?.GetProperty("AllocatedEquipmentInstanceIds")
                ?.PropertyType,
            typeof(List<string>),
            "WarehouseAddItemResult 内部 allocated ids 应保持 C# List<string>。"
        );
    }

    private void TestWarehouseRemoveItemResultKeepsTypedFields()
    {
        System.Type resultType = typeof(PartyWarehouseService).GetNestedType(
            "WarehouseRemoveItemResult",
            BindingFlags.NonPublic
        );
        AssertEq(
            resultType
                ?.GetProperty("RemovedQuantity")
                ?.PropertyType,
            typeof(int),
            "WarehouseRemoveItemResult 内部 removed quantity 应保持 typed int。"
        );
        AssertEq(
            resultType
                ?.GetProperty("ErrorCode")
                ?.PropertyType,
            typeof(string),
            "WarehouseRemoveItemResult 内部 error code 应保持 typed string。"
        );
    }

    private void TestEquipmentInstanceIndexLookupUsesTypedList()
    {
        AssertEq(
            typeof(PartyWarehouseService)
                .GetMethod(
                    "_find_equipment_instance_indexes_by_item",
                    BindingFlags.NonPublic | BindingFlags.Static
                )
                ?.ReturnType,
            typeof(List<int>),
            "PartyWarehouseService 装备实例索引 helper 应返回 C# List<int>。"
        );
    }

    private void TestBatchSwapItemIdTypedOverloadsUseReadOnlyLists()
    {
        System.Type swapResultType = typeof(PartyWarehouseService).GetNestedType(
            "WarehouseBatchSwapResult",
            BindingFlags.NonPublic
        );
        System.Type[] parameterTypes =
        {
            typeof(IReadOnlyList<StringName>),
            typeof(IReadOnlyList<StringName>),
        };

        AssertEq(
            typeof(PartyWarehouseService)
                .GetMethod(
                    "PreviewBatchSwapTyped",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    parameterTypes,
                    null
                )
                ?.ReturnType,
            swapResultType,
            "batch swap preview typed overload should accept IReadOnlyList<StringName> inputs."
        );
        AssertEq(
            typeof(PartyWarehouseService)
                .GetMethod(
                    "CommitBatchSwapTyped",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    parameterTypes,
                    null
                )
                ?.ReturnType,
            swapResultType,
            "batch swap commit typed overload should accept IReadOnlyList<StringName> inputs."
        );

        PartyState partyState = BuildPartyState(capacity: 2);
        PartyWarehouseService service = BuildService(partyState);
        service.add_item("potion", 1);

        var result = service.CommitBatchSwapTyped(
            new List<StringName> { "potion" },
            new List<StringName> { "herb" }
        );

        AssertTrue(result.Allowed, "typed list batch swap should commit successfully.");
        AssertEq(service.count_item("potion"), 0, "typed list batch swap should withdraw source item.");
        AssertEq(service.count_item("herb"), 1, "typed list batch swap should deposit target item.");
    }

    private void TestWarehouseStateReadSideUsesTypedLists()
    {
        WarehouseState state = new()
        {
            stacks = new Godot.Collections.Array<WarehouseStackState>
            {
                new() { item_id = "potion", quantity = 2 },
                new() { item_id = "", quantity = 3 },
            },
            equipment_instances = new Godot.Collections.Array<EquipmentInstanceState>
            {
                EquipmentInstanceState.create("iron_sword", "eq_read_side"),
                EquipmentInstanceState.create("iron_sword", default),
            },
        };

        IReadOnlyList<WarehouseStackState> stacks = state.GetStacksTyped();
        IReadOnlyList<WarehouseStackState> nonEmptyStacks = state.GetNonEmptyStacksTyped();
        IReadOnlyList<EquipmentInstanceState> instances = state.GetEquipmentInstancesTyped();
        IReadOnlyList<EquipmentInstanceState> nonEmptyInstances =
            state.GetNonEmptyEquipmentInstancesTyped();

        AssertEq(stacks.GetType(), typeof(List<WarehouseStackState>), "stack typed query should return C# List copy.");
        AssertEq(instances.GetType(), typeof(List<EquipmentInstanceState>), "instance typed query should return C# List copy.");
        AssertEq(stacks.Count, 2, "all stack typed query should preserve raw entries for validation.");
        AssertEq(nonEmptyStacks.Count, 1, "non-empty stack typed query should filter invalid entries.");
        AssertEq(instances.Count, 2, "all instance typed query should preserve raw entries for validation.");
        AssertEq(nonEmptyInstances.Count, 1, "non-empty instance typed query should filter missing ids.");

        ((List<WarehouseStackState>)stacks).Clear();
        ((List<EquipmentInstanceState>)instances).Clear();
        AssertEq(state.stacks.Count, 2, "mutating returned stack list should not mutate WarehouseState arrays.");
        AssertEq(state.equipment_instances.Count, 2, "mutating returned instance list should not mutate WarehouseState arrays.");

        AssertEq(
            typeof(WarehouseState)
                .GetMethod(nameof(WarehouseState.GetNonEmptyStacksTyped))
                ?.ReturnType,
            typeof(IReadOnlyList<WarehouseStackState>),
            "WarehouseState non-empty stack query should expose IReadOnlyList."
        );
        AssertEq(
            typeof(WarehouseState)
                .GetMethod(nameof(WarehouseState.GetNonEmptyEquipmentInstancesTyped))
                ?.ReturnType,
            typeof(IReadOnlyList<EquipmentInstanceState>),
            "WarehouseState non-empty instance query should expose IReadOnlyList."
        );
    }

    private void TestWarehouseStateWriteSideUsesTypedMethods()
    {
        WarehouseState state = new();
        WarehouseStackState stack = new() { item_id = "potion", quantity = 2 };
        EquipmentInstanceState instance = EquipmentInstanceState.create(
            "iron_sword",
            "eq_write_side"
        );

        state.AddStack(stack);
        state.AddEquipmentInstance(instance);

        AssertEq(state.GetStackAt(0), stack, "typed stack getter should return stored stack.");
        AssertEq(
            state.GetEquipmentInstanceAt(0),
            instance,
            "typed instance getter should return stored equipment instance."
        );

        AssertTrue(state.RemoveStackAt(0), "typed stack remover should remove valid index.");
        AssertEq(state.GetStacksTyped().Count, 0, "typed stack remover should update state.");
        AssertEq(
            state.RemoveEquipmentInstanceAt(0),
            instance,
            "typed instance remover should return removed instance."
        );
        AssertEq(
            state.GetEquipmentInstancesTyped().Count,
            0,
            "typed instance remover should update state."
        );

        state.ReplaceStacks(
            new List<WarehouseStackState>
            {
                new() { item_id = "herb", quantity = 1 },
            }
        );
        state.ReplaceEquipmentInstances(
            new List<EquipmentInstanceState>
            {
                EquipmentInstanceState.create("iron_sword", "eq_replaced"),
            }
        );
        AssertEq(state.GetStacksTyped().Count, 1, "typed stack replacement should overwrite stacks.");
        AssertEq(
            state.GetEquipmentInstancesTyped().Count,
            1,
            "typed instance replacement should overwrite instances."
        );

        AssertEq(
            typeof(WarehouseState)
                .GetMethod(nameof(WarehouseState.AddEquipmentInstance))
                ?.ReturnType,
            typeof(void),
            "WarehouseState should expose typed equipment instance add method."
        );
        AssertEq(
            typeof(WarehouseState)
                .GetMethod(nameof(WarehouseState.RemoveEquipmentInstanceAt))
                ?.ReturnType,
            typeof(EquipmentInstanceState),
            "WarehouseState typed instance remover should return the removed instance."
        );
    }

    private static PartyWarehouseService BuildService(PartyState partyState)
    {
        PartyWarehouseService service = new();
        service.setup(partyState, BuildItemDefs());
        return service;
    }

    private static PartyState BuildPartyState(int capacity)
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
            warehouse_state = new WarehouseState(),
        };
        PartyMemberState memberState = new()
        {
            member_id = "hero",
            display_name = "Hero",
        };
        memberState.progression.unit_id = "hero";
        memberState.progression.display_name = "Hero";
        memberState
            .progression
            .unit_base_attributes
            .set_attribute_value(PartyWarehouseService.STORAGE_SPACE_ATTRIBUTE_ID(), capacity);
        partyState.set_member_state(memberState);
        return partyState;
    }

    private static GDictionary BuildItemDefs()
    {
        return new GDictionary
        {
            ["potion"] = BuildStackItem("potion"),
            ["herb"] = BuildStackItem("herb"),
            ["gem"] = BuildStackItem("gem"),
            ["iron_sword"] = new ItemDef
            {
                item_id = "iron_sword",
                display_name = "Iron Sword",
                item_category = ItemDef.ITEM_CATEGORY_EQUIPMENT(),
                is_stackable = false,
                max_stack = 1,
                equipment_type_id = ItemDef.EQUIPMENT_TYPE_WEAPON(),
                equipment_slot_ids = new Godot.Collections.Array<string>
                {
                    EquipmentRules.MAIN_HAND().ToString(),
                },
            },
        };
    }

    private static ItemDef BuildStackItem(StringName itemId)
    {
        return new ItemDef
        {
            item_id = itemId,
            display_name = itemId.ToString(),
            item_category = ItemDef.ITEM_CATEGORY_MISC(),
            is_stackable = true,
            max_stack = 99,
        };
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} expected={expected} actual={actual}");
    }

    private static bool DictBool(GDictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsBool();
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsString();
    }
}
