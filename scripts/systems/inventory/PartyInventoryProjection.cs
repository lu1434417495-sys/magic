using Godot.Collections;
using GDictionary = Godot.Collections.Dictionary;

internal static class PartyInventoryProjection
{
    internal static GDictionary Project(WarehouseInventoryEntry entry)
    {
        if (entry == null)
            return new GDictionary();

        var result = new GDictionary
        {
            { "item_id", entry.ItemId.ToString() },
            { "display_name", entry.DisplayName },
            { "description", entry.Description },
            { "icon", entry.Icon },
            { "quantity", entry.Quantity },
            { "total_quantity", entry.TotalQuantity },
            { "is_stackable", entry.IsStackable },
            { "stack_limit", entry.StackLimit },
            { "item_category", entry.ItemCategory.ToString() },
            { "is_skill_book", entry.IsSkillBook },
            { "granted_skill_id", entry.GrantedSkillId.ToString() },
            { "storage_mode", entry.StorageMode.ToString() },
        };

        if (entry.HasEquipmentInstance)
        {
            result["instance_id"] = entry.InstanceId.ToString();
            result["rarity"] = entry.Rarity;
            result["current_durability"] = entry.CurrentDurability;
        }
        return result;
    }

    internal static GDictionary Project(PartyItemUseService.PartyItemUseResult result)
    {
        if (result == null)
            return new GDictionary();

        return new GDictionary
        {
            { "success", result.Success },
            { "reason", result.Reason },
            { "item_id", result.ItemId },
            { "member_id", result.MemberId },
            { "skill_id", result.SkillId },
            { "consumed_quantity", result.ConsumedQuantity },
            { "needs_confirmation", result.NeedsConfirmation },
            {
                "practice_replacement_preview",
                result.PracticeReplacementStatus.ToLearnedStatusDictionary()
            },
        };
    }

    internal static GDictionary Project(WarehouseBatchSwapResult result)
    {
        if (result == null)
            return new GDictionary();

        return new GDictionary
        {
            { "allowed", result.Allowed },
            { "error_code", result.ErrorCode },
            { "blocked_item_id", result.BlockedItemId.ToString() },
            { "blocked_instance_id", result.BlockedInstanceId.ToString() },
        };
    }

    internal static GDictionary Project(PartyWarehouseService.WarehouseAddItemResult result)
    {
        if (result == null)
            return new GDictionary();

        var payload = new GDictionary
        {
            { "item_id", result.ItemId.ToString() },
            { "requested_quantity", result.RequestedQuantity },
            { "added_quantity", result.AddedQuantity },
            { "remaining_quantity", result.RemainingQuantity },
            { "used_slots_before", result.UsedSlotsBefore },
            { "used_slots_after", result.UsedSlotsAfter },
            { "free_slots_after", result.FreeSlotsAfter },
            { "created_stack_count", result.CreatedStackCount },
            { "filled_existing_quantity", result.FilledExistingQuantity },
            { "is_over_capacity", result.IsOverCapacity },
            { "item_found", result.ItemFound },
        };
        if (result.IsEquipment)
        {
            payload["is_equipment"] = true;
            payload["allocated_equipment_instance_ids"] =
                new Array<string>(result.AllocatedEquipmentInstanceIds);
        }
        return payload;
    }

    internal static GDictionary Project(PartyWarehouseService.WarehouseRemoveItemResult result)
    {
        if (result == null)
            return new GDictionary();

        var payload = new GDictionary
        {
            { "item_id", result.ItemId.ToString() },
            { "requested_quantity", result.RequestedQuantity },
            { "removed_quantity", result.RemovedQuantity },
            { "remaining_quantity", result.RemainingQuantity },
            { "used_slots_before", result.UsedSlotsBefore },
            { "used_slots_after", result.UsedSlotsAfter },
            { "free_slots_after", result.FreeSlotsAfter },
            { "is_over_capacity", result.IsOverCapacity },
            { "error_code", result.ErrorCode },
        };
        if (result.IncludeInstanceId)
            payload["instance_id"] = result.InstanceId.ToString();
        return payload;
    }
}
