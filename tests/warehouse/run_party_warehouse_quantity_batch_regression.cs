using System.Collections.Generic;
using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_party_warehouse_quantity_batch_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        Run();
    }

    private void Run()
    {
        TestPreviewWithdrawsQuantityWithoutMutation();
        TestPreviewFailsWhenTotalQuantityIsInsufficient();
        TestCommitWithdrawsExactQuantityAndPreservesUnrelatedItems();
        TestCommitIsAtomicWhenRequestedItemIsInsufficient();
        TestDepositQuantityPreviewAndCommit();
        TestCaptureAndRestoreReturnsExactQuantities();
        TestInvalidQuantityEntriesDoNotMutate();
        TestEquipmentQuantityEntryIsBlocked();

        Quit(_test.Finish("Party warehouse quantity batch regression"));
    }

    private void TestPreviewWithdrawsQuantityWithoutMutation()
    {
        PartyState partyState = BuildPartyState(capacity: 4);
        PartyWarehouseService service = BuildService(partyState);
        service.AddItemTyped("potion", 5);

        WarehouseBatchSwapResult result = service.PreviewBatchQuantitySwapTyped(
            new List<WarehouseBatchQuantityEntry>
            {
                new("potion", 3),
            },
            new List<WarehouseBatchQuantityEntry>()
        );

        _test.True(result.Allowed, $"quantity preview should allow available stack quantity. error={result.ErrorCode}");
        _test.Eq(service.CountItem("potion"), 5, "quantity preview must not remove stack quantity.");
        _test.Eq(StackQuantities(partyState.warehouse_state, "potion"), "5", "quantity preview must preserve exact stack quantities.");
    }

    private void TestPreviewFailsWhenTotalQuantityIsInsufficient()
    {
        PartyState partyState = BuildPartyState(capacity: 4);
        PartyWarehouseService service = BuildService(partyState);
        service.AddItemTyped("herb", 3);

        WarehouseBatchSwapResult result = service.PreviewBatchQuantitySwapTyped(
            new List<WarehouseBatchQuantityEntry>
            {
                new("herb", 4),
            },
            new List<WarehouseBatchQuantityEntry>()
        );

        _test.False(result.Allowed, "quantity preview should reject insufficient total stack quantity.");
        _test.Eq(result.ErrorCode, "warehouse_missing_item", "insufficient quantity should return stable missing-item error.");
        _test.Eq(result.BlockedItemId, new StringName("herb"), "insufficient quantity should report blocked item id.");
        _test.Eq(StackQuantities(partyState.warehouse_state, "herb"), "2,1", "failed preview must not mutate split stacks.");
    }

    private void TestCommitWithdrawsExactQuantityAndPreservesUnrelatedItems()
    {
        PartyState partyState = BuildPartyState(capacity: 5);
        PartyWarehouseService service = BuildService(partyState);
        service.AddItemTyped("herb", 4);
        service.AddItemTyped("gem", 2);

        WarehouseBatchSwapResult result = service.CommitBatchQuantitySwapTyped(
            new List<WarehouseBatchQuantityEntry>
            {
                new("herb", 3),
            },
            new List<WarehouseBatchQuantityEntry>()
        );

        _test.True(result.Allowed, $"quantity commit should withdraw available quantity. error={result.ErrorCode}");
        _test.Eq(service.CountItem("herb"), 1, "quantity commit should remove the exact requested total.");
        _test.Eq(StackQuantities(partyState.warehouse_state, "herb"), "1", "quantity commit should consume stacks in deterministic existing order.");
        _test.Eq(service.CountItem("gem"), 2, "quantity commit should preserve unrelated items.");
    }

    private void TestCommitIsAtomicWhenRequestedItemIsInsufficient()
    {
        PartyState partyState = BuildPartyState(capacity: 5);
        PartyWarehouseService service = BuildService(partyState);
        service.AddItemTyped("potion", 5);
        service.AddItemTyped("gem", 1);

        WarehouseBatchSwapResult result = service.CommitBatchQuantitySwapTyped(
            new List<WarehouseBatchQuantityEntry>
            {
                new("potion", 3),
                new("gem", 2),
            },
            new List<WarehouseBatchQuantityEntry>()
        );

        _test.False(result.Allowed, "quantity commit should reject the whole batch when one item is insufficient.");
        _test.Eq(result.ErrorCode, "warehouse_missing_item", "atomic quantity failure should return stable missing-item error.");
        _test.Eq(service.CountItem("potion"), 5, "atomic failure must restore earlier sufficient withdrawals.");
        _test.Eq(service.CountItem("gem"), 1, "atomic failure must preserve the insufficient item quantity.");
    }

    private void TestDepositQuantityPreviewAndCommit()
    {
        PartyState partyState = BuildPartyState(capacity: 3);
        PartyWarehouseService service = BuildService(partyState);

        WarehouseBatchSwapResult preview = service.PreviewBatchQuantitySwapTyped(
            new List<WarehouseBatchQuantityEntry>(),
            new List<WarehouseBatchQuantityEntry>
            {
                new("elixir", 4),
            }
        );

        _test.True(preview.Allowed, $"quantity deposit preview should allow explicit quantity. error={preview.ErrorCode}");
        _test.Eq(service.CountItem("elixir"), 0, "quantity deposit preview must not mutate state.");

        WarehouseBatchSwapResult commit = service.CommitBatchQuantitySwapTyped(
            new List<WarehouseBatchQuantityEntry>(),
            new List<WarehouseBatchQuantityEntry>
            {
                new("elixir", 4),
            }
        );

        _test.True(commit.Allowed, $"quantity deposit commit should add explicit quantity. error={commit.ErrorCode}");
        _test.Eq(service.CountItem("elixir"), 4, "quantity deposit commit should add exact quantity.");
        _test.Eq(StackQuantities(partyState.warehouse_state, "elixir"), "3,1", "quantity deposit should reuse stack capacity behavior.");
    }

    private void TestCaptureAndRestoreReturnsExactQuantities()
    {
        PartyState partyState = BuildPartyState(capacity: 5);
        PartyWarehouseService service = BuildService(partyState);
        service.AddItemTyped("potion", 5);

        WarehouseState snapshot = service.CaptureWarehouseStateForTransaction();
        WarehouseBatchSwapResult commit = service.CommitBatchQuantitySwapTyped(
            new List<WarehouseBatchQuantityEntry>
            {
                new("potion", 2),
            },
            new List<WarehouseBatchQuantityEntry>
            {
                new("gem", 3),
            }
        );

        _test.True(commit.Allowed, $"quantity commit before restore should succeed. error={commit.ErrorCode}");
        _test.Eq(service.CountItem("potion"), 3, "pre-restore commit should change source quantity.");
        _test.Eq(service.CountItem("gem"), 3, "pre-restore commit should add deposit quantity.");

        service.RestoreWarehouseStateForTransaction(snapshot);

        _test.Eq(service.CountItem("potion"), 5, "restore should return withdrawn item to pre-commit quantity.");
        _test.Eq(service.CountItem("gem"), 0, "restore should remove committed deposit quantity.");
        _test.Eq(StackQuantities(partyState.warehouse_state, "potion"), "5", "restore should return exact original stack quantities.");
    }

    private void TestInvalidQuantityEntriesDoNotMutate()
    {
        PartyState partyState = BuildPartyState(capacity: 4);
        PartyWarehouseService service = BuildService(partyState);
        service.AddItemTyped("potion", 5);

        WarehouseBatchSwapResult invalidQuantity = service.CommitBatchQuantitySwapTyped(
            new List<WarehouseBatchQuantityEntry>
            {
                new("potion", 0),
            },
            new List<WarehouseBatchQuantityEntry>()
        );
        _test.False(invalidQuantity.Allowed, "zero quantity entry should be rejected.");
        _test.Eq(invalidQuantity.ErrorCode, "invalid_batch_quantity_entry", "zero quantity should return stable invalid-entry error.");
        _test.Eq(service.CountItem("potion"), 5, "zero quantity entry must not mutate state.");

        WarehouseBatchSwapResult emptyItem = service.CommitBatchQuantitySwapTyped(
            new List<WarehouseBatchQuantityEntry>
            {
                new("", 1),
            },
            new List<WarehouseBatchQuantityEntry>()
        );
        _test.False(emptyItem.Allowed, "empty item id entry should be rejected.");
        _test.Eq(emptyItem.ErrorCode, "invalid_batch_quantity_entry", "empty item id should return stable invalid-entry error.");
        _test.Eq(service.CountItem("potion"), 5, "empty item id entry must not mutate state.");
    }

    private void TestEquipmentQuantityEntryIsBlocked()
    {
        PartyState partyState = BuildPartyState(capacity: 4);
        PartyWarehouseService service = BuildService(partyState);

        WarehouseBatchSwapResult result = service.CommitBatchQuantitySwapTyped(
            new List<WarehouseBatchQuantityEntry>(),
            new List<WarehouseBatchQuantityEntry>
            {
                new("iron_sword", 1),
            }
        );

        _test.False(result.Allowed, "quantity batch should block equipment entries.");
        _test.Eq(result.ErrorCode, "warehouse_quantity_equipment_unsupported", "equipment quantity batch should return stable unsupported-equipment error.");
        _test.Eq(service.CountItem("iron_sword"), 0, "equipment quantity batch must not create arbitrary equipment instances.");
    }

    private static PartyWarehouseService BuildService(PartyState partyState)
    {
        PartyWarehouseService service = new();
        service.Setup(partyState, BuildItemDefIndex());
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

    private static Dictionary<StringName, ItemDef> BuildItemDefIndex() =>
        new()
        {
            ["potion"] = BuildStackItem("potion", 99),
            ["herb"] = BuildStackItem("herb", 2),
            ["gem"] = BuildStackItem("gem", 99),
            ["elixir"] = BuildStackItem("elixir", 3),
            ["iron_sword"] = new ItemDef
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

    private static ItemDef BuildStackItem(StringName itemId, int maxStack) =>
        new()
        {
            item_id = itemId,
            display_name = itemId.ToString(),
            CategoryKind = ItemCategoryKind.Misc,
            is_stackable = true,
            max_stack = maxStack,
        };

    private static string StackQuantities(WarehouseState warehouseState, StringName itemId)
    {
        var parts = new List<string>();
        foreach (WarehouseStackState stack in warehouseState.GetNonEmptyStacksTyped())
        {
            if (stack.item_id == itemId)
                parts.Add(stack.quantity.ToString());
        }
        return string.Join(",", parts);
    }
}
