using Godot;

[GlobalClass]
public partial class LowLuckRelicRules : RefCounted
{
    public static readonly StringName ITEM_REVERSE_FATE_AMULET = "reverse_fate_amulet";
    public static readonly StringName ITEM_BLACK_STAR_WEDGE = "black_star_wedge";
    public static readonly StringName ITEM_BLOOD_DEBT_SHAWL = "blood_debt_shawl";
    public static readonly StringName ITEM_DEAD_ROAD_LANTERN = "dead_road_lantern";

    public static readonly StringName ATTR_REVERSE_FATE_AMULET = "low_luck_reverse_fate_amulet";
    public static readonly StringName ATTR_BLACK_STAR_WEDGE = "low_luck_black_star_wedge";
    public static readonly StringName ATTR_BLOOD_DEBT_SHAWL = "low_luck_blood_debt_shawl";
    public static readonly StringName ATTR_DEAD_ROAD_LANTERN = "low_luck_dead_road_lantern";

    public static readonly StringName STATUS_REVERSE_FATE_WEAKENED = "low_luck_reverse_fate_weakened";
    public static readonly StringName STATUS_BLACK_STAR_WEDGE_EXPOSED = "low_luck_black_star_wedge_exposed";

    public const string BATTLE_FLAG_REVERSE_FATE_USED = "low_luck_reverse_fate_used";
    public const string BATTLE_FLAG_BLACK_STAR_WEDGE_USED = "low_luck_black_star_wedge_used";

    public const int REVERSE_FATE_DURATION_TU = 120;
    public const double REVERSE_FATE_DAMAGE_MULTIPLIER = 0.75;
    public const int BLACK_STAR_WEDGE_GUARD_IGNORE_FLAT = 4;
    public const int BLACK_STAR_WEDGE_EXPOSED_DURATION_TU = 60;
    public const double BLACK_STAR_WEDGE_EXPOSED_INCOMING_DAMAGE_MULTIPLIER = 1.25;
    public const double BLOOD_DEBT_LOW_HP_THRESHOLD_RATIO = 0.5;
    public const double BLOOD_DEBT_DAMAGE_MULTIPLIER = 0.75;
    public const double BLOOD_DEBT_RECOVERY_MULTIPLIER = 0.5;
    public const int BLOOD_DEBT_ALLY_DOWN_AP_GAIN = 1;

    public static readonly StringName PATH_TAG_HIDDEN_TRAP = "hidden_trap";
    public static readonly StringName PATH_TAG_BLACK_MARKET = "black_market";
    public static readonly StringName PATH_TAG_BLACK_OMEN = "black_omen";
    public static readonly StringName PATH_TAG_HIDDEN_PATH = "hidden_path";

    public static readonly Godot.Collections.Array<StringName> VISIBLE_PATH_TAGS = new()
    {
        PATH_TAG_HIDDEN_TRAP,
        PATH_TAG_BLACK_MARKET,
        PATH_TAG_BLACK_OMEN,
        PATH_TAG_HIDDEN_PATH,
    };

    public static bool snapshot_has_flag(GodotObject attributeSnapshot, StringName attributeId)
    {
        return attributeSnapshot != null && attributeId != "" && attributeSnapshot.Call("get_value", attributeId).AsInt32() > 0;
    }

    public static bool unit_has_flag(GodotObject unitState, StringName attributeId)
    {
        if (unitState == null)
            return false;
        var snapshot = unitState.Get("attribute_snapshot").AsGodotObject();
        return snapshot_has_flag(snapshot, attributeId);
    }

    public static Godot.Collections.Array<StringName> normalize_path_tags(Variant pathTagsVariant)
    {
        var normalizedTags = new Godot.Collections.Array<StringName>();
        if (pathTagsVariant.VariantType != Variant.Type.Array)
            return normalizedTags;
        foreach (var pathTagVariant in pathTagsVariant.AsGodotArray())
        {
            var pathTag = ProgressionDataUtils.to_string_name(pathTagVariant);
            if (pathTag == "" || normalizedTags.Contains(pathTag))
                continue;
            normalizedTags.Add(pathTag);
        }
        return normalizedTags;
    }

    public static bool should_reveal_hidden_path(GodotObject attributeSnapshot, Variant pathTagsVariant)
    {
        if (!snapshot_has_flag(attributeSnapshot, ATTR_DEAD_ROAD_LANTERN))
            return false;
        foreach (var pathTag in normalize_path_tags(pathTagsVariant))
        {
            if (VISIBLE_PATH_TAGS.Contains(pathTag))
                return true;
        }
        return false;
    }

    public static bool member_has_item(Godot.Collections.Dictionary itemDefs, GodotObject memberState, StringName itemId)
    {
        if (memberState == null || itemId == "" || memberState.Get("equipment_state").AsGodotObject() == null)
            return false;
        var equipmentState = memberState.Get("equipment_state").AsGodotObject();
        if (!equipmentState.HasMethod("get_entry_slot_ids"))
            return false;
        foreach (var slotIdVariant in equipmentState.Call("get_entry_slot_ids").AsGodotArray())
        {
            var equippedItemId = ProgressionDataUtils.to_string_name(
                equipmentState.Call("get_equipped_item_id", slotIdVariant)
            );
            if (equippedItemId == itemId)
                return true;
        }
        return false;
    }
}
