using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleEquipmentRequirementRules : RefCounted
{
    private static readonly StringName MainHand = "main_hand";
    private static readonly StringName OffHand = "off_hand";
    private static readonly StringName Head = "head";
    private static readonly StringName Body = "body";
    private static readonly StringName Hands = "hands";
    private static readonly StringName Feet = "feet";
    private static readonly StringName Cloak = "cloak";
    private static readonly StringName Necklace = "necklace";
    private static readonly StringName Ring1 = "ring_1";
    private static readonly StringName Ring2 = "ring_2";
    private static readonly StringName SpecialTrinket = "special_trinket";
    private static readonly StringName Badge = "badge";
    private static readonly StringName TagShield = "shield";

    private static readonly HashSet<StringName> ValidSlots = new()
    {
        MainHand,
        OffHand,
        Head,
        Body,
        Hands,
        Feet,
        Cloak,
        Necklace,
        Ring1,
        Ring2,
        SpecialTrinket,
        Badge,
    };

    public static bool unit_has_equipped_shield(BattleUnitState unit_state, GDictionary item_defs)
    {
        return unit_has_equipped_item_tag(unit_state, OffHand, TagShield, item_defs);
    }

    public static bool unit_has_equipped_item_tag(
        BattleUnitState unit_state,
        StringName slot_id,
        StringName tag_id,
        GDictionary item_defs
    )
    {
        if (unit_state == null || IsEmpty(tag_id))
        {
            return false;
        }

        EquipmentState equipmentView = unit_state.get_equipment_view();
        if (equipmentView == null)
        {
            return false;
        }

        StringName normalizedSlotId = slot_id ?? new StringName("");
        if (!IsValidSlot(normalizedSlotId))
        {
            return false;
        }

        StringName itemId = equipmentView.get_equipped_item_id(normalizedSlotId);
        if (
            IsEmpty(itemId)
            || item_defs == null
        )
        {
            return false;
        }

        ItemDef itemDef = ReadItemDef(item_defs, itemId);
        if (itemDef == null)
        {
            return false;
        }

        return itemDef.get_tags().Contains(tag_id);
    }

    private static bool IsValidSlot(StringName slotId)
    {
        return !IsEmpty(slotId) && ValidSlots.Contains(slotId);
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static ItemDef ReadItemDef(GDictionary dictionary, StringName itemId)
    {
        if (dictionary == null || IsEmpty(itemId))
        {
            return null;
        }
        if (dictionary.ContainsKey(itemId))
        {
            return dictionary[itemId].As<ItemDef>();
        }
        string stringKey = itemId.ToString();
        if (dictionary.ContainsKey(stringKey))
        {
            return dictionary[stringKey].As<ItemDef>();
        }
        return null;
    }
}
