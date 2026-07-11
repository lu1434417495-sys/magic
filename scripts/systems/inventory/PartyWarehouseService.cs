using System;
using System.Collections.Generic;
using Godot;

public sealed class PartyWarehouseService : IDisposable
{
    internal static readonly StringName StorageSpaceAttributeId = "storage_space";

    internal sealed class WarehouseBatchItemEntry
    {
        public StringName ItemId { get; init; } = "";
        public StringName InstanceId { get; init; } = "";
        public EquipmentInstanceState EquipmentInstance { get; init; }

        public bool HasEquipmentInstance => EquipmentInstance != null;
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

    private PartyState _party_state = new();
    private Dictionary<StringName, ItemDefinition> _itemDefinitions = new();
    private WarehouseState _party_backpack_view;
    private Func<StringName> _equipment_instance_id_allocator;
    private EquipmentTraitRollService _equipment_trait_roll_service;
    private int _local_equipment_instance_serial = 1;
    private bool _disposed;

    public void Setup(
        PartyState partyState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        Func<StringName> equipmentInstanceIdAllocator = null,
        EquipmentTraitRollService equipmentTraitRollService = null
    )
    {
        _party_state = partyState ?? new PartyState();
        _itemDefinitions =
            itemDefinitions != null
                ? new Dictionary<StringName, ItemDefinition>(itemDefinitions)
                : new Dictionary<StringName, ItemDefinition>();
        _party_backpack_view = null;
        _equipment_instance_id_allocator = equipmentInstanceIdAllocator;
        _equipment_trait_roll_service = equipmentTraitRollService;
    }

    public void Setup(PartyState partyState) =>
        Setup(
            partyState,
            default(IReadOnlyDictionary<StringName, ItemDefinition>),
            default(Func<StringName>)
        );

    public void SetupPartyBackpackView(
        PartyState partyState,
        WarehouseState partyBackpackView,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions,
        Func<StringName> equipmentInstanceIdAllocator = null,
        EquipmentTraitRollService equipmentTraitRollService = null)
    {
        _party_state = partyState ?? new PartyState();
        _itemDefinitions =
            itemDefinitions != null
                ? new Dictionary<StringName, ItemDefinition>(itemDefinitions)
                : new Dictionary<StringName, ItemDefinition>();
        _party_backpack_view = partyBackpackView ?? new WarehouseState();
        _equipment_instance_id_allocator = equipmentInstanceIdAllocator;
        _equipment_trait_roll_service = equipmentTraitRollService;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _party_state = null;
        _itemDefinitions.Clear();
        _party_backpack_view = null;
        _equipment_instance_id_allocator = null;
        _equipment_trait_roll_service = null;
        GC.SuppressFinalize(this);
    }

    public int GetTotalCapacity()
    {
        if (_party_state == null)
            return 0;

        int totalCapacity = 0;
        foreach (PartyMemberState memberState in _party_state.GetMemberStates())
        {
            var unitBaseAttributes = memberState?.progression?.unit_base_attributes;
            if (unitBaseAttributes == null)
                continue;
            totalCapacity += Mathf.Max(unitBaseAttributes.GetAttributeValue(StorageSpaceAttributeId), 0);
        }
        return Mathf.Max(totalCapacity, 0);
    }

    public int GetUsedSlots()
    {
        var warehouseState = _get_warehouse_state();
        return warehouseState.GetNonEmptyStacksTyped().Count
            + warehouseState.GetNonEmptyEquipmentInstancesTyped().Count;
    }

    public int GetFreeSlots() => Mathf.Max(GetTotalCapacity() - GetUsedSlots(), 0);

    public bool IsOverCapacity() => GetUsedSlots() > GetTotalCapacity();

    public int CountItem(StringName itemId)
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

