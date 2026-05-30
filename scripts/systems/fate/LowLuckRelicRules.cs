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

    public static readonly StringName STATUS_REVERSE_FATE_WEAKENED =
        "low_luck_reverse_fate_weakened";

    public static readonly StringName STATUS_BLACK_STAR_WEDGE_EXPOSED =
        "low_luck_black_star_wedge_exposed";

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

    public static StringName item_reverse_fate_amulet() => ITEM_REVERSE_FATE_AMULET;

    public static StringName item_black_star_wedge() => ITEM_BLACK_STAR_WEDGE;

    public static StringName item_blood_debt_shawl() => ITEM_BLOOD_DEBT_SHAWL;

    public static StringName item_dead_road_lantern() => ITEM_DEAD_ROAD_LANTERN;

    public static StringName attr_reverse_fate_amulet() => ATTR_REVERSE_FATE_AMULET;

    public static StringName attr_black_star_wedge() => ATTR_BLACK_STAR_WEDGE;

    public static StringName attr_blood_debt_shawl() => ATTR_BLOOD_DEBT_SHAWL;

    public static StringName attr_dead_road_lantern() => ATTR_DEAD_ROAD_LANTERN;

    public static StringName status_reverse_fate_weakened() => STATUS_REVERSE_FATE_WEAKENED;

    public static StringName status_black_star_wedge_exposed() => STATUS_BLACK_STAR_WEDGE_EXPOSED;

    public static StringName path_tag_hidden_trap() => PATH_TAG_HIDDEN_TRAP;

    public static StringName path_tag_black_market() => PATH_TAG_BLACK_MARKET;

    public static StringName path_tag_black_omen() => PATH_TAG_BLACK_OMEN;

    public static StringName path_tag_hidden_path() => PATH_TAG_HIDDEN_PATH;

    public static string battle_flag_reverse_fate_used() => BATTLE_FLAG_REVERSE_FATE_USED;

    public static string battle_flag_black_star_wedge_used() => BATTLE_FLAG_BLACK_STAR_WEDGE_USED;

    public static int reverse_fate_duration_tu() => REVERSE_FATE_DURATION_TU;

    public static double reverse_fate_damage_multiplier() => REVERSE_FATE_DAMAGE_MULTIPLIER;

    public static int black_star_wedge_guard_ignore_flat() => BLACK_STAR_WEDGE_GUARD_IGNORE_FLAT;

    public static int black_star_wedge_exposed_duration_tu() =>
        BLACK_STAR_WEDGE_EXPOSED_DURATION_TU;

    public static double black_star_wedge_exposed_incoming_damage_multiplier() =>
        BLACK_STAR_WEDGE_EXPOSED_INCOMING_DAMAGE_MULTIPLIER;

    public static double blood_debt_low_hp_threshold_ratio() => BLOOD_DEBT_LOW_HP_THRESHOLD_RATIO;

    public static double blood_debt_damage_multiplier() => BLOOD_DEBT_DAMAGE_MULTIPLIER;

    public static double blood_debt_recovery_multiplier() => BLOOD_DEBT_RECOVERY_MULTIPLIER;

    public static int blood_debt_ally_down_ap_gain() => BLOOD_DEBT_ALLY_DOWN_AP_GAIN;

    public static readonly Godot.Collections.Array<StringName> VISIBLE_PATH_TAGS = new()
    {
        PATH_TAG_HIDDEN_TRAP,
        PATH_TAG_BLACK_MARKET,
        PATH_TAG_BLACK_OMEN,
        PATH_TAG_HIDDEN_PATH,
    };

    public static bool snapshot_has_flag(AttributeSnapshot attributeSnapshot, StringName attributeId)
    {
        return attributeSnapshot != null
            && attributeId != ""
            && attributeSnapshot.get_value(attributeId) > 0;
    }

    public static bool unit_has_flag(GodotObject unitState, StringName attributeId)
    {
        if (unitState == null)
            return false;

        if (unitState is BattleUnitState battleUnitState)
        {
            return snapshot_has_flag(battleUnitState.attribute_snapshot, attributeId);
        }

        var snapshot = unitState.Get("attribute_snapshot").AsGodotObject() as AttributeSnapshot;
        return snapshot_has_flag(snapshot, attributeId);
    }

    public static Godot.Collections.Array<StringName> normalize_path_tags(
        Godot.Collections.Array pathTagsValue
    )
    {
        var normalizedTags = new Godot.Collections.Array<StringName>();

        if (pathTagsValue == null)
            return normalizedTags;

        foreach (var pathTagValue in pathTagsValue)
        {
            var pathTag = ProgressionDataUtils.to_string_name(pathTagValue);

            if (pathTag == "" || normalizedTags.Contains(pathTag))
                continue;

            normalizedTags.Add(pathTag);
        }

        return normalizedTags;
    }

    public static bool should_reveal_hidden_path(
        GodotObject attributeSnapshot,
        Godot.Collections.Array pathTagsValue
    )
    {
        if (!snapshot_has_flag(attributeSnapshot as AttributeSnapshot, ATTR_DEAD_ROAD_LANTERN))
            return false;

        foreach (var pathTag in normalize_path_tags(pathTagsValue))
        {
            if (VISIBLE_PATH_TAGS.Contains(pathTag))
                return true;
        }

        return false;
    }

    public static bool member_has_item(
        Godot.Collections.Dictionary itemDefs,
        GodotObject memberState,
        StringName itemId
    )
    {
        if (
            memberState == null
            || itemId == ""
            || memberState.Get("equipment_state").AsGodotObject() == null
        )
            return false;

        var equipmentState = memberState.Get("equipment_state").AsGodotObject() as EquipmentState;

        if (equipmentState == null)
            return false;

        foreach (var slotIdValue in equipmentState.get_entry_slot_ids())
        {
            var equippedItemId = ProgressionDataUtils.to_string_name(
                equipmentState.get_equipped_item_id(ProgressionDataUtils.to_string_name(slotIdValue))
            );

            if (equippedItemId == itemId)
                return true;
        }

        return false;
    }
}
