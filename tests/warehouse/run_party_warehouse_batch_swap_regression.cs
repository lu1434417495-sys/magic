using System.Collections.Generic;
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
            partyState.warehouse_state.equipment_instances.Count,
            1,
            "装备实例 entry 应写入共享仓库。"
        );
        EquipmentInstanceState storedInstance = partyState.warehouse_state.equipment_instances[0];
        AssertFalse(
            ReferenceEquals(storedInstance, sourceInstance),
            "batch swap entry 不应共享外部 EquipmentInstanceState 引用。"
        );
        storedInstance.current_durability = 2;
        AssertEq(sourceInstance.current_durability, 7, "修改仓库实例不应影响外部输入实例。");
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
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static string DictString(GDictionary dictionary, string key, string fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        Variant value = dictionary[key];
        return value.VariantType == Variant.Type.String ? value.AsString() : fallback;
    }
}
