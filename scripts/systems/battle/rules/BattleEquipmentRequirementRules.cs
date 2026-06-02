using System.Collections.Generic;
using Godot;

public static class BattleEquipmentRequirementRules
{
    private static readonly StringName TagShield = "shield";

    public static bool UnitHasEquippedShield(
        BattleUnitState unitState,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs
    )
    {
        return UnitHasEquippedItemTag(unitState, EquipmentRules.OFF_HAND(), TagShield, itemDefs);
    }

    public static bool UnitHasEquippedItemTag(
        BattleUnitState unitState,
        StringName slotId,
        StringName tagId,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs
    )
    {
        if (unitState == null || IsEmpty(tagId) || itemDefs == null || itemDefs.Count == 0)
        {
            return false;
        }

        EquipmentState equipmentView = unitState.get_equipment_view();
        if (equipmentView == null)
        {
            return false;
        }

        StringName normalizedSlotId = ProgressionDataUtils.to_string_name(slotId);
        if (!EquipmentRules.is_valid_slot(normalizedSlotId))
        {
            return false;
        }

        StringName itemId = ProgressionDataUtils.to_string_name(
            equipmentView.get_equipped_item_id(normalizedSlotId)
        );
        if (IsEmpty(itemId))
        {
            return false;
        }

        return itemDefs.TryGetValue(itemId, out ItemDef itemDef)
            && ItemHasTag(itemDef, tagId);
    }

    public static bool ItemHasTag(ItemDef itemDef, StringName tagId)
    {
        if (itemDef == null || IsEmpty(tagId))
        {
            return false;
        }
        return itemDef.get_tags().Contains(tagId);
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
