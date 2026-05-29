using System;
using Godot;

[GlobalClass]
public partial class PartyWarehouseService : RefCounted
{
    private static readonly StringName StorageSpaceAttributeId = "storage_space";

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

    public Godot.Collections.Array<Godot.Collections.Dictionary> get_inventory_entries()
    {
        var warehouseState = _get_warehouse_state().duplicate_state();
        var entries = new Godot.Collections.Array<Godot.Collections.Dictionary>();

        foreach (var stack in warehouseState.get_non_empty_stacks())
        {
            if (stack == null || stack.is_empty())
                continue;
            entries.Add(_build_inventory_entry(stack.item_id, stack.quantity, "stack"));
        }

        var equipmentEntries = new System.Collections.Generic.List<Godot.Collections.Dictionary>();
        foreach (var instance in warehouseState.get_non_empty_instances())
        {
            if (instance == null || instance.item_id == "")
                continue;
            equipmentEntries.Add(_build_inventory_entry(instance.item_id, 1, "instance", instance));
        }
        equipmentEntries.Sort((a, b) =>
            string.CompareOrdinal(
                $"{GdInterop.GetString(a, "item_id")}:{GdInterop.GetString(a, "instance_id")}",
                $"{GdInterop.GetString(b, "item_id")}:{GdInterop.GetString(b, "instance_id")}"));

        foreach (var entry in equipmentEntries)
            entries.Add(entry);

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
        _process_add(itemId, quantity, false, false);

    public Godot.Collections.Dictionary add_item(StringName itemId, int quantity) =>
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
        _run_batch_swap_transaction(_to_untyped_array(itemsToWithdraw), _to_untyped_array(itemsToDeposit), false);

    public Godot.Collections.Dictionary commit_batch_swap(
        Godot.Collections.Array<StringName> itemsToWithdraw,
        Godot.Collections.Array<StringName> itemsToDeposit) =>
        _run_batch_swap_transaction(_to_untyped_array(itemsToWithdraw), _to_untyped_array(itemsToDeposit), true);

    public Godot.Collections.Dictionary preview_batch_swap_entries(
        Godot.Collections.Array itemsToWithdraw,
        Godot.Collections.Array itemsToDeposit) =>
        _run_batch_swap_transaction(itemsToWithdraw, itemsToDeposit, false);

    public Godot.Collections.Dictionary commit_batch_swap_entries(
        Godot.Collections.Array itemsToWithdraw,
        Godot.Collections.Array itemsToDeposit) =>
        _run_batch_swap_transaction(itemsToWithdraw, itemsToDeposit, true);

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
            return EquipmentInstanceState.from_dict(instance.to_dict());
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
        bool forceNewInstanceId = false)
    {
        var warehouseState = _ensure_warehouse_state();
        _compact_state(warehouseState);
        int usedSlotsBefore = get_used_slots();
        var itemId = ProgressionDataUtils.to_string_name(instance?.item_id ?? new StringName(""));
        var itemDef = get_item_def(itemId);
        var result = new Godot.Collections.Dictionary
        {
            { "item_id", itemId.ToString() },
            { "requested_quantity", 1 },
            { "added_quantity", 0 },
            { "remaining_quantity", 1 },
            { "used_slots_before", usedSlotsBefore },
            { "used_slots_after", usedSlotsBefore },
            { "free_slots_after", Mathf.Max(get_total_capacity() - usedSlotsBefore, 0) },
            { "is_over_capacity", usedSlotsBefore > get_total_capacity() },
            { "item_found", itemDef != null },
            { "is_equipment", itemDef != null && itemDef.is_equipment() },
            { "allocated_equipment_instance_ids", new Godot.Collections.Array<string>() },
        };

        if (instance == null || itemId == "" || itemDef == null || !itemDef.is_equipment())
            return result;
        if (get_total_capacity() - usedSlotsBefore <= 0)
            return result;

        var allocatedInstanceId = new StringName("");
        if (forceNewInstanceId || instance.instance_id == "")
        {
            allocatedInstanceId = _allocate_equipment_instance_id(warehouseState);
            instance.instance_id = allocatedInstanceId;
            if (allocatedInstanceId == "")
                return result;
        }

        warehouseState.equipment_instances.Add(instance);
        _compact_state(warehouseState);
        int usedSlotsAfter = get_used_slots();
        result["added_quantity"] = 1;
        result["remaining_quantity"] = 0;
        result["used_slots_after"] = usedSlotsAfter;
        result["free_slots_after"] = Mathf.Max(get_total_capacity() - usedSlotsAfter, 0);
        result["is_over_capacity"] = usedSlotsAfter > get_total_capacity();
        if (allocatedInstanceId != "")
            result["allocated_equipment_instance_ids"] = new Godot.Collections.Array<string> { allocatedInstanceId.ToString() };
        return result;
    }

