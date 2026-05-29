using System.Collections.Generic;
using Godot;
using static GdInterop;
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

    public static bool unit_has_equipped_shield(GodotObject unit_state, GDictionary item_defs)
    {
        return unit_has_equipped_item_tag(unit_state, OffHand, TagShield, item_defs);
    }

    public static bool unit_has_equipped_item_tag(
        GodotObject unit_state,
        StringName slot_id,
        StringName tag_id,
        GDictionary item_defs
    )
    {
        if (unit_state == null || IsEmpty(tag_id))
        {
            return false;
        }

        GodotObject equipmentView = unit_state.Call("get_equipment_view").AsGodotObject();
        if (equipmentView == null || !equipmentView.HasMethod("get_equipped_item_id"))
        {
            return false;
        }

        StringName normalizedSlotId = slot_id ?? new StringName("");
        if (!IsValidSlot(normalizedSlotId))
        {
            return false;
        }

        StringName itemId = equipmentView
            .Call("get_equipped_item_id", normalizedSlotId)
            .AsStringName();
        if (
            IsEmpty(itemId)
            || item_defs == null
            || !TryGet(item_defs, itemId, out Variant itemDefValue)
        )
        {
            return false;
        }

        GodotObject itemDef = itemDefValue.AsGodotObject();
        if (itemDef == null || !itemDef.HasMethod("get_tags"))
        {
            return false;
        }

        var tagsValue = itemDef.Call("get_tags");
        return tagsValue.VariantType == Variant.Type.Array
            && tagsValue.AsGodotArray().Contains(tag_id);
    }

    private static bool IsValidSlot(StringName slotId)
    {
        return !IsEmpty(slotId) && ValidSlots.Contains(slotId);
    }
}
