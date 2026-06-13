using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_party_warehouse_batch_swap_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        Run();
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

        Quit(_test.Finish("Party warehouse batch swap regression"));
    }

    private void TestCommitBatchSwapRollsBackOnCapacityFailure()
    {
        PartyState partyState = BuildPartyState(capacity: 1);
        PartyWarehouseService service = BuildService(partyState);
        service.AddItemTyped("potion", 1);

        GDictionary result = service.CommitBatchSwapTyped(
            new GStringNameArray { "potion" },
            new GStringNameArray { "herb", "gem" }
        ).ToDictionary();

        _test.False(DictBool(result, "allowed", true), "容量不足时 batch swap 应拒绝。");
        _test.Eq(DictString(result, "error_code", ""), "warehouse_blocked_swap", "容量不足应返回稳定错误码。");
        _test.Eq(service.CountItem("potion"), 1, "失败 commit 应恢复被 withdraw 的物品。");
        _test.Eq(service.CountItem("herb"), 0, "失败 commit 不应保留中间 deposit。");
        _test.Eq(service.CountItem("gem"), 0, "失败 commit 不应写入阻塞物品。");
        _test.Eq(service.GetUsedSlots(), 1, "失败 commit 后占用格应回滚。");
    }

    private void TestBatchSwapEntriesClonesEquipmentInstancePayload()
    {
        PartyState partyState = BuildPartyState(capacity: 2);
        PartyWarehouseService service = BuildService(partyState);
        EquipmentInstanceState sourceInstance = EquipmentInstanceState.CreateInstance(
            "iron_sword",
            "eq_000001"
        );
        sourceInstance.current_durability = 7;

        GDictionary result = service.CommitBatchSwapEntriesTyped(
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
        ).ToDictionary();

        _test.True(DictBool(result, "allowed", false), "装备实例 batch swap entry 应提交成功。");
        _test.Eq(
            partyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped().Count,
            1,
            "装备实例 entry 应写入共享仓库。"
        );
        EquipmentInstanceState storedInstance = partyState
            .warehouse_state
            .GetNonEmptyEquipmentInstancesTyped()[0];
        _test.False(
            ReferenceEquals(storedInstance, sourceInstance),
            "batch swap entry 不应共享外部 EquipmentInstanceState 引用。"
        );
        storedInstance.current_durability = 2;
        _test.Eq(sourceInstance.current_durability, 7, "修改仓库实例不应影响外部输入实例。");
    }

    private void TestBatchSwapEntriesAcceptsEquipmentInstanceDictionaryPayload()
    {
        PartyState partyState = BuildPartyState(capacity: 2);
        PartyWarehouseService service = BuildService(partyState);
        EquipmentInstanceState sourceInstance = EquipmentInstanceState.CreateInstance(
            "iron_sword",
            "eq_000002"
        );

        GDictionary result = service.CommitBatchSwapEntriesTyped(
            new Godot.Collections.Array(),
            new Godot.Collections.Array
            {
                new GDictionary
                {
                    ["item_id"] = "iron_sword",
                    ["instance_id"] = "eq_000002",
                    ["equipment_instance"] = sourceInstance.ToDictionary(),
                },
            }
        ).ToDictionary();

        _test.True(DictBool(result, "allowed", false), "装备实例 Dictionary payload 应提交成功。");
        _test.Eq(
            partyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped().Count,
            1,
            "装备实例 Dictionary payload 应写入共享仓库。"
        );
        _test.Eq(
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
            EquipmentInstanceState.CreateInstance("iron_sword", "eq_common_sword")
        );
        partyState.warehouse_state.AddEquipmentInstance(
            EquipmentInstanceState.CreateInstance("iron_sword", "eq_rare_sword")
        );
        partyState.warehouse_state.AddEquipmentInstance(
            EquipmentInstanceState.CreateInstance("other_sword", "eq_wrong_item")
        );

        var itemOnlyRemove = service.RemoveItemTyped("iron_sword", 1);
        _test.Eq(
            itemOnlyRemove.RemovedQuantity,
            0,
            "重复装备实例的 item-id remove 不应删除任意一件。"
        );
        _test.Eq(
            itemOnlyRemove.ErrorCode,
            "equipment_instance_id_required",
            "重复装备实例 remove 应要求 instance_id。"
        );

        var mismatchRemove = service.RemoveEquipmentInstanceTyped(
            "iron_sword",
            "eq_wrong_item"
        );
        _test.Eq(mismatchRemove.RemovedQuantity, 0, "错 item 的 instance_id 不应删除装备。");
        _test.Eq(
            mismatchRemove.ErrorCode,
            "equipment_instance_item_mismatch",
            "错 item 的 instance_id 应返回 mismatch。"
        );

        var missingRemove = service.RemoveEquipmentInstanceTyped(
            "iron_sword",
            "eq_missing"
        );
        _test.Eq(missingRemove.RemovedQuantity, 0, "不存在的 instance_id 不应删除装备。");
        _test.Eq(
            missingRemove.ErrorCode,
            "warehouse_missing_instance",
            "不存在的 instance_id 应返回 missing_instance。"
        );

        var rareRemove = service.RemoveEquipmentInstanceTyped(
            "iron_sword",
            "eq_rare_sword"
        );
        _test.Eq(rareRemove.RemovedQuantity, 1, "指定 instance_id 应删除对应装备。");
        _test.Eq(
            service.HasEquipmentInstance("eq_rare_sword", "iron_sword"),
            false,
            "指定 instance_id 删除后不应留在仓库。"
        );
        _test.Eq(
            service.HasEquipmentInstance("eq_common_sword", "iron_sword"),
            true,
            "指定 instance_id 删除不应影响同 item_id 的其他装备。"
        );
    }

    private void TestWarehouseServiceKeepsTypedItemDefIndex()
    {
        _test.Eq(
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
        _test.Eq(
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
        _test.Eq(
            resultType
                ?.GetProperty("RemovedQuantity")
                ?.PropertyType,
            typeof(int),
            "WarehouseRemoveItemResult 内部 removed quantity 应保持 typed int。"
        );
        _test.Eq(
            resultType
                ?.GetProperty("ErrorCode")
                ?.PropertyType,
            typeof(string),
            "WarehouseRemoveItemResult 内部 error code 应保持 typed string。"
        );
    }

    private void TestEquipmentInstanceIndexLookupUsesTypedList()
    {
        _test.Eq(
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

        _test.Eq(
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
        _test.Eq(
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
        service.AddItemTyped("potion", 1);

        var result = service.CommitBatchSwapTyped(
            new List<StringName> { "potion" },
            new List<StringName> { "herb" }
        );

        _test.True(result.Allowed, "typed list batch swap should commit successfully.");
        _test.Eq(service.CountItem("potion"), 0, "typed list batch swap should withdraw source item.");
        _test.Eq(service.CountItem("herb"), 1, "typed list batch swap should deposit target item.");
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
                EquipmentInstanceState.CreateInstance("iron_sword", "eq_read_side"),
                EquipmentInstanceState.CreateTransientInstance("iron_sword"),
            },
        };

        IReadOnlyList<WarehouseStackState> stacks = state.GetStacksTyped();
        IReadOnlyList<WarehouseStackState> nonEmptyStacks = state.GetNonEmptyStacksTyped();
        IReadOnlyList<EquipmentInstanceState> instances = state.GetEquipmentInstancesTyped();
        IReadOnlyList<EquipmentInstanceState> nonEmptyInstances =
            state.GetNonEmptyEquipmentInstancesTyped();

        _test.Eq(stacks.GetType(), typeof(List<WarehouseStackState>), "stack typed query should return C# List copy.");
        _test.Eq(instances.GetType(), typeof(List<EquipmentInstanceState>), "instance typed query should return C# List copy.");
        _test.Eq(stacks.Count, 2, "all stack typed query should preserve raw entries for validation.");
        _test.Eq(nonEmptyStacks.Count, 1, "non-empty stack typed query should filter invalid entries.");
        _test.Eq(instances.Count, 2, "all instance typed query should preserve raw entries for validation.");
        _test.Eq(nonEmptyInstances.Count, 1, "non-empty instance typed query should filter missing ids.");

        ((List<WarehouseStackState>)stacks).Clear();
        ((List<EquipmentInstanceState>)instances).Clear();
        _test.Eq(state.stacks.Count, 2, "mutating returned stack list should not mutate WarehouseState arrays.");
        _test.Eq(state.equipment_instances.Count, 2, "mutating returned instance list should not mutate WarehouseState arrays.");

        _test.Eq(
            typeof(WarehouseState)
                .GetMethod(nameof(WarehouseState.GetNonEmptyStacksTyped))
                ?.ReturnType,
            typeof(IReadOnlyList<WarehouseStackState>),
            "WarehouseState non-empty stack query should expose IReadOnlyList."
        );
        _test.Eq(
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
        EquipmentInstanceState instance = EquipmentInstanceState.CreateInstance(
            "iron_sword",
            "eq_write_side"
        );

        state.AddStack(stack);
        state.AddEquipmentInstance(instance);

        _test.Eq(state.GetStackAt(0), stack, "typed stack getter should return stored stack.");
        _test.Eq(
            state.GetEquipmentInstanceAt(0),
            instance,
            "typed instance getter should return stored equipment instance."
        );

        _test.True(state.RemoveStackAt(0), "typed stack remover should remove valid index.");
        _test.Eq(state.GetStacksTyped().Count, 0, "typed stack remover should update state.");
        _test.Eq(
            state.RemoveEquipmentInstanceAt(0),
            instance,
            "typed instance remover should return removed instance."
        );
        _test.Eq(
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
                EquipmentInstanceState.CreateInstance("iron_sword", "eq_replaced"),
            }
        );
        _test.Eq(state.GetStacksTyped().Count, 1, "typed stack replacement should overwrite stacks.");
        _test.Eq(
            state.GetEquipmentInstancesTyped().Count,
            1,
            "typed instance replacement should overwrite instances."
        );

        _test.Eq(
            typeof(WarehouseState)
                .GetMethod(nameof(WarehouseState.AddEquipmentInstance))
                ?.ReturnType,
            typeof(void),
            "WarehouseState should expose typed equipment instance add method."
        );
        _test.Eq(
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
        service.Setup(partyState, BuildItemDefIndex(BuildItemDefs()));
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
            .SetAttributeValue(PartyWarehouseService.StorageSpaceAttributeId, capacity);
        partyState.SetMemberState(memberState);
        return partyState;
    }

    private static GDictionary BuildItemDefs()
    {
        return new GDictionary
        {
            [new StringName("potion")] = BuildStackItem("potion"),
            [new StringName("herb")] = BuildStackItem("herb"),
            [new StringName("gem")] = BuildStackItem("gem"),
            [new StringName("iron_sword")] = new ItemDef
            {
                item_id = "iron_sword",
                display_name = "Iron Sword",
                CategoryKind = ItemCategoryKind.Equipment,
                is_stackable = false,
                max_stack = 1,
                EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
                equipment_slot_ids = new Godot.Collections.Array<string>
                {
                    EquipmentRules.ToStringName(EquipmentSlotKind.MainHand).ToString(),
                },
            },
        };
    }

    private static Dictionary<StringName, ItemDef> BuildItemDefIndex(GDictionary itemDefs)
    {
        Dictionary<StringName, ItemDef> result = new();
        if (itemDefs == null)
            return result;
        foreach (Variant rawKey in itemDefs.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            StringName itemId = rawKey.AsStringName();
            if (itemId == "")
                continue;
            if (itemDefs[rawKey].AsGodotObject() is ItemDef itemDef)
                result[itemId] = itemDef;
        }
        return result;
    }

    private static ItemDef BuildStackItem(StringName itemId)
    {
        return new ItemDef
        {
            item_id = itemId,
            display_name = itemId.ToString(),
            CategoryKind = ItemCategoryKind.Misc,
            is_stackable = true,
            max_stack = 99,
        };
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