    internal IReadOnlyList<WarehouseInventoryEntry> GetInventoryEntriesTyped()
    {
        var warehouseState = _get_warehouse_state().DuplicateState();
        var entries = new List<WarehouseInventoryEntry>();

        foreach (var stack in warehouseState.GetNonEmptyStacksTyped())
        {
            if (stack == null || stack.IsEmpty())
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

    public ItemDefinition GetItemDef(StringName itemId)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        return normalizedItemId != ""
            && _itemDefinitions.TryGetValue(normalizedItemId, out var itemDefinition)
            ? itemDefinition
            : null;
    }

    internal WarehouseAddItemResult PreviewAddItemTyped(StringName itemId, int quantity) =>
        _process_add(itemId, quantity, false, false);

    internal WarehouseAddItemResult AddItemTyped(StringName itemId, int quantity) =>
        _process_add(itemId, quantity, true, true);

    internal WarehouseRemoveItemResult RemoveItemTyped(StringName itemId, int quantity)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        int requestedQuantity = Mathf.Max(quantity, 0);
        var warehouseState = _ensure_warehouse_state();
        _compact_state(warehouseState);
        int usedSlotsBefore = GetUsedSlots();

        if (normalizedItemId == "" || requestedQuantity <= 0)
            return _build_remove_item_result(normalizedItemId, requestedQuantity, 0, requestedQuantity, usedSlotsBefore, usedSlotsBefore, "");

        int remainingQuantity = requestedQuantity;
        var itemDef = GetItemDef(normalizedItemId);

        if (itemDef != null && itemDef.IsEquipment())
        {
            var matchingIndexes = _find_equipment_instance_indexes_by_item(warehouseState, normalizedItemId);
            if (requestedQuantity == 1 && matchingIndexes.Count == 1)
            {
                warehouseState.RemoveEquipmentInstanceAt(matchingIndexes[0]);
                remainingQuantity = 0;
            }
            else
            {
                int usedSlotsAfterReject = GetUsedSlots();
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
        int usedSlotsAfter = GetUsedSlots();
        return _build_remove_item_result(
            normalizedItemId,
            requestedQuantity,
            requestedQuantity - remainingQuantity,
            remainingQuantity,
            usedSlotsBefore,
            usedSlotsAfter,
            "");
    }

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

    internal WarehouseBatchSwapResult CommitBatchSwapEntriesTyped(
        Godot.Collections.Array itemsToWithdraw,
        Godot.Collections.Array itemsToDeposit) =>
        _run_batch_swap_transaction_typed(
            ParseBatchItemEntries(itemsToWithdraw),
            ParseBatchItemEntries(itemsToDeposit),
            true
        );

    internal WarehouseBatchSwapResult CommitBatchSwapEntriesTyped(
        IReadOnlyList<WarehouseBatchItemEntry> itemsToWithdraw,
        IReadOnlyList<WarehouseBatchItemEntry> itemsToDeposit) =>
        _run_batch_swap_transaction_typed(itemsToWithdraw, itemsToDeposit, true);

    internal WarehouseBatchSwapResult PreviewBatchQuantitySwapTyped(
        IReadOnlyList<WarehouseBatchQuantityEntry> itemsToWithdraw,
        IReadOnlyList<WarehouseBatchQuantityEntry> itemsToDeposit) =>
        _run_batch_quantity_swap_transaction_typed(itemsToWithdraw, itemsToDeposit, false);

    internal WarehouseBatchSwapResult CommitBatchQuantitySwapTyped(
        IReadOnlyList<WarehouseBatchQuantityEntry> itemsToWithdraw,
        IReadOnlyList<WarehouseBatchQuantityEntry> itemsToDeposit) =>
        _run_batch_quantity_swap_transaction_typed(itemsToWithdraw, itemsToDeposit, true);

    internal WarehouseState CaptureWarehouseStateForTransaction() =>
        _get_warehouse_state().DuplicateState();

    internal void RestoreWarehouseStateForTransaction(WarehouseState snapshot)
    {
        var restoredState = snapshot?.DuplicateState() ?? new WarehouseState();
        if (_party_backpack_view != null)
        {
            _copy_warehouse_state(restoredState, _party_backpack_view);
            return;
        }

        _party_state ??= new PartyState();
        _party_state.warehouse_state = restoredState;
    }

    public EquipmentInstanceState GetEquipmentInstanceById(
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
            return instance.DuplicateState();
        }
        return null;
    }

    public bool HasEquipmentInstance(StringName instanceId, StringName expectedItemId = default) =>
        GetEquipmentInstanceById(instanceId, expectedItemId) != null;

    public EquipmentInstanceState TakeEquipmentInstanceByItem(StringName itemId)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        var warehouseState = _ensure_warehouse_state();
        var matchingIndexes = _find_equipment_instance_indexes_by_item(warehouseState, normalizedItemId);
        if (matchingIndexes.Count != 1)
            return null;

        int index = matchingIndexes[0];
        return warehouseState.RemoveEquipmentInstanceAt(index);
    }

