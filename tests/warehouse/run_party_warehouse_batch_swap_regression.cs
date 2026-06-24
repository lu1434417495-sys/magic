using System.Collections.Generic;
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
        TestBatchSwapEntriesClonesTypedEquipmentInstance();
        TestBatchSwapEntriesAcceptsEquipmentInstanceDictionaryPayload();
        TestRemoveEquipmentInstanceTypedContracts();

        Quit(_test.Finish("Party warehouse batch swap regression"));
    }

    private void TestCommitBatchSwapRollsBackOnCapacityFailure()
    {
        PartyState partyState = BuildPartyState(capacity: 1);
        PartyWarehouseService service = BuildService(partyState);
        service.AddItemTyped("potion", 1);

        GDictionary result = PartyInventoryProjection.Project(
            service.CommitBatchSwapTyped(
                new GStringNameArray { "potion" },
                new GStringNameArray { "herb", "gem" }
            )
        );

        _test.False(DictBool(result, "allowed", true), "容量不足时 batch swap 应拒绝。");
        _test.Eq(DictString(result, "error_code", ""), "warehouse_blocked_swap", "容量不足应返回稳定错误码。");
        _test.Eq(service.CountItem("potion"), 1, "失败 commit 应恢复被 withdraw 的物品。");
        _test.Eq(service.CountItem("herb"), 0, "失败 commit 不应保留中间 deposit。");
        _test.Eq(service.CountItem("gem"), 0, "失败 commit 不应写入阻塞物品。");
        _test.Eq(service.GetUsedSlots(), 1, "失败 commit 后占用格应回滚。");
    }

    private void TestBatchSwapEntriesClonesTypedEquipmentInstance()
    {
        PartyState partyState = BuildPartyState(capacity: 2);
        PartyWarehouseService service = BuildService(partyState);
        EquipmentInstanceState sourceInstance = EquipmentInstanceState.CreateInstance(
            "iron_sword",
            "eq_000001"
        );
        sourceInstance.current_durability = 7;

        GDictionary result = PartyInventoryProjection.Project(
            service.CommitBatchSwapEntriesTyped(
                new List<PartyWarehouseService.WarehouseBatchItemEntry>(),
                new List<PartyWarehouseService.WarehouseBatchItemEntry>
                {
                    new()
                    {
                        ItemId = "iron_sword",
                        InstanceId = "eq_000001",
                        EquipmentInstance = sourceInstance,
                    },
                }
            )
        );

        _test.True(DictBool(result, "allowed", false), "typed 装备实例 batch swap entry 应提交成功。");
        _test.Eq(
            partyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped().Count,
            1,
            "typed 装备实例 entry 应写入共享仓库。"
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

        GDictionary result = PartyInventoryProjection.Project(
            service.CommitBatchSwapEntriesTyped(
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
            )
        );

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
