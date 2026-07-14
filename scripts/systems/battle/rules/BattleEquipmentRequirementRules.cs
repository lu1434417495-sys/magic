using System.Collections.Generic;
using Godot;

public static class BattleEquipmentRequirementRules
{
    private static readonly StringName TagShield = "shield";

    public static bool UnitHasEquippedShield(
        BattleUnitState unitState,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        return UnitHasEquippedItemTag(
            unitState,
            EquipmentRules.ToStringName(EquipmentSlotKind.OffHand),
            TagShield,
            itemDefinitions
        );
    }

    public static bool UnitHasEquippedItemTag(
        BattleUnitState unitState,
        StringName slotId,
        StringName tagId,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        if (
            unitState == null
            || IsEmpty(tagId)
            || itemDefinitions == null
            || itemDefinitions.Count == 0
        )
        {
            return false;
        }

        EquipmentState equipmentView = unitState.GetEquipmentView();
        if (equipmentView == null)
        {
            return false;
        }

        StringName normalizedSlotId = ProgressionDataUtils.to_string_name(slotId);
        if (!EquipmentRules.IsValidSlot(normalizedSlotId))
        {
            return false;
        }

        StringName itemId = ProgressionDataUtils.to_string_name(
            equipmentView.GetEquippedItemId(normalizedSlotId)
        );
        if (IsEmpty(itemId))
        {
            return false;
        }

        return itemDefinitions.TryGetValue(itemId, out ItemDefinition itemDefinition)
            && ItemHasTag(itemDefinition, tagId);
    }

    public static bool ItemHasTag(ItemDefinition itemDefinition, StringName tagId)
    {
        if (itemDefinition == null || IsEmpty(tagId))
        {
            return false;
        }
        return itemDefinition.GetTagsTyped().Contains(tagId);
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
