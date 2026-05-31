using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class PartyWarehouseService : RefCounted
{
    private static readonly StringName StorageSpaceAttributeId = "storage_space";

    private sealed class WarehouseBatchItemEntry
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
        public Godot.Collections.Array<string> AllocatedEquipmentInstanceIds { get; init; } =
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
                result["allocated_equipment_instance_ids"] =
                    AllocatedEquipmentInstanceIds.Duplicate();
            }
            return result;
        }
    }

    public static StringName STORAGE_SPACE_ATTRIBUTE_ID() => StorageSpaceAttributeId;

    private PartyState _party_state = new();
    private Godot.Collections.Dictionary _item_defs = new();
    private WarehouseState _party_backpack_view;
    private Func<StringName> _equipment_instance_id_allocator;
    private int _local_equipment_instance_serial = 1;

    public void setup(
        PartyState partyState,
        Godot.Collections.Dictionary itemDefs = null,
        Func<StringName> equipmentInstanceIdAllocator = null)
    {
        _party_state = partyState ?? new PartyState();
        _item_defs = itemDefs ?? new Godot.Collections.Dictionary();
        _party_backpack_view = null;
        _equipment_instance_id_allocator = equipmentInstanceIdAllocator;
    }

    public void setup(PartyState partyState, Godot.Collections.Dictionary itemDefs) =>
        setup(partyState, itemDefs, default);

    public void setup(PartyState partyState) =>
        setup(partyState, null, default);

    public void setup_party_backpack_view(
        PartyState partyState,
        WarehouseState partyBackpackView,
        Godot.Collections.Dictionary itemDefs = null,
        Func<StringName> equipmentInstanceIdAllocator = null)
    {
        _party_state = partyState ?? new PartyState();
        _item_defs = itemDefs ?? new Godot.Collections.Dictionary();
        _party_backpack_view = partyBackpackView ?? new WarehouseState();
        _equipment_instance_id_allocator = equipmentInstanceIdAllocator;
    }

    public void setup_party_backpack_view(
        PartyState partyState,
        WarehouseState partyBackpackView,
        Godot.Collections.Dictionary itemDefs) =>
        setup_party_backpack_view(partyState, partyBackpackView, itemDefs, default);

    public int get_total_capacity()
    {
        if (_party_state == null)
            return 0;

        int totalCapacity = 0;
        foreach (var memberValue in _party_state.member_states.Values)
        {
            var memberState = memberValue.AsGodotObject() as PartyMemberState;
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
        return warehouseState.get_non_empty_stacks().Count + warehouseState.get_non_empty_instances().Count;
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
        foreach (var stack in warehouseState.get_non_empty_stacks())
        {
            if (stack.item_id != normalizedItemId)
                continue;
            totalQuantity += Mathf.Max(stack.quantity, 0);
        }
        foreach (var instance in warehouseState.get_non_empty_instances())
        {
            if (instance.item_id == normalizedItemId)
                totalQuantity += 1;
        }
        return totalQuantity;
    }

    public Godot.Collections.Array<WarehouseStackState> get_stacks() =>
        _get_warehouse_state().duplicate_state().stacks;

    public IReadOnlyList<WarehouseInventoryEntry> GetInventoryEntriesTyped()
    {
        var warehouseState = _get_warehouse_state().duplicate_state();
        var entries = new List<WarehouseInventoryEntry>();

        foreach (var stack in warehouseState.get_non_empty_stacks())
        {
            if (stack == null || stack.is_empty())
                continue;
            entries.Add(_build_inventory_entry_typed(stack.item_id, stack.quantity, "stack"));
        }

        var equipmentEntries = new List<WarehouseInventoryEntry>();
        foreach (var instance in warehouseState.get_non_empty_instances())
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
        if (_item_defs.ContainsKey(normalizedItemId))
            return _item_defs[normalizedItemId].AsGodotObject() as ItemDef;

        var stringKey = normalizedItemId.ToString();
        return _item_defs.ContainsKey(stringKey) ? _item_defs[stringKey].AsGodotObject() as ItemDef : null;
    }

    public Godot.Collections.Dictionary preview_add_item(StringName itemId, int quantity) =>
        PreviewAddItemTyped(itemId, quantity).ToDictionary();

    internal WarehouseAddItemResult PreviewAddItemTyped(StringName itemId, int quantity) =>
        _process_add(itemId, quantity, false, false);

    public Godot.Collections.Dictionary add_item(StringName itemId, int quantity) =>
        AddItemTyped(itemId, quantity).ToDictionary();

    internal WarehouseAddItemResult AddItemTyped(StringName itemId, int quantity) =>
        _process_add(itemId, quantity, true, true);

    public Godot.Collections.Dictionary remove_item(StringName itemId, int quantity)
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
                warehouseState.equipment_instances.RemoveAt(matchingIndexes[0]);
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
            for (int index = warehouseState.stacks.Count - 1; index >= 0 && remainingQuantity > 0; index--)
            {
                var stack = warehouseState.stacks[index];
                if (stack == null || stack.item_id != normalizedItemId)
                    continue;

                int removedQuantity = Mathf.Min(Mathf.Max(stack.quantity, 0), remainingQuantity);
                stack.quantity -= removedQuantity;
                remainingQuantity -= removedQuantity;
                if (stack.quantity <= 0)
                    warehouseState.stacks.RemoveAt(index);
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

        foreach (var instance in _get_warehouse_state().get_non_empty_instances())
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
        var instance = warehouseState.equipment_instances[index];
        warehouseState.equipment_instances.RemoveAt(index);
        return instance;
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
        for (int index = 0; index < warehouseState.equipment_instances.Count; index++)
        {
            var instance = warehouseState.equipment_instances[index];
            if (instance == null || instance.instance_id != normalizedInstanceId)
                continue;
            if (normalizedItemId != "" && instance.item_id != normalizedItemId)
                return null;

            warehouseState.equipment_instances.RemoveAt(index);
            return instance;
        }
        return null;
    }

    public Godot.Collections.Dictionary remove_equipment_instance(StringName itemId, StringName instanceId)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        var normalizedInstanceId = ProgressionDataUtils.to_string_name(instanceId);
        var warehouseState = _ensure_warehouse_state();
        _compact_state(warehouseState);
        int usedSlotsBefore = get_used_slots();
        var itemDef = get_item_def(normalizedItemId);

        if (normalizedItemId == "" || itemDef == null)
            return _with_error(_build_remove_instance_result(normalizedItemId, normalizedInstanceId, 0, 1, usedSlotsBefore, usedSlotsBefore), "item_not_found");
        if (!itemDef.is_equipment())
            return _with_error(_build_remove_instance_result(normalizedItemId, normalizedInstanceId, 0, 1, usedSlotsBefore, usedSlotsBefore), "item_not_equipment");
        if (normalizedInstanceId == "")
            return _with_error(_build_remove_instance_result(normalizedItemId, normalizedInstanceId, 0, 1, usedSlotsBefore, usedSlotsBefore), "equipment_instance_id_required");

        bool matchedAnyInstance = false;
        foreach (var instance in warehouseState.get_non_empty_instances())
        {
            if (instance.instance_id != normalizedInstanceId)
                continue;

            matchedAnyInstance = true;
            if (instance.item_id != normalizedItemId)
                return _with_error(_build_remove_instance_result(normalizedItemId, normalizedInstanceId, 0, 1, usedSlotsBefore, usedSlotsBefore), "equipment_instance_item_mismatch");
            break;
        }

        if (!matchedAnyInstance)
            return _with_error(_build_remove_instance_result(normalizedItemId, normalizedInstanceId, 0, 1, usedSlotsBefore, usedSlotsBefore), "warehouse_missing_instance");

        var removedInstance = take_equipment_instance_by_instance_id(normalizedInstanceId, normalizedItemId);
        if (removedInstance == null)
            return _with_error(_build_remove_instance_result(normalizedItemId, normalizedInstanceId, 0, 1, usedSlotsBefore, usedSlotsBefore), "warehouse_missing_instance");

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

        warehouseState.equipment_instances.Add(instance);
        _compact_state(warehouseState);
        int usedSlotsAfter = get_used_slots();
        var allocatedInstanceIds = allocatedInstanceId != ""
            ? new Godot.Collections.Array<string> { allocatedInstanceId.ToString() }
            : new Godot.Collections.Array<string>();
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
        warehouseState.equipment_instances.Add(instance);
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
            Godot.Collections.Dictionary result =
                itemDef != null && itemDef.is_equipment() && instanceId != ""
                    ? remove_equipment_instance(itemId, instanceId)
                    : remove_item(itemId, 1);

            if (result.ContainsKey("removed_quantity") && result["removed_quantity"].AsInt32() <= 0)
            {
                var errorCode = result.ContainsKey("error_code") ? result["error_code"].AsString() : "";
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
        Godot.Collections.Array<StringName> itemIds
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
        foreach (Variant entryValue in entries)
            result.Add(ParseBatchItemEntry(entryValue));
        return result;
    }

    private static WarehouseBatchItemEntry ParseBatchItemEntry(Variant entryValue)
    {
        if (entryValue.VariantType == Variant.Type.Dictionary)
            return ParseBatchItemEntry(entryValue.AsGodotDictionary());
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
        var value = ReadValue(data, key);
        if (value.VariantType == Variant.Type.Object)
            return (value.AsGodotObject() as EquipmentInstanceState)?.duplicate_state();
        if (value.VariantType == Variant.Type.Dictionary)
            return EquipmentInstanceState.from_dict(value.AsGodotDictionary());
        return null;
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

        int currentUsed = targetState.stacks.Count + targetState.get_non_empty_instances().Count;
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
        var allocatedInstanceIds = new Godot.Collections.Array<string>();
        if (itemDef.is_equipment())
        {
            int availableNewSlots = Mathf.Max(get_total_capacity() - targetState.stacks.Count - targetState.equipment_instances.Count, 0);
            while (remainingQuantity > 0 && availableNewSlots > 0)
            {
                var newInstance = _create_equipment_instance(normalizedItemId, targetState, consumeAllocator);
                if (newInstance.instance_id == "")
                    break;
                if (consumeAllocator)
                    allocatedInstanceIds.Add(newInstance.instance_id.ToString());
                targetState.equipment_instances.Add(newInstance);
                remainingQuantity -= 1;
                availableNewSlots -= 1;
                createdStackCount += 1;
            }
        }
        else
        {
            int maxStack = itemDef.get_effective_max_stack();
            foreach (var stack in targetState.stacks)
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

            int availableNewStacks = Mathf.Max(get_total_capacity() - targetState.stacks.Count - targetState.equipment_instances.Count, 0);
            while (remainingQuantity > 0 && availableNewStacks > 0)
            {
                var newStack = new WarehouseStackState
                {
                    item_id = normalizedItemId,
                    quantity = Mathf.Min(maxStack, remainingQuantity),
                };
                targetState.stacks.Add(newStack);
                remainingQuantity -= newStack.quantity;
                availableNewStacks -= 1;
                createdStackCount += 1;
            }

            _compact_state(targetState);
        }

        int usedSlotsAfter = targetState.stacks.Count + targetState.get_non_empty_instances().Count;
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
            foreach (var instance in state.get_non_empty_instances())
            {
                if (instance.instance_id == normalizedId)
                    return true;
            }
        }
        return false;
    }

    private static Godot.Collections.Array<int> _find_equipment_instance_indexes_by_item(
        WarehouseState warehouseState,
        StringName itemId)
    {
        var result = new Godot.Collections.Array<int>();
        if (warehouseState == null)
            return result;

        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        for (int index = 0; index < warehouseState.equipment_instances.Count; index++)
        {
            var instance = warehouseState.equipment_instances[index];
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

        targetState.stacks.Clear();
        targetState.equipment_instances.Clear();
        foreach (var stack in sourceState.get_non_empty_stacks())
            targetState.stacks.Add(stack.duplicate_state());
        foreach (var instance in sourceState.get_non_empty_instances())
            targetState.equipment_instances.Add(instance.duplicate_state());
    }

    private static void _compact_state(WarehouseState warehouseState)
    {
        if (warehouseState == null)
            return;
        warehouseState.stacks = warehouseState.get_non_empty_stacks();
        warehouseState.equipment_instances = warehouseState.get_non_empty_instances();
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

    private Godot.Collections.Dictionary _build_remove_item_result(
        StringName itemId,
        int requestedQuantity,
        int removedQuantity,
        int remainingQuantity,
        int usedSlotsBefore,
        int usedSlotsAfter,
        string errorCode)
    {
        return new Godot.Collections.Dictionary
        {
            { "item_id", itemId.ToString() },
            { "requested_quantity", requestedQuantity },
            { "removed_quantity", removedQuantity },
            { "remaining_quantity", remainingQuantity },
            { "used_slots_before", usedSlotsBefore },
            { "used_slots_after", usedSlotsAfter },
            { "free_slots_after", Mathf.Max(get_total_capacity() - usedSlotsAfter, 0) },
            { "is_over_capacity", usedSlotsAfter > get_total_capacity() },
            { "error_code", errorCode },
        };
    }

    private Godot.Collections.Dictionary _build_remove_instance_result(
        StringName itemId,
        StringName instanceId,
        int removedQuantity,
        int remainingQuantity,
        int usedSlotsBefore,
        int usedSlotsAfter)
    {
        return new Godot.Collections.Dictionary
        {
            { "item_id", itemId.ToString() },
            { "instance_id", instanceId.ToString() },
            { "requested_quantity", 1 },
            { "removed_quantity", removedQuantity },
            { "remaining_quantity", remainingQuantity },
            { "used_slots_before", usedSlotsBefore },
            { "used_slots_after", usedSlotsAfter },
            { "free_slots_after", Mathf.Max(get_total_capacity() - usedSlotsAfter, 0) },
            { "is_over_capacity", usedSlotsAfter > get_total_capacity() },
            { "error_code", "" },
        };
    }

    private static Godot.Collections.Dictionary _with_error(
        Godot.Collections.Dictionary result,
        string errorCode)
    {
        result["error_code"] = errorCode;
        return result;
    }

    private static StringName ReadStringName(
        Godot.Collections.Dictionary data,
        string key,
        StringName fallback = default)
    {
        var value = ReadValue(data, key);
        if (value.VariantType == Variant.Type.StringName)
            return value.AsStringName();
        if (value.VariantType == Variant.Type.String)
            return new StringName(value.AsString());
        return fallback ?? new StringName("");
    }

    private static string ReadString(
        Godot.Collections.Dictionary data,
        string key,
        string fallback = "")
    {
        var value = ReadValue(data, key);
        if (value.VariantType == Variant.Type.String)
            return value.AsString();
        if (value.VariantType == Variant.Type.StringName)
            return value.AsStringName().ToString();
        return fallback;
    }

    private static Variant ReadValue(Godot.Collections.Dictionary data, string key)
    {
        if (data == null)
            return default;
        if (data.ContainsKey(key))
            return data[key];
        var stringNameKey = new StringName(key);
        if (data.ContainsKey(stringNameKey))
            return data[stringNameKey];
        return default;
    }
}