    public Godot.Collections.Dictionary add_equipment_instance(EquipmentInstanceState instance) =>
        add_equipment_instance(instance, false);

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

    private Godot.Collections.Dictionary _execute_batch_swap(
        Godot.Collections.Array itemsToWithdraw,
        Godot.Collections.Array itemsToDeposit,
        bool consumeAllocator)
    {
        foreach (var withdrawValue in itemsToWithdraw)
        {
            var withdrawEntry =
                withdrawValue.VariantType == Variant.Type.Dictionary
                    ? _normalize_batch_item_entry(withdrawValue.AsGodotDictionary())
                    : _normalize_batch_item_entry(ProgressionDataUtils.to_string_name(withdrawValue));
            var itemId = GdInterop.GetStringName(withdrawEntry, "item_id");
            var instanceId = GdInterop.GetStringName(withdrawEntry, "instance_id");
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
                return new Godot.Collections.Dictionary
                {
                    { "allowed", false },
                    { "error_code", errorCode },
                    { "blocked_item_id", itemId.ToString() },
                    { "blocked_instance_id", instanceId.ToString() },
                };
            }
        }

        foreach (var depositValue in itemsToDeposit)
        {
            var depositEntry =
                depositValue.VariantType == Variant.Type.Dictionary
                    ? _normalize_batch_item_entry(depositValue.AsGodotDictionary())
                    : _normalize_batch_item_entry(ProgressionDataUtils.to_string_name(depositValue));
            var itemId = GdInterop.GetStringName(depositEntry, "item_id");
            var preview = preview_add_item(itemId, 1);
            if (preview.ContainsKey("remaining_quantity") && preview["remaining_quantity"].AsInt32() > 0)
            {
                return new Godot.Collections.Dictionary
                {
                    { "allowed", false },
                    { "error_code", "warehouse_blocked_swap" },
                    { "blocked_item_id", itemId.ToString() },
                    { "blocked_instance_id", GdInterop.GetStringName(depositEntry, "instance_id").ToString() },
                };
            }

            if (depositEntry.ContainsKey("equipment_instance"))
            {
                var equipmentInstanceValue = depositEntry["equipment_instance"];
                EquipmentInstanceState instance = null;
                if (equipmentInstanceValue.VariantType == Variant.Type.Object)
                    instance = equipmentInstanceValue.AsGodotObject() as EquipmentInstanceState;
                else if (equipmentInstanceValue.VariantType == Variant.Type.Dictionary)
                    instance = EquipmentInstanceState.from_dict(
                        equipmentInstanceValue.AsGodotDictionary()
                    );
                var addInstanceResult = add_equipment_instance(instance, false);
                if (addInstanceResult.ContainsKey("added_quantity") && addInstanceResult["added_quantity"].AsInt32() <= 0)
                {
                    return new Godot.Collections.Dictionary
                    {
                        { "allowed", false },
                        { "error_code", addInstanceResult.ContainsKey("error_code") ? addInstanceResult["error_code"].AsString() : "warehouse_blocked_swap" },
                        { "blocked_item_id", itemId.ToString() },
                        { "blocked_instance_id", GdInterop.GetStringName(depositEntry, "instance_id").ToString() },
                    };
                }
            }
            else
            {
                _process_add(itemId, 1, true, consumeAllocator);
            }
        }