    public EquipmentInstanceState TakeEquipmentInstanceByInstanceId(
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

    internal WarehouseRemoveItemResult RemoveEquipmentInstanceTyped(
        StringName itemId,
        StringName instanceId)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        var normalizedInstanceId = ProgressionDataUtils.to_string_name(instanceId);
        var warehouseState = _ensure_warehouse_state();
        _compact_state(warehouseState);
        int usedSlotsBefore = GetUsedSlots();
        var itemDef = GetItemDef(normalizedItemId);

        if (normalizedItemId == "" || itemDef == null)
            return _build_remove_instance_result(normalizedItemId, normalizedInstanceId, 0, 1, usedSlotsBefore, usedSlotsBefore).WithError("item_not_found");
        if (!itemDef.IsEquipment())
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

        var removedInstance = TakeEquipmentInstanceByInstanceId(normalizedInstanceId, normalizedItemId);
        if (removedInstance == null)
            return _build_remove_instance_result(normalizedItemId, normalizedInstanceId, 0, 1, usedSlotsBefore, usedSlotsBefore).WithError("warehouse_missing_instance");

        _compact_state(warehouseState);
        int usedSlotsAfter = GetUsedSlots();
        return _build_remove_instance_result(normalizedItemId, normalizedInstanceId, 1, 0, usedSlotsBefore, usedSlotsAfter);
    }

    internal WarehouseAddItemResult AddEquipmentInstanceTyped(
        EquipmentInstanceState instance,
        bool forceNewInstanceId = false)
    {
        var warehouseState = _ensure_warehouse_state();
        _compact_state(warehouseState);
        int usedSlotsBefore = GetUsedSlots();
        var itemId = ProgressionDataUtils.to_string_name(instance?.item_id ?? new StringName(""));
        var itemDef = GetItemDef(itemId);
        bool itemFound = itemDef != null;
        bool isEquipment = itemDef != null && itemDef.IsEquipment();

        if (instance == null || itemId == "" || itemDef == null || !itemDef.IsEquipment())
            return new WarehouseAddItemResult
            {
                ItemId = itemId,
                RequestedQuantity = 1,
                AddedQuantity = 0,
                RemainingQuantity = 1,
                UsedSlotsBefore = usedSlotsBefore,
                UsedSlotsAfter = usedSlotsBefore,
                FreeSlotsAfter = Mathf.Max(GetTotalCapacity() - usedSlotsBefore, 0),
                IsOverCapacity = usedSlotsBefore > GetTotalCapacity(),
                ItemFound = itemFound,
                IsEquipment = isEquipment,
            };
        if (GetTotalCapacity() - usedSlotsBefore <= 0)
            return new WarehouseAddItemResult
            {
                ItemId = itemId,
                RequestedQuantity = 1,
                AddedQuantity = 0,
                RemainingQuantity = 1,
                UsedSlotsBefore = usedSlotsBefore,
                UsedSlotsAfter = usedSlotsBefore,
                FreeSlotsAfter = Mathf.Max(GetTotalCapacity() - usedSlotsBefore, 0),
                IsOverCapacity = usedSlotsBefore > GetTotalCapacity(),
                ItemFound = itemFound,
                IsEquipment = isEquipment,
            };

        var allocatedInstanceId = new StringName("");
        bool allocatedNewStableId = false;
        if (forceNewInstanceId || instance.instance_id == "")
        {
            allocatedInstanceId = _allocate_equipment_instance_id(warehouseState);
            instance.instance_id = allocatedInstanceId;
            allocatedNewStableId = allocatedInstanceId != "";
            if (allocatedInstanceId == "")
                return new WarehouseAddItemResult
                {
                    ItemId = itemId,
                    RequestedQuantity = 1,
                    AddedQuantity = 0,
                    RemainingQuantity = 1,
                    UsedSlotsBefore = usedSlotsBefore,
                    UsedSlotsAfter = usedSlotsBefore,
                    FreeSlotsAfter = Mathf.Max(GetTotalCapacity() - usedSlotsBefore, 0),
                    IsOverCapacity = usedSlotsBefore > GetTotalCapacity(),
                    ItemFound = itemFound,
                    IsEquipment = isEquipment,
                };
        }

        if (allocatedNewStableId && _equipment_trait_roll_service != null)
            _equipment_trait_roll_service.MintWithRolls(instance, itemDef);

        warehouseState.AddEquipmentInstance(instance);
        _compact_state(warehouseState);
        int usedSlotsAfter = GetUsedSlots();
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
            FreeSlotsAfter = Mathf.Max(GetTotalCapacity() - usedSlotsAfter, 0),
            IsOverCapacity = usedSlotsAfter > GetTotalCapacity(),
            ItemFound = itemFound,
            IsEquipment = isEquipment,
            AllocatedEquipmentInstanceIds = allocatedInstanceIds,
        };
    }

