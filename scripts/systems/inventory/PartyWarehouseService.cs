using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class PartyWarehouseService : RefCounted
{
    private static readonly StringName StorageSpaceAttributeId = "storage_space";

    internal sealed class WarehouseBatchItemEntry
    {
        public StringName ItemId { get; init; } = "";
        public StringName InstanceId { get; init; } = "";
        public EquipmentInstanceState EquipmentInstance { get; init; }

        public bool HasEquipmentInstance => EquipmentInstance != null;
    }

    internal sealed class WarehouseBatchSwapResult
    {
        public readonly bool Allowed;
        public readonly string ErrorCode;
        public readonly StringName BlockedItemId;
        public readonly StringName BlockedInstanceId;

        private WarehouseBatchSwapResult(
            bool allowed,
            string errorCode,
            StringName blockedItemId,
            StringName blockedInstanceId)
        {
            Allowed = allowed;
            ErrorCode = errorCode ?? "";
            BlockedItemId = ProgressionDataUtils.to_string_name(blockedItemId);
            BlockedInstanceId = ProgressionDataUtils.to_string_name(blockedInstanceId);
        }

        public Godot.Collections.Dictionary ToDictionary() =>
            new()
            {
                { "allowed", Allowed },
                { "error_code", ErrorCode },
                { "blocked_item_id", BlockedItemId.ToString() },
                { "blocked_instance_id", BlockedInstanceId.ToString() },
            };

        public static WarehouseBatchSwapResult Success() => new(true, "", "", "");

        public static WarehouseBatchSwapResult Blocked(
            string errorCode,
            StringName blockedItemId = default,
            StringName blockedInstanceId = default) =>
            new(false, errorCode, blockedItemId, blockedInstanceId);
    }

    internal sealed class WarehouseAddItemResult
    {
        public StringName ItemId { get; init; } = "";
        public int RequestedQuantity { get; init; }
        public int AddedQuantity { get; init; }
        public int RemainingQuantity { get; init; }
        public int UsedSlotsBefore { get; init; }
        public int UsedSlotsAfter { get; init; }
        public int FreeSlotsAfter { get; init; }
        public int CreatedStackCount { get; init; }
        public int FilledExistingQuantity { get; init; }
        public bool IsOverCapacity { get; init; }
        public bool ItemFound { get; init; }
        public bool IsEquipment { get; init; }
        public List<string> AllocatedEquipmentInstanceIds { get; init; } =
            new();

        public Godot.Collections.Dictionary ToDictionary()
        {
            var result = new Godot.Collections.Dictionary
            {
                { "item_id", ItemId.ToString() },
                { "requested_quantity", RequestedQuantity },
                { "added_quantity", AddedQuantity },
                { "remaining_quantity", RemainingQuantity },
                { "used_slots_before", UsedSlotsBefore },
                { "used_slots_after", UsedSlotsAfter },
                { "free_slots_after", FreeSlotsAfter },
                { "created_stack_count", CreatedStackCount },
                { "filled_existing_quantity", FilledExistingQuantity },
                { "is_over_capacity", IsOverCapacity },
                { "item_found", ItemFound },
            };
            if (IsEquipment)
            {
                result["is_equipment"] = true;
                result["allocated_equipment_instance_ids"] = new Godot.Collections.Array<string>(
                    AllocatedEquipmentInstanceIds
                );
            }
            return result;
        }
    }

    internal sealed class WarehouseRemoveItemResult
    {
        public StringName ItemId { get; init; } = "";
        public StringName InstanceId { get; init; } = "";
        public bool IncludeInstanceId { get; init; }
        public int RequestedQuantity { get; init; }
        public int RemovedQuantity { get; init; }
        public int RemainingQuantity { get; init; }
        public int UsedSlotsBefore { get; init; }
        public int UsedSlotsAfter { get; init; }
        public int FreeSlotsAfter { get; init; }
        public bool IsOverCapacity { get; init; }
        public string ErrorCode { get; init; } = "";

        public Godot.Collections.Dictionary ToDictionary()
        {
            var result = new Godot.Collections.Dictionary
            {
                { "item_id", ItemId.ToString() },
                { "requested_quantity", RequestedQuantity },
                { "removed_quantity", RemovedQuantity },
                { "remaining_quantity", RemainingQuantity },
                { "used_slots_before", UsedSlotsBefore },
                { "used_slots_after", UsedSlotsAfter },
                { "free_slots_after", FreeSlotsAfter },
                { "is_over_capacity", IsOverCapacity },
                { "error_code", ErrorCode },
            };
            if (IncludeInstanceId)
                result["instance_id"] = InstanceId.ToString();
            return result;
        }

        public WarehouseRemoveItemResult WithError(string errorCode) =>
            new()
            {
                ItemId = ItemId,
                InstanceId = InstanceId,
                IncludeInstanceId = IncludeInstanceId,
                RequestedQuantity = RequestedQuantity,
                RemovedQuantity = RemovedQuantity,
                RemainingQuantity = RemainingQuantity,
                UsedSlotsBefore = UsedSlotsBefore,
                UsedSlotsAfter = UsedSlotsAfter,
                FreeSlotsAfter = FreeSlotsAfter,
                IsOverCapacity = IsOverCapacity,
                ErrorCode = errorCode ?? "",
            };
    }

    public static StringName STORAGE_SPACE_ATTRIBUTE_ID() => StorageSpaceAttributeId;

    private PartyState _party_state = new();
    private Dictionary<StringName, ItemDef> _item_defs = new();
    private WarehouseState _party_backpack_view;
    private Func<StringName> _equipment_instance_id_allocator;
    private int _local_equipment_instance_serial = 1;

    public void setup(
        PartyState partyState,
        Godot.Collections.Dictionary itemDefs = null,
        Func<StringName> equipmentInstanceIdAllocator = null)
    {
        _party_state = partyState ?? new PartyState();
        _item_defs = BuildItemDefIndex(itemDefs);
        _party_backpack_view = null;
        _equipment_instance_id_allocator = equipmentInstanceIdAllocator;
    }

    public void setup(
        PartyState partyState,
        Godot.Collections.Dictionary itemDefs,
        Callable equipmentInstanceIdAllocator) =>
        setup(partyState, itemDefs, BuildCallableAllocator(equipmentInstanceIdAllocator));

    public void setup(PartyState partyState, Godot.Collections.Dictionary itemDefs) =>
        setup(partyState, itemDefs, default(Func<StringName>));

    public void setup(PartyState partyState) =>
        setup(partyState, null, default(Func<StringName>));

    public void setup_party_backpack_view(
        PartyState partyState,
        WarehouseState partyBackpackView,
        Godot.Collections.Dictionary itemDefs = null,
        Func<StringName> equipmentInstanceIdAllocator = null)
    {
        _party_state = partyState ?? new PartyState();
        _item_defs = BuildItemDefIndex(itemDefs);
        _party_backpack_view = partyBackpackView ?? new WarehouseState();
        _equipment_instance_id_allocator = equipmentInstanceIdAllocator;
    }

    public void setup_party_backpack_view(
        PartyState partyState,
        WarehouseState partyBackpackView,
        Godot.Collections.Dictionary itemDefs) =>
        setup_party_backpack_view(partyState, partyBackpackView, itemDefs, default);

    private static Func<StringName> BuildCallableAllocator(Callable equipmentInstanceIdAllocator)
    {
        return () => ProgressionDataUtils.to_string_name(equipmentInstanceIdAllocator.Call());
    }

    public int get_total_capacity()
    {
        if (_party_state == null)
            return 0;

        int totalCapacity = 0;
        foreach (PartyMemberState memberState in _party_state.get_member_states())
        {
            var unitBaseAttributes = memberState?.progression?.unit_base_attributes;
            if (unitBaseAttributes == null)
                continue;
            totalCapacity += Mathf.Max(unitBaseAttributes.get_attribute_value(StorageSpaceAttributeId), 0);
        }
        return Mathf.Max(totalCapacity, 0);
    }

    public int get_used_slots()
    {
        var warehouseState = _get_warehouse_state();
        return warehouseState.GetNonEmptyStacksTyped().Count
            + warehouseState.GetNonEmptyEquipmentInstancesTyped().Count;
    }

    public int get_free_slots() => Mathf.Max(get_total_capacity() - get_used_slots(), 0);

    public bool is_over_capacity() => get_used_slots() > get_total_capacity();

    public int count_item(StringName itemId)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        if (normalizedItemId == "")
            return 0;

        int totalQuantity = 0;
        var warehouseState = _get_warehouse_state();
        foreach (var stack in warehouseState.GetNonEmptyStacksTyped())
        {
            if (stack.item_id != normalizedItemId)
                continue;
            totalQuantity += Mathf.Max(stack.quantity, 0);
        }
        foreach (var instance in warehouseState.GetNonEmptyEquipmentInstancesTyped())
        {
            if (instance.item_id == normalizedItemId)
                totalQuantity += 1;
        }
        return totalQuantity;
    }

    public Godot.Collections.Array<WarehouseStackState> get_stacks() =>
        new Godot.Collections.Array<WarehouseStackState>(
            _get_warehouse_state().duplicate_state().GetStacksTyped()
        );

    public IReadOnlyList<WarehouseInventoryEntry> GetInventoryEntriesTyped()
    {
        var warehouseState = _get_warehouse_state().duplicate_state();
        var entries = new List<WarehouseInventoryEntry>();

        foreach (var stack in warehouseState.GetNonEmptyStacksTyped())
        {
            if (stack == null || stack.is_empty())
                continue;
            entries.Add(_build_inventory_entry_typed(stack.item_id, stack.quantity, "stack"));
        }

        var equipmentEntries = new List<WarehouseInventoryEntry>();
        foreach (var instance in warehouseState.GetNonEmptyEquipmentInstancesTyped())
        {
            if (instance == null || instance.item_id == "")
                continue;
            equipmentEntries.Add(_build_inventory_entry_typed(instance.item_id, 1, "instance", instance));
        }
        equipmentEntries.Sort((a, b) =>
            string.CompareOrdinal(
                $"{a.ItemId}:{a.InstanceId}",
                $"{b.ItemId}:{b.InstanceId}"));

        foreach (var entry in equipmentEntries)
            entries.Add(entry);

        return entries;
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> get_inventory_entries()
    {
        var entries = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var entry in GetInventoryEntriesTyped())
            entries.Add(entry.ToDictionary());
        return entries;
    }

    public ItemDef get_item_def(StringName itemId)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        return normalizedItemId != "" && _item_defs.TryGetValue(normalizedItemId, out var itemDef)
            ? itemDef
            : null;
    }

    public Godot.Collections.Dictionary preview_add_item(StringName itemId, int quantity) =>
        PreviewAddItemTyped(itemId, quantity).ToDictionary();

    internal WarehouseAddItemResult PreviewAddItemTyped(StringName itemId, int quantity) =>
        _process_add(itemId, quantity, false, false);

    public Godot.Collections.Dictionary add_item(StringName itemId, int quantity) =>
        AddItemTyped(itemId, quantity).ToDictionary();

    internal WarehouseAddItemResult AddItemTyped(StringName itemId, int quantity) =>
        _process_add(itemId, quantity, true, true);

    public Godot.Collections.Dictionary remove_item(StringName itemId, int quantity) =>
        RemoveItemTyped(itemId, quantity).ToDictionary();

    internal WarehouseRemoveItemResult RemoveItemTyped(StringName itemId, int quantity)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        int requestedQuantity = Mathf.Max(quantity, 0);
        var warehouseState = _ensure_warehouse_state();
        _compact_state(warehouseState);
        int usedSlotsBefore = get_used_slots();

        if (normalizedItemId == "" || requestedQuantity <= 0)
            return _build_remove_item_result(normalizedItemId, requestedQuantity, 0, requestedQuantity, usedSlotsBefore, usedSlotsBefore, "");

        int remainingQuantity = requestedQuantity;
        var itemDef = get_item_def(normalizedItemId);

        if (itemDef != null && itemDef.is_equipment())
        {
            var matchingIndexes = _find_equipment_instance_indexes_by_item(warehouseState, normalizedItemId);
            if (requestedQuantity == 1 && matchingIndexes.Count == 1)
            {
                warehouseState.RemoveEquipmentInstanceAt(matchingIndexes[0]);
                remainingQuantity = 0;
            }
            else
            {
                int usedSlotsAfterReject = get_used_slots();
                return _build_remove_item_result(
                    normalizedItemId,
                    requestedQuantity,
                    0,
                    requestedQuantity,
                    usedSlotsBefore,
                    usedSlotsAfterReject,
                    "equipment_instance_id_required");
            }
        }
        else
        {
            for (int index = warehouseState.GetStacksTyped().Count - 1; index >= 0 && remainingQuantity > 0; index--)
            {
                var stack = warehouseState.GetStackAt(index);
                if (stack == null || stack.item_id != normalizedItemId)
                    continue;

                int removedQuantity = Mathf.Min(Mathf.Max(stack.quantity, 0), remainingQuantity);
                stack.quantity -= removedQuantity;
                remainingQuantity -= removedQuantity;
                if (stack.quantity <= 0)
                    warehouseState.RemoveStackAt(index);
            }
        }

        _compact_state(warehouseState);
        int usedSlotsAfter = get_used_slots();
        return _build_remove_item_result(
            normalizedItemId,
            requestedQuantity,
            requestedQuantity - remainingQuantity,
            remainingQuantity,
            usedSlotsBefore,
            usedSlotsAfter,
            "");
    }

    public Godot.Collections.Dictionary preview_batch_swap(
        Godot.Collections.Array<StringName> itemsToWithdraw,
        Godot.Collections.Array<StringName> itemsToDeposit) =>
        _run_batch_swap_transaction(
            BuildBatchItemEntries(itemsToWithdraw),
            BuildBatchItemEntries(itemsToDeposit),
            false
        );

    internal WarehouseBatchSwapResult PreviewBatchSwapTyped(
        Godot.Collections.Array<StringName> itemsToWithdraw,
        Godot.Collections.Array<StringName> itemsToDeposit) =>
        _run_batch_swap_transaction_typed(
            BuildBatchItemEntries(itemsToWithdraw),
            BuildBatchItemEntries(itemsToDeposit),
            false
        );

    internal WarehouseBatchSwapResult PreviewBatchSwapTyped(
        IReadOnlyList<StringName> itemsToWithdraw,
        IReadOnlyList<StringName> itemsToDeposit) =>
        _run_batch_swap_transaction_typed(
            BuildBatchItemEntries(itemsToWithdraw),
            BuildBatchItemEntries(itemsToDeposit),
            false
        );

    public Godot.Collections.Dictionary commit_batch_swap(
        Godot.Collections.Array<StringName> itemsToWithdraw,
        Godot.Collections.Array<StringName> itemsToDeposit) =>
        _run_batch_swap_transaction(
            BuildBatchItemEntries(itemsToWithdraw),
            BuildBatchItemEntries(itemsToDeposit),
            true
        );

    internal WarehouseBatchSwapResult CommitBatchSwapTyped(
        Godot.Collections.Array<StringName> itemsToWithdraw,
        Godot.Collections.Array<StringName> itemsToDeposit) =>
        _run_batch_swap_transaction_typed(
            BuildBatchItemEntries(itemsToWithdraw),
            BuildBatchItemEntries(itemsToDeposit),
            true
        );

    internal WarehouseBatchSwapResult CommitBatchSwapTyped(
        IReadOnlyList<StringName> itemsToWithdraw,
        IReadOnlyList<StringName> itemsToDeposit) =>
        _run_batch_swap_transaction_typed(
            BuildBatchItemEntries(itemsToWithdraw),
            BuildBatchItemEntries(itemsToDeposit),
            true
        );

    public Godot.Collections.Dictionary preview_batch_swap_entries(
        Godot.Collections.Array itemsToWithdraw,
        Godot.Collections.Array itemsToDeposit) =>
        _run_batch_swap_transaction(
            ParseBatchItemEntries(itemsToWithdraw),
            ParseBatchItemEntries(itemsToDeposit),
            false
        );

    internal WarehouseBatchSwapResult PreviewBatchSwapEntriesTyped(
        Godot.Collections.Array itemsToWithdraw,
        Godot.Collections.Array itemsToDeposit) =>
        _run_batch_swap_transaction_typed(
            ParseBatchItemEntries(itemsToWithdraw),
            ParseBatchItemEntries(itemsToDeposit),
            false
        );

    internal WarehouseBatchSwapResult PreviewBatchSwapEntriesTyped(
        IReadOnlyList<WarehouseBatchItemEntry> itemsToWithdraw,
        IReadOnlyList<WarehouseBatchItemEntry> itemsToDeposit) =>
        _run_batch_swap_transaction_typed(itemsToWithdraw, itemsToDeposit, false);

    public Godot.Collections.Dictionary commit_batch_swap_entries(
        Godot.Collections.Array itemsToWithdraw,
        Godot.Collections.Array itemsToDeposit) =>
        _run_batch_swap_transaction(
            ParseBatchItemEntries(itemsToWithdraw),
            ParseBatchItemEntries(itemsToDeposit),
            true
        );

    internal WarehouseBatchSwapResult CommitBatchSwapEntriesTyped(
        Godot.Collections.Array itemsToWithdraw,
        Godot.Collections.Array itemsToDeposit) =>
        _run_batch_swap_transaction_typed(
            ParseBatchItemEntries(itemsToWithdraw),
            ParseBatchItemEntries(itemsToDeposit),
            true
        );

    public EquipmentInstanceState get_equipment_instance_by_id(
        StringName instanceId,
        StringName expectedItemId = default)
    {
        var normalizedInstanceId = ProgressionDataUtils.to_string_name(instanceId);
        var normalizedItemId = ProgressionDataUtils.to_string_name(expectedItemId);
        if (normalizedInstanceId == "")
            return null;

        foreach (var instance in _get_warehouse_state().GetNonEmptyEquipmentInstancesTyped())
        {
            if (instance.instance_id != normalizedInstanceId)
                continue;
            if (normalizedItemId != "" && instance.item_id != normalizedItemId)
                return null;
            return instance.duplicate_state();
        }
        return null;
    }

    public bool has_equipment_instance(StringName instanceId, StringName expectedItemId = default) =>
        get_equipment_instance_by_id(instanceId, expectedItemId) != null;

    public EquipmentInstanceState take_equipment_instance_by_item(StringName itemId)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        var warehouseState = _ensure_warehouse_state();
        var matchingIndexes = _find_equipment_instance_indexes_by_item(warehouseState, normalizedItemId);
        if (matchingIndexes.Count != 1)
            return null;

        int index = matchingIndexes[0];
        return warehouseState.RemoveEquipmentInstanceAt(index);
    }

    public EquipmentInstanceState take_equipment_instance_by_instance_id(
        StringName instanceId,
        StringName expectedItemId = default)
    {
        var normalizedInstanceId = ProgressionDataUtils.to_string_name(instanceId);
        var normalizedItemId = ProgressionDataUtils.to_string_name(expectedItemId);
        if (normalizedInstanceId == "")
            return null;

        var warehouseState = _ensure_warehouse_state();
        IReadOnlyList<EquipmentInstanceState> equipmentInstances =
            warehouseState.GetEquipmentInstancesTyped();
        for (int index = 0; index < equipmentInstances.Count; index++)
        {
            var instance = equipmentInstances[index];
            if (instance == null || instance.instance_id != normalizedInstanceId)
                continue;
            if (normalizedItemId != "" && instance.item_id != normalizedItemId)
                return null;

            return warehouseState.RemoveEquipmentInstanceAt(index);
        }
        return null;
    }

    public Godot.Collections.Dictionary remove_equipment_instance(
        StringName itemId,
        StringName instanceId) =>
        RemoveEquipmentInstanceTyped(itemId, instanceId).ToDictionary();

    internal WarehouseRemoveItemResult RemoveEquipmentInstanceTyped(
        StringName itemId,
        StringName instanceId)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        var normalizedInstanceId = ProgressionDataUtils.to_string_name(instanceId);
        var warehouseState = _ensure_warehouse_state();
        _compact_state(warehouseState);
        int usedSlotsBefore = get_used_slots();
        var itemDef = get_item_def(normalizedItemId);

        if (normalizedItemId == "" || itemDef == null)
            return _build_remove_instance_result(normalizedItemId, normalizedInstanceId, 0, 1, usedSlotsBefore, usedSlotsBefore).WithError("item_not_found");
        if (!itemDef.is_equipment())
            return _build_remove_instance_result(normalizedItemId, normalizedInstanceId, 0, 1, usedSlotsBefore, usedSlotsBefore).WithError("item_not_equipment");
        if (normalizedInstanceId == "")
            return _build_remove_instance_result(normalizedItemId, normalizedInstanceId, 0, 1, usedSlotsBefore, usedSlotsBefore).WithError("equipment_instance_id_required");

        bool matchedAnyInstance = false;
        foreach (var instance in warehouseState.GetNonEmptyEquipmentInstancesTyped())
        {
            if (instance.instance_id != normalizedInstanceId)
                continue;

            matchedAnyInstance = true;
            if (instance.item_id != normalizedItemId)
                return _build_remove_instance_result(normalizedItemId, normalizedInstanceId, 0, 1, usedSlotsBefore, usedSlotsBefore).WithError("equipment_instance_item_mismatch");
            break;
        }

        if (!matchedAnyInstance)
            return _build_remove_instance_result(normalizedItemId, normalizedInstanceId, 0, 1, usedSlotsBefore, usedSlotsBefore).WithError("warehouse_missing_instance");

        var removedInstance = take_equipment_instance_by_instance_id(normalizedInstanceId, normalizedItemId);
        if (removedInstance == null)
            return _build_remove_instance_result(normalizedItemId, normalizedInstanceId, 0, 1, usedSlotsBefore, usedSlotsBefore).WithError("warehouse_missing_instance");

        _compact_state(warehouseState);
        int usedSlotsAfter = get_used_slots();
        return _build_remove_instance_result(normalizedItemId, normalizedInstanceId, 1, 0, usedSlotsBefore, usedSlotsAfter);
    }

    public Godot.Collections.Dictionary add_equipment_instance(
        EquipmentInstanceState instance,
        bool forceNewInstanceId = false) =>
        AddEquipmentInstanceTyped(instance, forceNewInstanceId).ToDictionary();

    internal WarehouseAddItemResult AddEquipmentInstanceTyped(
        EquipmentInstanceState instance,
        bool forceNewInstanceId = false)
    {
        var warehouseState = _ensure_warehouse_state();
        _compact_state(warehouseState);
        int usedSlotsBefore = get_used_slots();
        var itemId = ProgressionDataUtils.to_string_name(instance?.item_id ?? new StringName(""));
        var itemDef = get_item_def(itemId);
        bool itemFound = itemDef != null;
        bool isEquipment = itemDef != null && itemDef.is_equipment();

        if (instance == null || itemId == "" || itemDef == null || !itemDef.is_equipment())
            return new WarehouseAddItemResult
            {
                ItemId = itemId,
                RequestedQuantity = 1,
                AddedQuantity = 0,
                RemainingQuantity = 1,
                UsedSlotsBefore = usedSlotsBefore,
                UsedSlotsAfter = usedSlotsBefore,
                FreeSlotsAfter = Mathf.Max(get_total_capacity() - usedSlotsBefore, 0),
                IsOverCapacity = usedSlotsBefore > get_total_capacity(),
                ItemFound = itemFound,
                IsEquipment = isEquipment,
            };
        if (get_total_capacity() - usedSlotsBefore <= 0)
            return new WarehouseAddItemResult
            {
                ItemId = itemId,
                RequestedQuantity = 1,
                AddedQuantity = 0,
                RemainingQuantity = 1,
                UsedSlotsBefore = usedSlotsBefore,
                UsedSlotsAfter = usedSlotsBefore,
                FreeSlotsAfter = Mathf.Max(get_total_capacity() - usedSlotsBefore, 0),
                IsOverCapacity = usedSlotsBefore > get_total_capacity(),
                ItemFound = itemFound,
                IsEquipment = isEquipment,
            };

        var allocatedInstanceId = new StringName("");
        if (forceNewInstanceId || instance.instance_id == "")
        {
            allocatedInstanceId = _allocate_equipment_instance_id(warehouseState);
            instance.instance_id = allocatedInstanceId;
            if (allocatedInstanceId == "")
                return new WarehouseAddItemResult
                {
                    ItemId = itemId,
                    RequestedQuantity = 1,
                    AddedQuantity = 0,
                    RemainingQuantity = 1,
                    UsedSlotsBefore = usedSlotsBefore,
                    UsedSlotsAfter = usedSlotsBefore,
                    FreeSlotsAfter = Mathf.Max(get_total_capacity() - usedSlotsBefore, 0),
                    IsOverCapacity = usedSlotsBefore > get_total_capacity(),
                    ItemFound = itemFound,
                    IsEquipment = isEquipment,
                };
        }

        warehouseState.AddEquipmentInstance(instance);
        _compact_state(warehouseState);
        int usedSlotsAfter = get_used_slots();
        var allocatedInstanceIds = allocatedInstanceId != ""
            ? new List<string> { allocatedInstanceId.ToString() }
            : new List<string>();
        return new WarehouseAddItemResult
        {
            ItemId = itemId,
            RequestedQuantity = 1,
            AddedQuantity = 1,
            RemainingQuantity = 0,
            UsedSlotsBefore = usedSlotsBefore,
            UsedSlotsAfter = usedSlotsAfter,
            FreeSlotsAfter = Mathf.Max(get_total_capacity() - usedSlotsAfter, 0),
            IsOverCapacity = usedSlotsAfter > get_total_capacity(),
            ItemFound = itemFound,
            IsEquipment = isEquipment,
            AllocatedEquipmentInstanceIds = allocatedInstanceIds,
        };
    }

    public Godot.Collections.Dictionary add_equipment_instance(EquipmentInstanceState instance) =>
        AddEquipmentInstanceTyped(instance, false).ToDictionary();

    public bool deposit_equipment_instance(EquipmentInstanceState instance)
    {
        if (instance == null)
            return false;

        var warehouseState = _ensure_warehouse_state();
        if (instance.instance_id == "")
        {
            instance.instance_id = _allocate_equipment_instance_id(warehouseState);
            if (instance.instance_id == "")
                return false;
        }
        warehouseState.AddEquipmentInstance(instance);
        return true;
    }

    private WarehouseBatchSwapResult _execute_batch_swap(
        IReadOnlyList<WarehouseBatchItemEntry> itemsToWithdraw,
        IReadOnlyList<WarehouseBatchItemEntry> itemsToDeposit,
        bool consumeAllocator)
    {
        foreach (var withdrawEntry in itemsToWithdraw)
        {
            var itemId = withdrawEntry.ItemId;
            var instanceId = withdrawEntry.InstanceId;
            var itemDef = get_item_def(itemId);
            WarehouseRemoveItemResult result =
                itemDef != null && itemDef.is_equipment() && instanceId != ""
                    ? RemoveEquipmentInstanceTyped(itemId, instanceId)
                    : RemoveItemTyped(itemId, 1);

            if (result.RemovedQuantity <= 0)
            {
                var errorCode = result.ErrorCode;
                if (errorCode.Length == 0)
                    errorCode = "warehouse_missing_item";
                return WarehouseBatchSwapResult.Blocked(errorCode, itemId, instanceId);
            }
        }

        foreach (var depositEntry in itemsToDeposit)
        {
            var itemId = depositEntry.ItemId;
            var preview = PreviewAddItemTyped(itemId, 1);
            if (preview.RemainingQuantity > 0)
                return WarehouseBatchSwapResult.Blocked(
                    "warehouse_blocked_swap",
                    itemId,
                    depositEntry.InstanceId
                );

            if (depositEntry.HasEquipmentInstance)
            {
                var addInstanceResult = AddEquipmentInstanceTyped(
                    depositEntry.EquipmentInstance.duplicate_state(),
                    false
                );
                if (addInstanceResult.AddedQuantity <= 0)
                {
                    return WarehouseBatchSwapResult.Blocked(
                        "warehouse_blocked_swap",
                        itemId,
                        depositEntry.InstanceId
                    );
                }
            }
            else
            {
                _process_add(itemId, 1, true, consumeAllocator);
            }
        }

        return WarehouseBatchSwapResult.Success();
    }

    private Godot.Collections.Dictionary _run_batch_swap_transaction(
        IReadOnlyList<WarehouseBatchItemEntry> itemsToWithdraw,
        IReadOnlyList<WarehouseBatchItemEntry> itemsToDeposit,
        bool commitOnSuccess) =>
        _run_batch_swap_transaction_typed(
            itemsToWithdraw,
            itemsToDeposit,
            commitOnSuccess
        ).ToDictionary();

    private WarehouseBatchSwapResult _run_batch_swap_transaction_typed(
        IReadOnlyList<WarehouseBatchItemEntry> itemsToWithdraw,
        IReadOnlyList<WarehouseBatchItemEntry> itemsToDeposit,
        bool commitOnSuccess)
    {
        var baselineState = _get_warehouse_state().duplicate_state();
        _party_state ??= new PartyState();
        var originalState = _party_backpack_view ?? _party_state.warehouse_state;

        _set_transaction_warehouse_state(baselineState);
        var result = _execute_batch_swap(itemsToWithdraw, itemsToDeposit, commitOnSuccess);
        if (result.Allowed && commitOnSuccess)
        {
            if (_party_backpack_view != null)
            {
                _copy_warehouse_state(baselineState, originalState);
                _party_backpack_view = originalState;
            }
            return result;
        }

        _set_transaction_warehouse_state(originalState);
        return result;
    }

    private static List<WarehouseBatchItemEntry> BuildBatchItemEntries(
        IEnumerable<StringName> itemIds
    )
    {
        var result = new List<WarehouseBatchItemEntry>();
        if (itemIds == null)
            return result;
        foreach (var itemId in itemIds)
            result.Add(new WarehouseBatchItemEntry
            {
                ItemId = ProgressionDataUtils.to_string_name(itemId),
            });
        return result;
    }

    private static List<WarehouseBatchItemEntry> ParseBatchItemEntries(
        Godot.Collections.Array entries
    )
    {
        var result = new List<WarehouseBatchItemEntry>();
        if (entries == null)
            return result;
        foreach (object entryValue in entries)
            result.Add(ParseBatchItemEntry(entryValue));
        return result;
    }

    private static WarehouseBatchItemEntry ParseBatchItemEntry(object entryValue)
    {
        if (TryRawDictionary(entryValue, out var entryData))
            return ParseBatchItemEntry(entryData);
        return new WarehouseBatchItemEntry
        {
            ItemId = ProgressionDataUtils.to_string_name(entryValue),
        };
    }

    private static WarehouseBatchItemEntry ParseBatchItemEntry(Godot.Collections.Dictionary entry)
    {
        var equipmentInstance = ReadEquipmentInstance(entry, "equipment_instance");
        return new WarehouseBatchItemEntry
        {
            ItemId = ReadStringName(entry, "item_id"),
            InstanceId = ReadStringName(entry, "instance_id"),
            EquipmentInstance = equipmentInstance,
        };
    }

    private static EquipmentInstanceState ReadEquipmentInstance(
        Godot.Collections.Dictionary data,
        string key)
    {
        object value = ReadValue(data, key);
        if (TryAsEquipmentInstance(value, out var equipmentInstance))
            return equipmentInstance.duplicate_state();
        if (TryRawDictionary(value, out var equipmentData))
            return EquipmentInstanceState.from_dict(equipmentData);
        return null;
    }

    private static ItemDef ReadItemDef(object rawValue)
    {
        return TryAsItemDef(rawValue, out var itemDef) ? itemDef : null;
    }

    private static Dictionary<StringName, ItemDef> BuildItemDefIndex(
        Godot.Collections.Dictionary itemDefs)
    {
        var result = new Dictionary<StringName, ItemDef>();
        if (itemDefs == null)
            return result;

        foreach (var key in itemDefs.Keys)
        {
            StringName itemId = ProgressionDataUtils.to_string_name(key);
            if (itemId == "")
                continue;
            ItemDef itemDef = ReadItemDef(itemDefs[key]);
            if (itemDef != null)
                result[itemId] = itemDef;
        }
        return result;
    }

    private WarehouseAddItemResult _process_add(
        StringName itemId,
        int quantity,
        bool mutate,
        bool consumeAllocator)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        int requestedQuantity = Mathf.Max(quantity, 0);
        int usedSlotsBefore = get_used_slots();
        var itemDef = get_item_def(normalizedItemId);
        var targetState = mutate ? _ensure_warehouse_state() : _get_warehouse_state().duplicate_state();
        _compact_state(targetState);

        int currentUsed =
            targetState.GetNonEmptyStacksTyped().Count
            + targetState.GetNonEmptyEquipmentInstancesTyped().Count;
        bool itemFound = itemDef != null;
        bool isEquipment = itemDef != null && itemDef.is_equipment();

        if (normalizedItemId == "" || requestedQuantity <= 0 || itemDef == null)
            return new WarehouseAddItemResult
            {
                ItemId = normalizedItemId,
                RequestedQuantity = requestedQuantity,
                AddedQuantity = 0,
                RemainingQuantity = requestedQuantity,
                UsedSlotsBefore = usedSlotsBefore,
                UsedSlotsAfter = currentUsed,
                FreeSlotsAfter = Mathf.Max(get_total_capacity() - currentUsed, 0),
                CreatedStackCount = 0,
                FilledExistingQuantity = 0,
                IsOverCapacity = currentUsed > get_total_capacity(),
                ItemFound = itemFound,
                IsEquipment = isEquipment,
            };

        int remainingQuantity = requestedQuantity;
        int createdStackCount = 0;
        int filledExistingQuantity = 0;
        var allocatedInstanceIds = new List<string>();
        if (itemDef.is_equipment())
        {
            int availableNewSlots = Mathf.Max(get_total_capacity() - currentUsed, 0);
            while (remainingQuantity > 0 && availableNewSlots > 0)
            {
                var newInstance = _create_equipment_instance(normalizedItemId, targetState, consumeAllocator);
                if (newInstance.instance_id == "")
                    break;
                if (consumeAllocator)
                    allocatedInstanceIds.Add(newInstance.instance_id.ToString());
                targetState.AddEquipmentInstance(newInstance);
                remainingQuantity -= 1;
                availableNewSlots -= 1;
                createdStackCount += 1;
            }
        }
        else
        {
            int maxStack = itemDef.get_effective_max_stack();
            foreach (var stack in targetState.GetNonEmptyStacksTyped())
            {
                if (remainingQuantity <= 0)
                    break;
                if (stack == null || stack.item_id != normalizedItemId)
                    continue;
                if (stack.quantity >= maxStack)
                    continue;

                int acceptedQuantity = Mathf.Min(maxStack - stack.quantity, remainingQuantity);
                if (acceptedQuantity <= 0)
                    continue;

                stack.quantity += acceptedQuantity;
                remainingQuantity -= acceptedQuantity;
                filledExistingQuantity += acceptedQuantity;
            }

            int availableNewStacks = Mathf.Max(
                get_total_capacity()
                    - targetState.GetNonEmptyStacksTyped().Count
                    - targetState.GetNonEmptyEquipmentInstancesTyped().Count,
                0
            );
            while (remainingQuantity > 0 && availableNewStacks > 0)
            {
                var newStack = new WarehouseStackState
                {
                    item_id = normalizedItemId,
                    quantity = Mathf.Min(maxStack, remainingQuantity),
                };
                targetState.AddStack(newStack);
                remainingQuantity -= newStack.quantity;
                availableNewStacks -= 1;
                createdStackCount += 1;
            }

            _compact_state(targetState);
        }

        int usedSlotsAfter =
            targetState.GetNonEmptyStacksTyped().Count
            + targetState.GetNonEmptyEquipmentInstancesTyped().Count;
        return new WarehouseAddItemResult
        {
            ItemId = normalizedItemId,
            RequestedQuantity = requestedQuantity,
            AddedQuantity = requestedQuantity - remainingQuantity,
            RemainingQuantity = remainingQuantity,
            UsedSlotsBefore = usedSlotsBefore,
            UsedSlotsAfter = usedSlotsAfter,
            FreeSlotsAfter = Mathf.Max(get_total_capacity() - usedSlotsAfter, 0),
            CreatedStackCount = createdStackCount,
            FilledExistingQuantity = filledExistingQuantity,
            IsOverCapacity = usedSlotsAfter > get_total_capacity(),
            ItemFound = itemFound,
            IsEquipment = isEquipment,
            AllocatedEquipmentInstanceIds = allocatedInstanceIds,
        };
    }

    private EquipmentInstanceState _create_equipment_instance(
        StringName itemId,
        WarehouseState targetState,
        bool consumeAllocator)
    {
        var instance = EquipmentInstanceState.create_transient_instance(itemId);
        instance.instance_id = consumeAllocator
            ? _allocate_equipment_instance_id(targetState)
            : _allocate_preview_equipment_instance_id(targetState);
        return instance;
    }

    private StringName _allocate_equipment_instance_id(WarehouseState targetState = null)
    {
        if (_equipment_instance_id_allocator != null)
            return ProgressionDataUtils.to_string_name(_equipment_instance_id_allocator.Invoke());

        while (true)
        {
            var candidate = EquipmentInstanceState.format_instance_id(_local_equipment_instance_serial);
            _local_equipment_instance_serial += 1;
            if (!_equipment_instance_id_exists(candidate, targetState))
                return candidate;
        }
    }

    private StringName _allocate_preview_equipment_instance_id(WarehouseState targetState = null)
    {
        int serial = 1;
        while (true)
        {
            var candidate = EquipmentInstanceState.format_preview_instance_id(serial);
            serial += 1;
            if (!_equipment_instance_id_exists(candidate, targetState))
                return candidate;
        }
    }

    private bool _equipment_instance_id_exists(StringName instanceId, WarehouseState targetState = null)
    {
        var normalizedId = ProgressionDataUtils.to_string_name(instanceId);
        if (normalizedId == "")
            return false;

        var states = new System.Collections.Generic.List<WarehouseState>();
        if (targetState != null)
            states.Add(targetState);

        var currentState = _get_warehouse_state();
        if (currentState != null && currentState != targetState)
            states.Add(currentState);

        foreach (var state in states)
        {
            foreach (var instance in state.GetNonEmptyEquipmentInstancesTyped())
            {
                if (instance.instance_id == normalizedId)
                    return true;
            }
        }
        return false;
    }

    private static List<int> _find_equipment_instance_indexes_by_item(
        WarehouseState warehouseState,
        StringName itemId)
    {
        var result = new List<int>();
        if (warehouseState == null)
            return result;

        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        IReadOnlyList<EquipmentInstanceState> equipmentInstances =
            warehouseState.GetEquipmentInstancesTyped();
        for (int index = 0; index < equipmentInstances.Count; index++)
        {
            var instance = equipmentInstances[index];
            if (instance != null && instance.item_id == normalizedItemId)
                result.Add(index);
        }
        return result;
    }

    private WarehouseState _ensure_warehouse_state()
    {
        if (_party_backpack_view != null)
            return _party_backpack_view;

        _party_state ??= new PartyState();
        _party_state.warehouse_state ??= new WarehouseState();
        return _party_state.warehouse_state;
    }

    private WarehouseState _get_warehouse_state()
    {
        if (_party_backpack_view != null)
            return _party_backpack_view;
        if (_party_state == null)
            return new WarehouseState();
        return _party_state.warehouse_state ?? new WarehouseState();
    }

    private void _set_transaction_warehouse_state(WarehouseState warehouseState)
    {
        if (_party_backpack_view != null)
        {
            _party_backpack_view = warehouseState;
            return;
        }

        _party_state ??= new PartyState();
        _party_state.warehouse_state = warehouseState;
    }

    private static void _copy_warehouse_state(WarehouseState sourceState, WarehouseState targetState)
    {
        if (sourceState == null || targetState == null)
            return;

        var stacks = new List<WarehouseStackState>();
        foreach (var stack in sourceState.GetNonEmptyStacksTyped())
            stacks.Add(stack.duplicate_state());
        targetState.ReplaceStacks(stacks);

        var instances = new List<EquipmentInstanceState>();
        foreach (var instance in sourceState.GetNonEmptyEquipmentInstancesTyped())
            instances.Add(instance.duplicate_state());
        targetState.ReplaceEquipmentInstances(instances);
    }

    private static void _compact_state(WarehouseState warehouseState)
    {
        if (warehouseState == null)
            return;
        warehouseState.ReplaceStacks(warehouseState.GetNonEmptyStacksTyped());
        warehouseState.ReplaceEquipmentInstances(warehouseState.GetNonEmptyEquipmentInstancesTyped());
    }

    private WarehouseInventoryEntry _build_inventory_entry_typed(
        StringName itemId,
        int quantity,
        StringName storageMode,
        EquipmentInstanceState equipmentInstance = null)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        int resolvedQuantity = Mathf.Max(quantity, 0);
        var itemDef = get_item_def(normalizedItemId);
        var grantedSkillId = itemDef?.granted_skill_id ?? new StringName("");
        return new WarehouseInventoryEntry(
            normalizedItemId,
            itemDef,
            itemDef != null && itemDef.display_name.Length > 0
                ? itemDef.display_name
                : normalizedItemId.ToString(),
            itemDef?.description ?? "该物品定义缺失，当前仅保留存档中的 item_id 与数量。",
            itemDef?.icon ?? "",
            resolvedQuantity,
            count_item(normalizedItemId),
            itemDef?.is_stackable ?? resolvedQuantity > 1,
            itemDef?.get_effective_max_stack() ?? Mathf.Max(resolvedQuantity, 1),
            itemDef != null ? itemDef.get_item_category_normalized() : new StringName(""),
            itemDef != null && itemDef.is_skill_book(),
            grantedSkillId,
            storageMode,
            equipmentInstance?.instance_id ?? new StringName(""),
            equipmentInstance?.rarity ?? 0,
            equipmentInstance?.current_durability ?? 0,
            equipmentInstance != null
        );
    }

    private WarehouseRemoveItemResult _build_remove_item_result(
        StringName itemId,
        int requestedQuantity,
        int removedQuantity,
        int remainingQuantity,
        int usedSlotsBefore,
        int usedSlotsAfter,
        string errorCode)
    {
        return new WarehouseRemoveItemResult
        {
            ItemId = itemId,
            RequestedQuantity = requestedQuantity,
            RemovedQuantity = removedQuantity,
            RemainingQuantity = remainingQuantity,
            UsedSlotsBefore = usedSlotsBefore,
            UsedSlotsAfter = usedSlotsAfter,
            FreeSlotsAfter = Mathf.Max(get_total_capacity() - usedSlotsAfter, 0),
            IsOverCapacity = usedSlotsAfter > get_total_capacity(),
            ErrorCode = errorCode ?? "",
        };
    }

    private WarehouseRemoveItemResult _build_remove_instance_result(
        StringName itemId,
        StringName instanceId,
        int removedQuantity,
        int remainingQuantity,
        int usedSlotsBefore,
        int usedSlotsAfter)
    {
        return new WarehouseRemoveItemResult
        {
            ItemId = itemId,
            InstanceId = instanceId,
            IncludeInstanceId = true,
            RequestedQuantity = 1,
            RemovedQuantity = removedQuantity,
            RemainingQuantity = remainingQuantity,
            UsedSlotsBefore = usedSlotsBefore,
            UsedSlotsAfter = usedSlotsAfter,
            FreeSlotsAfter = Mathf.Max(get_total_capacity() - usedSlotsAfter, 0),
            IsOverCapacity = usedSlotsAfter > get_total_capacity(),
            ErrorCode = "",
        };
    }

    private static StringName ReadStringName(
        Godot.Collections.Dictionary data,
        string key,
        StringName fallback = default)
    {
        StringName value = ProgressionDataUtils.to_string_name(ReadValue(data, key));
        return value == "" ? fallback ?? new StringName("") : value;
    }

    private static string ReadString(
        Godot.Collections.Dictionary data,
        string key,
        string fallback = "")
    {
        StringName value = ProgressionDataUtils.to_string_name(ReadValue(data, key));
        return value == "" ? fallback : value.ToString();
    }

    private static object ReadValue(Godot.Collections.Dictionary data, string key)
    {
        if (data == null)
            return null;
        if (data.ContainsKey(key))
            return data[key];
        var stringNameKey = new StringName(key);
        if (data.ContainsKey(stringNameKey))
            return data[stringNameKey];
        return null;
    }

    private static bool TryAsItemDef(object rawValue, out ItemDef value)
    {
        if (rawValue is ItemDef itemDef)
        {
            value = itemDef;
            return true;
        }

        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.As<ItemDef>();
            return value != null;
        }
        catch
        {
        }

        value = null;
        return false;
    }

    private static bool TryAsEquipmentInstance(
        object rawValue,
        out EquipmentInstanceState value)
    {
        if (rawValue is EquipmentInstanceState equipmentInstance)
        {
            value = equipmentInstance;
            return true;
        }

        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.As<EquipmentInstanceState>();
            return value != null;
        }
        catch
        {
        }

        value = null;
        return false;
    }

    private static bool TryRawDictionary(
        object rawValue,
        out Godot.Collections.Dictionary value)
    {
        if (rawValue is Godot.Collections.Dictionary dictionary)
        {
            value = dictionary;
            return true;
        }

        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.AsGodotDictionary();
            return value != null;
        }
        catch
        {
        }

        value = null;
        return false;
    }
}