        return new Godot.Collections.Dictionary
        {
            { "allowed", true },
            { "error_code", "" },
            { "blocked_item_id", "" },
            { "blocked_instance_id", "" },
        };
    }

    private Godot.Collections.Dictionary _run_batch_swap_transaction(
        Godot.Collections.Array itemsToWithdraw,
        Godot.Collections.Array itemsToDeposit,
        bool commitOnSuccess)
    {
        var baselineState = _get_warehouse_state().duplicate_state();
        _party_state ??= new PartyState();
        var originalState = _party_backpack_view ?? _party_state.warehouse_state;

        _set_transaction_warehouse_state(baselineState);
        var result = _execute_batch_swap(itemsToWithdraw, itemsToDeposit, commitOnSuccess);
        if (result.ContainsKey("allowed") && result["allowed"].AsBool() && commitOnSuccess)
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

    private static Godot.Collections.Dictionary _normalize_batch_item_entry(
        Godot.Collections.Dictionary entry
    )
    {
        var result = new Godot.Collections.Dictionary
        {
            { "item_id", GdInterop.GetStringName(entry, "item_id") },
            { "instance_id", GdInterop.GetStringName(entry, "instance_id") },
        };
        if (entry.ContainsKey("equipment_instance"))
            result["equipment_instance"] = entry["equipment_instance"];
        return result;
    }

    private static Godot.Collections.Dictionary _normalize_batch_item_entry(StringName itemId)
    {
        return new Godot.Collections.Dictionary
        {
            { "item_id", ProgressionDataUtils.to_string_name(itemId) },
            { "instance_id", new StringName("") },
        };
    }

    private Godot.Collections.Dictionary _process_add(
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
        var result = new Godot.Collections.Dictionary
        {
            { "item_id", normalizedItemId.ToString() },
            { "requested_quantity", requestedQuantity },
            { "added_quantity", 0 },
            { "remaining_quantity", requestedQuantity },
            { "used_slots_before", usedSlotsBefore },
            { "used_slots_after", currentUsed },
            { "free_slots_after", Mathf.Max(get_total_capacity() - currentUsed, 0) },
            { "created_stack_count", 0 },
            { "filled_existing_quantity", 0 },
            { "is_over_capacity", currentUsed > get_total_capacity() },
            { "item_found", itemDef != null },
        };

        if (normalizedItemId == "" || requestedQuantity <= 0 || itemDef == null)
            return result;

        int remainingQuantity = requestedQuantity;
        if (itemDef.is_equipment())
        {
            int availableNewSlots = Mathf.Max(get_total_capacity() - targetState.stacks.Count - targetState.equipment_instances.Count, 0);
            int createdCount = 0;
            var allocatedInstanceIds = new Godot.Collections.Array<string>();
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
                createdCount += 1;
            }
            result["created_stack_count"] = createdCount;
            result["allocated_equipment_instance_ids"] = allocatedInstanceIds;
        }
        else
        {
            int filledExistingQuantity = 0;
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

            int createdStackCount = 0;
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
            result["filled_existing_quantity"] = filledExistingQuantity;
            result["created_stack_count"] = createdStackCount;
        }

        int usedSlotsAfter = targetState.stacks.Count + targetState.get_non_empty_instances().Count;
        result["added_quantity"] = requestedQuantity - remainingQuantity;
        result["remaining_quantity"] = remainingQuantity;
        result["used_slots_after"] = usedSlotsAfter;
        result["free_slots_after"] = Mathf.Max(get_total_capacity() - usedSlotsAfter, 0);
        result["is_over_capacity"] = usedSlotsAfter > get_total_capacity();
        return result;
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
            targetState.equipment_instances.Add(EquipmentInstanceState.from_dict(instance.to_dict()));
    }

    private static void _compact_state(WarehouseState warehouseState)
    {
        if (warehouseState == null)
            return;
        warehouseState.stacks = warehouseState.get_non_empty_stacks();
        warehouseState.equipment_instances = warehouseState.get_non_empty_instances();
    }

    private Godot.Collections.Dictionary _build_inventory_entry(
        StringName itemId,
        int quantity,
        StringName storageMode,
        EquipmentInstanceState equipmentInstance = null)
    {
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        int resolvedQuantity = Mathf.Max(quantity, 0);
        var itemDef = get_item_def(normalizedItemId);
        var grantedSkillId = itemDef?.granted_skill_id ?? new StringName("");
        var entry = new Godot.Collections.Dictionary
        {
            { "item_id", normalizedItemId.ToString() },
            { "display_name", itemDef != null && itemDef.display_name.Length > 0 ? itemDef.display_name : normalizedItemId.ToString() },
            { "description", itemDef?.description ?? "该物品定义缺失，当前仅保留存档中的 item_id 与数量。" },
            { "icon", itemDef?.icon ?? "" },
            { "quantity", resolvedQuantity },
            { "total_quantity", count_item(normalizedItemId) },
            { "is_stackable", itemDef?.is_stackable ?? resolvedQuantity > 1 },
            { "stack_limit", itemDef?.get_effective_max_stack() ?? Mathf.Max(resolvedQuantity, 1) },
            { "item_category", itemDef != null ? itemDef.get_item_category_normalized().ToString() : "" },
            { "is_skill_book", itemDef != null && itemDef.is_skill_book() },
            { "granted_skill_id", grantedSkillId.ToString() },
            { "storage_mode", storageMode.ToString() },
        };

        if (equipmentInstance != null)
        {
            entry["instance_id"] = equipmentInstance.instance_id.ToString();
            entry["rarity"] = equipmentInstance.rarity;
            entry["current_durability"] = equipmentInstance.current_durability;
        }
        return entry;
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

    private static Godot.Collections.Array _to_untyped_array(Godot.Collections.Array<StringName> values)
    {
        var result = new Godot.Collections.Array();
        if (values == null)
            return result;

        foreach (var value in values)
            result.Add(value);
        return result;
    }

}