    public bool DepositEquipmentInstance(EquipmentInstanceState instance)
    {
        if (instance == null)
            return false;

        var warehouseState = _ensure_warehouse_state();
        bool allocatedNewStableId = false;
        if (instance.instance_id == "")
        {
            instance.instance_id = _allocate_equipment_instance_id(warehouseState);
            if (instance.instance_id == "")
                return false;
            allocatedNewStableId = true;
        }
        if (allocatedNewStableId && _equipment_trait_roll_service != null)
            _equipment_trait_roll_service.MintWithRolls(instance, GetItemDef(instance.item_id));
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
            var itemDef = GetItemDef(itemId);
            WarehouseRemoveItemResult result =
                itemDef != null && itemDef.IsEquipment() && instanceId != ""
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
                if (!consumeAllocator)
                {
                    if (GetTotalCapacity() - GetUsedSlots() <= 0)
                    {
                        return WarehouseBatchSwapResult.Blocked(
                            "warehouse_blocked_swap",
                            itemId,
                            depositEntry.InstanceId
                        );
                    }

                    var previewEquipmentInstance = depositEntry.EquipmentInstance.DuplicateState();
                    if (previewEquipmentInstance == null)
                    {
                        return WarehouseBatchSwapResult.Blocked(
                            "warehouse_blocked_swap",
                            itemId,
                            depositEntry.InstanceId
                        );
                    }

                    if (previewEquipmentInstance.instance_id == "")
                    {
                        previewEquipmentInstance.instance_id = _allocate_preview_equipment_instance_id(
                            _get_warehouse_state()
                        );
                    }

                    var previewAddInstanceResult = AddEquipmentInstanceTyped(
                        previewEquipmentInstance,
                        false
                    );
                    if (previewAddInstanceResult.AddedQuantity <= 0)
                    {
                        return WarehouseBatchSwapResult.Blocked(
                            "warehouse_blocked_swap",
                            itemId,
                            depositEntry.InstanceId
                        );
                    }

                    continue;
                }
                var addInstanceResult = AddEquipmentInstanceTyped(
                    depositEntry.EquipmentInstance.DuplicateState(),
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

    private WarehouseBatchSwapResult _run_batch_swap_transaction_typed(
        IReadOnlyList<WarehouseBatchItemEntry> itemsToWithdraw,
        IReadOnlyList<WarehouseBatchItemEntry> itemsToDeposit,
        bool commitOnSuccess)
    {
        var baselineState = _get_warehouse_state().DuplicateState();
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

    private WarehouseBatchSwapResult _run_batch_quantity_swap_transaction_typed(
        IReadOnlyList<WarehouseBatchQuantityEntry> itemsToWithdraw,
        IReadOnlyList<WarehouseBatchQuantityEntry> itemsToDeposit,
        bool commitOnSuccess)
    {
        _party_state ??= new PartyState();
        var baselineState = _get_warehouse_state().DuplicateState();
        var originalState = _party_backpack_view ?? _party_state.warehouse_state;

        _set_transaction_warehouse_state(baselineState);
        var result = _execute_batch_quantity_swap(itemsToWithdraw, itemsToDeposit);
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

    private WarehouseBatchSwapResult _execute_batch_quantity_swap(
        IReadOnlyList<WarehouseBatchQuantityEntry> itemsToWithdraw,
        IReadOnlyList<WarehouseBatchQuantityEntry> itemsToDeposit)
    {
        var validationResult = _validate_batch_quantity_entries(itemsToWithdraw);
        if (!validationResult.Allowed)
            return validationResult;
        validationResult = _validate_batch_quantity_entries(itemsToDeposit);
        if (!validationResult.Allowed)
            return validationResult;

        foreach (var withdrawEntry in itemsToWithdraw ?? Array.Empty<WarehouseBatchQuantityEntry>())
        {
            int removedQuantity = _remove_stack_quantity(withdrawEntry.ItemId, withdrawEntry.Quantity);
            if (removedQuantity < withdrawEntry.Quantity)
                return WarehouseBatchSwapResult.Blocked("warehouse_missing_item", withdrawEntry.ItemId);
        }

        foreach (var depositEntry in itemsToDeposit ?? Array.Empty<WarehouseBatchQuantityEntry>())
        {
            var addResult = _process_add(depositEntry.ItemId, depositEntry.Quantity, true, false);
            if (addResult.AddedQuantity < depositEntry.Quantity)
                return WarehouseBatchSwapResult.Blocked("warehouse_blocked_swap", depositEntry.ItemId);
        }

        return WarehouseBatchSwapResult.Success();
    }

    private WarehouseBatchSwapResult _validate_batch_quantity_entries(
        IReadOnlyList<WarehouseBatchQuantityEntry> entries)
    {
        if (entries == null)
            return WarehouseBatchSwapResult.Success();

        foreach (var entry in entries)
        {
            var itemId = ProgressionDataUtils.to_string_name(entry.ItemId);
            if (itemId == "" || entry.Quantity <= 0)
                return WarehouseBatchSwapResult.Blocked("invalid_batch_quantity_entry", itemId);

            var itemDef = GetItemDef(itemId);
            if (itemDef != null && itemDef.IsEquipment())
                return WarehouseBatchSwapResult.Blocked(
                    "warehouse_quantity_equipment_unsupported",
                    itemId
                );
        }

        return WarehouseBatchSwapResult.Success();
    }

    private int _remove_stack_quantity(StringName itemId, int quantity)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        if (normalizedItemId == "" || quantity <= 0)
            return 0;

        var warehouseState = _ensure_warehouse_state();
        _compact_state(warehouseState);

        int remainingQuantity = quantity;
        for (int index = 0; index < warehouseState.GetStacksTyped().Count && remainingQuantity > 0;)
        {
            var stack = warehouseState.GetStackAt(index);
            if (stack == null || stack.item_id != normalizedItemId)
            {
                index += 1;
                continue;
            }

            int removedQuantity = Mathf.Min(Mathf.Max(stack.quantity, 0), remainingQuantity);
            stack.quantity -= removedQuantity;
            remainingQuantity -= removedQuantity;
            if (stack.quantity <= 0)
            {
                warehouseState.RemoveStackAt(index);
                continue;
            }

            index += 1;
        }

        _compact_state(warehouseState);
        return quantity - remainingQuantity;
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
        if (value is EquipmentInstanceState equipmentInstance)
            return equipmentInstance.DuplicateState();
        if (TryRawDictionary(value, out var equipmentData))
            return EquipmentInstanceState.FromDictionary(equipmentData);
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
        int usedSlotsBefore = GetUsedSlots();
        var itemDef = GetItemDef(normalizedItemId);
        var targetState = mutate
            ? _ensure_warehouse_state()
            : _get_warehouse_state().DuplicateState();
        _compact_state(targetState);

        int currentUsed =
            targetState.GetNonEmptyStacksTyped().Count
            + targetState.GetNonEmptyEquipmentInstancesTyped().Count;
        bool itemFound = itemDef != null;
        bool isEquipment = itemDef != null && itemDef.IsEquipment();

        if (normalizedItemId == "" || requestedQuantity <= 0 || itemDef == null)
            return new WarehouseAddItemResult
            {
                ItemId = normalizedItemId,
                RequestedQuantity = requestedQuantity,
                AddedQuantity = 0,
                RemainingQuantity = requestedQuantity,
                UsedSlotsBefore = usedSlotsBefore,
                UsedSlotsAfter = currentUsed,
                FreeSlotsAfter = Mathf.Max(GetTotalCapacity() - currentUsed, 0),
                CreatedStackCount = 0,
                FilledExistingQuantity = 0,
                IsOverCapacity = currentUsed > GetTotalCapacity(),
                ItemFound = itemFound,
                IsEquipment = isEquipment,
            };

        int remainingQuantity = requestedQuantity;
        int createdStackCount = 0;
        int filledExistingQuantity = 0;
        var allocatedInstanceIds = new List<string>();
        if (itemDef.IsEquipment())
        {
            int availableNewSlots = Mathf.Max(GetTotalCapacity() - currentUsed, 0);
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
            int maxStack = itemDef.GetEffectiveMaxStack();
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
                GetTotalCapacity()
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
            FreeSlotsAfter = Mathf.Max(GetTotalCapacity() - usedSlotsAfter, 0),
            CreatedStackCount = createdStackCount,
            FilledExistingQuantity = filledExistingQuantity,
            IsOverCapacity = usedSlotsAfter > GetTotalCapacity(),
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
        var instance = EquipmentInstanceState.CreateTransientInstance(itemId);
        instance.instance_id = consumeAllocator
            ? _allocate_equipment_instance_id(targetState)
            : _allocate_preview_equipment_instance_id(targetState);
        if (consumeAllocator && _equipment_trait_roll_service != null)
            _equipment_trait_roll_service.MintWithRolls(instance, GetItemDef(itemId));
        return instance;
    }

    private StringName _allocate_equipment_instance_id(WarehouseState targetState = null)
    {
        if (_equipment_instance_id_allocator != null)
            return ProgressionDataUtils.to_string_name(_equipment_instance_id_allocator.Invoke());

        while (true)
        {
            var candidate = EquipmentInstanceState.FormatInstanceId(_local_equipment_instance_serial);
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
            var candidate = EquipmentInstanceState.FormatPreviewInstanceId(serial);
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
            stacks.Add(stack.DuplicateState());
        targetState.ReplaceStacks(stacks);

        var instances = new List<EquipmentInstanceState>();
        foreach (var instance in sourceState.GetNonEmptyEquipmentInstancesTyped())
            instances.Add(instance.DuplicateState());
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
        var itemDef = GetItemDef(normalizedItemId);
        var grantedSkillId = itemDef?.GrantedSkillId ?? new StringName("");
        return new WarehouseInventoryEntry(
            normalizedItemId,
            itemDef,
            itemDef != null && itemDef.DisplayName.Length > 0
                ? itemDef.DisplayName
                : normalizedItemId.ToString(),
            itemDef?.Description ?? "该物品定义缺失，当前仅保留存档中的 item_id 与数量。",
            itemDef?.Icon ?? "",
            resolvedQuantity,
            CountItem(normalizedItemId),
            itemDef?.IsStackable ?? resolvedQuantity > 1,
            itemDef?.GetEffectiveMaxStack() ?? Mathf.Max(resolvedQuantity, 1),
            itemDef != null ? itemDef.GetItemCategoryNormalized() : new StringName(""),
            itemDef != null && itemDef.IsSkillBook(),
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
            FreeSlotsAfter = Mathf.Max(GetTotalCapacity() - usedSlotsAfter, 0),
            IsOverCapacity = usedSlotsAfter > GetTotalCapacity(),
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
            FreeSlotsAfter = Mathf.Max(GetTotalCapacity() - usedSlotsAfter, 0),
            IsOverCapacity = usedSlotsAfter > GetTotalCapacity(),
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
